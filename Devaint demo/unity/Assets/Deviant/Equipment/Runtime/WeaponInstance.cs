using System;
using Deviant.Equipment.Contracts;

namespace Deviant.Equipment
{
    public static partial class WeaponResources
    {
        public static IWeaponInstance CreateWeapon() => CreateWeapon(CreateStats());
        public static IWeaponInstance CreateWeapon(IWeaponStats stats) => new WeaponInstance(stats ?? throw new ArgumentNullException(nameof(stats)));
        public static IWeaponInstance CreateWeapon(Guid id) => new WeaponInstance(CreateStats(), id);
        sealed class WeaponInstance : IWeaponInstance
        {
            WeaponSelection selection;
            bool configuring;
            public Guid InstanceId { get; }
            public IWeaponStats Stats { get; }
            public IInkPalette InkPalette { get; } = CreateInkPalette();
            public bool ExperimentalChargeStamp { get; set; }
            public IWeaponCharges Charges { get; } = CreateCharges(1, 5);
            public WeaponInstance(IWeaponStats stats, Guid id = default) { Stats = stats; InstanceId = id == Guid.Empty ? Guid.NewGuid() : id; }
            public WeaponSelection ReadSelection() => selection;
            public void Select(WeaponSelection next)
            {
                if (configuring) throw new InvalidOperationException("Weapon configuration cannot reenter.");
                selection = next;
            }
            public WeaponSnapshot Reconfigure(WeaponConfiguration configuration, float now)
            {
                if (configuration == null) throw new ArgumentNullException(nameof(configuration));
                if (float.IsNaN(now) || float.IsInfinity(now) || now < 0) throw new ArgumentOutOfRangeException(nameof(now));
                if (configuring) throw new InvalidOperationException("Weapon configuration cannot reenter.");
                configuring = true;
                try
                {
                    // Read/validate external facts before touching either owner.
                    var stats = Stats.Read() ?? throw new InvalidOperationException("Weapon stats are unavailable.");
                    if (float.IsNaN(stats.Reduction) || float.IsInfinity(stats.Reduction)) throw new InvalidOperationException("Weapon cooldown reduction must be finite.");
                    float period = Cooldown(configuration.BaseCooldown, stats.Reduction);
                    Charges.Reconfigure(configuration.Capacity, period, now);
                    selection = configuration.Selection;
                    return new WeaponSnapshot(InstanceId, selection, stats, Charges.Read(), InkPalette.Read(), ExperimentalChargeStamp);
                }
                finally { configuring = false; }
            }
            public WeaponSnapshot Read() => new WeaponSnapshot(InstanceId, selection, Stats.Read(), Charges.Read(), InkPalette.Read(), ExperimentalChargeStamp);
        }
    }
}
