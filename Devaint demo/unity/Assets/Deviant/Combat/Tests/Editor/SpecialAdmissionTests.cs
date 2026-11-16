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
    public class SpecialAdmissionTests
    {
        const string Game = "Assets/Deviant/Game/Scenes/deviant.unity", Lab = "Assets/Deviant/Combat/Scenes/CombatV2Arena.unity";
        [UnityTest] public IEnumerator GameRejectsAirborneGroundedSpecialReservation() => Exercise(Game, true);
        [UnityTest] public IEnumerator LabRejectsAirborneGroundedSpecialReservation() => Exercise(Lab, true);
        [UnityTest] public IEnumerator GameRejectsTargetlessSpecialReservation() => Exercise(Game, false);
        [UnityTest] public IEnumerator LabRejectsTargetlessSpecialReservation() => Exercise(Lab, false);
        [UnityTest] public IEnumerator GameRechecksGroundWhenQueuedSpecialStarts() => Exercise(Game, true, true);
        [UnityTest] public IEnumerator LabRechecksGroundWhenQueuedSpecialStarts() => Exercise(Lab, true, true);
        [UnityTest] public IEnumerator GameRechecksTargetWhenQueuedSpecialStarts() => Exercise(Game, false, true);
        [UnityTest] public IEnumerator LabRechecksTargetWhenQueuedSpecialStarts() => Exercise(Lab, false, true);
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        static IEnumerator Exercise(string scene, bool requiresGround, bool losesPrerequisite = false)
        {
            LegacySceneFixture.Open(scene);
            Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var p = arena.Player;
            // A common explicit floor fixture; the game scene need not have terrain at x=0.
            var floor = new GameObject("SpecialAdmissionFloor"); floor.transform.position = new Vector3(0, -.5f, 0);
            floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(20, 1);
            using (var driver = arena.AcquireManualSimulation())
            {
                driver.Reset(); p.selected = 0;
                foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 0); }
                string form = requiresGround ? "hammer" : "whip";
                p.Reconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == form), requiresGround ? 1 : 0);
                Assert.That(p.Form.id, Is.EqualTo(form), "The fixture must equip the requested form before testing its admission rules.");
                Assert.That(p.Special.id, Is.EqualTo(requiresGround ? "hammer_range" : "whip_default"));
                p.SyncFacing(1);
                Place(arena, requiresGround, losesPrerequisite);
                driver.Step(.02f, new CombatInput(0, CombatButtons.Basic)); var basic = p.currentCast;
                Assert.That(basic && basic.isBasic, Is.True);
                driver.Step(.02f, new CombatInput(0, CombatButtons.Special));
                Assert.That(p.Weapon.Cooldown.Available, Is.EqualTo(1));

                // Check both boundaries: invalid input becoming eligible later,
                // and valid input whose prerequisite disappears before execution.
                Place(arena, requiresGround, !losesPrerequisite);
                for (int i = 0; i < 150 && p.currentCast == basic; i++) driver.Step(.02f);
                Assert.That(p.currentCast, Is.Null, "Both reservation and execution must meet the special's prerequisites.");
                Assert.That(p.Weapon.Cooldown.Available, Is.EqualTo(1));
                Place(arena, requiresGround, true);
                driver.Step(.02f, new CombatInput(0, CombatButtons.Special));
                Assert.That(p.currentCast && !p.currentCast.isBasic, Is.True, "A new eligible press must still start the selected special.");
            }
        }
        static void Place(CombatArena arena, bool requiresGround, bool eligible)
        {
            var p = arena.Player;
            p.Position = requiresGround && !eligible ? new Vector2(0, 100) : Vector2.zero;
            p.velocity = Vector2.zero; p.grounded = !requiresGround || eligible;
            arena.enemies[0].Position = new Vector2(!requiresGround && eligible ? 2 : 35, 0);
            Physics2D.SyncTransforms();
            if (requiresGround) Assert.That(p.ProbeGround(), Is.EqualTo(eligible), "Fixture ground condition");
            else Assert.That(arena.NearestInBox(p, p.Special.hits[0].box) != null, Is.EqualTo(eligible), "Fixture target condition");
        }
    }
}
