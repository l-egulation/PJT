using System;
using System.Collections;
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
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class ScreenInputBindingTests
    {
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static ScreenView View(Transform canvas, EventSystem events, string name)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup)); panel.transform.SetParent(canvas, false);
            var button = new GameObject(name + "Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button)); button.transform.SetParent(panel.transform, false);
            var owner = new GameObject(name + "Owner"); owner.transform.SetParent(canvas, false);
            var view = owner.AddComponent<ScreenView>(); panel.SetActive(false);
            view.Configure(panel, panel.GetComponent<CanvasGroup>(), events, button.GetComponent<Button>(), null, true, true); return view;
        }
        static IScreenHandle Open(ScreenRouter router, ScreenView view, bool allowBack = true)
        {
            Assert.That(router.TryOpen("device fixture", view, new ScreenPolicy(10, InputChannels.Combat, false, allowBack), out var handle, out var error), Is.True, error); return handle;
        }
        [UnityTest] public IEnumerator KeyboardAndPadEdgesCloseOnlyTheCapturedFocusedScreenAndReportConsumption()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode(); yield return null; Assert.That(Application.isPlaying, Is.True);
            var canvas = new GameObject("DeviceScreenCanvas", typeof(RectTransform), typeof(Canvas));
            var events = new GameObject("DeviceScreenEvents", typeof(EventSystem));
            var owner = new GameObject("ScreenDeviceBinding"); var binding = owner.AddComponent<ScreenInputBinding>();
            Keyboard keyboard = null; Gamepad pad = null;
            var settings = InputSystem.settings; var background = settings.backgroundBehavior; var editorInput = settings.editorInputBehaviorInPlayMode;
            using (var controls = new ControlSession()) using (var external = controls.OpenScope("external restriction"))
            using (var router = new ScreenRouter(controls)) using (var action = new InputAction("injected back", InputActionType.Button))
            {
                try
                {
                    settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    keyboard = InputSystem.AddDevice<Keyboard>(); pad = InputSystem.AddDevice<Gamepad>();
                    action.AddBinding("<Keyboard>/escape"); action.AddBinding("<Gamepad>/buttonEast"); action.Enable();
                    int delivered = 0, behind = 0; binding.BackProcessed += result => { delivered++; if (!result.Consumed) behind++; };
                    binding.Configure(router, action);
                    var lower = Open(router, View(canvas.transform, events.GetComponent<EventSystem>(), "Lower"));
                    var upper = Open(router, View(canvas.transform, events.GetComponent<EventSystem>(), "Upper"));
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.QueueStateEvent(pad, new GamepadState()); InputSystem.Update();
                    keyboard.MakeCurrent(); pad.MakeCurrent();
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); InputSystem.Update();
                    Assert.That(upper.Status, Is.EqualTo(ScreenStatus.Closed), $"key={keyboard.escapeKey.isPressed}, action={action.IsPressed()}, controls={action.controls.Count}, deliveries={delivered}, token={router.FocusedBindingId}, consumed={binding.LastResult.Consumed}, error={binding.LastResult.Error}"); Assert.That(lower.Status, Is.EqualTo(ScreenStatus.Open));
                    Assert.That(binding.LastResult.Consumed && binding.LastResult.Closed, Is.True); Assert.That(delivered, Is.EqualTo(1)); Assert.That(behind, Is.Zero);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); InputSystem.Update(); Assert.That(delivered, Is.EqualTo(1));
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
                    var navigation = external.BlockInput(InputChannels.Navigation);
                    InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.East)); InputSystem.Update();
                    Assert.That(lower.Status, Is.EqualTo(ScreenStatus.Open)); Assert.That(binding.LastResult.Consumed, Is.True); Assert.That(binding.LastResult.Closed, Is.False);
                    InputSystem.QueueStateEvent(pad, new GamepadState()); InputSystem.Update(); navigation.Dispose();
                    InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.East)); InputSystem.Update();
                    Assert.That(lower.Status, Is.EqualTo(ScreenStatus.Closed)); Assert.That(behind, Is.Zero);
                    InputSystem.QueueStateEvent(pad, new GamepadState()); InputSystem.Update();
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); InputSystem.Update(); Assert.That(behind, Is.EqualTo(1));
                    binding.enabled = false; Assert.That(action.enabled, Is.True, "The adapter does not own the external action's enabled state.");
                }
                finally
                {
                    binding.Unbind(); if (keyboard != null) InputSystem.RemoveDevice(keyboard); if (pad != null) InputSystem.RemoveDevice(pad);
                    settings.backgroundBehavior = background; settings.editorInputBehaviorInPlayMode = editorInput;
                    Object.Destroy(owner); Object.Destroy(canvas); Object.Destroy(events);
                }
            }
        }
        [UnityTest] public IEnumerator RebindingDuringAnEarlierActionCallbackRejectsTheOldDelivery()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode(); yield return null; Assert.That(Application.isPlaying, Is.True);
            var canvas = new GameObject("RebindScreenCanvas", typeof(RectTransform), typeof(Canvas)); var events = new GameObject("RebindScreenEvents", typeof(EventSystem));
            var owner = new GameObject("RebindScreenInput"); var binding = owner.AddComponent<ScreenInputBinding>(); Keyboard keyboard = null;
            using (var controls = new ControlSession()) using (var router = new ScreenRouter(controls))
            using (var oldAction = new InputAction("old back", InputActionType.Button, "<Keyboard>/escape"))
            using (var nextAction = new InputAction("new back", InputActionType.Button, "<Keyboard>/enter"))
            {
                var settings = InputSystem.settings; var background = settings.backgroundBehavior; var editorInput = settings.editorInputBehaviorInPlayMode;
                try
                {
                    settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    keyboard = InputSystem.AddDevice<Keyboard>();
                    var handle = Open(router, View(canvas.transform, events.GetComponent<EventSystem>(), "Rebound"));
                    oldAction.performed += context => binding.Configure(router, nextAction); // Delivers before the captured old adapter callback.
                    binding.Configure(router, oldAction); oldAction.Enable(); nextAction.Enable();
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update(); keyboard.MakeCurrent();
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); InputSystem.Update(); Assert.That(handle.Status, Is.EqualTo(ScreenStatus.Open));
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter)); InputSystem.Update(); Assert.That(handle.Status, Is.EqualTo(ScreenStatus.Closed), $"key={keyboard.enterKey.isPressed}, action={nextAction.IsPressed()}, controls={nextAction.controls.Count}, token={router.FocusedBindingId}, consumed={binding.LastResult.Consumed}, error={binding.LastResult.Error}");
                    Assert.That(oldAction.enabled && nextAction.enabled, Is.True); binding.Unbind();
                }
                finally
                {
                    binding.Unbind(); if (keyboard != null) InputSystem.RemoveDevice(keyboard); settings.backgroundBehavior = background; settings.editorInputBehaviorInPlayMode = editorInput;
                    Object.Destroy(owner); Object.Destroy(canvas); Object.Destroy(events);
                }
            }
        }
        [UnityTest] public IEnumerator FocusReturnRebindDuringBackNeverDeliversOldResultToTheNewBinding()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode(); yield return null;
            var canvas = new GameObject("FocusRebindCanvas", typeof(RectTransform), typeof(Canvas));
            var eventOwner = new GameObject("FocusRebindEvents", typeof(EventSystem)); var events = eventOwner.GetComponent<EventSystem>();
            var owner = new GameObject("FocusRebindInput"); var binding = owner.AddComponent<ScreenInputBinding>();
            var baseline = new GameObject("FocusReturnTarget", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            baseline.transform.SetParent(canvas.transform, false); events.SetSelectedGameObject(baseline);
            var selection = baseline.AddComponent<ScreenSelectionCallback>();
            Keyboard keyboard = null;
            var settings = InputSystem.settings; var background = settings.backgroundBehavior; var editorInput = settings.editorInputBehaviorInPlayMode;
            using (var controls = new ControlSession()) using (var oldRouter = new ScreenRouter(controls)) using (var newRouter = new ScreenRouter(controls))
            using (var oldAction = new InputAction("focus old Back", InputActionType.Button, "<Keyboard>/escape"))
            using (var newAction = new InputAction("focus new Back", InputActionType.Button, "<Keyboard>/enter"))
            {
                try
                {
                    settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    keyboard = InputSystem.AddDevice<Keyboard>();
                    var oldHandle = Open(oldRouter, View(canvas.transform, events, "ClosingOldScreen"));
                    var nextView = View(canvas.transform, events, "NewBindingScreen");
                    IScreenHandle next = null; int newDeliveries = 0; bool rebound = false;
                    selection.Selected = () => {
                        if (rebound) return; rebound = true;
                        binding.Configure(newRouter, newAction);
                        binding.BackProcessed += result => newDeliveries++;
                        next = Open(newRouter, nextView);
                    };
                    binding.Configure(oldRouter, oldAction); oldAction.Enable(); newAction.Enable();
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update(); keyboard.MakeCurrent();
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); InputSystem.Update();
                    Assert.That(rebound, Is.True, "Actual EventSystem baseline focus callback must rebind during router.Back.");
                    Assert.That(oldHandle.Status, Is.EqualTo(ScreenStatus.Closed));
                    Assert.That(next.Status, Is.EqualTo(ScreenStatus.Open)); Assert.That(newDeliveries, Is.Zero);
                    Assert.That(binding.LastResult.Consumed, Is.False, "An old operation cannot mutate the newly configured adapter result.");
                    newRouter.Poll();
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter)); InputSystem.Update();
                    Assert.That(next.Status, Is.EqualTo(ScreenStatus.Closed)); Assert.That(newDeliveries, Is.EqualTo(1));
                    Assert.That(binding.LastResult.Consumed && binding.LastResult.Closed, Is.True);
                }
                finally
                {
                    selection.Selected = null; binding.Unbind();
                    if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                    settings.backgroundBehavior = background; settings.editorInputBehaviorInPlayMode = editorInput;
                    Object.Destroy(owner); Object.Destroy(canvas); Object.Destroy(eventOwner);
                }
            }
        }
    }
}
