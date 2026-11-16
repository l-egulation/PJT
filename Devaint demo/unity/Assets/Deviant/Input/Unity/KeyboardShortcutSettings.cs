using System;
using Deviant.Input.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Deviant.Input.Unity
{
    public static class KeyboardShortcutSettings
    {
        const string Preference = "deviant.keyboard-shortcuts.v1";

        public static bool IsKeyboard(InputBinding binding) =>
            binding.path != null && binding.path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase);

        internal static void Load(InputActionSource source)
        {
            var saved = PlayerPrefs.GetString(Preference, "");
            if (saved.Length == 0) return;
            try { source.LoadBindingOverrides(saved); }
            catch (ArgumentException) { PlayerPrefs.DeleteKey(Preference); }
        }

        public static bool TryAssign(LocalInput input, GameAction action, int index, KeyControl key, out GameAction conflict)
        {
            conflict = default;
            var source = input.Source;
            var target = source.GetAction(action);
            if (!IsKeyboard(target.bindings[index])) throw new ArgumentException("Expected a keyboard binding.", nameof(index));

            string path = "<Keyboard>/" + key.name;
            if (SamePath(path, target.bindings[index].effectivePath)) return true;
            foreach (GameAction other in Enum.GetValues(typeof(GameAction)))
            {
                var candidate = source.GetAction(other);
                if (candidate == null || !Conflicts(action, other)) continue;
                foreach (var binding in candidate.bindings)
                {
                    if (!IsKeyboard(binding) || !SamePath(path, binding.effectivePath)) continue;
                    conflict = other;
                    return false;
                }
            }

            source.OverrideBinding(action, index, path);
            PlayerPrefs.SetString(Preference, source.SaveBindingOverrides());
            PlayerPrefs.Save();
            return true;
        }

        public static void RestoreDefaults(LocalInput input)
        {
            var source = input.Source;
            foreach (GameAction action in Enum.GetValues(typeof(GameAction)))
            {
                var native = source.GetAction(action);
                if (native == null) continue;
                for (int i = 0; i < native.bindings.Count; i++)
                    if (IsKeyboard(native.bindings[i]) && native.bindings[i].hasOverrides) native.RemoveBindingOverride(i);
            }
            source.Reset();
            PlayerPrefs.DeleteKey(Preference);
            PlayerPrefs.Save();
        }

        static bool SamePath(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        static bool Conflicts(GameAction a, GameAction b)
        {
            if (a == b) return true;
            // Pause and Cancel can share one key; Back consumes both edges together.
            if (a == GameAction.Pause && b == GameAction.Cancel || b == GameAction.Pause && a == GameAction.Cancel) return false;
            if (a == GameAction.Pause || b == GameAction.Pause) return true;
            if (a == GameAction.Cancel || b == GameAction.Cancel)
            {
                var other = a == GameAction.Cancel ? b : a;
                return GameInputActions.Context(other) == InputContexts.UI || other == GameAction.Inventory || other == GameAction.Map || other == GameAction.Interact;
            }
            return (ActiveIn(a) & ActiveIn(b)) != 0;
        }

        // Actions on separate screens can reuse keys (for example Jump/Advance and Skill/Equip).
        const int Play = 1, Menu = 2, Dialogue = 4, Bag = 8, Map = 16;
        static int ActiveIn(GameAction action)
        {
            switch (action)
            {
                case GameAction.Navigate: case GameAction.Submit: return Menu | Bag | Map;
                case GameAction.Advance: return Dialogue;
                case GameAction.Equip: case GameAction.InventoryRemove: case GameAction.InventoryRename: return Bag;
                case GameAction.RerollReward: return Menu;
                case GameAction.MapZoomIn: case GameAction.MapZoomOut:
                case GameAction.MapFocus: case GameAction.MapFit: return Map;
                case GameAction.Inventory: return Play | Bag;
                case GameAction.Map: return Play | Map;
                case GameAction.Interact: return Play | Menu;
                default: return GameInputActions.Context(action) == InputContexts.UI ? 0 : Play;
            }
        }
    }
}
