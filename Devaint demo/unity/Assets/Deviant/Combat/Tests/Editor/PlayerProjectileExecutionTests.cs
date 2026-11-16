using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using Deviant.Definitions;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class PlayerProjectileExecutionTests
    {
        const float Epsilon = .000001f;
        sealed class Shot : IPlayerProjectileExecutionHost
        {
            public readonly PlayerProjectileExecution Execution;
            public float Speed = 3;
            public readonly List<string> Calls = new List<string>();
            public readonly List<float> Flights = new List<float>(), Lengths = new List<float>(), Endpoints = new List<float>(), Rotations = new List<float>();
            public Action<float> DuringFlight, DuringDistance, DuringRotation;
            public Action DuringEndpoint, DuringRemove;
            public int Removals;
            public Shot(float interval = .25f, float distance = 6)
                => Execution = new PlayerProjectileExecution(new RangedProjectileDefinition(new RangedData { distance = distance, hitInterval = interval }));
            public void Step(float dt, bool alive = true) => Execution.Step(dt, alive, Speed, this);
            public bool Claim(int actor, float at = -1) => Execution.TryClaimActor(actor, Speed, at);
            public void Release() => Execution.Release(this);
            public void SweepByTime(float duration) { Calls.Add("flight"); Flights.Add(duration); DuringFlight?.Invoke(duration); }
            public void SweepByDistance(float distance) { Calls.Add("distance"); Lengths.Add(distance); DuringDistance?.Invoke(distance); }
            public void SweepCurrent() { Calls.Add("endpoint"); Endpoints.Add(Execution.Age); DuringEndpoint?.Invoke(); }
            public void Rotate(float duration) { Calls.Add("rotate"); Rotations.Add(duration); DuringRotation?.Invoke(duration); }
            public void Remove() { Calls.Add("remove"); Removals++; DuringRemove?.Invoke(); }
        }
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        static float Before(float positive) => BitConverter.Int32BitsToSingle(Bits(positive) - 1);
        static float After(float positive) => BitConverter.Int32BitsToSingle(Bits(positive) + 1);

        [Test] public void SingleHitClaimsArePerActorAndPerExecutionEvenWhenTheReceiverRejects()
        {
            var first = new Shot(0); var second = new Shot(0);
            Assert.That(first.Claim(1), Is.True); // Receive may reject; the claim is already committed.
            Assert.That(first.Claim(1), Is.False); Assert.That(first.Claim(2), Is.True);
            Assert.That(second.Claim(1), Is.True); Assert.That(second.Claim(1), Is.False);
            first.Step(.1f); Assert.That(first.Claim(1), Is.False);
            Assert.That(first.Execution.Age, Is.Zero); Assert.That(second.Execution.Traveled, Is.Zero);
        }
        [Test] public void RecurringClaimsUseTheContactAgeAndDoNotCatchUpMissedIntervals()
        {
            var shot = new Shot(); Assert.That(shot.Claim(1), Is.True);
            Assert.That(shot.Claim(1, .24f), Is.False); Assert.That(shot.Claim(1, .25f), Is.True);
            Assert.That(shot.Claim(1, 1.2f), Is.True); Assert.That(shot.Claim(1, 1.2f), Is.False);
            Assert.That(shot.Claim(1, 1.44f), Is.False); Assert.That(shot.Claim(1, 1.45f), Is.True);
            Assert.That(shot.Execution.Age, Is.Zero); Assert.That(shot.Execution.Traveled, Is.Zero);
        }
        [Test] public void LateOverlapContactStartsItsOwnIntervalRatherThanTheSpawnInterval()
        {
            var shot = new Shot(); shot.Step(.1f);
            Assert.That(shot.Claim(7), Is.True); Assert.That(shot.Claim(7), Is.False);
            shot.Step(.24f); Assert.That(shot.Claim(7), Is.False);
            shot.Step(.01f); Assert.That(shot.Claim(7), Is.True);
            Assert.That(shot.Claim(8), Is.True, "Another actor starts its own contact schedule.");
        }
        [Test] public void NegativeContactAgeMeansCurrentAgeAndCastAgeKeepsRawDistanceFloatOrder()
        {
            var shot = new Shot(); shot.Step(.03f);
            float rawDistance = .037f, speed = 7.3f;
            Assert.That(Bits(shot.Execution.ContactAge(rawDistance, speed)), Is.EqualTo(Bits(shot.Execution.Age + rawDistance / speed)));
            Assert.That(Bits(shot.Execution.ContactAge(rawDistance, 0)), Is.EqualTo(Bits(shot.Execution.Age + rawDistance / Epsilon)));
            Assert.That(shot.Claim(1, -10), Is.True);
            Assert.That(shot.Claim(1, shot.Execution.Age + .24f), Is.False);
            Assert.That(shot.Claim(1, shot.Execution.Age + .25f), Is.True);
        }
        [Test] public void AdjacentContactFloatsKeepIntervalAndLifetimeTolerancesWithoutConsumingRejectedClaims()
        {
            float intervalEdge = .25f - Epsilon;
            var interval = new Shot(); Assert.That(interval.Claim(1, 0), Is.True);
            Assert.That(interval.Claim(1, Before(intervalEdge)), Is.False);
            Assert.That(interval.Claim(1, After(intervalEdge)), Is.True);
            float end = 6f / 3f - .00001f;
            var lifetime = new Shot(); Assert.That(lifetime.Claim(4, After(end)), Is.False);
            Assert.That(lifetime.Claim(4, Before(end)), Is.True, "Excluded lifetime contacts do not start an actor interval.");
        }
        [Test] public void ContactLifetimeUsesTheCurrentActualSpeedNotAStoredDefinitionSpeed()
        {
            var shot = new Shot(); shot.Speed = 6;
            Assert.That(shot.Claim(1, 1), Is.False);
            shot.Speed = 3; Assert.That(shot.Claim(1, 1), Is.True);
            shot.Speed = 0; Assert.That(shot.Claim(2, 5), Is.True);
        }
        [Test] public void RecurringMicrostepsKeepSweepThenAccountingThenRotationThenEndpointOrder()
        {
            var shot = new Shot(); float ageAtSweep = -1, traveledAtSweep = -1, ageAtRotation = -1;
            shot.DuringFlight = _ => { ageAtSweep = shot.Execution.Age; traveledAtSweep = shot.Execution.Traveled; };
            shot.DuringRotation = _ => ageAtRotation = shot.Execution.Age;
            shot.Step(.02f);
            Assert.That(shot.Calls, Is.EqualTo(new[] { "flight", "rotate", "endpoint" }));
            Assert.That(ageAtSweep, Is.Zero); Assert.That(traveledAtSweep, Is.Zero);
            Assert.That(Bits(ageAtRotation), Is.EqualTo(Bits(.02f)));
            Assert.That(Bits(shot.Execution.Traveled), Is.EqualTo(Bits(3f * .02f)));
            Assert.That(Bits(shot.Endpoints[0]), Is.EqualTo(Bits(.02f)));
        }
        [Test] public void CoarseTicksSplitAtEachActorsFutureDeadlineWithoutCopyingOrResettingTheLedger()
        {
            var shot = new Shot(.025f); shot.Claim(1); shot.Claim(2, .005f); shot.Step(.05f);
            Assert.That(shot.Flights.Count, Is.GreaterThanOrEqualTo(4));
            Assert.That(Bits(shot.Endpoints[0]), Is.EqualTo(Bits(.02f)));
            Assert.That(Bits(shot.Endpoints[1]), Is.EqualTo(Bits(.025f)));
            Assert.That(Bits(shot.Endpoints[2]), Is.EqualTo(Bits(.005f + .025f)));
            Assert.That(shot.Claim(1, .025f), Is.True); Assert.That(shot.Claim(2, .025f), Is.False);
            Assert.That(shot.Claim(2, .03f), Is.True);
        }
        [Test] public void EndpointOverlapIsCheckedAfterAdvancingAgeSoADeadlineDoesNotDriftToNextTick()
        {
            var shot = new Shot(); int hits = 0;
            void Contact() { if (shot.Claim(1)) hits++; }
            Contact(); shot.DuringFlight = _ => Contact(); shot.DuringEndpoint = Contact;
            shot.Step(.24f); Assert.That(hits, Is.EqualTo(1));
            shot.Step(.01f); Assert.That(hits, Is.EqualTo(2));
            Assert.That(shot.Claim(1), Is.False);
        }
        [Test] public void ContinuousContactHasEightHitsAndNeverAnExpiryHitAtTwoSeconds()
        {
            var shot = new Shot(); var hits = new List<float>();
            void Contact() { if (shot.Claim(1)) hits.Add(shot.Execution.Age); }
            Contact(); shot.DuringFlight = _ => Contact(); shot.DuringEndpoint = Contact;
            shot.Step(2); shot.Step(.02f);
            Assert.That(hits.Count, Is.EqualTo(8));
            for (int i = 0; i < hits.Count; i++) Assert.That(hits[i], Is.EqualTo(i * .25f).Within(.000002f));
            Assert.That(shot.Execution.Traveled, Is.EqualTo(6).Within(.000001f));
            Assert.That(shot.Execution.Removed, Is.True); Assert.That(shot.Removals, Is.EqualTo(1));
        }
        [Test] public void DeadlinesAndIntervalsStayIndependentAcrossSeparateProjectiles()
        {
            var first = new Shot(); var second = new Shot(); first.Claim(1); second.Claim(1);
            first.Step(.25f); Assert.That(first.Claim(1), Is.True); Assert.That(second.Claim(1), Is.False);
            first.Step(2); Assert.That(first.Execution.Removed, Is.True);
            Assert.That(second.Execution.Age, Is.Zero); Assert.That(second.Execution.Traveled, Is.Zero);
            second.Step(.25f); Assert.That(second.Claim(1), Is.True); Assert.That(second.Execution.Removed, Is.False);
        }
        [TestCase(-1f)] [TestCase(0f)] [TestCase(.0000005f)] [TestCase(Epsilon)]
        public void NonadvancingRecurringCallsDoNotSweepRotateOrExpire(float dt)
        {
            var shot = new Shot(distance: 0); shot.Step(dt);
            Assert.That(shot.Calls, Is.Empty); Assert.That(shot.Execution.Age, Is.Zero);
            Assert.That(shot.Execution.Traveled, Is.Zero); Assert.That(shot.Execution.Removed, Is.False);
            shot.Step(.02f); Assert.That(shot.Execution.Removed, Is.True); Assert.That(shot.Flights, Is.Empty);
        }
        [TestCase(0f)] [TestCase(-1f)] [TestCase(.000001f)]
        public void RecurringZeroSpeedAndMissingOwnerRemoveWithoutAnyNativeSweep(float speed)
        {
            var shot = new Shot(); shot.Speed = speed; shot.Step(0);
            Assert.That(shot.Calls, Is.EqualTo(new[] { "remove" })); Assert.That(shot.Execution.Age, Is.Zero);
            var dead = new Shot(); dead.Step(1, false); Assert.That(dead.Calls, Is.EqualTo(new[] { "remove" }));
        }
        [Test] public void TinyRemaindersAreDiscardedAndRangeIsNotPreemptivelyExpired()
        {
            var shot = new Shot(distance: .060002f); shot.Step(.02f + Epsilon * .5f);
            Assert.That(shot.Flights, Is.EqualTo(new[] { .02f })); Assert.That(shot.Execution.Removed, Is.False);
            Assert.That(shot.Endpoints.Count, Is.EqualTo(1));
            shot.Step(.02f); Assert.That(shot.Execution.Removed, Is.True);
            Assert.That(shot.Flights.Count, Is.EqualTo(1), "The remaining distance cannot produce a greater-than-epsilon time slice.");
            Assert.That(Bits(shot.Execution.Traveled), Is.EqualTo(Bits(3f * .02f)));
        }
        [Test] public void RecurringStepCapturesTravelSpeedOnceButContactsReadTheChangedCurrentSpeed()
        {
            var shot = new Shot(); shot.DuringFlight = _ => shot.Speed = 6;
            shot.Step(.04f);
            Assert.That(Bits(shot.Execution.Traveled), Is.EqualTo(Bits(3f * .02f + 3f * .02f)));
            Assert.That(shot.Claim(1, 1), Is.False);
            Assert.That(Bits(shot.Execution.ContactAge(.06f, shot.Speed)), Is.EqualTo(Bits(shot.Execution.Age + .06f / 6f)));
            shot.Step(.02f); Assert.That(Bits(shot.Execution.Traveled), Is.EqualTo(Bits(.12f + 6f * .02f)));
        }
        [Test] public void NormalFlightUsesOneClampedDistanceSweepAndNeverAdvancesRecurringAge()
        {
            var shot = new Shot(0, 1); shot.Speed = 10; shot.Step(.04f);
            Assert.That(shot.Lengths, Is.EqualTo(new[] { 10f * .04f })); Assert.That(shot.Execution.Age, Is.Zero);
            Assert.That(shot.Endpoints, Is.Empty); Assert.That(shot.Rotations, Is.Empty);
            float before = shot.Execution.Traveled; shot.Step(.2f);
            Assert.That(Bits(shot.Lengths[1]), Is.EqualTo(Bits(1f - before)));
            Assert.That(Bits(shot.Execution.Traveled), Is.EqualTo(Bits(before + (1f - before))));
            Assert.That(shot.Calls, Is.EqualTo(new[] { "distance", "distance", "remove" }));
        }
        [TestCase(0f)] [TestCase(-.1f)]
        public void NormalNonpositiveDistanceRemovesWithoutSweeping(float dt)
        {
            var shot = new Shot(0); shot.Step(dt);
            Assert.That(shot.Calls, Is.EqualTo(new[] { "remove" })); Assert.That(shot.Execution.Traveled, Is.Zero);
        }
        [Test] public void NativeContactRemovalStillFinishesTheEnteredTravelAgeAndRotationButNotEndpoint()
        {
            var recurring = new Shot(); recurring.DuringFlight = _ => recurring.Release(); recurring.Step(.2f);
            Assert.That(recurring.Calls, Is.EqualTo(new[] { "flight", "remove", "rotate" }));
            Assert.That(recurring.Execution.Age, Is.EqualTo(.02f)); Assert.That(recurring.Execution.Traveled, Is.EqualTo(3f * .02f));
            var normal = new Shot(0); normal.DuringDistance = _ => normal.Release(); normal.Step(.1f);
            Assert.That(normal.Calls, Is.EqualTo(new[] { "distance", "remove" }));
            Assert.That(normal.Execution.Traveled, Is.EqualTo(3f * .1f)); Assert.That(normal.Execution.Age, Is.Zero);
        }
        [Test] public void ReleaseClaimsTerminalBeforeNativeReentryAndNeverRepeatsRemoval()
        {
            var shot = new Shot(); bool observed = false;
            shot.DuringRemove = () => { observed = shot.Execution.Removed; shot.Step(1); shot.Release(); };
            shot.Release(); shot.Release(); shot.Step(2);
            Assert.That(observed, Is.True); Assert.That(shot.Calls, Is.EqualTo(new[] { "remove" }));
            Assert.That(shot.Execution.Age, Is.Zero); Assert.That(shot.Execution.Traveled, Is.Zero);
        }
        [Test] public void EnteredSweepClaimsRemainAvailableAfterReleaseWithoutReopeningFutureExecution()
        {
            var shot = new Shot(0); shot.DuringDistance = _ => {
                Assert.That(shot.Claim(1), Is.True); shot.Release();
                Assert.That(shot.Claim(1), Is.False); Assert.That(shot.Claim(2), Is.True);
            };
            shot.Step(.02f); shot.Step(.1f);
            Assert.That(shot.Lengths.Count, Is.EqualTo(1)); Assert.That(shot.Removals, Is.EqualTo(1));
            Assert.That(shot.Execution.Traveled, Is.EqualTo(3f * .02f));
        }
        [Test] public void NativeReentryUsesTheSameOwnerAndOuterAccountingFollowsTheNestedStep()
        {
            var shot = new Shot(); bool nested = false;
            shot.DuringFlight = _ => { if (nested) return; nested = true; shot.Step(.01f); };
            shot.Step(.02f);
            Assert.That(shot.Flights, Is.EqualTo(new[] { .02f, .01f }));
            Assert.That(Bits(shot.Execution.Age), Is.EqualTo(Bits(.01f + .02f)));
            Assert.That(Bits(shot.Execution.Traveled), Is.EqualTo(Bits(3f * .01f + 3f * .02f)));
            Assert.That(shot.Endpoints, Is.EqualTo(new[] { .01f, .01f + .02f }));
        }
        [Test] public void ReceiveExceptionKeepsTheActorClaimButDoesNotAdvanceTheUnfinishedSweep()
        {
            var shot = new Shot(); shot.DuringFlight = _ => {
                Assert.That(shot.Claim(1), Is.True); throw new InvalidOperationException("receive");
            };
            Assert.Throws<InvalidOperationException>(() => shot.Step(.02f));
            Assert.That(shot.Claim(1), Is.False); Assert.That(shot.Execution.Age, Is.Zero);
            Assert.That(shot.Execution.Traveled, Is.Zero); Assert.That(shot.Execution.Removed, Is.False);
            Assert.That(shot.Rotations, Is.Empty); Assert.That(shot.Endpoints, Is.Empty);
            shot.DuringFlight = null; shot.Step(.25f); Assert.That(shot.Claim(1), Is.True);
        }
        [Test] public void RotationAndEndpointExceptionsKeepTheAlreadyAdvancedAgeAndTravel()
        {
            var rotate = new Shot(); rotate.DuringRotation = _ => throw new InvalidOperationException("rotate");
            Assert.Throws<InvalidOperationException>(() => rotate.Step(.02f));
            Assert.That(rotate.Execution.Age, Is.EqualTo(.02f)); Assert.That(rotate.Execution.Traveled, Is.EqualTo(3f * .02f));
            Assert.That(rotate.Endpoints, Is.Empty); Assert.That(rotate.Execution.Removed, Is.False);
            var endpoint = new Shot(); endpoint.DuringEndpoint = () => {
                Assert.That(endpoint.Claim(4), Is.True); throw new InvalidOperationException("endpoint");
            };
            Assert.Throws<InvalidOperationException>(() => endpoint.Step(.02f));
            Assert.That(endpoint.Execution.Age, Is.EqualTo(.02f)); Assert.That(endpoint.Claim(4), Is.False);
            Assert.That(endpoint.Execution.Traveled, Is.EqualTo(3f * .02f)); Assert.That(endpoint.Execution.Removed, Is.False);
        }
        [Test] public void NormalReceiveExceptionKeepsTheSingleHitClaimWithoutAdvancingTravel()
        {
            var shot = new Shot(0); shot.DuringDistance = _ => {
                Assert.That(shot.Claim(3), Is.True); throw new InvalidOperationException("receive");
            };
            Assert.Throws<InvalidOperationException>(() => shot.Step(.1f));
            Assert.That(shot.Claim(3), Is.False); Assert.That(shot.Claim(4), Is.True);
            Assert.That(shot.Execution.Traveled, Is.Zero); Assert.That(shot.Execution.Age, Is.Zero);
            Assert.That(shot.Execution.Removed, Is.False); Assert.That(shot.Removals, Is.Zero);
            shot.DuringDistance = null; shot.Step(.1f);
            Assert.That(shot.Execution.Traveled, Is.EqualTo(3f * .1f)); Assert.That(shot.Claim(3), Is.False);
        }
        [Test] public void AThrowAfterNativeRemovalKeepsTheTerminalClaimWithoutCompletingOuterAccounting()
        {
            var shot = new Shot(); shot.DuringFlight = _ => {
                shot.Release(); throw new InvalidOperationException("native sweep");
            };
            Assert.Throws<InvalidOperationException>(() => shot.Step(.02f));
            Assert.That(shot.Execution.Removed, Is.True); Assert.That(shot.Execution.Age, Is.Zero);
            Assert.That(shot.Execution.Traveled, Is.Zero); Assert.That(shot.Rotations, Is.Empty);
            shot.Step(1); shot.Release(); Assert.That(shot.Removals, Is.EqualTo(1));
            Assert.That(shot.Flights.Count, Is.EqualTo(1));
        }
        [Test] public void RangeRemovalOccursAfterRotationWithoutAnExtraEndpointContact()
        {
            var shot = new Shot(distance: .06f); shot.Step(.02f);
            Assert.That(shot.Calls, Is.EqualTo(new[] { "flight", "rotate", "remove" }));
            Assert.That(shot.Execution.Age, Is.EqualTo(.02f)); Assert.That(shot.Execution.Traveled, Is.EqualTo(.06f));
            Assert.That(shot.Execution.Removed, Is.True); Assert.That(shot.Endpoints, Is.Empty);
        }
        [Test] public void NegativeConfiguredIntervalUsesTheSameSingleHitLedgerAndNormalTravelBranch()
        {
            var shot = new Shot(-1); Assert.That(shot.Claim(1), Is.True); Assert.That(shot.Claim(1, 10), Is.False);
            shot.Step(.02f); Assert.That(shot.Calls, Is.EqualTo(new[] { "distance" }));
            Assert.That(shot.Execution.Age, Is.Zero); Assert.That(shot.Execution.Traveled, Is.EqualTo(3f * .02f));
        }
        [Test] public void RemoveExceptionLeavesTerminationClaimedAndFutureCallsClosed()
        {
            var shot = new Shot(); shot.DuringRemove = () => throw new InvalidOperationException("remove");
            Assert.Throws<InvalidOperationException>(() => shot.Release());
            shot.Step(1); shot.Release(); Assert.That(shot.Removals, Is.EqualTo(1));
            Assert.That(shot.Execution.Removed, Is.True); Assert.That(shot.Execution.Age, Is.Zero);
        }
    }
}
