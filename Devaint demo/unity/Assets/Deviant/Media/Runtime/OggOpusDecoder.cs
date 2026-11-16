using System;
using System.Collections.Generic;
using System.IO;
using Concentus.Structs;

namespace Deviant.Media
{
    // The compression recipe emits one Ogg Opus stream, mapping family 0, mono/stereo, at 48kHz.
    // Pure managed decoding also works in IL2CPP Web builds without threads or native plug-ins.
    public static class OggOpusDecoder
    {
        public sealed class Pcm
        {
            public readonly float[] Samples;
            public readonly int Channels;
            public int Frames => Samples.Length / Channels;
            public const int Frequency = 48000;
            public Pcm(float[] samples, int channels) { Samples = samples; Channels = channels; }
        }
        struct Page { public int Offset, Segments, Body; public byte Flags; public ulong Granule; }
        const int MaxEncoded = 64 * 1024 * 1024, MaxSamples = 32 * 1024 * 1024, MaxPacket = 65536;
        static readonly uint[] CrcTable = MakeCrcTable();

        static uint[] MakeCrcTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint v = i << 24;
                for (int b = 0; b < 8; b++) v = (v & 0x80000000) != 0 ? (v << 1) ^ 0x04c11db7 : v << 1;
                table[i] = v;
            }
            return table;
        }
        static uint U32(byte[] b, int p) => (uint)(b[p] | b[p + 1] << 8 | b[p + 2] << 16 | b[p + 3] << 24);
        static ulong U64(byte[] b, int p) => U32(b, p) | (ulong)U32(b, p + 4) << 32;
        static bool Magic(byte[] b, int p, string s)
        {
            if (p < 0 || p > b.Length - s.Length) return false;
            for (int i = 0; i < s.Length; i++) if (b[p + i] != s[i]) return false;
            return true;
        }

        public static Pcm Decode(byte[] data)
        {
            if (data == null || data.Length < 47 || data.Length > MaxEncoded) throw new InvalidDataException("Invalid Opus size");
            var pages = new List<Page>();
            int at = 0; uint serial = 0, sequence = 0;
            while (at < data.Length)
            {
                if (data.Length - at < 27 || !Magic(data, at, "OggS") || data[at + 4] != 0) throw new InvalidDataException("Invalid Ogg page");
                int segments = data[at + 26], body = at + 27 + segments;
                if (body > data.Length) throw new InvalidDataException("Truncated Ogg lacing");
                int end = body;
                for (int i = 0; i < segments; i++) end += data[at + 27 + i];
                if (end > data.Length) throw new InvalidDataException("Truncated Ogg body");
                if (pages.Count == 0) serial = U32(data, at + 14);
                if (U32(data, at + 14) != serial || U32(data, at + 18) != sequence++) throw new InvalidDataException("Chained or unordered Ogg stream");
                uint crc = 0;
                for (int i = at; i < end; i++)
                {
                    byte value = i >= at + 22 && i < at + 26 ? (byte)0 : data[i];
                    crc = (crc << 8) ^ CrcTable[((crc >> 24) ^ value) & 255];
                }
                if (crc != U32(data, at + 22)) throw new InvalidDataException("Ogg checksum mismatch");
                byte flags = data[at + 5];
                if ((flags & ~7) != 0 || (pages.Count == 0 ? (flags & 2) == 0 : (flags & 2) != 0)
                    || (flags & 4) != 0 && end != data.Length) throw new InvalidDataException("Invalid Ogg page flags");
                pages.Add(new Page { Offset = at, Segments = segments, Body = body, Flags = flags, Granule = U64(data, at + 6) });
                at = end;
            }
            if (pages.Count < 3 || (pages[pages.Count - 1].Flags & 4) == 0) throw new InvalidDataException("Ogg stream has no end");
            ulong last = pages[pages.Count - 1].Granule;
            var packet = new byte[MaxPacket]; int packetLength = 0, packetNumber = 0, channels = 0, skip = 0;
            long decoded = 0; float gain = 1; float[] output = null, scratch = null;
            OpusDecoder decoder = null;
            try
            {
                foreach (var page in pages)
                {
                    if (((page.Flags & 1) != 0) != (packetLength != 0)) throw new InvalidDataException("Invalid Ogg packet continuation");
                    int cursor = page.Body;
                    for (int i = 0; i < page.Segments; i++)
                    {
                        int n = data[page.Offset + 27 + i];
                        if (packetLength > packet.Length - n) throw new InvalidDataException("Oversized Opus packet");
                        Buffer.BlockCopy(data, cursor, packet, packetLength, n); packetLength += n; cursor += n;
                        if (n == 255) continue;
                        if (packetNumber == 0)
                        {
                            if (packetLength != 19 || !Magic(packet, 0, "OpusHead") || packet[8] != 1
                                || packet[9] < 1 || packet[9] > 2 || packet[18] != 0) throw new InvalidDataException("Unsupported Opus header");
                            channels = packet[9]; skip = packet[10] | packet[11] << 8;
                            if (last <= (ulong)skip || last - (ulong)skip > (ulong)(MaxSamples / channels)) throw new InvalidDataException("Opus duration exceeds decoded limit");
                            output = new float[checked((int)(last - (ulong)skip) * channels)];
                            scratch = new float[5760 * channels];
                            short q8 = (short)(packet[16] | packet[17] << 8);
                            gain = (float)Math.Pow(10, q8 / (256.0 * 20));
                            decoder = new OpusDecoder(Pcm.Frequency, channels);
                        }
                        else if (packetNumber == 1)
                        {
                            if (packetLength < 8 || !Magic(packet, 0, "OpusTags")) throw new InvalidDataException("Missing Opus tags");
                        }
                        else
                        {
                            int frames = decoder.Decode(packet, 0, packetLength, scratch, 0, 5760, false);
                            if (frames <= 0) throw new InvalidDataException("Invalid Opus frame");
                            long start = Math.Max(decoded, skip), end = Math.Min(decoded + frames, (long)last);
                            if (end > start)
                            {
                                int source = checked((int)(start - decoded) * channels), target = checked((int)(start - skip) * channels);
                                int count = checked((int)(end - start) * channels);
                                if (gain == 1) Array.Copy(scratch, source, output, target, count);
                                else for (int k = 0; k < count; k++) output[target + k] = scratch[source + k] * gain;
                            }
                            decoded += frames;
                        }
                        packetLength = 0; packetNumber++;
                    }
                }
                if (packetLength != 0 || packetNumber < 3 || decoded < (long)last) throw new InvalidDataException("Truncated Opus audio");
                return new Pcm(output, channels);
            }
            finally { decoder?.Dispose(); }
        }
    }
}
