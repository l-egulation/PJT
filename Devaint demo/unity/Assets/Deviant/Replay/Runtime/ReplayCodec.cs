using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Deviant.Replay
{
    public static class ReplayCodec
    {
        public const int MaxCompressed = 4 * 1024 * 1024, MaxRaw = 16 * 1024 * 1024, MaxRecord = 256 * 1024, MaxRecords = 65536;
        static readonly byte[] Magic = Encoding.ASCII.GetBytes("DVRPLY01");
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        public static byte[] Encode(ReplayHeader header, IList<ReplayRecord> records, out int rawBytes)
        {
            if (records.Count < 2 || records.Count > MaxRecords || records[0].Kind != ReplayKind.SnapshotBegin) throw new ArgumentException("Snapshot required.");
            using (var raw = new MemoryStream())
            {
                raw.Write(Magic, 0, Magic.Length);
                using (var w = new BinaryWriter(raw, Utf8, true))
                {
                    var entityState = new Dictionary<long, long[]>();
                    w.Write((ushort)1); var h = Utf8.GetBytes(header.Encode()); w.Write(h.Length); w.Write(h);
                    long time = header.StartMs, sequence = header.FirstSequence - 1;
                    foreach (var r in records)
                    {
                        if (r.Kind == ReplayKind.Footer || r.TimeMs < time) throw new ArgumentException("Invalid record order.");
                        r.Sequence = checked(++sequence); WriteRecord(raw, r, r.TimeMs - time, entityState); time = r.TimeMs;
                        if (raw.Length > MaxRaw - MaxRecord) throw new InvalidDataException("Replay raw size limit.");
                    }
                    var footer = new ReplayRecord(ReplayKind.Footer, time, -1, new[] { sequence, (long)records.Count });
                    footer.Sequence = checked(sequence + 1); WriteRecord(raw, footer, 0, entityState);
                }
                rawBytes = checked((int)raw.Length);
                using (var packed = new MemoryStream())
                {
                    using (var gzip = new GZipStream(packed, CompressionLevel.Optimal, true)) { raw.Position = 0; raw.CopyTo(gzip); }
                    if (packed.Length > MaxCompressed) throw new InvalidDataException("Replay compressed size limit.");
                    return packed.ToArray();
                }
            }
        }
        static void WriteRecord(Stream output, ReplayRecord r, long deltaMs, Dictionary<long, long[]> entityState)
        {
            using (var bytes = new MemoryStream())
            {
                if (r.Kind == ReplayKind.SnapshotBegin) entityState.Clear();
                Var(bytes, (ulong)r.Kind); Var(bytes, 1); Var(bytes, (ulong)deltaMs); Signed(bytes, r.Tick);
                if (r.Kind == ReplayKind.Entity)
                {
                    if (r.Values.Length != 12) throw new InvalidDataException("Entity payload.");
                    entityState.TryGetValue(r.Values[0], out var old); int mask = 0, changed = 0;
                    for (int i = 1; i < 12; i++) if (old == null || old[i] != r.Values[i]) { mask |= 1 << (i - 1); changed++; }
                    Var(bytes, (ulong)(changed + 2)); Signed(bytes, r.Values[0]); Signed(bytes, mask);
                    for (int i = 1; i < 12; i++) if ((mask & (1 << (i - 1))) != 0) Signed(bytes, r.Values[i]);
                    entityState[r.Values[0]] = r.Values;
                }
                else { Var(bytes, (ulong)r.Values.Length); foreach (var value in r.Values) Signed(bytes, value); }
                if (r.Kind == ReplayKind.EntityExit && r.Values.Length > 0) entityState.Remove(r.Values[0]);
                if (r.Kind == ReplayKind.String)
                {
                    if (r.Text == null) throw new ArgumentException("Missing dictionary text.");
                    var text = Utf8.GetBytes(r.Text); if (text.Length > 4096) throw new ArgumentException("Dictionary text too long.");
                    Var(bytes, (ulong)text.Length); bytes.Write(text, 0, text.Length);
                }
                if (bytes.Length > MaxRecord) throw new InvalidDataException("Record too large.");
                Var(output, (ulong)bytes.Length); bytes.Position = 0; bytes.CopyTo(output);
            }
        }
        public static ReplayChunk Decode(byte[] packed)
        {
            if (packed == null || packed.Length == 0 || packed.Length > MaxCompressed) throw new InvalidDataException("Replay compressed size.");
            using (var raw = new MemoryStream())
            {
                using (var input = new MemoryStream(packed))
                using (var gzip = new GZipStream(input, CompressionMode.Decompress))
                {
                    var buffer = new byte[8192]; int n;
                    while ((n = gzip.Read(buffer, 0, buffer.Length)) != 0) { if (raw.Length + n > MaxRaw) throw new InvalidDataException("Replay expansion limit."); raw.Write(buffer, 0, n); }
                }
                raw.Position = 0;
                using (var reader = new BinaryReader(raw, Utf8, true))
                {
                    for (int i = 0; i < Magic.Length; i++) if (reader.ReadByte() != Magic[i]) throw new InvalidDataException("Replay magic.");
                    if (reader.ReadUInt16() != 1) throw new InvalidDataException("Unsupported container.");
                    int size = reader.ReadInt32(); if (size < 1 || size > 65536 || size > raw.Length - raw.Position) throw new InvalidDataException("Replay header length.");
                    var header = ReplayHeader.Decode(Utf8.GetString(reader.ReadBytes(size)));
                    var entityState = new Dictionary<long, long[]>();
                    var records = new List<ReplayRecord>(); long time = header.StartMs, sequence = header.FirstSequence - 1; bool snapshot = false, footer = false;
                    while (raw.Position < raw.Length)
                    {
                        int length = CheckedLength(ReadVar(raw), MaxRecord); long end = checked(raw.Position + length);
                        if (length == 0 || end > raw.Length) throw new InvalidDataException("Truncated record.");
                        // A bounded slice prevents a corrupt field from consuming the next record.
                        using (var part = new MemoryStream(reader.ReadBytes(length), false))
                        {
                            ulong kindNumber = ReadVar(part); if (kindNumber < 1 || kindNumber > 22) throw new InvalidDataException("Unknown record.");
                            var kind = (ReplayKind)kindNumber;
                            if (ReadVar(part) != 1) throw new InvalidDataException("Replay sequence gap.");
                            sequence = checked(sequence + 1); time = checked(time + CheckedLong(ReadVar(part)));
                            long tick = ReadSigned(part); int count = CheckedLength(ReadVar(part), 64); var values = new long[count];
                            for (int i = 0; i < count; i++) values[i] = ReadSigned(part);
                            if (kind == ReplayKind.SnapshotBegin) entityState.Clear();
                            if (kind == ReplayKind.Entity)
                            {
                                if (count < 2 || values[0] < 0 || values[1] < 0 || values[1] > 2047) throw new InvalidDataException("Entity delta mask.");
                                entityState.TryGetValue(values[0], out var old);
                                if (old == null && values[1] != 2047) throw new InvalidDataException("Missing entity baseline.");
                                var expanded = old == null ? new long[12] : (long[])old.Clone(); expanded[0] = values[0]; int cursor = 2;
                                for (int i = 1; i < 12; i++) if ((values[1] & (1 << (i - 1))) != 0) { if (cursor >= count) throw new InvalidDataException("Entity delta length."); expanded[i] = values[cursor++]; }
                                if (cursor != count) throw new InvalidDataException("Entity delta trailing fields.");
                                values = expanded; entityState[values[0]] = values;
                            }
                            if (kind == ReplayKind.EntityExit && count > 0) entityState.Remove(values[0]);
                            string text = null;
                            if (kind == ReplayKind.String) { int bytes = CheckedLength(ReadVar(part), 4096); if (bytes > part.Length - part.Position) throw new InvalidDataException("Dictionary length."); var b = new byte[bytes]; if (part.Read(b, 0, bytes) != bytes) throw new EndOfStreamException(); text = Utf8.GetString(b); }
                            if (part.Position != part.Length) throw new InvalidDataException("Trailing record data.");
                            var record = new ReplayRecord(kind, time, tick, values, text) { Sequence = sequence };
                            if (records.Count == 0 && kind != ReplayKind.SnapshotBegin) throw new InvalidDataException("Missing initial snapshot.");
                            if (kind == ReplayKind.SnapshotBegin) { if (snapshot) throw new InvalidDataException("Nested snapshot."); snapshot = true; }
                            if (kind == ReplayKind.SnapshotEnd) { if (!snapshot) throw new InvalidDataException("Snapshot end."); snapshot = false; }
                            if (kind == ReplayKind.Footer)
                            {
                                if (snapshot || count != 2 || values[0] != sequence - 1 || values[1] != records.Count || raw.Position != raw.Length) throw new InvalidDataException("Replay footer.");
                                footer = true;
                            }
                            records.Add(record); if (records.Count > MaxRecords + 1) throw new InvalidDataException("Replay record count.");
                        }
                    }
                    if (!footer || snapshot) throw new InvalidDataException("Incomplete replay.");
                    ReplayValidation.Validate(records);
                    return new ReplayChunk(header, records.AsReadOnly()) { RawBytes = checked((int)raw.Length) };
                }
            }
        }
        internal static void Var(Stream s, ulong v) { while (v >= 128) { s.WriteByte((byte)((v & 127) | 128)); v >>= 7; } s.WriteByte((byte)v); }
        internal static void Signed(Stream s, long v) => Var(s, unchecked((ulong)((v << 1) ^ (v >> 63))));
        internal static ulong ReadVar(Stream s)
        {
            ulong v = 0;
            for (int i = 0; i < 10; i++) { int b = s.ReadByte(); if (b < 0) throw new EndOfStreamException(); if (i == 9 && b > 1) throw new InvalidDataException("Integer overflow."); v |= (ulong)(b & 127) << (i * 7); if ((b & 128) == 0) { if (i > 0 && b == 0) throw new InvalidDataException("Noncanonical integer."); return v; } }
            throw new InvalidDataException("Integer overflow.");
        }
        internal static long ReadSigned(Stream s) { var v = ReadVar(s); return unchecked((long)(v >> 1) ^ -((long)v & 1)); }
        static int CheckedLength(ulong n, int max) { if (n > (ulong)max) throw new InvalidDataException("Replay length limit."); return (int)n; }
        static long CheckedLong(ulong n) { if (n > long.MaxValue) throw new InvalidDataException("Time overflow."); return (long)n; }
    }
    public static class ReplayValidation
    {
        public static void Validate(IReadOnlyList<ReplayRecord> records)
        {
            var strings = new HashSet<long>();
            void Reference(long id) { if (id < 0 || (id > 0 && !strings.Contains(id))) throw new InvalidDataException("Undeclared dictionary reference."); }
            foreach (var r in records)
            {
                var v = r.Values;
                switch (r.Kind)
                {
                    case ReplayKind.String: if (v.Length != 1 || v[0] <= 0 || !strings.Add(v[0]) || strings.Count > 8192) throw new InvalidDataException("Dictionary ID."); break;
                    case ReplayKind.SnapshotBegin: case ReplayKind.SnapshotEnd: if (v.Length != 0) throw new InvalidDataException("Snapshot payload."); break;
                    case ReplayKind.Entity: if (v.Length != 12 || v[0] < 0) throw new InvalidDataException("Entity layout."); break;
                    case ReplayKind.EntityExit: if (v.Length != 2) throw new InvalidDataException("Exit layout."); break;
                    case ReplayKind.Pointer: if (v.Length != 5 || v[1] < -1 || v[1] > 4096 || v[2] < -1 || v[2] > 4096) throw new InvalidDataException("Pointer layout."); break;
                    case ReplayKind.UiState: if (v.Length != 3) throw new InvalidDataException("UI state."); break;
                    case ReplayKind.UiActivate: if (v.Length != 2) throw new InvalidDataException("UI activate."); break;
                    case ReplayKind.UiRect: if (v.Length != 7) throw new InvalidDataException("UI rect."); break;
                    case ReplayKind.Input: if (v.Length != 7) throw new InvalidDataException("Input layout."); break;
                    case ReplayKind.Command: if (v.Length != 9) throw new InvalidDataException("Command layout."); break;
                    case ReplayKind.Hit: if (v.Length != 15) throw new InvalidDataException("Hit layout."); break;
                    case ReplayKind.Clock: if (v.Length != 8) throw new InvalidDataException("Clock layout."); break;
                    case ReplayKind.Run: if (v.Length != 5) throw new InvalidDataException("Run layout."); break;
                    case ReplayKind.Scene: case ReplayKind.Screen: if (v.Length != 2) throw new InvalidDataException("Scene/screen layout."); break;
                    case ReplayKind.Gap: if (v.Length != 3) throw new InvalidDataException("Gap layout."); break;
                    case ReplayKind.Layout: if (v.Length != 9) throw new InvalidDataException("Layout record."); break;
                    case ReplayKind.Weapon: if (v.Length != 9) throw new InvalidDataException("Weapon record."); break;
                    case ReplayKind.Growth: if (v.Length != 7) throw new InvalidDataException("Growth record."); break;
                    case ReplayKind.Economy: if (v.Length != 2) throw new InvalidDataException("Economy record."); break;
                }
                switch (r.Kind)
                {
                    case ReplayKind.Entity: Reference(v[7]); Reference(v[9]); ReplayNumbers.Unfloat(v[4]); ReplayNumbers.Unfloat(v[5]); ReplayNumbers.Unfloat(v[6]); ReplayNumbers.Unfloat(v[8]); break;
                    case ReplayKind.UiRect: case ReplayKind.UiActivate: case ReplayKind.Screen: Reference(v[0]); break;
                    case ReplayKind.Scene: Reference(v[0]); Reference(v[1]); break;
                    case ReplayKind.UiState: if (v[0] <= 2 || v[0] >= 10) Reference(v[1]); break;
                    case ReplayKind.Command: Reference(v[6]); Reference(v[8]); break;
                    case ReplayKind.Hit: Reference(v[9]); Reference(v[10]); break;
                    case ReplayKind.Run: Reference(v[0]); break;
                    case ReplayKind.Layout: Reference(v[1]); break;
                    case ReplayKind.Weapon: Reference(v[1]); Reference(v[2]); Reference(v[3]); break;
                    case ReplayKind.Growth:
                        if (v[0] == 1) { Reference(v[1]); Reference(v[2]); Reference(v[4]); Reference(v[5]); }
                        else if (v[0] == 2) { Reference(v[1]); Reference(v[2]); }
                        break;
                }
            }
        }
    }
}
