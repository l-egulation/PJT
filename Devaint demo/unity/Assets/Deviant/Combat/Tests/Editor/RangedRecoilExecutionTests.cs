using Deviant.Combat.Execution;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class RangedRecoilExecutionTests
    {
        [Test] public void LowHopDistributesFullBackwardDistanceAcrossItsArc()
        {
            var recoil = new RangedRecoilExecution();
            float hop = recoil.Begin(2, 1, .1f, 1, 50, 50);
            Assert.That(hop, Is.EqualTo(10));
            Assert.That(recoil.Read().Duration, Is.EqualTo(.4f));
            Assert.That(recoil.Velocity(2, .1f, false, 4), Is.EqualTo(-10));
            Assert.That(recoil.Velocity(2.35f, .1f, false, 4), Is.EqualTo(-5).Within(.00003f));
            Assert.That(recoil.IsActive(2.4f, false), Is.False);
        }
        [Test] public void LandingAndWallEndOnlyThisRecoilAndCancellationDoesNotReactivateIt()
        {
            var wall = new RangedRecoilExecution(); wall.Begin(1, -1, .5f, 0, 50, 50);
            var other = new RangedRecoilExecution(); other.Begin(1, 1, .5f, 0, 50, 50);
            wall.AfterMovement(1.1f, false, true, false);
            Assert.That(wall.IsActive(1.1f, false), Is.False);
            Assert.That(other.IsActive(1.1f, false), Is.True);
            Assert.That(other.IsActive(1.1f, true), Is.False);
            Assert.That(other.Velocity(1.1f, .1f, true, 4), Is.Zero);
            Assert.That(other.IsActive(1.1f, false), Is.False);
        }
        [Test] public void LeftwardFlightRecoilsRightAndUsesConfiguredDurationWithoutAHop()
        {
            var recoil = new RangedRecoilExecution();
            Assert.That(recoil.IsActive(0, false), Is.False);
            Assert.That(recoil.Begin(3, -1, .5f, 0, 50, 50), Is.Zero);
            Assert.That(recoil.Velocity(3, .02f, false, 4), Is.EqualTo(8));
            Assert.That(recoil.IsActive(3.5f - .000001f, false), Is.False);
        }
    }
}
