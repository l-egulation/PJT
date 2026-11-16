using System;
using System.Linq;
using Deviant.Equipment;
using Deviant.Equipment.Contracts;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class EquipmentCommandsTests
    {
        static WeaponConfiguration Config(int form = 2, int special = 3, int capacity = 3, float cooldown = 5) => new WeaponConfiguration(new WeaponSelection(form, special), capacity, cooldown);
        static void Same(WeaponSnapshot before, WeaponSnapshot after)
        {
            Assert.That(after.InstanceId, Is.EqualTo(before.InstanceId)); Assert.That(after.Selection.FormIndex, Is.EqualTo(before.Selection.FormIndex)); Assert.That(after.Selection.SpecialIndex, Is.EqualTo(before.Selection.SpecialIndex));
            Assert.That(after.Stats.Slots, Is.EqualTo(before.Stats.Slots)); Assert.That(after.Charges.Capacity, Is.EqualTo(before.Charges.Capacity)); Assert.That(after.Charges.Available, Is.EqualTo(before.Charges.Available));
            Assert.That(after.Charges.Period, Is.EqualTo(before.Charges.Period)); Assert.That(after.Charges.NextReady, Is.EqualTo(before.Charges.NextReady));
        }
        [Test] public void CommandsAndResolvedConfigurationStayOutsideCombatAndEngine()
        {
            foreach (var type in new[] { typeof(IEquipmentCommands), typeof(WeaponConfiguration), typeof(WeaponResources) })
                Assert.That(type.Assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("UnityEngine") || a.Name.StartsWith("UnityEditor") || a.Name.StartsWith("Deviant.Combat")), Is.False);
        }
        [Test] public void ConfigurationChangesTheSameWeaponAndPreservesNormalizedRecoveryDebt()
        {
            var owner = WeaponResources.CreateLoadout(); var weapon = owner.GetWeapon(0); var other = owner.GetWeapon(1); var commands = WeaponResources.CreateCommands(owner); var stats = weapon.Stats; var charges = weapon.Charges;
            weapon.Charges.Reset(1, 10); weapon.Charges.Spend(0); owner.SelectSlot(1); var before = weapon.Read();
            var result = commands.Reconfigure(0, weapon.InstanceId, Config(), 5);
            Assert.That(result.Status, Is.EqualTo(EquipmentCommandStatus.Succeeded)); Assert.That(result.Weapon.InstanceId, Is.EqualTo(weapon.InstanceId)); Assert.That(result.Weapon.Selection.FormIndex, Is.EqualTo(2)); Assert.That(result.Weapon.Selection.SpecialIndex, Is.EqualTo(3));
            Assert.That(result.Weapon.Charges.Capacity, Is.EqualTo(3)); Assert.That(result.Weapon.Charges.Available, Is.EqualTo(1)); Assert.That(result.Weapon.Charges.NextReady, Is.EqualTo(7.5f));
            Assert.That(weapon.Stats, Is.SameAs(stats)); Assert.That(weapon.Charges, Is.SameAs(charges)); Assert.That(owner.ActiveSlot, Is.EqualTo(1)); Assert.That(other.Charges.Read().Available, Is.EqualTo(1));
            Assert.That(before.Selection.FormIndex, Is.Zero); Assert.That(before.Charges.Capacity, Is.EqualTo(1));
            commands.Reconfigure(0, weapon.InstanceId, Config(capacity: 1, cooldown: 6), 5);
            Assert.That(weapon.Charges.Read().Available, Is.Zero); Assert.That(weapon.Charges.Read().NextReady, Is.EqualTo(8));
        }
        [Test] public void CooldownUsesTheOwnedStatsAndKeepsTheExistingCapAndFloatFormula()
        {
            var owner = WeaponResources.CreateLoadout(); var weapon = owner.GetWeapon(0); var commands = WeaponResources.CreateCommands(owner);
            weapon.Stats.ReplaceSlots(new[] { StatInkKind.SpecialCooldown, StatInkKind.SpecialCooldown, StatInkKind.SpecialCooldown, StatInkKind.BasicDamage, StatInkKind.SpecialDamage });
            var result = commands.Reconfigure(0, weapon.InstanceId, Config(capacity: 1, cooldown: 7), 0);
            Assert.That(result.Weapon.Charges.Period, Is.EqualTo(7f * .512f).Within(1e-4f), "three cooldown inks keep 0.8^3 of the cooldown"); Assert.That(result.Weapon.Stats.BasicBonus, Is.EqualTo(.2f)); Assert.That(result.Weapon.Stats.SpecialBonus, Is.EqualTo(.2f));
            Assert.That(WeaponResources.Cooldown(10, 1), Is.EqualTo(10f * (1f - .8f))); Assert.That(CombatRules.Cooldown(7, .3f), Is.EqualTo(WeaponResources.Cooldown(7, .3f)));
            result = commands.Reconfigure(0, weapon.InstanceId, Config(capacity: 1, cooldown: 0), 0); Assert.That(result.Weapon.Charges.Period, Is.EqualTo(.001f));
        }
        [TestCase(-1)] [TestCase(2)] public void InvalidSlotsRejectBeforeTouchingEitherWeapon(int slot)
        {
            var owner = WeaponResources.CreateLoadout(); var commands = WeaponResources.CreateCommands(owner); owner.GetWeapon(0).Charges.Spend(0); var a = owner.GetWeapon(0).Read(); var b = owner.GetWeapon(1).Read();
            Assert.That(commands.Reconfigure(slot, a.InstanceId, Config(), 1).Status, Is.EqualTo(EquipmentCommandStatus.Rejected)); Same(a, owner.GetWeapon(0).Read()); Same(b, owner.GetWeapon(1).Read());
        }
        [Test] public void StaleIdentityInvalidTimeAndMissingConfigurationPreserveTheSource()
        {
            var owner = WeaponResources.CreateLoadout(); var weapon = owner.GetWeapon(0); weapon.Charges.Spend(0); var before = weapon.Read(); var commands = WeaponResources.CreateCommands(owner);
            foreach (var identity in new[] { Guid.Empty, Guid.NewGuid(), owner.GetWeapon(1).InstanceId }) Assert.That(commands.Reconfigure(0, identity, Config(), 1).Status, Is.EqualTo(EquipmentCommandStatus.Rejected));
            foreach (float time in new[] { -1, float.NaN, float.PositiveInfinity }) Assert.That(commands.Reconfigure(0, weapon.InstanceId, Config(), time).Status, Is.EqualTo(EquipmentCommandStatus.Rejected));
            Assert.That(commands.Reconfigure(0, weapon.InstanceId, null, 1).Status, Is.EqualTo(EquipmentCommandStatus.Rejected)); Same(before, weapon.Read());
            Assert.Throws<ArgumentOutOfRangeException>(() => Config(capacity: 0)); Assert.Throws<ArgumentOutOfRangeException>(() => Config(cooldown: float.NaN)); Assert.Throws<ArgumentOutOfRangeException>(() => Config(cooldown: -1)); Same(before, weapon.Read());
            Assert.Throws<ArgumentNullException>(() => WeaponResources.CreateCommands(null));
        }
        [Test] public void StatsFailureOrMissingReadCannotPartiallyChangeSelectionOrRecovery()
        {
            var stats = new TestStats(); var weapon = WeaponResources.CreateWeapon(stats); var owner = WeaponResources.CreateLoadout(new[] { weapon, WeaponResources.CreateWeapon() }); var commands = WeaponResources.CreateCommands(owner);
            weapon.Select(new WeaponSelection(1, 2)); weapon.Charges.Spend(0); var before = weapon.Read();
            stats.Throw = true; var result = commands.Reconfigure(0, weapon.InstanceId, Config(), 1); Assert.That(result.Status, Is.EqualTo(EquipmentCommandStatus.Failed)); Assert.That(result.Error, Does.Contain("stats-fault"));
            stats.Throw = false; Same(before, weapon.Read()); stats.Missing = true;
            result = commands.Reconfigure(0, weapon.InstanceId, Config(), 1); Assert.That(result.Status, Is.EqualTo(EquipmentCommandStatus.Failed)); stats.Missing = false; Same(before, weapon.Read());
            stats.Invalid = true; result = commands.Reconfigure(0, weapon.InstanceId, Config(), 1); Assert.That(result.Status, Is.EqualTo(EquipmentCommandStatus.Failed)); stats.Invalid = false; Same(before, weapon.Read());
            Assert.That(commands.Reconfigure(0, weapon.InstanceId, Config(), 1).Status, Is.EqualTo(EquipmentCommandStatus.Succeeded));
        }
        [Test] public void ReentryIsRejectedAndTheOuterCommandReturnsItsOneSnapshot()
        {
            var stats = new TestStats(); var weapon = WeaponResources.CreateWeapon(stats); var owner = WeaponResources.CreateLoadout(new[] { weapon, WeaponResources.CreateWeapon() }); var commands = WeaponResources.CreateCommands(owner);
            EquipmentCommandResult nested = null; int reads = 0;
            stats.OnRead = () => { reads++; nested = commands.Reconfigure(0, weapon.InstanceId, Config(4, 1), 0); };
            var result = commands.Reconfigure(0, weapon.InstanceId, Config(), 0); stats.OnRead = null;
            Assert.That(nested.Status, Is.EqualTo(EquipmentCommandStatus.Rejected)); Assert.That(result.Status, Is.EqualTo(EquipmentCommandStatus.Succeeded)); Assert.That(reads, Is.EqualTo(1));
            Assert.That(result.Weapon.Selection.FormIndex, Is.EqualTo(2)); Assert.That(weapon.ReadSelection().SpecialIndex, Is.EqualTo(3));
        }
        [Test] public void LegacyViewsAndDirectCommandsShareOneConfigurationWithoutResettingCharges()
        {
            var owner = WeaponResources.CreateLoadout(); var view = new WeaponLoadoutView(owner); var other = new WeaponLoadoutView(owner); var weapon = owner.GetWeapon(0); weapon.Charges.Spend(0);
            var result = view.Reconfigure(0, Config(capacity: 1, cooldown: 10), 1);
            Assert.That(result.Status, Is.EqualTo(EquipmentCommandStatus.Succeeded)); Assert.That(other[0].FormIndex, Is.EqualTo(2)); Assert.That(other[0].Cooldown.Available, Is.Zero); Assert.That(other[0].Cooldown.NextReady, Is.EqualTo(9));
            Assert.That(view[0].InstanceId, Is.EqualTo(weapon.InstanceId)); Assert.That(view.ActiveSlot, Is.Zero);
            Assert.That(view.Reconfigure(2, Config(), 1).Status, Is.EqualTo(EquipmentCommandStatus.Rejected));
        }
        sealed class TestStats : IWeaponStats
        {
            readonly IWeaponStats source = WeaponResources.CreateStats();
            public bool Throw, Missing, Invalid; public Action OnRead;
            public WeaponStatSnapshot Read() { OnRead?.Invoke(); if (Throw) throw new InvalidOperationException("stats-fault"); return Missing ? null : Invalid ? new WeaponStatSnapshot(new StatInkKind[5], 0, 0, 1, float.NaN) : source.Read(); }
            public void SetSlot(int slot, StatInkKind kind) => source.SetSlot(slot, kind);
            public void ReplaceSlots(StatInkKind[] slots) => source.ReplaceSlots(slots);
            public void Clear() => source.Clear();
        }
    }
}
