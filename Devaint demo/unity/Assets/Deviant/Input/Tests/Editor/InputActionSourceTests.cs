using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Input.Contracts;
using Deviant.Input.Unity;
using Deviant.Integration.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock.LowLevel;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XInput;
using Object = UnityEngine.Object;

namespace Deviant.Input.Tests
{
    public class InputActionSourceTests : InputTestFixture
    {
        readonly List<InputActionSource> sources = new List<InputActionSource>();
        InputActionAsset asset;
        public override void Setup()
        {
            base.Setup();
            asset = ScriptableObject.CreateInstance<InputActionAsset>();
            foreach (GameAction id in Enum.GetValues(typeof(GameAction)))
            {
                string mapName = GameInputActions.Context(id).ToString();
                var map = asset.FindActionMap(mapName) ?? asset.AddActionMap(mapName);
                map.AddAction(id.ToString(), GameInputActions.IsVector(id) ? InputActionType.Value : InputActionType.Button,
                    expectedControlLayout: GameInputActions.IsVector(id) ? "Vector2" : "Button");
            }
            Action(GameAction.Move).AddCompositeBinding("2DVector(mode=1)")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow");
            Action(GameAction.Move).AddBinding("<Gamepad>/leftStick");
            // Test bindings demonstrate interchangeability, not product defaults.
            Action(GameAction.Jump).AddBinding("<Keyboard>/space");
            Action(GameAction.Jump).AddBinding("<Gamepad>/buttonSouth");
            Action(GameAction.Weapon1Basic).AddBinding("<Keyboard>/q");
            Action(GameAction.Weapon2Basic).AddBinding("<Keyboard>/w");
            Action(GameAction.Weapon1Special).AddBinding("<Keyboard>/e");
            Action(GameAction.Weapon1Special).AddBinding("<Gamepad>/leftShoulder");
            Action(GameAction.Cancel).AddBinding("<Keyboard>/escape");
            Action(GameAction.Cancel).AddBinding("<Gamepad>/buttonEast");
        }
        public override void TearDown()
        {
            foreach (var source in sources) source.Dispose();
            sources.Clear(); Object.DestroyImmediate(asset);
            base.TearDown();
        }
        InputAction Action(GameAction id) => asset.FindAction(GameInputActions.Path(id), true);
        InputActionSource Source(InputContexts contexts = InputContexts.Gameplay, int capacity = 1024)
        {
            var result = new InputActionSource(asset, capacity); sources.Add(result); result.SetContexts(contexts); return result;
        }
        static GameInputEvent[] Drain(InputActionSource source)
        {
            var events = new List<GameInputEvent>();
            while (source.TryDequeue(out var input)) events.Add(input);
            return events.ToArray();
        }

