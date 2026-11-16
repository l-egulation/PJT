using System;
using Deviant.Definitions;
using NUnit.Framework;

namespace Deviant.Presentation.Tests
{
    public sealed class LightingDirectorTests
    {
        static LightingProfile Profile(string id, float intensity) => new LightingProfile(new LightingProfileData { id = id, color = new[] { .5f, .6f, 1f }, intensity = intensity });
        [Test] public void PriorityAndReleaseResolveCurrentRequestsRatherThanRestoringStaleState()
        {
            using (var director = new LightingDirector(Profile("base", .1f)))
            {
                var zone = director.RequestProfile("zone", Profile("zone", .3f), 1, 0);
                var sequence = director.RequestProfile("sequence", Profile("sequence", .9f), 10, 0);
                var other = director.RequestProfile("new zone", Profile("other", .5f), 2, 0);
                Assert.That(director.Current.Intensity, Is.EqualTo(.9f));
                zone.Dispose(); sequence.Dispose(); sequence.Dispose();
                Assert.That(director.Current.Intensity, Is.EqualTo(.5f)); other.Dispose();
                Assert.That(director.Current.Intensity, Is.EqualTo(.1f)); Assert.That(director.RequestCount, Is.Zero);
            }
        }
        [Test] public void TransitionPauseInterruptionAndTieOrderAreStable()
        {
            using (var director = new LightingDirector(Profile("base", 0)))
            {
                var first = director.RequestProfile("first", Profile("first", 1), 1, 1);
                director.Advance(.5f); Assert.That(director.Current.Intensity, Is.EqualTo(.5f));
                director.Advance(0); Assert.That(director.Current.Intensity, Is.EqualTo(.5f));
                var second = director.RequestProfile("second", Profile("second", .7f), 1, 1);
                director.Advance(.5f); Assert.That(director.Current.Intensity, Is.EqualTo(.6f).Within(1e-6));
                second.Dispose(); director.Advance(1); Assert.That(director.Current.Intensity, Is.EqualTo(1));
                first.Dispose(); director.Advance(1); Assert.That(director.Current.Intensity, Is.Zero);
            }
        }
        [Test] public void InvalidRequestsDoNotReplaceTheCurrentDefinitionAndDisposalIsIdempotent()
        {
            var director = new LightingDirector(Profile("base", .2f));
            Assert.Throws<ArgumentOutOfRangeException>(() => director.RequestProfile("bad", Profile("bad", 1), 1, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => director.Advance(float.PositiveInfinity));
            Assert.That(director.RequestCount, Is.Zero); Assert.That(director.Current.Intensity, Is.EqualTo(.2f));
            var lease = director.RequestProfile("owner", Profile("high", 1), 1, 0); director.Dispose(); lease.Dispose(); director.Dispose();
            Assert.Throws<ObjectDisposedException>(() => director.Advance(0));
        }
        [Test] public void DefinitionsCopyMutableInputsAndRejectInvalidColorsAndRanges()
        {
            var data = new LightingProfileData { id = "a", color = new[] { .1f, .2f, .3f }, intensity = .4f };
            var definition = new LightingProfile(data); data.color[0] = 9; data.intensity = 5;
            Assert.That(definition.Color.R, Is.EqualTo(.1f)); Assert.That(definition.Intensity, Is.EqualTo(.4f));
            Assert.Throws<ArgumentException>(() => new LightingProfile(new LightingProfileData { id = "bad", color = new float[2] }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LightColor(float.NaN, 1, 1));
        }
        [Test] public void SeededFlickerIsStatelessBoundedAndIndependentOfSamplingOrder()
        {
            var definition = new LightEmitterDefinition(new LightEmitterData { id = "lamp", color = new[] { 1f, .5f, .2f }, intensity = 2, radius = 5, normalDistance = 2, shadowStrength = .8f, flickerAmplitude = .1f, flickerPeriod = .7f });
            float expected = definition.IntensityAt(.35, 42);
            for (int i = 0; i < 100; i++) Assert.That(definition.IntensityAt(i * .03, 42), Is.InRange(1.8f, 2.2f));
            Assert.That(definition.IntensityAt(.35, 42), Is.EqualTo(expected));
        }
        [Test] public void ShadowSoftnessAndFalloffAreCapturedAndRejectNonFiniteOrOutOfRangeValues()
        {
            var data = new LightEmitterData { id = "lamp", color = new[] { 1f, .5f, .2f }, intensity = 1, radius = 5, normalDistance = 2,
                shadowStrength = .8f, shadowSoftness = .55f, shadowFalloff = .5f, lightFalloff = .6f, flickerPeriod = 1 };
            var definition = new LightEmitterDefinition(data);
            data.shadowSoftness = 0; data.shadowFalloff = 0; data.lightFalloff = 0;
            Assert.That(definition.ShadowSoftness, Is.EqualTo(.55f)); Assert.That(definition.ShadowFalloff, Is.EqualTo(.5f)); Assert.That(definition.LightFalloff, Is.EqualTo(.6f));
            foreach (float invalid in new[] { -.1f, 1.01f, float.NaN, float.PositiveInfinity })
            {
                data.shadowSoftness = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => new LightEmitterDefinition(data)); data.shadowSoftness = 0;
                data.shadowFalloff = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => new LightEmitterDefinition(data)); data.shadowFalloff = 0;
                data.lightFalloff = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => new LightEmitterDefinition(data)); data.lightFalloff = 0;
            }
            Assert.That(new LightEmitterDefinition(data).ShadowSoftness, Is.Zero, "An explicit hard-shadow comparison remains valid.");
        }
        [Test] public void PartialWeightsComposeOverCurrentLowerRequestsWithoutChangingTieOrder()
        {
            using (var director = new LightingDirector(Profile("base", 0)))
            {
                var first = director.RequestProfile("first", Profile("first", 1), 1, 0);
                var second = director.RequestProfile("second", Profile("second", .2f), 1, 0, .5f);
                Assert.That(director.Current.Intensity, Is.EqualTo(.6f).Within(1e-6));
                first.SetWeight(.5f); Assert.That(director.Current.Intensity, Is.EqualTo(.35f).Within(1e-6));
                var over = director.RequestProfile("override", Profile("over", .9f), 10, 0);
                second.SetWeight(1); first.SetWeight(1); Assert.That(director.Current.Intensity, Is.EqualTo(.9f).Within(1e-6));
                over.Dispose(); Assert.That(director.Current.Intensity, Is.EqualTo(.2f).Within(1e-6), "Changing an old lease cannot win a same-priority tie.");
                second.Dispose(); Assert.That(director.Current.Intensity, Is.EqualTo(1));
            }
        }
        [TestCase(.9f), TestCase(1f)] public void HiddenAndRepeatedWeightsDoNotRestartAnUnchangedBlendTarget(float intensity)
        {
            using (var director = new LightingDirector(Profile("base", 0)))
            {
                var lower = director.RequestProfile("lower", Profile("low", .3f), 1, 1);
                var upper = director.RequestProfile("upper", Profile("up", intensity), 10, 1);
                director.Advance(.5f); Assert.That(director.Current.Intensity, Is.EqualTo(intensity * .5f));
                lower.SetWeight(.7f); upper.SetWeight(1);
                var empty = director.RequestProfile("empty", Profile("empty", .1f), 20, 1, 0); empty.Dispose();
                director.Advance(.5f); Assert.That(director.Current.Intensity, Is.EqualTo(intensity));
            }
        }
        [Test] public void InvalidWeightsAndReleasedLeasesCannotMutateOtherRequests()
        {
            using (var director = new LightingDirector(Profile("base", 0)))
            {
                var lease = director.RequestProfile("zone", Profile("zone", 1), 1, 0, .5f);
                foreach (float value in new[] { -.1f, 1.1f, float.NaN, float.PositiveInfinity })
                {
                    Assert.Throws<ArgumentOutOfRangeException>(() => lease.SetWeight(value));
                    Assert.Throws<ArgumentOutOfRangeException>(() => director.RequestProfile("bad", Profile("bad", 1), 1, 0, value));
                    Assert.That(lease.Weight, Is.EqualTo(.5f)); Assert.That(director.RequestCount, Is.EqualTo(1));
                }
                lease.Dispose(); lease.Dispose(); Assert.That(lease.IsActive, Is.False);
                Assert.Throws<ObjectDisposedException>(() => lease.SetWeight(1)); Assert.That(director.RequestCount, Is.Zero);
            }
        }
    }
}
