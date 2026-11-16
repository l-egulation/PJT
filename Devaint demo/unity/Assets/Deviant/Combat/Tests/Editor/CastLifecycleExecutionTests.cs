using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using Deviant.Definitions;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class CastLifecycleExecutionTests
    {
        sealed class State
        {
            public CastLifecycleExecution Lifecycle;
            public SkillExecutionDefinition Definition;
            public ChargeExecution Charge;
            public float Now, StartedAt, SmokeEnd, RecastRemaining, RecastDeadline, WaveEnd;
            public float WaveObservedLast, WaveObservedElapsed, WaveObservedStep;
            public bool Held, Approaching, MissileEnded, Melee, Admitted = true, ResourcesOpen = true;
            public int Activations, Preparations, BombReleases, Teleports, Completions, Disposals;
            public readonly List<string> Events = new List<string>();
            public Action Activating, Releasing, Failing, Completing, Retaining, Disposing, Advancing, CancellingMelee, PreparingMissile;
        }
        struct Host : ICastLifecycleHost
        {
            public State State;
            public float Now => State.Now;
            public float Elapsed => State.Now - State.StartedAt;
            public bool ChargeHeld => State.Held;
            public bool Approaching => State.Approaching;
            public void AdvanceApproach(float step) { State.Events.Add("approach"); State.Advancing?.Invoke(); }
            public void Activate()
            {
                State.Events.Add("admission"); State.Activations++;
                if (!State.Admitted) { State.Lifecycle.Fail(ref this); return; }
                State.Events.Add("commit");
                if (!State.ResourcesOpen) { State.Lifecycle.Fail(ref this); return; }
                State.Lifecycle.ClaimActivation(); State.Events.Add("effects"); State.Activating?.Invoke();
            }
            public void PrepareMissile(float step) { State.Events.Add("missile"); State.Preparations++; State.PreparingMissile?.Invoke(); }
            public void ReleaseDueBombs() { State.Events.Add("bombs"); State.BombReleases++; }
            public void PrepareTeleport(float step) { State.Events.Add("teleport"); State.Teleports++; }
            public void ReleaseResources() { State.Events.Add("release"); State.Releasing?.Invoke(); State.ResourcesOpen = false; State.Charge?.Cancel(); }
            public bool CancelMeleeAction()
            {
                if (!State.Melee) return false;
                State.Events.Add("melee-cancel"); State.CancellingMelee?.Invoke(); return true;
            }
            public void Failed() { State.Events.Add("failed"); State.Failing?.Invoke(); }
            public bool MissileEnded => State.MissileEnded;
            public void Complete() { State.Events.Add("complete"); State.Completions++; State.Completing?.Invoke(); }
            public float SmokeEnd => State.SmokeEnd;
            public float RetainSkillWaveUntil(float last, float elapsed, float step)
            {
                State.Events.Add("wave"); State.WaveObservedLast = last; State.WaveObservedElapsed = elapsed; State.WaveObservedStep = step;
                State.Retaining?.Invoke(); return last > State.WaveEnd ? last : State.WaveEnd;
            }
            public float RecastRemaining => State.RecastRemaining;
            public float RecastDeadline => State.RecastDeadline;
            public void Dispose() { State.Events.Add("dispose"); State.Disposals++; State.Disposing?.Invoke(); ReleaseResources(); }
        }
        static State Create(string kind = null, float activation = .25f, float end = .5f, HitData[] hits = null, string effect = null, float started = 0)
        {
            return new State { Now = started, StartedAt = started, Lifecycle = new CastLifecycleExecution(activation, end),
                Definition = new SkillExecutionDefinition(new SkillData { kind = kind, activation = activation, end = end,
                    hits = hits ?? Array.Empty<HitData>(), effect = effect == null ? null : new EffectData { type = effect } }) };
        }
        static void Prepare(State state, float step) { var host = new Host { State = state }; state.Lifecycle.BeforeMovement(step, state.Definition, state.Charge, ref host); }
        static void AfterHits(State state, float step) { var host = new Host { State = state }; state.Lifecycle.AfterHits(step, state.Definition, state.Charge, ref host); }
        static bool Cancel(State state) { var host = new Host { State = state }; return state.Lifecycle.Cancel(ref host); }
        static void Fail(State state) { var host = new Host { State = state }; state.Lifecycle.Fail(ref host); }
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);

        [TestCase(null)] [TestCase("")] [TestCase("other")]
        public void DirectActivationIncludesTheTickEndAndClaimsOnceBeforeEffects(string kind)
        {
            var state = Create(kind); state.Now = .1875f;
            state.Activating = () => Assert.That(state.Lifecycle.Activated, Is.True);
            Prepare(state, .0625f); Prepare(state, .0625f);
            Assert.That(state.Activations, Is.EqualTo(1)); Assert.That(state.Completions, Is.Zero);
            Assert.That(state.Events, Is.EqualTo(new[] { "admission", "commit", "effects", "teleport", "teleport" }));
        }
        [TestCase("ranged")] [TestCase("bomb")] [TestCase("missile")]
        public void IndependentReleaseKindsUseTheCurrentClockEpsilonRatherThanTheTickEnd(string kind)
        {
            var state = Create(kind); state.Now = .1875f; Prepare(state, .125f);
            Assert.That(state.Activations, Is.Zero); Assert.That(state.Teleports, Is.Zero);
            state.Now = .25f - .0000005f; Prepare(state, .0625f); Prepare(state, .0625f);
            Assert.That(state.Activations, Is.EqualTo(1)); Assert.That(state.Teleports, Is.EqualTo(2));
            Assert.That(state.BombReleases, Is.EqualTo(kind == "bomb" ? 2 : 0)); Assert.That(state.Preparations, Is.EqualTo(kind == "missile" ? 2 : 0));
        }
        [Test] public void TimeZeroAndPartialStartReadTheHostsExistingElapsedClock()
        {
            var zero = Create(activation: 0); Prepare(zero, .03125f); Prepare(zero, .03125f); Assert.That(zero.Activations, Is.EqualTo(1));
            var partial = Create(started: .0625f); partial.Now = .09375f; Prepare(partial, .21875f); Assert.That(partial.Activations, Is.EqualTo(1));
            Assert.That(partial.Lifecycle.Activation, Is.EqualTo(.25f)); Assert.That(partial.Lifecycle.MotionEnd, Is.EqualTo(.5f));
        }
        [TestCase(false)] [TestCase(true)]
        public void CancelAndCompletionSkipPreparationWhileAnApproachOnlyAdvancesItsExistingPolicy(bool complete)
        {
            var state = Create();
            if (complete) { state.Now = .5f; AfterHits(state, .0625f); } else Cancel(state);
            Prepare(state, 2); Assert.That(state.Activations, Is.Zero);
            var approach = Create(); approach.Approaching = true; approach.Advancing = () => approach.Lifecycle.SetMotionEnd(.75f);
            Prepare(approach, 1); Assert.That(approach.Events, Is.EqualTo(new[] { "approach" })); Assert.That(approach.Lifecycle.MotionEnd, Is.EqualTo(.75f));
        }
        [Test] public void ExistingChargeOwnsReleaseDamageAndShiftAndDerivesLifecycleTimesOnlyOnce()
        {
            var state = Create(activation: .25f, end: .75f, started: 4);
            state.Charge = new ChargeExecution(Guid.NewGuid(), new ChargeDefinition("charge", .25f, 1, 25, 75, .75f), 4);
            state.Now = 4.5f; state.Held = true; Prepare(state, .0625f); Assert.That(state.Charge.Release, Is.Null);
            Assert.That(state.Lifecycle.Activated, Is.False); state.Held = false; Prepare(state, .0625f);
            var release = state.Charge.Release.Value; Assert.That(release.ReleaseTime, Is.EqualTo(.5f)); Assert.That(release.Shift, Is.EqualTo(.25f));
            Assert.That(state.Lifecycle.Activation, Is.EqualTo(release.ReleaseTime)); Assert.That(state.Lifecycle.MotionEnd, Is.EqualTo(release.MotionEnd));
            state.Lifecycle.SetMotionEnd(.625f); Prepare(state, .0625f);
            Assert.That(state.Lifecycle.MotionEnd, Is.EqualTo(.625f), "A later policy override is not overwritten by the already-resolved charge.");
            Assert.That(state.Charge.Release.Value, Is.EqualTo(release)); Assert.That(state.Activations, Is.EqualTo(1));
        }
        [TestCase(false)] [TestCase(true)]
        public void UnresolvedChargeCannotCompleteButFailureCanFinishItsPointOneRecovery(bool failure)
        {
            var state = Create(end: .75f); state.Charge = new ChargeExecution(Guid.NewGuid(), new ChargeDefinition("charge", .25f, 1, 25, 75, .75f), 0);
            state.Now = 2;
            if (failure) Fail(state);
            AfterHits(state, .0625f); Assert.That(state.Completions, Is.Zero);
            if (!failure) { Assert.That(state.Lifecycle.MotionDone, Is.False); return; }
            Assert.That(state.Charge.IsActive, Is.False); state.Now = state.Lifecycle.MotionEnd; AfterHits(state, .0625f);
            Assert.That(state.Completions, Is.EqualTo(1)); Assert.That(state.Disposals, Is.EqualTo(1));
        }
        [Test] public void FailureReleasesResourcesAndChargeBeforeCancellationAndReadsTheClockAfterRelease()
        {
            var state = Create(started: 4); state.Now = 4.125f;
            state.Charge = new ChargeExecution(Guid.NewGuid(), new ChargeDefinition("charge", .25f, 1, 25, 75, .75f), 4);
            state.Releasing = () => { Assert.That(state.Lifecycle.Cancelled || state.Lifecycle.MotionDone, Is.False); Assert.That(state.Charge.IsActive, Is.True); state.Now = 4.375f; };
            state.Failing = () => { Assert.That(state.Lifecycle.Cancelled, Is.True); Assert.That(state.Charge.IsActive || state.ResourcesOpen, Is.False); };
            Fail(state); Assert.That(state.Events, Is.EqualTo(new[] { "release", "failed" }));
            Assert.That(Bits(state.Lifecycle.MotionEnd), Is.EqualTo(Bits(.375f + .1f))); Assert.That(state.Lifecycle.MotionDone, Is.False);
        }
        [TestCase(false)] [TestCase(true)]
        public void ReleaseExceptionsDoNotInventFailureCancellationCompletionOrChargeCancellation(bool failure)
        {
            var state = Create(); state.Charge = new ChargeExecution(Guid.NewGuid(), new ChargeDefinition("charge", .25f, 1, 25, 75, .75f), 0);
            var error = new InvalidOperationException("release"); state.Releasing = () => throw error;
            Assert.That(Assert.Throws<InvalidOperationException>(() => { if (failure) Fail(state); else Cancel(state); }), Is.SameAs(error));
            Assert.That(state.Lifecycle.Cancelled || state.Lifecycle.MotionDone || state.Lifecycle.Activated, Is.False);
            Assert.That(state.Charge.IsActive && state.ResourcesOpen, Is.True); Assert.That(state.Events, Is.EqualTo(new[] { "release" }));
        }
        [Test] public void MeleeCancellationKeepsTheExistingEarlyReturnWithoutGenericLifecycleWrites()
        {
            var state = Create(); state.Melee = true;
            state.CancellingMelee = () => Assert.That(state.ResourcesOpen, Is.False);
            Assert.That(Cancel(state), Is.False); Assert.That(state.Events, Is.EqualTo(new[] { "release", "melee-cancel" }));
            Assert.That(state.Lifecycle.Cancelled || state.Lifecycle.MotionDone, Is.False);
            state.Melee = false; Assert.That(Cancel(state), Is.True); Assert.That(state.Lifecycle.Cancelled && state.Lifecycle.MotionDone, Is.True);
            state.Lifecycle.ResumeMotion(); Assert.That(state.Lifecycle.Cancelled || state.Lifecycle.MotionDone, Is.False);
        }
        [Test] public void ResourceReleaseAloneDoesNotManufactureTerminalClaimsOrEraseActivation()
        {
            var state = Create(activation: 0); Prepare(state, .0625f); var host = new Host { State = state }; host.ReleaseResources();
            Assert.That(state.Lifecycle.Activated, Is.True); Assert.That(state.Lifecycle.Cancelled || state.Lifecycle.MotionDone, Is.False);
            state.Now = .5f; AfterHits(state, .0625f); Assert.That(state.Completions, Is.EqualTo(1));
        }
        [Test] public void ActivationCancellationSkipsLaterPreparationButDoesNotUndoClaimedEffects()
        {
            var state = Create("bomb", activation: 0); state.Activating = () => Cancel(state); Prepare(state, .0625f);
            Assert.That(state.Lifecycle.Activated && state.Lifecycle.Cancelled && state.Lifecycle.MotionDone, Is.True);
            Assert.That(state.BombReleases + state.Teleports, Is.Zero); Assert.That(state.Events, Is.EqualTo(new[] { "admission", "commit", "effects", "release" }));
        }
        [Test] public void EnteredMissilePreparationStillReachesTeleportAfterItsCallbackCancels()
        {
            var state = Create("missile", activation: 0); state.PreparingMissile = () => Cancel(state); Prepare(state, .0625f);
            Assert.That(state.Events, Is.EqualTo(new[] { "admission", "commit", "effects", "missile", "release", "teleport" }));
        }
        [Test] public void ThrowingActivationEffectsLeaveTheClaimAndDoNotReplayActivation()
        {
            var state = Create(activation: 0); state.Activating = () => throw new InvalidOperationException("effect");
            Assert.Throws<InvalidOperationException>(() => Prepare(state, .0625f)); Assert.That(state.Lifecycle.Activated, Is.True);
            state.Activating = () => Assert.Fail("Already claimed activation must not run twice."); Prepare(state, .0625f);
            Assert.That(state.Activations, Is.EqualTo(1)); Assert.That(state.Teleports, Is.EqualTo(1));
        }
        [TestCase("ranged")] [TestCase("bomb")] [TestCase("missile")]
        public void CompletionRequiresActualReleaseAndMissileEndButCancellationDoesNotNotify(string kind)
        {
            var state = Create(kind, activation: .25f, end: .25f); state.Now = .1875f; AfterHits(state, .125f);
            Assert.That(state.Completions, Is.Zero); state.Lifecycle.ClaimActivation(); AfterHits(state, .125f);
            Assert.That(state.Completions, Is.EqualTo(kind == "missile" ? 0 : 1));
            if (kind == "missile") { state.MissileEnded = true; AfterHits(state, .125f); Assert.That(state.Completions, Is.EqualTo(1)); }
            var cancelled = Create(kind); Cancel(cancelled); cancelled.Now = 1; AfterHits(cancelled, .0625f);
            Assert.That(cancelled.Completions, Is.Zero); Assert.That(cancelled.Disposals, Is.EqualTo(1));
        }
        [Test] public void CompletionObservesTheCurrentClockAndItsCallbackCanMoveTheSeparateDisposalBoundary()
        {
            var state = Create(end: .5f); state.Now = .5f;
            state.Completing = () => { Assert.That(state.Lifecycle.MotionDone, Is.True); state.Now = .125f; state.Lifecycle.SetMotionEnd(.75f); };
            AfterHits(state, .0625f); Assert.That(state.Completions, Is.EqualTo(1)); Assert.That(state.Disposals, Is.Zero);
            state.Now = .6875f; AfterHits(state, .0625f); Assert.That(state.Completions, Is.EqualTo(1)); Assert.That(state.Disposals, Is.EqualTo(1));
        }
        [Test] public void CompletionKeepsTheDirectFloatAdditionInsteadOfAStoredRoundedHitObservation()
        {
            var state = Create(end: .36f); float now = 0;
            for (int i = 0; i < 17; i++) { state.Now = now; AfterHits(state, .02f); now += .02f; }
            float rounded = now + .02f; Assert.That(rounded, Is.GreaterThanOrEqualTo(.36f)); Assert.That((double)now + .02f, Is.LessThan((double).36f));
            state.Now = now; AfterHits(state, .02f); Assert.That(state.Completions, Is.Zero);
            state.Now += .02f; AfterHits(state, .02f); Assert.That(state.Completions, Is.EqualTo(1));
        }
        [Test] public void CompletionReentryKeepsOneNotificationAndBothAlreadyEnteredDisposals()
        {
            var state = Create(); state.Now = .5f; state.Completing = () => { Cancel(state); AfterHits(state, .0625f); };
            AfterHits(state, .0625f); Assert.That(state.Completions, Is.EqualTo(1)); Assert.That(state.Disposals, Is.EqualTo(2));
        }
        [Test] public void ThrowingCompletionPreservesDoneAndDoesNotRepeatItsCallback()
        {
            var state = Create(); state.Now = .5f; state.Completing = () => throw new InvalidOperationException("complete");
            Assert.Throws<InvalidOperationException>(() => AfterHits(state, .0625f)); Assert.That(state.Lifecycle.MotionDone, Is.True); Assert.That(state.Disposals, Is.Zero);
            state.Completing = () => Assert.Fail("Completion has already been claimed."); AfterHits(state, .0625f);
            Assert.That(state.Completions, Is.EqualTo(1)); Assert.That(state.Disposals, Is.EqualTo(1));
        }
        [TestCase("actor", .5f)] [TestCase("teleport_target", 1.25f)] [TestCase("saved_primary_world_pose", 1.25f)]
        [TestCase("projectile", 1.25f)] [TestCase("ranged_projectile", 1.25f)] [TestCase("marked_target", 1.25f)]
        public void OnlyActorAnchorsAreExcludedFromTheExistingTailDeadline(string anchor, float expected)
        {
            var state = Create(hits: new[] { new HitData { anchor = anchor, time = 1, active = .25f } }); state.Lifecycle.ClaimActivation(); state.Now = .5f;
            AfterHits(state, .0625f); Assert.That(state.Completions, Is.EqualTo(1)); Assert.That(state.Disposals, Is.EqualTo(expected == .5f ? 1 : 0));
            Assert.That(state.Lifecycle.MotionEnd, Is.EqualTo(.5f));
            if (expected > .5f) { state.Now = 1.1875f; AfterHits(state, .0625f); Assert.That(state.Disposals, Is.EqualTo(1)); }
        }
        [Test] public void TailReadsChargeShiftSmokeWaveAndRecastFactsAfterTheCompletionCallback()
        {
            var state = Create(hits: new[] { new HitData { anchor = "saved_primary_world_pose", time = 1, active = .125f } }, effect: "projectile");
            state.Charge = new ChargeExecution(Guid.NewGuid(), new ChargeDefinition("charge", .25f, 1, 25, 75, .5f), 0);
            state.Now = .5f; state.Charge.TryRelease(state.Now, .0625f, false); state.Lifecycle.ClaimActivation();
            state.Completing = () => { state.SmokeEnd = 2; state.WaveEnd = 3; state.RecastRemaining = 1; state.RecastDeadline = 4; };
            AfterHits(state, .0625f); Assert.That(state.Completions, Is.EqualTo(1)); Assert.That(state.Disposals, Is.Zero);
            Assert.That(state.WaveObservedLast, Is.EqualTo(2)); Assert.That(state.WaveObservedElapsed, Is.EqualTo(.5f)); Assert.That(state.WaveObservedStep, Is.EqualTo(.0625f));
            state.Now = 3; AfterHits(state, .0625f); Assert.That(state.Disposals, Is.Zero);
            state.RecastRemaining = 0; AfterHits(state, .0625f); Assert.That(state.Disposals, Is.EqualTo(1));
        }
        [Test] public void CancelledActivatedTailsRemainIndependentButUnactivatedEffectsDoNotRetain()
        {
            foreach (bool activated in new[] { false, true })
            {
                var state = Create(hits: new[] { new HitData { anchor = "saved_primary_world_pose", time = 1, active = .25f } }, effect: "projectile");
                if (activated) state.Lifecycle.ClaimActivation(); Cancel(state); state.Now = .5f; state.SmokeEnd = 2; state.WaveEnd = 3; state.RecastRemaining = 1; state.RecastDeadline = 4;
                AfterHits(state, .0625f); Assert.That(state.Disposals, Is.EqualTo(activated ? 0 : 1)); Assert.That(state.Completions, Is.Zero);
                Assert.That(state.Events.Contains("wave"), Is.EqualTo(activated));
            }
        }
        [Test] public void TailUsesTheChargeOwnersShiftWithoutChangingItsReleaseOrMotionEnd()
        {
            var state = Create(hits: new[] { new HitData { anchor = "saved_primary_world_pose", time = .5f, active = .125f } });
            state.Charge = new ChargeExecution(Guid.NewGuid(), new ChargeDefinition("charge", .25f, 1, 25, 75, .5f), 0);
            state.Now = .75f; state.Charge.TryRelease(state.Now, .0625f, false); var release = state.Charge.Release;
            state.Lifecycle.ClaimActivation(); AfterHits(state, .0625f); Assert.That(state.Disposals, Is.Zero);
            state.Now = 1.0625f; AfterHits(state, .0625f); Assert.That(state.Disposals, Is.EqualTo(1));
            Assert.That(state.Charge.Release, Is.EqualTo(release)); Assert.That(state.Lifecycle.MotionEnd, Is.EqualTo(.5f));
        }
    }
}
