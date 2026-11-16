using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using UnityEngine;

namespace Deviant.Combat
{
    public partial class CombatPlayer
    {
        CombatInputIngress inputIngress;
        internal long InputInvalidation { get; private set; }
        void OnDestroy()
        {
            ClearCheckpoint();
            inputIngress?.EndCapture();
            ClearBufferedJump(CombatCommandReason.ActorUnavailable);
            actionArbiter.ClearReservations(CombatCommandReason.ActorUnavailable);
            CancelDashCommand(CombatCommandReason.ActorUnavailable);
        }
        internal CombatInputIngress InputIngress => inputIngress != null && inputIngress.Session.ActorId == ActorId ? inputIngress : OpenInputIngress();
        CombatInputIngress OpenInputIngress()
        {
            inputIngress?.EndCapture();
            return inputIngress = new CombatInputIngress(ActorId);
        }
        public CombatInputSession InputSession => InputIngress.Session;
        public long NextInputSequence => InputIngress.NextSequence;
        public CombatInput CurrentInput => InputIngress.Current;
        long InputTick => arena ? arena.SimulationTickCount : 0;
        float InputNow => arena ? arena.Now : 0;

        CombatInputDiscardReason InputRestriction
        {
            get
            {
                if (Dead) return CombatInputDiscardReason.Dead;
                if (InputBlocked) return CombatInputDiscardReason.Blocked;
                if (arena && arena.Controls.Current.SimulationPaused) return CombatInputDiscardReason.Paused;
                return CombatInputDiscardReason.None;
            }
        }

        // The caller first obtains the current session/next sequence. Old actor,
        // generation and sequence requests are rejected before any state changes.
        public CombatInputAdmission SubmitInput(CombatInputSample sample) => arena
            ? arena.Commands.SubmitPlayerInput(sample) : new CombatInputAdmission(sample, CombatInputAdmissionStatus.RejectedSession);
        internal CombatInputAdmission AdmitInput(CombatInputSample sample)
        {
            if (inputIngress == null || sample.Session.ActorId != ActorId || !sample.Session.Equals(inputIngress.Session))
                return new CombatInputAdmission(sample, CombatInputAdmissionStatus.RejectedSession);
            // A dash or jump pressed while a portal is being installed cancels the install; the move itself is then admitted.
            CancelCheckpointInstallByMovement(sample.Input.Pressed);
            var restriction = sample.Source != CombatInputSource.Simulation && Time.timeScale == 0 ? CombatInputDiscardReason.Paused : InputRestriction;
            if (restriction != CombatInputDiscardReason.None && inputIngress.Validate(sample, restriction) == CombatInputAdmissionStatus.Admitted)
                RejectPendingCommands(CombatCommandReasons.For(restriction));
            return inputIngress.Submit(sample, InputTick, InputNow, restriction);
        }

        // Legacy/manual intents are expanded on the same admission path, with
        // explicit simulation time metadata. Same-tick arbitration is unchanged.
        public void SubmitInput(CombatInput input)
        {
            var ingress = InputIngress;
            int edges = CountEdges(input.Pressed) + CountEdges(input.Released);
            int samples = edges == 0 ? 1 : edges;
            if (ingress.NextSequence > long.MaxValue - samples)
                throw new System.InvalidOperationException("Input sequence is exhausted before this intent could be admitted.");
            if (InputRestriction == CombatInputDiscardReason.None && ingress.AvailableCapacity < samples)
                throw new System.InvalidOperationException("Input ingress capacity reached before this intent could be admitted.");
            if (edges == 0) { SubmitSimulationSample(input); return; }
            for (int i = 0; i < CombatInput.ButtonCount; i++)
            {
                var button = StableInputButton(i);
                if ((input.Pressed & button) != 0) SubmitSimulationSample(new CombatInput(input.Axis, button, input.SpecialHeld, climbAxis: input.ClimbAxis, heldSpecials: input.HeldSpecials));
                if ((input.Released & button) != 0) SubmitSimulationSample(new CombatInput(input.Axis, CombatButtons.None, input.SpecialHeld, button, input.ClimbAxis, input.HeldSpecials));
            }
        }
        static int CountEdges(CombatButtons buttons)
        {
            int value = (int)buttons, count = 0;
            while (value != 0) { value &= value - 1; count++; }
            return count;
        }
        internal static CombatButtons StableInputButton(int index)
        {
            switch (index)
            {
                case 0: return CombatButtons.Basic;
                case 1: return CombatButtons.Special;
                case 2: return CombatButtons.Dash;
                case 3: return CombatButtons.Swap;
                case 4: return CombatButtons.Jump;
                case 5: return CombatButtons.Drop;
                default: return (CombatButtons)(1 << index);
            }
        }
        void SubmitSimulationSample(CombatInput input)
            => SubmitInput(new CombatInputSample(InputSession, NextInputSequence, input,
                CombatInputSource.Simulation, 0, CombatInputTimeDomain.Simulation, InputNow));
        void SetContinuousInput(int axis, bool held) => SubmitInput(new CombatInput(axis, specialHeld: held, climbAxis: CurrentInput.ClimbAxis, heldSpecials: CurrentInput.HeldSpecials));

        public void ClearInputState() => DiscardInput(InputRestriction == CombatInputDiscardReason.None ? CombatInputDiscardReason.Cleared : InputRestriction);
        internal void PauseKeyboardInput() => DiscardInput(CombatInputDiscardReason.Paused);
        void RejectPendingCommands(CombatCommandReason reason, bool consuming = false)
        {
            if (inputIngress == null || !arena) return;
            for (int i = 0; i < inputIngress.PendingCount; i++)
            {
                var sample = inputIngress.PendingAt(i);
                var key = CombatCommandKey.For(sample);
                if (!key.IsValid) continue;
                bool evaluated = inputIngress.WasEvaluated(sample.Sequence);
                if (evaluated && inputIngress.Origins.For(sample.Input.Pressed).Equals(key)) continue;
                arena.RecordCommand(key, CombatCommandState.Rejected, consuming && evaluated ? CombatCommandReason.Priority : reason);
            }
        }
        void DiscardInput(CombatInputDiscardReason reason, bool keepContinuous = false)
        {
            // Replacing a per-tick manual sample is not a movement interruption.
            if (reason != CombatInputDiscardReason.Replaced) CancelWallMovement(CombatCommandReasons.For(reason));
            InputInvalidation++;
            RejectPendingCommands(CombatCommandReasons.For(reason));
            inputIngress?.Discard(reason, InputTick, InputNow, keepContinuous);
        }
        void ClearInputs()
        {
            if (Dead) DiscardInput(CombatInputDiscardReason.Dead, true);
            else
            {
                RejectPendingCommands(CombatCommandReason.Cleared, consuming: true);
                InputIngress.Consume(InputTick, InputNow);
            }
        }
        internal void ResetInputSession(bool newEpoch)
        {
            RejectPendingCommands(CombatCommandReason.Reset);
            actionArbiter.ClearReservations(CombatCommandReason.Reset);
            ClearBufferedJump(CombatCommandReason.Reset);
            resettingCommands = true;
            if (currentCast) currentCast.CommandCancellationReason = CombatCommandReason.Reset;
            InputIngress.Reset(newEpoch, InputTick, InputNow);
        }
        internal void ReplaceInput(CombatInput input)
        { DiscardInput(CombatInputDiscardReason.Replaced); SubmitInput(input); }
        public ICombatInputCapture BeginInputCapture(int batchCapacity = 256) => InputIngress.BeginCapture(batchCapacity);
    }
}
