using System;
using System.Linq;
using NUnit.Framework;

namespace Deviant.Run.Tests
{
    // Worked examples of docs/design/equipment/sketch-weapons.md.
    public sealed class SketchArsenalTests
    {
        static string ColorOf(string ink) => ink.Split('_')[0];
        static SketchArsenal New() => new SketchArsenal(ColorOf);
        static SketchWeapon Ready(SketchArsenal a, string form = "sword", string skill = "slash", params string[] enhance)
        {
            var w = a.Acquire(form, skill, enhance.Length == 0 ? new string[] { null, null, null } : enhance);
            a.Confirm(w.Id); return w;
        }

        [Test]
        public void TotalUsesTrackAcquisitionsWithoutChangingWhenReservedOrSpent()
        {
            var a = new SketchArsenal(ColorOf, new SketchArsenalRules { InkUsesPerAcquisition = 5 });
            Assert.AreEqual(0, a.TotalInkUses("red_burn"));
            a.RegisterInk("red_burn");
            var weapon = a.Acquire("sword", "slash", new string[3]);
            a.PlaceInk(weapon.Id, 0, "red_burn");
            Assert.AreEqual(5, a.TotalInkUses("red_burn"));
            Assert.AreEqual(4, a.AvailableInkUses("red_burn"));
            a.Confirm(weapon.Id);
            Assert.AreEqual(5, a.TotalInkUses("red_burn"));
            Assert.AreEqual(4, a.RemainingInkUses("red_burn"));
            a.RegisterInk("red_burn");
            Assert.AreEqual(10, a.TotalInkUses("red_burn"));
            Assert.AreEqual(9, a.RemainingInkUses("red_burn"));
        }

        [Test]
        public void RenameTracksTheWeaponAcrossSlotsWithoutChangingItsCombatState()
        {
            var arsenal=New();var weapon=Ready(arsenal);arsenal.Equip(weapon.Id,0);
            var durability=weapon.DurabilityLeft;var layout=weapon.LayoutKey;
            int revision=arsenal.Revision;
            arsenal.Rename(weapon.Id,"  새벽의 검  ");
            Assert.That(weapon.CustomName,Is.EqualTo("새벽의 검"));
            Assert.That(arsenal.Revision,Is.GreaterThan(revision));
            arsenal.SwapOrder();
            Assert.That(arsenal.Equipped(1).CustomName,Is.EqualTo("새벽의 검"));
            Assert.That(weapon.LayoutKey,Is.EqualTo(layout));
            Assert.That(weapon.DurabilityLeft,Is.EqualTo(durability));
            Assert.That(arsenal.ChangeCooldownRemaining,Is.EqualTo(0f));
            foreach(var invalid in new[]{"", "   ", new string('가',21), "이름\n줄바꿈", "<b>"+new string('가',20)+"</b>"})
                Assert.Throws<InvalidOperationException>(()=>arsenal.Rename(weapon.Id,invalid));
            Assert.That(weapon.CustomName,Is.EqualTo("새벽의 검"));
            arsenal.Rename(weapon.Id,new string('가',20));
            Assert.That(weapon.CustomName.Length,Is.EqualTo(20));
            var draft=arsenal.Acquire("hammer","slam",new string[]{null});
            Assert.Throws<InvalidOperationException>(()=>arsenal.Rename(draft.Id,"초안"));
            arsenal.Destroy(weapon.Id);
            Assert.Throws<InvalidOperationException>(()=>arsenal.Rename(weapon.Id,"파괴됨"));
        }

        // X swaps A and B once one weapon is equipped: A bare hands, B weapon -> A weapon, B bare hands (2026-11-13).
        // The swap ignores the equipment change cooldown and never starts it (2026-11-16).
        [Test]
        public void SwapOrderIgnoresAndNeverStartsTheChangeCooldown()
        {
            var a = New();
            Assert.IsFalse(a.CanSwapOrder);
            var w = Ready(a); a.Equip(w.Id, SketchArsenal.SlotB);
            a.Tick(.1f); Assert.Greater(a.ChangeCooldownRemaining, 0f);
            Assert.IsTrue(a.CanSwapOrder, "the swap stays free while the change cooldown runs");
            a.SwapOrder(); Assert.AreSame(w, a.Equipped(0));
            a.Tick(5); Assert.AreEqual(0, a.ChangeCooldownRemaining);
            a.SwapOrder(); a.Tick(.1f);
            Assert.AreSame(w, a.Equipped(1));
            Assert.AreEqual(0, a.ChangeCooldownRemaining, "a swap alone starts nothing");
        }

