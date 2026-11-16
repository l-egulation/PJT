using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Deviant.Combat;

namespace Deviant.Definitions
{
    // Initial action timeline: one actor-anchored melee hit and the existing
    // input-driven ground advance. Other action policies remain on their runner.
    public sealed class MeleeActionDefinition
    {
        readonly BasicMeleeDefinition source;
        public string Id => source.Id;
        public string SourceVersion { get; }
        public string ContentHash { get; }
        public float Speed { get; }
        public float Impact { get; }
        public float Active { get; }
        public float End { get; }
        public float AdvanceDistance { get; }
        public float AdvanceStart { get; }
        public float AdvanceDuration { get; }
        // Existing melee-basic policy, shared by gameplay construction and
        // read-only authoring previews. inputSpeed is the legacy movement flag.
        public static MeleeActionDefinition ForBasic(BasicMeleeDefinition source, float speed, float inputSpeed)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new MeleeActionDefinition(source, speed, inputSpeed > 0 ? .65f : 0,
                Math.Max(0, source.Impact - .04f) / speed, .08f / speed);
        }
        public MeleeActionDefinition(BasicMeleeDefinition source, float speed, float advanceDistance, float advanceStart, float advanceDuration)
            : this(source, speed, advanceDistance, advanceStart, advanceDuration, null) { }
        internal MeleeActionDefinition(BasicMeleeDefinition source, float speed, float advanceDistance, float advanceStart, float advanceDuration, string sourceVersion, float extraRecovery = 0)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            SourceVersion = sourceVersion ?? source.ContentHash;
            if (!Finite(speed) || speed <= 0 || !Finite(advanceDistance) || advanceDistance < 0 || !Finite(advanceStart) || advanceStart < 0 || !Finite(advanceDuration) || advanceDuration <= 0 || !Finite(advanceStart + advanceDuration))
                throw new ArgumentOutOfRangeException(nameof(speed), "Finite positive speed/duration and nonnegative advance values are required.");
            Speed = speed; Impact = source.Impact / speed; Active = source.Active / speed; End = source.Duration / speed + extraRecovery;
            if (!Finite(Impact) || !Finite(Active) || Active <= 0 || !Finite(End) || End <= 0)
                throw new ArgumentOutOfRangeException(nameof(speed), "Scaled melee timing must remain finite and positive.");
            AdvanceDistance = advanceDistance; AdvanceStart = advanceStart; AdvanceDuration = advanceDuration;
            var canonical = new StringBuilder("melee-action-v1\n").Append(SourceVersion).Append('\n');
            foreach (float value in new[] { Speed, Impact, Active, End, AdvanceDistance, AdvanceStart, AdvanceDuration }) canonical.Append(value.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
            using (var sha = SHA256.Create()) ContentHash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()))).Replace("-", "").ToLowerInvariant();
        }
        // Detached trial timing: the authored action and its hit windows remain unchanged.
        public MeleeActionDefinition WithExtraRecovery(float seconds)
        {
            if (!Finite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            return new MeleeActionDefinition(source, Speed, AdvanceDistance, AdvanceStart, AdvanceDuration, SourceVersion,
                End - source.Duration / Speed + seconds);
        }
        // A private adapter copy, independent of the public compatibility DTO.
        public HitData CreateHit() => new HitData {
            time = Impact, active = Active, damage = source.Damage, anchor = "actor", targets = "all",
            box = new BoxData { width = source.Width, height = source.Height, x = source.OffsetX, y = source.OffsetY }
        };
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
