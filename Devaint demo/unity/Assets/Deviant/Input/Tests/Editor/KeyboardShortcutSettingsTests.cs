using System.Linq;
using Deviant.Input.Contracts;
using Deviant.Input.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Deviant.Input.Tests
{
    public sealed class KeyboardShortcutSettingsTests : InputTestFixture
    {
        [Test]
        public void ReassignsMovementAndInventoryRejectsConflictsAndRestoresDefaults()
        {
            const string preference = "deviant.keyboard-shortcuts.v1";
            string saved = PlayerPrefs.HasKey(preference) ? PlayerPrefs.GetString(preference) : null;
            var owner = new GameObject("shortcut settings test");
            var nextOwner = new GameObject("fresh input source");
            try
            {
                PlayerPrefs.DeleteKey(preference);
                var keyboard = InputSystem.AddDevice<Keyboard>();
                var input = owner.AddComponent<LocalInput>();
                input.Source.SetFocused(true);
                input.Source.SetDevices(keyboard);

                var move = input.Source.GetAction(GameAction.Move);
                int left = Enumerable.Range(0, move.bindings.Count)
                    .Single(i => move.bindings[i].path == "<Keyboard>/leftArrow");
                var inventory = input.Source.GetAction(GameAction.Inventory);
                int bag = Enumerable.Range(0, inventory.bindings.Count)
                    .Single(i => inventory.bindings[i].path == "<Keyboard>/tab");

                Assert.That(KeyboardShortcutSettings.TryAssign(input, GameAction.Move, left, keyboard.qKey, out var conflict), Is.False);
                Assert.That(conflict, Is.EqualTo(GameAction.Weapon1Special));
                Assert.That(KeyboardShortcutSettings.TryAssign(input, GameAction.Move, left, keyboard.jKey, out _), Is.True);
                Assert.That(KeyboardShortcutSettings.TryAssign(input, GameAction.Inventory, bag, keyboard.nKey, out var renameConflict), Is.False);
                Assert.That(renameConflict, Is.EqualTo(GameAction.InventoryRename));
                Assert.That(KeyboardShortcutSettings.TryAssign(input, GameAction.Inventory, bag, keyboard.iKey, out _), Is.True);
                Assert.That(input.FormatPrompt("{Move.Left} 이동 · {Inventory} 가방"), Is.EqualTo("J 이동 · I 가방"));

                Press(keyboard.leftArrowKey);
                Assert.That(input.Read(GameAction.Move).X, Is.Zero);
                Release(keyboard.leftArrowKey);
                Press(keyboard.jKey);
                Assert.That(input.Read(GameAction.Move).X, Is.EqualTo(-1));
                Release(keyboard.jKey);

                var nextInput = nextOwner.AddComponent<LocalInput>();
                Assert.That(nextInput.FormatPrompt("{Move.Left} · {Inventory}"), Is.EqualTo("J · I"));
                KeyboardShortcutSettings.RestoreDefaults(nextInput);
                Assert.That(nextInput.FormatPrompt("{Move.Left} · {Inventory}"), Is.EqualTo("← · Tab"));
                // Short keycap names on every screen (user 2026-11-13), never the engine's 「Left Shift」.
                Assert.That(nextInput.FormatPrompt("[{Dash}] [{Pause}] [{Jump}] [{Move.Up}]"), Is.EqualTo("[Shift] [Esc] [Space] [↑]"));
                nextInput.Source.SetFocused(true);
                nextInput.Source.SetDevices(keyboard);
                foreach (var (key, action) in new[]
                {
                    (keyboard.aKey, GameAction.Weapon1Basic), (keyboard.sKey, GameAction.Weapon2Basic),
                    (keyboard.qKey, GameAction.Weapon1Special), (keyboard.wKey, GameAction.Weapon2Special),
                    (keyboard.leftShiftKey, GameAction.Dash), (keyboard.rightShiftKey, GameAction.Dash),
                    (keyboard.fKey, GameAction.Interact), (keyboard.tabKey, GameAction.Inventory)
                })
                {
                    Press(key);
                    Assert.That(nextInput.Read(action).IsPressed, Is.True, $"Default {key.name}: {action}");
                    Release(key);
                }
                Assert.That(PlayerPrefs.HasKey(preference), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(nextOwner);
                if (saved == null) PlayerPrefs.DeleteKey(preference);
                else PlayerPrefs.SetString(preference, saved);
                PlayerPrefs.Save();
            }
        }
    }
}
