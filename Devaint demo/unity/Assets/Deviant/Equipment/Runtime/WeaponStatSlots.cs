using System;
using Deviant.Equipment.Contracts;

namespace Deviant.Equipment
{
    public static partial class WeaponResources
    {
        public static IWeaponStats CreateStats() => new StatSlots();
        public const double DamagePerInk = 1.2, SpeedPerInk = 1.1, CooldownKeptPerInk = .8;
        // Evaluated in double so a single ink stays exactly +20% / x1.1 / -20% after the float conversion.
        static double Compound(double perInk, int count) => Math.Pow(perInk, count);
        sealed class StatSlots : IWeaponStats
        {
            readonly StatInkKind[] slots = new StatInkKind[WeaponStatSnapshot.SlotCount];
            public WeaponStatSnapshot Read()
            {
                int basic = 0, special = 0, speed = 0, cooldown = 0;
                foreach (var kind in slots)
                {
                    if (kind == StatInkKind.BasicDamage) basic++;
                    else if (kind == StatInkKind.SpecialDamage) special++;
                    else if (kind == StatInkKind.BasicSpeed) speed++;
                    else if (kind == StatInkKind.SpecialCooldown) cooldown++;
                }
                // Same-kind inks multiply: damage x1.2 and basic speed x1.1 per ink, and each cooldown ink
                // leaves 80% of the remaining cooldown. The 80% reduction cap stays in Cooldown().
                return new WeaponStatSnapshot(slots, (float)(Compound(DamagePerInk, basic) - 1), (float)(Compound(DamagePerInk, special) - 1),
                    (float)Compound(SpeedPerInk, speed), (float)(1 - Compound(CooldownKeptPerInk, cooldown)));
            }
            public void SetSlot(int index, StatInkKind kind)
            {
                if (index < 0 || index >= slots.Length) throw new ArgumentOutOfRangeException(nameof(index));
                Validate(kind); slots[index] = kind;
            }
            public void ReplaceSlots(StatInkKind[] next)
            {
                if (next == null || next.Length != slots.Length) throw new ArgumentException("Five stat slots are required.", nameof(next));
                foreach (var kind in next) Validate(kind);
                Array.Copy(next, slots, slots.Length);
            }
            public void Clear() => Array.Clear(slots, 0, slots.Length);
            static void Validate(StatInkKind kind)
            { if (!Enum.IsDefined(typeof(StatInkKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind)); }
        }
    }
}
