using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using Deviant.Definitions;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class PlayerProjectileSweepExecutionTests
    {
        sealed class Actor
        {
            public readonly int Id;
            public bool Exists = true, Player, Decoy, Accept = true;
            public float X, Y, Health = 100;
            public int Attempts;
            public Actor(int id, float x = 0) { Id = id; X = x; }
        }
        sealed class Collider
        {
            public readonly string Name;
            public Actor Actor;
            public bool Surface, OneWay, RegionExists;
            public float X, Radius = 0;
            public float? ReportedDistance;
            public Collider(string name, float x, Actor actor = null)
            { Name = name; X = x; Actor = actor; RegionExists = actor != null; }
        }
        struct Cast
        {
            public Collider Collider;
            public float Distance;
        }
        sealed class Shot : IPlayerProjectileExecutionHost
        {
            public readonly PlayerProjectileExecution Execution;
            public readonly List<string> Effects = new List<string>();
            public readonly List<float> CastStarts = new List<float>(), CastLengths = new List<float>();
            public readonly List<float> RawDistances = new List<float>(), ContactAges = new List<float>(), ContactPositions = new List<float>();
            public readonly List<float> DistanceOrigins = new List<float>();
            public List<Collider> Geometry = new List<Collider>();
            public Actor Owner = new Actor(100) { Player = true };
            public float X, Speed = 10, Damage = 5;
            public int OverlapQueries, CastQueries, Removals;
            public Action<Actor> DuringReceive;
            public Action DuringRemove, DuringCastQuery, AfterFirstDistance;
            public Shot(string type = "shuriken", bool pierce = false, float interval = 0, float distance = 20)
                => Execution = new PlayerProjectileExecution(new RangedProjectileDefinition(new RangedData
                { type = type, radius = .1f, distance = distance, pierce = pierce, hitInterval = interval }));
            public void SweepTo(float destination)
            { var host = new SweepHost(this, destination); PlayerProjectileSweepExecution.Sweep(ref host); }
            public bool Valid(Collider collider)
            { var host = new ContactHost(this, collider); return PlayerProjectileSweepExecution.IsValid(ref host); }
            public static int Priority(Collider collider)
            { var host = new ContactHost(null, collider); return PlayerProjectileSweepExecution.Priority(ref host); }
            public int CompareOverlap(Collider first, Collider second)
            { var host = new OverlapHost(this, first, second); return PlayerProjectileSweepExecution.CompareOverlap(ref host); }
            public bool Resolve(Collider collider, float age = -1)
            { var host = new HitHost(this, collider); return PlayerProjectileHitExecution.Resolve(age, ref host); }
            public bool Receive(Actor actor)
            {
                actor.Attempts++; Effects.Add("receive:" + actor.Id);
                if (!actor.Accept || actor.Health <= 0) return false;
                actor.Health = Math.Max(0, actor.Health - Damage); DuringReceive?.Invoke(actor); return true;
            }
            public void Release() => Execution.Release(this);
            public void Remove() { Removals++; Effects.Add("remove"); DuringRemove?.Invoke(); }
            public void SweepByTime(float duration) => SweepTo(X + Speed * duration);
            public void SweepByDistance(float distance) => SweepTo(X + distance);
            public void SweepCurrent() => SweepTo(X);
            public void Rotate(float duration) { }
        }
        struct ContactHost : IPlayerProjectileContactHost
        {
            readonly Shot shot;
            readonly Collider collider;
            public ContactHost(Shot shot, Collider collider) { this.shot = shot; this.collider = collider; }
            public bool FindSurface() => collider.Surface;
            public bool IgnoresTerrain => shot.Execution.Definition.IgnoresTerrain;
            public bool SurfaceIsOneWay => collider.OneWay;
            public bool FindRegion() => collider.RegionExists;
            public bool TargetExists => collider.Actor != null && collider.Actor.Exists;
            public bool TargetIsOwner => ReferenceEquals(collider.Actor, shot.Owner);
            public bool TargetIsPlayer => collider.Actor.Player;
            public bool TargetIsDecoy => collider.Actor.Decoy;
            public bool TargetDead => collider.Actor.Health <= 0;
            public int TargetId => collider.Actor.Id;
        }
        struct OverlapHost : IPlayerProjectileOverlapHost
        {
            readonly Shot shot;
            readonly Collider first, second;
            Actor firstActor, secondActor;
            public OverlapHost(Shot shot, Collider first, Collider second)
            { this.shot = shot; this.first = first; this.second = second; firstActor = secondActor = null; }
            public string ProjectileType => shot.Execution.Definition.Type;
            public bool FirstValid => shot.Valid(first);
            public bool FirstIsSurface => first.Surface;
            public bool SecondValid => shot.Valid(second);
            public bool SecondIsSurface => second.Surface;
            public void CaptureActors() { firstActor = first.Actor; secondActor = second.Actor; }
            public float FirstSquaredDistance
            {
                get
                {
                    shot.DistanceOrigins.Add(shot.X);
                    float dx = firstActor.X - shot.X, value = dx * dx + firstActor.Y * firstActor.Y;
                    shot.AfterFirstDistance?.Invoke(); return value;
                }
            }
            public float SecondSquaredDistance
            {
                get
                {
                    shot.DistanceOrigins.Add(shot.X);
                    float dx = secondActor.X - shot.X; return dx * dx + secondActor.Y * secondActor.Y;
                }
            }
            public int FirstPriority => Shot.Priority(first);
            public int SecondPriority => Shot.Priority(second);
        }
        struct CastOrderHost : IPlayerProjectileCastOrderHost
        {
            readonly Cast first, second;
            public CastOrderHost(Cast first, Cast second) { this.first = first; this.second = second; }
            public float FirstDistance => first.Distance;
            public float SecondDistance => second.Distance;
            public int FirstPriority => Shot.Priority(first.Collider);
            public int SecondPriority => Shot.Priority(second.Collider);
        }
        static int CompareCasts(Cast first, Cast second)
        { var host = new CastOrderHost(first, second); return PlayerProjectileSweepExecution.CompareCast(ref host); }
        struct SweepHost : IPlayerProjectileSweepHost
        {
            readonly Shot shot;
            readonly float destination;
            float delta, start;
            List<Collider> overlaps;
            List<Cast> casts;
            Cast current;
            public SweepHost(Shot shot, float destination)
            { this.shot = shot; this.destination = destination; delta = start = 0; overlaps = null; casts = null; current = default; }
            public bool Removed => shot.Execution.Removed;
            public int QueryOverlaps()
            {
                shot.OverlapQueries++; overlaps = new List<Collider>();
                foreach (var collider in shot.Geometry)
                    if (Math.Abs(collider.X - shot.X) <= collider.Radius + shot.Execution.Definition.Radius) overlaps.Add(collider);
                return overlaps.Count;
            }
            public void SortOverlaps() => overlaps.Sort(shot.CompareOverlap);
            public bool ResolveOverlap(int index) => shot.Resolve(overlaps[index]);
            public void SnapshotDelta() => delta = destination - shot.X;
            public float DeltaLength => Math.Abs(delta);
            public void CaptureStart() => start = shot.X;
            public int QueryCast(float length)
            {
                shot.CastQueries++; shot.CastStarts.Add(start); shot.CastLengths.Add(length); casts = new List<Cast>();
                foreach (var collider in shot.Geometry)
                {
                    float center = (collider.X - start) * (delta < 0 ? -1 : 1);
                    float radius = collider.Radius + shot.Execution.Definition.Radius;
                    if (center - radius <= length && center + radius >= 0)
                        casts.Add(new Cast { Collider = collider, Distance = collider.ReportedDistance ?? Math.Max(0, center - radius) });
                }
                shot.DuringCastQuery?.Invoke(); return casts.Count;
            }
            public void SortCasts() => casts.Sort(CompareCasts);
            public bool CastValid(int index) { current = casts[index]; return shot.Valid(current.Collider); }
            public float CastDistance => current.Distance;
            public void ApplyContact(float length, float distance) => shot.X = start + delta / length * distance;
            public float ContactAge(float rawDistance)
            {
                float age = shot.Execution.ContactAge(rawDistance, shot.Speed);
                shot.RawDistances.Add(rawDistance); shot.ContactAges.Add(age); return age;
            }
            public bool ResolveCast(float contactAge)
            { shot.ContactPositions.Add(shot.X); return shot.Resolve(current.Collider, contactAge); }
            public void ApplyDestination() => shot.X = destination;
        }
        struct HitHost : IPlayerProjectileHitHost
        {
            readonly Shot shot;
            readonly Collider collider;
            Actor actor;
            public HitHost(Shot shot, Collider collider) { this.shot = shot; this.collider = collider; actor = null; }
            public bool ValidContact => shot.Valid(collider);
            public float BlastRadius => shot.Execution.Definition.BlastRadius;
            public void Detonate() => throw new InvalidOperationException("Not a blast fixture.");
            public bool CaptureHitRegion() { actor = collider.Actor; return collider.RegionExists; }
            public bool TryClaimActor(float contactAge) => shot.Execution.TryClaimActor(actor.Id, shot.Speed, contactAge);
            public bool Receive() => shot.Receive(actor);
            public float ReturnDelay => shot.Execution.Definition.ReturnDelay;
            public bool TargetDead => actor.Health <= 0;
            public bool CaptureReturnPlayer() => throw new InvalidOperationException("Not a return fixture.");
            public void VisitTarget() => throw new InvalidOperationException("Not a return fixture.");
            public bool Pierce => shot.Execution.Definition.Pierce;
            public void Release() => shot.Release();
        }

        [TestCase("owner")] [TestCase("player")] [TestCase("decoy")] [TestCase("dead")]
        [TestCase("missing-actor")] [TestCase("missing-region")] [TestCase("destroyed-actor")]
        public void IneligibleActorsDoNotReceiveOrStopTheFlight(string reason)
        {
            var shot = new Shot(); var actor = new Actor(1); var collider = new Collider(reason, 1, actor);
            if (reason == "owner") shot.Owner = actor;
            if (reason == "player") actor.Player = true;
            if (reason == "decoy") actor.Decoy = true;
            if (reason == "dead") actor.Health = 0;
            if (reason == "missing-actor") collider.Actor = null;
            if (reason == "missing-region") collider.RegionExists = false;
            if (reason == "destroyed-actor") actor.Exists = false;
            float health = actor.Health; shot.Geometry.Add(collider); shot.SweepTo(2);
            Assert.That(actor.Health, Is.EqualTo(health)); Assert.That(actor.Attempts, Is.Zero);
            Assert.That(shot.Execution.Removed, Is.False); Assert.That(shot.X, Is.EqualTo(2));
        }
        [Test] public void SurfaceEligibilityTakesPrecedenceAndOneWayCannotFallThroughToAnEnemy()
        {
            var shot = new Shot(); var actor = new Actor(1);
            var platform = new Collider("one-way-enemy", 1, actor) { Surface = true, OneWay = true };
            shot.Geometry.Add(platform); shot.SweepTo(2);
            Assert.That(actor.Health, Is.EqualTo(100)); Assert.That(actor.Attempts, Is.Zero);
            Assert.That(shot.X, Is.EqualTo(2)); Assert.That(shot.Execution.Removed, Is.False);
            var solid = new Collider("solid", 0) { Surface = true };
            Assert.That(shot.Valid(solid), Is.True); Assert.That(Shot.Priority(solid), Is.EqualTo(int.MinValue));
            Assert.That(Shot.Priority(new Collider("empty", 0)), Is.EqualTo(int.MaxValue));
            actor.Health = 0; Assert.That(Shot.Priority(new Collider("dead", 0, actor)), Is.EqualTo(actor.Id));
        }
        [TestCase("shuriken")] [TestCase("shuriken_special")] [TestCase("shuriken_range")]
        [TestCase("shuriken_move")] [TestCase("shuriken_followup")]
        public void ShurikenOverlapRanksNearestBodyThenActorIdRatherThanColliderDiscovery(string type)
        {
            var shot = new Shot(type); var far = new Actor(1, 3); var nearest = new Actor(9, 1); var tied = new Actor(2, -1);
            shot.Geometry.AddRange(new[] { new Collider("far", 0, far), new Collider("nearest", 0, nearest), new Collider("tie", 0, tied) });
            shot.SweepTo(0);
            Assert.That(tied.Health, Is.EqualTo(95)); Assert.That(nearest.Health, Is.EqualTo(100)); Assert.That(far.Health, Is.EqualTo(100));
            Assert.That(shot.Effects, Is.EqualTo(new[] { "receive:2", "remove" })); Assert.That(shot.CastQueries, Is.Zero);
        }
        [TestCase("shuriken")] [TestCase("shuriken_special")] [TestCase("shuriken_range")]
        [TestCase("shuriken_move")] [TestCase("shuriken_followup")] [TestCase("launcher")]
        public void SolidOverlapStopsBeforeEnemiesEvenOnAZeroLengthSweep(string type)
        {
            var shot = new Shot(type, pierce: true); var actor = new Actor(1);
            shot.Geometry.AddRange(new[] { new Collider("enemy", 0, actor), new Collider("platform", 0) { Surface = true, OneWay = true }, new Collider("wall", 0) { Surface = true } });
            shot.SweepTo(0);
            Assert.That(actor.Health, Is.EqualTo(100)); Assert.That(actor.Attempts, Is.Zero);
            Assert.That(shot.Effects, Is.EqualTo(new[] { "remove" })); Assert.That(shot.CastQueries, Is.Zero);
        }
        [Test] public void OtherProjectileOverlapKeepsActorIdOrderInsteadOfShurikenDistanceOrder()
        {
            var shot = new Shot("launcher"); var far = new Actor(1, 5); var near = new Actor(9, 0);
            shot.Geometry.AddRange(new[] { new Collider("near", 0, near), new Collider("far", 0, far) }); shot.SweepTo(0);
            Assert.That(far.Health, Is.EqualTo(95)); Assert.That(near.Health, Is.EqualTo(100));
        }
        [Test] public void OverlapRankingReadsEachLivePositionAndDoesNotReadDistancesForDifferentRanks()
        {
            var shot = new Shot(); var first = new Collider("first", 0, new Actor(1, 1)); var second = new Collider("second", 0, new Actor(2, 4));
            shot.AfterFirstDistance = () => shot.X = 4;
            Assert.That(shot.CompareOverlap(first, second), Is.GreaterThan(0));
            Assert.That(shot.DistanceOrigins, Is.EqualTo(new[] { 0f, 4f })); shot.DistanceOrigins.Clear();
            Assert.That(shot.CompareOverlap(new Collider("wall", 0) { Surface = true }, first), Is.LessThan(0));
            Assert.That(shot.DistanceOrigins, Is.Empty);
            first.Actor.Health = 0;
            Assert.That(shot.CompareOverlap(first, second), Is.GreaterThan(0)); Assert.That(shot.DistanceOrigins, Is.Empty);
        }
        [Test] public void CastsDamageInDistanceThenIdOrderAndStopAtTheWallBeforeFartherTargets()
        {
            var shot = new Shot(pierce: true); var near = new Actor(7); var tied = new Actor(2); var later = new Actor(9); var beyond = new Actor(1);
            shot.Geometry.AddRange(new[] { new Collider("beyond", 5, beyond), new Collider("later", 3, later), new Collider("wall", 4) { Surface = true },
                new Collider("near", 1, near), new Collider("duplicate", 1.5f, near), new Collider("tie", 3, tied) });
            shot.SweepTo(6);
            Assert.That(shot.Effects, Is.EqualTo(new[] { "receive:7", "receive:2", "receive:9", "remove" }));
            Assert.That(near.Health, Is.EqualTo(95)); Assert.That(near.Attempts, Is.EqualTo(1));
            Assert.That(tied.Health, Is.EqualTo(95)); Assert.That(later.Health, Is.EqualTo(95)); Assert.That(beyond.Health, Is.EqualTo(100));
            Assert.That(shot.X, Is.EqualTo(3.9f).Within(.000001f));
        }
        [Test] public void ExactCastDistanceTieLetsTheWallStopBeforeAnEnemy()
        {
            var shot = new Shot(pierce: true); var actor = new Actor(1);
            shot.Geometry.AddRange(new[] { new Collider("enemy", 1, actor), new Collider("wall", 1) { Surface = true } }); shot.SweepTo(2);
            Assert.That(actor.Health, Is.EqualTo(100)); Assert.That(actor.Attempts, Is.Zero);
            Assert.That(shot.X, Is.EqualTo(.9f).Within(.000001f)); Assert.That(shot.Removals, Is.EqualTo(1));
        }
        [Test] public void DuplicateOverlapAndCastCollidersConsumeOneClaimEvenWhenReceiveRejects()
        {
            var shot = new Shot(pierce: true); var rejected = new Actor(1) { Accept = false }; var next = new Actor(2);
            shot.Geometry.AddRange(new[] { new Collider("initial", 0, rejected), new Collider("overlap-duplicate", 0, rejected),
                new Collider("cast-duplicate", 1, rejected), new Collider("next", 2, next) }); shot.SweepTo(3);
            Assert.That(rejected.Health, Is.EqualTo(100)); Assert.That(rejected.Attempts, Is.EqualTo(1));
            Assert.That(next.Health, Is.EqualTo(95)); Assert.That(shot.X, Is.EqualTo(3)); Assert.That(shot.Execution.Removed, Is.False);
            Assert.That(shot.Effects, Is.EqualTo(new[] { "receive:1", "receive:2" }));
        }
        [Test] public void ARejectedIntervalClaimStillPlacesTheShotBeforeContinuingToTheNextEnemy()
        {
            var shot = new Shot(interval: .25f); var claimed = new Actor(1); var next = new Actor(2);
            Assert.That(shot.Execution.TryClaimActor(claimed.Id, shot.Speed), Is.True);
            shot.Geometry.AddRange(new[] { new Collider("claimed", 1, claimed), new Collider("next", 2, next) }); shot.SweepTo(3);
            Assert.That(claimed.Attempts, Is.Zero); Assert.That(claimed.Health, Is.EqualTo(100)); Assert.That(next.Health, Is.EqualTo(95));
            Assert.That(shot.ContactPositions[0], Is.EqualTo(.9f).Within(.000001f));
            Assert.That(shot.ContactPositions[1], Is.EqualTo(1.9f).Within(.000001f)); Assert.That(shot.X, Is.EqualTo(1.9f).Within(.000001f));
            Assert.That(shot.ContactAges[0], Is.EqualTo(.09f).Within(.000001f)); Assert.That(shot.Removals, Is.EqualTo(1));
        }
        [Test] public void OverlapCallbacksMoveTheCastOriginAndEachContactReadsTheCurrentVelocity()
        {
            var shot = new Shot(pierce: true); var first = new Actor(1); var second = new Actor(2); var third = new Actor(3);
            shot.Geometry.AddRange(new[] { new Collider("first", 0, first), new Collider("second", 5, second), new Collider("third", 5.5f, third) });
            shot.DuringReceive = actor => { if (actor == first) { shot.X = 4; shot.Speed = 2; } else if (actor == second) shot.Speed = 4; };
            shot.SweepTo(6);
            Assert.That(shot.CastStarts, Is.EqualTo(new[] { 4f })); Assert.That(shot.CastLengths, Is.EqualTo(new[] { 2f }));
            Assert.That(first.Health, Is.EqualTo(95)); Assert.That(second.Health, Is.EqualTo(95)); Assert.That(third.Health, Is.EqualTo(95));
            Assert.That(shot.ContactAges[0], Is.EqualTo(.9f / 2).Within(.000001f));
            Assert.That(shot.ContactAges[1], Is.EqualTo(1.4f / 4).Within(.000001f)); Assert.That(shot.X, Is.EqualTo(6));
        }
        [TestCase(false)] [TestCase(true)]
        public void LaterCandidatesUseEligibilityChangedByAnEarlierReceive(bool overlaps)
        {
            var shot = new Shot(pierce: true); var first = new Actor(1); var second = new Actor(2);
            shot.Geometry.AddRange(new[] { new Collider("first", overlaps ? 0 : 1, first), new Collider("second", overlaps ? 0 : 2, second) });
            shot.DuringReceive = actor => { if (actor == first) second.Player = true; }; shot.SweepTo(3);
            Assert.That(first.Health, Is.EqualTo(95)); Assert.That(second.Health, Is.EqualTo(100)); Assert.That(second.Attempts, Is.Zero);
            Assert.That(shot.X, Is.EqualTo(3));
        }
        [TestCase(-2f, 0f)] [TestCase(8f, 2f)]
        public void ContactPositionClampsNativeDistanceButContactAgeUsesTheRawDistance(float rawDistance, float position)
        {
            var shot = new Shot { Speed = 4 }; var actor = new Actor(1);
            shot.Geometry.Add(new Collider("native-distance", 1, actor) { ReportedDistance = rawDistance }); shot.SweepTo(2);
            Assert.That(actor.Health, Is.EqualTo(95)); Assert.That(shot.X, Is.EqualTo(position));
            Assert.That(shot.RawDistances, Is.EqualTo(new[] { rawDistance })); Assert.That(shot.ContactAges, Is.EqualTo(new[] { rawDistance / 4 }));
        }
        [Test] public void RawDistanceCanRejectAnExpiredRecurringContactDespiteAClampedPositionInsideTheFlight()
        {
            var shot = new Shot(interval: .25f, distance: 6) { Speed = 2 }; var actor = new Actor(1);
            shot.Geometry.Add(new Collider("expired-native-contact", 1, actor) { ReportedDistance = 8 }); shot.SweepTo(2);
            Assert.That(actor.Health, Is.EqualTo(100)); Assert.That(actor.Attempts, Is.Zero); Assert.That(shot.ContactAges, Is.EqualTo(new[] { 4f }));
            Assert.That(shot.ContactPositions, Is.EqualTo(new[] { 2f })); Assert.That(shot.X, Is.EqualTo(2)); Assert.That(shot.Execution.Removed, Is.False);
        }
        [Test] public void ACallbackCanAdvanceTheExistingAgeBeforeTheNextCastContactWithoutASweepClock()
        {
            var shot = new Shot(pierce: true, interval: .25f, distance: 6) { Speed = 2 };
            var first = new Actor(1); var second = new Actor(2);
            var geometry = new List<Collider> { new Collider("first", 1, first), new Collider("second", 2, second) };
            shot.Geometry = geometry;
            shot.DuringReceive = actor =>
            {
                if (actor != first) return;
                shot.Geometry = new List<Collider>(); shot.Execution.Step(2.5f, true, shot.Speed, shot); shot.Geometry = geometry;
            };
            shot.SweepTo(3);
            Assert.That(first.Health, Is.EqualTo(95)); Assert.That(second.Health, Is.EqualTo(100)); Assert.That(second.Attempts, Is.Zero);
            Assert.That(shot.ContactAges[0], Is.EqualTo(.45f).Within(.00001f));
            Assert.That(shot.ContactAges[1], Is.GreaterThan(3)); Assert.That(shot.Execution.Age, Is.EqualTo(2.5f).Within(.0001f));
            Assert.That(shot.X, Is.EqualTo(3)); Assert.That(shot.Execution.Removed, Is.False);
        }
        [TestCase(0f)] [TestCase(.0000005f)]
        public void TinyTravelResolvesStartingOverlapsWithoutMovingToTheRequestedDestination(float destination)
        {
            var shot = new Shot(pierce: true); var actor = new Actor(1); shot.Geometry.Add(new Collider("overlap", 0, actor)); shot.SweepTo(destination);
            Assert.That(actor.Health, Is.EqualTo(95)); Assert.That(shot.OverlapQueries, Is.EqualTo(1)); Assert.That(shot.CastQueries, Is.Zero);
            Assert.That(shot.X, Is.Zero); Assert.That(shot.Execution.Removed, Is.False);
        }
        [TestCase(.000001f)] [TestCase(4f)] [TestCase(-4f)]
        public void EmptyFlightsAtOrAboveTheMovementBoundaryReachTheirDestination(float destination)
        {
            var shot = new Shot(); shot.SweepTo(destination);
            Assert.That(shot.X, Is.EqualTo(destination)); Assert.That(shot.OverlapQueries, Is.EqualTo(1)); Assert.That(shot.CastQueries, Is.EqualTo(1));
            Assert.That(shot.Effects, Is.Empty); Assert.That(shot.Execution.Removed, Is.False);
        }
        [Test] public void ReleaseDuringReceiveBlocksNewSweepsButDoesNotAbortEnteredOverlapsCastsOrDestination()
        {
            var shot = new Shot(pierce: true); var first = new Actor(1); var second = new Actor(2); var third = new Actor(3);
            shot.Geometry.AddRange(new[] { new Collider("first", 0, first), new Collider("second", 0, second), new Collider("third", 2, third) });
            shot.DuringReceive = actor => { if (actor == first) shot.Release(); }; shot.DuringRemove = () => shot.SweepTo(100);
            shot.SweepTo(3); shot.SweepTo(9);
            Assert.That(first.Health, Is.EqualTo(95)); Assert.That(second.Health, Is.EqualTo(95)); Assert.That(third.Health, Is.EqualTo(95));
            Assert.That(shot.Effects, Is.EqualTo(new[] { "receive:1", "remove", "receive:2", "receive:3" }));
            Assert.That(shot.X, Is.EqualTo(3)); Assert.That(shot.Removals, Is.EqualTo(1));
            Assert.That(shot.OverlapQueries, Is.EqualTo(1)); Assert.That(shot.CastQueries, Is.EqualTo(1));
        }
        [Test] public void ReentrantSweepsKeepIndependentListsAndOuterDeltaUsesTheNestedFinalPosition()
        {
            var shot = new Shot(pierce: true); var first = new Actor(1); var second = new Actor(2); var nested = new Actor(3); var nestedCast = new Actor(4); var outerCast = new Actor(5);
            var outerGeometry = new List<Collider> { new Collider("first", 0, first), new Collider("second", 0, second), new Collider("outer-cast", 13, outerCast) };
            shot.Geometry = outerGeometry;
            shot.DuringReceive = actor =>
            {
                if (actor != first) return;
                shot.X = 10; shot.Geometry = new List<Collider> { new Collider("nested", 10, nested), new Collider("nested-cast", 11, nestedCast) };
                shot.SweepTo(12); shot.Geometry = outerGeometry;
            };
            shot.SweepTo(14);
            Assert.That(shot.Effects, Is.EqualTo(new[] { "receive:1", "receive:3", "receive:4", "receive:2", "receive:5" }));
            foreach (var actor in new[] { first, second, nested, nestedCast, outerCast })
            { Assert.That(actor.Health, Is.EqualTo(95)); Assert.That(actor.Attempts, Is.EqualTo(1)); }
            Assert.That(shot.CastStarts, Is.EqualTo(new[] { 10f, 12f })); Assert.That(shot.CastLengths, Is.EqualTo(new[] { 2f, 2f }));
            Assert.That(shot.X, Is.EqualTo(14)); Assert.That(shot.OverlapQueries, Is.EqualTo(2));
        }
        [Test] public void ReentrantCastCallbacksKeepTheOuterCastListAndItsAlreadyCapturedStart()
        {
            var shot = new Shot(pierce: true); var first = new Actor(1); var second = new Actor(2); var nested = new Actor(3); var nestedCast = new Actor(4);
            var geometry = new List<Collider> { new Collider("first", 1, first), new Collider("second", 2, second) };
            shot.Geometry = geometry;
            shot.DuringReceive = actor =>
            {
                if (actor != first) return;
                shot.X = 10; shot.Geometry = new List<Collider> { new Collider("nested", 10, nested), new Collider("nested-cast", 11, nestedCast) };
                shot.SweepTo(12); shot.Geometry = geometry;
            };
            shot.SweepTo(3);
            Assert.That(shot.Effects, Is.EqualTo(new[] { "receive:1", "receive:3", "receive:4", "receive:2" }));
            foreach (var actor in new[] { first, second, nested, nestedCast }) Assert.That(actor.Health, Is.EqualTo(95));
            Assert.That(shot.CastStarts, Is.EqualTo(new[] { 0f, 10f })); Assert.That(shot.CastLengths, Is.EqualTo(new[] { 3f, 2f }));
            Assert.That(shot.ContactPositions[3], Is.EqualTo(1.9f).Within(.000001f)); Assert.That(shot.X, Is.EqualTo(3));
        }
        [Test] public void ReceiveExceptionKeepsTheContactPositionDamageAndClaimWithoutFinishingTheFlight()
        {
            var shot = new Shot(pierce: true); var first = new Actor(1); var second = new Actor(2);
            shot.Geometry.AddRange(new[] { new Collider("first", 1, first), new Collider("second", 2, second) });
            shot.DuringReceive = actor => { if (actor == first) throw new InvalidOperationException("receive"); };
            Assert.Throws<InvalidOperationException>(() => shot.SweepTo(3));
            Assert.That(shot.X, Is.EqualTo(.9f).Within(.000001f)); Assert.That(first.Health, Is.EqualTo(95)); Assert.That(second.Health, Is.EqualTo(100));
            Assert.That(shot.Execution.Removed, Is.False); shot.DuringReceive = null; shot.SweepTo(3);
            Assert.That(first.Attempts, Is.EqualTo(1)); Assert.That(second.Health, Is.EqualTo(95)); Assert.That(shot.X, Is.EqualTo(3));
        }
        [Test] public void CastQueryExceptionKeepsTheEarlierOverlapDamageAndCallbackMovement()
        {
            var shot = new Shot(pierce: true); var actor = new Actor(1); shot.Geometry.Add(new Collider("overlap", 0, actor));
            shot.DuringReceive = _ => shot.X = 4; shot.DuringCastQuery = () => throw new InvalidOperationException("cast");
            Assert.Throws<InvalidOperationException>(() => shot.SweepTo(6));
            Assert.That(actor.Health, Is.EqualTo(95)); Assert.That(shot.X, Is.EqualTo(4)); Assert.That(shot.Execution.Removed, Is.False);
            Assert.That(shot.CastStarts, Is.EqualTo(new[] { 4f })); Assert.That(shot.CastLengths, Is.EqualTo(new[] { 2f }));
        }
    }
}
