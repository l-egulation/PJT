using Deviant.Combat.Execution;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class HorizontalRunControlTests
    {
        [Test] public void ZeroSecondsKeepsImmediateStartStopAndReversal()
        {
            Assert.That(HorizontalRunControl.Approach(0, 7.5f, 7.5f, .02f, 0, 0), Is.EqualTo(7.5f));
            Assert.That(HorizontalRunControl.Approach(7.5f, -7.5f, 7.5f, .02f, 0, 0), Is.EqualTo(-7.5f));
            Assert.That(HorizontalRunControl.Approach(7.5f, 0, 7.5f, .02f, 0, 0), Is.Zero);
        }
        [Test] public void AccelerationAndDecelerationReachRunSpeedInTheirSecondsIndependentOfStepSize()
        {
            foreach (float step in new[] { .01f, .02f, .025f })
            {
                float velocity = 0, time = 0;
                while (velocity < 7.5f) { velocity = HorizontalRunControl.Approach(velocity, 7.5f, 7.5f, step, .05f, .03f); time += step; }
                Assert.That(time, Is.EqualTo(.05f).Within(step + .0001f));
                time = 0;
                while (velocity > 0) { velocity = HorizontalRunControl.Approach(velocity, 0, 7.5f, step, .05f, .03f); time += step; }
                Assert.That(time, Is.EqualTo(.03f).Within(step + .0001f));
            }
        }
        [Test] public void ReversalStopsFirstThenSpendsTheRemainingStepAccelerating()
        {
            // 2.5u/s stops in 0.01s at 250u/s^2, then 0.01s at 150u/s^2 gives -1.5u/s.
            Assert.That(HorizontalRunControl.Approach(2.5f, -7.5f, 7.5f, .02f, .05f, .03f), Is.EqualTo(-1.5f).Within(.0001f));
            Assert.That(HorizontalRunControl.Approach(-7.5f, 7.5f, 7.5f, .02f, .05f, .03f), Is.EqualTo(-2.5f).Within(.0001f));
        }
    }
}
