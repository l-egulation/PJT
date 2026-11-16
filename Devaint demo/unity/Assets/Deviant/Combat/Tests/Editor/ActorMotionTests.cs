using System;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class ActorMotionTests
    {
        [TestCase(.01f)] [TestCase(.017f)] [TestCase(.02f)] [TestCase(.05f)]
        public void VoluntaryJumpApexPreservesHeightAndExactDurationAcrossTickPartitions(float step)
        {
            var apex = new ActorJumpApexState(true, 0);
            float time = 0, y = 20, height = 0;
            while (time < .48f - .000001f)
            {
                float dt = Math.Min(step, .48f - time);
                var motion = ActorMotionResolver.Resolve(new ActorMotionInput(1, dt, 7.5f, y, false, false,
                    false, false, false, 0, 0, 0, 0, 0, 0, 66.6666667f, 104.1666667f, null, apex, .06f));
                height += motion.DeltaY; y = motion.VelocityY; apex = motion.Apex; time += dt;
                Assert.That(motion.DeltaX, Is.EqualTo(7.5f * dt), "Hang must not block horizontal movement.");
            }
            Assert.That(height, Is.EqualTo(2.25f).Within(.00002f), "3-unit apex, then 0.12 seconds of falling after 0.06-second hold.");
            Assert.That(y, Is.EqualTo(-12.5f).Within(.00002f));
            Assert.That(apex.Armed, Is.False); Assert.That(apex.Remaining, Is.Zero);
        }
        [Test] public void ApexHoldConsumesOnlyTheFractionAfterRisingAndFallsDuringTheFractionAfterExpiry()
        {
            var motion = ActorMotionResolver.Resolve(new ActorMotionInput(1, .02f, 0, 1, false, false,
                false, false, false, 0, 0, 0, 0, 0, 0, 66.6666667f, 104.1666667f, null, new ActorJumpApexState(true, 0), .06f));
            Assert.That(motion.DeltaY, Is.EqualTo(.0075f).Within(.000001f));
            Assert.That(motion.VelocityY, Is.Zero); Assert.That(motion.Apex.Remaining, Is.EqualTo(.055f).Within(.000001f));
            motion = ActorMotionResolver.Resolve(new ActorMotionInput(1, .06f, 0, 0, false, false,
                false, false, false, 0, 0, 0, 0, 0, 0, 66.6666667f, 104.1666667f, null, motion.Apex, .06f));
            Assert.That(motion.DeltaY, Is.EqualTo(-.5f * 104.1666667f * .005f * .005f).Within(.000001f));
            Assert.That(motion.Apex.Remaining, Is.Zero);
        }
        static ActorMotionInput Input(float now = 1, float step = .5f, float x = 3, float y = 2, bool ground = false, bool freeze = false,
            bool forced = false, bool dead = false, bool decoy = false, float knock = 0, float locked = 0, float stun = 0, float root = 0, float stagger = 0)
            => new ActorMotionInput(now, step, x, y, ground, freeze, forced, dead, decoy, -7, knock, locked, stun, root, stagger, 2, 4);
        [Test] public void MotionContractsAndResolverHaveNoEngineSceneOrUiReference()
        {
            foreach (var type in new[] { typeof(ActorMotionInput), typeof(ActorMotionProposal), typeof(ActorMotionResolver) })
                Assert.That(type.Assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("UnityEngine") || a.Name.StartsWith("UnityEditor") || a.Name == "Deviant.Combat" || a.Name.StartsWith("Deviant.World") || a.Name.StartsWith("Deviant.UI")), Is.False);
        }
        [Test] public void KnockbackWinsOverLocksUntilItsStrictExpiryAndForcedMovementWinsOverBoth()
        {
            Assert.That(ActorMotionResolver.Resolve(Input(knock: 2, locked: 3, stun: 4, root: 5, stagger: 6)).VelocityX, Is.EqualTo(-7));
            Assert.That(ActorMotionResolver.Resolve(Input(now: 2, knock: 2)).VelocityX, Is.EqualTo(3));
            Assert.That(ActorMotionResolver.Resolve(Input(forced: true, knock: 2, locked: 3, stun: 4, root: 5, stagger: 6)).VelocityX, Is.EqualTo(3));
        }
        [TestCase(.02f)] [TestCase(.03f)] [TestCase(.016666668f)]
        public void BasicPushIntegratesExactlyItsWindowIncludingTheFinalPartialTick(float step)
        {
            float distance = 0;
            for (int tick = 0; tick * step < .1f; tick++)
            {
                float now = tick * step;
                var motion = ActorMotionResolver.Resolve(new ActorMotionInput(now, step, 9, 0, true, true,
                    false, false, false, -2.5f, .1f, .3f, 0, 0, .3f, 2, 4));
                distance += motion.DeltaX;
            }
            Assert.That(distance, Is.EqualTo(-.25f).Within(.000001f));
        }
        [Test] public void FinalKnockTickUsesOnlyRemainingDurationAndDoesNotResumeInputWithinTheTick()
        {
            var result = ActorMotionResolver.Resolve(Input(now: .09f, step: .02f, x: 9, ground: true, freeze: true, knock: .1f));
            Assert.That(result.VelocityX, Is.EqualTo(-3.5f).Within(.000001f));
            Assert.That(result.DeltaX, Is.EqualTo(-.07f).Within(.000001f));
            Assert.That(ActorMotionResolver.Resolve(Input(now: .1f, step: .02f, knock: .1f, stagger: .3f)).VelocityX, Is.Zero);
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] public void EachIndependentLockSuppressesInputOnlyBeforeItsExpiry(int kind)
        {
            ActorMotionInput State(float now) => Input(now: now, locked: kind == 0 ? 2 : 0, stun: kind == 1 ? 2 : 0, root: kind == 2 ? 2 : 0, stagger: kind == 3 ? 2 : 0);
            Assert.That(ActorMotionResolver.Resolve(State(1)).VelocityX, Is.Zero); Assert.That(ActorMotionResolver.Resolve(State(2)).VelocityX, Is.EqualTo(3));
        }
        [TestCase(true, false)] [TestCase(false, true)] public void DeathAndDecoySuppressHorizontalEvenWhenForced(bool dead, bool decoy)
        {
            var result = ActorMotionResolver.Resolve(Input(forced: true, dead: dead, decoy: decoy));
            Assert.That(result.VelocityX, Is.Zero); Assert.That(result.DeltaX, Is.Zero); Assert.That(result.DeltaY, Is.EqualTo(.75f), "Existing vertical integration still applies.");
        }
        [TestCase(.5f, 2f, false, .75f, 1f)]
        [TestCase(1f, 2f, false, 1f, 0f)]
        [TestCase(1.5f, 2f, false, .5f, -2f)]
        [TestCase(.5f, -2f, false, -1.5f, -4f)]
        [TestCase(.5f, -2f, true, 0f, 0f)]
        [TestCase(.5f, 2f, true, .75f, 1f)]
        public void KnownArcsCoverRisingApexCrossingFallingAndGrounding(float step, float y, bool ground, float delta, float finalVelocity)
        {
            // Independent small-number examples: rise gravity 2, fall gravity 4.
            var result = ActorMotionResolver.Resolve(Input(step: step, y: y, ground: ground));
            Assert.That(result.DeltaY, Is.EqualTo(delta)); Assert.That(result.VelocityY, Is.EqualTo(finalVelocity)); Assert.That(result.DeltaX, Is.EqualTo(3 * step));
        }
        [Test] public void VerticalFreezeClearsExistingVelocityWithoutAddingGravity()
        {
            var result = ActorMotionResolver.Resolve(Input(y: 20, freeze: true)); Assert.That(result.VelocityY, Is.Zero); Assert.That(result.DeltaY, Is.Zero); Assert.That(result.VelocityX, Is.EqualTo(3));
        }
        [Test] public void InputsAndResultsStayReadOnlyAndIndependent()
        {
            var input = Input(); var before = ActorMotionResolver.Resolve(input); ActorMotionResolver.Resolve(Input(y: -2, x: -8));
            Assert.That(input.VelocityY, Is.EqualTo(2)); Assert.That(before.VelocityY, Is.EqualTo(1)); Assert.That(before.VelocityX, Is.EqualTo(3));
            foreach (var type in new[] { typeof(ActorMotionInput), typeof(ActorMotionProposal) }) Assert.That(type.GetProperties().Any(p => p.GetSetMethod(true) != null), Is.False);
        }
        [Test] public void InvalidOrOverflowingMotionCannotProduceAProposal()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Input(now: -1)); Assert.Throws<ArgumentOutOfRangeException>(() => Input(step: 0)); Assert.Throws<ArgumentOutOfRangeException>(() => Input(x: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => Input(y: float.PositiveInfinity)); Assert.Throws<ArgumentOutOfRangeException>(() => Input(knock: float.NaN));
            Assert.Throws<ArgumentException>(() => ActorMotionResolver.Resolve(default)); Assert.Throws<ArgumentOutOfRangeException>(() => ActorMotionResolver.Resolve(Input(step: 10, x: float.MaxValue)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ActorMotionProposal(0, 0, float.PositiveInfinity, 0));
        }
    }
}
