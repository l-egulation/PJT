using System;
using Deviant.Definitions;
using NUnit.Framework;

namespace Deviant.World.Tests
{
    public sealed class ReactivePendulumTests
    {
        static PendulumDefinition Definition() => new PendulumDefinition(new PendulumData { length = 1.4f, gravity = 9.81f, damping = 1.1f, maxAngleDegrees = 45, maxAngularSpeed = 4, attackImpulse = 1.8f, bodyImpulseScale = .12f });
        [Test] public void ImpulseMovesInTheContactDirectionAndSettlesWithoutMovingTheAnchor()
        {
            var state = new ReactivePendulum(Definition()); state.Impulse(1); state.Advance(.1f);
            Assert.That(state.OffsetX, Is.GreaterThan(0)); Assert.That(state.OffsetY, Is.LessThan(0));
            for (int i = 0; i < 2000; i++) state.Advance(.01f);
            Assert.That(Math.Abs(state.Angle), Is.LessThan(.001)); Assert.That(Math.Abs(state.AngularSpeed), Is.LessThan(.001));
        }
        [Test] public void HitIdentityDeduplicatesUntilTheActualExecutionEndsAndResetClearsIt()
        {
            var state = new ReactivePendulum(Definition()); var id = Guid.NewGuid(); var key = new PropImpactKey(id, 0);
            Assert.That(state.Attack(key, 1), Is.True); float speed = state.AngularSpeed;
            Assert.That(state.Attack(key, 1), Is.False); Assert.That(state.AngularSpeed, Is.EqualTo(speed));
            Assert.That(state.Attack(new PropImpactKey(id, 1), -1), Is.True);
            state.ForgetExecution(id); Assert.That(state.Attack(key, 1), Is.True);
            state.Reset(); Assert.That(state.AcceptedAttacks, Is.Zero); Assert.That(state.Angle, Is.Zero); Assert.That(state.Attack(key, -1), Is.True);
        }
        [Test] public void PauseBoundsAndLongStepStayFiniteAndFramePartitionIsClose()
        {
            var definition = Definition(); var first = new ReactivePendulum(definition); var second = new ReactivePendulum(definition);
            first.Impulse(100000); second.Impulse(100000); first.Advance(0); Assert.That(first.Angle, Is.Zero);
            first.Advance(.5f); for (int i = 0; i < 50; i++) second.Advance(.01f);
            Assert.That(first.Angle, Is.EqualTo(second.Angle).Within(.02));
            Assert.That(Math.Abs(first.Angle), Is.LessThanOrEqualTo(definition.MaxAngle));
            Assert.That(Math.Abs(first.AngularSpeed), Is.LessThanOrEqualTo(definition.MaxAngularSpeed));
            Assert.Throws<ArgumentOutOfRangeException>(() => first.Advance(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => first.Impulse(float.PositiveInfinity));
        }
    }
}
