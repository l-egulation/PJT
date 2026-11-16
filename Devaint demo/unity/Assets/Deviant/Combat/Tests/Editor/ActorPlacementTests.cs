using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class ActorPlacementTests
    {
        [Test] public void WallCrossingTeleportSkipsThePathButStillRequiresAClearEndpoint()
        {
            var world = new World { CastHits = Array.AsReadOnly(new[] { new ActorSurfaceContact(1, 0, 0, 0, false, false) }) };
            var result = ActorPlacementResolver.TeleportToward(Body(), 3, 4, world, true);
            Assert.That(result.Applied, Is.True); Assert.That(result.X, Is.EqualTo(3)); Assert.That(result.Y, Is.EqualTo(4));
            Assert.That(world.Casts, Is.Empty); Assert.That(world.Overlaps.Count, Is.EqualTo(1));
            world.Bounds = Array.AsReadOnly(new[] { Box() });
            result = ActorPlacementResolver.TeleportToward(Body(), 1, 0, world, true);
            Assert.That(result.Applied, Is.False); Assert.That(result.X, Is.Zero);
        }
        sealed class World : IActorPlacementQuery
        {
            public IReadOnlyList<ActorSurfaceContact> CastHits = Array.AsReadOnly(Array.Empty<ActorSurfaceContact>());
            public IReadOnlyList<ActorSurfaceBounds> Bounds = Array.AsReadOnly(Array.Empty<ActorSurfaceBounds>());
            public readonly Queue<IReadOnlyList<ActorRayContact>> Floors = new Queue<IReadOnlyList<ActorRayContact>>();
            public readonly List<ActorBoxCast> Casts = new List<ActorBoxCast>();
            public readonly List<ActorOverlapBox> Overlaps = new List<ActorOverlapBox>();
            public readonly List<ActorRayCast> Rays = new List<ActorRayCast>();
            public IReadOnlyList<ActorSurfaceContact> BoxCast(ActorBoxCast request) { Casts.Add(request); return CastHits; }
            public IReadOnlyList<ActorSurfaceBounds> OverlapBox(ActorOverlapBox request) { Overlaps.Add(request); return Bounds; }
            public IReadOnlyList<ActorRayContact> Raycast(ActorRayCast request) { Rays.Add(request); return Floors.Count == 0 ? Array.AsReadOnly(Array.Empty<ActorRayContact>()) : Floors.Dequeue(); }
        }
        static ActorBodyFrame Body(float y = 0) => new ActorBodyFrame(0, y, 1, 2, .25f, 1, 1);
        static ActorSurfaceBounds Box(float minX = -1, float maxY = 2, float minZ = 0, float maxZ = 0, bool oneWay = false) => new ActorSurfaceBounds(minX, -1, minZ, 2, maxY, maxZ, oneWay);
        static World Ground(float y = 0) => new World { CastHits = Array.AsReadOnly(new[] { new ActorSurfaceContact(.01f, 0, 1, y, false, false) }) };
        static void Ray(World world, params ActorRayContact[] hits) => world.Floors.Enqueue(Array.AsReadOnly(hits));
        static ActorRayContact Floor(float y = 0, float normal = 1, bool ignored = false) => new ActorRayContact(y, normal, ignored);
        [Test] public void PlacementContractsAndImplementationStayImmutableAndEngineFree()
        {
            foreach (var type in new[] { typeof(ActorSurfaceBounds), typeof(ActorRayCast), typeof(ActorRayContact), typeof(ActorOverlapBox), typeof(ActorTeleportMotion) }) Assert.That(type.GetProperties().Any(p => p.GetSetMethod(true) != null), Is.False);
            foreach (var type in new[] { typeof(IActorPlacementQuery), typeof(ActorPlacementResolver) }) Assert.That(type.Assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("UnityEngine") || a.Name.StartsWith("UnityEditor") || a.Name == "Deviant.Combat" || a.Name.StartsWith("Deviant.World") || a.Name.StartsWith("Deviant.UI")), Is.False);
        }
        [Test] public void ObstructionUsesTheRequestedPoseAndFacingWithTheExistingInnerBox()
        {
            var world = new World(); Assert.That(ActorPlacementResolver.Blocked(Body(), 2, 3, -1, world), Is.False); var request = world.Overlaps.Single();
            Assert.That(request.X, Is.EqualTo(1.75f)); Assert.That(request.Y, Is.EqualTo(4)); Assert.That(request.Width, Is.EqualTo(.985f)); Assert.That(request.Height, Is.EqualTo(1.985f));
            Assert.That(ActorPlacementResolver.Blocked(Body(), 2, 3, 0, world), Is.False); Assert.That(world.Overlaps.Last().X, Is.EqualTo(2), "Preserve the existing explicit-direction API, including an unshifted test pose.");
        }
        [Test] public void ObstructionRechecksExactBoundsFootThresholdZAndOneWayFacts()
        {
            var world = new World { Bounds = Array.AsReadOnly(new[] { Box(maxY: .02f), Box(minZ: 1, maxZ: 2), Box(oneWay: true), Box(minX: .75f) }) };
            Assert.That(ActorPlacementResolver.Blocked(Body(), 0, 0, 1, world), Is.False, "Native contact margin alone is not an obstruction.");
            world.Bounds = Array.AsReadOnly(new[] { Box(maxY: .020001f) }); Assert.That(ActorPlacementResolver.Blocked(Body(), 0, 0, 1, world), Is.True);
        }
        [Test] public void SafeFloorRequiresThreeFullBodySupportsAndUsesTheirFirstHeight()
        {
            var world = Ground(10); for (int i = 0; i < 3; i++) Ray(world, Floor(10));
            Assert.That(ActorPlacementResolver.ProbeSafeFloor(Body(10), 0, world, out float height), Is.True); Assert.That(height, Is.EqualTo(10)); Assert.That(world.Rays.Select(r => r.X), Is.EqualTo(new[] { -.225f, .25f, .725f }));
            Assert.That(world.Rays.All(r => r.Y == 10.04f && r.DirectionX == 0 && r.DirectionY == -1 && r.Distance == .10f), Is.True);
        }
        [Test] public void SafeFloorSkipsIgnoredAndSlopedHitsButNeverInventsMissingSupport()
        {
            var world = Ground(); for (int i = 0; i < 3; i++) Ray(world, Floor(4, ignored: true), Floor(4, normal: .98f), Floor(0, normal: .99f), Floor(4));
            Assert.That(ActorPlacementResolver.ProbeSafeFloor(Body(), 0, world, out _), Is.True, "Keep first eligible native hit and accept the exact .99 normal boundary.");
            var missing = Ground(); Assert.That(ActorPlacementResolver.ProbeSafeFloor(Body(), 0, missing, out float height), Is.False); Assert.That(height, Is.Zero); Assert.That(missing.Rays.Count, Is.EqualTo(1));
            var rising = Ground(); Assert.That(ActorPlacementResolver.ProbeSafeFloor(Body(), .051f, rising, out _), Is.False); Assert.That(rising.Casts, Is.Empty); Assert.That(rising.Rays, Is.Empty);
        }
        [Test] public void SafeFloorKeepsFlatnessBoundaryAndReturnsNoPartialHeight()
        {
            var world = Ground(); Ray(world, Floor()); Ray(world, Floor(.02f)); Ray(world, Floor()); Assert.That(ActorPlacementResolver.ProbeSafeFloor(Body(), 0, world, out _), Is.True);
            var uneven = Ground(); Ray(uneven, Floor()); Ray(uneven, Floor(.02001f)); Assert.That(ActorPlacementResolver.ProbeSafeFloor(Body(), 0, uneven, out float height), Is.False); Assert.That(height, Is.Zero); Assert.That(uneven.Rays.Count, Is.EqualTo(2));
        }
        [Test] public void TeleportUsesAnUnchangedDiagonalSweepAndEndpointOverlap()
        {
            var world = new World(); var result = ActorPlacementResolver.TeleportToward(Body(), 3, 4, world); var cast = world.Casts.Single();
            Assert.That(cast.X, Is.EqualTo(.25f)); Assert.That(cast.Y, Is.EqualTo(1)); Assert.That(cast.Width, Is.EqualTo(.995f)); Assert.That(cast.Height, Is.EqualTo(1.995f)); Assert.That(cast.DirectionX, Is.EqualTo(.6f)); Assert.That(cast.DirectionY, Is.EqualTo(.8f)); Assert.That(cast.Distance, Is.EqualTo(5));
            Assert.That(result.Applied, Is.True); Assert.That(result.X, Is.EqualTo(3)); Assert.That(result.Y, Is.EqualTo(4)); Assert.That(world.Overlaps.Single().X, Is.EqualTo(3.25f));
        }
        [Test] public void TeleportPreservesInitialOverlapOneWayAndIgnoredSolidPolicies()
        {
            var world = new World { CastHits = Array.AsReadOnly(new[] { new ActorSurfaceContact(0, 0, 0, 0, false, false), new ActorSurfaceContact(.001f, 0, 0, 0, false, false), new ActorSurfaceContact(.1f, 0, 0, 0, true, false), new ActorSurfaceContact(1, 0, 0, 0, false, true) }) };
            var result = ActorPlacementResolver.TeleportToward(Body(), 3, 4, world); Assert.That(result.Applied, Is.True); Assert.That(result.X, Is.EqualTo(.594f).Within(.0000001f)); Assert.That(result.Y, Is.EqualTo(.792f).Within(.0000001f));
        }
        [Test] public void BlockedTeleportKeepsCurrentPoseAndTinyRequestsDoNotQuery()
        {
            var blocked = new World { Bounds = Array.AsReadOnly(new[] { Box() }) }; var result = ActorPlacementResolver.TeleportToward(Body(), 1, 0, blocked); Assert.That(result.Applied, Is.False); Assert.That(result.Skipped, Is.False); Assert.That(result.X, Is.Zero);
            var tiny = new World(); var skipped = ActorPlacementResolver.TeleportToward(Body(), .0005f, 0, tiny); Assert.That(skipped.Skipped, Is.True); Assert.That(tiny.Casts, Is.Empty); Assert.That(tiny.Overlaps, Is.Empty);
        }
        [Test] public void InvalidOrMissingFactsFailBeforeAPlacementResult()
        {
            Assert.Throws<ArgumentNullException>(() => ActorPlacementResolver.Blocked(Body(), 0, 0, 1, null)); Assert.Throws<InvalidOperationException>(() => ActorPlacementResolver.Blocked(Body(), 0, 0, 1, new World { Bounds = null }));
            Assert.Throws<ArgumentOutOfRangeException>(() => ActorPlacementResolver.TeleportToward(Body(), float.PositiveInfinity, 0, new World())); Assert.Throws<ArgumentException>(() => new ActorRayCast(0, 0, 0, 0, 1)); Assert.Throws<ArgumentException>(() => new ActorSurfaceBounds(2, 0, 0, 1, 1, 0, false));
            var missing = Ground(); missing.Floors.Enqueue(null); Assert.Throws<InvalidOperationException>(() => ActorPlacementResolver.ProbeSafeFloor(Body(), 0, missing, out _));
        }
    }
}
