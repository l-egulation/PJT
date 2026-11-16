using System;
using Deviant.Input.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XInput;
using Object = UnityEngine.Object;

namespace Deviant.Input.Unity
{
    // Owns an isolated clone. No singleton, scene discovery or gameplay callback.
    // The host polls after Input System updates and forwards control restrictions.
    public sealed class InputActionSource : IGameInput, IDisposable
    {
        readonly InputActionAsset asset;
        readonly InputAction[] actions;
        readonly GameInputEvent[] queue, nativeEvent;
        int head, count, nativeCount;
        long sequence;
        bool overflow, suppress, focused = true, disposed;
        InputContexts contexts;
        public long Revision { get; private set; } = 1;
        public InputDeviceInfo LastUsedDevice { get; private set; }
        public InputContexts Contexts => contexts;
        public bool IsFocused => focused && !disposed;
        // The last meaningful device must still belong to this source's pairing.
        // UI display information alone never grants access to another player's pad.
        public Gamepad FeedbackGamepad
        {
            get
            {
                if (!IsFocused || !(InputSystem.GetDeviceById(LastUsedDevice.Id) is Gamepad pad) || !pad.added || !pad.enabled) return null;
                var paired = asset.devices;
                if (!paired.HasValue) return pad;
                foreach (var candidate in paired.Value) if (candidate == pad) return pad;
                return null;
            }
        }
        public Func<long> CaptureToken { get; set; }

        public InputActionSource(InputActionAsset bindings, int capacity = 1024)
        {
            if (!bindings) throw new ArgumentNullException(nameof(bindings));
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            asset = Object.Instantiate(bindings);
            actions = new InputAction[Enum.GetValues(typeof(GameAction)).Length];
            queue = new GameInputEvent[capacity]; nativeEvent = new GameInputEvent[capacity];
            try
            {
                // Binding overrides are runtime data and Unity's object clone
                // does not serialize them. Preserve the supplied effective profile.
                asset.LoadBindingOverridesFromJson(bindings.SaveBindingOverridesAsJson());
                foreach (GameAction id in Enum.GetValues(typeof(GameAction)))
                {
                    var action = asset.FindAction(GameInputActions.Path(id));
                    if (action == null) continue; // A title/lab profile can be a subset.
                    GameInputActions.Validate(id, action);
                    action.wantsInitialStateCheck = GameInputActions.IsVector(id);
                    actions[(int)id] = action;
                    action.performed += Capture;
                    action.canceled += Capture;
                }
                InputSystem.onEvent += BeforeNativeEvent;
                InputSystem.onAfterUpdate += FlushNativeEvent;
                InputSystem.onDeviceChange += DeviceChanged;
                Application.focusChanged += SetFocused;
            }
            catch { Dispose(); throw; }
        }

        public GameInputValue Read(GameAction id)
        {
            ThrowIfDisposed();
            var action = GetAction(id);
            if (action == null || !action.enabled) return default;
            if (!GameInputActions.IsVector(id)) return new GameInputValue(action.IsPressed() ? 1 : 0);
            var value = action.ReadValue<Vector2>();
            return new GameInputValue(value.x, value.y);
        }

        // Borrowed action for InputSystemUIInputModule / ScreenInputBinding only.
        // Source owns Enable/Disable; consumers must not change its lifetime.
        public InputAction GetAction(GameAction id)
        {
            ThrowIfDisposed();
            if ((int)id < 0 || (int)id >= actions.Length) throw new ArgumentOutOfRangeException(nameof(id));
            return actions[(int)id];
        }

        public void SetContexts(InputContexts enabled)
        {
            ThrowIfDisposed();
            if ((enabled & ~InputContexts.All) != 0) throw new ArgumentOutOfRangeException(nameof(enabled));
            if (contexts == enabled) return;
            contexts = enabled; Reset();
        }

