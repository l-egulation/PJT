using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace Deviant.Definitions
{
    public enum TimelineAnchor { Seconds, StepStart, StepEnd, Marker }
    public sealed class TimelineFrame
    {
        public string Id { get; }
        public float Seconds { get; }
        public TimelineFrame(string id, float seconds)
        {
            if (string.IsNullOrWhiteSpace(id) || !FrameTimeline.Finite(seconds) || seconds <= 0) throw new ArgumentException("A stable step ID and positive finite duration are required.");
            Id = id; Seconds = seconds;
        }
    }
    public sealed class TimelineMarker
    {
        public string Id { get; }
        public TimelineAnchor Anchor { get; }
        public string Reference { get; }
        public float Offset { get; }
        public TimelineMarker(string id, TimelineAnchor anchor, string reference, float offset)
        {
            if (string.IsNullOrWhiteSpace(id) || !Enum.IsDefined(typeof(TimelineAnchor), anchor) || !FrameTimeline.Finite(offset)) throw new ArgumentException("A marker ID, supported anchor and finite time/offset are required.");
            if (anchor == TimelineAnchor.Seconds ? !string.IsNullOrEmpty(reference) || offset < 0 : string.IsNullOrWhiteSpace(reference)) throw new ArgumentException("A marker has exactly one absolute or referenced time source.");
            Id = id; Anchor = anchor; Reference = reference ?? ""; Offset = offset;
        }
    }

    // Definition/compilation only: no simulation clock, host callbacks or effects.
    public sealed class FrameTimeline
    {
        public IReadOnlyList<TimelineFrame> Frames { get; }
        public IReadOnlyList<TimelineMarker> Markers { get; }
        public IReadOnlyDictionary<string, float> Times { get; }
        public float Duration { get; }
        public string ContentHash { get; }
        public FrameTimeline(IEnumerable<TimelineFrame> frames, IEnumerable<TimelineMarker> markers)
        {
            var steps = frames?.ToArray(); var bindings = markers?.ToArray();
            if (steps == null || steps.Length == 0 || steps.Length > 256 || steps.Any(s => s == null) || steps.Select(s => s.Id).Distinct(StringComparer.Ordinal).Count() != steps.Length)
                throw new ArgumentException("Timeline needs 1..256 uniquely identified frames.");
            if (bindings == null || bindings.Length == 0 || bindings.Length > 256 || bindings.Any(m => m == null) || bindings.Select(m => m.Id).Distinct(StringComparer.Ordinal).Count() != bindings.Length)
                throw new ArgumentException("Timeline needs 1..256 uniquely identified markers.");
            var starts = new Dictionary<string, float>(StringComparer.Ordinal); var ends = new Dictionary<string, float>(StringComparer.Ordinal); float total = 0;
            foreach (var step in steps)
            {
                float next = total + step.Seconds;
                if (!Finite(next) || next <= total) throw new ArgumentException("Every frame must advance finite source time.");
                starts.Add(step.Id, total); ends.Add(step.Id, next); total = next;
            }
            var byId = bindings.ToDictionary(m => m.Id, StringComparer.Ordinal); var visiting = new HashSet<string>(StringComparer.Ordinal); var times = new Dictionary<string, float>(StringComparer.Ordinal);
            float Resolve(string id)
            {
                if (times.TryGetValue(id, out var cached)) return cached;
                if (!byId.TryGetValue(id, out var marker)) throw new ArgumentException("Unknown marker: " + id);
                if (!visiting.Add(id)) throw new ArgumentException("Marker cycle: " + id);
                float value;
                switch (marker.Anchor)
                {
                    case TimelineAnchor.Seconds: value = marker.Offset; break;
                    case TimelineAnchor.StepStart:
                    case TimelineAnchor.StepEnd:
                        var positions = marker.Anchor == TimelineAnchor.StepStart ? starts : ends;
                        if (!positions.TryGetValue(marker.Reference, out var position)) throw new ArgumentException("Unknown step: " + marker.Reference);
                        value = position + marker.Offset; break;
                    default: value = Resolve(marker.Reference) + marker.Offset; break;
                }
                if (!Finite(value) || value < 0) throw new ArgumentException("Resolved marker time must be finite and nonnegative: " + id);
                visiting.Remove(id); times.Add(id, value); return value;
            }
            foreach (var marker in bindings) Resolve(marker.Id);
            Frames = Array.AsReadOnly(steps); Markers = Array.AsReadOnly(bindings); Times = new ReadOnlyDictionary<string, float>(times); Duration = total;
            using (var bytes = new MemoryStream())
            {
                using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, true))
                {
                    writer.Write(1); writer.Write(steps.Length);
                    foreach (var step in steps) { writer.Write(step.Id); writer.Write(step.Seconds); }
                    writer.Write(bindings.Length);
                    foreach (var marker in bindings.OrderBy(m => m.Id, StringComparer.Ordinal)) { writer.Write(marker.Id); writer.Write((int)marker.Anchor); writer.Write(marker.Reference); writer.Write(marker.Offset); }
                }
                using (var sha = SHA256.Create()) ContentHash = BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }
        // A directly referenced end stores the authored interval itself. Keep
        // that float rather than subtracting two rounded absolute times: Native
        // execution tests start + duration and must retain that operation order.
        public float IntervalDuration(string start, string end)
        {
            if (!Times.TryGetValue(start, out var from) || !Times.TryGetValue(end, out var to))
                throw new ArgumentException("An interval requires two existing markers.");
            var binding = Markers.First(marker => marker.Id == end);
            float duration = binding.Anchor == TimelineAnchor.Marker && binding.Reference == start ? binding.Offset : to - from;
            if (!Finite(duration) || duration <= 0 || to <= from) throw new ArgumentException("A finite positive marker interval is required.");
            return duration;
        }
        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