        [Test]
        public void AcquiredWeaponIsUnconfirmedAndCannotBeEquipped()
        {
            var a = New();
            var w = a.Acquire("sword", "slash", new string[] { null, null, null });
            Assert.AreEqual(SketchWeaponState.Unconfirmed, a.StateOf(w));
            Assert.IsFalse(a.CanEquip(w.Id, SketchArsenal.SlotA, out _));
            a.Confirm(w.Id);
            Assert.AreEqual(SketchWeaponState.Stored, a.StateOf(w));
            a.Equip(w.Id, SketchArsenal.SlotA);
            Assert.AreEqual(SketchWeaponState.Equipped, a.StateOf(w));
        }

        [Test]
        public void PaletteLayoutLimitsFollowTheDesignData()
        {
            var a = New();
            Assert.Throws<InvalidOperationException>(() => a.Acquire("sword", "slash", new string[0]));
            Assert.Throws<InvalidOperationException>(() => a.Acquire("sword", "slash", new string[7]));
            Assert.DoesNotThrow(() => a.Acquire("sword", "slash", new string[] { null }));
            Assert.DoesNotThrow(() => a.Acquire("sword", "slash", new string[6]));
            Assert.Throws<InvalidOperationException>(() => a.Acquire("sword", "slash", new[] { "red", "red", "red", null }));
            Assert.Throws<InvalidOperationException>(() => a.Acquire("sword", "slash", new[] { "red", "blue", null }));
            Assert.DoesNotThrow(() => a.Acquire("sword", "slash", new[] { "red", "red", null, null, null }));
        }

        [Test]
        public void TentativeInksMoveFreelyAndSurviveClosingUntilConfirmed()
        {
            var a = New(); a.RegisterInk("red_burn");
            var w = a.Acquire("sword", "slash", new string[] { null, null, null });
            a.PlaceInk(w.Id, 0, "red_burn");
            a.ClearInk(w.Id, 0);
            a.PlaceInk(w.Id, 2, "red_burn");
            // closing the bag keeps the draft: the model simply keeps the state
            Assert.AreEqual("red_burn", w.Slots[2].Ink);
            Assert.IsFalse(w.Slots[2].Fixed);
            a.Confirm(w.Id);
            Assert.IsTrue(w.Slots[2].Fixed);
            Assert.Throws<InvalidOperationException>(() => a.ClearInk(w.Id, 2));
        }

        [Test]
        public void EmptyPaletteConfirmationLocksEverySlotAndRejectsLaterInk()
        {
            var a = New();
            var w = Ready(a);
            a.RegisterInk("blue_chill");
            Assert.That(w.Slots.All(s => s.Fixed && s.Ink == null), Is.True);
            Assert.Throws<InvalidOperationException>(() => a.PlaceInk(w.Id, 1, "blue_chill"));
        }

        [Test]
        public void OneInkReservesSeveralSlotsAndConsumesOnlyOnConfirmation()
        {
            var a = New(); a.RegisterInk("red_burn");
            var sword = a.Acquire("sword", "slash", new string[] { null, null, null });
            var hammer = a.Acquire("hammer", "quake", new[] { "red", null, null });
            a.PlaceInk(sword.Id, 0, "red_burn"); a.PlaceInk(sword.Id, 1, "red_burn"); a.PlaceInk(hammer.Id, 0, "red_burn");
            Assert.AreEqual(1, a.Codex.Count);
            Assert.AreEqual(10, a.RemainingInkUses("red_burn"));
            Assert.AreEqual(3, a.ReservedInkUses("red_burn"));
            Assert.AreEqual(7, a.AvailableInkUses("red_burn"));
            a.Confirm(sword.Id);
            Assert.AreEqual(8, a.RemainingInkUses("red_burn"));
            Assert.AreEqual(1, a.ReservedInkUses("red_burn"));
            Assert.AreEqual(7, a.AvailableInkUses("red_burn"));
            Assert.IsFalse(a.UsesEnhancedValue(sword.Slots[0]), "colourless slot uses the base value");
            Assert.IsTrue(a.UsesEnhancedValue(hammer.Slots[0]), "red-enhanced slot uses the enhanced value for red ink");
        }

