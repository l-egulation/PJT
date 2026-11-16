using Deviant.Combat.Contracts;

namespace Deviant.Combat
{
    public partial class CombatPlayer
    {
        // Movement the player has been taught. Everything is open by default (runs, the village after the
        // opening); the opening closes them all and opens each one when its guide card appears.
        public MovementAbilities Abilities { get; private set; } = MovementAbilities.All;

        // Animation set drawn instead of the weapon form's (presentation only): the village is walked bare-handed, and
        // the opening shows the hero before and after the ink. Null draws the weapon form as usual.
        public string AppearanceOverride { get; set; }

        public void SetMovementAbilities(MovementAbilities abilities)
        {
            Abilities = abilities;
            jumps.Limit = !abilities.Has(MovementAbilities.Jump) ? 0 : abilities.Has(MovementAbilities.AirJump) ? 2 : 1;
            if (!abilities.Has(MovementAbilities.WallCling)) CancelWallMovement(CombatCommandReason.Unavailable);
        }
    }
}
