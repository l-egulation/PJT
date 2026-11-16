using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Equipment;
using Deviant.Equipment.Contracts;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class EquipmentStatTests
    {
        [Test] public void EquipmentStatContractsAndOwnerHaveNoEngineOrCombatDependency()
        {
            foreach (var type in new[] { typeof(IWeaponStats), typeof(WeaponStatSnapshot), typeof(WeaponResources) })
                Assert.That(type.Assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("UnityEngine") || a.Name.StartsWith("UnityEditor") || a.Name.StartsWith("Deviant.Combat")), Is.False);
            Assert.That(typeof(IWeaponStats).Assembly.GetName().Name, Is.EqualTo("Deviant.Equipment.Contracts"));
        }
        [Test] public void InitialFiveSlotsAndMixedInksUseTheApprovedSeparateFloatSums()
        {
            var owner = WeaponResources.CreateStats(); var empty = owner.Read(); Assert.That(empty.Slots, Is.EqualTo(new StatInkKind[5])); Assert.That(empty.Speed, Is.EqualTo(1));
            owner.ReplaceSlots(new[] { StatInkKind.BasicDamage, StatInkKind.BasicDamage, StatInkKind.SpecialDamage, StatInkKind.BasicSpeed, StatInkKind.SpecialCooldown });
            var mixed = owner.Read(); Assert.That(mixed.BasicBonus, Is.EqualTo(.44f).Within(1e-5f)); Assert.That(mixed.SpecialBonus, Is.EqualTo(.2f).Within(1e-5f)); Assert.That(mixed.Speed, Is.EqualTo(1.1f).Within(1e-5f)); Assert.That(mixed.Reduction, Is.EqualTo(.2f).Within(1e-5f));
            owner.ReplaceSlots(Enumerable.Repeat(StatInkKind.BasicSpeed, 5).ToArray()); Assert.That(owner.Read().Speed, Is.EqualTo(1.61051f).Within(1e-5f));
            owner.ReplaceSlots(Enumerable.Repeat(StatInkKind.SpecialDamage, 5).ToArray()); Assert.That(owner.Read().SpecialBonus, Is.EqualTo(1.48832f).Within(1e-5f));
            owner.ReplaceSlots(Enumerable.Repeat(StatInkKind.SpecialCooldown, 5).ToArray()); Assert.That(owner.Read().Reduction, Is.EqualTo(.67232f).Within(1e-5f));
        }
        [Test] public void InputsAndPublishedSnapshotsCannotMutateTheOwnersSlots()
        {
            var owner = WeaponResources.CreateStats(); var input = new StatInkKind[5]; input[0] = StatInkKind.BasicDamage; owner.ReplaceSlots(input); var before = owner.Read();
            input[0] = StatInkKind.BasicSpeed; Assert.That(owner.Read().BasicBonus, Is.EqualTo(.2f)); Assert.That(owner.Read().Speed, Is.EqualTo(1));
            Assert.Throws<NotSupportedException>(() => ((IList<StatInkKind>)before.Slots)[0] = StatInkKind.Empty);
            owner.SetSlot(0, StatInkKind.SpecialDamage); Assert.That(before.Slots[0], Is.EqualTo(StatInkKind.BasicDamage)); Assert.That(before.BasicBonus, Is.EqualTo(.2f));
            Assert.That(owner.Read().BasicBonus, Is.Zero); Assert.That(owner.Read().SpecialBonus, Is.EqualTo(.2f));
        }
        [TestCase("length")] [TestCase("null")] [TestCase("kind")] public void InvalidReplacementLeavesEverySlotAndStatUnchanged(string failure)
        {
            var owner = WeaponResources.CreateStats(); owner.SetSlot(1, StatInkKind.BasicDamage); var before = owner.Read();
            var input = failure == "null" ? null : new StatInkKind[failure == "length" ? 4 : 5]; if (failure == "kind") input[4] = (StatInkKind)99;
            Assert.Catch<ArgumentException>(() => owner.ReplaceSlots(input)); var after = owner.Read(); Assert.That(after.Slots, Is.EqualTo(before.Slots)); Assert.That(after.BasicBonus, Is.EqualTo(before.BasicBonus));
        }
        [Test] public void InvalidSingleSlotCommandsDoNotPartiallyChangeTheOwner()
        {
            var owner = WeaponResources.CreateStats(); owner.SetSlot(0, StatInkKind.BasicDamage);
            Assert.Throws<ArgumentOutOfRangeException>(() => owner.SetSlot(-1, StatInkKind.Empty)); Assert.Throws<ArgumentOutOfRangeException>(() => owner.SetSlot(5, StatInkKind.Empty));
            Assert.Throws<ArgumentOutOfRangeException>(() => owner.SetSlot(0, (StatInkKind)99)); Assert.That(owner.Read().BasicBonus, Is.EqualTo(.2f));
        }
        [Test] public void LegacyViewsAndDirectCommandsShareOneOwnerAndClonesStayDetached()
        {
            var owner = WeaponResources.CreateStats(); var weapon = new WeaponState(owner); var otherView = new StatSlotView(owner);
            owner.SetSlot(0, StatInkKind.BasicDamage); Assert.That(weapon.StatSlots[0], Is.EqualTo(1)); Assert.That(weapon.BasicBonus, Is.EqualTo(.2f));
            otherView[1] = 2; Assert.That(weapon.SpecialBonus, Is.EqualTo(.2f)); Assert.That(owner.Read().Slots[1], Is.EqualTo(StatInkKind.SpecialDamage));
            var copy = (int[])weapon.StatSlots.Clone(); copy[0] = 4; Assert.That(weapon.StatSlots[0], Is.EqualTo(1));
            weapon.StatSlots.CopyFrom(new[] { 4, 3, 2, 1, 0 }); Assert.That(otherView.ToArray(), Is.EqualTo(new[] { 4, 3, 2, 1, 0 })); Assert.That(weapon.Count(0), Is.EqualTo(1)); Assert.That(weapon.Count(99), Is.Zero);
            var before = owner.Read(); Assert.Catch<ArgumentException>(() => weapon.StatSlots.CopyFrom(new[] { 1, 1, 1, 1, 99 })); Assert.That(owner.Read().Slots, Is.EqualTo(before.Slots));
            otherView.Clear(); Assert.That(weapon.Count(0), Is.EqualTo(5)); Assert.That(weapon.BasicBonus, Is.Zero); Assert.That(weapon.Speed, Is.EqualTo(1));
        }
        [Test] public void IndependentWeaponsAndStatCommandsDoNotModifyChargesOrSelection()
        {
            var first = new WeaponState { FormIndex = 2, SpecialIndex = 3 }; var second = new WeaponState(); var id = first.InstanceId;
            first.Cooldown.Spend(0); var charges = first.Cooldown.Snapshot; first.StatSlots[0] = 1; first.StatSlots[1] = 4;
            Assert.That(second.BasicBonus, Is.Zero); Assert.That(second.Reduction, Is.Zero); Assert.That(second.InstanceId, Is.Not.EqualTo(id));
            first.StatSlots.Clear(); Assert.That(first.FormIndex, Is.EqualTo(2)); Assert.That(first.SpecialIndex, Is.EqualTo(3)); Assert.That(first.InstanceId, Is.EqualTo(id));
            Assert.That(first.Cooldown.Available, Is.EqualTo(charges.Available)); Assert.That(first.Cooldown.NextReady, Is.EqualTo(charges.NextReady)); Assert.That(first.Cooldown.Period, Is.EqualTo(charges.Period));
        }
    }
}
