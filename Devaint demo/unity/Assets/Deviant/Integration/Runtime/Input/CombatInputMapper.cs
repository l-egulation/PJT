using System;
using Deviant.Combat.Contracts;
using Deviant.Input.Contracts;

namespace Deviant.Integration.Input
{
    // Stateful translation only; it never submits a command or advances combat.
    // The router checks actor/control ownership before admitting its output.
    public sealed class CombatInputMapper
    {
        readonly float axisThreshold;
        int axis, climb;
        CombatButtons held, jumpButton;
        bool jumping;
        long revision, sequence;
        public CombatInput Current => new CombatInput(axis, climbAxis: climb, heldSpecials: held);
        public CombatInputMapper(float axisThreshold = .5f)
        {
            if (float.IsNaN(axisThreshold) || axisThreshold <= 0 || axisThreshold > 1)
                throw new ArgumentOutOfRangeException(nameof(axisThreshold));
            this.axisThreshold = axisThreshold;
        }
        // Poll even when there are no events: invalidation must release holds.
        public void Synchronize(long inputRevision)
        {
            if (inputRevision < 0) throw new ArgumentOutOfRangeException(nameof(inputRevision));
            if (inputRevision <= revision) return;
            revision = inputRevision; sequence = 0; axis = climb = 0;
            held = jumpButton = CombatButtons.None; jumping = false;
        }
        public bool TryMap(GameInputEvent input, out CombatInput mapped)
        {
            mapped = default;
            if (input.Revision < revision || input.Sequence <= 0) return false;
            Synchronize(input.Revision);
            if (input.Sequence <= sequence) return false;
            sequence = input.Sequence;
            if (input.Action == GameAction.Move && input.Phase == InputPhase.Changed)
            {
                axis = Direction(input.Value.X); climb = Direction(input.Value.Y);
                mapped = Current; return true;
            }
            if (input.Phase == InputPhase.Changed) return false;
            var button = Button(input.Action);
            if (button == CombatButtons.None) return false;
            bool pressed = input.Phase == InputPhase.Pressed;
            if (input.Action == GameAction.Jump)
            {
                if (pressed) { jumpButton = climb < 0 ? CombatButtons.Drop : CombatButtons.Jump; jumping = true; }
                else if (!jumping) return false;
                button = jumpButton;
                if (!pressed) jumping = false;
            }
            if ((button & CombatInput.SlotSpecials) != 0)
            { if (pressed) held |= button; else held &= ~button; }
            mapped = new CombatInput(axis, pressed ? button : CombatButtons.None,
                released: pressed ? CombatButtons.None : button, climbAxis: climb, heldSpecials: held);
            return true;
        }
        public bool TryMap(GameInputEvent input, CombatInputSession session, long nextSequence, out CombatInputSample sample)
        {
            sample = default;
            if (!TryMap(input, out var mapped)) return false;
            var source = input.Device.Family == InputDeviceFamily.KeyboardMouse ? CombatInputSource.Keyboard :
                input.Device.Family == InputDeviceFamily.Gamepad || input.Device.Family == InputDeviceFamily.PlayStation || input.Device.Family == InputDeviceFamily.Xbox
                    ? CombatInputSource.Gamepad : CombatInputSource.OtherDevice;
            sample = new CombatInputSample(session, nextSequence, mapped, source, input.Device.Id,
                CombatInputTimeDomain.Realtime, input.Timestamp);
            return true;
        }
        int Direction(float value) => value >= axisThreshold ? 1 : value <= -axisThreshold ? -1 : 0;
        static CombatButtons Button(GameAction action)
        {
            switch (action)
            {
                case GameAction.Weapon1Basic: return CombatButtons.Weapon1Basic;
                case GameAction.Weapon2Basic: return CombatButtons.Weapon2Basic;
                case GameAction.Weapon1Special: return CombatButtons.Weapon1Special;
                case GameAction.Weapon2Special: return CombatButtons.Weapon2Special;
                case GameAction.Dash: return CombatButtons.Dash;
                case GameAction.Jump: return CombatButtons.Jump;
                default: return CombatButtons.None;
            }
        }
    }
}
