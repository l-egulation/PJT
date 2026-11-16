using Deviant.Combat.Contracts;

namespace Deviant.Combat.Execution
{
    // The current tick's arbitration projection, not the ingress history.
    // Individual samples and their assignment belong to CombatInputIngress.
    public sealed class CombatInputBuffer
    {
        public CombatInput Current { get; private set; }
        public void Submit(CombatInput input) => Current = new CombatInput(input.Axis, Current.Pressed | input.Pressed, input.SpecialHeld, Current.Released | input.Released, input.ClimbAxis, input.HeldSpecials);
        public void SetContinuous(int axis, bool specialHeld) => Current = new CombatInput(axis, Current.Pressed, specialHeld, Current.Released, Current.ClimbAxis, Current.HeldSpecials);
        public void ClearPressed() => Current = new CombatInput(Current.Axis, CombatButtons.None, Current.SpecialHeld, climbAxis: Current.ClimbAxis, heldSpecials: Current.HeldSpecials);
        public void Clear() => Current = default;
    }
}