        // Null means all compatible devices for this local player. Explicit device
        // pairing keeps another controller from driving the same input session.
        public void SetDevices(params InputDevice[] devices)
        {
            ThrowIfDisposed();
            if (devices != null)
                foreach (var device in devices)
                    if (device == null || !device.added) throw new ArgumentException("Pair only connected input devices.", nameof(devices));
            suppress = true;
            try { asset.Disable(); asset.devices = devices == null ? null : (InputDevice[])devices.Clone(); }
            finally { suppress = false; }
            LastUsedDevice = default;
            Reset();
        }

        public void SetFocused(bool hasFocus)
        {
            if (disposed || focused == hasFocus) return;
            focused = hasFocus; Reset();
        }

        // Call on actor reset, ownership change or a control restriction boundary,
        // including a block opened and closed between two polls.
        public void Reset(InputContexts affected = InputContexts.All)
        {
            ThrowIfDisposed();
            if ((affected & ~InputContexts.All) != 0) throw new ArgumentOutOfRangeException(nameof(affected));
            FlushNativeEvent();
            suppress = true;
            try
            {
                foreach (GameAction id in Enum.GetValues(typeof(GameAction)))
                    if ((GameInputActions.Context(id) & affected) != 0) actions[(int)id]?.Disable();
                Revision++;
                if (affected == InputContexts.All) { head = count = nativeCount = 0; overflow = false; }
                else
                {
                    int kept = 0;
                    for (int i = 0; i < count; i++)
                    {
                        var input = queue[(head + i) % queue.Length];
                        if ((GameInputActions.Context(input.Action) & affected) == 0)
                            queue[(head + kept++) % queue.Length] = AtCurrentRevision(input);
                    }
                    count = kept; kept = 0;
                    for (int i = 0; i < nativeCount; i++)
                        if ((GameInputActions.Context(nativeEvent[i].Action) & affected) == 0) nativeEvent[kept++] = AtCurrentRevision(nativeEvent[i]);
                    nativeCount = kept;
                }
                if (focused)
                    foreach (GameAction id in Enum.GetValues(typeof(GameAction)))
                        if ((GameInputActions.Context(id) & contexts & affected) != 0) actions[(int)id]?.Enable();
            }
            finally { suppress = false; }
        }
        GameInputEvent AtCurrentRevision(GameInputEvent input)
            => new GameInputEvent(Revision, input.Sequence, input.Action, input.Phase, input.Value, input.Device, input.Timestamp, input.CaptureToken);

        public string SaveBindingOverrides() { ThrowIfDisposed(); return asset.SaveBindingOverridesAsJson(); }

        public void OverrideBinding(GameAction id, int bindingIndex, string path)
        {
            ThrowIfDisposed();
            var action = GetAction(id);
            if (action == null) throw new ArgumentException("The profile does not provide " + id);
            action.ApplyBindingOverride(bindingIndex, path);
            Reset();
        }
        public void LoadBindingOverrides(string json)
        {
            ThrowIfDisposed();
            // Validate on a temporary clone so malformed profiles cannot partly
            // replace the active mapping. Storage/UI policy belongs to the host.
            var candidate = Object.Instantiate(asset);
            try
            {
                candidate.Disable();
                candidate.LoadBindingOverridesFromJson(json);
                foreach (GameAction id in Enum.GetValues(typeof(GameAction)))
                {
                    var action = candidate.FindAction(GameInputActions.Path(id));
                    if (action != null) GameInputActions.Validate(id, action);
                }
                suppress = true;
                try { asset.Disable(); asset.LoadBindingOverridesFromJson(candidate.SaveBindingOverridesAsJson()); }
                finally { suppress = false; }
                Reset();
            }
            finally { Destroy(candidate); }
        }

        public bool TryDequeue(out GameInputEvent input)
        {
            ThrowIfDisposed();
            FlushNativeEvent();
            if (overflow)
            {
                Reset();
                throw new InvalidOperationException("Input event capacity exceeded; the complete pending batch was discarded.");
            }
            if (count == 0) { input = default; return false; }
            input = queue[head]; head = (head + 1) % queue.Length; count--;
            return true;
        }

