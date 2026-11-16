using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class Chapter1DownKeyTests
    {
        [UnityTearDown] public IEnumerator Cleanup() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static CombatInput[] Send(Keyboard keyboard, CombatKeyboardInput adapter, CombatArena arena, ICombatInputCapture capture, params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); InputSystem.Update();
            adapter.SendMessage("Update"); arena.SendMessage("FixedUpdate");
            var inputs = new List<CombatInput>();
            while (capture.TryRead(out var batch)) inputs.AddRange(batch.Receipts.Select(r => r.Sample.Input));
            return inputs.ToArray();
        }
        [UnityTest] public IEnumerator DownArrowDropsAndLegacyMovementKeysDoNotMove()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false; EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed = 42;
            yield return new EnterPlayMode();
            ForestCombatWorld world = null;
            for (int i = 0; i < 600 && !(world && world.Ready); i++) { yield return null; world = Object.FindAnyObjectByType<ForestCombatWorld>(); }
            Assert.IsTrue(world && world.Ready);
            var arena = world.arena; var player = arena.Player;
            var offers = world.GetComponent<Chapter1StartingBlueprints>(); player.Position = offers.OfferPosition(0); Assert.IsTrue(offers.TryChoose(0));
            foreach (var e in arena.enemies) if (e) { e.aggressive = false; e.CancelAttack(); }
            var adapter = player.GetComponent<CombatKeyboardInput>();
            Keyboard keyboard = null; var settings = InputSystem.settings;
            var background = settings.backgroundBehavior; var editor = settings.editorInputBehaviorInPlayMode;
            try
            {
                settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
                using (var capture = player.BeginInputCapture(128))
                {
                    Send(keyboard, adapter, arena, capture);
                    var held = Send(keyboard, adapter, arena, capture, Key.DownArrow);
                    Assert.That(held.Last().ClimbAxis, Is.EqualTo(-1), "continuous descent");
                    var press = Send(keyboard, adapter, arena, capture, Key.DownArrow, Key.Space);
                    Assert.That(press.Any(i => (i.Pressed & CombatButtons.Drop) != 0), Is.True, "Down then Space");
                    Assert.That(press.All(i => (i.Pressed & CombatButtons.Jump) == 0), Is.True);
                    var release = Send(keyboard, adapter, arena, capture, Key.DownArrow);
                    Assert.That(release.Any(i => (i.Released & CombatButtons.Drop) != 0), Is.True, "Space releases its original Drop intent");
                    Send(keyboard, adapter, arena, capture);
                    var together = Send(keyboard, adapter, arena, capture, Key.DownArrow, Key.Space);
                    Assert.That(together.Any(i => (i.Pressed & CombatButtons.Drop) != 0 && i.ClimbAxis == -1), Is.True, "simultaneous Down+Space event");
                    Send(keyboard, adapter, arena, capture);
                    foreach (var legacy in new[] { Key.A, Key.S, Key.D })
                    {
                        var inputs = Send(keyboard, adapter, arena, capture, legacy);
                        Assert.That(inputs.All(i => i.Axis == 0 && i.ClimbAxis == 0), Is.True, legacy.ToString());
                    }
                    Assert.That(Send(keyboard, adapter, arena, capture, Key.S, Key.Space)
                        .Any(i => (i.Pressed & CombatButtons.Jump) != 0 && (i.Pressed & CombatButtons.Drop) == 0), Is.True);
                    Assert.That(Send(keyboard, adapter, arena, capture).Last().ClimbAxis, Is.Zero);
                    Assert.That(Send(keyboard, adapter, arena, capture, Key.Space).Any(i => (i.Pressed & CombatButtons.Jump) != 0), Is.True, "Space alone still jumps");
                }
            }
            finally
            {
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                settings.backgroundBehavior = background; settings.editorInputBehaviorInPlayMode = editor;
            }
        }
    }
}
