using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using Deviant.Definitions;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class BombReleaseExecutionTests
    {
        sealed class State
        {
            public BombReleaseExecution Execution;
            public IReadOnlyList<SkillHitDefinition> Hits;
            public bool Cancelled, OwnerExists = true, OwnerDead;
            public float Elapsed, ChargeShift, DamageMultiplier = 1;
            public int Direction = 1, CancelReads, ExistsReads, DeadReads, ElapsedReads, ShiftReads;
            public readonly List<float> Requested = new List<float>(), Damage = new List<float>(), Multipliers = new List<float>();
            public readonly List<int> Directions = new List<int>(), Claims = new List<int>();
            public Action<float> Spawn;
            public Action ElapsedRead, ShiftRead;
        }
        struct Host : IBombReleaseHost
        {
            public State State;
            public bool Cancelled { get { State.CancelReads++; return State.Cancelled; } }
            public bool OwnerExists { get { State.ExistsReads++; return State.OwnerExists; } }
            public bool OwnerDead { get { State.DeadReads++; return State.OwnerDead; } }
            public float Elapsed { get { State.ElapsedReads++; State.ElapsedRead?.Invoke(); return State.Elapsed; } }
            public float ChargeShift { get { State.ShiftReads++; State.ShiftRead?.Invoke(); return State.ChargeShift; } }
            public void SpawnBomb(int hitIndex, float damage)
            {
                State.Claims.Add(State.Execution.Read().NextBomb); State.Requested.Add(damage);
                State.Damage.Add(damage * State.DamageMultiplier); State.Directions.Add(State.Direction); State.Multipliers.Add(State.DamageMultiplier);
                State.Spawn?.Invoke(damage);
            }
        }
        static State Create(params float[] times)
        {
            var hits = new HitData[times.Length];
            for (int i = 0; i < times.Length; i++) hits[i] = new HitData { time = times[i], damage = 10 + i, anchor = "bomb_projectile" };
            return new State { Hits = new SkillExecutionDefinition(new SkillData { hits = hits }).Hits };
        }
        static void Release(State state)
        {
            var host = new Host { State = state }; state.Execution.ReleaseDue(state.Hits, ref host);
        }

        [Test] public void CurrentTimeReleasesZeroThenEachAuthoredBoundaryExactlyOnceWithoutATickClock()
        {
            var state = Create(0, .2f, .35f, .5f);
            Release(state); Assert.That(state.Requested, Is.EqualTo(new[] { 10f }));
            state.Elapsed = .199f; Release(state); Assert.That(state.Requested.Count, Is.EqualTo(1));
            state.Elapsed = .2f; Release(state); Assert.That(state.Requested, Is.EqualTo(new[] { 10f, 11f }));
            Release(state); Assert.That(state.Execution.Read().NextBomb, Is.EqualTo(2));
            state.Elapsed = .35f; Release(state); Assert.That(state.Requested, Is.EqualTo(new[] { 10f, 11f, 12f }));
            state.Elapsed = .5f; Release(state); Release(state);
            Assert.That(state.Requested, Is.EqualTo(new[] { 10f, 11f, 12f, 13f }));
            Assert.That(state.Claims, Is.EqualTo(new[] { 1, 2, 3, 4 }));
        }
        [Test] public void ALateFirstPassDrainsAllDueHitsInAuthoredOrderRatherThanSortingOrRescheduling()
        {
            var state = Create(.5f, .2f, .5f); state.Elapsed = .35f; Release(state);
            Assert.That(state.Requested, Is.Empty, "The cursor's first authored release gates later entries, even when a later time is earlier.");
            state.Elapsed = .5f; Release(state);
            Assert.That(state.Requested, Is.EqualTo(new[] { 10f, 11f, 12f }));
            Assert.That(state.Execution.Read().NextBomb, Is.EqualTo(3));
            var burst = Create(.2f, .35f, .5f); burst.Elapsed = 9; Release(burst);
            Assert.That(burst.Requested, Is.EqualTo(new[] { 10f, 11f, 12f }));
        }
        [Test] public void TheExistingEpsilonIncludesTheNearBoundaryButNotAnEarlierCurrentClock()
        {
            var state = Create(.25f); state.Elapsed = .249998f; Release(state);
            Assert.That(state.Requested, Is.Empty); Assert.That(state.Execution.Read().NextBomb, Is.Zero);
            state.Elapsed = .2499995f; Release(state);
            Assert.That(state.Requested, Is.EqualTo(new[] { 10f }));
        }
        [TestCase("cancel")] [TestCase("missing")] [TestCase("dead")]
        public void AdmissionShortCircuitsBeforeClockScheduleOrSpawnAndDoesNotConsumeAnything(string fact)
        {
            var state = Create(0); state.Hits = null;
            if (fact == "cancel") state.Cancelled = true;
            if (fact == "missing") state.OwnerExists = false;
            if (fact == "dead") state.OwnerDead = true;
            Release(state);
            Assert.That(state.Requested, Is.Empty); Assert.That(state.Execution.Read().NextBomb, Is.Zero);
            Assert.That(state.CancelReads, Is.EqualTo(1)); Assert.That(state.ExistsReads, Is.EqualTo(fact == "cancel" ? 0 : 1));
            Assert.That(state.DeadReads, Is.EqualTo(fact == "dead" ? 1 : 0)); Assert.That(state.ElapsedReads, Is.Zero); Assert.That(state.ShiftReads, Is.Zero);
            state.Cancelled = state.OwnerDead = false; state.OwnerExists = true; state.Hits = Create(0).Hits; Release(state);
            Assert.That(state.Requested, Is.EqualTo(new[] { 10f }), "Admission rejection does not become a second cancellation owner.");
        }
        [TestCase("cancel")] [TestCase("missing")] [TestCase("dead")]
        public void ASpawnCallbackCannotRetractAnAlreadyAdmittedPassButTheNextPassRechecksEligibility(string fact)
        {
            var state = Create(0, .125f, .25f, 1); state.Elapsed = .25f;
            state.Spawn = _ =>
            {
                if (fact == "cancel") state.Cancelled = true;
                if (fact == "missing") state.OwnerExists = false;
                if (fact == "dead") state.OwnerDead = true;
            };
            Release(state);
            Assert.That(state.Requested, Is.EqualTo(new[] { 10f, 11f, 12f })); Assert.That(state.Execution.Read().NextBomb, Is.EqualTo(3));
            Assert.That(state.CancelReads, Is.EqualTo(1)); Assert.That(state.ExistsReads, Is.EqualTo(1)); Assert.That(state.DeadReads, Is.EqualTo(1));
            state.Elapsed = 2; Release(state); Assert.That(state.Requested.Count, Is.EqualTo(3));
            state.Cancelled = state.OwnerDead = false; state.OwnerExists = true; Release(state);
            Assert.That(state.Requested, Is.EqualTo(new[] { 10f, 11f, 12f, 13f }));
        }
        [Test] public void SpawnClaimsBeforeCallbackReentrySoNestedReleaseCannotDuplicateOrOverwriteTheCursor()
        {
            var state = Create(0, .125f, .25f); state.Elapsed = .5f; bool nested = false;
            state.Spawn = _ =>
            {
                if (nested) return;
                nested = true; Assert.That(state.Execution.Read().NextBomb, Is.EqualTo(1)); Release(state);
                Assert.That(state.Execution.Read().NextBomb, Is.EqualTo(3));
            };
            Release(state);
            Assert.That(state.Requested, Is.EqualTo(new[] { 10f, 11f, 12f })); Assert.That(state.Claims, Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(state.Execution.Read().NextBomb, Is.EqualTo(3));
        }
        [Test] public void CancelledNestedAdmissionDoesNotAbortTheOuterPassAlreadyInsideTheLoop()
        {
            var state = Create(0, 0, 0); state.Spawn = _ => { state.Cancelled = true; Release(state); };
            Release(state);
            Assert.That(state.Requested, Is.EqualTo(new[] { 10f, 11f, 12f })); Assert.That(state.Execution.Read().NextBomb, Is.EqualTo(3));
            Assert.That(state.CancelReads, Is.EqualTo(4)); Assert.That(state.ExistsReads, Is.EqualTo(1));
        }
        [Test] public void SpawnExceptionPreservesItsClaimButDoesNotClaimAnUnreachedRelease()
        {
            var state = Create(0, .125f, .25f); state.Elapsed = 1; var failure = new InvalidOperationException("native spawn");
            state.Spawn = _ => throw failure;
            Assert.That(Assert.Throws<InvalidOperationException>(() => Release(state)), Is.SameAs(failure));
            Assert.That(state.Execution.Read().NextBomb, Is.EqualTo(1)); Assert.That(state.Requested, Is.EqualTo(new[] { 10f }));
            state.Spawn = null; Release(state);
            Assert.That(state.Requested, Is.EqualTo(new[] { 10f, 11f, 12f })); Assert.That(state.Claims, Is.EqualTo(new[] { 1, 2, 3 }));
        }
        [Test] public void NestedReleaseExceptionRetainsEveryClaimReachedBeforeStackUnwinding()
        {
            var state = Create(0, 0, 0); var failure = new InvalidOperationException("second spawn");
            state.Spawn = _ => { if (state.Requested.Count == 1) Release(state); else throw failure; };
            Assert.That(Assert.Throws<InvalidOperationException>(() => Release(state)), Is.SameAs(failure));
            Assert.That(state.Execution.Read().NextBomb, Is.EqualTo(2)); Assert.That(state.Requested, Is.EqualTo(new[] { 10f, 11f }));
            state.Spawn = null; Release(state); Assert.That(state.Requested, Is.EqualTo(new[] { 10f, 11f, 12f }));
        }
        [Test] public void EveryLoopConditionReadsTheCurrentClockAfterSpawnRatherThanFreezingThePassTime()
        {
            var state = Create(.25f, .5f, .75f); state.Elapsed = .25f;
            state.Spawn = _ => state.Elapsed += .25f;
            Release(state); Assert.That(state.Requested, Is.EqualTo(new[] { 10f, 11f, 12f }));
            var backwards = Create(.25f, .5f, .75f); backwards.Elapsed = 1; backwards.Spawn = _ => backwards.Elapsed = 0;
            Release(backwards); Assert.That(backwards.Requested, Is.EqualTo(new[] { 10f })); Assert.That(backwards.Execution.Read().NextBomb, Is.EqualTo(1));
            backwards.Spawn = null; backwards.Elapsed = .75f; Release(backwards);
            Assert.That(backwards.Requested, Is.EqualTo(new[] { 10f, 11f, 12f }));
        }
        [Test] public void EveryHitUsesTheLiveExistingChargeShiftAndClockIsReadBeforeThatShift()
        {
            var state = Create(.25f, .5f, .75f); state.Elapsed = .75f;
            state.Spawn = _ => state.ChargeShift = .5f; Release(state);
            Assert.That(state.Requested, Is.EqualTo(new[] { 10f }));
            state.ChargeShift = 0; state.Spawn = _ => state.ChargeShift = -.5f; Release(state);
            Assert.That(state.Requested, Is.EqualTo(new[] { 10f, 11f, 12f }));
            var evaluation = Create(.5f); evaluation.Elapsed = .25f;
            evaluation.ShiftRead = () => evaluation.Elapsed = 1;
            Release(evaluation); Assert.That(evaluation.Requested, Is.Empty, "The left-hand current elapsed value was read before the shift callback.");
            Release(evaluation); Assert.That(evaluation.Requested, Is.EqualTo(new[] { 10f }));
        }
        [Test] public void CurrentNativeDirectionAndDamageAreReadPerSpawnRatherThanCopiedForTheWholePass()
        {
            var state = Create(0, 0, 0); state.DamageMultiplier = 2;
            state.Spawn = _ =>
            {
                if (state.Requested.Count == 1) { state.Direction = -1; state.DamageMultiplier = 3; }
                else { state.Direction = 1; state.DamageMultiplier = .5f; }
            };
            Release(state);
            Assert.That(state.Requested, Is.EqualTo(new[] { 10f, 11f, 12f })); Assert.That(state.Damage, Is.EqualTo(new[] { 20f, 33f, 6f }));
            Assert.That(state.Directions, Is.EqualTo(new[] { 1, -1, 1 })); Assert.That(state.Multipliers, Is.EqualTo(new[] { 2f, 3f, .5f }));
        }
        [Test] public void InputCaptureDoesNotInventAnotherQueueAndReadSnapshotsNeverAdvanceOrMutateTheOwner()
        {
            var source = new SkillData { hits = new[] { new HitData { time = .25f, damage = 12 }, new HitData { time = .5f, damage = 30 } } };
            var definition = new SkillExecutionDefinition(source); var state = new State { Hits = definition.Hits, Elapsed = .25f }; var other = new State { Hits = definition.Hits };
            source.hits[0].time = 99; source.hits[0].damage = 999; source.hits = Array.Empty<HitData>();
            var before = state.Execution.Read(); Assert.That(state.Execution.Read().NextBomb, Is.Zero); Release(state);
            Assert.That(before.NextBomb, Is.Zero); Assert.That(state.Execution.Read().NextBomb, Is.EqualTo(1)); Assert.That(state.Requested, Is.EqualTo(new[] { 12f }));
            Release(other); Assert.That(other.Requested, Is.Empty); Assert.That(other.Execution.Read().NextBomb, Is.Zero);
            state.Elapsed = .5f; Release(state); Assert.That(state.Requested, Is.EqualTo(new[] { 12f, 30f }));
        }
        [TestCase("clock")] [TestCase("shift")]
        public void AConditionExceptionLeavesOnlyPreviouslyClaimedReleasesAndPreservesExceptionIdentity(string phase)
        {
            var state = Create(0, .25f); state.Elapsed = 1; var failure = new InvalidOperationException(phase);
            state.Spawn = _ => { if (phase == "clock") state.ElapsedRead = () => throw failure; else state.ShiftRead = () => throw failure; };
            Assert.That(Assert.Throws<InvalidOperationException>(() => Release(state)), Is.SameAs(failure));
            Assert.That(state.Requested, Is.EqualTo(new[] { 10f })); Assert.That(state.Execution.Read().NextBomb, Is.EqualTo(1));
            state.Spawn = null; state.ElapsedRead = state.ShiftRead = null; Release(state);
            Assert.That(state.Requested, Is.EqualTo(new[] { 10f, 11f }));
        }
        [Test] public void AnEmptyOrFullyClaimedScheduleDoesNotReadClockAndNaNDoesNotBecomeDue()
        {
            var empty = Create(); Release(empty); Assert.That(empty.ElapsedReads, Is.Zero); Assert.That(empty.ShiftReads, Is.Zero);
            var state = Create(.25f); state.Elapsed = float.NaN; Release(state);
            Assert.That(state.Requested, Is.Empty); Assert.That(state.Execution.Read().NextBomb, Is.Zero);
            state.Elapsed = 1; state.ChargeShift = float.NaN; Release(state); Assert.That(state.Requested, Is.Empty);
            state.ChargeShift = 0; Release(state); int reads = state.ElapsedReads;
            Release(state); Assert.That(state.Requested, Is.EqualTo(new[] { 10f })); Assert.That(state.ElapsedReads, Is.EqualTo(reads));
        }
    }
}
