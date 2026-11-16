using NUnit.Framework;

namespace Deviant.Presentation.Tests
{
    public sealed class PlayerDeathSequenceTests
    {
        [Test]
        public void SlowDeathAnimationFinishesBeforeRealTimeBlackoutAndResult()
        {
            var death = new PlayerDeathSequence(); death.Begin(.8f);
            death.Advance(1, .35f);
            Assert.That(death.FadeOpacity, Is.Zero); Assert.That(death.ResultReady, Is.False);
            death.Advance(.45f, .8f); death.Advance(.1f, .9f);
            Assert.That(death.FadeOpacity, Is.EqualTo(0).Within(.00001));
            death.Advance(.9f, 1.8f);
            Assert.That(death.FadeOpacity, Is.EqualTo(.5f).Within(.00001)); Assert.That(death.ResultReady, Is.False);
            death.Advance(.9f, 2.7f);
            Assert.That(death.FadeOpacity, Is.EqualTo(1).Within(.00001)); Assert.That(death.ResultReady, Is.False);
            death.Advance(.251f, 2.951f); Assert.That(death.ResultReady, Is.True);
        }
        [Test]
        public void ClearUsesTheDeathBlackoutAndHoldWithoutPoseWaitOrKick()
        {
            var clear = new PlayerDeathSequence(); Assert.That(clear.BeginClear(), Is.True);
            Assert.That(clear.Clearing, Is.True); Assert.That(clear.BeginClear(), Is.False); Assert.That(clear.Begin(.8f), Is.False);
            clear.Advance(.055f, 0);
            Assert.That(clear.ReadKick(1).X, Is.Zero); Assert.That(clear.ReadKick(1).Y, Is.Zero);
            clear.Advance(PlayerDeathSequence.FadeSeconds / 2 - .055f, 0);
            Assert.That(clear.FadeOpacity, Is.EqualTo(.5f).Within(.00001));
            clear.Advance(PlayerDeathSequence.FadeSeconds / 2, 0);
            Assert.That(clear.FadeOpacity, Is.EqualTo(1).Within(.00001)); Assert.That(clear.ResultReady, Is.False);
            clear.Advance(PlayerDeathSequence.BlackHoldSeconds + .001f, 0); Assert.That(clear.ResultReady, Is.True);
            clear.Reset(); Assert.That(clear.Clearing, Is.False); Assert.That(clear.FadeOpacity, Is.Zero);
        }
        [Test]
        public void PauseDoesNotAdvanceFadeAndRepeatedDeathCannotRestartIt()
        {
            var death = new PlayerDeathSequence(); death.Begin(.64f);
            death.Advance(.64f, .64f); death.Advance(.6f, 1.24f);
            float opacity = death.FadeOpacity;
            Assert.That(death.Begin(.8f), Is.False); death.Advance(0, 1.24f);
            Assert.That(death.FadeOpacity, Is.EqualTo(opacity));
            death.Reset(); Assert.That(death.Active, Is.False); Assert.That(death.ResultReady, Is.False); Assert.That(death.FadeOpacity, Is.Zero);
            Assert.That(death.Begin(.8f), Is.True);
        }
        [Test]
        public void CameraImpulseStartsAndReturnsAtZeroAndMirrorsWithoutMovingTheWorld()
        {
            var death = new PlayerDeathSequence(); death.Begin(.8f);
            Assert.That(death.ReadKick(1).X, Is.Zero);
            death.Advance(.055f, .055f);
            Assert.That(death.ReadKick(1).X, Is.LessThan(-7));
            Assert.That(death.ReadKick(-1).X, Is.EqualTo(-death.ReadKick(1).X));
            death.Advance(.2251f, .2801f);
            Assert.That(death.ReadKick(1).X, Is.Zero); Assert.That(death.ReadKick(1).Y, Is.Zero);
        }
        [Test]
        public void BlackoutIsMonotonicAndHandlesAnimationCompletionBetweenFrames()
        {
            var death = new PlayerDeathSequence(); death.Begin(.8f);
            death.Advance(1, 1);
            Assert.That(death.FadeOpacity, Is.GreaterThan(0));
            float previous = death.FadeOpacity;
            for (int i = 0; i < 200; i++)
            {
                death.Advance(.02f, 1 + (i + 1) * .02f);
                Assert.That(death.FadeOpacity, Is.InRange(previous, 1)); previous = death.FadeOpacity;
            }
            Assert.That(death.ResultReady, Is.True);
        }
    }
}
