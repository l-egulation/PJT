using System;
using System.Globalization;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Deviant.Combat;

namespace Deviant.Definitions
{
    // Fixed, uncharged, actor-anchored single-hit specials with no control or
    // movement/effect policy. More complex specials retain their own execution.
    public sealed class DirectMeleeSpecialDefinition
    {
        public static IReadOnlyList<string> SupportedIds { get; } = Array.AsReadOnly(new[] { "base_sword_default", "rapier_range", "greatsword_range" });
        readonly BasicMeleeDefinition hit;
        readonly string name, slot, description;
        public string FormId { get; }
        public string Id => hit.Id;
        public string ContentHash { get; }
        public string SourceVersion { get; }
        public float Cooldown { get; }
        public int Capacity { get; }
        public float PushSeconds { get; }
        public float Impact => hit.Impact;
        public float Active => hit.Active;
        public float Duration => hit.Duration;
        public float Damage => hit.Damage;
        DirectMeleeSpecialDefinition(string form, SkillData data, BasicMeleeDefinition hit)
        {
            FormId = form; this.hit = hit; name = data.name; slot = data.slot; description = data.description;
            Cooldown = data.cooldown; Capacity = data.capacity; PushSeconds = data.hits[0].pushSeconds;
            var canonical = new StringBuilder("direct-melee-special-v1\n").Append(hit.ContentHash).Append('\n');
            foreach (string text in new[] { FormId, name, slot, description }) canonical.Append(text?.Length ?? -1).Append(':').Append(text).Append('\n');
            canonical.Append(Capacity.ToString(CultureInfo.InvariantCulture)).Append('\n').Append(Cooldown.ToString("R", CultureInfo.InvariantCulture)).Append('\n').Append(PushSeconds.ToString("R", CultureInfo.InvariantCulture));
            using (var sha = SHA256.Create()) ContentHash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()))).Replace("-", "").ToLowerInvariant();
            var policy = new StringBuilder("direct-melee-policy-v1\n");
            foreach (string text in new[] { FormId, Id, name, slot, description }) policy.Append(text?.Length ?? -1).Append(':').Append(text).Append('\n');
            policy.Append(Capacity.ToString(CultureInfo.InvariantCulture)).Append('\n').Append(Cooldown.ToString("R", CultureInfo.InvariantCulture)).Append('\n').Append(PushSeconds.ToString("R", CultureInfo.InvariantCulture));
            using (var sha = SHA256.Create()) SourceVersion = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(policy.ToString()))).Replace("-", "").ToLowerInvariant();
        }
        public MeleeActionDefinition Compile() => new MeleeActionDefinition(hit, 1, 0, 0, hit.Duration, ContentHash);
        public BasicData ToBasicData() => hit.ToData();
        public DirectMeleeSpecialDefinition Recompile(BasicData numeric)
        {
            if (numeric == null) throw new ArgumentNullException(nameof(numeric));
            var source = ToData(); source.activation = numeric.impact; source.end = numeric.duration;
            source.hits[0].time = numeric.impact; source.hits[0].active = numeric.active; source.hits[0].damage = numeric.damage; source.hits[0].box = numeric.box;
            if (!TryCreate(FormId, source, out var result, out var error)) throw new ArgumentException(error);
            return result;
        }
        public SkillData ToData()
        {
            var box = hit.ToData().box;
            return new SkillData { id = Id, name = name, slot = slot, description = description, cooldown = Cooldown, capacity = Capacity,
                activation = Impact, end = Duration, inputSpeed = 0, vertical = "normal", facing = "windup_start", invulStart = new float[0], invulEnd = new float[0],
                hits = new[] { new HitData { time = Impact, active = Active, damage = Damage, box = box, anchor = "actor", targets = "all", pushSeconds = PushSeconds } } };
        }
        public static bool TryCreate(string form, SkillData data, out DirectMeleeSpecialDefinition definition, out string error)
        {
            definition = null; error = null;
            if (string.IsNullOrWhiteSpace(form) || data == null || string.IsNullOrWhiteSpace(data.id) || !data.id.StartsWith(form + "_", StringComparison.Ordinal)) error = "A matching form and special identity are required.";
            else if (!string.IsNullOrEmpty(data.kind) || data.unavailable || data.vertical != "normal" || data.facing != "windup_start" || data.inputSpeed != 0 ||
                data.chargeMin != 0 || data.chargeMax != 0 || data.chargeMinDamage != 0 || data.chargeMaxDamage != 0 ||
                data.basicLungeDistance != 0 || data.basicLungeStart != 0 || data.basicLungeDuration != 0 ||
                data.invulStart != null && data.invulStart.Length != 0 || data.invulEnd != null && data.invulEnd.Length != 0 ||
                !ActionTrackDocument.IsAbsent(data.actionTracks) || !Empty(data.movement) || !Empty(data.effect)) error = "The direct melee compiler requires a fixed normal/facing-locked special with no charge, tracks, movement, defense or effect policy.";
            else if (data.hits == null || data.hits.Length != 1 || data.hits[0] == null || data.hits[0].anchor != "actor" || data.hits[0].targets != "all" ||
                data.hits[0].root != 0 || data.hits[0].stun != 0 || data.hits[0].push != 0 || data.hits[0].pushWithCaster ||
                !Finite(data.hits[0].pushSeconds) || data.hits[0].pushSeconds < 0 || data.activation != data.hits[0].time) error = "One activation-aligned actor hit without additional control is required.";
            else if (!Finite(data.cooldown) || data.cooldown < 0 || data.capacity < 1) error = "Finite cooldown and positive capacity are required.";
            else if (data.id == "greatsword_range" && (data.hits[0].box == null || data.hits[0].box.x != 0)) error = "The existing greatsword range hit remains root-symmetric (X=0).";
            if (error != null) return false;
            var h = data.hits[0];
            if (!BasicMeleeDefinition.TryCreate(data.id, new BasicData { damage = h.damage, duration = data.end, impact = h.time, active = h.active, box = h.box }, out var hit, out error)) return false;
            definition = new DirectMeleeSpecialDefinition(form, data, hit); return true;
        }
        // JsonUtility can materialize absent inline DTOs as empty objects.
        static bool Empty(MoveData m) => m == null || string.IsNullOrEmpty(m.type) && string.IsNullOrEmpty(m.wallPolicy) && m.start == 0 && m.duration == 0 && m.distance == 0 && m.speed == 0 && m.wallRecovery == 0 && m.gap == 0 && m.recastWindow == 0 && m.targetRadius == 0 && !m.primaryPush && m.endDamage == 0 && Empty(m.targetBox);
        static bool Empty(EffectData e) => e == null || string.IsNullOrEmpty(e.type) && e.duration == 0 && e.hp == 0 && e.reduction == 0 && e.speed == 0 && e.distance == 0 && e.radius == 0 && e.x == 0 && e.y == 0 && Empty(e.box);
        static bool Empty(BoxData b) => b == null || b.width == 0 && b.height == 0 && b.x == 0 && b.y == 0;
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
