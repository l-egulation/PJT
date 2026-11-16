using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Deviant.Definitions
{
    public readonly struct ChargeReleaseTiming
    {
        public float ReleaseTime { get; }
        public float Shift { get; }
        public float MotionEnd { get; }
        public float Damage { get; }
        internal ChargeReleaseTiming(float release, float shift, float end, float damage)
        { ReleaseTime = release; Shift = shift; MotionEnd = end; Damage = damage; }
    }
    // A charge policy snapshot, not a full skill/geometry/effect definition.
    public sealed class ChargeDefinition
    {
        public string Id { get; }
        public string ContentHash { get; }
        public float Minimum { get; }
        public float Maximum { get; }
        public float MinimumDamage { get; }
        public float MaximumDamage { get; }
        public float BaseEnd { get; }
        public bool HoldUntilRelease { get; }
        public float ReleaseWindup { get; }
        // 0: continuous. n >= 2: the hold range splits into n equal bands that release 0, 1/(n-1), ... 1 of the growth.
        public int Steps { get; }
        public ChargeDefinition(string id, float minimum, float maximum, float minimumDamage, float maximumDamage, float baseEnd, bool holdUntilRelease = false, float releaseWindup = 0, int steps = 0)
        {
            if (steps < 0 || steps == 1) throw new ArgumentOutOfRangeException(nameof(steps), "Charge steps are 0 (continuous) or at least 2.");
            if (string.IsNullOrWhiteSpace(id) || id.IndexOfAny(new[] { '\r', '\n' }) >= 0) throw new ArgumentException("A single-line charge identity is required.", nameof(id));
            if (!Finite(minimum) || minimum < 0 || !Finite(maximum) || maximum <= minimum || !Finite(maximum - minimum) ||
                !Finite(minimumDamage) || minimumDamage < 0 || !Finite(maximumDamage) || maximumDamage < 0 || !Finite(maximumDamage - minimumDamage) ||
                !Finite(baseEnd) || baseEnd < minimum || !Finite(baseEnd + (maximum - minimum))) throw new ArgumentOutOfRangeException(nameof(minimum), "Finite charge bounds, damage and recovery timing are required.");
            if (!Finite(releaseWindup) || releaseWindup < 0 || !Finite(baseEnd + releaseWindup + (maximum - minimum)))
                throw new ArgumentOutOfRangeException(nameof(releaseWindup));
            HoldUntilRelease = holdUntilRelease; ReleaseWindup = releaseWindup; Steps = steps;
            Id = id; Minimum = minimum; Maximum = maximum; MinimumDamage = minimumDamage; MaximumDamage = maximumDamage; BaseEnd = baseEnd;
            var canonical = new StringBuilder("charge-policy-v1\n").Append(Id).Append('\n');
            foreach (float value in new[] { Minimum, Maximum, MinimumDamage, MaximumDamage, BaseEnd }) canonical.Append(value.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
            if (HoldUntilRelease || ReleaseWindup > 0)
                canonical.Append(HoldUntilRelease ? "hold" : "auto").Append('\n').Append(ReleaseWindup.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
            if (Steps > 0) canonical.Append("steps\n").Append(Steps.ToString(CultureInfo.InvariantCulture)).Append('\n');
            using (var sha = SHA256.Create()) ContentHash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()))).Replace("-", "").ToLowerInvariant();
        }
        public ChargeReleaseTiming Resolve(float elapsed)
        {
            if (!Finite(elapsed)) throw new ArgumentOutOfRangeException(nameof(elapsed));
            float release = elapsed < Minimum ? Minimum : !HoldUntilRelease && elapsed > Maximum ? Maximum : elapsed;
            float shift = release - Minimum;
            float amount = Clamp01((release - Minimum) / (Maximum - Minimum));
            if (Steps >= 2) amount = Math.Min(Steps - 1, (int)Math.Floor(amount * Steps)) / (float)(Steps - 1);
            float damage = MinimumDamage + (MaximumDamage - MinimumDamage) * Clamp01(amount);
            return new ChargeReleaseTiming(release, shift, BaseEnd + shift + ReleaseWindup, damage);
        }
        static float Clamp01(float value) => value < 0 ? 0 : value > 1 ? 1 : value;
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
