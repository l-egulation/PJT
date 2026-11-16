using System;

namespace Deviant.Equipment.Contracts
{
    // Resolved current-profile catalog values. Item acquisition, inventory and
    // the product's safe-maintenance policy remain outside this operation.
    public sealed class WeaponConfiguration
    {
        public WeaponSelection Selection { get; }
        public int Capacity { get; }
        public float BaseCooldown { get; }
        public WeaponConfiguration(WeaponSelection selection, int capacity, float baseCooldown)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity), "A positive charge capacity is required.");
            if (float.IsNaN(baseCooldown) || float.IsInfinity(baseCooldown) || baseCooldown < 0)
                throw new ArgumentOutOfRangeException(nameof(baseCooldown), "A finite nonnegative cooldown is required.");
            Selection = selection; Capacity = capacity; BaseCooldown = baseCooldown;
        }
    }
    public enum EquipmentCommandStatus { Succeeded, Rejected, Failed }
    public sealed class EquipmentCommandResult
    {
        public EquipmentCommandStatus Status { get; }
        public WeaponSnapshot Weapon { get; }
        public string Error { get; }
        EquipmentCommandResult(EquipmentCommandStatus status, WeaponSnapshot weapon, string error)
        { Status = status; Weapon = weapon; Error = error; }
        public static EquipmentCommandResult Succeeded(WeaponSnapshot weapon) => new EquipmentCommandResult(EquipmentCommandStatus.Succeeded, weapon ?? throw new ArgumentNullException(nameof(weapon)), null);
        public static EquipmentCommandResult Rejected(string error) => new EquipmentCommandResult(EquipmentCommandStatus.Rejected, null, error);
        public static EquipmentCommandResult Failed(string error) => new EquipmentCommandResult(EquipmentCommandStatus.Failed, null, error);
    }
    public interface IEquipmentCommands
    {
        EquipmentCommandResult Reconfigure(int slot, Guid expectedWeapon, WeaponConfiguration configuration, float now);
    }
}
