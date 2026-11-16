using System;
using System.Globalization;
using UnityEngine;

namespace Deviant.Combat
{
    [Serializable]
    public sealed class CombatDamageVariance
    {
        public int schema;
        public bool varyPlayerReceivedDamage;
        public float minimum, maximum;
        static CombatDamageVariance loaded;
        public static CombatDamageVariance Settings
        {
            get
            {
                if (loaded != null) return loaded;
                var asset = Resources.Load<TextAsset>("CombatDamageVariance");
                if (!asset) throw new InvalidOperationException("Missing CombatDamageVariance data.");
                var settings = JsonUtility.FromJson<CombatDamageVariance>(asset.text);
                if (settings.schema != 1 || !(settings.minimum > 0 && settings.minimum <= 1 && settings.maximum >= 1) || float.IsInfinity(settings.maximum))
                    throw new InvalidOperationException("Invalid combat damage variance bounds.");
                return loaded = settings;
            }
        }
        // The random factor is sampled once per accepted hit, before reduction/shields.
        public float Apply(float damage, float sample)
            => damage * Mathf.Lerp(minimum, maximum, sample);
        public static string Format(float appliedDamage)
            => Math.Round(appliedDamage, 0, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);
    }
}
