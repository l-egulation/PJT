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
    public sealed class ApproachMarkOrderProbe : CombatActor
    {
        public Action OnCancel;
        public override void CancelAttack() { OnCancel?.Invoke(); base.CancelAttack(); }
    }

    // Frozen against the old public Cast/Player API before extracting recast
    // state. The inherited all-skill trace supplies broad catalogue coverage;
    // these two-scene traces concentrate on recast boundaries and Native order.
    public class ApproachRecastNativeTraceTests
    {
        [Serializable] sealed class Row
        {
            public string scenario, phase, action;
            public int tick, hits, charges, slot, casts;
            public bool listed, current, approaching, moving, freeze, resources, targetAlive, faulted;
            public int[] bits;
        }
        [Serializable] sealed class Trace { public List<Row> rows = new List<Row>(); }
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        [UnityTest] public IEnumerator GameApproachRecastNativeTrace() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "game");
        [UnityTest] public IEnumerator LabApproachRecastNativeTrace() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "lab");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        static void Record(Trace trace, string scenario, string phase, int tick, CombatArena arena, CombatCast cast, CombatActor target)
        {
            var p = arena.Player;
            trace.rows.Add(new Row { scenario = scenario, phase = phase, tick = tick, action = p.currentCast ? p.currentCast.name : "",
                hits = target ? target.ReceivedHitCount : -1, charges = p.weapons[0].Cooldown.Available, slot = p.selected, casts = arena.casts.Count,
                listed = arena.casts.Contains(cast), current = ReferenceEquals(p.currentCast, cast), approaching = cast && cast.Approaching,
                moving = cast && cast.Moving, freeze = cast && cast.FreezeVertical, resources = cast && cast.ResourcesActive,
                targetAlive = target && !target.Dead, faulted = arena.SimulationFaulted,
                bits = new[] { Bits(arena.Now), Bits(p.Position.x), Bits(p.Position.y), Bits(p.velocity.x), Bits(p.velocity.y),
                    Bits(cast ? cast.RecastRemaining : 0), Bits(p.ApproachRemaining), Bits(cast ? cast.EndTime : 0), Bits(cast ? cast.Age : 0),
                    Bits(cast ? cast.DashVisualDuration : 0), Bits(target ? target.Position.x : 0), Bits(target ? target.Position.y : 0),
                    Bits(target ? target.health : 0), Bits(target ? target.rootUntil : 0), Bits(p.weapons[0].Cooldown.NextReady) } });
        }
        static ApproachMarkOrderProbe Setup(CombatArena arena, CombatArena.ManualSimulation driver, int facing, float x = 3.5f)
        {
            driver.Reset(); var p = arena.Player; p.selected = 0; p.InputBlocked = false;
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(40, 1000); enemy.velocity = Vector2.zero; }
            foreach (var weapon in p.weapons) weapon.StatSlots.Clear();
            int whip = Array.FindIndex(arena.catalog.forms, f => f.id == "whip");
            Assert.That(p.TryReconfigure(0, whip, 2, out var error), Is.True, error);
            Assert.That(p.TryReconfigure(1, whip, 2, out error), Is.True, error);
            p.Position = new Vector2(0, 1000); p.velocity = Vector2.zero; p.grounded = true; p.SyncFacing(facing);
            var target = new GameObject("ApproachNativeTarget").AddComponent<ApproachMarkOrderProbe>();
            target.Configure(arena, new Vector2(x * facing, 1000), new Vector2(.5f, 1.8f), new Vector2(0, .9f), false);
            target.health = 1000; target.SyncFacing(-facing); Physics2D.SyncTransforms(); return target;
        }
        static SkillData Source(CombatArena arena) => JsonUtility.FromJson<SkillData>(JsonUtility.ToJson(arena.Player.Special));
        static CombatCast Start(CombatArena arena, SkillData source = null)
        {
            var p = arena.Player; var cast = CombatCast.Create(arena, p, source ?? Source(arena), p.Weapon, false); p.currentCast = cast; return cast;
        }
        static void Hit(CombatArena arena, CombatArena.ManualSimulation driver, CombatCast cast, CombatActor target)
        {
            for (int i = 0; i < 20 && cast.RecastRemaining <= 0; i++) driver.Step(.02f);
            Assert.That(target.ReceivedHitCount, Is.EqualTo(1)); Assert.That(cast.RecastRemaining, Is.GreaterThan(0));
        }
        static void FinishPrimary(CombatArena arena, CombatArena.ManualSimulation driver, CombatCast cast)
        {
            for (int i = 0; i < 40 && arena.Player.currentCast; i++) driver.Step(.02f);
            Assert.That(arena.Player.currentCast, Is.Null); Assert.That(arena.casts.Contains(cast), Is.True, "The mark keeps the finished primary cast alive.");
        }
        static void Remove(CombatActor target)
        {
            if (!target) return; if (target is ApproachMarkOrderProbe probe) probe.OnCancel = null;
            target.gameObject.SetActive(false); Object.Destroy(target.gameObject); Physics2D.SyncTransforms();
        }
        static void FirstHitAndInput(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver)
        {
            var target = Setup(arena, driver, 1); var p = arena.Player; var source = Source(arena); source.hits[0].stun = .03125f;
            var cast = Start(arena, source); bool observedReceive = false;
            target.OnCancel = () =>
            {
                observedReceive = true; Assert.That(target.ReceivedHitCount, Is.EqualTo(1));
                Assert.That(cast.RecastRemaining, Is.Zero, "Receive callbacks run before the accepted-hit mark is assigned.");
                Record(trace, "fresh-input", "receive", -1, arena, cast, target);
            };
            try
            {
                Assert.That(cast.TryApproachRecast(p.Weapon), Is.False); Record(trace, "fresh-input", "before-hit", -1, arena, cast, target);
                driver.Step(.02f, new CombatInput(0, CombatButtons.Special, true));
                for (int i = 0; i < 30; i++) driver.Step(.02f, new CombatInput(0, CombatButtons.None, true));
                Assert.That(observedReceive, Is.True); Assert.That(cast.Approaching, Is.False); Assert.That(p.currentCast, Is.Null);
                Assert.That(cast.RecastRemaining, Is.GreaterThan(0), "An early press/continued hold does not queue recast.");
                Record(trace, "fresh-input", "held-after-primary", 30, arena, cast, target);
                float root = target.rootUntil, ready = p.Weapon.Cooldown.NextReady; int charges = p.Weapon.Cooldown.Available;
                driver.Step(.02f, new CombatInput(0, CombatButtons.Special, true));
                Assert.That(p.currentCast, Is.SameAs(cast)); Assert.That(cast.Approaching, Is.True);
                Assert.That(target.rootUntil, Is.EqualTo(root)); Assert.That(p.Weapon.Cooldown.NextReady, Is.EqualTo(ready)); Assert.That(p.Weapon.Cooldown.Available, Is.EqualTo(charges));
                Record(trace, "fresh-input", "new-press", 31, arena, cast, target);
            }
            finally { Remove(target); }

            target = Setup(arena, driver, 1, 40); cast = Start(arena);
            try
            {
                for (int i = 0; i < 32; i++) driver.Step(.02f);
                Assert.That(target.ReceivedHitCount, Is.Zero); Assert.That(cast.RecastRemaining, Is.Zero); Assert.That(cast.TryApproachRecast(p.Weapon), Is.False);
                Record(trace, "first-hit-required", "miss", 32, arena, cast, target);
            }
            finally { Remove(target); }
        }
        static void WindowAndSelection(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver)
        {
            foreach (bool exact in new[] { false, true })
            {
                var target = Setup(arena, driver, 1); var p = arena.Player; var source = Source(arena);
                source.activation = source.hits[0].time = .125f; source.movement.recastWindow = 1;
                var cast = Start(arena, source);
                try
                {
                    driver.Step(.125f); driver.Step(.375f);
                    Assert.That(p.currentCast, Is.Null); Assert.That(arena.casts.Contains(cast), Is.True);
                    driver.Step(exact ? .5f : .4990234375f);
                    string label = exact ? "exact-expiry" : "late-input"; Record(trace, label, "deadline", 0, arena, cast, target);
                    Assert.That(cast.TryApproachRecast(p.Weapon), Is.EqualTo(!exact));
                    if (exact)
                    {
                        Assert.That(arena.Now, Is.EqualTo(1)); Assert.That(cast.RecastRemaining, Is.Zero); driver.Step(.125f);
                        Assert.That(cast.TryApproachRecast(p.Weapon), Is.False);
                    }
                    Record(trace, label, "attempt", 1, arena, cast, target);
                }
                finally { Remove(target); }
            }
            var selectedTarget = Setup(arena, driver, 1); var player = arena.Player; var selectedCast = Start(arena);
            try
            {
                Hit(arena, driver, selectedCast, selectedTarget); FinishPrimary(arena, driver, selectedCast);
                float remaining = selectedCast.RecastRemaining;
                Assert.That(selectedCast.RecastTimeFor(player.weapons[1]), Is.Zero); Assert.That(selectedCast.TryApproachRecast(player.weapons[1]), Is.False);
                Assert.That(selectedCast.RecastRemaining, Is.EqualTo(remaining));
                player.InputBlocked = true; driver.Step(.02f, new CombatInput(0, CombatButtons.Special));
                Assert.That(selectedCast.Approaching, Is.False); Record(trace, "selection", "input-disabled", 0, arena, selectedCast, selectedTarget);
                player.InputBlocked = false;
                var disabled = player.Special; bool oldUnavailable = disabled.unavailable;
                try
                {
                    disabled.unavailable = true;
                    Assert.That(selectedCast.TryApproachRecast(player.Weapon), Is.True, "Existing same-weapon recast is independent of new-skill availability and spent charge.");
                }
                finally { disabled.unavailable = oldUnavailable; }
                Record(trace, "selection", "same-instance", 1, arena, selectedCast, selectedTarget);
            }
            finally { player.InputBlocked = false; Remove(selectedTarget); }
        }
        static void MotionCases(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver)
        {
            foreach (string scenario in new[] { "moving", "lost", "dead", "blocked", "partial-sweep", "budget" })
            foreach (int facing in new[] { 1, -1 })
            {
                var target = Setup(arena, driver, facing); var p = arena.Player; var cast = Start(arena); GameObject wall = null;
                try
                {
                    Hit(arena, driver, cast, target); FinishPrimary(arena, driver, cast);
                    if (scenario == "budget") target.Position = new Vector2(20 * facing, 1000);
                    if (scenario == "partial-sweep") target.Position = new Vector2(6 * facing, 1002);
                    if (scenario == "blocked" || scenario == "partial-sweep")
                    {
                        wall = new GameObject("ApproachNativeWall"); wall.AddComponent<ArenaSurface>();
                        wall.transform.position = new Vector3((scenario == "blocked" ? .68f : 1.2f) * facing, 1003, 0);
                        wall.AddComponent<BoxCollider2D>().size = new Vector2(.04f, 8);
                    }
                    Physics2D.SyncTransforms(); Assert.That(cast.TryApproachRecast(p.Weapon), Is.True); Vector2 origin = p.Position;
                    Record(trace, scenario + "/" + facing, "start", -1, arena, cast, target);
                    for (int tick = 0; tick < 22; tick++)
                    {
                        if (scenario == "moving" && tick < 8) { target.Position += new Vector2(.0625f * facing, .015625f); Physics2D.SyncTransforms(); }
                        if (scenario == "lost" && tick == 1) Object.DestroyImmediate(target.gameObject);
                        if (scenario == "dead" && tick == 1) target.health = 0;
                        driver.Step(.02f); Record(trace, scenario + "/" + facing, "step", tick, arena, cast, target);
                    }
                    Assert.That(cast.Approaching, Is.False); Assert.That(arena.SimulationFaulted, Is.False);
                    float moved = Vector2.Distance(origin, p.Position);
                    if (scenario == "budget") Assert.That(moved, Is.EqualTo(5).Within(.002f));
                    if (scenario == "blocked") Assert.That(moved, Is.LessThan(.05f));
                    if (scenario == "partial-sweep") Assert.That(Mathf.Abs(p.Position.x - origin.x), Is.GreaterThan(.1f).And.LessThan(1));
                    if (target) Assert.That(target.ReceivedHitCount, Is.EqualTo(1));
                }
                finally { if (wall) { wall.SetActive(false); Object.Destroy(wall); } Remove(target); }
            }
        }
        static void LifecycleCases(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver)
        {
            foreach (string scenario in new[] { "cancel-before-hit", "cancel-mark", "cancel-approach", "reconfigure", "invalid-reconfigure", "swap", "reset-mark", "reset-approach", "death-mark", "death-approach" })
            {
                var target = Setup(arena, driver, 1); var p = arena.Player; var cast = Start(arena);
                try
                {
                    if (scenario != "cancel-before-hit") { Hit(arena, driver, cast, target); FinishPrimary(arena, driver, cast); }
                    if (scenario.EndsWith("approach", StringComparison.Ordinal)) Assert.That(cast.TryApproachRecast(p.Weapon), Is.True);
                    if (scenario.StartsWith("cancel", StringComparison.Ordinal)) cast.Cancel();
                    if (scenario == "reconfigure") Assert.That(p.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == "base_sword"), 0, out var error), Is.True, error);
                    if (scenario == "invalid-reconfigure") Assert.That(p.TryReconfigure(0, arena.catalog.forms.Length, 0, out _), Is.False);
                    if (scenario == "swap") driver.Step(.02f, new CombatInput(0, CombatButtons.Swap));
                    if (scenario.StartsWith("reset", StringComparison.Ordinal)) driver.Reset();
                    if (scenario.StartsWith("death", StringComparison.Ordinal)) { p.health = 0; p.Die(); }
                    Record(trace, scenario, "boundary", 0, arena, cast, target);
                    if (scenario.StartsWith("reset", StringComparison.Ordinal) || scenario.StartsWith("death", StringComparison.Ordinal))
                    {
                        Assert.That(arena.casts.Contains(cast), Is.False); Assert.That(p.ApproachRemaining, Is.Zero); Assert.That(p.currentCast, Is.Null);
                    }
                    else if (scenario == "invalid-reconfigure") Assert.That(cast.TryApproachRecast(p.Weapon), Is.True);
                    else Assert.That(cast.TryApproachRecast(p.Weapon), Is.False);
                    for (int tick = 0; tick < 4; tick++) { driver.Step(.02f); Record(trace, scenario, "step", tick, arena, cast, target); }
                }
                finally { Remove(target); }
            }
        }
        static void CurrentCastOrder(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver)
        {
            var target = Setup(arena, driver, 1); var p = arena.Player; var cast = Start(arena);
            try
            {
                Hit(arena, driver, cast, target);
                Assert.That(cast.TryApproachRecast(p.Weapon), Is.True, "StartApproach cancels this same current Cast before recast clears cancellation/motion completion.");
                Assert.That(p.currentCast, Is.SameAs(cast)); Assert.That(cast.Approaching, Is.True); Record(trace, "current-cast", "same-cast", 0, arena, cast, target);
                driver.Reset(); Remove(target); target = Setup(arena, driver, 1); cast = Start(arena); Hit(arena, driver, cast, target); FinishPrimary(arena, driver, cast);
                driver.Step(.02f, new CombatInput(0, CombatButtons.Basic)); var stale = p.currentCast; Assert.That(stale, Is.Not.Null);
                bool recorded = false;
                Application.LogCallback observe = (message, stack, type) =>
                {
                    if (!message.Contains("whip D recast: approach speed=")) return;
                    recorded = true; Assert.That(p.currentCast, Is.SameAs(cast)); Assert.That(cast.RecastRemaining, Is.Zero); Assert.That(cast.Approaching, Is.True);
                    Assert.That(stale.ResourcesActive, Is.False); Assert.That(cast.TryApproachRecast(p.Weapon), Is.False);
                    p.AttackFinished(stale); Assert.That(p.currentCast, Is.SameAs(cast), "An old completion must not clear the recast installed by StartApproach.");
                    Record(trace, "current-cast", "record-callback", 1, arena, cast, target);
                };
                Application.logMessageReceived += observe;
                try { Assert.That(cast.TryApproachRecast(p.Weapon), Is.True); }
                finally { Application.logMessageReceived -= observe; }
                Assert.That(recorded, Is.True); Record(trace, "current-cast", "replaced-basic", 2, arena, cast, target);
            }
            finally { Remove(target); }
        }
        static IEnumerator Exercise(string scene, string label)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); Assert.That(Application.isPlaying, Is.True); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var trace = new Trace();
            var floor = new GameObject("ApproachNativeFloor"); floor.transform.position = new Vector3(0, 999.5f, 0);
            floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(200, 1); Physics2D.SyncTransforms();
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    FirstHitAndInput(trace, arena, driver); WindowAndSelection(trace, arena, driver); MotionCases(trace, arena, driver);
                    LifecycleCases(trace, arena, driver); CurrentCastOrder(trace, arena, driver); Assert.That(arena.SimulationFaulted, Is.False);
                }
                var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-approachRecastTraceRoot");
                if (at >= 0 && at + 1 < args.Length) { Directory.CreateDirectory(args[at + 1]); File.WriteAllText(Path.Combine(args[at + 1], label + ".json"), JsonUtility.ToJson(trace)); }
            }
            finally { Object.Destroy(floor); }
        }
    }
}
