using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Run.Tests
{
    public sealed class RogueliteOverhaulTests
    {
        static ProgressionCatalog Catalog() => new ProgressionCatalog(JsonUtility.FromJson<ProgressionData>(File.ReadAllText("../data/combat/roguelite_progression.json")));
        static ProgressionService Service()
        {
            var p = ProfileData.CreateNew(Guid.NewGuid().ToString()); p.runsDied = 1; p.chaptersCleared = 2; p.chapter1BossFirstClear = true; p.pigment = 100000;
            return new ProgressionService(Catalog(), new MemoryProfileStore(p));
        }
        static RunSession FullRun()
        {
            var service = Service();
            foreach (var unlock in service.Catalog.Unlocks)
                Assert.That(service.TryPurchase(unlock.id, out var state), Is.True, unlock.id + ": " + state);
            return new RunFlow(() => 42, JsonUtility.FromJson<EconomyData>(Resources.Load<TextAsset>("Chapter1Economy").text))
                .StartNewRun(0, plan: service.PlanUnarmedRun());
        }
        [Test] public void LiveCatalogRemovesOldEquipmentUnlocksAndSnapshotsApprovedBenefits()
        {
            var catalog = Catalog();
            Assert.That(catalog.Unlocks.Any(u => new[] { "palette_slot_count", "first_slot_wildcard", "bag_slot_count", "ink_part_unlock", "destroyed_ink_gold_bonus" }.Contains(u.effect.kind)), Is.False);
            var run = FullRun();
            Assert.That(run.Plan.Unarmed, Is.True); Assert.That(run.Plan.PaletteSlots, Is.EqualTo(3));
            Assert.That(run.Plan.Wildcard1 || run.Plan.Wildcard2, Is.False); Assert.That(run.Plan.BagSlots, Is.Zero);
            Assert.That(run.Perks.RerollsRemaining, Is.EqualTo(5)); Assert.That(run.Perks.RevivesRemaining, Is.EqualTo(1));
            Assert.That(run.Perks.StartingRewardsRemaining, Is.EqualTo(1)); Assert.That(run.Perks.StartingInkRewardsRemaining, Is.EqualTo(1));
            Assert.That(run.Perks.Settings.ExtraRecoveryUses, Is.EqualTo(2)); Assert.That(run.Perks.Settings.ExtraShopItems, Is.EqualTo(2));
            Assert.That(run.Plan.AttackBonusPercent, Is.EqualTo(40)); Assert.That(run.Plan.MaxHealthBonus, Is.EqualTo(40));
            Assert.That(run.Plan.MoveSpeedBonusPercent, Is.EqualTo(20)); Assert.That(run.Plan.StartingGoldBonus, Is.EqualTo(200));
            Assert.That(run.Perks.Settings.KillGoldBonusPercent, Is.EqualTo(3)); Assert.That(run.Perks.Settings.WeaponBreakGold, Is.EqualTo(40));
        }
        [Test] public void EveryRerollConsumerSharesOneRunBudgetAndFailuresDoNotSpend()
        {
            var run = FullRun();
            Assert.That(run.Perks.TryReroll(() => false), Is.False);
            Assert.That(run.Perks.RerollsRemaining, Is.EqualTo(5));
            for (int i = 0; i < 5; i++) Assert.That(run.Perks.TryReroll(() => true), Is.True);
            Assert.That(run.Perks.TryReroll(() => true), Is.False);
            Assert.That(run.Perks.RerollsRemaining, Is.Zero);
        }
        [Test] public void ChapterTransitionPreservesSpentRevivalRerollsAndDepartureReward()
        {
            var run = FullRun();
            Assert.That(run.Perks.TryReroll(() => true), Is.True);
            Assert.That(run.Perks.TryTakeStartingReward(), Is.True);
            Assert.That(run.Perks.TryRevive(0, 140, out float restored), Is.True); Assert.That(restored, Is.EqualTo(70));
            run.MarkBossDefeated();
            var advance = typeof(RunSession).GetMethod("TryAdvanceChapter", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(advance.Invoke(run, new object[] { 10d }), Is.True);
            Assert.That(run.Perks.RerollsRemaining, Is.EqualTo(4)); Assert.That(run.Perks.RevivesRemaining, Is.Zero);
            Assert.That(run.Perks.TryTakeStartingReward(), Is.False);
        }
        [Test] public void SmallKillBonusesAccumulateButStartingGoldAndRecoveryAreNotMultiplied()
        {
            var run = FullRun(); int before = run.Economy.Gold;
            for (int i = 0; i < 100; i++) run.Perks.AwardKillGold(6);
            Assert.That(run.Economy.Gold, Is.EqualTo(before + 618));
            var id = Guid.NewGuid(); Assert.That(run.Perks.AwardWeaponBreak(id), Is.EqualTo(40));
            Assert.That(run.Perks.AwardWeaponBreak(id), Is.Zero);
            Assert.That(run.Economy.Gold, Is.EqualTo(before + 658));
        }
        [Test] public void OldProfileRefundPreservesIdentityAndCannotRepeatOnReload()
        {
            var catalog = Catalog(); var p = ProfileData.CreateNew(Guid.NewGuid().ToString());
            p.pigment = 25; p.chapter1BossFirstClear = true; p.runsDied = 3;
            p.unlocks = new[] { "palette_slots_4", "palette_wildcard_1", "starting_gold_50" };
            long refund = catalog.Data.retiredUnlocks.Where(u => p.unlocks.Contains(u.id)).Sum(u => (long)u.refund);
            var store = new MemoryProfileStore(p); var first = new ProgressionService(catalog, store);
            Assert.That(first.Snapshot.ProfileId, Is.EqualTo(p.profileId)); Assert.That(first.Snapshot.RunsDied, Is.EqualTo(3));
            Assert.That(first.Snapshot.Pigment, Is.EqualTo(25 + refund)); Assert.That(first.Snapshot.Has("starting_gold_50"), Is.True);
            Assert.That(new ProgressionService(catalog, store).Snapshot.Pigment, Is.EqualTo(25 + refund));
        }
        [Test] public void BaseAugmentsCanAlwaysDrawAndBundlePurchasesOpenAllTheirCards()
        {
            var service = Service(); var plan = service.PlanUnarmedRun();
            var catalog = JsonUtility.FromJson<AugmentCatalog>(Resources.Load<TextAsset>("MaintenanceAugments").text);
            var augments = new MaintenanceAugments(catalog, 42); augments.LockCards(plan.LockedAugments);
            Assert.That(augments.CanDraw, Is.True);
            Assert.That(augments.DrawReward().Any(a => plan.LockedAugments.Contains(a.id)), Is.False);
            // Three themed bundles of four or five cards chain on the shared reroll line (user decision 2026-11-13).
            var bundles = service.Catalog.Unlocks.Where(u => u.effect.kind == ProgressionIds.AugmentBundleUnlock).ToArray();
            Assert.That(bundles.Select(b => b.id), Is.EqualTo(new[] { "augment_bundle_survival", "augment_bundle_burst", "augment_bundle_style" }));
            Assert.That(service.StatusOf(bundles[0].id).State, Is.EqualTo(UnlockState.MissingPrerequisite), "the first bundle waits for the whole reroll node I");
            Assert.That(service.StatusOf(bundles[0].id).MissingPrerequisite, Is.EqualTo("shared_rerolls_4"));
            Assert.That(service.StatusOf(bundles[1].id).MissingPrerequisite, Is.EqualTo(bundles[0].id), "each bundle opens after the one before it");
            foreach (var bundle in bundles)
            {
                Assert.That(bundle.effect.parts.Length, Is.InRange(4, 5));
                foreach (var pre in bundle.prerequisites.Where(p => p.StartsWith("shared_rerolls_")).ToArray())
                    for (var tier = service.Catalog.FirstTier(service.Catalog.Find(pre)); !service.Snapshot.Has(pre); tier = service.Catalog.NextTier(tier))
                        if (!service.Snapshot.Has(tier.id)) Assert.That(service.TryPurchase(tier.id, out var s), Is.True, tier.id + ": " + s);
                Assert.That(service.TryPurchase(bundle.id, out var state), Is.True, bundle.id + ": " + state);
            }
            Assert.That(service.PlanUnarmedRun().LockedAugments, Is.Empty);
        }
        // ---- Sketch-weapon run (the real chapter runs) ----
        static RunInkCatalog RunInks() => JsonUtility.FromJson<RunInkCatalog>(File.ReadAllText("Assets/Deviant/Combat/Resources/RunInks.json"));
        static SketchRunState Sketch(RunInkCatalog inks, int seed = 7) => new SketchRunState(
            new System.Collections.Generic.Dictionary<string, string[]> { ["sword"] = new[] { "0", "1" }, ["blunt"] = new[] { "0", "1" }, ["ranged"] = new[] { "0" } },
            inks.inks.Select(i => i.id).ToArray(), id => inks.Find(id)?.color, seed);

        // User decision 2026-11-13: basic-attack and skill inks are open; status, switch and dash inks come in three bundles.
        [Test] public void InkBundlesLockTheStatusSwitchAndDashRunInksUntilBought()
        {
            var inks = RunInks(); var service = Service(); var catalog = service.Catalog;
            var bundles = catalog.Unlocks.Where(u => u.effect.kind == ProgressionIds.InkBundleUnlock).ToArray();
            Assert.That(bundles.Select(b => b.id), Is.EqualTo(new[] { "ink_bundle_status", "ink_bundle_switch", "ink_bundle_dash" }));
            Assert.That(bundles.Select(b => b.effect.parts.Length), Is.EqualTo(new[] { 3, 6, 6 }));
            var locked = catalog.LockedInkParts(_ => false);
            Assert.That(locked.Count, Is.EqualTo(15));
            Assert.That(locked.All(id => inks.Find(id) != null && inks.Find(id).trigger != "basic_attack" && inks.Find(id).trigger != "skill_attack"), Is.True);
            Assert.That(inks.inks.Length - locked.Count, Is.EqualTo(21), "21 basic-attack and skill inks stay open by default");

            var state = Sketch(inks); state.LockInks(locked);
            Assert.That(state.SupplyInks.Intersect(locked), Is.Empty);
            Assert.That(state.ShopStock("t").Where(o => o.Ink != null).Select(o => o.Ink).Intersect(locked), Is.Empty);
            Assert.That(state.OpenChestOffer(), Is.True);
            Assert.That(state.PendingOffer.Where(c => c.IsInk).Select(c => c.Ink).Intersect(locked), Is.Empty);
        }
        [Test] public void DepartureBonusIsAThreeWeaponOfferOutsideTheGuaranteeAndRerollKeepsItsRule()
        {
            var state = Sketch(RunInks());
            Assert.That(state.OpenStartingOffer(), Is.True); Assert.That(state.OpenStartingOffer(), Is.False, "one offer at a time");
            var first = state.PendingOffer;
            Assert.That(first.Length, Is.EqualTo(3)); Assert.That(state.PendingIsStart && state.PendingIsEvent, Is.True);
            Assert.That(first.All(c => !c.IsInk && c.Weapon.Slots == 4), Is.True, "slot counts drawn among stage 1's counts");
            Assert.That(state.RerollPending(), Is.True);
            Assert.That(state.PendingOffer, Is.Not.SameAs(first)); Assert.That(state.PendingIsStart, Is.True);
            Assert.That(state.Choose(0), Is.Not.Null);
            Assert.That(state.WeaponsPicked, Is.Zero); Assert.That(state.Banked, Is.Zero); Assert.That(state.PendingIsStart, Is.False);
            Assert.That(state.RerollPending(), Is.False, "nothing on screen: nothing to reroll");
        }
        // User decision 2026-11-13: three random unlocked ink kinds; acquisition adds uses and is not banked.
        [Test] public void DepartureInkRewardOffersThreeOpenInkKindsAndRerollsWithinThem()
        {
            var inks = RunInks(); var state = Sketch(inks);
            var locked = Service().Catalog.LockedInkParts(_ => false); state.LockInks(locked);
            Assert.That(state.OpenStartingInkOffer(), Is.True);
            var first = state.PendingOffer;
            Assert.That(first.Length, Is.EqualTo(3)); Assert.That(first.All(c => c.IsInk && !locked.Contains(c.Ink)), Is.True);
            Assert.That(first.Select(c => c.Ink).Distinct().Count(), Is.EqualTo(3));
            Assert.That(state.PendingIsStartInk && state.PendingIsStart, Is.True);
            Assert.That(state.RerollPending(), Is.True);
            Assert.That(state.PendingOffer.All(c => c.IsInk && !locked.Contains(c.Ink)), Is.True, "a reroll keeps the ink rule");
            string picked = state.PendingOffer[1].Ink;
            Assert.That(state.Choose(1), Is.Null); Assert.That(state.Arsenal.Registered(picked), Is.True);
            Assert.That(state.PendingIsStartInk, Is.False); Assert.That(state.Banked, Is.Zero);
            Assert.That(state.Arsenal.RemainingInkUses(picked), Is.EqualTo(10));
            Assert.That(state.SupplyInks, Does.Contain(picked));
            Assert.That(state.OpenStartingOffer(), Is.True, "the departure weapon can follow");
        }
        [Test] public void ShopDisplayUnlockAddsPlacesAndRefreshKeepsSoldGoods()
        {
            var state = Sketch(RunInks()); state.ExtraShopItems = 2;
            var economy = new RunFlow(() => 1, JsonUtility.FromJson<EconomyData>(Resources.Load<TextAsset>("Chapter1Economy").text)).StartNewRun(0).Economy;
            economy.Earn(100000);
            var stock = state.ShopStock("f");
            Assert.That(stock.Count(o => o.Weapon != null), Is.EqualTo(5)); Assert.That(stock.Count(o => o.Ink != null), Is.EqualTo(5));
            state.Buy("f", 0, economy); state.Buy("f", 5, economy);
            var keptWeapon = stock[0]; string keptInk = stock[5].Ink;
            var before = stock.Skip(1).Take(4).Select(o => o.Weapon.LayoutKey).ToArray();
            Assert.That(state.RerollShop("f", true), Is.True);
            stock = state.ShopStock("f");
            Assert.That(stock[0], Is.SameAs(keptWeapon)); Assert.That(stock[0].Sold, Is.True);
            Assert.That(stock.Skip(1).Take(4).Select(o => o.Weapon.LayoutKey), Is.Not.EqualTo(before));
            Assert.That(state.RerollShop("f", false), Is.True);
            Assert.That(state.ShopStock("f")[5].Ink, Is.EqualTo(keptInk)); Assert.That(state.ShopStock("f")[5].Sold, Is.True);
        }

        [Test] public void ExpandedInkShelfKeepsSoldProductsWhenRerolledAndUsesTheCommonBudget()
        {
            var run = FullRun(); var data = JsonUtility.FromJson<GrowthData>(Resources.Load<TextAsset>("WeaponGrowthMvp").text);
            var growth = new WeaponGrowth(data, run.Economy) { Perks = run.Perks };
            run.Economy.Earn(100000);
            var stock = growth.InkStock("test", new System.Random(42));
            Assert.That(stock.Count, Is.InRange(4, 5));
            var bought = growth.BuyStockInk("test", 0); var sold = stock[0];
            Assert.That(growth.RerollInkStock("test", new System.Random(43)), Is.True);
            Assert.That(growth.StockOf("test")[0], Is.SameAs(sold)); Assert.That(sold.Sold, Is.True);
            Assert.That(run.Perks.RerollsRemaining, Is.EqualTo(4));
            Assert.That(growth.Inks.Any(i => i.Id == bought.Id), Is.True);
        }
    }
}
