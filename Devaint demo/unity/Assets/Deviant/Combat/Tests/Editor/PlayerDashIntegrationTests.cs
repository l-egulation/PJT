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
    public class PlayerDashIntegrationTests
    {
        [Serializable] sealed class Row
        {
            public string scenario, phase, action;
            public int tick, facing, selected, jumps, basicIndex, hits, targetHits, available, otherAvailable, actionState, observedState;
            public int[] bits;
            public bool dashing, invulnerable, grounded, wall, dead, basic, moving, freeze, actionInvulnerable, resourcesActive, resourceSpent, observedResourcesActive, observedResourceSpent;
        }
        [Serializable] sealed class Trace { public List<Row> rows = new List<Row>(); }
        const int Ticks = 65;
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);

        [UnityTest] public IEnumerator GameKeepsDashMovementResourcesAndCancellation()
        {
            LegacySceneFixture.Open("Assets/Deviant/Game/Scenes/deviant.unity");
            Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); Assert.That(Application.isPlaying, Is.True); yield return null;
            Exercise(Object.FindAnyObjectByType<CombatArena>(), "game");
        }
        [UnityTest] public IEnumerator LabKeepsDashMovementResourcesAndCancellation()
        {
            LegacySceneFixture.Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
            Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); Assert.That(Application.isPlaying, Is.True); yield return null;
            Exercise(Object.FindAnyObjectByType<CombatArena>(), "lab");
        }
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        static void Record(Trace trace, string scenario, string phase, int tick, CombatArena arena, CombatCast observed = null)
        {
            var p = arena.Player; var cast = p.currentCast;
            trace.rows.Add(new Row {
                scenario = scenario, phase = phase, tick = tick, facing = p.facing, selected = p.selected, jumps = p.jumps.Used, basicIndex = p.BasicIndex,
                hits = p.ReceivedHitCount, targetHits = arena.enemies[0].ReceivedHitCount,
                available = p.weapons[0].Cooldown.Available, otherAvailable = p.weapons[1].Cooldown.Available,
                action = cast ? cast.Definition.Id : "", actionState = cast ? (int?)cast.ActionTimelineState ?? -1 : -1,
                observedState = observed ? (int?)observed.ActionTimelineState ?? -1 : -1,
                dashing = p.Dashing, invulnerable = p.Invulnerable, grounded = p.grounded, wall = p.wallHit, dead = p.Dead,
                basic = cast && cast.isBasic, moving = cast && cast.Moving, freeze = cast && cast.FreezeVertical, actionInvulnerable = cast && cast.Invulnerable,
                resourcesActive = cast && cast.ResourcesActive, resourceSpent = cast && cast.ResourceResult != null,
                observedResourcesActive = observed && observed.ResourcesActive, observedResourceSpent = observed && observed.ResourceResult != null,
                bits = new[] { Bits(arena.Now), Bits(p.Position.x), Bits(p.Position.y), Bits(p.velocity.x), Bits(p.velocity.y), Bits(p.DashRemaining),
                    Bits(p.jumps.BufferedUntil), Bits(p.health), Bits(arena.enemies[0].health), Bits(p.weapons[0].Cooldown.NextReady), Bits(p.weapons[1].Cooldown.NextReady),
                    Bits(p.moveLockUntil), Bits(p.knockUntil), Bits(p.knockSpeed), Bits(p.LastFallTime), Bits(p.LastRestoreTime), Bits(p.LastRestorePosition.x), Bits(p.LastRestorePosition.y),
                    Bits(cast ? cast.StartedAt : -1), Bits(cast ? cast.EndTime : -1), Bits(cast ? cast.Age : -1), Bits(cast ? cast.ImpactTime : -1),
                    Bits(observed ? observed.StartedAt : -1), Bits(observed ? observed.EndTime : -1) }
            });
        }

        static void Prepare(CombatArena arena, CombatArena.ManualSimulation driver, bool air = false, int facing = 1)
        {
            driver.Reset(); var p = arena.Player; p.selected = 0; p.InputBlocked = false;
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 1000); }
            foreach (var weapon in p.weapons) weapon.StatSlots.Clear();
            Assert.That(p.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, form => form.id == "base_sword"), 0, out var error), Is.True, error);
            p.Position = new Vector2(0, air ? 1004 : 1000); p.velocity = new Vector2(0, air ? -7 : 0); p.grounded = !air; p.SyncFacing(facing);
            arena.enemies[0].Position = new Vector2(2, p.Position.y); Physics2D.SyncTransforms();
        }

        static void Exercise(CombatArena arena, string label)
        {
            var p = arena.Player; var trace = new Trace(); Vector2 oldSpawn = p.spawn; var originalMovement = arena.Movement;
            var floor = new GameObject("PlayerDashFixtureFloor"); floor.transform.position = new Vector3(0, 999.5f, 0);
            floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1);
            var wall = new GameObject("PlayerDashFixtureWall"); wall.AddComponent<ArenaSurface>();
            var wallBox = wall.AddComponent<BoxCollider2D>(); wallBox.size = new Vector2(.5f, 20); wallBox.enabled = false;
            p.spawn = new Vector2(0, 1000);
            var scenarios = new[] { "facing", "air", "terminal-tick", "wall", "turn-at-wall", "jump", "invalid-jump", "cancel", "carry-cancel", "fall", "carry-fall", "death", "reset", "basic-cancel", "invalid-dash", "special-before", "special-after", "special-jump" };
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    foreach (string scenario in scenarios)
                    {
                        wallBox.enabled = scenario == "wall" || scenario == "turn-at-wall";
                        wall.transform.position = new Vector3(scenario == "turn-at-wall" ? -.8f : arena.Movement.DashSpeed * .03f + .8f, 1005, 0);
                        bool air = scenario == "air" || scenario == "terminal-tick" || scenario == "jump" || scenario == "invalid-jump" || scenario == "carry-cancel" || scenario == "carry-fall";
                        bool special = scenario == "special-before" || scenario == "special-after" || scenario == "special-jump";
                        Prepare(arena, driver, air, scenario == "facing" ? -1 : 1);
                        if (scenario == "invalid-jump") { Assert.That(p.jumps.Press(arena.Now, arena.Movement, out _), Is.True); Assert.That(p.jumps.Press(arena.Now, arena.Movement, out _), Is.True); }
                        CombatCast observed = null;
                        Record(trace, scenario, "ready", -1, arena);
                        for (int tick = 0; tick < Ticks; tick++)
                        {
                            CombatButtons buttons = CombatButtons.None;
                            int axis = scenario == "facing" ? 0 : scenario == "turn-at-wall" ? -1 : 1;
                            if (tick == 0) buttons = scenario == "basic-cancel" ? CombatButtons.Basic : special ? CombatButtons.Special : CombatButtons.Dash;
                            if (scenario == "facing" && tick > 0) axis = 1;
                            if (scenario == "air" && tick > 0) { axis = -1; if (tick == 2) buttons = CombatButtons.Basic | CombatButtons.Special; }
                            if (scenario == "jump" || scenario == "carry-cancel" || scenario == "carry-fall")
                            {
                                if (tick > 0) axis = -1;
                                if (tick == 2 || scenario == "jump" && tick == 3) buttons = CombatButtons.Jump;
                                if (scenario == "jump" && tick == 0) buttons |= CombatButtons.Jump | CombatButtons.Special | CombatButtons.Basic | CombatButtons.Swap;
                            }
                            if (scenario == "invalid-jump" && tick == 2) buttons = CombatButtons.Jump;
                            if (scenario == "basic-cancel")
                            {
                                if (tick == 1) buttons = CombatButtons.Basic | CombatButtons.Special | CombatButtons.Swap;
                                if (tick == 2) buttons = CombatButtons.Dash | CombatButtons.Jump;
                            }
                            if (scenario == "invalid-dash")
                            {
                                if (tick == 4) buttons = CombatButtons.Basic;
                                if (tick == 5) buttons = CombatButtons.Dash;
                                if (tick == 6) buttons = CombatButtons.Special;
                            }
                            if (special && tick == 1) buttons = scenario == "special-jump" ? CombatButtons.Jump : CombatButtons.Swap;
                            if (scenario == "special-before" && tick == 2 || scenario == "special-after" && tick == 17 || scenario == "special-jump" && tick == 2) buttons = CombatButtons.Dash;
                            if (tick == 3)
                            {
                                float remaining = p.DashRemaining;
                                switch (scenario)
                                {
                                    case "cancel": case "carry-cancel": case "invalid-dash":
                                        p.CancelAction(); Assert.That(p.Dashing, Is.False); Assert.That(Bits(p.DashRemaining), Is.EqualTo(Bits(remaining)));
                                        Record(trace, scenario, "cancel", tick, arena, observed); break;
                                    case "fall": case "carry-fall":
                                        p.Fall(); Assert.That(p.Dashing, Is.False); Assert.That(p.health, Is.EqualTo(90)); Assert.That(p.jumps.Used, Is.Zero);
                                        Assert.That(Bits(p.DashRemaining), Is.EqualTo(Bits(remaining))); Assert.That(p.Invulnerable, Is.True);
                                        Record(trace, scenario, "fall", tick, arena, observed); break;
                                    case "death":
                                        p.health = 0; p.Die(); Assert.That(p.Dashing, Is.False); Assert.That(Bits(p.DashRemaining), Is.EqualTo(Bits(remaining)));
                                        Record(trace, scenario, "death", tick, arena, observed); break;
                                    case "reset":
                                        p.ResetPlayer(); Assert.That(p.Dashing, Is.False); Assert.That(p.DashRemaining, Is.Zero); Assert.That(p.health, Is.EqualTo(100));
                                        Assert.That(p.jumps.Used, Is.Zero); Assert.That(p.currentCast, Is.Null); Record(trace, scenario, "reset", tick, arena, observed); break;
                                }
                            }
                            if (tick == 1 && scenario == "air") buttons |= CombatButtons.Dash;
                            driver.Step(scenario == "terminal-tick" && tick == 0 ? .25f : .02f, new CombatInput(axis, buttons));
                            if (p.currentCast && observed == null) observed = p.currentCast;
                            Record(trace, scenario, "tick", tick, arena, observed);
                            if (tick == 0)
                            {
                                if (special || scenario == "basic-cancel") Assert.That(p.currentCast, Is.Not.Null);
                                else if (scenario != "terminal-tick" && scenario != "turn-at-wall") Assert.That(p.Dashing, Is.True);
                                if (scenario == "facing") { Assert.That(p.facing, Is.EqualTo(-1)); Assert.That(p.velocity.x, Is.EqualTo(-22.5f)); }
                                if (scenario == "jump") { Assert.That(p.jumps.Used, Is.Zero); Assert.That(p.selected, Is.Zero); Assert.That(p.currentCast, Is.Null); }
                                if (scenario == "terminal-tick")
                                {
                                    Assert.That(p.Position.x, Is.EqualTo(5.625f)); Assert.That(p.Position.y, Is.EqualTo(1004));
                                    Assert.That(p.velocity.y, Is.Zero); Assert.That(p.Dashing, Is.False, "Legacy movement uses the full tick, not a clipped dash substep.");
                                }
                                if (scenario == "turn-at-wall") { Assert.That(p.facing, Is.EqualTo(-1)); Assert.That(p.wallHit, Is.True); Assert.That(p.Dashing, Is.False); }
                            }
                            if (scenario == "wall" && tick == 1)
                            { Assert.That(p.wallHit, Is.True); Assert.That(p.Dashing, Is.False); Assert.That(p.Invulnerable, Is.False); Assert.That(p.velocity.y, Is.Zero); Assert.That(p.DashRemaining, Is.GreaterThan(.9f)); }
                            if (scenario == "air" && tick < 9) { Assert.That(p.velocity.x, Is.EqualTo(22.5f)); Assert.That(p.velocity.y, Is.Zero); Assert.That(p.currentCast, Is.Null); }
                            if ((scenario == "jump" || scenario == "carry-cancel" || scenario == "carry-fall") && tick == 2)
                            { Assert.That(p.Dashing, Is.False); Assert.That(p.Invulnerable, Is.False); Assert.That(p.jumps.Used, Is.EqualTo(1)); Assert.That(p.velocity.y, Is.GreaterThan(0)); Assert.That(p.velocity.x, Is.EqualTo(5)); Assert.That(p.facing, Is.EqualTo(-1)); }
                            if (scenario == "jump" && tick == 3) { Assert.That(p.jumps.Used, Is.EqualTo(2)); Assert.That(p.velocity.x, Is.EqualTo(5)); }
                            if ((scenario == "carry-cancel" || scenario == "carry-fall") && tick == 3) Assert.That(p.velocity.x, Is.EqualTo(5), "Ordinary cancellation retains an already-started carry.");
                            if (scenario == "invalid-jump" && tick == 2)
                            { Assert.That(p.Dashing, Is.True); Assert.That(p.jumps.Used, Is.EqualTo(2)); Assert.That(p.jumps.BufferedUntil, Is.GreaterThan(arena.Now)); Assert.That(p.velocity.y, Is.Zero); }
                            if (scenario == "invalid-dash" && tick == 5) { Assert.That(p.currentCast, Is.SameAs(observed)); Assert.That(p.Dashing, Is.False); }
                            if (scenario == "special-jump" && tick == 1) { Assert.That(p.currentCast, Is.SameAs(observed)); Assert.That(p.jumps.Used, Is.Zero); }
                            if ((scenario == "basic-cancel" || scenario == "special-before" || scenario == "special-jump") && tick == 2)
                            { Assert.That(p.currentCast, Is.Null); Assert.That(p.Dashing, Is.True); Assert.That(p.weapons[0].Cooldown.Available, Is.EqualTo(1)); }
                            if (scenario == "special-after" && tick == 17)
                            { Assert.That(p.currentCast, Is.Null); Assert.That(p.Dashing, Is.True); Assert.That(p.weapons[0].Cooldown.Available, Is.Zero); Assert.That(arena.enemies[0].health, Is.EqualTo(arena.enemies[0].maxHealth - arena.catalog.forms[p.weapons[0].FormIndex].specials[0].hits[0].damage)); }
                        }
                        if (scenario == "basic-cancel" || special) Assert.That(p.selected, Is.Zero, "A successful dash discards the pending weapon switch.");
                        Assert.That(arena.SimulationFaulted, Is.False);
                    }
                    Assert.That(trace.rows.FindAll(row => row.phase == "tick").Count, Is.EqualTo(scenarios.Length * Ticks));
                    wallBox.enabled = false;
                    ExerciseExactRecovery(arena, driver, trace);
                    ExerciseProfiles(arena, driver, trace);
                }
                // Uses only pre-existing consumer APIs so the exact old
                // CombatPlayer can run this same fixture without modification.
                var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-playerDashTraceRoot");
                if (index >= 0 && index + 1 < args.Length)
                { Directory.CreateDirectory(args[index + 1]); File.WriteAllText(Path.Combine(args[index + 1], label + ".json"), JsonUtility.ToJson(trace)); }
            }
            finally
            {
                p.spawn = oldSpawn;
                if (arena.Movement != originalMovement) Assert.That(arena.TryApplyMovementAndReset(originalMovement.ToData(), out var error), Is.True, error);
                Object.Destroy(wall); Object.Destroy(floor);
            }
        }

        static void ExerciseExactRecovery(CombatArena arena, CombatArena.ManualSimulation driver, Trace trace)
        {
            Prepare(arena, driver, true); var p = arena.Player;
            driver.Step(.25f, new CombatInput(1, CombatButtons.Dash)); Record(trace, "recovery", "spent", 0, arena);
            driver.Step(.5f); Record(trace, "recovery", "recovering", 1, arena);
            driver.Step(.25f, new CombatInput(0, CombatButtons.Dash));
            Assert.That(p.Dashing, Is.False); Assert.That(arena.Now, Is.EqualTo(1)); Assert.That(p.DashRemaining, Is.Zero);
            Record(trace, "recovery", "deadline", 2, arena);
            driver.Step(.02f, new CombatInput(-1, CombatButtons.Dash));
            Assert.That(p.Dashing, Is.True); Assert.That(p.velocity.x, Is.EqualTo(-22.5f)); Record(trace, "recovery", "restart", 3, arena);
        }

        static void ExerciseProfiles(CombatArena arena, CombatArena.ManualSimulation driver, Trace trace)
        {
            Prepare(arena, driver, true); var p = arena.Player;
            driver.Step(.02f, new CombatInput(1, CombatButtons.Dash)); var current = arena.Movement;
            float remaining = p.DashRemaining; var invalid = current.ToData(); invalid.dashDuration = -1;
            Assert.That(arena.TryApplyMovementAndReset(invalid, out _), Is.False); Assert.That(arena.Movement, Is.SameAs(current));
            Assert.That(p.Dashing, Is.True); Assert.That(Bits(p.DashRemaining), Is.EqualTo(Bits(remaining))); Record(trace, "profile", "rejected", -1, arena);
            var changed = current.ToData(); changed.runSpeed = 7; changed.dashSpeed = 9; changed.dashDuration = .07f;
            changed.dashRecovery = .31f; changed.dashInvulnerability = .05f; changed.dashJumpCarrySpeed = 2.5f; changed.dashJumpCarryDuration = .03f;
            Assert.That(arena.TryApplyMovementAndReset(changed, out var error), Is.True, error);
            Assert.That(p.Dashing, Is.False); Assert.That(p.DashRemaining, Is.Zero); Assert.That(p.jumps.Used, Is.Zero);
            Prepare(arena, driver, true); changed.dashSpeed = 999;
            for (int tick = 0; tick < 25; tick++)
            {
                driver.Step(.02f, new CombatInput(tick == 0 ? 1 : -1, tick == 0 ? CombatButtons.Dash : tick == 1 ? CombatButtons.Jump : tick == 20 ? CombatButtons.Dash : CombatButtons.None));
                Record(trace, "profile", "tick", tick, arena);
                if (tick == 0) { Assert.That(p.velocity.x, Is.EqualTo(9)); Assert.That(p.Invulnerable, Is.True); }
                if (tick == 1) { Assert.That(p.velocity.x, Is.EqualTo(2.5f)); Assert.That(p.Dashing, Is.False); Assert.That(p.Invulnerable, Is.False); }
                if (tick == 3) Assert.That(p.velocity.x, Is.EqualTo(-7));
                if (tick == 20) { Assert.That(p.Dashing, Is.True); Assert.That(p.velocity.x, Is.EqualTo(-9)); }
            }
            Assert.That(arena.SimulationFaulted, Is.False);
        }
    }
}
