using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Deviant.Combat.Contracts;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    // Uses the existing public native API so this exact fixture can collect the
    // pre-authoring baseline without depending on the new authored definition.
    public class AuthoredSkillNativeTraceTests
    {
        static readonly string[] Ids = { "whip_default", "whip_range", "whip_movement", "whip_follow_up", "rapier_movement", "rapier_follow_up", "dagger_default", "dagger_range", "dagger_movement", "dagger_follow_up", "hammer_default", "hammer_range", "hammer_movement", "hammer_follow_up", "shield_default", "shield_movement", "shield_follow_up", "greatsword_movement", "greatsword_follow_up" };
        [Serializable] sealed class Row { public string id; public int tick, hits, casts, decoys, charges, slot; public bool current, moving, freeze, invulnerable, hidden, faulted; public int[] values; }
        [Serializable] sealed class Trace { public List<Row> rows = new List<Row>(); }
        [UnityTest] public IEnumerator GameNineteenNativePolicyTrace() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "game");
        [UnityTest] public IEnumerator LabNineteenNativePolicyTrace() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "lab");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        static void Record(Trace trace, string id, int tick, CombatArena arena, CombatCast cast)
        {
            var p = arena.Player; var target = arena.enemies[0]; trace.rows.Add(new Row { id = id, tick = tick, hits = target.ReceivedHitCount, casts = arena.casts.Count, decoys = arena.decoys.Count, charges = p.weapons[0].Cooldown.Available, slot = p.selected,
                current = p.currentCast == cast, moving = cast && cast.Moving, freeze = cast && cast.FreezeVertical, invulnerable = p.Invulnerable, hidden = p.Hidden, faulted = arena.SimulationFaulted,
                values = new[] { Bits(arena.Now), Bits(p.Position.x), Bits(p.Position.y), Bits(p.velocity.x), Bits(p.velocity.y), Bits(p.health), Bits(p.shield), Bits(p.guardUntil), Bits(p.stealthUntil), Bits(p.smokeUntil), Bits(target.health), Bits(target.rootUntil), Bits(target.stunUntil), Bits(target.knockUntil), Bits(p.weapons[0].Cooldown.NextReady), cast ? Bits(cast.StartedAt) : 0, cast ? Bits(cast.Age) : 0, cast ? Bits(cast.ImpactTime) : 0, cast ? Bits(cast.EndTime) : 0 } });
        }
        static IEnumerator Exercise(string scene, string name)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false; yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var trace = new Trace(); var floor = new GameObject("AuthoredNativeTraceFloor"); floor.transform.position = new Vector3(0, 999.5f, 0); floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(200, 1);
            try { using (var driver = arena.AcquireManualSimulation()) foreach (string id in Ids) foreach (int cancel in new[] { -1, 3, 20 }) {
                driver.Reset(); var p = arena.Player; p.selected = 0; p.InputBlocked = false; foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(40, 1000); }
                foreach (var weapon in p.weapons) weapon.StatSlots.Clear(); int form = Array.FindIndex(arena.catalog.forms, value => Array.Exists(value.specials, skill => skill.id == id)); int special = Array.FindIndex(arena.catalog.forms[form].specials, value => value.id == id);
                Assert.That(p.TryReconfigure(0, form, special, out var error), Is.True, error); p.Position = new Vector2(0, 1000); p.velocity = Vector2.zero; p.grounded = true; p.SyncFacing(cancel == 3 ? -1 : 1); arena.enemies[0].Position = new Vector2(cancel == 3 ? -2 : 2, 1000); arena.enemies[0].health = 1000; Physics2D.SyncTransforms();
                typeof(CombatPlayer).GetMethod("BeginSpecial", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(p, new object[] { default(CombatCommandKey), .015f }); var cast = p.currentCast; Assert.That(cast, Is.Not.Null, id); Record(trace, id + "/" + cancel, -1, arena, cast);
                for (int tick = 0; tick < 180; tick++) { driver.Step(.02f, new CombatInput(tick < 10 ? cancel == 3 ? -1 : 1 : 0, tick == cancel ? CombatButtons.Dash : tick == 40 ? CombatButtons.Swap : CombatButtons.None)); Record(trace, id + "/" + cancel, tick, arena, cast); }
            }
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-authoredSkillTraceRoot"); if (at >= 0 && at + 1 < args.Length) { string directory = Path.GetFullPath(args[at + 1]); Directory.CreateDirectory(directory); File.WriteAllText(Path.Combine(directory, name + ".json"), JsonUtility.ToJson(trace, false)); } }
            finally { Object.Destroy(floor); }
        }
    }
}
