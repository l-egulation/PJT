using System;

namespace Deviant.Combat.Contracts
{
    [Flags]
    public enum CombatButtons
    {
        None = 0, Basic = 1, Special = 2, Dash = 4, Jump = 8, Swap = 16, Drop = 32,
        Weapon1Basic = 64, Weapon2Basic = 128, Weapon1Special = 256, Weapon2Special = 512
    }

    // Targeted edges remain distinct through admission, coalescing and replay.
    // Basic/Special and SpecialHeld retain the simulation adapter's active-slot intent.
    public readonly struct CombatInput
    {
        public const int ButtonCount = 10;
        public const CombatButtons TargetedAttacks = CombatButtons.Weapon1Basic | CombatButtons.Weapon2Basic | CombatButtons.Weapon1Special | CombatButtons.Weapon2Special;
        public const CombatButtons SlotSpecials = CombatButtons.Weapon1Special | CombatButtons.Weapon2Special;
        const CombatButtons AllButtons = CombatButtons.Basic | CombatButtons.Special | CombatButtons.Dash | CombatButtons.Jump | CombatButtons.Swap | CombatButtons.Drop | TargetedAttacks;
        public int Axis { get; }
        public int ClimbAxis { get; }
        public bool SpecialHeld { get; }
        public CombatButtons HeldSpecials { get; }
        public CombatButtons Pressed { get; }
        public CombatButtons Released { get; }
        public CombatInput(int axis = 0, CombatButtons pressed = CombatButtons.None, bool specialHeld = false, CombatButtons released = CombatButtons.None, int climbAxis = 0, CombatButtons heldSpecials = CombatButtons.None)
        {
            if (axis < -1 || axis > 1) throw new ArgumentOutOfRangeException(nameof(axis));
            if ((pressed & ~AllButtons) != 0) throw new ArgumentOutOfRangeException(nameof(pressed));
            if ((released & ~AllButtons) != 0) throw new ArgumentOutOfRangeException(nameof(released));
            if ((heldSpecials & ~SlotSpecials) != 0) throw new ArgumentOutOfRangeException(nameof(heldSpecials));
            if (climbAxis < -1 || climbAxis > 1) throw new ArgumentOutOfRangeException(nameof(climbAxis));
            ClimbAxis = climbAxis; Axis = axis; Pressed = pressed; SpecialHeld = specialHeld; Released = released; HeldSpecials = heldSpecials;
        }
        public bool Has(CombatButtons button) => (Pressed & button) != 0;
        public bool IsSpecialHeld(int slot)
        {
            if (slot < 0 || slot > 1) throw new ArgumentOutOfRangeException(nameof(slot));
            return SpecialHeld || (HeldSpecials & (slot == 0 ? CombatButtons.Weapon1Special : CombatButtons.Weapon2Special)) != 0;
        }
    }
}
