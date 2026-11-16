using Deviant.Combat.Contracts;
using Deviant.Input.Contracts;
using Deviant.Input.Unity;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Deviant.Combat
{
    // Presentation-only observer of accepted hits and actual command starts.
    // It never changes input thresholds, charge clocks, resources or simulation.
    [DefaultExecutionOrder(500)]
    public sealed class CombatGamepadFeedback : MonoBehaviour
    {
        struct ActivePulse { public GamepadFeedbackProfile.Pulse Profile; public double Started; }
        readonly ActivePulse[] pulses = new ActivePulse[16];
        readonly CombatAcceptedHit[] hits = new CombatAcceptedHit[64];
        readonly CombatCommandOutcome[] outcomes = new CombatCommandOutcome[64];
        readonly GamepadFeedbackOutput output = new GamepadFeedbackOutput();
        CombatKeyboardInput adapter;
        CombatArena arena;
        CombatPlayer boundPlayer;
        GamepadFeedbackProfile profile;
        CombatOutcomeCursor hitCursor, commandCursor;
        long invalidation;
        bool bound, focused = true, applicationPaused;
        double leftKickUntil, rightKickUntil;
        public GamepadFeedbackState CurrentState { get; private set; }
        public GamepadFeedbackCapabilities Capabilities => output.Capabilities;
        public bool LastWriteSucceeded => output.LastWriteSucceeded;
        public void Suspend() => ResetBoundary();

        public void Bind(CombatKeyboardInput input)
        {
            Unbind(); adapter = input;
            if (!adapter || !adapter.Player || !adapter.Player.arena) return;
            boundPlayer = adapter.Player;
            arena = boundPlayer.arena; profile = GamepadFeedbackProfile.Load();
            arena.ResetCompleted += ResetBoundary; arena.Deactivated += Stop;
            boundPlayer.Died += Stop;
            RebindCursors();
        }
        void OnEnable() { InputSystem.onDeviceChange += DeviceChanged; ResetBoundary(); }
        void OnDisable() { InputSystem.onDeviceChange -= DeviceChanged; ResetBoundary(); }
        void OnDestroy() { Unbind(); output.Dispose(); }
        void OnApplicationFocus(bool value) { focused = value; if (!value) ResetBoundary(); }
        void OnApplicationPause(bool value) { applicationPaused = value; if (value) ResetBoundary(); }
        void DeviceChanged(InputDevice device, InputDeviceChange change)
        {
            if (device == output.Device && (change == InputDeviceChange.Removed || change == InputDeviceChange.Disconnected || change == InputDeviceChange.Disabled))
            { ResetBoundary(); output.Bind(null); }
        }
        void Unbind()
        {
            Stop();
            if (arena) { arena.ResetCompleted -= ResetBoundary; arena.Deactivated -= Stop; }
            if (boundPlayer) boundPlayer.Died -= Stop;
            arena = null; boundPlayer = null; adapter = null; bound = false; output.Bind(null);
        }
        void RebindCursors()
        {
            bound = arena && arena.HitEvents.TryGetCursor(out hitCursor);
            if (bound) commandCursor = new CombatOutcomeCursor(hitCursor.Session);
            // Read only the latest sequence, then bind at the current stream end.
            if (bound && arena.Commands.TryReadOutcomes(commandCursor, outcomes, out var page))
                commandCursor = new CombatOutcomeCursor(hitCursor.Session, page.LatestEventSequence);
            if (adapter && adapter.Player) invalidation = adapter.Player.InputInvalidation;
        }
        void ResetBoundary() { Stop(); output.Bind(null); RebindCursors(); }
        void Stop()
        {
            System.Array.Clear(pulses, 0, pulses.Length); leftKickUntil = rightKickUntil = 0;
            CurrentState = default; output.Release();
        }
        void LateUpdate()
        {
            var player = adapter ? adapter.Player : null;
            var actions = adapter ? adapter.Actions : null;
            if (!arena || !player || !actions || !adapter.isActiveAndEnabled || !actions.isActiveAndEnabled || !focused || applicationPaused ||
                !actions.Source.IsFocused || (actions.Source.Contexts & InputContexts.Gameplay) == 0 || player.Dead || player.InputBlocked || arena.Controls.Current.SimulationPaused ||
                arena.SimulationFaulted || Time.timeScale == 0 || player.UseTestInput || arena.ManualSimulationActive || actions.GameplaySuppressed)
            { ResetBoundary(); return; }
            if (!bound || !arena.ReadModel.TryGetSession(out var session) || !session.Equals(hitCursor.Session) || invalidation != player.InputInvalidation)
            { ResetBoundary(); return; }
            if (!arena.ReadModel.TryReadPlayer(session, out var snapshot)) { ResetBoundary(); return; }
            var pad = actions.Source.FeedbackGamepad;
            if (output.Device != pad)
            {
                Stop();
                if (!output.Bind(pad)) { RebindCursors(); return; }
            }
            double now = Time.realtimeSinceStartupAsDouble;
            ReadCommands(snapshot.Handle, now, pad != null);
            ReadHits(snapshot.Handle, now, pad != null);
            if (pad == null) return;
            float low = 0, high = 0;
            for (int i = 0; i < pulses.Length; i++)
            {
                var pulse = pulses[i];
                double elapsed = now - pulse.Started;
                if (pulse.Profile.Duration <= 0 || elapsed >= pulse.Profile.Duration) { pulses[i] = default; continue; }
                var level = pulse.Profile.Sample(elapsed);
                low = Mathf.Max(low, level.x); high = Mathf.Max(high, level.y);
            }
            bool charging = snapshot.Action.Phase == CombatActionReadPhase.Charging || player.InkCharging;
            var left = Trigger(snapshot.Resources.Slot0, player.IsWeaponSlotReady(0), charging && snapshot.Resources.ActiveSlot == 0, now < leftKickUntil);
            var right = Trigger(snapshot.Resources.Slot1, player.IsWeaponSlotReady(1), charging && snapshot.Resources.ActiveSlot == 1, now < rightKickUntil);
            CurrentState = new GamepadFeedbackState(low, high, left, right);
            output.Apply(CurrentState);
        }
        TriggerFeedback Trigger(WeaponChargeReadSnapshot weapon, bool slotReady, bool charging, bool kick)
        {
            if (!slotReady) return default;
            if (kick) return new TriggerFeedback(TriggerFeedbackMode.Vibration, profile.triggerStart, profile.kickStrength, profile.kickFrequency);
            if (charging) return profile.chargingStrength > 0
                ? new TriggerFeedback(TriggerFeedbackMode.Resistance, profile.triggerStart, profile.chargingStrength) : default;
            return weapon.Available > 0 && profile.readyStrength > 0
                ? new TriggerFeedback(TriggerFeedbackMode.Resistance, profile.triggerStart, profile.readyStrength) : default;
        }
        void Add(GamepadFeedbackProfile.Pulse pulse, double now)
        {
            int chosen = 0; double earliest = double.MaxValue;
            for (int i = 0; i < pulses.Length; i++)
            {
                double end = pulses[i].Started + pulses[i].Profile.Duration;
                if (end < earliest) { earliest = end; chosen = i; }
            }
            pulses[chosen] = new ActivePulse { Profile = pulse, Started = now };
        }
        void ReadCommands(CombatActorHandle player, double now, bool play)
        {
            while (arena.Commands.TryReadOutcomes(commandCursor, outcomes, out var page))
            {
                commandCursor = page.Next;
                for (int i = 0; play && i < page.Count; i++)
                {
                    var outcome = outcomes[i];
                    if (!outcome.Command.Actor.Equals(player) || outcome.State != CombatCommandState.Started || arena.Now - outcome.Now > profile.maximumEventAge) continue;
                    switch (outcome.Command.Kind)
                    {
                        case CombatCommandKind.Basic: Add(profile.basic, now); break;
                        case CombatCommandKind.Special:
                            Add(profile.special, now);
                            if (outcome.Snapshot.Resources.ActiveSlot == 0) leftKickUntil = now + profile.kickDuration;
                            else rightKickUntil = now + profile.kickDuration;
                            break;
                        case CombatCommandKind.Dash: Add(profile.dash, now); break;
                        case CombatCommandKind.Jump: Add(profile.jump, now); break;
                    }
                }
                if (page.Count < outcomes.Length) break;
            }
        }
        void ReadHits(CombatActorHandle player, double now, bool play)
        {
            while (arena.HitEvents.TryRead(hitCursor, hits, out var page))
            {
                hitCursor = page.Next;
                for (int i = 0; play && i < page.Count; i++)
                {
                    var hit = hits[i];
                    if (arena.Now - hit.Now > profile.maximumEventAge) continue;
                    if (hit.Target.Equals(player)) Add(profile.hurt, now);
                    else if (hit.Origin.EffectiveSource.Equals(player))
                    {
                        bool heavy = !hit.Origin.IsBasicAction || hit.Origin.Kind == CombatHitKind.Blast || hit.Origin.Kind == CombatHitKind.Missile;
                        Add(hit.Origin.Kind == CombatHitKind.Parry || hit.Origin.Reflector.Equals(player) ? profile.parry : heavy ? profile.heavyHit : profile.hit, now);
                    }
                }
                if (page.Count < hits.Length) break;
            }
        }
    }
}
