using System;
using Deviant.Combat.Contracts;

namespace Deviant.Combat
{
    // Initial adapter over the existing test-profile weapon state. Equipment
    // can later implement the same source without a Combat cooldown replica.
    public sealed class WeaponActionResourceSource : IActionResourceSource
    {
        readonly WeaponState weapon;
        public WeaponActionResourceSource(WeaponState weapon) { this.weapon = weapon ?? throw new ArgumentNullException(nameof(weapon)); }
        public ActionResourceResult TryActivate(ActionResourceKey key, float now)
        {
            if (key.ExecutionId == Guid.Empty || key.WeaponId != weapon.InstanceId || float.IsNaN(now) || float.IsInfinity(now) || now < 0)
                return new ActionResourceResult(ActionResourceStatus.Rejected, reason: "Weapon activation identity/time is invalid.");
            // Validate/capture before spending. Basic speed already belongs to the
            // action's start definition; only activation-time damage is captured.
            var snapshot = new ActionResourceSnapshot(key, now, 1 + (key.IsBasic ? weapon.BasicBonus : weapon.SpecialBonus));
            if (!key.IsBasic && !weapon.Cooldown.Spend(now))
                return new ActionResourceResult(ActionResourceStatus.Rejected, reason: "The weapon has no available special charge.");
            return new ActionResourceResult(ActionResourceStatus.Succeeded, snapshot);
        }
    }
}
