using System;
using System.Linq;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Combat.Tests
{
    public class MeleeFrameCompilerTests
    {
        static BasicMeleeDefinition Basic(string id = "base_sword_basic_1")
        {
            Assert.That(BasicMeleeDefinition.TryCreate(id, new BasicData { damage = 12, duration = .6f, impact = .2f, active = .08f, box = new BoxData { width = 2, height = 2, x = 1, y = 1 } }, out var value, out var error), Is.True, error); return value;
        }
        static MeleeFrameMedia Media(float ready = .1f, string last = "return", bool cue = true)
            => new MeleeFrameMedia("fixture/attack", new[] { "ready", "contact", last }, new AttackMotionTiming("fixture/attack", new[] { "fixture.a", "fixture.b", "fixture.a" }, new[] { ready, .1f, .2f }, -1, ready, .05f, .2f, .6f), cue, "base_sword_basic_1/basic/fixture-attack");
        [Test] public void FixedModeReusesExactExistingBasicCompilerAndBodyWeightVersion()
        {
            var basic = Basic(); var media = Media(); var source = MeleeFrameCompiler.FixedBasic(basic, "base_sword", 2.5f, media);
            var compiled = MeleeFrameCompiler.CompileBasic(basic, "base_sword", 2.5f, media, source);
            Assert.That(compiled.Action, Is.SameAs(basic)); Assert.That(compiled.Timeline, Is.Null);
            Assert.That(compiled.CompileForStart(1.3f).ContentHash, Is.EqualTo(MeleeActionDefinition.ForBasic(basic, 1.3f, 2.5f).ContentHash));
            var changed = Media(.2f); var replacement = MeleeFrameCompiler.CompileBasic(basic, "base_sword", 2.5f, changed, source);
            Assert.That(changed.BindingVersion, Is.EqualTo(media.BindingVersion)); Assert.That(replacement.ContentHash, Is.Not.EqualTo(compiled.ContentHash));
        }
        [Test] public void ExplicitProjectionAndReadyEditMoveGameplayMarkersAndScaleBasicOnce()
        {
            var basic = Basic(); var media = Media(); var source = MeleeFrameCompiler.ProjectBasic(basic, "base_sword", 2.5f, media);
            var initial = MeleeFrameCompiler.CompileBasic(basic, "base_sword", 2.5f, media, source);
            Assert.That(initial.Action.Impact, Is.EqualTo(basic.Impact).Within(.000001f)); Assert.That(initial.Action.Active, Is.EqualTo(basic.Active)); Assert.That(initial.Action.Duration, Is.EqualTo(basic.Duration).Within(.000001f));
            source.timeline.frames[0].seconds += .03f;
            var next = MeleeFrameCompiler.CompileBasic(basic, "base_sword", 2.5f, media, source); var fast = next.CompileForStart(1.5f);
            Assert.That(next.Action.Impact, Is.EqualTo(.23f).Within(.000001f)); Assert.That(next.Action.Duration, Is.EqualTo(.63f).Within(.000001f)); Assert.That(next.Action.Active, Is.EqualTo(.08f));
            Assert.That(fast.Impact, Is.EqualTo(next.Action.Impact / 1.5f)); Assert.That(fast.End, Is.EqualTo(next.Action.Duration / 1.5f));
            Assert.That(fast.AdvanceStart, Is.EqualTo(next.Timeline.Times[MeleeFrameDefinition.AdvanceStart] / 1.5f));
            Assert.That(fast.AdvanceDuration, Is.EqualTo(.08f / 1.5f)); Assert.That(next.AdvanceDistance, Is.EqualTo(.65f));
        }
        [Test] public void SharedFixedWeightEditsCannotRetargetAnActiveActionOverrideOrItsCueMapping()
        {
            var basic = Basic(); var media = Media(); var source = MeleeFrameCompiler.ProjectBasic(basic, "base_sword", 2.5f, media);
            var initial = MeleeFrameCompiler.CompileBasic(basic, "base_sword", 2.5f, media, source);
            var changed = MeleeFrameCompiler.CompileBasic(basic, "base_sword", 2.5f, Media(.25f), source);
            Assert.That(changed.ContentHash, Is.EqualTo(initial.ContentHash)); Assert.That(changed.Action.ContentHash, Is.EqualTo(initial.Action.ContentHash));
            Assert.That(changed.CueMapping.ContentHash, Is.EqualTo(initial.CueMapping.ContentHash)); Assert.That(changed.CueMapping.SourceImpact, Is.EqualTo(.1f));
            Assert.Throws<ArgumentException>(() => MeleeFrameCompiler.CompileBasic(basic, "base_sword", 2.5f, Media(last: "renamed"), source));
        }
        [Test] public void DirectFamilyKeepsPolicyResourcesAndNeverTakesBasicSpeedOrCue()
        {
            var catalog = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("MeleeCatalog").text);
            var sourceSkill = catalog.forms.Single(form => form.id == "base_sword").specials.Single(skill => skill.id == "base_sword_default");
            Assert.That(DirectMeleeSpecialDefinition.TryCreate("base_sword", sourceSkill, out var direct, out var error), Is.True, error);
            var media = Media(cue: false); var source = MeleeFrameCompiler.ProjectDirect(direct, media); source.timeline.frames[0].seconds += .03f;
            var compiled = MeleeFrameCompiler.CompileDirect(direct, media, source);
            Assert.That(compiled.Direct.Cooldown, Is.EqualTo(direct.Cooldown)); Assert.That(compiled.Direct.Capacity, Is.EqualTo(direct.Capacity)); Assert.That(compiled.Direct.SourceVersion, Is.EqualTo(direct.SourceVersion));
            Assert.That(compiled.CompileForStart(9).Speed, Is.EqualTo(1)); Assert.That(compiled.AdvanceDistance, Is.Zero); Assert.That(compiled.CueMapping, Is.Null);
            Assert.That(compiled.CompileForStart(9).Impact, Is.EqualTo(compiled.Action.Impact));
        }
        [Test] public void InvalidModeReferencesAndIntervalsRejectTheWholeCandidate()
        {
            var basic = Basic(); var media = Media(); var source = MeleeFrameCompiler.ProjectBasic(basic, "base_sword", 2.5f, media);
            foreach (Action<MeleeFrameSourceDocument> mutate in new Action<MeleeFrameSourceDocument>[] {
                value => value.policyVersion = "stale", value => value.mediaBindingVersion = "stale", value => value.motionId = "missing",
                value => value.timeline.frames[0].id = "missing", value => value.timeline.markers = value.timeline.markers.Where(marker => marker.id != MeleeFrameDefinition.HitEnd).ToArray(),
                value => value.timeline.markers.Single(marker => marker.id == MeleeFrameDefinition.HitEnd).offset = -1,
                value => value.timeline.markers.Single(marker => marker.id == MeleeFrameDefinition.AdvanceEnd).reference = "cycle",
                value => value.cueMapping.sourceDuration = 0
            })
            {
                var document = MeleeFrameCompiler.CompileBasic(basic, "base_sword", 2.5f, media, source).ToDocument(); mutate(document);
                Assert.Catch<ArgumentException>(() => MeleeFrameCompiler.CompileBasic(basic, "base_sword", 2.5f, media, document));
            }
            var hidden = MeleeFrameCompiler.FixedBasic(basic, "base_sword", 2.5f, media); hidden.timeline = source.timeline;
            Assert.Throws<ArgumentException>(() => MeleeFrameCompiler.CompileBasic(basic, "base_sword", 2.5f, media, hidden));
        }
        [Test] public void ZeroSourceContactConversionPreservesExistingInitialHoldWithoutInventingFrameIds()
        {
            var basic = Basic("rapier_basic_1"); var fixedTiming = new AttackMotionTiming("rapier/attack2", new[] { "a", "b" }, new[] { .045f, .04f }, -1, 0, 0, basic.Impact, basic.Duration);
            var media = new MeleeFrameMedia("rapier/attack2", new[] { "a", "b" }, fixedTiming, true, "rapier/basic1");
            var source = MeleeFrameCompiler.ProjectBasic(basic, "rapier", 2.5f, media);
            var compiled = MeleeFrameCompiler.CompileBasic(basic, "rapier", 2.5f, media, source); var body = compiled.CompileMotion(1);
            Assert.That(source.timeline.frames.Select(frame => frame.id), Is.EqualTo(new[] { "a", "b" }));
            Assert.That(source.timeline.frames[0].seconds, Is.GreaterThan(basic.Impact));
            foreach (float time in new[] { 0f, .05f, .19f, .2f, .25f, .5f, .59f }) Assert.That(body.FrameAt(time), Is.EqualTo(fixedTiming.FrameAt(time)));
            Assert.That(compiled.Action.Impact, Is.EqualTo(basic.Impact)); Assert.That(compiled.Action.Duration, Is.EqualTo(basic.Duration).Within(.000001f));
        }
        [Test] public void BasicCompilerCannotExpandToOtherActionFamilies()
        {
            Assert.Throws<ArgumentException>(() => MeleeFrameCompiler.FixedBasic(Basic("shuriken_basic_0"), "shuriken", 5, Media()));
            Assert.Throws<ArgumentException>(() => MeleeFrameCompiler.FixedBasic(Basic("base_sword_basic_9"), "base_sword", 2.5f, Media()));
            Assert.Throws<ArgumentException>(() => MeleeFrameCompiler.FixedBasic(Basic(), "base_sword", -1, Media()));
        }
    }
}
