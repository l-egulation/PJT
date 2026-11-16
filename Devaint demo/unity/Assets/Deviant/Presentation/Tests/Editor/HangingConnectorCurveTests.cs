using System;
using Deviant.Definitions;
using NUnit.Framework;

namespace Deviant.Presentation.Tests
{
    public sealed class HangingConnectorCurveTests
    {
        static HangingConnectorData Data() => new HangingConnectorData { style = "chain", segments = 24, linkPitch = .1f, chainWidth = .13f, ropeWidth = .045f, linkOverlap = 1.55f, maxBend = .12f, lagScale = .045f, response = 10 };
        [Test] public void DefinitionsCaptureTuningAndRejectUnknownStylesAndInvalidGeometryBudgets()
        {
            var data = Data(); var definition = new HangingConnectorDefinition(data); data.linkPitch = 20; data.style = "rope";
            Assert.That(definition.LinkPitch, Is.EqualTo(.1f)); Assert.That(definition.Style, Is.EqualTo(HangingConnectorStyle.Chain));
            data = Data(); data.style = "rod"; Assert.Throws<ArgumentException>(() => new HangingConnectorDefinition(data));
            data = Data(); data.segments = 10000; Assert.Throws<ArgumentOutOfRangeException>(() => new HangingConnectorDefinition(data));
            data = Data(); data.maxBend = float.NaN; Assert.Throws<ArgumentOutOfRangeException>(() => new HangingConnectorDefinition(data));
        }
        [Test] public void BowFollowsMotionButBothSocketsRemainExact()
        {
            var curve = new HangingConnectorCurve(new HangingConnectorDefinition(Data()));
            var start = new ConnectorPoint(0, 2); var end = new ConnectorPoint(.3f, 1);
            curve.Advance(.1f, 3);
            Assert.That(curve.Bend, Is.LessThan(0));
            Assert.That(curve.Sample(start, end, 0).X, Is.EqualTo(start.X)); Assert.That(curve.Sample(start, end, 0).Y, Is.EqualTo(start.Y));
            Assert.That(curve.Sample(start, end, 1).X, Is.EqualTo(end.X)); Assert.That(curve.Sample(start, end, 1).Y, Is.EqualTo(end.Y));
            Assert.That(curve.Sample(start, end, .5f).X, Is.Not.EqualTo((start.X + end.X) / 2));
        }
        [Test] public void PauseResetAndRestDoNotCreateIndependentTimeOrMotion()
        {
            var curve = new HangingConnectorCurve(new HangingConnectorDefinition(Data()));
            curve.Advance(.1f, 2); float bend = curve.Bend;
            curve.Advance(0, -4); Assert.That(curve.Bend, Is.EqualTo(bend));
            for (int i = 0; i < 200; i++) curve.Sample(new ConnectorPoint(0, 0), new ConnectorPoint(0, -1), .5f);
            Assert.That(curve.Bend, Is.EqualTo(bend), "Rendering cannot advance the curve.");
            curve.Advance(2, 0); Assert.That(Math.Abs(curve.Bend), Is.LessThan(1e-5));
            curve.Reset(); Assert.That(curve.Bend, Is.Zero);
        }
        [Test] public void ConstantMotionIsStableAcrossFramePartitionsAndTinySpansStayBounded()
        {
            var definition = new HangingConnectorDefinition(Data()); var a = new HangingConnectorCurve(definition); var b = new HangingConnectorCurve(definition);
            a.Advance(.5f, 3); for (int i = 0; i < 50; i++) b.Advance(.01f, 3);
            Assert.That(a.Bend, Is.EqualTo(b.Bend).Within(1e-6));
            var start = new ConnectorPoint(0, 0); var end = new ConnectorPoint(0, -.001f);
            Assert.That(Math.Abs(a.Sample(start, end, .5f).X), Is.LessThanOrEqualTo(.0002f + 1e-7f));
            Assert.That(a.Sample(start, start, .5f).X, Is.Zero);
        }
        [Test] public void InvalidSamplingDoesNotMutateTheCurrentBend()
        {
            var curve = new HangingConnectorCurve(new HangingConnectorDefinition(Data())); curve.Advance(.1f, 2); float bend = curve.Bend;
            Assert.Throws<ArgumentOutOfRangeException>(() => curve.Advance(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => curve.Advance(1, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => curve.Sample(new ConnectorPoint(0, 0), new ConnectorPoint(0, -1), 2));
            Assert.That(curve.Bend, Is.EqualTo(bend));
        }
    }
}
