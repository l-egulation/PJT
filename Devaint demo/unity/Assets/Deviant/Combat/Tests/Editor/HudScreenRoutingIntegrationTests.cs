using System;
using System.Collections;
using System.Reflection;
using Deviant.Control;
using Deviant.Control.Contracts;
using Deviant.UI.Contracts;
using Deviant.UI.Presentation;
using Deviant.UI.Unity;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class HudScreenRoutingIntegrationTests
    {
        sealed class Devices : IDisposable
        {
            readonly InputSettings settings = InputSystem.settings;
            readonly InputSettings.BackgroundBehavior background;
            readonly InputSettings.EditorInputBehaviorInPlayMode editorInput;
            public readonly Keyboard Keyboard;
            public readonly Gamepad Pad;
            public readonly Mouse Mouse;
            public Devices()
            {
                background = settings.backgroundBehavior; editorInput = settings.editorInputBehaviorInPlayMode;
                settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                Keyboard = InputSystem.AddDevice<Keyboard>(); Pad = InputSystem.AddDevice<Gamepad>(); Mouse = InputSystem.AddDevice<Mouse>();
                Release(); Keyboard.MakeCurrent(); Pad.MakeCurrent(); Mouse.MakeCurrent();
            }
            public void Key(params Key[] keys) { InputSystem.QueueStateEvent(Keyboard, new KeyboardState(keys)); InputSystem.Update(); Keyboard.MakeCurrent(); }
            public void PadBack(bool pressed)
            {
                InputSystem.QueueStateEvent(Pad, pressed ? new GamepadState().WithButton(GamepadButton.East) : new GamepadState());
                InputSystem.Update(); Pad.MakeCurrent();
            }
            public void Pointer(Vector2 position, bool pressed)
            {
                InputSystem.QueueStateEvent(Mouse, pressed ? new MouseState { position = position }.WithButton(MouseButton.Left) : new MouseState { position = position });
                InputSystem.Update(); Mouse.MakeCurrent();
            }
            public void Release()
            {
                InputSystem.QueueStateEvent(Keyboard, new KeyboardState()); InputSystem.QueueStateEvent(Pad, new GamepadState());
                InputSystem.QueueStateEvent(Mouse, new MouseState()); InputSystem.Update();
            }
            public void Dispose()
            {
                InputSystem.RemoveDevice(Keyboard); InputSystem.RemoveDevice(Pad); InputSystem.RemoveDevice(Mouse);
                settings.backgroundBehavior = background; settings.editorInputBehaviorInPlayMode = editorInput;
            }
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            Time.timeScale = 1;
        }

        [UnityTest] public IEnumerator ActualGameHudUsesExistingKeyboardAndPadCancelExactlyOnceWithFocusReturn()
        {
            var open = OpenGame(); while (open.MoveNext()) yield return open.Current;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var hud = Hud(arena);
            var events = EventSystem.current; var module = events.GetComponent<InputSystemUIInputModule>();
            var back = module.cancel.action; var actionAsset = module.actionsAsset; var router = Router(hud);
            var screen = hud.GetComponent<ScreenView>(); var input = hud.GetComponent<ScreenInputBinding>();
            Assert.That(back.enabled, Is.True); Assert.That(router.OpenCount, Is.Zero);
            Assert.That(hud.GetComponents<ScreenView>().Length, Is.EqualTo(1));
            Assert.That(hud.GetComponents<ScreenInputBinding>().Length, Is.EqualTo(1));
            Assert.That(Button(hud, "mixButton").gameObject.activeSelf, Is.False);
            using (var devices = new Devices())
            {
                events.SetSelectedGameObject(Button(hud, "bagButton").gameObject);
                Click(hud, "bagButton");
                Assert.That(router.OpenCount, Is.EqualTo(1)); Assert.That(screen.BindingId, Is.Not.EqualTo(Guid.Empty));
                Assert.That(events.currentSelectedGameObject, Is.EqualTo(Button(hud, "closeButton").gameObject));
                Assert.That(arena.Controls.Current.SimulationPaused, Is.False); Assert.That(Dialog(hud).activeSelf, Is.True);
                devices.Key(Key.Escape);
                Assert.That(router.OpenCount, Is.Zero); Assert.That(Dialog(hud).activeSelf, Is.False);
                Assert.That(input.LastResult.Consumed && input.LastResult.Closed, Is.True);
                Assert.That(events.currentSelectedGameObject, Is.EqualTo(Button(hud, "bagButton").gameObject));
                hud.SendMessage("Update");
                Assert.That(router.OpenCount, Is.Zero, "The removed Escape poll cannot reopen Pause for the same consumed edge.");
                devices.Key(Key.Escape); Assert.That(router.OpenCount, Is.Zero, "Held input is not a second edge.");
                devices.Key(); devices.Key(Key.Escape);
                Assert.That(router.OpenCount, Is.EqualTo(1)); Assert.That(Paused(hud), Is.True);
                Assert.That(input.LastResult.Consumed, Is.False, "Only a fresh unconsumed physical Back opens the existing pause screen.");
                devices.Key(); devices.PadBack(true);
                Assert.That(router.OpenCount, Is.Zero); Assert.That(Paused(hud), Is.False);
                Assert.That(input.LastResult.Consumed && input.LastResult.Closed, Is.True);
                devices.PadBack(false);
                Assert.That(module.actionsAsset, Is.SameAs(actionAsset)); Assert.That(module.cancel.action, Is.SameAs(back));
                Assert.That(back.enabled, Is.True, "HUD never owns the module action's enabled state.");
            }
        }

        [UnityTest] public IEnumerator ExternalOverlayAndNavigationRestrictionConsumeBackWithoutClosingOrOpeningHudBehindThem()
        {
            var open = OpenGame(); while (open.MoveNext()) yield return open.Current;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var hud = Hud(arena); var router = Router(hud);
            var events = EventSystem.current; var canvas = hud.transform;
            var panel = new GameObject("HudRoutingExternalPanel", typeof(RectTransform), typeof(CanvasGroup)); panel.transform.SetParent(canvas, false);
            var buttonObject = new GameObject("ExternalClose", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button)); buttonObject.transform.SetParent(panel.transform, false);
            var owner = new GameObject("HudRoutingExternalOwner"); owner.transform.SetParent(canvas, false);
            var view = owner.AddComponent<ScreenView>(); panel.SetActive(false);
            view.Configure(panel, panel.GetComponent<CanvasGroup>(), events, buttonObject.GetComponent<Button>(), null, true, true);
            try
            {
                using (var external = arena.Controls.OpenScope("independent dialogue")) using (var devices = new Devices())
                {
                    var otherInput = external.BlockInput(InputChannels.Interaction); var otherPause = external.PauseSimulation();
                    Click(hud, "bagButton");
                    Assert.That(router.TryOpen("external overlay", view, new ScreenPolicy(10, InputChannels.Combat, false, true), out var overlay, out var error), Is.True, error);
                    Assert.That(hud.GetComponent<ScreenView>().IsFocused, Is.False);
                    devices.Key(Key.Escape);
                    Assert.That(overlay.Status, Is.EqualTo(ScreenStatus.Closed)); Assert.That(router.OpenCount, Is.EqualTo(1));
                    Assert.That(Dialog(hud).activeSelf, Is.True); Assert.That(Paused(hud), Is.False);
                    Assert.That(otherInput.IsActive && otherPause.IsActive, Is.True);
                    devices.Key(); var navigation = external.BlockInput(InputChannels.Navigation);
                    devices.Key(Key.Escape);
                    Assert.That(router.OpenCount, Is.EqualTo(1)); Assert.That(Dialog(hud).activeSelf, Is.True);
                    Assert.That(hud.GetComponent<ScreenInputBinding>().LastResult.Consumed, Is.True);
                    navigation.Dispose(); devices.Key(); devices.Key(Key.Escape);
                    Assert.That(router.OpenCount, Is.Zero); Assert.That(otherPause.IsActive, Is.True);
                    Assert.That(arena.Controls.Current.SimulationPaused, Is.True);
                    devices.Key(); hud.enabled = false;
                    Assert.That(router.TryOpen("still externally owned", view, new ScreenPolicy(10, InputChannels.None, false, true), out var remaining, out error), Is.True, error);
                    Assert.That(remaining.Status, Is.EqualTo(ScreenStatus.Open));
                    remaining.Dispose();
                }
            }
            finally { Object.Destroy(owner); Object.Destroy(panel); }
        }

        [UnityTest] public IEnumerator ClosingPointerAndBackFramesNeverLeakCombatPressesAndNextFrameResumesNormally()
        {
            var open = OpenGame(); while (open.MoveNext()) yield return open.Current;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var hud = Hud(arena); var player = arena.Player;
            player.UseTestInput = false; player.CancelAction();
            using (var devices = new Devices())
            {
                Canvas.ForceUpdateCanvases();
                var rect = (RectTransform)Button(hud, "bagButton").transform;
                var point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
                devices.Pointer(point, true); devices.Key(Key.A);
                hud.SendMessage("Update"); TickInput(arena);
                Assert.That(player.InputBlocked, Is.True); Assert.That(player.currentCast, Is.Null);
                devices.Key();
                var outside = new Vector2(-1000, -1000);
                devices.Pointer(outside, true); hud.SendMessage("Update");
                Assert.That(player.InputBlocked, Is.True, "Dragging a HUD-owned press outside does not transfer it to combat.");
                devices.Pointer(outside, false); hud.SendMessage("Update");
                int releasedAt = Time.frameCount;
                while (Time.frameCount == releasedAt) yield return null;
                hud.SendMessage("Update");
                Assert.That(Dialog(hud).activeSelf, Is.False, "A canceled physical drag is not a second manual Bag request.");
                Assert.That(player.InputBlocked, Is.False);
                Click(hud, "bagButton"); Click(hud, "closeButton"); devices.Key(Key.A);
                hud.SendMessage("Update"); TickInput(arena);
                Assert.That(player.InputBlocked, Is.True); Assert.That(player.currentCast, Is.Null, "Closing a dialog cannot admit an attack in that frame.");
                int closedAt = Time.frameCount;
                devices.Key();
                while (Time.frameCount == closedAt) yield return null;
                hud.SendMessage("Update"); Assert.That(player.InputBlocked, Is.False);
                TickInput(arena); Assert.That(player.currentCast, Is.Null, "The discarded press must not replay.");
                Click(hud, "bagButton");
                devices.Key(Key.Escape, Key.A);
                Assert.That(Dialog(hud).activeSelf, Is.False);
                hud.SendMessage("Update"); TickInput(arena);
                Assert.That(player.currentCast, Is.Null); Assert.That(player.InputBlocked, Is.True);
                int backAt = Time.frameCount; devices.Key();
                while (Time.frameCount == backAt) yield return null;
                hud.SendMessage("Update"); Assert.That(player.InputBlocked, Is.False);
                devices.Key(Key.A); TickInput(arena);
                Assert.That(player.currentCast, Is.Not.Null, "A genuinely new gameplay press after the consumed frame still works.");
            }
        }

        [UnityTest] public IEnumerator ActualPhysicalBagClickRunsOnceAndBackCannotReplayItsQueuedRelease()
        {
            var open = OpenGame(); while (open.MoveNext()) yield return open.Current;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var hud = Hud(arena); var router = Router(hud);
            var module = EventSystem.current.GetComponent<InputSystemUIInputModule>();
            using (var devices = new Devices())
            {
                Canvas.ForceUpdateCanvases();
                var rect = (RectTransform)Button(hud, "bagButton").transform;
                var point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
                int before = (int)hud.GetType().GetProperty("BagRequests").GetValue(hud);
                devices.Pointer(point, true); hud.SendMessage("Update"); module.Process();
                Assert.That((int)hud.GetType().GetProperty("BagRequests").GetValue(hud), Is.EqualTo(before));
                devices.Pointer(point, false); hud.SendMessage("Update"); module.Process();
                Assert.That((int)hud.GetType().GetProperty("BagRequests").GetValue(hud), Is.EqualTo(before + 1));
                Assert.That(Dialog(hud).activeSelf, Is.True); Assert.That(router.OpenCount, Is.EqualTo(1));
                devices.Key(Key.Escape); module.Process();
                Assert.That(Dialog(hud).activeSelf, Is.False); Assert.That(router.OpenCount, Is.Zero);
                int closedAt = Time.frameCount; devices.Key();
                while (Time.frameCount == closedAt) yield return null;
                hud.SendMessage("Update"); module.Process();
                Assert.That((int)hud.GetType().GetProperty("BagRequests").GetValue(hud), Is.EqualTo(before + 1));
                Assert.That(Dialog(hud).activeSelf, Is.False); Assert.That(router.OpenCount, Is.Zero);
                Assert.That(arena.Player.InputBlocked, Is.False);
            }
        }

        [UnityTest] public IEnumerator RebindDisableAndPausedBagKeepOwnedRouterScopesAndExistingHudSemantics()
        {
            var open = OpenGame(); while (open.MoveNext()) yield return open.Current;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var hud = Hud(arena); var original = Router(hud);
            var events = EventSystem.current; var back = events.GetComponent<InputSystemUIInputModule>().cancel.action;
            var rootCanvas = hud.GetComponent<Canvas>(); var dialog = Dialog(hud);
            using (var other = arena.Controls.OpenScope("independent session modal"))
            using (var replacement = new ControlSession()) using (var replacementRouter = new ScreenRouter(replacement))
            {
                var otherPause = other.PauseSimulation(); var otherInput = other.BlockInput(InputChannels.Interaction);
                Click(hud, "pauseButton"); Assert.That(Paused(hud), Is.True);
                var token = hud.GetComponent<ScreenView>().BindingId;
                Click(hud, "bagButton");
                Assert.That(Paused(hud), Is.True); Assert.That(original.OpenCount, Is.EqualTo(1));
                Assert.That(hud.GetComponent<ScreenView>().BindingId, Is.EqualTo(token), "Bag while paused preserves the existing pause ownership.");
                Click(hud, "closeButton");
                Assert.That(Paused(hud), Is.False); Assert.That(otherPause.IsActive, Is.True);
                Bind(hud, replacement, replacementRouter, back, events);
                hud.GetType().GetMethod("UnbindControls").Invoke(hud, new object[] { arena.Controls, original });
                Click(hud, "pauseButton");
                Assert.That(replacementRouter.OpenCount, Is.EqualTo(1)); Assert.That(replacement.Current.SimulationPaused, Is.True);
                hud.enabled = false;
                Assert.That(replacementRouter.OpenCount, Is.Zero); Assert.That(replacement.Current.SimulationPaused, Is.False);
                Assert.That(back.enabled, Is.True); Assert.That(otherPause.IsActive && otherInput.IsActive, Is.True);
                hud.SendMessage("TogglePause"); Assert.That(replacementRouter.OpenCount, Is.Zero);
                hud.enabled = true; Click(hud, "bagButton"); Assert.That(replacementRouter.OpenCount, Is.EqualTo(1));
                Bind(hud, arena.Controls, original, back, events);
                Assert.That(replacementRouter.OpenCount, Is.Zero); Assert.That(rootCanvas, Is.SameAs(hud.GetComponent<Canvas>()));
                Assert.That(dialog, Is.SameAs(Dialog(hud))); Assert.That(hud.GetComponents<ScreenView>().Length, Is.EqualTo(1));
                Assert.That(hud.GetComponents<ScreenInputBinding>().Length, Is.EqualTo(1));
                hud.enabled = false;
            }
        }

        static void Bind(MonoBehaviour hud, IControlRequests controls, ScreenRouter router, InputAction back, EventSystem events)
            => hud.GetType().GetMethod("BindControls").Invoke(hud, new object[] { controls, router, back, events });
        static ScreenRouter Router(MonoBehaviour hud) => (ScreenRouter)typeof(ScreenInputBinding).GetField("router", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hud.GetComponent<ScreenInputBinding>());
        static GameObject Dialog(MonoBehaviour hud) => (GameObject)hud.GetType().GetField("dialog").GetValue(hud);
        static bool Paused(MonoBehaviour hud) => (bool)hud.GetType().GetProperty("Paused").GetValue(hud);
        static Button Button(MonoBehaviour hud, string field) => (Button)hud.GetType().GetField(field).GetValue(hud);
        static void Click(MonoBehaviour hud, string field) => Button(hud, field).onClick.Invoke();
        static MonoBehaviour Hud(CombatArena arena)
        {
            var composition = arena.GetComponent("GameComposition");
            return (MonoBehaviour)composition.GetType().GetField("hud").GetValue(composition);
        }
        static void TickInput(CombatArena arena)
        {
            arena.Player.GetComponent<CombatKeyboardInput>().SendMessage("Update");
            arena.SendMessage("FixedUpdate");
        }
        static IEnumerator OpenGame()
        {
            LegacySceneFixture.Open("Assets/Deviant/Game/Scenes/deviant.unity");
            Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode();
            float deadline = Time.realtimeSinceStartup + 5;
            CombatArena arena;
            while (!(arena = Object.FindAnyObjectByType<CombatArena>()) || !arena.Player || !Hud(arena).GetComponent<ScreenInputBinding>())
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "The actual GameComposition must inject the existing module action and HUD screen.");
                yield return null;
            }
            arena.Player.UseTestInput = true;
            foreach (var enemy in arena.enemies) enemy.aggressive = false;
        }
    }
}