        [Test]
        public void InkMustBeAcquiredAndEveryAcquisitionAddsTenUses()
        {
            var a = New();
            var w = a.Acquire("sword", "slash", new string[] { null, null, null });
            Assert.Throws<InvalidOperationException>(() => a.PlaceInk(w.Id, 0, "red_burn"));
            Assert.IsTrue(a.RegisterInk("red_burn"));
            Assert.IsTrue(a.RegisterInk("red_burn"));
            Assert.AreEqual(20, a.RemainingInkUses("red_burn"));
            Assert.DoesNotThrow(() => a.PlaceInk(w.Id, 0, "red_burn"));
        }

        [Test]
        public void ReservationsAcrossDraftsRejectOverbookingAndFailedReplacementKeepsBothReservations()
        {
            var a = new SketchArsenal(ColorOf, new SketchArsenalRules { InkUsesPerAcquisition = 5 }); // Exercise reservation boundaries independently of grant size.
            a.RegisterInk("red_burn"); a.RegisterInk("blue_chill");
            var first = a.Acquire("sword", "slash", new string[6]);
            var second = a.Acquire("hammer", "quake", new string[2]);
            for (int i = 0; i < 5; i++) a.PlaceInk(first.Id, i, "red_burn");
            a.PlaceInk(second.Id, 0, "blue_chill");
            Assert.Throws<InvalidOperationException>(() => a.PlaceInk(second.Id, 0, "red_burn"));
            Assert.AreEqual("blue_chill", second.Slots[0].Ink);
            Assert.AreEqual(1, a.ReservedInkUses("blue_chill"));
            Assert.DoesNotThrow(() => a.PlaceInk(first.Id, 0, "red_burn"), "same slot is not another reservation");
            a.ClearInk(first.Id, 0); a.PlaceInk(second.Id, 0, "red_burn");
            Assert.AreEqual(0, a.ReservedInkUses("blue_chill"));
            Assert.AreEqual(0, a.AvailableInkUses("red_burn"));
            a.Discard(first.Id);
            Assert.AreEqual(4, a.AvailableInkUses("red_burn"));
            a.Destroy(second.Id);
            Assert.AreEqual(5, a.AvailableInkUses("red_burn"), "unconfirmed destruction releases draft reservations");
        }

        [Test]
        public void LastUseKeepsMountedEffectsAndConfirmedLossNeverRefundsReacquisitionRestoresSupply()
        {
            var a = new SketchArsenal(ColorOf, new SketchArsenalRules { InkUsesPerAcquisition = 5 }); // Five uses spread over two drafts.
            a.RegisterInk("red_burn");
            var first = a.Acquire("sword", "slash", new string[4]);
            var last = a.Acquire("hammer", "quake", new string[2]);
            for (int i = 0; i < 4; i++) a.PlaceInk(first.Id, i, "red_burn");
            a.PlaceInk(last.Id, 0, "red_burn");
            a.Confirm(first.Id); a.Confirm(last.Id);
            Assert.AreEqual(0, a.RemainingInkUses("red_burn"));
            Assert.IsEmpty(a.AvailableInks);
            Assert.AreEqual("red_burn", last.Slots[0].Ink);
            Assert.IsTrue(last.Slots[0].Fixed);
            a.Discard(first.Id); a.Destroy(last.Id);
            Assert.AreEqual(0, a.RemainingInkUses("red_burn"));
            a.RegisterInk("red_burn");
            CollectionAssert.AreEqual(new[] { "red_burn" }, a.AvailableInks);
            Assert.AreEqual(5, a.AvailableInkUses("red_burn"));
            a.RegisterInk("red_burn");
            Assert.AreEqual(10, a.AvailableInkUses("red_burn"));
        }

        // Equip, unequip and replace repeat freely while the bag is open (combat time stopped); the first combat
        // step after them starts one shared five-second cooldown (2026-11-16).
        [Test]
        public void BagChangesAreFreeUntilCombatTimeResumesThenShareOneCooldown()
        {
            var a = New();
            var x = Ready(a); var y = Ready(a, "hammer", "quake"); var z = Ready(a, "bow", "volley");
            a.Equip(x.Id, SketchArsenal.SlotA);
            a.Equip(y.Id, SketchArsenal.SlotB);
            a.Equip(z.Id, SketchArsenal.SlotA);
            a.Unequip(SketchArsenal.SlotB);
            Assert.AreEqual(0f, a.ChangeCooldownRemaining, "nothing waits while the bag stays open");
            Assert.IsTrue(a.ChangeCooldownPending);
            a.Tick(1f);
            Assert.AreEqual(4f, a.ChangeCooldownRemaining, 1e-4, "closing the bag starts five seconds of combat time");
            Assert.IsFalse(a.CanEquip(x.Id, SketchArsenal.SlotB, out var reason)); StringAssert.Contains("4초", reason);
            Assert.Throws<InvalidOperationException>(() => a.Unequip(SketchArsenal.SlotA));
            Assert.Throws<InvalidOperationException>(() => a.Equip(y.Id, SketchArsenal.SlotA), "replace shares the cooldown");
            a.Tick(4f);
            Assert.IsTrue(a.CanEquip(x.Id, SketchArsenal.SlotB, out _));
        }

