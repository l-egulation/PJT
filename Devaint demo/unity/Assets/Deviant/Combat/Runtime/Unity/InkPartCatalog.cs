using System;
using UnityEngine;

namespace Deviant.Combat
{
    // Generated experimental content, separate from the display-only catalogue.
    public static class InkPartCatalog
    {
        [Serializable] internal sealed class Effect
        {
            public string op, stat, target, when;
            public float damage, ratio, delay, duration, interval, radius, cap, amount, distance, range, threshold, cost, seconds, damagePerStack, heal;
            public int count, stacks, minimum;
            public bool excludeTarget;
        }
        [Serializable] internal sealed class Part
        {
            public string id, condition, trigger, requiresStamp;
            public bool duplicate, oncePerAction;
            public int every;
            public float cooldown, conditionThreshold, conditionWindow;
            public Effect[] effects;
        }
        [Serializable] internal sealed class Tuning
        {
            public float waveSpeed, waveWidth, waveHeight, seedPickupRadius, seedLifetime, frostDuration,
                minimumActionSeconds, minimumChargeSeconds, chargeSeconds, chargeRecovery, controlRecoverySeconds,
                maxControlSeconds, markDuration, shieldCap, maxSlow, maxDamageReduction, chargeStoreCap,
                chargeImpactSeconds, chargeActiveSeconds, chargeHeight, maxWindupReduction, maxRecoveryReduction;
            public int markCap;
        }
        [Serializable] internal sealed class ChargeProfile { public string form; public float damage, reach; }
        [Serializable] sealed class Document { public Part[] parts; public Tuning tuning; public ChargeProfile[] chargeProfiles; }
        static Part[] parts;
        static Document document;
        internal static Tuning Settings { get { Find(""); return document.tuning; } }
        internal static ChargeProfile Profile(string form) { Find(""); return Array.Find(document.chargeProfiles,p=>p.form==form); }
        internal static Part Find(string id)
        {
            if (parts == null)
            {
                var resource = Resources.Load<TextAsset>("RuntimeInkParts");
                if (!resource) throw new InvalidOperationException("Generated experimental ink content is missing.");
                document = JsonUtility.FromJson<Document>(resource.text); parts = document.parts;
            }
            return Array.Find(parts, p => p.id == id);
        }
        public static bool Implemented(string id) => id != null && Find(id) != null;
        public static int ImplementedCount { get { Find(""); return parts.Length; } }
    }
    public partial class CombatPlayer
    {
        public bool TrySetInkPart(int weaponSlot, int paletteSlot, string id, out string error)
        {
            error = null;
            if (arena == null || !EditingAllowed || weaponSlot < 0 || weaponSlot >= weapons.Count)
            { error = "장비를 편집할 수 없습니다."; return false; }
            var part = id == null ? null : InkPartCatalog.Find(id);
            if (id != null && part == null) { error = id=="g_armor"?"강철 주입은 방어력 체계 결정 전까지 보류합니다.":"아직 효과가 구현되지 않은 파츠입니다."; return false; }
            return weapons[weaponSlot].InkPalette.TrySet(paletteSlot, id, part?.duplicate ?? true, out error);
        }
    }
}
