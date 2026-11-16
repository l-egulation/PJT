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
    // Public Native contract includes confirmed immediate exit/expiry removal.
    public class SmokeEffectNativeTraceTests
    {
        [Serializable] sealed class Row
        {
            public string label;
            public int now, x, y, smoke, stealth, health, age, end, slot, casts, available, ready;
            public bool hidden, current, tracked, resources, invulnerable, dead, faulted;
        }
        [Serializable] sealed class Trace { public List<Row> rows = new List<Row>(); }
        [UnityTest] public IEnumerator GameSmokeNativeTrace() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "game");
        [UnityTest] public IEnumerator LabSmokeNativeTrace() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "lab");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        static SkillData Source(CombatArena arena, int special = 1) => JsonUtility.FromJson<SkillData>(JsonUtility.ToJson(Array.Find(arena.catalog.forms, f => f.id == "dagger").specials[special]));
        static void Observe(Trace trace, string label, CombatArena arena, CombatCast cast)
        {
            var p = arena.Player;
            trace.rows.Add(new Row { label = label, now = Bits(arena.Now), x = Bits(p.Position.x), y = Bits(p.Position.y), smoke = Bits(p.smokeUntil), stealth = Bits(p.stealthUntil), health = Bits(p.health),
                age = cast ? Bits(cast.Age) : 0, end = cast ? Bits(cast.EndTime) : 0, slot = p.selected, casts = arena.casts.Count, available = p.Weapon.Cooldown.Available, ready = Bits(p.Weapon.Cooldown.NextReady),
                hidden = p.Hidden, current = ReferenceEquals(p.currentCast, cast), tracked = arena.casts.Contains(cast), resources = cast && cast.ResourcesActive, invulnerable = p.Invulnerable, dead = p.Dead, faulted = arena.SimulationFaulted });
        }
        static void Prepare(CombatArena arena, CombatArena.ManualSimulation driver, int secondSpecial = 1)
        {
            driver.Reset(); var p = arena.Player; p.selected = 0; p.InputBlocked = false;
            foreach (var weapon in p.weapons) weapon.StatSlots.Clear();
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(40, 1000); }
            int dagger = Array.FindIndex(arena.catalog.forms, f => f.id == "dagger");
            Assert.That(p.TryReconfigure(0, dagger, 1, out var error), Is.True, error);
            Assert.That(p.TryReconfigure(1, dagger, secondSpecial, out error), Is.True, error);
            p.Position = new Vector2(0, 1000); p.velocity = Vector2.zero; p.grounded = true; p.SyncFacing(1); Physics2D.SyncTransforms();
        }
        static CombatCast Start(CombatArena arena, SkillData source = null)
        {
            var p = arena.Player; var cast = CombatCast.Create(arena, p, source ?? Source(arena), p.Weapon, false); p.currentCast = cast; return cast;
        }
        static void Ticks(CombatArena.ManualSimulation driver, int count) { for (int i = 0; i < count; i++) driver.Step(.02f); }
        static void Move(CombatArena arena, float x) { arena.Player.Position = new Vector2(x, 1000); arena.Player.velocity = Vector2.zero; Physics2D.SyncTransforms(); }
        static void SpatialRefresh(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver)
        {
            Prepare(arena, driver); var p = arena.Player; var cast = Start(arena);
            Ticks(driver, 7); Assert.That(p.Hidden, Is.False); Observe(trace, "before-activation", arena, cast);
            driver.Step(.02f); Assert.That(p.Hidden, Is.True); Assert.That(p.Invulnerable, Is.True); Observe(trace, "initial-enter", arena, cast);
            Ticks(driver, 2); float deadline = p.smokeUntil; Move(arena, 8);
            driver.Step(.005f); Assert.That(p.Hidden, Is.False); Assert.That(p.smokeUntil, Is.Zero); Observe(trace, "outside-clears-smoke", arena, cast);
            driver.Step(deadline - arena.Now); Assert.That(arena.Now, Is.EqualTo(deadline)); Assert.That(p.Hidden, Is.False); Observe(trace, "outside-stays-visible", arena, cast);
            Move(arena, 0); driver.Step(.02f); Assert.That(p.Hidden, Is.True); Assert.That(p.smokeUntil, Is.GreaterThan(deadline)); Observe(trace, "reenter", arena, cast);
            deadline = p.smokeUntil;
            Move(arena, 3 + p.size.x / 2 - p.offset.x * p.facing); driver.Step(.02f);
            Assert.That(p.smokeUntil, Is.GreaterThan(deadline), "Native Bounds includes a touching edge."); Observe(trace, "bounds-touch", arena, cast);
            deadline = p.smokeUntil; Move(arena, p.Position.x + .0005f); driver.Step(.002f);
            Assert.That(p.smokeUntil, Is.Zero, "Beyond the same edge smoke concealment is cleared."); Assert.That(p.Hidden, Is.False); Observe(trace, "bounds-separated", arena, cast);
        }
        static void ExactEffectBoundary(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver)
        {
            Prepare(arena, driver); var p = arena.Player; var source = Source(arena);
            // Binary-exact authored times retain the action past effect expiry so the
            // actual Native tick observes both sides of the exclusive effect boundary.
            source.activation = .125f; source.effect.duration = .5f; source.end = 1;
            var cast = Start(arena, source); driver.Step(.125f); driver.Step(.375f); driver.Step(.0625f); driver.Step(.0625f);
            Assert.That(arena.Now, Is.EqualTo(.625f)); Assert.That(p.smokeUntil, Is.EqualTo(.625f)); Assert.That(p.Hidden, Is.False); Observe(trace, "effect-deadline", arena, cast);
            float deadline = p.smokeUntil; driver.Step(.015625f);
            Assert.That(p.smokeUntil, Is.Zero); Assert.That(p.Hidden, Is.False); Assert.That(arena.casts.Contains(cast), Is.True); Observe(trace, "expired-effect-cleared", arena, cast);
            driver.Step(.015625f); Assert.That(p.Hidden, Is.False); Observe(trace, "expired-effect-stays-visible", arena, cast);
            driver.Step(.34375f); Assert.That(arena.casts.Contains(cast), Is.False); Assert.That(cast.ResourcesActive, Is.False); Observe(trace, "longer-primary-cleanup", arena, cast);
        }
        static void AttackAndReplacement(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver)
        {
            Prepare(arena, driver); var p = arena.Player; var old = Start(arena); Ticks(driver, 10); p.CancelAction();
            float deadline = p.smokeUntil; driver.Step(.02f);
            Assert.That(p.currentCast, Is.Null); Assert.That(arena.casts.Contains(old), Is.True); Assert.That(old.ResourcesActive, Is.False); Assert.That(p.smokeUntil, Is.GreaterThan(deadline)); Observe(trace, "cancel-preserves-smoke", arena, old);
            driver.Step(.02f, new CombatInput(0, CombatButtons.Basic));
            Assert.That(p.currentCast && p.currentCast.isBasic, Is.True); Assert.That(p.Hidden, Is.False); Assert.That(p.smokeUntil, Is.Zero); Observe(trace, "attack-spends-smoke", arena, old);
            p.CancelAction(); Move(arena, 8); driver.Step(.02f); Move(arena, 0); driver.Step(.02f);
            Assert.That(p.Hidden, Is.False); Assert.That(arena.casts.Contains(old), Is.True); Observe(trace, "spent-reentry", arena, old);
            driver.Step(.02f, new CombatInput(0, CombatButtons.Swap)); Assert.That(p.selected, Is.EqualTo(1));
            var replacement = Start(arena); Ticks(driver, 8);
            Assert.That(arena.casts.Contains(old), Is.False); Assert.That(old.ResourcesActive, Is.False); Assert.That(arena.casts.Contains(replacement), Is.True); Assert.That(p.Hidden, Is.True, "An attack does not globally spend later smoke instances."); Observe(trace, "new-instance-replaces-spent", arena, replacement);
        }
        static void CoexistingStealth(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver)
        {
            Prepare(arena, driver, 0); var p = arena.Player; var smoke = Start(arena); Ticks(driver, 10); p.CancelAction();
            driver.Step(.02f, new CombatInput(0, CombatButtons.Swap)); Assert.That(p.Hidden, Is.True);
            Start(arena, Source(arena, 0)); Ticks(driver, 6); p.CancelAction();
            Assert.That(p.stealthUntil, Is.GreaterThan(arena.Now)); Assert.That(p.smokeUntil, Is.GreaterThan(arena.Now)); Observe(trace, "coexisting-sources", arena, smoke);
            Move(arena, 8); driver.Step(.05f);
            Assert.That(p.smokeUntil, Is.LessThanOrEqualTo(arena.Now)); Assert.That(p.Hidden, Is.True); Observe(trace, "outside-keeps-basic-stealth", arena, smoke);
            driver.Step(.02f, new CombatInput(0, CombatButtons.Basic));
            Assert.That(p.currentCast && p.currentCast.isBasic, Is.True); Assert.That(p.smokeUntil, Is.Zero); Assert.That(p.stealthUntil, Is.Zero); Assert.That(p.Hidden, Is.False); Observe(trace, "attack-clears-both-sources", arena, smoke);
            p.CancelAction(); Move(arena, 0); driver.Step(.02f); Assert.That(p.Hidden, Is.False); Observe(trace, "coexist-spent-reentry", arena, smoke);
        }
        static void LifetimeAndCleanup(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver)
        {
            Prepare(arena, driver); var p = arena.Player; var cast = Start(arena); Ticks(driver, 26);
            Assert.That(p.currentCast, Is.Null); Assert.That(arena.casts.Contains(cast), Is.True); Assert.That(p.Hidden, Is.True); Observe(trace, "primary-done-effect-alive", arena, cast);
            driver.Step(.02f, new CombatInput(0, CombatButtons.Swap));
            Assert.That(p.selected, Is.EqualTo(1)); Assert.That(arena.casts.Contains(cast), Is.True); Assert.That(p.Hidden, Is.True); Observe(trace, "swap-keeps-smoke", arena, cast);
            Ticks(driver, 132); Assert.That(arena.casts.Contains(cast), Is.False); Assert.That(cast.ResourcesActive, Is.False); Observe(trace, "natural-lifetime-cleanup", arena, cast);

            Prepare(arena, driver); cast = Start(arena); Ticks(driver, 10); p.health = 0; p.Die();
            Assert.That(arena.casts.Contains(cast), Is.False); Assert.That(cast.ResourcesActive, Is.False); Observe(trace, "death-removes-effect", arena, cast);
            driver.Step(.04f); Assert.That(p.Hidden, Is.False); Observe(trace, "death-grace-expires", arena, cast);
            driver.Reset(); Assert.That(p.Dead, Is.False); Assert.That(p.Hidden, Is.False); Assert.That(arena.casts.Count, Is.Zero); Observe(trace, "reset-after-death", arena, cast);

            Prepare(arena, driver); cast = Start(arena); Ticks(driver, 10); float now = arena.Now; arena.ResetTest();
            Assert.That(arena.Now, Is.EqualTo(now)); Assert.That(arena.casts.Contains(cast), Is.False); Assert.That(cast.ResourcesActive, Is.False); Assert.That(p.smokeUntil, Is.Zero); Assert.That(p.Hidden, Is.False); Observe(trace, "live-reset-removes-effect", arena, cast);
        }
        static IEnumerator Exercise(string scene, string traceName)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); Assert.That(Application.isPlaying, Is.True); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var trace = new Trace();
            var floor = new GameObject("SmokeNativeTraceFloor"); floor.transform.position = new Vector3(0, 999.5f, 0); floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(200, 1); Physics2D.SyncTransforms();
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    SpatialRefresh(trace, arena, driver); ExactEffectBoundary(trace, arena, driver); AttackAndReplacement(trace, arena, driver); CoexistingStealth(trace, arena, driver); LifetimeAndCleanup(trace, arena, driver);
                    Assert.That(arena.SimulationFaulted, Is.False);
                }
                var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-smokeEffectTraceRoot");
                if (at >= 0 && at + 1 < args.Length)
                {
                    string directory = Path.GetFullPath(args[at + 1]); Directory.CreateDirectory(directory);
                    File.WriteAllText(Path.Combine(directory, traceName + ".json"), JsonUtility.ToJson(trace, false));
                }
            }
            finally { Object.Destroy(floor); }
        }
    }
}
