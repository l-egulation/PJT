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
    // This fixture deliberately uses the pre-capture public API. The identical
    // source can run on the old consumer and compare its native gameplay trace.
    public class SkillExecutionDefinitionTests
    {
        [Serializable] sealed class Row
        {
            public string scenario, action, effects;
            public int tick, now, x, y, vx, vy, hp, hits, otherHp, otherHits, shield, guard, root, stun, knock, smoke, stealth;
            public int available, ready, age, impact, duration, end, recast, casts, shots, bombs, decoys, explosions, slot;
            public bool current, moving, freeze, invulnerable, missile, recoil, resources, faulted;
        }
        [Serializable] sealed class Trace { public List<Row> rows = new List<Row>(); }
        [UnityTest] public IEnumerator GameUneditedCatalogKeepsOldConsumerGameplay() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "game");
        [UnityTest] public IEnumerator LabUneditedCatalogKeepsOldConsumerGameplay() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "lab");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        static void Setup(CombatArena arena, CombatArena.ManualSimulation driver, int form, int special, bool air, int facing)
        {
            driver.Reset(); var p = arena.Player; p.selected = 0; p.InputBlocked = false;
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(40, 1000); }
            foreach (var weapon in p.weapons) weapon.StatSlots.Clear();
            Assert.That(p.TryReconfigure(0, form, special, out var error), Is.True, error);
            p.Position = new Vector2(0, air ? 1003 : 1000); p.velocity = Vector2.zero; p.grounded = !air; p.SyncFacing(facing);
            arena.enemies[0].Position = new Vector2(2 * facing, p.Position.y); arena.enemies[1].Position = new Vector2(4 * facing, p.Position.y);
            arena.enemies[0].health = arena.enemies[1].health = 1000; Physics2D.SyncTransforms();
        }
        static string Effects(CombatArena arena)
        {
            var result = new System.Text.StringBuilder();
            foreach (var shot in arena.playerShots) if (shot) result.Append('s').Append(Bits(shot.Position.x)).Append('/').Append(Bits(shot.Position.y)).Append('/').Append(Bits(shot.velocity.x)).Append('/').Append(Bits(shot.velocity.y)).Append('/').Append(Bits(shot.damage)).Append(';');
            foreach (var bomb in arena.bombs) if (bomb) result.Append('b').Append(Bits(bomb.Position.x)).Append('/').Append(Bits(bomb.Position.y)).Append('/').Append(Bits(bomb.velocity.x)).Append('/').Append(Bits(bomb.velocity.y)).Append('/').Append(Bits(bomb.damage)).Append(';');
            foreach (var decoy in arena.decoys) if (decoy) result.Append('d').Append(Bits(decoy.Position.x)).Append('/').Append(Bits(decoy.Position.y)).Append('/').Append(Bits(decoy.health)).Append('/').Append(Bits(decoy.expiredAt)).Append(';');
            return result.ToString();
        }
        static void Observe(Trace trace, string scenario, int tick, CombatArena arena, CombatCast observed)
        {
            var p = arena.Player; var target = arena.enemies[0]; var other = arena.enemies[1]; var active = p.currentCast;
            trace.rows.Add(new Row { scenario = scenario, tick = tick, now = Bits(arena.Now), action = active ? active.name : "", effects = Effects(arena),
                x = Bits(p.Position.x), y = Bits(p.Position.y), vx = Bits(p.velocity.x), vy = Bits(p.velocity.y), hp = Bits(target.health), hits = target.ReceivedHitCount, otherHp = Bits(other.health), otherHits = other.ReceivedHitCount,
                shield = Bits(p.shield), guard = Bits(p.guardUntil), root = Bits(target.rootUntil), stun = Bits(target.stunUntil), knock = Bits(target.knockUntil), smoke = Bits(p.smokeUntil), stealth = Bits(p.stealthUntil),
                available = p.weapons[0].Cooldown.Available, ready = Bits(p.weapons[0].Cooldown.NextReady), age = observed ? Bits(observed.Age) : 0, impact = observed ? Bits(observed.ImpactTime) : 0,
                duration = observed ? Bits(observed.MotionDuration) : 0, end = observed ? Bits(observed.EndTime) : 0, recast = observed ? Bits(observed.RecastRemaining) : 0,
                casts = arena.casts.Count, shots = arena.playerShots.Count, bombs = arena.bombs.Count, decoys = arena.decoys.Count, explosions = arena.BombExplosionCount, slot = p.selected,
                current = observed && ReferenceEquals(active, observed), moving = p.IsForcedMoving, freeze = active && active.FreezeVertical, invulnerable = p.Invulnerable,
                missile = active && active.MissileFlying, recoil = active && active.Recoiling, resources = observed && observed.ResourcesActive, faulted = arena.SimulationFaulted });
        }
        static void Run(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver, int formIndex, int special, int hit, bool air, int facing, int cancel)
        {
            Setup(arena, driver, formIndex, special, air, facing); var p = arena.Player; var form = p.Form;
            string scenario = form.id + "/" + (hit >= 0 ? "basic-" + hit + "/conversion-" + special : form.specials[special].id) + "/" + air + "/" + facing + "/" + cancel;
            var method = typeof(CombatPlayer).GetMethod(hit >= 0 ? "BeginBasic" : "BeginSpecial", BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(p, hit >= 0 ? new object[] { default(Deviant.Combat.Contracts.CombatCommandKey), hit, .015f } : new object[] { default(Deviant.Combat.Contracts.CombatCommandKey), .015f });
            var observed = p.currentCast;
            if (hit >= 0 || !form.specials[special].unavailable) Assert.That(observed, Is.Not.Null, scenario);
            Observe(trace, scenario, -1, arena, observed);
            for (int tick = 0; tick < 360; tick++)
            {
                var buttons = tick == cancel ? CombatButtons.Dash : CombatButtons.None;
                if (tick == 20 && observed && observed.RecastRemaining > 0) buttons |= CombatButtons.Special;
                if (tick == 25 && observed && observed.MissileFlying) buttons |= CombatButtons.Special;
                if (tick == 35 && cancel >= 0) buttons |= CombatButtons.Swap;
                driver.Step(.02f, new CombatInput(tick < 15 ? facing : 0, buttons, tick < 18));
                Observe(trace, scenario, tick, arena, observed);
            }
            Assert.That(arena.SimulationFaulted, Is.False, scenario);
        }
        static IEnumerator Exercise(string scene, string traceName)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); Assert.That(Application.isPlaying, Is.True); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var trace = new Trace();
            var floor = new GameObject("SkillStartDefinitionFloor"); floor.transform.position = new Vector3(0, 999.5f, 0); floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(200, 1); Physics2D.SyncTransforms();
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                for (int form = 0; form < arena.catalog.forms.Length; form++)
                {
                    var data = arena.catalog.forms[form];
                    for (int hit = 0; hit < data.basics.Length; hit++)
                    {
                        int conversions = Deviant.Definitions.MotionSourceRegistry.HasInkAppearance(data.id) ? Deviant.Definitions.MotionSourceRegistry.InkAppearanceCount : 1;
                        for (int special = 0; special < conversions; special++)
                        {
                            Run(trace, arena, driver, form, special, hit, false, 1, -1);
                            Run(trace, arena, driver, form, special, hit, true, -1, 3);
                        }
                    }
                    for (int special = 0; special < data.specials.Length; special++)
                    {
                        Run(trace, arena, driver, form, special, -1, false, 1, -1);
                        if (!data.specials[special].unavailable) Run(trace, arena, driver, form, special, -1, false, -1, 3);
                    }
                }
                var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-skillExecutionTraceRoot");
                if (at >= 0 && at + 1 < args.Length) { string directory = Path.GetFullPath(args[at + 1]); Directory.CreateDirectory(directory); File.WriteAllText(Path.Combine(directory, traceName + ".json"), JsonUtility.ToJson(trace, false)); }
            }
            finally { Object.Destroy(floor); }
        }
    }
}
