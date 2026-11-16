using Deviant.Combat.Contracts;
using Deviant.Input.Contracts;
using Deviant.Integration.Input;
using NUnit.Framework;

namespace Deviant.Input.Tests
{
    public class CombatInputMapperTests
    {
        static GameInputEvent Event(long sequence, GameAction action, InputPhase phase, float x = 1, float y = 0, long revision = 1)
            => new GameInputEvent(revision, sequence, action, phase, new GameInputValue(x, y),
                new InputDeviceInfo(42, InputDeviceFamily.Gamepad, "Gamepad"), 12 + sequence * .01);

        [Test] public void SlotChargesAndAttacksRemainIndependent()
        {
            var mapper = new CombatInputMapper();
            mapper.TryMap(Event(1, GameAction.Weapon1Special, InputPhase.Pressed), out var first);
            mapper.TryMap(Event(2, GameAction.Weapon2Special, InputPhase.Pressed), out var second);
            mapper.TryMap(Event(3, GameAction.Weapon1Special, InputPhase.Released, 0), out var released);
            Assert.That(first.Pressed, Is.EqualTo(CombatButtons.Weapon1Special));
            Assert.That(second.HeldSpecials, Is.EqualTo(CombatInput.SlotSpecials));
            Assert.That(released.Released, Is.EqualTo(CombatButtons.Weapon1Special));
            Assert.That(released.HeldSpecials, Is.EqualTo(CombatButtons.Weapon2Special));
            mapper.TryMap(Event(4, GameAction.Weapon2Basic, InputPhase.Pressed), out var basic);
            Assert.That(basic.Pressed, Is.EqualTo(CombatButtons.Weapon2Basic));
            Assert.That(basic.HeldSpecials, Is.EqualTo(CombatButtons.Weapon2Special));
        }
        [Test] public void DropReleaseKeepsItsOriginalMeaningAfterDirectionChanges()
        {
            var mapper = new CombatInputMapper();
            mapper.TryMap(Event(1, GameAction.Move, InputPhase.Changed, .8f, -.8f), out var move);
            mapper.TryMap(Event(2, GameAction.Jump, InputPhase.Pressed), out var press);
            mapper.TryMap(Event(3, GameAction.Move, InputPhase.Changed, -.01f, .8f), out var changed);
            mapper.TryMap(Event(4, GameAction.Jump, InputPhase.Released, 0), out var release);
            Assert.That(move.Axis, Is.EqualTo(1)); Assert.That(move.ClimbAxis, Is.EqualTo(-1));
            Assert.That(press.Pressed, Is.EqualTo(CombatButtons.Drop));
            Assert.That(changed.Axis, Is.Zero); Assert.That(changed.ClimbAxis, Is.EqualTo(1));
            Assert.That(release.Released, Is.EqualTo(CombatButtons.Drop));
            mapper.TryMap(Event(5, GameAction.Jump, InputPhase.Pressed), out var jump);
            Assert.That(jump.Pressed, Is.EqualTo(CombatButtons.Jump));
        }
        [Test] public void RevisionInvalidatesHoldsEvenWithoutEventsAndRejectsOldDeliveries()
        {
            var mapper = new CombatInputMapper();
            mapper.TryMap(Event(1, GameAction.Weapon1Special, InputPhase.Pressed), out _);
            mapper.TryMap(Event(2, GameAction.Move, InputPhase.Changed, 1, 1), out _);
            mapper.Synchronize(2);
            Assert.That(mapper.Current, Is.EqualTo(default(CombatInput)));
            Assert.That(mapper.TryMap(Event(3, GameAction.Weapon1Basic, InputPhase.Pressed), out _), Is.False);
            Assert.That(mapper.TryMap(Event(4, GameAction.Jump, InputPhase.Released, revision: 2), out _), Is.False);
            Assert.That(mapper.TryMap(Event(5, GameAction.Jump, InputPhase.Pressed, revision: 2), out _), Is.True);
            Assert.That(mapper.TryMap(Event(5, GameAction.Jump, InputPhase.Pressed, revision: 2), out _), Is.False);
        }
        [Test] public void WorldAndUIActionsNeverBecomeCombatCommands()
        {
            var mapper = new CombatInputMapper();
            Assert.That(mapper.TryMap(Event(1, GameAction.Interact, InputPhase.Pressed), out _), Is.False);
            Assert.That(mapper.TryMap(Event(2, GameAction.Cancel, InputPhase.Pressed), out _), Is.False);
            Assert.That(mapper.TryMap(Event(3, GameAction.Pause, InputPhase.Pressed), out _), Is.False);
        }
        [Test] public void HardwareSampleKeepsItsDeviceClockAndTheCallersActorSession()
        {
            var mapper = new CombatInputMapper(); var input = Event(1, GameAction.Weapon1Basic, InputPhase.Pressed);
            var actor = new CombatInputSession(7, 8, 9);
            Assert.That(mapper.TryMap(input, actor, 63, out var sample), Is.True);
            Assert.That(sample.Session, Is.EqualTo(actor)); Assert.That(sample.Sequence, Is.EqualTo(63));
            Assert.That(sample.Source, Is.EqualTo(CombatInputSource.Gamepad)); Assert.That(sample.DeviceId, Is.EqualTo(42));
            Assert.That(sample.TimeDomain, Is.EqualTo(CombatInputTimeDomain.Realtime)); Assert.That(sample.Timestamp, Is.EqualTo(input.Timestamp));
        }
    }
}
