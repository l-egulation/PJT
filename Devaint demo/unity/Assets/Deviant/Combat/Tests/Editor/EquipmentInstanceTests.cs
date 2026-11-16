using System;
using System.Linq;
using Deviant.Equipment;
using Deviant.Equipment.Contracts;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class EquipmentInstanceTests
    {
        [Test] public void InstanceContractsAndOwnerDoNotReferenceCombatOrEngine()
        {
            foreach (var type in new[] { typeof(IWeaponInstance), typeof(WeaponSnapshot), typeof(WeaponResources) })
                Assert.That(type.Assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("UnityEngine") || a.Name.StartsWith("UnityEditor") || a.Name.StartsWith("Deviant.Combat")), Is.False);
        }
        [Test] public void OneInstanceOwnsStableIdentitySelectionStatsAndCharges()
        {
            var owner = WeaponResources.CreateWeapon(); var id = owner.InstanceId; var stats = owner.Stats; var charges = owner.Charges;
            Assert.That(id, Is.Not.EqualTo(Guid.Empty)); Assert.That(owner.ReadSelection().FormIndex, Is.Zero); Assert.That(owner.ReadSelection().SpecialIndex, Is.Zero);
            owner.Stats.SetSlot(0, StatInkKind.BasicDamage); owner.Charges.Spend(0); var before = owner.Read();
            owner.Select(new WeaponSelection(2, 3)); var after = owner.Read();
            Assert.That(after.InstanceId, Is.EqualTo(id)); Assert.That(after.Selection.FormIndex, Is.EqualTo(2)); Assert.That(after.Selection.SpecialIndex, Is.EqualTo(3));
            Assert.That(owner.Stats, Is.SameAs(stats)); Assert.That(owner.Charges, Is.SameAs(charges)); Assert.That(after.Stats.BasicBonus, Is.EqualTo(before.Stats.BasicBonus));
            Assert.That(after.Charges.Available, Is.EqualTo(before.Charges.Available)); Assert.That(after.Charges.NextReady, Is.EqualTo(before.Charges.NextReady));
        }
        [Test] public void SnapshotsDoNotFollowLaterSelectionSlotsOrRecovery()
        {
            var owner = WeaponResources.CreateWeapon(); var initial = owner.Read(); owner.Select(new WeaponSelection(4, 2)); owner.Stats.SetSlot(0, StatInkKind.BasicSpeed); owner.Charges.Spend(1); var active = owner.Read();
            owner.Stats.Clear(); owner.Charges.Tick(6); owner.Select(new WeaponSelection(0, 0));
            Assert.That(initial.Selection.FormIndex, Is.Zero); Assert.That(initial.Stats.Speed, Is.EqualTo(1)); Assert.That(initial.Charges.Available, Is.EqualTo(1));
            Assert.That(active.Selection.FormIndex, Is.EqualTo(4)); Assert.That(active.Selection.SpecialIndex, Is.EqualTo(2)); Assert.That(active.Stats.Speed, Is.EqualTo(1.1f)); Assert.That(active.Charges.Available, Is.Zero); Assert.That(active.Charges.NextReady, Is.EqualTo(6));
            Assert.That(owner.Read().Charges.Available, Is.EqualTo(1)); Assert.That(owner.Read().Stats.Speed, Is.EqualTo(1));
        }
        [TestCase(-1, 0)] [TestCase(0, -1)] public void InvalidSelectionConstructionDoesNotChangeThePreviousPair(int form, int special)
        {
            var owner = WeaponResources.CreateWeapon(); owner.Select(new WeaponSelection(2, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => owner.Select(new WeaponSelection(form, special)));
            Assert.That(owner.ReadSelection().FormIndex, Is.EqualTo(2)); Assert.That(owner.ReadSelection().SpecialIndex, Is.EqualTo(3));
        }
        [Test] public void SeparateInstancesKeepIndependentIdentitySelectionStatsAndResources()
        {
            var a = WeaponResources.CreateWeapon(); var b = WeaponResources.CreateWeapon(); a.Select(new WeaponSelection(2, 3)); a.Stats.SetSlot(0, StatInkKind.BasicDamage); a.Charges.Spend(0);
            Assert.That(a.InstanceId, Is.Not.EqualTo(b.InstanceId)); Assert.That(b.ReadSelection().FormIndex, Is.Zero); Assert.That(b.Stats.Read().BasicBonus, Is.Zero); Assert.That(b.Charges.Read().Available, Is.EqualTo(1));
        }
        [Test] public void MultipleLegacyViewsShareTheSameEquipmentInstanceWithoutCopies()
        {
            var owner = WeaponResources.CreateWeapon(); var a = new WeaponState(owner); var b = new WeaponState(owner);
            owner.Select(new WeaponSelection(2, 3)); Assert.That(a.FormIndex, Is.EqualTo(2)); Assert.That(b.SpecialIndex, Is.EqualTo(3)); Assert.That(a.InstanceId, Is.EqualTo(owner.InstanceId)); Assert.That(b.InstanceId, Is.EqualTo(owner.InstanceId));
            a.FormIndex = 1; Assert.That(owner.ReadSelection().FormIndex, Is.EqualTo(1)); Assert.That(b.FormIndex, Is.EqualTo(1)); Assert.That(b.SpecialIndex, Is.EqualTo(3));
            b.Select(4, 2); Assert.That(a.FormIndex, Is.EqualTo(4)); Assert.That(a.SpecialIndex, Is.EqualTo(2));
            a.StatSlots[0] = 1; Assert.That(owner.Read().Stats.BasicBonus, Is.EqualTo(.2f)); Assert.That(b.BasicBonus, Is.EqualTo(.2f));
            b.Cooldown.Spend(0); Assert.That(a.Cooldown.Available, Is.Zero); Assert.That(owner.Read().Charges.Available, Is.Zero);
            var old = a.Snapshot; b.StatSlots.Clear(); b.Cooldown.Reset(1, 5); Assert.That(old.Stats.BasicBonus, Is.EqualTo(.2f)); Assert.That(old.Charges.Available, Is.Zero);
        }
        [Test] public void ExistingStatInjectionRemainsTheSameChildAndNullOwnersAreRejected()
        {
            var stats = WeaponResources.CreateStats(); var owner = WeaponResources.CreateWeapon(stats); var view = new WeaponState(owner);
            Assert.That(owner.Stats, Is.SameAs(stats)); stats.SetSlot(0, StatInkKind.SpecialDamage); Assert.That(view.SpecialBonus, Is.EqualTo(.2f));
            var legacy = new WeaponState(stats); Assert.That(legacy.SpecialBonus, Is.EqualTo(.2f)); Assert.That(legacy.InstanceId, Is.Not.EqualTo(owner.InstanceId));
            Assert.Throws<ArgumentNullException>(() => WeaponResources.CreateWeapon((IWeaponStats)null)); Assert.Throws<ArgumentNullException>(() => new WeaponState((IWeaponInstance)null));
        }
    }
}
