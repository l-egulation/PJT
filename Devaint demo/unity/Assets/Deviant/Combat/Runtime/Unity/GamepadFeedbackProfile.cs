using System;
using UnityEngine;

namespace Deviant.Combat
{
    [Serializable]
    public sealed class GamepadFeedbackProfile
    {
        public const int MaximumSegments = 16;
        public const float MaximumPatternDuration = 2;

        [Serializable]
        public struct Segment
        {
            public float low, high, duration;
            public string curve;
            public void Validate()
            {
                ValidateLevels(low, high, duration);
                if (curve != "decay" && curve != "hold" && curve != "rise")
                    throw new ArgumentException("Unknown controller feedback curve.");
            }
            public Vector2 Sample(double elapsed)
            {
                float progress = Mathf.Clamp01((float)(elapsed / duration));
                float gain = curve == "rise" ? progress : curve == "hold" ? 1 : 1 - progress;
                return new Vector2(low * gain, high * gain);
            }
        }

        [Serializable]
        public struct Pulse
        {
            // Schema 1 pulses retain their original linear decay. Schema 2 can
            // publish an explicit sequence, including zero-strength rest segments.
            public float low, high, duration;
            public Segment[] segments;
            public float Duration
            {
                get
                {
                    if (segments == null) return duration;
                    float total = 0;
                    for (int i = 0; i < segments.Length; i++) total += segments[i].duration;
                    return total;
                }
            }
            public void Validate(int version = 2)
            {
                if (segments == null) { ValidateLevels(low, high, duration); return; }
                if (version != 2 || segments.Length < 1 || segments.Length > MaximumSegments)
                    throw new ArgumentException("Invalid controller feedback segment count or schema.");
                double total = 0;
                for (int i = 0; i < segments.Length; i++) { segments[i].Validate(); total += segments[i].duration; }
                if (total > MaximumPatternDuration + .0000001)
                    throw new ArgumentException("Controller feedback patterns must not exceed two seconds.");
            }
            public Vector2 Sample(double elapsed)
            {
                if (elapsed < 0) return Vector2.zero;
                if (segments == null)
                {
                    if (elapsed >= duration || duration <= 0) return Vector2.zero;
                    float remaining = 1 - (float)(elapsed / duration);
                    return new Vector2(low * remaining, high * remaining);
                }
                for (int i = 0; i < segments.Length; i++)
                {
                    if (elapsed < segments[i].duration) return segments[i].Sample(elapsed);
                    elapsed -= segments[i].duration;
                }
                return Vector2.zero;
            }
        }
        public int schemaVersion;
        public Pulse basic, special, dash, jump, hit, heavyHit, hurt, parry;
        public int triggerStart, readyStrength, chargingStrength, kickStrength, kickFrequency;
        public float kickDuration, maximumEventAge;
        public static GamepadFeedbackProfile Load()
        {
            var asset = Resources.Load<TextAsset>("GamepadFeedback");
            if (!asset) throw new InvalidOperationException("The generated GamepadFeedback catalog is missing.");
            var profile = JsonUtility.FromJson<GamepadFeedbackProfile>(asset.text);
            profile.Validate(); return profile;
        }
        public void Validate()
        {
            if ((schemaVersion != 1 && schemaVersion != 2) || triggerStart < 0 || triggerStart > 9 || readyStrength < 0 || readyStrength > 8 ||
                chargingStrength < 0 || chargingStrength > 8 || kickStrength < 1 || kickStrength > 8 || kickFrequency < 1 || kickFrequency > 255 ||
                !Finite(kickDuration) || kickDuration <= 0 || kickDuration > 1 || !Finite(maximumEventAge) || maximumEventAge <= 0 || maximumEventAge > 1)
                throw new ArgumentException("Invalid controller feedback profile.");
            basic.Validate(schemaVersion); special.Validate(schemaVersion); dash.Validate(schemaVersion); jump.Validate(schemaVersion);
            hit.Validate(schemaVersion); heavyHit.Validate(schemaVersion); hurt.Validate(schemaVersion); parry.Validate(schemaVersion);
        }
        static void ValidateLevels(float low, float high, float duration)
        {
            if (!Finite(low) || !Finite(high) || !Finite(duration) || low < 0 || low > 1 || high < 0 || high > 1 || duration <= 0 || duration > 1)
                throw new ArgumentException("Invalid controller feedback segment levels or duration.");
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
