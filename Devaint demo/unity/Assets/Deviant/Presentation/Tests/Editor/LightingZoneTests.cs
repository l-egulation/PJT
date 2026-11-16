using System;
using Deviant.Definitions;
using NUnit.Framework;

namespace Deviant.Presentation.Tests
{
    public sealed class LightingZoneTests
    {
        static LightingProfile Profile(string id, float intensity) => new LightingProfile(new LightingProfileData { id = id, color = new[] { 1f, 1f, 1f }, intensity = intensity });
        static LightingZoneData Data(float transition = 0) => new LightingZoneData {
            id = "room", profile = "warm", bounds = new[] { 0f, 0f, 10f, 8f }, priority = 1, blendWidth = 2, exitHysteresis = .5f, transitionSeconds = transition };
        [Test] public void InwardDistanceSmoothlyBlendsWhileTheOuterHaloRetainsOneLease()
        {
            using (var director = new LightingDirector(Profile("base", 0)))
            using (var zone = new LightingZoneTracker(director, new LightingZoneDefinition(Data()), Profile("warm", 1)))
            {
                zone.Sample(-1, 4); Assert.That(zone.IsActive, Is.False);
                zone.Sample(0, 4); Assert.That(director.RequestCount, Is.Zero);
                zone.Sample(1, 4); Assert.That(zone.Weight, Is.EqualTo(.5f)); Assert.That(director.Current.Intensity, Is.EqualTo(.5f));
                zone.Sample(2, 4); Assert.That(zone.Weight, Is.EqualTo(1));
                zone.Sample(9, 4); Assert.That(zone.Weight, Is.EqualTo(.5f));
                zone.Sample(10.2f, 4); Assert.That(zone.IsActive, Is.True); Assert.That(zone.Weight, Is.Zero);
                Assert.That(zone.EntryCount, Is.EqualTo(1)); Assert.That(director.RequestCount, Is.EqualTo(1));
                zone.Sample(10.5f, 4); Assert.That(zone.IsActive, Is.False); Assert.That(director.RequestCount, Is.Zero);
            }
        }
        [Test] public void BoundaryOscillationDoesNotReacquireOrReorderTheZoneLease()
        {
            using (var director = new LightingDirector(Profile("base", 0)))
            using (var zone = new LightingZoneTracker(director, new LightingZoneDefinition(Data()), Profile("warm", 1)))
            {
                zone.Sample(.01f, 4);
                for (int i = 0; i < 100; i++) zone.Sample(i % 2 == 0 ? -.01f : .01f, 4);
                Assert.That(zone.EntryCount, Is.EqualTo(1)); Assert.That(director.RequestCount, Is.EqualTo(1));
                zone.Sample(-.5f, 4); Assert.That(director.RequestCount, Is.Zero);
                zone.Sample(.01f, 4); Assert.That(zone.EntryCount, Is.EqualTo(2));
            }
        }
        [Test] public void EndingAnOverrideResolvesTheCurrentZoneNotTheCapturedEntryProfile()
        {
            using (var director = new LightingDirector(Profile("base", .1f)))
            using (var zone = new LightingZoneTracker(director, new LightingZoneDefinition(Data()), Profile("warm", .5f)))
            {
                zone.Sample(2, 4); var over = director.RequestProfile("sequence", Profile("over", .9f), 10, 0);
                zone.Sample(1, 4); Assert.That(director.Current.Intensity, Is.EqualTo(.9f).Within(1e-6));
                over.Dispose(); Assert.That(director.Current.Intensity, Is.EqualTo(.3f).Within(1e-6));
                over = director.RequestProfile("sequence", Profile("over", .9f), 10, 0);
                zone.Sample(-1, 4); over.Dispose(); Assert.That(director.Current.Intensity, Is.EqualTo(.1f).Within(1e-6));
            }
        }
        [Test] public void StableSamplesAreTimePartitionInvariantAndPauseDoesNotAdvanceTheBlend()
        {
            using (var a = new LightingDirector(Profile("base", 0)))
            using (var b = new LightingDirector(Profile("base", 0)))
            using (var za = new LightingZoneTracker(a, new LightingZoneDefinition(Data(1)), Profile("warm", 1)))
            using (var zb = new LightingZoneTracker(b, new LightingZoneDefinition(Data(1)), Profile("warm", 1)))
            {
                za.Sample(2, 4); a.Advance(.5f);
                for (int i = 0; i < 10; i++) { zb.Sample(2, 4); b.Advance(.05f); }
                Assert.That(b.Current.Intensity, Is.EqualTo(a.Current.Intensity).Within(1e-6));
                for (int i = 0; i < 100; i++) { zb.Sample(2, 4); b.Advance(0); }
                Assert.That(b.Current.Intensity, Is.EqualTo(.5f).Within(1e-6));
                b.Advance(.5f); Assert.That(b.Current.Intensity, Is.EqualTo(1));
            }
        }
        [Test] public void ResetAndDisposeReleaseOnlyTheirOwnRequestsAndClearEntryState()
        {
            using (var director = new LightingDirector(Profile("base", 0)))
            {
                var other = director.RequestProfile("other", Profile("other", .8f), 10, 0);
                var zone = new LightingZoneTracker(director, new LightingZoneDefinition(Data()), Profile("warm", 1));
                zone.Sample(2, 4); zone.Reset(); Assert.That(zone.EntryCount, Is.Zero); Assert.That(director.RequestCount, Is.EqualTo(1));
                zone.Sample(2, 4); Assert.That(zone.EntryCount, Is.EqualTo(1));
                zone.Dispose(); zone.Dispose(); Assert.That(director.RequestCount, Is.EqualTo(1)); Assert.That(other.IsActive, Is.True);
                Assert.Throws<ObjectDisposedException>(() => zone.Sample(2, 4)); Assert.Throws<ObjectDisposedException>(() => zone.Reset());
                director.Dispose(); other.Dispose(); Assert.That(other.IsActive, Is.False);
            }
        }
        [Test] public void DefinitionsCaptureBoundsAndInvalidSamplingPreservesTheValidState()
        {
            var data = Data(); var definition = new LightingZoneDefinition(data); data.bounds[0] = -100; data.blendWidth = 0;
            Assert.That(definition.Left, Is.Zero); Assert.That(definition.BlendWidth, Is.EqualTo(2));
            using (var director = new LightingDirector(Profile("base", 0)))
            using (var zone = new LightingZoneTracker(director, definition, Profile("warm", 1)))
            {
                zone.Sample(1, 4);
                Assert.Throws<ArgumentOutOfRangeException>(() => zone.Sample(float.NaN, 4));
                Assert.Throws<ArgumentOutOfRangeException>(() => zone.Sample(1, float.PositiveInfinity));
                Assert.That(zone.Weight, Is.EqualTo(.5f)); Assert.That(zone.EntryCount, Is.EqualTo(1));
            }
        }
        [Test] public void InvalidBoundsBlendWidthsAndBindingsAreRejected()
        {
            var data = Data(); data.bounds = new float[3]; Assert.Throws<ArgumentException>(() => new LightingZoneDefinition(data));
            data = Data(); data.bounds[2] = 0; Assert.Throws<ArgumentException>(() => new LightingZoneDefinition(data));
            data = Data(); data.bounds[0] = float.NaN; Assert.Throws<ArgumentOutOfRangeException>(() => new LightingZoneDefinition(data));
            data = Data(); data.blendWidth = 5; Assert.Throws<ArgumentException>(() => new LightingZoneDefinition(data));
            data = Data(); data.blendWidth = 0; Assert.Throws<ArgumentOutOfRangeException>(() => new LightingZoneDefinition(data));
            data = Data(); data.exitHysteresis = -1; Assert.Throws<ArgumentOutOfRangeException>(() => new LightingZoneDefinition(data));
            data = Data(); data.transitionSeconds = float.NaN; Assert.Throws<ArgumentOutOfRangeException>(() => new LightingZoneDefinition(data));
            using (var director = new LightingDirector(Profile("base", 0)))
                Assert.Throws<ArgumentException>(() => new LightingZoneTracker(director, new LightingZoneDefinition(Data()), Profile("wrong", 1)));
        }
    }
}
