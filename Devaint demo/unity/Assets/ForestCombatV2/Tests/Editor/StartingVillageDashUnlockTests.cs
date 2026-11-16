using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Control.Contracts;
using Deviant.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class StartingVillageDashUnlockTests
    {
        StartingVillageWorld village;
        CombatArena Arena => village.World.arena;
        CombatPlayer Player => Arena.Player;
        const float Step = .005f;
        // The dash follows the whole speed line and attack tier 4 (prerequisite tree, user decision 2026-11-13).
        static readonly string[] MoveUnlocks = { "move_speed_bonus_1", "move_speed_bonus_2", "move_speed_bonus_3", "move_speed_bonus_4",
            "attack_bonus_1", "attack_bonus_2", "attack_bonus_3", "attack_bonus_4" };

        static ProfileData Profile(bool owned = false) => new ProfileData
        {
            profileId = Guid.NewGuid().ToString(), pigment = 10000, runsStarted = 1, runsDied = 1,
            chapter1BossFirstClear = true, chapter1Clears = 1, chaptersCleared = 2,
            unlocks = owned ? MoveUnlocks.Concat(new[] { "extra_dash_1" }).ToArray() : MoveUnlocks.ToArray(),
            // The village ink rewards and the executioner's note are already seen, so no return scene blocks the dash input.
            seenStories = new[] { "opening", "first-return", "artisan-first-run", "artisan-visit-1", "artisan-visit-2", "artisan-visit-3", "artisan-visit-4", "artisan-visit-5",
                "village-ink-reward-1", "village-ink-reward-2", "village-ink-reward-3", "village-ink-reward-4", "village-ink-reward-5", "village-executioner-note",
                "station-opened-palette_bench", "station-opened-supply_counter", "station-opened-reclaim_bench" }
        };
        static void PrepareScene()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(StartingVillageTests.ScenePath);
        }
        IEnumerator Open(MemoryProfileStore store)
        {
            Chapter1ProfileHost.UseStore(store);
            for (int i = 0; i < 600 && !(village && village.Ready); i++) { yield return null; village = Object.FindAnyObjectByType<StartingVillageWorld>(); }
            Assert.That(village && village.Ready, Is.True);
            const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
            var gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(value => value.GetType().Name == "TemporaryGameGui");
            if ((bool)gui.GetType().GetProperty("OpeningActive").GetValue(gui)) gui.GetType().GetMethod("SkipOpening", Private).Invoke(gui, null);
            yield return null; yield return null;
            Assert.That(Player.InputBlocked, Is.False);
            Assert.That(Arena.Controls.Current.SimulationPaused, Is.False);
            Assert.That(Player.Abilities.Has(MovementAbilities.Dash), Is.True);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            Chapter1ProfileHost.UseStore(null);
        }
        void Place()
        {
            Player.Position = village.Find(StartingVillageWorld.ReturnWell).Position;
            Player.spawn = Player.Position; Player.velocity = Vector2.zero;
            Physics2D.SyncTransforms(); Player.grounded = Player.ProbeGround();
        }
        void Until(CombatArena.ManualSimulation driver, float time)
        { while (Arena.Now < time) driver.Step(Step); }

        [UnityTest] public IEnumerator UnownedVillageStillAllowsOnlyOneDashBeforeRecovery()
        {
            PrepareScene(); yield return new EnterPlayMode();
            yield return Open(new MemoryProfileStore(Profile()));
            Assert.That(Player.DashCharges, Is.EqualTo(1));
            using (var driver = Arena.AcquireManualSimulation())
            {
                driver.Reset(); Place();
                driver.Step(Step, new CombatInput(1, CombatButtons.Dash));
                Assert.That(Player.Dashing, Is.True); Assert.That(Player.DashAvailable, Is.Zero);
                float first = Player.Dash.StartedAt, ready = Player.Dash.ReadyAt;
                Until(driver, first + Arena.Movement.DashDuration + Step);
                driver.Step(Step, new CombatInput(1, CombatButtons.Dash));
                Assert.That(Player.Dash.StartedAt, Is.EqualTo(first), "The unowned profile cannot start a second dash.");
                Until(driver, ready + Step); Assert.That(Player.DashAvailable, Is.EqualTo(1));
            }
        }
        [UnityTest] public IEnumerator SuccessfulPurchaseImmediatelyAddsVillageDashAndKeepsTheSharedRecovery()
        {
            PrepareScene(); yield return new EnterPlayMode();
            var store = new MemoryProfileStore(Profile());
            yield return Open(store);
            var service = Chapter1ProfileHost.Service; int runs = service.Snapshot.RunsStarted; long pigment = service.Snapshot.Pigment;
            var gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(value => value.GetType().Name == "TemporaryGameGui");
            var when = gui.GetType().GetMethod("When", BindingFlags.Static | BindingFlags.NonPublic);
            var timing = (string)when.Invoke(null, new object[] { service.Catalog.Find("extra_dash_1") });
            Assert.That(timing, Does.StartWith("적용 ").And.Contain("마을 즉시").And.Contain("다음 새 런"));
            store.FailNextSave = true;
            Assert.That(service.TryPurchase("extra_dash_1", out _), Is.False);
            yield return null; Assert.That(Player.DashCharges, Is.EqualTo(1), "A failed save cannot grant the unlock.");
            using (var scope = Arena.Controls.OpenScope("village dash purchase"))
            {
                scope.BlockInput(InputChannels.Combat | InputChannels.Interaction); scope.PauseSimulation();
                Assert.That(service.TryPurchase("extra_dash_1", out var result), Is.True, result.ToString());
                yield return null;
                Assert.That(Player.DashCharges, Is.EqualTo(2), "The purchase applies in the village before departing.");
            }
            Assert.That(service.Snapshot.RunsStarted, Is.EqualTo(runs), "Updating the lobby must not create a departure plan.");
            Assert.That(service.Snapshot.Pigment, Is.EqualTo(pigment - service.Catalog.Find("extra_dash_1").cost));
            using (var driver = Arena.AcquireManualSimulation())
            {
                driver.Reset(); Place();
                driver.Step(Step, new CombatInput(1, CombatButtons.Dash));
                Assert.That(Player.Dashing, Is.True); Assert.That(Player.DashAvailable, Is.EqualTo(1));
                float first = Player.Dash.StartedAt, ready = Player.Dash.ReadyAt;
                Until(driver, first + Arena.Movement.DashDuration + Step);
                Assert.That(service.MarkStorySeen("village-dash-capacity-refresh-test"), Is.True);
                yield return null;
                Assert.That(Player.DashAvailable, Is.EqualTo(1), "An unrelated profile change must not refill a spent charge.");
                Assert.That(Player.Dash.ReadyAt, Is.EqualTo(ready));
                driver.Step(Step, new CombatInput(1, CombatButtons.Dash));
                Assert.That(Player.Dashing, Is.True); Assert.That(Player.DashAvailable, Is.Zero);
                Assert.That(Player.Dash.ReadyAt, Is.EqualTo(ready), "The second dash shares the first dash's deadline.");
                Until(driver, ready - Step * 2); Assert.That(Player.DashAvailable, Is.Zero);
                Until(driver, ready + Step); Assert.That(Player.DashAvailable, Is.EqualTo(2));
            }
        }
        [UnityTest] public IEnumerator SavedUnlockRebindAndCombatResetKeepTwoChargesAndProfileResetRestoresOne()
        {
            PrepareScene(); yield return new EnterPlayMode();
            var store = new MemoryProfileStore(Profile(true));
            yield return Open(store);
            Assert.That(Player.DashCharges, Is.EqualTo(2), "A loaded permanent unlock applies on village entry.");
            Arena.ResetTest(); yield return null;
            Assert.That(Player.DashCharges, Is.EqualTo(2)); Assert.That(Player.DashAvailable, Is.EqualTo(2));
            Chapter1ProfileHost.UseStore(store); yield return null;
            Assert.That(Player.DashCharges, Is.EqualTo(2), "Replacing the service with its saved profile rebinds the same capacity.");
            var fresh = new MemoryProfileStore(Profile());
            Chapter1ProfileHost.UseStore(fresh); yield return null;
            Assert.That(Player.DashCharges, Is.EqualTo(1), "Switching to a profile without the unlock removes the extra charge.");
            Chapter1ProfileHost.UseStore(store); yield return null; Assert.That(Player.DashCharges, Is.EqualTo(2));
            Assert.That(Chapter1ProfileHost.Service.ResetProfile(), Is.True); yield return null;
            Assert.That(Player.DashCharges, Is.EqualTo(1));
        }
    }
}
