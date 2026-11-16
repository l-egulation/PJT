using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Combat.Tests
{
    public class ActionSpecialFrameCompilerTests
    {
        static Catalog Combat() => JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("MeleeCatalog").text);
        static AuthoredSkillDefinition Authored(string id)
        {
            var combat = Combat(); var form = combat.forms.Single(value => value.specials.Any(skill => skill.id == id));
            return new AuthoredSkillDefinition(form.id, form.specials.Single(value => value.id == id));
        }
        static ActionTrackDefinition Finite(string id) => new ActionTrackDefinition(Combat().forms.SelectMany(form => form.specials).Single(value => value.id == id));
        static string FiniteForm(string id) => id == ActionTrackDefinition.GuardId ? "shield" : "base_sword";
        static MeleeFrameMedia Media(string id, float first = .25f, float second = .25f)
            => new MeleeFrameMedia("fixture/attack1", new[] { "ready", "finish" }, new AttackMotionTiming("fixture/attack1", new[] { "known-ready", "known-finish" },
                new[] { first, second }, -1, first, -1, 0, 1), false, new MotionActionKey(id, MotionActionKind.Special) + "|fixture/attack1");
        static FrameTimeline Timeline(SpecialFrameSourceDocument source) => new FrameTimeline(source.timeline.frames.Select(frame => new TimelineFrame(frame.id, frame.seconds)),
            source.timeline.markers.Select(marker => new TimelineMarker(marker.id, marker.anchor, marker.reference, marker.offset)));
        [Test] public void EveryFixedSpecialUsesItsOriginalImmutablePayloadAndVersion()
        {
            foreach (string id in AuthoredSkillDefinition.SupportedIds)
            {
                var numeric = Authored(id); var media = Media(id); var source = ActionSpecialFrameCompiler.FixedAuthored(numeric, media);
                var compiled = ActionSpecialFrameCompiler.CompileAuthored(numeric, media, source);
                Assert.That(compiled.Authored, Is.SameAs(numeric)); Assert.That(compiled.Execution, Is.SameAs(numeric.Execution)); Assert.That(compiled.ContentHash, Is.EqualTo(numeric.ContentHash));
                Assert.That(compiled.Timeline, Is.Null); Assert.That(compiled.UsesFrameTime, Is.False);
            }
            foreach (string id in ActionTrackDefinition.SupportedIds)
            {
                var numeric = Finite(id); var media = Media(id); var source = ActionSpecialFrameCompiler.FixedFinite(numeric, FiniteForm(id), media);
                var compiled = ActionSpecialFrameCompiler.CompileFinite(numeric, FiniteForm(id), media, source);
                Assert.That(compiled.Finite, Is.SameAs(numeric)); Assert.That(compiled.Execution, Is.SameAs(numeric.Execution)); Assert.That(compiled.ContentHash, Is.EqualTo(numeric.ContentHash));
            }
        }
        [TestCase("whip_default")] [TestCase("whip_movement")] [TestCase("dagger_movement")] [TestCase("dagger_follow_up")]
        [TestCase("hammer_default")] [TestCase("hammer_follow_up")] [TestCase("shield_movement")]
        public void LocalPreparationEditMovesOwnedGameplayChannelsButNotEventRelativeNumericDurations(string id)
        {
            var numeric = Authored(id); var media = Media(id); var source = ActionSpecialFrameCompiler.ProjectAuthored(numeric, media); var projected = Timeline(source);
            var before = ActionSpecialFrameCompiler.CompileAuthored(numeric, media, source);
            source.timeline.frames[0].seconds += .03125f;
            var after = ActionSpecialFrameCompiler.CompileAuthored(numeric, media, source);
            Assert.That(after.Execution.Activation, Is.GreaterThan(before.Execution.Activation)); Assert.That(after.Execution.End, Is.GreaterThan(before.Execution.End));
            for (int index = 0; index < numeric.Execution.Hits.Count; index++)
            {
                Assert.That(after.Execution.Hits[index].Time, Is.GreaterThan(before.Execution.Hits[index].Time));
                Assert.That(Bits(after.Execution.Hits[index].Active), Is.EqualTo(Bits(before.Execution.Hits[index].Active)));
                Assert.That(Bits(after.Execution.Hits[index].Root), Is.EqualTo(Bits(numeric.Execution.Hits[index].Root)));
                Assert.That(Bits(after.Execution.Hits[index].Stun), Is.EqualTo(Bits(numeric.Execution.Hits[index].Stun)));
                Assert.That(Bits(after.Execution.Hits[index].PushSeconds), Is.EqualTo(Bits(numeric.Execution.Hits[index].PushSeconds)));
            }
            if (numeric.Execution.Movement != null)
            {
                Assert.That(Bits(after.Execution.Movement.RecastWindow), Is.EqualTo(Bits(numeric.Execution.Movement.RecastWindow)));
                Assert.That(Bits(after.Execution.Movement.WallRecovery), Is.EqualTo(Bits(numeric.Execution.Movement.WallRecovery)));
                Assert.That(Bits(after.Execution.Movement.Speed), Is.EqualTo(Bits(numeric.Execution.Movement.Speed)));
            }
            Assert.That(after.CompileMotion().UsesFrameTime, Is.True); Assert.That(after.CompileMotion().FxStart, Is.EqualTo(-1));
            // Rounded positive frame spans and the compensated body-end marker
            // have distinct representations; gameplay keeps the original marker.
            Assert.That(Bits(projected.Times[ActionSpecialFrameDefinition.BodyEnd]), Is.EqualTo(Bits(numeric.Execution.End)));
            Assert.That(Bits(before.Execution.End), Is.EqualTo(Bits(numeric.Execution.End)));
            Assert.That(Bits(before.CompileMotion().End), Is.EqualTo(Bits(numeric.Execution.End)));
            Assert.That(Bits(before.CompileMotion().SourceDuration), Is.EqualTo(Bits(projected.Duration)));
        }
        [TestCase(ActionTrackDefinition.MovementId)] [TestCase(ActionTrackDefinition.FollowUpId)]
        [TestCase(ActionTrackDefinition.WaveId)] [TestCase(ActionTrackDefinition.GuardId)]
        public void FiniteLocalTimelineDrivesExistingCompleteGraphAndKeepsCoupledIntervals(string id)
        {
            var numeric = Finite(id); var media = Media(id); var source = ActionSpecialFrameCompiler.ProjectFinite(numeric, FiniteForm(id), media);
            var before = ActionSpecialFrameCompiler.CompileFinite(numeric, FiniteForm(id), media, source);
            source.timeline.frames[0].seconds += .03125f; var after = ActionSpecialFrameCompiler.CompileFinite(numeric, FiniteForm(id), media, source);
            Assert.That(after.Finite.SourceTimeMode, Is.EqualTo(ActionTrackSourceTimeMode.FrameTime)); Assert.That(after.Finite.Activation, Is.GreaterThan(before.Finite.Activation));
            Assert.That(after.Execution.UsesCompiledTracks, Is.True); Assert.That(after.Finite.BodyEnd, Is.GreaterThan(before.Finite.BodyEnd));
            if (after.Finite.Motion != null) Assert.That(after.Finite.Motion.Duration, Is.EqualTo(after.Finite.Hits[0].Duration));
            if (after.Finite.Effect != null) Assert.That(after.Finite.Effect.Duration, Is.EqualTo(numeric.Effect.Duration));
            if (after.Finite.Defense != null) Assert.That(after.Finite.Defense.Duration, Is.EqualTo(numeric.Defense.Duration));
            Assert.That(after.Finite.Execution.Effect?.Speed, Is.EqualTo(numeric.Execution.Effect?.Speed));
            Assert.That(after.Finite.Execution.Effect?.Distance, Is.EqualTo(numeric.Execution.Effect?.Distance));
            Assert.That(after.CompileMotion().UsesFrameTime, Is.True);
        }
        [TestCase("shield_default")] [TestCase("greatsword_follow_up")]
        public void CollapsedProjectionRejectsWithoutInventedSpansButIntentionalValidLocalAuthoringWorks(string id)
        {
            var numeric = Authored(id); var media = Media(id); string unchanged = numeric.ContentHash;
            Assert.Throws<ArgumentException>(() => ActionSpecialFrameCompiler.ProjectAuthored(numeric, media)); Assert.That(numeric.ContentHash, Is.EqualTo(unchanged));
            var frames = new[] { new TimelineFrame("ready", .25f), new TimelineFrame("finish", .25f) };
            var markers = new List<TimelineMarker> { new TimelineMarker("body-start", TimelineAnchor.Seconds, "", 0),
                new TimelineMarker("activation", TimelineAnchor.Seconds, "", numeric.Execution.Activation), new TimelineMarker("body-end", TimelineAnchor.StepEnd, "finish", 0) };
            if (id == "shield_default") { markers.Add(new TimelineMarker("parry-start", TimelineAnchor.Seconds, "", 0)); markers.Add(new TimelineMarker("parry-end", TimelineAnchor.Marker, "parry-start", .125f)); }
            else for (int i = 0; i < numeric.Execution.Hits.Count; i++)
            {
                string hit = $"hit-{i:00}";
                markers.Add(new TimelineMarker(hit + "-start", TimelineAnchor.Marker, "body-end", .25f + i * .15f));
                markers.Add(new TimelineMarker(hit + "-end", TimelineAnchor.Marker, hit + "-start", .08f));
            }
            var source = ActionSpecialFrameCompiler.AuthorAuthored(numeric, media, new FrameTimeline(frames, markers));
            var compiled = ActionSpecialFrameCompiler.CompileAuthored(numeric, media, source);
            if (id == "shield_default") { Assert.That(compiled.Execution.Activation, Is.Zero); Assert.That(compiled.Execution.Effect.ParryWindow, Is.EqualTo(.125f)); }
            else Assert.That(compiled.Execution.Hits[0].Time, Is.GreaterThan(compiled.Execution.End));
            Assert.That(compiled.CompileMotion().UsesFrameTime, Is.True);
        }
        [TestCase("dagger_default")] [TestCase("dagger_range")] [TestCase("shield_follow_up")]
        public void ExplicitEffectDurationRemainsRelativeToExistingActivationEpoch(string id)
        {
            var numeric = Authored(id); var media = Media(id); var source = ActionSpecialFrameCompiler.ProjectAuthored(numeric, media);
            var before = ActionSpecialFrameCompiler.CompileAuthored(numeric, media, source); source.timeline.frames[0].seconds += .03125f;
            var after = ActionSpecialFrameCompiler.CompileAuthored(numeric, media, source);
            Assert.That(after.Execution.Activation, Is.GreaterThan(before.Execution.Activation));
            Assert.That(Bits(after.Execution.Effect.Duration), Is.EqualTo(Bits(before.Execution.Effect.Duration)));
            var end = source.timeline.markers.Single(value => value.id == "effect-end"); end.offset += .25f;
            var extended = ActionSpecialFrameCompiler.CompileAuthored(numeric, media, source);
            Assert.That(extended.Execution.Effect.Duration, Is.EqualTo(before.Execution.Effect.Duration + .25f));
        }
        [Test] public void StableMediaIdentityRejectsChangedStepOrPolicyButNotUnrelatedFixedWeightEdits()
        {
            var numeric = Authored("whip_default"); var media = Media(numeric.Id); var source = ActionSpecialFrameCompiler.ProjectAuthored(numeric, media);
            var changedWeights = Media(numeric.Id, .125f, .375f);
            Assert.That(changedWeights.BindingVersion, Is.EqualTo(media.BindingVersion));
            var first = ActionSpecialFrameCompiler.CompileAuthored(numeric, media, source); var second = ActionSpecialFrameCompiler.CompileAuthored(numeric, changedWeights, source);
            Assert.That(first.SourceHash, Is.EqualTo(second.SourceHash)); Assert.That(first.ContentHash, Is.EqualTo(second.ContentHash));
            var reordered = new MeleeFrameMedia(media.MotionId, new[] { "finish", "ready" }, media.FixedTiming, false, "changed binding");
            Assert.Throws<ArgumentException>(() => ActionSpecialFrameCompiler.CompileAuthored(numeric, reordered, source));
            source.timeline.frames[0].id = "unknown"; Assert.Throws<ArgumentException>(() => ActionSpecialFrameCompiler.CompileAuthored(numeric, media, source));
        }
        [Test] public void WholePolicyGraphRejectsMissingDuplicateCyclesUnsupportedIntervalsAndHiddenFixedTimeline()
        {
            var numeric = Authored("rapier_follow_up"); var media = Media(numeric.Id);
            Assert.Throws<ArgumentException>(() => ActionSpecialFrameCompiler.ProjectAuthored(numeric, media), "Automatic projection must still reject changed committed floats.");
            var intentional = SpecialFramePolicyCoverageTests.IntentionalAuthored(numeric);
            var timeline = new FrameTimeline(media.StepIds.Select((id, index) => new TimelineFrame(id, intentional.Frames[index].Seconds)), intentional.Markers);
            var source = ActionSpecialFrameCompiler.AuthorAuthored(numeric, media, timeline);
            Assert.DoesNotThrow(() => ActionSpecialFrameCompiler.CompileAuthored(numeric, media, source));
            var invalid = Copy(source); invalid.timeline.markers = invalid.timeline.markers.Skip(1).ToArray(); Assert.Throws<ArgumentException>(() => ActionSpecialFrameCompiler.CompileAuthored(numeric, media, invalid));
            invalid = Copy(source); invalid.timeline.markers.First(value => value.id == "hit-00-end").offset = 0; Assert.Throws<ArgumentException>(() => ActionSpecialFrameCompiler.CompileAuthored(numeric, media, invalid));
            invalid = Copy(source); invalid.timeline.frames[0].seconds = 0; Assert.Catch<ArgumentException>(() => ActionSpecialFrameCompiler.CompileAuthored(numeric, media, invalid));
            invalid = Copy(source); invalid.mode = MeleeTimingMode.CombatTimeFixed; Assert.Throws<ArgumentException>(() => ActionSpecialFrameCompiler.CompileAuthored(numeric, media, invalid));
            invalid = Copy(source); invalid.timeline.markers[0].anchor = TimelineAnchor.Marker; invalid.timeline.markers[0].reference = invalid.timeline.markers[0].id; Assert.Throws<ArgumentException>(() => ActionSpecialFrameCompiler.CompileAuthored(numeric, media, invalid));
        }
        static SpecialFrameSourceDocument Copy(SpecialFrameSourceDocument source) => JsonUtility.FromJson<SpecialFrameSourceDocument>(JsonUtility.ToJson(source));
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
    }
}
