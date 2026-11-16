using System;
using System.Collections;
using System.Collections.Generic;
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
    public class FiniteActionTrackIntegrationTests
    {
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator GameFiniteStartsKeepResourcesMotionAndIndependentVersionPins() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", Run);
        [UnityTest] public IEnumerator LabFiniteStartsKeepResourcesMotionAndIndependentVersionPins() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", Run);
        [UnityTest] public IEnumerator GameUneditedFiniteTracksMatchNativePolicyTraceBitForBit() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", TraceParity);
        [UnityTest] public IEnumerator LabUneditedFiniteTracksMatchNativePolicyTraceBitForBit() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", TraceParity);
        [UnityTest] public IEnumerator GameNumericDamageDashAndWallRecoveryUsePinnedDefinitions() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", NumericConsumers);
        [UnityTest] public IEnumerator LabNumericDamageDashAndWallRecoveryUsePinnedDefinitions() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", NumericConsumers);
        static IEnumerator Exercise(string scene, Action<CombatArena, CombatArena.ManualSimulation> action)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); Assert.That(arena.Player, Is.Not.Null); Assert.That(arena.ActionTrackCount, Is.EqualTo(ActionTrackDefinition.SupportedIds.Count));
            var floor = new GameObject("FiniteTracksFixtureFloor"); floor.transform.position = new Vector3(0, 999.5f, 0); floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(200, 1); Physics2D.SyncTransforms();
            try { using (var driver = arena.AcquireManualSimulation()) { action(arena, driver); Assert.That(arena.SimulationFaulted, Is.False); } }
            finally { Object.Destroy(floor); }
        }
        static void Setup(CombatArena arena, CombatArena.ManualSimulation driver, string id, float x = 2, int facing = 1, bool air = false)
        {
            driver.Reset(); var player = arena.Player; player.selected = 0; player.InputBlocked = false;
            foreach (var weapon in player.weapons) weapon.StatSlots.Clear();
            foreach (var target in arena.enemies) { target.aggressive = false; target.Position = new Vector2(40, 1000); }
            int form = Array.FindIndex(arena.catalog.forms, value => value.id == "base_sword");
            int special = Array.FindIndex(arena.catalog.forms[form].specials, value => value.id == id);
            Assert.That(player.TryReconfigure(0, form, special, out var error), Is.True, error);
            player.Position = new Vector2(0, air ? 1003 : 1000); player.velocity = Vector2.zero; player.grounded = !air; player.SyncFacing(facing);
            arena.enemies[0].Position = new Vector2(x * facing, player.Position.y); arena.enemies[0].health = 1000;
            Physics2D.SyncTransforms();
        }
        static ActionTrackDefinition[] Pair(CombatArena arena) => new[] { arena.GetActionTracks(ActionTrackDefinition.MovementId), arena.GetActionTracks(ActionTrackDefinition.FollowUpId) };
        static void Tick(CombatArena.ManualSimulation driver, int count) { for (int index = 0; index < count; index++) driver.Step(.02f); }
        static void Run(CombatArena arena, CombatArena.ManualSimulation driver)
        {
            var original = Pair(arena);
            Setup(arena, driver, ActionTrackDefinition.FollowUpId); driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); var running = arena.Player.currentCast;
            Assert.That(running.UsesCompiledTracks, Is.True); Assert.That(running.ActionTracks, Is.SameAs(original[1]));
            Tick(driver, 11); Assert.That(arena.enemies[0].health, Is.EqualTo(975)); Assert.That(running.ResourceResult.Status, Is.EqualTo(ActionResourceStatus.Succeeded));
            var editedDocument = original[1].ToDocument(); editedDocument.timeline.markers.Single(marker => marker.id == "echo-start").offset = .6f;
            editedDocument.hits[1].box.x = -50; var edited = original[1].Recompile(editedDocument);
            Assert.That(arena.TryApplyActionTracks(new[] { original[0], edited }, false, out var error), Is.True, error);
            Assert.That(running.DefinitionVersion, Is.EqualTo(original[1].ContentHash)); Assert.That(running.Definition.Hits[1].Time, Is.EqualTo(.5f));
            running.Cancel(); arena.Player.Position = new Vector2(10, 1000); Physics2D.SyncTransforms(); Tick(driver, 25);
            Assert.That(arena.enemies[0].health, Is.EqualTo(950)); Assert.That(arena.enemies[0].ReceivedHitCount, Is.EqualTo(2));
            Assert.That(arena.Player.weapons[0].Cooldown.Available, Is.Zero);
            Setup(arena, driver, ActionTrackDefinition.FollowUpId); driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); var next = arena.Player.currentCast;
            Assert.That(next.ActionTracks, Is.SameAs(edited)); Assert.That(next.Definition.Hits[1].Time, Is.EqualTo(.8f)); Tick(driver, 50);
            Assert.That(arena.enemies[0].health, Is.EqualTo(975));
            float now = arena.Now; Assert.That(arena.TryApplyActionTracks(new[] { original[0], original[0] }, true, out error), Is.False);
            Assert.That(arena.Now, Is.EqualTo(now)); Assert.That(arena.GetActionTracks(ActionTrackDefinition.FollowUpId), Is.SameAs(edited));
            Assert.That(arena.TryApplyActionTracks(original, false, out error), Is.True, error);

            Setup(arena, driver, ActionTrackDefinition.FollowUpId); driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); running = arena.Player.currentCast;
            Tick(driver, 10); driver.Step(.02f, new CombatInput(0, CombatButtons.Swap)); Tick(driver, 30);
            Assert.That(arena.Player.selected, Is.EqualTo(1)); Assert.That(arena.enemies[0].health, Is.EqualTo(950), "Echo retains activation pose and original weapon damage after exchange.");
            Setup(arena, driver, ActionTrackDefinition.MovementId, 40); driver.Step(.02f, new CombatInput(0, CombatButtons.Special));
            Tick(driver, 2); driver.Step(.02f, new CombatInput(0, CombatButtons.Dash)); Tick(driver, 35);
            Assert.That(arena.Player.weapons[0].Cooldown.Available, Is.EqualTo(1), "Windup cancel does not consume activation resources.");
            Setup(arena, driver, ActionTrackDefinition.MovementId, 40); driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); running = arena.Player.currentCast;
            var moveDocument = original[0].ToDocument(); moveDocument.timeline.frames[0].seconds += .03f;
            moveDocument.timeline.frames.Single(frame => frame.id == "dash").seconds = .24f;
            moveDocument.timeline.markers.Single(marker => marker.id == "dash-end").offset = .24f; moveDocument.hits[0].box.width = 2;
            var movement = original[0].Recompile(moveDocument); Assert.That(arena.TryApplyActionTracks(new[] { movement, original[1] }, false, out error), Is.True, error);
            Tick(driver, 40); Assert.That(arena.Player.Position.x, Is.EqualTo(4).Within(.0001f));
            Setup(arena, driver, ActionTrackDefinition.MovementId, 40); driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); next = arena.Player.currentCast;
            Assert.That(next.ImpactTime, Is.EqualTo(.18f).Within(.000001f)); Assert.That(next.MotionDuration, Is.EqualTo(.67f).Within(.000001f)); Assert.That(next.Definition.Hits[0].Box.Width, Is.EqualTo(2));
            Assert.That(next.Definition.Movement.Duration, Is.EqualTo(.24f));
            Tick(driver, 40); Assert.That(arena.Player.Position.x, Is.EqualTo(4.8f).Within(.0001f));
            Assert.That(arena.TryApplyActionTracks(original, false, out error), Is.True, error);
            Setup(arena, driver, ActionTrackDefinition.FollowUpId); driver.Step(.02f, new CombatInput(0, CombatButtons.Special));
            arena.Player.Weapon.StatSlots[0] = 2; Tick(driver, 10); float afterPrimary = arena.enemies[0].health;
            Assert.That(afterPrimary, Is.LessThan(975), "Stats are captured at activation, not at cast construction.");
            arena.Player.Weapon.StatSlots.Clear(); Tick(driver, 30);
            Assert.That(1000 - arena.enemies[0].health, Is.EqualTo(2 * (1000 - afterPrimary)).Within(.00001f), "The independent tail retains the primary activation resource snapshot.");
            Setup(arena, driver, ActionTrackDefinition.FollowUpId); arena.Player.Weapon.StatSlots[0] = 3;
            driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); next = arena.Player.currentCast;
            Assert.That(next.ImpactTime, Is.EqualTo(original[1].Activation)); Assert.That(next.MotionDuration, Is.EqualTo(original[1].BodyEnd), "Special finite tracks do not apply basic attack speed.");
            Tick(driver, 40); Assert.That(arena.enemies[0].ReceivedHitCount, Is.EqualTo(2));
        }
        static void NumericConsumers(CombatArena arena, CombatArena.ManualSimulation driver)
        {
            var original = Pair(arena); var movement = original[0].ToNumericData(); movement.hits[0].damage = 13; movement.dashSpeed = 12; movement.wallRecovery = .4f;
            var editedMovement = original[0].Recompile(original[0].ToDocument(), movement);
            Setup(arena, driver, ActionTrackDefinition.MovementId, 40); driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); var running = arena.Player.currentCast;
            Assert.That(arena.TryApplyActionTracks(new[] { editedMovement }, false, out var error), Is.True, error); Tick(driver, 40);
            Assert.That(running.ActionTracks, Is.SameAs(original[0])); Assert.That(arena.Player.Position.x, Is.EqualTo(4).Within(.0001f));
            Setup(arena, driver, ActionTrackDefinition.MovementId, 40); driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); var next = arena.Player.currentCast; Tick(driver, 40);
            Assert.That(next.ActionTracks, Is.SameAs(editedMovement)); Assert.That(arena.Player.Position.x, Is.EqualTo(2.4f).Within(.0001f));
            Setup(arena, driver, ActionTrackDefinition.MovementId, 2); driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); Tick(driver, 40);
            Assert.That(arena.enemies[0].health, Is.EqualTo(987)); Assert.That(arena.enemies[0].ReceivedHitCount, Is.EqualTo(1)); Assert.That(arena.Player.Weapon.Cooldown.Available, Is.Zero);

            var wall = new GameObject("FiniteNumericWall"); wall.AddComponent<ArenaSurface>(); wall.transform.position = new Vector3(2, 1007, 0); wall.AddComponent<BoxCollider2D>().size = new Vector2(.5f, 16);
            try {
                foreach (var definition in new[] { original[0], editedMovement }) {
                    Assert.That(arena.TryApplyActionTracks(new[] { definition }, false, out error), Is.True, error); Setup(arena, driver, ActionTrackDefinition.MovementId, 40);
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); var cast = arena.Player.currentCast; bool contacted = false;
                    for (int tick = 0; tick < 30; tick++) {
                        driver.Step(.02f);
                        if (!arena.Player.wallHit) continue;
                        contacted = true; Assert.That(cast.EndTime - arena.Now, Is.EqualTo(definition.Execution.Movement.WallRecovery).Within(.00001f)); Assert.That(cast.Moving, Is.False); break;
                    }
                    Assert.That(contacted, Is.True, "The actual solid wall must exercise the existing recovery consumer.");
                }
            }
            finally { wall.SetActive(false); Object.Destroy(wall); }

            var numeric = original[1].ToNumericData(); numeric.hits[0].damage = 9; numeric.hits[1].damage = 17; var editedEcho = original[1].Recompile(original[1].ToDocument(), numeric);
            Setup(arena, driver, ActionTrackDefinition.FollowUpId); driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); running = arena.Player.currentCast; Tick(driver, 11);
            Assert.That(arena.TryApplyActionTracks(new[] { editedEcho }, false, out error), Is.True, error); running.Cancel(); driver.Step(.02f, new CombatInput(0, CombatButtons.Swap)); Tick(driver, 35);
            Assert.That(arena.enemies[0].health, Is.EqualTo(950)); Assert.That(arena.enemies[0].ReceivedHitCount, Is.EqualTo(2), "An old independent tail keeps its activation weapon and numeric definition.");
            Setup(arena, driver, ActionTrackDefinition.FollowUpId); driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); Tick(driver, 11); Assert.That(arena.enemies[0].health, Is.EqualTo(991));
            Tick(driver, 30); Assert.That(arena.enemies[0].health, Is.EqualTo(974)); Assert.That(arena.enemies[0].ReceivedHitCount, Is.EqualTo(2));
            var policy = original[0].ToData(); policy.cooldown += 1; float now = arena.Now;
            Assert.That(arena.TryApplyActionTracks(new[] { new ActionTrackDefinition(policy) }, true, out error), Is.False); Assert.That(arena.Now, Is.EqualTo(now));
            Assert.That(arena.TryApplyActionTracks(original, false, out error), Is.True, error);
        }
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        static string Observe(CombatArena arena, CombatCast cast)
        {
            var p = arena.Player; var target = arena.enemies[0];
            return string.Join("/", Bits(arena.Now), Bits(p.Position.x), Bits(p.Position.y), Bits(p.velocity.x), Bits(p.velocity.y), Bits(target.health), target.ReceivedHitCount,
                p.weapons[0].Cooldown.Available, Bits(p.weapons[0].Cooldown.NextReady), cast ? Bits(cast.Age) : 0, cast ? Bits(cast.ImpactTime) : 0,
                cast ? Bits(cast.MotionDuration) : 0, cast ? Bits(cast.EndTime) : 0, cast && cast.ResourcesActive, cast && cast.Moving, cast && cast.FreezeVertical,
                p.currentCast == cast, arena.casts.Count, p.selected);
        }
        static List<string> Trace(CombatArena arena, CombatArena.ManualSimulation driver, ActionTrackDefinition tracks, bool compiled, int facing, bool air, int cancel)
        {
            Setup(arena, driver, tracks.Id, 2, facing, air); var source = tracks.ToData();
            if (!compiled) source.actionTracks = null; // Explicit retained Native fixture, never a product fallback.
            var cast = CombatCast.Create(arena, arena.Player, source, arena.Player.Weapon, false, .015f,
                compiled ? tracks.ContentHash : null, actionTracks: compiled ? tracks : null); arena.Player.currentCast = cast;
            var result = new List<string> { Observe(arena, cast) };
            for (int tick = 0; tick < 70; tick++) {
                driver.Step(.02f, new CombatInput(tick < 15 ? facing : 0, tick == cancel ? CombatButtons.Dash : tick == 35 && cancel >= 0 ? CombatButtons.Swap : CombatButtons.None));
                result.Add(Observe(arena, cast));
            }
            return result;
        }
        static void TraceParity(CombatArena arena, CombatArena.ManualSimulation driver)
        {
            foreach (var tracks in Pair(arena)) foreach (var settings in new[] { (facing:1,air:false,cancel:-1), (facing:-1,air:true,cancel:3), (facing:1,air:false,cancel:15) }) {
                var native = Trace(arena, driver, tracks, false, settings.facing, settings.air, settings.cancel);
                var compiled = Trace(arena, driver, tracks, true, settings.facing, settings.air, settings.cancel);
                Assert.That(compiled, Is.EqualTo(native), tracks.Id + "/" + settings);
            }
        }
    }
}
