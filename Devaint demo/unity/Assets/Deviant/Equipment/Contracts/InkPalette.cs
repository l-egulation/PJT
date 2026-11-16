using System;
using System.Collections.Generic;

namespace Deviant.Equipment.Contracts
{
    // Experimental six unrestricted slots; independent of the five stat slots.
    public sealed class InkPartMount
    {
        public Guid InstanceId { get; } = Guid.NewGuid();
        public string PartId { get; }
        public bool Duplicate { get; }
        public InkPartMount(string partId, bool duplicate)
        {
            if (string.IsNullOrWhiteSpace(partId)) throw new ArgumentException("A part id is required.");
            PartId = partId; Duplicate = duplicate;
        }
    }
    public sealed class InkPaletteSnapshot
    {
        public const int SlotCount = 6;
        public IReadOnlyList<InkPartMount> Slots { get; }
        public InkPaletteSnapshot(IList<InkPartMount> slots)
        {
            if (slots == null || slots.Count != SlotCount) throw new ArgumentException("Six experimental slots required.");
            var copy = new InkPartMount[SlotCount]; slots.CopyTo(copy, 0);
            Slots = Array.AsReadOnly(copy);
        }
    }
    public interface IInkPalette
    {
        InkPaletteSnapshot Read();
        bool TrySet(int slot, string partId, bool duplicate, out string error);
        void Clear();
    }
}
