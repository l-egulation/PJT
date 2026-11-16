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
    public class EnemyAttackIntegrationTests
    {
        const int Ticks = 180, Scenarios = 12;
        [Serializable] sealed class Row
        {
            public string phase;
            public int profile, scenario, tick, facing, hits, enemyHits, emitted;
            public bool dead;
            public int[] bits, projectiles;
        }
        [Serializable] sealed class Trace { public List<Row> rows = new List<Row>(); }
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        static void Record(Trace trace, string phase, int profile, int scenario, int tick, CombatArena arena, TestEnemy enemy, int emitted)
        {
            var shots = new List<int>();
            foreach (var shot in arena.projectiles)
            {
                shots.Add(Bits(shot.transform.position.x)); shots.Add(Bits(shot.transform.position.y));
                var state = shot.Execution;
                shots.Add(Bits(shot.velocity.x)); shots.Add(Bits(shot.velocity.y)); shots.Add(Bits(state.Damage)); shots.Add(Bits(state.Expires));
                shots.Add(state.Reflected ? 1 : 0); shots.Add(shot.source == enemy ? 1 : 0);
            }
            var player = arena.Player;
            trace.rows.Add(new Row { phase = phase, profile = profile, scenario = scenario, tick = tick, facing = enemy.facing,
                hits = player.ReceivedHitCount, enemyHits = enemy.ReceivedHitCount, emitted = emitted, dead = enemy.Dead, projectiles = shots.ToArray(),
                bits = new[] { Bits(arena.Now), Bits(enemy.Position.x), Bits(enemy.Position.y), Bits(enemy.velocity.x), Bits(enemy.velocity.y),
                    Bits(enemy.health), Bits(enemy.AttackElapsed), Bits(enemy.Telegraph), Bits(player.health), Bits(player.Position.x), Bits(player.Position.y),
                    Bits(player.velocity.x), Bits(player.velocity.y), Bits(enemy.staggerUntil), Bits(enemy.stunUntil) } });
        }
        [UnityTest] public IEnumerator GameKeepsAttackWindowsCancellationResetAndNativeCallbacks() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "game");
        [UnityTest] public IEnumerator LabKeepsAttackWindowsCancellationResetAndNativeCallbacks() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "lab");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static IEnumerator Exercise(string scene, string label)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            Assert.That(Application.isPlaying, Is.True);
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player; var trace = new Trace();
            var floor = new GameObject("EnemyAttackFixtureFloor"); floor.transform.position = new Vector3(0, 999.5f, 0);
            floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1);
            float oldMaximum = player.maxHealth; bool oldBoxes = arena.showBoxes;
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    arena.showBoxes = false;
                    for (int profile = 0; profile < 3; profile++) for (int scenario = 0; scenario < Scenarios; scenario++)
                    {
                        driver.Reset(); player.maxHealth = player.health = 10000; player.InputBlocked = false;
                        foreach (var other in arena.enemies) { other.aggressive = false; other.Position = new Vector2(-35, 1000); }
                        var enemy = arena.enemies[profile]; enemy.Position = new Vector2(0, 1000); enemy.velocity = Vector2.zero; enemy.SyncFacing(1);
                        if (scenario == 11)
                        {
                            player.Reconfigure(0, Array.FindIndex(arena.catalog.forms, form => form.id == "shield"), 0);
                            enemy.health = 1;
                        }
                        float first = profile == 2 ? .45f : .35f, resetAt = -1;
                        bool started = false, reacted = false, restarted = false; int starts = 0, effectsBeforeReset = 0;
                        var emitted = new HashSet<ArenaProjectile>();
                        for (int tick = 0; tick < Ticks; tick++)
                        {
                            // Admission is enabled only when ready, avoiding chase
                            // movement into the boss's intentionally forward box.
                            if (!started && arena.Now >= .5f) enemy.aggressive = true;
                            if (resetAt >= 0 && !restarted && arena.Now >= resetAt + .5f) enemy.aggressive = true;
                            CombatButtons buttons = CombatButtons.None;
                            bool effect = player.ReceivedHitCount > 0 || emitted.Count > 0;
                            if (started && !reacted)
                            {
                                bool before = enemy.AttackElapsed >= .1f;
                                if ((scenario == 2 || scenario == 4 || scenario == 6 || scenario == 8 || scenario == 9) && before ||
                                    (scenario == 3 || scenario == 5 || scenario == 7) && effect)
                                {
                                    reacted = true;
                                    if (scenario == 2 || scenario == 3) enemy.CancelAttack();
                                    if (scenario == 4) enemy.Receive(0, player, 1, true, null);
                                    if (scenario == 5) enemy.Receive(0, player, 1, false, new HitData { stun = .3f });
                                    if (scenario == 6 || scenario == 7)
                                    {
                                        float elapsed = enemy.AttackElapsed; effectsBeforeReset = player.ReceivedHitCount; resetAt = arena.Now;
                                        enemy.ResetEnemy();
                                        Assert.That(enemy.AttackElapsed, Is.EqualTo(elapsed), "Reset retains the last attack's start time.");
                                        Assert.That(enemy.Telegraph, Is.EqualTo(-1));
                                    }
                                    if (scenario == 8) enemy.stunUntil = arena.Now + .5f; // Suppression, not the cancellation command.
                                    if (scenario == 9) player.stealthUntil = arena.Now + 5;
                                    Record(trace, "command", profile, scenario, tick, arena, enemy, emitted.Count);
                                }
                                if (scenario == 11 && enemy.AttackElapsed >= (profile == 1 ? first + .01f : first - .08f))
                                { reacted = true; buttons = CombatButtons.Special; }
                            }
                            enemy.Position = new Vector2(0, 1000); enemy.velocity = Vector2.zero;
                            float distance = scenario == 1 && started && enemy.AttackElapsed < first + .02f ? 12 : 2.15f;
                            player.SyncFacing(scenario == 11 ? -1 : 1);
                            player.Position = enemy.Center + new Vector2(distance, 0) - new Vector2(player.offset.x * player.facing, player.offset.y);
                            player.velocity = Vector2.zero; player.ClearHitMotionLocks();
                            Physics2D.SyncTransforms();
                            float dt = .02f;
                            if (scenario == 10 && started && !reacted && profile != 1) { reacted = true; dt = .9f; }
                            driver.Step(dt, new CombatInput(0, buttons));
                            foreach (var shot in arena.projectiles) if (shot.source == enemy) emitted.Add(shot);
                            if (enemy.Telegraph >= 0 && (!started || resetAt >= 0 && !restarted))
                            {
                                starts++; if (started) restarted = true; started = true; enemy.aggressive = false;
                            }
                            if (dt == .9f)
                            {
                                Assert.That(player.ReceivedHitCount, Is.EqualTo(profile == 2 ? 3 : 1), "A single tick intersects every ordered native hit window.");
                                Assert.That(enemy.Telegraph, Is.GreaterThanOrEqualTo(0), "Completion uses current elapsed, not the end of this long tick.");
                            }
                            Record(trace, "tick", profile, scenario, tick, arena, enemy, emitted.Count);
                        }
                        Assert.That(started, Is.True, "Every profile must start the real adapter attack.");
                        int full = profile == 2 ? 3 : 1;
                        int expected = scenario == 2 || scenario == 6 || scenario == 11 ? 0 :
                            scenario == 3 || scenario == 5 ? 1 : scenario == 4 && profile != 2 ? 0 : scenario == 8 ? profile == 2 ? 2 : 0 : full;
                        if (scenario == 6 || scenario == 7)
                        {
                            Assert.That(starts, Is.EqualTo(2), "Reset's half-second recovery admits exactly one fresh execution.");
                            Assert.That(restarted, Is.True);
                            expected = scenario == 7 && profile == 1 ? 2 : effectsBeforeReset + full;
                        }
                        Assert.That(player.ReceivedHitCount, Is.EqualTo(expected), $"Profile {profile}, scenario {scenario}.");
                        if (profile == 1 && scenario != 6 && scenario != 7)
                            Assert.That(emitted.Count, Is.EqualTo(scenario == 11 ? 1 : expected), "The real ranged adapter emits once, never once per overlapping tick.");
                        if (scenario == 11) Assert.That(enemy.Dead, Is.True, "A native parry callback kills and cancels the source before later ticks.");
                        Assert.That(arena.SimulationFaulted, Is.False);
                    }
                }
                Assert.That(trace.rows.FindAll(row => row.phase == "tick").Count, Is.EqualTo(3 * Scenarios * Ticks));
                // The exact old TestEnemy can be restored without changing this
                // fixture. Binding identity is checked separately from behavior.
                var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-enemyAttackTraceRoot");
                if (index >= 0 && index + 1 < args.Length)
                { Directory.CreateDirectory(args[index + 1]); File.WriteAllText(Path.Combine(args[index + 1], label + ".json"), JsonUtility.ToJson(trace)); }
            }
            finally { player.maxHealth = oldMaximum; arena.showBoxes = oldBoxes; Object.Destroy(floor); }
        }
    }
}
