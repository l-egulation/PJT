using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class CombatInputIngressTests
    {
        [TestCase(CombatInputSource.Keyboard)] [TestCase(CombatInputSource.Gamepad)] [TestCase(CombatInputSource.OtherDevice)]
        public void HardwareSourcesUseTheSameAdmissionAndRealtimeMetadataRules(CombatInputSource source)
        {
            var ingress = new CombatInputIngress(12);
            var sample = new CombatInputSample(ingress.Session, ingress.NextSequence,
                new CombatInput(pressed: CombatButtons.Weapon1Basic), source, 42, CombatInputTimeDomain.Realtime, 8.5);
            Assert.That(ingress.Submit(sample, 0, 0).Status, Is.EqualTo(CombatInputAdmissionStatus.Admitted));
            Assert.That(ingress.Current.Pressed, Is.EqualTo(CombatButtons.Weapon1Basic));
            Assert.That(ingress.Validate(new CombatInputSample(ingress.Session, ingress.NextSequence, default,
                source, 0, CombatInputTimeDomain.Realtime, 8.6)), Is.EqualTo(CombatInputAdmissionStatus.RejectedMetadata));
            Assert.That(ingress.Validate(new CombatInputSample(ingress.Session, ingress.NextSequence, default,
                source, 42, CombatInputTimeDomain.Simulation, 8.6)), Is.EqualTo(CombatInputAdmissionStatus.RejectedMetadata));
        }

        [Test] public void TargetedEdgesKeepDistinctOriginsAndLatestSlotHoldsThroughCapture()
        {
            var ingress = new CombatInputIngress(12);
            using (var capture = ingress.BeginCapture(4))
            {
                ingress.Submit(Sample(ingress, new CombatInput(pressed: CombatButtons.Weapon1Basic)), 0, 0);
                ingress.Submit(Sample(ingress, new CombatInput(pressed: CombatButtons.Weapon2Basic)), 0, 0);
                ingress.Submit(Sample(ingress, new CombatInput(pressed: CombatButtons.Weapon1Special, heldSpecials: CombatButtons.Weapon1Special)), 0, 0);
                ingress.Submit(Sample(ingress, new CombatInput(pressed: CombatButtons.Weapon2Special, released: CombatButtons.None, heldSpecials: CombatButtons.Weapon2Special)), 0, 0);
                var input = ingress.ReadForArbitration();
                Assert.That(input.Pressed, Is.EqualTo(CombatInput.TargetedAttacks));
                Assert.That(input.IsSpecialHeld(0), Is.False); Assert.That(input.IsSpecialHeld(1), Is.True);
                Assert.That(ingress.Origins.For(CombatButtons.Weapon1Basic).Sequence, Is.EqualTo(1));
                Assert.That(ingress.Origins.For(CombatButtons.Weapon2Basic).Sequence, Is.EqualTo(2));
                Assert.That(ingress.Origins.For(CombatButtons.Weapon1Special).Sequence, Is.EqualTo(3));
                Assert.That(ingress.Origins.For(CombatButtons.Weapon2Special).Sequence, Is.EqualTo(4));
                Assert.That(ingress.Origins.For(CombatButtons.Weapon2Special).Kind, Is.EqualTo(CombatCommandKind.Special));
                ingress.Consume(0, 0);
                Assert.That(capture.TryRead(out var batch), Is.True); Assert.That(batch.Input, Is.EqualTo(input));
                Assert.That(batch.Receipts[3].Sample.Input.HeldSpecials, Is.EqualTo(CombatButtons.Weapon2Special));
                Assert.That(ingress.Current.Pressed, Is.EqualTo(CombatButtons.None));
                Assert.That(ingress.Current.IsSpecialHeld(1), Is.True);
            }
        }
        static CombatInputSample Sample(CombatInputIngress ingress, CombatInput input, double time = 12.5)
            => new CombatInputSample(ingress.Session, ingress.NextSequence, input, CombatInputSource.Keyboard, 17, CombatInputTimeDomain.Realtime, time);

        [Test] public void RepeatedPressReleaseSamplesKeepOrderAndMetadataWhileTheArbiterSeesOnlyTheUnion()
        {
            var ingress = new CombatInputIngress(12);
            using (var capture = ingress.BeginCapture(4))
            {
                var first = Sample(ingress, new CombatInput(1, CombatButtons.Basic, true), 8.1);
                Assert.That(ingress.Submit(first, 41, .82f).Status, Is.EqualTo(CombatInputAdmissionStatus.Admitted));
                ingress.Submit(Sample(ingress, new CombatInput(-1, specialHeld: false, released: CombatButtons.Basic), 8.2), 41, .82f);
                ingress.Submit(Sample(ingress, new CombatInput(0, CombatButtons.Basic, true), 8.3), 41, .82f);
                ingress.Submit(Sample(ingress, new CombatInput(-1, CombatButtons.Dash)), 41, .82f);
                var input = ingress.ReadForArbitration();
                Assert.That(input.Pressed, Is.EqualTo(CombatButtons.Basic | CombatButtons.Dash));
                Assert.That(input.Released, Is.EqualTo(CombatButtons.Basic));
                Assert.That(input.Axis, Is.EqualTo(-1)); Assert.That(input.SpecialHeld, Is.False);
                Assert.That(capture.TryRead(out _), Is.False, "Admission alone is not consumption.");
                ingress.Consume(41, .82f);
                Assert.That(capture.TryRead(out var batch), Is.True);
                Assert.That(batch.Session, Is.EqualTo(ingress.Session)); Assert.That(batch.TickCount, Is.EqualTo(41)); Assert.That(batch.Now, Is.EqualTo(.82f));
                Assert.That(batch.Input, Is.EqualTo(input)); Assert.That(batch.Receipts.Count, Is.EqualTo(4));
                Assert.That(batch.TickOutcome, Is.EqualTo(CombatInputTickOutcome.NotObserved));
                Assert.That(batch.Receipts.Select(r => r.Sample.Sequence), Is.EqualTo(new long[] { 1, 2, 3, 4 }));
                Assert.That(batch.Receipts.Take(3).Select(r => r.Sample.Timestamp), Is.EqualTo(new[] { 8.1, 8.2, 8.3 }));
                Assert.That(batch.Receipts.All(r => r.Disposition == CombatInputDisposition.Consumed && r.Sample.Source == CombatInputSource.Keyboard && r.Sample.DeviceId == 17 && r.Sample.TimeDomain == CombatInputTimeDomain.Realtime), Is.True);
                Assert.Throws<NotSupportedException>(() => ((IList<CombatInputReceipt>)batch.Receipts)[0] = default);
                ingress.Consume(42, .84f);
                Assert.That(capture.TryRead(out var idle), Is.True); Assert.That(idle.Receipts, Is.Empty);
                Assert.That(idle.Input.Pressed, Is.EqualTo(CombatButtons.None)); Assert.That(idle.Input.Released, Is.EqualTo(CombatButtons.None));
                Assert.That(idle.Input.Axis, Is.EqualTo(-1)); Assert.That(batch.Receipts[0].Sample, Is.EqualTo(first));
                Assert.That(capture.TryRead(out _), Is.False);
            }
        }

        [Test] public void StaleIdentitySequenceAndMalformedMetadataCannotMutateOrClearAcceptedSamples()
        {
            var ingress = new CombatInputIngress(12);
            var accepted = Sample(ingress, new CombatInput(1, CombatButtons.Basic, true));
            ingress.Submit(accepted, 0, 0);
            var before = ingress.Current; long sequence = ingress.NextSequence;
            var foreign = new CombatInputSample(new CombatInputSession(13, ingress.Session.Generation, ingress.Session.Epoch), sequence, default, CombatInputSource.Simulation, 0, CombatInputTimeDomain.Simulation, 0);
            Assert.That(ingress.Submit(foreign, 0, 0, CombatInputDiscardReason.Blocked).Status, Is.EqualTo(CombatInputAdmissionStatus.RejectedSession));
            Assert.That(ingress.Submit(accepted, 0, 0, CombatInputDiscardReason.Blocked).Status, Is.EqualTo(CombatInputAdmissionStatus.RejectedSequence));
            foreach (var sample in new[] {
                new CombatInputSample(ingress.Session, sequence, default, CombatInputSource.Keyboard, 17, CombatInputTimeDomain.Realtime, double.NaN),
                new CombatInputSample(ingress.Session, sequence, default, CombatInputSource.Keyboard, 0, CombatInputTimeDomain.Realtime, 1),
                new CombatInputSample(ingress.Session, sequence, default, CombatInputSource.Simulation, 0, CombatInputTimeDomain.Realtime, 1),
                Sample(ingress, default, -1),
                Sample(ingress, new CombatInput(0, CombatButtons.Basic | CombatButtons.Jump)),
                Sample(ingress, new CombatInput(0, CombatButtons.Basic, released: CombatButtons.Basic)) })
                Assert.That(ingress.Submit(sample, 0, 0, CombatInputDiscardReason.Blocked).Status, Is.EqualTo(CombatInputAdmissionStatus.RejectedMetadata));
            Assert.That(ingress.Current, Is.EqualTo(before)); Assert.That(ingress.NextSequence, Is.EqualTo(sequence));
        }

        [TestCase(CombatInputDiscardReason.Blocked)] [TestCase(CombatInputDiscardReason.Paused)] [TestCase(CombatInputDiscardReason.Dead)]
        public void RestrictedAdmissionDiscardsBothPendingAndNewSamplesWithoutLeavingEdges(CombatInputDiscardReason reason)
        {
            var ingress = new CombatInputIngress(12);
            using (var capture = ingress.BeginCapture(8))
            {
                ingress.Submit(Sample(ingress, new CombatInput(1, CombatButtons.Basic, true)), 4, .08f);
                var admission = ingress.Submit(Sample(ingress, new CombatInput(-1, CombatButtons.Special, true)), 4, .08f, reason);
                Assert.That(admission.Status, Is.EqualTo(CombatInputAdmissionStatus.Discarded)); Assert.That(admission.DiscardReason, Is.EqualTo(reason));
                Assert.That(ingress.Current, Is.EqualTo(default(CombatInput)));
                for (int i = 0; i < 2; i++)
                {
                    Assert.That(capture.TryRead(out var batch), Is.True); Assert.That(batch.Kind, Is.EqualTo(CombatInputBatchKind.Discard));
                    Assert.That(batch.Receipts[0].Disposition, Is.EqualTo(CombatInputDisposition.Discarded)); Assert.That(batch.Receipts[0].DiscardReason, Is.EqualTo(reason));
                }
                ingress.Consume(4, .08f); Assert.That(capture.TryRead(out var idle), Is.True); Assert.That(idle.Receipts, Is.Empty);
                Assert.That(idle.Input.Pressed, Is.EqualTo(CombatButtons.None));
            }
        }

        [Test] public void BothResetsInvalidateTokensButOnlyNewEpochRestartsContinuousStateAndEpoch()
        {
            var ingress = new CombatInputIngress(12);
            using (var capture = ingress.BeginCapture(4))
            {
                var old = Sample(ingress, new CombatInput(-1, CombatButtons.Basic, true)); ingress.Submit(old, 5, .1f);
                ingress.Reset(false, 5, .1f);
                Assert.That(ingress.Session.Generation, Is.EqualTo(old.Session.Generation + 1)); Assert.That(ingress.Session.Epoch, Is.EqualTo(old.Session.Epoch));
                Assert.That(ingress.Current.Axis, Is.EqualTo(-1)); Assert.That(ingress.Current.SpecialHeld, Is.True); Assert.That(ingress.Current.Pressed, Is.EqualTo(CombatButtons.None));
                Assert.That(ingress.Submit(new CombatInputSample(old.Session, ingress.NextSequence, default, CombatInputSource.Simulation, 0, CombatInputTimeDomain.Simulation, 0), 5, .1f).Status, Is.EqualTo(CombatInputAdmissionStatus.RejectedSession));
                Assert.That(capture.TryRead(out var reset), Is.True); Assert.That(reset.TickCount, Is.EqualTo(5)); Assert.That(reset.Receipts[0].DiscardReason, Is.EqualTo(CombatInputDiscardReason.Reset));
                Assert.That(reset.Receipts[0].Sample.Session, Is.EqualTo(old.Session));
                long sequence = ingress.NextSequence; var prior = ingress.Session;
                ingress.Reset(true, 0, 0);
                Assert.That(ingress.Session.Epoch, Is.EqualTo(prior.Epoch + 1)); Assert.That(ingress.Session.Generation, Is.EqualTo(prior.Generation + 1));
                Assert.That(ingress.NextSequence, Is.EqualTo(sequence)); Assert.That(ingress.Current, Is.EqualTo(default(CombatInput)));
            }
        }

        [Test] public void PendingAndObservationStorageAreBoundedAndObservationOverflowNeverChangesGameplay()
        {
            var ingress = new CombatInputIngress(12, 2);
            using (var capture = ingress.BeginCapture(2))
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => ingress.BeginCapture(0));
                Assert.Throws<ArgumentOutOfRangeException>(() => ingress.BeginCapture(CombatInputIngress.MaximumCapturedReceipts / 2 + 1));
                Assert.That(capture.IsActive, Is.True, "Rejected observation setup must leave its existing capture active.");
                ingress.Submit(Sample(ingress, new CombatInput(1, CombatButtons.Basic)), 0, 0);
                ingress.Submit(Sample(ingress, new CombatInput(-1, CombatButtons.Jump)), 0, 0);
                var before = ingress.Current; long sequence = ingress.NextSequence;
                Assert.That(ingress.Submit(Sample(ingress, new CombatInput(0, CombatButtons.Dash)), 0, 0).Status, Is.EqualTo(CombatInputAdmissionStatus.RejectedCapacity));
                Assert.That(ingress.Current, Is.EqualTo(before)); Assert.That(ingress.NextSequence, Is.EqualTo(sequence));
                ingress.Consume(0, 0); ingress.Consume(1, .02f); ingress.Consume(2, .04f);
                Assert.That(capture.DroppedBatches, Is.EqualTo(1)); Assert.That(ingress.Current.Pressed, Is.EqualTo(CombatButtons.None));
                Assert.That(capture.TryRead(out var first), Is.True); Assert.That(first.TickCount, Is.EqualTo(1));
                Assert.That(capture.TryRead(out var second), Is.True); Assert.That(second.TickCount, Is.EqualTo(2));
                capture.Dispose(); Assert.That(capture.IsActive, Is.False);
                ingress.Consume(3, .06f); Assert.That(capture.TryRead(out _), Is.False);
            }
        }

        [Test] public void InputSubmittedBySynchronousArbitrationCannotBeMisreportedAsChronologicallyExecuted()
        {
            var ingress = new CombatInputIngress(12);
            using (var capture = ingress.BeginCapture(2))
            {
                ingress.Submit(Sample(ingress, new CombatInput(1, CombatButtons.Basic)), 0, 0);
                var input = ingress.ReadForArbitration();
                ingress.Submit(Sample(ingress, new CombatInput(-1, CombatButtons.Special, true)), 0, 0);
                ingress.Consume(0, 0);
                Assert.That(capture.TryRead(out var batch), Is.True); Assert.That(batch.Input, Is.EqualTo(input));
                Assert.That(batch.Receipts[0].Disposition, Is.EqualTo(CombatInputDisposition.Consumed));
                Assert.That(batch.Receipts[1].Disposition, Is.EqualTo(CombatInputDisposition.Discarded));
                Assert.That(ingress.Current.Axis, Is.EqualTo(-1)); Assert.That(ingress.Current.SpecialHeld, Is.True);
                Assert.That(ingress.Current.Pressed, Is.EqualTo(CombatButtons.None), "The existing post-arbitration clear boundary remains unchanged.");
            }
        }
        [TestCase(true)] [TestCase(false)]
        public void TickCapturePublishesOnlyAtCompletionAndSeparatesInputConsumptionFromWorldSuccess(bool completed)
        {
            var ingress = new CombatInputIngress(12);
            using (var capture = ingress.BeginCapture(4))
            {
                ingress.BeginTick(); ingress.Submit(Sample(ingress, new CombatInput(0, CombatButtons.Basic)), 9, .18f);
                ingress.ReadForArbitration(); ingress.Consume(9, .18f);
                Assert.That(capture.TryRead(out _), Is.False);
                ingress.CompleteTick(9, .18f, completed ? .2f : .18f, completed);
                Assert.That(capture.TryRead(out var batch), Is.True); Assert.That(batch.Receipts.Single().Disposition, Is.EqualTo(CombatInputDisposition.Consumed));
                Assert.That(batch.TickOutcome, Is.EqualTo(completed ? CombatInputTickOutcome.Completed : CombatInputTickOutcome.Faulted));
                Assert.That(batch.Now, Is.EqualTo(.18f)); Assert.That(batch.ObservedThroughNow, Is.EqualTo(completed ? .2f : .18f));
                ingress.BeginTick(); ingress.CompleteTick(10, .2f, .2f, false);
                Assert.That(capture.TryRead(out var earlyFailure), Is.True);
                Assert.That(earlyFailure.Kind, Is.EqualTo(CombatInputBatchKind.TickBoundary)); Assert.That(earlyFailure.Receipts, Is.Empty);
                Assert.That(earlyFailure.TickOutcome, Is.EqualTo(CombatInputTickOutcome.Faulted));
            }
        }
        [Test] public void InTickDeathDiscardsFollowConsumedSamplesAndOverflowCannotStrandTheObserver()
        {
            var ingress = new CombatInputIngress(12);
            using (var capture = ingress.BeginCapture(2))
            {
                ingress.BeginTick();
                for (int i = 0; i < 3; i++)
                {
                    ingress.Submit(Sample(ingress, new CombatInput(0, CombatButtons.Basic)), 7, .14f);
                    ingress.ReadForArbitration(); ingress.Consume(7, .14f);
                }
                var dead = Sample(ingress, new CombatInput(1, CombatButtons.Special, true));
                ingress.Submit(dead, 7, .14f, CombatInputDiscardReason.Dead);
                Assert.That(capture.DroppedBatches, Is.EqualTo(2)); Assert.That(capture.TryRead(out _), Is.False);
                ingress.CompleteTick(7, .14f, .14f, false);
                Assert.That(capture.TryRead(out var consumed), Is.True); Assert.That(consumed.Receipts.Single().Sample.Sequence, Is.EqualTo(3));
                Assert.That(consumed.TickOutcome, Is.EqualTo(CombatInputTickOutcome.Faulted));
                Assert.That(capture.TryRead(out var discarded), Is.True); Assert.That(discarded.Receipts.Single().Sample, Is.EqualTo(dead));
                Assert.That(discarded.Receipts.Single().DiscardReason, Is.EqualTo(CombatInputDiscardReason.Dead));
                Assert.That(discarded.TickOutcome, Is.EqualTo(CombatInputTickOutcome.NotObserved));
                Assert.That(capture.TryRead(out _), Is.False);
                ingress.BeginTick(); ingress.CompleteTick(8, .16f, .18f, true);
                Assert.That(capture.TryRead(out var next), Is.True); Assert.That(next.TickCount, Is.EqualTo(8));
                Assert.That(next.TickOutcome, Is.EqualTo(CombatInputTickOutcome.Completed));
            }
        }
        [TestCase(false)] [TestCase(true)]
        public void GameplayAdmissionAndCompletedCaptureWriteAllocateNothingAfterSetup(bool observing)
        {
            var ingress = new CombatInputIngress(12);
            using (var capture = observing ? ingress.BeginCapture(2) : null)
            {
                for (int i = 0; i < 32; i++)
                {
                    ingress.BeginTick(); ingress.Submit(Sample(ingress, new CombatInput(0, CombatButtons.Basic)), i, 0);
                    ingress.ReadForArbitration(); ingress.Consume(i, 0); ingress.CompleteTick(i, 0, 0, true);
                }
                GC.GetAllocatedBytesForCurrentThread(); long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 2048; i++)
                {
                    ingress.BeginTick(); ingress.Submit(Sample(ingress, new CombatInput(0, CombatButtons.Basic)), i, 0);
                    ingress.ReadForArbitration(); ingress.Consume(i, 0); ingress.CompleteTick(i, 0, 0, true);
                }
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.That(allocated, Is.Zero, "Only explicit capture setup and observer polling may allocate, never the gameplay writer or its full-tick completion.");
            }
        }
    }
}
