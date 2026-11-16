using System;
using UnityEngine;

namespace Deviant.Combat
{
    [Serializable]
    public sealed class ShieldRuntimeRules
    {
        public int schemaVersion;
        public float parryProjectileReach, decoyDamage, decoyRadius;
        public float parrySuccessCooldownSeconds, parrySuccessRecoverySeconds;
        public bool decoyBurstOnExpiry, decoyBurstOnDamageDeath, decoyBurstOnReplacement;
        static ShieldRuntimeRules current;
        public static ShieldRuntimeRules Current
        {
            get
            {
                if (current != null) return current;
                var text = Resources.Load<TextAsset>("ShieldRuntimeRules");
                if (!text) throw new InvalidOperationException("ShieldRuntimeRules is missing.");
                var value = JsonUtility.FromJson<ShieldRuntimeRules>(text.text);
                if (value.schemaVersion != 1 || !(value.parryProjectileReach >= 0 && value.parryProjectileReach <= 1) ||
                    !(value.decoyDamage > 0) || !(value.decoyRadius > 0) || value.decoyBurstOnReplacement ||
                    !(value.parrySuccessCooldownSeconds > 0 && value.parrySuccessCooldownSeconds <= 1.2f) ||
                    !(value.parrySuccessRecoverySeconds > 0 && value.parrySuccessRecoverySeconds <= .45f))
                    throw new InvalidOperationException("Invalid approved shield rules.");
                return current = value;
            }
        }
    }
}
