using System.Linq;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    // Segment graph rules shared with tools/combat/monster_pursuit_tuner/index.html.
    public class EnemyPursuitGraphTests
    {
        [TestCase(-1)] [TestCase(1)]
        public void TargetFacingKeepsItsDirectionInsideFourPixelsAndTurnsOutside(int facing)
        {
            var settings=EnemyPursuitSettings.Load();
            Assert.That(settings.targetFacingDeadzone,Is.EqualTo(4f/32));
            foreach(float pixels in new[]{-4f,-3f,0f,3f,4f})
                Assert.That(settings.FacingToward(pixels/32,facing),Is.EqualTo(facing),"pixel delta="+pixels);
            Assert.That(settings.FacingToward(-5f/32,facing),Is.EqualTo(-1));
            Assert.That(settings.FacingToward(5f/32,facing),Is.EqualTo(1));
        }
        static EnemyPursuitSettings Settings() => new EnemyPursuitSettings
        {
            schema = "enemy-pursuit-v1", appliesTo = new[] { "K4" }, chaseSpeedScale = 1.6f, jumpHeight = 3.2f, jumpAirSpeed = 6,
            jumpWindupSeconds = .12f, maxDropHeight = 16, replanSeconds = .25f, detectionScale = 1, dropThrough = true, walkOffLedges = true,
        };
        static PursuitTerrain.Box Box(float x, float y, float w, float h, bool oneWay = false)
            => new PursuitTerrain.Box { x0 = x, x1 = x + w, y0 = y, y1 = y + h, oneWay = oneWay };
        static PursuitGraph Graph(float height, params PursuitTerrain.Box[] boxes)
        {
            var terrain = new PursuitTerrain(); terrain.boxes.AddRange(boxes);
            var s = Settings(); return terrain.Graph(.5f, height, 6, s.jumpHeight, s);
        }
        static PursuitSegment Seg(PursuitGraph g, float y, float x) => g.segments.Single(s => System.Math.Abs(s.y - y) < .01f && s.x0 <= x && x <= s.x1);
        static bool Linked(PursuitGraph g, PursuitSegment a, PursuitSegment b, PursuitEdgeKind kind)
            => g.edges[a.id].Any(e => e.to == b.id && e.kind == kind);

        [Test] public void GraphCacheSharesEqualPoliciesAndSeparatesBodyAndGravityChanges()
        {
            var terrain = new PursuitTerrain(); terrain.boxes.Add(Box(0, -2, 30, 2));
            var first = terrain.Graph(.5f, 1.3f, 6, Settings().jumpHeight, Settings());
            Assert.That(terrain.Graph(.5f, 1.3f, 6, Settings().jumpHeight, Settings()), Is.SameAs(first));
            Assert.That(terrain.Graph(.6f, 1.3f, 6, Settings().jumpHeight, Settings()), Is.Not.SameAs(first));
            float rise = PursuitGraph.Rise;
            try
            {
                PursuitGraph.Rise = rise * 2;
                Assert.That(terrain.Graph(.5f, 1.3f, 6, Settings().jumpHeight, Settings()), Is.Not.SameAs(first));
            }
            finally { PursuitGraph.Rise = rise; }
        }

        [Test] public void OneWayPlatformWithinJumpHeightIsJumpedOntoAndDroppedFrom()
        {
            var g = Graph(1.3f, Box(0, -2, 30, 2), Box(10, 2.6f, 6, .4f, true));
            var ground = Seg(g, 0, 5); var deck = Seg(g, 3, 12);
            Assert.That(Linked(g, ground, deck, PursuitEdgeKind.Jump), Is.True);
            Assert.That(Linked(g, deck, ground, PursuitEdgeKind.Drop), Is.True);
            Assert.That(Linked(g, deck, ground, PursuitEdgeKind.Fall), Is.True);
        }

        [Test] public void TooHighPlatformHasNoJump()
        {
            var g = Graph(1.3f, Box(0, -2, 30, 2), Box(10, 3.6f, 6, .4f, true));
            Assert.That(Linked(g, Seg(g, 0, 5), Seg(g, 4, 12), PursuitEdgeKind.Jump), Is.False);
        }

        [Test] public void SolidLedgeNeedsCornerClearance()
        {
            // 3.0 above with a 3.2 jump leaves less than the 0.3 margin a solid corner needs; 2.5 is climbable.
            var high = Graph(1.3f, Box(0, -2, 30, 2), Box(12, 0, 4, 3f));
            Assert.That(high.edges[Seg(high, 0, 5).id].Any(e => e.to == Seg(high, 3f, 14).id), Is.False);
            var low = Graph(1.3f, Box(0, -2, 30, 2), Box(12, 0, 4, 2.5f));
            Assert.That(Linked(low, Seg(low, 0, 5), Seg(low, 2.5f, 14), PursuitEdgeKind.Jump), Is.True);
        }

        [Test] public void LowCeilingRemovesFloorForTallBodies()
        {
            // 1.5 units under a solid slab: a 1.3-tall body stands there, a 3-tall body does not.
            var boxes = new[] { Box(0, -2, 30, 2), Box(0, 1.5f, 10, 1) };
            Assert.That(Graph(1.3f, boxes).segments.Any(s => s.y == 0 && s.x0 < 5 && s.x1 > 5), Is.True);
            Assert.That(Graph(3f, boxes).segments.Any(s => s.y == 0 && s.x0 < 5 && s.x1 > 5), Is.False);
        }

        [Test] public void SameSeedSpawnAndSpeciesGiveTheSameIndividual()
        {
            // No arena registered: seed 0. Identity comes from species id, spawn position and stream only.
            var spawn = new UnityEngine.Vector2(12.5f, 3);
            var a = EnemyRandom.For(null, "K4", spawn, EnemyRandom.Variance);
            var b = EnemyRandom.For(null, "K4", spawn, EnemyRandom.Variance);
            for (int i = 0; i < 5; i++) Assert.That(a.NextDouble(), Is.EqualTo(b.NextDouble()));
            var other = EnemyRandom.For(null, "K4", spawn + UnityEngine.Vector2.right, EnemyRandom.Variance);
            var stream = EnemyRandom.For(null, "K4", spawn, EnemyRandom.Hop);
            double first = EnemyRandom.For(null, "K4", spawn, EnemyRandom.Variance).NextDouble();
            Assert.That(other.NextDouble(), Is.Not.EqualTo(first));
            Assert.That(stream.NextDouble(), Is.Not.EqualTo(first));
        }

        [Test] public void GapWiderThanAirReachIsNotJumped()
        {
            var g = Graph(1.3f, Box(0, -2, 10, 2), Box(20, -2, 10, 2), Box(0, -20, 30, 2));
            Assert.That(Linked(g, Seg(g, 0, 5), Seg(g, 0, 25), PursuitEdgeKind.Jump), Is.False);
            var near = Graph(1.3f, Box(0, -2, 10, 2), Box(12, -2, 10, 2), Box(0, -20, 30, 2));
            Assert.That(Linked(near, Seg(near, 0, 5), Seg(near, 0, 15), PursuitEdgeKind.Jump), Is.True);
        }
    }
}
