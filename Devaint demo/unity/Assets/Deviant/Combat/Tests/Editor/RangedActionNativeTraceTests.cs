using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Deviant.Combat.Contracts;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    // Runs unchanged before and after authoring cutover. No new ranged API is
    // referenced. All payload/flight samples use original Native consumers.
    public sealed class RangedActionNativeTraceTests
    {
        const float Step = .015625f;
        static readonly MethodInfo Basic = typeof(CombatPlayer).GetMethod("BeginBasic", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly MethodInfo Special = typeof(CombatPlayer).GetMethod("BeginSpecial", BindingFlags.Instance | BindingFlags.NonPublic);
        [Serializable] sealed class Row
        {
            public string action, mode, phase, payload;
            public int tick, facing, charges, casts, shots, bombs, lasers, bursts, hits;
            public bool grounded, invulnerable, pendingReturn, resources;
            public int[] bits;
            public string[] projectiles;
        }
        [Serializable] sealed class Trace { public List<Row> rows = new List<Row>(); public List<string> logs = new List<string>(); }
        sealed class Attack
        {
            public string Id, Form;
            public int Slot, Index;
            public bool Basic;
            public Attack(string id, string form, int slot, bool basic, int index = 0) { Id = id; Form = form; Slot = slot; Basic = basic; Index = index; }
        }
        static readonly Attack[] Attacks = {
            new Attack("launcher_basic_0", "launcher", 0, true), new Attack("launcher_basic_special", "launcher", 0, false),
            new Attack("launcher_range_special", "launcher", 1, false), new Attack("launcher_move_special", "launcher", 2, false), new Attack("launcher_followup_special", "launcher", 3, false),
            new Attack("bomb_basic_0", "bomb", 0, true), new Attack("bomb_basic_special", "bomb", 0, false),
            new Attack("bomb_range_special", "bomb", 1, false), new Attack("bomb_move_special", "bomb", 2, false), new Attack("bomb_followup_special", "bomb", 3, false),
            new Attack("shuriken_basic_0", "shuriken", 0, true), new Attack("shuriken_basic_1", "shuriken", 0, true, 1),
            new Attack("shuriken_basic_special", "shuriken", 0, false), new Attack("shuriken_range_special", "shuriken", 1, false),
            new Attack("shuriken_move_special", "shuriken", 2, false), new Attack("shuriken_followup_special", "shuriken", 3, false)
        };
        [UnityTest] public IEnumerator GameRangedNumericNativeTrace() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "game");
        [UnityTest] public IEnumerator LabRangedNumericNativeTrace() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "lab");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        static string Payload(object value)
        {
            var parts = new List<string>(); Capture(value, "", parts); return string.Join("|", parts);
        }
        static void Capture(object value, string path, List<string> parts)
        {
            if (value == null) { parts.Add(path + "=null"); return; }
            if (value is float f) { parts.Add(path + "=f" + Bits(f)); return; }
            if (value is string text) { parts.Add(path + "=s" + text); return; }
            var type = value.GetType(); if (type.IsPrimitive || type.IsEnum) { parts.Add(path + "=" + value); return; }
            if (value is IEnumerable items) { int index = 0; foreach (var item in items) Capture(item, path + "[" + index++ + "]", parts); return; }
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(item => item.GetIndexParameters().Length == 0).OrderBy(item => item.Name, StringComparer.Ordinal))
                if (property.Name != "Execution") Capture(property.GetValue(value), path + "." + property.Name, parts);
        }
        static CombatActionAttempt Start(CombatArena arena, Attack attack)
        {
            var player = arena.Player; var command = new CombatCommandKey(new CombatActorHandle(player.InputSession, player.ActorId), 100,
                attack.Basic ? CombatCommandKind.Basic : CombatCommandKind.Special);
            try { return (CombatActionAttempt)(attack.Basic ? Basic.Invoke(player, new object[] { command, attack.Index, -1f }) : Special.Invoke(player, new object[] { command, -1f })); }
            catch (TargetInvocationException error) { throw error.InnerException; }
        }
        static void Record(Trace trace, Attack attack, string mode, string phase, int tick, CombatArena arena)
        {
            var player = arena.Player; var cast = player.currentCast; var target = arena.enemies[0];
            var outputs = new List<string>();
            foreach (var shot in arena.playerShots) if (shot) outputs.Add("shot/" + shot.Definition.Type + "/" + Bits(shot.Position.x) + "/" + Bits(shot.Position.y) + "/" + Bits(shot.velocity.x) + "/" + Bits(shot.velocity.y) + "/" + Bits(shot.damage) + "/" + Bits(shot.traveled) + "/" + Payload(shot.Definition));
            foreach (var bomb in arena.bombs) if (bomb) outputs.Add("bomb/" + bomb.name + "/" + Bits(bomb.Position.x) + "/" + Bits(bomb.Position.y) + "/" + Bits(bomb.velocity.x) + "/" + Bits(bomb.velocity.y) + "/" + Bits(bomb.damage) + "/" + bomb.TimedLanded + "/" + Bits(bomb.TimedFuseRemaining) + "/" + Payload(bomb.Definition));
            trace.rows.Add(new Row { action = attack.Id, mode = mode, phase = phase, tick = tick, facing = player.facing, grounded = player.grounded,
                charges = player.Weapon.Cooldown.Available, casts = arena.casts.Count, shots = arena.playerShots.Count, bombs = arena.bombs.Count, lasers = arena.lasers.Count, bursts = arena.bombBursts.Count,
                hits = target.ReceivedHitCount, invulnerable = player.Invulnerable, pendingReturn = player.ShurikenReturnPending(player.weapons[0]), resources = cast && cast.ResourcesActive,
                payload = cast ? Payload(cast.Definition) : "", projectiles = outputs.ToArray(),
                bits = new[] { Bits(arena.Now), Bits(player.Position.x), Bits(player.Position.y), Bits(player.velocity.x), Bits(player.velocity.y), Bits(player.health), Bits(target.health),
                    Bits(player.Weapon.Cooldown.NextReady), Bits(cast ? cast.StartedAt : -1), Bits(cast ? cast.ImpactTime : -1), Bits(cast ? cast.EndTime : -1), Bits(player.ApproachRemaining) } });
        }
        static IEnumerator Exercise(string scene, string label)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null; yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var trace = new Trace(); var floor = new GameObject("RangedNumericTraceFloor");
            floor.transform.position = new Vector3(0, 999.5f, 0); floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1);
            Application.LogCallback logs = (message, stack, type) => { if (message.StartsWith("[CombatV2]", StringComparison.Ordinal)) trace.logs.Add(message); };
            Application.logMessageReceived += logs;
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                foreach (var attack in Attacks) foreach (string mode in new[] { "contact", "air-cancel", "flight" })
                {
                    driver.Reset(); var player = arena.Player; player.selected = 0;
                    foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.ResetActor(new Vector2(40, 1000)); enemy.health = 10000; }
                    foreach (var weapon in player.weapons) weapon.StatSlots.Clear();
                    Assert.That(player.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, form => form.id == attack.Form), attack.Slot, out var error), Is.True, error);
                    player.Position = new Vector2(0, mode == "air-cancel" ? 1004 : 1000); player.velocity = Vector2.zero; player.grounded = mode != "air-cancel"; player.axis = 0; player.SyncFacing(1);
                    arena.enemies[0].Position = new Vector2(mode == "flight" ? 40 : 3, 1000); Physics2D.SyncTransforms();
                    var result = Start(arena, attack); Assert.That(result.Started && player.currentCast, Is.True, attack.Id);
                    Record(trace, attack, mode, "start", 0, arena);
                    for (int tick = 0; tick < 400; tick++)
                    {
                        if (mode == "air-cancel" && tick == 40) player.CancelAction();
                        if (attack.Id == "bomb_move_special" && mode == "flight" && tick == 36 && player.currentCast) player.currentCast.TryMissileDetonate();
                        driver.Step(Step); if (tick < 80 || tick % 8 == 0) Record(trace, attack, mode, "tick", tick, arena);
                    }
                    Assert.That(arena.SimulationFaulted, Is.False); Record(trace, attack, mode, "end", 400, arena);
                }
                var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-rangedNumericTraceRoot");
                if (index >= 0 && index + 1 < args.Length) { Directory.CreateDirectory(args[index + 1]); File.WriteAllText(Path.Combine(args[index + 1], label + ".json"), JsonUtility.ToJson(trace)); }
            }
            finally { Application.logMessageReceived -= logs; Object.Destroy(floor); }
        }
    }
}
