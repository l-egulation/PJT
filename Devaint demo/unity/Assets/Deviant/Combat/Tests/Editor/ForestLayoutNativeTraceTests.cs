using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Deviant.Combat.Contracts;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    // Deliberately uses only the pre-cutover actual world/motor APIs. The same
    // fixture is run before and after the common-layout consumer replaces them.
    public class ForestLayoutNativeTraceTests
    {
        [Serializable] sealed class Row
        {
            public string scenario;
            public int tick, facing, usedJumps, activeChunks;
            public bool grounded, wall;
            public int[] motion, platforms, chunks;
        }
        [Serializable] sealed class Trace { public List<Row> rows = new List<Row>(); }
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [UnityTest] public IEnumerator ActualGameForestPreservesAuthoredSurfaceStreamingAndCommonMotorFloatBits()
        {
            LegacySceneFixture.Open("Assets/Deviant/Game/Scenes/deviant.unity");
            Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player;
            var composition = arena.GetComponent("GameComposition");
            var world = (MonoBehaviour)composition.GetType().GetField("forest").GetValue(composition);
            var ensure = world.GetType().GetMethod("EnsureChunks");
            var trace = new Trace();
            using (var driver = arena.AcquireManualSimulation())
            {
                Run("flat-right", new Vector2(4.6875f, -8.97f), 1, 100);
                Run("step-up-x48", new Vector2(46, -8.97f), 1, 70, jump: true);
                Run("step-down-x48", new Vector2(50, -7.97f), -1, 70);
                Run("platform-ascent", new Vector2(15, -8.97f), 0, 80, jump: true, secondJump: 12);
                Run("platform-drop", new Vector2(15, -5.97f), 0, 55, drop: true);
                Run("seam-forward-64", new Vector2(62, -6.97f), 1, 60);
                Run("seam-backward-64", new Vector2(66, -6.97f), -1, 60);
                Run("repeat-forward-512", new Vector2(510, -8.97f), 1, 60);
                Run("repeat-backward-512", new Vector2(514, -8.97f), -1, 60);
                Run("dash-over-flat", new Vector2(25, -8.97f), 1, 40, dash: true);
                foreach (float position in new[] { 4.6875f, 192f, 512f, 768f, 4.6875f, 512f })
                {
                    ensure.Invoke(world, new object[] { position });
                    Capture("stream-window-" + position.ToString("R", System.Globalization.CultureInfo.InvariantCulture), -1);
                }
                Assert.That(arena.SimulationFaulted, Is.False);

                void Run(string id, Vector2 at, int axis, int ticks, bool jump = false, int secondJump = -1, bool drop = false, bool dash = false)
                {
                    driver.Reset(); player.UseTestInput = true;
                    foreach (var enemy in arena.enemies) enemy.aggressive = false;
                    ensure.Invoke(world, new object[] { at.x });
                    player.Position = at; player.velocity = Vector2.zero; player.grounded = false; player.SyncFacing(axis < 0 ? -1 : 1);
                    Physics2D.SyncTransforms();
                    Capture(id, -1);
                    for (int tick = 0; tick < ticks; tick++)
                    {
                        ensure.Invoke(world, new object[] { player.Position.x });
                        var buttons = tick == 0 ? jump ? CombatButtons.Jump : drop ? CombatButtons.Drop : dash ? CombatButtons.Dash : CombatButtons.None
                            : tick == secondJump ? CombatButtons.Jump : CombatButtons.None;
                        driver.Step(Time.fixedDeltaTime, new CombatInput(axis, buttons));
                        Capture(id, tick);
                    }
                }
                void Capture(string id, int tick)
                {
                    var chunks = world.GetComponentsInChildren<MonoBehaviour>().Where(part => part.GetType().Name == "InkForestChunk")
                        .OrderBy(part => (int)part.GetType().GetField("index").GetValue(part)).ToArray();
                    var chunkValues = new List<int>(); var platformValues = new List<int>();
                    foreach (var chunk in chunks)
                    {
                        int index = (int)chunk.GetType().GetField("index").GetValue(chunk);
                        chunkValues.Add(index); chunkValues.Add(Bits(chunk.transform.position.x));
                        chunkValues.Add(Bits(chunk.transform.position.y));
                        var root = (Transform)chunk.GetType().GetField("platformRoot").GetValue(chunk);
                        foreach (var box in root.GetComponentsInChildren<BoxCollider2D>().OrderBy(box => box.transform.position.x))
                        {
                            platformValues.Add(index); platformValues.Add(Bits(box.transform.position.x)); platformValues.Add(Bits(box.transform.position.y));
                            platformValues.Add(Bits(box.size.x)); platformValues.Add(Bits(box.size.y));
                            platformValues.Add(box.GetComponent<ArenaSurface>().oneWay ? 1 : 0);
                        }
                    }
                    trace.rows.Add(new Row {
                        scenario = id, tick = tick, facing = player.facing, usedJumps = player.jumps.Read().Used, grounded = player.grounded, wall = player.wallHit,
                        activeChunks = chunks.Length, chunks = chunkValues.ToArray(), platforms = platformValues.ToArray(),
                        motion = new[] { Bits(arena.Now), Bits(player.Position.x), Bits(player.Position.y), Bits(player.VelocityX), Bits(player.VelocityY),
                            Bits(player.size.x), Bits(player.size.y), Bits(player.offset.x), Bits(player.offset.y), Bits(player.health) }
                    });
                }
            }
            Assert.That(trace.rows.Count, Is.EqualTo(671));
            string json = JsonUtility.ToJson(trace);
            var args = Environment.GetCommandLineArgs(); int output = Array.IndexOf(args, "-forestLayoutTraceRoot");
            if (output >= 0 && output + 1 < args.Length)
            {
                Directory.CreateDirectory(args[output + 1]);
                File.WriteAllText(Path.Combine(args[output + 1], "forest-native.json"), json);
                using (var hash = SHA256.Create()) File.WriteAllText(Path.Combine(args[output + 1], "forest-native.sha256"),
                    BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(json))).Replace("-", "").ToLowerInvariant());
            }
            int before = Array.IndexOf(args, "-forestLayoutBeforeTrace");
            if (before >= 0 && before + 1 < args.Length)
                Assert.That(json, Is.EqualTo(File.ReadAllText(args[before + 1])), "The unchanged actual Native forest/motor trace must match the pre-cutover source.");
        }
    }
}
