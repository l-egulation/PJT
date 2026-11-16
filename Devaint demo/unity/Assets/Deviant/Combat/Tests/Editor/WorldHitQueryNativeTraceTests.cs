using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public sealed class WorldHitQueryNativeProbe : TestEnemy
    {
        public Action OnCancel;
        public override void CancelAttack() { OnCancel?.Invoke(); base.CancelAttack(); }
    }

    public class WorldHitQueryNativeTraceTests
    {
        [Serializable] sealed class Row
        {
            public string scenario, selected;
            public string[] candidates;
            public bool exposed, occluded;
            public int hits, callbacks;
            public int[] bits;
        }
        [Serializable] sealed class Trace { public List<Row> rows = new List<Row>(); }
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        [UnityTest] public IEnumerator GameKeepsNativeNearestAndExposedQueries() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "game");
        [UnityTest] public IEnumerator LabKeepsNativeNearestAndExposedQueries() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "lab");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        static void Record(Trace trace, string scenario, CombatArena arena, CombatActor selected, CombatActor target, Vector2 center, Vector2 size,
            bool exposed = false, int callbacks = 0)
        {
            var p = arena.Player; var bounds = target.hurtCollider.bounds; var closest = target.hurtCollider.ClosestPoint(p.Center);
            var candidates = arena.TargetsInBox(center, size); var names = new string[candidates.Count];
            for (int i = 0; i < names.Length; i++) names[i] = candidates[i].name;
            trace.rows.Add(new Row
            {
                scenario = scenario, selected = selected ? selected.name : "none", candidates = names, exposed = exposed,
                occluded = arena.Occluded(p.Center, closest), hits = target.ReceivedHitCount, callbacks = callbacks,
                bits = new[] { Bits(arena.Now), Bits(p.Position.x), Bits(p.Position.y), Bits(p.Center.x), Bits(p.Center.y),
                    Bits(target.Position.x), Bits(target.Position.y), Bits(target.Center.x), Bits(target.Center.y),
                    Bits(bounds.min.x), Bits(bounds.max.x), Bits(bounds.min.y), Bits(bounds.max.y), Bits(closest.x), Bits(closest.y),
                    Bits(center.x), Bits(center.y), Bits(size.x), Bits(size.y), Bits(target.health) }
            });
        }

        static IEnumerator Exercise(string scene, string label)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var roots = new List<GameObject>(); var targets = new List<WorldHitQueryNativeProbe>();
            var trace = new Trace();
            WorldHitQueryNativeProbe Target(string name, Vector2 at, Vector2 size)
            {
                var target = new GameObject(name).AddComponent<WorldHitQueryNativeProbe>(); roots.Add(target.gameObject); targets.Add(target);
                target.Configure(arena, at, size, Vector2.zero, false); target.aggressive = false; target.health = target.maxHealth = 100;
                target.spawn = at; arena.enemies.Add(target); return target;
            }
            GameObject Wall(Vector2 at, Vector2 size, bool oneWay = false)
            {
                var wall = new GameObject("WorldQueryWall"); roots.Add(wall); wall.transform.position = at;
                wall.AddComponent<ArenaSurface>().oneWay = oneWay; wall.AddComponent<BoxCollider2D>().size = size; Physics2D.SyncTransforms(); return wall;
            }
            void Clear()
            {
                foreach (var target in targets) { target.OnCancel = null; arena.enemies.Remove(target); }
                targets.Clear();
                foreach (var root in roots) if (root) { root.SetActive(false); Object.DestroyImmediate(root); }
                roots.Clear(); Physics2D.SyncTransforms();
            }
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    driver.Reset(); var p = arena.Player; p.Position = new Vector2(0, 1000); p.velocity = Vector2.zero; p.SyncFacing(1);
                    foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(40, 1000); }
                    var box = new BoxData { x = 2, y = 1, width = 12, height = 12 }; var frozenBox = new SkillBoxDefinition(box);
                    Vector2 center = p.Position + new Vector2(box.x, box.y), size = new Vector2(box.width, box.height), origin = p.Center;
                    Assert.That(arena.NearestInBox(p, (BoxData)null), Is.Null); Assert.That(arena.NearestInBox(p, (SkillBoxDefinition)null), Is.Null);
                    {
                        var hidden = Target("occluded-nearest", origin + new Vector2(2, 0), Vector2.one);
                        var visible = Target("visible-farther", origin + new Vector2(3, 3), Vector2.one);
                        var wall = Wall(origin + Vector2.right, new Vector2(.04f, 1));
                        Assert.That(arena.Occluded(origin, hidden.hurtCollider.ClosestPoint(origin)), Is.True);
                        Assert.That(arena.Occluded(origin, visible.hurtCollider.ClosestPoint(origin)), Is.False);
                        Assert.That(arena.NearestInBox(p, box), Is.SameAs(visible)); Assert.That(arena.NearestInBox(p, frozenBox), Is.SameAs(visible));
                        Assert.That(arena.NearestInCircle(p, 10), Is.SameAs(visible)); Record(trace, "occluded-nearest", arena, visible, hidden, center, size);
                        wall.GetComponent<ArenaSurface>().oneWay = true;
                        Assert.That(arena.NearestInBox(p, box), Is.SameAs(hidden)); Assert.That(arena.NearestInCircle(p, 10), Is.SameAs(hidden));
                        Record(trace, "one-way-does-not-occlude", arena, hidden, hidden, center, size); Clear();
                    }
                    {
                        var lower = Target("lower-id", origin + new Vector2(2.0000005f, 0), Vector2.one);
                        var higher = Target("higher-id", origin + new Vector2(2, 0), Vector2.one); Physics2D.SyncTransforms();
                        Assert.That(lower.ActorId, Is.LessThan(higher.ActorId));
                        Assert.That(Vector2.Distance(origin, lower.Center), Is.GreaterThan(Vector2.Distance(origin, higher.Center)));
                        Assert.That(Mathf.Approximately(Vector2.Distance(origin, lower.Center), Vector2.Distance(origin, higher.Center)), Is.True);
                        Assert.That(arena.NearestInCircle(p, 10), Is.SameAs(higher), "Strictly closer wins even inside the approximate tolerance.");
                        Record(trace, "approx-strictly-closer", arena, higher, lower, center, size);
                        arena.enemies.Remove(higher); arena.enemies.Insert(0, higher);
                        Assert.That(arena.NearestInCircle(p, 10), Is.SameAs(lower), "A later approximately equal lower ID can replace the closer candidate.");
                        Record(trace, "approx-later-lower-id", arena, lower, lower, center, size);
                        lower.hurtCollider.transform.localPosition = new Vector3(0, 0, 1); higher.hurtCollider.transform.localPosition = new Vector3(0, 0, -1); Physics2D.SyncTransforms();
                        var order = arena.TargetsInBox(center, size); Assert.That(order.IndexOf(higher), Is.LessThan(order.IndexOf(lower)));
                        Assert.That(arena.NearestInBox(p, box), Is.SameAs(lower)); Assert.That(arena.NearestInBox(p, frozenBox), Is.SameAs(lower));
                        Record(trace, "native-box-order-approx", arena, lower, lower, center, size);
                        lower.Position = higher.Position; Physics2D.SyncTransforms();
                        Assert.That(arena.NearestInBox(p, box), Is.SameAs(lower)); Assert.That(arena.NearestInCircle(p, 10), Is.SameAs(lower));
                        Record(trace, "exact-equal-lower-id", arena, lower, lower, center, size); Clear();
                    }
                    {
                        var target = Target("radius-edge", origin + Vector2.right * 2, Vector2.one); Physics2D.SyncTransforms();
                        Assert.That(Vector2.Distance(p.Center, target.Center), Is.EqualTo(2)); Assert.That(arena.NearestInCircle(p, 2), Is.SameAs(target));
                        Record(trace, "radius-inclusive", arena, target, target, center, size);
                        Assert.That(arena.NearestInCircle(p, BitConverter.Int32BitsToSingle(Bits(2) - 1)), Is.Null);
                        Record(trace, "radius-outside-one-ulp", arena, null, target, center, size);
                        foreach (string kind in new[] { "player", "decoy", "dead" })
                        {
                            target.isPlayer = kind == "player"; target.isDecoy = kind == "decoy"; target.health = kind == "dead" ? 0 : 100;
                            Assert.That(arena.NearestInCircle(p, 10), Is.Null); Assert.That(arena.NearestInBox(p, box), Is.Null);
                            Record(trace, "reject-" + kind, arena, null, target, center, size);
                        }
                        target.isPlayer = target.isDecoy = false; target.health = 100; arena.enemies.Add(null);
                        try { Assert.That(arena.NearestInCircle(p, 10), Is.SameAs(target)); }
                        finally { arena.enemies.Remove(null); }
                        Clear();
                    }
                    {
                        var target = Target("large-exposed-parts", origin + new Vector2(5, 1), new Vector2(5.3f, 3.8f));
                        var wall = Wall(origin + Vector2.right * 1.5f, new Vector2(.04f, 1));
                        center = target.Center; size = new Vector2(6, 4);
                        Assert.That(arena.Occluded(origin, target.Center), Is.True); Assert.That(arena.Occluded(origin, target.hurtCollider.ClosestPoint(origin)), Is.True);
                        Assert.That(arena.Exposed(origin, target, center, size), Is.True, "The center and closest part are blocked but a large enemy corner is exposed.");
                        Record(trace, "large-enemy-corner-exposed", arena, target, target, center, size, true);
                        wall.GetComponent<BoxCollider2D>().size = new Vector2(.04f, 20); Physics2D.SyncTransforms();
                        Assert.That(arena.Exposed(origin, target, center, size), Is.False); Record(trace, "all-five-parts-occluded", arena, target, target, center, size);
                        wall.SetActive(false); Physics2D.SyncTransforms(); var bounds = target.hurtCollider.bounds;
                        size = new Vector2(1, 1); center = new Vector2(bounds.min.x - .5f, target.Center.y);
                        Assert.That(arena.Exposed(origin, target, center, size), Is.True); Record(trace, "bounds-edge-inclusive", arena, target, target, center, size, true);
                        center.x -= .0009765625f;
                        Assert.That(arena.Exposed(origin, target, center, size), Is.False); Record(trace, "bounds-separated", arena, target, target, center, size);
                        size = new Vector2(.0009765625f, .0009765625f); center = target.Center;
                        Assert.That(arena.Exposed(origin, target, center, size), Is.True); Record(trace, "narrow-intersection", arena, target, target, center, size, true); Clear();
                    }
                    {
                        var target = Target("receive-query-target", origin + Vector2.right * 2, Vector2.one);
                        var other = Target("receive-query-other", origin + Vector2.right * 4, Vector2.one); Physics2D.SyncTransforms();
                        center = p.Position + new Vector2(box.x, box.y); size = new Vector2(box.width, box.height);
                        int callbacks = 0; target.OnCancel = () =>
                        {
                            callbacks++; Assert.That(target.ReceivedHitCount, Is.EqualTo(1));
                            Assert.That(arena.NearestInBox(p, box), Is.SameAs(target)); Assert.That(arena.NearestInCircle(p, 10), Is.SameAs(target));
                            Record(trace, "receive-query-before-edit", arena, target, target, center, size, callbacks: callbacks);
                            target.isDecoy = true; p.Position += Vector2.right * 3; Physics2D.SyncTransforms();
                            Assert.That(arena.NearestInBox(p, frozenBox), Is.SameAs(other)); Assert.That(arena.NearestInCircle(p, 10), Is.SameAs(other));
                            Record(trace, "receive-query-after-owner-edit", arena, other, target, center, size, callbacks: callbacks);
                        };
                        var source = new SkillData { id = "world-query-native-consumer", activation = 0, end = 1, vertical = "normal", facing = "windup_start",
                            invulStart = Array.Empty<float>(), invulEnd = Array.Empty<float>(),
                            hits = new[] { new HitData { time = 0, active = .125f, damage = 7, stun = .25f, anchor = "actor", targets = "nearest", box = box } } };
                        var cast = CombatCast.Create(arena, p, source, p.Weapon, false); p.currentCast = cast; cast.BeforeMovement(0); cast.Step(.02f);
                        Assert.That(callbacks, Is.EqualTo(1)); Assert.That(target.ReceivedHitCount, Is.EqualTo(1)); Assert.That(target.health, Is.EqualTo(93));
                        Assert.That(other.ReceivedHitCount, Is.Zero); Record(trace, "native-cast-selected-only-one", arena, target, target, center, size, callbacks: callbacks);
                        p.CancelAction(); Clear();
                    }
                    Assert.That(arena.SimulationFaulted, Is.False);
                }
                Assert.That(trace.rows.Count, Is.EqualTo(19));
                var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-worldHitQueryTraceRoot");
                if (index >= 0 && index + 1 < args.Length) { Directory.CreateDirectory(args[index + 1]); File.WriteAllText(Path.Combine(args[index + 1], label + ".json"), JsonUtility.ToJson(trace)); }
            }
            finally { Clear(); }
        }
    }
}
