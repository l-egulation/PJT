using System;
using System.Collections.Generic;
using System.Globalization;
using Deviant.Definitions;
using NUnit.Framework;

namespace Deviant.Presentation.Tests
{
    public class AttackMotionTimingTests
    {
        static AttackMotionTiming Timing(float impact = .2f, float end = .4f, float sourceImpact = .15f, float fxStart = .1f)
            => new AttackMotionTiming("fixture-motion", new[] { "pose-a", "pose-b" }, new[] { .1f, .3f }, -1, sourceImpact, fxStart, impact, end);
        [Test] public void SourceFramesAndActionMarkersAreImmutableAndUseOneWarp()
        {
            var frames = new[] { "a", "b" }; var seconds = new[] { .1f, .3f };
            var timing = new AttackMotionTiming("fixture-motion", frames, seconds, -1, .15f, .1f, .2f, .4f); frames[0] = "edited"; seconds[0] = 9;
            Assert.That(timing.Frames[0], Is.EqualTo("a")); Assert.That(timing.Seconds[0], Is.EqualTo(.1f));
            Assert.Throws<NotSupportedException>(() => ((IList<float>)timing.Seconds)[0] = 9);
            Assert.That(timing.SourceTime(0), Is.Zero); Assert.That(timing.SourceTime(.1f), Is.EqualTo(.075f).Within(.000001f));
            Assert.That(timing.SourceTime(.2f), Is.EqualTo(.15f)); Assert.That(timing.SourceTime(.4f), Is.EqualTo(.4f).Within(.000001f));
            Assert.That(timing.FrameAt(-1), Is.Zero); Assert.That(timing.FrameAt(.1f), Is.Zero); Assert.That(timing.FrameAt(.2f), Is.EqualTo(1)); Assert.That(timing.FrameAt(10), Is.EqualTo(1));
        }
        [Test] public void ContactFallbackAndExplicitSourceImpactCompileToTheSameMeaning()
        {
            var frames = new[] { "a", "b", "a" }; var seconds = new[] { .1f, .2f, .1f }; float hit = .1f + .2f;
            var fallback = new AttackMotionTiming("contact", frames, seconds, 2, -1, -1, .2f, .4f);
            var absolute = new AttackMotionTiming("contact", frames, seconds, -1, hit, -1, .2f, .4f);
            Assert.That(fallback.SourceImpact, Is.EqualTo(hit)); Assert.That(fallback.ContentHash, Is.EqualTo(absolute.ContentHash));
            Assert.That(fallback.SourceTime(.2f), Is.EqualTo(hit)); Assert.That(fallback.FrameAt(.2f), Is.EqualTo(2));
            Assert.That(new AttackMotionTiming("zero", frames, seconds, -1, -1, -1, 0, .4f).SourceTime(0), Is.Zero);
        }
        [Test] public void ExistingMinimumRecoveryWarpIsPreservedForShortActionSpans()
        {
            var timing = Timing(.2f, .2005f, .2f);
            Assert.That(timing.SourceTime(.2005f), Is.InRange(.29998f, .30002f));
            Assert.That(Timing(0, .4f, .1f).SourceTime(0), Is.EqualTo(.1f));
        }
        [Test] public void FrameSelectionKeepsExactIntervalsClampingAndLooping()
        {
            var seconds = new[] { .1f, .2f, .1f }; float duration = 0; foreach (float s in seconds) duration += s;
            foreach (var sample in new[] { (-.1f, 0), (0f, 0), (.099f, 0), (.1f, 1), (.299f, 1), (.301f, 2), (1f, 2) })
                Assert.That(AttackMotionTiming.SelectFrame(seconds, duration, sample.Item1, false), Is.EqualTo(sample.Item2));
            Assert.That(AttackMotionTiming.SelectFrame(seconds, duration, -.05f, true), Is.EqualTo(2));
            Assert.That(AttackMotionTiming.SelectFrame(seconds, duration, duration, true), Is.Zero);
        }
        [Test] public void HashIncludesFrameContentAndTimingAcrossCultures()
        {
            var timing = Timing(); var culture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("fr-FR"); Assert.That(Timing().ContentHash, Is.EqualTo(timing.ContentHash));
                foreach (var changed in new[] { Timing(.25f), Timing(end: .5f), Timing(sourceImpact: .2f), Timing(fxStart: .12f),
                    new AttackMotionTiming("fixture-motion", new[] { "different", "pose-b" }, new[] { .1f, .3f }, -1, .15f, .1f, .2f, .4f),
                    new AttackMotionTiming("fixture-motion", new[] { "pose-a", "pose-b" }, new[] { .12f, .28f }, -1, .15f, .1f, .2f, .4f) })
                    Assert.That(changed.ContentHash, Is.Not.EqualTo(timing.ContentHash));
            }
            finally { CultureInfo.CurrentCulture = culture; }
        }
        [TestCase(0f)] [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidFrameDurationCannotBecomeACompiledSnapshot(float value)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AttackMotionTiming("bad", new[] { "a" }, new[] { value }, -1, 0, -1, .2f, .4f));
        }
        [Test] public void InvalidReferencesMarkersOrActionTimesAreRejected()
        {
            Assert.Throws<ArgumentException>(() => new AttackMotionTiming(" ", new[] { "a" }, new[] { 1f }, 0, 0, -1, .2f, .4f));
            Assert.Throws<ArgumentException>(() => new AttackMotionTiming("bad", new[] { "a", "b" }, new[] { 1f }, 0, 0, -1, .2f, .4f));
            Assert.Throws<ArgumentException>(() => new AttackMotionTiming("bad", new[] { " " }, new[] { 1f }, 0, 0, -1, .2f, .4f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AttackMotionTiming("bad", new[] { "a" }, new[] { 1f }, 2, -1, -1, .2f, .4f));
            Assert.Throws<ArgumentOutOfRangeException>(() => Timing(sourceImpact: 9)); Assert.Throws<ArgumentOutOfRangeException>(() => Timing(impact: -.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => Timing(end: 0)); Assert.Throws<ArgumentOutOfRangeException>(() => Timing(.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => Timing(sourceImpact: float.NaN)); Assert.Throws<ArgumentOutOfRangeException>(() => Timing(fxStart: float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => Timing().SourceTime(float.NaN));
        }
    }
}
