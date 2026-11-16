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
    // Uses only the existing public native API so exactly this fixture can run
    // before and after the missile owner cutover, including both compositions.
    public class MissileNativeTraceTests
    {
        enum Case
        {
            Maximum, EarlyOnly, ExactDelay, NewInput, Enemy, WallBeforeEnemy,
            OneWay, ThinWall, FloorMargin, BlastOcclusion, Duplicate, Air,
            RecoilWall, RecoilCeiling, RecoilLanding, CancelBefore, CancelAfter,
            DeathBefore, DeathAfter, ResetBefore, ResetAfter, ActivationCapture
        }
        [Serializable] sealed class Row
        {
            public string scenario;
            public int tick, now, x, y, vx, vy, hp, hits, otherHp, otherHits, playerHp;
            public int age, end, available, ready, period, explosions, blastX, blastY, slot, attempts, detonations;
            public bool current, flying, recoiling, moving, freeze, invulnerable, grounded, wall, visual, resources, faulted;
        }
        [Serializable] sealed class Trace { public List<Row> rows = new List<Row>(); }
        [UnityTest] public IEnumerator GameMissileNativeTrace() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "game");
        [UnityTest] public IEnumerator LabMissileNativeTrace() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "lab");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        static GameObject Surface(List<GameObject> geometry, string name, Vector2 center, Vector2 size, bool oneWay = false)
        {
            var go = new GameObject(name); geometry.Add(go); go.transform.position = center;
            go.AddComponent<ArenaSurface>().oneWay = oneWay; go.AddComponent<BoxCollider2D>().size = size; return go;
        }
        static void Observe(Trace trace, string scenario, int tick, CombatArena arena, CombatCast cast, int attempts, int detonations)
        {
            var p = arena.Player; var target = arena.enemies[0]; var other = arena.enemies[1];
            trace.rows.Add(new Row { scenario = scenario, tick = tick, now = Bits(arena.Now), x = Bits(p.Position.x), y = Bits(p.Position.y), vx = Bits(p.velocity.x), vy = Bits(p.velocity.y),
                hp = Bits(target.health), hits = target.ReceivedHitCount, otherHp = Bits(other.health), otherHits = other.ReceivedHitCount, playerHp = Bits(p.health),
                age = cast ? Bits(cast.MissileFlightAge) : 0, end = cast ? Bits(cast.EndTime) : 0, available = p.weapons[0].Cooldown.Available,
                ready = Bits(p.weapons[0].Cooldown.NextReady), period = Bits(p.weapons[0].Cooldown.Period), explosions = arena.BombExplosionCount,
                blastX = Bits(arena.LastBombExplosion.x), blastY = Bits(arena.LastBombExplosion.y), slot = p.selected, attempts = attempts, detonations = detonations,
                current = cast && ReferenceEquals(p.currentCast, cast), flying = cast && cast.MissileFlying, recoiling = cast && cast.MissileRecoiling,
                moving = p.IsForcedMoving, freeze = p.currentCast && p.currentCast.FreezeVertical, invulnerable = p.Invulnerable,
                grounded = p.grounded, wall = p.wallHit, visual = p.visual && p.visual.enabled, resources = cast && cast.ResourcesActive, faulted = arena.SimulationFaulted });
        }
        static void Run(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver, Case scenario, int facing)
        {
            driver.Reset(); var p = arena.Player; p.selected = 0; p.InputBlocked = false;
            foreach (var weapon in p.weapons) weapon.StatSlots.Clear();
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(40, 1000); enemy.health = 1000; enemy.SyncFacing(-facing); }
            Assert.That(p.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == "bomb"), 2, out var error), Is.True, error);
            p.Position = new Vector2(0, scenario == Case.Air ? 1007 : 1000); p.velocity = scenario == Case.Air ? new Vector2(0, -4) : Vector2.zero;
            p.grounded = scenario != Case.Air; p.SyncFacing(facing);
            var geometry = new List<GameObject>();
            try
            {
                if (scenario == Case.Enemy || scenario == Case.Duplicate) arena.enemies[0].Position = new Vector2(2 * facing, 1000);
                if (scenario == Case.WallBeforeEnemy || scenario == Case.BlastOcclusion)
                {
                    Surface(geometry, "MissileTraceWall", new Vector2(2 * facing, 1001), new Vector2(.04f, 4));
                    arena.enemies[0].Position = new Vector2(3.5f * facing, 1000);
                    if (scenario == Case.BlastOcclusion) arena.enemies[1].Position = new Vector2(1.5f * facing, 1002.2f);
                }
                if (scenario == Case.OneWay) Surface(geometry, "MissileTraceOneWay", new Vector2(2 * facing, 1001), new Vector2(.02f, 4), true);
                if (scenario == Case.ThinWall) Surface(geometry, "MissileTraceThinWall", new Vector2(1.7f * facing, 1001), new Vector2(.002f, 4));
                if (scenario == Case.FloorMargin) Surface(geometry, "MissileTraceThinFloor", new Vector2(2 * facing, 999.999f), new Vector2(4, .002f));
                if (scenario == Case.Duplicate)
                {
                    var target = arena.enemies[0]; var duplicate = new GameObject("MissileTraceDuplicate"); geometry.Add(duplicate);
                    duplicate.transform.SetParent(target.hurtCollider.transform, false);
                    var box = duplicate.AddComponent<BoxCollider2D>(); box.size = target.hurtCollider.size; box.offset = target.hurtCollider.offset; box.isTrigger = true;
                    duplicate.AddComponent<HurtRegion>().actor = target;
                }
                if (scenario == Case.ActivationCapture) arena.enemies[0].Position = new Vector2(4 * facing, 1000);
                Physics2D.SyncTransforms();
                var source = JsonUtility.FromJson<SkillData>(JsonUtility.ToJson(p.Form.specials[2]));
                var cast = CombatCast.Create(arena, p, source, p.Weapon, false); p.currentCast = cast;
                string label = scenario + "/" + facing; int attempts = 0, detonations = 0; bool exactAttempted = false;
                Observe(trace, label, -1, arena, cast, attempts, detonations);
                for (int tick = 0; tick < 90; tick++)
                {
                    if (scenario == Case.ActivationCapture && tick == 4) { p.Weapon.StatSlots[0] = 3; p.Weapon.StatSlots[1] = 2; p.Weapon.StatSlots[3] = 4; }
                    if (scenario == Case.ActivationCapture && tick == 11) p.Weapon.StatSlots.Clear();
                    if (scenario == Case.CancelBefore && tick == 4 || scenario == Case.CancelAfter && tick == 14) p.CancelAction();
                    if (scenario == Case.DeathBefore && tick == 4 || scenario == Case.DeathAfter && tick == 14) { p.health = 0; p.Die(); }
                    if (scenario == Case.ResetBefore && tick == 4 || scenario == Case.ResetAfter && tick == 14) driver.Reset();
                    if (scenario == Case.ExactDelay && !exactAttempted && cast && cast.MissileFlying && cast.MissileFlightAge + .000001f >= source.missile.manualDelay)
                    {
                        exactAttempted = true; attempts++; if (cast.TryMissileDetonate()) detonations++;
                    }
                    if ((scenario == Case.RecoilWall || scenario == Case.RecoilCeiling || scenario == Case.RecoilLanding) && tick == 21)
                    {
                        if (scenario == Case.RecoilWall) Surface(geometry, "MissileTraceRecoilWall", p.Center - Vector2.right * facing * (p.size.x / 2 + .145f), new Vector2(.05f, 4));
                        if (scenario == Case.RecoilCeiling) Surface(geometry, "MissileTraceRecoilCeiling", new Vector2(p.Center.x, p.Position.y + 2.145f), new Vector2(4, .05f));
                        if (scenario == Case.RecoilLanding) Surface(geometry, "MissileTraceRecoilLanding", new Vector2(p.Center.x - .3f * facing, p.Position.y + .275f), new Vector2(2, .05f), true);
                        Physics2D.SyncTransforms(); attempts++; if (cast.TryMissileDetonate()) detonations++;
                    }
                    var buttons = CombatButtons.None;
                    if ((scenario == Case.EarlyOnly || scenario == Case.NewInput) && tick == 13) buttons = CombatButtons.Special;
                    if (scenario == Case.NewInput && tick == 21) buttons = CombatButtons.Special;
                    if (scenario == Case.NewInput && tick == 15) buttons = CombatButtons.Basic | CombatButtons.Dash | CombatButtons.Jump | CombatButtons.Swap;
                    driver.Step(.02f, new CombatInput(scenario == Case.NewInput ? -facing : 0, buttons));
                    Observe(trace, label, tick, arena, cast, attempts, detonations);
                    Assert.That(arena.SimulationFaulted, Is.False, label + "/" + tick);
                    if ((scenario == Case.EarlyOnly || scenario == Case.NewInput) && tick == 20) Assert.That(cast.MissileFlying, Is.True, "An early press is not queued.");
                }
                if (scenario == Case.ExactDelay) Assert.That(detonations, Is.EqualTo(1), label);
                if (scenario == Case.CancelBefore || scenario == Case.CancelAfter || scenario == Case.DeathBefore || scenario == Case.DeathAfter || scenario == Case.ResetBefore || scenario == Case.ResetAfter)
                    Assert.That(arena.BombExplosionCount, Is.Zero, label);
                else Assert.That(arena.BombExplosionCount, Is.EqualTo(1), label);
            }
            finally
            {
                foreach (var go in geometry) if (go) { go.SetActive(false); Object.Destroy(go); }
                Physics2D.SyncTransforms();
            }
        }
        static IEnumerator Exercise(string scene, string traceName)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); Assert.That(Application.isPlaying, Is.True); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var trace = new Trace(); var geometry = new List<GameObject>();
            Surface(geometry, "MissileTraceFloor", new Vector2(0, 999.5f), new Vector2(200, 1)); Physics2D.SyncTransforms();
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                foreach (Case scenario in Enum.GetValues(typeof(Case)))
                { Run(trace, arena, driver, scenario, 1); Run(trace, arena, driver, scenario, -1); }
                var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-missileExecutionTraceRoot");
                if (at >= 0 && at + 1 < args.Length)
                {
                    string directory = Path.GetFullPath(args[at + 1]); Directory.CreateDirectory(directory);
                    File.WriteAllText(Path.Combine(directory, traceName + ".json"), JsonUtility.ToJson(trace, false));
                }
            }
            finally { foreach (var go in geometry) if (go) { go.SetActive(false); Object.Destroy(go); } }
        }
    }
}
