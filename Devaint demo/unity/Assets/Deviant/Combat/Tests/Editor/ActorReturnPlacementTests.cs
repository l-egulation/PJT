using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class ActorReturnPlacementTests
    {
        sealed class World : IActorPlacementQuery
        {
            public readonly List<ActorBoxCast> Casts = new List<ActorBoxCast>();
            public readonly List<ActorOverlapBox> Overlaps = new List<ActorOverlapBox>();
            public IReadOnlyList<ActorSurfaceContact> Hits = Array.AsReadOnly(Array.Empty<ActorSurfaceContact>());
            public Func<ActorOverlapBox, IReadOnlyList<ActorSurfaceBounds>> Lookup = _ => Array.AsReadOnly(Array.Empty<ActorSurfaceBounds>());
            public IReadOnlyList<ActorSurfaceContact> BoxCast(ActorBoxCast request) { Casts.Add(request); return Hits; }
            public IReadOnlyList<ActorSurfaceBounds> OverlapBox(ActorOverlapBox request) { Overlaps.Add(request); return Lookup(request); }
            public IReadOnlyList<ActorRayContact> Raycast(ActorRayCast request) => throw new InvalidOperationException("Return placement has no floor-ray policy.");
        }
        static ActorBodyFrame Body(float x = 0) => new ActorBodyFrame(x, 0, 1, 2, .25f, 1, 1);
        static IReadOnlyList<ActorSurfaceBounds> Bounds(params ActorSurfaceBounds[] values) => Array.AsReadOnly(values);
        static ActorSurfaceBounds Block(float minX = .2f, float minY = 0, float maxX = .3f, float maxY = 2, bool oneWay = false) => new ActorSurfaceBounds(minX, minY, 0, maxX, maxY, 0, oneWay);
        [Test] public void PoseAndPlacementAreImmutableAndHaveNoEngineOrSceneReference()
        {
            Assert.That(typeof(ActorPose).GetProperties().Any(p => p.GetSetMethod(true) != null), Is.False);
            foreach (var type in new[] { typeof(ActorPose), typeof(ActorReturnPlacement) }) Assert.That(type.Assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("UnityEngine") || a.Name.StartsWith("UnityEditor") || a.Name == "Deviant.Combat" || a.Name.StartsWith("Deviant.World")), Is.False);
        }
        [Test] public void AdjacentPoseUsesBothBodyWidthsAndTheDestinationFacingOffset()
        {
            var target = new ActorBodyFrame(10, 4, 2, 2, .5f, 1, 1);
            var behind = ActorReturnPlacement.AdjacentTo(Body(), target, -1, 1, .25f); Assert.That(behind.X, Is.EqualTo(8.5f)); Assert.That(behind.Y, Is.EqualTo(4)); Assert.That(behind.Facing, Is.EqualTo(1));
            var front = ActorReturnPlacement.AdjacentTo(Body(), target, 1, -1, .25f); Assert.That(front.X, Is.EqualTo(12.5f)); Assert.That(front.Facing, Is.EqualTo(-1));
        }
        [Test] public void DestinationObstructionPrecedesEvenATinyReachSweep()
        {
            var world = new World { Lookup = _ => Bounds(Block()) }; Assert.That(ActorReturnPlacement.CanReach(Body(), new ActorPose(0, 0, 1), world), Is.False); Assert.That(world.Casts, Is.Empty);
            world.Lookup = _ => Bounds(); Assert.That(ActorReturnPlacement.CanReach(Body(), new ActorPose(.0005f, 0, 1), world), Is.True); Assert.That(world.Casts, Is.Empty);
        }
        [Test] public void ReachUsesTheWholeBodyCenterDeltaAndExistingInnerSkin()
        {
            var world = new World(); Assert.That(ActorReturnPlacement.CanReach(Body(), new ActorPose(3, 4, 1), world), Is.True); var cast = world.Casts.Single();
            Assert.That(cast.X, Is.EqualTo(.25f)); Assert.That(cast.Y, Is.EqualTo(1)); Assert.That(cast.Width, Is.EqualTo(.985f)); Assert.That(cast.Height, Is.EqualTo(1.985f)); Assert.That(cast.DirectionX, Is.EqualTo(.6f)); Assert.That(cast.DirectionY, Is.EqualTo(.8f)); Assert.That(cast.Distance, Is.EqualTo(5));
        }
        [Test] public void ReachKeepsInitialContactEndpointAndOneWayBoundaryPolicies()
        {
            var world = new World { Hits = Array.AsReadOnly(new[] { new ActorSurfaceContact(.001f, 0, 0, 0, false, false), new ActorSurfaceContact(5, 0, 0, 0, false, false), new ActorSurfaceContact(2, 0, 0, 0, true, false) }) };
            Assert.That(ActorReturnPlacement.CanReach(Body(), new ActorPose(3, 4, 1), world), Is.True);
            world.Hits = Array.AsReadOnly(new[] { new ActorSurfaceContact(2, 0, 0, 0, false, true) }); Assert.That(ActorReturnPlacement.CanReach(Body(), new ActorPose(3, 4, 1), world), Is.False, "An ignore flag cannot permit a solid crossing.");
        }
        [Test] public void ReachableOriginWinsBeforeAnyNeighborhoodSearch()
        {
            var world = new World(); var origin = new ActorPose(.125f, 0, -1); Assert.That(ActorReturnPlacement.TryNearOrigin(Body(2), origin, world, out var result), Is.True); Assert.That(result.X, Is.EqualTo(.125f)); Assert.That(result.Facing, Is.EqualTo(-1)); Assert.That(world.Overlaps.Count, Is.EqualTo(1));
        }
        [Test] public void EqualDistanceCandidatesUseXOrderAndKeepTheOneUnitNeighborhood()
        {
            var world = new World { Lookup = request => request.Width > 2 || request.X == .25f && request.Y == 1 ? Bounds(Block()) : Bounds() };
            Assert.That(ActorReturnPlacement.TryNearOrigin(Body(2), new ActorPose(0, 0, 1), world, out var result), Is.True); Assert.That(result.X, Is.EqualTo(-.56f).Within(.000001f)); Assert.That(result.Y, Is.Zero); Assert.That(world.Overlaps[1].Width, Is.EqualTo(3)); Assert.That(world.Overlaps[1].Height, Is.EqualTo(4));
            world.Lookup = _ => Bounds(Block(-3, -3, 3, 3)); var origin = new ActorPose(0, 0, 1); Assert.That(ActorReturnPlacement.TryNearOrigin(Body(2), origin, world, out result), Is.False); Assert.That(result.X, Is.EqualTo(origin.X)); Assert.That(result.Y, Is.EqualTo(origin.Y));
        }
        [Test] public void InvalidOrMissingGeometryCannotInventAPlacement()
        {
            Assert.Throws<ArgumentException>(() => new ActorPose(float.NaN, 0, 1)); Assert.Throws<ArgumentException>(() => ActorReturnPlacement.CanReach(Body(), default, new World())); Assert.Throws<ArgumentNullException>(() => ActorReturnPlacement.CanReach(Body(), new ActorPose(0, 0, 1), null));
            Assert.Throws<InvalidOperationException>(() => ActorReturnPlacement.CanReach(Body(), new ActorPose(1, 0, 1), new World { Hits = null })); Assert.Throws<InvalidOperationException>(() => ActorReturnPlacement.CanReach(Body(), new ActorPose(0, 0, 1), new World { Lookup = _ => null }));
        }
    }
}
