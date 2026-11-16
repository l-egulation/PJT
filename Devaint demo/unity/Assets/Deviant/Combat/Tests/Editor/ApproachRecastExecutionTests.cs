using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class ApproachRecastExecutionTests
    {
        sealed class State
        {
            public ApproachRecastExecution Execution;
            public bool SameWeapon = true, Cancelled, MotionDone, TargetHit, TargetAlive = true, RecastMovement = true, AfterMove;
            public float Now = 4, Elapsed = 1, Window = 3, Distance = 5, Speed = 2, Position, Target = 4, Destination;
            public float Moved = .5f, Requested = .5f, RemainingDistance = 3, MoveLimit, MotionEnd = 99, VerticalSpeed = 7;
            public int Starts, Captures, Moves, Records;
            public readonly List<string> Events = new List<string>();
            public Action Capture, Start, Resume, Move, End, ClearVertical, Record;
        }
        struct Host : IApproachRecastHost
        {
            public State State;
            public bool SameWeapon { get { State.Events.Add("weapon"); return State.SameWeapon; } }
            public bool Cancelled { get { State.Events.Add("cancelled"); return State.Cancelled; } }
            public bool MotionDone { get { State.Events.Add("motion-done"); return State.MotionDone; } }
            public bool TargetHit { get { State.Events.Add("hit"); return State.TargetHit; } }
            public bool MarkedTargetAlive { get { State.Events.Add("target"); return State.TargetAlive; } }
            public bool IsRecastMovement { get { State.Events.Add("movement"); return State.RecastMovement; } }
            public float Now { get { State.Events.Add("now"); return State.Now; } }
            public float Elapsed { get { State.Events.Add("elapsed"); return State.Elapsed; } }
            public float RecastWindow { get { State.Events.Add("window"); return State.Window; } }
            public float Distance { get { State.Events.Add("distance"); return State.Distance; } }
            public float Speed { get { State.Events.Add("speed"); return State.Speed; } }
            public float DistanceToDestination { get { State.Events.Add("destination-distance"); return State.AfterMove ? State.RemainingDistance : Math.Abs(State.Position - State.Destination); } }
            public float MovedDistance { get { State.Events.Add("moved"); return State.Moved; } }
            public float RequestedDistance { get { State.Events.Add("requested"); return State.Requested; } }
            public void CaptureDestination()
            {
                State.Events.Add("destination"); State.Captures++; State.Destination = State.Target; State.AfterMove = false; State.Capture?.Invoke();
            }
            public void CaptureOrigin() => State.Events.Add("origin");
            public void StartApproach() { State.Events.Add("start"); State.Starts++; State.Start?.Invoke(); }
            public void ResumeMotion() { State.Events.Add("resume"); State.Cancelled = State.MotionDone = false; State.Resume?.Invoke(); }
            public void MoveTowardDestination(float distance)
            {
                State.Events.Add("move"); State.MoveLimit = distance; State.Moves++; State.AfterMove = true; State.Move?.Invoke();
            }
            public void SetMotionEnd(float elapsed) { State.Events.Add("end"); State.MotionEnd = elapsed; State.End?.Invoke(); }
            public void ClearVerticalSpeed() { State.Events.Add("vertical"); State.VerticalSpeed = 0; State.ClearVertical?.Invoke(); }
            public void RecordApproach() { State.Events.Add("record"); State.Records++; State.Record?.Invoke(); }
        }
        static Host Create(bool marked = true)
        {
            var host = new Host { State = new State() };
            if (marked) { host.State.TargetHit = true; host.State.Execution.MarkAccepted(ref host); }
            host.State.Events.Clear(); return host;
        }
        static void Start(ref Host host) => Assert.That(host.State.Execution.TryStart(ref host), Is.True);

        [Test] public void UnlimitedApproachKeepsFollowingBeyondItsInitialDistanceAndMotionDeadline()
        {
            var host = Create(); var state = host.State;
            state.Distance = 0; state.Speed = 50; state.Target = 100; Start(ref host);
            Assert.That(state.Execution.Read().Duration, Is.EqualTo(2));
            Assert.That(state.Execution.Read().Budget, Is.EqualTo(float.PositiveInfinity));
            state.Elapsed = 2.9f; state.Target = 200; state.RemainingDistance = 150;
            state.Moved = state.Requested = 50; state.Execution.Advance(1, ref host);
            Assert.That(state.MoveLimit, Is.EqualTo(50));
            Assert.That(state.Execution.Read().Mode, Is.True);
            Assert.That(state.MotionEnd, Is.EqualTo(6.9f).Within(.0001f));
            state.Moved = .5f; state.Requested = 50;
            state.Execution.Advance(1, ref host);
            Assert.That(state.Execution.Read().Mode, Is.False, "Walls still terminate an unlimited approach.");
        }

        [Test] public void UnacceptedSelectionCannotCreateAWindowAndANewMarkUsesTheCurrentClock()
        {
            var host = Create(false); var state = host.State;
            Assert.That(state.Execution.Remaining(ref host), Is.Zero); Assert.That(state.Execution.TryStart(ref host), Is.False); Assert.That(state.Captures, Is.Zero);
            state.TargetHit = true; state.Now = 8; state.Window = 1.5f; state.Execution.MarkAccepted(ref host);
            Assert.That(state.Execution.Read().MarkedUntil, Is.EqualTo(9.5f)); Assert.That(state.Execution.Remaining(ref host), Is.EqualTo(1.5f));
            state.Now = 9; Assert.That(state.Execution.Remaining(ref host), Is.EqualTo(.5f)); Assert.That(state.Execution.Remaining(ref host), Is.EqualTo(.5f));
            state.Now = 9.5f; Assert.That(state.Execution.Remaining(ref host), Is.Zero); Assert.That(state.Execution.TryStart(ref host), Is.False);
            state.Now = 10; state.Window = .25f; state.Execution.MarkAccepted(ref host);
            Assert.That(state.Execution.Remaining(ref host), Is.EqualTo(.25f)); Assert.That(state.Execution.DeadlineFrom(4), Is.EqualTo(6.25f));
            Assert.That(state.Execution.Read().Consumed, Is.False);
        }
        [Test] public void WrongMovementDoesNotReadTheClockOrReplaceAnExistingDeadline()
        {
            var host = Create(); var state = host.State; float deadline = state.Execution.Read().MarkedUntil;
            state.RecastMovement = false; state.Now = 99; state.Events.Clear(); state.Execution.MarkAccepted(ref host);
            Assert.That(state.Events, Is.EqualTo(new[] { "movement" })); Assert.That(state.Execution.Read().MarkedUntil, Is.EqualTo(deadline));
            state.Events.Clear(); Assert.That(state.Execution.Remaining(ref host), Is.Zero);
            Assert.That(state.Events, Is.EqualTo(new[] { "cancelled", "hit", "target", "movement" }));
        }
        [Test] public void EligibilityShortCircuitsInTheExistingWeaponCancellationHitTargetMovementClockOrder()
        {
            var host = Create(); var state = host.State;
            state.SameWeapon = false; state.Events.Clear(); Assert.That(state.Execution.TryStart(ref host), Is.False); Assert.That(state.Events, Is.EqualTo(new[] { "weapon" }));
            state.SameWeapon = true; state.Cancelled = true; state.Events.Clear(); Assert.That(state.Execution.TryStart(ref host), Is.False); Assert.That(state.Events, Is.EqualTo(new[] { "weapon", "cancelled" }));
            state.Cancelled = false; state.TargetHit = false; state.Events.Clear(); Assert.That(state.Execution.TryStart(ref host), Is.False); Assert.That(state.Events, Is.EqualTo(new[] { "weapon", "cancelled", "hit" }));
            state.TargetHit = true; state.TargetAlive = false; state.Events.Clear(); Assert.That(state.Execution.TryStart(ref host), Is.False); Assert.That(state.Events, Is.EqualTo(new[] { "weapon", "cancelled", "hit", "target" }));
            state.TargetAlive = true; state.RecastMovement = false; state.Events.Clear(); Assert.That(state.Execution.TryStart(ref host), Is.False); Assert.That(state.Events, Is.EqualTo(new[] { "weapon", "cancelled", "hit", "target", "movement" }));
            state.RecastMovement = true; state.Now = 7; state.Events.Clear(); Assert.That(state.Execution.TryStart(ref host), Is.False); Assert.That(state.Events, Is.EqualTo(new[] { "weapon", "cancelled", "hit", "target", "movement", "now" }));
            Assert.That(state.Execution.Read().Consumed, Is.False); Assert.That(state.Starts, Is.Zero);
        }
        [Test] public void PrimaryMotionCompletionDoesNotEndTheMarkAndClockReadsDoNotAdvanceIt()
        {
            var host = Create(); var state = host.State; state.MotionDone = true; state.Now = 6;
            Assert.That(state.Execution.Remaining(ref host), Is.EqualTo(1)); Assert.That(state.Execution.IsApproaching(ref host), Is.False);
            Assert.That(state.Execution.Read().MarkedUntil, Is.EqualTo(7)); Start(ref host);
            Assert.That(state.MotionDone, Is.False); Assert.That(state.Execution.IsApproaching(ref host), Is.True); Assert.That(state.Execution.Remaining(ref host), Is.Zero);
        }
        [Test] public void AnEarlyAttemptIsNotBufferedAndOnlyANewCallAfterAcceptedHitStarts()
        {
            var host = Create(false); var state = host.State;
            Assert.That(state.Execution.TryStart(ref host), Is.False);
            state.TargetHit = true; state.Execution.MarkAccepted(ref host); state.Now += .5f;
            Assert.That(state.Execution.Read().Mode, Is.False); Assert.That(state.Starts, Is.Zero);
            Start(ref host); Assert.That(state.Starts, Is.EqualTo(1)); Assert.That(state.Execution.TryStart(ref host), Is.False);
        }
        [Test] public void DestinationIsCapturedBeforeConsumptionAndPlayerCancellationCompletesBeforeMotionResume()
        {
            var host = Create(); var state = host.State;
            state.Capture = () => Assert.That(state.Execution.Read().Consumed, Is.False);
            state.Start = () =>
            {
                Assert.That(state.Execution.Read().Consumed, Is.True); Assert.That(state.Execution.Read().Mode, Is.False);
                var nested = new Host { State = state }; Assert.That(state.Execution.TryStart(ref nested), Is.False);
                state.Cancelled = state.MotionDone = true; state.Position = 1; state.Target = 99; state.Distance = 2; state.Speed = 4; state.Elapsed = 8;
            };
            state.Resume = () => { Assert.That(state.Execution.Read().Mode, Is.False); Assert.That(state.Execution.Read().Budget, Is.Zero); };
            Start(ref host);
            Assert.That(state.Destination, Is.EqualTo(4), "The pre-callback destination stays captured, but the owner position and definition reads stay current.");
            Assert.That(state.Execution.Read().Budget, Is.EqualTo(2)); Assert.That(state.Execution.Read().Duration, Is.EqualTo(.5f));
            Assert.That(state.MotionEnd, Is.EqualTo(8.5f)); Assert.That(state.VerticalSpeed, Is.Zero); Assert.That(state.Execution.IsApproaching(ref host), Is.True);
            Assert.That(state.Records, Is.EqualTo(1));
        }
        [Test] public void StartSamplesGeometrySpeedAndElapsedOnlyAfterTheCallbackInTheOriginalOrder()
        {
            var host = Create(); var state = host.State; Start(ref host);
            Assert.That(state.Events, Is.EqualTo(new[] { "weapon", "cancelled", "hit", "target", "movement", "now", "destination", "start", "resume", "distance", "destination-distance", "speed", "elapsed", "end", "vertical", "record" }));
            Assert.That(state.Execution.Read().Duration, Is.EqualTo(2)); Assert.That(state.MotionEnd, Is.EqualTo(3));
        }
        [Test] public void DestinationFailurePrecedesTheClaimButStartFailureKeepsConsumptionWithoutResuming()
        {
            var host = Create(); var state = host.State; state.Capture = () => throw new InvalidOperationException("destination");
            Assert.Throws<InvalidOperationException>(() => state.Execution.TryStart(ref host));
            Assert.That(state.Execution.Read().Consumed, Is.False); Assert.That(state.Starts, Is.Zero); Assert.That(state.Execution.Read().Mode, Is.False);
            state.Capture = null; state.Start = () => { state.Cancelled = state.MotionDone = true; throw new InvalidOperationException("start"); };
            Assert.Throws<InvalidOperationException>(() => state.Execution.TryStart(ref host));
            Assert.That(state.Execution.Read().Consumed, Is.True); Assert.That(state.Execution.Read().Mode, Is.False); Assert.That(state.MotionEnd, Is.EqualTo(99));
            Assert.That(state.Cancelled, Is.True); Assert.That(state.MotionDone, Is.True); Assert.That(state.VerticalSpeed, Is.EqualTo(7)); Assert.That(state.Records, Is.Zero);
        }
        [Test] public void LaterExceptionsKeepEachAlreadyAppliedStartFactRatherThanRollingBackAtomically()
        {
            var host = Create(); var state = host.State; state.ClearVertical = () => throw new InvalidOperationException("vertical");
            Assert.Throws<InvalidOperationException>(() => state.Execution.TryStart(ref host));
            Assert.That(state.Execution.Read().Consumed, Is.True); Assert.That(state.Execution.Read().Mode, Is.True); Assert.That(state.Execution.Read().Budget, Is.EqualTo(5));
            Assert.That(state.Execution.Read().Duration, Is.EqualTo(2)); Assert.That(state.MotionEnd, Is.EqualTo(3)); Assert.That(state.VerticalSpeed, Is.Zero); Assert.That(state.Records, Is.Zero);
            var record = Create(); var recorded = record.State; recorded.Record = () => throw new InvalidOperationException("record");
            Assert.Throws<InvalidOperationException>(() => recorded.Execution.TryStart(ref record)); Assert.That(recorded.Execution.Read().Mode, Is.True); Assert.That(recorded.Records, Is.EqualTo(1));
        }
        [Test] public void RecordReentryCannotStartAgainAndItsCancellationIsNotOverwrittenAfterTheCallback()
        {
            var host = Create(); var state = host.State;
            state.Record = () =>
            {
                var nested = new Host { State = state }; Assert.That(state.Execution.IsApproaching(ref nested), Is.True);
                Assert.That(state.Execution.TryStart(ref nested), Is.False); state.Cancelled = state.MotionDone = true;
            };
            Start(ref host); Assert.That(state.Starts, Is.EqualTo(1)); Assert.That(state.Execution.Read().Mode, Is.True); Assert.That(state.Execution.IsApproaching(ref host), Is.False);
            Assert.That(state.Execution.Read().Budget, Is.EqualTo(5));
        }
        [Test] public void ZeroDistanceStillUsesTheMinimumVisualAndMotionDurationWithoutInventingAStop()
        {
            var host = Create(); var state = host.State; state.Target = state.Position; Start(ref host);
            Assert.That(state.Execution.Read().Duration, Is.Zero); Assert.That(state.Execution.VisualDuration, Is.EqualTo(.02f));
            Assert.That(state.MotionEnd, Is.EqualTo(1.02f)); Assert.That(state.Execution.IsApproaching(ref host), Is.True);
        }
        [Test] public void ActualDistanceDebitsBudgetAndCapsTheNextRequestWithoutAccumulatingAnotherClock()
        {
            var host = Create(); var state = host.State; state.Distance = 1; state.Speed = 4; Start(ref host);
            state.Moved = state.Requested = .375f; state.RemainingDistance = 10; state.Execution.Advance(.125f, ref host);
            Assert.That(state.MoveLimit, Is.EqualTo(.5f)); Assert.That(state.Execution.Read().Budget, Is.EqualTo(.625f)); Assert.That(state.Execution.Read().Mode, Is.True);
            state.Elapsed = 9; state.Moved = state.Requested = .625f; state.Execution.Advance(.25f, ref host);
            Assert.That(state.MoveLimit, Is.EqualTo(.625f)); Assert.That(state.Execution.Read().Budget, Is.Zero); Assert.That(state.Execution.Read().Mode, Is.False);
            Assert.That(state.MotionEnd, Is.EqualTo(9.25f)); Assert.That(state.Execution.Read().Duration, Is.EqualTo(.25f));
        }
        [Test] public void ANewAdvanceRecapturesTheTargetAndStopsAtTheLivePostMoveClockOnPartialTravel()
        {
            var host = Create(); var state = host.State; Start(ref host); state.Events.Clear(); state.Target = 20;
            state.Requested = 1; state.Moved = .25f; state.RemainingDistance = 19; state.Move = () => state.Elapsed = 12;
            state.Execution.Advance(.25f, ref host);
            Assert.That(state.Destination, Is.EqualTo(20)); Assert.That(state.Execution.Read().Budget, Is.EqualTo(4.75f));
            Assert.That(state.Execution.Read().Mode, Is.False); Assert.That(state.MotionEnd, Is.EqualTo(12.25f));
            Assert.That(state.Events, Is.EqualTo(new[] { "target", "destination", "origin", "speed", "move", "moved", "destination-distance", "requested", "elapsed", "end" }));
        }
        [TestCase(.005f, .5f, .5f, true)]
        [TestCase(.004999f, .5f, .5f, false)]
        [TestCase(2f, .5f, .501f, true)]
        [TestCase(2f, .5f, .501001f, false)]
        public void ArrivalAndPartialTravelRetainTheirStrictBoundaryComparisons(float remaining, float moved, float requested, bool active)
        {
            var host = Create(); var state = host.State; Start(ref host);
            state.RemainingDistance = remaining; state.Moved = moved; state.Requested = requested; state.Execution.Advance(.25f, ref host);
            Assert.That(state.Execution.Read().Mode, Is.EqualTo(active)); Assert.That(state.Execution.Read().Budget, Is.EqualTo(4.5f));
        }
        [TestCase(.0005f, false)] [TestCase(.001f, false)] [TestCase(.002f, true)]
        public void RemainingBudgetEndsAtTheExistingInclusiveThreshold(float budget, bool active)
        {
            var host = Create(); var state = host.State; state.Distance = budget; state.Target = 1; Start(ref host);
            state.Moved = state.Requested = 0; state.RemainingDistance = 1; state.Execution.Advance(.02f, ref host);
            Assert.That(state.Execution.Read().Mode, Is.EqualTo(active)); Assert.That(state.Execution.Read().Budget, Is.EqualTo(budget));
        }
        [Test] public void LostTargetEndsAtCurrentElapsedWithoutReadingGeometryOrSpendingBudget()
        {
            var host = Create(); var state = host.State; Start(ref host); var before = state.Execution.Read(); state.TargetAlive = false; state.Elapsed = 13; state.Events.Clear();
            state.Execution.Advance(.5f, ref host);
            Assert.That(state.Events, Is.EqualTo(new[] { "target", "elapsed", "end" })); Assert.That(state.MotionEnd, Is.EqualTo(13)); Assert.That(state.Execution.Read().Mode, Is.False);
            Assert.That(state.Execution.Read().Budget, Is.EqualTo(before.Budget)); Assert.That(state.Execution.Read().Consumed, Is.True);
        }
        [Test] public void AnInterruptedMoveDoesNotDebitButAFailedCompletionKeepsTheDebitAndModeStop()
        {
            var host = Create(); var state = host.State; Start(ref host); state.Move = () => throw new InvalidOperationException("move");
            Assert.Throws<InvalidOperationException>(() => state.Execution.Advance(.25f, ref host));
            Assert.That(state.Execution.Read().Budget, Is.EqualTo(5)); Assert.That(state.Execution.Read().Mode, Is.True);
            state.Move = null; state.Moved = .125f; state.Requested = 1; state.End = () => throw new InvalidOperationException("end");
            Assert.Throws<InvalidOperationException>(() => state.Execution.Advance(.25f, ref host));
            Assert.That(state.Execution.Read().Budget, Is.EqualTo(4.875f)); Assert.That(state.Execution.Read().Mode, Is.False); Assert.That(state.MotionEnd, Is.EqualTo(1.25f));
        }
        [Test] public void ArrivalShortCircuitDoesNotReadARequestedDistanceThatNativeDidNotNeed()
        {
            var host = Create(); var state = host.State; Start(ref host); state.RemainingDistance = 0; state.Events.Clear();
            state.Execution.Advance(.25f, ref host);
            Assert.That(state.Events.Contains("requested"), Is.False); Assert.That(state.Execution.Read().Mode, Is.False);
        }
        [Test] public void ForgetConsumesOnlyAndCannotResetOrRefundAnEnteredApproach()
        {
            var host = Create(); var state = host.State; state.Execution.Forget();
            Assert.That(state.Execution.Remaining(ref host), Is.Zero); Assert.That(state.Execution.TryStart(ref host), Is.False);
            state.Now = 5; state.Execution.MarkAccepted(ref host); Assert.That(state.Execution.Read().MarkedUntil, Is.EqualTo(8)); Assert.That(state.Execution.Read().Consumed, Is.True);
            var active = Create(); var entered = active.State; Start(ref active); var before = entered.Execution.Read(); entered.Execution.Forget();
            Assert.That(entered.Execution.Read(), Is.EqualTo(before)); Assert.That(entered.Execution.IsApproaching(ref active), Is.True);
            entered.Cancelled = true; Assert.That(entered.Execution.IsApproaching(ref active), Is.False); entered.Cancelled = false;
            Assert.That(entered.Execution.IsApproaching(ref active), Is.True, "Cancellation belongs to Cast, not to a second copied execution flag.");
        }
    }
}
