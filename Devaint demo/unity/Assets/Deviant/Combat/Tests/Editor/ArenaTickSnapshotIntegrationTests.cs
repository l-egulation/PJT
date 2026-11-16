using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public sealed class ArenaTickSnapshotProbe : TestEnemy
    {
        public Action OnCancel;
        public override void CancelAttack() { OnCancel?.Invoke(); base.CancelAttack(); }
    }

    // Uses the existing arena/actor API so this exact fixture can also run with
    // the old seven ToArray phases. No direct private tick-world execution.
    public class ArenaTickSnapshotIntegrationTests
    {
        const float Dt = .125f;
        static readonly FieldInfo Simulation = typeof(CombatArena).GetField("simulation", BindingFlags.Instance | BindingFlags.NonPublic);
        [Serializable] sealed class Row { public string scenario; public int[] order, state; }
        [Serializable] sealed class Trace { public List<Row> rows = new List<Row>(); }
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        [UnityTest] public IEnumerator GameKeepsSnapshotMutationFailureAndReentrySemantics()
            => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "game");
        [UnityTest] public IEnumerator LabKeepsSnapshotMutationFailureAndReentrySemantics()
            => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "lab");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        static T Track<T>(T component, List<GameObject> roots) where T : Component
        { roots.Add(component.gameObject); return component; }
        static void Record(Trace trace, string scenario, CombatArena arena, ArenaTickSnapshotProbe target, List<int> order = null, params int[] extra)
        {
            var state = new List<int> {
                Bits(arena.Now), (int)arena.SimulationTickCount, arena.SimulationFaulted ? 1 : 0,
                Bits(arena.Player.Position.x), Bits(arena.Player.Position.y), Bits(target.Position.x), Bits(target.Position.y),
                Bits(target.health), target.ReceivedHitCount, Bits(target.knockUntil), Bits(target.knockSpeed),
                arena.casts.Count, arena.projectiles.Count, arena.bombs.Count, arena.playerShots.Count, arena.lasers.Count,
                arena.bombBursts.Count, arena.decoys.Count
            };
            state.AddRange(extra); trace.rows.Add(new Row { scenario = scenario, order = order?.ToArray() ?? Array.Empty<int>(), state = state.ToArray() });
        }
        static void Setup(CombatArena arena, CombatArena.ManualSimulation driver, ArenaTickSnapshotProbe target)
        {
            target.OnCancel = null; driver.Reset();
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(60, 1000); enemy.velocity = Vector2.zero; }
            var player = arena.Player; player.selected = 0; player.InputBlocked = false; player.UseTestInput = true;
            player.Position = new Vector2(0, 1000); player.velocity = Vector2.zero; player.grounded = true; player.SyncFacing(1);
            foreach (var weapon in player.weapons) weapon.StatSlots.Clear();
            target.isPlayer = target.isBoss = target.isDecoy = false;
            target.ResetActor(new Vector2(1.25f, 1000)); target.SyncFacing(-1); Physics2D.SyncTransforms();
        }
        static CombatCast Cast(CombatArena arena, List<GameObject> roots, string id, float damage, bool push = false)
        {
            var source = new SkillData { id = id, vertical = "normal", activation = 0, end = 2,
                invulStart = Array.Empty<float>(), invulEnd = Array.Empty<float>(),
                hits = new[] { new HitData { time = 0, active = 1, damage = damage, anchor = "actor", targets = "all",
                    pushWithCaster = push, box = new BoxData { x = 1.5f, y = 1, width = 4, height = 2 } } },
                movement = push ? new MoveData { type = "dash", start = 0, duration = 1, speed = 7 } : null };
            var cast = Track(CombatCast.Create(arena, arena.Player, source, arena.Player.Weapon, true), roots);
            cast.BeforeMovement(Dt); return cast;
        }
        static ArenaProjectile Projectile(CombatArena arena, List<GameObject> roots, CombatActor source, Vector2 at, Vector2 velocity, float damage = 1)
        {
            var shot = Track(new GameObject("TickSnapshotEnemyShot").AddComponent<ArenaProjectile>(), roots);
            shot.Initialize(arena, source, at, velocity, damage, arena.Now + 5); arena.projectiles.Add(shot); return shot;
        }
        static BombProjectile Bomb(CombatArena arena, List<GameObject> roots, Vector2 at)
            => Track(BombProjectile.Spawn(arena, arena.Player, at, Vector2.right * 2,
                new BombData { radius = .1f, blastRadius = .2f, lifetime = 5 }, 1), roots);
        static PlayerProjectile PlayerShot(CombatArena arena, List<GameObject> roots, Vector2 at)
            => Track(PlayerProjectile.Spawn(arena, arena.Player, at, Vector2.right * 2,
                new RangedData { radius = .1f, distance = 10 }, 1), roots);
        static LaserVisual Laser(CombatArena arena, List<GameObject> roots)
        {
            var laser = Track(new GameObject("TickSnapshotLaser").AddComponent<LaserVisual>(), roots);
            laser.arena = arena; laser.until = arena.Now; arena.lasers.Add(laser); return laser;
        }
        static BombBurst Burst(CombatArena arena, List<GameObject> roots)
        {
            var burst = Track(new GameObject("TickSnapshotBurst").AddComponent<BombBurst>(), roots);
            burst.arena = arena; burst.started = arena.Now - 1; arena.bombBursts.Add(burst); return burst;
        }
        static void AppendNullAndDestroyed<T>(List<T> list) where T : Component
        {
            var root = new GameObject("TickSnapshotAlreadyDestroyed"); var destroyed = root.AddComponent<T>();
            Object.DestroyImmediate(root); list.Add(null); list.Add(destroyed);
        }
        static void AssertOnlyNullAndDestroyed<T>(List<T> list) where T : Component
        { Assert.That(list.Count, Is.EqualTo(2)); foreach (var entry in list) Assert.That((bool)entry, Is.False); }

        static void ActionMutation(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver, ArenaTickSnapshotProbe target, List<GameObject> roots)
        {
            Setup(arena, driver, target);
            var push = Cast(arena, roots, "snapshot-prepared-push", 0, true);
            push.Step(Dt); arena.casts.Remove(push); target.ResetActor(new Vector2(1.25f, 1000));
            var first = Cast(arena, roots, "snapshot-first", 1);
            var removed = Cast(arena, roots, "snapshot-removed", 2);
            var destroyed = Cast(arena, roots, "snapshot-destroyed", 3);
            AppendNullAndDestroyed(arena.casts);
            var order = new List<int>(); float before = target.health; CombatCast added = null;
            target.OnCancel = () => {
                order.Add((int)(before - target.health)); before = target.health;
                if (order.Count != 1) return;
                arena.casts.Remove(removed); arena.casts[1] = push;
                Object.DestroyImmediate(destroyed.gameObject);
                added = Cast(arena, roots, "snapshot-added", 4);
            };
            driver.Step(Dt);
            Assert.That(order, Is.EqualTo(new[] { 1, 2 }), "Removed live entries still run; replacements/additions wait; destroyed entries do not run.");
            Assert.That(target.health, Is.EqualTo(97)); Assert.That(target.ReceivedHitCount, Is.EqualTo(2));
            Assert.That(target.knockUntil, Is.EqualTo(Dt)); Assert.That(target.knockSpeed, Is.EqualTo(7),
                "ActionPush must take its own fresh list, including a newly installed already-prepared cast.");
            Assert.That(arena.casts.Contains(first), Is.True); Assert.That(arena.casts.Contains(removed), Is.False);
            Assert.That(arena.casts.Contains(push), Is.True); Assert.That(arena.casts.Contains(added), Is.True);
            Record(trace, "actions-entry-and-independent-push", arena, target, order);
            driver.Step(Dt);
            Assert.That(order, Is.EqualTo(new[] { 1, 2, 4 })); Assert.That(target.health, Is.EqualTo(93));
            Record(trace, "actions-next-entry", arena, target, order); target.OnCancel = null;
        }

        static void ProjectileMutation(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver, ArenaTickSnapshotProbe target, List<GameObject> roots)
        {
            Setup(arena, driver, target); arena.Player.Position = new Vector2(-50, 1000);
            target.isDecoy = true; target.health = 1;
            var source = arena.enemies[0]; var first = Projectile(arena, roots, source, target.Center, Vector2.right * 2);
            var removed = Projectile(arena, roots, source, new Vector2(30, 1004), Vector2.right * 2);
            var destroyed = Projectile(arena, roots, source, new Vector2(40, 1004), Vector2.right * 2);
            AppendNullAndDestroyed(arena.projectiles);
            ArenaProjectile added = null; BombProjectile bomb = null; PlayerProjectile shot = null; LaserVisual laser = null; BombBurst burst = null;
            int callbacks = 0;
            target.OnCancel = () => {
                callbacks++; arena.projectiles.Remove(removed); Object.DestroyImmediate(destroyed.gameObject);
                added = Projectile(arena, roots, source, new Vector2(50, 1004), Vector2.right * 2);
                bomb = Bomb(arena, roots, new Vector2(70, 1004)); shot = PlayerShot(arena, roots, new Vector2(80, 1004));
                laser = Laser(arena, roots); burst = Burst(arena, roots);
            };
            Physics2D.SyncTransforms(); driver.Step(Dt);
            Assert.That(callbacks, Is.EqualTo(1)); Assert.That(target.Dead, Is.True);
            Assert.That(removed.transform.position.x, Is.EqualTo(30 + 2 * Dt));
            Assert.That(added.transform.position.x, Is.EqualTo(50), "An appended projectile is not in the entered projectile snapshot.");
            Assert.That(arena.projectiles.Contains(first), Is.False); Assert.That(arena.projectiles.Contains(removed), Is.False);
            Assert.That(bomb.Position.x, Is.EqualTo(70 + 2 * Dt)); Assert.That(shot.Position.x, Is.EqualTo(80 + 2 * Dt),
                "Later phases capture additions made by an earlier phase in the same tick.");
            Assert.That(arena.lasers.Contains(laser), Is.False); Assert.That(arena.bombBursts.Contains(burst), Is.False);
            Record(trace, "projectile-mutation-and-later-phases", arena, target, null,
                Bits(removed.transform.position.x), Bits(added.transform.position.x), Bits(bomb.Position.x), Bits(shot.Position.x), callbacks);
            target.OnCancel = null; driver.Step(Dt);
            Assert.That(added.transform.position.x, Is.EqualTo(50 + 2 * Dt));
            Record(trace, "projectile-next-entry", arena, target, null, Bits(added.transform.position.x));
        }

        static void EveryPhaseFiltersDestroyedAndAllowsSelfRemoval(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver, ArenaTickSnapshotProbe target, List<GameObject> roots)
        {
            Setup(arena, driver, target);
            for (int i = 0; i < 2; i++)
            {
                Cast(arena, roots, "snapshot-missing-owner-" + i, 1).owner = null;
                Projectile(arena, roots, null, new Vector2(30, 1004), Vector2.right);
                Bomb(arena, roots, new Vector2(40, 1004)).owner = null;
                PlayerShot(arena, roots, new Vector2(50, 1004)).owner = null;
                Laser(arena, roots); Burst(arena, roots);
            }
            AppendNullAndDestroyed(arena.casts); AppendNullAndDestroyed(arena.projectiles); AppendNullAndDestroyed(arena.bombs);
            AppendNullAndDestroyed(arena.playerShots); AppendNullAndDestroyed(arena.lasers); AppendNullAndDestroyed(arena.bombBursts);
            CombatActor Decoy(string name, float expires, bool dead = false)
            {
                var decoy = Track(new GameObject(name).AddComponent<CombatActor>(), roots);
                decoy.arena = arena; decoy.isDecoy = true; decoy.expiredAt = expires; if (dead) decoy.health = 0;
                arena.decoys.Add(decoy); return decoy;
            }
            var live = Decoy("TickSnapshotLiveDecoy", 1);
            Decoy("TickSnapshotExpiredDecoy", 0); Decoy("TickSnapshotDeadDecoy", 1, true); AppendNullAndDestroyed(arena.decoys);
            driver.Step(Dt);
            AssertOnlyNullAndDestroyed(arena.casts); AssertOnlyNullAndDestroyed(arena.projectiles); AssertOnlyNullAndDestroyed(arena.bombs);
            AssertOnlyNullAndDestroyed(arena.playerShots); AssertOnlyNullAndDestroyed(arena.lasers); AssertOnlyNullAndDestroyed(arena.bombBursts);
            Assert.That(arena.decoys, Is.EqualTo(new[] { live }), "The existing reverse/live decoy expiration path stays separate from snapshots.");
            Record(trace, "every-phase-null-destroyed-self-removal", arena, target);
        }

        static void ExceptionBoundary(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver, ArenaTickSnapshotProbe target, List<GameObject> roots)
        {
            Setup(arena, driver, target); Cast(arena, roots, "snapshot-throws", 1); Cast(arena, roots, "snapshot-unvisited", 2);
            using var inputCapture = arena.Player.BeginInputCapture(8);
            var failure = new InvalidOperationException("Arena snapshot Receive boundary"); int callbacks = 0;
            target.OnCancel = () => { callbacks++; arena.casts.Clear(); throw failure; };
            Assert.That(Assert.Throws<InvalidOperationException>(() => driver.Step(Dt)), Is.SameAs(failure));
            Assert.That(callbacks, Is.EqualTo(1)); Assert.That(target.health, Is.EqualTo(99)); Assert.That(target.ReceivedHitCount, Is.EqualTo(1));
            Assert.That(arena.Now, Is.Zero); Assert.That(arena.SimulationTickCount, Is.Zero); Assert.That(arena.SimulationFaulted, Is.True);
            Assert.That(inputCapture.TryRead(out var failedInput), Is.True);
            Assert.That(failedInput.Kind, Is.EqualTo(CombatInputBatchKind.Consumption));
            Assert.That(failedInput.TickOutcome, Is.EqualTo(CombatInputTickOutcome.Faulted));
            Assert.That(failedInput.TickCount, Is.Zero); Assert.That(failedInput.Now, Is.Zero); Assert.That(failedInput.ObservedThroughNow, Is.Zero);
            Assert.That(failedInput.Receipts[0].Disposition, Is.EqualTo(CombatInputDisposition.Consumed), "The input boundary completed before a later action phase threw.");
            Assert.Throws<InvalidOperationException>(() => driver.Step(Dt)); Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(inputCapture.TryRead(out _), Is.False, "A rejected retry does not publish another tick or input receipt.");
            Record(trace, "callback-failure-retains-only-entered-work", arena, target, null, callbacks);
            target.OnCancel = null; driver.Reset(); target.Position = new Vector2(1.25f, 1000);
            arena.Player.Position = new Vector2(0, 1000); Physics2D.SyncTransforms(); Cast(arena, roots, "snapshot-after-reset", 8);
            driver.Step(Dt);
            Assert.That(arena.SimulationFaulted, Is.False); Assert.That(target.health, Is.EqualTo(92)); Assert.That(target.ReceivedHitCount, Is.EqualTo(1));
            Assert.That(arena.SimulationTickCount, Is.EqualTo(1)); Record(trace, "callback-failure-successful-reset", arena, target);
            Assert.That(inputCapture.TryRead(out var completedInput), Is.True);
            Assert.That(completedInput.TickOutcome, Is.EqualTo(CombatInputTickOutcome.Completed));
            Assert.That(completedInput.TickCount, Is.Zero); Assert.That(completedInput.ObservedThroughNow, Is.EqualTo(Dt));
        }

        static void ExistingReentryGuards(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver, ArenaTickSnapshotProbe target, List<GameObject> roots)
        {
            Setup(arena, driver, target); Cast(arena, roots, "snapshot-reentry-first", 1); Cast(arena, roots, "snapshot-reentry-second", 2);
            var simulation = (CombatSimulation)Simulation.GetValue(arena); int callbacks = 0, rejected = 0;
            target.OnCancel = () => {
                if (++callbacks != 1) return;
                Assert.Throws<InvalidOperationException>(() => driver.Step(Dt)); rejected++;
                Assert.Throws<InvalidOperationException>(() => driver.Reset()); rejected++;
                Assert.Throws<InvalidOperationException>(() => arena.ResetTest()); rejected++;
                Assert.Throws<InvalidOperationException>(() => simulation.Step(Dt, new CombatInput(1, CombatButtons.Basic))); rejected++;
                Assert.Throws<InvalidOperationException>(() => simulation.Reset(SimulationResetMode.NewEpoch)); rejected++;
                Assert.That(arena.Now, Is.Zero); Assert.That(arena.SimulationTickCount, Is.Zero);
                Assert.That(arena.casts.Count, Is.EqualTo(2)); Assert.That(arena.Player.axis, Is.Zero);
            };
            driver.Step(Dt);
            Assert.That(rejected, Is.EqualTo(5)); Assert.That(callbacks, Is.EqualTo(2)); Assert.That(target.health, Is.EqualTo(97));
            Assert.That(arena.SimulationFaulted, Is.False); Assert.That(arena.SimulationTickCount, Is.EqualTo(1));
            Record(trace, "supported-reentry-rejected-before-recapture", arena, target, null, callbacks, rejected); target.OnCancel = null;
        }

        static IEnumerator Exercise(string scene, string label)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); Assert.That(Application.isPlaying, Is.True); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var roots = new List<GameObject>(); var trace = new Trace();
            var floor = Track(new GameObject("ArenaTickSnapshotFloor").AddComponent<BoxCollider2D>(), roots);
            floor.transform.position = new Vector3(0, 999.5f, 0); floor.size = new Vector2(200, 1); floor.gameObject.AddComponent<ArenaSurface>();
            var target = Track(new GameObject("ArenaTickSnapshotTarget").AddComponent<ArenaTickSnapshotProbe>(), roots);
            target.Configure(arena, new Vector2(1.25f, 1000), new Vector2(.5f, 1.8f), new Vector2(0, .9f), false);
            target.spawn = new Vector2(1.25f, 1000); arena.enemies.Add(target);
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    ActionMutation(trace, arena, driver, target, roots);
                    ProjectileMutation(trace, arena, driver, target, roots);
                    EveryPhaseFiltersDestroyedAndAllowsSelfRemoval(trace, arena, driver, target, roots);
                    ExceptionBoundary(trace, arena, driver, target, roots);
                    ExistingReentryGuards(trace, arena, driver, target, roots);
                }
                var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-combatTickSnapshotTraceRoot");
                if (at >= 0 && at + 1 < args.Length)
                { Directory.CreateDirectory(args[at + 1]); File.WriteAllText(Path.Combine(args[at + 1], label + ".json"), JsonUtility.ToJson(trace)); }
            }
            finally
            {
                target.OnCancel = null; arena.enemies.Remove(target);
                for (int i = roots.Count - 1; i >= 0; i--) if (roots[i]) Object.DestroyImmediate(roots[i]);
            }
        }
    }
}
