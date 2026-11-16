using System;
using Deviant.Equipment.Contracts;

namespace Deviant.Equipment
{
    public static partial class WeaponResources
    {
        public static IEquipmentCommands CreateCommands(IWeaponLoadout loadout) => new EquipmentCommands(loadout);
        public static float Cooldown(float baseValue, float reduction) => baseValue * (1f - Math.Min(.8f, Math.Max(0, reduction)));

        sealed class EquipmentCommands : IEquipmentCommands
        {
            readonly IWeaponLoadout loadout;
            bool busy;
            public EquipmentCommands(IWeaponLoadout loadout) { this.loadout = loadout ?? throw new ArgumentNullException(nameof(loadout)); }
            public EquipmentCommandResult Reconfigure(int slot, Guid expectedWeapon, WeaponConfiguration configuration, float now)
            {
                if (busy) return EquipmentCommandResult.Rejected("Equipment commands cannot reenter.");
                if (slot < 0 || slot >= WeaponLoadoutSnapshot.SlotCount || expectedWeapon == Guid.Empty || configuration == null || float.IsNaN(now) || float.IsInfinity(now) || now < 0)
                    return EquipmentCommandResult.Rejected("A valid slot, weapon identity, configuration and simulation time are required.");
                busy = true;
                try
                {
                    var weapon = loadout.GetWeapon(slot);
                    if (weapon == null || weapon.InstanceId != expectedWeapon) return EquipmentCommandResult.Rejected("The equipment request belongs to another weapon.");
                    var snapshot = weapon.Reconfigure(configuration, now);
                    if (snapshot == null || snapshot.InstanceId != expectedWeapon || snapshot.Selection.FormIndex != configuration.Selection.FormIndex || snapshot.Selection.SpecialIndex != configuration.Selection.SpecialIndex)
                        return EquipmentCommandResult.Failed("The weapon returned an invalid configuration result.");
                    return EquipmentCommandResult.Succeeded(snapshot);
                }
                catch (Exception exception) { return EquipmentCommandResult.Failed(exception.Message); }
                finally { busy = false; }
            }
        }
    }
}
