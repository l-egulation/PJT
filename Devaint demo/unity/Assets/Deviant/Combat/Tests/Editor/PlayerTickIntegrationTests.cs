using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Deviant.Combat.Contracts;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    // Uses the original public player/driver APIs and the existing surface-query
    // seam, so the identical fixture can run on the pre-extraction consumer.
    public class PlayerTickIntegrationTests
    {
        [Serializable] sealed class Row
        {
            public string scenario, phase;
            public int jumps, returns;
            public bool grounded, faulted;
            public int[] bits;
        }
        [Serializable] sealed class Trace { public List<Row> rows = new List<Row>(); }
        sealed class Query : IActorPlacementQuery
        {
            readonly IActorPlacementQuery inner;
            public Action<ActorBoxCast> Casting;
            public Action<ActorRayCast> Raycasting;
            public Query(IActorPlacementQuery inner) { this.inner = inner; }
            public IReadOnlyList<ActorSurfaceContact> BoxCast(ActorBoxCast request) { Casting?.Invoke(request); return inner.BoxCast(request); }
            public IReadOnlyList<ActorSurfaceBounds> OverlapBox(ActorOverlapBox request) => inner.OverlapBox(request);
            public IReadOnlyList<ActorRayContact> Raycast(ActorRayCast request) { Raycasting?.Invoke(request); return inner.Raycast(request); }
        }
        static readonly FieldInfo Surfaces = typeof(CombatActor).GetField("surfaces", BindingFlags.Instance | BindingFlags.NonPublic);
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        static void Record(Trace trace, string scenario, string phase, CombatArena arena)
        {
            var p = arena.Player;
            trace.rows.Add(new Row { scenario = scenario, phase = phase, jumps = p.jumps.Used, returns = p.ReturnReservations.Count,
                grounded = p.grounded, faulted = arena.SimulationFaulted,
                bits = new[] { Bits(arena.Now), Bits(p.Position.x), Bits(p.Position.y), Bits(p.velocity.x), Bits(p.velocity.y),
                    Bits(p.health), Bits(p.jumps.BufferedUntil), Bits(p.LastFallTime), Bits(p.LastRestoreTime) } });
        }
        static void Prepare(CombatArena arena, CombatArena.ManualSimulation driver, Vector2 spawn)
        {
            Surfaces.SetValue(arena.Player, null); driver.Reset(); var p = arena.Player;
            p.spawn = spawn; p.ResetPlayer(); p.InputBlocked = false; p.selected = 0; p.SyncFacing(1);
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 1000); enemy.velocity = Vector2.zero; }
            Physics2D.SyncTransforms();
        }
        static void SpendJumpUsage(CombatPlayer player)
        {
            Assert.That(player.jumps.Press(player.arena.Now, player.arena.Movement, out _), Is.True);
            Assert.That(player.jumps.Used, Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator GameKeepsTheSingleLiveGroundTransitionAndItsFailurePrefix() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "game");
        [UnityTest] public IEnumerator LabKeepsTheSingleLiveGroundTransitionAndItsFailurePrefix() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "lab");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static IEnumerator Exercise(string scene, string label)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); Assert.That(Application.isPlaying, Is.True); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var p = arena.Player; var trace = new Trace(); var oldSpawn = p.spawn;
            var spawn = new Vector2(0, 1000);
            var floor = new GameObject("PlayerTickGroundTransitionFloor"); floor.transform.position = new Vector3(0, 999.5f, 0);
            floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1);
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    Prepare(arena, driver, spawn);
                    SpendJumpUsage(p); driver.Step(.02f);
                    Assert.That(p.grounded, Is.True); Assert.That(p.jumps.Used, Is.Zero);
                    Record(trace, "landing", "first-contact", arena);
                    SpendJumpUsage(p); driver.Step(.02f);
                    Assert.That(p.jumps.Used, Is.EqualTo(1), "An unchanged grounded contact is not a new landing.");
                    Record(trace, "landing", "stable-contact", arena);

                    Prepare(arena, driver, spawn);
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Jump));
                    Assert.That(p.grounded, Is.False); Assert.That(p.jumps.Used, Is.EqualTo(1));
                    Record(trace, "jump", "accepted", arena);
                    int ticks = 0;
                    while (!p.grounded && ticks++ < 100) driver.Step(.02f);
                    Assert.That(p.grounded, Is.True); Assert.That(p.jumps.Used, Is.Zero);
                    Record(trace, "jump", "new-contact", arena);

                    Prepare(arena, driver, spawn);
                    p.Position = new Vector2(0, 1000.01f); p.velocity = new Vector2(0, -1); Physics2D.SyncTransforms();
                    p.jumps.Press(arena.Now, arena.Movement, out _); p.jumps.Press(arena.Now, arena.Movement, out _);
                    Assert.That(p.jumps.Press(arena.Now, arena.Movement, out _), Is.False);
                    driver.Step(.02f);
                    Assert.That(p.grounded, Is.False); Assert.That(p.velocity.y, Is.EqualTo(arena.Movement.FirstJumpVelocity)); Assert.That(p.jumps.Used, Is.EqualTo(1));
                    Record(trace, "buffered-landing", "jump-consumed", arena);
                    ticks = 0;
                    while (!p.grounded && ticks++ < 100) driver.Step(.02f);
                    Assert.That(p.grounded, Is.True); Assert.That(p.jumps.Used, Is.Zero);
                    Record(trace, "buffered-landing", "next-contact", arena);

                    foreach (bool fall in new[] { false, true })
                    {
                        Prepare(arena, driver, spawn); driver.Step(.02f);
                        if (fall) p.Fall(); else p.ResetPlayer();
                        SpendJumpUsage(p); driver.Step(.02f);
                        Assert.That(p.grounded, Is.True); Assert.That(p.jumps.Used, Is.Zero);
                        Assert.That(p.health, Is.EqualTo(fall ? 90 : 100));
                        Record(trace, fall ? "fall" : "reset", "new-contact", arena);
                    }

                    Prepare(arena, driver, spawn);
                    var target = arena.enemies[0]; target.Position = new Vector2(6, 1000); target.SyncFacing(1); Physics2D.SyncTransforms();
                    Assert.That(p.TryShurikenVisit(target, p.Weapon, .04f, .25f), Is.True); SpendJumpUsage(p);
                    driver.Step(.02f);
                    Assert.That(p.grounded, Is.True); Assert.That(p.jumps.Used, Is.EqualTo(1));
                    Record(trace, "return-pose", "visit-grounded", arena);
                    driver.Step(.03f); driver.Step(.02f);
                    Assert.That(p.ReturnReservations, Is.Empty); Assert.That(p.grounded, Is.True); Assert.That(p.jumps.Used, Is.EqualTo(1));
                    Record(trace, "return-pose", "automatic-grounded", arena);

                    Prepare(arena, driver, spawn); target.Position = new Vector2(6, 1000); target.SyncFacing(1); Physics2D.SyncTransforms();
                    SpendJumpUsage(p);
                    bool visited = false;
                    var query = new Query(new UnityActorSurfaceQuery(_ => false));
                    query.Casting = request =>
                    {
                        if (request.DirectionX == 0 || visited) return;
                        visited = true;
                        Assert.That(p.TryShurikenVisit(target, p.Weapon, 1, .25f), Is.True);
                        Record(trace, "ground-owner-callback", "visit-during-x", arena);
                    };
                    Surfaces.SetValue(p, query); driver.Step(.02f, new CombatInput(1, CombatButtons.None));
                    Assert.That(visited, Is.True); Assert.That(p.grounded, Is.True);
                    Assert.That(p.jumps.Used, Is.EqualTo(1), "A native callback updates the embedded owner seen later in this same tick.");
                    Record(trace, "ground-owner-callback", "after-movement", arena);

                    Prepare(arena, driver, spawn); SpendJumpUsage(p);
                    var failure = new InvalidOperationException("player safe floor query");
                    query = new Query(new UnityActorSurfaceQuery(_ => false));
                    query.Raycasting = request =>
                    {
                        Assert.That(p.grounded, Is.True); Assert.That(p.jumps.Used, Is.Zero);
                        Record(trace, "landing-before-failure", "floor-ray-throws", arena); throw failure;
                    };
                    Surfaces.SetValue(p, query);
                    Assert.That(Assert.Throws<InvalidOperationException>(() => driver.Step(.02f)), Is.SameAs(failure));
                    Assert.That(arena.SimulationFaulted, Is.True); Assert.That(arena.Now, Is.Zero);
                    Assert.That(p.grounded, Is.True); Assert.That(p.jumps.Used, Is.Zero);
                    Record(trace, "landing-before-failure", "applied-prefix", arena);
                    Surfaces.SetValue(p, null); SpendJumpUsage(p); p.Step(.02f);
                    Assert.That(p.jumps.Used, Is.EqualTo(1), "The ground transition committed before the later floor error and is not replayed.");
                    Record(trace, "landing-before-failure", "transition-not-replayed", arena);
                    driver.Reset();
                }
                var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-playerTickTraceRoot");
                if (at >= 0 && at + 1 < args.Length)
                { Directory.CreateDirectory(args[at + 1]); File.WriteAllText(Path.Combine(args[at + 1], label + ".json"), JsonUtility.ToJson(trace)); }
            }
            finally { Surfaces.SetValue(p, null); p.spawn = oldSpawn; Object.Destroy(floor); }
        }
    }
}
