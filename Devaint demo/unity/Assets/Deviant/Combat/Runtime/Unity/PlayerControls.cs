using System;
using Deviant.Input.Contracts;
using Deviant.Input.Unity;

namespace Deviant.Combat
{
    public static class PlayerControls
    {
        public static LocalInput For(CombatPlayer player)
        {
            var adapter = player ? player.GetComponent<CombatKeyboardInput>() : null;
            return adapter ? adapter.Actions : null;
        }
        public static bool Pressed(CombatPlayer player, GameAction action)
        {
            var input = For(player);
            if (!input) return false;
            bool world = action == GameAction.Interact || action == GameAction.CheckpointInstall || action == GameAction.CheckpointReturn;
            if (!world) return input.Pressed(action);
            if (input.GameplaySuppressed) return false;
            var adapter = player.GetComponent<CombatKeyboardInput>();
            long token = adapter.CurrentCaptureToken;
            for (int i = 0; i < input.EventCount; i++)
            {
                var value = input.EventAt(i);
                if (!input.IsConsumed(i) && value.Action == action && value.Phase == InputPhase.Pressed && value.CaptureToken == token) return true;
            }
            return false;
        }
        public static bool Take(CombatPlayer player, GameAction action) => For(player)?.Take(action) ?? false;
        public static bool Held(CombatPlayer player, GameAction action) => For(player)?.Read(action).IsPressed ?? false;
        public static bool TryPerform(CombatPlayer player, GameAction action, Func<bool> execute)
        {
            var input = For(player);
            if (!input || !Pressed(player, action) || !execute()) return false;
            input.Take(action); return true;
        }
        public static string Label(CombatPlayer player, GameAction action) => For(player)?.Label(action) ?? action.ToString();
        public static string Prompt(CombatPlayer player, string template) => For(player)?.FormatPrompt(template) ?? template;
    }
}
