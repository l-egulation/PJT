using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Combat.Tests
{
    public class SpecialFramePolicyCoverageTests
    {
        static Catalog Combat() => JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("MeleeCatalog").text);
        static MeleeFrameMedia Media(string id) => new MeleeFrameMedia("fixture/attack1", new[] { "prepare", "recover" },
            new AttackMotionTiming("fixture/attack1", new[] { "known-prepare", "known-recover" }, new[] { .25f, .25f }, -1, .25f, -1, 0, 1), false, id + "/Special");
        internal static FrameTimeline IntentionalAuthored(AuthoredSkillDefinition numeric)
        {
            // Explicit test input, not runtime conversion: local body duration
            // covers the original gameplay channels, including the complete spin.
            var values = numeric.ToNumericData(); var execution = numeric.Execution;
            var bodyEnd = Math.Max(2f, execution.End);
            var markers = new List<TimelineMarker> { Point("body-start", 0), Point("activation", values.activation), Point("body-end", bodyEnd) };
            for (int index = 0; index < values.hits.Length; index++)
            {
                string start = "hit-" + index.ToString("D2") + "-start"; markers.Add(Point(start, values.hits[index].time));
                markers.Add(new TimelineMarker("hit-" + index.ToString("D2") + "-end", TimelineAnchor.Marker, start, values.hits[index].active));
            }
            if (execution.Movement?.Type == "dash")
            {
                markers.Add(Point("motion-start", values.movement.start)); markers.Add(new TimelineMarker("motion-end", TimelineAnchor.Marker, "motion-start", values.movement.duration));
            }
            else if (execution.Movement?.Type == "target_teleport_strike") markers.Add(Point("teleport-start", values.movement.start));
            for (int index = 0; index < values.invulStart.Length; index++)
            { markers.Add(Point("invul-" + index.ToString("D2") + "-start", values.invulStart[index])); markers.Add(Point("invul-" + index.ToString("D2") + "-end", values.invulEnd[index])); }
            if (values.verticalFreeze.Length != 0) { markers.Add(Point("freeze-start", values.verticalFreeze[0])); markers.Add(Point("freeze-end", values.verticalFreeze[1])); }
            if (ActionSpecialFrameCompiler.OwnsEffectDuration(numeric))
            {
                markers.Add(new TimelineMarker("effect-start", TimelineAnchor.Marker, "activation", 0));
                markers.Add(new TimelineMarker("effect-end", TimelineAnchor.Marker, "effect-start", values.effect.duration));
            }
            if (execution.Effect?.Type == "parry")
            {
                markers.Add(Point("parry-start", 0)); markers.Add(new TimelineMarker("parry-end", TimelineAnchor.Marker, "parry-start", values.effect.parryWindow));
            }
            return new FrameTimeline(new[] { new TimelineFrame("prepare", 1), new TimelineFrame("recover", bodyEnd - 1) }, markers);
        }
        static TimelineMarker Point(string id, float seconds) => new TimelineMarker(id, TimelineAnchor.Seconds, "", seconds);
        [Test] public void EveryExistingAuthoredPolicyCanCompileAnIntentionalCompleteLocalFrameGraph()
        {
            int count = 0; var combat = Combat();
            foreach (var form in combat.forms) foreach (var skill in form.specials.Where(value => AuthoredSkillDefinition.Supports(value.id)))
            {
                var numeric = new AuthoredSkillDefinition(form.id, skill); var media = Media(skill.id); var timeline = IntentionalAuthored(numeric);
                var source = ActionSpecialFrameCompiler.AuthorAuthored(numeric, media, timeline);
                var result = ActionSpecialFrameCompiler.CompileAuthored(numeric, media, source);
                Assert.That(result.UsesFrameTime, Is.True, numeric.Id); Assert.That(result.Execution.End, Is.EqualTo(Math.Max(2f, numeric.Execution.End)), numeric.Id);
                Assert.That(result.Execution.Activation, Is.EqualTo(numeric.Execution.Activation), numeric.Id); Assert.That(result.Execution.Authored, Is.SameAs(result.Authored));
                Assert.That(result.Execution.Hits.Select(value => value.Anchor), Is.EqualTo(numeric.Execution.Hits.Select(value => value.Anchor)), numeric.Id);
                Assert.That(result.Execution.Hits.Select(value => value.Active), Is.EqualTo(numeric.Execution.Hits.Select(value => value.Active)), numeric.Id);
                Assert.That(result.Execution.Capacity, Is.EqualTo(numeric.Execution.Capacity)); Assert.That(result.Execution.Cooldown, Is.EqualTo(numeric.Execution.Cooldown));
                Assert.That(result.CompileMotion().UsesFrameTime, Is.True); Assert.That(result.CompileMotion().FxStart, Is.EqualTo(-1)); count++;
            }
            Assert.That(count, Is.EqualTo(19));
        }
        [Test] public void EveryFinitePolicyCanCompileMatchingLocalBodyAndRetainItsOriginalSemanticChannels()
        {
            int count = 0; var combat = Combat();
            foreach (var skill in combat.forms.SelectMany(value => value.specials).Where(value => ActionTrackDefinition.Supports(value.id)))
            {
                var numeric = new ActionTrackDefinition(skill); var document = numeric.ToDocument();
                var markers = document.timeline.markers.Select(marker => new TimelineMarker(marker.id, TimelineAnchor.Seconds, "", numeric.Timeline.Times[marker.id])).ToList();
                for (int index = 0; index < markers.Count; index++)
                {
                    var original = numeric.Timeline.Markers.Single(value => value.Id == markers[index].Id);
                    if (original.Anchor == TimelineAnchor.Marker) markers[index] = original;
                }
                var frames = new[] { new TimelineFrame("prepare", numeric.Activation), new TimelineFrame("recover", numeric.BodyEnd - numeric.Activation) };
                var timeline = new FrameTimeline(frames, markers); string form = skill.id == ActionTrackDefinition.GuardId ? "shield" : "base_sword"; var media = Media(skill.id);
                var source = ActionSpecialFrameCompiler.AuthorFinite(numeric, form, media, timeline); var result = ActionSpecialFrameCompiler.CompileFinite(numeric, form, media, source);
                Assert.That(result.Execution.Tracks, Is.SameAs(result.Finite)); Assert.That(result.Finite.SourceTimeMode, Is.EqualTo(ActionTrackSourceTimeMode.FrameTime));
                Assert.That(result.Execution.Hits.Select(value => value.Id), Is.EqualTo(numeric.Execution.Hits.Select(value => value.Id)));
                Assert.That(result.Finite.Execution.Cooldown, Is.EqualTo(numeric.Execution.Cooldown)); Assert.That(result.Finite.Execution.Capacity, Is.EqualTo(numeric.Execution.Capacity)); count++;
            }
            Assert.That(count, Is.EqualTo(4));
        }
        [Test] public void FixedDelayedWorldBodyPreviewUsesThrowReleaseRatherThanIndependentHit()
        {
            var form = Combat().forms.Single(value => value.id == "greatsword"); var numeric = new AuthoredSkillDefinition(form.id, form.specials.Single(value => value.id == "greatsword_follow_up"));
            var media = Media(numeric.Id); var fixedAction = ActionSpecialFrameCompiler.CompileAuthored(numeric, media, ActionSpecialFrameCompiler.FixedAuthored(numeric, media));
            var timing = fixedAction.CompileMotion();
            for (int index = 0; index < 70; index++)
            {
                float age = index * .01f; float source = AttackMotionTiming.MapSourceTime(age, numeric.Execution.Activation, numeric.Execution.End, media.FixedTiming.SourceImpact, media.FixedTiming.SourceDuration);
                Assert.That(fixedAction.BodyFrameAt(age, timing), Is.EqualTo(AttackMotionTiming.SelectFrame(timing.Seconds, timing.SourceDuration, source, false)));
            }
        }
        [Test] public void IntentionalZeroParryAndAfterBodyWorldHitKeepTheirPolicyBoundaries()
        {
            var combat = Combat(); var shield = combat.forms.Single(value => value.id == "shield"); var parry = new AuthoredSkillDefinition(shield.id, shield.specials.Single(value => value.id == "shield_default"));
            var timeline = IntentionalAuthored(parry); var source = ActionSpecialFrameCompiler.AuthorAuthored(parry, Media(parry.Id), timeline);
            source.timeline.markers.Single(value => value.id == "activation").offset = .03125f;
            Assert.Throws<ArgumentException>(() => ActionSpecialFrameCompiler.CompileAuthored(parry, Media(parry.Id), source));
            var great = combat.forms.Single(value => value.id == "greatsword"); var sword = new AuthoredSkillDefinition(great.id, great.specials.Single(value => value.id == "greatsword_follow_up"));
            var markers = new[] { Point("body-start", 0), Point("activation", .25f), Point("body-end", .5f) }
                .Concat(Enumerable.Range(0, sword.Execution.Hits.Count).SelectMany(i => new[]
                {
                    Point($"hit-{i:00}-start", .75f + i * .15f),
                    new TimelineMarker($"hit-{i:00}-end", TimelineAnchor.Marker, $"hit-{i:00}-start", .08f)
                }));
            timeline = new FrameTimeline(new[] { new TimelineFrame("prepare", .25f), new TimelineFrame("recover", .25f) }, markers);
            var compiled = ActionSpecialFrameCompiler.CompileAuthored(sword, Media(sword.Id), ActionSpecialFrameCompiler.AuthorAuthored(sword, Media(sword.Id), timeline));
            Assert.That(compiled.Execution.Hits[0].Time, Is.EqualTo(.75f)); Assert.That(compiled.Execution.End, Is.EqualTo(.5f)); Assert.That(compiled.Execution.Hits[0].Anchor, Is.EqualTo("saved_spawn_world_pose"));
        }
    }
}
