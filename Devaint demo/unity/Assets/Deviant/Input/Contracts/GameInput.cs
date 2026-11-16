using System;

namespace Deviant.Input.Contracts
{
    [Flags]
    public enum InputContexts { None = 0, Gameplay = 1, World = 2, UI = 4, Shortcuts = 8, Debug = 16, All = 31 }

    // Semantic actions survive physical rebinding and device changes.
    public enum GameAction
    {
        Move, Weapon1Basic, Weapon2Basic, Weapon1Special, Weapon2Special, Dash, Jump,
        Interact, Navigate, Submit, Cancel, Point, Click, Scroll,
        Pause, Inventory, Map, CheckpointInstall, CheckpointReturn, DevelopmentReset,
        Advance, Equip, Help, MapPan, MapZoomIn, MapZoomOut, MapFocus, MapFit, SwapWeaponOrder, OpenReward, RerollReward, InventoryRemove, InventoryRename
    }
    public enum InputPhase { Changed, Pressed, Released }
    public enum InputDeviceFamily { None, KeyboardMouse, PlayStation, Xbox, Gamepad, Joystick, Other }

    public readonly struct GameInputValue
    {
        public float X { get; }
        public float Y { get; }
        public GameInputValue(float x, float y = 0)
        {
            if (float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(y) || float.IsInfinity(y))
                throw new ArgumentOutOfRangeException(nameof(x));
            X = x; Y = y;
        }
        public bool IsPressed => X >= .5f;
    }

    public readonly struct InputDeviceInfo
    {
        public int Id { get; }
        public InputDeviceFamily Family { get; }
        public string Layout { get; }
        public InputDeviceInfo(int id, InputDeviceFamily family, string layout)
        { Id = id; Family = family; Layout = layout ?? string.Empty; }
    }

    public readonly struct GameInputEvent
    {
        public long Revision { get; }
        public long Sequence { get; }
        public GameAction Action { get; }
        public InputPhase Phase { get; }
        public GameInputValue Value { get; }
        public InputDeviceInfo Device { get; }
        // Input System realtime metadata; never the simulation clock.
        public double Timestamp { get; }
        // Opaque owner token sampled during capture, before UI/world can change authority.
        public long CaptureToken { get; }
        public GameInputEvent(long revision, long sequence, GameAction action, InputPhase phase,
            GameInputValue value, InputDeviceInfo device, double timestamp, long captureToken = 0)
        {
            if (revision < 0 || sequence < 0 || double.IsNaN(timestamp) || double.IsInfinity(timestamp) || timestamp < 0)
                throw new ArgumentOutOfRangeException(nameof(timestamp));
            Revision = revision; Sequence = sequence; Action = action; Phase = phase;
            Value = value; Device = device; Timestamp = timestamp; CaptureToken = captureToken;
        }
    }

    // One composition-owned reader drains the queue and routes each edge once.
    // Views receive routed commands or borrowed UI actions, not this destructive reader.
    public interface IGameInput
    {
        long Revision { get; }
        InputDeviceInfo LastUsedDevice { get; }
        GameInputValue Read(GameAction action);
        bool TryDequeue(out GameInputEvent input);
    }
}
