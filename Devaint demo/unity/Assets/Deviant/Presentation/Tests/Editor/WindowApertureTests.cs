using System;
using NUnit.Framework;

namespace Deviant.Presentation.Tests
{
    public sealed class WindowApertureTests
    {
        static WindowPoint[] Box(float bottom, float top) => new[] {
            new WindowPoint(-1, bottom), new WindowPoint(1, bottom), new WindowPoint(1, top), new WindowPoint(-1, top) };
        [Test] public void DisconnectedPanesDoNotIlluminateTheirGapAndCaptureInput()
        {
            var lower = Box(-1, -.2f); var upper = Box(.2f, 1); var shape = new WindowAperture(lower, upper);
            lower[0] = new WindowPoint(-100, -100); upper[1] = new WindowPoint(100, 100);
            Assert.That(shape.Bounds.Left, Is.EqualTo(-1)); Assert.That(shape.VertexCount, Is.EqualTo(8));
            foreach (float x in new[] { 2f, 5f, 8f })
            {
                Assert.That(shape.TrySourceDistance(new WindowPoint(x, 0), new WindowPoint(1, 0), 10, out _), Is.False);
                Assert.That(shape.TrySourceDistance(new WindowPoint(x, .5f), new WindowPoint(1, 0), 10, out float distance), Is.True);
                Assert.That(distance, Is.EqualTo(x - 1).Within(1e-5));
            }
        }
        [Test] public void PolygonShapeNotItsBoundsDeterminesTransmissionAndWindingIsNormalized()
        {
            var vertices = new[] { new WindowPoint(-1, 0), new WindowPoint(0, -1), new WindowPoint(1, 0), new WindowPoint(0, 1) };
            var shape = new WindowAperture(vertices); Array.Reverse(vertices); var reversed = new WindowAperture(vertices);
            foreach (var aperture in new[] { shape, reversed })
            {
                Assert.That(aperture.TrySourceDistance(new WindowPoint(0, .8f), new WindowPoint(1, 0), 10, out _), Is.True);
                Assert.That(aperture.TrySourceDistance(new WindowPoint(-.8f, .8f), new WindowPoint(1, 0), 10, out _), Is.False);
                Assert.That(aperture.TrySourceDistance(new WindowPoint(12, 0), new WindowPoint(1, 0), 10, out _), Is.False);
            }
        }
        [Test] public void MalformedConcaveAndOverBudgetOpeningsAreRejected()
        {
            Assert.Throws<ArgumentException>(() => new WindowAperture());
            Assert.Throws<ArgumentException>(() => new WindowAperture(new WindowPoint[2]));
            Assert.Throws<ArgumentException>(() => new WindowAperture(new[] { new WindowPoint(0, 0), new WindowPoint(1, 0), new WindowPoint(1, 0), new WindowPoint(0, 1) }));
            Assert.Throws<ArgumentException>(() => new WindowAperture(new[] { new WindowPoint(0, 0), new WindowPoint(2, 0), new WindowPoint(1, .5f), new WindowPoint(2, 2), new WindowPoint(0, 2) }));
            var pieces = new WindowPoint[9][]; for (int i = 0; i < pieces.Length; i++) pieces[i] = Box(-1, 1);
            Assert.Throws<ArgumentException>(() => new WindowAperture(pieces));
            var tooMany = new WindowPoint[65];
            for (int i = 0; i < tooMany.Length; i++) tooMany[i] = new WindowPoint((float)Math.Cos(i * Math.PI * 2 / 65), (float)Math.Sin(i * Math.PI * 2 / 65));
            Assert.Throws<ArgumentException>(() => new WindowAperture(tooMany));
        }
    }
}
