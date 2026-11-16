using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Run.Tests
{
    public sealed class ProgressionTests
    {
        // Version-1 fixtures exercise backwards-compatible transactions and legacy profile input.
        // The live revision-2 catalog and migration are covered by RogueliteOverhaulTests.
        const string SourcePath = "Tests/Fixtures/roguelite-progression-v1.json";
        static ProgressionData Source() => JsonUtility.FromJson<ProgressionData>(File.ReadAllText(SourcePath));
        static ProgressionCatalog Catalog(Action<ProgressionData> edit = null) { var d = Source(); edit?.Invoke(d); return new ProgressionCatalog(d); }
        static ProgressionService Service(MemoryProfileStore store = null, Action<ProgressionData> edit = null) => new ProgressionService(Catalog(edit), store ?? new MemoryProfileStore());

        [Test] public void UnarmedDepartureKeepsPermanentUpgradesAndSettlementGates()
        {
            var service = Service();
            Assert.IsFalse(service.StationOpen("supply_counter"));
            Assert.IsFalse(service.StationOpen("palette_bench"));
            Assert.IsFalse(service.StationOpen("reclaim_bench"));
            var first = service.PlanUnarmedRun();
            Assert.IsTrue(first.Unarmed);
            Assert.IsEmpty(first.Blueprints);
            Assert.AreEqual(1, service.Snapshot.RunsStarted);
            Assert.IsTrue(service.SettleRun(first.RunId, RunOutcome.Died, false));
            Assert.IsTrue(service.StationOpen("supply_counter"));
            Assert.IsFalse(service.StationOpen("palette_bench"));
            Assert.IsFalse(service.StationOpen("reclaim_bench"));
            Assert.IsTrue(service.DebugSetTier("starting_gold_50", 1, true));
            Assert.IsTrue(service.DebugSetTier("palette_slots_4", 1, true));
            var next = service.PlanUnarmedRun();
            Assert.IsTrue(next.Unarmed);
            Assert.AreEqual(50, next.StartingGoldBonus);
            Assert.AreEqual(4, next.PaletteSlots);
            Assert.IsTrue(service.SettleRun(next.RunId, RunOutcome.ChapterCleared, false, 1));
            Assert.IsTrue(service.StationOpen("palette_bench"));
            Assert.IsFalse(service.StationOpen("reclaim_bench"));
            Assert.IsTrue(service.SettleRun(service.PlanUnarmedRun().RunId, RunOutcome.ChapterCleared, false, 2));
            Assert.IsTrue(service.StationOpen("reclaim_bench"));
        }

        [Test] public void ForcedTierPersistsWithoutClearConditionsAndReportsWhatWasSkipped()
        {
            var store=new MemoryProfileStore();var service=Service(store);
            store.FailNextSave=true;
            Assert.IsFalse(service.DebugSetTier("palette_slots_4",2,force:true));
            Assert.IsFalse(service.Snapshot.Has("palette_slots_5"));Assert.IsNull(service.LastDebugNotice);
            Assert.IsTrue(service.DebugSetTier("palette_slots_4",2,force:true),service.LastSaveError);
            StringAssert.Contains("1챕터 보스 처치",service.LastDebugNotice);
            StringAssert.Contains("팔레트 조각",service.LastDebugNotice);
            StringAssert.Contains(service.Catalog.Data.currency.displayName,service.LastDebugNotice);
            StringAssert.Contains("임의 설정 완료",service.LastDebugNotice);
            Assert.AreEqual(0,service.Snapshot.Pigment);Assert.AreEqual(0,service.Snapshot.ChaptersCleared);
            var saved=store.Load(_=>null,out _);
            var roundTrip=JsonUtility.FromJson<ProfileData>(JsonUtility.ToJson(saved));
            Assert.IsEmpty(ProfileData.Validate(roundTrip,service.Catalog));
            var loaded=new ProgressionService(service.Catalog,new MemoryProfileStore(roundTrip));
            Assert.IsNull(loaded.LoadWarning);Assert.IsTrue(loaded.Snapshot.Has("palette_slots_5"));
            Assert.AreEqual(5,loaded.PlanRun(new[]{"sword","blunt"}).PaletteSlots);
            Assert.AreEqual(0,loaded.Snapshot.ChaptersCleared);
            Assert.IsTrue(loaded.DebugSetPigment(1));
            Assert.IsTrue(loaded.DebugSetTier("palette_slots_4",0,force:true));
            Assert.IsFalse(loaded.Snapshot.Has("palette_slots_4"));
        }

        [Test] public void DebugChangesSaveAtomicallyAndRejectInvalidUnlocks()
        {
            var store = new MemoryProfileStore();
            var service = Service(store);
            Assert.IsTrue(service.DebugSetPigment(123));
            Assert.AreEqual(123, new ProgressionService(service.Catalog, store).Snapshot.Pigment);
            store.FailNextSave = true;
            Assert.IsFalse(service.DebugSetPigment(456));
            Assert.AreEqual(123, service.Snapshot.Pigment);
            Assert.IsFalse(service.DebugSetPigment(-1));
            Assert.IsFalse(service.DebugSetUnlock("missing", true));
            Assert.IsFalse(service.DebugSetUnlock("palette_slots_5", true));
            Assert.IsFalse(service.Snapshot.Has("palette_slots_5"));
            Assert.IsTrue(service.SettleRun(Guid.NewGuid(), RunOutcome.ChapterCleared, false, 4));
            Assert.IsTrue(service.DebugSetTier("palette_slots_4", 2));
            Assert.IsTrue(service.Snapshot.Has("palette_slots_4") && service.Snapshot.Has("palette_slots_5"));
            Assert.IsFalse(service.DebugSetUnlock("palette_slots_4", false), "Higher tiers still depend on this unlock");
            Assert.IsTrue(service.DebugSetTier("palette_slots_4", 0));
            Assert.IsFalse(new ProgressionService(service.Catalog, store).Snapshot.Has("palette_slots_5"));
        }

        // Grants pigment through real kills and records the boss clear through a settled run; further clears without kills
        // bring the village palette pieces to `clears`.
        static Guid ClearedWithPigment(ProgressionService service, int bossKills, int clears = 1)
        {
            var run = service.PlanRun(new[] { "sword", "blunt" }).RunId;
            for (int i = 0; i < bossKills; i++) service.AwardPigment(run, "boss" + i, "boss", false);
            Assert.IsTrue(service.FlushPigment());
            Assert.IsTrue(service.SettleRun(run, RunOutcome.ChapterCleared, false));
            // Each further clear beats the next chapter (one piece each).
            for (int i = 1; i < clears; i++) Assert.IsTrue(service.SettleRun(service.PlanRun(new[] { "sword", "blunt" }).RunId, RunOutcome.ChapterCleared, false, i + 1));
            return run;
        }

        [Test] public void EachNewChapterClearGivesAPieceThatOpensTheNextWorkbench()
        {
            var service = Service();
            Assert.AreEqual(5, service.Catalog.PalettePieces);
            Assert.AreEqual(0, service.PalettePieces);
            Assert.IsTrue(service.StationOpen("record_desk") && service.StationOpen("departure_table") && service.StationOpen("return_well"), "always open");
            Assert.IsFalse(service.StationOpen("palette_bench"));
            Assert.AreEqual(1, service.PiecesFor("palette_bench")); Assert.AreEqual(0, service.PiecesFor("supply_counter")); Assert.AreEqual(2, service.PiecesFor("reclaim_bench"));
            Assert.IsFalse(service.StationOpen("supply_counter"), "the supply office waits for the first settled run");
            ClearedWithPigment(service, 10);
            Assert.AreEqual(1, service.PalettePieces);
            Assert.IsTrue(service.StationOpen("palette_bench")); Assert.IsTrue(service.StationOpen("supply_counter"), "a settled run opens the supply office");
            Assert.AreEqual(UnlockState.Available, service.StatusOf("palette_slots_4").State);
            Assert.AreEqual(UnlockState.Available, service.StatusOf("starting_gold_50").State, "the supply office needs no piece");
            Assert.IsTrue(service.TryPurchase("palette_slots_4", out _));
            Assert.AreEqual(UnlockState.LockedUntilPieces, service.StatusOf("palette_slots_5").State, "the fifth slot waits for the fourth piece");
            // Clearing chapter 1 again gives nothing: a piece comes with each chapter beaten for the first time.
            Assert.IsTrue(service.SettleRun(service.PlanRun(new[] { "sword", "blunt" }).RunId, RunOutcome.ChapterCleared, false, 1));
            Assert.AreEqual(1, service.PalettePieces, "a second chapter-1 clear brings no piece");
            for (int clears = 2; clears <= 7; clears++)
            {
                Assert.IsTrue(service.SettleRun(service.PlanRun(new[] { "sword", "blunt" }).RunId, RunOutcome.ChapterCleared, false, clears));
                Assert.AreEqual(Math.Min(5, clears), service.PalettePieces, "one piece per chapter, five in all");
                Assert.IsTrue(service.StationOpen("supply_counter")); Assert.AreEqual(clears >= 2, service.StationOpen("reclaim_bench"));
                Assert.AreEqual(clears >= 4 ? UnlockState.Available : UnlockState.LockedUntilPieces, service.StatusOf("palette_slots_5").State);
            }
            Assert.Throws<ArgumentException>(() => Catalog(d => d.buildings[0].openAtPieces = 6), "beyond the pieces");
        }

        // Chapter 1's exit always leads on to chapter 2; chapter 2 is the last chapter this build has, so its clear goes
        // home. A death goes home from either (user decision 2026-11-13). Each chapter's piece is kept on its first clear.
        [Test] public void ChapterOneLeadsOnAndTheLastChapterGoesHome()
        {
            var service = Service();
            Assert.IsFalse(service.ChapterOneClearEndsRun, "even the first chapter-1 clear leads on to chapter 2");
            Assert.IsTrue(service.ChapterClearEndsRun(2), "chapter 2 is the last chapter");
            var run = service.PlanRun(new[] { "sword", "blunt" }).RunId;
            Assert.IsTrue(service.RecordChapterOneClear(run, false), "leaving chapter 1 on the way to chapter 2");
            Assert.AreEqual(1, service.ChaptersCleared); Assert.AreEqual(1, service.PalettePieces, "the first chapter-1 clear keeps its piece");
            Assert.IsTrue(service.SettleRun(run, RunOutcome.ChapterCleared, false, 2));
            Assert.AreEqual(2, service.ChaptersCleared); Assert.AreEqual(2, service.PalettePieces);
            Assert.IsFalse(service.ChapterOneClearEndsRun, "still leads on once beaten");
            Assert.IsTrue(service.ChapterClearEndsRun(2), "chapter 2 still goes home once beaten: there is no chapter 3 yet");
            Assert.AreEqual(2, ProgressionService.LastChapter);
        }

        [Test] public void LegacyCatalogRetainsItsOriginalTrialValues()
        {
            var catalog = Catalog();
            // User decision 2026-11-05 (recommended): elites and bosses pay twice as much, prices rise (trial-3).
            Assert.AreEqual(1, catalog.PigmentFor("normal")); Assert.AreEqual(10, catalog.PigmentFor("elite")); Assert.AreEqual(40, catalog.PigmentFor("boss"));
            Assert.AreEqual(45, catalog.Unlocks.Count);
            Assert.IsTrue(catalog.Unlocks.All(catalog.PriceKnown));
            CollectionAssert.AreEqual(new[] { "palette_slots_4" }, catalog.Find("palette_slots_5").prerequisites);
            // Palette and wildcard wait for the boss, recovery for the chapter-2 boss with its workshop (user decision
            // 2026-11-05); gold, attack and health open after the first settled run.
            foreach (var id in new[] { "palette_slots_4", "palette_slots_5", "palette_wildcard_1", "palette_wildcard_2" })
                Assert.AreEqual(ProgressionIds.BossGate, catalog.Find(id).requiredClear, id);
            Assert.AreEqual(ProgressionIds.Chapter2Gate, catalog.Find("destroyed_ink_gold_20").requiredClear);
            foreach (var id in new[] { "starting_gold_50", "starting_gold_100", "attack_bonus_1", "max_health_bonus_4" })
                Assert.AreEqual(ProgressionIds.FirstRunGate, catalog.Find(id).requiredClear, id);
            // Eight tiers with stepped prices: 1-4 after the first run, 5-6 after the chapter-1 boss, 7-8 after the chapter-2 boss.
            int[] costs = { 30, 45, 65, 90, 120, 160, 210, 270 };
            for (int i = 0; i < 8; i++)
            {
                var attack = catalog.Find("attack_bonus_" + (i + 1)); var health = catalog.Find("max_health_bonus_" + (i + 1));
                Assert.AreEqual(5 * (i + 1), attack.effect.value, "attack tiers carry their total: +5% each up to +40%");
                Assert.AreEqual(5 * (i + 1), health.effect.value, "health tiers carry their total: +5 each up to +40");
                Assert.AreEqual(costs[i], attack.cost); Assert.AreEqual(costs[i], health.cost);
                string gate = i < 4 ? ProgressionIds.FirstRunGate : i < 6 ? ProgressionIds.BossGate : ProgressionIds.Chapter2Gate;
                Assert.AreEqual(gate, attack.requiredClear); Assert.AreEqual(gate, health.requiredClear);
                Assert.AreEqual("supply_office", attack.building); Assert.AreEqual("supply_office", health.building);
            }
            Assert.AreEqual(catalog.Find("attack_bonus_2"), catalog.NextTier(catalog.Find("attack_bonus_1")));
            Assert.AreEqual(catalog.Find("attack_bonus_1"), catalog.FirstTier(catalog.Find("attack_bonus_8")));
            Assert.AreEqual(catalog.Find("palette_wildcard_2"), catalog.NextTier(catalog.Find("palette_wildcard_1")), "weapon 1 then weapon 2 is one wildcard line");
            Assert.AreEqual(200, catalog.Find("starting_gold_200").effect.amount); Assert.AreEqual(ProgressionIds.Chapter2Gate, catalog.Find("starting_gold_150").requiredClear);
        }

        // User decision 2026-11-05: the shared bag grows 5 -> 15 by two per tier, a move-speed line sits beside attack
        // and health, and the extra dash follows it after the chapter-2 boss.
        [Test] public void BagMoveSpeedAndDashLinesUseTheDecidedTrialValues()
        {
            var catalog = Catalog();
            int[] bagCosts = { 45, 70, 100, 130, 170 };
            for (int i = 0; i < 5; i++)
            {
                var bag = catalog.Find("bag_slots_" + (i + 1));
                Assert.AreEqual(ProgressionIds.BagSlotCount, bag.effect.kind);
                Assert.AreEqual(5 + 2 * (i + 1), bag.effect.value, "bag tiers carry the whole bag: 7, 9, 11, 13, 15");
                Assert.AreEqual(bagCosts[i], bag.cost); Assert.AreEqual("supply_office", bag.building);
                Assert.AreEqual(i < 3 ? ProgressionIds.BossGate : ProgressionIds.Chapter2Gate, bag.requiredClear);
                CollectionAssert.AreEqual(i == 0 ? new string[0] : new[] { "bag_slots_" + i }, bag.prerequisites);
            }
            int[] costs = { 30, 45, 65, 90 };
            for (int i = 0; i < 4; i++)
            {
                var move = catalog.Find("move_speed_bonus_" + (i + 1));
                Assert.AreEqual(ProgressionIds.MoveSpeedBonusPercent, move.effect.kind);
                Assert.AreEqual(5 * (i + 1), move.effect.value); Assert.AreEqual(costs[i], move.cost);
                Assert.AreEqual(ProgressionIds.FirstRunGate, move.requiredClear); Assert.AreEqual("supply_office", move.building);
            }
            var dash = catalog.Find("extra_dash_1");
            Assert.AreEqual(ProgressionIds.DashCharges, dash.effect.kind); Assert.AreEqual(2, dash.effect.value);
            Assert.AreEqual(400, dash.cost); Assert.AreEqual(ProgressionIds.Chapter2Gate, dash.requiredClear);
            CollectionAssert.AreEqual(new[] { "move_speed_bonus_4" }, dash.prerequisites);
            Assert.IsNull(catalog.NextTier(catalog.Find("move_speed_bonus_4")), "the dash is its own line after the speed line");
        }

        [Test] public void BagMoveSpeedAndDashUnlocksReachTheDeparturePlan()
        {
            var service = Service();
            var fresh = service.PlanRun(new[] { "sword", "blunt" });
            Assert.AreEqual(0, fresh.BagSlots, "no tier: the run keeps its base bag"); Assert.AreEqual(0, fresh.MoveSpeedBonusPercent); Assert.AreEqual(1, fresh.DashCharges);
            ClearedWithPigment(service, 50);
            Assert.AreEqual(UnlockState.Available, service.StatusOf("bag_slots_1").State, "the chapter-1 clear opens the bag line");
            Assert.AreEqual(UnlockState.Available, service.StatusOf("move_speed_bonus_1").State);
            foreach (var id in new[] { "bag_slots_1", "bag_slots_2", "bag_slots_3", "move_speed_bonus_1", "move_speed_bonus_2", "move_speed_bonus_3", "move_speed_bonus_4" })
                Assert.IsTrue(service.TryPurchase(id, out var state), id + " " + state);
            Assert.AreEqual(UnlockState.LockedUntilChapter2Clear, service.StatusOf("bag_slots_4").State);
            Assert.AreEqual(UnlockState.LockedUntilChapter2Clear, service.StatusOf("extra_dash_1").State, "the dash waits for the chapter-2 boss");
            var mid = service.PlanRun(new[] { "sword", "blunt" });
            Assert.AreEqual(11, mid.BagSlots); Assert.AreEqual(20, mid.MoveSpeedBonusPercent); Assert.AreEqual(1, mid.DashCharges);
            Assert.IsTrue(service.SettleRun(service.PlanRun(new[] { "sword", "blunt" }).RunId, RunOutcome.ChapterCleared, false, 2));
            foreach (var id in new[] { "bag_slots_4", "bag_slots_5", "extra_dash_1" })
                Assert.IsTrue(service.TryPurchase(id, out var state), id + " " + state);
            var full = service.PlanRun(new[] { "sword", "blunt" });
            Assert.AreEqual(15, full.BagSlots); Assert.AreEqual(20, full.MoveSpeedBonusPercent); Assert.AreEqual(2, full.DashCharges);
            Assert.AreEqual(50 * 40 - (45 + 70 + 100 + 30 + 45 + 65 + 90 + 130 + 170 + 400), service.Snapshot.Pigment);
        }

        [Test] public void TheExtraDashNeedsTheWholeSpeedLine()
        {
            var service = Service();
            ClearedWithPigment(service, 50, clears: 2);
            Assert.AreEqual(UnlockState.MissingPrerequisite, service.StatusOf("extra_dash_1").State);
            Assert.AreEqual("move_speed_bonus_4", service.StatusOf("extra_dash_1").MissingPrerequisite);
            Assert.IsFalse(service.TryPurchase("extra_dash_1", out _));
        }

        [Test] public void InvalidCatalogsAreRejected()
        {
            Assert.Throws<ArgumentException>(() => Catalog(d => d.unlocks[1].id = d.unlocks[0].id), "duplicate id");
            Assert.Throws<ArgumentException>(() => Catalog(d => d.unlocks[0].prerequisites = new[] { "nope" }), "missing prerequisite");
            Assert.Throws<ArgumentException>(() => Catalog(d => d.unlocks[0].prerequisites = new[] { "palette_slots_5" }), "cycle 4 <-> 5");
            Assert.Throws<ArgumentException>(() => Catalog(d => d.unlocks[0].cost = 0), "a decided price of 0 would make it free");
            Assert.Throws<ArgumentException>(() => Catalog(d => d.unlocks[0].cost = -5), "negative price");
            Assert.Throws<ArgumentException>(() => Catalog(d => d.unlocks[0].effect.kind = "max_health"), "unknown effect");
            Assert.Throws<ArgumentException>(() => Catalog(d => d.currency.dropAmounts.normal = -1), "negative pigment");
            Assert.Throws<ArgumentException>(() => Catalog(d => d.unlocks[0].requiredClear = "chapter3_boss_first_clear"), "unknown open condition");
            Assert.Throws<ArgumentException>(() => Catalog(d => d.unlocks.First(u => u.id == "extra_dash_1").effect.value = 1), "an extra dash must add a charge");
            Assert.Throws<ArgumentException>(() => Catalog(d => d.unlocks.First(u => u.id == "move_speed_bonus_1").effect.value = 0), "move speed bonus must be positive");
            Assert.Throws<ArgumentException>(() => Catalog(d => d.unlocks.First(u => u.id == "palette_slots_5").requiredClear = ProgressionIds.FirstRunGate), "a tier opens before its parent");
            Assert.Throws<ArgumentException>(() => Catalog(d => d.progressGates = new[] { ProgressionIds.BossGate }), "the gate order is fixed");
        }

        [Test] public void PendingPriceStaysDisabledEvenThoughJsonReadsNullAsZero()
        {
            var service = Service(edit: d => { d.unlocks[0].costStatus = "pending"; d.unlocks[0].cost = 0; });
            ClearedWithPigment(service, 5, clears: 2);
            Assert.AreEqual(UnlockState.PricePending, service.StatusOf("palette_slots_4").State);
            Assert.IsFalse(service.TryPurchase("palette_slots_4", out _));
            Assert.AreEqual(200, service.Snapshot.Pigment, "nothing was charged");
            Assert.AreEqual(UnlockState.Available, service.StatusOf("starting_gold_50").State, "only the undecided trade is disabled");
        }

        [Test] public void PurchasesWaitForTheFirstBossClearAndChargeExactlyOnce()
        {
            var store = new MemoryProfileStore(); var service = Service(store);
            var run = service.PlanRun(new[] { "sword", "ranged" }).RunId;
            for (int i = 0; i < 30; i++) service.AwardPigment(run, "n" + i, "normal", false);
            Assert.AreEqual(30, service.Snapshot.Pigment, "pigment is collected before the gate opens");
            Assert.AreEqual(UnlockState.LockedUntilBossClear, service.StatusOf("palette_slots_4").State);
            Assert.IsFalse(service.TryPurchase("palette_slots_4", out _));
            Assert.IsTrue(service.SettleRun(run, RunOutcome.Died, false));
            Assert.AreEqual(UnlockState.LockedUntilBossClear, service.StatusOf("palette_slots_4").State, "a death does not open the shop");

            ClearedWithPigment(service, 3, clears: 4);
            Assert.AreEqual(150, service.Snapshot.Pigment);
            Assert.AreEqual(UnlockState.MissingPrerequisite, service.StatusOf("palette_slots_5").State);
            Assert.IsTrue(service.TryPurchase("palette_slots_4", out var state)); Assert.AreEqual(UnlockState.Owned, state);
            Assert.IsFalse(service.TryPurchase("palette_slots_4", out state), "a repeated click finds it owned"); Assert.AreEqual(UnlockState.Owned, state);
            Assert.AreEqual(30, service.Snapshot.Pigment);
            Assert.AreEqual(UnlockState.NotEnoughPigment, service.StatusOf("palette_slots_5").State);
            Assert.IsFalse(service.TryPurchase("palette_slots_5", out _));
            Assert.AreEqual(30, service.Snapshot.Pigment);

            var reloaded = new ProgressionService(service.Catalog, store);
            Assert.AreEqual(service.Snapshot.ProfileId, reloaded.Snapshot.ProfileId);
            Assert.IsTrue(reloaded.Snapshot.Has("palette_slots_4")); Assert.AreEqual(30, reloaded.Snapshot.Pigment);
        }

        // Gold, attack and health open after the first run ends (death or return), before any boss clear.
        [Test] public void PreBossUnlocksOpenAfterTheFirstSettledRun()
        {
            var store = new MemoryProfileStore(); var service = Service(store);
            var run = service.PlanRun(new[] { "sword", "blunt" }).RunId;
            for (int i = 0; i < 70; i++) service.AwardPigment(run, "n" + i, "normal", false);
            Assert.AreEqual(UnlockState.LockedUntilFirstRun, service.StatusOf("attack_bonus_1").State, "a run under way has not ended yet");
            Assert.IsFalse(service.StationOpen("supply_counter")); Assert.IsTrue(service.StationGated("supply_counter"));
            Assert.AreEqual("첫 런을 마치면 열림", service.StationNeed("supply_counter"));
            Assert.AreEqual("2챕터 클리어 필요", service.StationNeed("reclaim_bench")); Assert.AreEqual("", service.StationNeed("record_desk"));
            Assert.IsFalse(service.TryPurchase("attack_bonus_1", out _));
            Assert.IsTrue(service.SettleRun(run, RunOutcome.Died, false));
            Assert.IsTrue(service.Snapshot.FirstRunSettled); Assert.IsFalse(service.Snapshot.BossFirstClear);
            Assert.IsTrue(service.StationOpen("supply_counter"), "a death opens the supply office before any piece");
            Assert.AreEqual(0, service.PalettePieces); Assert.IsFalse(service.StationOpen("palette_bench"));
            Assert.AreEqual(UnlockState.Available, service.StatusOf("attack_bonus_1").State, "a death opens the pre-boss lines");
            Assert.AreEqual(UnlockState.Available, service.StatusOf("max_health_bonus_1").State);
            Assert.AreEqual(UnlockState.Available, service.StatusOf("starting_gold_50").State);
            Assert.AreEqual(UnlockState.LockedUntilBossClear, service.StatusOf("palette_slots_4").State, "palettes still wait for the boss");
            Assert.AreEqual(UnlockState.LockedUntilChapter2Clear, service.StatusOf("destroyed_ink_gold_20").State);
            Assert.AreEqual(UnlockState.MissingPrerequisite, service.StatusOf("attack_bonus_2").State, "tiers are bought in order");
            Assert.IsTrue(service.TryPurchase("attack_bonus_1", out _));
            Assert.AreEqual(40, service.Snapshot.Pigment);
            Assert.AreEqual(UnlockState.NotEnoughPigment, service.StatusOf("attack_bonus_2").State);
            var reloaded = new ProgressionService(service.Catalog, store);
            Assert.IsNull(reloaded.LoadWarning, "an unlock bought before the boss is a valid saved state");
            Assert.IsTrue(reloaded.Snapshot.Has("attack_bonus_1"));
        }

        [Test] public void FailedSaveChangesNothing()
        {
            var store = new MemoryProfileStore(); var service = Service(store);
            ClearedWithPigment(service, 3, clears: 2);
            store.FailNextSave = true;
            Assert.IsFalse(service.TryPurchase("starting_gold_50", out _));
            Assert.AreEqual(120, service.Snapshot.Pigment); Assert.IsFalse(service.Snapshot.Has("starting_gold_50"));
            Assert.IsTrue(service.TryPurchase("starting_gold_50", out _), "the next attempt succeeds normally");
            Assert.AreEqual(60, service.Snapshot.Pigment);
        }

        [Test] public void KillsGrantPigmentOncePerRunAndNeverInTraining()
        {
            var service = Service();
            var run = service.PlanRun(new[] { "sword", "blunt" }).RunId;
            Assert.AreEqual(10, service.AwardPigment(run, "enemy-7", "elite", false));
            Assert.AreEqual(0, service.AwardPigment(run, "enemy-7", "elite", false), "a reset-revived enemy pays only once");
            Assert.AreEqual(0, service.AwardPigment(run, "enemy-8", "elite", true), "training never pays");
            var next = service.PlanRun(new[] { "sword", "blunt" }).RunId;
            Assert.AreEqual(10, service.AwardPigment(next, "enemy-7", "elite", false), "a new run starts a new ledger");
            Assert.AreEqual(20, service.Snapshot.Pigment);
            Assert.IsTrue(service.HasUnsavedPigment); Assert.IsTrue(service.FlushPigment()); Assert.IsFalse(service.HasUnsavedPigment);
        }

        [Test] public void RunPlansSnapshotSlotsWildcardsAndBonusesAtDeparture()
        {
            var service = Service();
            var basic = service.PlanRun(new[] { "blunt", "ranged" });
            Assert.AreEqual(3, basic.PaletteSlots); Assert.IsFalse(basic.Wildcard1); Assert.IsFalse(basic.Wildcard2);
            Assert.AreEqual(0, basic.StartingGoldBonus); Assert.AreEqual(0, basic.DestroyedInkGoldBonus);
            Assert.Throws<ArgumentException>(() => service.PlanRun(new[] { "sword", "sword" }), "two different blueprints");
            Assert.Throws<ArgumentException>(() => service.PlanRun(new[] { "sword" }), "exactly two");
            Assert.Throws<ArgumentException>(() => service.PlanRun(new[] { "sword", "blunt", "ranged" }), "never three");

            Assert.AreEqual(0, basic.AttackBonusPercent); Assert.AreEqual(0, basic.MaxHealthBonus);
            ClearedWithPigment(service, 21, clears: 4);
            foreach (var id in new[] { "palette_slots_4", "palette_wildcard_1", "starting_gold_50", "starting_gold_100", "destroyed_ink_gold_20", "attack_bonus_1", "attack_bonus_2", "max_health_bonus_1" })
                Assert.IsTrue(service.TryPurchase(id, out _), id);
            var plan = service.PlanRun(new[] { "sword", "ranged" });
            Assert.AreEqual(4, plan.PaletteSlots); Assert.IsTrue(plan.Wildcard1); Assert.IsFalse(plan.Wildcard2);
            Assert.AreEqual(100, plan.StartingGoldBonus, "the highest gold tier applies; tiers do not add up to 150");
            Assert.AreEqual(20, plan.DestroyedInkGoldBonus);
            Assert.AreEqual(10, plan.AttackBonusPercent, "attack tier 2 is +10% in total"); Assert.AreEqual(5, plan.MaxHealthBonus);
            Assert.AreNotEqual(basic.RunId, plan.RunId);
            Assert.IsTrue(service.TryPurchase("palette_slots_5", out _));
            Assert.AreEqual(4, plan.PaletteSlots, "a purchase does not change a run already under way");
            Assert.AreEqual(5, service.PlanRun(new[] { "sword", "ranged" }).PaletteSlots);
        }

        [Test] public void SettlementAndStoriesAreIdempotent()
        {
            var service = Service();
            var run = service.PlanRun(new[] { "sword", "blunt" }).RunId;
            Assert.IsTrue(service.SettleRun(run, RunOutcome.ChapterCleared, false));
            Assert.IsFalse(service.SettleRun(run, RunOutcome.ChapterCleared, false));
            Assert.AreEqual(1, service.Snapshot.RunsCleared);
            Assert.IsFalse(service.SettleRun(Guid.NewGuid(), RunOutcome.ChapterCleared, true), "training never clears the boss gate");
            Assert.IsTrue(service.MarkStorySeen("opening")); Assert.IsFalse(service.MarkStorySeen("opening"));
            Assert.IsTrue(service.Snapshot.HasSeen("opening"));
        }

        // Chapter 1 clears continue into chapter 2, so the clear is counted when leaving chapter 1, not at settlement.
        [Test] public void ChapterOneClearsCountWhenTheRunContinues()
        {
            var service = Service();
            var died = service.PlanRun(new[] { "sword", "blunt" }).RunId;
            Assert.IsTrue(service.RecordChapterOneClear(died, false));
            Assert.IsFalse(service.RecordChapterOneClear(died, false), "one chapter-1 clear per run");
            Assert.IsTrue(service.Snapshot.BossFirstClear);
            Assert.IsTrue(service.SettleRun(died, RunOutcome.Died, false), "death in chapter 2");
            Assert.AreEqual(1, service.Snapshot.Chapter1Clears); Assert.AreEqual(0, service.Snapshot.RunsCleared); Assert.AreEqual(1, service.Snapshot.RunsDied);

            var cleared = service.PlanRun(new[] { "sword", "blunt" }).RunId;
            Assert.IsTrue(service.RecordChapterOneClear(cleared, false));
            Assert.IsTrue(service.SettleRun(cleared, RunOutcome.ChapterCleared, false));
            Assert.AreEqual(2, service.Snapshot.Chapter1Clears, "a cleared run that already left chapter 1 is not counted twice");
            Assert.AreEqual(1, service.Snapshot.RunsCleared);

            // A cleared run settled without the chapter-1 record still passed chapter 1.
            var direct = service.PlanRun(new[] { "sword", "blunt" }).RunId;
            Assert.IsTrue(service.SettleRun(direct, RunOutcome.ChapterCleared, false));
            Assert.AreEqual(3, service.Snapshot.Chapter1Clears);
            Assert.IsFalse(service.RecordChapterOneClear(direct, false), "a settled run is not counted again");
            Assert.IsFalse(service.RecordChapterOneClear(Guid.NewGuid(), true), "training never counts");
            Assert.AreEqual(3, service.Snapshot.Chapter1Clears);
        }

        [Test] public void OlderProfilesDeriveChapterOneClears()
        {
            var store = new MemoryProfileStore(new ProfileData { profileId = Guid.NewGuid().ToString(), chapter1BossFirstClear = true, runsCleared = 2, runsDied = 3 });
            Assert.AreEqual(2, Service(store).Snapshot.Chapter1Clears);
            store = new MemoryProfileStore(new ProfileData { profileId = Guid.NewGuid().ToString(), chapter1BossFirstClear = true, runsDied = 3 });
            Assert.AreEqual(1, Service(store).Snapshot.Chapter1Clears);
        }

        // Playtest 2026-11-09: after the first chapter-1 clear the next load showed chapter 2's yellow ink as already gathered.
        [Test] public void AFirstChapterOneClearStillHoldsOnePieceAfterReload()
        {
            var store = new MemoryProfileStore();
            var service = Service(store);
            Assert.IsTrue(service.SettleRun(service.PlanRun(new[] { "sword", "blunt" }).RunId, RunOutcome.ChapterCleared, false, 1));
            var reloaded = Service(store);
            Assert.AreEqual(1, reloaded.ChaptersCleared); Assert.AreEqual(1, reloaded.PalettePieces, "chapter 2's piece waits for its boss");
            Assert.IsTrue(reloaded.ChapterClearEndsRun(2), "the first chapter-2 clear still goes home");
            // A profile saved before chaptersCleared existed still reads a cleared run as chapter 2 beaten.
            Assert.AreEqual(2, Service(new MemoryProfileStore(new ProfileData { profileId = Guid.NewGuid().ToString(), chapter1BossFirstClear = true, runsCleared = 1 })).ChaptersCleared);
        }

        [Test] public void OnlyBuildingsWithoutUnlocksTakeAColourRule()
        {
            var catalog = Catalog();
            // No building colours from the profile any more (user decision 2026-11-04); the rule is still validated.
            Assert.That(catalog.Buildings.All(b => string.IsNullOrEmpty(b.colorWhen)), Is.True);
            Assert.Throws<ArgumentException>(() => Catalog(d => d.buildings.First(b => b.id == "palette_atelier").colorWhen = ProgressionIds.BossGate));
            Assert.Throws<ArgumentException>(() => Catalog(d => d.buildings.First(b => b.id == "departure_lane").colorWhen = "first_kill"));
        }

        [Test] public void FileStoreReplacesAtomicallyAndRecoversFromCorruption()
        {
            string dir = Path.Combine(Path.GetTempPath(), "deviant-profile-" + Guid.NewGuid().ToString("N"));
            try
            {
                string file = Path.Combine(dir, "profile.json");
                var store = new FileProfileStore(file, d => JsonUtility.ToJson(d), JsonUtility.FromJson<ProfileData>);
                var service = new ProgressionService(Catalog(), store);
                ClearedWithPigment(service, 4);
                Assert.IsTrue(service.TryPurchase("palette_wildcard_1", out _));
                Assert.IsTrue(File.Exists(file)); Assert.IsTrue(File.Exists(store.BackupPath)); Assert.IsFalse(File.Exists(file + ".tmp"));

                File.WriteAllText(file, "{ broken");
                var recovered = new ProgressionService(Catalog(), store);
                StringAssert.Contains("backup", recovered.LoadWarning);
                Assert.AreEqual(service.Snapshot.ProfileId, recovered.Snapshot.ProfileId);
                Assert.IsTrue(recovered.Snapshot.BossFirstClear, "the last valid save before the purchase is kept");

                File.WriteAllText(file, "{ broken"); File.WriteAllText(store.BackupPath, "{ broken");
                var fresh = new ProgressionService(Catalog(), store);
                Assert.IsNotNull(fresh.LoadWarning);
                Assert.AreEqual("{ broken", File.ReadAllText(file), "an unreadable profile is not overwritten on load");
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        }

        [Test] public void TamperedProfilesAreRejected()
        {
            var catalog = Catalog();
            var data = ProfileData.CreateNew(Guid.NewGuid().ToString());
            data.chapter1BossFirstClear = true; data.unlocks = new[] { "palette_slots_5" };
            CollectionAssert.IsNotEmpty(ProfileData.Validate(data, catalog), "5 slots without 4");
            data.unlocks = new[] { "palette_slots_4", "palette_slots_4" };
            CollectionAssert.IsNotEmpty(ProfileData.Validate(data, catalog), "duplicate unlock");
            data.unlocks = new[] { "max_health_up" };
            CollectionAssert.IsNotEmpty(ProfileData.Validate(data, catalog), "unknown unlock");
            data.unlocks = Array.Empty<string>(); data.pigment = -1;
            CollectionAssert.IsNotEmpty(ProfileData.Validate(data, catalog), "negative pigment");
            data.pigment = 0; data.chapter1BossFirstClear = false; data.unlocks = new[] { "palette_slots_4" };
            CollectionAssert.IsNotEmpty(ProfileData.Validate(data, catalog), "unlock before the gate");
            data.unlocks = new[] { "attack_bonus_1" };
            CollectionAssert.IsNotEmpty(ProfileData.Validate(data, catalog), "a pre-boss unlock before any run ended");
            data.runsDied = 1;
            CollectionAssert.IsEmpty(ProfileData.Validate(data, catalog), "a pre-boss unlock after a death");
            data.runsDied = 0; data.unlocks = new[] { "palette_slots_4" };

            var store = new MemoryProfileStore(data);
            var service = new ProgressionService(catalog, store);
            Assert.IsNotNull(service.LoadWarning);
            Assert.AreEqual(0, service.Snapshot.Unlocks.Count, "a rejected profile is not used");
        }

        [Test] public void StartingGoldIsAddedOncePerRunFromTheDepartureSnapshot()
        {
            var service = Service();
            ClearedWithPigment(service, 5);
            Assert.IsTrue(service.TryPurchase("starting_gold_50", out _)); Assert.IsTrue(service.TryPurchase("starting_gold_100", out _));
            var economy = new EconomyData { rewards = new RewardTableData[0], prices = new PriceTrackData[0] };
            var flow = new RunFlow(() => 1, economy);
            var plan = service.PlanRun(new[] { "sword", "blunt" });
            var run = flow.StartNewRun(0, null, plan);
            Assert.AreEqual(100, run.Economy.Gold, "added to the existing starting gold of 0");
            Assert.IsTrue(run.HasReceipt(ProgressionIds.StartingGoldReceipt));
            Assert.IsFalse(run.GrantStartingGold(), "scene reloads and resets reuse the session and its receipt");
            Assert.AreEqual(100, run.Economy.Gold);
            Assert.Throws<InvalidOperationException>(() => flow.StartNewRun(0, null, plan), "the same departure cannot start a second run");
            Assert.AreEqual(100, flow.StartNewRun(0, null, service.PlanRun(new[] { "sword", "blunt" })).Economy.Gold, "the next departure pays again");
            Assert.AreEqual(0, flow.StartNewRun(0, null, service.PlanRun(new[] { "sword", "blunt" }, training: true)).Economy.Gold, "training never pays");
            Assert.AreEqual(0, flow.StartNewRun(0).Economy.Gold, "development runs have no plan");
        }

        // Before the gold tiers, starting_gold_100 was a single unlock; such a profile owns tier 1 implicitly.
        [Test] public void ProfilesFromTheSingleGoldUnlockKeepTheirBonus()
        {
            var store = new MemoryProfileStore(new ProfileData { profileId = Guid.NewGuid().ToString(), pigment = 5, chapter1BossFirstClear = true, runsCleared = 1, unlocks = new[] { "starting_gold_100", "palette_slots_4" } });
            var service = Service(store);
            Assert.IsNull(service.LoadWarning);
            Assert.IsTrue(service.Snapshot.Has("starting_gold_50")); Assert.IsTrue(service.Snapshot.Has("starting_gold_100"));
            Assert.AreEqual(5, service.Snapshot.Pigment, "nothing is charged for the filled-in tier");
            Assert.AreEqual(100, service.PlanRun(new[] { "sword", "blunt" }).StartingGoldBonus);
        }

        // User decision 2026-11-05 (recommended): two strong inks of each colour wait for the dye atelier. The departure
        // snapshot lists the parts still locked; training keeps every part. Each colour's second ink follows its first.
        [Test] public void InkUnlocksLeaveLockedPartsOutOfTheDeparturePlan()
        {
            var service = Service();
            var inks = service.Catalog.Unlocks.Where(u => u.effect.kind == ProgressionIds.InkPartUnlock).ToArray();
            Assert.AreEqual(10, inks.Length, "two inks per colour");
            foreach (var colour in new[] { "red", "yellow", "green", "blue", "purple" })
                Assert.AreEqual(2, inks.Count(u => u.effect.part.Contains("_" + colour + "_")), colour);
            Assert.IsTrue(inks.All(u => u.building == "palette_atelier" && u.requiredClear == ProgressionIds.BossGate));
            Assert.AreEqual(10, service.PlanRun(new[] { "sword", "blunt" }).LockedInkParts.Count);
            Assert.AreEqual(0, service.PlanRun(new[] { "sword", "blunt" }, training: true).LockedInkParts.Count, "training keeps every part");
            ClearedWithPigment(service, 10);
            Assert.AreEqual(UnlockState.MissingPrerequisite, service.StatusOf("ink_part_ink3_red_02").State, "the ink line follows the 7-slot bag");
            Assert.IsTrue(service.TryPurchase("bag_slots_1", out _));
            Assert.AreEqual(UnlockState.MissingPrerequisite, service.StatusOf("ink_part_ink3_red_09").State, "a colour's second ink follows its first");
            Assert.IsTrue(service.TryPurchase("ink_part_ink3_red_02", out var state), state.ToString());
            Assert.AreEqual(service.Catalog.Find("ink_part_ink3_red_09"), service.Catalog.NextTier(service.Catalog.Find("ink_part_ink3_red_02")));
            var plan = service.PlanRun(new[] { "sword", "blunt" });
            Assert.AreEqual(9, plan.LockedInkParts.Count); CollectionAssert.DoesNotContain(plan.LockedInkParts, "ink3_red_02"); CollectionAssert.Contains(plan.LockedInkParts, "ink3_red_09");
        }

        [Test] public void FailedResetKeepsProgressAndPendingPigment()
        {
            var store = new MemoryProfileStore();
            var service = Service(store);
            service.MarkStorySeen("opening");
            service.AwardPigment(Guid.NewGuid(), "reset-failure", "normal", false);
            string id = service.Snapshot.ProfileId;
            long pigment = service.Snapshot.Pigment;
            store.FailNextSave = true;
            Assert.IsFalse(service.ResetProfile());
            Assert.AreEqual(id, service.Snapshot.ProfileId);
            Assert.AreEqual(pigment, service.Snapshot.Pigment);
            Assert.IsTrue(service.Snapshot.HasSeen("opening"));
            Assert.IsTrue(service.HasUnsavedPigment);
            Assert.IsNotNull(service.LastSaveError);
        }

        [Test] public void ResetReplacesRecoveryBackupAndDiscardsPendingPigment()
        {
            string dir = Path.Combine(Path.GetTempPath(), "deviant-reset-" + Guid.NewGuid().ToString("N"));
            try
            {
                var path = Path.Combine(dir, "profile.json");
                var store = new FileProfileStore(path, d => JsonUtility.ToJson(d), JsonUtility.FromJson<ProfileData>);
                var service = new ProgressionService(Catalog(), store);
                ClearedWithPigment(service, 2);
                service.MarkStorySeen("opening");
                service.RecordSkyParkour(new SkyParkourRecord { seconds = 42 });
                service.AwardPigment(Guid.NewGuid(), "reset-test", "normal", false);
                Assert.IsTrue(service.ResetProfile());
                Assert.IsFalse(service.HasUnsavedPigment);
                string freshId = service.Snapshot.ProfileId;
                File.WriteAllText(path, "{ broken");
                var recovered = new ProgressionService(Catalog(), store);
                Assert.AreEqual(freshId, recovered.Snapshot.ProfileId);
                Assert.AreEqual(0, recovered.Snapshot.Pigment);
                Assert.IsFalse(recovered.Snapshot.BossFirstClear);
                Assert.IsFalse(recovered.Snapshot.HasSeen("opening"));
                Assert.IsEmpty(recovered.Snapshot.SkyParkourRecords);
                Assert.AreEqual(0, recovered.Snapshot.RunsStarted);
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        }

        [Test] public void ResetStartsAFreshProfile()
        {
            var service = Service();
            ClearedWithPigment(service, 2);
            string old = service.Snapshot.ProfileId;
            Assert.IsTrue(service.ResetProfile());
            Assert.AreNotEqual(old, service.Snapshot.ProfileId);
            Assert.AreEqual(0, service.Snapshot.Pigment); Assert.IsFalse(service.Snapshot.BossFirstClear);
        }
    }
}
