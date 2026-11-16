using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace Deviant.Definitions
{
    // Shared playback lifetime only. Each medium owns genuine sampling rather
    // than representing non-sprite media as synthetic sprite frames.
    public interface ICueDefinition
    {
        string Id { get; }
        bool Loop { get; }
        float Duration { get; }
        string ContentHash { get; }
    }

    // Immutable sprite sequence; the version-one hash and frame intervals are
    // unchanged. Unity objects stay in its binding, outside this definition.
    public sealed class PresentationCueDefinition : ICueDefinition
    {
        public string Id { get; }
        public IReadOnlyList<string> Frames { get; }
        public IReadOnlyList<float> Seconds { get; }
        public IReadOnlyList<float> X { get; }
        public IReadOnlyList<float> Y { get; }
        public bool FlipX { get; }
        public bool Loop { get; }
        public float Duration { get; }
        public string ContentHash { get; }
        public PresentationCueDefinition(string id, string[] frames, float[] seconds, float[] x, float[] y, bool flipX = false, bool loop = false)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A stable cue ID is required.", nameof(id));
            if (frames == null || seconds == null || x == null || y == null || frames.Length == 0 ||
                frames.Length != seconds.Length || frames.Length != x.Length || frames.Length != y.Length || frames.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException("Cue frame keys, durations and offsets must have matching nonempty lengths.");
            float duration = 0;
            for (int i = 0; i < seconds.Length; i++)
            {
                if (!Finite(seconds[i]) || seconds[i] <= 0 || !Finite(x[i]) || !Finite(y[i])) throw new ArgumentOutOfRangeException(nameof(seconds));
                float next = duration + seconds[i];
                if (!Finite(next) || next <= duration) throw new ArgumentOutOfRangeException(nameof(seconds), "Frame time must advance finite cue time.");
                duration = next;
            }
            Id = id; Frames = Array.AsReadOnly((string[])frames.Clone()); Seconds = Array.AsReadOnly((float[])seconds.Clone());
            X = Array.AsReadOnly((float[])x.Clone()); Y = Array.AsReadOnly((float[])y.Clone());
            Duration = duration; FlipX = flipX; Loop = loop;
            using (var bytes = new MemoryStream())
            {
                using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, true))
                {
                    writer.Write(1); writer.Write(Id); writer.Write(FlipX); writer.Write(Loop); writer.Write(Frames.Count);
                    for (int i = 0; i < Frames.Count; i++) { writer.Write(Frames[i]); writer.Write(Seconds[i]); writer.Write(X[i]); writer.Write(Y[i]); }
                }
                using (var sha = SHA256.Create()) ContentHash = BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }
        public int FrameAt(float elapsed)
        {
            if (!Finite(elapsed)) throw new ArgumentOutOfRangeException(nameof(elapsed));
            // Match the legacy frame intervals and float subtraction. The player
            // ends one-shot cues before sampling at Duration.
            float time = Loop ? elapsed - (float)Math.Floor(elapsed / Duration) * Duration : Math.Max(0, Math.Min(elapsed, Duration - .00001f));
            time = Math.Max(0, Math.Min(time, Duration));
            for (int i = 0; i < Seconds.Count; i++) { if (time < Seconds[i]) return i; time -= Seconds[i]; }
            return Seconds.Count - 1;
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
