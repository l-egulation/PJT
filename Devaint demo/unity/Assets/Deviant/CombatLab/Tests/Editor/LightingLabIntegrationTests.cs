using System;
using System.Collections;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Integration;
using Deviant.Presentation.Unity;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.CombatLab.Tests
{
    public sealed class LightingLabIntegrationTests
    {
        [UnitySetUp] public IEnumerator Setup()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Lighting integration fixture").AddComponent<LightingLabComposition>();
            yield return new EnterPlayMode(); yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static LightingLabComposition Lab()
        { var lab = Object.FindAnyObjectByType<LightingLabComposition>(); Assert.That(lab && lab.Ready, Is.True); return lab; }
        static void Advance(CombatArena.ManualSimulation driver, float seconds)
        { for (int i = 0; i < Mathf.CeilToInt(seconds / .02f); i++) driver.Step(.02f); }
        [UnityTest] public IEnumerator ActualMeleeSweepsShakeANonActorPropOnceWithoutAnEnemyHit()
        {
            var lab = Lab(); var arena = lab.Arena;
            using (var driver = arena.AcquireManualSimulation())
            {
                driver.Reset(); arena.enemies[0].Position = new Vector2(7, 0); Physics2D.SyncTransforms();
                driver.Step(.02f, new CombatInput(pressed: CombatButtons.Weapon1Basic));
                Advance(driver, .16f);
                Assert.That(lab.Lantern.State.AcceptedAttacks, Is.EqualTo(1), "A missed enemy attack still touches the lantern during its real active sweep.");
                Assert.That(Mathf.Abs(lab.Lantern.State.AngularSpeed), Is.GreaterThan(.01f));
                Advance(driver, .5f); Assert.That(lab.Lantern.State.AcceptedAttacks, Is.EqualTo(1));
                Assert.That(arena.enemies[0].ReceivedHitCount, Is.Zero); Assert.That(arena.Player.health, Is.EqualTo(100));
                Assert.That(lab.Lantern.GetComponentsInChildren<Collider2D>(), Is.Empty, "A lantern must not enter damage or motor collision queries.");
                Assert.That(lab.Lantern.GetComponent<CombatActor>(), Is.Null);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator SolidWallsBlockPropAttacksAndCancelledWindupDoesNotCreateAContact()
        {
            var lab = Lab(); var arena = lab.Arena;
            using (var driver = arena.AcquireManualSimulation())
            {
                driver.Reset(); arena.enemies[0].Position = new Vector2(7, 0);
                driver.Step(.02f, new CombatInput(pressed: CombatButtons.Weapon1Basic));
                arena.Player.CancelAction(); Advance(driver, .4f);
                Assert.That(lab.Lantern.State.AcceptedAttacks, Is.Zero);
                driver.Reset();
                var wall = new GameObject("Prop attack occluder"); wall.transform.position = new Vector2(-.6f, 1.5f);
                wall.AddComponent<ArenaSurface>(); wall.AddComponent<BoxCollider2D>().size = new Vector2(.2f, 3);
                Physics2D.SyncTransforms(); driver.Step(.02f, new CombatInput(pressed: CombatButtons.Weapon1Basic)); Advance(driver, .5f);
                Assert.That(lab.Lantern.State.AcceptedAttacks, Is.Zero);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator BodySweepPauseAndResetUseTheSameCombatClockAndKeepOtherOwnersPaused()
        {
            var lab = Lab(); var arena = lab.Arena;
            using (var driver = arena.AcquireManualSimulation())
            {
                driver.Reset();
                for (int i = 0; i < 24; i++) driver.Step(.02f, new CombatInput(1));
                Assert.That(lab.Lantern.BodyContacts, Is.GreaterThan(0));
                Assert.That(arena.Player.Position.x, Is.GreaterThan(.4f), "The lantern cannot block player movement.");
                float angle = lab.Lantern.State.Angle, now = arena.Now;
                lab.SetPaused(true); yield return null; yield return null;
                Assert.That(lab.Lantern.State.Angle, Is.EqualTo(angle)); Assert.That(arena.Now, Is.EqualTo(now));
                using (var other = arena.Controls.OpenScope("Independent pause owner"))
                {
                    other.PauseSimulation(); lab.SetPaused(false);
                    Assert.That(arena.Controls.Current.SimulationPaused, Is.True);
                }
                driver.Reset(); Assert.That(lab.Lantern.State.Angle, Is.Zero); Assert.That(lab.Lantern.State.AcceptedAttacks, Is.Zero); Assert.That(lab.Lantern.BodyContacts, Is.Zero);
                Assert.That(arena.CompletedEnvironmentAttacks.Count, Is.Zero, "Reset clears old contact observations immediately.");
                driver.Step(.02f); Assert.That(arena.CompletedEnvironmentAttacks.Count, Is.Zero, "Old sweep samples are not replayed after reset.");
            }
        }
        [UnityTest] public IEnumerator ProfileAndNormalTogglesLeaveActorVitalsLoadoutAndMovementUntouched()
        {
            var lab = Lab(); var arena = lab.Arena;
            using (var driver = arena.AcquireManualSimulation())
            {
                driver.Reset(); float health = arena.Player.health; var position = arena.Player.Position;
                int weapon = arena.Player.selected; lab.SetNormals(false); lab.SetLights(false); lab.SetSunset(true);
                Assert.That(arena.Player.health, Is.EqualTo(health)); Assert.That(arena.Player.Position, Is.EqualTo(position)); Assert.That(arena.Player.selected, Is.EqualTo(weapon));
                Advance(driver, 1); Assert.That(lab.Director.Current.Intensity, Is.EqualTo(.65f).Within(.001));
                lab.SetSunset(false); Advance(driver, 1); Assert.That(lab.Director.Current.Intensity, Is.EqualTo(.28f).Within(.001));
                foreach (var binding in lab.SpriteBindings) { binding.Apply(); Assert.That(binding.MissingFrame, Is.False); }
                Assert.That(arena.Player.health, Is.EqualTo(health));
            }
            yield return null;
        }
        [Test] public void SweptContactFindsThinObjectsAndRejectsNearMisses()
        {
            var rect = new Rect(-.1f, -.1f, .2f, .2f);
            Assert.That(ReactiveLanternBridge.SweptPoint(new Vector2(-10, 0), new Vector2(10, 0), rect), Is.True);
            Assert.That(ReactiveLanternBridge.SweptPoint(new Vector2(-10, 1), new Vector2(10, 1), rect), Is.False);
        }
        [UnityTest] public IEnumerator TeleportDoesNotSweepThroughALanternAndDisabledObserversDoNotCollectContacts()
        {
            var lab = Lab(); var arena = lab.Arena;
            using (var driver = arena.AcquireManualSimulation())
            {
                driver.Reset(); arena.Player.Position = new Vector2(2, 0); Physics2D.SyncTransforms();
                driver.Step(.02f);
                Assert.That(lab.Lantern.BodyContacts, Is.Zero, "Only actual motor movement is a body sweep, not a teleport between ticks.");
                lab.Lantern.enabled = false;
                driver.Reset(); driver.Step(.02f, new CombatInput(pressed: CombatButtons.Weapon1Basic));
                Advance(driver, .16f);
                Assert.That(arena.CompletedEnvironmentAttacks.Count, Is.Zero);
                Assert.That(arena.CompletedEnvironmentBodies.Count, Is.Zero);
                lab.Lantern.enabled = true; driver.Reset();
                driver.Step(.02f, new CombatInput(pressed: CombatButtons.Weapon1Basic)); Advance(driver, .16f);
                Assert.That(lab.Lantern.State.AcceptedAttacks, Is.EqualTo(1));
            }
            yield return null;
        }
    }
}
