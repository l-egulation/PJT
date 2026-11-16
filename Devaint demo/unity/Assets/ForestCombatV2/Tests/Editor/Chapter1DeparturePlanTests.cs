using System.Collections;
using System.IO;
using System.Linq;
using Deviant.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ForestCombatV2.Tests
{
    // A village departure plan equips both blueprints, their palettes and the starting bonus exactly once.
    public sealed class Chapter1DeparturePlanTests
    {
        static ForestCombatWorld Bound(RunSession run)
        {
            var world = Object.FindAnyObjectByType<ForestCombatWorld>();
            var binding = world ? world.GetComponent<Chapter1RunBinding>() : null;
            return world && world.Ready && binding && binding.Run == run ? world : null;
        }

        [UnityTest] public IEnumerator DepartureEquipsPlannedBlueprintsPalettesAndGoldOnce()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();
            for (int frame = 0; frame < 600 && !(Object.FindAnyObjectByType<ForestCombatWorld>()?.Ready ?? false); frame++) yield return null;

            // A test-only profile: real purchases through the service, never the player's saved profile.
            var catalog = new ProgressionCatalog(JsonUtility.FromJson<ProgressionData>(File.ReadAllText("../data/combat/roguelite_progression.json")));
            var service = new ProgressionService(catalog, new MemoryProfileStore());
            var warmup = service.PlanRun(new[] { "sword", "blunt" }).RunId;
            for (int i = 0; i < 80; i++) service.AwardPigment(warmup, "boss" + i, "boss", false);
            Assert.That(service.SettleRun(warmup, RunOutcome.ChapterCleared, false, service.ChaptersCleared + 1), Is.True);
            // Two more clears: the reclaim workshop opens with the second palette piece (the chapter-2 boss) and the supply
            // office opened after the first settled run.
            for (int i = 0; i < 2; i++) Assert.That(service.SettleRun(service.PlanRun(new[] { "sword", "blunt" }).RunId, RunOutcome.ChapterCleared, false, service.ChaptersCleared + 1), Is.True);
            // The third clear also passed the chapter-2 boss, so the last bag tiers and the extra dash are on sale.
            // Kill gold follows the whole starting-gold node and weapon-break gold the whole kill-gold node.
            foreach (var id in new[] { "starting_gold_50", "starting_gold_100", "starting_gold_150", "starting_gold_200", "attack_bonus_1", "attack_bonus_2", "attack_bonus_3", "attack_bonus_4", "max_health_bonus_1",
                "shared_rerolls_1", "shared_rerolls_2", "kill_gold_1", "kill_gold_2", "kill_gold_3", "weapon_break_gold_1",
                "move_speed_bonus_1", "move_speed_bonus_2", "move_speed_bonus_3", "move_speed_bonus_4", "extra_dash_1" })
                Assert.That(service.TryPurchase(id, out _), Is.True, id);
            var plan = service.PlanRun(new[] { "blunt", "ranged" });

            var host = Chapter1RunHost.Ensure();
            host.StartNewRun(plan);
            var run = host.Flow.Current;
            ForestCombatWorld world = null;
            for (float waitFrom = Time.realtimeSinceStartup; Time.realtimeSinceStartup - waitFrom < 20 && !(world = Bound(run));) yield return null;
            Assert.That(world, Is.Not.Null, "the chapter loads for the planned run");
            var blueprints = world.GetComponent<Chapter1StartingBlueprints>();
            for (int frame = 0; frame < 120 && !blueprints.Complete; frame++) yield return null;
            yield return null;

            Assert.That(run.Plan, Is.SameAs(plan));
            Assert.That(blueprints.Preselected && blueprints.Complete, Is.True, "no start-room choice is needed");
            Assert.That(blueprints.ExitLocked, Is.False);
            var player = world.arena.Player; var forms = world.arena.catalog.forms;
            Assert.That(forms[player.weapons[0].FormIndex].id, Is.EqualTo("hammer"));
            Assert.That(forms[player.weapons[1].FormIndex].id, Is.EqualTo("launcher"));
            Assert.That(player.selected, Is.Zero, "the new run starts holding weapon 1");
            var growth = run.Growth;
            var first = growth.Equipped(player.weapons[0].InstanceId); var second = growth.Equipped(player.weapons[1].InstanceId);
            Assert.That(first.Slots.Count, Is.EqualTo(3)); Assert.That(second.Slots.Count, Is.EqualTo(3), "development preselection retains base palettes");
            Assert.That(first.Slots.Concat(second.Slots).All(s => s.Color != "colorless"), Is.True, "retired wildcard unlocks no longer affect departure");
            Assert.That(growth.Palettes.Count, Is.EqualTo(2));
            Assert.That(run.Economy.Gold, Is.EqualTo(200), "starting gold 0 + 200");
            Assert.That(growth.DestroyedInkGoldBonus, Is.Zero);
            Assert.That(run.Perks.RerollsRemaining, Is.EqualTo(2)); Assert.That(run.Perks.Settings.WeaponBreakGold, Is.EqualTo(20));
            Assert.That(growth.AttackMultiplier, Is.EqualTo(1.2f).Within(1e-5f), "attack tier 4 (the dash's prerequisite) adds 20% on top of no maintenance level");
            Assert.That(player.maxHealth, Is.EqualTo(105), "health tier 1 raises the maximum by 5");
            Assert.That(player.health, Is.EqualTo(105), "and the run starts full");
            var bag = world.arena.GetComponent<Deviant.Inventory.Unity.InventoryRuntimeHost>();
            Assert.That(growth.InkCapacity, Is.EqualTo(growth.Data.inkCapacity), "retired bag tiers no longer affect departure");
            Assert.That(bag.Inventory.Capacity, Is.EqualTo(growth.Data.inkCapacity));
            Assert.That(world.arena.PermanentMoveSpeedBonus, Is.EqualTo(.2f).Within(1e-5f), "move-speed tier 4 adds 20%");
            Assert.That(player.DashCharges, Is.EqualTo(2), "the extra dash");

            // A combat reset keeps the session: no second payout and the same palettes.
            world.arena.ResetTest();
            for (int frame = 0; frame < 10; frame++) yield return null;
            Assert.That(run.Economy.Gold, Is.EqualTo(200));
            Assert.That(player.maxHealth, Is.EqualTo(105), "a reset keeps the raised maximum without adding it again");
            Assert.That(player.health, Is.EqualTo(105));
            Assert.That(world.arena.PermanentMoveSpeedBonus, Is.EqualTo(.2f).Within(1e-5f)); Assert.That(player.DashCharges, Is.EqualTo(2), "a reset keeps the unlocks");
            Assert.That(bag.Inventory.Capacity, Is.EqualTo(growth.Data.inkCapacity));
            Assert.That(growth.Equipped(player.weapons[0].InstanceId), Is.SameAs(first));
            Assert.That(run.GrantStartingGold(), Is.False);
            yield return new ExitPlayMode();
        }
    }
}
