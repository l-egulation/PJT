using System;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using Deviant.Definitions;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class PlayerDashTests
    {
        static LocomotionProfile Movement(float duration = .2f, float recovery = 1, float invulnerability = .15f,
            float speed = 15, float carryDuration = .1f, float carrySpeed = 5)
        {
            Assert.That(LocomotionProfile.TryCreate(new LocomotionData {
                schemaVersion = 2, id = "dash-test", runSpeed = 5, dashSpeed = speed, dashDuration = duration,
                dashRecovery = recovery, dashInvulnerability = invulnerability,
                dashJumpCarrySpeed = carrySpeed, dashJumpCarryDuration = carryDuration,
                firstJumpVelocity = 20, secondJumpVelocity = 20, riseGravity = 66.6666667f, fallGravity = 104.1666667f
            }, out var profile, out var error), Is.True, error);
            return profile;
        }
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        static float Before(float positive) => BitConverter.Int32BitsToSingle(Bits(positive) - 1);
        static void Motion(PlayerDashExecution dash, float now, LocomotionProfile profile, float initialX, bool initialFreeze, float expectedX, bool expectedFreeze)
        {
            float x = initialX; bool freeze = initialFreeze;
            dash.ApplyMotion(now, profile, ref x, ref freeze);
            Assert.That(Bits(x), Is.EqualTo(Bits(expectedX))); Assert.That(freeze, Is.EqualTo(expectedFreeze));
        }

        [Test] public void NewOwnerHasLegacyInactiveDeadlinesAndOneReadyDash()
        {
            var dash = new PlayerDashExecution(); var read = dash.Read();
            Assert.That(read.StartedAt, Is.EqualTo(-10)); Assert.That(read.EndsAt, Is.EqualTo(-10));
            Assert.That(read.ReadyAt, Is.Zero); Assert.That(read.Facing, Is.Zero);
            Assert.That(read.JumpCarryUntil, Is.Zero); Assert.That(read.JumpCarryFacing, Is.Zero);
            Assert.That(dash.IsDashing(0), Is.False); Assert.That(dash.IsInvulnerable(0, Movement()), Is.False);
            Assert.That(dash.CanStart(0), Is.True); Assert.That(dash.Remaining(0), Is.Zero);
            Motion(dash, 0, Movement(), -3, false, -3, false);
        }

        [TestCase(-1)] [TestCase(1)]
        public void StartUsesTheCorrectedFacingAndIndependentAbsoluteDeadlines(int facing)
        {
            var dash = new PlayerDashExecution(); var profile = Movement();
            dash.Begin(2.125f, facing, profile); var read = dash.Read();
            Assert.That(Bits(read.StartedAt), Is.EqualTo(Bits(2.125f)));
            Assert.That(Bits(read.EndsAt), Is.EqualTo(Bits(2.325f)));
            Assert.That(Bits(read.ReadyAt), Is.EqualTo(Bits(3.125f)));
            Assert.That(read.Facing, Is.EqualTo(facing)); Assert.That(read.JumpCarryUntil, Is.Zero);
            Assert.That(dash.IsDashing(2.125f), Is.True); Assert.That(dash.IsInvulnerable(2.125f, Movement()), Is.True);
            Assert.That(dash.CanStart(2.125f), Is.False); Assert.That(dash.Remaining(2.125f), Is.EqualTo(1));
            Motion(dash, 2.125f, profile, -facing * 5, false, facing * 15, true);
        }

        [Test] public void InvulnerabilityMovementAndRecoveryAreHalfOpenAtTheirOwnDeadlines()
        {
            var dash = new PlayerDashExecution(); var profile = Movement(); dash.Begin(0, 1, profile);
            Assert.That(dash.IsInvulnerable(Before(.15f), Movement()), Is.True);
            Assert.That(dash.IsInvulnerable(.15f, Movement()), Is.False); Assert.That(dash.IsDashing(.15f), Is.True);
            Assert.That(dash.IsDashing(Before(.2f)), Is.True); Assert.That(dash.IsDashing(.2f), Is.False);
            Assert.That(dash.IsInvulnerable(.2f, Movement(invulnerability: .2f)), Is.False);
            Motion(dash, Before(.2f), profile, -5, false, 15, true);
            Motion(dash, .2f, profile, -5, false, -5, false);
            Assert.That(dash.CanStart(Before(1)), Is.False); Assert.That(dash.Remaining(Before(1)), Is.GreaterThan(0));
            Assert.That(dash.CanStart(1), Is.True); Assert.That(dash.Remaining(1), Is.Zero); Assert.That(dash.Remaining(9), Is.Zero);
            dash.Begin(1, -1, profile); Assert.That(dash.Read().ReadyAt, Is.EqualTo(2));
        }

        [Test] public void AReadyResourceCannotRestartAnActiveDashEvenWithShorterRecovery()
        {
            var dash = new PlayerDashExecution(); var profile = Movement(duration: .5f, recovery: .1f, invulnerability: .15f);
            dash.Begin(4, 1, profile);
            Assert.That(dash.Remaining(4.125f), Is.Zero); Assert.That(dash.CanStart(4.125f), Is.False);
            Assert.That(dash.CanStart(Before(4.5f)), Is.False); Assert.That(dash.CanStart(4.5f), Is.True);
            dash.Cancel(4.25f); Assert.That(dash.CanStart(4.25f), Is.True);
        }

        [Test] public void WallContactEndsOnlyAnActiveDashAndNeverRefundsRecovery()
        {
            var dash = new PlayerDashExecution(); var profile = Movement(); dash.Begin(0, -1, profile);
            Assert.That(dash.EndAtWall(.04f, false), Is.False); Assert.That(dash.IsDashing(.04f), Is.True);
            Assert.That(dash.EndAtWall(.04f, true), Is.True);
            var read = dash.Read(); Assert.That(read.EndsAt, Is.EqualTo(.04f)); Assert.That(read.StartedAt, Is.Zero);
            Assert.That(read.ReadyAt, Is.EqualTo(1)); Assert.That(read.Facing, Is.EqualTo(-1));
            Assert.That(dash.IsDashing(.04f), Is.False); Assert.That(dash.IsInvulnerable(.04f, Movement()), Is.False);
            Assert.That(dash.Remaining(.04f), Is.EqualTo(.96f)); Assert.That(dash.CanStart(.04f), Is.False);
            Assert.That(dash.EndAtWall(.06f, true), Is.False); Assert.That(dash.Read().EndsAt, Is.EqualTo(.04f));
            Motion(dash, .06f, profile, 5, false, 5, false);
        }

        [Test] public void ContactAtTheTimeDeadlineDoesNotMoveTheDeadlineOrInventCarry()
        {
            var dash = new PlayerDashExecution(); dash.Begin(0, 1, Movement());
            Assert.That(dash.EndAtWall(.2f, true), Is.False); Assert.That(dash.Read().EndsAt, Is.EqualTo(.2f));
            Assert.That(dash.Read().JumpCarryUntil, Is.Zero); Assert.That(dash.Read().JumpCarryFacing, Is.Zero);
        }

        [TestCase(-1)] [TestCase(1)]
        public void ValidJumpCarryUsesTheCancelledDashFacingForPointOneSeconds(int facing)
        {
            var dash = new PlayerDashExecution(); var profile = Movement(); dash.Begin(0, facing, profile);
            dash.Cancel(.04f); dash.BeginJumpCarry(.04f, profile); var read = dash.Read();
            Assert.That(read.EndsAt, Is.EqualTo(.04f)); Assert.That(read.ReadyAt, Is.EqualTo(1));
            Assert.That(read.JumpCarryFacing, Is.EqualTo(facing)); Assert.That(Bits(read.JumpCarryUntil), Is.EqualTo(Bits(.14f)));
            Assert.That(dash.IsDashing(.04f), Is.False); Assert.That(dash.IsInvulnerable(.04f, Movement()), Is.False);
            Motion(dash, .04f, profile, -facing * 5, false, facing * 5, false);
            Motion(dash, Before(.14f), profile, -facing * 5, false, facing * 5, false);
            Motion(dash, .14f, profile, -facing * 5, false, -facing * 5, false);
            Motion(dash, .06f, profile, 0, true, facing * 5, true);
            Assert.That(dash.CanStart(.14f), Is.False);
        }

        [Test] public void AdmissionAndMotionQueriesDoNotSpendOrExtendDashOrCarry()
        {
            var dash = new PlayerDashExecution(); var profile = Movement(); dash.Begin(0, 1, profile);
            var before = dash.Read();
            for (int i = 0; i < 3; i++)
            {
                Assert.That(dash.CanStart(.04f), Is.False); Assert.That(dash.IsDashing(.04f), Is.True);
                Assert.That(dash.IsInvulnerable(.04f, Movement()), Is.True); Assert.That(dash.Remaining(.04f), Is.EqualTo(.96f));
                Motion(dash, .04f, profile, -5, false, 15, true);
            }
            Assert.That(dash.Read().StartedAt, Is.EqualTo(before.StartedAt)); Assert.That(dash.Read().EndsAt, Is.EqualTo(before.EndsAt));
            Assert.That(dash.Read().ReadyAt, Is.EqualTo(before.ReadyAt)); Assert.That(dash.Read().JumpCarryUntil, Is.Zero);
            Assert.That(dash.IsDashing(.2f), Is.False, "An invalid jump issues no cancellation; only the existing dash deadline ends it.");
        }

        [Test] public void OrdinaryCancellationRetainsCarryAndBothFacingsButResetRestoresResources()
        {
            var dash = new PlayerDashExecution(); var profile = Movement(); dash.Begin(0, -1, profile);
            dash.Cancel(.04f); dash.BeginJumpCarry(.04f, profile); dash.Cancel(.06f);
            var cancelled = dash.Read(); Assert.That(cancelled.StartedAt, Is.Zero); Assert.That(cancelled.EndsAt, Is.EqualTo(.06f));
            Assert.That(cancelled.ReadyAt, Is.EqualTo(1)); Assert.That(cancelled.JumpCarryUntil, Is.EqualTo(.14f));
            Motion(dash, .08f, profile, 5, false, -5, false);
            dash.Reset(); var reset = dash.Read();
            Assert.That(reset.StartedAt, Is.EqualTo(-10)); Assert.That(reset.EndsAt, Is.EqualTo(-10)); Assert.That(reset.ReadyAt, Is.Zero);
            Assert.That(reset.JumpCarryUntil, Is.Zero); Assert.That(reset.Facing, Is.EqualTo(-1)); Assert.That(reset.JumpCarryFacing, Is.EqualTo(-1));
            Assert.That(dash.CanStart(.08f), Is.True); Assert.That(dash.Remaining(.08f), Is.Zero);
            Motion(dash, .08f, profile, 5, false, 5, false);
            dash.Cancel(.08f); Assert.That(dash.Read().EndsAt, Is.EqualTo(.08f)); Assert.That(dash.IsDashing(.08f), Is.False);
            Assert.That(dash.CanStart(.08f), Is.True); Assert.That(cancelled.ReadyAt, Is.EqualTo(1));
        }

        [Test] public void SuccessfulReturnCanClearCarryWithoutRefundingOrResettingTheDash()
        {
            var dash = new PlayerDashExecution(); var profile = Movement();
            dash.Begin(0, -1, profile); dash.Cancel(.04f); dash.BeginJumpCarry(.04f, profile);
            Motion(dash, .06f, profile, 5, false, -5, false);
            dash.Cancel(.06f); dash.ClearCarry(); var read = dash.Read();
            Motion(dash, .06f, profile, 5, false, 5, false);
            Assert.That(read.JumpCarryUntil, Is.Zero); Assert.That(read.JumpCarryFacing, Is.EqualTo(-1));
            Assert.That(read.StartedAt, Is.Zero); Assert.That(read.EndsAt, Is.EqualTo(.06f));
            Assert.That(read.ReadyAt, Is.EqualTo(1)); Assert.That(read.Facing, Is.EqualTo(-1));
            Assert.That(dash.CanStart(.06f), Is.False); Assert.That(dash.Remaining(.06f), Is.EqualTo(.94f));
        }

        [Test] public void ANewDashClearsCarryWithoutResettingItsHistoricalFacing()
        {
            var dash = new PlayerDashExecution(); var profile = Movement(recovery: 0);
            dash.Begin(0, -1, profile); dash.Cancel(.02f); dash.BeginJumpCarry(.02f, profile);
            Assert.That(dash.CanStart(.04f), Is.True); dash.Begin(.04f, 1, profile);
            var read = dash.Read(); Assert.That(read.JumpCarryUntil, Is.Zero); Assert.That(read.JumpCarryFacing, Is.EqualTo(-1));
            Assert.That(read.Facing, Is.EqualTo(1)); Motion(dash, .05f, profile, -5, false, 15, true);
        }

        [Test] public void ProfilesAreReadLiveForMotionDefenseAndCarryButExistingDeadlinesStayAbsolute()
        {
            var dash = new PlayerDashExecution(); var first = Movement();
            var changed = Movement(duration: .6f, recovery: 4, invulnerability: .05f, speed: 9, carryDuration: .03f, carrySpeed: 2.5f);
            dash.Begin(0, -1, first); var started = dash.Read();
            Motion(dash, .08f, changed, 5, false, -9, true);
            Assert.That(dash.IsInvulnerable(.08f, changed), Is.False);
            Assert.That(dash.IsInvulnerable(.08f, first), Is.True);
            Assert.That(dash.Read().EndsAt, Is.EqualTo(.2f)); Assert.That(dash.Read().ReadyAt, Is.EqualTo(1));
            dash.Cancel(.08f); dash.BeginJumpCarry(.08f, changed);
            Assert.That(Bits(dash.Read().JumpCarryUntil), Is.EqualTo(Bits(.11f)));
            Motion(dash, .1f, changed, 5, false, -2.5f, false);
            Motion(dash, .1f, first, 5, false, -5, false);
            Motion(dash, .11f, changed, 5, false, 5, false);
            Assert.That(started.EndsAt, Is.EqualTo(.2f)); Assert.That(started.JumpCarryUntil, Is.Zero);
            Assert.That(dash.CanStart(1), Is.True); dash.Begin(1, 1, changed);
            Assert.That(dash.Read().EndsAt, Is.EqualTo(1.6f)); Assert.That(dash.Read().ReadyAt, Is.EqualTo(5));
        }

        [Test] public void ZeroDefenseAndCarryDurationsDoNotCreateAnActiveWindow()
        {
            var dash = new PlayerDashExecution(); var profile = Movement(invulnerability: 0, carryDuration: 0, carrySpeed: 0);
            dash.Begin(0, 1, profile); Assert.That(dash.IsDashing(0), Is.True); Assert.That(dash.IsInvulnerable(0, profile), Is.False);
            dash.Cancel(.04f); dash.BeginJumpCarry(.04f, profile);
            Motion(dash, .04f, profile, -5, false, -5, false);
            Assert.That(dash.Remaining(.04f), Is.EqualTo(.96f));
        }

        [Test] public void EveryActorOwnsItsDashAndSnapshotsRemainDetachedAcrossLaterCommands()
        {
            var first = new PlayerDashExecution(); var second = new PlayerDashExecution(); var profile = Movement();
            first.Begin(2, -1, profile); PlayerDashSnapshot started = first.Read();
            Assert.That(second.IsDashing(2), Is.False); Assert.That(second.CanStart(2), Is.True); Assert.That(second.Remaining(2), Is.Zero);
            first.Cancel(2.04f); first.BeginJumpCarry(2.04f, profile); PlayerDashSnapshot carry = first.Read();
            first.Reset(); first.Cancel(2.06f);
            Assert.That(started.StartedAt, Is.EqualTo(2)); Assert.That(started.EndsAt, Is.EqualTo(2.2f)); Assert.That(started.ReadyAt, Is.EqualTo(3));
            Assert.That(started.JumpCarryUntil, Is.Zero); Assert.That(carry.JumpCarryFacing, Is.EqualTo(-1)); Assert.That(carry.JumpCarryUntil, Is.GreaterThan(2.1f));
            Assert.That(first.Read().ReadyAt, Is.Zero); Assert.That(second.Read().Facing, Is.Zero); Assert.That(second.Read().EndsAt, Is.EqualTo(-10));
        }

        // Village extra-dash unlock (user decision 2026-11-05): two charges share one recovery that the first dash of a
        // full set starts; when it ends both charges are back at once.
        [Test] public void TwoChargesShareOneRecoveryStartedByTheFirstDash()
        {
            var dash = new PlayerDashExecution { MaxCharges = 2 }; var profile = Movement();
            Assert.That(dash.Available(0), Is.EqualTo(2));
            dash.Begin(0, 1, profile);
            Assert.That(dash.Available(0), Is.EqualTo(1)); Assert.That(dash.CanStart(.1f), Is.False, "never during a dash");
            Assert.That(dash.CanStart(.2f), Is.True, "the second charge is ready once the first dash ends");
            Assert.That(dash.Remaining(.2f), Is.Zero);
            dash.Begin(.5f, 1, profile);
            Assert.That(dash.Read().ReadyAt, Is.EqualTo(1), "the second dash does not restart the recovery");
            Assert.That(dash.Available(.8f), Is.Zero); Assert.That(dash.CanStart(.8f), Is.False);
            Assert.That(dash.Remaining(.75f), Is.EqualTo(.25f).Within(1e-6f));
            Assert.That(dash.CanStart(Before(1)), Is.False);
            Assert.That(dash.Available(1), Is.EqualTo(2), "both charges come back together"); Assert.That(dash.CanStart(1), Is.True);
            dash.Begin(1, 1, profile);
            Assert.That(dash.Read().ReadyAt, Is.EqualTo(2), "a dash from a full set starts a new recovery");
            Assert.That(dash.Available(1.2f), Is.EqualTo(1));
        }

        [Test] public void OneSpentChargeStillRefillsTheWholeSetAtTheSharedRecovery()
        {
            var dash = new PlayerDashExecution { MaxCharges = 2 }; var profile = Movement();
            dash.Begin(0, 1, profile);
            Assert.That(dash.Available(.9f), Is.EqualTo(1));
            Assert.That(dash.Available(1), Is.EqualTo(2), "one dash used: the full set returns when the recovery ends");
            dash.Reset(); Assert.That(dash.Available(0), Is.EqualTo(2)); Assert.That(dash.MaxCharges, Is.EqualTo(2), "a reset keeps the unlock");
            Assert.Throws<ArgumentOutOfRangeException>(() => dash.MaxCharges = 0);
        }

        [Test] public void OneChargeKeepsTheOriginalDashResource()
        {
            var dash = new PlayerDashExecution(); var profile = Movement();
            Assert.That(dash.MaxCharges, Is.EqualTo(1));
            dash.Begin(0, 1, profile);
            Assert.That(dash.Available(.5f), Is.Zero); Assert.That(dash.Remaining(.5f), Is.EqualTo(.5f));
            Assert.That(dash.CanStart(Before(1)), Is.False); Assert.That(dash.CanStart(1), Is.True);
        }
    }
}
