using System;
using System.Runtime.InteropServices;
using Deviant.Input.Contracts;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
#if UNITY_EDITOR || UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.HID;
#endif

namespace Deviant.Input.Unity
{
    // Explicit local-player device, never Gamepad.current. This owner combines all
    // its effects before writing and only caches writes acknowledged by the backend.
    public sealed class GamepadFeedbackOutput : IDisposable
    {
        readonly byte[] report = new byte[DualSenseOutputReport.MaximumSize];
        Gamepad device;
        GamepadFeedbackState sent;
        int reportSize;
        byte sequence;
        bool hasSent, ownsOutput;
        public Gamepad Device => device;
        public GamepadFeedbackCapabilities Capabilities { get; private set; }
        public bool LastWriteSucceeded { get; private set; }

        public bool Bind(Gamepad next)
        {
            if (device == next) return true;
            if (!Release()) return false;
            device = next; reportSize = 0; sequence = 0; hasSent = ownsOutput = false;
            Capabilities = next != null ? GamepadFeedbackCapabilities.Rumble : GamepadFeedbackCapabilities.None;
#if UNITY_EDITOR || UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
            if (next is DualSenseGamepadHID)
            {
                Capabilities = GamepadFeedbackCapabilities.None;
                if (next.description.interfaceName != "HID" || string.IsNullOrWhiteSpace(next.description.capabilities)) return true;
                try
                {
                    var descriptor = HID.HIDDeviceDescriptor.FromJson(next.description.capabilities);
                    if (next.description.interfaceName == "HID" && descriptor.vendorId == 0x054c && (descriptor.productId == 0x0ce6 || descriptor.productId == 0x0df2) &&
                        DualSenseOutputReport.SupportsSize(descriptor.outputReportSize))
                    { reportSize = descriptor.outputReportSize; Capabilities = GamepadFeedbackCapabilities.Rumble | GamepadFeedbackCapabilities.AdaptiveTriggers; }
                }
                catch (ArgumentException) { /* Unidentified DualSense transport has no safe output report. */ }
            }
#endif
            return true;
        }
        public bool Apply(GamepadFeedbackState state)
        {
            if (device == null || !device.added || !device.enabled || Capabilities == GamepadFeedbackCapabilities.None) return LastWriteSucceeded = false;
            if (reportSize == 0) state = new GamepadFeedbackState(state.Low, state.High);
            if (hasSent && state.Equals(sent)) return LastWriteSucceeded = true;
            // Mark before sending: even a failed/busy write must be followed by an
            // explicit stop at a restriction boundary. Retry dirty packets next poll.
            ownsOutput = true;
            LastWriteSucceeded = Send(state);
            if (LastWriteSucceeded) { sent = state; hasSent = true; }
            else hasSent = false;
            return LastWriteSucceeded;
        }
        public bool Release()
        {
            hasSent = false;
            if (!ownsOutput) return true;
            if (device == null || !device.added) { ownsOutput = false; return true; }
            LastWriteSucceeded = Send(default);
            if (LastWriteSucceeded) ownsOutput = false;
            return LastWriteSucceeded;
        }
        unsafe bool Send(GamepadFeedbackState state)
        {
            if (reportSize > 0)
            {
                DualSenseOutputReport.Write(report, reportSize, sequence++, state);
                var command = new HidOutputCommand { Header = new InputDeviceCommand(HidOutputCommand.Type, InputDeviceCommand.BaseCommandSize + reportSize) };
                for (int i = 0; i < reportSize; i++) command.Data[i] = report[i];
                return device.ExecuteCommand(ref command) >= 0;
            }
            // Preserve layout-specific rumble (including DualShock HID overrides).
            // The public standard API has no acknowledgement result.
            device.SetMotorSpeeds(state.Low, state.High);
            return true;
        }
        public void Dispose() { Release(); device = null; Capabilities = GamepadFeedbackCapabilities.None; }

        [StructLayout(LayoutKind.Explicit, Size = InputDeviceCommand.BaseCommandSize + DualSenseOutputReport.MaximumSize)]
        unsafe struct HidOutputCommand : IInputDeviceCommandInfo
        {
            public static FourCC Type => new FourCC('H', 'I', 'D', 'O');
            public FourCC typeStatic => Type;
            [FieldOffset(0)] public InputDeviceCommand Header;
            [FieldOffset(InputDeviceCommand.BaseCommandSize)] public fixed byte Data[DualSenseOutputReport.MaximumSize];
        }
    }
}
