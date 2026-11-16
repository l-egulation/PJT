using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Deviant.Replay
{
    // Numeric payload layouts are versioned in docs/development/telemetry/replay-recording.md.
    public enum ReplayKind : byte
    {
        String = 1, SnapshotBegin = 2, SnapshotEnd = 3, Entity = 4, EntityExit = 5,
        Pointer = 6, UiState = 7, UiActivate = 8, Input = 9, Command = 10, Hit = 11,
        Clock = 12, Gap = 13, Run = 14, Scene = 15, Footer = 16, UiRect = 17, Screen = 18, Layout = 19, Weapon = 20, Growth = 21, Economy = 22
    }
    public sealed class ReplayRecord
    {
        public ReplayKind Kind { get; }
        public long TimeMs { get; }
        public long Tick { get; }
        public long Sequence { get; internal set; }
        public long[] Values { get; }
        public string Text { get; }
        public ReplayRecord(ReplayKind kind, long timeMs, long tick, long[] values = null, string text = null)
        {
            if (!Enum.IsDefined(typeof(ReplayKind), kind) || timeMs < 0 || tick < -1) throw new ArgumentOutOfRangeException();
            Kind = kind; TimeMs = timeMs; Tick = tick;
            Values = values == null ? Array.Empty<long>() : (long[])values.Clone(); Text = text;
            if (Values.Length > 64 || (kind != ReplayKind.String && text != null)) throw new ArgumentException("Invalid replay payload.");
        }
    }
    public sealed class ReplayHeader
    {
        public string SessionId, SegmentId, RunId = "", BuildId, ContentHash, UiVersion = "1", ProfileId = "compact-v1";
        public int ChunkIndex;
        public long FirstSequence = 1, StartMs;
        public string PreviousSha256 = "";
        static readonly string[] Keys = { "session", "segment", "run", "build", "content", "ui", "profile", "previous" };
        public string Encode()
        {
            Validate();
            string[] values = { SessionId, SegmentId, RunId, BuildId, ContentHash, UiVersion, ProfileId, PreviousSha256 };
            var s = new StringBuilder("{");
            for (int i = 0; i < Keys.Length; i++) { if (i > 0) s.Append(','); s.Append('"').Append(Keys[i]).Append("\":\"").Append(values[i]).Append('"'); }
            return s.Append(",\"schema\":1,\"chunk\":").Append(ChunkIndex.ToString(CultureInfo.InvariantCulture))
                .Append(",\"first\":").Append(FirstSequence.ToString(CultureInfo.InvariantCulture))
                .Append(",\"start\":").Append(StartMs.ToString(CultureInfo.InvariantCulture)).Append('}').ToString();
        }
        public static ReplayHeader Decode(string json)
        {
            if (json == null || json.Length > 65536) throw new FormatException("Replay header size.");
            var tokens = Regex.Matches(json, "\"([a-z]+)\":(?:\"([A-Za-z0-9._-]*)\"|([0-9]+))");
            var v = new Dictionary<string, string>();
            foreach (Match t in tokens) { if (v.ContainsKey(t.Groups[1].Value)) throw new FormatException("Duplicate header field."); v.Add(t.Groups[1].Value, t.Groups[2].Success ? t.Groups[2].Value : t.Groups[3].Value); }
            try
            {
                if (v.Count != 12 || v["schema"] != "1") throw new FormatException("Unsupported replay schema.");
                var h = new ReplayHeader { SessionId = v["session"], SegmentId = v["segment"], RunId = v["run"], BuildId = v["build"],
                    ContentHash = v["content"], UiVersion = v["ui"], ProfileId = v["profile"], PreviousSha256 = v["previous"],
                    ChunkIndex = int.Parse(v["chunk"], CultureInfo.InvariantCulture), FirstSequence = long.Parse(v["first"], CultureInfo.InvariantCulture), StartMs = long.Parse(v["start"], CultureInfo.InvariantCulture) };
                if (h.Encode() != json) throw new FormatException("Noncanonical replay header.");
                return h;
            }
            catch (Exception e) when (e is KeyNotFoundException || e is ArgumentException || e is OverflowException) { throw new FormatException("Invalid replay header.", e); }
        }
        public void Validate()
        {
            if (!Guid.TryParseExact(SessionId, "N", out _) || !Guid.TryParseExact(SegmentId, "N", out _) ||
                (!string.IsNullOrEmpty(RunId) && !Guid.TryParseExact(RunId, "N", out _)) || ChunkIndex < 0 || FirstSequence < 1 || StartMs < 0)
                throw new ArgumentException("Invalid replay identity.");
            foreach (var s in new[] { BuildId, ContentHash, UiVersion, ProfileId, PreviousSha256 })
                if (s == null || s.Length > 128 || !Regex.IsMatch(s, "^[A-Za-z0-9._-]*$")) throw new ArgumentException("Invalid replay version.");
            if (string.IsNullOrEmpty(BuildId) || string.IsNullOrEmpty(ContentHash) || string.IsNullOrEmpty(ProfileId) ||
                (ChunkIndex == 0 ? PreviousSha256.Length != 0 : !Regex.IsMatch(PreviousSha256, "^[a-f0-9]{64}$"))) throw new ArgumentException("Invalid replay chain.");
        }
    }
    public sealed class ReplayChunk
    {
        public ReplayHeader Header { get; }
        public IReadOnlyList<ReplayRecord> Records { get; }
        public int RawBytes { get; internal set; }
        public ReplayChunk(ReplayHeader header, IReadOnlyList<ReplayRecord> records) { Header = header; Records = records; }
    }
    public static class ReplayNumbers
    {
        [StructLayout(LayoutKind.Explicit)] struct FloatUnion { [FieldOffset(0)] public float F; [FieldOffset(0)] public int I; }
        public static long Float(float value) { if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value)); return new FloatUnion { F = value }.I; }
        public static float Unfloat(long value) { if (value < int.MinValue || value > int.MaxValue) throw new FormatException("Float range."); var f = new FloatUnion { I = (int)value }.F; if (float.IsNaN(f) || float.IsInfinity(f)) throw new FormatException("Nonfinite float."); return f; }
        public static long Quantize(double value, int scale = 256)
        { if (double.IsNaN(value) || double.IsInfinity(value) || scale < 1 || Math.Abs(value) > int.MaxValue / (double)scale) throw new ArgumentOutOfRangeException(nameof(value)); return (long)Math.Round(value * scale, MidpointRounding.AwayFromZero); }
    }
}
