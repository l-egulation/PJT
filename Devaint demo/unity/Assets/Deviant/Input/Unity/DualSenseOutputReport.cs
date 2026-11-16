using System;
using Deviant.Input.Contracts;

namespace Deviant.Input.Unity
{
    // DualSense output protocol. One packet updates both motors and both triggers;
    // valid flags deliberately leave LEDs, microphone and audio routing untouched.
    public static class DualSenseOutputReport
    {
        public const int MaximumSize = 1024;
        public static bool SupportsSize(int size) => size == 48 || size >= 78 && size <= MaximumSize;
        public static int Write(byte[] destination, int reportSize, byte sequence, GamepadFeedbackState state)
        {
            if (!SupportsSize(reportSize) || destination == null || destination.Length < reportSize)
                throw new ArgumentException("DualSense requires a 48-byte USB or 78..1024-byte Bluetooth output buffer.");
            Array.Clear(destination, 0, reportSize);
            bool bluetooth = reportSize != 48;
            int payload = bluetooth ? 3 : 1;
            destination[0] = bluetooth ? (byte)0x31 : (byte)0x02;
            if (bluetooth) { destination[1] = (byte)((sequence & 15) << 4); destination[2] = 0x10; }
            destination[payload] = 0x0f; // compatible rumble + haptics select + R2 + L2
            destination[payload + 2] = (byte)(state.High * 255 + .5f);
            destination[payload + 3] = (byte)(state.Low * 255 + .5f);
            WriteTrigger(destination, payload + 10, state.Right);
            WriteTrigger(destination, payload + 21, state.Left);
            if (bluetooth)
            {
                uint crc = UpdateCrc(0xffffffff, 0xa2);
                for (int i = 0; i < 74; i++) crc = UpdateCrc(crc, destination[i]);
                crc ^= 0xffffffff;
                for (int i = 0; i < 4; i++) destination[74 + i] = (byte)(crc >> (8 * i));
            }
            return reportSize;
        }
        static void WriteTrigger(byte[] target, int offset, TriggerFeedback effect)
        {
            if (effect.Mode == TriggerFeedbackMode.Off) { target[offset] = 0x05; return; }
            target[offset] = effect.Mode == TriggerFeedbackMode.Resistance ? (byte)0x21 : (byte)0x26;
            int mask = 0; uint strengths = 0;
            for (int zone = effect.Start; zone < 10; zone++)
            { mask |= 1 << zone; strengths |= (uint)(effect.Strength - 1) << (3 * zone); }
            target[offset + 1] = (byte)mask; target[offset + 2] = (byte)(mask >> 8);
            for (int i = 0; i < 4; i++) target[offset + 3 + i] = (byte)(strengths >> (8 * i));
            if (effect.Mode == TriggerFeedbackMode.Vibration) target[offset + 9] = (byte)effect.Frequency;
        }
        static uint UpdateCrc(uint crc, byte value)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xedb88320u : crc >> 1;
            return crc;
        }
    }
}
