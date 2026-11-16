using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat.Contracts;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class CombatHitIntegrationTests
    {
        [UnityTest] public IEnumerator GameAcceptedFactsRetainRealSourcesAndFlashOnZeroDamage() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", true);
        [UnityTest] public IEnumerator LabAcceptedFactsRetainRealSourcesAndRejectOldSessions() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", false);
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static List<CombatAcceptedHit> Read(CombatArena arena, ref CombatOutcomeCursor cursor)
        {
            var result = new List<CombatAcceptedHit>(); var buffer = new CombatAcceptedHit[128];
            do
            {
                Assert.That(arena.HitEvents.TryRead(cursor, buffer, out var page), Is.True); Assert.That(page.DroppedBefore, Is.Zero); cursor = page.Next;
                for (int i = 0; i < page.Count; i++) result.Add(buffer[i]);
                if (page.Count < buffer.Length) return result;
            } while (true);
        }
        static void Prepare(CombatArena arena, CombatArena.ManualSimulation driver, string form, int special, out CombatOutcomeCursor cursor)
        {
            driver.Reset(); var player = arena.Player; player.selected = 0;
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(40, 1000); }
            arena.enemies[0].maxHealth = arena.enemies[0].health = 100;
            Assert.That(player.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == form), special, out var error), Is.True, error);
            foreach (var weapon in player.weapons) weapon.StatSlots.Clear();
            player.Position = new Vector2(0, 1000); player.velocity = Vector2.zero; player.SyncFacing(1); Physics2D.SyncTransforms();
            Assert.That(arena.HitEvents.TryGetCursor(out cursor), Is.True);
        }
        static IEnumerator Exercise(string scene, bool game)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player; var target = arena.enemies[0];
            var floor = new GameObject("AcceptedHitFloor"); floor.transform.position = new Vector3(0, 999.5f, 0); floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(120, 1);
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    Prepare(arena, driver, "base_sword", 0, out var cursor); target.Position = new Vector2(2.3f, 1000); Physics2D.SyncTransforms();
                    long commandSequence = player.NextInputSequence; var session = player.InputSession;
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Basic)); var cast = player.currentCast; Guid execution = cast.ExecutionId;
                    Assert.That(Read(arena, ref cursor), Is.Empty);
                    var hits = new List<CombatAcceptedHit>();
                    for (int i = 0; i < 35; i++) { driver.Step(.02f); hits.AddRange(Read(arena, ref cursor)); }
                    var melee = hits.Single(hit => hit.Target.ActorId == target.ActorId);
                    Assert.That(melee.Origin.ExecutionId, Is.EqualTo(execution)); Assert.That(melee.Origin.Command.Sequence, Is.EqualTo(commandSequence));
                    Assert.That(melee.Origin.Command.Actor.Session.Equals(session), Is.True); Assert.That(melee.Origin.Source.ActorId, Is.EqualTo(player.ActorId));
                    Assert.That(melee.Origin.HitIndex, Is.Zero); Assert.That(melee.Before.Health - melee.After.Health, Is.EqualTo(10)); Assert.That(melee.Damage, Is.EqualTo(10));
                    Assert.That(melee.After.ReceivedHits, Is.EqualTo(melee.Before.ReceivedHits + 1));

                    // Accepted zero/absorbed hits have facts; invalid/parried/dead
                    // receives do not. A raw health write cannot create a fact.
                    Prepare(arena, driver, "base_sword", 0, out cursor); target.shield = 10; target.guardUntil = 10;
                    var forestView = game ? target.GetComponent("ForestEnemyPresentation") : null;
                    if (game) { Assert.That(forestView, Is.Not.Null); forestView.SendMessage("BindHitArena"); }
                    Assert.That(target.Receive(0, player, 1, false, null), Is.True);
                    Assert.That(target.Receive(4, player, 1, false, null), Is.True);
                    hits = Read(arena, ref cursor); Assert.That(hits.Count, Is.EqualTo(2)); Assert.That(hits.All(hit => hit.Damage == 0), Is.True);
                    Assert.That(hits[1].Absorbed, Is.EqualTo(4)); Assert.That(hits[0].Origin.Kind, Is.EqualTo(CombatHitKind.Direct));
                    if (game)
                    {
                        forestView.SendMessage("LateUpdate"); Assert.That(target.visual.color, Is.EqualTo(Color.red), "Zero/absorbed accepted facts flash even without a health delta.");
                    }
                    target.health -= 1; Assert.That(Read(arena, ref cursor), Is.Empty);
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Dash)); Read(arena, ref cursor);
                    Assert.That(player.Receive(4, target, -1, false, null), Is.False); Assert.That(Read(arena, ref cursor), Is.Empty);
                    target.health = 0; Assert.That(target.Receive(0, player, 1, false, null), Is.False); Assert.That(Read(arena, ref cursor), Is.Empty);

                    // Facts commit before existing log callbacks and retain old
                    // session identity even if those callbacks reset the arena.
                    Prepare(arena, driver, "base_sword", 0, out cursor); var old = cursor; bool reset = false;
                    Application.LogCallback resetAtDamage = (message, stack, type) => { if (!reset && message.Contains(target.name + " damage=")) { reset = true; arena.ResetTest(); } };
                    Application.logMessageReceived += resetAtDamage;
                    try { Assert.That(target.Receive(2, player, 1, false, null), Is.True); }
                    finally { Application.logMessageReceived -= resetAtDamage; }
                    Assert.That(reset, Is.True); Assert.That(arena.HitEvents.TryRead(old, new CombatAcceptedHit[4], out _), Is.False);
                    Assert.That(arena.ReadModel.TryGetSession(out var fresh), Is.True); cursor = new CombatOutcomeCursor(fresh, old.AfterEventSequence);
                    var committed = Read(arena, ref cursor).Single(); Assert.That(committed.Target.Session.Equals(old.Session), Is.True); Assert.That(committed.After.Health, Is.EqualTo(98));
                    var staleOrigin = committed.Origin; Assert.That(target.Receive(1, player, 1, false, null, origin: staleOrigin), Is.True);
                    Assert.That(Read(arena, ref cursor), Is.Empty, "A stale origin must not rebind as a current-session fact.");

                    // Real action-born projectile, bomb and laser receipts retain
                    // their emission command after cancellation/weapon switching.
                    foreach (var configuration in new[] { (form: "shuriken", special: 0, button: CombatButtons.Basic), (form: "bomb", special: 0, button: CombatButtons.Special), (form: "launcher", special: 1, button: CombatButtons.Basic) })
                    {
                        Prepare(arena, driver, configuration.form, configuration.special, out cursor); target.Position = new Vector2(2.3f, 1000); Physics2D.SyncTransforms();
                        commandSequence = player.NextInputSequence; driver.Step(.02f, new CombatInput(0, configuration.button)); cast = player.currentCast; execution = cast.ExecutionId;
                        hits.Clear();
                        for (int i = 0; i < 100 && hits.Count == 0; i++) { driver.Step(.02f); hits.AddRange(Read(arena, ref cursor)); }
                        Assert.That(hits, Is.Not.Empty, configuration.form); var hit = hits.First(h => h.Target.ActorId == target.ActorId);
                        Assert.That(hit.Origin.ExecutionId, Is.EqualTo(execution)); Assert.That(hit.Origin.Command.Sequence, Is.EqualTo(commandSequence)); Assert.That(hit.Origin.EffectId, Is.Not.EqualTo(Guid.Empty));
                        Assert.That(hit.Origin.Kind, Is.EqualTo(configuration.form == "launcher" ? CombatHitKind.Laser : configuration.form == "bomb" ? CombatHitKind.Blast : CombatHitKind.Projectile));
                    }

                    Prepare(arena, driver, "shuriken", 0, out cursor); target.Position = new Vector2(5, 1000); Physics2D.SyncTransforms();
                    commandSequence = player.NextInputSequence; driver.Step(.02f, new CombatInput(0, CombatButtons.Basic)); execution = player.currentCast.ExecutionId;
                    for (int i = 0; i < 40 && arena.playerShots.Count == 0; i++) driver.Step(.02f);
                    Assert.That(arena.playerShots, Is.Not.Empty); var retainedShot = arena.playerShots[0];
                    player.CancelAction(); player.selected = 1; Read(arena, ref cursor);
                    retainedShot.SweepTo(new Vector2(7, retainedShot.Position.y)); var independent = Read(arena, ref cursor).First(hit => hit.Target.ActorId == target.ActorId);
                    Assert.That(independent.Origin.ExecutionId, Is.EqualTo(execution)); Assert.That(independent.Origin.Command.Sequence, Is.EqualTo(commandSequence));

                    Prepare(arena, driver, "launcher", 3, out cursor); target.Position = new Vector2(4, 1000); Physics2D.SyncTransforms();
                    var grenadeData = player.Form.specials[3].bomb;
                    var grenade = BombProjectile.Spawn(arena, player, target.Center, Vector2.zero, grenadeData, 12);
                    grenade.SweepTo(grenade.Position); var fragments = Read(arena, ref cursor);
                    Assert.That(fragments.Count(hit => hit.Origin.Kind == CombatHitKind.Fragment), Is.EqualTo(4));
                    Assert.That(fragments.Select(hit => hit.Origin.ExecutionId).Distinct().Count(), Is.EqualTo(1));
                    Assert.That(fragments.Select(hit => hit.Origin.EffectId).Distinct().Count(), Is.EqualTo(5));

                    Prepare(arena, driver, "base_sword", 3, out cursor); target.Position = new Vector2(2.3f, 1000); Physics2D.SyncTransforms();
                    commandSequence = player.NextInputSequence; driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); cast = player.currentCast; execution = cast.ExecutionId;
                    for (int i = 0; i < 60 && cast.ResourceResult == null; i++) driver.Step(.02f);
                    player.CancelAction(); player.selected = 1; Read(arena, ref cursor);
                    hits.Clear(); for (int i = 0; i < 60; i++) { driver.Step(.02f); hits.AddRange(Read(arena, ref cursor)); }
                    var tail = hits.First(hit => hit.Origin.Kind == CombatHitKind.Independent);
                    Assert.That(tail.Origin.Command.Sequence, Is.EqualTo(commandSequence)); Assert.That(tail.Origin.ExecutionId, Is.EqualTo(execution));
                    Assert.That(tail.Origin.HitId, Is.EqualTo("echo")); Assert.That(tail.Origin.EffectId, Is.Not.EqualTo(Guid.Empty));

                    Prepare(arena, driver, "bomb", 2, out cursor); target.Position = new Vector2(2.5f, 1000); Physics2D.SyncTransforms();
                    commandSequence = player.NextInputSequence; driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); execution = player.currentCast.ExecutionId;
                    hits.Clear(); for (int i = 0; i < 90 && hits.Count == 0; i++) { driver.Step(.02f); hits.AddRange(Read(arena, ref cursor)); }
                    var missileHit = hits.First(hit => hit.Target.ActorId == target.ActorId); Assert.That(missileHit.Origin.Kind, Is.EqualTo(CombatHitKind.Missile));
                    Assert.That(missileHit.Origin.Command.Sequence, Is.EqualTo(commandSequence)); Assert.That(missileHit.Origin.ExecutionId, Is.EqualTo(execution));

                    foreach (int profile in new[] { 0, 1, 2 })
                    {
                        Prepare(arena, driver, "base_sword", 0, out cursor); target.profile = profile; target.Position = new Vector2(2.2f, 1000); target.SyncFacing(-1); Physics2D.SyncTransforms();
                        for (int i = 0; i < 30; i++) driver.Step(.02f); target.aggressive = true;
                        hits.Clear(); for (int i = 0; i < 120 && hits.Count == 0; i++) { driver.Step(.02f); hits.AddRange(Read(arena, ref cursor)); }
                        var enemyHit = hits.First(hit => hit.Target.ActorId == player.ActorId);
                        Assert.That(enemyHit.Origin.Command.Kind, Is.EqualTo(CombatCommandKind.EnemyAttack)); Assert.That(enemyHit.Origin.Command.Actor.ActorId, Is.EqualTo(target.ActorId));
                        Assert.That(enemyHit.Origin.ExecutionId, Is.Not.EqualTo(Guid.Empty)); Assert.That(enemyHit.Origin.HitIndex, Is.GreaterThanOrEqualTo(0));
                    }
                    target.profile = 0;

                    // Reflection keeps original enemy emission and the shield's
                    // independently captured reflector command/execution.
                    Prepare(arena, driver, "shield", 0, out cursor); target.Position = new Vector2(3, 1000); target.health = 100; Physics2D.SyncTransforms();
                    commandSequence = player.NextInputSequence; driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); cast = player.currentCast; execution = cast.ExecutionId;
                    arena.FireEnemyProjectile(target, player.Center + Vector2.right, Vector2.left * 8, 8); var projectile = arena.projectiles.Last();
                    hits.Clear(); for (int i = 0; i < 60 && hits.Count == 0; i++) { driver.Step(.02f); hits.AddRange(Read(arena, ref cursor)); }
                    Assert.That(hits, Is.Not.Empty); var reflected = hits.First(hit => hit.Origin.Reflector.ActorId > 0);
                    Assert.That(reflected.Target.ActorId, Is.EqualTo(target.ActorId)); Assert.That(reflected.Origin.Source.ActorId, Is.EqualTo(target.ActorId));
                    Assert.That(reflected.Origin.Reflector.ActorId, Is.EqualTo(player.ActorId)); Assert.That(reflected.Origin.ReflectorExecutionId, Is.EqualTo(execution));
                    Assert.That(reflected.Origin.ReflectorCommand.Sequence, Is.EqualTo(commandSequence)); Assert.That(reflected.Origin.EffectId, Is.Not.EqualTo(Guid.Empty));
                    Assert.That(hits.Any(hit => hit.Target.ActorId == player.ActorId), Is.False, "Parried projectile contact is not accepted damage on the defender.");
                }
            }
            finally { Object.DestroyImmediate(floor); }
        }
    }
}
