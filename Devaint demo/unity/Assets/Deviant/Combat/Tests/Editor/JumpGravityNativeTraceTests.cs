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
    // Deliberately uses only existing public Native APIs so this exact fixture
    // also runs on base541bb13 before profile/schema/jump API migration.
    public class JumpGravityNativeTraceTests
    {
        [Serializable] sealed class Row { public string scenario, phase, action; public int tick, used, casts, shots; public bool grounded, dashing, flying; public int[] bits; }
        [Serializable] sealed class Trace { public List<Row> rows = new List<Row>(); }
        const float Dt = .00390625f;
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        [UnityTest] public IEnumerator GameJumpGravityNativeTrace() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "game");
        [UnityTest] public IEnumerator LabJumpGravityNativeTrace() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "lab");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static void Prepare(CombatArena arena, CombatArena.ManualSimulation driver, string form = "base_sword", int special = 0)
        {
            driver.Reset(); var player = arena.Player; player.selected = 0;
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 1000); enemy.velocity = Vector2.zero; }
            foreach (var weapon in player.weapons) weapon.StatSlots.Clear();
            Assert.That(player.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, item => item.id == form), special, out var error), Is.True, error);
            player.Position = new Vector2(0, 1000); player.velocity = Vector2.zero; player.grounded = true; player.SyncFacing(1); Physics2D.SyncTransforms();
        }
        static void Record(Trace trace, string scenario, string phase, int tick, CombatArena arena)
        {
            var player = arena.Player; var cast = player.currentCast; var enemy = arena.enemies[0];
            trace.rows.Add(new Row { scenario = scenario, phase = phase, tick = tick, action = cast ? cast.Definition.Id : "", used = player.jumps.Used,
                casts = arena.casts.Count, shots = arena.PlayerShotCount, grounded = player.grounded, dashing = player.Dashing, flying = cast && cast.MissileFlying,
                bits = new[] { Bits(arena.Now), Bits(player.Position.x), Bits(player.Position.y), Bits(player.velocity.x), Bits(player.velocity.y), Bits(player.health),
                    Bits(enemy.Position.x), Bits(enemy.Position.y), Bits(enemy.velocity.x), Bits(enemy.velocity.y), Bits(enemy.health), Bits(player.jumps.BufferedUntil),
                    Bits(cast ? cast.StartedAt : -1), Bits(cast ? cast.Age : -1), Bits(cast ? cast.EndTime : -1), Bits(cast ? cast.MotionDuration : -1) } });
        }
        static void Jump(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver, string scenario)
        {
            Prepare(arena, driver); float start = arena.Now, origin = arena.Player.Position.y, maximum = origin; bool descending = false; float apexAt = -1, secondOrigin = -1;
            for (int tick = 0; tick < 210; tick++)
            {
                CombatButtons button = tick == 0 || scenario == "second" && tick == 40 ? CombatButtons.Jump : CombatButtons.None;
                if (tick == 40 && scenario == "second") secondOrigin = arena.Player.Position.y;
                driver.Step(Dt, new CombatInput(0, button)); var player = arena.Player; maximum = Math.Max(maximum, player.Position.y);
                if (!descending && player.velocity.y <= 0 && tick > 0) { descending = true; apexAt = arena.Now; }
                Record(trace, scenario, "tick", tick, arena);
            }
            Assert.That(maximum - origin, Is.GreaterThan(2.98f));
            if (scenario == "first") { Assert.That(maximum - origin, Is.EqualTo(3).Within(.025f)); Assert.That(apexAt - start, Is.EqualTo(.30f).Within(Dt * 2)); }
            else Assert.That(maximum - secondOrigin, Is.EqualTo(3).Within(.04f));
        }
        static IEnumerator Exercise(string scene, string label)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false; yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var trace = new Trace();
            var floor = new GameObject("JumpGravityFloor"); floor.transform.position = new Vector3(0, 999.5f, 0); floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1);
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    Jump(trace, arena, driver, "first"); Jump(trace, arena, driver, "second");
                    Prepare(arena, driver); var enemy = arena.enemies[0]; enemy.Position = new Vector2(30, 1010); enemy.velocity = new Vector2(0, 20); enemy.grounded = false;
                    for (int tick = 0; tick < 140; tick++) { driver.Step(Dt); Record(trace, "all-actor-gravity", "tick", tick, arena); }
                    Prepare(arena, driver, "launcher", 2); arena.Player.Position = new Vector2(0, 1005); arena.Player.grounded = false;
                    for (int tick = 0; tick < 220; tick++) { driver.Step(Dt, new CombatInput(0, tick == 0 ? CombatButtons.Special : CombatButtons.None)); Record(trace, "shotgun-hop", "tick", tick, arena); }
                    Prepare(arena, driver, "bomb", 2); arena.Player.Position = new Vector2(0, 1005); arena.Player.grounded = false;
                    for (int tick = 0; tick < 250; tick++)
                    {
                        driver.Step(Dt, new CombatInput(0, tick == 0 ? CombatButtons.Special : CombatButtons.None));
                        if (tick == 120 && arena.Player.currentCast) Assert.That(arena.Player.currentCast.TryMissileDetonate(), Is.True);
                        Record(trace, "missile-hop", "tick", tick, arena);
                    }
                }
                var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-jumpGravityTraceRoot");
                if (index >= 0 && index + 1 < args.Length) { Directory.CreateDirectory(args[index + 1]); File.WriteAllText(Path.Combine(args[index + 1], label + ".json"), JsonUtility.ToJson(trace)); }
            }
            finally { Object.Destroy(floor); }
        }
    }
}
