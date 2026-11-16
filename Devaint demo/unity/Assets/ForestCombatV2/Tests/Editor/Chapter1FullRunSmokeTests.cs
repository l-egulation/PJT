using System.Collections;
using System.Linq;
using Deviant.Chapter2;
using Deviant.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ForestCombatV2.Tests
{
    // One chapter 1 run through the play-build path: title → blueprints → elite reward → facility purchase
    // → boss → exit → same run in chapter 2 → final result → village → a fresh second run. Combat is driven through
    // the same public entry points the screens use; locals are not captured by lambdas across EnterPlayMode.
    public sealed class Chapter1FullRunSmokeTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Chapter1ProfileHost.UseStore(null);
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        static ForestCombatWorld ReadyWorld()
        {
            var world = Object.FindAnyObjectByType<ForestCombatWorld>();
            return world && world.Ready && world.GetComponent<Chapter1RunBinding>() ? world : null;
        }

        static void Stand(ForestCombatWorld world, Vector2 feet)
        {
            world.arena.Player.Position = feet;
            Physics2D.SyncTransforms();
        }
        // Create the transaction closure after EnterPlayMode, not on the test iterator before its domain reload.
        static bool UpgradeGrowthSlot(Chapter1MaintenanceShop shop, System.Guid weapon)
            => shop.GrowthTransaction(() => shop.Growth.Upgrade(weapon, 0), out _);
        static void AssertChapterTwoFormPool(Chapter2World world)
        {
            var pool=(System.Collections.Generic.List<string>)typeof(Chapter2World).GetMethod("InkPool",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(world,new object[]{false,false});
            foreach(var id in new[]{"form.base_sword","form.hammer","form.launcher"})CollectionAssert.DoesNotContain(pool,id);
            CollectionAssert.Contains(pool,"form.shield");
        }

        [UnityTest] public IEnumerator TitleThroughChapterTwoAndVillageStartsAFreshRun()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.OpenScene(Chapter1RunHost.TitleScene);
            yield return new EnterPlayMode();
            Chapter1ProfileHost.UseStore(new MemoryProfileStore());
            yield return null;
            var host = Chapter1RunHost.Instance;
            Assert.That(host, Is.Not.Null, "title prepares the run host");
            host.StartNewRun();
            for (int frame = 0; frame < 600 && !ReadyWorld(); frame++) yield return null;
            var world = ReadyWorld();
            Assert.That(world, Is.Not.Null, "new game loads chapter 1");
            yield return null;
            var run = host.Flow.Current;
            var player = world.arena.Player;
            player.maxHealth = player.health = 100000;
            foreach (var enemy in world.arena.enemies) if (enemy) { enemy.aggressive = false; enemy.CancelAttack(); }

            // Start room: choose two blueprints.
            var offer = world.GetComponent<Chapter1StartingBlueprints>();
            Stand(world, offer.OfferPosition(0)); Assert.That(offer.TryChoose(0), Is.True);
            Stand(world, offer.OfferPosition(1)); Assert.That(offer.TryChoose(1), Is.True);
            Assert.That(offer.Complete, Is.True);

            // Elite: gold credits immediately; only non-gold items require contact.
            var elite = world.arena.enemies.Single(e => e && Chapter1EnemyRole.Of(e) == Chapter1EnemyRole.Elite);
            elite.health = 0;
            for (int frame = 0; frame < 60 && run.Kills < 1; frame++) yield return null;
            Assert.That(run.Economy.Gold, Is.InRange(200,280), "Gold credits at defeat without touching its fading visual.");
            var rewards = world.GetComponent<Chapter1Rewards>();
            var items = rewards.Pickups.ToArray();
            Assert.That(items.Length, Is.GreaterThanOrEqualTo(1), "An elite drops gold plus stamp pickups.");
            var bag = world.arena.GetComponent<Deviant.Inventory.Unity.InventoryRuntimeHost>();
            Assert.That(bag.Drops.Count, Is.Zero, "Chapter 1 has no bag inks; stamps replace form and special-attack inks.");
            for (int frame = 0; frame < 600 && !items.All(p => !p || p.Collected || p.Ready); frame++) yield return null;
            foreach (var item in items)
                for (int frame = 0; frame < 30 && item && !item.Collected; frame++) { Stand(world, (Vector2)item.transform.position - (player.Center - player.Position)); yield return null; }
            Assert.That(run.Economy.Gold, Is.InRange(200, 280));
            Assert.That(run.Stash.InkParts, Is.Zero, "elites no longer drop colour ink parts");

            // Facility: maintenance allowed only inside; spend the elite gold on a character upgrade.
            Assert.That(player.EditingAllowed, Is.False, "outside a facility");
            var facilityRoom = world.Data.rooms.First(r => Chapter1MaintenanceFacility.IsFacility(r.kind));
            Stand(world, new Vector2(facilityRoom.x + facilityRoom.w / 2f, -(facilityRoom.y + facilityRoom.entryY)));
            Assert.That(player.EditingAllowed, Is.True, "inside a facility");
            var shop = world.GetComponent<Chapter1MaintenanceShop>();
            Assert.That(shop.TryOpen(), Is.True);
            int gold = run.Economy.Gold;
            Assert.That(shop.TryUpgradeMvpAttack(out _), Is.True);
            Assert.That(run.Economy.Gold, Is.EqualTo(gold - 60));
            var growth = run.Growth;
            // Record actual growth values before crossing the chapter scene boundary.
            var growthWeapon = player.weapons[0].InstanceId;
            var redPalette = growth.GrantPalette(new[] { "red", "red", "red" });
            growth.Bind(redPalette.Id, growthWeapon);
            growth.Insert(growthWeapon, 0, growth.GrantInk("ink3_red_01").Id);
            growth.Economy.Earn(1000);
            Assert.That(UpgradeGrowthSlot(shop, growthWeapon), Is.True);
            Assert.That(UpgradeGrowthSlot(shop, growthWeapon), Is.True);
            var carriedInk = growth.Equipped(growthWeapon).Slots[0].InstanceId;
            Assert.That(growth.AttackMultiplier, Is.EqualTo(1.05f).Within(.0001));
            Assert.That(growth.SnapshotSlots(growthWeapon)[0].Part.levels[2].ratio, Is.EqualTo(.26f).Within(.0001));
            shop.Close();
            Assert.That(world.arena.Controls.Current.SimulationPaused, Is.False);

            // Boss room: the exit stays shut until the boss falls, then continues the same run in chapter 2.
            var encounter = world.GetComponent<Chapter1BossEncounter>();
            Stand(world, encounter.ExitPosition);
            yield return null;
            Assert.That(encounter.TryUseExit(), Is.False);
            encounter.Boss.health = 0;
            for (int frame = 0; frame < 60 && !run.BossDefeated; frame++) yield return null;
            Assert.That(run.BossDefeated, Is.True);
            // Use a valid gameplay health value for the carry check, not the artificial survival boost above.
            player.health = 57;
            var loadout = player.Loadout;
            var economy = run.Economy;
            int carriedGold = economy.Gold;
            float carriedHealth = player.health;
            Stand(world, encounter.ExitPosition);
            string leavingForm = world.arena.Presentation.CurrentForm;
            Assert.That(encounter.TryUseExit(), Is.True);
            Chapter2World chapterTwo = null;
            float deadline = Time.realtimeSinceStartup + 30;
            while (!ChapterSceneFade.Busy && Time.realtimeSinceStartup < deadline) yield return null;
            // The loading shows chapter 2 with the hero in the look chapter 1 drew it with (its weapon form).
            Assert.That(ChapterSceneFade.ShowingPlace, Is.EqualTo(LoadingPlace.Chapter2));
            Assert.That(ChapterSceneFade.ShowingLeaving, Is.EqualTo(LoadingPlace.Chapter1), "the window opens on chapter 1 and sweeps to chapter 2");
            Assert.That(ChapterSceneFade.ShowingHeroForm, Is.Not.Null.And.EqualTo(leavingForm));
            do
            {
                yield return null;
                chapterTwo = Object.FindAnyObjectByType<Chapter2World>();
            } while ((!chapterTwo || !chapterTwo.Ready) && Time.realtimeSinceStartup < deadline);
            Assert.That(chapterTwo && chapterTwo.Ready, Is.True, "the real portcullis/fade enters chapter 2");
            while (ChapterSceneFade.Busy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(ChapterSceneFade.Busy, Is.False, "chapter 2 fade releases the simulation");
            Assert.That(chapterTwo.gameObject.scene.path, Is.EqualTo(Chapter2EntryBridge.ScenePath));
            Assert.That(host.Flow.Current, Is.SameAs(run));
            Assert.That(chapterTwo.SourceRun, Is.SameAs(run));
            AssertChapterTwoFormPool(chapterTwo);
            Assert.That(run.Chapter, Is.EqualTo(2));
            Assert.That(run.Ended, Is.False, "chapter 1 exit must not settle the run");
            Assert.That(run.Result.HasValue, Is.False);
            Assert.That(chapterTwo.Economy, Is.SameAs(economy));
            Assert.That(chapterTwo.Economy.Gold, Is.EqualTo(carriedGold));
            Assert.That(chapterTwo.arena.Player.health, Is.EqualTo(carriedHealth));
            Assert.That(chapterTwo.arena.MvpGrowth, Is.SameAs(growth));
            Assert.That(chapterTwo.arena.MvpGrowth.AttackLevel, Is.EqualTo(1));
            Assert.That(chapterTwo.arena.MvpGrowth.AttackMultiplier, Is.EqualTo(1.05f).Within(.0001));
            var carriedSlot = chapterTwo.arena.MvpGrowth.SnapshotSlots(growthWeapon)[0];
            Assert.That(carriedSlot.InstanceId, Is.EqualTo(carriedInk));
            Assert.That(carriedSlot.Level, Is.EqualTo(2));
            Assert.That(carriedSlot.Part.levels[carriedSlot.Level].ratio, Is.EqualTo(.26f).Within(.0001));
            for (int slot = 0; slot < 2; slot++)
                Assert.That(chapterTwo.arena.Player.Loadout.Weapons[slot].InstanceId,
                            Is.EqualTo(loadout.Weapons[slot].InstanceId));

            // The current last chapter, not chapter 1, owns the final result and return to the village.
            chapterTwo.WarpToRoom("B");
            Assert.That(chapterTwo.TryUseExit(), Is.False);
            int killsBeforeLastBoss = run.Kills;
            using (var bossSim = chapterTwo.arena.AcquireManualSimulation())
            {
                void AdvanceBoss(float seconds) { for(int i=0;i<Mathf.CeilToInt(seconds*120);i++){Physics2D.SyncTransforms();bossSim.Step(1f/120);} }
                AdvanceBoss(2.6f);chapterTwo.Boss.Machine.health=0;AdvanceBoss(1.6f);
                chapterTwo.Boss.Rose.health=0;AdvanceBoss(2.4f);chapterTwo.Boss.Blue.health=0;AdvanceBoss(.1f);
            }
            deadline = Time.realtimeSinceStartup + 5;
            while (run.Kills == killsBeforeLastBoss && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(run.Kills, Is.EqualTo(killsBeforeLastBoss + 1), "native simulation counts the last boss before settling");
            chapterTwo.arena.Player.Position = chapterTwo.Layout.exit.Vector;
            Assert.That(chapterTwo.TryUseExit(), Is.True);
            Assert.That(chapterTwo.Complete, Is.True);
            Assert.That(run.Result.HasValue, Is.True, "last chapter exit settles the run");
            Assert.That(run.Result.Value.Outcome, Is.EqualTo(RunOutcome.ChapterCleared));
            Assert.That(run.Result.Value.Kills, Is.EqualTo(3));
            Assert.That(chapterTwo.ShowingResult, Is.True);

            // The return goes through the loading, to the village, with the hero as chapter 2 drew it.
            string chapterTwoForm = chapterTwo.arena.Presentation.CurrentForm;
            host.GoToVillage();
            Assert.That(ChapterSceneFade.ShowingPlace, Is.EqualTo(LoadingPlace.Village));
            Assert.That(ChapterSceneFade.ShowingLeaving, Is.EqualTo(LoadingPlace.Chapter2), "the window opens on chapter 2 and sweeps to the village");
            Assert.That(ChapterSceneFade.ShowingHeroForm, Is.Not.Null.And.EqualTo(chapterTwoForm));
            for (float waitFrom = Time.realtimeSinceStartup; Time.realtimeSinceStartup - waitFrom < 20 && (SceneManager.GetActiveScene().path != Chapter1RunHost.VillageScene || ChapterSceneFade.Busy);) yield return null;
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(Chapter1RunHost.VillageScene));
            Assert.That(host.ChapterTwoPending, Is.False);
            host.StartNewRun();
            yield return null;
            for (int frame = 0; frame < 600 && (!ReadyWorld() || host.Flow.Current == run); frame++) yield return null;
            var second = host.Flow.Current;
            Assert.That(second, Is.Not.SameAs(run));
            Assert.That(second.Economy.Gold, Is.Zero);
            Assert.That(second.Growth, Is.Not.SameAs(growth));
            Assert.That(second.Growth.AttackLevel, Is.Zero);
            Assert.That(second.Stash.InkParts + second.Stash.Stamps.Count, Is.Zero);
            Assert.That(second.BossDefeated, Is.False);
            Assert.That(ReadyWorld().GetComponent<Chapter1StartingBlueprints>().Complete, Is.False);
            yield return new ExitPlayMode();
        }
    }
}
