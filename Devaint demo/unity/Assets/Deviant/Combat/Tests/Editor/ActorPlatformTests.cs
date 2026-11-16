using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class ActorPlatformTests
    {
        sealed class World : IActorSurfaceQuery, IActorPlatformQuery
        {
            public Guid Scope { get; set; }
            public readonly List<ActorBoxCast> BodyQueries = new List<ActorBoxCast>();
            public readonly List<ActorBoxCast> PlatformQueries = new List<ActorBoxCast>();
            public IReadOnlyList<ActorPlatformContact> Contacts = Array.AsReadOnly(Array.Empty<ActorPlatformContact>());
            public IReadOnlyList<ActorSurfaceContact> Ground = Array.AsReadOnly(new[] { new ActorSurfaceContact(.01f, 0, 1, 0, true, false) });
            public IReadOnlyList<ActorSurfaceContact> Solids = Array.AsReadOnly(Array.Empty<ActorSurfaceContact>());
            public readonly Dictionary<ActorPlatformKey, ActorPlatformObservation> Observations = new Dictionary<ActorPlatformKey, ActorPlatformObservation>();
            public Func<ActorPlatformKey, ActorPlatformObservation> OnObserve;
            public IReadOnlyList<ActorSurfaceContact> BoxCast(ActorBoxCast request) { BodyQueries.Add(request); return request.Height < .1f ? Ground : Solids; }
            public IReadOnlyList<ActorPlatformContact> CastOneWayPlatforms(ActorBoxCast request) { PlatformQueries.Add(request); return Contacts; }
            public ActorPlatformObservation Observe(ActorPlatformKey key) => OnObserve != null ? OnObserve(key) : Observations.TryGetValue(key, out var value) ? value : new ActorPlatformObservation(key, false);
        }
        static ActorBodyFrame Body() => new ActorBodyFrame(2, 0, 1, 2, .25f, 1, -1);
        static ActorPlatformKey Key(IActorPlatforms owner) => new ActorPlatformKey(owner.Scope, Guid.NewGuid());
        static ActorDropMotion Drop(ActorPlatformState owner, World world, ActorPlatformKey key, float now = 0, float planeY = 0)
        { world.Contacts = Array.AsReadOnly(new[] { new ActorPlatformContact(key, 1, planeY, true) }); return owner.TryDrop(Body(), 0, now, world, world); }
        static World Query(IActorPlatforms owner) => new World { Scope = owner.Scope };
        [Test] public void ContractsAndOwnerAreEngineFreeImmutableAndHaveNoColliderAuthority()
        {
            foreach (var type in new[] { typeof(ActorPlatformKey), typeof(ActorPlatformContact), typeof(ActorPlatformObservation), typeof(ActorIgnoredPlatform), typeof(ActorDropMotion), typeof(ActorPlatformSnapshot) }) Assert.That(type.GetProperties().Any(p => p.GetSetMethod(true) != null), Is.False);
            foreach (var type in new[] { typeof(IActorPlatforms), typeof(ActorPlatformState) }) Assert.That(type.Assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("UnityEngine") || a.Name.StartsWith("UnityEditor") || a.Name == "Deviant.Combat" || a.Name.StartsWith("Deviant.World")), Is.False);
        }
        [Test] public void DropUsesTheFirstStrictUpNormalAndPublishesOneAbsoluteDeadline()
        {
            var owner = new ActorPlatformState(); var world = Query(owner); var rejected = Key(owner); var accepted = Key(owner); var later = Key(owner);
            world.Contacts = Array.AsReadOnly(new[] { new ActorPlatformContact(rejected, .8f, -3, true), new ActorPlatformContact(accepted, 1, .02f, true), new ActorPlatformContact(later, 1, 9, true) });
            var result = owner.TryDrop(Body(), 0, .1f, world, world); var request = world.PlatformQueries.Single(); Assert.That(request.X, Is.EqualTo(1.75f)); Assert.That(request.Y, Is.EqualTo(.03f)); Assert.That(request.Width, Is.EqualTo(.92f)); Assert.That(request.Height, Is.EqualTo(.04f)); Assert.That(request.Distance, Is.EqualTo(.08f));
            Assert.That(result.Applied, Is.True); Assert.That(result.DeltaY, Is.EqualTo(-.08f)); Assert.That(result.VelocityY, Is.EqualTo(-2)); Assert.That(owner.Read().Entries.Single().Key, Is.EqualTo(accepted)); Assert.That(owner.Read().Entries.Single().Until, Is.EqualTo(.35f));
            Assert.That(owner.Read().Entries.Single().PlaneY, Is.EqualTo(.02f), "Only the selected contact's original hit plane is retained.");
        }
        [Test] public void NoGroundOrCandidateCannotInventADrop()
        {
            var owner = new ActorPlatformState(); var world = Query(owner); Assert.That(owner.TryDrop(Body(), .051f, 0, world, world).Applied, Is.False); Assert.That(world.PlatformQueries, Is.Empty);
            world.Ground = Array.AsReadOnly(Array.Empty<ActorSurfaceContact>()); Assert.That(Drop(owner, world, Key(owner)).Applied, Is.False); Assert.That(world.PlatformQueries, Is.Empty); Assert.That(owner.Read().Entries, Is.Empty);
            world.Ground = Array.AsReadOnly(new[] { new ActorSurfaceContact(.01f, 0, 1, 0, true, false) }); world.Contacts = Array.AsReadOnly(Array.Empty<ActorPlatformContact>()); Assert.That(owner.TryDrop(Body(), 0, 0, world, world).Applied, Is.False);
        }
        [Test] public void ACurrentSolidRejectsTheWholeDownwardRequestWithoutPublishingIgnore()
        {
            var owner = new ActorPlatformState(); var world = Query(owner); world.Solids = Array.AsReadOnly(new[] { new ActorSurfaceContact(.04f, 0, 1, 0, false, true) });
            Assert.That(Drop(owner, world, Key(owner)).Applied, Is.False, "A previous ignore fact cannot grant solid passage."); Assert.That(owner.Read().Entries, Is.Empty); Assert.That(world.BodyQueries.Last().Distance, Is.EqualTo(.083f));
        }
        [TestCase(false)] [TestCase(true)] public void NonDroppableSupportRejectsOverlappingDropRegardlessOfQueryOrder(bool blockedFirst)
        {
            var owner = new ActorPlatformState(); var world = Query(owner);
            var allowed = new ActorPlatformContact(Key(owner), 1, 0, true);
            var blocked = new ActorPlatformContact(Key(owner), 1, 0, false);
            world.Contacts = Array.AsReadOnly(blockedFirst ? new[] { blocked, allowed } : new[] { allowed, blocked });
            var result = owner.TryDrop(Body(), 0, 0, world, world);
            Assert.That(result.Applied, Is.False); Assert.That(result.DeltaY, Is.Zero); Assert.That(owner.Read().Entries, Is.Empty);
            world.Contacts = Array.AsReadOnly(new[] { allowed });
            Assert.That(owner.TryDrop(Body(), 0, 0, world, world).Applied, Is.True, "Rejected support cannot leak an ignore or poison the next legal drop.");
        }
        [Test] public void RestorationRequiresBothMinimumTimeAndWholeBodyClearanceAtEquality()
        {
            var owner = new ActorPlatformState(); var world = Query(owner); var key = Key(owner); Drop(owner, world, key, planeY: .02f); world.Observations[key] = new ActorPlatformObservation(key, true);
            owner.Refresh(.249f, -2, 2, world); Assert.That(owner.Contains(key), Is.True);
            owner.Refresh(.25f, -1.99999f, 2, world); Assert.That(owner.Contains(key), Is.True);
            owner.Refresh(.25f, -2, 2, world); Assert.That(owner.Contains(key), Is.False);
        }
        [Test] public void JumpingBackAboveALowPlatformRestoresCollisionAfterTheMinimumTime()
        {
            var owner = new ActorPlatformState(); var world = Query(owner); var key = Key(owner);
            Drop(owner, world, key, planeY: 2); world.Observations[key] = new ActorPlatformObservation(key, true);
            owner.Refresh(.5f, 0, 2, world);
            Assert.That(owner.Contains(key), Is.True, "A 2u-high platform cannot obtain whole-body clearance above its floor.");
            owner.Refresh(.5f, 1.99f, 2, world);
            Assert.That(owner.Contains(key), Is.True, "Keep ignoring while the feet are still below the platform.");
            owner.Refresh(.5f, 2.03f, 2, world);
            Assert.That(owner.Contains(key), Is.False, "Once the feet clear the top, a later fall must land on it again.");
        }
        [Test] public void ReturningAboveThePlatformDoesNotBypassTheMinimumDropTime()
        {
            var owner = new ActorPlatformState(); var world = Query(owner); var key = Key(owner);
            Drop(owner, world, key, planeY: 2); world.Observations[key] = new ActorPlatformObservation(key, true);
            owner.Refresh(.249f, 3, 2, world); Assert.That(owner.Contains(key), Is.True);
            owner.Refresh(.25f, 3, 2, world); Assert.That(owner.Contains(key), Is.False);
        }
        [Test] public void ReadsDoNotExpireAndRepeatDropsReplaceOnlyTheirOwnDeadlineAndPlane()
        {
            var owner = new ActorPlatformState(); var world = Query(owner); var first = Key(owner); var second = Key(owner); Drop(owner, world, first, planeY: .02f); var before = owner.Read(); Drop(owner, world, second, .1f, -7); Drop(owner, world, first, .2f, .5f);
            Assert.That(owner.Read().Entries.Count, Is.EqualTo(2)); Assert.That(owner.Read().Entries.Single(e => e.Key.Equals(first)).Until, Is.EqualTo(.45f)); Assert.That(before.Entries.Count, Is.EqualTo(1)); Assert.That(before.Entries[0].Until, Is.EqualTo(.25f));
            Assert.That(owner.Read().Entries.Single(e => e.Key.Equals(first)).PlaneY, Is.EqualTo(.5f)); Assert.That(before.Entries[0].PlaneY, Is.EqualTo(.02f));
            Assert.That(owner.Read().Entries.Single(e => e.Key.Equals(second)).PlaneY, Is.EqualTo(-7), "A repeated drop does not replace another platform's plane.");
            Assert.Throws<NotSupportedException>(() => ((IList<ActorIgnoredPlatform>)before.Entries)[0] = default); owner.Reset(); Assert.That(before.Entries.Count, Is.EqualTo(1)); Assert.That(owner.Read().Entries, Is.Empty); Assert.That(owner.Scope, Is.EqualTo(before.Scope));
        }
        [Test] public void DestroyedObservationsReleaseBeforeDeadlineButLiveObservationsCannotReplaceTheOriginalHitPlane()
        {
            var owner = new ActorPlatformState(); var world = Query(owner); var dead = Key(owner); var live = Key(owner);
            Drop(owner, world, dead); Drop(owner, world, live, planeY: .02f); world.Observations[live] = new ActorPlatformObservation(live, true);
            owner.Refresh(.1f, 0, 2, world); Assert.That(owner.Contains(dead), Is.False); Assert.That(owner.Contains(live), Is.True);
            world.Contacts = Array.AsReadOnly(new[] { new ActorPlatformContact(live, 1, 100, true) });
            owner.Refresh(.25f, 0, 2, world); Assert.That(owner.Contains(live), Is.True, "A later contact plane does not change a claimed drop.");
            owner.Refresh(.25f, -2, 2, world); Assert.That(owner.Contains(live), Is.False);
        }
        [Test] public void FailedObservationOrReentryCannotPartiallyReleaseOrResetState()
        {
            var owner = new ActorPlatformState(); var world = Query(owner); var first = Key(owner); var second = Key(owner); Drop(owner, world, first); Drop(owner, world, second); int count = 0;
            world.OnObserve = key => { if (++count == 2) throw new InvalidOperationException("fixture failure"); return new ActorPlatformObservation(key, false); };
            Assert.Throws<InvalidOperationException>(() => owner.Refresh(1, -2, 2, world)); Assert.That(owner.Read().Entries.Count, Is.EqualTo(2));
            world.OnObserve = key => { owner.Reset(); return new ActorPlatformObservation(key, false); }; Assert.Throws<InvalidOperationException>(() => owner.Refresh(1, -2, 2, world)); Assert.That(owner.Read().Entries.Count, Is.EqualTo(2)); owner.Reset(); Assert.That(owner.Read().Entries, Is.Empty);
        }
        [Test] public void ForeignOrInvalidFactsAndInputsLeaveAcceptedStateIntact()
        {
            var owner = new ActorPlatformState(); var world = Query(owner); var key = Key(owner); Drop(owner, world, key);
            Assert.Throws<ArgumentException>(() => owner.Refresh(1, -2, 2, new World { Scope = Guid.NewGuid() })); Assert.That(owner.Contains(default), Is.False);
            world.Contacts = Array.AsReadOnly(new[] { new ActorPlatformContact(new ActorPlatformKey(Guid.NewGuid(), Guid.NewGuid()), 1, 0, true) }); Assert.Throws<InvalidOperationException>(() => owner.TryDrop(Body(), 0, 0, world, world));
            Assert.Throws<ArgumentOutOfRangeException>(() => owner.Refresh(1, float.MaxValue, float.MaxValue, world)); Assert.Throws<ArgumentOutOfRangeException>(() => owner.TryDrop(Body(), 0, float.NaN, world, world)); Assert.That(owner.Read().Entries.Count, Is.EqualTo(1));
            Assert.Throws<ArgumentException>(() => new ActorPlatformContact(key, 1, float.NaN, true));
            Assert.Throws<ArgumentException>(() => new ActorIgnoredPlatform(key, 1, float.PositiveInfinity));
        }
    }
}
