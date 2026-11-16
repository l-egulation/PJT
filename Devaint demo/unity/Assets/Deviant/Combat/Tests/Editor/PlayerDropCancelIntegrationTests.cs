using System;
using System.Collections;
using System.Reflection;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using Deviant.Integration;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public sealed class PlayerDropCancelIntegrationTests
    {
        [UnityTearDown] public IEnumerator Cleanup() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [UnityTest] public IEnumerator RealBasicAttacksCancelOnLegalDropsAndSurviveRejectedDrops()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Drop cancel arena").AddComponent<CombatArena>();
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>();
            var content = CombatContentLoader.Load(null, false);
            content.Profile = CombatCompositionProfile.Game; content.Loadout = Deviant.Equipment.WeaponResources.CreateLoadout();
            content.Camera = new GameObject("Drop cancel camera").AddComponent<Camera>();
            content.PlayerSpawn = new Vector2(0, 1000); content.PlayerSize = new Vector2(.9375f, 2); content.PlayerOffset = new Vector2(.1875f, 1);
            content.Enemies = Array.Empty<CombatEnemySpawn>();
            Assert.That(arena.Initialize(content), Is.True);
            var platformRoot = new GameObject("Drop cancel platform"); platformRoot.transform.position = new Vector2(0, 999.95f);
            var surface = platformRoot.AddComponent<ArenaSurface>(); surface.oneWay = true;
            var platform = platformRoot.AddComponent<BoxCollider2D>(); platform.size = new Vector2(200, .1f);
            var player = arena.Player;
            var arbiter = (PlayerActionArbiter)typeof(CombatPlayer).GetField("actionArbiter", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player);
            using (var sim = arena.AcquireManualSimulation())
            {
                foreach (var form in arena.catalog.forms)
                foreach (float phase in new[] { 0f, .5f, .85f })
                {
                    Prepare(form.id); var cast = BeginBasic();
                    Step((cast.EndTime - arena.Now) * phase);
                    Assert.That(player.currentCast, Is.SameAs(cast), form.id + " / " + phase);
                    sim.Step(.0001f, new CombatInput(pressed: CombatButtons.Weapon1Basic));
                    Assert.That(arbiter.Reserved, Is.EqualTo(ReservedPlayerAction.Basic));
                    int jumps = player.jumps.Used, shots = arena.playerShots.Count, bombs = arena.bombs.Count;
                    float y = player.Position.y;
                    sim.Step(.0001f, new CombatInput(pressed: CombatButtons.Drop));
                    Assert.That(player.currentCast, Is.Null, form.id + " / " + phase);
                    Assert.That(cast.PresentationCancelled, Is.True);
                    Assert.That(arbiter.Reserved, Is.EqualTo(ReservedPlayerAction.None));
                    Assert.That(player.Position.y, Is.LessThan(y)); Assert.That(player.velocity.y, Is.LessThan(0));
                    Assert.That(player.jumps.Used, Is.EqualTo(jumps));
                    Assert.That(arena.playerShots.Count, Is.EqualTo(shots), "launched shots survive cancellation");
                    Assert.That(arena.bombs.Count, Is.EqualTo(bombs), "released bombs survive cancellation");
                    sim.Step(.01f); Assert.That(player.currentCast, Is.Null, "queued basic must not restart");
                }
                foreach (string rejected in new[] { "solid", "blocked-platform", "air" })
                {
                    Prepare("base_sword");
                    if (rejected == "solid") surface.oneWay = false;
                    if (rejected == "blocked-platform") surface.blockDropThrough = true;
                    if (rejected == "air") platform.enabled = false;
                    Physics2D.SyncTransforms();
                    var cast = BeginBasic();
                    sim.Step(.0001f, new CombatInput(pressed: CombatButtons.Weapon1Basic));
                    int jumps = player.jumps.Used;
                    sim.Step(.0001f, new CombatInput(pressed: CombatButtons.Drop));
                    Assert.That(player.currentCast, Is.SameAs(cast), rejected);
                    Assert.That(cast.PresentationCancelled, Is.False, rejected);
                    Assert.That(arbiter.Reserved, Is.EqualTo(ReservedPlayerAction.Basic), rejected);
                    Assert.That(player.jumps.Used, Is.EqualTo(jumps));
                }
                Assert.That(arena.SimulationFaulted, Is.False);

                void Prepare(string form)
                {
                    surface.oneWay = true; surface.blockDropThrough = false; platform.enabled = true;
                    sim.Reset(); player.spawn = content.PlayerSpawn; player.ResetPlayer(); player.SyncFacing(1);
                    Assert.That(player.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == form), 0, out var error), Is.True, error);
                    Physics2D.SyncTransforms(); sim.Step(.001f);
                }
                CombatCast BeginBasic()
                {
                    sim.Step(.0001f, new CombatInput(pressed: CombatButtons.Weapon1Basic));
                    Assert.That(player.currentCast, Is.Not.Null); Assert.That(player.currentCast.isBasic, Is.True);
                    return player.currentCast;
                }
                void Step(float seconds)
                { while (seconds > .000001f) { float dt = Mathf.Min(seconds, 1f / 120); sim.Step(dt); seconds -= dt; } }
            }
        }
    }
}