        [Test] public void RepeatedNativeEventsKeepArrivalOrderAndNativeTimes()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>(); var source = Source();
            var observed = new List<double>();
            Action<InputEventPtr, InputDevice> observe = (input, device) => { if (device == keyboard) observed.Add(input.time); };
            InputSystem.onEvent += observe;
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q), 10);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(), 10.01);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q), 10.02);
                InputSystem.Update();
                var events = Drain(source);
                Assert.That(events.Select(e => e.Phase), Is.EqualTo(new[] { InputPhase.Pressed, InputPhase.Released, InputPhase.Pressed }));
                Assert.That(events.Select(e => e.Timestamp), Is.EqualTo(observed));
                Assert.That(events.Select(e => e.Sequence), Is.Ordered);
                Assert.That(events.Select(e => e.Sequence), Is.Unique);
                Assert.That(events.All(e => e.Device.Id == keyboard.deviceId), Is.True);
            }
            finally { InputSystem.onEvent -= observe; }
        }
        [Test] public void SimultaneousNativeControlsPutMovementBeforeJumpWithoutReorderingSeparateEvents()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>(); var source = Source();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space, Key.DownArrow, Key.W, Key.Q));
            InputSystem.Update();
            var events = Drain(source);
            Assert.That(events.Select(e => e.Action), Is.EqualTo(new[] { GameAction.Move, GameAction.Weapon1Basic, GameAction.Weapon2Basic, GameAction.Jump }));
            var mapper = new CombatInputMapper(); CombatInput last = default;
            foreach (var input in events) mapper.TryMap(input, out last);
            Assert.That(last.Pressed, Is.EqualTo(CombatButtons.Drop));
        }
        [TestCase("Gamepad", InputDeviceFamily.Gamepad)]
        [TestCase("XInputController", InputDeviceFamily.Xbox)]
        [TestCase("DualSenseGamepadHID", InputDeviceFamily.PlayStation)]
        public void OneGenericBindingAcceptsEveryGamepadFamily(string layout, InputDeviceFamily family)
        {
            var pad = (Gamepad)InputSystem.AddDevice(layout); var source = Source();
            if (layout == "DualSenseGamepadHID")
            {
                // DualSense preprocessors intentionally reject DeltaStateEvent;
                // send its public virtual full-state format, as the HID backend does.
                InputSystem.QueueStateEvent(pad, new DualSenseHIDInputReport { leftStickX = 128, leftStickY = 128,
                    rightStickX = 128, rightStickY = 128, buttons0 = 8 | (1 << 5) });
                InputSystem.QueueStateEvent(pad, new DualSenseHIDInputReport { leftStickX = 128, leftStickY = 128,
                    rightStickX = 128, rightStickY = 128, buttons0 = 8 });
                InputSystem.Update();
            }
            else { Press(pad.buttonSouth); Release(pad.buttonSouth); }
            var events = Drain(source).Where(e => e.Action == GameAction.Jump).ToArray();
            Assert.That(events.Select(e => e.Phase), Is.EqualTo(new[] { InputPhase.Pressed, InputPhase.Released }));
            Assert.That(events.All(e => e.Device.Family == family && e.Device.Id == pad.deviceId), Is.True);
            Assert.That(source.LastUsedDevice.Family, Is.EqualTo(family));
        }
        [Test] public void SharedChargeStaysHeldUntilBothDevicesRelease()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>(); var pad = InputSystem.AddDevice<Gamepad>(); var source = Source();
            Press(keyboard.eKey); Press(pad.leftShoulder); Release(keyboard.eKey);
            Assert.That(source.Read(GameAction.Weapon1Special).IsPressed, Is.True);
            Assert.That(Drain(source).Count(e => e.Phase == InputPhase.Released), Is.Zero);
            Release(pad.leftShoulder);
            Assert.That(Drain(source).Single().Phase, Is.EqualTo(InputPhase.Released));
            Assert.That(source.Read(GameAction.Weapon1Special).IsPressed, Is.False);
        }
        [Test] public void PairingFiltersOtherPlayersAndStickValuesRetainAnalogMagnitude()
        {
            var a = InputSystem.AddDevice<Gamepad>(); var b = InputSystem.AddDevice<Gamepad>(); var source = Source();
            source.SetDevices(a);
            Press(b.buttonSouth); Assert.That(Drain(source), Is.Empty);
            Set(a.leftStick, new Vector2(.65f, -.7f));
            var value = source.Read(GameAction.Move);
            Assert.That(value.X, Is.GreaterThan(0).And.LessThan(1)); Assert.That(value.Y, Is.LessThan(0).And.GreaterThan(-1));
            Set(a.leftStick, new Vector2(.01f, 0)); Assert.That(source.Read(GameAction.Move).X, Is.Zero);
            Press(a.buttonSouth); Assert.That(Drain(source).Any(e => e.Action == GameAction.Jump), Is.True);
        }
        [Test] public void ContextAndFocusBoundariesDiscardQueuedEdgesAndDoNotReplayHeldButtons()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>(); var source = Source();
            Press(keyboard.qKey);
            long before = source.Revision;
            source.SetContexts(InputContexts.UI);
            source.SetContexts(InputContexts.Gameplay);
            InputSystem.Update();
            Assert.That(source.Revision, Is.GreaterThan(before)); Assert.That(Drain(source), Is.Empty);
            Release(keyboard.qKey); Press(keyboard.qKey);
            source.SetFocused(false); source.SetFocused(true); InputSystem.Update();
            Assert.That(Drain(source), Is.Empty);
            Release(keyboard.qKey); Press(keyboard.qKey);
            Assert.That(Drain(source).Single().Phase, Is.EqualTo(InputPhase.Pressed));
        }
        [Test] public void CombatInvalidationKeepsUiHoldsAndUnreadUiEdges()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var source = Source(InputContexts.Gameplay | InputContexts.UI);
            Press(keyboard.qKey); Press(keyboard.escapeKey);
            source.Reset(InputContexts.Gameplay);
            Assert.That(source.Read(GameAction.Cancel).IsPressed, Is.True);
            var kept = Drain(source);
            Assert.That(kept.Select(e => e.Action), Is.EqualTo(new[] { GameAction.Cancel }));
            Assert.That(kept[0].Revision, Is.EqualTo(source.Revision));
            source.Reset(InputContexts.Gameplay); InputSystem.Update();
            Assert.That(source.Read(GameAction.Cancel).IsPressed, Is.True, "A repeated combat clear cannot release a held menu/skip button.");
            Assert.That(Drain(source), Is.Empty);
            Release(keyboard.escapeKey);
            Assert.That(Drain(source).Single().Phase, Is.EqualTo(InputPhase.Released));
        }

        [Test] public void DeviceLossInvalidatesQueuedAndHeldInput()
        {
            var pad = InputSystem.AddDevice<Gamepad>(); var source = Source();
            Press(pad.leftShoulder); long before = source.Revision;
            InputSystem.RemoveDevice(pad);
            Assert.That(source.Revision, Is.GreaterThan(before)); Assert.That(Drain(source), Is.Empty);
            Assert.That(source.Read(GameAction.Weapon1Special).IsPressed, Is.False);
            Assert.That(source.LastUsedDevice.Family, Is.EqualTo(InputDeviceFamily.None));
        }
        [Test] public void BindingOverridesRoundTripWithoutMutatingSharedAssetOrAnotherSession()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>(); var a = Source(); var b = Source();
            Action(GameAction.Weapon1Basic).ApplyBindingOverride(0, "<Keyboard>/t");
            a.LoadBindingOverrides(asset.SaveBindingOverridesAsJson());
            string saved = a.SaveBindingOverrides();
            Action(GameAction.Weapon1Basic).RemoveAllBindingOverrides();
            Assert.That(a.GetAction(GameAction.Weapon1Basic).bindings[0].effectivePath, Is.EqualTo("<Keyboard>/t"));
            Assert.That(b.GetAction(GameAction.Weapon1Basic).bindings[0].effectivePath, Is.EqualTo("<Keyboard>/q"));
            Assert.That(Action(GameAction.Weapon1Basic).bindings[0].effectivePath, Is.EqualTo("<Keyboard>/q"));
            Press(keyboard.tKey); Assert.That(Drain(a).Single().Action, Is.EqualTo(GameAction.Weapon1Basic)); Assert.That(Drain(b), Is.Empty);
            var c = Source(); c.LoadBindingOverrides(saved);
            Assert.That(c.SaveBindingOverrides(), Is.EqualTo(saved));
        }
        [Test] public void InvalidOverridesAndInteractionsCannotChangeEdgeMeaning()
        {
            var source = Source();
            Assert.Throws<ArgumentException>(() => source.LoadBindingOverrides("this is not json"));
            Assert.That(source.GetAction(GameAction.Weapon1Basic).bindings[0].effectivePath, Is.EqualTo("<Keyboard>/q"));
            Action(GameAction.Weapon1Basic).ApplyBindingOverride(0, new InputBinding { overrideInteractions = "hold" });
            Assert.Throws<ArgumentException>(() => source.LoadBindingOverrides(asset.SaveBindingOverridesAsJson()));
            Assert.That(source.GetAction(GameAction.Weapon1Basic).enabled, Is.True);
            Assert.Throws<ArgumentException>(() => new InputActionSource(asset));
        }
        [Test] public void OverflowRejectsTheWholeUnreadBatchAndResetsState()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>(); var source = Source(capacity: 2);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q)); InputSystem.Update();
            Assert.Throws<InvalidOperationException>(() => source.TryDequeue(out _));
            Assert.That(Drain(source), Is.Empty);
        }
        [Test] public void DisposeReturnsOnlyItsOwnActionsAndNeverDisablesTheOwnersAsset()
        {
            asset.Enable(); var source = Source();
            source.Dispose(); source.Dispose();
            Assert.That(Action(GameAction.Jump).enabled, Is.True);
            Assert.Throws<ObjectDisposedException>(() => source.TryDequeue(out _));
        }
    }
}
