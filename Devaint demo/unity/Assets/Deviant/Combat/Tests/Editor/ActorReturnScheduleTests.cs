using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class ActorReturnScheduleTests
    {
        static ActorReturnReservation Entry(Guid? weapon = null, Guid? id = null, float now = 0, float delay = 5) => new ActorReturnReservation(id ?? Guid.NewGuid(), weapon ?? Guid.NewGuid(), 7, new ActorPose(.125f, 3, -1), now, delay, .25f);
        [Test] public void ReservationAndScheduleStayEngineFreeAndCapturedFactsAreImmutable()
        {
            Assert.That(typeof(ActorReturnReservation).GetProperties().Any(p => p.GetSetMethod(true) != null), Is.False);
            foreach (var type in new[] { typeof(IActorReturnSchedule), typeof(ActorReturnSchedule) }) Assert.That(type.Assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("UnityEngine") || a.Name.StartsWith("UnityEditor") || a.Name == "Deviant.Combat" || a.Name.StartsWith("Deviant.Equipment") || a.Name.StartsWith("Deviant.World")), Is.False);
            var entry = Entry(now: 2); Assert.That(entry.Due, Is.EqualTo(7)); Assert.That(entry.TargetActorId, Is.EqualTo(7)); Assert.That(entry.Origin.X, Is.EqualTo(.125f)); Assert.That(entry.Origin.Facing, Is.EqualTo(-1)); Assert.That(entry.Gap, Is.EqualTo(.25f));
        }
        [Test] public void OnlyOneReservationPerWeaponAndTokenIsPublishedInInsertionOrder()
        {
            var schedule = new ActorReturnSchedule(); var first = Entry(); var second = Entry(); Assert.That(schedule.TryAdd(first), Is.True); Assert.That(schedule.TryAdd(Entry(first.WeaponId)), Is.False); Assert.That(schedule.TryAdd(Entry(id: first.Id)), Is.False); Assert.That(schedule.TryAdd(second), Is.True); Assert.That(schedule.Read().Select(p => p.Id), Is.EqualTo(new[] { first.Id, second.Id }));
        }
        [Test] public void DueTakePreservesGraceAdditionAndCannotBeRepeated()
        {
            var schedule = new ActorReturnSchedule(); var entry = Entry(delay: .25f); schedule.TryAdd(entry);
            Assert.That(schedule.TryTakeDue(entry.Id, entry.WeaponId, .24998f, out _), Is.False); Assert.That(schedule.TryTakeDue(entry.Id, entry.WeaponId, .25f, out var accepted), Is.True); Assert.That(accepted, Is.SameAs(entry)); Assert.That(schedule.TryTakeDue(entry.Id, entry.WeaponId, .3f, out _), Is.False);
            var grace = Entry(delay: .00001f); schedule.TryAdd(grace); Assert.That(schedule.TryTakeDue(grace.Id, grace.WeaponId, 0, out _), Is.True);
        }
        [Test] public void ReadsAndRemainingDoNotConsumeAnOverdueReservation()
        {
            var schedule = new ActorReturnSchedule(); var entry = Entry(); schedule.TryAdd(entry); var before = schedule.Read(); Assert.That(schedule.Remaining(entry.WeaponId, 2), Is.EqualTo(3)); Assert.That(schedule.Remaining(entry.WeaponId, 10), Is.Zero); Assert.That(schedule.Contains(entry.WeaponId), Is.True); Assert.That(before[0].Due, Is.EqualTo(5)); Assert.That(schedule.Read().Count, Is.EqualTo(1));
            Assert.That(schedule.Contains(Guid.Empty), Is.False); Assert.That(schedule.Remaining(Guid.NewGuid(), 10), Is.Zero); Assert.Throws<NotSupportedException>(() => ((IList<ActorReturnReservation>)before)[0] = Entry());
        }
        [Test] public void ConsumptionReleasesTheSourceBeforePlacementAndStaleTokenCannotTakeANewReturn()
        {
            var schedule = new ActorReturnSchedule(); var first = Entry(); schedule.TryAdd(first); Assert.That(schedule.TryTakeDue(first.Id, first.WeaponId, 5, out _), Is.True); Assert.That(schedule.Contains(first.WeaponId), Is.False);
            var next = Entry(first.WeaponId, now: 5); Assert.That(schedule.TryAdd(next), Is.True); Assert.That(schedule.TryTakeDue(first.Id, first.WeaponId, 20, out _), Is.False); Assert.That(schedule.Read().Single(), Is.SameAs(next));
        }
        [Test] public void DifferentSourceOrSnapshotFromAnotherOwnerCannotTakeTheReservation()
        {
            var first = new ActorReturnSchedule(); var second = new ActorReturnSchedule(); var entry = Entry(); first.TryAdd(entry); Assert.That(first.TryTakeDue(entry.Id, Guid.NewGuid(), 10, out _), Is.False); Assert.That(second.TryTakeDue(entry.Id, entry.WeaponId, 10, out _), Is.False); Assert.That(first.Contains(entry.WeaponId), Is.True);
        }
        [Test] public void ClearInvalidatesOldSnapshotsWithoutChangingTheirFacts()
        {
            var schedule = new ActorReturnSchedule(); var a = Entry(); var b = Entry(); schedule.TryAdd(a); schedule.TryAdd(b); var snapshot = schedule.Read(); Assert.That(schedule.TryTakeDue(a.Id, a.WeaponId, 5, out _), Is.True); schedule.Clear(); schedule.Clear(); Assert.That(schedule.TryTakeDue(b.Id, b.WeaponId, 5, out _), Is.False); Assert.That(snapshot.Count, Is.EqualTo(2)); Assert.That(snapshot[1].Origin.Y, Is.EqualTo(3)); Assert.That(schedule.Read(), Is.Empty);
        }
        [Test] public void InvalidDefinitionsCannotPublishAReservation()
        {
            Assert.Throws<ArgumentException>(() => new ActorReturnReservation(Guid.Empty, Guid.NewGuid(), 1, new ActorPose(0, 0, 1), 0, 5, .25f)); Assert.Throws<ArgumentException>(() => new ActorReturnReservation(Guid.NewGuid(), Guid.Empty, 1, new ActorPose(0, 0, 1), 0, 5, .25f)); Assert.Throws<ArgumentException>(() => new ActorReturnReservation(Guid.NewGuid(), Guid.NewGuid(), 0, new ActorPose(0, 0, 1), 0, 5, .25f)); Assert.Throws<ArgumentException>(() => new ActorReturnReservation(Guid.NewGuid(), Guid.NewGuid(), 1, default, 0, 5, .25f));
            Assert.Throws<ArgumentOutOfRangeException>(() => Entry(now: float.MaxValue, delay: float.MaxValue)); Assert.Throws<ArgumentOutOfRangeException>(() => Entry(delay: -1)); Assert.Throws<ArgumentOutOfRangeException>(() => Entry(now: float.NaN));
        }
        [Test] public void InvalidQueriesPreserveAcceptedReservationAndOriginalDeadline()
        {
            var schedule = new ActorReturnSchedule(); var entry = Entry(); schedule.TryAdd(entry); Assert.Throws<ArgumentNullException>(() => schedule.TryAdd(null)); Assert.Throws<ArgumentOutOfRangeException>(() => schedule.Remaining(entry.WeaponId, float.NaN)); Assert.Throws<ArgumentOutOfRangeException>(() => schedule.TryTakeDue(entry.Id, entry.WeaponId, -1, out _)); Assert.That(schedule.Read().Single(), Is.SameAs(entry)); Assert.That(entry.Due, Is.EqualTo(5));
        }
    }
}
