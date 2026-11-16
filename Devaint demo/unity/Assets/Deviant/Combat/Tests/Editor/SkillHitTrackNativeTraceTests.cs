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
    public sealed class SkillHitTrackNativeProbe : CombatActor
    {
        public Action OnCancel;
        public override void CancelAttack() { OnCancel?.Invoke(); base.CancelAttack(); }
    }

    // Captured before the extraction, using the old public Cast/Actor API and
    // actual Native geometry. No execution-owner state is observed here.
    public class SkillHitTrackNativeTraceTests
    {
        [Serializable] sealed class Row
        {
            public string scenario, phase;
            public int tick, hits, otherHits, casts, available, status, callbacks, facing;
            public bool current, listed, resources, faulted;
            public int[] bits;
        }
        [Serializable] sealed class Trace { public List<Row> rows = new List<Row>(); }
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        [UnityTest] public IEnumerator GamePreservesDirectAndIndependentHitTracks() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "game");
        [UnityTest] public IEnumerator LabPreservesDirectAndIndependentHitTracks() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "lab");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static void Record(Trace trace, string scenario, string phase, int tick, float step, CombatArena arena, CombatCast cast,
            SkillHitTrackNativeProbe target, SkillHitTrackNativeProbe other, int callbacks = 0)
        {
            var p = arena.Player; var receipt = cast.ResourceResult;
            trace.rows.Add(new Row { scenario = scenario, phase = phase, tick = tick, hits = target.ReceivedHitCount,
                otherHits = other.ReceivedHitCount, casts = arena.casts.Count, available = p.weapons[0].Cooldown.Available,
                status = receipt == null ? -1 : (int)receipt.Status, callbacks = callbacks, facing = p.facing,
                current = ReferenceEquals(p.currentCast, cast), listed = arena.casts.Contains(cast), resources = cast.ResourcesActive,
                faulted = arena.SimulationFaulted,
                bits = new[] { Bits(arena.Now), Bits(step), Bits(p.Position.x), Bits(p.Position.y), Bits(p.velocity.x), Bits(p.velocity.y),
                    Bits(target.Position.x), Bits(target.Position.y), Bits(target.health), Bits(target.rootUntil), Bits(target.stunUntil),
                    Bits(other.Position.x), Bits(other.Position.y), Bits(other.health), Bits(other.rootUntil), Bits(other.stunUntil),
                    Bits(cast.StartedAt), Bits(cast.Age), Bits(cast.ImpactTime), Bits(cast.MotionDuration), Bits(cast.EndTime),
                    Bits(p.weapons[0].Cooldown.NextReady), Bits(receipt?.Snapshot?.ActivatedAt ?? -1), Bits(receipt?.Snapshot?.DamageMultiplier ?? -1) } });
        }
        static SkillData Setup(CombatArena arena, CombatArena.ManualSimulation driver, string form, int facing,
            SkillHitTrackNativeProbe target, SkillHitTrackNativeProbe other)
        {
            target.OnCancel = other.OnCancel = null; driver.Reset(); var p = arena.Player; p.selected = 0; p.InputBlocked = false;
            foreach (var weapon in p.weapons) weapon.StatSlots.Clear();
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(40, 1000); enemy.velocity = Vector2.zero; }
            Assert.That(p.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, item => item.id == form), 3, out var error), Is.True, error);
            p.Position = new Vector2(0, 1000); p.velocity = Vector2.zero; p.grounded = true; p.SyncFacing(facing);
            target.ResetActor(new Vector2((form == "greatsword" ? 3 : 1.4f) * facing, 1000)); target.health = 1000; target.SyncFacing(-facing);
            other.ResetActor(new Vector2((form == "greatsword" ? 3.7f : 1.8f) * facing, 1000)); other.health = 1000; other.SyncFacing(-facing);
            Physics2D.SyncTransforms(); return JsonUtility.FromJson<SkillData>(JsonUtility.ToJson(p.Special));
        }
        static CombatCast Start(CombatArena arena, SkillData source)
        {
            var cast = CombatCast.Create(arena, arena.Player, source, arena.Player.Weapon, false); arena.Player.currentCast = cast; return cast;
        }
        static void CatalogCases(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver,
            SkillHitTrackNativeProbe target, SkillHitTrackNativeProbe other)
        {
            foreach (string form in new[] { "dagger", "base_sword", "greatsword", "whip" })
            foreach (int facing in new[] { 1, -1 })
            foreach (string scenario in new[] { "normal", "coarse", "variable", "cancel-before", "cancel-after", "move-after" })
            {
                var source = Setup(arena, driver, form, facing, target, other); var p = arena.Player; var cast = Start(arena, source);
                string label = form + "/" + scenario + "/" + facing; bool changed = false;
                Record(trace, label, "start", -1, 0, arena, cast, target, other);
                for (int tick = 0; tick < 80; tick++)
                {
                    if (!changed && (scenario == "cancel-before" && tick == 0 ||
                        (scenario == "cancel-after" || scenario == "move-after") && cast.ResourceResult?.Status == ActionResourceStatus.Succeeded))
                    {
                        changed = true;
                        if (scenario == "move-after")
                        {
                            p.Position = new Vector2(-8 * facing, 1000);
                            if (form == "whip") { target.Position = new Vector2(20 * facing, 1000); other.Position = new Vector2(22 * facing, 1000); }
                            Physics2D.SyncTransforms();
                        }
                        else p.CancelAction();
                        Record(trace, label, "command", tick, 0, arena, cast, target, other);
                    }
                    float step = scenario == "coarse" && tick == 0 ? 1.125f : scenario == "variable" ? (tick % 3 == 0 ? .007f : tick % 3 == 1 ? .083f : .031f) : .02f;
                    driver.Step(step); Record(trace, label, "step", tick, step, arena, cast, target, other);
                }
                Assert.That(arena.SimulationFaulted, Is.False, label); Assert.That(p.currentCast, Is.Null, label);
                Assert.That(cast.ResourcesActive, Is.False, label);
                if (scenario == "cancel-before")
                { Assert.That(cast.ResourceResult, Is.Null, label); Assert.That(target.ReceivedHitCount + other.ReceivedHitCount, Is.Zero, label); }
                else
                {
                    Assert.That(cast.ResourceResult.Status, Is.EqualTo(ActionResourceStatus.Succeeded), label);
                    int expected = form == "dagger" ? (scenario == "cancel-after" || scenario == "move-after" ? 1 : 6) : form == "greatsword" ? 1 : 2;
                    Assert.That(target.ReceivedHitCount, Is.EqualTo(expected), label);
                    Assert.That(other.ReceivedHitCount, Is.EqualTo(form == "whip" ? 0 : expected), label);
                    if (form == "whip") Assert.That(target.health, Is.EqualTo(955), label);
                    if (form == "greatsword") Assert.That(target.health, Is.EqualTo(940), label);
                }
            }
        }
        static HitData Hit(float time, float damage, string anchor, float stun = 0) => new HitData {
            time = time, damage = damage, active = .125f, anchor = anchor, targets = "all", stun = stun,
            box = new BoxData { x = 1.5f, y = 1, width = 3, height = 2 } };
        static void CallbackCases(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver,
            SkillHitTrackNativeProbe target, SkillHitTrackNativeProbe other)
        {
            foreach (string scenario in new[] { "cancel", "reentry", "throw", "clock-change" })
            {
                var source = Setup(arena, driver, "dagger", 1, target, other); source.activation = 0; source.end = 2;
                float followTime = scenario == "clock-change" ? .375f : .03125f;
                source.hits = new[] { Hit(0, 5, "actor", .01f), Hit(followTime, 7, "actor"), Hit(followTime, 11, "saved_primary_world_pose") };
                var cast = Start(arena, source); int callbacks = 0;
                target.OnCancel = () =>
                {
                    callbacks++; Record(trace, scenario, "receive", 0, .125f, arena, cast, target, other, callbacks);
                    if (callbacks != 1) return;
                    if (scenario == "cancel") arena.Player.CancelAction();
                    if (scenario == "reentry") cast.Step(.125f);
                    if (scenario == "throw") throw new InvalidOperationException("Hit track Native receive boundary");
                    if (scenario == "clock-change")
                    {
                        // Advance the real arena clock without recursively scheduling
                        // this cast; the entered hit pass must read that clock again.
                        arena.casts.Remove(cast); arena.Player.currentCast = null;
                        try { driver.Step(.5f); }
                        finally { arena.casts.Add(cast); arena.Player.currentCast = cast; }
                        Record(trace, scenario, "clock-advanced", 0, .125f, arena, cast, target, other, callbacks);
                    }
                };
                try
                {
                    cast.BeforeMovement(.125f); Record(trace, scenario, "prepared", 0, .125f, arena, cast, target, other);
                    if (scenario == "throw")
                    {
                        Assert.Throws<InvalidOperationException>(() => cast.Step(.125f)); target.OnCancel = null;
                        Assert.That(target.ReceivedHitCount, Is.EqualTo(1)); Assert.That(other.ReceivedHitCount, Is.Zero);
                        Record(trace, scenario, "throw", 0, .125f, arena, cast, target, other, callbacks); cast.Step(.125f);
                    }
                    else cast.Step(.125f);
                    int expected = scenario == "cancel" ? 2 : 3;
                    Assert.That(target.ReceivedHitCount, Is.EqualTo(expected), scenario);
                    Assert.That(other.ReceivedHitCount, Is.EqualTo(expected), scenario);
                    Assert.That(target.health, Is.EqualTo(scenario == "cancel" ? 984 : 977), scenario);
                    Assert.That(other.health, Is.EqualTo(target.health), scenario);
                    Assert.That(callbacks, Is.EqualTo(1), scenario);
                    Record(trace, scenario, "returned", 0, .125f, arena, cast, target, other, callbacks);
                    target.OnCancel = null; cast.Step(.125f);
                    Assert.That(target.ReceivedHitCount, Is.EqualTo(expected)); Assert.That(other.ReceivedHitCount, Is.EqualTo(expected));
                    Record(trace, scenario, "repeated", 1, .125f, arena, cast, target, other, callbacks);
                    Assert.That(arena.SimulationFaulted, Is.False);
                }
                finally { target.OnCancel = null; }
            }
        }
        static void LateAndMarkedCases(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver,
            SkillHitTrackNativeProbe target, SkillHitTrackNativeProbe other)
        {
            var source = Setup(arena, driver, "dagger", 1, target, other); source.activation = 0; source.end = 2;
            source.hits = new[] { Hit(0, 5, "actor") }; source.hits[0].active = .5f;
            other.Position = new Vector2(40, 1000); Physics2D.SyncTransforms(); var cast = Start(arena, source);
            cast.BeforeMovement(.125f); cast.Step(.125f); driver.Step(.125f);
            other.Position = new Vector2(1.8f, 1000); Physics2D.SyncTransforms(); cast.Step(.125f);
            Assert.That(target.ReceivedHitCount, Is.EqualTo(1)); Assert.That(other.ReceivedHitCount, Is.EqualTo(1));
            Record(trace, "late-entrant", "entered", 1, .125f, arena, cast, target, other);
            foreach (string scenario in new[] { "dead-mark", "missed-primary", "unactivated" })
            {
                source = Setup(arena, driver, "whip", 1, target, other); cast = Start(arena, source);
                if (scenario == "unactivated") { cast.Cancel(); cast.Step(1); }
                else
                {
                    cast.BeforeMovement(.25f);
                    if (scenario == "missed-primary") { target.Position = new Vector2(40, 1000); other.Position = new Vector2(42, 1000); Physics2D.SyncTransforms(); }
                    cast.Step(.25f);
                    if (scenario == "dead-mark") target.health = 0;
                    driver.Step(1);
                }
                Assert.That(target.ReceivedHitCount, Is.EqualTo(scenario == "dead-mark" ? 1 : 0), scenario);
                Assert.That(other.ReceivedHitCount, Is.Zero, scenario);
                Record(trace, scenario, "end", 1, 1, arena, cast, target, other);
            }
        }
        static IEnumerator Exercise(string scene, string label)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); Assert.That(Application.isPlaying, Is.True); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var trace = new Trace();
            var floor = new GameObject("SkillHitTrackNativeFloor"); floor.transform.position = new Vector3(0, 999.5f, 0);
            floor.AddComponent<ArenaSurface>().oneWay = true; floor.AddComponent<BoxCollider2D>().size = new Vector2(200, 1);
            var target = new GameObject("SkillHitTrackNativeTarget").AddComponent<SkillHitTrackNativeProbe>();
            var other = new GameObject("SkillHitTrackNativeOther").AddComponent<SkillHitTrackNativeProbe>();
            target.Configure(arena, new Vector2(40, 1000), new Vector2(.5f, 1.8f), new Vector2(0, .9f), false);
            other.Configure(arena, new Vector2(42, 1000), new Vector2(.5f, 1.8f), new Vector2(0, .9f), false);
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    CatalogCases(trace, arena, driver, target, other); CallbackCases(trace, arena, driver, target, other);
                    LateAndMarkedCases(trace, arena, driver, target, other); Assert.That(arena.SimulationFaulted, Is.False);
                }
                var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-skillHitTrackTraceRoot");
                if (at >= 0 && at + 1 < args.Length)
                { Directory.CreateDirectory(args[at + 1]); File.WriteAllText(Path.Combine(args[at + 1], label + ".json"), JsonUtility.ToJson(trace)); }
            }
            finally { target.OnCancel = other.OnCancel = null; Object.Destroy(target.gameObject); Object.Destroy(other.gameObject); Object.Destroy(floor); }
        }
    }
}
