using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace Deviant.Definitions
{
    // Clip content is injected by a Unity binding. Exact PCM metadata fixes the
    // sampling interval without inventing a parallel duration or sprite frames.
    public sealed class AudioCueDefinition : ICueDefinition
    {
        public string Id { get; }
        public string ClipId { get; }
        public int SampleCount { get; }
        public int Frequency { get; }
        public bool Loop { get; }
        public float Duration { get; }
        public string ContentHash { get; }

        public AudioCueDefinition(string id, string clipId, int sampleCount, int frequency, bool loop = false)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(clipId))
                throw new ArgumentException("Stable cue and clip IDs are required.");
            if (sampleCount <= 0) throw new ArgumentOutOfRangeException(nameof(sampleCount));
            if (frequency <= 0) throw new ArgumentOutOfRangeException(nameof(frequency));
            Id = id; ClipId = clipId; SampleCount = sampleCount; Frequency = frequency; Loop = loop;
            Duration = (float)((double)sampleCount / frequency);
            ContentHash = HashOf(Id, ClipId, SampleCount, Frequency, Loop);
        }

        // The hash is a pure function of these fields. Combat audio builds a definition for every attack, hit and
        // footstep, so a SHA-256 instance and stream per play are kept to one per distinct cue (QA 2026-11-16).
        static readonly Dictionary<(string, string, int, int, bool), string> hashes = new Dictionary<(string, string, int, int, bool), string>();
        static string HashOf(string id, string clipId, int sampleCount, int frequency, bool loop)
        {
            var key = (id, clipId, sampleCount, frequency, loop);
            lock (hashes) if (hashes.TryGetValue(key, out var known)) return known;
            string hash;
            using (var bytes = new MemoryStream())
            {
                using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, true))
                {
                    writer.Write("audio-cue"); writer.Write(1); writer.Write(id); writer.Write(clipId);
                    writer.Write(sampleCount); writer.Write(frequency); writer.Write(loop);
                }
                using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-", "").ToLowerInvariant();
            }
            lock (hashes) hashes[key] = hash;
            return hash;
        }

        public int SampleAt(float elapsed)
        {
            if (float.IsNaN(elapsed) || float.IsInfinity(elapsed)) throw new ArgumentOutOfRangeException(nameof(elapsed));
            double time = elapsed;
            if (Loop) time -= Math.Floor(time / Duration) * Duration;
            time = Math.Max(0, Math.Min(time, Duration));
            return (int)Math.Max(0, Math.Min(SampleCount - 1, Math.Floor(time * Frequency)));
        }
    }
}
