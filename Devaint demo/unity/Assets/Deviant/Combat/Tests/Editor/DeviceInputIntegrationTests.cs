using System;
using System.Collections;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Control.Contracts;
using Deviant.Input.Contracts;
using Deviant.Input.Unity;
using Deviant.Integration;
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
    public sealed class DeviceInputIntegrationTests
    {
        CombatArena arena;
        CombatKeyboardInput adapter;
        Gamepad pad;
        Keyboard keyboard;
        InputSettings.BackgroundBehavior background;
        InputSettings.EditorInputBehaviorInPlayMode editorInput;
        CombatPlayer Player => arena.Player;
        LocalInput Input => adapter.Actions;
        CombatGamepadFeedback Feedback => Player.GetComponent<CombatGamepadFeedback>();
        GamepadFeedbackProfile FeedbackSettings => GamepadFeedbackProfile.Load();
        static TriggerFeedbackMode ResistanceMode(int strength) => strength > 0 ? TriggerFeedbackMode.Resistance : TriggerFeedbackMode.Off;
        void SampleFeedback() => Feedback.SendMessage("LateUpdate");
        IEnumerator AdvanceFeedbackTime(float duration)
        {
            double end = Time.realtimeSinceStartupAsDouble + duration;
            while (Time.realtimeSinceStartupAsDouble < end)
            { arena.SendMessage("FixedUpdate"); yield return null; }
        }

        [UnitySetUp] public IEnumerator Setup()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            background = InputSystem.settings.backgroundBehavior;
            editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            arena = new GameObject("Shared device input arena").AddComponent<CombatArena>(); arena.loadCharacterArt = false;
            var content = CombatContentLoader.Load(null, false); content.Profile = CombatCompositionProfile.Game;
            content.Camera = new GameObject("Input camera").AddComponent<Camera>(); content.Loadout = Deviant.Equipment.WeaponResources.CreateLoadout();
            content.PlayerSpawn = Vector2.zero; content.PlayerSize = new Vector2(.9375f, 2); content.PlayerOffset = new Vector2(.1875f, 1);
            content.SecondaryFormIndex = 0; content.Enemies = Array.Empty<CombatEnemySpawn>();
            content.PrepareEnvironment = owner =>
            {
                var floor = new GameObject("Input floor"); floor.transform.position = new Vector3(0, -.5f);
                floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(30, 1);
            };
            content.BindPlayerInput = player => player.gameObject.AddComponent<CombatKeyboardInput>().Player = player;
            Assert.That(arena.Initialize(content), Is.True); arena.enabled = false;
            adapter = Player.GetComponent<CombatKeyboardInput>();
            keyboard = InputSystem.AddDevice<Keyboard>(); pad = InputSystem.AddDevice<Gamepad>(); pad.MakeCurrent();
            Input.Source.SetDevices(keyboard, pad);
            Poll(default); SampleFeedback();
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
        void Poll(GamepadState state)
        { InputSystem.QueueStateEvent(pad, state); InputSystem.Update(); adapter.SendMessage("Update"); arena.SendMessage("FixedUpdate"); }
        void Reset()
        { Poll(default); arena.ResetTest(); Player.Position = Vector2.zero; Player.velocity = Vector2.zero; Physics2D.SyncTransforms(); Player.grounded = Player.ProbeGround(); Poll(default); }

        [UnityTest] public IEnumerator FaultedSimulationDoesNotFillInputQueueAndResetAllowsInputAgain()
        {
            Action<float> fail = _ => throw new InvalidOperationException("input fault regression");
            arena.SimulationStepCompleted += fail;
            try
            {
                var step = typeof(CombatArena).GetMethod("FixedUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var error = Assert.Throws<System.Reflection.TargetInvocationException>(() => step.Invoke(arena, null));
                Assert.That(error.InnerException.Message, Is.EqualTo("input fault regression"));
            }
            finally { arena.SimulationStepCompleted -= fail; }
            Assert.That(arena.SimulationFaulted, Is.True);
            Poll(default); long sequence=Player.NextInputSequence;
            for(int i=0;i<1100;i++)Poll(new GamepadState { leftStick = Vector2.right });
            Assert.That(Player.NextInputSequence, Is.EqualTo(sequence), "Faulted frames must not submit new input samples.");
            Assert.That(Player.CurrentInput, Is.EqualTo(default(CombatInput)));
            arena.ResetTest(); float now=arena.Now;
            Poll(default); Assert.That(arena.SimulationFaulted, Is.False); Assert.That(arena.Now, Is.GreaterThan(now));
            yield return null;
        }
        [UnityTest] public IEnumerator GamepadFeedbackUsesStartedCommandsAndDoesNotReplayAfterPauseOrReset()
        {
            Poll(new GamepadState().WithButton(GamepadButton.West)); SampleFeedback();
            Assert.That(Feedback.CurrentState.Low + Feedback.CurrentState.High, Is.GreaterThan(0));
            Assert.That(Feedback.Capabilities, Is.EqualTo(GamepadFeedbackCapabilities.Rumble));
            using (var scope = arena.Controls.OpenScope("feedback pause"))
            {
                scope.PauseSimulation(); SampleFeedback();
                Assert.That(Feedback.CurrentState.Equals(default), Is.True);
            }
            Poll(default); SampleFeedback();
            Assert.That(Feedback.CurrentState.Low, Is.Zero);
            Reset(); SampleFeedback();
            Assert.That(Feedback.CurrentState.Low, Is.Zero);
            Poll(new GamepadState().WithButton(GamepadButton.East)); SampleFeedback();
            Assert.That(Feedback.CurrentState.Low, Is.GreaterThan(0));
            yield return AdvanceFeedbackTime(.3f);
            SampleFeedback(); Assert.That(Feedback.CurrentState.Low, Is.Zero, "Polling an old Started outcome cannot retrigger the pulse.");
        }
        [UnityTest] public IEnumerator GamepadFeedbackTracksBothSkillSlotsAndActualChargeWithoutChangingInput()
        {
            var settings = FeedbackSettings;
            Player.weapons[0].ExperimentalChargeStamp = true;
            Poll(new GamepadState { leftTrigger = 1 }); SampleFeedback();
            Assert.That(Player.InkCharging, Is.True);
            Assert.That(Feedback.CurrentState.Left.Mode, Is.EqualTo(TriggerFeedbackMode.Vibration));
            Assert.That(Feedback.CurrentState.Right.Mode, Is.EqualTo(ResistanceMode(settings.readyStrength)));
            yield return AdvanceFeedbackTime(settings.kickDuration + .03f);
            SampleFeedback();
            Assert.That(Player.InkCharging, Is.True, "Finishing the kick must not end the held charge.");
            Assert.That(Feedback.CurrentState.Left.Mode, Is.EqualTo(ResistanceMode(settings.chargingStrength)));
            Assert.That(Feedback.CurrentState.Left.Strength, Is.EqualTo(settings.chargingStrength));
            Poll(default); SampleFeedback();
            Assert.That(Player.InkCharging, Is.False);
            Reset(); SampleFeedback();
            Poll(new GamepadState { rightTrigger = 1 }); SampleFeedback();
            Assert.That(Player.selected, Is.EqualTo(1));
            Assert.That(Feedback.CurrentState.Right.Mode, Is.EqualTo(TriggerFeedbackMode.Vibration));
            Assert.That(Feedback.CurrentState.Left.Mode, Is.EqualTo(ResistanceMode(settings.readyStrength)));
            yield return AdvanceFeedbackTime(settings.kickDuration + .03f);
            SampleFeedback();
            if (settings.readyStrength == 0) Assert.That(Feedback.CurrentState.Right.Mode, Is.EqualTo(TriggerFeedbackMode.Off));
            else Assert.That(Feedback.CurrentState.Right.Mode, Is.Not.EqualTo(TriggerFeedbackMode.Vibration));
        }
        [UnityTest] public IEnumerator GamepadFeedbackObservesAcceptedZeroDamageAndIgnoresRejectedHits()
        {
            Poll(new GamepadState { leftStick = Vector2.right }); SampleFeedback();
            var enemy = arena.SpawnEnemy(0, new Vector2(12, 0));
            Assert.That(arena.TryBindActor(Player, out var handle), Is.True);
            var origin = new CombatHitOrigin(handle, default, Guid.NewGuid(), 0, "feedback fixture", Guid.Empty, CombatHitKind.Melee, isBasicAction: true);
            Assert.That(enemy.Receive(0, Player, 1, true, null, origin: origin), Is.True);
            SampleFeedback(); Assert.That(Feedback.CurrentState.Low, Is.GreaterThan(0));
            Feedback.Suspend();
            enemy.health = 0;
            Assert.That(enemy.Receive(1, Player, 1, true, null, origin: origin), Is.False);
            SampleFeedback(); Assert.That(Feedback.CurrentState.Low, Is.Zero);
            Assert.That(Player.Receive(1, enemy, -1, false, null), Is.True);
            SampleFeedback();
            var hurt = FeedbackSettings.hurt.Sample(0);
            Assert.That(Feedback.CurrentState.Low, Is.EqualTo(hurt.x).Within(.000001));
            Assert.That(Feedback.CurrentState.High, Is.EqualTo(hurt.y).Within(.000001));
            yield return null;
        }
        [UnityTest] public IEnumerator GamepadFeedbackReleasesOnFocusLossPairingKeyboardUseAndDisable()
        {
            Poll(new GamepadState().WithButton(GamepadButton.West)); SampleFeedback();
            Assert.That(Feedback.CurrentState.Low + Feedback.CurrentState.High, Is.GreaterThan(0));
            Input.Source.SetFocused(false); SampleFeedback();
            Assert.That(Feedback.CurrentState.Equals(default), Is.True);
            Input.Source.SetFocused(true); Poll(default); SampleFeedback();
            Input.Source.SetDevices(keyboard); SampleFeedback();
            Assert.That(Feedback.Capabilities, Is.EqualTo(GamepadFeedbackCapabilities.None));
            Input.Source.SetDevices(keyboard, pad); Poll(new GamepadState { leftStick = Vector2.right }); SampleFeedback();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F)); InputSystem.Update(); SampleFeedback();
            Assert.That(Input.Source.LastUsedDevice.Family, Is.EqualTo(InputDeviceFamily.KeyboardMouse));
            Assert.That(Feedback.Capabilities, Is.EqualTo(GamepadFeedbackCapabilities.None));
            Poll(new GamepadState { leftStick = Vector2.left }); SampleFeedback();
            Assert.That(Feedback.CurrentState.Left.Mode, Is.EqualTo(ResistanceMode(FeedbackSettings.readyStrength)));
            adapter.enabled = false;
            Assert.That(Feedback.CurrentState.Equals(default), Is.True);
            InputSystem.RemoveDevice(pad); SampleFeedback();
            Assert.That(Feedback.CurrentState.Equals(default), Is.True);
            yield return null;
        }
        [UnityTest] public IEnumerator GenericPadMovesJumpsDashesAndAddressesBothWeaponSlots()
        {
            Poll(new GamepadState { leftStick = Vector2.right }.WithButton(GamepadButton.West));
            Assert.That(Player.axis, Is.EqualTo(1)); Assert.That(Player.currentCast, Is.Not.Null); Assert.That(Player.selected, Is.Zero);
            Assert.That(Input.Label(GameAction.Weapon1Basic), Is.EqualTo("X"));
            Assert.That(Input.Label(GameAction.Weapon2Basic), Is.EqualTo("Y"));
            Assert.That(Input.Label(GameAction.Interact), Is.EqualTo("RB"));
            Assert.That(Input.Label(GameAction.Inventory), Is.EqualTo("LB"));
            Reset(); Poll(new GamepadState().WithButton(GamepadButton.North));
            Assert.That(Player.currentCast, Is.Not.Null); Assert.That(Player.selected, Is.EqualTo(1));
            Reset(); Poll(new GamepadState().WithButton(GamepadButton.South));
            Assert.That(Player.jumps.Used, Is.EqualTo(1)); Assert.That(Player.VelocityY, Is.GreaterThan(0));
            Reset(); Poll(new GamepadState { leftStick = Vector2.left }.WithButton(GamepadButton.East));
            Assert.That(Player.Dashing, Is.True); Assert.That(Player.facing, Is.EqualTo(-1));
            yield return null;
        }
        [UnityTest] public IEnumerator PadEdgesReachTheExistingCaptureWithNativeMetadataAndIndependentSlotHolds()
        {
            using (var capture = Player.BeginInputCapture(8))
            {
                Poll(new GamepadState { leftTrigger = 1, rightTrigger = 1 });
                Assert.That(capture.TryRead(out var batch), Is.True);
                Assert.That(batch.Input.HeldSpecials, Is.EqualTo(CombatInput.SlotSpecials));
                var presses = batch.Receipts.Where(r => r.Sample.Input.Pressed != CombatButtons.None).ToArray();
                Assert.That(presses.Select(r => r.Sample.Input.Pressed), Is.EqualTo(new[] { CombatButtons.Weapon1Special, CombatButtons.Weapon2Special }));
                Assert.That(presses.All(r => r.Sample.Source == CombatInputSource.Gamepad && r.Sample.DeviceId == pad.deviceId && r.Sample.TimeDomain == CombatInputTimeDomain.Realtime), Is.True);
                Poll(new GamepadState { rightTrigger = 1 });
                Assert.That(capture.TryRead(out batch), Is.True);
                Assert.That(batch.Input.HeldSpecials, Is.EqualTo(CombatButtons.Weapon2Special));
                Assert.That(batch.Input.Released, Is.EqualTo(CombatButtons.Weapon1Special));
            }
            yield return null;
        }
        [UnityTest] public IEnumerator BelowAndJumpUseDropAndKeepTheReleasePairedAfterDirectionChanges()
        {
            using (var capture = Player.BeginInputCapture(8))
            {
                Poll(new GamepadState { leftStick = Vector2.down }.WithButton(GamepadButton.South));
                Assert.That(capture.TryRead(out var first), Is.True); Assert.That(first.Input.Pressed, Is.EqualTo(CombatButtons.Drop));
                Poll(new GamepadState { leftStick = Vector2.up });
                Assert.That(capture.TryRead(out var second), Is.True); Assert.That(second.Input.Released, Is.EqualTo(CombatButtons.Drop));
            }
            yield return null;
        }
        [UnityTest] public IEnumerator ShortLivedModalAndActorResetCannotReplayOldPadButtons()
        {
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South).WithButton(GamepadButton.East)); InputSystem.Update();
            using (var scope = arena.Controls.OpenScope("same frame UI")) { scope.BlockInput(InputChannels.Combat); scope.PauseSimulation(); }
            adapter.SendMessage("Update"); arena.SendMessage("FixedUpdate");
            Assert.That(Player.jumps.Used, Is.Zero); Assert.That(Player.Dashing, Is.False);
            Poll(default);
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.West)); InputSystem.Update();
            arena.ResetTest(); adapter.SendMessage("Update"); arena.SendMessage("FixedUpdate");
            Assert.That(Player.currentCast, Is.Null);
            Poll(default); Poll(new GamepadState().WithButton(GamepadButton.West)); Assert.That(Player.currentCast, Is.Not.Null);
            yield return null;
        }
        void PollKeyboard(params Key[] keys)
        { InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); InputSystem.Update(); adapter.SendMessage("Update"); arena.SendMessage("FixedUpdate"); }

        [UnityTest] public IEnumerator WeaponOrderKeySwapsOnceAndNewSlotAttacksTheSameWeapon()
        {
            var a = Player.weapons[0]; var b = Player.weapons[1];
            a.Cooldown.Spend(arena.Now); float recovery = a.Cooldown.NextReady;
            PollKeyboard(Key.X);
            Assert.That(Player.weapons[0], Is.SameAs(b)); Assert.That(Player.weapons[1], Is.SameAs(a));
            Assert.That(Player.Weapon, Is.SameAs(a)); Assert.That(a.Cooldown.NextReady, Is.EqualTo(recovery));
            PollKeyboard(Key.X); Assert.That(Player.weapons[0], Is.SameAs(b), "Holding X must not repeatedly swap.");
            PollKeyboard(); PollKeyboard(Key.A);
            Assert.That(Player.currentCast, Is.Not.Null); Assert.That(Player.Weapon, Is.SameAs(b));
            PollKeyboard(Key.X); Assert.That(Player.weapons[0], Is.SameAs(b), "Cannot bypass action recovery by reordering.");
            Player.CancelAction(); PollKeyboard(); PollKeyboard(Key.X);
            Assert.That(Player.weapons[0], Is.SameAs(a)); Assert.That(Player.weapons[1], Is.SameAs(b));
            yield return null;
        }
        [UnityTest] public IEnumerator WeaponOrderKeyDoesNotReplayAfterPauseOrSwapAnUnreadyPair()
        {
            var a = Player.weapons[0];
            Player.SetWeaponSlotReady(1, false); PollKeyboard(Key.X);
            Assert.That(Player.weapons[0], Is.SameAs(a)); Player.SetWeaponSlotReady(1, true);
            PollKeyboard();
            using (var scope = arena.Controls.OpenScope("weapon reorder menu"))
            { scope.BlockInput(InputChannels.Combat); scope.PauseSimulation(); PollKeyboard(Key.X); Assert.That(Player.weapons[0], Is.SameAs(a)); }
            PollKeyboard(Key.X); Assert.That(Player.weapons[0], Is.SameAs(a));
            PollKeyboard(); PollKeyboard(Key.X); Assert.That(Player.weapons[1], Is.SameAs(a));
            yield return null;
        }
        [UnityTest] public IEnumerator LegacyKeyboardOverridesAndFocusRecoveryUseTheSameNativeSource()
        {
            adapter.Bindings.Weapon1Basic = Key.T;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.T)); InputSystem.Update(); adapter.SendMessage("Update"); arena.SendMessage("FixedUpdate");
            Assert.That(Player.currentCast, Is.Not.Null);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update(); Reset();
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.West)); InputSystem.Update();
            adapter.SendMessage("OnApplicationFocus", false); adapter.SendMessage("OnApplicationFocus", true);
            adapter.SendMessage("Update"); arena.SendMessage("FixedUpdate"); Assert.That(Player.currentCast, Is.Null);
            Poll(default); Poll(new GamepadState().WithButton(GamepadButton.West)); Assert.That(Player.currentCast, Is.Not.Null);
            yield return null;
        }
        [UnityTest] public IEnumerator DeviceLossClearsMovementAndOneInteractionHasOnlyOneSuccessfulConsumer()
        {
            Poll(new GamepadState { leftStick = Vector2.right }); Assert.That(Player.axis, Is.EqualTo(1));
            InputSystem.RemoveDevice(pad); adapter.SendMessage("Update"); arena.SendMessage("FixedUpdate"); Assert.That(Player.axis, Is.Zero);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F)); InputSystem.Update();
            int called = 0;
            Assert.That(PlayerControls.TryPerform(Player, GameAction.Interact, () => false), Is.False);
            Assert.That(PlayerControls.TryPerform(Player, GameAction.Interact, () => { called++; return true; }), Is.True);
            Assert.That(PlayerControls.TryPerform(Player, GameAction.Interact, () => { called++; return true; }), Is.False);
            Assert.That(called, Is.EqualTo(1));
            yield return null;
        }
        [UnityTest] public IEnumerator NativeMenuNavigatesScrollsAndConfirmsWithoutJumping()
        {
            var events = new GameObject("Input menu events", typeof(EventSystem), typeof(InputSystemUIInputModule)).GetComponent<EventSystem>();
            var panel = new GameObject("Input menu", typeof(RectTransform), typeof(Canvas));
            Button Make(string name, float y)
            {
                var button = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Button>();
                button.transform.SetParent(panel.transform, false); var rect = (RectTransform)button.transform;
                rect.sizeDelta = new Vector2(100, 30); rect.anchoredPosition = new Vector2(0, y); return button;
            }
            var first = Make("First", 30); var second = Make("Second", -30); int clicks = 0;
            var scroll = new GameObject("Description", typeof(RectTransform), typeof(ScrollRect)).GetComponent<ScrollRect>();
            scroll.transform.SetParent(panel.transform, false); scroll.viewport = (RectTransform)scroll.transform;
            scroll.viewport.sizeDelta = new Vector2(200, 80); scroll.horizontal = false;
            scroll.content = new GameObject("Long description", typeof(RectTransform)).GetComponent<RectTransform>();
            scroll.content.SetParent(scroll.viewport, false); scroll.content.anchorMin = scroll.content.anchorMax = new Vector2(.5f, 1);
            scroll.content.pivot = new Vector2(.5f, 1); scroll.content.sizeDelta = new Vector2(200, 500);
            Canvas.ForceUpdateCanvases(); scroll.verticalNormalizedPosition = 1;
            var binding = panel.AddComponent<LocalInputUIBinding>(); binding.Bind(Input, events); binding.SetPanel(panel);
            second.onClick.AddListener(() => { clicks++; binding.SetPanel(null); });
            events.SetSelectedGameObject(first.gameObject);
            InputSystem.QueueStateEvent(pad, new GamepadState { rightStick = Vector2.down }); InputSystem.Update();
            binding.SendMessage("LateUpdate");
            Assert.That(scroll.verticalNormalizedPosition, Is.LessThan(1), "Right stick can scroll a description without selectable children.");
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.DpadDown)); InputSystem.Update();
            events.GetComponent<InputSystemUIInputModule>().Process();
            Assert.That(events.currentSelectedGameObject, Is.EqualTo(second.gameObject));
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South)); InputSystem.Update();
            events.GetComponent<InputSystemUIInputModule>().Process(); adapter.SendMessage("Update"); arena.SendMessage("FixedUpdate");
            Assert.That(clicks, Is.EqualTo(1)); Assert.That(Player.jumps.Used, Is.Zero);
            binding.Unbind(); Object.Destroy(panel); Object.Destroy(events.gameObject);
            yield return null;
        }
    }
}
