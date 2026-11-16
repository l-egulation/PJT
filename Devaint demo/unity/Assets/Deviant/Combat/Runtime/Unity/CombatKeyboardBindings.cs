using System;
using UnityEngine.InputSystem;

namespace Deviant.Combat
{
    // Serialized independently of action arbitration so a future binding UI can
    // edit physical keys without changing combat intents or replay data.
    [Serializable]
    public sealed class CombatKeyboardBindings
    {
        public Key Weapon1Basic = Key.A;
        public Key Weapon2Basic = Key.S;
        public Key Weapon1Special = Key.Q;
        public Key Weapon2Special = Key.W;
        public Key Jump = Key.Space;
        public Key Dash = Key.LeftShift;
        public Key AlternateDash = Key.RightShift;
        public Key MoveLeft = Key.LeftArrow;
        public Key MoveRight = Key.RightArrow;
        public Key ClimbUp = Key.UpArrow;
        public Key ClimbDown = Key.DownArrow;
        public Key Interact = Key.F;
        public Key CheckpointInstall = Key.C;
        public Key CheckpointReturn = Key.V;
        public Key DevelopmentReset = Key.F5;

        // World features read the player's adapter so one mapping owns every key.
        public static CombatKeyboardBindings For(CombatPlayer player)
        {
            var adapter = player ? player.GetComponent<CombatKeyboardInput>() : null;
            return adapter ? adapter.Bindings : new CombatKeyboardBindings();
        }
        public static bool WasPressed(Keyboard keyboard, Key key) => keyboard != null && key != Key.None && keyboard[key].wasPressedThisFrame;
        public static bool IsHeld(Keyboard keyboard, Key key) => keyboard != null && key != Key.None && keyboard[key].isPressed;
    }
}
