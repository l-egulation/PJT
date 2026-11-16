using System;
using System.Collections.Generic;

namespace Deviant.Equipment.Contracts
{
    // Existing test-profile slot values, not a new ink catalog or item identity.
    public enum StatInkKind { Empty, BasicDamage, SpecialDamage, BasicSpeed, SpecialCooldown }
    public sealed class WeaponStatSnapshot
    {
        public const int SlotCount = 5;
        public IReadOnlyList<StatInkKind> Slots { get; }
        public float BasicBonus { get; }
        public float SpecialBonus { get; }
        public float Speed { get; }
        public float Reduction { get; }
        public WeaponStatSnapshot(StatInkKind[] slots, float basicBonus, float specialBonus, float speed, float reduction)
        {
            if (slots == null || slots.Length != SlotCount) throw new ArgumentException("Five stat slots are required.", nameof(slots));
            Slots = Array.AsReadOnly((StatInkKind[])slots.Clone()); BasicBonus = basicBonus; SpecialBonus = specialBonus; Speed = speed; Reduction = reduction;
        }
    }
    public interface IWeaponStats
    {
        WeaponStatSnapshot Read();
        void SetSlot(int index, StatInkKind kind);
        void ReplaceSlots(StatInkKind[] slots);
        void Clear();
    }
}
