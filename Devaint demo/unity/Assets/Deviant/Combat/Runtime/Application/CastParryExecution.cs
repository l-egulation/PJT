using Deviant.Combat.Contracts;

namespace Deviant.Combat.Execution
{
    public static class CastParryExecution
    {
        public static bool TryParry<T>(bool projectileAttack, ref T host) where T : struct, ICastParryHost
        {
            if (!host.Activated || host.Cancelled || !host.EffectExists || host.EffectType != "parry" ||
                host.Elapsed < 0 || host.Elapsed >= host.ParryWindow || !host.SourceExists) return false;
            if ((host.AttackOriginX - host.OwnerCenterX) * host.Direction < 0) return false;
            if (host is ICastParryClaimHost claim && !claim.TryClaimParry()) return false;
            // The incoming hit is blocked even when the source rejects Receive.
            // An entered response is not re-gated after synchronous callbacks.
            if (!projectileAttack) host.Receive(host.ParryDamage * host.DamageMultiplier);
            host.Record(projectileAttack);
            return true;
        }
    }
}
