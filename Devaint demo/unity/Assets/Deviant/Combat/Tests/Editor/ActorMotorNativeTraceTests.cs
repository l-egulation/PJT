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
    public class ActorMotorNativeTraceTests
    {
        [Serializable] sealed class Row
        {
            public string scenario, phase;
            public int[] bits, query;
            public bool grounded, wall;
            public int facing;
        }
        [Serializable] sealed class Trace { public List<Row> rows = new List<Row>(); }
        sealed class Query : IActorPlacementQuery
        {
            public readonly List<ActorBoxCast> Requests = new List<ActorBoxCast>();
            public Func<ActorBoxCast, IReadOnlyList<ActorSurfaceContact>> Cast;
            public IReadOnlyList<ActorSurfaceContact> BoxCast(ActorBoxCast request) { Requests.Add(request); return Cast(request); }
            public IReadOnlyList<ActorSurfaceBounds> OverlapBox(ActorOverlapBox request) => throw new InvalidOperationException("The motor must not issue placement overlaps.");
            public IReadOnlyList<ActorRayContact> Raycast(ActorRayCast request) => throw new InvalidOperationException("The motor must not issue placement rays.");
        }
        static readonly ActorSurfaceContact[] Empty = Array.Empty<ActorSurfaceContact>();
        static readonly FieldInfo Surfaces = typeof(CombatActor).GetField("surfaces", BindingFlags.Instance | BindingFlags.NonPublic);
        static int Bits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        static bool GroundProbe(ActorBoxCast request) => request.Height == .04f;
        static void Bind(CombatActor actor, Query query) => Surfaces.SetValue(actor, query);
        static void Record(Trace trace, string scenario, string phase, CombatActor actor, ActorBoxCast? request = null)
        {
            var at = actor.Position;
            trace.rows.Add(new Row
            {
                scenario = scenario, phase = phase, grounded = actor.grounded, wall = actor.wallHit, facing = actor.facing,
                bits = new[] { Bits(actor.arena.Now), Bits(at.x), Bits(at.y), Bits(actor.velocity.x), Bits(actor.velocity.y), Bits(actor.size.x), Bits(actor.size.y), Bits(actor.offset.x), Bits(actor.offset.y) },
                query = request.HasValue ? new[] { Bits(request.Value.X), Bits(request.Value.Y), Bits(request.Value.Width), Bits(request.Value.Height), Bits(request.Value.DirectionX), Bits(request.Value.DirectionY), Bits(request.Value.Distance) } : Array.Empty<int>()
            });
        }
        [UnityTest] public IEnumerator GameKeepsLiveAxisFactsAndPartialFailures() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "game");
        [UnityTest] public IEnumerator LabKeepsLiveAxisFactsAndPartialFailures() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "lab");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static IEnumerator Exercise(string scene, string label)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>();
            var actor = new GameObject("MotorOrderProbe").AddComponent<CombatActor>();
            actor.Configure(arena, new Vector2(0, 1000), new Vector2(.9375f, 2), new Vector2(.1875f, 1), false);
            var trace = new Trace();
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    driver.Reset(); arena.Player.Position = new Vector2(35, 1000);
                    foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(-35, 1000); }
                    void Reset()
                    {
                        actor.ResetActor(new Vector2(0, 1000)); actor.size = new Vector2(.9375f, 2); actor.offset = new Vector2(.1875f, 1); actor.facing = 1; actor.SyncFacing(1);
                        actor.velocity = new Vector2(19, -1); actor.grounded = actor.wallHit = true;
                    }
                    foreach (bool blocked in new[] { false, true })
                    {
                        Reset(); string scenario = blocked ? "wall-before-y-failure" : "x-before-y-failure";
                        var failure = new InvalidOperationException("vertical contact failure"); var query = new Query();
                        query.Cast = request =>
                        {
                            Record(trace, scenario, GroundProbe(request) ? "ground" : request.DirectionX != 0 ? "x" : "y-throw", actor, request);
                            if (GroundProbe(request)) return Empty;
                            if (request.DirectionX != 0) return blocked ? new[] { new ActorSurfaceContact(.01f, -1, 0, 1000, false, false) } : Empty;
                            Assert.That(actor.Position.x, Is.EqualTo(blocked ? .008f : .1f).Within(.0000001f)); Assert.That(actor.Position.y, Is.EqualTo(1000));
                            Assert.That((Vector2)actor.transform.position, Is.EqualTo(actor.Position), "X output must reach the native view before Y queries, even when Y throws.");
                            Assert.That(actor.velocity.x, Is.EqualTo(5)); Assert.That(actor.velocity.y, Is.LessThan(-1));
                            Assert.That(actor.grounded, Is.False); Assert.That(actor.wallHit, Is.EqualTo(blocked)); throw failure;
                        };
                        Bind(actor, query);
                        Assert.That(Assert.Throws<InvalidOperationException>(() => actor.PhysicsStep(.02f, 5, false)), Is.SameAs(failure));
                        Assert.That(query.Requests.Count, Is.EqualTo(3)); Assert.That(actor.Position.y, Is.EqualTo(1000));
                        Assert.That(actor.wallHit, Is.EqualTo(blocked)); Record(trace, scenario, "after-failure", actor);
                    }
                    {
                        Reset(); const string scenario = "live-body-and-query"; var first = new Query(); var second = new Query();
                        first.Cast = request =>
                        {
                            Record(trace, scenario, GroundProbe(request) ? "ground" : "x", actor, request);
                            if (GroundProbe(request)) { actor.VelocityY = -4; actor.knockSpeed = 7; actor.knockUntil = .5f; return Empty; }
                            Assert.That(request.DirectionX, Is.EqualTo(1)); Assert.That(actor.velocity.x, Is.EqualTo(7)); Assert.That(actor.velocity.y, Is.LessThan(-4));
                            actor.Position = new Vector2(2, 1003); actor.size = new Vector2(2, 4); actor.offset = new Vector2(.5f, 2); actor.facing = -1;
                            actor.velocity = new Vector2(17, -11); actor.grounded = actor.wallHit = true; Bind(actor, second); return Empty;
                        };
                        second.Cast = request =>
                        {
                            Record(trace, scenario, "y-current-body", actor, request);
                            Assert.That(request.DirectionX, Is.Zero); Assert.That(request.DirectionY, Is.EqualTo(-1));
                            Assert.That(actor.Position.x, Is.EqualTo(2.14f).Within(.000001f)); Assert.That(request.X, Is.EqualTo(1.64f).Within(.000001f)); Assert.That(request.Y, Is.EqualTo(1005));
                            Assert.That((Vector2)actor.transform.position, Is.EqualTo(actor.Position), "The Y query observes the synchronous X output after live callback writes.");
                            Assert.That(request.Width, Is.EqualTo(1.995f)); Assert.That(request.Height, Is.EqualTo(3.995f));
                            return new[] { new ActorSurfaceContact(.01f, 0, 1, 1003, false, false) };
                        };
                        Bind(actor, first); actor.PhysicsStep(.02f, 5, false);
                        Assert.That(first.Requests.Count, Is.EqualTo(2)); Assert.That(second.Requests.Count, Is.EqualTo(1));
                        Assert.That(actor.velocity.x, Is.EqualTo(17)); Assert.That(actor.velocity.y, Is.Zero); Assert.That(actor.grounded && actor.wallHit, Is.True);
                        Record(trace, scenario, "after-contact", actor);
                    }
                    {
                        Reset(); const string scenario = "skipped-y-retains-callback-facts"; var query = new Query();
                        query.Cast = request =>
                        {
                            Record(trace, scenario, GroundProbe(request) ? "ground" : "x", actor, request);
                            if (GroundProbe(request)) return Empty;
                            Assert.That(request.DirectionX, Is.EqualTo(1)); actor.VelocityY = 9; actor.grounded = actor.wallHit = true; return Empty;
                        };
                        Bind(actor, query); actor.PhysicsStep(.02f, 5, true);
                        Assert.That(query.Requests.Count, Is.EqualTo(2)); Assert.That(actor.Position.x, Is.EqualTo(.1f).Within(.0000001f)); Assert.That(actor.Position.y, Is.EqualTo(1000));
                        Assert.That(actor.velocity.y, Is.EqualTo(9)); Assert.That(actor.grounded && actor.wallHit, Is.True); Record(trace, scenario, "after-skipped-y", actor);
                    }
                    foreach (bool rising in new[] { false, true })
                    {
                        Reset(); string scenario = rising ? "ceiling-is-not-ground" : "falling-contact-is-ground"; actor.VelocityY = rising ? 20 : -1;
                        var query = new Query();
                        query.Cast = request =>
                        {
                            Record(trace, scenario, GroundProbe(request) ? "ground" : "vertical-contact", actor, request);
                            if (GroundProbe(request)) return Empty;
                            Assert.That(request.DirectionX, Is.Zero); Assert.That(actor.grounded, Is.False); Assert.That(actor.wallHit, Is.False);
                            return new[] { new ActorSurfaceContact(.01f, 0, rising ? -1 : 1, 1000, false, false) };
                        };
                        Bind(actor, query); actor.PhysicsStep(.02f, 0, false);
                        Assert.That(query.Requests.Count, Is.EqualTo(rising ? 1 : 2)); Assert.That(actor.velocity.y, Is.Zero); Assert.That(actor.grounded, Is.EqualTo(!rising));
                        Assert.That(actor.wallHit, Is.False);
                        if (rising) Assert.That(actor.Position.y, Is.GreaterThan(1000)); else Assert.That(actor.Position.y, Is.LessThan(1000));
                        Record(trace, scenario, "after-contact", actor);
                    }
                    Assert.That(arena.SimulationFaulted, Is.False);
                }
                Assert.That(trace.rows.Count, Is.EqualTo(20));
                var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-actorMotorTraceRoot");
                if (index >= 0 && index + 1 < args.Length) { Directory.CreateDirectory(args[index + 1]); File.WriteAllText(Path.Combine(args[index + 1], label + ".json"), JsonUtility.ToJson(trace)); }
            }
            finally { Object.Destroy(actor.gameObject); }
        }
    }
}
