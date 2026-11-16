using Deviant.Input.Contracts;
using Deviant.Input.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Deviant.Input.Tests
{
    // HUD asks for key names every frame; the name is kept until the device kind or the binding changes (2026-11-06).
    public class LocalInputLabelTests : InputTestFixture
    {
        GameObject host;
        public override void TearDown() { if (host) Object.DestroyImmediate(host); base.TearDown(); }

        [TestCase("Gamepad", "A", "RB", "LB", "B", "View")]
        [TestCase("XInputController", "A", "RB", "LB", "B", "View")]
        [TestCase("DualSenseGamepadHID", "Cross", "R1", "L1", "Circle", "Create")]
        public void PromptTemplatesFollowDeviceChangesAndKeepActionAndDirectionOverrides(string layout, string jump, string interact, string inventory, string cancel, string map)
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var pad = (Gamepad)InputSystem.AddDevice(layout);
            host = new GameObject("prompt labels");
            var input = host.AddComponent<LocalInput>();
            input.Source.SetFocused(true);
            input.Source.SetDevices(keyboard, pad);
            const string template = "{Interact} 열기 · {Inventory}/{Cancel} 닫기 · {Jump} 점프 · {Move.Up}+{Jump} 벽차기 · {CheckpointInstall}/{CheckpointReturn} 포탈 · {Advance} 다음 · {Map} 지도";
            if (layout == "DualSenseGamepadHID")
            {
                InputSystem.QueueStateEvent(pad, new UnityEngine.InputSystem.DualShock.LowLevel.DualSenseHIDInputReport {
                    leftStickX = 128, leftStickY = 128, rightStickX = 128, rightStickY = 128, buttons0 = 8 | (1 << 5) });
                InputSystem.Update();
            }
            else { Press(pad.buttonSouth); Release(pad.buttonSouth); }
            Assert.That(input.FormatPrompt(template), Is.EqualTo($"{interact} 열기 · {inventory}/{cancel} 닫기 · {jump} 점프 · LS/D-Pad 위+{jump} 벽차기 · L3/R3 포탈 · {jump} 다음 · {map} 지도"));
            Assert.That(input.FormatPrompt("{Move.Down}+{Jump} 내려가기"), Is.EqualTo($"LS/D-Pad 아래+{jump} 내려가기"));
            Assert.That(input.Label(GameAction.RerollReward), Is.EqualTo(layout == "DualSenseGamepadHID" ? "Square" : "X"));
            Assert.That(input.Label(GameAction.InventoryRemove), Is.EqualTo(layout == "DualSenseGamepadHID" ? "Square" : "X"));
            Assert.That(input.Label(GameAction.InventoryRename), Is.EqualTo(layout == "DualSenseGamepadHID" ? "Triangle" : "Y"));
            Assert.That(input.FormatPrompt("{Help} 조작 안내"), Does.StartWith("키보드 "), "an unbound controller action never invents a pad button");
            var leap = input.Source.GetAction(GameAction.Jump);
            var use = input.Source.GetAction(GameAction.Interact);
            var movement = input.Source.GetAction(GameAction.Move);
            for (int i = 0; i < leap.bindings.Count; i++) if (leap.bindings[i].effectivePath == "<Keyboard>/space") leap.ApplyBindingOverride(i, "<Keyboard>/k");
            for (int i = 0; i < use.bindings.Count; i++) if (use.bindings[i].effectivePath == "<Keyboard>/f") use.ApplyBindingOverride(i, "<Keyboard>/g");
            for (int i = 0; i < movement.bindings.Count; i++) if (movement.bindings[i].effectivePath == "<Keyboard>/upArrow") movement.ApplyBindingOverride(i, "<Keyboard>/u");
            Press(keyboard.gKey); Release(keyboard.gKey);
            Assert.That(input.FormatPrompt("{Interact} 열기 · {Jump} 점프 · {Move.Up}+{Jump} 벽차기"), Is.EqualTo("G 열기 · K 점프 · U+K 벽차기"));
            Assert.That(input.FormatPrompt("아무 키 이름도 없는 문장 {unknown}"), Is.EqualTo("아무 키 이름도 없는 문장 {unknown}"));
            Assert.That(template, Does.Contain("{Interact}"), "the source template remains reusable after every device change");
        }

        [Test] public void RewardAndInventoryActionsUseNativePadBindingsAndKeyboardOverrides()
        {
            var keyboard=InputSystem.AddDevice<Keyboard>();var pad=InputSystem.AddDevice<Gamepad>();
            host=new GameObject("reward inventory input");var input=host.AddComponent<LocalInput>();
            input.Source.SetDevices(keyboard,pad);input.Source.SetFocused(true);
            Set(pad.rightStick,Vector2.up);
            Assert.That(input.Label(GameAction.OpenReward),Is.EqualTo("RS ↑"));
            Assert.That(input.Take(GameAction.OpenReward),Is.True);
            Assert.That(input.Take(GameAction.OpenReward),Is.False);
            Assert.That(input.Pressed(GameAction.CheckpointReturn),Is.False,"a stick tilt never installs a portal");
            Assert.That(input.Read(GameAction.Move).X,Is.Zero);
            Set(pad.rightStick,Vector2.zero);
            Set(pad.rightStick,Vector2.left);
            Assert.That(input.Take(GameAction.SwapWeaponOrder),Is.True);
            Assert.That(input.Label(GameAction.SwapWeaponOrder),Is.EqualTo("RS ←"));
            Set(pad.rightStick,Vector2.zero);
            Press(pad.buttonWest);
            Assert.That(input.Take(GameAction.RerollReward),Is.True);
            Assert.That(input.Take(GameAction.InventoryRemove),Is.True,"separate screens route their own action");
            Assert.That(input.Label(GameAction.InventoryRemove),Is.EqualTo("X"));
            Release(pad.buttonWest);Press(pad.buttonNorth);
            Assert.That(input.Take(GameAction.InventoryRename),Is.True);
            Release(pad.buttonNorth);
            var reward=input.Source.GetAction(GameAction.OpenReward);
            for(int i=0;i<reward.bindings.Count;i++)if(reward.bindings[i].path=="<Keyboard>/g")input.Source.OverrideBinding(GameAction.OpenReward,i,"<Keyboard>/j");
            Press(keyboard.jKey);
            Assert.That(input.Take(GameAction.OpenReward),Is.True);
            Assert.That(input.Label(GameAction.OpenReward),Is.EqualTo("J"));
        }

        [Test] public void MapPanUsesKeyboardArrowsAndKeepsRebindingAndPadLabels()
        {
            var keyboard=InputSystem.AddDevice<Keyboard>();var pad=InputSystem.AddDevice<Gamepad>();
            host=new GameObject("map input labels");var input=host.AddComponent<LocalInput>();
            input.Source.SetDevices(keyboard,pad);input.Source.SetFocused(true);
            Press(keyboard.rightArrowKey);
            Assert.That(input.Read(GameAction.MapPan).X,Is.EqualTo(1));
            Assert.That(input.FormatPrompt("{MapPan}"),Is.EqualTo("방향키"));
            Release(keyboard.rightArrowKey);
            var action=input.Source.GetAction(GameAction.MapPan);
            for(int i=0;i<action.bindings.Count;i++)
                if(action.bindings[i].path=="<Keyboard>/upArrow")input.Source.OverrideBinding(GameAction.MapPan,i,"<Keyboard>/k");
            Press(keyboard.kKey);
            Assert.That(input.Read(GameAction.MapPan).Y,Is.EqualTo(1));
            Assert.That(input.FormatPrompt("{MapPan}"),Does.Contain("K"));
            Release(keyboard.kKey);
            InputSystem.QueueStateEvent(pad,new UnityEngine.InputSystem.LowLevel.GamepadState{rightStick=new Vector2(.75f,0)});InputSystem.Update();
            Assert.That(input.Read(GameAction.MapPan).X,Is.GreaterThan(.5f));
            Assert.That(input.FormatPrompt("{MapPan}"),Is.EqualTo("RS"));
        }

        [Test] public void LabelIsReusedUntilTheBindingChanges()
        {
            host = new GameObject("local input label test");
            var input = host.AddComponent<LocalInput>();
            string first = input.Label(GameAction.Jump);
            Assert.That(first, Is.Not.Empty);
            Assert.That(input.Label(GameAction.Jump), Is.SameAs(first), "the stored name is returned without rebuilding it");

            var action = input.Source.GetAction(GameAction.Jump);
            int index = -1;
            for (int i = 0; i < action.bindings.Count && index < 0; i++)
                if (action.bindings[i].effectivePath.StartsWith("<Keyboard>")) index = i;
            Assert.That(index, Is.GreaterThanOrEqualTo(0), "Jump has a keyboard binding");
            action.ApplyBindingOverride(index, "<Keyboard>/k");
            try
            {
                Assert.That(input.Label(GameAction.Jump), Is.EqualTo("K"), "a rebinding is seen on the next request");
            }
            finally { action.RemoveBindingOverride(index); }
            Assert.That(input.Label(GameAction.Jump), Is.EqualTo(first), "removing the override restores the original name");

            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 200; i++) input.Label(GameAction.Weapon1Basic);
            watch.Stop();
            Assert.That(watch.Elapsed.TotalMilliseconds / 200, Is.LessThan(.05), "a stored name costs far less than building it");
        }
    }
}
