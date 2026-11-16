using System;
using System.Linq;
using Deviant.Equipment;
using Deviant.Equipment.Contracts;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class EquipmentChargeTests
    {
        [Test] public void EquipmentContractsAndOwnerHaveNoEngineOrCombatDependency()
        {
            foreach (var assembly in new[] { typeof(IWeaponCharges).Assembly, typeof(WeaponResources).Assembly })
                Assert.That(assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("UnityEngine") || a.Name.StartsWith("UnityEditor") || a.Name.StartsWith("Deviant.Combat")), Is.False);
            Assert.That(typeof(IWeaponCharges).Assembly.GetName().Name, Is.EqualTo("Deviant.Equipment.Contracts"));
            Assert.That(typeof(WeaponResources).Assembly.GetName().Name, Is.EqualTo("Deviant.Equipment"));
        }
        [Test] public void ReadIsAnImmutableSnapshotAndIndependentOwnersDoNotShareCharges()
        {
            var first = WeaponResources.CreateCharges(3, 5); var other = WeaponResources.CreateCharges(3, 5); var before = first.Read();
            Assert.That(first.Spend(.08f), Is.True); Assert.That(before.Available, Is.EqualTo(3)); Assert.That(before.NextReady, Is.Zero);
            Assert.That(first.Read().Available, Is.EqualTo(2)); Assert.That(first.Read().NextReady, Is.EqualTo(5.08f)); Assert.That(other.Read().Available, Is.EqualTo(3));
            var spent = first.Read(); first.Tick(15); Assert.That(spent.Available, Is.EqualTo(2)); Assert.That(first.Read().Available, Is.EqualTo(3));
        }
        [Test] public void CompatibilityAdaptersReadAndCommandTheSameEquipmentOwner()
        {
            var owner = WeaponResources.CreateCharges(3, 5); var a = new ChargePool(owner); var b = new ChargePool(owner);
            owner.Spend(.08f); Assert.That(a.Available, Is.EqualTo(2)); Assert.That(b.Available, Is.EqualTo(2));
            Assert.That(a.Spend(.46f), Is.True); Assert.That(owner.Read().Available, Is.EqualTo(1)); Assert.That(b.NextReady, Is.EqualTo(5.08f));
            b.Tick(5.08f); Assert.That(a.Available, Is.EqualTo(2)); Assert.That(owner.Read().Available, Is.EqualTo(2));
            a.Reset(1, 7); Assert.That(b.Capacity, Is.EqualTo(1)); Assert.That(owner.Read().Period, Is.EqualTo(7));
            Assert.Throws<ArgumentNullException>(() => new ChargePool(null));
        }
        [Test] public void ThreeSpendsKeepTheFirstRecoveryAndRecoverSequentiallyAcrossLargeTicks()
        {
            var owner = WeaponResources.CreateCharges(3, 5);
            Assert.That(owner.Spend(.08f), Is.True); Assert.That(owner.Spend(.46f), Is.True); Assert.That(owner.Spend(.84f), Is.True);
            Assert.That(owner.Read().Available, Is.Zero); Assert.That(owner.Read().NextReady, Is.EqualTo(5.08f)); Assert.That(owner.Spend(1), Is.False);
            owner.Tick(5); Assert.That(owner.Read().Available, Is.Zero); owner.Tick(5.08f); Assert.That(owner.Read().Available, Is.EqualTo(1)); Assert.That(owner.Read().NextReady, Is.EqualTo(10.08f));
            Assert.That(owner.Spend(5.2f), Is.True); Assert.That(owner.Read().NextReady, Is.EqualTo(10.08f));
            owner.Tick(25.08f); Assert.That(owner.Read().Available, Is.EqualTo(3)); Assert.That(owner.Debt(25.08f), Is.Zero);
        }
        [Test] public void OneToThreeAndBackPreserveTheApprovedHalfRecoveryBurden()
        {
            var owner = WeaponResources.CreateCharges(1, 10); owner.Spend(0); Assert.That(owner.Debt(5), Is.EqualTo(.5f));
            owner.Reconfigure(3, 5, 5); var three = owner.Read(); Assert.That(three.Available, Is.EqualTo(1)); Assert.That(three.NextReady, Is.EqualTo(7.5f)); Assert.That(owner.Debt(5), Is.EqualTo(.5f));
            owner.Reconfigure(1, 6, 5); Assert.That(owner.Read().Available, Is.Zero); Assert.That(owner.Read().NextReady, Is.EqualTo(8)); Assert.That(owner.Debt(5), Is.EqualTo(.5f));
            owner.Tick(8); Assert.That(owner.Read().Available, Is.EqualTo(1)); Assert.That(owner.Debt(8), Is.Zero);
        }
        [Test] public void PeriodOnlyMaintenanceKeepsTheRemainingFractionAndDoesNotRefill()
        {
            var owner = WeaponResources.CreateCharges(1, 8); owner.Spend(2); owner.Reconfigure(1, 4, 4);
            Assert.That(owner.Read().Available, Is.Zero); Assert.That(owner.Read().NextReady, Is.EqualTo(7)); Assert.That(owner.Debt(4), Is.EqualTo(.75f));
            owner.Reconfigure(3, 8, 4); Assert.That(owner.Read().Available, Is.Zero); Assert.That(owner.Read().NextReady, Is.EqualTo(6)); Assert.That(owner.Debt(4), Is.EqualTo(.75f));
        }
        [Test] public void FullMaintenanceAndExplicitResetRetainTheirExistingCompatibilitySemantics()
        {
            var owner = WeaponResources.CreateCharges(1, 5); owner.Reconfigure(3, 7, 10); Assert.That(owner.Read().Available, Is.EqualTo(3)); Assert.That(owner.Read().NextReady, Is.Zero);
            owner.Spend(10); owner.Reset(1, 9); Assert.That(owner.Read().Available, Is.EqualTo(1)); Assert.That(owner.Read().Period, Is.EqualTo(9)); Assert.That(owner.Read().NextReady, Is.Zero);
            owner.Reset(0, 0); Assert.That(owner.Read().Capacity, Is.EqualTo(1)); Assert.That(owner.Read().Period, Is.EqualTo(.001f));
        }
    }
}
