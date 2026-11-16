using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Deviant.Combat.Contracts;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public sealed class TeleportNativeProbe : TestEnemy
    {
        public Action OnCancel;
        public override void CancelAttack() { OnCancel?.Invoke(); base.CancelAttack(); }
    }

    // Frozen against the old public Cast/Actor API before extracting teleport
    // state. Geometry, actual arrival hits and callbacks all run through Native.
    public class TeleportNativeTraceTests
    {
        [Serializable] sealed class Row
        {
            public string scenario, phase;
            public int tick, facing, targetFacing, hits, charges, casts, callbacks, status;
            public bool current, listed, moving, freeze, invulnerable, resources, targetAlive, faulted;
            public int[] bits;
        }
        [Serializable] sealed class Trace { public List<Row> rows = new List<Row>(); }
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        [UnityTest] public IEnumerator GameTeleportNativeTrace() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "game");
        [UnityTest] public IEnumerator LabTeleportNativeTrace() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "lab");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static void Record(Trace trace, string scenario, string phase, int tick, CombatArena arena, CombatCast cast, CombatActor target, int callbacks = 0)
        {
            var p = arena.Player; var result = cast.ResourceResult; var snapshot = result?.Snapshot;
            var bounds = p.bodyCollider.bounds;
            trace.rows.Add(new Row { scenario = scenario, phase = phase, tick = tick, facing = p.facing, targetFacing = target ? target.facing : 0,
                hits = target ? target.ReceivedHitCount : -1, charges = p.weapons[0].Cooldown.Available, casts = arena.casts.Count, callbacks = callbacks,
                status = result == null ? -1 : (int)result.Status, current = ReferenceEquals(p.currentCast, cast), listed = arena.casts.Contains(cast),
                moving = cast && cast.Moving, freeze = cast && cast.FreezeVertical, invulnerable = p.Invulnerable, resources = cast.ResourcesActive,
                targetAlive = target && !target.Dead, faulted = arena.SimulationFaulted,
                bits = new[] { Bits(arena.Now), Bits(p.Position.x), Bits(p.Position.y), Bits(p.Center.x), Bits(p.Center.y), Bits(p.velocity.x), Bits(p.velocity.y),
                    Bits(bounds.min.x), Bits(bounds.max.x), Bits(bounds.min.y), Bits(bounds.max.y), Bits(p.health), Bits(cast.Age), Bits(cast.ImpactTime), Bits(cast.EndTime),
                    Bits(target ? target.Position.x : 0), Bits(target ? target.Position.y : 0), Bits(target ? target.Center.x : 0), Bits(target ? target.Center.y : 0),
                    Bits(target ? target.health : 0), Bits(p.weapons[0].Cooldown.NextReady), Bits(p.weapons[0].Cooldown.Period),
                    snapshot == null ? 0 : Bits(snapshot.ActivatedAt), snapshot == null ? 0 : Bits(snapshot.DamageMultiplier) } });
        }
        static TeleportNativeProbe Setup(CombatArena arena, CombatArena.ManualSimulation driver, int special = 2, int facing = 1)
        {
            driver.Reset(); var p = arena.Player; p.selected = 0; p.InputBlocked = false;
            foreach (var weapon in p.weapons) weapon.StatSlots.Clear();
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(40, 1000); enemy.velocity = Vector2.zero; }
            Assert.That(p.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == "rapier"), special, out var error), Is.True, error);
            p.Position = new Vector2(0, 1000); p.velocity = Vector2.zero; p.grounded = true; p.SyncFacing(facing);
            var target = new GameObject("TeleportNativeTarget").AddComponent<TeleportNativeProbe>();
            target.Configure(arena, new Vector2(2 * facing, 1000), new Vector2(.9375f, 2), new Vector2(.125f, 1), false);
            target.maxHealth = 1000; target.health = 1000; target.SyncFacing(facing); target.spawn = target.Position;
            arena.enemies.Add(target); Physics2D.SyncTransforms(); return target;
        }
        static void Remove(CombatArena arena, TeleportNativeProbe target)
        {
            if (!ReferenceEquals(target, null)) arena.enemies.Remove(target);
            if (!target) return; target.OnCancel = null; target.gameObject.SetActive(false); Object.DestroyImmediate(target.gameObject); Physics2D.SyncTransforms();
        }
        static SkillData Source(CombatArena arena) => JsonUtility.FromJson<SkillData>(JsonUtility.ToJson(arena.Player.Special));
        static CombatCast Start(CombatArena arena, SkillData source = null)
        {
            var p = arena.Player; var cast = CombatCast.Create(arena, p, source ?? Source(arena), p.Weapon, false); p.currentCast = cast; return cast;
        }
        static GameObject Wall(float x, float bottom = 1000, float height = 5)
        {
            var wall = new GameObject("TeleportNativeWall"); wall.transform.position = new Vector3(x, bottom + height / 2, 0);
            wall.AddComponent<ArenaSurface>(); wall.AddComponent<BoxCollider2D>().size = new Vector2(.04f, height); Physics2D.SyncTransforms(); return wall;
        }
        static void RemoveWall(GameObject wall)
        { if (wall) { wall.SetActive(false); Object.DestroyImmediate(wall); Physics2D.SyncTransforms(); } }
        static void AssertFront(CombatPlayer p, CombatActor target)
        {
            Assert.That(p.facing, Is.EqualTo(-target.facing)); Assert.That(p.Position.y, Is.EqualTo(target.Position.y).Within(.0001f));
            float gap = target.facing == 1 ? p.bodyCollider.bounds.min.x - target.hurtCollider.bounds.max.x : target.hurtCollider.bounds.min.x - p.bodyCollider.bounds.max.x;
            Assert.That(gap, Is.EqualTo(.25f).Within(.002f));
        }
        static void CatalogCases(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver)
        {
            foreach (int special in new[] { 2, 3 })
            foreach (int facing in new[] { 1, -1 })
            foreach (int targetFacing in new[] { 1, -1 })
            foreach (string placement in new[] { "front", "behind", "above-behind", "below" })
            {
                var target = Setup(arena, driver, special, facing); var p = arena.Player;
                target.Position = new Vector2((placement == "front" ? 2 : -1.5f) * facing, placement == "above-behind" ? 1002 : 1000);
                if (placement == "below") p.Position = new Vector2(0, 1002);
                target.SyncFacing(targetFacing); Physics2D.SyncTransforms(); Assert.That(arena.NearestInCircle(p, p.Special.movement.targetRadius), Is.SameAs(target));
                var cast = Start(arena); string label = "catalog/" + special + "/" + facing + "/" + targetFacing + "/" + placement; int blinks = 0;
                Application.LogCallback observe = (message, stack, type) =>
                {
                    if (!message.Contains("rapier instant target teleport at=")) return;
                    blinks++; AssertFront(p, target); Assert.That(cast.Moving, Is.False); Record(trace, label, "arrival", -1, arena, cast, target, blinks);
                };
                Application.logMessageReceived += observe;
                try
                {
                    Record(trace, label, "start", -1, arena, cast, target);
                    for (int tick = 0; tick < 55; tick++) { driver.Step(.02f); Record(trace, label, "step", tick, arena, cast, target, blinks); }
                    Assert.That(blinks, Is.EqualTo(1)); Assert.That(target.ReceivedHitCount, Is.GreaterThan(0));
                    if (placement == "front" || placement == "behind")
                    { Assert.That(target.ReceivedHitCount, Is.EqualTo(special == 2 ? 1 : 5)); Assert.That(target.health, Is.EqualTo(special == 2 ? 965 : 950)); }
                    Assert.That(p.Weapon.Cooldown.Available, Is.Zero); Assert.That(arena.SimulationFaulted, Is.False);
                }
                finally { Application.logMessageReceived -= observe; Remove(arena, target); }
            }
        }
        static void Boundaries(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver)
        {
            foreach (string scenario in new[] { "exact-start", "coarse-start", "cancel-before", "cancel-after", "completed", "claim-hit-gate" })
            {
                var target = Setup(arena, driver); var p = arena.Player; var source = Source(arena);
                source.activation = source.movement.start = source.hits[0].time = .25f; source.end = 2;
                if (scenario == "completed" || scenario == "claim-hit-gate") source.activation = 0;
                if (scenario == "completed") { source.end = .125f; source.movement.start = .5f; }
                if (scenario == "claim-hit-gate")
                {
                    target.Position = new Vector2(.75f, 1000); source.hits[0].time = .125f; source.hits[0].active = .5f; Physics2D.SyncTransforms();
                }
                var cast = Start(arena, source); int blinks = 0;
                Application.LogCallback observe = (message, stack, type) => { if (message.Contains("rapier instant target teleport at=")) blinks++; };
                Application.logMessageReceived += observe;
                try
                {
                    Record(trace, scenario, "start", -1, arena, cast, target);
                    if (scenario == "cancel-before") { p.CancelAction(); cast.BeforeMovement(1); cast.Step(1); Assert.That(blinks, Is.Zero); Assert.That(p.Weapon.Cooldown.Available, Is.EqualTo(1)); }
                    else if (scenario == "completed")
                    {
                        cast.BeforeMovement(0); cast.Step(.125f); Assert.That(p.currentCast, Is.Null); cast.BeforeMovement(1); Assert.That(blinks, Is.Zero);
                    }
                    else if (scenario == "claim-hit-gate")
                    {
                        cast.BeforeMovement(0); cast.Step(.125f); Assert.That(target.ReceivedHitCount, Is.Zero, "An activated strike cannot hit until movement is claimed.");
                        Record(trace, scenario, "activated-unclaimed", 0, arena, cast, target); cast.BeforeMovement(.25f); cast.Step(.25f);
                        Assert.That(blinks, Is.EqualTo(1)); Assert.That(target.ReceivedHitCount, Is.EqualTo(1));
                    }
                    else
                    {
                        driver.Step(.125f); cast.BeforeMovement(.1240234375f); Assert.That(blinks, Is.Zero); Assert.That(p.Weapon.Cooldown.Available, Is.EqualTo(1));
                        Record(trace, scenario, "before-start", 0, arena, cast, target);
                        cast.BeforeMovement(scenario == "coarse-start" ? .5f : .125f); Assert.That(blinks, Is.EqualTo(1)); AssertFront(p, target);
                        if (scenario == "cancel-after") p.CancelAction();
                        cast.Step(.125f); Assert.That(target.ReceivedHitCount, Is.EqualTo(scenario == "cancel-after" ? 0 : 1));
                        Vector2 arrived = p.Position; target.Position += Vector2.right; Physics2D.SyncTransforms(); cast.BeforeMovement(1);
                        Assert.That(p.Position, Is.EqualTo(arrived)); Assert.That(blinks, Is.EqualTo(1));
                    }
                    Record(trace, scenario, "end", 1, arena, cast, target, blinks); Assert.That(arena.SimulationFaulted, Is.False);
                }
                finally { Application.logMessageReceived -= observe; Remove(arena, target); }
            }
        }
        static void LostTargets(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver)
        {
            foreach (int special in new[] { 2, 3 })
            foreach (string scenario in new[] { "no-target", "dead-before", "lost-before", "dead-after-claim", "lost-after-claim" })
            {
                var target = Setup(arena, driver, special); var p = arena.Player; var cast = Start(arena); string label = scenario + "/" + special;
                try
                {
                    if (scenario == "no-target") { target.Position = new Vector2(40, 1000); Physics2D.SyncTransforms(); }
                    if (scenario.EndsWith("before", StringComparison.Ordinal)) driver.Step(.0625f);
                    if (scenario.EndsWith("after-claim", StringComparison.Ordinal)) { cast.BeforeMovement(.125f); AssertFront(p, target); }
                    if (scenario.StartsWith("dead", StringComparison.Ordinal)) target.health = 0;
                    if (scenario.StartsWith("lost", StringComparison.Ordinal)) Remove(arena, target);
                    Vector2 origin = p.Position; Record(trace, label, "loss", 0, arena, cast, target);
                    cast.BeforeMovement(.125f); cast.Step(.125f); cast.BeforeMovement(.5f);
                    Assert.That(p.Position, Is.EqualTo(origin)); if (target) Assert.That(target.ReceivedHitCount, Is.Zero);
                    Assert.That(p.Weapon.Cooldown.Available, Is.EqualTo(scenario.EndsWith("after-claim", StringComparison.Ordinal) ? 0 : 1));
                    Record(trace, label, "attempt", 1, arena, cast, target); Assert.That(arena.SimulationFaulted, Is.False);
                }
                finally { Remove(arena, target); }
            }
        }
        static void BlockedArrival(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver)
        {
            foreach (int facing in new[] { 1, -1 })
            foreach (bool stillInRange in new[] { false, true })
            {
                var target = Setup(arena, driver, 2, facing); var p = arena.Player;
                target.Position = new Vector2((stillInRange ? 1.5f : 4) * facing, 1000); target.SyncFacing(facing); Physics2D.SyncTransforms();
                var cast = Start(arena); GameObject wall = null; string label = "blocked/" + facing + "/" + stillInRange;
                try
                {
                    driver.Step(.0625f); cast.BeforeMovement(.0625f); AssertFront(p, target);
                    // Activate first, then move back and add a body-height obstruction.
                    // A second cast has selected the target before the wall exists.
                    p.CancelAction(); p.Weapon.Cooldown.Reset(1, p.Special.cooldown); p.Position = new Vector2(0, 1000); p.SyncFacing(facing);
                    var source = Source(arena); source.activation = 0; source.movement.start = .25f; source.hits[0].time = .25f; source.end = 2;
                    cast = Start(arena, source); cast.BeforeMovement(0);
                    wall = Wall((stillInRange ? 2.8f : 1.5f) * facing); cast.BeforeMovement(.25f); Physics2D.SyncTransforms();
                    Record(trace, label, "arrival", 0, arena, cast, target); cast.Step(.25f);
                    Assert.That(target.ReceivedHitCount, Is.EqualTo(stillInRange ? 1 : 0), "Only the attack box at the actual stopped position can hit.");
                    Assert.That(p.bodyCollider.bounds.Intersects(wall.GetComponent<BoxCollider2D>().bounds), Is.False);
                    Vector2 stopped = p.Position; RemoveWall(wall); wall = null; cast.BeforeMovement(1); Assert.That(p.Position, Is.EqualTo(stopped));
                    Record(trace, label, "wall-removed-no-retry", 1, arena, cast, target);
                }
                finally { RemoveWall(wall); Remove(arena, target); }
            }
        }
        static SkillData LegacyTargetSource(CombatArena arena)
        {
            var source = Source(arena); source.activation = 0; source.end = 2; source.movement.type = "target_teleport"; source.movement.start = .25f;
            source.hits = new[] { new HitData { time = 0, damage = 5, active = .125f, anchor = "actor", targets = "nearest", box = new BoxData { width = 6, height = 2, x = 3, y = 1 } } };
            return source;
        }
        static void OtherMovementTypes(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver)
        {
            foreach (int facing in new[] { 1, -1 })
            foreach (string scenario in new[] { "accepted", "capped", "origin-side", "unaccepted", "dead", "missing", "fallback" })
            {
                var target = Setup(arena, driver, 2, facing); var p = arena.Player; var source = LegacyTargetSource(arena); source.movement.distance = scenario == "capped" ? .5f : 7;
                if (scenario == "missing") { source.hits = Array.Empty<HitData>(); source.movement.targetRadius = 0; }
                if (scenario == "fallback") { source.hits = Array.Empty<HitData>(); source.movement.type = "teleport"; source.movement.distance = 2.25f; }
                var cast = Start(arena, source); string label = "other/" + scenario + "/" + facing;
                try
                {
                    cast.BeforeMovement(0);
                    if (scenario != "unaccepted") cast.Step(.125f);
                    if (scenario == "dead") target.health = 0;
                    if (scenario == "origin-side") { p.Position = new Vector2(5 * facing, 1000); Physics2D.SyncTransforms(); }
                    if (scenario == "fallback") { p.Position = new Vector2(.5f, 1001); p.SyncFacing(-facing); Physics2D.SyncTransforms(); }
                    Vector2 origin = p.Position; Record(trace, label, "before-claim", 0, arena, cast, target); cast.BeforeMovement(.25f);
                    if (scenario == "unaccepted" || scenario == "dead" || scenario == "missing") Assert.That(p.Position, Is.EqualTo(origin));
                    else if (scenario == "fallback") Assert.That(p.Position, Is.EqualTo(origin + Vector2.right * facing * 2.25f));
                    else
                    {
                        Assert.That(p.Position.y, Is.EqualTo(origin.y)); Assert.That(p.facing, Is.EqualTo(facing));
                        if (scenario == "capped") Assert.That(Vector2.Distance(origin, p.Position), Is.EqualTo(.5f).Within(.00001f));
                        else
                        {
                            float gap = p.Center.x <= target.Center.x ? target.hurtCollider.bounds.min.x - p.bodyCollider.bounds.max.x : p.bodyCollider.bounds.min.x - target.hurtCollider.bounds.max.x;
                            Assert.That(gap, Is.EqualTo(.25f).Within(.002f));
                        }
                    }
                    Vector2 arrival = p.Position;
                    if (scenario == "unaccepted") { cast.Step(.125f); Assert.That(target.ReceivedHitCount, Is.EqualTo(1)); }
                    if (scenario == "dead") target.health = 1000;
                    target.Position += Vector2.right * 3; Physics2D.SyncTransforms(); cast.BeforeMovement(1);
                    Assert.That(p.Position, Is.EqualTo(arrival), "A no-op target attempt still consumes the one-shot claim."); Record(trace, label, "no-retry", 1, arena, cast, target);
                }
                finally { Remove(arena, target); }
            }
        }
        static void CallbackCases(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver)
        {
            foreach (string scenario in new[] { "activation-live-geometry", "activation-cancel", "arrival-reentry", "arrival-cancel", "receive-reentry", "receive-cancel", "receive-throw" })
            {
                var target = Setup(arena, driver); var p = arena.Player; var source = Source(arena);
                source.activation = source.movement.start = source.hits[0].time = .125f; source.end = 2;
                bool receive = scenario.StartsWith("receive", StringComparison.Ordinal); if (receive) source.hits[0].stun = .03125f;
                var cast = Start(arena, source); int blinks = 0, callbacks = 0; bool edited = false;
                Application.LogCallback observe = (message, stack, type) =>
                {
                    if (message.Contains("activate " + source.id) && !edited)
                    {
                        edited = true;
                        if (scenario == "activation-live-geometry") { target.Position = new Vector2(-2, 1001); target.SyncFacing(-1); p.Position = new Vector2(.25f, 1000); Physics2D.SyncTransforms(); }
                        if (scenario == "activation-cancel") p.CancelAction();
                    }
                    if (!message.Contains("rapier instant target teleport at=")) return;
                    blinks++; Record(trace, scenario, "arrival-callback", 0, arena, cast, target, callbacks); AssertFront(p, target);
                    if (scenario == "arrival-reentry")
                    {
                        Vector2 arrival = p.Position; target.Position += Vector2.right; Physics2D.SyncTransforms(); cast.BeforeMovement(.5f);
                        Assert.That(p.Position, Is.EqualTo(arrival)); Assert.That(blinks, Is.EqualTo(1));
                    }
                    if (scenario == "arrival-cancel") p.CancelAction();
                };
                target.OnCancel = () =>
                {
                    callbacks++; Assert.That(target.ReceivedHitCount, Is.EqualTo(1)); Record(trace, scenario, "receive-callback", 0, arena, cast, target, callbacks);
                    if (scenario == "receive-reentry") { Vector2 arrival = p.Position; cast.BeforeMovement(.5f); cast.Step(.125f); Assert.That(p.Position, Is.EqualTo(arrival)); }
                    if (scenario == "receive-cancel") p.CancelAction();
                    if (scenario == "receive-throw") throw new InvalidOperationException("Teleport Native receive boundary");
                };
                Application.logMessageReceived += observe;
                try
                {
                    cast.BeforeMovement(.125f); Record(trace, scenario, "prepared", 0, arena, cast, target, callbacks);
                    if (scenario == "receive-throw")
                    {
                        Assert.Throws<InvalidOperationException>(() => cast.Step(.125f)); target.OnCancel = null; cast.Step(.125f);
                    }
                    else cast.Step(.125f);
                    Assert.That(blinks, Is.EqualTo(scenario == "activation-cancel" ? 0 : 1));
                    if (receive) { Assert.That(callbacks, Is.EqualTo(1)); Assert.That(target.ReceivedHitCount, Is.EqualTo(1)); Assert.That(target.health, Is.EqualTo(965)); }
                    if (scenario == "activation-cancel" || scenario == "arrival-cancel") Assert.That(target.ReceivedHitCount, Is.Zero);
                    Record(trace, scenario, "end", 1, arena, cast, target, callbacks); Assert.That(arena.SimulationFaulted, Is.False);
                }
                finally { Application.logMessageReceived -= observe; target.OnCancel = null; Remove(arena, target); }
            }
        }
        static IEnumerator Exercise(string scene, string label)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); Assert.That(Application.isPlaying, Is.True); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var trace = new Trace();
            var floor = new GameObject("TeleportNativeFloor"); floor.transform.position = new Vector3(0, 999.5f, 0);
            floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(200, 1); Physics2D.SyncTransforms();
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    CatalogCases(trace, arena, driver); Boundaries(trace, arena, driver); LostTargets(trace, arena, driver);
                    BlockedArrival(trace, arena, driver); OtherMovementTypes(trace, arena, driver); CallbackCases(trace, arena, driver);
                }
                var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-teleportTraceRoot");
                if (at >= 0 && at + 1 < args.Length) { Directory.CreateDirectory(args[at + 1]); File.WriteAllText(Path.Combine(args[at + 1], label + ".json"), JsonUtility.ToJson(trace)); }
            }
            finally { Object.Destroy(floor); }
        }
    }
}
