using System;
using System.Collections.Generic;
using System.Globalization;
using Deviant.Definitions;
using NUnit.Framework;

namespace Deviant.Presentation.Tests
{
    public class FixedAttackMotionTests
    {
        static AttackMotionDocument Document() => new AttackMotionDocument {
            schemaVersion = 1, id = FixedAttackMotion.FirstSwordId, actionId = FixedAttackMotion.FirstSwordAction, mode = FixedAttackMotion.Mode,
            sourceImpact = .155f, fxStart = .125f,
            steps = new[] {
                new AttackMotionStep { id = "ready", frame = "same", seconds = .06f },
                new AttackMotionStep { id = "windup", frame = "other", seconds = .095f },
                new AttackMotionStep { id = "contact", frame = "same", seconds = .215f }
            }
        };
        [Test] public void SnapshotAndRoundTripDoNotShareMutableDrafts()
        {
            var draft = Document(); var motion = new FixedAttackMotion(draft); string hash = motion.ContentHash;
            draft.steps[0].id = "edited"; draft.steps[0].frame = "edited"; draft.steps[0].seconds = 10; draft.sourceImpact = 10;
            Assert.That(motion.StepIds[0], Is.EqualTo("ready")); Assert.That(motion.Frames[0], Is.EqualTo("same")); Assert.That(motion.Seconds[0], Is.EqualTo(.06f));
            Assert.Throws<NotSupportedException>(() => ((IList<string>)motion.StepIds)[0] = "edited");
            var exported = motion.ToDocument(); Assert.That(new FixedAttackMotion(exported).ContentHash, Is.EqualTo(hash)); exported.steps[0].seconds = 10;
            Assert.That(motion.Seconds[0], Is.EqualTo(.06f));
        }
        [Test] public void FixedCombatContactAndEndKeepTheirTimesWhenFrameWeightsChange()
        {
            var draft = Document(); var original = new FixedAttackMotion(draft); draft.steps[0].seconds = .12f; var changed = new FixedAttackMotion(draft);
            var before = original.EffectiveSeconds(.2f, .4f); var after = changed.EffectiveSeconds(.2f, .4f);
            Assert.That(after[0], Is.GreaterThan(before[0])); Assert.That(after[1], Is.LessThan(before[1]));
            float total = 0; foreach (float value in after) total += value; Assert.That(total, Is.EqualTo(.4f).Within(.000001f));
            foreach (var motion in new[] { original, changed }) {
                var compiled = motion.Compile(.2f, .4f); Assert.That(compiled.SourceTime(.2f), Is.EqualTo(.155f)); Assert.That(compiled.SourceTime(.4f), Is.EqualTo(motion.SourceDuration).Within(.000001f));
            }
            Assert.That(original.Compile(.2f, .4f).FrameAt(.1f), Is.EqualTo(1)); Assert.That(changed.Compile(.2f, .4f).FrameAt(.1f), Is.Zero);
        }
        [Test] public void PreviewIntervalsAgreeWithRuntimeAtNormalFastAndMinimumRecoverySpans()
        {
            var motion = new FixedAttackMotion(Document());
            foreach (var pair in new[] { (.2f, .4f), (.02f, .04f), (.2f, .2005f), (0f, .4f), (.4f, .4f) })
            {
                var compiled = motion.Compile(pair.Item1, pair.Item2); var spans = motion.EffectiveSeconds(pair.Item1, pair.Item2); float time = 0;
                for (int i = 0; i < spans.Count; i++) {
                    if (spans[i] > .000001f) Assert.That(compiled.FrameAt(time + spans[i] * .5f), Is.EqualTo(i), "Preview must sample the same frame as playback.");
                    time += spans[i];
                }
                Assert.That(time, Is.EqualTo(pair.Item2).Within(.000001f));
            }
        }
        [Test] public void HashIncludesStableStepIdentityOrderFramesAndMarkersAcrossCultures()
        {
            var baseline = new FixedAttackMotion(Document()).ContentHash; var oldCulture = CultureInfo.CurrentCulture;
            try {
                CultureInfo.CurrentCulture = new CultureInfo("fr-FR"); Assert.That(new FixedAttackMotion(Document()).ContentHash, Is.EqualTo(baseline));
                foreach (Action<AttackMotionDocument> edit in new Action<AttackMotionDocument>[] { d => d.steps[0].id = "new", d => d.steps[0].frame = "new", d => d.steps[0].seconds = .1f, d => d.sourceImpact = .16f, d => d.fxStart = -1, d => Array.Reverse(d.steps) }) {
                    var draft = Document(); edit(draft); Assert.That(new FixedAttackMotion(draft).ContentHash, Is.Not.EqualTo(baseline));
                }
            }
            finally { CultureInfo.CurrentCulture = oldCulture; }
        }
        [Test] public void UnsupportedModesDuplicateOrMissingStepsAndInvalidTimesFailBeforeCompilation()
        {
            foreach (Action<AttackMotionDocument> edit in new Action<AttackMotionDocument>[] {
                d => d.schemaVersion = 2, d => d.mode = "FrameTime", d => d.id = "other", d => d.actionId = "other", d => d.steps = null,
                d => d.steps[0] = null, d => d.steps[0].id = d.steps[1].id, d => d.steps[0].frame = " ", d => d.steps[0].seconds = 0,
                d => d.steps[0].seconds = float.NaN, d => d.steps[0].seconds = float.PositiveInfinity,
                d => d.sourceImpact = -1, d => d.sourceImpact = 0, d => d.sourceImpact = 10, d => d.fxStart = -.5f, d => d.fxStart = 10, d => d.fxStart = float.NaN
            }) { var draft = Document(); edit(draft); Assert.Catch<ArgumentException>(() => new FixedAttackMotion(draft)); }
            Assert.Throws<ArgumentOutOfRangeException>(() => new FixedAttackMotion(Document()).Compile(.5f, .4f));
        }
    }
}