        void BeforeNativeEvent(InputEventPtr input, InputDevice device) => FlushNativeEvent();
        void Capture(InputAction.CallbackContext callback)
        {
            if (suppress || disposed || !focused || overflow) return;
            int index = Array.IndexOf(actions, callback.action);
            if (index < 0 || callback.control?.device == null) return;
            var id = (GameAction)index;
            bool vector = GameInputActions.IsVector(id);
            var value = vector ? callback.ReadValue<Vector2>() : new Vector2(callback.canceled ? 0 : 1, 0);
            var device = Describe(callback.control.device);
            var phase = vector ? InputPhase.Changed : callback.canceled ? InputPhase.Released : InputPhase.Pressed;
            if (double.IsNaN(callback.time) || double.IsInfinity(callback.time) || callback.time < 0) return;
            if (nativeCount + count == queue.Length) { overflow = true; return; }
            var input = new GameInputEvent(Revision, 0, id, phase, new GameInputValue(value.x, value.y), device, callback.time, CaptureToken?.Invoke() ?? 0);
            // Order within ONE native event only: Move first, then the documented
            // combat action order. Distinct events are never sorted by timestamp.
            int at = nativeCount++;
            while (at > 0 && (int)nativeEvent[at - 1].Action > index)
            { nativeEvent[at] = nativeEvent[at - 1]; at--; }
            nativeEvent[at] = input;
            if (phase == InputPhase.Pressed || vector && id != GameAction.Point && value.sqrMagnitude >= .04f)
                LastUsedDevice = device;
        }
        void FlushNativeEvent()
        {
            if (disposed || suppress || overflow) return;
            for (int i = 0; i < nativeCount; i++)
            {
                var input = nativeEvent[i];
                queue[(head + count++) % queue.Length] = new GameInputEvent(input.Revision, ++sequence,
                    input.Action, input.Phase, input.Value, input.Device, input.Timestamp, input.CaptureToken);
            }
            nativeCount = 0;
        }
        void DeviceChanged(InputDevice device, InputDeviceChange change)
        {
            if (disposed || change != InputDeviceChange.Removed && change != InputDeviceChange.Disconnected && change != InputDeviceChange.Disabled) return;
            bool bound = false;
            foreach (var action in actions)
                if (action != null)
                    foreach (var control in action.controls)
                        if (control.device == device) bound = true;
            // Resolved controls may already have lost the removed device. Clearing
            // also covers queued edges, and never synthesizes a fresh attack.
            for (int i = 0; i < count && !bound; i++) bound = queue[(head + i) % queue.Length].Device.Id == device.deviceId;
            for (int i = 0; i < nativeCount && !bound; i++) bound = nativeEvent[i].Device.Id == device.deviceId;
            if (!bound && LastUsedDevice.Id != device.deviceId) return;
            if (LastUsedDevice.Id == device.deviceId) LastUsedDevice = default;
            Reset();
        }
        static InputDeviceInfo Describe(InputDevice device)
        {
            var family = device is Keyboard || device is Mouse ? InputDeviceFamily.KeyboardMouse :
                device is DualShockGamepad ? InputDeviceFamily.PlayStation : device is XInputController ? InputDeviceFamily.Xbox :
                device is Gamepad ? InputDeviceFamily.Gamepad : device is Joystick ? InputDeviceFamily.Joystick : InputDeviceFamily.Other;
            return new InputDeviceInfo(device.deviceId, family, device.layout);
        }
        void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(InputActionSource)); }
        static void Destroy(InputActionAsset value)
        { if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value); }
        public void Dispose()
        {
            if (disposed) return;
            disposed = suppress = true;
            InputSystem.onEvent -= BeforeNativeEvent;
            InputSystem.onAfterUpdate -= FlushNativeEvent;
            InputSystem.onDeviceChange -= DeviceChanged;
            Application.focusChanged -= SetFocused;
            foreach (var action in actions)
                if (action != null) { action.performed -= Capture; action.canceled -= Capture; }
            asset.Disable(); head = count = nativeCount = 0;
            Destroy(asset);
        }
    }
}