        [Test]
        public void EndingTheBagVisitWithTheSamePairStartsNoCooldown()
        {
            var a = New(); var sword = Ready(a); var bow = Ready(a, "bow", "volley");
            a.Equip(sword.Id, SketchArsenal.SlotA); a.Equip(bow.Id, SketchArsenal.SlotB); a.Tick(5f);
            a.Unequip(SketchArsenal.SlotA); a.Equip(sword.Id, SketchArsenal.SlotA); a.SwapOrder();
            a.Tick(.1f);
            Assert.AreEqual(0f, a.ChangeCooldownRemaining, "same weapons in either order is no change");
        }

        // Two bare hands (every weapon broke) may take a first weapon while the cooldown runs; the cooldown stays.
        [Test]
        public void BareHandsMayEquipAFirstWeaponDuringTheCooldown()
        {
            var a = New(); var sword = Ready(a); var bow = Ready(a, "bow", "volley"); var axe = Ready(a, "axe", "spin");
            a.Equip(sword.Id, SketchArsenal.SlotA); a.Tick(1f);
            Assert.IsFalse(a.CanEquip(bow.Id, SketchArsenal.SlotB, out _));
            a.Destroy(sword.Id);
            Assert.IsTrue(a.CanEquip(bow.Id, SketchArsenal.SlotB, out _), "two bare hands may take a first weapon");
            a.Equip(bow.Id, SketchArsenal.SlotB);
            Assert.AreEqual(4f, a.ChangeCooldownRemaining, 1e-4, "the running cooldown is kept");
            Assert.IsFalse(a.CanEquip(axe.Id, SketchArsenal.SlotA, out _), "a second weapon waits for the cooldown");
            Assert.IsTrue(a.CanSwapOrder);
        }

        [Test]
        public void HitsAndAttacksDoNotLockEquipmentChanges()
        {
            var a = New(); var sword = Ready(a); var bow = Ready(a,"bow","volley");
            a.Equip(sword.Id,0); a.Tick(5f); a.RecordAttackStart(0);
            Assert.AreEqual(0,a.ChangeCooldownRemaining, "combat itself never starts the cooldown");
            Assert.IsTrue(a.CanEquip(bow.Id,0,out _));
        }

        [Test]
        public void ManualUnequipLeavesBareHands()
        {
            var a=New();var sword=Ready(a);a.Equip(sword.Id,0);a.Unequip(0);
            Assert.IsNull(a.BasicAttackWeapon(0));Assert.IsFalse(a.CanUseSkill(0));
            Assert.IsTrue(a.CanEquip(sword.Id,0,out _), "still the same bag visit");
        }

        [Test]
        public void StoredWeaponSkillCooldownFreezesWhileEquippedOnesAdvance()
        {
            var a = New();
            var sword = Ready(a); var bow = Ready(a, "bow", "volley"); var hammer = Ready(a, "hammer", "quake");
            a.Equip(sword.Id, SketchArsenal.SlotA); a.Equip(bow.Id, SketchArsenal.SlotB);
            a.StartSkillCooldown(SketchArsenal.SlotA, 20f);
            a.StartSkillCooldown(SketchArsenal.SlotB, 8f);
            a.Tick(5f);
            Assert.AreEqual(15f, sword.SkillCooldown, 1e-4);
            Assert.AreEqual(3f, bow.SkillCooldown, 1e-4);
            a.Equip(hammer.Id, SketchArsenal.SlotA); // sword to the bag with 15 s left
            a.Tick(30f);
            Assert.AreEqual(15f, sword.SkillCooldown, 1e-4);
            a.Equip(sword.Id, SketchArsenal.SlotA);
            a.Tick(4f);
            Assert.AreEqual(11f, sword.SkillCooldown, 1e-4);
        }

