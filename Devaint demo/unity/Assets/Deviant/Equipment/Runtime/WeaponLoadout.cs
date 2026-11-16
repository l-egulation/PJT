using System;
using System.Collections.Generic;
using Deviant.Equipment.Contracts;

namespace Deviant.Equipment
{
    public static partial class WeaponResources
    {
        public static IWeaponLoadout CreateLoadout() => CreateLoadout(new[] { CreateWeapon(), CreateWeapon() });
        public static IWeaponLoadout CreateLoadout(IReadOnlyList<IWeaponInstance> weapons, int activeSlot = 0) => new WeaponLoadout(weapons, activeSlot);

        sealed class WeaponLoadout : IWeaponLoadout, IWeaponLoadoutReplacement
        {
            readonly IWeaponInstance[] weapons;
            public int ActiveSlot { get; private set; }

            public WeaponLoadout(IReadOnlyList<IWeaponInstance> source, int activeSlot)
            {
                if (source == null || source.Count != WeaponLoadoutSnapshot.SlotCount) throw new ArgumentException("Two equipped weapons are required.", nameof(source));
                ValidateSlot(activeSlot);
                weapons = new IWeaponInstance[WeaponLoadoutSnapshot.SlotCount];
                for (int i = 0; i < weapons.Length; i++)
                {
                    var weapon = source[i];
                    if (weapon == null || weapon.InstanceId == Guid.Empty) throw new ArgumentException("An equipped weapon identity is missing.", nameof(source));
                    weapons[i] = weapon;
                }
                if (weapons[0].InstanceId == weapons[1].InstanceId) throw new ArgumentException("Equipped weapons must have distinct identities.", nameof(source));
                ActiveSlot = activeSlot;
            }

            static void ValidateSlot(int slot)
            {
                if (slot < 0 || slot >= WeaponLoadoutSnapshot.SlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
            }
            public IWeaponInstance GetWeapon(int slot) { ValidateSlot(slot); return weapons[slot]; }
            public void SelectSlot(int slot) { ValidateSlot(slot); ActiveSlot = slot; }
            public void ReplaceSlot(int slot, IWeaponInstance weapon)
            {
                ValidateSlot(slot);
                if (weapon == null || weapon.InstanceId == weapons[1-slot].InstanceId) throw new ArgumentException("Invalid replacement weapon.", nameof(weapon));
                weapons[slot] = weapon;
            }
            public bool TryReplace(int slot, Guid expectedOldId, IWeaponInstance next)
            {
                if (slot < 0 || slot >= weapons.Length || next == null || next.InstanceId == Guid.Empty ||
                    weapons[slot].InstanceId != expectedOldId || weapons[1 - slot].InstanceId == next.InstanceId) return false;
                weapons[slot] = next; return true;
            }
            public void SwapSlots()
            {
                (weapons[0], weapons[1]) = (weapons[1], weapons[0]);
                ActiveSlot = 1 - ActiveSlot;
            }
            public WeaponLoadoutSnapshot Read() => new WeaponLoadoutSnapshot(ActiveSlot, new[] { weapons[0].Read(), weapons[1].Read() });
        }
    }
}
