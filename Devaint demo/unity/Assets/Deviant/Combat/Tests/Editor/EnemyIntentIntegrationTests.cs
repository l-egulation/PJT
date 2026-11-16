using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class EnemyIntentIntegrationTests
    {
        [Serializable] sealed class Row
        {
            public int profile, scenario, tick, facing, hits, projectiles;
            public float time, x, y, vx, vy, health, telegraph;
        }
        [Serializable] sealed class Trace { public List<Row> rows = new List<Row>(); }
        [UnityTest] public IEnumerator GameKeepsTemporaryEnemyDecisionsAndAttackExecution() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "game");
        [UnityTest] public IEnumerator LabKeepsTemporaryEnemyDecisionsAndAttackExecution() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "lab");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static IEnumerator Exercise(string scene, string label)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            Assert.That(Application.isPlaying, Is.True);
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player; var trace = new Trace();
            var floor = new GameObject("EnemyIntentFixtureFloor"); floor.transform.position = new Vector3(0, 999.5f, 0);
            floor.AddComponent<ArenaSurface>(); var collider = floor.AddComponent<BoxCollider2D>(); collider.size = new Vector2(100, 1);
            float oldMaximum = player.maxHealth;
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    for (int profile = 0; profile < 3; profile++) for (int scenario = 0; scenario < 10; scenario++)
                    {
                        driver.Reset(); player.maxHealth = player.health = 10000; player.Position = new Vector2(0, 1000); player.velocity = Vector2.zero; player.SyncFacing(1); player.InputBlocked = false;
                        foreach (var other in arena.enemies) { other.aggressive = false; other.Position = new Vector2(-35, 1000); }
                        var enemy = arena.enemies[profile]; enemy.aggressive = scenario != 0 && scenario != 5; enemy.Position = new Vector2(0, 1000); enemy.velocity = Vector2.zero; enemy.SyncFacing(1);
                        collider.size = new Vector2(scenario == 8 ? 10 : 100, 1); floor.transform.position = new Vector3(scenario == 8 ? -5 : 0, 999.5f, 0);
                        if (scenario == 8) enemy.Position = new Vector2(-.9f, 1000);
                        float distance = scenario == 1 || scenario == 8 ? 9 : scenario == 2 ? 11 : scenario == 9 ? -2.15f : 2.15f;
                        var target = enemy.Center + new Vector2(distance, scenario == 3 || scenario == 8 ? 4 : 0);
                        player.Position = target - new Vector2(player.offset.x * player.facing, player.offset.y); player.velocity = Vector2.zero;
                        if (scenario == 4) enemy.stunUntil = 1.2f;
                        if (scenario == 6) player.stealthUntil = 1.2f;
                        if (scenario == 7)
                        {
                            player.Position = new Vector2(9, 1000); arena.SpawnDecoy(player, enemy.Position + new Vector2(1, 0), new EffectData { hp = 10000, duration = 5 });
                        }
                        Physics2D.SyncTransforms(); bool started = false; int hitsBeforeDisable = 0;
                        for (int tick = 0; tick < 150; tick++)
                        {
                            // Admit the explicit hit cases after the initial cooldown.
                            // Otherwise legacy boss chase moves inside its own forward box.
                            if ((scenario == 0 || scenario == 5) && tick == 25) enemy.aggressive = true;
                            if (scenario == 5 && tick == 30) { enemy.aggressive = false; hitsBeforeDisable = player.ReceivedHitCount; }
                            if (scenario == 9 && tick == 30) enemy.Receive(0, player, 1, true, null);
                            if (scenario == 3 || scenario == 8) { player.Position = target - new Vector2(player.offset.x * player.facing, player.offset.y); player.velocity = Vector2.zero; Physics2D.SyncTransforms(); }
                            driver.Step(.02f); started |= enemy.Telegraph >= 0;
                            trace.rows.Add(new Row { profile = profile, scenario = scenario, tick = tick, time = arena.Now, x = enemy.Position.x, y = enemy.Position.y, vx = enemy.velocity.x, vy = enemy.velocity.y, facing = enemy.facing,
                                health = player.health, hits = player.ReceivedHitCount, projectiles = arena.projectiles.Count, telegraph = enemy.Telegraph });
                            if (scenario == 2 || scenario == 3) Assert.That(enemy.Telegraph, Is.EqualTo(-1), "No attack outside horizontal/vertical admission.");
                            if (scenario == 4 && arena.Now < 1.2f || scenario == 6 && arena.Now < 1.2f) Assert.That(enemy.Telegraph, Is.EqualTo(-1));
                            if (scenario == 8) Assert.That(enemy.Position.x, Is.LessThan(0), "A grounded chase must stop before the missing floor probe.");
                        }
                        if (scenario == 0) { Assert.That(started, Is.True, "Profile " + profile + " must admit a close attack."); Assert.That(player.ReceivedHitCount, Is.GreaterThan(0), $"Profile {profile}: enemy {enemy.Position}/{enemy.Center}, target {player.Position}/{player.Center}, facing {enemy.facing}, time {arena.Now}, telegraph {enemy.Telegraph}"); }
                        if (scenario == 5) Assert.That(player.ReceivedHitCount, Is.GreaterThan(hitsBeforeDisable), "Turning off admission does not cancel the already-started attack.");
                        if (scenario == 7 && profile != 2) Assert.That(arena.decoys[0].health, Is.LessThan(10000), "Existing nonboss target selection prefers the nearby decoy.");
                        Assert.That(arena.SimulationFaulted, Is.False);
                    }
                }
                Assert.That(trace.rows.Count, Is.EqualTo(4500));
                // Optional before/after evidence uses the real scenario, never a
                // second product AI. Ordinary suite runs do not write a trace.
                var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-enemyIntentTraceRoot");
                if (index >= 0 && index + 1 < args.Length) { Directory.CreateDirectory(args[index + 1]); File.WriteAllText(Path.Combine(args[index + 1], label + ".json"), JsonUtility.ToJson(trace)); }
            }
            finally { player.maxHealth = oldMaximum; Object.Destroy(floor); }
        }
    }
}
