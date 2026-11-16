using System;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class CombatCommandsTests
    {
        static readonly CombatInputSession Session = new CombatInputSession(7, 3, 2);
        static CombatCommandKey Key(long sequence, CombatCommandKind kind = CombatCommandKind.Basic)
            => new CombatCommandKey(new CombatActorHandle(Session, 7), sequence, kind);

        [Test] public void PublicCommandOutcomesAreDetachedAndContractsHaveNoEngineDependency()
        {
            var assembly = typeof(ICombatCommands).Assembly;
            Assert.That(assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("UnityEngine") || a.Name.StartsWith("UnityEditor") || a.Name == "Deviant.Combat" || a.Name == "Deviant.Combat.Execution"), Is.False);
            foreach (var type in new[] { typeof(CombatCommandKey), typeof(CombatCommandOutcome), typeof(CombatOutcomeCursor), typeof(CombatOutcomePage), typeof(CombatAICommand), typeof(CombatAICommandResult) })
            {
                Assert.That(type.IsValueType, Is.True);
                Assert.That(type.GetProperties().Any(p => p.GetSetMethod(true) != null), Is.False);
                Assert.That(type.GetProperties().All(p => p.PropertyType.IsValueType || p.PropertyType == typeof(string)), Is.True);
            }
            Assert.That(Key(1).Equals(Key(1)), Is.True); Assert.That(Key(1).GetHashCode(), Is.EqualTo(Key(1).GetHashCode()));
            Assert.That(Key(1).Equals(Key(2)), Is.False); Assert.That(Key(1).Equals(Key(1, CombatCommandKind.Special)), Is.False);
            Assert.That(Key(1).Equals(new CombatCommandKey(new CombatActorHandle(new CombatInputSession(7, 4, 2), 7), 1, CombatCommandKind.Basic)), Is.False);
            Assert.That(default(CombatCommandKey).IsValid, Is.False);
            Assert.That(Key(1, (CombatCommandKind)999).IsValid, Is.False);
            Assert.That(new CombatCommandKey(new CombatActorHandle(default, 7), 1, CombatCommandKind.Basic).IsValid, Is.False);
        }
        [Test] public void OrderedBoundedPollingReportsExactGapsAndDoesNotConsumeOtherCursors()
        {
            var stream = new CombatCommandOutcomes(3); var id = Guid.NewGuid();
            for (int i = 1; i <= 5; i++) stream.Add(i, i * .02f, Key(i), CombatCommandState.Started, CombatCommandReason.None, id, default);
            var page = new CombatCommandOutcome[2]; var cursor = new CombatOutcomeCursor(Session);
            Assert.That(stream.TryRead(Session, cursor, page, out var result), Is.True);
            Assert.That(result.DroppedBefore, Is.EqualTo(2)); Assert.That(result.Count, Is.EqualTo(2)); Assert.That(result.LatestEventSequence, Is.EqualTo(5));
            Assert.That(page[0].EventSequence, Is.EqualTo(3)); Assert.That(page[1].EventSequence, Is.EqualTo(4));
            var retained = page[0];
            Assert.That(stream.TryRead(Session, result.Next, page, out result), Is.True);
            Assert.That(result.Count, Is.EqualTo(1)); Assert.That(result.DroppedBefore, Is.Zero); Assert.That(page[0].EventSequence, Is.EqualTo(5));
            Assert.That(stream.TryRead(Session, cursor, page, out var independent), Is.True); Assert.That(independent.DroppedBefore, Is.EqualTo(2));
            Assert.That(retained.Command.Sequence, Is.EqualTo(3)); Assert.That(retained.ExecutionId, Is.EqualTo(id));
        }
        [Test] public void PollingRejectsInvalidOrOldSessionWithoutMutationAndReboundSessionKeepsOldEventKeys()
        {
            var stream = new CombatCommandOutcomes(2); var page = new CombatCommandOutcome[2];
            stream.Add(2, .5f, Key(1), CombatCommandState.Cancelled, CombatCommandReason.Reset, Guid.NewGuid(), default);
            var next = new CombatInputSession(7, 4, 3);
            Assert.That(stream.TryRead(next, new CombatOutcomeCursor(Session), page, out _), Is.False);
            Assert.That(stream.TryRead(next, new CombatOutcomeCursor(next, -1), page, out _), Is.False);
            Assert.That(stream.TryRead(next, new CombatOutcomeCursor(next, 2), page, out _), Is.False);
            Assert.That(stream.TryRead(next, new CombatOutcomeCursor(next), Array.Empty<CombatCommandOutcome>(), out _), Is.False);
            Assert.That(stream.TryRead(next, new CombatOutcomeCursor(next), page, out var result), Is.True);
            Assert.That(result.Count, Is.EqualTo(1)); Assert.That(page[0].Command.Actor.Session.Equals(Session), Is.True);
            Assert.That(result.Next.Session.Equals(next), Is.True);
        }
        [Test] public void WriterAndCallerBufferPollingAllocateNothingAfterConstruction()
        {
            var stream = new CombatCommandOutcomes(16); var page = new CombatCommandOutcome[16]; var cursor = new CombatOutcomeCursor(Session); var key = Key(1); var id = Guid.NewGuid();
            stream.Add(0, 0, key, CombatCommandState.Started, CombatCommandReason.None, id, default); stream.TryRead(Session, cursor, page, out _);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1024; i++)
            {
                stream.Add(i, 0, key, CombatCommandState.Completed, CombatCommandReason.None, id, default);
                stream.TryRead(Session, cursor, page, out var result); cursor = result.Next;
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
        }
        [Test] public void OriginProjectionKeepsFirstPressWithoutInventingOutcomesFromConsumption()
        {
            var ingress = new CombatInputIngress(7);
            CombatInputSample Sample(CombatButtons button) => new CombatInputSample(ingress.Session, ingress.NextSequence,
                new CombatInput(0, button), CombatInputSource.Simulation, 0, CombatInputTimeDomain.Simulation, 0);
            var first = Sample(CombatButtons.Basic); ingress.Submit(first, 0, 0);
            var duplicate = Sample(CombatButtons.Basic); ingress.Submit(duplicate, 0, 0);
            var special = Sample(CombatButtons.Special); ingress.Submit(special, 0, 0);
            Assert.That(ingress.ReadForArbitration().Pressed, Is.EqualTo(CombatButtons.Basic | CombatButtons.Special));
            Assert.That(ingress.Origins.For(CombatButtons.Basic).Equals(CombatCommandKey.For(first)), Is.True);
            Assert.That(ingress.Origins.For(CombatButtons.Special).Equals(CombatCommandKey.For(special)), Is.True);
            Assert.That(ingress.Origins.For(CombatButtons.Dash).IsValid, Is.False);
            Assert.That(ingress.PendingCount, Is.EqualTo(3)); Assert.That(ingress.PendingAt(1).Sequence, Is.EqualTo(duplicate.Sequence));
            Assert.That(ingress.WasEvaluated(first.Sequence), Is.True);
            ingress.Consume(0, 0); Assert.That(ingress.PendingCount, Is.Zero);
        }
        [Test] public void InvalidRestrictedSubmissionDoesNotDiscardPendingCommands()
        {
            var ingress = new CombatInputIngress(7);
            var sample = new CombatInputSample(ingress.Session, 1, new CombatInput(0, CombatButtons.Basic), CombatInputSource.Simulation, 0, CombatInputTimeDomain.Simulation, 0);
            ingress.Submit(sample, 0, 0);
            Assert.That(ingress.Validate(sample, CombatInputDiscardReason.Blocked), Is.EqualTo(CombatInputAdmissionStatus.RejectedSequence));
            Assert.That(ingress.Submit(sample, 0, 0, CombatInputDiscardReason.Blocked).Status, Is.EqualTo(CombatInputAdmissionStatus.RejectedSequence));
            Assert.That(ingress.PendingCount, Is.EqualTo(1)); Assert.That(ingress.Current.Has(CombatButtons.Basic), Is.True);
        }
    }
}
