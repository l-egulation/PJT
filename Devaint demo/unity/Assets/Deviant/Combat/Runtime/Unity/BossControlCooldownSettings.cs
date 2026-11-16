using System;
using UnityEngine;

namespace Deviant.Combat
{
    // Generated from data/combat/boss_status_design.json by tools/combat/export_boss_control_cooldown.py.
    [Serializable]
    public sealed class BossControlCooldownSettings
    {
        public int schema;
        public float stunSeconds, rootSeconds;
        static BossControlCooldownSettings loaded;
        public static BossControlCooldownSettings Settings
        {
            get
            {
                if (loaded != null) return loaded;
                var asset = Resources.Load<TextAsset>("BossControlCooldown");
                if (!asset) throw new InvalidOperationException("Missing BossControlCooldown data.");
                var settings = JsonUtility.FromJson<BossControlCooldownSettings>(asset.text);
                if (settings.schema != 1 || !(settings.stunSeconds >= 0) || !(settings.rootSeconds >= 0) || float.IsInfinity(settings.stunSeconds) || float.IsInfinity(settings.rootSeconds))
                    throw new InvalidOperationException("Invalid boss control cooldown data.");
                return loaded = settings;
            }
        }
    }
}
