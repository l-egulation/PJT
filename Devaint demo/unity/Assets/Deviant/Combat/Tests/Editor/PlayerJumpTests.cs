using Deviant.Combat.Execution;
using Deviant.Definitions;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class PlayerJumpTests
    {
        static LocomotionProfile Movement(float second = 20)
        {
            Assert.That(LocomotionProfile.TryCreate(new LocomotionData { schemaVersion = 2, id = "jump-test", runSpeed = 5,
                dashSpeed = 15, dashDuration = .2f, dashRecovery = 1, dashInvulnerability = .15f, dashJumpCarrySpeed = 5, dashJumpCarryDuration = .1f,
                firstJumpVelocity = 20, secondJumpVelocity = second, riseGravity = 66.6666667f, fallGravity = 104.1666667f }, out var movement, out var error), Is.True, error);
            return movement;
        }
        [Test] public void TwoPressesReplaceVelocityAndOnlyExhaustedInputCreatesAReservation()
        {
            var jump = new PlayerJumpExecution(); var movement = Movement(18);
            Assert.That(jump.Press(1, movement, out float velocity), Is.True); Assert.That(velocity, Is.EqualTo(20));
            Assert.That(jump.Press(1.1f, movement, out velocity), Is.True); Assert.That(velocity, Is.EqualTo(18));
            Assert.That(jump.Used, Is.EqualTo(2)); Assert.That(jump.BufferedUntil, Is.EqualTo(-1));
            Assert.That(jump.Press(1.2f, movement, out velocity), Is.False); Assert.That(velocity, Is.Zero);
            Assert.That(jump.BufferedUntil, Is.EqualTo(1.28f).Within(.000001f)); Assert.That(jump.Used, Is.EqualTo(2));
        }
        [Test] public void AnUntaughtJumpNeitherJumpsNorBuffersAndTheLimitCapsAirJumps()
        {
            var jump = new PlayerJumpExecution { Limit = 0 }; var movement = Movement();
            Assert.That(jump.Press(1, movement, out float velocity), Is.False); Assert.That(velocity, Is.Zero);
            Assert.That(jump.BufferedUntil, Is.EqualTo(-1), "a locked jump is not reserved for the landing");
            Assert.That(jump.Land(1.01f, movement, out velocity), Is.False);
            jump.Limit = 1;
            Assert.That(jump.Press(2, movement, out velocity), Is.True); Assert.That(velocity, Is.EqualTo(20));
            Assert.That(jump.Press(2.1f, movement, out velocity), Is.False, "no air jump before it is taught");
            jump.Limit = 2;
            Assert.That(jump.Press(2.2f, movement, out velocity), Is.True, "the air jump opens mid-air once taught");
        }
        [Test] public void LandingConsumesOneBufferedJumpAtTheInclusiveDeadlineAndNotAfterIt()
        {
            var jump = new PlayerJumpExecution(); var movement = Movement();
            jump.Press(0, movement, out _); jump.Press(0, movement, out _); jump.Press(1, movement, out _);
            Assert.That(jump.Land(jump.BufferedUntil, movement, out float velocity), Is.True);
            Assert.That(velocity, Is.EqualTo(20)); Assert.That(jump.Used, Is.EqualTo(1)); Assert.That(jump.BufferedUntil, Is.EqualTo(-1));
            jump.Press(2, movement, out _); jump.Press(2, movement, out _);
            Assert.That(jump.Land(2.09f, movement, out velocity), Is.False); Assert.That(velocity, Is.Zero); Assert.That(jump.Used, Is.Zero);
            Assert.That(jump.Press(2.1f, movement, out velocity), Is.True); Assert.That(velocity, Is.EqualTo(20));
        }
        [Test] public void RepeatedExhaustedEdgesRefreshRatherThanExtendAndClearDoesNotRefundUsage()
        {
            var jump = new PlayerJumpExecution(); var movement = Movement(); jump.Press(0, movement, out _); jump.Press(0, movement, out _);
            jump.Press(1, movement, out _); var before = jump.Read(); jump.Press(1.05f, movement, out _);
            Assert.That(jump.BufferedUntil, Is.EqualTo(1.13f)); Assert.That(before.BufferedUntil, Is.EqualTo(1.08f));
            jump.ClearBuffer(); Assert.That(jump.Used, Is.EqualTo(2)); Assert.That(jump.BufferedUntil, Is.EqualTo(-1));
            Assert.That(jump.Land(1.1f, movement, out _), Is.False); Assert.That(jump.Used, Is.Zero);
            jump.Press(2, movement, out _); jump.Reset(); Assert.That(jump.Used, Is.Zero); Assert.That(jump.BufferedUntil, Is.EqualTo(-1));
        }
        [Test] public void ReleaseCutsAnArmedRiseOnceAndSameTickEdgesUseThePreviousHold()
        {
            var jump = new PlayerJumpExecution();
            jump.ArmCut(true); Assert.That(jump.ObserveHold(true, false), Is.False, "held press keeps full rise");
            Assert.That(jump.ObserveHold(false, true), Is.True); Assert.That(jump.ObserveHold(false, true), Is.False, "one cut per jump");
            jump.ArmCut(true); Assert.That(jump.ObserveHold(true, true), Is.True, "press and release from rest is a tap");
            jump.ObserveHold(true, false); jump.ArmCut(true);
            Assert.That(jump.ObserveHold(true, true), Is.False, "release of the old hold plus a new press stays held");
            jump.ArmCut(false); Assert.That(jump.ObserveHold(false, true), Is.False, "wall and dash-cancel jumps are never cut");
            jump.ArmCut(true); jump.Reset(); Assert.That(jump.CutArmed || jump.Held, Is.False);
            jump.ArmCut(true); Assert.That(jump.ObserveHold(false, false), Is.True, "a buffered jump released before landing is cut on its first tick");
        }
        [Test] public void LandingReservationLengthComesFromTheProfile()
        {
            var data = Movement().ToData(); data.jumpAssist = new JumpAssistData { groundBufferDuration = .12f, releaseVelocityScale = 1 };
            Assert.That(LocomotionProfile.TryCreate(data, out var movement, out var error), Is.True, error);
            var jump = new PlayerJumpExecution(); jump.Press(0, movement, out _); jump.Press(0, movement, out _); jump.Press(1, movement, out _);
            Assert.That(jump.BufferedUntil, Is.EqualTo(1.12f).Within(.000001f));
            Assert.That(jump.Land(1.11f, movement, out float velocity), Is.True); Assert.That(velocity, Is.EqualTo(20));
        }
    }
}
