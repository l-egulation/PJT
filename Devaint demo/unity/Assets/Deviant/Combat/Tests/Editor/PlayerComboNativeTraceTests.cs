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
    // Frozen before combo extraction. Only the existing public Player/Cast API
    // is used so the unchanged fixture also runs on the original implementation.
    public class PlayerComboNativeTraceTests
    {
        [Serializable] sealed class Row
        {
            public string scenario, phase, action;
            public int tick, index, slot, charges, otherCharges, jumps, hits, casts, shots;
            public bool basic, grounded, dashing, resources, spent, observedResources, faulted;
            public int[] bits;
        }
        [Serializable] sealed class Trace { public List<Row> rows = new List<Row>(); }
        const float Tick = .015625f;
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        static float After(float value) => BitConverter.Int32BitsToSingle(Bits(value) + 1);
        [UnityTest] public IEnumerator GamePlayerComboNativeTrace() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "game");
        [UnityTest] public IEnumerator LabPlayerComboNativeTrace() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "lab");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        static void Record(Trace trace, string scenario, string phase, int tick, CombatArena arena, CombatCast observed = null)
        {
            var p = arena.Player; var cast = p.currentCast;
            trace.rows.Add(new Row {
                scenario = scenario, phase = phase, tick = tick, action = cast ? cast.Definition.Id : "",
                index = p.BasicIndex, slot = p.selected, charges = p.weapons[0].Cooldown.Available, otherCharges = p.weapons[1].Cooldown.Available,
                jumps = p.jumps.Used, hits = arena.enemies[0].ReceivedHitCount, casts = arena.casts.Count, shots = arena.PlayerShotCount,
                basic = cast && cast.isBasic, grounded = p.grounded, dashing = p.Dashing,
                resources = cast && cast.ResourcesActive, spent = cast && cast.ResourceResult != null,
                observedResources = observed && observed.ResourcesActive, faulted = arena.SimulationFaulted,
                bits = new[] { Bits(arena.Now), Bits(p.Position.x), Bits(p.Position.y), Bits(p.velocity.x), Bits(p.velocity.y), Bits(p.health),
                    Bits(arena.enemies[0].health), Bits(p.weapons[0].Cooldown.NextReady), Bits(p.weapons[1].Cooldown.NextReady), Bits(p.jumps.BufferedUntil),
                    Bits(cast ? cast.StartedAt : -1), Bits(cast ? cast.EndTime : -1), Bits(cast ? cast.Age : -1), Bits(cast ? cast.MotionDuration : -1),
                    Bits(observed ? observed.StartedAt : -1), Bits(observed ? observed.EndTime : -1) }
            });
        }
        static void Prepare(CombatArena arena, CombatArena.ManualSimulation driver, string form = "base_sword", int speed = 0, int special = 0, string otherForm = "base_sword")
        {
            driver.Reset(); var p = arena.Player; p.selected = 0; p.InputBlocked = false;
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 1000); enemy.velocity = Vector2.zero; }
            foreach (var weapon in p.weapons) weapon.StatSlots.Clear();
            Assert.That(p.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == form), special, out var error), Is.True, error);
            Assert.That(p.TryReconfigure(1, Array.FindIndex(arena.catalog.forms, f => f.id == otherForm), 0, out error), Is.True, error);
            for (int i = 0; i < speed; i++) p.Weapon.StatSlots[i] = 3;
            p.Position = new Vector2(0, 1000); p.velocity = Vector2.zero; p.grounded = true; p.SyncFacing(1); Physics2D.SyncTransforms();
        }
        static CombatCast PressBasic(Trace trace, string scenario, CombatArena arena, CombatArena.ManualSimulation driver)
        {
            driver.Step(Tick, new CombatInput(0, CombatButtons.Basic));
            var cast = arena.Player.currentCast; Assert.That(cast && cast.isBasic, Is.True);
            Record(trace, scenario, "press", -1, arena, cast); return cast;
        }
        static void UntilChanged(Trace trace, string scenario, CombatArena arena, CombatArena.ManualSimulation driver, CombatCast cast)
        {
            int tick = 0;
            while (ReferenceEquals(arena.Player.currentCast, cast) && tick < 200)
            {
                driver.Step(Tick); Record(trace, scenario, "tick", tick++, arena, cast);
            }
            Assert.That(ReferenceEquals(arena.Player.currentCast, cast), Is.False, "The actual action must finish within the fixture bound.");
        }
        static void To(CombatArena arena, CombatArena.ManualSimulation driver, float time)
        {
            Assert.That(arena.Now, Is.LessThanOrEqualTo(time));
            if (arena.Now < time) driver.Step(time - arena.Now);
            Assert.That(Bits(arena.Now), Is.EqualTo(Bits(time)));
        }
        static void Cycle(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver, string form, int[] expected, int speed = 0, int special = 0)
        {
            Prepare(arena, driver, form, speed, special); string scenario = "cycle/" + form + "/" + speed + "/" + special;
            driver.Step(Tick); var cast = PressBasic(trace, scenario, arena, driver);
            Assert.That(cast.StartedAt, Is.EqualTo(.015625f)); Assert.That(arena.Player.BasicIndex, Is.EqualTo(expected[0]));
            for (int hit = 1; hit < expected.Length; hit++)
            {
                driver.Step(Tick, new CombatInput(0, CombatButtons.Basic));
                // Repeated presses still reserve only one successor.
                driver.Step(Tick, new CombatInput(0, CombatButtons.Basic));
                Record(trace, scenario, "queued", hit, arena, cast);
                float completedAt = cast.EndTime; UntilChanged(trace, scenario, arena, driver, cast);
                var next = arena.Player.currentCast; Assert.That(next && next.isBasic, Is.True);
                Assert.That(arena.Player.BasicIndex, Is.EqualTo(expected[hit]));
                Assert.That(next.Definition.Id, Is.EqualTo(form + "_basic_" + expected[hit]));
                Assert.That(Bits(next.StartedAt), Is.EqualTo(Bits(completedAt)), "A fractional completion, not the observing tick, starts the successor.");
                Assert.That(next.ExecutionId, Is.Not.EqualTo(cast.ExecutionId)); cast = next;
            }
            UntilChanged(trace, scenario, arena, driver, cast); Assert.That(arena.Player.currentCast, Is.Null);
            driver.Step(.5f); Assert.That(arena.Player.currentCast, Is.Null, "No new press means no automatic repetition.");
            Record(trace, scenario, "no-repeat", expected.Length, arena, cast);
        }
        static void Expiry(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver)
        {
            foreach (int speed in new[] { 0, 5 }) foreach (bool expired in new[] { false, true })
            {
                Prepare(arena, driver, speed: speed); string scenario = "expiry/" + speed + "/" + expired;
                var first = PressBasic(trace, scenario, arena, driver); UntilChanged(trace, scenario, arena, driver, first);
                Assert.That(arena.Player.currentCast, Is.Null); float deadline = first.EndTime + .3f;
                To(arena, driver, expired ? After(deadline) : deadline); Record(trace, scenario, "boundary", -1, arena, first);
                var next = PressBasic(trace, scenario, arena, driver);
                Assert.That(arena.Player.BasicIndex, Is.EqualTo(expired ? 0 : 1));
                Assert.That(next.Definition.Id, Is.EqualTo(expired ? "base_sword_basic_0" : "base_sword_basic_1"));
            }
        }
        static void Priorities(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver)
        {
            foreach (bool swap in new[] { false, true })
            {
                Prepare(arena, driver, speed: 5); string scenario = swap ? "swap-over-special-basic" : "special-over-basic";
                var first = PressBasic(trace, scenario, arena, driver); float completedAt = first.EndTime;
                driver.Step(Tick, new CombatInput(0, CombatButtons.Basic));
                driver.Step(Tick, new CombatInput(0, CombatButtons.Special | (swap ? CombatButtons.Swap : CombatButtons.None)));
                Record(trace, scenario, "reserved", -1, arena, first);
                Assert.That(arena.Player.Weapon.Cooldown.Available, Is.EqualTo(1)); UntilChanged(trace, scenario, arena, driver, first);
                Assert.That(arena.Player.BasicIndex, Is.EqualTo(-1));
                if (swap) { Assert.That(arena.Player.selected, Is.EqualTo(1)); Assert.That(arena.Player.currentCast, Is.Null); }
                else
                {
                    var special = arena.Player.currentCast; Assert.That(special && !special.isBasic, Is.True);
                    Assert.That(Bits(special.StartedAt), Is.EqualTo(Bits(completedAt))); Assert.That(special.MotionDuration, Is.EqualTo(.7f));
                    Assert.That(arena.Player.Weapon.Cooldown.Available, Is.EqualTo(1), "Admission does not spend an unactivated special.");
                    Record(trace, scenario, "special-start", -1, arena, special);
                }
            }
            foreach (bool jump in new[] { false, true })
            {
                Prepare(arena, driver); string scenario = jump ? "jump-cancel" : "dash-cancel";
                var first = PressBasic(trace, scenario, arena, driver);
                driver.Step(Tick, new CombatInput(0, CombatButtons.Basic | CombatButtons.Special | CombatButtons.Swap));
                driver.Step(Tick, new CombatInput(1, jump ? CombatButtons.Jump : CombatButtons.Dash));
                Assert.That(arena.Player.currentCast, Is.Null); Assert.That(arena.Player.BasicIndex, Is.EqualTo(-1));
                Assert.That(first.ResourcesActive, Is.False); Assert.That(arena.Player.selected, Is.Zero);
                Assert.That(arena.Player.Dashing, Is.EqualTo(!jump)); Record(trace, scenario, "cancelled", -1, arena, first);
                driver.Step(.5f); Assert.That(arena.Player.currentCast, Is.Null); Assert.That(arena.Player.selected, Is.Zero);
                var next = PressBasic(trace, scenario, arena, driver); Assert.That(next.Definition.Id, Is.EqualTo("base_sword_basic_0"));
            }
        }
        static void FailedSpecials(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver)
        {
            Prepare(arena, driver, "whip"); var first = PressBasic(trace, "targetless-special", arena, driver);
            UntilChanged(trace, "targetless-special", arena, driver, first);
            driver.Step(Tick, new CombatInput(-1, CombatButtons.Special));
            // A targetless nearest special still starts, spends its charge and whiffs.
            var whiff = arena.Player.currentCast; Assert.That(whiff && !whiff.isBasic, Is.True); Assert.That(whiff.Definition.Id, Is.EqualTo("whip_default"));
            Assert.That(arena.Player.Weapon.Cooldown.Available, Is.Zero); Record(trace, "targetless-special", "whiff", -1, arena, whiff);
            UntilChanged(trace, "targetless-special", arena, driver, whiff); Assert.That(arena.Player.currentCast, Is.Null);
            var next = PressBasic(trace, "targetless-special", arena, driver); Assert.That(next.Definition.Id, Is.EqualTo("whip_basic_0"));

            Prepare(arena, driver, "hammer", special: 1); first = PressBasic(trace, "airborne-special", arena, driver);
            UntilChanged(trace, "airborne-special", arena, driver, first);
            arena.Player.Position = new Vector2(0, 1010); arena.Player.velocity = Vector2.zero; arena.Player.grounded = false; Physics2D.SyncTransforms();
            driver.Step(Tick, new CombatInput(0, CombatButtons.Special));
            Assert.That(arena.Player.currentCast, Is.Null); Assert.That(arena.Player.BasicIndex, Is.Zero);
            Record(trace, "airborne-special", "rejected", -1, arena, first);
            next = PressBasic(trace, "airborne-special", arena, driver); Assert.That(next.Definition.Id, Is.EqualTo("hammer_basic_1"));

            Prepare(arena, driver, "whip"); arena.enemies[0].Position = new Vector2(2, 1000); Physics2D.SyncTransforms();
            first = PressBasic(trace, "queued-target-loss", arena, driver);
            driver.Step(Tick, new CombatInput(0, CombatButtons.Basic)); driver.Step(Tick, new CombatInput(0, CombatButtons.Special));
            arena.enemies[0].Position = new Vector2(35, 1000); Physics2D.SyncTransforms();
            UntilChanged(trace, "queued-target-loss", arena, driver, first);
            // The queued special starts even though its target left, and whiffs.
            whiff = arena.Player.currentCast; Assert.That(whiff && !whiff.isBasic, Is.True); Assert.That(arena.Player.Weapon.Cooldown.Available, Is.Zero);
            Record(trace, "queued-target-loss", "whiff", -1, arena, whiff); UntilChanged(trace, "queued-target-loss", arena, driver, whiff);
            next = PressBasic(trace, "queued-target-loss", arena, driver);
            Assert.That(next.Definition.Id, Is.EqualTo("whip_basic_0"), "A whiffed special clears the combo like any started special.");
        }
        static void MaintenanceAndLiveSelection(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver)
        {
            foreach (bool success in new[] { false, true })
            {
                Prepare(arena, driver); string scenario = success ? "maintenance-success" : "maintenance-failure";
                var first = PressBasic(trace, scenario, arena, driver);
                Assert.That(arena.Player.TryReconfigure(0, 0, 0, out _), Is.False, "Active-action maintenance must not clear the current combo.");
                Assert.That(arena.Player.BasicIndex, Is.Zero); UntilChanged(trace, scenario, arena, driver, first);
                int sword = Array.FindIndex(arena.catalog.forms, f => f.id == "base_sword");
                Assert.That(arena.Player.TryReconfigure(1, success ? sword : arena.catalog.forms.Length, 0, out _), Is.EqualTo(success));
                Assert.That(arena.Player.BasicIndex, Is.EqualTo(success ? -1 : 0)); Record(trace, scenario, "maintenance", -1, arena, first);
                var next = PressBasic(trace, scenario, arena, driver);
                Assert.That(next.Definition.Id, Is.EqualTo(success ? "base_sword_basic_0" : "base_sword_basic_1"));
            }
            Prepare(arena, driver, "dagger", otherForm: "shield");
            var current = PressBasic(trace, "live-queued-count", arena, driver);
            driver.Step(Tick, new CombatInput(0, CombatButtons.Basic)); arena.Player.selected = 1;
            UntilChanged(trace, "live-queued-count", arena, driver, current);
            Assert.That(arena.Player.currentCast.Definition.Id, Is.EqualTo("shield_basic_0")); Assert.That(arena.Player.BasicIndex, Is.Zero);
            Record(trace, "live-queued-count", "selected", -1, arena, current);

            Prepare(arena, driver, otherForm: "rapier"); current = PressBasic(trace, "live-late-count", arena, driver);
            driver.Step(Tick, new CombatInput(0, CombatButtons.Basic)); UntilChanged(trace, "live-late-count", arena, driver, current);
            current = arena.Player.currentCast; Assert.That(arena.Player.BasicIndex, Is.EqualTo(1));
            UntilChanged(trace, "live-late-count", arena, driver, current); arena.Player.selected = 1;
            var late = PressBasic(trace, "live-late-count", arena, driver);
            Assert.That(late.Definition.Id, Is.EqualTo("rapier_basic_2")); Assert.That(arena.Player.BasicIndex, Is.EqualTo(2));
        }
        static void Lifecycle(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver)
        {
            foreach (string scenario in new[] { "cancel", "death", "reset" })
            {
                Prepare(arena, driver); var first = PressBasic(trace, scenario, arena, driver);
                driver.Step(Tick, new CombatInput(0, CombatButtons.Basic | CombatButtons.Special | CombatButtons.Swap));
                if (scenario == "death") { arena.Player.health = 0; arena.Player.Die(); }
                else if (scenario == "reset") arena.Player.ResetPlayer();
                else arena.Player.CancelAction();
                Assert.That(arena.Player.BasicIndex, Is.EqualTo(-1)); Assert.That(arena.Player.currentCast, Is.Null);
                Assert.That(first.ResourcesActive, Is.False); Record(trace, scenario, "cleared", -1, arena, first);
                if (scenario == "death") { driver.Step(Tick); Assert.That(arena.Player.BasicIndex, Is.EqualTo(-1)); arena.Player.ResetPlayer(); }
                driver.Step(.5f); Assert.That(arena.Player.currentCast, Is.Null); Assert.That(arena.Player.selected, Is.Zero);
                Assert.That(PressBasic(trace, scenario, arena, driver).Definition.Id, Is.EqualTo("base_sword_basic_0"));
            }
        }
        static IEnumerator Exercise(string scene, string label)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); Assert.That(Application.isPlaying, Is.True); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var trace = new Trace(); var p = arena.Player; Vector2 spawn = p.spawn;
            var floor = new GameObject("PlayerComboFixtureFloor"); floor.transform.position = new Vector3(0, 999.5f, 0);
            floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1); p.spawn = new Vector2(0, 1000);
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    foreach (int speed in new[] { 0, 5 })
                    {
                        Cycle(trace, arena, driver, "base_sword", new[] { 0, 1, 0 }, speed);
                        Cycle(trace, arena, driver, "whip", new[] { 0, 1, 0 }, speed);
                        Cycle(trace, arena, driver, "rapier", new[] { 0, 1, 2, 0 }, speed);
                        Cycle(trace, arena, driver, "dagger", new[] { 0, 1, 2, 3, 0 }, speed);
                        Cycle(trace, arena, driver, "hammer", new[] { 0, 1, 0 }, speed);
                        Cycle(trace, arena, driver, "shield", new[] { 0, 0 }, speed);
                        Cycle(trace, arena, driver, "greatsword", new[] { 0, 1, 2, 0 }, speed);
                        Cycle(trace, arena, driver, "shuriken", new[] { 0, 1, 0 }, speed);
                        Cycle(trace, arena, driver, "bomb", new[] { 0, 0 }, speed);
                        for (int special = 0; special < 4; special++) Cycle(trace, arena, driver, "launcher", new[] { 0, 0 }, speed, special);
                    }
                    Expiry(trace, arena, driver); Priorities(trace, arena, driver); FailedSpecials(trace, arena, driver);
                    MaintenanceAndLiveSelection(trace, arena, driver); Lifecycle(trace, arena, driver);
                    Assert.That(arena.SimulationFaulted, Is.False);
                }
                var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-playerComboTraceRoot");
                if (index >= 0 && index + 1 < args.Length)
                { Directory.CreateDirectory(args[index + 1]); File.WriteAllText(Path.Combine(args[index + 1], label + ".json"), JsonUtility.ToJson(trace)); }
            }
            finally { p.spawn = spawn; Object.Destroy(floor); }
        }
    }
}
