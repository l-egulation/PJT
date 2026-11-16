using System;
using System.Globalization;

namespace Deviant.Replay
{
    // RFC 9562: canonical text byte order, independent of Guid.ToByteArray's mixed endianness.
    public sealed class ReplayUuid7
    {
        const string Hex = "0123456789abcdef";
        const long MaxTimestamp = 0xffffffffffffL;
        static readonly ReplayUuid7 Shared = new ReplayUuid7();
        readonly object gate = new object();
        readonly Func<DateTimeOffset> utcNow;
        readonly Func<Guid> random;
        long lastTimestamp = -1;
        char[] previous;

        public ReplayUuid7(Func<DateTimeOffset> utcNow = null, Func<Guid> random = null)
        { this.utcNow = utcNow ?? (() => DateTimeOffset.UtcNow); this.random = random ?? Guid.NewGuid; }
        public static string NewId() => Shared.Next();

        public string Next()
        {
            lock (gate)
            {
                long timestamp = utcNow().ToUnixTimeMilliseconds();
                if (timestamp < 0 || timestamp > MaxTimestamp) throw new ArgumentOutOfRangeException(nameof(timestamp));
                char[] id;
                if (previous != null && timestamp <= lastTimestamp)
                {
                    timestamp = lastTimestamp;
                    id = (char[])previous.Clone();
                    // Increment the 74 random bits, skipping version and the two fixed variant bits.
                    if (Increment(id)) { previous = id; return new string(id); }
                    if (timestamp == MaxTimestamp) throw new InvalidOperationException("UUIDv7 timestamp exhausted.");
                    timestamp++;
                }
                id = random().ToString("N").ToCharArray();
                string time = timestamp.ToString("x12", CultureInfo.InvariantCulture);
                time.CopyTo(0, id, 0, 12);
                id[12] = '7';
                id[16] = Hex[8 | (Hex.IndexOf(id[16]) & 3)];
                previous = id; lastTimestamp = timestamp;
                return new string(id);
            }
        }
        static bool Increment(char[] id)
        {
            for (int i = 31; i >= 13; i--)
            {
                int minimum = i == 16 ? 8 : 0, maximum = i == 16 ? 11 : 15;
                int value = Hex.IndexOf(id[i]);
                if (value < maximum) { id[i] = Hex[value + 1]; return true; }
                id[i] = Hex[minimum];
            }
            return false;
        }
        public static bool TryGetUtc(string value, out DateTimeOffset utc)
        {
            utc = default;
            if (!Guid.TryParse(value, out var guid)) return false;
            string id = guid.ToString("N");
            if (id[12] != '7' || Hex.IndexOf(id[16]) < 8 || Hex.IndexOf(id[16]) > 11) return false;
            long ms = long.Parse(id.Substring(0, 12), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            try { utc = DateTimeOffset.FromUnixTimeMilliseconds(ms); return true; }
            catch (ArgumentOutOfRangeException) { return false; }
        }
    }
}
