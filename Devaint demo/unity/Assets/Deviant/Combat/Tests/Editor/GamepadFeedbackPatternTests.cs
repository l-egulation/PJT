using System;
using Deviant.Input.Contracts;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Combat.Tests
{
    public sealed class GamepadFeedbackPatternTests
    {
        static GamepadFeedbackProfile.Segment Segment(float low, float high, float duration, string curve = "decay")
            => new GamepadFeedbackProfile.Segment { low = low, high = high, duration = duration, curve = curve };
        static GamepadFeedbackProfile.Pulse Pattern(params GamepadFeedbackProfile.Segment[] segments)
            => new GamepadFeedbackProfile.Pulse { segments = segments };
        static void Levels(GamepadFeedbackProfile.Pulse pattern, double at, float low, float high)
        {
            var result = pattern.Sample(at);
            Assert.That(result.x, Is.EqualTo(low).Within(.000001));
            Assert.That(result.y, Is.EqualTo(high).Within(.000001));
        }
        [Test] public void LegacyPulseRetainsItsLinearDecayAndExactLifetime()
        {
            var pulse = new GamepadFeedbackProfile.Pulse { low = .6f, high = .2f, duration = .2f };
            pulse.Validate(1);
            Assert.That(pulse.Duration, Is.EqualTo(.2f));
            Levels(pulse, -.01, 0, 0); Levels(pulse, 0, .6f, .2f); Levels(pulse, .1, .3f, .1f);
            Levels(pulse, pulse.Duration, 0, 0); Levels(pulse, 2, 0, 0);
        }
        [Test] public void SegmentTimelineSamplesHoldRestRiseAndDecayOnTheSameClock()
        {
            var pattern = Pattern(Segment(.8f, 0, .04f, "hold"), Segment(0, 0, .03f, "hold"),
                Segment(0, .6f, .08f, "rise"), Segment(.4f, .2f, .04f));
            pattern.Validate(2);
            Assert.That(pattern.Duration, Is.EqualTo(.19f).Within(.000001));
            Levels(pattern, .02, .8f, 0); Levels(pattern, .055, 0, 0);
            Levels(pattern, .11, 0, .3f); Levels(pattern, .17, .2f, .1f);
            Levels(pattern, -.1, 0, 0); Levels(pattern, pattern.Duration + .001, 0, 0);
        }
        [Test] public void RestIsNotACompletionAndTheNextBeatStillPlays()
        {
            var pattern = Pattern(Segment(.7f, .2f, .03f, "hold"), Segment(0, 0, .04f, "hold"), Segment(.4f, .6f, .05f, "hold"));
            Levels(pattern, .01, .7f, .2f); Levels(pattern, .05, 0, 0); Levels(pattern, .09, .4f, .6f);
            Assert.That(pattern.Duration, Is.GreaterThan(.09));
        }
        [Test] public void SchemaTwoAcceptsMixedLegacyAndPatternEventsAndSchemaOneRejectsSegments()
        {
            var profile = GamepadFeedbackProfile.Load();
            profile.schemaVersion = 2;
            profile.hit = Pattern(Segment(.5f, .7f, .04f), Segment(0, 0, .025f, "hold"), Segment(.2f, .3f, .05f));
            Assert.DoesNotThrow(profile.Validate);
            profile.schemaVersion = 1;
            Assert.Throws<ArgumentException>(profile.Validate);
        }
        [Test] public void InvalidSegmentFieldsAndEmptyOrOversizedPatternsAreRejected()
        {
            foreach (var segment in new[] { Segment(-.1f, 0, .1f), Segment(0, float.NaN, .1f), Segment(0, 0, 0),
                Segment(0, 0, float.PositiveInfinity), Segment(0, 0, 1.1f), Segment(1, 1, .1f, "unknown") })
            {
                var pattern = Pattern(segment);
                Assert.Throws<ArgumentException>(() => pattern.Validate(2));
            }
            var empty = Pattern(Array.Empty<GamepadFeedbackProfile.Segment>());
            Assert.Throws<ArgumentException>(() => empty.Validate(2));
            var many = new GamepadFeedbackProfile.Segment[17];
            for (int i = 0; i < many.Length; i++) many[i] = Segment(.1f, .1f, .01f);
            var oversized = Pattern(many);
            Assert.Throws<ArgumentException>(() => oversized.Validate(2));
        }
        [Test] public void TotalDurationIncludesRestsAndHasATwoSecondLimit()
        {
            var exact = Pattern(Segment(1, 1, 1, "hold"), Segment(0, 0, 1, "hold"));
            Assert.DoesNotThrow(() => exact.Validate(2));
            Assert.That(exact.Duration, Is.EqualTo(2));
            var tooLong = Pattern(Segment(.5f, .5f, .8f), Segment(0, 0, .8f, "hold"), Segment(.5f, .5f, .5f));
            Assert.Throws<ArgumentException>(() => tooLong.Validate(2));
        }
        [Test] public void ZeroResistanceIsValidAndKeepsTheSeparateKickSettings()
        {
            var profile = GamepadFeedbackProfile.Load();
            profile.readyStrength = profile.chargingStrength = 0;
            profile.kickStrength = 5; profile.kickFrequency = 29; profile.kickDuration = .15f;
            Assert.DoesNotThrow(profile.Validate);
            Assert.That(new TriggerFeedback(TriggerFeedbackMode.Resistance, profile.triggerStart, profile.readyStrength).Mode, Is.EqualTo(TriggerFeedbackMode.Off));
            Assert.That(new TriggerFeedback(TriggerFeedbackMode.Resistance, profile.triggerStart, profile.chargingStrength).Mode, Is.EqualTo(TriggerFeedbackMode.Off));
            var kick = new TriggerFeedback(TriggerFeedbackMode.Vibration, profile.triggerStart, profile.kickStrength, profile.kickFrequency);
            Assert.That(kick.Mode, Is.EqualTo(TriggerFeedbackMode.Vibration));
            Assert.That(kick.Strength, Is.EqualTo(5)); Assert.That(kick.Frequency, Is.EqualTo(29));
            profile.readyStrength = -1; Assert.Throws<ArgumentException>(profile.Validate);
        }
        [Test] public void JsonPatternDecodesWithoutLegacyFieldsAndKeepsChannelOrder()
        {
            const string json = "{\"segments\":[{\"low\":0.8,\"high\":0.1,\"duration\":0.04,\"curve\":\"hold\"},{\"low\":0,\"high\":0,\"duration\":0.03,\"curve\":\"hold\"},{\"low\":0.2,\"high\":0.6,\"duration\":0.06,\"curve\":\"rise\"}]}";
            var pulse = JsonUtility.FromJson<GamepadFeedbackProfile.Pulse>(json);
            pulse.Validate(2);
            Levels(pulse, .02, .8f, .1f); Levels(pulse, .055, 0, 0); Levels(pulse, .1, .1f, .3f);
        }
    }
}
