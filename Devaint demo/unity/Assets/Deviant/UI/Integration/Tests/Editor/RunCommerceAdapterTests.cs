using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Run;
using NUnit.Framework;

namespace Deviant.UI.Integration.Tests
{
    public sealed class RunCommerceAdapterTests
    {
        static readonly string[] Inks = {
            "yellow_basic_attack_movement_speed_increase", "yellow_skill_attack_movement_speed_increase",
            "yellow_weapon_switch_movement_speed_increase", "yellow_dash_use_movement_speed_increase",
            "green_basic_attack_shield", "green_skill_attack_shield" };
        sealed class Host : IRunCommerceHost
        {
            public bool Paused; public Guid? Weapon; public string Ink; public int Opens; public Action OnOpen;
            public void SetCommercePaused(bool paused) { Paused = paused; }
            public void OpenBag(Guid? weaponId, string inkId) { Weapon = weaponId; Ink = inkId; Opens++; OnOpen?.Invoke(); }
        }
        static SketchRunState State(string[] inks = null) => new SketchRunState(
            new Dictionary<string, string[]> { ["base_sword"] = new[] { "base_sword_default", "base_sword_range" },
                ["hammer"] = new[] { "hammer_default" } }, inks ?? Inks, id => id.Split('_')[0], 71);
        static RunEconomy Money(int gold = 10000)
        { var e = new RunEconomy(new EconomyData()); e.Earn(gold); return e; }
        static void Open(SketchRunState state) { state.RecordKill("elite", 100); Assert.IsTrue(state.OpenBanked()); }

