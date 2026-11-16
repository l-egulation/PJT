using System;
using Deviant.Equipment.Contracts;

namespace Deviant.Equipment
{
    public static partial class WeaponResources
    {
        public static IInkPalette CreateInkPalette() => new InkPalette();
        sealed class InkPalette : IInkPalette
        {
            readonly InkPartMount[] slots = new InkPartMount[InkPaletteSnapshot.SlotCount];
            public InkPaletteSnapshot Read() => new InkPaletteSnapshot(slots);
            public bool TrySet(int slot, string partId, bool duplicate, out string error)
            {
                error = null;
                if (slot < 0 || slot >= slots.Length) { error = "Invalid palette slot."; return false; }
                if (partId != null && string.IsNullOrWhiteSpace(partId)) { error = "Invalid part id."; return false; }
                for (int i = 0; i < slots.Length; i++)
                    if (i != slot && partId != null && slots[i]?.PartId == partId && (!duplicate || !slots[i].Duplicate))
                    { error = "This experimental part is limited to one per weapon."; return false; }
                if (slots[slot]?.PartId == partId) return true;
                // A replacement gets a fresh instance and fresh counters/cooldown.
                slots[slot] = partId == null ? null : new InkPartMount(partId, duplicate);
                return true;
            }
            public void Clear() => Array.Clear(slots, 0, slots.Length);
        }
    }
}
