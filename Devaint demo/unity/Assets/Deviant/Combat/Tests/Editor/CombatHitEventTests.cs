using System;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class CombatHitEventTests
    {
        static readonly CombatInputSession Session = new CombatInputSession(7, 2, 1);
        static CombatActorHandle Actor(int id) => new CombatActorHandle(Session, id);
        static CombatHitOrigin Origin() => new CombatHitOrigin(Actor(7), new CombatCommandKey(Actor(7), 3, CombatCommandKind.Basic), Guid.NewGuid(), 2, "echo", Guid.NewGuid(), CombatHitKind.Projectile);
        [Test] public void ContractsContainOnlyDetachedValuesAndRetainOriginalAndReflectorCauses()
        {
            foreach (var type in new[] { typeof(CombatHitOrigin), typeof(CombatAcceptedHit), typeof(CombatHitPage) })
            {
                Assert.That(type.IsValueType, Is.True);
                Assert.That(type.GetProperties().Any(p => p.GetSetMethod(true) != null), Is.False);
                Assert.That(type.GetProperties().All(p => p.PropertyType.IsValueType || p.PropertyType == typeof(string)), Is.True);
            }
            Assert.That(typeof(ICombatHitEvents).Assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("UnityEngine") || a.Name == "Deviant.Combat" || a.Name == "Deviant.Combat.Execution"), Is.False);
            var original = Origin(); var reflector = new CombatHitOrigin(Actor(12), new CombatCommandKey(Actor(12), 8, CombatCommandKind.Special), Guid.NewGuid(), -1, null, Guid.Empty, CombatHitKind.Parry);
            var reflected = original.ReflectedBy(reflector);
            Assert.That(reflected.Source.Equals(original.Source), Is.True); Assert.That(reflected.Command.Equals(original.Command), Is.True);
            Assert.That(reflected.ExecutionId, Is.EqualTo(original.ExecutionId)); Assert.That(reflected.EffectId, Is.EqualTo(original.EffectId));
            Assert.That(reflected.EffectiveSource.Equals(reflector.Source), Is.True); Assert.That(reflected.ReflectorCommand.Equals(reflector.Command), Is.True);
            Assert.That(reflected.ReflectorExecutionId, Is.EqualTo(reflector.ExecutionId)); Assert.That(reflected.HitId, Is.EqualTo("echo"));
        }
        [Test] public void AcceptedZeroDamageAndAbsorptionAreImmutableFactsNotHealthDeltas()
        {
            var stream = new CombatHitEventStream(4); var vitals = new ActorVitalsState(100); vitals.WriteShield(5); var before = vitals.Read();
            var applied = vitals.ApplyDamage(3, 0, false); var origin = Origin();
            stream.Add(2, .5f, origin, Actor(12), 3, before, applied, default);
            before = vitals.Read(); var zero = vitals.ApplyDamage(0, 0, false); stream.Add(2, .5f, origin, Actor(12), 0, before, zero, default);
            vitals.Reset(); var values = new CombatAcceptedHit[4];
            Assert.That(stream.TryRead(Session, new CombatOutcomeCursor(Session), values, out var page), Is.True); Assert.That(page.Count, Is.EqualTo(2));
            Assert.That(values[0].Damage, Is.Zero); Assert.That(values[0].Absorbed, Is.EqualTo(3)); Assert.That(values[0].Before.Shield, Is.EqualTo(5)); Assert.That(values[0].After.Shield, Is.EqualTo(2));
            Assert.That(values[0].After.Health, Is.EqualTo(100)); Assert.That(values[1].RequestedDamage, Is.Zero); Assert.That(values[1].After.ReceivedHits, Is.EqualTo(2));
            Assert.That(values[1].EventSequence, Is.GreaterThan(values[0].EventSequence));
        }
        [Test] public void BoundedRingReportsGapsSupportsIndependentReadersAndRejectsOldSessions()
        {
            var stream = new CombatHitEventStream(3); var values = new CombatAcceptedHit[2]; var vitals = new ActorVitalsState(100); var origin = Origin();
            for (int i = 0; i < 5; i++) { var before = vitals.Read(); stream.Add(i, i, origin, Actor(12), 0, before, vitals.ApplyDamage(0, i, false), default); }
            var cursor = new CombatOutcomeCursor(Session);
            Assert.That(stream.TryRead(Session, cursor, values, out var page), Is.True); Assert.That(page.DroppedBefore, Is.EqualTo(2)); Assert.That(page.Count, Is.EqualTo(2));
            Assert.That(values[0].EventSequence, Is.EqualTo(3)); Assert.That(values[1].EventSequence, Is.EqualTo(4));
            var next = page.Next; Assert.That(stream.TryRead(Session, cursor, values, out var independent), Is.True); Assert.That(independent.DroppedBefore, Is.EqualTo(2));
            Assert.That(stream.TryRead(Session, next, values, out page), Is.True); Assert.That(page.Count, Is.EqualTo(1)); Assert.That(values[0].EventSequence, Is.EqualTo(5));
            var reset = new CombatInputSession(7, 3, 2); Assert.That(stream.TryRead(reset, next, values, out _), Is.False);
            Assert.That(stream.TryRead(Session, new CombatOutcomeCursor(Session, -1), values, out _), Is.False);
            Assert.That(stream.TryRead(Session, new CombatOutcomeCursor(Session, 99), values, out _), Is.False);
            Assert.That(stream.TryRead(Session, cursor, Array.Empty<CombatAcceptedHit>(), out _), Is.False);
        }
        [Test] public void WriterAndFixedBufferPollingAllocateNothing()
        {
            var stream = new CombatHitEventStream(8); var values = new CombatAcceptedHit[8]; var origin = Origin(); var target = Actor(12);
            var vitals = new ActorVitalsState(100); var state = vitals.Read(); var result = vitals.ApplyDamage(0, 0, false); var cursor = new CombatOutcomeCursor(Session);
            stream.Add(0, 0, origin, target, 0, state, result, default); stream.TryRead(Session, cursor, values, out var page); cursor = page.Next;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 2048; i++) { stream.Add(i, 0, origin, target, 0, state, result, default); stream.TryRead(Session, cursor, values, out page); cursor = page.Next; }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before; Assert.That(allocated, Is.Zero);
        }
    }
}
