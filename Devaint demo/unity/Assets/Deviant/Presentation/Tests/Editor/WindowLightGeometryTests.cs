using System;
using Deviant.Definitions;
using NUnit.Framework;

namespace Deviant.Presentation.Tests
{
    public sealed class WindowLightGeometryTests
    {
        static readonly WindowRect Opening = new WindowRect(-1, -1, 1, 1);
        static readonly WindowRect Room = new WindowRect(-4, -4, 12, 4);
        [Test] public void WholeApertureSweepsIntoAParallelFootprintRatherThanARadialCircle()
        {
            var shape = WindowLightGeometry.Build(Opening, Room, 0, 6);
            Assert.That(shape.Length, Is.EqualTo(4));
            foreach (float y in new[] { -.8f, 0, .8f }) Assert.That(WindowLightGeometry.Contains(shape, new WindowPoint(5, y)), Is.True);
            Assert.That(WindowLightGeometry.Contains(shape, new WindowPoint(5, 1.5f)), Is.False);
            Assert.That(WindowLightGeometry.Contains(shape, new WindowPoint(-2, 0)), Is.False);
            Assert.That(WindowLightGeometry.Contains(shape, new WindowPoint(8, 0)), Is.False);
        }
        [Test] public void AngleChangesWhichRegionIsIlluminatedWhileKeepingTheSourceArea()
        {
            var down = WindowLightGeometry.Build(Opening, Room, -30, 10); var up = WindowLightGeometry.Build(Opening, Room, 30, 10);
            Assert.That(WindowLightGeometry.Contains(down, new WindowPoint(4, -2)), Is.True);
            Assert.That(WindowLightGeometry.Contains(up, new WindowPoint(4, -2)), Is.False);
            Assert.That(WindowLightGeometry.Contains(up, new WindowPoint(4, 2)), Is.True);
            foreach (var point in new[] { new WindowPoint(-.8f, -.8f), new WindowPoint(.8f, .8f) })
            { Assert.That(WindowLightGeometry.Contains(down, point), Is.True); Assert.That(WindowLightGeometry.Contains(up, point), Is.True); }
        }
        [TestCase(-180), TestCase(-90), TestCase(-30), TestCase(0), TestCase(25), TestCase(90), TestCase(180)]
        public void ClippedFootprintsStayConvexFiniteAndInsideTheReceiver(float angle)
        {
            var shape = WindowLightGeometry.Build(Opening, Room, angle, 100);
            Assert.That(shape.Length, Is.InRange(3, 12));
            foreach (var point in shape) Assert.That(Room.Contains(point, 1e-4f), Is.True);
            Assert.That(WindowLightGeometry.Contains(shape, new WindowPoint(0, 0)), Is.True);
            for (int i = 0; i < shape.Length; i++)
            {
                var a = shape[i]; var b = shape[(i + 1) % shape.Length];
                Assert.That(Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y), Is.GreaterThan(1e-6));
            }
        }
        [Test] public void ParallelShadowsKeepTheirWidthAtDifferentReceiverDepthsAndIgnoreGeometryBehindTheSource()
        {
            var aperture = new WindowRect(-1, -5, 1, 5); var direction = WindowLightGeometry.Direction(0);
            var edges = new[] { new WindowShadowEdge(new WindowPoint(2, -1), new WindowPoint(2, 1), 1) };
            foreach (float x in new[] { 3f, 5f, 9f })
            {
                Assert.That(WindowDirectionalShadows.IsBlocked(aperture, new WindowPoint(x, .8f), direction, 10, edges), Is.True);
                Assert.That(WindowDirectionalShadows.IsBlocked(aperture, new WindowPoint(x, 1.2f), direction, 10, edges), Is.False);
            }
            Assert.That(WindowDirectionalShadows.IsBlocked(aperture, new WindowPoint(9, 0), direction, 10, edges, 1), Is.False, "A receiver does not shadow itself.");
            var behind = new[] { new WindowShadowEdge(new WindowPoint(-2, -1), new WindowPoint(-2, 1), 2) };
            Assert.That(WindowDirectionalShadows.IsBlocked(aperture, new WindowPoint(9, 0), direction, 10, behind), Is.False);
        }
        [Test] public void IdenticalOccludersAtDifferentLateralPositionsDoNotConvergeOnACentroid()
        {
            var aperture = new WindowRect(-1, -5, 1, 5); var direction = WindowLightGeometry.Direction(0);
            var edges = new[] { new WindowShadowEdge(new WindowPoint(2, -2), new WindowPoint(2, -1), 1), new WindowShadowEdge(new WindowPoint(4, 1), new WindowPoint(4, 2), 2) };
            foreach (float x in new[] { 5f, 8f }) foreach (float y in new[] { -1.5f, 1.5f })
                Assert.That(WindowDirectionalShadows.IsBlocked(aperture, new WindowPoint(x, y), direction, 10, edges), Is.True);
            Assert.That(WindowDirectionalShadows.IsBlocked(aperture, new WindowPoint(8, 0), direction, 10, edges), Is.False);
        }
        [Test] public void InvalidInputsRejectAndDefinitionsCaptureTheirOwnValues()
        {
            var input = new WindowLightData { angleDegrees = -30, length = 10, edgeFeather = .18f, beamOpacity = .08f, endFade = 1.5f };
            var definition = new WindowLightDefinition(input); input.angleDegrees = 80; input.length = 2;
            Assert.That(definition.AngleDegrees, Is.EqualTo(-30)); Assert.That(definition.Length, Is.EqualTo(10));
            input.angleDegrees = float.NaN; Assert.Throws<ArgumentOutOfRangeException>(() => new WindowLightDefinition(input));
            Assert.Throws<ArgumentException>(() => new WindowRect(0, 0, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => WindowLightGeometry.Build(Opening, Room, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => WindowLightGeometry.Direction(181));
            Assert.That(WindowLightGeometry.Build(new WindowRect(-100, -100, -99, -99), Room, 0, 1), Is.Empty);
        }
    }
}
