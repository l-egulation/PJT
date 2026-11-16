using System;
using System.Collections.Generic;

namespace Deviant.Replay
{
    public sealed class ReplayRecorder
    {
        readonly List<ReplayRecord> records = new List<ReplayRecord>();
        readonly Dictionary<string, int> strings = new Dictionary<string, int>(StringComparer.Ordinal);
        readonly Dictionary<(ReplayKind, long), long[]> previous = new Dictionary<(ReplayKind, long), long[]>();
        long lastTime;
        bool sealedCandidates;
        public int Count => records.Count;
        public int EstimatedBytes { get; private set; }
        public ReplayRecorder(long startMs) { lastTime = startMs; Add(ReplayKind.SnapshotBegin, startMs, -1); }
        public int Intern(string value, long time, long tick = -1)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            if (strings.TryGetValue(value, out var id)) return id;
            if (value.Length > 1024 || strings.Count >= 8192) throw new InvalidOperationException("Replay dictionary capacity.");
            id = strings.Count + 1; strings.Add(value, id);
            AddRecord(new ReplayRecord(ReplayKind.String, time, tick, new[] { (long)id }, value)); return id;
        }
        public void Add(ReplayKind kind, long time, long tick, params long[] values) => AddRecord(new ReplayRecord(kind, time, tick, values));
        public bool Changed(ReplayKind kind, long key, long time, long tick, params long[] values)
        {
            var stateKey = (kind, key);
            if (previous.TryGetValue(stateKey, out var old) && Equal(old, values)) return false;
            if (kind == ReplayKind.Entity && old != null && old.Length == 12 && values.Length == 12 &&
                (Math.Abs(old[1] - values[1]) > 1024 || Math.Abs(old[2] - values[2]) > 1024))
                Add(ReplayKind.EntityExit, time, tick, key, 2); // Display discontinuity, not a factual teleport classification.
            var record = new ReplayRecord(kind, time, tick, values); previous[stateKey] = record.Values; AddRecord(record); return true;
        }
        public void Forget(ReplayKind kind, long key) => previous.Remove((kind, key));
        static bool Equal(long[] a, long[] b) { if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
        void AddRecord(ReplayRecord r)
        {
            if (sealedCandidates) throw new InvalidOperationException("Replay candidates are sealed.");
            if (r.TimeMs < lastTime) throw new ArgumentException("Replay time reversed.");
            if (records.Count >= ReplayCodec.MaxRecords - 1 || EstimatedBytes > 2 * 1024 * 1024) throw new InvalidOperationException("Replay buffer capacity.");
            lastTime = r.TimeMs; records.Add(r); EstimatedBytes += 32 + r.Values.Length * 8 + (r.Text?.Length ?? 0) * 3;
        }
        public List<ReplayRecord> Finish()
        {
            if (records.Count < 2) throw new InvalidOperationException("Snapshot is incomplete.");
            return ReplaySimplifier.Simplify(records);
        }
        public ReplayRecord[] SealCandidates() { if (sealedCandidates) throw new InvalidOperationException("Already sealed."); sealedCandidates = true; return records.ToArray(); }
    }
    public static class ReplaySimplifier
    {
        // Bounded chunk-local RDP in time, not geometric distance to an untimed line.
        public static List<ReplayRecord> Simplify(IList<ReplayRecord> source)
        {
            var keep = new bool[source.Count]; for (int i = 0; i < keep.Length; i++) keep[i] = true;
            var tracks = new Dictionary<string, List<int>>(); bool snapshot = false;
            Action flush = () => { foreach (var t in tracks.Values) Reduce(source, t, keep); tracks.Clear(); };
            for (int i = 0; i < source.Count; i++)
            {
                var r = source[i];
                if (r.Kind == ReplayKind.SnapshotBegin) { flush(); snapshot = true; }
                if (r.Kind == ReplayKind.SnapshotEnd) { snapshot = false; flush(); continue; }
                bool spatial = r.Kind == ReplayKind.Entity || r.Kind == ReplayKind.Pointer;
                if (!spatial || snapshot)
                {
                    // Dictionary entries carry no gameplay discontinuity; other events anchor every active track.
                    if (r.Kind != ReplayKind.String) flush();
                    continue;
                }
                string key = ((int)r.Kind).ToString() + ":" + r.Values[0];
                if (!tracks.TryGetValue(key, out var track)) tracks.Add(key, track = new List<int>());
                if (track.Count > 0 && !SameState(source[track[track.Count - 1]], r)) { Reduce(source, track, keep); track.Clear(); }
                track.Add(i);
            }
            flush(); var output = new List<ReplayRecord>();
            for (int i = 0; i < source.Count; i++) if (keep[i]) output.Add(source[i]);
            return output;
        }
        static bool SameState(ReplayRecord a, ReplayRecord b)
        {
            if (a.Values.Length != b.Values.Length) return false;
            for (int i = 0; i < a.Values.Length; i++) if (i != 1 && i != 2 && a.Values[i] != b.Values[i]) return false;
            return true;
        }
        static void Reduce(IList<ReplayRecord> source, List<int> track, bool[] keep)
        {
            if (track.Count < 3) return;
            for (int i = 1; i < track.Count - 1; i++) keep[track[i]] = false;
            var stack = new Stack<(int, int)>(); stack.Push((0, track.Count - 1));
            while (stack.Count != 0)
            {
                var range = stack.Pop(); var a = source[track[range.Item1]]; var b = source[track[range.Item2]];
                double tolerance = a.Kind == ReplayKind.Pointer ? 2 : a.Values[0] == 1 || a.Values[0] == 0 ? 2 : 8;
                double max = tolerance * tolerance; int split = -1;
                for (int i = range.Item1 + 1; i < range.Item2; i++)
                {
                    var p = source[track[i]]; double t = b.TimeMs == a.TimeMs ? 0 : (p.TimeMs - a.TimeMs) / (double)(b.TimeMs - a.TimeMs);
                    double dx = p.Values[1] - (a.Values[1] + (b.Values[1] - a.Values[1]) * t);
                    double dy = p.Values[2] - (a.Values[2] + (b.Values[2] - a.Values[2]) * t);
                    double error = dx * dx + dy * dy;
                    if (error > max) { max = error; split = i; }
                }
                if (split < 0) continue;
                keep[track[split]] = true; stack.Push((range.Item1, split)); stack.Push((split, range.Item2));
            }
        }
    }
}
