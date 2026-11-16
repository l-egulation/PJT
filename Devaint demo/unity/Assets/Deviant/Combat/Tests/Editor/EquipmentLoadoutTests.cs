using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Equipment;
using Deviant.Equipment.Contracts;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class EquipmentLoadoutTests
    {
        [Test] public void SwapOrderPreservesIdentityActiveWeaponAndResourcesAcrossViews()
        {
            var owner = WeaponResources.CreateLoadout(); var first = new WeaponLoadoutView(owner); var second = new WeaponLoadoutView(owner);
            var a = first[0]; var b = first[1]; var old = owner.Read();
            a.StatSlots[0] = 1; a.Cooldown.Spend(0); float recovery = a.Cooldown.NextReady;
            a.TestSpeedScale = 1.4f;
            owner.SwapSlots();
            Assert.That(first[0], Is.SameAs(b)); Assert.That(first[1], Is.SameAs(a));
            Assert.That(second[1].InstanceId, Is.EqualTo(a.InstanceId));
            Assert.That(first.ActiveWeapon, Is.SameAs(a)); Assert.That(owner.ActiveSlot, Is.EqualTo(1));
            Assert.That(first.ToArray(), Is.EqualTo(new[] { b, a }));
            Assert.That(first[1].Cooldown.NextReady, Is.EqualTo(recovery)); Assert.That(first[1].Cooldown.Available, Is.Zero);
            Assert.That(first[1].StatSlots[0], Is.EqualTo(1)); Assert.That(first[1].TestSpeedScale, Is.EqualTo(1.4f));
            Assert.That(old.Weapons[0].InstanceId, Is.EqualTo(a.InstanceId), "Previously captured snapshots remain immutable.");
            first.SwapSlots();
            Assert.That(first[0], Is.SameAs(a)); Assert.That(first[1], Is.SameAs(b)); Assert.That(owner.ActiveSlot, Is.Zero);
        }
        [Test] public void LoadoutContractsAndOwnerHaveNoCombatOrEngineReferences()
        {
            foreach (var type in new[] { typeof(IWeaponLoadout), typeof(WeaponLoadoutSnapshot), typeof(WeaponResources) })
                Assert.That(type.Assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("UnityEngine") || a.Name.StartsWith("UnityEditor") || a.Name.StartsWith("Deviant.Combat")), Is.False);
        }

        [Test] public void DefaultProfileHasTwoDistinctWeaponOwnersAndOneActiveSlot()
        {
            var owner = WeaponResources.CreateLoadout(); var state = owner.Read();
            Assert.That(state.Weapons.Count, Is.EqualTo(2)); Assert.That(owner.ActiveSlot, Is.Zero);
            Assert.That(owner.GetWeapon(0), Is.Not.SameAs(owner.GetWeapon(1)));
            Assert.That(state.Weapons[0].InstanceId, Is.Not.EqualTo(state.Weapons[1].InstanceId));
            Assert.That(state.ActiveWeapon, Is.SameAs(state.Weapons[0]));
        }

        [Test] public void InjectedMembershipIsCopiedAndKeepsTheActualChildOwners()
        {
            var a = WeaponResources.CreateWeapon(); var b = WeaponResources.CreateWeapon(); var source = new[] { a, b };
            var owner = WeaponResources.CreateLoadout(source, 1); source[0] = WeaponResources.CreateWeapon();
            Assert.That(owner.GetWeapon(0), Is.SameAs(a)); Assert.That(owner.GetWeapon(1), Is.SameAs(b)); Assert.That(owner.ActiveSlot, Is.EqualTo(1));
            a.Select(new WeaponSelection(3, 2)); a.Stats.SetSlot(0, StatInkKind.SpecialDamage); a.Charges.Spend(1);
            var state = owner.Read(); Assert.That(state.Weapons[0].Selection.FormIndex, Is.EqualTo(3)); Assert.That(state.Weapons[0].Stats.SpecialBonus, Is.EqualTo(.2f));
            Assert.That(state.Weapons[0].Charges.Available, Is.Zero); Assert.That(state.Weapons[1].Charges.Available, Is.EqualTo(1));
        }

        [TestCase(-1)] [TestCase(2)] public void InvalidSelectionAndLookupLeaveThePreviousLoadoutUntouched(int slot)
        {
            var owner = WeaponResources.CreateLoadout(); owner.SelectSlot(1); owner.GetWeapon(0).Charges.Spend(0); var before = owner.Read();
            Assert.Throws<ArgumentOutOfRangeException>(() => owner.SelectSlot(slot)); Assert.Throws<ArgumentOutOfRangeException>(() => owner.GetWeapon(slot));
            var after = owner.Read(); Assert.That(after.ActiveSlot, Is.EqualTo(before.ActiveSlot));
            Assert.That(after.Weapons.Select(w => w.InstanceId), Is.EqualTo(before.Weapons.Select(w => w.InstanceId)));
            Assert.That(after.Weapons[0].Charges.NextReady, Is.EqualTo(before.Weapons[0].Charges.NextReady)); Assert.That(after.Weapons[0].Charges.Available, Is.Zero);
        }

        [Test] public void MissingDuplicateOrInvalidInitialOwnersAreRejected()
        {
            var a = WeaponResources.CreateWeapon(); var b = WeaponResources.CreateWeapon();
            Assert.Throws<ArgumentException>(() => WeaponResources.CreateLoadout(null));
            Assert.Throws<ArgumentException>(() => WeaponResources.CreateLoadout(new IWeaponInstance[0]));
            Assert.Throws<ArgumentException>(() => WeaponResources.CreateLoadout(new[] { a }));
            Assert.Throws<ArgumentException>(() => WeaponResources.CreateLoadout(new[] { a, b, WeaponResources.CreateWeapon() }));
            Assert.Throws<ArgumentException>(() => WeaponResources.CreateLoadout(new[] { a, a }));
            Assert.Throws<ArgumentException>(() => WeaponResources.CreateLoadout(new IWeaponInstance[] { a, null }));
            Assert.Throws<ArgumentException>(() => WeaponResources.CreateLoadout(new IWeaponInstance[] { a, new IdentityOverride(a.InstanceId) }));
            Assert.Throws<ArgumentException>(() => WeaponResources.CreateLoadout(new IWeaponInstance[] { a, new IdentityOverride(Guid.Empty) }));
            Assert.Throws<ArgumentOutOfRangeException>(() => WeaponResources.CreateLoadout(new[] { a, b }, 2));
        }

        [Test] public void PreviousSnapshotDoesNotFollowSelectionStatsOrChargeRecovery()
        {
            var owner = WeaponResources.CreateLoadout(); var a = owner.GetWeapon(0); a.Stats.SetSlot(0, StatInkKind.BasicDamage); a.Charges.Spend(0); var before = owner.Read();
            owner.SelectSlot(1); a.Select(new WeaponSelection(2, 3)); a.Stats.Clear(); a.Charges.Tick(5);
            Assert.That(before.ActiveSlot, Is.Zero); Assert.That(before.ActiveWeapon.InstanceId, Is.EqualTo(a.InstanceId)); Assert.That(before.ActiveWeapon.Selection.FormIndex, Is.Zero);
            Assert.That(before.ActiveWeapon.Stats.BasicBonus, Is.EqualTo(.2f)); Assert.That(before.ActiveWeapon.Charges.Available, Is.Zero);
            Assert.That(owner.Read().ActiveSlot, Is.EqualTo(1)); Assert.That(owner.Read().Weapons[0].Charges.Available, Is.EqualTo(1));
        }

        [Test] public void SnapshotMembershipIsDefensivelyCopiedAndReadOnly()
        {
            var owner = WeaponResources.CreateLoadout(); var a = owner.GetWeapon(0).Read(); var b = owner.GetWeapon(1).Read(); var input = new[] { a, b };
            var state = new WeaponLoadoutSnapshot(1, input); input[0] = b;
            Assert.That(state.Weapons[0], Is.SameAs(a)); Assert.That(state.ActiveWeapon, Is.SameAs(b));
            Assert.Throws<NotSupportedException>(() => ((IList<WeaponSnapshot>)state.Weapons)[0] = b);
            Assert.Throws<ArgumentException>(() => new WeaponLoadoutSnapshot(0, new[] { a, a }));
            Assert.Throws<ArgumentException>(() => new WeaponLoadoutSnapshot(0, new[] { a, null }));
            Assert.Throws<ArgumentException>(() => new WeaponLoadoutSnapshot(0, new[] { a }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WeaponLoadoutSnapshot(-1, new[] { a, b }));
        }

        [Test] public void LegacyViewsObserveOneSelectionAndKeepStableWeaponReferences()
        {
            var owner = WeaponResources.CreateLoadout(); var first = new WeaponLoadoutView(owner); var second = new WeaponLoadoutView(owner); var a = first[0]; var b = first[1];
            owner.SelectSlot(1); Assert.That(first.ActiveWeapon, Is.SameAs(b)); Assert.That(second.ActiveSlot, Is.EqualTo(1));
            second.SelectSlot(0); Assert.That(owner.ActiveSlot, Is.Zero); Assert.That(first.ActiveWeapon, Is.SameAs(a));
            Assert.That(first[0], Is.SameAs(a)); Assert.That(first[1], Is.SameAs(b)); Assert.That(first.ToArray(), Is.EqualTo(new[] { a, b })); Assert.That(first.Length, Is.EqualTo(first.Count));
            a.StatSlots[0] = 1; second[0].Cooldown.Spend(0); Assert.That(owner.Read().Weapons[0].Stats.BasicBonus, Is.EqualTo(.2f)); Assert.That(a.Cooldown.Available, Is.Zero);
            Assert.That(second[0].InstanceId, Is.EqualTo(a.InstanceId)); Assert.That(second[1].Cooldown.Available, Is.EqualTo(1));
            Assert.Throws<ArgumentNullException>(() => new WeaponLoadoutView(null));
        }

        [Test] public void RepeatedSelectionPreservesBothWeaponInstancesAndTheirRecovery()
        {
            var owner = WeaponResources.CreateLoadout(); var a = owner.GetWeapon(0); var b = owner.GetWeapon(1); a.Charges.Spend(0); b.Charges.Reset(3, 2); b.Charges.Spend(1); var initial = owner.Read();
            for (int i = 0; i < 10; i++) owner.SelectSlot(1 - owner.ActiveSlot);
            Assert.That(owner.ActiveSlot, Is.Zero); Assert.That(owner.GetWeapon(0), Is.SameAs(a)); Assert.That(owner.GetWeapon(1), Is.SameAs(b));
            var after = owner.Read();
            for (int i = 0; i < 2; i++)
            {
                Assert.That(after.Weapons[i].InstanceId, Is.EqualTo(initial.Weapons[i].InstanceId)); Assert.That(after.Weapons[i].Charges.Available, Is.EqualTo(initial.Weapons[i].Charges.Available));
                Assert.That(after.Weapons[i].Charges.NextReady, Is.EqualTo(initial.Weapons[i].Charges.NextReady));
            }
        }

        sealed class IdentityOverride : IWeaponInstance
        {
            readonly IWeaponInstance source = WeaponResources.CreateWeapon();
            public Guid InstanceId { get; }
            public IdentityOverride(Guid identity) { InstanceId = identity; }
            public WeaponSelection ReadSelection() => source.ReadSelection();
            public void Select(WeaponSelection selection) => source.Select(selection);
            public WeaponSnapshot Reconfigure(WeaponConfiguration configuration, float now)
            {
                var snapshot = source.Reconfigure(configuration, now); return new WeaponSnapshot(InstanceId, snapshot.Selection, snapshot.Stats, snapshot.Charges, snapshot.InkPalette, snapshot.ExperimentalChargeStamp);
            }
            public IWeaponStats Stats => source.Stats;
            public IInkPalette InkPalette => source.InkPalette;
            public bool ExperimentalChargeStamp { get => source.ExperimentalChargeStamp; set => source.ExperimentalChargeStamp=value; }
            public IWeaponCharges Charges => source.Charges;
            public WeaponSnapshot Read() => new WeaponSnapshot(InstanceId, ReadSelection(), Stats.Read(), Charges.Read(), InkPalette.Read(), ExperimentalChargeStamp);
        }
    }
}
