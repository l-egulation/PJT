using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    // Runs unchanged against original CombatPlayer.cs and the extracted consumer.
    // Random execution/actor IDs are represented only by semantic relationships.
    public class PlayerStartRecoveryNativeTraceTests
    {
        [Serializable] sealed class Row
        {
            public string scenario, phase, action, version, payload, result;
            public int index, facing, slot, charges, jumps, casts, shots;
            public bool basic, tracks, grounded, invulnerable, dead, resources, receiptMatches;
            public int[] bits;
            public string[] outcomes;
        }
        [Serializable] sealed class Trace { public List<Row> rows = new List<Row>(); public List<string> logs = new List<string>(); }
        static readonly MethodInfo basic = typeof(CombatPlayer).GetMethod("BeginBasic", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly MethodInfo special = typeof(CombatPlayer).GetMethod("BeginSpecial", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo floors = typeof(CombatPlayer).GetField("floors", BindingFlags.Instance | BindingFlags.NonPublic);
        const float Step = .015625f;
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        [UnityTest] public IEnumerator GamePlayerStartRecoveryNativeTrace() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "game");
        [UnityTest] public IEnumerator LabPlayerStartRecoveryNativeTrace() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "lab");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static string Payload(SkillExecutionDefinition definition)
        {
            // Sorted public primitive/property traversal records all start payload
            // float bits, nested ranged/bomb/charge/track values without serializer loss.
            var values = new List<string>(); Capture(definition, "", values); return string.Join("|", values);
        }
        static void Capture(object value, string path, List<string> values)
        {
            if (value == null) { values.Add(path + "=null"); return; }
            if (value is float f) { values.Add(path + "=f" + Bits(f)); return; }
            if (value is double d) { values.Add(path + "=d" + BitConverter.DoubleToInt64Bits(d)); return; }
            if (value is string text) { values.Add(path + "=s" + text); return; }
            var type = value.GetType(); if (type.IsPrimitive || type.IsEnum) { values.Add(path + "=" + value); return; }
            if (value is IEnumerable enumerable) { int index = 0; foreach (var item in enumerable) Capture(item, path + "[" + index++ + "]", values); return; }
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(item => item.GetIndexParameters().Length == 0).OrderBy(item => item.Name, StringComparer.Ordinal))
            {
                // Execution is the finite graph's back-reference to this definition.
                if (property.Name == "Execution") continue;
                Capture(property.GetValue(value), path + "." + property.Name, values);
            }
        }
        static List<CombatCommandOutcome> Outcomes(CombatArena arena, ref CombatOutcomeCursor cursor)
        {
            var result = new List<CombatCommandOutcome>(); var buffer = new CombatCommandOutcome[64];
            while (true)
            {
                Assert.That(arena.Commands.TryReadOutcomes(cursor, buffer, out var page), Is.True); Assert.That(page.DroppedBefore, Is.Zero); cursor = page.Next;
                for (int i = 0; i < page.Count; i++) result.Add(buffer[i]); if (page.Count < buffer.Length) return result;
            }
        }
        static void Record(Trace trace, string scenario, string phase, CombatArena arena, ref CombatOutcomeCursor cursor, string result = "")
        {
            var player = arena.Player; var cast = player.currentCast; var receipts = Outcomes(arena, ref cursor);
            trace.rows.Add(new Row {
                scenario = scenario, phase = phase, action = cast ? cast.Definition.Id : "", version = cast ? cast.DefinitionVersion : "", payload = cast ? Payload(cast.Definition) : "", result = result,
                index = player.BasicIndex, facing = player.facing, slot = player.selected, charges = player.Weapon.Cooldown.Available, jumps = player.jumps.Used, casts = arena.casts.Count, shots = arena.PlayerShotCount,
                basic = cast && cast.isBasic, tracks = cast && cast.UsesCompiledTracks, grounded = player.grounded, invulnerable = player.Invulnerable, dead = player.Dead, resources = cast && cast.ResourcesActive,
                receiptMatches = receipts.Where(receipt => receipt.State == CombatCommandState.Started && cast).All(receipt => receipt.ExecutionId == cast.ExecutionId),
                bits = new[] { Bits(arena.Now), Bits(player.Position.x), Bits(player.Position.y), Bits(player.velocity.x), Bits(player.velocity.y), Bits(player.health), Bits(player.maxHealth), Bits(player.shield), Bits(player.Weapon.Speed), Bits(player.Weapon.Cooldown.NextReady), Bits(player.LastFallTime), Bits(player.LastRestoreTime), Bits(player.LastRestorePosition.x), Bits(player.LastRestorePosition.y), Bits(cast ? cast.StartedAt : -1), Bits(cast ? cast.ImpactTime : -1), Bits(cast ? cast.EndTime : -1), Bits(cast ? cast.Age : -1) },
                outcomes = receipts.Select(receipt => receipt.Command.Sequence + "/" + receipt.Command.Kind + "/" + receipt.State + "/" + receipt.Reason + "/" + Bits(receipt.Now) + "/" + receipt.TickCount + "/" + (receipt.ExecutionId != Guid.Empty) + "/" + receipt.Snapshot.Action.ContentId + "/" + receipt.Detail).ToArray()
            });
        }
        static void Prepare(CombatArena arena, CombatArena.ManualSimulation driver, int form, int specialIndex, out CombatOutcomeCursor cursor)
        {
            driver.Reset(); var player = arena.Player; player.selected = 0;
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 1000); enemy.velocity = Vector2.zero; }
            foreach (var weapon in player.weapons) weapon.StatSlots.Clear();
            Assert.That(player.TryReconfigure(0, form, specialIndex, out var error), Is.True, error);
            player.Position = new Vector2(0, 1000); player.velocity = Vector2.zero; player.grounded = true; player.SyncFacing(1); Physics2D.SyncTransforms();
            Assert.That(arena.ReadModel.TryGetSession(out var session), Is.True); cursor = new CombatOutcomeCursor(session); Outcomes(arena, ref cursor);
        }
        static CombatActionAttempt Invoke(MethodInfo method, CombatPlayer player, params object[] args)
        {
            try { return (CombatActionAttempt)method.Invoke(player, args); }
            catch (TargetInvocationException exception) { throw exception.InnerException; }
        }
        static string Result(CombatActionAttempt result) => result.Started + "/" + result.Deferred + "/" + result.OutcomeRecorded + "/" + result.Reason + "/" + (result.ExecutionId != Guid.Empty);
        static IEnumerator Exercise(string scene, string label)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player; var trace = new Trace();
            var floor = new GameObject("PlayerStartRecoveryFloor"); floor.transform.position = new Vector3(0, 999.5f, 0); floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1);
            var spawn = player.spawn; player.spawn = new Vector2(-4, 1000);
            Application.LogCallback logs = (message, stack, type) => { if (message.StartsWith("[CombatV2]", StringComparison.Ordinal)) trace.logs.Add(message); };
            Application.logMessageReceived += logs;
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    for (int form = 0; form < arena.catalog.forms.Length; form++)
                    {
                        var source = arena.catalog.forms[form]; int conversions = MotionSourceRegistry.HasInkAppearance(source.id) ? MotionSourceRegistry.InkAppearanceCount : 1;
                        for (int conversion = 0; conversion < conversions; conversion++) for (int speed = 0; speed < 2; speed++)
                        {
                            int count = conversions == 4 ? 1 : source.basics.Length;
                            for (int index = 0; index < count; index++)
                            {
                                Prepare(arena, driver, form, conversion, out var cursor); if (speed != 0) for (int slot = 0; slot < 5; slot++) player.Weapon.StatSlots[slot] = 3;
                                driver.Step(.25f);
                                player.axis = -1; var command = new CombatCommandKey(new CombatActorHandle(player.InputSession, player.InputSession.ActorId), 100 + index, CombatCommandKind.Basic);
                                var attempt = Invoke(basic, player, command, index, .125f); string scenario = "basic/" + source.id + "/" + conversion + "/" + speed + "/" + index;
                                Assert.That(attempt.Started && attempt.OutcomeRecorded, Is.True); Assert.That(player.currentCast.ExecutionId, Is.EqualTo(attempt.ExecutionId)); Assert.That(player.currentCast.StartedAt, Is.EqualTo(.125f));
                                Record(trace, scenario, "start", arena, ref cursor, Result(attempt));
                                for (int tick = 0; tick < 6; tick++) { driver.Step(Step); Record(trace, scenario, "tick" + tick, arena, ref cursor); }
                            }
                        }
                        for (int skill = 0; skill < source.specials.Length; skill++)
                        {
                            Prepare(arena, driver, form, skill, out var cursor); arena.enemies[0].Position = new Vector2(2, 1000); Physics2D.SyncTransforms(); player.axis = -1;
                            var command = new CombatCommandKey(new CombatActorHandle(player.InputSession, player.InputSession.ActorId), 500 + skill, CombatCommandKind.Special);
                            var attempt = Invoke(special, player, command, -1f); string scenario = "special/" + source.id + "/" + skill;
                            Record(trace, scenario, "start", arena, ref cursor, Result(attempt));
                            for (int tick = 0; tick < 12; tick++) { driver.Step(Step); Record(trace, scenario, "tick" + tick, arena, ref cursor); }
                        }
                    }
                    foreach (string scenario in new[] { "history", "spawn", "lethal", "log-mutation" })
                    {
                        Prepare(arena, driver, 0, 0, out var cursor); driver.Step(4);
                        ((SafeGroundHistory)floors.GetValue(player)).Clear();
                        if (scenario != "spawn") ((SafeGroundHistory)floors.GetValue(player)).Record(1, 3, 1000, -1);
                        player.health = scenario == "lethal" ? 1 : 80; player.maxHealth = 175; player.shield = 50; player.guardUntil = 100; player.guardReduction = 1;
                        player.Position = new Vector2(10, 990); player.velocity = new Vector2(2, -4); Physics2D.SyncTransforms();
                        Application.LogCallback mutate = (message, stack, type) => { if (scenario == "log-mutation" && message.Contains("safe floor found=")) { player.Position = new Vector2(99, 999); player.facing = 1; } };
                        Application.logMessageReceived += mutate;
                        try { player.Fall(); } finally { Application.logMessageReceived -= mutate; }
                        Assert.That(player.health, Is.EqualTo(scenario == "lethal" ? 0 : 62.5f)); Assert.That(player.shield, Is.EqualTo(50));
                        if (scenario != "lethal") { Assert.That(player.Position.y, Is.EqualTo(1000)); Assert.That(player.Invulnerable, Is.True); Assert.That(player.jumps.Used, Is.Zero); }
                        Record(trace, "fall/" + scenario, "returned", arena, ref cursor);
                    }
                }
                var args = Environment.GetCommandLineArgs(); int output = Array.IndexOf(args, "-playerStartRecoveryTraceRoot");
                if (output >= 0 && output + 1 < args.Length) { Directory.CreateDirectory(args[output + 1]); File.WriteAllText(Path.Combine(args[output + 1], label + ".json"), JsonUtility.ToJson(trace)); }
            }
            finally { Application.logMessageReceived -= logs; player.spawn = spawn; Object.Destroy(floor); }
        }
    }
}
