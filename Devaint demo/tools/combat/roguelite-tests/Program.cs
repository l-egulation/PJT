using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Deviant.Run;

static class Program
{
    static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true };
    static int passed, failed;
    static string Root => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../.."));
    static ProgressionData Data() => JsonSerializer.Deserialize<ProgressionData>(File.ReadAllText(Path.Combine(Environment.CurrentDirectory, "data/combat/roguelite_progression.json")), Json);
    static void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
    static void Test(string name, Action action)
    {
        try { action(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e.Message); }
    }
    static ProgressionUnlock Unlock(string id, string effect) => JsonSerializer.Deserialize<ProgressionUnlock>(
        "{\"id\":\"" + id + "\",\"name\":\"Test\",\"building\":\"supply_office\",\"branch\":\"combat\",\"cost\":100,\"costStatus\":\"trial-3\",\"requiredClear\":\"first_run_settled\",\"prerequisites\":[],\"maxPurchases\":1,\"effect\":" + effect + "}", Json);
    static ProgressionCatalog With(params ProgressionUnlock[] unlocks)
    {
        var d = Data(); d.unlocks = unlocks; return new ProgressionCatalog(d);
    }
    static RunSession RunWith(params ProgressionUnlock[] unlocks)
    {
        var catalog = With(unlocks);
        var p = ProfileData.CreateNew(Guid.NewGuid().ToString()); p.runsDied = 1;
        p.unlocks = unlocks.Select(u => u.id).ToArray();
        var service = new ProgressionService(catalog, new MemoryProfileStore(p));
        return new RunFlow(() => 42, new EconomyData()).StartNewRun(0, plan: service.PlanUnarmedRun());
    }
    static SketchRunState Sketch() => new SketchRunState(
        new Dictionary<string, string[]> { ["sword"] = new[] { "0", "1" }, ["blunt"] = new[] { "0", "1" }, ["ranged"] = new[] { "0" } },
        new[] { "red_a", "blue_a", "green_a", "green_b", "yellow_a", "purple_a" }, id => id.Split('_')[0], 7);
    static int Main()
    {
        Test("bare hands may equip one confirmed weapon during the change cooldown without clearing it", () => {
            var a = new SketchArsenal(id => id);
            var broken = a.Acquire("sword", "slash", new string[3]); a.Confirm(broken.Id);
            var first = a.Acquire("sword", "slash", new string[3]); a.Confirm(first.Id);
            var second = a.Acquire("sword", "slash", new string[3]); a.Confirm(second.Id);
            a.Equip(broken.Id, 0); a.Tick(1); a.Destroy(broken.Id);
            Check(a.ChangeCooldownRemaining > 0, "closing the bag after a change starts the cooldown");
            Check(a.CanEquip(first.Id, 0, out _) && a.CanEquip(first.Id, 1, out _), "either empty destination must work");
            a.Equip(first.Id, 1);
            Check(a.ChangeCooldownRemaining > 0 && !a.CanEquip(second.Id, 0, out _), "second equip must wait for the cooldown");
            Check(a.CanSwapOrder, "X ignores the cooldown");
            bool blocked = false; try { a.Unequip(1); } catch (InvalidOperationException) { blocked = true; }
            Check(blocked, "unequip must wait for the cooldown");
            a.Tick(5); Check(a.CanEquip(second.Id, 0, out _), "the cooldown ends after five seconds of combat time");
        });
        Test("workshop needs both the first run settlement and its artisan scene", () => {
            var p = ProfileData.CreateNew("workshop-gate"); p.runsStarted = 1;
            Check(!new ProfileSnapshot(p).ArtisanWorkshopOpen, "starting a run is insufficient");
            p.runsDied = 1;
            Check(!new ProfileSnapshot(p).ArtisanWorkshopOpen, "settlement alone is insufficient");
            p.seenStories = new[] { "artisan-first-run" };
            Check(new ProfileSnapshot(p).ArtisanWorkshopOpen, "settlement plus scene opens the workshop");
            p.runsDied = 0;
            Check(!new ProfileSnapshot(p).ArtisanWorkshopOpen, "scene alone is insufficient");
        });
        Test("an ink bundle withholds both members until its single purchase", () => {
            var catalog = With(Unlock("red_bundle", "{\"kind\":\"ink_bundle_unlock\",\"parts\":[\"green_dash_use_shield\",\"green_dash_use_heal\"]}"));
            Check(catalog.LockedInkParts(_ => false).Count == 2, "both inks must be locked together");
            Check(catalog.LockedInkParts(_ => true).Count == 0, "buying the bundle must open both inks");
        });
        Test("the highest shared reroll tier is used rather than adding tiers", () => {
            var a = Unlock("reroll_1", "{\"kind\":\"shared_reroll_count\",\"value\":1}");
            var b = Unlock("reroll_2", "{\"kind\":\"shared_reroll_count\",\"value\":2}"); b.prerequisites = new[] { a.id };
            var catalog = With(a, b);
            Check(catalog.Highest("shared_reroll_count", _ => true, e => e.value) == 2, "tier totals must not stack");
        });
        Test("different reroll callers share one budget and a rejected draw costs nothing", () => {
            dynamic perks = ((dynamic)RunWith(Unlock("reroll", "{\"kind\":\"shared_reroll_count\",\"value\":2}"))).Perks;
            Check(!perks.TryReroll((Func<bool>)(() => false)), "failed draw must reject");
            Check(perks.RerollsRemaining == 2, "failed draw spent a reroll");
            Check(perks.TryReroll((Func<bool>)(() => true)), "ink reroll");
            Check(perks.TryReroll((Func<bool>)(() => true)), "augment reroll");
            Check(!perks.TryReroll((Func<bool>)(() => true)), "shop cannot draw with an empty common budget");
        });
        Test("revival restores half the upgraded maximum once per run", () => {
            dynamic perks = ((dynamic)RunWith(Unlock("revive", "{\"kind\":\"revive_count\",\"value\":1,\"healthPercent\":50}"))).Perks;
            float restored;
            Check(!perks.TryRevive(20f, 130f, out restored), "a living player must not spend revival");
            Check(perks.TryRevive(0f, 130f, out restored) && restored == 65, "must restore exactly half maximum");
            Check(!perks.TryRevive(0f, 130f, out restored), "revival must not refresh with a second caller");
        });
        Test("small kill gold bonuses retain fractional gold without applying to other income", () => {
            var run = RunWith(Unlock("gold", "{\"kind\":\"kill_gold_bonus_percent\",\"value\":3}"));
            dynamic perks = ((dynamic)run).Perks;
            for (int i = 0; i < 100; i++) perks.AwardKillGold(6);
            Check(run.Economy.Gold == 618, "100 six-gold kills at +3% should earn exactly 618");
            run.Economy.Earn(100); Check(run.Economy.Gold == 718, "starting gold, sales and refunds must not receive kill bonus");
        });
        Test("departure reward and broken weapon payments cannot be claimed twice", () => {
            var run = RunWith(Unlock("reward", "{\"kind\":\"starting_reward_count\",\"value\":1}"),
                              Unlock("break", "{\"kind\":\"weapon_break_gold\",\"value\":20}"));
            dynamic perks = ((dynamic)run).Perks;
            Check(perks.TryTakeStartingReward() && !perks.TryTakeStartingReward(), "departure reward was duplicated");
            var weapon = Guid.NewGuid();
            Check(perks.AwardWeaponBreak(weapon) == 20 && perks.AwardWeaponBreak(weapon) == 0, "duplicate destruction paid twice");
            Check(run.Economy.Gold == 20, "wrong weapon recovery amount");
        });
        Test("locked augment bundles never appear in a free augment draw", () => {
            var stable = new AugmentDefinition { id = "s", group = "safe", name = "stable", effect = new AugmentEffect() };
            var a = new AugmentDefinition { id = "a", group = "a", name = "a", effect = new AugmentEffect() };
            var b = new AugmentDefinition { id = "b", group = "b", name = "b", effect = new AugmentEffect() };
            var locked = new AugmentDefinition { id = "locked", group = "a", name = "locked", effect = new AugmentEffect() };
            var catalog = new AugmentCatalog { schema = 1, offeredCards = 3, stableCards = 1, riskCards = 2,
                minimumDamageScale = .1f, minimumIncomingScale = .1f, minimumMaxHealthScale = .1f, minimumWalkSpeedScale = .2f,
                minimumBasicSpeedScale = .2f, maximumSkillCooldownReduction = .8f, maximumCritChance = 1,
                groups = new[] { new AugmentGroup { id = "safe" }, new AugmentGroup { id = "a" }, new AugmentGroup { id = "b" } },
                augments = new[] { stable, a, b, locked } };
            dynamic augments = new MaintenanceAugments(catalog, 42);
            augments.LockCards(new[] { "locked" });
            var offer = (AugmentDefinition[])augments.DrawReward();
            Check(!offer.Any(x => x.id == "locked"), "a locked card was offered");
        });
        Test("extra reservoir charges keep original prices and extend the last price", () => {
            var settings = new StageRecoveryData { usesPerStage = 2, prices = new[] { 100, 150 }, healthFraction = .25f,
                holdSeconds = 1, reuseDelaySeconds = 1, interactionReach = 1.8f };
            var method = typeof(StageRecovery).GetMethod("WithExtraUses");
            Check(method != null, "reservoir extension is missing");
            var extended = (StageRecoveryData)method.Invoke(null, new object[] { settings, 2 });
            var recovery = new StageRecovery(extended);
            int paid = 0;
            for (int i = 0; i < 4; i++) Check(recovery.TryUse(10, 100, i, out _, p => { paid += p; return true; }), "extra charge unavailable");
            Check(paid == 550 && recovery.Remaining == 0 && settings.usesPerStage == 2, "wrong prices, capacity, or shared tuning mutation");
        });
        Test("retired purchases refund once while preserving profile and progression", () => {
            var catalog = new ProgressionCatalog(Data());
            var old = ProfileData.CreateNew(Guid.NewGuid().ToString()); old.pigment = 50; old.runsDied = 2;
            old.chapter1BossFirstClear = true; old.chaptersCleared = 2; old.unlocks = new[] { "palette_slots_4", "ink_part_ink3_red_02", "attack_bonus_1" };
            var store = new MemoryProfileStore(old); var service = new ProgressionService(catalog, store);
            Check(service.Snapshot.ProfileId == old.profileId && service.Snapshot.RunsDied == 2, "migration reset profile");
            Check(service.Snapshot.Pigment == 290 && service.Snapshot.Has("attack_bonus_1"), "refund or retained purchase wrong");
            Check(!service.Snapshot.Has("palette_slots_4"), "retired purchase remains active");
            var again = new ProgressionService(catalog, store);
            Check(again.Snapshot.Pigment == 290 && again.Snapshot.ProfileId == old.profileId, "reload duplicated refund");
        });
        Test("the departure bonus is a three-weapon sketch offer, rerolled without leaving its rule", () => {
            var state = Sketch();
            Check(state.OpenStartingOffer() && state.PendingOffer.Length == 3 && state.PendingIsStart, "bonus reward needs three weapon choices");
            Check(state.PendingOffer.All(c => !c.IsInk && c.Weapon.Slots == 4), "stage 1 weapons have four slots (sketch-weapons.md)");
            var first = state.PendingOffer;
            Check(state.RerollPending() && !ReferenceEquals(first, state.PendingOffer) && state.PendingIsStart, "reroll must keep the bonus rule");
            Check(state.Choose(0) != null && state.WeaponsPicked == 0 && state.PendingOffer == null, "bonus counted as a normal pick");
        });
        Test("the shop display unlock adds sketch shelf places and a refresh keeps sold places", () => {
            var state = Sketch(); state.ExtraShopItems = 1;
            var economy = new RunEconomy(new EconomyData()); economy.Earn(100000);
            var stock = state.ShopStock("stage1");
            Check(stock.Count(o => o.Weapon != null) == 4 && stock.Count(o => o.Ink != null) == 4, "expanded shelf count");
            state.Buy("stage1", 0, economy); var sold = stock[0];
            Check(state.RerollShop("stage1", true) && ReferenceEquals(state.ShopStock("stage1")[0], sold) && sold.Sold, "refresh replaced a sold place");
        });
        Test("equipping a new item changes identity and rejects stale slot commands", () => {
            var loadout = Deviant.Equipment.WeaponResources.CreateLoadout();
            var before = loadout.GetWeapon(0).InstanceId;
            var next = Deviant.Equipment.WeaponResources.CreateWeapon();
            var replacement = loadout.GetType().GetInterface("Deviant.Equipment.Contracts.IWeaponLoadoutReplacement");
            Check(replacement != null, "new weapon replacement command is missing");
            var method = replacement.GetMethod("TryReplace");
            Check((bool)method.Invoke(loadout, new object[] { 0, before, next }), "replacement rejected");
            Check(loadout.GetWeapon(0).InstanceId == next.InstanceId, "replacement retained the previous identity");
            Check(!(bool)method.Invoke(loadout, new object[] { 0, before, Deviant.Equipment.WeaponResources.CreateWeapon() }), "stale replacement succeeded");
        });
        Test("stored weapon charge clocks rebase without filling or adding old chapter time", () => {
            var charges = Deviant.Equipment.WeaponResources.CreateCharges(2, 10); charges.Spend(500);
            var clock = charges.GetType().GetInterface("Deviant.Equipment.Contracts.IWeaponChargeClock");
            Check(clock != null, "relative weapon clock is missing");
            clock.GetMethod("Rebase").Invoke(charges, new object[] { 500f, 0f });
            charges.Tick(0); Check(charges.Read().Available == 1, "chapter transition filled the charge");
            charges.Tick(10); Check(charges.Read().Available == 2, "old chapter deadline survived rebase");
        });
        Console.WriteLine($"RESULT {passed} passed, {failed} failed");
        return failed == 0 ? 0 : 1;
    }
}
