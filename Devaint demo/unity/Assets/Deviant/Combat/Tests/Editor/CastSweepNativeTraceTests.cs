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
    public sealed class CastSweepNativeProbe : CombatActor
    {
        public Action OnCancel;
        public override void CancelAttack() { OnCancel?.Invoke(); base.CancelAttack(); }
    }

    // The same fixture runs against the pre-extraction Native consumer. It
    // observes existing geometry, damage callbacks and bounds, not a new port.
    public class CastSweepNativeTraceTests
    {
        [Serializable] sealed class Row
        {
            public string scenario, phase, order;
            public int hits, otherHits, thirdHits, callbacks, casts, available;
            public bool current, resources, faulted;
            public int[] bits, bounds;
        }
        [Serializable] sealed class Trace { public List<Row> rows = new List<Row>(); }
        static readonly FieldInfo DebugBoxes = typeof(CombatCast).GetField("debugBoxes", BindingFlags.Instance | BindingFlags.NonPublic);
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        [UnityTest] public IEnumerator GameSweepKeepsNativeOrderingGeometryAndCallbacks() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "game");
        [UnityTest] public IEnumerator LabSweepKeepsNativeOrderingGeometryAndCallbacks() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "lab");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        static BoxCollider2D Surface(List<GameObject> objects, string name, Vector2 center, Vector2 size, bool oneWay = false)
        {
            var go = new GameObject(name); objects.Add(go); go.transform.position = center;
            go.AddComponent<ArenaSurface>().oneWay = oneWay; var box = go.AddComponent<BoxCollider2D>(); box.size = size; return box;
        }
        static void Record(Trace trace, string scenario, string phase, CombatArena arena, CombatCast cast,
            CastSweepNativeProbe target, CastSweepNativeProbe other, CastSweepNativeProbe third, List<int> order, int callbacks)
        {
            var p = arena.Player; var boxes = (List<Bounds>)DebugBoxes.GetValue(cast); var bounds = new List<int>();
            foreach (var box in boxes)
            { bounds.Add(Bits(box.center.x)); bounds.Add(Bits(box.center.y)); bounds.Add(Bits(box.size.x)); bounds.Add(Bits(box.size.y)); }
            trace.rows.Add(new Row { scenario = scenario, phase = phase, order = string.Join(",", order), hits = target.ReceivedHitCount,
                otherHits = other.ReceivedHitCount, thirdHits = third.ReceivedHitCount, callbacks = callbacks, casts = arena.casts.Count,
                available = p.Weapon.Cooldown.Available, current = ReferenceEquals(p.currentCast, cast), resources = cast.ResourcesActive,
                faulted = arena.SimulationFaulted, bounds = bounds.ToArray(),
                bits = new[] { Bits(arena.Now), Bits(p.Position.x), Bits(p.Position.y), Bits(p.Center.x), Bits(p.Center.y), Bits(p.health),
                    Bits(target.Position.x), Bits(target.Position.y), Bits(target.health), Bits(other.Position.x), Bits(other.Position.y), Bits(other.health),
                    Bits(third.Position.x), Bits(third.Position.y), Bits(third.health), Bits(cast.StartedAt), Bits(cast.Age), Bits(cast.EndTime),
                    Bits(p.Weapon.Cooldown.NextReady), Bits(cast.ResourceResult?.Snapshot?.DamageMultiplier ?? -1) } });
        }
        static SkillData Setup(CombatArena arena, CombatArena.ManualSimulation driver, int facing,
            CastSweepNativeProbe target, CastSweepNativeProbe other, CastSweepNativeProbe third)
        {
            target.OnCancel = other.OnCancel = third.OnCancel = null; driver.Reset(); var p = arena.Player;
            p.selected = 0; p.InputBlocked = false;
            foreach (var weapon in p.weapons) weapon.StatSlots.Clear();
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(40, 1000); enemy.velocity = Vector2.zero; }
            Assert.That(p.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, form => form.id == "dagger"), 0, out var error), Is.True, error);
            p.Position = new Vector2(0, 1000); p.velocity = Vector2.zero; p.grounded = true; p.SyncFacing(facing);
            var actors = new[] { target, other, third };
            for (int i = 0; i < actors.Length; i++)
            {
                var actor = actors[i]; actor.isDecoy = actor.isPlayer = false; actor.ResetActor(new Vector2((i + 1) * facing, 1000));
                actor.health = 1000; actor.offset = new Vector2(0, 1); actor.SyncFacing(-facing);
            }
            Physics2D.SyncTransforms();
            return new SkillData { id = "cast_sweep_native", name = "Native sweep fixture", activation = 0, end = 2, facing = "locked", vertical = "normal",
                invulStart = Array.Empty<float>(), invulEnd = Array.Empty<float>(),
                hits = new[] { new HitData { time = 0, damage = 5, active = .5f, stun = .01f, anchor = "actor", targets = "all",
                    box = new BoxData { x = 2, y = 1, width = 6, height = 2 } } } };
        }
        static CombatCast Start(CombatArena arena, SkillData source)
        {
            var cast = CombatCast.Create(arena, arena.Player, source, arena.Player.Weapon, false); arena.Player.currentCast = cast; return cast;
        }
        static void Run(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver, string scenario, int facing,
            CastSweepNativeProbe target, CastSweepNativeProbe other, CastSweepNativeProbe third)
        {
            var source = Setup(arena, driver, facing, target, other, third); var p = arena.Player;
            var objects = new List<GameObject>(); var order = new List<int>(); int callbacks = 0; string label = scenario + "/" + facing;
            bool callback = scenario.StartsWith("callback-", StringComparison.Ordinal);
            if (scenario == "distance-order") { target.Position = new Vector2(3 * facing, 1000); third.Position = new Vector2(1 * facing, 1000); }
            if (scenario == "id-tie") { target.Position = new Vector2(-facing, 1000); other.Position = new Vector2(facing, 1000); third.Position = new Vector2(40, 1000); p.offset.x = 0; p.SyncFacing(facing); }
            if (scenario == "tiny-distance-difference") { target.Position = new Vector2(1.00000048f * facing, 1000); other.Position = new Vector2(facing, 1000); third.Position = new Vector2(40, 1000); p.offset.x = 0; p.SyncFacing(facing); }
            if (scenario == "late-candidate") other.Position = new Vector2(40, 1000);
            if (scenario == "swept-path") { source.hits[0].box.width = .25f; source.hits[0].box.x = 0; third.Position = new Vector2(4 * facing, 1000); }
            if (scenario == "nearest" || scenario == "marked-leaves") source.hits[0].targets = "nearest";
            if (scenario == "null-box") source.hits[0].box = null;
            if (scenario == "excluded") { target.isDecoy = true; other.isPlayer = true; third.health = 0; }
            if (scenario == "duplicate")
            {
                var go = new GameObject("SweepTraceDuplicate"); objects.Add(go); go.transform.SetParent(target.hurtCollider.transform, false);
                var duplicate = go.AddComponent<BoxCollider2D>(); duplicate.size = target.hurtCollider.size; duplicate.offset = target.hurtCollider.offset;
                duplicate.isTrigger = true; go.AddComponent<HurtRegion>().actor = target;
            }
            if (scenario == "wall" || scenario == "partial-exposure" || scenario == "one-way")
            {
                target.Position = new Vector2(2 * facing, 1000); other.Position = new Vector2(3 * facing, 1000); third.Position = new Vector2(4 * facing, 1000);
                Surface(objects, "SweepTraceWall", new Vector2(1 * facing, scenario == "partial-exposure" ? 1000.5f : 1001),
                    new Vector2(.02f, scenario == "partial-exposure" ? .6f : 4), scenario == "one-way");
            }
            Physics2D.SyncTransforms(); var cast = Start(arena, source);
            target.OnCancel = () => Observe(1); other.OnCancel = () => Observe(2); third.OnCancel = () => Observe(3);
            void Observe(int actor)
            {
                order.Add(actor); callbacks++; Record(trace, label, "receive-" + actor, arena, cast, target, other, third, order, callbacks);
                if (!callback || callbacks != 1) return;
                if (scenario == "callback-cancel") p.CancelAction();
                if (scenario == "callback-move") { other.Position = new Vector2(40, 1000); other.isDecoy = true; Physics2D.SyncTransforms(); }
                if (scenario == "callback-reentry") cast.Step(.125f);
                if (scenario == "callback-throw") throw new InvalidOperationException("Sweep Native receive boundary");
            }
            try
            {
                Record(trace, label, "start", arena, cast, target, other, third, order, callbacks);
                cast.BeforeMovement(.125f);
                if (scenario == "swept-path") { p.Position = new Vector2(4 * facing, 1000); Physics2D.SyncTransforms(); }
                if (scenario == "callback-throw")
                {
                    Assert.Throws<InvalidOperationException>(() => cast.Step(.125f)); Assert.That(order, Is.EqualTo(new[] { 1 }));
                    Record(trace, label, "thrown", arena, cast, target, other, third, order, callbacks);
                    target.OnCancel = null; cast.Step(.125f);
                }
                else cast.Step(.125f);
                Record(trace, label, "returned", arena, cast, target, other, third, order, callbacks);
                if (scenario == "late-candidate")
                {
                    Assert.That(other.ReceivedHitCount, Is.Zero); other.Position = new Vector2(2 * facing, 1000); Physics2D.SyncTransforms(); cast.Step(.125f);
                }
                if (scenario == "marked-leaves")
                {
                    Assert.That(target.ReceivedHitCount, Is.EqualTo(1)); target.Position = new Vector2(40, 1000); Physics2D.SyncTransforms(); cast.Step(.125f);
                }
                if (scenario == "wall" || scenario == "excluded" || scenario == "null-box") Assert.That(callbacks, Is.Zero, label);
                else if (scenario == "id-tie") Assert.That(order, Is.EqualTo(new[] { 1, 2 }), label);
                else if (scenario == "tiny-distance-difference") Assert.That(order, Is.EqualTo(new[] { 2, 1 }), label);
                else if (scenario == "distance-order" || scenario == "swept-path") Assert.That(order, Is.EqualTo(new[] { 3, 2, 1 }), label);
                else if (scenario == "nearest" || scenario == "marked-leaves") Assert.That(order, Is.EqualTo(new[] { 1 }), label);
                else if (scenario == "late-candidate") Assert.That(order, Is.EqualTo(new[] { 1, 3, 2 }), label);
                else Assert.That(order, Is.EqualTo(new[] { 1, 2, 3 }), label);
                if (scenario == "callback-move") Assert.That(other.ReceivedHitCount, Is.EqualTo(1), "An entered candidate is not filtered again after another Receive callback.");
                cast.Step(.125f); Record(trace, label, "repeated", arena, cast, target, other, third, order, callbacks);
                Assert.That(arena.SimulationFaulted, Is.False, label);
            }
            finally
            {
                target.OnCancel = other.OnCancel = third.OnCancel = null;
                foreach (var go in objects) if (go) { go.SetActive(false); Object.Destroy(go); }
                Physics2D.SyncTransforms();
            }
        }
        static IEnumerator Exercise(string scene, string label)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); Assert.That(Application.isPlaying, Is.True); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var trace = new Trace(); Vector2 playerOffset = arena.Player.offset;
            var target = new GameObject("SweepTraceTarget").AddComponent<CastSweepNativeProbe>();
            var other = new GameObject("SweepTraceOther").AddComponent<CastSweepNativeProbe>();
            var third = new GameObject("SweepTraceThird").AddComponent<CastSweepNativeProbe>();
            foreach (var actor in new[] { target, other, third })
            { actor.Configure(arena, new Vector2(40, 1000), new Vector2(.25f, 2), new Vector2(0, 1), false); actor.maxHealth = 1000; }
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                foreach (string scenario in new[] { "stationary", "distance-order", "id-tie", "tiny-distance-difference", "swept-path", "duplicate", "wall",
                    "partial-exposure", "one-way", "excluded", "null-box", "nearest", "marked-leaves", "late-candidate", "callback-cancel", "callback-move", "callback-reentry", "callback-throw" })
                foreach (int facing in new[] { 1, -1 })
                {
                    arena.Player.offset = playerOffset; Run(trace, arena, driver, scenario, facing, target, other, third);
                }
                var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-castSweepTraceRoot");
                if (at >= 0 && at + 1 < args.Length)
                { Directory.CreateDirectory(args[at + 1]); File.WriteAllText(Path.Combine(args[at + 1], label + ".json"), JsonUtility.ToJson(trace)); }
            }
            finally
            {
                arena.Player.offset = playerOffset; target.OnCancel = other.OnCancel = third.OnCancel = null;
                Object.Destroy(target.gameObject); Object.Destroy(other.gameObject); Object.Destroy(third.gameObject);
            }
        }
    }
}
