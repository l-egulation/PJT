using System;
using System.Collections;
using System.Linq;
using Deviant.Combat.Contracts;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class SimulationInputTests
    {
        [Test] public void InputContractIsEngineIndependentAndRejectsInvalidSamples()
        {
            Assert.That(typeof(CombatInput).Assembly.GetName().Name, Is.EqualTo("Deviant.Combat.Contracts"));
            Assert.That(typeof(CombatInput).Assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("UnityEngine")), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => new CombatInput(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CombatInput(0, (CombatButtons)1024));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CombatInput(0, released: (CombatButtons)1024));
        }

        [UnityTest] public IEnumerator KeyboardAdapterSubmitsGameplayIntentAndCannotResetManualOwner()
        {
            LegacySceneFixture.Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
            Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>();
            var adapter = arena.Player.GetComponent<CombatKeyboardInput>();
            var settings = InputSystem.settings;
            var background = settings.backgroundBehavior;
            var editorInput = settings.editorInputBehaviorInPlayMode;
            Keyboard keyboard = null;
            try
            {
                // Batch Editor runs have no focused Game View. Route this synthetic
                // device to game input explicitly and restore project settings below.
                settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                keyboard = InputSystem.AddDevice<Keyboard>();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A, Key.RightArrow)); InputSystem.Update(); keyboard.MakeCurrent();
                Assert.That(keyboard.aKey.wasPressedThisFrame, Is.True, "Synthetic key edge must reach the game update.");
                adapter.SendMessage("Update"); arena.SendMessage("FixedUpdate");
                Assert.That(arena.Player.currentCast, Is.Not.Null);
                Assert.That(arena.Player.currentCast.isBasic, Is.True);
                Assert.That(arena.Player.axis, Is.EqualTo(1));
                using (var owner = arena.AcquireManualSimulation())
                {
                    owner.Reset(); owner.Step(.02f); arena.Player.health = 42;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F5, Key.A)); InputSystem.Update();
                    adapter.SendMessage("Update");
                    Assert.That(arena.Now, Is.EqualTo(.02f));
                    Assert.That(arena.Player.health, Is.EqualTo(42), "Keyboard F5 must not reset the owned experiment.");
                    owner.Step(.02f);
                    Assert.That(arena.Player.currentCast, Is.Null);
                }
            }
            finally
            {
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                settings.backgroundBehavior = background; settings.editorInputBehaviorInPlayMode = editorInput;
            }
        }

        [UnityTest] public IEnumerator FailedResetStopsAutomaticTicksUntilResetSucceeds()
        {
            LegacySceneFixture.Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
            Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>();
            Action failure = () => throw new InvalidOperationException("reset observer failed");
            arena.ResetCompleted += failure;
            try { Assert.Throws<InvalidOperationException>(() => arena.ResetTest()); }
            finally { arena.ResetCompleted -= failure; }
            Assert.That(arena.SimulationFaulted, Is.True);
            float clock = arena.Now; long tick = arena.SimulationTickCount;
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(arena.Now, Is.EqualTo(clock)); Assert.That(arena.SimulationTickCount, Is.EqualTo(tick));
            using (var owner = arena.AcquireManualSimulation())
                Assert.Throws<InvalidOperationException>(() => owner.Step(.02f));
            arena.ResetTest();
            Assert.That(arena.SimulationFaulted, Is.False);
            Assert.That(arena.Now, Is.EqualTo(clock));
            float deadline = Time.realtimeSinceStartup + 2;
            while (arena.Now <= clock && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(arena.Now, Is.GreaterThan(clock));
        }

        [UnityTest] public IEnumerator LabUsesSharedManualTicks() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        [UnityTest] public IEnumerator GameUsesSharedManualTicks() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene);
            Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>();
            var player = arena.Player;
            Assert.That(player.GetComponent<CombatKeyboardInput>().Player, Is.SameAs(player));
            float timeScale = Time.timeScale;
            var driver = arena.AcquireManualSimulation();
            try
            {
                Assert.Throws<InvalidOperationException>(() => arena.AcquireManualSimulation());
                driver.Reset();
                yield return new WaitForFixedUpdate(); yield return null;
                Assert.That(arena.Now, Is.Zero, "Automatic ticking must not race the manual owner.");
                Assert.That(arena.SimulationTickCount, Is.Zero);
                Assert.That(Time.timeScale, Is.EqualTo(timeScale));
                foreach (float invalid in new[] { 0, -1, float.NaN, float.PositiveInfinity })
                    Assert.Throws<ArgumentOutOfRangeException>(() => driver.Step(invalid, new CombatInput(0, CombatButtons.Basic)));
                Assert.That(arena.Now, Is.Zero); Assert.That(player.currentCast, Is.Null);
                player.InputBlocked = true;
                driver.Step(.02f, new CombatInput(1, CombatButtons.Basic));
                Assert.That(player.currentCast, Is.Null, "Manual input must respect the existing input block.");
                player.InputBlocked = false;

                var a = Run(arena, driver);
                Assert.That(arena.SimulationTickCount, Is.EqualTo(60));
                float epochTime = arena.Now;
                arena.ResetTest();
                Assert.That(arena.Now, Is.EqualTo(epochTime), "Ordinary resets preserve the simulation clock.");
                Assert.That(arena.SimulationTickCount, Is.EqualTo(60));
                var repeat = Run(arena, driver);
                Assert.That(repeat, Is.EqualTo(a), "Same definitions, placement, epoch and input must reproduce every observation.");
                Assert.That(a[0], Is.EqualTo(10));
                Assert.That(a[1], Is.GreaterThan(0));
                Assert.That(a[2], Is.GreaterThan(2.9f));
                var movement = arena.Movement.ToData(); movement.dashSpeed = 8;
                Assert.That(arena.TryApplyMovementAndReset(movement, out var error), Is.True, error);
                var basic = arena.BasicSwordFirst.ToData(); basic.impact = .3f;
                Assert.That(arena.TryApplyBasicSwordFirst(basic, false, out error), Is.True, error);
                var b = Run(arena, driver);
                Assert.That(b[0], Is.EqualTo(a[0])); Assert.That(b[1], Is.GreaterThan(a[1])); Assert.That(b[2], Is.LessThan(a[2]));

                driver.Reset();
                player.Position = new Vector2(11.8f, 0); player.SyncFacing(1);
                // Scene-independent wall fixture: actual shared sweeps, no alternate movement rules.
                var wall = new GameObject("ManualTickWall"); wall.transform.position = new Vector3(13, 1.5f, 0);
                wall.AddComponent<ArenaSurface>(); wall.AddComponent<BoxCollider2D>().size = new Vector2(.4f, 3);
                Physics2D.SyncTransforms();
                for (int i = 0; i < 20; i++) driver.Step(.02f, new CombatInput(0, i == 0 ? CombatButtons.Dash : CombatButtons.None));
                Assert.That(player.Position.x, Is.LessThan(12.16f));
                Assert.That(player.Dashing, Is.False);
                wall.SetActive(false); Object.Destroy(wall);
                driver.Reset();
                player.SubmitInput(new CombatInput(1, CombatButtons.Basic));
            }
            finally { driver.Dispose(); }
            var next = arena.AcquireManualSimulation();
            driver.Dispose();
            Assert.That(arena.ManualSimulationActive, Is.True, "Stale release cannot release the new owner.");
            Assert.Throws<ObjectDisposedException>(() => driver.Step(.02f));
            next.Step(.02f);
            Assert.That(player.currentCast, Is.Null, "Release/acquire clears pending input.");
            float at = arena.Now;
            next.Dispose();
            float limit = Time.realtimeSinceStartup + 2;
            while (arena.Now <= at && Time.realtimeSinceStartup < limit) yield return null;
            Assert.That(arena.Now, Is.GreaterThan(at));
        }

        static float[] Run(CombatArena arena, CombatArena.ManualSimulation driver)
        {
            driver.Reset();
            arena.Player.Position = Vector2.zero; arena.Player.SyncFacing(1);
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 0); }
            var target = arena.enemies[0]; target.Position = new Vector2(3, 0);
            Physics2D.SyncTransforms();
            float health = target.health, firstHit = -1;
            for (int tick = 0; tick < 60; tick++)
            {
                var buttons = tick == 0 ? CombatButtons.Basic : tick == 30 ? CombatButtons.Dash : CombatButtons.None;
                driver.Step(.02f, new CombatInput(0, buttons));
                if (firstHit < 0 && target.ReceivedHitCount > 0) firstHit = tick * .02f;
            }
            return new[] { health - target.health, firstHit, arena.Player.Position.x, arena.Now };
        }
    }
}
