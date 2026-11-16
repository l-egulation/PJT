using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using Deviant.Definitions;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class MissileExecutionTests
    {
        sealed class State
        {
            public MissileExecution Execution;
            public bool Activated = true, Cancelled, MotionDone, OwnerAlive = true, WallHit, Grounded;
            public float Now, StartedAt, StepDuration = .02f, MovedX, VerticalSpeed, MotionEnd;
            public int Explosions;
            public readonly List<string> Events = new List<string>();
            public Action Visual, Explosion, Hop, Motion, Record;
        }
        struct Host : IMissileExecutionHost
        {
            public State State;
            public bool Activated => State.Activated;
            public bool Cancelled => State.Cancelled;
            public bool MotionDone => State.MotionDone;
            public bool OwnerAlive => State.OwnerAlive;
            public float Now => State.Now;
            public float StartedAt => State.StartedAt;
            public float StepDuration => State.StepDuration;
            public float MovedX => State.MovedX;
            public bool WallHit => State.WallHit;
            public bool Grounded => State.Grounded;
            public void UpdateVisual() { State.Events.Add("visual"); State.Visual?.Invoke(); }
            public void Explode() { State.Events.Add("explode"); State.Explosions++; State.Explosion?.Invoke(); }
            public void StartHop(float verticalSpeed) { State.Events.Add("hop"); State.VerticalSpeed = verticalSpeed; State.Grounded = false; State.Hop?.Invoke(); }
            public void SetMotionEnd(float elapsed) { State.Events.Add("motion"); State.MotionEnd = elapsed; State.Motion?.Invoke(); }
            public void RecordEnd() { State.Events.Add("record"); State.Record?.Invoke(); }
        }
        static Host Create(float speed = 12, float distance = 10, float delay = .2f, float height = .5f, float recoil = .6f, float recovery = .3f, float launch = 0)
        {
            var definition = new SkillMissileDefinition(new MissileData { speed = speed, distance = distance, manualDelay = delay, recoilHeight = height, recoilDistance = recoil, recovery = recovery, blastRadius = 2 });
            var state = new State { Now = launch, StartedAt = launch, Execution = new MissileExecution(definition, 66.6666667f, 104.1666667f) };
            state.Execution.Launch(launch); return new Host { State = state };
        }
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);

        [TestCase("activation")] [TestCase("cancel")] [TestCase("motion")] [TestCase("life")]
        public void FlightAndNewMovementReadCastEligibilityLiveWithoutOwningAnotherCancellation(string fact)
        {
            var host = Create(); var state = host.State; state.Now = .4f;
            Assert.That(state.Execution.Prepare(.02f, ref host), Is.True); var before = state.Execution.Read();
            if (fact == "activation") state.Activated = false;
            if (fact == "cancel") state.Cancelled = true;
            if (fact == "motion") state.MotionDone = true;
            if (fact == "life") state.OwnerAlive = false;
            Assert.That(state.Execution.IsFlying(ref host), Is.False);
            Assert.That(state.Execution.Prepare(.5f, ref host), Is.False);
            Assert.That(state.Execution.TryManualDetonation(ref host), Is.False);
            state.MovedX = 20; state.WallHit = true; state.Execution.AfterMovement(ref host);
            Assert.That(state.Execution.Read(), Is.EqualTo(before)); Assert.That(state.Events, Is.Empty);
        }
        [Test] public void ExplosionDamageGrowsLinearlyWithFlightDistanceUpToTheTotalCap()
        {
            // 2026-11-10: 폭발 40에서 비행 거리에 비례해 30unit 이상이면 총 100.
            var bonus = new SkillMissileDefinition(new MissileData { speed = 60, distance = 600, blastRadius = 2, distanceBonusMaxDamage = 100, distanceBonusFullDistance = 30 });
            Assert.That(bonus.DamageAfter(40, 0), Is.EqualTo(40).Within(.0001f));
            Assert.That(bonus.DamageAfter(40, 15), Is.EqualTo(70).Within(.0001f));
            Assert.That(bonus.DamageAfter(40, 30), Is.EqualTo(100).Within(.0001f));
            Assert.That(bonus.DamageAfter(40, 300), Is.EqualTo(100).Within(.0001f));
            Assert.That(bonus.DamageAfter(40, -1), Is.EqualTo(40).Within(.0001f));
            var plain = new SkillMissileDefinition(new MissileData { speed = 60, distance = 600, blastRadius = 2 });
            Assert.That(plain.DamageAfter(40, 300), Is.EqualTo(40), "no bonus fields keeps the old fixed damage");
        }
        [Test] public void AnEarlyPressDoesNotArmOrQueueDetonationButANewExactDelayPressDoes()
        {
            var host = Create(launch: 4); var state = host.State;
            state.Now = 4.125f;
            Assert.That(state.Execution.TryManualDetonation(ref host), Is.False); Assert.That(state.Execution.Read().EndPending, Is.False);
            state.Now = 4.25f; state.Execution.Prepare(.02f, ref host); state.MovedX = .125f; state.Execution.AfterMovement(ref host);
            Assert.That(state.Execution.IsFlying(ref host), Is.True); Assert.That(state.Explosions, Is.Zero);
            Assert.That(state.Execution.TryManualDetonation(ref host), Is.True); Assert.That(state.Explosions, Is.EqualTo(1));
            Assert.That(state.Execution.TryManualDetonation(ref host), Is.False);
            var exact = Create(delay: .25f, launch: 4); exact.State.Now = 4.25f;
            Assert.That(exact.State.Execution.TryManualDetonation(ref exact), Is.True);
        }
        [Test] public void ManualDelayIncludesTheExistingEpsilonAndFlightAgeDoesNotAccumulateOnRead()
        {
            var host = Create(delay: .25f, launch: 4); var state = host.State;
            state.Now = 4.25f - .0000005f;
            Assert.That(state.Execution.FlightAge(3), Is.Zero);
            Assert.That(Bits(state.Execution.FlightAge(state.Now)), Is.EqualTo(Bits(state.Now - 4)));
            Assert.That(state.Execution.TryManualDetonation(ref host), Is.True);
            var early = Create(delay: .25f, launch: 4); early.State.Now = 4.25f - .000002f;
            Assert.That(early.State.Execution.TryManualDetonation(ref early), Is.False);
            Assert.That(early.State.Execution.Read().Ended, Is.False);
        }
        [Test] public void TravelUsesActualAbsoluteDisplacementAndPreparesOnlyTheRemainingDistance()
        {
            var host = Create(speed: 2, distance: 1, delay: 99); var state = host.State;
            Assert.That(state.Execution.Prepare(.25f, ref host), Is.True); Assert.That(state.Execution.Move, Is.EqualTo(.5f));
            state.MovedX = -.125f; state.Execution.AfterMovement(ref host);
            Assert.That(state.Execution.Read().Travel, Is.EqualTo(.125f)); Assert.That(state.Explosions, Is.Zero);
            state.Execution.Prepare(.25f, ref host); state.MovedX = .5f; state.Execution.AfterMovement(ref host);
            Assert.That(state.Execution.Read().Travel, Is.EqualTo(.625f));
            state.Execution.Prepare(.25f, ref host);
            Assert.That(state.Execution.Move, Is.EqualTo(.375f)); Assert.That(state.Execution.Read().EndPending, Is.True);
            Assert.That(state.Execution.FlightVelocity(1, .25f), Is.EqualTo(1.5f)); Assert.That(state.Execution.FlightVelocity(-1, .25f), Is.EqualTo(-1.5f));
            state.MovedX = .375f; state.Execution.AfterMovement(ref host);
            Assert.That(state.Explosions, Is.EqualTo(1)); Assert.That(state.Execution.Read().Travel, Is.EqualTo(1));
            Assert.That(state.Execution.Read().RecoilStartedAt, Is.EqualTo(.02f), "Automatic distance termination does not wait for manual input eligibility.");
        }
        [TestCase(false)] [TestCase(true)]
        public void NativeContactOrMovementWallEndsAfterActualMovementAtTheSameTick(bool wall)
        {
            var host = Create(delay: 99); var state = host.State; state.Now = .0625f;
            state.Execution.Prepare(.02f, ref host);
            if (wall) state.WallHit = true;
            else { state.Execution.Contact(); state.Execution.SetMove(.0625f); }
            Assert.That(state.Explosions, Is.Zero);
            state.MovedX = -.03125f;
            state.Visual = () =>
            {
                Assert.That(state.Execution.Read().Travel, Is.EqualTo(.03125f));
                Assert.That(state.Execution.Read().Ended, Is.False);
            };
            state.Execution.AfterMovement(ref host);
            Assert.That(state.Events, Is.EqualTo(new[] { "visual", "explode", "hop", "motion", "record" }));
            Assert.That(state.Execution.Read().Ended, Is.True); Assert.That(state.Explosions, Is.EqualTo(1));
        }
        [Test] public void ContactClaimSurvivesAnInterruptedNativeSweepWithoutCommittingItsLocalNearestMove()
        {
            var host = Create(delay: 99); var state = host.State;
            state.Execution.Prepare(.02f, ref host); float proposed = state.Execution.Move;
            state.Execution.Contact(); // A later native candidate can throw before SetMove.
            Assert.That(state.Execution.Move, Is.EqualTo(proposed)); Assert.That(state.Execution.Read().EndPending, Is.True);
            state.MovedX = .01f; state.Execution.AfterMovement(ref host);
            Assert.That(state.Explosions, Is.EqualTo(1)); Assert.That(state.Execution.Read().Travel, Is.EqualTo(.01f));
        }
        [Test] public void ANewPreparationReplacesThePreviousPendingContactRatherThanInventingABuffer()
        {
            var host = Create(); var state = host.State;
            state.Execution.Prepare(.02f, ref host); state.Execution.Contact(); state.Execution.SetMove(0);
            state.Execution.Prepare(.02f, ref host);
            Assert.That(state.Execution.Read().EndPending, Is.False); Assert.That(state.Execution.Move, Is.GreaterThan(0));
            state.Execution.AfterMovement(ref host); Assert.That(state.Explosions, Is.Zero);
        }
        [TestCase(false)] [TestCase(true)]
        public void EndClaimsFlightBeforeExplosionAndRetainsTheManualVersusAutomaticHopStart(bool automatic)
        {
            var host = Create(delay: 0, launch: 4); var state = host.State; state.Now = 5; state.StepDuration = .125f;
            state.Explosion = () =>
            {
                var nested = new Host { State = state };
                Assert.That(state.Execution.Read().Ended, Is.True); Assert.That(state.Execution.IsFlying(ref nested), Is.False);
                Assert.That(state.Execution.TryManualDetonation(ref nested), Is.False);
                Assert.That(state.Execution.Prepare(.25f, ref nested), Is.False);
                state.Execution.AfterMovement(ref nested);
                Assert.That(state.Execution.Read().RecoilStartedAt, Is.EqualTo(-1));
            };
            if (automatic) { state.Execution.Prepare(.02f, ref host); state.Execution.Contact(); state.Execution.AfterMovement(ref host); }
            else Assert.That(state.Execution.TryManualDetonation(ref host), Is.True);
            float start = automatic ? 5.125f : 5;
            Assert.That(state.Execution.Read().RecoilStartedAt, Is.EqualTo(start)); Assert.That(state.MotionEnd, Is.EqualTo(start - 4 + .3f));
            Assert.That(state.Explosions, Is.EqualTo(1)); Assert.That(state.Execution.IsRecoiling(ref host), Is.True);
        }
        [Test] public void CancellationDuringExplosionDoesNotUnwindTheEnteredHopOrRecord()
        {
            var host = Create(delay: 0); var state = host.State;
            state.Explosion = () => { state.Cancelled = true; state.MotionDone = true; state.OwnerAlive = false; };
            Assert.That(state.Execution.TryManualDetonation(ref host), Is.True);
            Assert.That(state.Events, Is.EqualTo(new[] { "explode", "hop", "motion", "record" }));
            Assert.That(state.Execution.Read().RecoilStartedAt, Is.Zero); Assert.That(state.MotionEnd, Is.EqualTo(.3f));
            Assert.That(state.Execution.IsRecoiling(ref host), Is.False); Assert.That(state.Execution.Read().Ended, Is.True);
        }
        [Test] public void ReentryFromTheVisualUpdateCannotExplodeAgainAfterItsManualDetonation()
        {
            var host = Create(delay: 0); var state = host.State; state.Execution.Prepare(.02f, ref host); state.Execution.Contact();
            state.Visual = () => { var nested = new Host { State = state }; Assert.That(state.Execution.TryManualDetonation(ref nested), Is.True); };
            state.Execution.AfterMovement(ref host);
            Assert.That(state.Explosions, Is.EqualTo(1)); Assert.That(state.Execution.Read().RecoilStartedAt, Is.Zero);
            Assert.That(state.Events, Is.EqualTo(new[] { "visual", "explode", "hop", "motion", "record" }));
        }
        [Test] public void RecoilStartAndMotionEndReadTheCurrentClockAfterNativeCallbacks()
        {
            var host = Create(delay: 0, launch: 4); var state = host.State; state.Now = 5;
            state.Explosion = () => state.Now = 6;
            state.Hop = () => { state.Now = 7; state.StepDuration = .125f; };
            state.Record = () => state.Now = 8;
            state.Execution.Prepare(.02f, ref host); state.Execution.Contact(); state.Execution.AfterMovement(ref host);
            Assert.That(state.Execution.Read().RecoilStartedAt, Is.EqualTo(7.125f));
            Assert.That(state.MotionEnd, Is.EqualTo(7.125f - 4 + .3f)); Assert.That(state.Now, Is.EqualTo(8));
        }
        [Test] public void GroundStopIsStrictlyAfterStartPlusEpsilonAndWallStopHasNoSuchDelay()
        {
            var host = Create(delay: 0); var state = host.State; state.Now = 4;
            state.Execution.TryManualDetonation(ref host); state.Grounded = true;
            state.Execution.AfterMovement(ref host); Assert.That(state.Execution.Read().RecoilStopped, Is.False);
            state.Now = 4 + .000001f; state.Execution.AfterMovement(ref host); Assert.That(state.Execution.Read().RecoilStopped, Is.False);
            state.Now = 4 + .000002f; state.Execution.AfterMovement(ref host); Assert.That(state.Execution.Read().RecoilStopped, Is.True);
            var wall = Create(delay: 0); wall.State.Execution.TryManualDetonation(ref wall); wall.State.WallHit = true;
            wall.State.Execution.AfterMovement(ref wall); Assert.That(wall.State.Execution.Read().RecoilStopped, Is.True);
        }
        [Test] public void AutomaticHopDoesNotStopOnStaleGroundAtTheNextMovementStart()
        {
            var host = Create(delay: 99); var state = host.State;
            state.Execution.Prepare(.02f, ref host); state.Execution.Contact(); state.Execution.AfterMovement(ref host);
            state.Grounded = true; state.Now = state.Execution.Read().RecoilStartedAt;
            state.Execution.AfterMovement(ref host); Assert.That(state.Execution.Read().RecoilStopped, Is.False);
            state.Now += .02f; state.Execution.AfterMovement(ref host); Assert.That(state.Execution.Read().RecoilStopped, Is.True);
        }
        [Test] public void RecoilUsesTheExistingArcAndPartialLastStepAndDoesNotRequireOwnerLifeAgain()
        {
            var host = Create(delay: 0, height: .5f, recoil: .6f); var state = host.State; state.Now = 4;
            state.Execution.TryManualDetonation(ref host); var recoil = state.Execution.Read();
            float duration = (float)Math.Sqrt(1f / 66.6666667f) + (float)Math.Sqrt(1f / 104.1666667f);
            Assert.That(Bits(state.VerticalSpeed), Is.EqualTo(Bits((float)Math.Sqrt(66.6666667f))));
            Assert.That(Bits(recoil.RecoilDuration), Is.EqualTo(Bits(duration)));
            Assert.That(state.Execution.RecoilVelocity(1, 4, .02f), Is.LessThan(0)); Assert.That(state.Execution.RecoilVelocity(-1, 4, .02f), Is.GreaterThan(0));
            float end = recoil.RecoilStartedAt + duration;
            float partialVelocity = state.Execution.RecoilVelocity(1, end - .005f, .02f);
            Assert.That(partialVelocity, Is.LessThan(0));
            Assert.That(Math.Abs(partialVelocity), Is.LessThan(Math.Abs(state.Execution.RecoilVelocity(1, 4, .02f))));
            Assert.That(state.Execution.RecoilVelocity(1, end, .02f), Is.Zero);
            state.OwnerAlive = false; Assert.That(state.Execution.IsRecoiling(ref host), Is.True);
            state.Now = end - .000001f; Assert.That(state.Execution.IsRecoiling(ref host), Is.False);
        }
        [Test] public void VisualExceptionRetainsTravelButDoesNotEnterThePendingExplosion()
        {
            var host = Create(); var state = host.State; state.Execution.Prepare(.02f, ref host); state.Execution.Contact(); state.MovedX = -.125f;
            var error = new InvalidOperationException("visual"); state.Visual = () => throw error;
            Assert.That(Assert.Throws<InvalidOperationException>(() => state.Execution.AfterMovement(ref host)), Is.SameAs(error));
            Assert.That(state.Execution.Read().Travel, Is.EqualTo(.125f)); Assert.That(state.Execution.Read().EndPending, Is.True);
            Assert.That(state.Execution.Read().Ended, Is.False); Assert.That(state.Explosions, Is.Zero);
        }
        [TestCase("explode")] [TestCase("hop")] [TestCase("motion")] [TestCase("record")]
        public void EndExceptionsRetainTheirTerminalClaimAndOnlyTheReachedRecoveryFacts(string phase)
        {
            var host = Create(delay: 0); var state = host.State; state.Now = 4; var error = new InvalidOperationException(phase);
            if (phase == "explode") state.Explosion = () => throw error;
            if (phase == "hop") state.Hop = () => throw error;
            if (phase == "motion") state.Motion = () => throw error;
            if (phase == "record") state.Record = () => throw error;
            Assert.That(Assert.Throws<InvalidOperationException>(() => state.Execution.TryManualDetonation(ref host)), Is.SameAs(error));
            Assert.That(state.Execution.Read().Ended, Is.True); Assert.That(state.Explosions, Is.EqualTo(1));
            bool recoveryReached = phase == "motion" || phase == "record";
            Assert.That(state.Execution.Read().RecoilStartedAt, Is.EqualTo(recoveryReached ? 4 : -1));
            Assert.That(state.Execution.Read().RecoilDuration > 0, Is.EqualTo(recoveryReached));
            Assert.That(state.Execution.TryManualDetonation(ref host), Is.False); Assert.That(state.Explosions, Is.EqualTo(1));
        }
    }
}
