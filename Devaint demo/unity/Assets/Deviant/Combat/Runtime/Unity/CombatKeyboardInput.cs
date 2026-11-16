using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Input.Contracts;
using Deviant.Input.Unity;
using Deviant.Integration.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Deviant.Combat
{
    // Compatibility component for existing scenes/tools. All physical devices now
    // use one shared native action source; this adapter only admits combat intent.
    public sealed class CombatKeyboardInput : MonoBehaviour
    {
        [SerializeField] CombatKeyboardBindings bindings = new CombatKeyboardBindings();
        readonly CombatInputSample[] pending = new CombatInputSample[1024];
        readonly Dictionary<Key, Key> previousKeys = new Dictionary<Key, Key>();
        readonly CombatInputMapper mapper = new CombatInputMapper();
        CombatPlayer player;
        LocalInput actions;
        CombatGamepadFeedback feedback;
        CombatInputSession observedSession, preparedSession;
        long observedInvalidation, authority = 1, preparedAuthority, deliveredSequence, mappedRevision;
        bool observedEligible, observedResetEligible;
        InputDeviceInfo lastCombatDevice;
        public CombatKeyboardBindings Bindings => bindings ?? (bindings = new CombatKeyboardBindings());
        public bool ResetKeyEnabled { get; set; } = true;
        public LocalInput Actions => actions;
        public CombatPlayer Player
        {
            get => player;
            set
            {
                if (player == value) return;
                if (feedback) feedback.Bind(null);
                player = value;
                if (!player) return;
                actions = GetComponent<LocalInput>() ?? gameObject.AddComponent<LocalInput>();
                actions.Source.CaptureToken = CaptureToken;
                actions.SetDebugEnabled(false);
                actions.Source.Reset();
                preparedAuthority = CaptureToken(); preparedSession = player.InputSession; mappedRevision = actions.Source.Revision;
                mapper.Synchronize(mappedRevision);
                feedback = GetComponent<CombatGamepadFeedback>() ?? gameObject.AddComponent<CombatGamepadFeedback>();
                feedback.Bind(this);
            }
        }
        bool Restricted => !player || !player.arena || !isActiveAndEnabled || player.InputBlocked || player.Dead ||
            player.arena.SimulationFaulted || player.arena.Controls.Current.SimulationPaused || Time.timeScale == 0;
        bool CanReset => player && player.arena && isActiveAndEnabled && ResetKeyEnabled &&
            !player.InputBlocked && !player.arena.Controls.Current.SimulationPaused && Time.timeScale != 0 &&
            !player.arena.ManualSimulationActive && !player.UseTestInput;
        bool Eligible => !Restricted && !player.arena.ManualSimulationActive && !player.UseTestInput;
        internal long CurrentCaptureToken => CaptureToken();
        long CaptureToken()
        {
            if (!player || !player.arena) return -authority;
            var session = player.InputSession; bool eligible = Eligible, resetEligible = CanReset;
            if (!session.Equals(observedSession) || observedInvalidation != player.InputInvalidation || observedEligible != eligible || observedResetEligible != resetEligible)
            {
                observedSession = session; observedInvalidation = player.InputInvalidation;
                observedEligible = eligible; observedResetEligible = resetEligible; authority++;
            }
            return eligible ? authority : -authority;
        }
        void OnEnable() => InputSystem.onBeforeUpdate += BeforeInputUpdate;
        void OnDisable()
        {
            InputSystem.onBeforeUpdate -= BeforeInputUpdate;
            if (actions) actions.Source.Reset();
            if (feedback) feedback.Suspend();
            if (player) player.ClearInputState();
        }
        void OnDestroy() { if (actions) actions.Source.CaptureToken = null; }
        void OnApplicationFocus(bool focused) { if (actions) actions.Source.SetFocused(focused); if (!focused && player) player.ClearInputState(); }
        void OnApplicationPause(bool paused) { if (paused) OnApplicationFocus(false); }
        void BeforeInputUpdate()
        {
            if (!player || !actions) return;
            RefreshKeyboardBindings();
            actions.SetDebugEnabled(false);
            PrepareBoundary();
        }
        void PrepareBoundary()
        {
            long token = CaptureToken();
            if (preparedAuthority == token) return;
            bool actorChanged = !preparedSession.Equals(player.InputSession);
            preparedSession = player.InputSession; preparedAuthority = token;
            // A combat restriction must not release a held UI Pause/Advance.
            actions.Source.Reset(actorChanged ? InputContexts.All : InputContexts.Gameplay | InputContexts.Debug);
        }
        void Update()
        {
            if (!player || !player.arena || !actions) return;
            PrepareBoundary();
            var source = actions.Source;
            actions.Poll();
            if (mappedRevision != source.Revision)
            { mappedRevision = source.Revision; mapper.Synchronize(mappedRevision); }
            long token = CaptureToken();
            int total = actions.EventCount;
            for (int i = 0; i < total; i++)
            {
                var input = actions.EventAt(i);
                if (input.Sequence <= deliveredSequence || input.CaptureToken != token || actions.IsConsumed(i)) continue;
                if (input.Action == GameAction.DevelopmentReset && input.Phase == InputPhase.Pressed && CanReset)
                { deliveredSequence = input.Sequence; actions.Take(GameAction.DevelopmentReset); player.arena.ResetTest(); return; }
            }
            if (player.UseTestInput || player.arena.ManualSimulationActive)
            { MarkDelivered(total); return; }
            if (Restricted || actions.GameplaySuppressed)
            {
                var current = player.CurrentInput;
                if (player.InputIngress.PendingCount > 0 || current.Axis != 0 || current.ClimbAxis != 0 || current.HeldSpecials != CombatButtons.None ||
                    current.SpecialHeld || current.Pressed != CombatButtons.None || current.Released != CombatButtons.None)
                { if (Time.timeScale == 0) player.PauseKeyboardInput(); else player.ClearInputState(); }
                MarkDelivered(total); return;
            }
            for (int i = 0; i < total; i++)
            {
                var input = actions.EventAt(i);
                if (input.Sequence <= deliveredSequence || input.CaptureToken != token || input.Revision != source.Revision || actions.IsConsumed(i) ||
                    input.Action != GameAction.SwapWeaponOrder || input.Phase != InputPhase.Pressed) continue;
                actions.Take(GameAction.SwapWeaponOrder);
                if (!player.TrySwapWeaponOrder()) continue;
                // A fresh press is required for attacks/skills after the new ordering;
                // never deliver an old slot's hold or queued attack to its replacement.
                source.Reset(InputContexts.Gameplay);
                mappedRevision = source.Revision; mapper.Synchronize(mappedRevision);
                MarkDelivered(total); return;
            }
            int count = 0;
            for (int i = 0; i < total; i++)
            {
                var input = actions.EventAt(i);
                if (input.Sequence <= deliveredSequence || input.CaptureToken != token || input.Revision != source.Revision || actions.IsConsumed(i)) continue;
                if (!mapper.TryMap(input, player.InputSession, 0, out var mapped)) continue;
                pending[count++] = mapped; lastCombatDevice = input.Device;
            }
            MarkDelivered(total);
            var continuous = mapper.Current;
            var last = count > 0 ? pending[count - 1].Input : player.CurrentInput;
            bool addContinuous = count == 0 || last.Axis != continuous.Axis || last.ClimbAxis != continuous.ClimbAxis || last.HeldSpecials != continuous.HeldSpecials;
            int deviceId = lastCombatDevice.Id;
            if (deviceId <= 0 || InputSystem.GetDeviceById(deviceId) == null)
            {
                var keyboard = Keyboard.current;
                var pad = Gamepad.current;
                deviceId = keyboard != null && keyboard.enabled ? keyboard.deviceId : pad != null && pad.enabled ? pad.deviceId : 0;
            }
            if (deviceId == 0) { player.ClearInputState(); return; }
            if (count + (addContinuous ? 1 : 0) > player.InputIngress.AvailableCapacity)
            { source.Reset(); throw new InvalidOperationException("Device input batch exceeds remaining input capacity; no partial batch was admitted."); }
            for (int i = 0; i < count; i++)
            {
                var input = pending[i];
                player.arena.Commands.SubmitPlayerInput(new CombatInputSample(input.Session, player.NextInputSequence, input.Input,
                    input.Source, input.DeviceId, input.TimeDomain, input.Timestamp));
            }
            if (addContinuous)
                player.arena.Commands.SubmitPlayerInput(new CombatInputSample(player.InputSession, player.NextInputSequence, continuous,
                    InputSystem.GetDeviceById(deviceId) is Keyboard ? CombatInputSource.Keyboard : CombatInputSource.Gamepad,
                    deviceId, CombatInputTimeDomain.Realtime, Time.realtimeSinceStartupAsDouble));
        }
        void MarkDelivered(int count)
        { for (int i = 0; i < count; i++) deliveredSequence = Math.Max(deliveredSequence, actions.EventAt(i).Sequence); }

        // Migration bridge for serialized keys and existing editor tooling. New
        // bindings are authored in the native asset; only changed legacy fields
        // override that session's keyboard controls before native event processing.
        void RefreshKeyboardBindings()
        {
            Rebind(GameAction.Move, Key.LeftArrow, Bindings.MoveLeft);
            Rebind(GameAction.Move, Key.RightArrow, Bindings.MoveRight);
            Rebind(GameAction.Move, Key.UpArrow, Bindings.ClimbUp);
            Rebind(GameAction.Move, Key.DownArrow, Bindings.ClimbDown);
            Rebind(GameAction.Weapon1Basic, Key.A, Bindings.Weapon1Basic); Rebind(GameAction.Weapon2Basic, Key.S, Bindings.Weapon2Basic);
            Rebind(GameAction.Weapon1Special, Key.Q, Bindings.Weapon1Special); Rebind(GameAction.Weapon2Special, Key.W, Bindings.Weapon2Special);
            Rebind(GameAction.Jump, Key.Space, Bindings.Jump); Rebind(GameAction.Dash, Key.LeftShift, Bindings.Dash); Rebind(GameAction.Dash, Key.RightShift, Bindings.AlternateDash);
            Rebind(GameAction.Interact, Key.F, Bindings.Interact); Rebind(GameAction.CheckpointInstall, Key.C, Bindings.CheckpointInstall);
            Rebind(GameAction.CheckpointReturn, Key.V, Bindings.CheckpointReturn); Rebind(GameAction.DevelopmentReset, Key.F5, Bindings.DevelopmentReset);
        }
        void Rebind(GameAction action, Key original, Key next)
        {
            if ((previousKeys.TryGetValue(original, out var previous) ? previous : original) == next) return;
            string path = "<Keyboard>/" + original;
            var native = actions.Source.GetAction(action);
            for (int i = 0; i < native.bindings.Count; i++)
                if (string.Equals(native.bindings[i].path, path, StringComparison.OrdinalIgnoreCase))
                { actions.Source.OverrideBinding(action, i, next == Key.None ? string.Empty : "<Keyboard>/" + next); break; }
            previousKeys[original] = next;
        }
    }
}
