using System;
using System.Collections.Generic;

namespace Deviant.Equipment.Contracts
{
    // The currently equipped profile pair. Inventory, replacement, and Run
    // persistence are separate commands/policies, not part of this contract.
    public sealed class WeaponLoadoutSnapshot
    {
        public const int SlotCount = 2;
        public int ActiveSlot { get; }
        public IReadOnlyList<WeaponSnapshot> Weapons { get; }
        public WeaponSnapshot ActiveWeapon => Weapons[ActiveSlot];

        public WeaponLoadoutSnapshot(int activeSlot, IReadOnlyList<WeaponSnapshot> weapons)
        {
            if (activeSlot < 0 || activeSlot >= SlotCount) throw new ArgumentOutOfRangeException(nameof(activeSlot));
            if (weapons == null || weapons.Count != SlotCount) throw new ArgumentException("Two equipped weapons are required.", nameof(weapons));
            var copy = new WeaponSnapshot[SlotCount];
            for (int i = 0; i < copy.Length; i++)
                copy[i] = weapons[i] ?? throw new ArgumentException("An equipped weapon snapshot is missing.", nameof(weapons));
            if (copy[0].InstanceId == copy[1].InstanceId) throw new ArgumentException("Equipped weapons must have distinct identities.", nameof(weapons));
            ActiveSlot = activeSlot; Weapons = Array.AsReadOnly(copy);
        }
    }

    public interface IWeaponLoadout
    {
        int ActiveSlot { get; }
        IWeaponInstance GetWeapon(int slot);
        void SelectSlot(int slot);
        // Reorder the pair while keeping the same active weapon and its resources.
        void SwapSlots();
        void ReplaceSlot(int slot, IWeaponInstance weapon);
        WeaponLoadoutSnapshot Read();
    }
    // Acquiring another physical weapon is distinct from reconfiguring the same weapon's form/skill.
    public interface IWeaponLoadoutReplacement
    {
        bool TryReplace(int slot, Guid expectedOldId, IWeaponInstance next);
    }
}
