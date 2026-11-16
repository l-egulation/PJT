using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using Deviant.Definitions;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class SkillHitTrackExecutionTests
    {
        sealed class State
        {
            public SkillHitTrackExecution Execution = new SkillHitTrackExecution(-.000001f);
            public IReadOnlyList<SkillHitDefinition> Hits;
            public bool Activated = true, Cancelled, MotionDone, DirectAllowed = true, TargetHit, TeleportClaimed, MarkedAlive = true;
            public float Elapsed, Shift;
            public int Marked = 7, AliveReads, ShiftReads, ElapsedReads, DirectReads;
            public bool ArrivalInside = true, ArrivalExposed = true, Reject;
            public readonly List<int> Candidates = new List<int> { 7 };
            public readonly HitLedger Ledger = new HitLedger();
            public readonly List<string> Dispatches = new List<string>();
            public readonly List<(int hit, int target)> Claims = new List<(int hit, int target)>(), Accepted = new List<(int hit, int target)>();
            public Action<int> Dispatching;
            public Action<int, int> Receiving;
            public Action ReadingShift;
        }
        struct Host : ISkillHitTrackHost
        {
            public State State;
            public bool Activated => State.Activated;
            public bool Cancelled => State.Cancelled;
            public bool MotionDone => State.MotionDone;
            public float Elapsed { get { State.ElapsedReads++; return State.Elapsed; } }
            public float ChargeShift { get { State.ShiftReads++; State.ReadingShift?.Invoke(); return State.Shift; } }
            public bool DirectHitAllowed { get { State.DirectReads++; return State.DirectAllowed; } }
            public bool TargetHit => State.TargetHit;
            public bool TeleportClaimed => State.TeleportClaimed;
            public bool MarkedTargetAlive { get { State.AliveReads++; return State.MarkedAlive; } }
            public void ApplyMarkedTarget(int index)
            {
                State.Dispatches.Add(index + ":marked"); State.Dispatching?.Invoke(index); Receive(State, index, State.Marked);
            }
            public void ApplyTeleportTarget(int index)
            {
                State.Dispatches.Add(index + ":teleport"); State.Dispatching?.Invoke(index);
                if (State.ArrivalInside && State.ArrivalExposed) Receive(State, index, State.Marked);
            }
            public void ApplyBox(int index, SkillHitBoxAnchor anchor)
            {
                State.Dispatches.Add(index + ":" + anchor); State.Dispatching?.Invoke(index);
                // An already-entered Native sweep owns its target enumeration.
                // Cancellation only affects the next authored hit admission.
                foreach (int target in State.Candidates) Receive(State, index, target);
            }
        }
        static void Receive(State state, int index, int target)
        {
            if (!state.Ledger.First(index, target)) return;
            state.Claims.Add((index, target)); state.Receiving?.Invoke(index, target);
            if (!state.Reject) state.Accepted.Add((index, target));
        }
        static HitData Hit(float time = 0, float active = .125f, string anchor = "actor")
            => new HitData { time = time, active = active, damage = 1, anchor = anchor, targets = "all" };
        static State Create(params HitData[] hits)
            => new State { Hits = new SkillExecutionDefinition(new SkillData { hits = hits }).Hits };
        static void Evaluate(State state, float step = 0)
        {
            var host = new Host { State = state }; state.Execution.Evaluate(state.Hits, step, ref host);
        }
        static void Commit(State state, float step = 0) => state.Execution.CommitEvaluation(state.Elapsed + step);

        [Test] public void TimeZeroAndSubTickWindowsAreObservedAtTheirBoundariesWithoutASecondClock()
        {
            var state = Create(Hit(0, 0), Hit(.25f, .000001f));
            Evaluate(state); Assert.That(state.Claims, Is.EqualTo(new[] { (0, 7) })); Commit(state);
            Evaluate(state); Assert.That(state.Dispatches, Is.EqualTo(new[] { "0:Actor" }));
            state.Elapsed = .125f; Evaluate(state, .25f); Commit(state, .25f);
            Assert.That(state.Claims, Is.EqualTo(new[] { (0, 7), (1, 7) }));
            state.Elapsed = .5f; Evaluate(state);
            Assert.That(state.Dispatches, Is.EqualTo(new[] { "0:Actor", "1:Actor" }), "A completed window is not reopened for a new query.");
        }
        [Test] public void OneCoarseEvaluationVisitsEveryIntersectedWindowInAuthoredIndexOrder()
        {
            var state = Create(Hit(.75f, .01f), Hit(0, .001f), Hit(.25f, .01f), Hit(.25f, .01f));
            Evaluate(state, 1);
            Assert.That(state.Claims, Is.EqualTo(new[] { (0, 7), (1, 7), (2, 7), (3, 7) }));
            Commit(state, 1); state.Elapsed = 1; state.Candidates.Add(8); Evaluate(state, .5f);
            Assert.That(state.Claims.Count, Is.EqualTo(4), "Crossed windows are not rescheduled at the observation time.");
        }
        [Test] public void ActiveWindowsAreQueriedAgainForLateEntrantsButExistingClaimsSurviveLeaveAndReturn()
        {
            var state = Create(Hit(0, .5f)); Evaluate(state, .125f); Commit(state, .125f);
            state.Candidates.Clear(); state.Elapsed = .125f; Evaluate(state, .125f); Commit(state, .125f);
            state.Candidates.Add(7); state.Candidates.Add(8); state.Elapsed = .25f; Evaluate(state, .125f); Commit(state, .125f);
            Assert.That(state.Claims, Is.EqualTo(new[] { (0, 7), (0, 8) }));
            state.Elapsed = .5f; state.Candidates.Add(9); Evaluate(state, .125f); Commit(state, .125f);
            Assert.That(state.Claims, Is.EqualTo(new[] { (0, 7), (0, 8), (0, 9) }), "The final crossing still queries the remaining active interval.");
            state.Elapsed = .625f; state.Candidates.Add(10); Evaluate(state, .125f);
            Assert.That(state.Claims.Count, Is.EqualTo(3));
        }
        [Test] public void ProjectileAnchorsAreSkippedBeforeEligibilityClockChargeOrNativeDispatch()
        {
            var state = Create(Hit(anchor: "projectile"), Hit(anchor: "ranged_projectile"), Hit(anchor: "bomb_projectile"));
            state.ReadingShift = () => Assert.Fail("Projectile entries do not read the direct track clock.");
            Evaluate(state, 1);
            Assert.That(state.Dispatches, Is.Empty); Assert.That(state.ElapsedReads + state.ShiftReads + state.DirectReads + state.AliveReads, Is.Zero);
        }
        [TestCase("cancel")] [TestCase("motion")] [TestCase("cutoff")]
        public void ActorAndTeleportAdmissionStopsButActivatedIndependentAnchorsContinue(string reason)
        {
            var state = Create(Hit(), Hit(anchor: "teleport_target"), Hit(anchor: "saved_primary_world_pose"),
                Hit(anchor: "saved_spawn_world_pose"), Hit(anchor: "marked_target"));
            state.TargetHit = state.TeleportClaimed = true;
            state.Cancelled = reason == "cancel"; state.MotionDone = reason == "motion"; state.DirectAllowed = reason != "cutoff";
            Evaluate(state, 1);
            Assert.That(state.Dispatches, Is.EqualTo(new[] { "2:SavedPrimaryWorldPose", "3:SavedSpawnWorldPose", "4:marked" }));
        }
        [Test] public void CancellationFromAnEnteredSweepPreservesItsTargetsThenSuppressesOnlyLaterDirectHits()
        {
            var state = Create(Hit(), Hit(), Hit(anchor: "teleport_target"), Hit(anchor: "saved_primary_world_pose"), Hit(anchor: "marked_target"));
            state.Candidates.Add(8); state.TargetHit = state.TeleportClaimed = true;
            state.Receiving = (hit, target) => { if (hit == 0 && target == 7) state.Cancelled = state.MotionDone = true; };
            Evaluate(state, .125f);
            Assert.That(state.Claims, Is.EqualTo(new[] { (0, 7), (0, 8), (3, 7), (3, 8), (4, 7) }));
        }
        [Test] public void IndependentAdmissionReadsLiveActivationRatherThanTheEnteredOuterRegion()
        {
            var state = Create(Hit(), Hit(anchor: "saved_primary_world_pose"), Hit());
            state.Dispatching = index => { if (index == 0) state.Activated = false; };
            Evaluate(state, .125f);
            Assert.That(state.Dispatches, Is.EqualTo(new[] { "0:Actor", "2:Actor" }), "Actor admission deliberately has no duplicate activation guard.");
        }
        [Test] public void EachHitReadsCurrentClockAndChargeAfterThePreviousNativeCallback()
        {
            var state = Create(Hit(), Hit(.75f), Hit(.25f));
            state.Dispatching = index => { if (index == 0) { state.Elapsed = .5f; state.Shift = .25f; } };
            Evaluate(state, .25f);
            Assert.That(state.Dispatches, Is.EqualTo(new[] { "0:Actor", "2:Actor" }));
            Assert.That(state.ShiftReads, Is.EqualTo(3));
            state.Dispatching = null; state.Shift = 0; Evaluate(state, .25f);
            Assert.That(state.Claims, Does.Contain((1, 7)), "A later call uses the same live clock rather than an accumulated local timer.");
        }
        [Test] public void ElapsedIsReadBeforeChargeShiftWithoutReorderingFloatAdmission()
        {
            var state = Create(Hit(.5f)); state.ReadingShift = () => state.Elapsed = 1;
            Evaluate(state, .125f); Assert.That(state.Dispatches, Is.Empty);
            state.ReadingShift = null; Evaluate(state, .125f); Assert.That(state.Claims, Is.EqualTo(new[] { (0, 7) }));
        }
        [Test] public void MarkAndTeleportClaimsAndLifeAreReadForEachHitAfterNativeCallbacks()
        {
            var state = Create(Hit(), Hit(anchor: "marked_target"), Hit(anchor: "teleport_target"), Hit(anchor: "marked_target"));
            state.Receiving = (hit, target) =>
            {
                if (hit == 0) { state.TargetHit = state.TeleportClaimed = true; state.Marked = 19; }
                if (hit == 1) state.MarkedAlive = false;
            };
            Evaluate(state, .125f);
            Assert.That(state.Claims, Is.EqualTo(new[] { (0, 7), (1, 19) }));
            Assert.That(state.AliveReads, Is.EqualTo(3));
        }
        [TestCase(false, true)] [TestCase(true, false)] [TestCase(false, false)]
        public void MarkedTargetRequiresAcceptedPrimaryAndLifeWithoutUsingBoxOrOcclusion(bool targetHit, bool alive)
        {
            var state = Create(Hit(anchor: "marked_target")); state.TargetHit = targetHit; state.MarkedAlive = alive;
            state.ArrivalInside = state.ArrivalExposed = false; Evaluate(state, .125f);
            Assert.That(state.Claims, Is.Empty); Assert.That(state.AliveReads, Is.EqualTo(targetHit ? 1 : 0));
            state.TargetHit = state.MarkedAlive = true; Evaluate(state, .125f);
            Assert.That(state.Claims, Is.EqualTo(new[] { (0, 7) }));
        }
        [TestCase(false, true, true, true)] [TestCase(true, false, true, true)]
        [TestCase(true, true, false, true)] [TestCase(true, true, true, false)]
        public void TeleportRequiresItsClaimAndLifeThenDelegatesActualArrivalOverlapAndExposure(bool claimed, bool alive, bool inside, bool exposed)
        {
            var state = Create(Hit(anchor: "teleport_target")); state.TeleportClaimed = claimed; state.MarkedAlive = alive;
            state.ArrivalInside = inside; state.ArrivalExposed = exposed; Evaluate(state, .125f);
            Assert.That(state.Claims, Is.Empty); Assert.That(state.Dispatches.Count, Is.EqualTo(claimed && alive ? 1 : 0));
            state.TeleportClaimed = state.MarkedAlive = state.ArrivalInside = state.ArrivalExposed = true;
            Evaluate(state, .125f); Assert.That(state.Claims, Is.EqualTo(new[] { (0, 7) }));
        }
        [Test] public void RejectedNativeDamageKeepsTheExistingPerHitTargetClaim()
        {
            var state = Create(Hit()); state.Reject = true; Evaluate(state, .125f); state.Reject = false; Evaluate(state, .125f);
            Assert.That(state.Claims, Is.EqualTo(new[] { (0, 7) })); Assert.That(state.Accepted, Is.Empty);
        }
        [Test] public void ThrowingNativeDamageKeepsItsClaimButDoesNotCommitEvaluationOrSkipLaterUnclaimedTargets()
        {
            var state = Create(Hit(0, .01f), Hit(.125f, .01f)); state.Candidates.Add(8);
            state.Receiving = (_, __) => throw new InvalidOperationException("receive");
            Assert.Throws<InvalidOperationException>(() => Evaluate(state, .25f));
            Assert.That(state.Claims, Is.EqualTo(new[] { (0, 7) })); Assert.That(state.Accepted, Is.Empty);
            state.Receiving = null; state.Elapsed = 1; Evaluate(state, .125f);
            Assert.That(state.Claims, Is.EqualTo(new[] { (0, 7), (0, 8), (1, 7), (1, 8) }));
            Commit(state, .125f); state.Candidates.Add(9); Evaluate(state, .125f);
            Assert.That(state.Claims.Count, Is.EqualTo(4));
        }
        [Test] public void ExplicitCommitUsesTheSuppliedPostCallbackTimeRatherThanTheLastSweepObservation()
        {
            var state = Create(Hit(0, .01f)); Evaluate(state, .125f);
            state.Elapsed = 1; state.Candidates.Add(8); Evaluate(state, .125f);
            Assert.That(state.Claims, Is.EqualTo(new[] { (0, 7), (0, 8) }));
            Commit(state, .125f); state.Candidates.Add(9); Evaluate(state, .125f);
            Assert.That(state.Claims.Count, Is.EqualTo(2));
        }
        [Test] public void ReentrantEvaluationUsesStableClaimsAndNestedCommitImmediatelyAffectsTheEnteredOuterPass()
        {
            var state = Create(Hit(0, .01f), Hit(.125f, .01f)); bool nested = false;
            state.Receiving = (_, __) =>
            {
                if (nested) return; nested = true; state.Elapsed = .5f; Evaluate(state, .125f); Commit(state, .125f);
            };
            Evaluate(state, .25f);
            Assert.That(state.Claims, Is.EqualTo(new[] { (0, 7), (1, 7) }));
            Assert.That(state.Accepted, Is.EqualTo(new[] { (1, 7), (0, 7) }), "Already-entered Receive continues after nested hit processing.");
            Assert.That(state.Dispatches, Is.EqualTo(new[] { "0:Actor", "0:Actor", "1:Actor" }), "Do not cache the previous-evaluation time for the whole pass.");
        }
        [Test] public void AuthoredHitIndexesAndSignedTargetIdsRemainIndependentLedgerKeys()
        {
            var state = Create(Hit(), Hit()); state.Candidates.Clear(); state.Candidates.Add(int.MinValue); state.Candidates.Add(int.MaxValue);
            Evaluate(state, .125f); Evaluate(state, .125f);
            Assert.That(state.Claims, Is.EqualTo(new[] { (0, int.MinValue), (0, int.MaxValue), (1, int.MinValue), (1, int.MaxValue) }));
        }
    }
}
