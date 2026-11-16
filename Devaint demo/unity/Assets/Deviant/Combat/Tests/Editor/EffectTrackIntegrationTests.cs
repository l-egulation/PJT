using System;
using System.Collections;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class EffectTrackIntegrationTests
    {
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator GameWaveAndGuardUseCompiledSpansAndKeepNativeIndependentPolicies() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabWaveAndGuardUseCompiledSpansAndKeepNativeIndependentPolicies() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        [UnityTest] public IEnumerator GameWaveFlightDamageAndGuardValuesPinCurrentAndAdoptNextNumbers() => NumericExercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabWaveFlightDamageAndGuardValuesPinCurrentAndAdoptNextNumbers() => NumericExercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        static void Setup(CombatArena arena, CombatArena.ManualSimulation driver, string id)
        {
            driver.Reset(); var p = arena.Player; p.selected = 0; p.InputBlocked = false;
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(40, 1000); }
            foreach (var weapon in p.weapons) weapon.StatSlots.Clear();
            int form = Array.FindIndex(arena.catalog.forms, value => value.id == (id == ActionTrackDefinition.GuardId ? "shield" : "base_sword"));
            int special = Array.FindIndex(arena.catalog.forms[form].specials, value => value.id == id);
            Assert.That(p.TryReconfigure(0, form, special, out var error), Is.True, error);
            p.Position = new Vector2(0, 1000); p.velocity = Vector2.zero; p.grounded = true; p.SyncFacing(1);
            arena.enemies[0].Position = new Vector2(5, 1000); arena.enemies[0].health = 1000; Physics2D.SyncTransforms();
        }
        static void Tick(CombatArena.ManualSimulation driver, int count) { for (int tick = 0; tick < count; tick++) driver.Step(.02f); }
        static IEnumerator NumericExercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false; yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var floor = new GameObject("EffectNumericFloor"); floor.transform.position = new Vector3(0, 999.5f, 0); floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(200, 1);
            try { using (var driver = arena.AcquireManualSimulation()) {
                var wave = arena.GetActionTracks(ActionTrackDefinition.WaveId); var numeric = wave.ToNumericData(); numeric.hits[0].damage = 53; numeric.waveSpeed = 8; numeric.waveDistance = 2; numeric.waveSpawnX = 2; numeric.waveSpawnY = 1.25f;
                var editedWave = wave.Recompile(wave.ToDocument(), numeric);
                Setup(arena, driver, wave.Id); driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); var running = arena.Player.currentCast; Tick(driver, 10);
                Assert.That(arena.TryApplyActionTracks(new[] { editedWave }, false, out var error), Is.True, error); running.Cancel(); driver.Step(.02f, new CombatInput(0, CombatButtons.Swap)); Tick(driver, 40);
                Assert.That(arena.enemies[0].health, Is.EqualTo(960)); Assert.That(arena.enemies[0].ReceivedHitCount, Is.EqualTo(1), "The old independent wave keeps launch speed, range, damage and activation weapon after publication/exchange.");
                Setup(arena, driver, wave.Id); arena.enemies[0].Position = new Vector2(3.5f, 1000); Physics2D.SyncTransforms(); driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); var next = arena.Player.currentCast;
                int flightTicks = 0; while (next.DebugBoxCount == 0 && flightTicks++ < 30) driver.Step(.02f);
                Assert.That(next.DebugBoxCount, Is.GreaterThan(0)); Assert.That(next.GetDebugBox(0).center.x, Is.EqualTo(2).Within(.00001f)); Assert.That(next.GetDebugBox(0).center.y, Is.EqualTo(1001.25f).Within(.0001f));
                float previousX = next.GetDebugBox(next.DebugBoxCount - 1).center.x; Assert.That(previousX - 2, Is.EqualTo(8 * .02f).Within(.00001f));
                driver.Step(.02f); float followingX = next.GetDebugBox(next.DebugBoxCount - 1).center.x; Assert.That(followingX - previousX, Is.EqualTo(8 * .02f).Within(.00001f));
                Tick(driver, 40); Assert.That(arena.enemies[0].health, Is.EqualTo(947)); Assert.That(arena.enemies[0].ReceivedHitCount, Is.EqualTo(1));
                Setup(arena, driver, wave.Id); arena.enemies[0].Position = new Vector2(6, 1000); Physics2D.SyncTransforms(); driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); Tick(driver, 50);
                Assert.That(arena.enemies[0].health, Is.EqualTo(1000)); Assert.That(arena.enemies[0].ReceivedHitCount, Is.Zero, "The edited live range, not the retention marker, ends the actual wave sweep.");

                var guard = arena.GetActionTracks(ActionTrackDefinition.GuardId); numeric = guard.ToNumericData(); numeric.guardHp = 50; numeric.guardReduction = .25f; var editedGuard = guard.Recompile(guard.ToDocument(), numeric);
                Setup(arena, driver, guard.Id); driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); running = arena.Player.currentCast; Tick(driver, 6); float deadline = arena.Player.guardUntil;
                Assert.That(arena.TryApplyActionTracks(new[] { editedGuard }, false, out error), Is.True, error); running.Cancel(); driver.Step(.02f, new CombatInput(0, CombatButtons.Swap)); Tick(driver, 15);
                float health = arena.Player.health; Assert.That(arena.Player.Receive(100, arena.enemies[0], -1, false, null), Is.True); Assert.That(arena.Player.health, Is.EqualTo(health - 30).Within(.00001f)); Assert.That(arena.Player.guardUntil, Is.EqualTo(deadline));
                Setup(arena, driver, guard.Id); driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); Tick(driver, 6); Assert.That(arena.Player.shield, Is.EqualTo(50));
                health = arena.Player.health; Assert.That(arena.Player.Receive(100, arena.enemies[0], -1, false, null), Is.True); Assert.That(arena.Player.health, Is.EqualTo(health - 25).Within(.00001f)); Assert.That(arena.Player.shield, Is.Zero);
                health = arena.Player.health; Assert.That(arena.Player.Receive(10, arena.enemies[0], -1, false, null, true), Is.True); Assert.That(arena.Player.health, Is.EqualTo(health - 10));
                float now = arena.Now; Assert.That(arena.TryApplyActionTracks(new[] { editedWave, editedWave }, true, out error), Is.False); Assert.That(arena.Now, Is.EqualTo(now));
                Assert.That(arena.TryApplyActionTracks(new[] { wave, guard }, false, out error), Is.True, error); Assert.That(arena.SimulationFaulted, Is.False);
            } }
            finally { Object.Destroy(floor); }
        }
        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false; yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>();
            var floor = new GameObject("EffectTracksFloor"); floor.transform.position = new Vector3(0, 999.5f, 0); floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(200, 1);
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    var originalWave = arena.GetActionTracks(ActionTrackDefinition.WaveId); var originalGuard = arena.GetActionTracks(ActionTrackDefinition.GuardId);
                    var oldMovement = arena.GetActionTracks(ActionTrackDefinition.MovementId); var oldEcho = arena.GetActionTracks(ActionTrackDefinition.FollowUpId);
                    Setup(arena, driver, ActionTrackDefinition.WaveId); driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); var running = arena.Player.currentCast;
                    Assert.That(running.ActionTracks, Is.SameAs(originalWave)); Tick(driver, 10);
                    var document = originalWave.ToDocument(); document.timeline.frames[0].seconds += .03f; document.hits[0].box.height = .25f;
                    document.timeline.markers.Single(marker => marker.id == "wave-end").offset = .01f; var editedWave = originalWave.Recompile(document);
                    Assert.That(arena.TryApplyActionTracks(new[] { editedWave }, false, out var error), Is.True, error);
                    Assert.That(arena.GetActionTracks(ActionTrackDefinition.MovementId), Is.SameAs(oldMovement)); Assert.That(arena.GetActionTracks(ActionTrackDefinition.FollowUpId), Is.SameAs(oldEcho));
                    running.Cancel(); arena.Player.Position = new Vector2(12, 1000); Physics2D.SyncTransforms(); Tick(driver, 40);
                    Assert.That(arena.enemies[0].ReceivedHitCount, Is.EqualTo(1)); Assert.That(arena.enemies[0].health, Is.EqualTo(960));
                    Setup(arena, driver, ActionTrackDefinition.WaveId); driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); var next = arena.Player.currentCast;
                    Assert.That(next.ActionTracks, Is.SameAs(editedWave)); Assert.That(next.ImpactTime, Is.EqualTo(.23f).Within(.000001f));
                    Assert.That(next.Definition.Effect.Duration, Is.EqualTo(.01f)); Tick(driver, 45);
                    Assert.That(arena.enemies[0].ReceivedHitCount, Is.EqualTo(1), "Wave still traverses the existing distance after the authored minimum retention span.");
                    Assert.That(arena.TryApplyActionTracks(new[] { originalWave }, false, out error), Is.True, error);

                    Setup(arena, driver, ActionTrackDefinition.GuardId); driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); running = arena.Player.currentCast;
                    Tick(driver, 6); float oldDeadline = arena.Player.guardUntil; Assert.That(arena.Player.shield, Is.EqualTo(30)); Assert.That(oldDeadline, Is.GreaterThan(3));
                    document = originalGuard.ToDocument(); document.timeline.frames[0].seconds = .2f;
                    document.timeline.markers.Single(marker => marker.id == "guard-end").offset = .5f; var editedGuard = originalGuard.Recompile(document);
                    Assert.That(arena.TryApplyActionTracks(new[] { editedGuard }, false, out error), Is.True, error);
                    Assert.That(running.ActionTracks, Is.SameAs(originalGuard)); running.Cancel();
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Swap)); Tick(driver, 15);
                    Assert.That(arena.Player.guardUntil, Is.EqualTo(oldDeadline)); Assert.That(arena.Player.shield, Is.EqualTo(30));
                    float health = arena.Player.health; Assert.That(arena.Player.Receive(100, arena.enemies[0], -1, false, null), Is.True);
                    Assert.That(arena.Player.health, Is.EqualTo(health - 30).Within(.00001f)); Assert.That(arena.Player.shield, Is.Zero); Assert.That(arena.Player.guardUntil, Is.EqualTo(oldDeadline));
                    health = arena.Player.health; Assert.That(arena.Player.Receive(10, arena.enemies[0], -1, false, null, true), Is.True);
                    Assert.That(arena.Player.health, Is.EqualTo(health - 10), "Bypass ignores guard reduction/shield in the existing damage owner.");
                    Setup(arena, driver, ActionTrackDefinition.GuardId); driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); next = arena.Player.currentCast;
                    Assert.That(next.ActionTracks, Is.SameAs(editedGuard)); Tick(driver, 12);
                    float shortDeadline = arena.Player.guardUntil; Assert.That(shortDeadline, Is.LessThan(1)); Assert.That(arena.Player.shield, Is.EqualTo(30));
                    Tick(driver, 50); Assert.That(arena.Player.shield, Is.Zero); Assert.That(arena.Player.guardUntil, Is.EqualTo(shortDeadline));
                    Assert.That(arena.TryApplyActionTracks(new[] { originalGuard }, false, out error), Is.True, error);

                    Setup(arena, driver, ActionTrackDefinition.GuardId); arena.Player.guardUntil = arena.Now + 5; arena.Player.shield = 7;
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); Tick(driver, 6);
                    Assert.That(arena.Player.shield, Is.EqualTo(30)); Assert.That(arena.Player.guardUntil, Is.EqualTo(5), "A recast replaces shield and keeps the longer existing deadline.");
                    float now = arena.Now; Assert.That(arena.TryApplyActionTracks(new[] { originalWave, originalWave }, true, out error), Is.False); Assert.That(arena.Now, Is.EqualTo(now));
                    Assert.That(arena.SimulationFaulted, Is.False);
                }
            }
            finally { Object.Destroy(floor); }
        }
    }
}
