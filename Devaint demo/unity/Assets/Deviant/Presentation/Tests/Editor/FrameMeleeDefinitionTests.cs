using System;
using System.Linq;
using Deviant.Definitions;
using NUnit.Framework;

namespace Deviant.Presentation.Tests
{
    public class FrameMeleeDefinitionTests
    {
        static BasicMeleeDefinition Action()
        {
            Assert.That(BasicMeleeDefinition.TryCreate(FixedAttackMotion.FirstSwordAction, new Deviant.Combat.BasicData { damage = 10, impact = .2f, active = .08f, duration = .4f, box = new Deviant.Combat.BoxData { width = 2, height = 2, x = 1, y = 1 } }, out var result, out var error), Is.True, error); return result;
        }
        static FixedAttackMotion Motion(float cue = .125f) => new FixedAttackMotion(new AttackMotionDocument {
            schemaVersion = 1, id = FixedAttackMotion.FirstSwordId, actionId = FixedAttackMotion.FirstSwordAction, mode = FixedAttackMotion.Mode, sourceImpact = .155f, fxStart = cue,
            steps = new[] {
                new AttackMotionStep { id = "ready", frame = "a", seconds = .06f },
                new AttackMotionStep { id = "windup", frame = "b", seconds = .095f },
                new AttackMotionStep { id = "hit", frame = "c", seconds = .035f },
                new AttackMotionStep { id = "follow", frame = "d", seconds = .095f },
                new AttackMotionStep { id = "recover", frame = "a", seconds = .085f }
            }
        });
        static FrameTimeline Edit(FrameTimeline original, float delta)
        {
            var frames = original.Frames.ToArray(); frames[0] = new TimelineFrame(frames[0].Id, frames[0].Seconds + delta); return new FrameTimeline(frames, original.Markers);
        }
        [Test] public void ProjectionPreservesContactActiveEndAndCueOriginThenPublishesOneVersion()
        {
            var action = Action(); var motion = Motion(); var baseline = MeleeFrameCompiler.ProjectFirst(action, motion, 2.5f); var resolved = MeleeFrameCompiler.CompileFirst(action, motion, baseline, 2.5f);
            Assert.That(resolved.Action.Impact, Is.EqualTo(action.Impact).Within(.000001f)); Assert.That(resolved.Action.Active, Is.EqualTo(action.Active).Within(.000001f)); Assert.That(resolved.Action.Duration, Is.EqualTo(action.Duration).Within(.000001f));
            var changed = MeleeFrameCompiler.CompileFirst(action, motion, Edit(baseline, .03f), 2.5f);
            Assert.That(changed.Action.Impact, Is.EqualTo(.23f).Within(.000001f)); Assert.That(changed.Action.Active, Is.EqualTo(.08f).Within(.000001f)); Assert.That(changed.Action.Duration, Is.EqualTo(.43f).Within(.000001f)); Assert.That(changed.ContentHash, Is.Not.EqualTo(resolved.ContentHash));
            Assert.That(changed.Timeline.Times[MeleeFrameDefinition.CueStart], Is.EqualTo(baseline.Times[MeleeFrameDefinition.CueStart] + .03f).Within(.000001f));
            Assert.That(action.Impact, Is.EqualTo(.2f)); Assert.That(motion.Seconds[0], Is.EqualTo(.06f));
        }
        [Test] public void FrameMotionUsesDirectActionTimeAndStartingSpeedWithoutRewarpingBodyFrames()
        {
            var action = Action(); var motion = Motion(); var candidate = MeleeFrameCompiler.CompileFirst(action, motion, Edit(MeleeFrameCompiler.ProjectFirst(action, motion, 2.5f), .03f), 2.5f);
            var timing = candidate.CompileMotion(2);
            Assert.That(timing.UsesFrameTime, Is.True); Assert.That(timing.SourceTime(.1f), Is.EqualTo(.1f)); Assert.That(timing.Impact, Is.EqualTo(.115f).Within(.000001f)); Assert.That(timing.End, Is.EqualTo(.215f).Within(.000001f));
            Assert.That(timing.Seconds[0], Is.EqualTo(candidate.Timeline.Frames[0].Seconds / 2)); Assert.That(timing.CueElapsed(timing.FxStart), Is.Zero.Within(.000001f));
        }
        [Test] public void UneditedConversionPreservesExistingCueMediaClockAndFrameMidpoints()
        {
            var action = Action(); var motion = Motion(); var candidate = MeleeFrameCompiler.CompileFirst(action, motion, MeleeFrameCompiler.ProjectFirst(action, motion, 2.5f), 2.5f);
            foreach (float speed in new[] { 1f, 1.5f, 10f }) {
                var fixedTiming = motion.Compile(action.Impact / speed, action.Duration / speed); var frameTiming = candidate.CompileMotion(speed);
                foreach (float age in new[] { .18f, .2f, .24f, .3f, .38f }) Assert.That(frameTiming.CueElapsed(age / speed), Is.EqualTo(fixedTiming.CueElapsed(age / speed)).Within(.000001f));
                float begin = 0;
                for (int i = 0; i < frameTiming.Seconds.Count; i++) {
                    float middle = begin + frameTiming.Seconds[i] * .5f; Assert.That(frameTiming.FrameAt(middle), Is.EqualTo(fixedTiming.FrameAt(middle))); begin += frameTiming.Seconds[i];
                }
            }
        }
        [Test] public void MissingInvalidActiveCueOrChangedBindingsFailBeforeAnActionCanBeApplied()
        {
            var action = Action(); var motion = Motion(); var timeline = MeleeFrameCompiler.ProjectFirst(action, motion, 2.5f);
            Assert.Catch<ArgumentException>(() => MeleeFrameCompiler.CompileFirst(action, motion, new FrameTimeline(timeline.Frames, timeline.Markers.Where(m => m.Id != MeleeFrameDefinition.HitEnd)), 2.5f));
            foreach (var changed in new[] { new TimelineMarker(MeleeFrameDefinition.HitEnd, TimelineAnchor.Seconds, "", .1f), new TimelineMarker(MeleeFrameDefinition.CueStart, TimelineAnchor.Seconds, "", 1f), new TimelineMarker(FrameTimingProjection.ActionEnd, TimelineAnchor.Seconds, "", .5f) }) {
                var markers = timeline.Markers.Select(m => m.Id == changed.Id ? changed : m); Assert.Catch<ArgumentException>(() => MeleeFrameCompiler.CompileFirst(action, motion, new FrameTimeline(timeline.Frames, markers), 2.5f));
            }
            var frames = timeline.Frames.ToArray(); frames[0] = new TimelineFrame("rebound", frames[0].Seconds); Assert.Catch<ArgumentException>(() => MeleeFrameCompiler.CompileFirst(action, motion, new FrameTimeline(frames, new[] { new TimelineMarker("contact", TimelineAnchor.Seconds, "", .2f) }), 2.5f));
        }
        [Test] public void DisabledCueStaysDisabledAndBadScaledSpansAreRejected()
        {
            var action = Action(); var motion = Motion(-1); var candidate = MeleeFrameCompiler.CompileFirst(action, motion, MeleeFrameCompiler.ProjectFirst(action, motion, 2.5f), 2.5f);
            Assert.That(candidate.CueEnabled, Is.False); Assert.That(candidate.CompileMotion(1).FxStart, Is.EqualTo(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => candidate.CompileMotion(0)); Assert.Throws<ArgumentOutOfRangeException>(() => candidate.CompileMotion(float.NaN));
        }
    }
}
