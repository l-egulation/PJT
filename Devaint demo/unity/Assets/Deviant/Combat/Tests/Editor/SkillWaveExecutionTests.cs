using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using Deviant.Definitions;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class SkillWaveExecutionTests
    {
        sealed class State
        {
            public SkillWaveExecution Execution;
            public SkillEffectDefinition Effect;
            public int Direction = 1;
            public bool Blocked, Occluded, Cancelled, Removed, Visual;
            public float VisualX, VisualY, Requested;
            public int Queries, Hits;
            public readonly List<string> Events = new List<string>();
            public readonly List<SkillWaveSnapshot> Observed = new List<SkillWaveSnapshot>();
            public Action OnBlocked, OnOccluded, OnCreate, OnTravel, OnApply, OnUpdate, OnDestroy;
            public Func<float, float> Allowed;
        }
        struct Host : ISkillWaveExecutionHost
        {
            public State State;
            public int Direction => State.Direction;
            void Observe(string name) { State.Events.Add(name); State.Observed.Add(State.Execution.Read()); }
            public bool Blocked(float x, float y) { Observe("blocked"); State.OnBlocked?.Invoke(); return State.Blocked; }
            public bool Occluded(float x, float y) { Observe("occluded"); State.OnOccluded?.Invoke(); return State.Occluded; }
            public void CreateVisual(float x, float y) { Observe("create"); State.OnCreate?.Invoke(); State.Visual = true; State.VisualX = x; State.VisualY = y; }
            public float Travel(float x, float y, int direction, float requested)
            { Observe("travel"); State.Queries++; State.Requested = requested; State.OnTravel?.Invoke(); return State.Allowed == null ? requested : State.Allowed(requested); }
            public void ApplyBox(float previousX, float previousY, float x, float y) { Observe("apply"); State.Hits++; State.OnApply?.Invoke(); }
            public void UpdateVisual(float x, float y) { Observe("update"); State.OnUpdate?.Invoke(); if (State.Visual) { State.VisualX = x; State.VisualY = y; } }
            public void DestroyVisual() { Observe("destroy"); State.OnDestroy?.Invoke(); State.Visual = false; }
        }
        static Host Create(float speed = 4, float distance = 1, float spawnX = 0, float spawnY = 0, int direction = 1)
        {
            var state = new State { Direction = direction, Effect = new SkillEffectDefinition(new EffectData { type = "projectile", speed = speed, distance = distance, x = spawnX, y = spawnY }) };
            var host = new Host { State = state }; state.Execution.Activate(0, 0, state.Effect, ref host);
            state.Events.Clear(); state.Observed.Clear(); return host;
        }
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        static void Step(ref Host host, float step) => host.State.Execution.Step(step, host.State.Effect, ref host);

        [TestCase(1)] [TestCase(-1)]
        public void ActivationCommitsRootRelativePoseBeforeNativeGeometryAndCreatesOnlyAfterBothQueries(int direction)
        {
            var state = new State { Direction = direction, Effect = new SkillEffectDefinition(new EffectData { x = 1.15625f, y = 1 }) };
            var host = new Host { State = state };
            state.OnBlocked = () =>
            {
                var at = state.Execution.Read(); Assert.That(at.X, Is.EqualTo(direction == 1 ? 4.15625f : 1.84375f));
                Assert.That(at.Y, Is.EqualTo(8)); Assert.That(at.PreviousX, Is.EqualTo(at.X)); Assert.That(at.PreviousY, Is.EqualTo(at.Y)); Assert.That(state.Visual, Is.False);
            };
            state.Execution.Activate(3, 7, state.Effect, ref host);
            Assert.That(state.Events, Is.EqualTo(new[] { "blocked", "occluded", "create" }));
            Assert.That(state.Visual, Is.True); Assert.That(state.Execution.Read().Travel, Is.Zero);
        }
        [TestCase(true, true)] [TestCase(false, true)]
        public void ABlockedSpawnClaimsDeadBeforeAnyVisualAndShortCircuitsTheNativePathQuery(bool blocked, bool occluded)
        {
            var state = new State { Blocked = blocked, Occluded = occluded, Effect = new SkillEffectDefinition(new EffectData { speed = 4, distance = 8, x = 2, y = 1 }) };
            var host = new Host { State = state }; state.Execution.Activate(3, 7, state.Effect, ref host);
            Assert.That(state.Events, Is.EqualTo(blocked ? new[] { "blocked" } : new[] { "blocked", "occluded" }));
            Assert.That(state.Execution.Read().Dead, Is.True); Assert.That(state.Execution.Read().X, Is.EqualTo(5)); Assert.That(state.Visual, Is.False);
            state.Events.Clear(); Step(ref host, .5f);
            Assert.That(state.Events, Is.EqualTo(new[] { "destroy" })); Assert.That(state.Hits, Is.Zero); Assert.That(state.Queries, Is.Zero);
            Assert.That(state.Execution.RetainUntil(.5f, 20, 1), Is.EqualTo(.5f));
        }
        [TestCase("blocked")] [TestCase("occluded")] [TestCase("create")]
        public void ThrowingSpawnCallbacksRetainTheCommittedPoseWithoutInventingATerminalClaim(string boundary)
        {
            var state = new State { Effect = new SkillEffectDefinition(new EffectData { x = 2, y = 1 }) };
            Action fail = () => throw new InvalidOperationException(boundary);
            if (boundary == "blocked") state.OnBlocked = fail;
            if (boundary == "occluded") state.OnOccluded = fail;
            if (boundary == "create") state.OnCreate = fail;
            var host = new Host { State = state };
            Assert.Throws<InvalidOperationException>(() => state.Execution.Activate(3, 7, state.Effect, ref host));
            var at = state.Execution.Read(); Assert.That(at.X, Is.EqualTo(5)); Assert.That(at.Y, Is.EqualTo(8));
            Assert.That(at.PreviousX, Is.EqualTo(5)); Assert.That(at.PreviousY, Is.EqualTo(8)); Assert.That(at.Dead, Is.False); Assert.That(state.Visual, Is.False);
        }
        [Test] public void ContactAppliesTheAllowedSweepBeforeClaimingDeadThenUpdatesAndRemovesTheActualVisual()
        {
            var host = Create(distance: 8); var state = host.State; state.Allowed = requested => .375f;
            state.OnApply = () =>
            {
                var at = state.Execution.Read(); Assert.That(at.X, Is.EqualTo(.375f)); Assert.That(at.Travel, Is.EqualTo(.375f));
                Assert.That(at.PreviousX, Is.Zero); Assert.That(at.Dead, Is.False); Assert.That(state.VisualX, Is.Zero);
            };
            state.OnUpdate = () => Assert.That(state.Execution.Read().Dead, Is.True);
            state.OnDestroy = () => Assert.That(state.VisualX, Is.EqualTo(.375f));
            Step(ref host, .25f);
            Assert.That(state.Events, Is.EqualTo(new[] { "travel", "apply", "update", "destroy" })); Assert.That(state.Hits, Is.EqualTo(1)); Assert.That(state.Visual, Is.False);
        }
        [Test] public void ACoarseFinalStepStillAppliesTheWholeRemainingSweepBeforeTermination()
        {
            var host = Create(speed: 16, distance: 8, spawnX: 1.15625f, spawnY: 1); var state = host.State;
            state.OnApply = () => { Assert.That(state.Execution.Read().Dead, Is.False); Assert.That(state.Execution.Read().PreviousX, Is.EqualTo(1.15625f)); Assert.That(state.Execution.Read().X, Is.EqualTo(9.15625f)); };
            Step(ref host, .75f);
            Assert.That(state.Requested, Is.EqualTo(8)); Assert.That(state.Execution.Read().Travel, Is.EqualTo(8)); Assert.That(state.Execution.Read().Dead, Is.True);
            Assert.That(state.Hits, Is.EqualTo(1)); Assert.That(state.Events, Is.EqualTo(new[] { "travel", "apply", "update", "destroy" }));
        }
        [TestCase(.00005f, false)] [TestCase(.0002f, true)]
        public void ShortTravelToleranceKeepsASmallLossAliveButEndsOnAClearContact(float lost, bool dead)
        {
            var host = Create(distance: 8); var state = host.State; state.Allowed = requested => requested - lost;
            Step(ref host, .25f);
            Assert.That(state.Execution.Read().Dead, Is.EqualTo(dead)); Assert.That(state.Hits, Is.EqualTo(1));
            Assert.That(state.Visual, Is.EqualTo(!dead));
        }
        [TestCase(.00005f, true)] [TestCase(.0002f, false)]
        public void RangeToleranceDistinguishesClearlyInsideAndOutsideAfterApplyingTheSweep(float remaining, bool dead)
        {
            var host = Create(); var state = host.State;
            Step(ref host, (1 - remaining) / 4);
            Assert.That(state.Execution.Read().Dead, Is.EqualTo(dead)); Assert.That(state.Hits, Is.EqualTo(1));
        }
        [Test] public void NativeTravelOwnsTheAllowedDistanceAndAZeroMoveStillAppliesAnOverlap()
        {
            var host = Create(distance: 8); var state = host.State; state.Allowed = requested => .5f;
            Step(ref host, .25f); Assert.That(state.Execution.Read().Travel, Is.EqualTo(.5f)); Assert.That(state.Execution.Read().Dead, Is.True);
            var zero = Create(speed: 0, distance: 8); var zeroState = zero.State; Step(ref zero, 100);
            Assert.That(zeroState.Requested, Is.Zero); Assert.That(zeroState.Hits, Is.EqualTo(1)); Assert.That(zeroState.Execution.Read().Dead, Is.False);
            Assert.That(zeroState.Execution.RetainUntil(.5f, 2, .125f), Is.EqualTo(2.25f), "No second lifetime clock replaces the live cast age.");
        }
        [Test] public void CancellationOrRemovalDuringDamageDoesNotSkipTheRemainingNativeContinuation()
        {
            var host = Create(); var state = host.State;
            state.OnApply = () => { state.Cancelled = true; state.Removed = true; state.Visual = false; };
            Step(ref host, .25f);
            Assert.That(state.Cancelled && state.Removed, Is.True); Assert.That(state.Execution.Read().Dead, Is.True);
            Assert.That(state.Events, Is.EqualTo(new[] { "travel", "apply", "update", "destroy" }));
            Assert.That(state.Execution.RetainUntil(.5f, 2, .125f), Is.EqualTo(.5f));
        }
        [Test] public void DirectionIsReadFromTheCurrentCastEachStepAndAgainAfterTheNativeTravelCallback()
        {
            var host = Create(distance: 8); var state = host.State;
            Step(ref host, .125f); state.Direction = -1; Step(ref host, .125f);
            Assert.That(state.Execution.Read().X, Is.Zero); Assert.That(state.Execution.Read().PreviousX, Is.EqualTo(.5f)); Assert.That(state.Execution.Read().Travel, Is.EqualTo(1));
            state.OnTravel = () => state.Direction = 1; Step(ref host, .125f);
            Assert.That(state.Execution.Read().X, Is.EqualTo(.5f)); Assert.That(state.Execution.Read().PreviousX, Is.Zero);
        }
        [Test] public void AnInterruptedTravelQueryHasAdvancedOnlyThePreviousPoseNotPositionOrTravel()
        {
            var host = Create(distance: 8); var state = host.State; Step(ref host, .125f); float visual = state.VisualX;
            state.OnTravel = () => throw new InvalidOperationException("travel"); state.Events.Clear();
            Assert.Throws<InvalidOperationException>(() => Step(ref host, .25f));
            var at = state.Execution.Read(); Assert.That(at.PreviousX, Is.EqualTo(.5f)); Assert.That(at.X, Is.EqualTo(.5f)); Assert.That(at.Travel, Is.EqualTo(.5f));
            Assert.That(at.Dead, Is.False); Assert.That(state.VisualX, Is.EqualTo(visual)); Assert.That(state.Events, Is.EqualTo(new[] { "travel" }));
        }
        [Test] public void AThrowingDamageCallbackLeavesMovementCommittedButDeathAndVisualPendingAndDoesNotReplayTheSweep()
        {
            var host = Create(); var state = host.State; state.OnApply = () => throw new InvalidOperationException("damage");
            Assert.Throws<InvalidOperationException>(() => Step(ref host, .25f));
            var moved = state.Execution.Read(); Assert.That(moved.X, Is.EqualTo(1)); Assert.That(moved.Travel, Is.EqualTo(1));
            Assert.That(moved.PreviousX, Is.Zero); Assert.That(moved.Dead, Is.False); Assert.That(state.VisualX, Is.Zero);
            Assert.That(state.Events, Is.EqualTo(new[] { "travel", "apply" }));
            state.OnApply = () => { var at = state.Execution.Read(); Assert.That(at.PreviousX, Is.EqualTo(1)); Assert.That(at.X, Is.EqualTo(1)); };
            Step(ref host, 0); Assert.That(state.Requested, Is.Zero); Assert.That(state.Execution.Read().Dead, Is.True); Assert.That(state.Visual, Is.False);
        }
        [Test] public void AThrowingVisualUpdateKeepsTheTerminalClaimAndASecondCallOnlyAttemptsCleanup()
        {
            var host = Create(); var state = host.State; state.OnUpdate = () => throw new InvalidOperationException("visual");
            Assert.Throws<InvalidOperationException>(() => Step(ref host, .25f));
            Assert.That(state.Execution.Read().Dead, Is.True); Assert.That(state.Visual, Is.True); Assert.That(state.VisualX, Is.Zero);
            state.Events.Clear(); Step(ref host, .25f);
            Assert.That(state.Events, Is.EqualTo(new[] { "destroy" })); Assert.That(state.Queries, Is.EqualTo(1)); Assert.That(state.Hits, Is.EqualTo(1)); Assert.That(state.Visual, Is.False);
        }
        [Test] public void AThrowingCleanupDoesNotRetractTheMovedPoseOrTerminalClaim()
        {
            var host = Create(); var state = host.State; state.OnDestroy = () => throw new InvalidOperationException("destroy");
            Assert.Throws<InvalidOperationException>(() => Step(ref host, .25f));
            Assert.That(state.Execution.Read().Dead, Is.True); Assert.That(state.VisualX, Is.EqualTo(1)); Assert.That(state.Visual, Is.True);
            state.OnDestroy = null; Step(ref host, 1); Assert.That(state.Visual, Is.False); Assert.That(state.Hits, Is.EqualTo(1));
        }
        [Test] public void ReentryDuringDamageSeesTheMovedNonterminalOwnerAndOuterContinuationUsesTheLatestPose()
        {
            var host = Create(distance: 8); var state = host.State; bool nested = false;
            state.OnApply = () =>
            {
                if (nested) return; nested = true;
                Assert.That(state.Execution.Read().X, Is.EqualTo(1)); Assert.That(state.Execution.Read().Dead, Is.False);
                var inner = new Host { State = state }; Step(ref inner, .25f);
            };
            Step(ref host, .25f);
            Assert.That(state.Execution.Read().X, Is.EqualTo(2)); Assert.That(state.Execution.Read().PreviousX, Is.EqualTo(1)); Assert.That(state.Execution.Read().Travel, Is.EqualTo(2));
            Assert.That(state.VisualX, Is.EqualTo(2)); Assert.That(state.Hits, Is.EqualTo(2));
            Assert.That(state.Events, Is.EqualTo(new[] { "travel", "apply", "travel", "apply", "update", "update" }));
        }
        [Test] public void ReentryAtTheRangeBoundaryIsNotSilentlySuppressedBeforeTheDamageCallbackCompletes()
        {
            var host = Create(); var state = host.State; bool nested = false;
            state.OnApply = () =>
            {
                if (nested) return; nested = true;
                Assert.That(state.Execution.Read().Dead, Is.False); var inner = new Host { State = state }; Step(ref inner, 0);
            };
            Step(ref host, .25f);
            Assert.That(state.Hits, Is.EqualTo(2)); Assert.That(state.Execution.Read().PreviousX, Is.EqualTo(1)); Assert.That(state.Execution.Read().Dead, Is.True);
            Assert.That(state.Events, Is.EqualTo(new[] { "travel", "apply", "travel", "apply", "update", "destroy", "update", "destroy" }));
        }
        [Test] public void LifetimeQueryUsesTheExistingCastClockAndPreservesALaterAuthoredDeadline()
        {
            var host = Create(distance: 8); var state = host.State;
            Assert.That(state.Execution.RetainUntil(20, 1, .125f), Is.EqualTo(20));
            Assert.That(state.Execution.RetainUntil(0, 1, .125f), Is.EqualTo(1.25f));
            Assert.That(state.Execution.RetainUntil(0, 2, .125f), Is.EqualTo(2.25f));
            Assert.That(state.Execution.Read().Travel, Is.Zero, "Querying lifetime does not integrate any extra time or motion.");
            Step(ref host, 2); Assert.That(state.Execution.RetainUntil(.5f, 3, .125f), Is.EqualTo(.5f));
        }
        [Test] public void SignedZeroVerticalArithmeticRetainsTheExistingVectorOperationOrder()
        {
            var host = Create(distance: 8, direction: -1); var state = host.State;
            Step(ref host, .125f);
            Assert.That(Bits(state.Execution.Read().Y), Is.EqualTo(Bits(0f))); Assert.That(state.Execution.Read().X, Is.EqualTo(-.5f));
        }
    }
}
