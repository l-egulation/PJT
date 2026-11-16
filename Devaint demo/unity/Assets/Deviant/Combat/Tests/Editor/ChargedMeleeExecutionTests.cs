using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using Deviant.Definitions;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class ChargedMeleeExecutionTests
    {
        sealed class Host : IChargedMeleeExecutionHost
        {
            public float Now { get; set; }
            public bool HasTarget = true, Admit = true, ResourcesOpen = true;
            public int Available = 1, Completions, Disposals;
            public readonly List<string> Events = new List<string>();
            public readonly List<int> Present = new List<int>();
            public readonly Dictionary<int, float> Health = new Dictionary<int, float>();
            public Action Selecting, Committing, Activating, Releasing, Failing, Sweeping, Completing;
            public Action<int> Receiving;
            public ChargedMeleeExecution Execution;
            public ChargeExecution Charge;
            public bool SelectTarget() { Events.Add("select"); Selecting?.Invoke(); return HasTarget; }
            public bool CommitResources()
            {
                Events.Add("resource"); Committing?.Invoke();
                if (!Admit || !ResourcesOpen || Available == 0) return false;
                Available--; return true;
            }
            public void Activated() { Events.Add("activated"); Activating?.Invoke(); }
            public void ReleaseResources() { Events.Add("release"); Releasing?.Invoke(); ResourcesOpen = false; }
            public void Failed() { Events.Add("failed"); Failing?.Invoke(); }
            public void Sweep()
            {
                Events.Add("sweep"); Sweeping?.Invoke();
                foreach (int target in Present)
                {
                    if (!Execution.TryClaimTarget(target)) continue;
                    Receiving?.Invoke(target);
                    Health[target] -= Charge.Release.Value.Damage;
                }
            }
            public void Complete() { Events.Add("complete"); Completions++; Completing?.Invoke(); }
            public void Dispose() { Events.Add("dispose"); Disposals++; Execution.ReleaseResources(); }
            public void Add(int target) { Present.Add(target); Health.Add(target, 100); }
        }
        static Host Create(float minimum = .25f, float maximum = 1, float end = .75f, float active = .125f, float started = 0)
        {
            var host = new Host { Now = started };
            host.Charge = new ChargeExecution(Guid.NewGuid(), new ChargeDefinition("charged", minimum, maximum, 25, 75, end), started);
            host.Execution = new ChargedMeleeExecution(host.Charge, active, host);
            return host;
        }
        static void Tick(Host host, float now, float step = .0625f, bool held = false)
        {
            host.Now = now; host.Execution.BeforeMovement(step, held); host.Execution.Step(step);
        }
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);

        [Test] public void ActivationCommitsBeforeMovementButDamageWaitsForTheNativeSweep()
        {
            var host = Create(); host.Add(10); host.Now = .1875f;
            host.Execution.BeforeMovement(.0625f, false);
            Assert.That(host.Available, Is.Zero); Assert.That(host.Health[10], Is.EqualTo(100));
            Assert.That(host.Events, Is.EqualTo(new[] { "select", "resource", "activated" }));
            host.Execution.Step(.0625f);
            Assert.That(host.Health[10], Is.EqualTo(75)); Assert.That(host.Completions, Is.Zero);
            Tick(host, .25f); Tick(host, .3125f); Tick(host, .6875f);
            Assert.That(host.Completions, Is.EqualTo(1)); Assert.That(host.Disposals, Is.EqualTo(1));
            Assert.That(host.Events.IndexOf("complete"), Is.GreaterThan(host.Events.LastIndexOf("sweep")));
            Assert.That(host.ResourcesOpen || host.Charge.IsActive, Is.False);
            Tick(host, .75f); Assert.That(host.Completions, Is.EqualTo(1)); Assert.That(host.Health[10], Is.EqualTo(75));
        }
        [TestCase(.2f, .8f, .53f, .18f, .02f, false, 25)]
        [TestCase(.2f, .8f, .53f, .5f, .02f, false, 50)]
        [TestCase(.2f, .8f, .53f, .82f, .02f, true, 75)]
        [TestCase(.25f, 1f, .7f, .24f, .02f, false, 25)]
        [TestCase(.25f, 1f, .7f, .625f, .02f, false, 50)]
        [TestCase(.25f, 1f, .7f, 1.02f, .02f, true, 75)]
        public void ExistingChargeReleaseDrivesDamageAndRecoveryWithoutAnotherReleaseState(float minimum, float maximum, float end, float now, float step, bool held, float expectedDamage)
        {
            var host = Create(minimum, maximum, end, .08f); host.Add(1);
            Tick(host, now, step, held);
            Assert.That(host.Health[1], Is.EqualTo(100 - expectedDamage).Within(.00001f));
            Assert.That(Bits(host.Execution.EndTime), Is.EqualTo(Bits(host.Charge.StartedAt + host.Charge.Release.Value.MotionEnd)));
            Tick(host, host.Execution.EndTime, step, true);
            Assert.That(host.Available, Is.Zero); Assert.That(host.Completions, Is.EqualTo(1)); Assert.That(host.ResourcesOpen, Is.False);
        }
        [Test] public void RepeatedWindowAdmitsLateEntrantsOnceAndExcludesTheHalfOpenEnd()
        {
            var host = Create(); host.Add(1); Tick(host, .1875f);
            host.Add(2); Tick(host, .25f);
            host.Add(3); Tick(host, .3125f);
            host.Add(4); Tick(host, .375f);
            Assert.That(host.Health[1], Is.EqualTo(75)); Assert.That(host.Health[2], Is.EqualTo(75));
            Assert.That(host.Health[3], Is.EqualTo(75)); Assert.That(host.Health[4], Is.EqualTo(100));
            Assert.That(host.Completions, Is.Zero); Assert.That(host.Available, Is.Zero);
        }
        [TestCase(0f, .125f)] [TestCase(.25f, 2f)]
        public void TimeZeroAndWholeActionIntervalsStillHitBeforeCompleting(float minimum, float step)
        {
            var host = Create(minimum, minimum + .5f, minimum + .125f, .0625f); host.Add(1);
            Tick(host, 0, step);
            Assert.That(host.Health[1], Is.EqualTo(75)); Assert.That(host.Completions, Is.EqualTo(1));
            Assert.That(host.Events, Is.EqualTo(new[] { "select", "resource", "activated", "sweep", "complete", "dispose", "release" }));
        }
        [Test] public void ResourceRejectionRetainsPointOneRecoveryAndNeverSweeps()
        {
            var host = Create(started: 4); host.Admit = false;
            host.Add(1); host.Now = 4.1875f; host.Execution.BeforeMovement(.0625f, false);
            Assert.That(host.Execution.Cancelled, Is.True); Assert.That(host.Execution.MotionDone, Is.False);
            Assert.That(host.Execution.Activated || host.Charge.IsActive || host.ResourcesOpen, Is.False);
            Assert.That(Bits(host.Execution.EndTime), Is.EqualTo(Bits(4 + (.1875f + .1f))));
            Assert.That(host.Events, Is.EqualTo(new[] { "select", "resource", "release", "failed" }));
            Tick(host, 4.25f, .015625f); Assert.That(host.Completions, Is.Zero);
            Tick(host, host.Execution.EndTime, .015625f);
            Assert.That(host.Completions, Is.EqualTo(1)); Assert.That(host.Available, Is.EqualTo(1)); Assert.That(host.Health[1], Is.EqualTo(100));
        }
        // No target at release still swings: the charge is spent and the whole release motion plays as a whiff.
        [Test] public void MissingTargetAtReleaseStillActivatesSpendsAndPlaysTheWholeSwing()
        {
            var host = Create(started: 4); host.HasTarget = false;
            host.Now = 4.1875f; host.Execution.BeforeMovement(.0625f, false);
            Assert.That(host.Execution.Activated, Is.True); Assert.That(host.Execution.Cancelled, Is.False); Assert.That(host.Available, Is.Zero);
            Assert.That(host.Events, Is.EqualTo(new[] { "select", "resource", "activated" }));
            float end = host.Execution.EndTime; Assert.That(end - 4, Is.GreaterThan(.1875f + .1f));
            Tick(host, 4.25f); Tick(host, end); Assert.That(host.Completions, Is.EqualTo(1)); Assert.That(host.Events, Does.Not.Contain("failed"));
        }
        [TestCase(false)] [TestCase(true)]
        public void CancellationBeforeOrAfterActivationCannotSpendAgainOrNotifyCompletion(bool afterActivation)
        {
            var host = Create(); host.Add(1);
            if (afterActivation) { host.Now = .1875f; host.Execution.BeforeMovement(.0625f, false); }
            host.Execution.Cancel(); host.Execution.Cancel(); Tick(host, 2, .02f, true);
            Assert.That(host.Execution.Cancelled && host.Execution.MotionDone, Is.True);
            Assert.That(host.Health[1], Is.EqualTo(100)); Assert.That(host.Available, Is.EqualTo(afterActivation ? 0 : 1));
            Assert.That(host.Completions, Is.Zero); Assert.That(host.ResourcesOpen || host.Charge.IsActive, Is.False);
            Assert.That(host.Charge.Release.HasValue, Is.EqualTo(afterActivation));
        }
        [Test] public void ReleasingANoncurrentCastDoesNotInventCancellationOrClearConfirmedHits()
        {
            var host = Create(); host.Add(1); Tick(host, .1875f); var release = host.Charge.Release;
            host.Execution.ReleaseResources(); host.Add(2); Tick(host, .25f);
            Assert.That(host.Execution.Cancelled || host.Execution.MotionDone, Is.False);
            Assert.That(host.ResourcesOpen || host.Charge.IsActive, Is.False); Assert.That(host.Charge.Release, Is.EqualTo(release));
            Assert.That(host.Health[1], Is.EqualTo(75)); Assert.That(host.Health[2], Is.EqualTo(75)); Assert.That(host.Available, Is.Zero);
            Tick(host, .6875f); Assert.That(host.Completions, Is.EqualTo(1));
            var pending = Create(); pending.Execution.ReleaseResources(); Tick(pending, 2, .02f, true);
            Assert.That(pending.Available, Is.EqualTo(1)); Assert.That(pending.Charge.Release, Is.Null);
            Assert.That(pending.Execution.Cancelled || pending.Execution.MotionDone, Is.False); Assert.That(pending.Completions, Is.Zero);
        }
        [Test] public void CancellationInsideReceiveDoesNotAbortTheAlreadyEnteredNativeSweep()
        {
            var host = Create(); host.Add(int.MinValue); host.Add(int.MaxValue);
            host.Receiving = target => { if (target == int.MinValue) host.Execution.Cancel(); };
            Tick(host, .1875f);
            Assert.That(host.Health[int.MinValue], Is.EqualTo(75)); Assert.That(host.Health[int.MaxValue], Is.EqualTo(75));
            Assert.That(host.Execution.Cancelled && host.Execution.MotionDone, Is.True); Assert.That(host.Completions, Is.Zero);
            Tick(host, .25f); Assert.That(host.Health[int.MaxValue], Is.EqualTo(75)); Assert.That(host.Available, Is.Zero);
            var another = Create(); another.Add(int.MinValue); Tick(another, .1875f);
            Assert.That(another.Health[int.MinValue], Is.EqualTo(75), "Claims belong to one charged execution, not the actor or charge definition.");
        }
        [Test] public void CancellationFromSelectionStillReachesResourceRejectionAndFailureInNativeOrder()
        {
            var host = Create(); host.Selecting = host.Execution.Cancel; Tick(host, .1875f);
            Assert.That(host.Events, Is.EqualTo(new[] { "select", "release", "resource", "release", "failed" }));
            Assert.That(host.Execution.MotionDone && host.Execution.Cancelled, Is.True); Assert.That(host.Available, Is.EqualTo(1));
            Assert.That(Bits(host.Execution.EndTime), Is.EqualTo(Bits(.1875f + .1f))); Assert.That(host.Completions, Is.Zero);
        }
        [Test] public void CancellationFromActivationKeepsTheCommittedChargeButSkipsTheFutureSweep()
        {
            var host = Create(); host.Add(1); host.Activating = host.Execution.Cancel; Tick(host, .1875f);
            Assert.That(host.Available, Is.Zero); Assert.That(host.Execution.Activated, Is.True);
            Assert.That(host.Execution.Cancelled && host.Execution.MotionDone, Is.True); Assert.That(host.Health[1], Is.EqualTo(100));
            Assert.That(host.Completions, Is.Zero); Assert.That(host.Events, Is.EqualTo(new[] { "select", "resource", "activated", "release" }));
        }
        [Test] public void SweepAndCompletionReadTheLiveClockAgainAfterCallbacks()
        {
            var host = Create(); host.Add(1); host.Sweeping = () => host.Now = .75f;
            host.Completing = () => host.Now = .125f;
            Tick(host, .1875f);
            Assert.That(host.Health[1], Is.EqualTo(75)); Assert.That(host.Completions, Is.EqualTo(1));
            Assert.That(host.Disposals, Is.Zero, "Completion can change the live clock before the separate disposal comparison.");
            host.Completing = null; host.Now = .75f; host.Execution.Step(.0625f);
            Assert.That(host.Completions, Is.EqualTo(1)); Assert.That(host.Disposals, Is.EqualTo(1));
        }
        [Test] public void CompletionKeepsTheDirectFloatAdditionInsteadOfTheRoundedSweepSample()
        {
            var host = Create(.12f, .8f, .36f, .08f); host.Add(1); float now = 0;
            for (int i = 0; i < 17; i++) { Tick(host, now, .02f); now += .02f; }
            float rounded = now + .02f;
            Assert.That(rounded, Is.GreaterThanOrEqualTo(.36f)); Assert.That((double)now + .02f, Is.LessThan((double).36f));
            Tick(host, now, .02f); Assert.That(host.Completions, Is.Zero); Assert.That(host.Execution.MotionDone, Is.False);
            now += .02f; Tick(host, now, .02f);
            Assert.That(host.Completions, Is.EqualTo(1)); Assert.That(host.Health[1], Is.EqualTo(75));
        }
        [Test] public void CompletionCanReenterTheDoneCastWithoutRepeatingItsNotificationOrDamage()
        {
            var host = Create(); host.Add(1); Tick(host, .1875f);
            host.Completing = () => { host.Execution.Cancel(); host.Execution.Step(.0625f); };
            Tick(host, .6875f);
            Assert.That(host.Completions, Is.EqualTo(1)); Assert.That(host.Health[1], Is.EqualTo(75)); Assert.That(host.Available, Is.Zero);
            Assert.That(host.Execution.Cancelled && host.Execution.MotionDone, Is.True);
            Assert.That(host.Disposals, Is.EqualTo(2), "The entered outer step still executes its disposal after the reentrant native callback returns.");
        }
        [TestCase("select")] [TestCase("resource")]
        public void PreActivationExceptionsPreserveTheExistingReleaseWithoutInventingFailure(string phase)
        {
            var host = Create(); var error = new InvalidOperationException(phase);
            if (phase == "select") host.Selecting = () => throw error; else host.Committing = () => throw error;
            host.Now = .1875f;
            Assert.That(Assert.Throws<InvalidOperationException>(() => host.Execution.BeforeMovement(.0625f, false)), Is.SameAs(error));
            Assert.That(host.Charge.Release.HasValue, Is.True); Assert.That(host.Charge.IsActive && host.ResourcesOpen, Is.True);
            Assert.That(host.Execution.Activated || host.Execution.Cancelled || host.Execution.MotionDone, Is.False);
            host.Selecting = host.Committing = null; host.Add(1); Tick(host, .1875f);
            Assert.That(host.Available, Is.Zero); Assert.That(host.Health[1], Is.EqualTo(75));
        }
        [Test] public void ActivationRecordExceptionKeepsTheCommitAndDoesNotRepeatActivation()
        {
            var host = Create(); host.Add(1); host.Activating = () => throw new InvalidOperationException("record"); host.Now = .1875f;
            Assert.Throws<InvalidOperationException>(() => host.Execution.BeforeMovement(.0625f, false));
            Assert.That(host.Execution.Activated, Is.True); Assert.That(host.Available, Is.Zero);
            host.Activating = () => Assert.Fail("An already activated charge must not activate again.");
            Tick(host, .1875f); Assert.That(host.Health[1], Is.EqualTo(75)); Assert.That(host.Execution.Cancelled, Is.False);
        }
        [Test] public void ReceiveExceptionKeepsItsClaimAndDoesNotAdvanceTheWindowObservation()
        {
            var host = Create(); host.Add(1); host.Receiving = _ => throw new InvalidOperationException("receive"); host.Now = .1875f;
            host.Execution.BeforeMovement(.0625f, false);
            Assert.Throws<InvalidOperationException>(() => host.Execution.Step(.0625f));
            Assert.That(host.Health[1], Is.EqualTo(100)); Assert.That(host.Execution.Activated, Is.True);
            Assert.That(host.Execution.Cancelled || host.Execution.MotionDone, Is.False);
            host.Receiving = null; host.Add(2);
            // A failed native sweep never executed the trailing lastEvaluated
            // assignment: even this later explicit call still intersects it.
            host.Now = .5f; host.Execution.Step(.0625f);
            Assert.That(host.Health[1], Is.EqualTo(100), "The claimed Receive is not retried after its exception.");
            Assert.That(host.Health[2], Is.EqualTo(75)); Assert.That(host.Completions, Is.Zero);
        }
        [Test] public void CompletionExceptionKeepsDoneAndDoesNotRepeatTheNotification()
        {
            var host = Create(); host.Add(1); Tick(host, .1875f);
            host.Completing = () => throw new InvalidOperationException("complete"); host.Now = .6875f;
            Assert.Throws<InvalidOperationException>(() => host.Execution.Step(.0625f));
            Assert.That(host.Execution.MotionDone, Is.True); Assert.That(host.Disposals, Is.Zero);
            host.Completing = () => Assert.Fail("The completion fact precedes its native callback."); host.Execution.Step(.0625f);
            Assert.That(host.Completions, Is.EqualTo(1)); Assert.That(host.Disposals, Is.EqualTo(1)); Assert.That(host.Health[1], Is.EqualTo(75));
        }
        [TestCase(false)] [TestCase(true)]
        public void ResourceDisposalExceptionPrecedesChargeAndLifecycleCancellation(bool failure)
        {
            var host = Create(); host.Releasing = () => throw new InvalidOperationException("release");
            Assert.Throws<InvalidOperationException>(() => { if (failure) host.Execution.Fail(); else host.Execution.Cancel(); });
            Assert.That(host.ResourcesOpen && host.Charge.IsActive, Is.True);
            Assert.That(host.Execution.Cancelled || host.Execution.MotionDone, Is.False); Assert.That(host.Execution.EndTime, Is.EqualTo(.75f));
            host.Releasing = null; host.Add(1); Tick(host, .1875f); Assert.That(host.Health[1], Is.EqualTo(75));
        }
        [Test] public void FailureRecordExceptionKeepsItsCancelledRecoveryAndStillCompletesOnce()
        {
            var host = Create(); host.Admit = false; host.Failing = () => throw new InvalidOperationException("failed record"); host.Now = .1875f;
            Assert.Throws<InvalidOperationException>(() => host.Execution.BeforeMovement(.0625f, false));
            Assert.That(host.Execution.Cancelled, Is.True); Assert.That(host.Execution.MotionDone, Is.False);
            Assert.That(host.ResourcesOpen || host.Charge.IsActive, Is.False);
            Tick(host, host.Execution.EndTime); Assert.That(host.Completions, Is.EqualTo(1)); Assert.That(host.Available, Is.EqualTo(1));
        }
    }
}
