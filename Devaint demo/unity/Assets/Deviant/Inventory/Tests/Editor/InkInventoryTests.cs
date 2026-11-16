using System;
using System.Linq;
using Deviant.Control;
using Deviant.Control.Contracts;
using NUnit.Framework;

namespace Deviant.Inventory.Tests
{
    public class InkInventoryTests
    {
        [Test] public void WholeWeaponReorderMovesInkAndCategoryWithoutReapplyingEquipment()
        {
            var port = new Port(); var bag = Bag(port); var ink = Stat(); var stored = Stat(2);
            Assert.IsTrue(bag.TryAcquire(ink, out _)); Assert.IsTrue(bag.TryEquip(0, 0, 1, out _));
            Assert.IsTrue(bag.TryAcquire(stored, out _)); var applied = port.Applied;
            bag.SwapWeaponSlots();
            Assert.That(bag.EquippedAt(1, 1), Is.SameAs(ink)); Assert.That(bag.EquippedAt(0, 1), Is.Null);
            Assert.That(bag.BagAt(0), Is.SameAs(stored)); Assert.That(port.Applied, Is.SameAs(applied));
            Assert.IsTrue(bag.Fits(OfKind(InkKind.Form), 1, 0)); Assert.IsFalse(bag.Fits(OfKind(InkKind.Form), 0, 0));
            Assert.IsFalse(bag.TryMoveEquipment(1, 1, 0, 1, out _), "Ink still cannot move between weapon instances.");
            bag.SwapWeaponSlots(); Assert.That(bag.EquippedAt(0, 1), Is.SameAs(ink));
        }
        static InkInstance Stat(int kind = 1) => new InkInstance(new InkDefinition("stat." + kind, "Stat", "Test", InkKind.Stat, kind));
        static InkInventory Bag(IInkEquipmentPort port = null) => new InkInventory(new[] { "sword", "ranged" }, port);
        static InkInstance OfKind(InkKind kind)=>kind==InkKind.Stat?Stat():kind==InkKind.Special?new InkInstance(new InkDefinition("special.test","Special","",kind,1)):new InkInstance(new InkDefinition("form.test","Form","",kind,formId:"whip",weaponCategory:"sword"));
        [Test] public void AllKindsShareOneFivePlaceBagAndCrossKindExchangeKeepsTheCount()
        {
            var bag=new InkInventory(new[]{"sword"});Assert.AreEqual(5,bag.Capacity);
            foreach(var kind in new[]{InkKind.Form,InkKind.Stat,InkKind.Special,InkKind.Form,InkKind.Stat})Assert.IsTrue(bag.TryAcquire(OfKind(kind),out _));
            foreach(InkKind kind in Enum.GetValues(typeof(InkKind)))Assert.IsFalse(bag.TryAcquire(OfKind(kind),out _));
            Assert.AreEqual(5,bag.Used);var original=bag.BagAt(0);var incoming=OfKind(InkKind.Special);
            Assert.IsTrue(bag.TryExchangeGround(0,incoming,out var outgoing,out _));Assert.AreSame(original,outgoing);Assert.AreSame(incoming,bag.BagAt(0));
            Assert.AreEqual(5,bag.Used);Assert.AreEqual(2,bag.Stored(InkKind.Special));
        }
        // The village bag unlock grows the run's bag (5 -> up to 15); stored items keep their cells and it never shrinks.
        [Test] public void TheBagGrowsToTheRunsSizeAndNeverShrinks()
        {
            var bag=new InkInventory(new[]{"sword"});for(int i=0;i<5;i++)Assert.IsTrue(bag.TryAcquire(Stat(),out _));
            var first=bag.BagAt(0);int changes=0;bag.Changed+=()=>changes++;
            bag.SetCapacity(7);Assert.AreEqual(7,bag.Capacity);Assert.AreSame(first,bag.BagAt(0));Assert.AreEqual(1,changes);
            bag.SetCapacity(7);Assert.AreEqual(1,changes,"the same size changes nothing");
            Assert.IsTrue(bag.TryAcquire(Stat(),out _));Assert.IsTrue(bag.TryAcquire(Stat(),out _));Assert.IsFalse(bag.TryAcquire(Stat(),out _));
            Assert.Throws<ArgumentOutOfRangeException>(()=>bag.SetCapacity(5));Assert.AreEqual(7,bag.Used);
        }
        [Test] public void CurrentColorInksTakeRoomFromEveryKind()
        {
            var bag=new InkInventory(new[]{"sword"});int colored=4;bag.ExternalUsage=()=>colored;
            Assert.IsTrue(bag.TryAcquire(Stat(),out _));Assert.IsFalse(bag.HasRoom);
            foreach(InkKind kind in Enum.GetValues(typeof(InkKind)))Assert.IsFalse(bag.TryAcquire(OfKind(kind),out _));
            colored--;Assert.IsTrue(bag.TryAcquire(OfKind(InkKind.Form),out _));Assert.AreEqual(2,bag.Used);
        }
        sealed class Port : IInkEquipmentPort
        {
            public string Error;
            public EquipmentLayout Applied;
            public string Apply(EquipmentLayout candidate) { if (Error != null) return Error; Applied = candidate; return null; }
        }
        [Test] public void SameContentHasSeparateInstancesAndUsesSeparateSlots()
        {
            var bag = Bag(); var a = Stat(); var b = new InkInstance(a.Definition);
            Assert.That(bag.TryAcquire(a, out _), Is.True); Assert.That(bag.TryAcquire(b, out _), Is.True);
            Assert.That(a.InstanceId, Is.Not.EqualTo(b.InstanceId)); Assert.That(bag.Used, Is.EqualTo(2));
            Assert.That(bag.BagAt(0), Is.SameAs(a)); Assert.That(bag.BagAt(1), Is.SameAs(b));
        }
        [Test] public void FullBagAndDuplicateInstanceRejectWithoutAcquiredEvent()
        {
            var bag = Bag(); int events = 0; bag.Acquired += _ => events++;
            var first = Stat(); bag.TryAcquire(first, out _);
            Assert.That(bag.TryAcquire(first, out _), Is.False);
            for (int i = 1; i < 5; i++) bag.TryAcquire(Stat(), out _);
            Assert.That(bag.TryAcquire(Stat(), out _), Is.False); Assert.That(events, Is.EqualTo(5)); Assert.That(bag.Used, Is.EqualTo(5));
        }
        [Test] public void EquippedInksDoNotCountTowardCapacityAndKeepIdentity()
        {
            var bag = Bag(); var ink = Stat(); bag.TryAcquire(ink, out _);
            Assert.That(bag.TryEquip(0, 0, 1, out _), Is.True); Assert.That(bag.Used, Is.Zero);
            Assert.That(bag.EquippedAt(0, 1), Is.SameAs(ink)); Assert.That(bag.Owns(ink), Is.True);
            Assert.That(bag.TryAcquire(ink, out _), Is.False);
        }
        [Test] public void EquippedInkIsPermanentAndOccupiedSlotsRefuseReplacement()
        {
            var bag = Bag(); var old = Stat(); bag.TryAcquire(old, out _); Assert.That(bag.TryEquip(0, 0, 1, out _), Is.True);
            var replacement = Stat(3); bag.TryAcquire(replacement, out _);
            Assert.That(bag.TryUnequip(0, 1, out string error), Is.False); Assert.That(error, Is.EqualTo(InkInventory.LockedMessage));
            Assert.That(bag.TryEquip(0, 0, 1, out error), Is.False); Assert.That(error, Does.Contain(InkInventory.LockedMessage));
            Assert.That(bag.EquippedAt(0, 1), Is.SameAs(old)); Assert.That(bag.BagAt(0), Is.SameAs(replacement));
            Assert.That(bag.TryEquip(0, 0, 2, out _), Is.True, "an empty slot still accepts ink");
            Assert.That(bag.EquippedAt(0, 2), Is.SameAs(replacement)); Assert.That(bag.Used, Is.Zero);
        }
        [Test] public void EquippedInkCannotMoveToAnotherSlotOrWeapon()
        {
            var bag = Bag(); var a = Stat(1); var b = Stat(3);
            bag.TryAcquire(a, out _); bag.TryEquip(0, 0, 1, out _); bag.TryAcquire(b, out _); bag.TryEquip(0, 1, 1, out _);
            Assert.That(bag.TryMoveEquipment(0, 1, 1, 1, out string error), Is.False); Assert.That(error, Is.EqualTo(InkInventory.LockedMessage));
            Assert.That(bag.TryMoveEquipment(0, 1, 0, 2, out _), Is.False);
            Assert.That(bag.EquippedAt(0, 1), Is.SameAs(a)); Assert.That(bag.EquippedAt(1, 1), Is.SameAs(b)); Assert.That(bag.EquippedAt(0, 2), Is.Null);
        }
        [Test] public void CategoryAndSlotMismatchLeaveBothSidesUntouched()
        {
            var bag = Bag(); var form = new InkInstance(new InkDefinition("whip", "Whip", "", InkKind.Form, 0, "whip", "sword"));
            bag.TryAcquire(form, out _);
            Assert.That(bag.TryEquip(0, 1, 0, out _), Is.False); Assert.That(bag.TryEquip(0, 0, 1, out _), Is.False);
            Assert.That(bag.BagAt(0), Is.SameAs(form)); Assert.That(bag.TryEquip(0, 0, 0, out _), Is.True);
        }
        [Test] public void CombatRejectionDoesNotConsumeBagOrModifyEquipment()
        {
            var port = new Port { Error = "combat" }; var bag = Bag(port); var ink = Stat(); bag.TryAcquire(ink, out _);
            Assert.That(bag.TryEquip(0, 0, 1, out string error), Is.False); Assert.That(error, Is.EqualTo("combat"));
            Assert.That(bag.BagAt(0), Is.SameAs(ink)); Assert.That(bag.EquippedAt(0, 1), Is.Null); Assert.That(port.Applied, Is.Null);
        }
        [Test] public void OldEquipmentSnapshotsDoNotChangeAfterAnotherEdit()
        {
            var bag = Bag(); var before = bag.Equipment; bag.TryAcquire(Stat(), out _); bag.TryEquip(0, 0, 1, out _);
            Assert.That(before.Get(0, 1), Is.Null); Assert.That(bag.Equipment.Get(0, 1), Is.Not.Null);
        }
        [Test] public void FilterMapsToRealSlotsWithoutReorderingOrStacking()
        {
            var bag = Bag(); bag.TryAcquire(Stat(), out _);
            bag.TryAcquire(new InkInstance(new InkDefinition("range", "Range", "", InkKind.Special, 1)), out _);
            bag.TryAcquire(Stat(), out _);
            Assert.That(bag.VisibleSlots(InkKind.Stat), Is.EqualTo(new[] { 0, 2 })); Assert.That(bag.VisibleSlots(null).Count, Is.EqualTo(5));
        }
        [Test] public void GroundExchangeReturnsOriginalInstanceAndNotifiesOnlyIncoming()
        {
            var bag = Bag(); var old = Stat(); var incoming = Stat(3); bag.TryAcquire(old, out _);
            InkInstance notified = null; bag.Acquired += item => notified = item;
            Assert.That(bag.TryExchangeGround(0, incoming, out var outgoing, out _), Is.True);
            Assert.That(outgoing, Is.SameAs(old)); Assert.That(bag.BagAt(0), Is.SameAs(incoming)); Assert.That(notified, Is.SameAs(incoming));
            Assert.That(bag.TryExchangeGround(0, incoming, out _, out _), Is.False);
        }
        [Test] public void ReentrantAcquisitionCannotDuplicateOrConsumeAnotherSlot()
        {
            var bag = Bag(); bool nested = true; bag.Acquired += acquired => nested = bag.TryAcquire(Stat(), out _);
            Assert.That(bag.TryAcquire(Stat(), out _), Is.True); Assert.That(nested, Is.False); Assert.That(bag.Used, Is.EqualTo(1));
        }
        [Test] public void NotificationQueuePreservesEveryIdenticalPickupAndResetClearsPending()
        {
            var bag = Bag(); using (var p = new InventoryPresenter(bag, null))
            {
                var a = Stat(); var b = Stat(); bag.TryAcquire(a, out _); bag.TryAcquire(b, out _);
                p.Tick(10); Assert.That(p.Toast, Is.SameAs(a)); p.Tick(12.3); Assert.That(p.Toast, Is.SameAs(b));
                p.Reset(); p.Tick(100); Assert.That(p.Toast, Is.Null);
            }
        }
        [Test] public void ClosingInventoryReleasesOnlyItsOwnInputAndPause()
        {
            using (var controls = new ControlSession())
            using (var other = controls.OpenScope("dialogue"))
            using (var block = other.BlockInput(InputChannels.Combat))
            using (var p = new InventoryPresenter(Bag(), controls))
            {
                p.Open(); Assert.That(controls.Current.Blocks(InputChannels.Interaction), Is.True);
                Assert.That(controls.Current.SimulationPaused, Is.True); p.Close();
                Assert.That(controls.Current.SimulationPaused, Is.False);
                Assert.That(controls.Current.Blocks(InputChannels.Combat), Is.True);
                Assert.That(controls.Current.Blocks(InputChannels.Interaction), Is.False);
            }
        }
        [Test] public void RuntimeHasNoUnityOrCombatDependency()
        {
            Assert.That(typeof(InkInventory).Assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("UnityEngine") || a.Name.StartsWith("Deviant.Combat")), Is.False);
        }
    }
}
