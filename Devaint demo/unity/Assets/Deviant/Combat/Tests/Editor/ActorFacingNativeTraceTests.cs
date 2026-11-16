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
    public class ActorFacingNativeTraceTests
    {
        [Serializable] sealed class Row
        {
            public string scenario, phase;
            public int direction, facing;
            public bool result, player, body, hurt, visual, flip;
            public int[] bits, query;
        }
        [Serializable] sealed class Trace { public List<Row> rows = new List<Row>(); }
        sealed class Query : IActorPlacementQuery
        {
            public readonly List<ActorOverlapBox> Requests = new List<ActorOverlapBox>();
            public Func<ActorOverlapBox, IReadOnlyList<ActorSurfaceBounds>> Overlap;
            public IReadOnlyList<ActorSurfaceBounds> OverlapBox(ActorOverlapBox request) { Requests.Add(request); return Overlap(request); }
            public IReadOnlyList<ActorSurfaceContact> BoxCast(ActorBoxCast request) => throw new InvalidOperationException("Facing must not sweep the body.");
            public IReadOnlyList<ActorRayContact> Raycast(ActorRayCast request) => throw new InvalidOperationException("Facing must not probe the floor.");
        }
        static readonly ActorSurfaceBounds[] Empty = Array.Empty<ActorSurfaceBounds>();
        static readonly FieldInfo Surfaces = typeof(CombatActor).GetField("surfaces", BindingFlags.Instance | BindingFlags.NonPublic);
        static int Bits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        static void Bind(CombatActor actor, Query query) => Surfaces.SetValue(actor, query);
        static void Record(Trace trace, string scenario, string phase, CombatActor actor, int direction, bool result = true, ActorOverlapBox? request = null)
        {
            var at = actor.Position; var center = actor.Center;
            var body = actor.bodyCollider ? actor.bodyCollider.offset : Vector2.zero;
            var hurt = actor.hurtCollider ? actor.hurtCollider.offset : Vector2.zero;
            var visual = actor.visual ? actor.visual.transform.localPosition : Vector3.zero;
            trace.rows.Add(new Row
            {
                scenario = scenario, phase = phase, direction = direction, facing = actor.facing, result = result, player = actor.isPlayer,
                body = actor.bodyCollider, hurt = actor.hurtCollider, visual = actor.visual, flip = actor.visual && actor.visual.flipX,
                bits = new[] { Bits(at.x), Bits(at.y), Bits(actor.transform.position.z), Bits(center.x), Bits(center.y), Bits(actor.size.x), Bits(actor.size.y), Bits(actor.offset.x), Bits(actor.offset.y),
                    Bits(body.x), Bits(body.y), Bits(hurt.x), Bits(hurt.y), Bits(visual.x), Bits(visual.y), Bits(visual.z), Bits(actor.velocity.x), Bits(actor.velocity.y) },
                query = request.HasValue ? new[] { Bits(request.Value.X), Bits(request.Value.Y), Bits(request.Value.Width), Bits(request.Value.Height) } : Array.Empty<int>()
            });
        }
        static void AssertPose(CombatActor actor, Vector2 position, int facing, Vector3 playerVisual)
        {
            Assert.That(actor.Position, Is.EqualTo(position)); Assert.That(actor.facing, Is.EqualTo(facing));
            Assert.That((Vector2)actor.transform.position, Is.EqualTo(position), "The native view must already reflect the authoritative pose.");
            var offset = new Vector2(actor.offset.x * facing, actor.offset.y);
            Assert.That(actor.bodyCollider.offset, Is.EqualTo(offset)); Assert.That(actor.hurtCollider.offset, Is.EqualTo(offset));
            Assert.That(actor.visual.flipX, Is.EqualTo(facing < 0));
            Assert.That(actor.visual.transform.localPosition, Is.EqualTo(actor.isPlayer ? playerVisual : new Vector3(offset.x, offset.y, 0)));
        }
        [UnityTest] public IEnumerator GameKeepsFacingGeometryAndLiveNativeOrder() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "game");
        [UnityTest] public IEnumerator LabKeepsFacingGeometryAndLiveNativeOrder() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "lab");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static IEnumerator Exercise(string scene, string label)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var roots = new List<GameObject>(); var trace = new Trace();
            BoxCollider2D Surface(string name, Vector2 at, Vector2 size, bool oneWay = false)
            {
                var root = new GameObject(name); roots.Add(root); root.transform.position = at; root.AddComponent<ArenaSurface>().oneWay = oneWay;
                var collider = root.AddComponent<BoxCollider2D>(); collider.size = size; return collider;
            }
            var left = Surface("FacingLeftWall", new Vector2(-3, 1002), new Vector2(.4f, 10));
            var right = Surface("FacingRightWall", new Vector2(3, 1002), new Vector2(.4f, 10));
            Surface("FacingFloor", new Vector2(0, 999.5f), new Vector2(30, 1));
            Surface("FacingOneWay", new Vector2(0, 1001), new Vector2(30, 2), true);
            var probe = new GameObject("FacingOrderProbe").AddComponent<CombatActor>(); roots.Add(probe.gameObject);
            probe.Configure(arena, new Vector2(0, 1000), new Vector2(.9375f, 2), new Vector2(.1875f, 1), false);
            var renderer = new GameObject("FacingVisual"); renderer.transform.SetParent(probe.transform, false); probe.visual = renderer.AddComponent<SpriteRenderer>();
            var sentinel = new Vector3(.3125f, 2.25f, 4);
            void Reset(CombatActor actor, int facing = 1, float x = .125f, float offsetX = .1875f)
            {
                left.enabled = right.enabled = false; Bind(actor, null); actor.ResetActor(new Vector2(x, 1000));
                actor.size = new Vector2(.9375f, 2); actor.offset = new Vector2(offsetX, 1.25f); actor.facing = facing;
                actor.bodyCollider.size = actor.hurtCollider.size = actor.size; actor.SyncFacing(facing);
                actor.visual.transform.localPosition = sentinel; actor.velocity = new Vector2(5, -7); Physics2D.SyncTransforms();
            }
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    driver.Reset(); arena.Player.Position = new Vector2(35, 1000);
                    foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(-35, 1000); }
                    foreach (var actor in new CombatActor[] { arena.Player, arena.enemies[0] })
                    {
                        var body = actor.bodyCollider; var hurt = actor.hurtCollider; var visual = actor.visual;
                        foreach (int oldFacing in new[] { -1, 1 })
                            foreach (float offsetX in new[] { .1875f, .4375f })
                                foreach (bool blocked in new[] { false, true })
                                {
                                    int direction = -oldFacing; string scenario = (actor.isPlayer ? "player" : "enemy") + ":" + oldFacing + ":" + offsetX + ":" + blocked;
                                    float x = blocked ? direction * (2.8f - .9375f * .5f) - offsetX * oldFacing : .125f;
                                    Reset(actor, oldFacing, x, offsetX); (direction < 0 ? left : right).enabled = blocked; Physics2D.SyncTransforms();
                                    Assert.That(actor.Blocked(actor.Position, oldFacing), Is.False); Assert.That(actor.Blocked(actor.Position, direction), Is.EqualTo(blocked));
                                    var oldPosition = actor.Position; var oldCenter = actor.Center; int input = direction < 0 ? int.MinValue : int.MaxValue;
                                    Record(trace, scenario, "before", actor, input); bool result = actor.SyncFacing(input); Assert.That(result, Is.True);
                                    AssertPose(actor, oldPosition + new Vector2(blocked ? offsetX * (oldFacing - direction) : 0, 0), direction, sentinel);
                                    if (blocked) { Assert.That(actor.Center.x, Is.EqualTo(oldCenter.x).Within(.000001f)); Physics2D.SyncTransforms(); Assert.That(actor.Blocked(actor.Position, direction), Is.False); }
                                    Assert.That(actor.bodyCollider, Is.SameAs(body)); Assert.That(actor.hurtCollider, Is.SameAs(hurt)); Assert.That(actor.visual, Is.SameAs(visual));
                                    Record(trace, scenario, "after", actor, input, result); var corrected = actor.Position;
                                    Assert.That(actor.SyncFacing(input), Is.True); AssertPose(actor, corrected, direction, sentinel); Record(trace, scenario, "repeated", actor, input);
                                }
                        Reset(actor); actor.bodyCollider.offset = new Vector2(8, 9); actor.hurtCollider.offset = new Vector2(10, 11); actor.visual.flipX = true;
                        actor.SetPosition(new Vector3(.125f, 1000, 3)); var zeroPosition = actor.transform.position;
                        var forbidden = new Query { Overlap = _ => throw new InvalidOperationException("Zero and repeated facing must not query.") }; Bind(actor, forbidden);
                        Record(trace, actor.isPlayer ? "player-zero" : "enemy-zero", "before", actor, 0); Assert.That(actor.SyncFacing(0), Is.True);
                        Assert.That(actor.transform.position, Is.EqualTo(zeroPosition)); Assert.That(actor.bodyCollider.offset, Is.EqualTo(new Vector2(8, 9)));
                        Assert.That(actor.hurtCollider.offset, Is.EqualTo(new Vector2(10, 11))); Assert.That(actor.visual.flipX, Is.True); Assert.That(actor.visual.transform.localPosition, Is.EqualTo(sentinel));
                        Record(trace, actor.isPlayer ? "player-zero" : "enemy-zero", "after", actor, 0);
                        Assert.That(actor.SyncFacing(23), Is.True); AssertPose(actor, (Vector2)zeroPosition, 1, sentinel); Assert.That(actor.transform.position.z, Is.EqualTo(3));
                        Assert.That(forbidden.Requests, Is.Empty); Record(trace, actor.isPlayer ? "player-resync" : "enemy-resync", "after", actor, 23);
                        for (int mask = 0; mask < 8; mask++)
                        {
                            Reset(actor); actor.bodyCollider = (mask & 1) == 0 ? null : body; actor.hurtCollider = (mask & 2) == 0 ? null : hurt; actor.visual = (mask & 4) == 0 ? null : visual;
                            Assert.That(actor.SyncFacing(-17), Is.True); Assert.That(actor.facing, Is.EqualTo(-1)); Assert.That(actor.Position, Is.EqualTo(new Vector2(.125f, 1000)));
                            if (actor.bodyCollider) Assert.That(actor.bodyCollider.offset, Is.EqualTo(new Vector2(-.1875f, 1.25f)));
                            if (actor.hurtCollider) Assert.That(actor.hurtCollider.offset, Is.EqualTo(new Vector2(-.1875f, 1.25f)));
                            if (actor.visual) { Assert.That(actor.visual.flipX, Is.True); Assert.That(actor.visual.transform.localPosition, Is.EqualTo(actor.isPlayer ? sentinel : new Vector3(-.1875f, 1.25f, 0))); }
                            Record(trace, (actor.isPlayer ? "player-presence-" : "enemy-presence-") + mask, "after", actor, -17);
                            actor.bodyCollider = body; actor.hurtCollider = hurt; actor.visual = visual;
                        }
                        Bind(actor, null); actor.Position = new Vector2(actor.isPlayer ? 35 : -35, 1000);
                    }
                    {
                        Reset(probe); const string scenario = "live-query-correction"; var query = new Query();
                        query.Overlap = request =>
                        {
                            Record(trace, scenario, "query-old-body", probe, -23, request: request);
                            Assert.That(probe.facing, Is.EqualTo(1)); Assert.That(request.X, Is.EqualTo(-.0625f)); Assert.That(request.Y, Is.EqualTo(1001.25f));
                            Assert.That(request.Width, Is.EqualTo(.9225f)); Assert.That(request.Height, Is.EqualTo(1.985f));
                            Assert.That(probe.bodyCollider.offset, Is.EqualTo(new Vector2(.1875f, 1.25f))); Assert.That(probe.visual.transform.localPosition, Is.EqualTo(sentinel));
                            probe.Position = new Vector2(5, 1003); probe.offset = new Vector2(.5f, 2); probe.facing = 1;
                            return new[] { new ActorSurfaceBounds(-1, 999, -1, 1, 1003, 1, false) };
                        };
                        Bind(probe, query); Assert.That(probe.SyncFacing(-23), Is.True); AssertPose(probe, new Vector2(6, 1003), -1, sentinel);
                        Assert.That(query.Requests.Count, Is.EqualTo(1)); Record(trace, scenario, "after", probe, -23);
                    }
                    {
                        Reset(probe); const string scenario = "query-reentry"; var first = new Query(); var second = new Query();
                        second.Overlap = request => { Record(trace, scenario, "nested-query", probe, -1, request: request); return Empty; };
                        first.Overlap = request =>
                        {
                            Record(trace, scenario, "outer-query", probe, -1, request: request); Bind(probe, second); Assert.That(probe.SyncFacing(-1), Is.True);
                            probe.offset = new Vector2(.5f, 2); return new[] { new ActorSurfaceBounds(-1, 999, -1, 1, 1003, 1, false) };
                        };
                        Bind(probe, first); Assert.That(probe.SyncFacing(-1), Is.True); AssertPose(probe, new Vector2(.125f, 1000), -1, sentinel);
                        Assert.That(first.Requests.Count, Is.EqualTo(1)); Assert.That(second.Requests.Count, Is.EqualTo(1)); Record(trace, scenario, "after", probe, -1);
                    }
                    {
                        Reset(probe); const string scenario = "query-partial-failure"; var failure = new InvalidOperationException("facing query failed"); var query = new Query();
                        query.Overlap = request =>
                        {
                            Record(trace, scenario, "query", probe, -1, request: request); probe.Position = new Vector2(7, 1004); probe.offset = new Vector2(.75f, 3); throw failure;
                        };
                        Bind(probe, query); Assert.That(Assert.Throws<InvalidOperationException>(() => probe.SyncFacing(-1)), Is.SameAs(failure));
                        Assert.That(probe.Position, Is.EqualTo(new Vector2(7, 1004))); Assert.That(probe.facing, Is.EqualTo(1));
                        Assert.That(probe.bodyCollider.offset, Is.EqualTo(new Vector2(.1875f, 1.25f))); Assert.That(probe.hurtCollider.offset, Is.EqualTo(new Vector2(.1875f, 1.25f)));
                        Assert.That(probe.visual.flipX, Is.False); Assert.That(probe.visual.transform.localPosition, Is.EqualTo(sentinel)); Record(trace, scenario, "after-failure", probe, -1, false);
                    }
                    {
                        Reset(probe); const string scenario = "callback-direct-facing"; var query = new Query();
                        query.Overlap = request => { Record(trace, scenario, "query", probe, -1, request: request); probe.facing = -1; return Empty; };
                        Bind(probe, query); Assert.That(probe.SyncFacing(-1), Is.True); AssertPose(probe, new Vector2(.125f, 1000), -1, sentinel); Record(trace, scenario, "after", probe, -1);
                    }
                    var childVisual = probe.visual; var rootVisual = probe.gameObject.AddComponent<SpriteRenderer>(); var originalHurt = probe.hurtCollider;
                    foreach (bool player in new[] { false, true })
                    {
                        probe.visual = childVisual; probe.hurtCollider = originalHurt; Reset(probe); probe.isPlayer = player;
                        probe.visual = rootVisual; probe.hurtCollider = probe.bodyCollider; probe.SetPosition(new Vector3(.125f, 1000, 3));
                        Assert.That(probe.SyncFacing(-1), Is.True); Assert.That(probe.bodyCollider, Is.SameAs(probe.hurtCollider)); Assert.That(probe.visual, Is.SameAs(rootVisual));
                        Assert.That(probe.bodyCollider.offset, Is.EqualTo(new Vector2(-.1875f, 1.25f))); Assert.That(rootVisual.flipX, Is.True);
                        Assert.That(probe.transform.position, Is.EqualTo(player ? new Vector3(.125f, 1000, 3) : new Vector3(-.1875f, 1.25f, 0)));
                        Assert.That(probe.Position, Is.EqualTo((Vector2)probe.transform.position), "A root visual alias commits its actor pose through the owner.");
                        Record(trace, player ? "player-root-visual-alias" : "enemy-root-visual-alias", "after", probe, -1);
                    }
                    Assert.That(arena.SimulationFaulted, Is.False);
                }
                Assert.That(trace.rows.Count, Is.EqualTo(81));
                var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-actorFacingTraceRoot");
                if (index >= 0 && index + 1 < args.Length) { Directory.CreateDirectory(args[index + 1]); File.WriteAllText(Path.Combine(args[index + 1], label + ".json"), JsonUtility.ToJson(trace)); }
            }
            finally { foreach (var root in roots) Object.Destroy(root); }
        }
    }
}
