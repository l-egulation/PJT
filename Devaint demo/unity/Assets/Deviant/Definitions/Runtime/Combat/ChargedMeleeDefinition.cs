using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Deviant.Combat;

namespace Deviant.Definitions
{
    [Serializable] public sealed class ChargedMeleeNumericData
    {
        public float minimum, maximum, minimumDamage, maximumDamage, active, baseEnd;
        public BoxData box;
    }
    // The two current charged area-hit melee skills. A direct-melee value
    // snapshot supplies fixed geometry/metadata; ChargeDefinition owns release.
    public sealed class ChargedMeleeDefinition
    {
        public static IReadOnlyList<string> SupportedIds { get; } = Array.AsReadOnly(new[] { "rapier_default", "greatsword_default" });
        readonly DirectMeleeSpecialDefinition fixedPart;
        public string Id => fixedPart.Id;
        public string FormId => fixedPart.FormId;
        public ChargeDefinition Policy { get; }
        public string ContentHash { get; }
        public string SourceVersion => fixedPart.SourceVersion;
        public float Cooldown => fixedPart.Cooldown;
        public int Capacity => fixedPart.Capacity;
        ChargedMeleeDefinition(DirectMeleeSpecialDefinition fixedPart, ChargeDefinition policy)
        {
            this.fixedPart = fixedPart; Policy = policy;
            string canonical = "charged-melee-v2\n" + fixedPart.ContentHash + "\n" + policy.ContentHash + "\nall";
            using (var sha = SHA256.Create()) ContentHash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical))).Replace("-", "").ToLowerInvariant();
        }
        public HitData CreateHit()
        {
            var hit = fixedPart.ToData().hits[0]; hit.targets = "all"; return hit;
        }
        public SkillData ToData()
        {
            var data = fixedPart.ToData(); data.hits[0].targets = "all";
            data.chargeMin = Policy.Minimum; data.chargeMax = Policy.Maximum;
            data.chargeMinDamage = Policy.MinimumDamage; data.chargeMaxDamage = Policy.MaximumDamage;
            data.chargeHoldUntilRelease = Policy.HoldUntilRelease; data.chargeReleaseWindup = Policy.ReleaseWindup;
            return data;
        }
        public ChargedMeleeNumericData ToNumericData() => new ChargedMeleeNumericData {
            minimum = Policy.Minimum, maximum = Policy.Maximum, minimumDamage = Policy.MinimumDamage, maximumDamage = Policy.MaximumDamage,
            active = fixedPart.Active, baseEnd = Policy.BaseEnd, box = fixedPart.ToBasicData().box
        };
        public ChargedMeleeDefinition Recompile(ChargedMeleeNumericData numeric)
        {
            if (numeric == null) throw new ArgumentNullException(nameof(numeric));
            var data = ToData(); data.activation = data.chargeMin = data.hits[0].time = numeric.minimum;
            data.chargeMax = numeric.maximum; data.chargeMinDamage = data.hits[0].damage = numeric.minimumDamage; data.chargeMaxDamage = numeric.maximumDamage;
            data.end = numeric.baseEnd; data.hits[0].active = numeric.active; data.hits[0].box = numeric.box;
            if (!TryCreate(FormId, data, out var result, out var error)) throw new ArgumentException(error);
            return result;
        }
        public static bool TryCreate(string form, SkillData data, out ChargedMeleeDefinition definition, out string error)
        {
            definition = null; error = null;
            if (!ActionTrackDocument.IsAbsent(data?.actionTracks) || data?.hits == null || data.hits.Length != 1 || data.hits[0] == null || data.hits[0].targets != "all" ||
                data.activation != data.chargeMin || data.hits[0].time != data.chargeMin || data.hits[0].damage != data.chargeMinDamage)
            { error = "A minimum-aligned charged area melee hit is required."; return false; }
            ChargeDefinition policy;
            try { policy = new ChargeDefinition(data.id, data.chargeMin, data.chargeMax, data.chargeMinDamage, data.chargeMaxDamage, data.end, data.chargeHoldUntilRelease, data.chargeReleaseWindup); }
            catch (ArgumentException exception) { error = exception.Message; return false; }
            var h = data.hits[0];
            // Reuse direct melee geometry/policy validation without changing the
            // actual charged area-hit definition or publishing this DTO.
            var fixedData = new SkillData { kind = data.kind, bomb = data.bomb, ranged = data.ranged, missile = data.missile, unavailable = data.unavailable,
                id = data.id, name = data.name, slot = data.slot, description = data.description, cooldown = data.cooldown, capacity = data.capacity,
                activation = data.activation, end = data.end, inputSpeed = data.inputSpeed, vertical = data.vertical, facing = data.facing,
                invulStart = data.invulStart, invulEnd = data.invulEnd, movement = data.movement, effect = data.effect,
                basicLungeDistance = data.basicLungeDistance, basicLungeStart = data.basicLungeStart, basicLungeDuration = data.basicLungeDuration,
                hits = new[] { new HitData { time = h.time, damage = h.damage, active = h.active, anchor = h.anchor, targets = "all", box = h.box,
                    root = h.root, stun = h.stun, push = h.push, pushSeconds = h.pushSeconds, pushWithCaster = h.pushWithCaster } } };
            if (!DirectMeleeSpecialDefinition.TryCreate(form, fixedData, out var fixedPart, out error)) return false;
            definition = new ChargedMeleeDefinition(fixedPart, policy); return true;
        }
    }
}
