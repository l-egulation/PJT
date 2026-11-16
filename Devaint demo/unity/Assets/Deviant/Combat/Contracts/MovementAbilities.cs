using System;

namespace Deviant.Combat.Contracts
{
    // Movement the player may use. Wall jumps belong to WallCling because they start from a held wall.
    [Flags]
    public enum MovementAbilities
    {
        None = 0,
        Jump = 1,
        AirJump = 2,
        WallCling = 4,
        Dash = 8,
        All = Jump | AirJump | WallCling | Dash,
    }

    public static class MovementAbilitiesExtensions
    {
        public static bool Has(this MovementAbilities set, MovementAbilities ability) => (set & ability) == ability;

        // Data names used by the opening script.
        public static bool TryParse(string name, out MovementAbilities ability)
        {
            switch (name)
            {
                case "jump": ability = MovementAbilities.Jump; return true;
                case "airJump": ability = MovementAbilities.AirJump; return true;
                case "wall": ability = MovementAbilities.WallCling; return true;
                case "dash": ability = MovementAbilities.Dash; return true;
                default: ability = MovementAbilities.None; return false;
            }
        }
    }
}