        [Test] public void RewardChoiceAcquiresOnceAndReceiptCarriesRealWeaponId()
        {
            var s = State(); Open(s); var h = new Host(); var a = new RunCommerceAdapter(s, Money(), h);
            Assert.AreEqual(3, a.Snapshot().Rewards.Count);
            var card = a.Snapshot().Rewards.First(c => !c.Item.IsInk);
            Assert.AreSame(card.Key, a.Snapshot().Rewards[card.Slot].Key);
            Assert.AreEqual(CommerceResult.Applied, a.Choose(card.Key));
            Assert.AreEqual(CommerceResult.Busy, a.Choose(card.Key));
            var receipt = a.Snapshot().Receipt;
            Assert.AreEqual(1, s.Arsenal.Weapons.Count);
            Assert.AreEqual(s.Arsenal.Weapons[0].Id, receipt.WeaponId);
            Assert.IsFalse(s.Arsenal.Weapons[0].Confirmed);
            Assert.IsTrue(h.Paused);
            Assert.AreEqual(CommerceResult.Applied, a.ContinueReceipt(receipt.Key));
            Assert.IsFalse(h.Paused);
            Assert.AreEqual(CommerceResult.Stale, a.Choose(card.Key));
        }
        [Test] public void InkRegistersBeforeConfigureAndBagClosurePreservesAcquisition()
        {
            var s = State(); Assert.IsTrue(s.OpenStartingInkOffer()); var h = new Host(); var a = new RunCommerceAdapter(s, Money(), h);
            var card = a.Snapshot().Rewards.First(c => c.Item.IsInk);
            a.Choose(card.Key); var receipt = a.Snapshot().Receipt;
            Assert.IsTrue(s.Arsenal.Registered(card.Item.InkId));
            Assert.AreEqual(CommerceResult.Applied, a.ConfigureReceipt(receipt.Key));
            Assert.AreEqual(card.Item.InkId, h.Ink); Assert.IsNull(h.Weapon);
            Assert.IsTrue(h.Paused); Assert.IsTrue(a.Snapshot().BagOpen);
            Assert.AreEqual(CommerceResult.Busy, a.ConfigureReceipt(receipt.Key));
            Assert.AreEqual(1, h.Opens); a.BagClosed();
            Assert.IsFalse(h.Paused); Assert.IsTrue(s.Arsenal.Registered(card.Item.InkId));
            Assert.AreEqual(CommerceResult.Stale, a.ContinueReceipt(receipt.Key));
        }
        [Test] public void QueuedBossWaitsForReceiptAndRejectsOldChoice()
        {
            var s = State(); Open(s); s.OpenBossOffer(); var a = new RunCommerceAdapter(s, Money(), new Host());
            var old = a.Snapshot().Rewards[0]; a.Choose(old.Key);
            Assert.AreEqual(0, a.Snapshot().Rewards.Count);
            var receipt = a.Snapshot().Receipt; a.ContinueReceipt(receipt.Key);
            Assert.IsTrue(a.Snapshot().IsBossReward); Assert.AreEqual(3, a.Snapshot().Rewards.Count);
            Assert.AreEqual(CommerceResult.Stale, a.Choose(old.Key));
        }
        [Test] public void CompareBagPreservesCandidateIdentityAndBlocksChoice()
        {
            var s = State(); Open(s); var h = new Host(); var a = new RunCommerceAdapter(s, Money(), h);
            var card = a.Snapshot().Rewards[0];
            Assert.AreEqual(CommerceResult.Applied, a.CompareBag());
            Assert.AreEqual(CommerceResult.Busy, a.Choose(card.Key)); a.BagClosed();
            Assert.AreSame(card.Key, a.Snapshot().Rewards[0].Key); Assert.IsTrue(h.Paused);
        }
        [Test] public void ShopBuyChargesOnceAndKeepsSoldPlaceOnRevisit()
        {
            var s = State(); var money = Money(); var a = new RunCommerceAdapter(s, money, new Host());
            var shelf = a.Snapshot("forge").Shop; var card = shelf[0];
            Assert.AreEqual(6, shelf.Count); Assert.IsTrue(card.CanAct);
            Assert.AreEqual(CommerceResult.Applied, a.Buy("forge", card.Key));
            Assert.AreEqual(10000 - card.Price, money.Gold);
            Assert.AreEqual(CommerceResult.Busy, a.Buy("forge", card.Key));
            a.ContinueReceipt(a.Snapshot().Receipt.Key);
            Assert.AreEqual(CommerceResult.SoldOut, a.Buy("forge", card.Key));
            a.Snapshot("another"); Assert.IsTrue(a.Snapshot("forge").Shop[0].Sold);
            Assert.IsFalse(shelf[0].Sold, "previous snapshots stay immutable");
            Assert.AreEqual(1, s.Arsenal.Weapons.Count);
        }
        [Test] public void InsufficientFundsDoNotConsumeStockAndCanRetryAfterEarning()
        {
            var s = State(); var money = Money(0); var a = new RunCommerceAdapter(s, money, new Host());
            var card = a.Snapshot("forge").Shop[0]; Assert.IsFalse(card.CanAct);
            Assert.AreEqual(CommerceResult.InsufficientGold, a.Buy("forge", card.Key));
            Assert.AreEqual(0, s.Arsenal.Weapons.Count); Assert.IsFalse(a.Snapshot("forge").Shop[0].Sold);
            money.Earn(card.Price.Value);
            Assert.AreEqual(CommerceResult.Applied, a.Buy("forge", card.Key)); Assert.AreEqual(0, money.Gold);
        }
        [Test] public void InkRerollRejectsOldIdentityAndPreservesUnchangedWeaponKeys()
        {
            var s = State(); var money = Money(); var a = new RunCommerceAdapter(s, money, new Host());
            var before = a.Snapshot("forge").Shop; var old = before.First(c => c.Item.IsInk);
            Assert.IsTrue(s.RerollShop("forge", false));
            Assert.AreEqual(CommerceResult.Stale, a.Buy("forge", old.Key)); Assert.AreEqual(10000, money.Gold);
            var after = a.Snapshot("forge").Shop;
            Assert.AreNotEqual(old.Item.InkId, after[old.Slot].Item.InkId);
            Assert.AreSame(before[0].Key, after[0].Key);
            Assert.AreEqual(CommerceResult.Applied, a.Buy("forge", after[old.Slot].Key));
            Assert.AreEqual(10, s.Arsenal.RemainingInkUses(after[old.Slot].Item.InkId));
        }
        [Test] public void EmptyInkCatalogReturnsOnlyActualWeaponPlaces()
        {
            var s = State(Array.Empty<string>()); var a = new RunCommerceAdapter(s, Money(), new Host());
            var shelf = a.Snapshot("forge").Shop;
            Assert.AreEqual(s.ShopStock("forge").Count, shelf.Count);
            Assert.AreEqual(s.Planner.Rules.ShopWeapons, shelf.Count);
            Assert.IsTrue(shelf.All(c => !c.Empty && !c.Item.IsInk));
        }
        [Test] public void ExpandedShelfExposesAllPlacesAndKeepsKeysOnRevisit()
        {
            var s = State(); s.ExtraShopItems = 2;
            var a = new RunCommerceAdapter(s, Money(), new Host());
            var shelf = a.Snapshot("forge").Shop;
            Assert.AreEqual(10, shelf.Count);
            Assert.AreEqual(5, shelf.Count(c => !c.Item.IsInk));
            Assert.AreEqual(5, shelf.Count(c => c.Item.IsInk));
            var again = a.Snapshot("forge").Shop;
            for (int i = 0; i < shelf.Count; i++) Assert.AreSame(shelf[i].Key, again[i].Key);
            Assert.AreEqual(CommerceResult.Applied, a.Buy("forge", shelf[9].Key));
        }
        [Test] public void WeaponRerollPreservesSoldPlacesAndInvalidatesChangedPlaces()
        {
            var s = State(); var a = new RunCommerceAdapter(s, Money(), new Host());
            var sold = a.Snapshot("forge").Shop[0]; a.Buy("forge", sold.Key);
            a.ContinueReceipt(a.Snapshot().Receipt.Key);
            var before = a.Snapshot("forge").Shop;
            Assert.IsTrue(s.RerollShop("forge", true));
            var after = a.Snapshot("forge").Shop;
            Assert.IsTrue(after[0].Sold); Assert.AreSame(sold.Key, after[0].Key);
            var changed = before.First(c => !c.Sold && !c.Item.IsInk && !ReferenceEquals(c.Key, after[c.Slot].Key));
            Assert.AreEqual(CommerceResult.Stale, a.Buy("forge", changed.Key));
            var ink = before.First(c => c.Item.IsInk); Assert.AreSame(ink.Key, after[ink.Slot].Key);
        }
        [Test] public void FacilityAndStageCannotReuseOldPurchaseKeys()
        {
            var s = State(); var a = new RunCommerceAdapter(s, Money(), new Host());
            var card = a.Snapshot("forge").Shop[0];
            Assert.AreEqual(CommerceResult.Stale, a.Buy("other", card.Key));
            s.StartStage(2); Assert.AreEqual(CommerceResult.Stale, a.Buy("forge", card.Key));
            Assert.AreEqual(0, s.Arsenal.Weapons.Count);
        }
        [Test] public void SynchronousEconomyCallbackCannotBuyTwice()
        {
            var s = State(); var money = Money(); var a = new RunCommerceAdapter(s, money, new Host());
            var card = a.Snapshot("forge").Shop[0]; var nested = CommerceResult.Applied;
            money.Changed += () => nested = a.Buy("forge", card.Key);
            Assert.AreEqual(CommerceResult.Applied, a.Buy("forge", card.Key));
            Assert.AreEqual(CommerceResult.Busy, nested); Assert.AreEqual(1, s.Arsenal.Weapons.Count);
            Assert.AreEqual(10000 - card.Price, money.Gold);
        }
        [Test] public void DisposeReleasesOnlyOwnedPauseAndRejectsFurtherCommands()
        {
            var s = State(); Open(s); var h = new Host(); var a = new RunCommerceAdapter(s, Money(), h);
            a.Snapshot(); Assert.IsTrue(h.Paused); a.Dispose(); Assert.IsFalse(h.Paused);
            Assert.IsNotNull(s.PendingOffer); Assert.AreEqual(0, s.Arsenal.Weapons.Count);
            Assert.Throws<ObjectDisposedException>(() => a.Snapshot()); a.Dispose();
        }
        [Test] public void ExternalRewardConsumptionInvalidatesRenderedChoice()
        {
            var s = State(); Open(s); var a = new RunCommerceAdapter(s, Money(), new Host());
            var old = a.Snapshot().Rewards[0]; s.Choose(0); Open(s);
            Assert.AreEqual(CommerceResult.Stale, a.Choose(old.Key)); Assert.IsNotNull(s.PendingOffer);
        }
        [Test] public void RepeatedInkAcquisitionAddsUsesWithoutReplacingShelf()
        {
            var s = State(Inks.Take(1).ToArray()); var money = Money();
            var a = new RunCommerceAdapter(s, money, new Host());
            var card = a.Snapshot("forge").Shop.First(c => c.Item.IsInk);
            s.Arsenal.RegisterInk(card.Item.InkId);
            Assert.AreSame(card.Key, a.Snapshot("forge").Shop[card.Slot].Key);
            Assert.AreEqual(CommerceResult.Applied, a.Buy("forge", card.Key));
            Assert.AreEqual(20, a.Snapshot().Inks.Single().Remaining);
            Assert.AreEqual(10000 - card.Price, money.Gold);
        }
        [Test] public void InkSnapshotTracksDraftReservationAndConfirmationConsumption()
        {
            var s = State(); var a = new RunCommerceAdapter(s, Money(), new Host());
            var id = Inks[0]; s.Arsenal.RegisterInk(id);
            var prior = s.Arsenal.Acquire("base_sword", "base_sword_default", new string[5]);
            for (int i = 0; i < 5; i++) s.Arsenal.PlaceInk(prior.Id, i, id);
            s.Arsenal.Confirm(prior.Id); // Five uses remain; exercise reservation limits across weapons.
            var w = s.Arsenal.Acquire("base_sword", "base_sword_default", new string[] { "red", "red", null, null, null, null });
            var before = a.Snapshot().Inks.Single();
            for (int i = 0; i < 5; i++) s.Arsenal.PlaceInk(w.Id, i, id);
            var reserved = a.Snapshot().Inks.Single();
            Assert.AreEqual(5, reserved.Remaining); Assert.AreEqual(5, reserved.Reserved); Assert.AreEqual(0, reserved.Available);
            Assert.AreEqual(0, before.Reserved, "previous snapshots are immutable");
            Assert.Throws<InvalidOperationException>(() => s.Arsenal.PlaceInk(w.Id, 5, id));
            s.Arsenal.ClearInk(w.Id, 4); Assert.AreEqual(1, a.Snapshot().Inks.Single().Available);
            s.Arsenal.Confirm(w.Id); var confirmed = a.Snapshot().Inks.Single();
            Assert.AreEqual(1, confirmed.Remaining); Assert.AreEqual(0, confirmed.Reserved); Assert.AreEqual(1, confirmed.Available);
            Assert.Throws<InvalidOperationException>(() => s.Arsenal.PlaceInk(w.Id, 5, id));
        }
        [Test] public void ConfigureReceiptHandlesSynchronousBagClosureWithoutStuckPause()
        {
            var s = State(); Assert.IsTrue(s.OpenStartingInkOffer()); var h = new Host(); var a = new RunCommerceAdapter(s, Money(), h);
            var card = a.Snapshot().Rewards.First(c => c.Item.IsInk); a.Choose(card.Key);
            var receipt = a.Snapshot().Receipt; var nested = CommerceResult.Applied;
            h.OnOpen = () => { a.BagClosed(); nested = a.ContinueReceipt(receipt.Key); };
            Assert.AreEqual(CommerceResult.Applied, a.ConfigureReceipt(receipt.Key));
            Assert.IsFalse(h.Paused, "immediate host closure releases the commerce pause");
            Assert.IsFalse(a.Snapshot().BagOpen); Assert.IsNull(a.Snapshot().Receipt);
            Assert.AreEqual(CommerceResult.Busy, nested, "closure does not permit command reentry");
            Assert.IsTrue(s.Arsenal.Registered(card.Item.InkId));
            Assert.AreEqual(CommerceResult.Stale, a.ConfigureReceipt(receipt.Key));
        }
        [Test] public void CompareBagHandlesSynchronousClosureAndKeepsPendingRewardPaused()
        {
            var s = State(); Open(s); var h = new Host(); var a = new RunCommerceAdapter(s, Money(), h);
            var card = a.Snapshot().Rewards[0]; var nested = CommerceResult.Applied;
            h.OnOpen = () => { a.BagClosed(); nested = a.Choose(card.Key); };
            Assert.AreEqual(CommerceResult.Applied, a.CompareBag());
            Assert.IsFalse(a.Snapshot().BagOpen, "immediate host closure must not leave a phantom bag");
            Assert.IsTrue(h.Paused, "the pending reward still owns the commerce pause");
            Assert.AreSame(card.Key, a.Snapshot().Rewards[0].Key);
            Assert.AreEqual(CommerceResult.Busy, nested, "closure does not permit command reentry");
            Assert.AreEqual(CommerceResult.Applied, a.Choose(card.Key));
            a.ContinueReceipt(a.Snapshot().Receipt.Key); Assert.IsFalse(h.Paused);
        }
    }
}
