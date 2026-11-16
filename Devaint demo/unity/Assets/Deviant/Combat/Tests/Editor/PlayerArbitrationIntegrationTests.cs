using System;
using System.Collections;
using Deviant.Combat.Contracts;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class PlayerArbitrationIntegrationTests
    {
        [UnityTest] public IEnumerator GameUsesSharedArbitration() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabUsesSharedArbitration() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene);
            Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var p = arena.Player;
            using (var driver = arena.AcquireManualSimulation())
            {
                Reset(arena, driver);
                driver.Step(.02f, new CombatInput(1, CombatButtons.Dash | CombatButtons.Jump | CombatButtons.Special | CombatButtons.Basic | CombatButtons.Swap));
                Assert.That(p.Dashing, Is.True); Assert.That(p.currentCast, Is.Null); Assert.That(p.selected, Is.Zero);
                Idle(driver, 20);
                driver.Step(.02f, new CombatInput(0, CombatButtons.Basic)); Assert.That(p.currentCast, Is.Not.Null);
                driver.Step(.02f, new CombatInput(0, CombatButtons.Dash | CombatButtons.Jump));
                Assert.That(p.Dashing, Is.False, "The dash is still recovering; a valid jump cancels the basic instead.");
                Assert.That(p.currentCast, Is.Null); Assert.That(p.velocity.y, Is.GreaterThan(0));

                Reset(arena, driver);
                driver.Step(.02f, new CombatInput(0, CombatButtons.Basic)); var first = p.currentCast;
                driver.Step(.02f, new CombatInput(0, CombatButtons.Basic | CombatButtons.Special));
                driver.Step(.02f, new CombatInput(0, CombatButtons.Swap)); Idle(driver, 30);
                Assert.That(p.selected, Is.EqualTo(1)); Assert.That(p.currentCast, Is.Null);
                Assert.That(p.weapons[0].Cooldown.Available, Is.EqualTo(1), "Overridden special reservations spend nothing.");
                p.AttackFinished(first); Assert.That(p.selected, Is.EqualTo(1), "Stale completions cannot switch again.");

                Reset(arena, driver);
                driver.Step(.02f, new CombatInput(0, CombatButtons.Basic));
                driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); p.CancelAction(); Idle(driver, 30);
                Assert.That(p.currentCast, Is.Null); Assert.That(p.Weapon.Cooldown.Available, Is.EqualTo(1));
                driver.Step(.02f, new CombatInput(0, CombatButtons.Basic));
                driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); driver.Reset(); Idle(driver, 30);
                Assert.That(p.currentCast, Is.Null); Assert.That(p.Weapon.Cooldown.Available, Is.EqualTo(1));

                Reset(arena, driver);
                p.Reconfigure(1, Array.FindIndex(arena.catalog.forms, f => f.id == "base_sword"), 0);
                p.SetWeaponSlotReady(1, false);
                driver.Step(.02f, new CombatInput(pressed: CombatButtons.Weapon2Basic));
                Assert.That(p.selected, Is.Zero); Assert.That(p.currentCast, Is.Null);
                p.SetWeaponSlotReady(1, true);
                driver.Step(.02f, new CombatInput(pressed: CombatButtons.Weapon2Basic));
                Assert.That(p.selected, Is.EqualTo(1)); Assert.That(p.currentCast, Is.Not.Null);
                var targetedBasic = p.currentCast;
                p.SetWeaponSlotReady(0, false);
                driver.Step(.02f, new CombatInput(pressed: CombatButtons.Weapon1Special));
                Assert.That(p.selected, Is.EqualTo(1)); Assert.That(p.currentCast, Is.SameAs(targetedBasic), "An unready target slot keeps the running basic.");
                p.SetWeaponSlotReady(0, true);
                driver.Step(.02f, new CombatInput(pressed: CombatButtons.Weapon1Special));
                Assert.That(p.selected, Is.Zero); Assert.That(p.currentCast, Is.Not.Null.And.Not.SameAs(targetedBasic));
                Assert.That(p.currentCast.isBasic, Is.False, "An admitted other-slot skill replaces the running basic at once.");

                Reset(arena, driver);
                Assert.That(p.weapons[1].Cooldown.Spend(arena.Now), Is.True);
                driver.Step(.02f, new CombatInput(pressed: CombatButtons.Weapon1Basic));
                var uninterrupted = p.currentCast;
                driver.Step(.02f, new CombatInput(pressed: CombatButtons.Weapon2Special));
                Assert.That(p.selected, Is.Zero); Assert.That(p.currentCast, Is.SameAs(uninterrupted), "An empty other-slot charge cannot replace the running basic.");
                p.CancelAction();
                driver.Step(.02f, new CombatInput(pressed: CombatButtons.Weapon2Basic, heldSpecials: CombatButtons.Weapon1Special));
                Assert.That(p.selected, Is.EqualTo(1)); Assert.That(p.specialHeld, Is.False);
                driver.Step(.02f, new CombatInput(heldSpecials: CombatButtons.Weapon1Special | CombatButtons.Weapon2Special));
                Assert.That(p.specialHeld, Is.True);
                driver.Step(.02f, new CombatInput(heldSpecials: CombatButtons.Weapon1Special));
                Assert.That(p.specialHeld, Is.False, "Holding E cannot sustain slot 2 after R is released.");

                Reset(arena, driver); p.InputBlocked = true;
                driver.Step(.02f, new CombatInput(1, CombatButtons.Basic | CombatButtons.Special | CombatButtons.Dash | CombatButtons.Jump | CombatButtons.Swap, true));
                Assert.That(p.axis, Is.Zero); Assert.That(p.specialHeld, Is.False);
                Assert.That(p.currentCast, Is.Null); Assert.That(p.Dashing, Is.False); Assert.That(p.selected, Is.Zero);
                p.InputBlocked = false; Idle(driver, 30); Assert.That(p.currentCast, Is.Null);

                // An ordinary test reset clears edges/reservations, but keeps sampled holds.
                p.SubmitInput(new CombatInput(-1, CombatButtons.Basic, true)); arena.ResetTest();
                Assert.That(p.axis, Is.EqualTo(-1)); Assert.That(p.specialHeld, Is.True);
                driver.Reset(); Assert.That(p.axis, Is.Zero); Assert.That(p.specialHeld, Is.False);
                driver.Step(.02f, new CombatInput(0, CombatButtons.Basic));
                driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); p.health = 0; p.Die();
                driver.Step(.02f, new CombatInput(0, CombatButtons.Basic)); Assert.That(p.currentCast, Is.Null);
                driver.Reset(); Idle(driver, 30); Assert.That(p.currentCast, Is.Null);
            }
        }
        static void Idle(CombatArena.ManualSimulation driver, int ticks)
        { for (int i = 0; i < ticks; i++) driver.Step(.02f); }
        static void Reset(CombatArena arena, CombatArena.ManualSimulation driver)
        {
            driver.Reset(); var p = arena.Player; p.selected = 0;
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 0); }
            Assert.That(p.EditingAllowed, Is.True);
            p.Reconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == "base_sword"), 0);
            Assert.That(p.Form.id, Is.EqualTo("base_sword"));
            p.Position = Vector2.zero; p.SyncFacing(1);
            Physics2D.SyncTransforms();
        }
    }
}
