using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace Deviant.Definitions
{
    // Compatibility compilation for the existing combat-time-fixed motion mode.
    // Immutable source frames and action markers share one time interpretation.
    public sealed class AttackMotionTiming
    {
        readonly CapturedCueMediaMapping cueMapping;
        public bool UsesFrameTime { get; }
        public string Id { get; }
        public IReadOnlyList<string> Frames { get; }
        public IReadOnlyList<float> Seconds { get; }
        public float SourceDuration { get; }
        public float SourceImpact { get; }
        public float FxStart { get; }
        public float Impact { get; }
        public float End { get; }
        public string ContentHash { get; }
        public AttackMotionTiming(string id, string[] frames, float[] seconds, int contact, float sourceImpact, float fxStart, float impact, float end)
            : this(id, frames, seconds, contact, sourceImpact, fxStart, impact, end, false, null) { }
        AttackMotionTiming(string id, string[] frames, float[] seconds, int contact, float sourceImpact, float fxStart, float impact, float end,
            bool frameTime, CapturedCueMediaMapping cueMapping)
        {
            UsesFrameTime = frameTime; this.cueMapping = cueMapping;
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A stable motion binding ID is required.", nameof(id));
            if (frames == null || seconds == null || frames.Length == 0 || frames.Length != seconds.Length || frames.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException("Motion frames and seconds require matching nonempty lists.");
            float duration = 0;
            foreach (float value in seconds)
            {
                float next = duration + value;
                if (!Finite(value) || value <= 0 || !Finite(next) || next <= duration) throw new ArgumentOutOfRangeException(nameof(seconds));
                duration = next;
            }
            if (!Finite(sourceImpact) || !Finite(fxStart) || !Finite(impact) || impact < 0 || !Finite(end) || end <= 0 || impact > end)
                throw new ArgumentOutOfRangeException(nameof(impact));
            if (sourceImpact < 0)
            {
                if (contact > seconds.Length) throw new ArgumentOutOfRangeException(nameof(contact));
                sourceImpact = 0; for (int i = 0; i < Math.Max(0, contact); i++) sourceImpact += seconds[i];
            }
            if (sourceImpact > duration) throw new ArgumentOutOfRangeException(nameof(sourceImpact));
            Id = id; Frames = Array.AsReadOnly((string[])frames.Clone()); Seconds = Array.AsReadOnly((float[])seconds.Clone());
            SourceDuration = duration; SourceImpact = sourceImpact; FxStart = fxStart; Impact = impact; End = end;
            using (var bytes = new MemoryStream())
            {
                using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, true))
                {
                    writer.Write(UsesFrameTime ? 3 : 1); writer.Write(Id); writer.Write(Frames.Count);
                    for (int i = 0; i < Frames.Count; i++) { writer.Write(Frames[i]); writer.Write(Seconds[i]); }
                    writer.Write(SourceImpact); writer.Write(FxStart); writer.Write(Impact); writer.Write(End);
                    if (UsesFrameTime) writer.Write(cueMapping?.ContentHash ?? "");
                }
                using (var sha = SHA256.Create()) ContentHash = BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }
        public float SourceTime(float age)
        {
            if (!Finite(age)) throw new ArgumentOutOfRangeException(nameof(age));
            return UsesFrameTime ? age : MapSourceTime(age, Impact, End, SourceImpact, SourceDuration);
        }
        public static AttackMotionTiming FrameTime(string id, string[] frames, float[] seconds, float impact, float fxStart, float end, CapturedCueMediaMapping cueMapping)
            => new AttackMotionTiming(id, frames, seconds, -1, impact, fxStart, impact, end, true, cueMapping);
        public float CueElapsed(float age)
        {
            if (!Finite(age)) throw new ArgumentOutOfRangeException(nameof(age));
            if (!UsesFrameTime) return SourceTime(age) - FxStart;
            // Retain the existing Cue media curve while body frames use direct
            // action time. The explicit Cue marker chooses the curve's origin.
            if (cueMapping == null) throw new InvalidOperationException("This FrameTime action has no eligible Cue media mapping.");
            float origin = MapSourceTime(FxStart, Impact, End, cueMapping.SourceImpact, cueMapping.SourceDuration);
            return MapSourceTime(age, Impact, End, cueMapping.SourceImpact, cueMapping.SourceDuration) - origin;
        }
        public int FrameAt(float age) => SelectFrame(Seconds, SourceDuration, SourceTime(age), false);
        public float ActionTime(float sourceTime)
        {
            if (!Finite(sourceTime) || sourceTime < 0) throw new ArgumentOutOfRangeException(nameof(sourceTime));
            if (UsesFrameTime) return Math.Min(End, sourceTime);
            if (sourceTime <= 0) return 0;
            float value = sourceTime <= SourceImpact && SourceImpact > 0 ? sourceTime / SourceImpact * Impact
                : SourceDuration > SourceImpact ? Impact + (sourceTime - SourceImpact) / (SourceDuration - SourceImpact) * Math.Max(.001f, End - Impact) : End;
            return Math.Min(End, Math.Max(0, value));
        }
        public IReadOnlyList<float> EffectiveSeconds()
        {
            var result = new float[Seconds.Count]; float source = 0, previous = 0;
            for (int index = 0; index < result.Length; index++)
            {
                source += Seconds[index]; float boundary = index + 1 == result.Length ? End : ActionTime(source);
                result[index] = Math.Max(0, boundary - previous); previous = boundary;
            }
            return Array.AsReadOnly(result);
        }
        // Preserve float evaluation order and the legacy minimum recovery span.
        public static float MapSourceTime(float age, float impact, float end, float sourceImpact, float sourceDuration)
            => age < impact && impact > 0 ? age / impact * sourceImpact : sourceImpact + (age - impact) / Math.Max(.001f, end - impact) * (sourceDuration - sourceImpact);
        public int ChargeSlamFrameAt(float age, float impact, ChargeDefinition charge, bool charging)
        {
            const float holdPoseAt = .16f, liftAt = .28f;
            float recovery = Math.Max(.01f, charge.BaseEnd - charge.Minimum);
            float sourceTime = charging ? Math.Min(age, holdPoseAt) : age < impact
                ? liftAt + (SourceImpact - liftAt) * Clamp((age - impact + charge.ReleaseWindup) / charge.ReleaseWindup, 0, 1)
                : SourceImpact + (age - impact) * (SourceDuration - SourceImpact) / recovery;
            return SelectFrame(Seconds, SourceDuration, sourceTime, false);
        }

        public static int SelectFrame(IReadOnlyList<float> seconds, float duration, float time, bool loop)
        {
            float t = loop ? Clamp(time - (float)Math.Floor(time / duration) * duration, 0, duration) : Clamp(time, 0, duration - .00001f);
            for (int i = 0; i < seconds.Count; i++) { if (t < seconds[i]) return i; t -= seconds[i]; }
            return seconds.Count - 1;
        }
        static float Clamp(float value, float min, float max) { if (value < min) return min; if (value > max) return max; return value; }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
