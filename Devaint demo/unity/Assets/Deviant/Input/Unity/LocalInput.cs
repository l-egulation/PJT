using System;
using System.Collections.Generic;
using Deviant.Input.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Deviant.Input.Unity
{
    // One owner drains the native queue; world/UI consumers share a frame view.
    // No scene lookup or static current-player state is needed.
    [DefaultExecutionOrder(-1000)]
    public sealed class LocalInput : MonoBehaviour
    {
        public const string DefaultProfile = "Input/PlayerControls";
        [SerializeField] InputActionAsset profile;
        readonly GameInputEvent[] events = new GameInputEvent[1024];
        readonly bool[] consumed = new bool[1024];
        InputActionSource source;
        int frame = -1, count, suppressedFrame = -1;
        long revision;
        InputContexts contexts = InputContexts.Gameplay | InputContexts.World | InputContexts.UI | InputContexts.Shortcuts;
        public InputActionSource Source { get { Ensure(); return source; } }
        bool captured; int captureReleaseFrame = -1;
        bool rebinding;
        public bool Rebinding
        {
            get => rebinding;
            set
            {
                if (rebinding && !value) captureReleaseFrame = Time.frameCount;
                rebinding = value;
            }
        }
        public bool MenuCaptured { get => captured || rebinding || captureReleaseFrame == Time.frameCount; set { if (captured && !value) captureReleaseFrame = Time.frameCount; captured = value; } }
        public bool GameplaySuppressed => MenuCaptured || suppressedFrame == Time.frameCount;
        public bool UsingGamepad => Source.LastUsedDevice.Family != InputDeviceFamily.None && Source.LastUsedDevice.Family != InputDeviceFamily.KeyboardMouse;
        public int EventCount { get { Poll(); return MenuCaptured ? 0 : count; } }
        public GameInputEvent EventAt(int index) => events[index];
        public bool IsConsumed(int index) => consumed[index];
        public void SuppressGameplay() => suppressedFrame = Time.frameCount;
        void OnEnable() { Ensure(); source.SetContexts(contexts); }
        void Update() => Poll();
        void Ensure()
        {
            if (source != null) return;
            var bindings = profile ? profile : Resources.Load<InputActionAsset>(DefaultProfile);
            if (!bindings) throw new InvalidOperationException("The shared PlayerControls.inputactions profile is missing.");
            source = new InputActionSource(bindings);
            if (!profile) KeyboardShortcutSettings.Load(source);
            source.SetContexts(isActiveAndEnabled ? contexts : InputContexts.None);
        }
        public void SetDebugEnabled(bool enabled)
        {
            var next = enabled ? contexts | InputContexts.Debug : contexts & ~InputContexts.Debug;
            if (contexts == next) return;
            contexts = next;
            Source.SetContexts(isActiveAndEnabled ? contexts : InputContexts.None);
        }
        public void Poll()
        {
            Ensure();
            if (frame != Time.frameCount || revision != source.Revision)
            { frame = Time.frameCount; revision = source.Revision; count = 0; }
            while (source.TryDequeue(out var input))
            {
                if (count == events.Length)
                { source.Reset(); count = 0; throw new InvalidOperationException("Local input frame capacity exceeded; no partial frame can be delivered."); }
                events[count] = input; consumed[count++] = false;
            }
        }
        public bool Pressed(GameAction action)
        {
            if (MenuCaptured) return false;
            Poll();
            for (int i = 0; i < count; i++)
                if (!consumed[i] && events[i].Action == action && events[i].Phase == InputPhase.Pressed) return true;
            return false;
        }
        public bool Take(GameAction action)
        {
            if (MenuCaptured) return false;
            Poll(); bool found = false;
            for (int i = 0; i < count; i++)
                if (!consumed[i] && events[i].Action == action && events[i].Phase == InputPhase.Pressed)
                { consumed[i] = true; found = true; }
            return found;
        }
        public bool TakeBack()
        {
            // Both actions can share Escape: always consume both, once.
            bool cancel = Take(GameAction.Cancel), pause = Take(GameAction.Pause);
            return cancel || pause;
        }
        public bool BackHeld => Read(GameAction.Cancel).IsPressed || Read(GameAction.Pause).IsPressed;
        public GameInputValue Read(GameAction action) => MenuCaptured ? default : Source.Read(action);
        // HUD는 매 프레임 키 이름을 여러 번 묻는다. 이름을 만드는 InputControlPath.ToHumanReadableString은 호출당
        // 0.5ms가량 들고 문자열을 새로 할당하므로(2026-11-06 측정), 결과는 동작별로 저장해 두고
        // 마지막 입력 장치 종류·레이아웃이나 그 동작의 바인딩 경로(키 재설정)가 바뀔 때만 다시 만든다.
        sealed class LabelEntry { public InputDeviceFamily family; public string layout; public string[] paths; public string label; }
        readonly Dictionary<(GameAction action, string direction), LabelEntry> labels = new Dictionary<(GameAction, string), LabelEntry>();
        public string Label(GameAction action) => Label(action, null);
        public string DirectionLabel(string direction) => Label(GameAction.Move, direction.ToLowerInvariant());
        string Label(GameAction action, string direction)
        {
            var input = Source.GetAction(action);
            if (input == null) return action.ToString();
            var device = Source.LastUsedDevice;
            var bindings = input.bindings;
            var key = (action, direction);
            if (labels.TryGetValue(key, out var entry) && entry.family == device.Family && entry.layout == device.Layout && entry.paths.Length == bindings.Count)
            {
                bool same = true;
                for (int i = 0; i < bindings.Count && same; i++) same = bindings[i].effectivePath == entry.paths[i];
                if (same) return entry.label;
            }
            var paths = new string[bindings.Count];
            for (int i = 0; i < paths.Length; i++) paths[i] = bindings[i].effectivePath;
            string label = BuildLabel(action, input, direction);
            labels[key] = new LabelEntry { family = device.Family, layout = device.Layout, paths = paths, label = label };
            return label;
        }
        static bool PadPath(string path) => path.StartsWith("<Gamepad>") || path.StartsWith("<Dual") || path.StartsWith("<XInput") || path.StartsWith("<Joystick>");
        string BuildLabel(GameAction action, InputAction input, string direction)
        {
            bool pad = UsingGamepad;
            if (direction != null)
            {
                if (pad) return Label(GameAction.Move) + " " + (direction == "up" ? "위" : direction == "down" ? "아래" : direction == "left" ? "왼쪽" : "오른쪽");
                foreach (var binding in input.bindings)
                    if (binding.isPartOfComposite && string.Equals(binding.name, direction, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(binding.effectivePath) && !PadPath(binding.effectivePath))
                        return ControlLabel(binding.effectivePath, false);
                return direction;
            }
            if(action==GameAction.MapPan&&!pad)
            {
                var keys=new List<string>();bool arrows=true;
                foreach(var binding in input.bindings)
                    if(binding.isPartOfComposite&&!string.IsNullOrEmpty(binding.effectivePath)&&!PadPath(binding.effectivePath))
                    {keys.Add(ControlLabel(binding.effectivePath,false));arrows&=binding.effectivePath.EndsWith("Arrow",StringComparison.OrdinalIgnoreCase);}
                if(keys.Count>0)return arrows?"방향키":string.Join("/",keys);
            }
            if (action == GameAction.Move || action == GameAction.Navigate)
            {
                if (!pad) return action == GameAction.Move ? DirectionLabel("left") + "/" + DirectionLabel("right") : "방향키";
                var controls = new List<string>();
                foreach (var binding in input.bindings)
                    if (!binding.isComposite && !binding.isPartOfComposite && !string.IsNullOrEmpty(binding.effectivePath) && PadPath(binding.effectivePath))
                    { string label = ControlLabel(binding.effectivePath, true); if (!controls.Contains(label)) controls.Add(label); }
                return string.Join("/", controls);
            }
            foreach (var binding in input.bindings)
            {
                if (binding.isComposite || binding.isPartOfComposite || string.IsNullOrEmpty(binding.effectivePath)) continue;
                if (pad != PadPath(binding.effectivePath)) continue;
                return ControlLabel(binding.effectivePath, pad);
            }
            // Debug/help actions without a controller binding must identify their keyboard shortcut honestly.
            return (pad ? "키보드 " : "") + input.GetBindingDisplayString();
        }
        string ControlLabel(string path, bool pad)
        {
            string control = path.Substring(path.LastIndexOf('/') + 1);
            if (!pad) return KeyboardKeyName(path);
            bool sony = Source.LastUsedDevice.Family == InputDeviceFamily.PlayStation;
            switch (control)
            {
                case "buttonSouth": return sony ? "Cross" : "A";
                case "buttonEast": return sony ? "Circle" : "B";
                case "buttonWest": return sony ? "Square" : "X";
                case "buttonNorth": return sony ? "Triangle" : "Y";
                case "leftShoulder": return sony ? "L1" : "LB";
                case "rightShoulder": return sony ? "R1" : "RB";
                case "leftTrigger": return sony ? "L2" : "LT";
                case "rightTrigger": return sony ? "R2" : "RT";
                case "start": return sony ? "Options" : "Menu";
                case "select": return sony ? (Source.LastUsedDevice.Layout.IndexOf("DualSense", StringComparison.OrdinalIgnoreCase) >= 0 ? "Create" : "Share") : "View";
                case "leftStickPress": return "L3";
                case "rightStickPress": return "R3";
                case "leftStick": return "LS";
                case "rightStick": return "RS";
                case "up": if(path.Contains("/rightStick/")) return "RS ↑"; break;
                case "left": if(path.Contains("/rightStick/")) return "RS ←"; break;
                case "dpad": return "D-Pad";
                default: break;
            }
            return InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
        }
        // The short keycap name every screen shows for a keyboard or mouse binding (user 2026-11-13): arrows as glyphs and
        // the usual short names (Shift, Ctrl, Alt, Esc, Enter, Space, PgUp...), never the long engine names such as
        // 「Left Shift」. Left and right modifiers share one name; prompts put it in brackets before the action, 「[F] 닫기」.
        public static string KeyboardKeyName(string path)
        {
            string control = path.Substring(path.LastIndexOf('/') + 1);
            switch (control)
            {
                case "upArrow": return "↑";
                case "downArrow": return "↓";
                case "leftArrow": return "←";
                case "rightArrow": return "→";
                case "escape": return "Esc";
                case "shift": case "leftShift": case "rightShift": return "Shift";
                case "ctrl": case "leftCtrl": case "rightCtrl": return "Ctrl";
                case "alt": case "leftAlt": case "rightAlt": return "Alt";
                case "leftMeta": case "rightMeta": case "leftWindows": case "rightWindows": return "Win";
                case "enter": case "numpadEnter": return "Enter";
                case "space": return "Space";
                case "tab": return "Tab";
                case "backspace": return "Backspace";
                case "delete": return "Del";
                case "insert": return "Ins";
                case "home": return "Home";
                case "end": return "End";
                case "pageUp": return "PgUp";
                case "pageDown": return "PgDn";
                case "capsLock": return "Caps";
                case "minus": case "numpadMinus": return "-";
                case "equals": return "=";
                case "numpadPlus": return "+";
                case "numpadMultiply": return "*";
                case "numpadDivide": return "/";
                case "leftButton": return "좌클릭";
                case "rightButton": return "우클릭";
                case "middleButton": return "휠 클릭";
            }
            string name = InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
            if (name.StartsWith("Numpad ", StringComparison.Ordinal)) name = "Num " + name.Substring(7);
            return name.Length == 1 ? name.ToUpperInvariant() : name;
        }
        static readonly System.Text.RegularExpressions.Regex promptToken = new System.Text.RegularExpressions.Regex(@"\{([A-Za-z][A-Za-z0-9]*)(?:\.(Up|Down|Left|Right))?\}");
        // Templates name actions, never physical keys. Resolve at display time so open screens follow device/rebinding changes.
        public string FormatPrompt(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) return text;
            return promptToken.Replace(text, match =>
            {
                if (!Enum.TryParse<GameAction>(match.Groups[1].Value, out var action)) return match.Value;
                return action == GameAction.Move && match.Groups[2].Success ? DirectionLabel(match.Groups[2].Value) : Label(action);
            });
        }
        void OnApplicationFocus(bool focused) { if (source != null) source.SetFocused(focused); }
        void OnApplicationPause(bool paused) { if (source != null) source.SetFocused(!paused); }
        void OnDisable() { if (source != null) source.SetContexts(InputContexts.None); count = 0; }
        void OnDestroy() { source?.Dispose(); source = null; }
    }
}
