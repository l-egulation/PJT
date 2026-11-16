using System;

namespace Deviant.Equipment.Contracts
{
    // Compatibility indices into the current test-profile catalog. These are
    // not persistent content/item identifiers or an inventory schema.
    public readonly struct WeaponSelection
    {
        public int FormIndex { get; }
        public int SpecialIndex { get; }
        public WeaponSelection(int formIndex, int specialIndex)
        {
            if (formIndex < 0 || specialIndex < 0) throw new ArgumentOutOfRangeException(nameof(formIndex));
            FormIndex = formIndex; SpecialIndex = specialIndex;
        }
    }
    public sealed class WeaponSnapshot
    {
        public Guid InstanceId { get; }
        public WeaponSelection Selection { get; }
        public WeaponStatSnapshot Stats { get; }
        public ChargeState Charges { get; }
        public InkPaletteSnapshot InkPalette { get; }
        public bool ExperimentalChargeStamp { get; }
        public WeaponSnapshot(Guid instanceId, WeaponSelection selection, WeaponStatSnapshot stats, ChargeState charges)
            : this(instanceId, selection, stats, charges, null) { }
        public WeaponSnapshot(Guid instanceId, WeaponSelection selection, WeaponStatSnapshot stats, ChargeState charges, InkPaletteSnapshot inkPalette, bool experimentalChargeStamp = false)
        {
            if (instanceId == Guid.Empty || stats == null) throw new ArgumentException("A weapon identity and immutable stats are required.");
            InstanceId = instanceId; Selection = selection; Stats = stats; Charges = charges; InkPalette = inkPalette; ExperimentalChargeStamp = experimentalChargeStamp;
        }
    }
    public interface IWeaponInstance
    {
        Guid InstanceId { get; }
        WeaponSelection ReadSelection();
        void Select(WeaponSelection selection);
        WeaponSnapshot Reconfigure(WeaponConfiguration configuration, float now);
        IWeaponStats Stats { get; }
        IInkPalette InkPalette { get; }
        bool ExperimentalChargeStamp { get; set; }
        IWeaponCharges Charges { get; }
        WeaponSnapshot Read();
    }
}
