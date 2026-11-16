using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Deviant.Input.Contracts;
using Deviant.Input.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace Deviant.Input.Tests
{
    public sealed class GamepadFeedbackOutputTests : InputTestFixture
    {
        [Test] public void UsbUsesIndependentTriggerBlocksAndDoesNotChangeLightsOrAudio()
        {
            var bytes = new byte[48];
            var left = new TriggerFeedback(TriggerFeedbackMode.Resistance, 2, 3);
            var right = new TriggerFeedback(TriggerFeedbackMode.Vibration, 5, 4, 35);
            DualSenseOutputReport.Write(bytes, 48, 0, new GamepadFeedbackState(.5f, 1, left, right));
            Assert.That(bytes[0], Is.EqualTo(2)); Assert.That(bytes[1], Is.EqualTo(15));
            Assert.That(bytes[2], Is.Zero); Assert.That(bytes[3], Is.EqualTo(255)); Assert.That(bytes[4], Is.EqualTo(128));
            Assert.That(bytes[11], Is.EqualTo(0x26)); Assert.That(bytes[12], Is.EqualTo(0xe0)); Assert.That(bytes[13], Is.EqualTo(3));
            Assert.That(bytes[20], Is.EqualTo(35)); Assert.That(bytes[22], Is.EqualTo(0x21));
            Assert.That(bytes[23], Is.EqualTo(0xfc)); Assert.That(bytes[24], Is.EqualTo(3));
            uint forces = BitConverter.ToUInt32(bytes, 25);
            for (int zone = 0; zone < 10; zone++) Assert.That((forces >> (zone * 3)) & 7, Is.EqualTo(zone < 2 ? 0 : 2));
            for (int i = 33; i < 48; i++) Assert.That(bytes[i], Is.Zero);
            DualSenseOutputReport.Write(bytes, 48, 0, default);
            Assert.That(bytes[11], Is.EqualTo(5)); Assert.That(bytes[22], Is.EqualTo(5));
            Assert.That(bytes[3], Is.Zero); Assert.That(bytes[4], Is.Zero);
        }
        [TestCase(78)] [TestCase(1024)]
        public void BluetoothStopMatchesIndependentCrcVectorAndPadsDescriptorLength(int size)
        {
            var bytes = new byte[size]; Array.Fill(bytes, (byte)0xff);
            DualSenseOutputReport.Write(bytes, size, 3, default);
            Assert.That(bytes[0], Is.EqualTo(0x31)); Assert.That(bytes[1], Is.EqualTo(0x30)); Assert.That(bytes[2], Is.EqualTo(0x10));
            Assert.That(bytes[13], Is.EqualTo(5)); Assert.That(bytes[24], Is.EqualTo(5));
            // zlib.crc32(0xA2 + report[0:74]), generated independently of C#.
            Assert.That(BitConverter.ToUInt32(bytes, 74), Is.EqualTo(0x03faed64u));
            for (int i = 78; i < size; i++) Assert.That(bytes[i], Is.Zero);
        }
        [Test] public void InvalidEffectsAndUnknownTransportLengthsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TriggerFeedback(TriggerFeedbackMode.Resistance, 10, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TriggerFeedback(TriggerFeedbackMode.Vibration, 0, 2, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GamepadFeedbackState(float.NaN, 0));
            Assert.Throws<ArgumentException>(() => DualSenseOutputReport.Write(new byte[1024], 64, 0, default));
            Assert.Throws<ArgumentException>(() => DualSenseOutputReport.Write(new byte[48], 78, 0, default));
        }
        static Gamepad DualSense(int size) => (Gamepad)InputSystem.AddDevice(new InputDeviceDescription
        {
            interfaceName = "HID", product = "DualSense Wireless Controller",
            capabilities = "{\"vendorId\":1356,\"productId\":3302,\"inputReportSize\":64,\"outputReportSize\":" + size + "}"
        });
        [TestCase(48)] [TestCase(78)] [TestCase(1024)]
        public unsafe void NativeWritesCombineEffectsRetryBusyPacketsAndReleaseBeforeChangingDevices(int size)
        {
            var pad = DualSense(size); var other = InputSystem.AddDevice<Gamepad>();
            var packets = new List<byte[]>(); bool busy = false;
            InputDeviceCommandDelegate capture = (device, command) =>
            {
                if (device != pad || command->type != new FourCC('H', 'I', 'D', 'O')) return null;
                var packet = new byte[command->sizeInBytes - InputDeviceCommand.BaseCommandSize];
                Marshal.Copy((IntPtr)command + InputDeviceCommand.BaseCommandSize, packet, 0, packet.Length); packets.Add(packet);
                return busy ? -1L : 1L;
            };
            InputSystem.onDeviceCommand += capture;
            try
            {
                using (var output = new GamepadFeedbackOutput())
                {
                    output.Bind(pad);
                    Assert.That(output.Capabilities, Is.EqualTo(GamepadFeedbackCapabilities.Rumble | GamepadFeedbackCapabilities.AdaptiveTriggers));
                    var state = new GamepadFeedbackState(.6f, .4f, new TriggerFeedback(TriggerFeedbackMode.Resistance, 2, 4));
                    Assert.That(output.Apply(state), Is.True); Assert.That(output.Apply(state), Is.True); Assert.That(packets.Count, Is.EqualTo(1));
                    Assert.That(packets[0].Length, Is.EqualTo(size));
                    busy = true;
                    Assert.That(output.Apply(default), Is.False); Assert.That(output.Apply(default), Is.False);
                    Assert.That(output.Bind(other), Is.False); Assert.That(output.Device, Is.SameAs(pad));
                    busy = false;
                    Assert.That(output.Bind(other), Is.True); Assert.That(output.Device, Is.SameAs(other));
                    int payload = size == 48 ? 1 : 3;
                    var stopped = packets[packets.Count - 1];
                    Assert.That(stopped[payload + 2], Is.Zero); Assert.That(stopped[payload + 3], Is.Zero);
                    Assert.That(stopped[payload + 10], Is.EqualTo(5)); Assert.That(stopped[payload + 21], Is.EqualTo(5));
                    Assert.That(output.Capabilities, Is.EqualTo(GamepadFeedbackCapabilities.Rumble));
                }
            }
            finally { InputSystem.onDeviceCommand -= capture; }
        }
        [Test] public void UnknownDualSenseTransportDoesNotSendAnAssumedUsbPacket()
        {
            using (var output = new GamepadFeedbackOutput())
            {
                output.Bind(DualSense(64));
                Assert.That(output.Capabilities, Is.EqualTo(GamepadFeedbackCapabilities.None));
                Assert.That(output.Apply(new GamepadFeedbackState(.5f, .5f)), Is.False);
            }
        }
        [Test] public void DualSenseWithoutTransportMetadataKeepsInputAvailableAndDisablesOutput()
        {
            var pad = (Gamepad)InputSystem.AddDevice("DualSenseGamepadHID");
            using (var output = new GamepadFeedbackOutput())
            {
                Assert.That(output.Bind(pad), Is.True);
                Assert.That(output.Device, Is.SameAs(pad));
                Assert.That(output.Capabilities, Is.EqualTo(GamepadFeedbackCapabilities.None));
                Assert.That(output.Apply(new GamepadFeedbackState(.4f, .2f)), Is.False);
            }
        }
        [Test] public unsafe void GenericPadReceivesRumbleAndNoAdaptiveTriggerPacket()
        {
            var pad = InputSystem.AddDevice<Gamepad>(); var packets = new List<Vector2>();
            InputDeviceCommandDelegate capture = (device, command) =>
            {
                if (device != pad) return null;
                if (command->type != new FourCC('R', 'M', 'B', 'L')) return null;
                float* motors = (float*)((byte*)command + InputDeviceCommand.BaseCommandSize);
                packets.Add(new Vector2(motors[0], motors[1])); return 1L;
            };
            InputSystem.onDeviceCommand += capture;
            try
            {
                using (var output = new GamepadFeedbackOutput())
                {
                    output.Bind(pad); output.Apply(new GamepadFeedbackState(.3f, .6f, new TriggerFeedback(TriggerFeedbackMode.Resistance, 2, 4)));
                    Assert.That(output.Capabilities, Is.EqualTo(GamepadFeedbackCapabilities.Rumble));
                }
                Assert.That(packets, Is.EqualTo(new[] { new Vector2(.3f, .6f), Vector2.zero }));
            }
            finally { InputSystem.onDeviceCommand -= capture; }
        }
    }
}
