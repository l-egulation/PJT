using System;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using Deviant.Definitions;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class ForcedDashExecutionTests
    {
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        static float Before(float positive) => BitConverter.Int32BitsToSingle(Bits(positive) - 1);
        static SkillMovementDefinition Definition(float start = .25f, float duration = .5f, float speed = 10, float recovery = .375f,
            string policy = null, string type = "dash", bool primary = false)
            => new SkillMovementDefinition(new MoveData { type = type, start = start, duration = duration, speed = speed,
                wallRecovery = recovery, wallPolicy = policy, primaryPush = primary, endDamage = primary ? 20 : 0 });
        static ForcedDashExecution Dash(float start = .25f, float duration = .5f, float speed = 10, float recovery = .375f,
            string policy = null, string type = "dash", bool primary = false)
            => new ForcedDashExecution(Definition(start, duration, speed, recovery, policy, type, primary));
        static void Velocity(ref ForcedDashExecution dash, bool activated, float elapsed, float step, int direction, bool expected, float velocity = 0)
        {
            Assert.That(dash.TryHorizontalVelocity(activated, elapsed, step, direction, out float actual), Is.EqualTo(expected));
            Assert.That(Bits(actual), Is.EqualTo(Bits(velocity)));
        }

        [Test] public void StartsWithoutMovementCompletionOrAnyWallCutoff()
        {
            var dash = Dash(); var state = dash.Read();
            Assert.That(state.MovementDone, Is.False); Assert.That(state.WallStopTime, Is.EqualTo(float.PositiveInfinity));
            Assert.That(dash.DirectHitAllowed(0), Is.True); Assert.That(dash.DirectHitAllowed(float.MaxValue), Is.True);
            Assert.That(dash.IsMoving(false, .25f, .02f), Is.False);
            Assert.That(dash.AfterMovement(false, 1, .02f, true).Ended, Is.False);
            Assert.That(dash.MovementDone, Is.False); Assert.That(dash.Read().WallStopTime, Is.EqualTo(float.PositiveInfinity));
        }

        [TestCase("target_dash_recast")] [TestCase("target_teleport_strike")] [TestCase("target_teleport")] [TestCase(null)]
        public void NonDashDefinitionsNeverOwnTranslationOrAfterMovement(string type)
        {
            var dash = Dash(type: type);
            Assert.That(dash.IsMoving(true, .3f, .02f), Is.False);
            Velocity(ref dash, true, .3f, .02f, 1, false);
            Assert.That(dash.AfterMovement(true, 1, .02f, true).Ended, Is.False);
            Assert.That(dash.MovementDone, Is.False); Assert.That(dash.DirectHitAllowed(20), Is.True);
        }
        [Test] public void MissingMovementDefinitionLeavesOtherCastBehaviorsAlone()
        {
            var dash = new ForcedDashExecution(null);
            Assert.That(dash.IsMoving(true, .25f, .5f), Is.False);
            Velocity(ref dash, true, .25f, .5f, 1, false);
            Assert.That(dash.AfterMovement(true, 1, 1, true).Ended, Is.False);
            Assert.That(dash.DirectHitAllowed(float.MaxValue), Is.True);
        }

        [Test] public void MovementAdmissionIsStrictAtStartAndHalfOpenAtEnd()
        {
            var dash = Dash();
            Assert.That(dash.IsMoving(true, 0, .25f), Is.False, "A step ending exactly at start has no translation.");
            Assert.That(dash.IsMoving(true, 0, BitConverter.Int32BitsToSingle(Bits(.25f) + 1)), Is.True);
            Assert.That(dash.IsMoving(true, .25f, .02f), Is.True);
            Assert.That(dash.IsMoving(true, Before(.75f), .02f), Is.True);
            Assert.That(dash.IsMoving(true, .75f, .02f), Is.False);
            Assert.That(dash.IsMoving(true, 1, .02f), Is.False);
            Assert.That(dash.MovementDone, Is.False, "Queries do not finish movement or retain time.");
        }
        [TestCase(-1)] [TestCase(1)]
        public void HorizontalVelocityClipsBothPartialBoundariesAndALongStep(int direction)
        {
            var dash = Dash();
            Velocity(ref dash, true, 0, .25f, direction, false);
            Velocity(ref dash, true, .125f, .25f, direction, true, direction * 5);
            Velocity(ref dash, true, .25f, .25f, direction, true, direction * 10);
            Velocity(ref dash, true, .625f, .25f, direction, true, direction * 5);
            Velocity(ref dash, true, 0, 1, direction, true, direction * 5);
            Velocity(ref dash, true, .75f, .25f, direction, false);
            Assert.That(dash.MovementDone, Is.False);
        }
        [Test] public void QueryingWithoutActivationDoesNotDonateElapsedMotionToLaterActivation()
        {
            var dash = Dash();
            Velocity(ref dash, false, 0, .625f, 1, false);
            Velocity(ref dash, true, .625f, .25f, 1, true, 5);
            Assert.That(dash.IsMoving(true, .75f, .25f), Is.False);
            Assert.That(dash.Read().WallStopTime, Is.EqualTo(float.PositiveInfinity));
        }

        [Test] public void WallBeforeMovementStartDoesNotEndTranslationOrInstallCutoff()
        {
            var dash = Dash();
            var result = dash.AfterMovement(true, 0, .125f, true);
            Assert.That(result.Ended, Is.False); Assert.That(result.OverridesMotionEnd, Is.False);
            Assert.That(dash.MovementDone, Is.False); Assert.That(dash.Read().WallStopTime, Is.EqualTo(float.PositiveInfinity));
            Assert.That(dash.IsMoving(true, .25f, .125f), Is.True);
        }
        [Test] public void WallAtExactStartEndsMovementEvenThoughTheStepHasNoTranslation()
        {
            var dash = Dash(); Velocity(ref dash, true, 0, .25f, 1, false);
            var result = dash.AfterMovement(true, 0, .25f, true);
            Assert.That(result.Ended, Is.True); Assert.That(result.OverridesMotionEnd, Is.True);
            Assert.That(Bits(result.MotionEnd), Is.EqualTo(Bits(.625f)));
            Assert.That(dash.Read().MovementDone, Is.True); Assert.That(dash.Read().WallStopTime, Is.EqualTo(.25f));
            Assert.That(dash.DirectHitAllowed(Before(.25f)), Is.True, "The current tick's final sweep can still hit.");
            Assert.That(dash.DirectHitAllowed(.25f), Is.False, "The next tick starts at the wall cutoff.");
            Assert.That(dash.DirectHitAllowed(1), Is.False);
        }
        [Test] public void WallRecoveryAndCutoffUseElapsedPlusStepInOriginalOrder()
        {
            var dash = Dash(start: .15f, duration: .2f, recovery: .25f);
            float elapsed = .137f, step = .031f;
            var result = dash.AfterMovement(true, elapsed, step, true);
            Assert.That(result.Ended && result.OverridesMotionEnd, Is.True);
            Assert.That(Bits(dash.Read().WallStopTime), Is.EqualTo(Bits(elapsed + step)));
            Assert.That(Bits(result.MotionEnd), Is.EqualTo(Bits(elapsed + step + .25f)));
            Assert.That(dash.DirectHitAllowed(elapsed), Is.True);
            Assert.That(dash.IsMoving(true, elapsed, step), Is.False); Velocity(ref dash, true, elapsed, step, 1, false);
        }
        [TestCase(.625f, .125f)] [TestCase(0f, 1f)]
        public void NaturalEndCompletesWithoutAHitCutoffOrRecoveryOverride(float elapsed, float step)
        {
            var dash = Dash(); var result = dash.AfterMovement(true, elapsed, step, false);
            Assert.That(result.Ended, Is.True); Assert.That(result.OverridesMotionEnd, Is.False);
            Assert.That(dash.MovementDone, Is.True); Assert.That(dash.Read().WallStopTime, Is.EqualTo(float.PositiveInfinity));
            Assert.That(dash.DirectHitAllowed(.75f), Is.True); Assert.That(dash.DirectHitAllowed(20), Is.True);
            Assert.That(dash.IsMoving(true, elapsed, step), Is.False); Velocity(ref dash, true, elapsed, step, -1, false);
        }
        [Test] public void WallWinsOverNaturalEndInTheSameLongStep()
        {
            var dash = Dash(); var result = dash.AfterMovement(true, 0, 1, true);
            Assert.That(result.Ended && result.OverridesMotionEnd, Is.True); Assert.That(result.MotionEnd, Is.EqualTo(1.375f));
            Assert.That(dash.Read().WallStopTime, Is.EqualTo(1)); Assert.That(dash.DirectHitAllowed(1), Is.False);
        }
        [Test] public void ContactAfterNaturalEndIsStillAWallUntilAfterMovementClaimsCompletion()
        {
            var dash = Dash(); Assert.That(dash.IsMoving(true, 1, .125f), Is.False);
            var result = dash.AfterMovement(true, 1, .125f, true);
            Assert.That(result.Ended && result.OverridesMotionEnd, Is.True); Assert.That(dash.Read().WallStopTime, Is.EqualTo(1.125f));
        }
        [TestCase(false)] [TestCase(true)]
        public void CompletedMovementNeverRewritesItsReasonOnAnotherContact(bool wallFirst)
        {
            var dash = Dash(); var first = dash.AfterMovement(true, .625f, .125f, wallFirst); var state = dash.Read();
            Assert.That(first.Ended, Is.True);
            var again = dash.AfterMovement(true, 2, 1, !wallFirst);
            Assert.That(again.Ended, Is.False); Assert.That(again.OverridesMotionEnd, Is.False);
            Assert.That(dash.Read().WallStopTime, Is.EqualTo(state.WallStopTime)); Assert.That(dash.Read().MovementDone, Is.True);
        }
        [Test] public void WallContinueSpinStopsOnlyTranslationAndKeepsRemainingDirectHits()
        {
            var dash = Dash(policy: "stop_translation_continue_spin"); var result = dash.AfterMovement(true, .25f, .125f, true);
            Assert.That(result.Ended, Is.True); Assert.That(result.OverridesMotionEnd, Is.False);
            Assert.That(dash.Read().WallStopTime, Is.EqualTo(.375f)); Assert.That(dash.MovementDone, Is.True);
            Velocity(ref dash, true, .375f, .125f, 1, false); Assert.That(dash.IsMoving(true, .375f, .125f), Is.False);
            Assert.That(dash.DirectHitAllowed(.375f), Is.True); Assert.That(dash.DirectHitAllowed(.8f), Is.True);
        }

        [Test] public void BossPrimaryStopChangesRecoveryButNotTheWallCutoffOrDirectHits()
        {
            var dash = Dash(primary: true); float end = dash.EndAtPrimaryBoss(.3125f, .125f);
            Assert.That(Bits(end), Is.EqualTo(Bits(.8125f))); Assert.That(dash.MovementDone, Is.True);
            Assert.That(dash.Read().WallStopTime, Is.EqualTo(float.PositiveInfinity));
            Assert.That(dash.DirectHitAllowed(.4375f), Is.True); Assert.That(dash.DirectHitAllowed(10), Is.True);
            Assert.That(dash.IsMoving(true, .4375f, .125f), Is.False); Velocity(ref dash, true, .4375f, .125f, 1, false);
            Assert.That(dash.AfterMovement(true, .4375f, .125f, true).Ended, Is.False);
        }
        [Test] public void AcceptedBossPrimaryMayFollowTheFinalContactWithoutClearingItsCutoff()
        {
            var dash = Dash(primary: true); var wall = dash.AfterMovement(true, .25f, .125f, true);
            Assert.That(wall.Ended, Is.True); Assert.That(dash.DirectHitAllowed(.25f), Is.True);
            float bossEnd = dash.EndAtPrimaryBoss(.3125f, .125f);
            Assert.That(Bits(bossEnd), Is.EqualTo(Bits(.8125f))); Assert.That(dash.Read().WallStopTime, Is.EqualTo(.375f));
            Assert.That(dash.DirectHitAllowed(.375f), Is.False);
        }
        [Test] public void AcceptedBossPrimaryMayFollowNaturalCompletionWithoutInventingAWall()
        {
            var dash = Dash(primary: true); Assert.That(dash.AfterMovement(true, .625f, .125f, false).Ended, Is.True);
            Assert.That(dash.EndAtPrimaryBoss(.625f, .125f), Is.EqualTo(1.125f));
            Assert.That(dash.Read().WallStopTime, Is.EqualTo(float.PositiveInfinity)); Assert.That(dash.DirectHitAllowed(1), Is.True);
        }
        [Test] public void AcceptedPrimaryOwnerCallbackKeepsItsExistingTimeCalculation()
        {
            var dash = Dash(start: .2f, duration: .3f, recovery: .35f, primary: true);
            float elapsed = .197f, step = .031f;
            Assert.That(Bits(dash.EndAtPrimaryBoss(elapsed, step)), Is.EqualTo(Bits(elapsed + step + .35f)));
        }
        [Test] public void DetachedSnapshotDoesNotChangeWhenTheSingleOwnerCompletes()
        {
            var dash = Dash(); var before = dash.Read(); dash.AfterMovement(true, .25f, .125f, true);
            Assert.That(before.MovementDone, Is.False); Assert.That(before.WallStopTime, Is.EqualTo(float.PositiveInfinity));
            Assert.That(dash.Read().MovementDone, Is.True); Assert.That(dash.Read().WallStopTime, Is.EqualTo(.375f));
        }

        [Test] public void ForcedMovementOverridesHitAndStatusControlOnlyWhileTheDashWindowIsActive()
        {
            var dash = Dash();
            ActorMotionProposal Resolve(float elapsed, float desiredX, bool freeze) => ActorMotionResolver.Resolve(new ActorMotionInput(
                elapsed, .125f, desiredX, 7, false, freeze, dash.IsMoving(true, elapsed, .125f), false, false, -5, 1, 2, 2, 2, 2, 60, 100));
            var preparing = Resolve(0, 0, false); Assert.That(preparing.VelocityX, Is.EqualTo(-5), "Knockback precedes all input/status locks outside forced motion.");
            Velocity(ref dash, true, .25f, .125f, 1, true, 10);
            var active = Resolve(.25f, 10, true); Assert.That(active.VelocityX, Is.EqualTo(10)); Assert.That(active.VelocityY, Is.Zero);
            dash.AfterMovement(true, .25f, .125f, true);
            var stopped = Resolve(.375f, 0, false); Assert.That(stopped.VelocityX, Is.EqualTo(-5)); Assert.That(stopped.VelocityY, Is.LessThan(7));
        }
        [Test] public void CompletedMovementRestoresStatusSuppressionWhenNoKnockbackIsActive()
        {
            var dash = Dash(); dash.AfterMovement(true, .625f, .125f, false);
            var motion = ActorMotionResolver.Resolve(new ActorMotionInput(.75f, .125f, 10, 0, true, false,
                dash.IsMoving(true, .75f, .125f), false, false, -5, 0, 2, 2, 2, 2, 60, 100));
            Assert.That(motion.VelocityX, Is.Zero); Assert.That(motion.DeltaX, Is.Zero);
        }
    }
}
