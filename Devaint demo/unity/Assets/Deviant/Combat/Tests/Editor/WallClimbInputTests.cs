using System;
using System.Collections;
using Deviant.Combat.Contracts;
using Deviant.Integration;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Deviant.Combat.Tests
{
    public sealed class WallClimbInputTests
    {
        CombatArena arena;
        CombatArena.ManualSimulation driver;
        CombatPlayer Player => arena.Player;
        const float StepSeconds = 1f / 120;

        [UnitySetUp] public IEnumerator Setup()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            arena = new GameObject("Up wall movement arena").AddComponent<CombatArena>();
            arena.loadCharacterArt = false;
            var content = CombatContentLoader.Load(null, false);
            content.Profile = CombatCompositionProfile.Game;
            content.Camera = new GameObject("Wall movement camera").AddComponent<Camera>();
            content.Loadout = Deviant.Equipment.WeaponResources.CreateLoadout();
            content.PlayerSpawn = new Vector2(2.34f, 200);
            content.PlayerSize = new Vector2(.9375f, 2);
            content.PlayerOffset = new Vector2(.1875f, 1);
            content.Enemies = Array.Empty<CombatEnemySpawn>();
            content.BindPlayerInput = player => player.gameObject.AddComponent<CombatKeyboardInput>().Player = player;
            Assert.That(arena.Initialize(content), Is.True);
            foreach (int side in new[] { 1, -1 })
            {
                var wall = new GameObject("Wall " + side);
                wall.transform.position = new Vector3(side * 4, 200, 0);
                wall.AddComponent<ArenaSurface>();
                wall.AddComponent<BoxCollider2D>().size = new Vector2(2, 80);
            }
            driver = arena.AcquireManualSimulation();
            Player.UseTestInput = true;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            driver?.Dispose();
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
        void Pose(int side = 1, float y = 200, float vertical = 0, float gap = 0, float speed = 0)
        {
            Player.Position = new Vector2(side * (2.34f - gap), y);
            Player.velocity = new Vector2(side * speed, vertical);
            Player.SyncFacing(side); Player.grounded = false;
            Physics2D.SyncTransforms();
        }
        void Place(int side = 1, float y = 200, float vertical = 0, float gap = 0, float speed = 0)
        { driver.Reset(); Pose(side, y, vertical, gap, speed); }
        void Step(int axis = 0, int climb = 0, CombatButtons buttons = CombatButtons.None)
            => driver.Step(StepSeconds, new CombatInput(axis, buttons, climbAxis: climb));

        [UnityTest] public IEnumerator UpClimbsAndReleaseDropsOffWithoutSlidingBothWays()
        {
            foreach (int side in new[] { 1, -1 })
            {
                Place(side); float start = Player.Position.y;
                for (int i = 0; i < 12; i++) Step(climb: 1);
                Assert.That(Player.WallClimbing, Is.True); Assert.That(Player.WallSliding, Is.False);
                Assert.That(Player.Position.y - start, Is.EqualTo(arena.Movement.RunSpeed * .1f).Within(.02f));
                Step();
                Assert.That(Player.WallClimbing, Is.False); Assert.That(Player.WallSliding, Is.False);
                Assert.That(Player.VelocityY, Is.LessThan(arena.Movement.RunSpeed), "Releasing up drops into ordinary gravity.");
                for (int i = 0; i < 36; i++) Step();
                Assert.That(Player.VelocityY, Is.LessThan(0));
                Assert.That(Player.WallClimbing, Is.False); Assert.That(Player.WallSliding, Is.False);
                Step(climb: -1);
                Assert.That(Player.WallClimbing, Is.False, "Down never grabs the wall.");
            }
            yield return null;
        }
        [UnityTest] public IEnumerator KickRequiresUpAndSpaceAloneRemainsAnOrdinaryAirJump()
        {
            foreach (int side in new[] { 1, -1 })
            {
                foreach (int climb in new[] { 0, -1, 1 })
                {
                    Place(side); Step(climb: 1); Step(climb: climb);
                    Step(climb: climb, buttons: CombatButtons.Jump);
                    Assert.That(Player.WallJumpInFlight, Is.EqualTo(climb > 0));
                    Assert.That(Player.jumps.Used, Is.EqualTo(climb > 0 ? 0 : 1));
                    Assert.That(Player.VelocityY, Is.GreaterThan(19));
                    if (climb <= 0) Assert.That(Player.WallClimbing, Is.False, "An ordinary air jump escapes the slide.");
                }
                Place(side); Step(climb: -1, buttons: CombatButtons.Drop);
                Assert.That(Player.WallJumpInFlight, Is.False, "Down+Space cannot bypass the up requirement.");
            }
            yield return null;
        }
        [UnityTest] public IEnumerator FallingContactWithoutUpDoesNotGrabAndRisingPastAWallKeepsItsMomentum()
        {
            foreach (int side in new[] { 1, -1 })
            {
                Place(side, vertical: -1); Step(side);
                Assert.That(Player.WallClimbing, Is.False); Assert.That(Player.WallSliding, Is.False);
                Assert.That(Player.VelocityY, Is.LessThan(-1), "Without up the wall does not slow a fall.");
                Place(side, vertical: 10); Step(side);
                Assert.That(Player.WallClimbing, Is.False);
                Assert.That(Player.VelocityY, Is.GreaterThan(9), "Ordinary upward jumps are not caught by the wall.");
            }
            yield return null;
        }
        [UnityTest] public IEnumerator BufferedKickNeedsUpAtContactAndReleaseCannotBecomeALateKick()
        {
            foreach (int side in new[] { 1, -1 })
            {
                Place(side, vertical: -1, gap: .35f, speed: arena.Movement.RunSpeed);
                Step(side, climb: 1, buttons: CombatButtons.Jump);
                Assert.That(Player.jumps.Used, Is.Zero);
                for (int i = 0; i < 12; i++) Step(side);
                Assert.That(Player.WallJumpInFlight, Is.False);
                Assert.That(Player.WallClimbing, Is.False);
                Step(side, climb: 1);
                Assert.That(Player.WallJumpInFlight, Is.False, "Repressing up cannot revive the cancelled Space.");
                Assert.That(Player.jumps.Used, Is.Zero);
                Place(side, vertical: -1, gap: .35f, speed: arena.Movement.RunSpeed);
                Step(side, climb: 1, buttons: CombatButtons.Jump);
                for (int i = 0; i < 12; i++) Step(side, climb: 1);
                Assert.That(Player.WallJumpInFlight, Is.True, "Keeping up permits the reserved kick at contact.");
                Assert.That(Player.jumps.Used, Is.Zero);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator CoyoteCannotBypassTheLiveUpRequirement()
        {
            foreach (int side in new[] { 1, -1 }) foreach (bool up in new[] { false, true })
            {
                Place(side); Step(climb: 1);
                for (int i = 0; i < 7; i++) Step(-side);
                Assert.That(Player.WallClimbing, Is.False);
                Step(-side, climb: up ? 1 : 0, buttons: CombatButtons.Jump);
                Assert.That(Player.WallJumpInFlight, Is.EqualTo(up));
                Assert.That(Player.jumps.Used, Is.EqualTo(up ? 0 : 1));
            }
            yield return null;
        }
        [UnityTest] public IEnumerator KickArrivalClimbsWithUpAndDoesNotGrabWithout()
        {
            foreach (int side in new[] { 1, -1 }) foreach (bool up in new[] { false, true })
            {
                Place(side); Step(climb: 1, buttons: CombatButtons.Jump);
                bool grabbed = false;
                for (int i = 0; i < 90 && !grabbed; i++) { Step(-side, climb: up ? 1 : 0); grabbed = Player.WallClimbing; }
                Assert.That(Player.WallSliding, Is.False);
                Assert.That(grabbed, Is.EqualTo(up), "Arrival grabs the far wall only while up is held.");
                if (up) { Assert.That(Player.Position.x * side, Is.LessThan(-2)); Assert.That(Player.VelocityY, Is.GreaterThan(0)); }
            }
            yield return null;
        }
        [UnityTest] public IEnumerator LedgeAssistNeedsUpAndOneWayOrBlockedMovementNeverStartsClimbing()
        {
            foreach (int side in new[] { 1, -1 })
            {
                Place(side, y: 239.5f); Step(side);
                Assert.That(Player.Position.y, Is.LessThan(239.5f));
                Assert.That(Player.WallClimbing, Is.False);
                Place(side, y: 239.5f);
                for (int i = 0; i < 120 && !Player.grounded; i++) Step(climb: 1);
                Assert.That(Player.grounded, Is.True); Assert.That(Player.Position.y, Is.EqualTo(240).Within(.1f));
                Place(side); Player.InputBlocked = true; Step(climb: 1);
                Assert.That(Player.WallClimbing, Is.False); Player.InputBlocked = false;
                Place(side); var wall = GameObject.Find("Wall " + side).GetComponent<ArenaSurface>(); wall.oneWay = true;
                Step(climb: 1); Assert.That(Player.WallClimbing, Is.False); wall.oneWay = false;
            }
            yield return null;
        }
        // 2026-11-14 사용자: 올라가기는 그린 대로 재생한다. 벽에 붙은 채 ledgeClimbSeconds 동안 멈추고(입력 무시) 끝나면 윗면에 선다.
        [UnityTest] public IEnumerator LedgeClimbHoldsAtTheWallThenStandsOnTheTopAsDrawn()
        {
            var movement = arena.Movement;
            Assert.That(movement.WallLedgeClimbSeconds, Is.GreaterThan(0));
            foreach (int side in new[] { 1, -1 })
            {
                Place(side, y: 240 - movement.WallLedgeHopHeight + .1f);
                Step(climb: 1);
                Assert.That(Player.WallLedgeClimbing, Is.True, "the hands reach the edge within ledgeHopHeight");
                Assert.That(Player.WallLedgeCorner.y, Is.EqualTo(240).Within(.01f));
                Assert.That(Player.WallLedgeCorner.x, Is.EqualTo(side * 3).Within(.01f));
                Assert.That(Player.WallLedgeSide, Is.EqualTo(side));
                var held = Player.Position;
                int steps = Mathf.FloorToInt(movement.WallLedgeClimbSeconds / StepSeconds) - 2;
                for (int i = 0; i < steps; i++) Step(-side, 1, i % 2 == 0 ? CombatButtons.Jump : CombatButtons.None);
                Assert.That(Player.WallLedgeClimbing, Is.True);
                Assert.That(Vector2.Distance(Player.Position, held), Is.LessThan(.001f), "held still while the climb plays; move and jump are ignored");
                for (int i = 0; i < 30; i++) Step();
                Assert.That(Player.WallLedgeClimbing, Is.False);
                Assert.That(Player.grounded, Is.True);
                Assert.That(Player.Position.y, Is.EqualTo(240).Within(.05f));
                Assert.That(Player.Position.x, Is.EqualTo(side * (3 + movement.WallLedgeClimbForward)).Within(.05f), "stands where the last drawn frame stands");

                Place(side, y: 240 - movement.WallLedgeHopHeight + .1f); Step(climb: 1);
                Assert.That(Player.WallLedgeClimbing, Is.True);
                float before = Player.Position.y;
                Player.InputBlocked = true; for (int i = 0; i < 10; i++) Step(climb: 1); Player.InputBlocked = false;
                Assert.That(Player.WallLedgeClimbing, Is.False, "an interruption drops the hero");
                Assert.That(Player.Position.y, Is.LessThan(before));
            }
            yield return null;
        }
        [UnityTest] public IEnumerator LedgePoseRemainsOwnedUntilTheMotorActuallyStandsOnTheTop()
        {
            var movement = arena.Movement;
            foreach (int side in new[] { 1, -1 })
            {
                Place(side, y: 240 - movement.WallLedgeHopHeight + .1f);
                Step(climb: 1);
                Assert.That(Player.WallLedgeClimbing, Is.True);
                var held = Player.Position;
                float end = Player.WallLedgeStartedAt + movement.WallLedgeClimbSeconds;
                // Simulation publishes its advanced clock after the motor. A render can therefore
                // observe the deadline while the motor still holds the body below the corner.
                for (int i = 0; i < 240 && arena.Now < end; i++) Step();
                Assert.That(arena.Now, Is.GreaterThanOrEqualTo(end));
                Assert.That(Vector2.Distance(Player.Position, held), Is.LessThan(.001f));
                Assert.That(Player.WallLedgeClimbing, Is.True,
                    "Keep the anchored ledge drawing until FinishLedgeClimb moves the physical body; never show its ordinary pose below the ledge.");
                Step();
                Assert.That(Player.WallLedgeClimbing, Is.False);
                Assert.That(Player.Position.y, Is.EqualTo(240).Within(.05f));
                Assert.That(Player.grounded, Is.True);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator LedgeStandSnapsRenderHistoryInsteadOfBlendingFromBelow()
        {
            Place(y: 240 - arena.Movement.WallLedgeHopHeight + .1f);
            Step(climb: 1);
            Assert.That(Player.WallLedgeClimbing, Is.True);
            var held = Player.Position;
            var helper = Player.GetComponent<ActorRenderInterpolation>() ?? Player.gameObject.AddComponent<ActorRenderInterpolation>();
            helper.Bind(Player);
            helper.SendMessage("Update");
            for (int i = 0; i < 240 && !Player.grounded; i++) Step();
            Assert.That(Player.grounded, Is.True);
            // The fixed samples straddle a short authored snap (< the ordinary teleport threshold).
            var history = new ActorRenderPoseHistory();
            history.Push(held, Time.fixedTimeAsDouble - .02);
            history.Push(Player.Position, Time.fixedTimeAsDouble);
            typeof(ActorRenderInterpolation).GetField("history", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(helper, history);
            driver.Dispose(); driver = null;
            helper.SendMessage("Update");
            Assert.That(Vector2.Distance(helper.RenderPosition(), Player.Position), Is.LessThan(.001f),
                "The first ordinary pose must be on top, not interpolated back toward the held body below.");
            Assert.That(Player.visual.transform.parent.localPosition.sqrMagnitude, Is.LessThan(.000001f));
            yield return null;
        }
        [UnityTest] public IEnumerator KeyboardUsesUpForClimbingAndKickingWhileCtrlHasNoGrabAction()
        {
            Place(); driver.Dispose(); driver = null; Player.UseTestInput = false; arena.enabled = false;
            var adapter = Player.GetComponent<CombatKeyboardInput>();
            var settings = InputSystem.settings;
            var background = settings.backgroundBehavior; var editorInput = settings.editorInputBehaviorInPlayMode;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
            try
            {
                void Poll(params Key[] keys)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); InputSystem.Update();
                    adapter.SendMessage("Update"); arena.SendMessage("FixedUpdate");
                }
                Poll(Key.LeftCtrl); Assert.That(Player.WallClimbing, Is.False); Assert.That(Player.WallSliding, Is.False);
                Poll(Key.UpArrow); Assert.That(Player.WallClimbing, Is.True);
                Assert.That(Player.VelocityY, Is.GreaterThan(0));
                Poll(); Assert.That(Player.WallClimbing, Is.False); Assert.That(Player.WallSliding, Is.False);
                Poll(Key.Space); Assert.That(Player.WallJumpInFlight, Is.False); Assert.That(Player.jumps.Used, Is.EqualTo(1));
                Poll(); arena.ResetTest(); Pose();
                Poll(Key.UpArrow, Key.Space);
                Assert.That(Player.WallJumpInFlight, Is.True); Assert.That(Player.jumps.Used, Is.Zero);
            }
            finally
            {
                Player.UseTestInput = true; InputSystem.RemoveDevice(keyboard);
                settings.backgroundBehavior = background; settings.editorInputBehaviorInPlayMode = editorInput;
            }
            yield return null;
        }
    }
}
