using System;
using Deviant.Input.Contracts;
using UnityEngine.InputSystem;

namespace Deviant.Input.Unity
{
    // Paths describe game intent. All physical controls/processors/interactions
    // belong to the caller's .inputactions asset, including per-device overrides.
    public static class GameInputActions
    {
        public static InputContexts Context(GameAction action)
        {
            switch (action)
            {
                case GameAction.Move: case GameAction.Weapon1Basic: case GameAction.Weapon2Basic:
                case GameAction.Weapon1Special: case GameAction.Weapon2Special: case GameAction.Dash: case GameAction.Jump:
                case GameAction.SwapWeaponOrder:
                    return InputContexts.Gameplay;
                case GameAction.Interact: case GameAction.CheckpointInstall: case GameAction.CheckpointReturn: return InputContexts.World;
                case GameAction.Navigate: case GameAction.Submit: case GameAction.Cancel: case GameAction.Advance: case GameAction.Equip:
                case GameAction.MapPan: case GameAction.MapZoomIn: case GameAction.MapZoomOut: case GameAction.MapFocus: case GameAction.MapFit:
                case GameAction.RerollReward: case GameAction.InventoryRemove: case GameAction.InventoryRename:
                case GameAction.Point: case GameAction.Click: case GameAction.Scroll: return InputContexts.UI;
                case GameAction.Pause: case GameAction.Inventory: case GameAction.Map: case GameAction.Help: case GameAction.OpenReward: return InputContexts.Shortcuts;
                case GameAction.DevelopmentReset: return InputContexts.Debug;
                default: throw new ArgumentOutOfRangeException(nameof(action));
            }
        }
        public static string Path(GameAction action) => Context(action) + "/" + action;
        public static bool IsVector(GameAction action) => action == GameAction.Move || action == GameAction.Navigate || action == GameAction.Point || action == GameAction.Scroll || action == GameAction.MapPan;
        public static void Validate(GameAction id, InputAction action)
        {
            bool vector = IsVector(id);
            if (action.type != (vector ? InputActionType.Value : InputActionType.Button) ||
                action.expectedControlType != (vector ? "Vector2" : "Button"))
                throw new ArgumentException(Path(id) + " must be " + (vector ? "Value/Vector2." : "Button/Button."));
            // Tap/Hold/Press interactions redefine performed/canceled. The common
            // stream always means physical press/release; charging belongs to combat.
            if (!string.IsNullOrEmpty(action.interactions))
                throw new ArgumentException(Path(id) + " must preserve press/release without interactions.");
            foreach (var binding in action.bindings)
                if (!string.IsNullOrEmpty(binding.effectiveInteractions))
                    throw new ArgumentException(Path(id) + " has a binding interaction that changes edge semantics.");
        }
    }
}
