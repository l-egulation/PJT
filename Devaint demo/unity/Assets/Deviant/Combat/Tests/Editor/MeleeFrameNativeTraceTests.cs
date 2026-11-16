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
    // Before fixture: no new frame/registry APIs. The parent runs these exact bytes
    // on original numeric/fixed behavior before activating the family cutover.
    public class MeleeFrameNativeTraceTests
    {
        [Serializable] sealed class Row { public string action, phase, version, timeline; public int speed, axis, tick, hits, charges; public bool air, current, basic, resources; public int[] bits; }
        [Serializable] sealed class Trace { public List<Row> rows = new List<Row>(); public List<string> logs = new List<string>(); }
        const float Dt = .015625f;
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        static readonly MethodInfo beginBasic = typeof(CombatPlayer).GetMethod("BeginBasic", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly MethodInfo beginSpecial = typeof(CombatPlayer).GetMethod("BeginSpecial", BindingFlags.Instance | BindingFlags.NonPublic);
        [UnityTest] public IEnumerator GameAllTwentyMeleeFrameFamiliesNativeTrace() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "game");
        [UnityTest] public IEnumerator LabAllTwentyMeleeFrameFamiliesNativeTrace() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "lab");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static void Record(Trace trace, string action, string phase, int speed, int axis, bool air, int tick, CombatArena arena, CombatCast observed)
        {
            var player = arena.Player; var enemy = arena.enemies[0]; var hit = observed.Definition.Hits[0];
            trace.rows.Add(new Row { action = action, phase = phase, speed = speed, axis = axis, air = air, tick = tick, version = observed.DefinitionVersion, timeline = observed.ActionTimelineVersion,
                hits = enemy.ReceivedHitCount, charges = player.Weapon.Cooldown.Available, current = player.currentCast == observed, basic = observed.isBasic, resources = observed.ResourcesActive,
                bits = new[] { Bits(arena.Now), Bits(player.Position.x), Bits(player.Position.y), Bits(player.velocity.x), Bits(player.velocity.y), Bits(enemy.health), Bits(observed.StartedAt), Bits(observed.Age), Bits(observed.ImpactTime), Bits(observed.EndTime), Bits(observed.MotionDuration), Bits(hit.Time), Bits(hit.Active), Bits(hit.Damage), Bits(hit.Box.Width), Bits(hit.Box.Height), Bits(hit.Box.X), Bits(hit.Box.Y), Bits(player.Weapon.Speed), Bits(player.Weapon.Cooldown.NextReady) } });
        }
        static CombatCast Start(CombatPlayer player, bool direct, int index)
        {
            object result;
            try { result = direct ? beginSpecial.Invoke(player, new object[] { default(CombatCommandKey), -1f }) : beginBasic.Invoke(player, new object[] { default(CombatCommandKey), index, -1f }); }
            catch (TargetInvocationException error) { throw error.InnerException; }
            Assert.That(((CombatActionAttempt)result).Started, Is.True); return player.currentCast;
        }
        static IEnumerator Exercise(string scene, string label)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false; yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var trace = new Trace();
            var floor = new GameObject("MeleeFrameTwentyFixtureFloor"); floor.transform.position = new Vector3(0, 999.5f, 0); floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1);
            Application.LogCallback log = (message, stack, type) => { if (message.StartsWith("[CombatV2]", StringComparison.Ordinal)) trace.logs.Add(message); }; Application.logMessageReceived += log;
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    foreach (var form in arena.catalog.forms.Where(form => form.basics.All(basic => string.IsNullOrEmpty(basic.kind))))
                    {
                        var direct = form.specials.Select((skill, index) => (skill, index)).Where(item => DirectMeleeSpecialDefinition.SupportedIds.Contains(item.skill.id)).ToArray();
                        int count = form.basics.Length + direct.Length;
                        for (int item = 0; item < count; item++) foreach (int speed in new[] { 0, 5 }) foreach (int axis in new[] { 0, -1, 1 }) foreach (bool air in new[] { false, true })
                        {
                            bool special = item >= form.basics.Length; int skill = special ? direct[item - form.basics.Length].index : 0; int basicIndex = special ? 0 : item;
                            driver.Reset(); var player = arena.Player; player.selected = 0;
                            foreach (var target in arena.enemies) { target.aggressive = false; target.Position = new Vector2(35, 1000); target.velocity = Vector2.zero; }
                            foreach (var weapon in player.weapons) weapon.StatSlots.Clear();
                            Assert.That(player.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, candidate => candidate.id == form.id), skill, out var error), Is.True, error);
                            for (int slot = 0; slot < speed; slot++) player.Weapon.StatSlots[slot] = 3;
                            player.Position = new Vector2(0, air ? 1002 : 1000); player.velocity = Vector2.zero; player.grounded = !air; player.SyncFacing(1); player.axis = axis;
                            var enemy = arena.enemies[0]; enemy.health = enemy.maxHealth = 1000; enemy.Position = new Vector2(axis < 0 && !special ? -1.5f : 1.5f, 1000); Physics2D.SyncTransforms();
                            var cast = Start(player, special, basicIndex); string id = cast.Definition.Id; Record(trace, id, "start", speed, axis, air, -1, arena, cast);
                            for (int tick = 0; tick < 80; tick++) { driver.Step(Dt, new CombatInput(axis)); Record(trace, id, "tick", speed, axis, air, tick, arena, cast); }
                        }
                    }
                }
                var args = Environment.GetCommandLineArgs(); int output = Array.IndexOf(args, "-meleeFrameNativeTraceRoot");
                if (output >= 0 && output + 1 < args.Length) { Directory.CreateDirectory(args[output + 1]); File.WriteAllText(Path.Combine(args[output + 1], label + ".json"), JsonUtility.ToJson(trace)); }
            }
            finally { Application.logMessageReceived -= log; Object.Destroy(floor); }
        }
    }
}