        [Test]
        public void BreakingTheCurrentWeaponKeepsKeysAndCooldown()
        {
            var a = New();
            var sword = Ready(a); var bow = Ready(a, "bow", "volley"); var hammer = Ready(a, "hammer", "quake"); var axe = Ready(a, "axe", "spin");
            a.Equip(sword.Id, SketchArsenal.SlotA); a.Equip(bow.Id, SketchArsenal.SlotB);
            a.Tick(5f); a.Equip(hammer.Id, SketchArsenal.SlotA);
            a.RecordAttackStart(SketchArsenal.SlotA);
            a.Tick(3f);
            a.Destroy(hammer.Id);
            Assert.AreEqual(SketchArsenal.SlotB, a.ActiveSlot, "the remaining weapon becomes current");
            Assert.AreSame(bow, a.Equipped(SketchArsenal.SlotB), "it stays on its own keys");
            Assert.IsNull(a.Equipped(SketchArsenal.SlotA));
            Assert.AreEqual(2f, a.ChangeCooldownRemaining, 1e-4, "breaking does not change the cooldown");
            Assert.IsFalse(a.CanEquip(axe.Id, SketchArsenal.SlotA, out _));
            Assert.IsFalse(a.RecordAttackStart(SketchArsenal.SlotB), "a break-driven change is not a switch");
        }

        [Test]
        public void WeaponBreakingInTheBagIsRemovedWithNotice()
        {
            var a = New();
            var sword = Ready(a);
            bool? stored = null; a.WeaponDestroyed += (_, s) => stored = s;
            a.Destroy(sword.Id);
            Assert.AreEqual(true, stored);
            Assert.IsFalse(a.Bag.Any());
        }

        [Test]
        public void DiscardRemovesOneBagWeaponOnly()
        {
            var a = New();
            var sword = Ready(a); var other = a.Acquire("sword", "slash", new string[] { null, null, null });
            a.Equip(sword.Id, SketchArsenal.SlotA);
            Assert.Throws<InvalidOperationException>(() => a.Discard(sword.Id));
            a.Discard(other.Id);
            Assert.AreEqual(1, a.Weapons.Count);
        }

        [Test]
        public void MaxDurabilityScalesWithSlotCount()
        {
            var a = New();
            Assert.AreEqual(3500f, a.Acquire("sword", "slash", new string[] { null }).MaxDurability);
            Assert.AreEqual(6500f, a.Acquire("sword", "slash", new string[3]).MaxDurability);
            Assert.AreEqual(11000f, a.Acquire("sword", "slash", new string[6]).MaxDurability);
        }

        [Test]
        public void DepletingHitBreaksAfterDamageAndLaterChargesAreIgnored()
        {
            var a = New();
            var sword = Ready(a, "sword", "slash", new string[] { null }); // 3,500
            a.Equip(sword.Id, SketchArsenal.SlotA);
            Assert.IsFalse(a.ChargeDurability(sword.Id, 3495f));
            Assert.AreEqual(5f, sword.DurabilityLeft, 1e-3);
            // three targets of one sweep lose 10 HP each: the caller sums them, the sword breaks
            Assert.IsTrue(a.ChargeDurability(sword.Id, 30f));
            Assert.AreEqual(SketchWeaponState.Destroyed, a.StateOf(sword));
            Assert.IsNull(a.Equipped(SketchArsenal.SlotA));
            Assert.IsFalse(a.ChargeDurability(sword.Id, 6f), "poison of a destroyed weapon charges nothing");
        }

        [Test]
        public void DelayedDamageWhileStoredStillChargesTheSourceWeapon()
        {
            var a = New();
            var sword = Ready(a, "sword", "slash", new string[] { null });
            bool? stored = null; a.WeaponDestroyed += (_, s) => stored = s;
            a.ChargeDurability(sword.Id, 3500f);
            Assert.AreEqual(true, stored);
        }

        [Test]
        public void LayoutKeyIgnoresSlotOrder()
        {
            var a = New();
            var x = a.Acquire("sword", "slash", new[] { "red", null, null });
            var y = a.Acquire("sword", "slash", new[] { null, "red", null });
            var z = a.Acquire("sword", "pierce", new[] { null, "red", null });
            Assert.AreEqual(x.LayoutKey, y.LayoutKey);
            Assert.AreNotEqual(x.LayoutKey, z.LayoutKey);
        }
    }
}
