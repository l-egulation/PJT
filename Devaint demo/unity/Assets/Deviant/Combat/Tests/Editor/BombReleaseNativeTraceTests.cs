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
    // Frozen against the pre-extraction public Native API. Both compositions
    // observe real releases, resources and independent bombs, not a second queue.
    public class BombReleaseNativeTraceTests
    {
        [Serializable] sealed class BombRow
        {
            public int x, y, vx, vy, damage, multiplier, radius, blast, fuse, remaining;
            public bool landed;
        }
        [Serializable] sealed class Row
        {
            public string scenario, phase;
            public int now, x, y, vx, vy, playerHp, age, impact, end, emitted, explosions, casts, slot, available, ready, period, resourceStatus, resourceAt, resourceDamage;
            public bool current, resources, faulted;
            public BombRow[] bombs;
        }
        [Serializable] sealed class Trace { public List<Row> rows = new List<Row>(); }
        [UnityTest] public IEnumerator GameBombReleaseNativeTrace() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "game");
        [UnityTest] public IEnumerator LabBombReleaseNativeTrace() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "lab");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        static int Emitted(CombatArena arena) => arena.bombs.Count + arena.BombExplosionCount;
        static void Record(Trace trace, string scenario, string phase, CombatArena arena, CombatCast cast)
        {
            var p = arena.Player; var receipt = cast.ResourceResult; var snapshot = receipt?.Snapshot;
            var bombs = new BombRow[arena.bombs.Count];
            for (int i = 0; i < bombs.Length; i++)
            {
                var bomb = arena.bombs[i]; var definition = bomb.Definition;
                bombs[i] = new BombRow { x = Bits(bomb.Position.x), y = Bits(bomb.Position.y), vx = Bits(bomb.velocity.x), vy = Bits(bomb.velocity.y), damage = Bits(bomb.damage), multiplier = Bits(bomb.fragmentMultiplier),
                    radius = Bits(definition.Radius), blast = Bits(definition.BlastRadius), fuse = Bits(definition.FuseSeconds), remaining = Bits(bomb.TimedFuseRemaining), landed = bomb.TimedLanded };
            }
            trace.rows.Add(new Row { scenario = scenario, phase = phase, now = Bits(arena.Now), x = Bits(p.Position.x), y = Bits(p.Position.y), vx = Bits(p.velocity.x), vy = Bits(p.velocity.y), playerHp = Bits(p.health),
                age = Bits(cast.Age), impact = Bits(cast.ImpactTime), end = Bits(cast.EndTime), emitted = Emitted(arena), explosions = arena.BombExplosionCount, casts = arena.casts.Count, slot = p.selected,
                available = p.weapons[0].Cooldown.Available, ready = Bits(p.weapons[0].Cooldown.NextReady), period = Bits(p.weapons[0].Cooldown.Period), resourceStatus = receipt == null ? -1 : (int)receipt.Status,
                resourceAt = snapshot == null ? 0 : Bits(snapshot.ActivatedAt), resourceDamage = snapshot == null ? 0 : Bits(snapshot.DamageMultiplier), current = ReferenceEquals(p.currentCast, cast),
                resources = cast.ResourcesActive, faulted = arena.SimulationFaulted, bombs = bombs });
        }
        static void Setup(CombatArena arena, CombatArena.ManualSimulation driver, int special = 0)
        {
            driver.Reset(); var p = arena.Player; p.selected = 0; p.InputBlocked = false; p.UseTestInput = true;
            foreach (var weapon in p.weapons) weapon.StatSlots.Clear();
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(40, 1000); enemy.velocity = Vector2.zero; }
            Assert.That(p.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, form => form.id == "bomb"), special, out var error), Is.True, error);
            p.Weapon.Cooldown.Reset(p.Special.capacity, p.Special.cooldown);
            p.Position = new Vector2(0, 1000); p.velocity = Vector2.zero; p.grounded = true; p.SyncFacing(1); Physics2D.SyncTransforms();
        }
        static CombatCast Start(CombatArena arena, SkillData source = null)
        {
            var p = arena.Player; var input = JsonUtility.FromJson<SkillData>(JsonUtility.ToJson(source ?? p.Special));
            var cast = CombatCast.Create(arena, p, input, p.Weapon, false); p.currentCast = cast; return cast;
        }
        static void Boundaries(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver)
        {
            Setup(arena, driver); var p = arena.Player; var cast = Start(arena);
            cast.BeforeMovement(.02f); Record(trace, "boundaries", "zero", arena, cast);
            Assert.That(Emitted(arena), Is.Zero); Assert.That(p.Weapon.Cooldown.Available, Is.EqualTo(1));
            driver.Step(.2f); Record(trace, "boundaries", "first-before-evaluation", arena, cast);
            Assert.That(Emitted(arena), Is.Zero, "Release uses the current clock, not this tick's end.");
            cast.BeforeMovement(.02f); Record(trace, "boundaries", "first", arena, cast);
            Assert.That(Emitted(arena), Is.EqualTo(1)); Assert.That(p.Weapon.Cooldown.Available, Is.Zero);
            float ready = p.Weapon.Cooldown.NextReady;
            Assert.That(cast.ResourceResult.Snapshot.ActivatedAt, Is.EqualTo(.2f));
            Assert.That(ready, Is.EqualTo(8.2f));
            cast.BeforeMovement(.02f); Assert.That(Emitted(arena), Is.EqualTo(1), "Activate and BeforeMovement share one release cursor.");
            driver.Step(.15f); cast.BeforeMovement(.02f); Record(trace, "boundaries", "second", arena, cast);
            Assert.That(Emitted(arena), Is.EqualTo(2));
            driver.Step(.15f); cast.BeforeMovement(.02f); Record(trace, "boundaries", "third", arena, cast);
            Assert.That(Emitted(arena), Is.EqualTo(3)); Assert.That(p.Weapon.Cooldown.NextReady, Is.EqualTo(ready));
            driver.Step(.4f); Record(trace, "boundaries", "recovery", arena, cast);
            Assert.That(Emitted(arena), Is.EqualTo(3)); Assert.That(p.currentCast, Is.Null);

            Setup(arena, driver); cast = Start(arena); driver.Step(.55f);
            Record(trace, "large-step", "crossed-all-before-evaluation", arena, cast);
            Assert.That(Emitted(arena), Is.Zero); Assert.That(p.currentCast, Is.SameAs(cast));
            cast.BeforeMovement(.02f); Record(trace, "large-step", "all-due", arena, cast);
            Assert.That(Emitted(arena), Is.EqualTo(3)); Assert.That(cast.ResourceResult.Snapshot.ActivatedAt, Is.EqualTo(.55f));
            cast.BeforeMovement(.02f); Assert.That(Emitted(arena), Is.EqualTo(3));

            Setup(arena, driver); var zero = JsonUtility.FromJson<SkillData>(JsonUtility.ToJson(p.Special)); zero.activation = zero.hits[0].time = 0;
            cast = Start(arena, zero); cast.BeforeMovement(.02f); cast.BeforeMovement(.02f); Record(trace, "zero-release", "activated-once", arena, cast);
            Assert.That(Emitted(arena), Is.EqualTo(1)); Assert.That(cast.ResourceResult.Snapshot.ActivatedAt, Is.Zero);
        }
        static void Cancellation(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver)
        {
            for (int count = 0; count <= 2; count++)
            {
                Setup(arena, driver); var p = arena.Player; var cast = Start(arena); string scenario = "cancel-after-" + count;
                if (count == 0) driver.Step(.125f);
                else
                {
                    driver.Step(.2f); cast.BeforeMovement(.02f);
                    if (count == 2) { driver.Step(.15f); cast.BeforeMovement(.02f); }
                }
                Assert.That(Emitted(arena), Is.EqualTo(count)); var emitted = arena.bombs.ToArray(); float ready = p.Weapon.Cooldown.NextReady;
                Record(trace, scenario, "before-cancel", arena, cast); p.CancelAction(); Record(trace, scenario, "cancel", arena, cast);
                Assert.That(cast.ResourcesActive, Is.False); Assert.That(p.Weapon.Cooldown.Available, Is.EqualTo(count == 0 ? 1 : 0));
                if (count == 0) Assert.That(cast.ResourceResult, Is.Null);
                else Assert.That(cast.ResourceResult.Status, Is.EqualTo(ActionResourceStatus.Succeeded));
                foreach (var bomb in emitted) Assert.That(arena.bombs.Contains(bomb), Is.True, "Cancellation only removes unsent releases.");
                driver.Step(.08f); Record(trace, scenario, "independent-flight", arena, cast);
                foreach (var bomb in emitted) Assert.That(arena.bombs.Contains(bomb), Is.True);
                driver.Step(.6f); Record(trace, scenario, "past-last-release", arena, cast);
                Assert.That(Emitted(arena), Is.EqualTo(count)); Assert.That(p.Weapon.Cooldown.NextReady, Is.EqualTo(ready));
            }
        }
        static void ActivationCapture(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver)
        {
            Setup(arena, driver); var p = arena.Player; var weapon = p.Weapon;
            weapon.StatSlots[0] = 1; weapon.StatSlots[2] = 3; weapon.StatSlots[3] = 4;
            Assert.That(p.TryReconfigure(0, weapon.FormIndex, 0, out var error), Is.True, error);
            var cast = Start(arena); driver.Step(.125f); weapon.StatSlots[1] = 2;
            Record(trace, "activation-capture", "stats-before-first", arena, cast);
            driver.Step(.075f); Assert.That(Emitted(arena), Is.Zero); cast.BeforeMovement(.02f);
            Record(trace, "activation-capture", "first", arena, cast);
            Assert.That(arena.bombs[0].damage, Is.EqualTo(14.4f).Within(.00001f)); Assert.That(weapon.Cooldown.Period, Is.EqualTo(7.2f).Within(.00001f));
            weapon.StatSlots[1] = 0; p.SyncFacing(-1);
            driver.Step(.15f, new CombatInput(-1)); cast.BeforeMovement(.02f);
            driver.Step(.15f, new CombatInput(-1)); cast.BeforeMovement(.02f); Record(trace, "activation-capture", "third-after-stat-edit", arena, cast);
            Assert.That(arena.bombs.Count, Is.EqualTo(3)); Assert.That(p.Position.x, Is.EqualTo(0).Within(.00001f));
            foreach (var bomb in arena.bombs)
            { Assert.That(bomb.damage, Is.EqualTo(14.4f).Within(.00001f)); Assert.That(bomb.velocity.x, Is.GreaterThan(8)); }
            var first = arena.bombs[0]; p.CancelAction(); weapon.Cooldown.Reset(1, 8);
            var next = Start(arena); driver.Step(.2f); next.BeforeMovement(.02f); Record(trace, "activation-capture", "same-weapon-next-cast", arena, next);
            Assert.That(arena.bombs.Contains(first), Is.True); Assert.That(first.damage, Is.EqualTo(14.4f).Within(.00001f));
            Assert.That(arena.bombs[arena.bombs.Count - 1].damage, Is.EqualTo(12)); Assert.That(weapon.InstanceId, Is.EqualTo(p.Weapon.InstanceId));
        }
        static void BasicAndVariants(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver)
        {
            Setup(arena, driver); var p = arena.Player;
            p.Weapon.StatSlots[0] = p.Weapon.StatSlots[1] = 1; p.Weapon.StatSlots[2] = 2; p.Weapon.StatSlots[3] = p.Weapon.StatSlots[4] = 3;
            driver.Step(.01f, new CombatInput(0, CombatButtons.Basic)); var basic = p.currentCast;
            Assert.That(basic, Is.Not.Null); Record(trace, "basic", "start", arena, basic);
            driver.Step(basic.ImpactTime - arena.Now); Assert.That(Emitted(arena), Is.Zero);
            basic.BeforeMovement(.02f); Record(trace, "basic", "release", arena, basic);
            Assert.That(arena.bombs.Count, Is.EqualTo(1)); Assert.That(arena.bombs[0].damage, Is.EqualTo(25.2f).Within(.00001f));
            Assert.That(basic.ResourceResult.Snapshot.DamageMultiplier, Is.EqualTo(1.4f)); Assert.That(p.Weapon.Cooldown.Available, Is.EqualTo(1));
            var emitted = arena.bombs[0]; p.CancelAction(); driver.Step(.02f); Assert.That(arena.bombs.Contains(emitted), Is.True);

            foreach (int special in new[] { 1, 3 })
            {
                Setup(arena, driver, special); p.Weapon.StatSlots[0] = 1; p.Weapon.StatSlots[1] = 2;
                var cast = Start(arena); driver.Step(p.Special.activation); cast.BeforeMovement(.02f);
                string scenario = special == 1 ? "large-bomb" : "timed-bomb"; Record(trace, scenario, "release", arena, cast);
                Assert.That(arena.bombs.Count, Is.EqualTo(1)); var bomb = arena.bombs[0];
                Assert.That(bomb.damage, Is.EqualTo(special == 1 ? 36 : 60).Within(.00001f));
                Assert.That(bomb.Definition.FuseSeconds, Is.EqualTo(special == 1 ? 0 : 1.5f));
                p.CancelAction(); p.selected = 1; driver.Step(.02f); Record(trace, scenario, "cancel-and-swap", arena, cast);
                Assert.That(arena.bombs.Contains(bomb), Is.True); Assert.That(cast.ResourcesActive, Is.False);
                Assert.That(cast.ResourceResult.Status, Is.EqualTo(ActionResourceStatus.Succeeded));
            }
        }
        static void Cleanup(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver)
        {
            foreach (bool reset in new[] { false, true })
            foreach (bool released in new[] { false, true })
            {
                Setup(arena, driver); var p = arena.Player; var cast = Start(arena); driver.Step(released ? .2f : .125f);
                if (released) cast.BeforeMovement(.02f);
                string scenario = (reset ? "reset" : "death") + (released ? "-after" : "-before"); Record(trace, scenario, "before-cleanup", arena, cast);
                if (reset) arena.ResetTest(); else { p.health = 0; p.Die(); }
                Record(trace, scenario, "cleanup", arena, cast);
                Assert.That(arena.bombs.Count, Is.Zero); Assert.That(arena.casts.Count, Is.Zero); Assert.That(p.currentCast, Is.Null); Assert.That(cast.ResourcesActive, Is.False);
                Assert.That(p.Weapon.Cooldown.Available, Is.EqualTo(reset || !released ? 1 : 0));
                if (released) Assert.That(cast.ResourceResult.Status, Is.EqualTo(ActionResourceStatus.Succeeded));
                else Assert.That(cast.ResourceResult, Is.Null);
                Assert.That(arena.BombExplosionCount, Is.Zero); Assert.That(arena.SimulationFaulted, Is.False);
            }
        }
        static IEnumerator Exercise(string scene, string label)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); Assert.That(Application.isPlaying, Is.True); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var trace = new Trace();
            var floor = new GameObject("BombReleaseTraceFloor"); floor.transform.position = new Vector3(0, 999.5f, 0);
            floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(200, 1); Physics2D.SyncTransforms();
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                { Boundaries(trace, arena, driver); Cancellation(trace, arena, driver); ActivationCapture(trace, arena, driver); BasicAndVariants(trace, arena, driver); Cleanup(trace, arena, driver); }
                var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-bombReleaseExecutionTraceRoot");
                if (at >= 0 && at + 1 < args.Length) { Directory.CreateDirectory(args[at + 1]); File.WriteAllText(Path.Combine(args[at + 1], label + ".json"), JsonUtility.ToJson(trace)); }
            }
            finally { Object.Destroy(floor); }
        }
    }
}
