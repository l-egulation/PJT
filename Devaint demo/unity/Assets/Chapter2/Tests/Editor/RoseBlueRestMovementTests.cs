using System;
using Deviant.Chapter2.RoseBlue;
using NUnit.Framework;

namespace Deviant.Chapter2.Tests
{
    public sealed class RoseBlueRestMovementTests
    {
        [TestCase(193, true, 30)] [TestCase(193, false, 30)]
        [TestCase(193, true, 120)] [TestCase(193, false, 120)]
        [TestCase(-193, true, 30)] [TestCase(-193, false, 30)]
        [TestCase(-193, true, 120)] [TestCase(-193, false, 120)]
        [TestCase(0, true, 120)] [TestCase(0, false, 120)]
        public void FloatPositionsReachEitherWallAndRemainThereAcrossRests(int origin, bool left, int hz)
        {
            const float width = 2.0661417322834645f;
            double minimum = origin + (double)width / 2, maximum = origin + 30 - (double)width / 2;
            float x = origin + (left ? 2 : 28), endpoint = (float)(left ? minimum : maximum);
            var movement = new RestMovement();
            for (int rest = 0; rest < 3; rest++)
            {
                movement.Begin(x, origin + 15);
                for (int tick = 0; tick < hz * 2; tick++)
                    x = (float)movement.Step(x, 1d / hz, minimum, maximum);
                Assert.That(x, Is.EqualTo(endpoint));
            }
            movement.Begin(x, origin + (left ? -10 : 40));
            x = (float)movement.Step(x, .5, minimum, maximum);
            Assert.That(x, left ? Is.GreaterThan(endpoint) : Is.LessThan(endpoint), "A new rest can move back into the arena.");
        }

        [Test] public void RestKeepsItsSpeedDistanceLimitAndRejectsInvalidBounds()
        {
            var movement = new RestMovement(); movement.Begin(10, 20);
            Assert.That(movement.Step(10, .5, 0, 30), Is.EqualTo(9));
            Assert.That(movement.Step(9, 10, 0, 30), Is.EqualTo(7));
            Assert.That(movement.Step(7, 10, 0, 30), Is.EqualTo(7));
            Assert.Throws<ArgumentException>(() => movement.Step(-.001, .1, 0, 30));
            Assert.Throws<ArgumentException>(() => movement.Step(30.001, .1, 0, 30));
            Assert.Throws<ArgumentException>(() => movement.Step(0, .1, 30, 0));
            Assert.Throws<ArgumentException>(() => movement.Step(double.NaN, .1, 0, 30));
            Assert.Throws<ArgumentException>(() => movement.Step(0, .1, double.NegativeInfinity, 30));
            Assert.Throws<ArgumentException>(() => movement.Step(0, .1, 0, double.PositiveInfinity));
        }
    }
}
