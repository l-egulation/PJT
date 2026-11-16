using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Definitions;

namespace Deviant.Combat.Execution
{
    public enum MeleeActionState { Preparing, Active, Completed, Cancelled, Failed }

    // Initial single-hit action runner. It reads the simulation clock; it never
    // accumulates a second clock or performs geometry, damage or file operations.
    public sealed class MeleeActionRunner
    {
        readonly IMeleeActionHost host;
        readonly HashSet<int> targets = new HashSet<int>();
        float preparedNow, preparedStep, lastNow = -1, lastEvaluated = -.000001f, lastAdvance;
        bool prepared, busy, sweeping;
        public Guid ExecutionId { get; }
        public MeleeActionDefinition Definition { get; }
        public float StartedAt { get; }
        public float EndTime => StartedAt + Definition.End;
        public MeleeActionState State { get; private set; }
        public string Error { get; private set; }
        public bool IsTerminal => State == MeleeActionState.Completed || State == MeleeActionState.Cancelled || State == MeleeActionState.Failed;
        public MeleeActionRunner(Guid executionId, MeleeActionDefinition definition, float startedAt, IMeleeActionHost host)
        {
            if (executionId == Guid.Empty) throw new ArgumentException("An execution ID is required.", nameof(executionId));
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            if (!Finite(startedAt) || startedAt < 0 || !Finite(startedAt + definition.End)) throw new ArgumentOutOfRangeException(nameof(startedAt));
            ExecutionId = executionId; StartedAt = startedAt;
        }
        public float Age(float now)
        {
            if (!Finite(now) || now < 0) throw new ArgumentOutOfRangeException(nameof(now));
            return Math.Max(0, now - StartedAt);
        }
        public void Prepare(float now, float step)
        {
            Guard(); if (IsTerminal) return;
            Validate(now, step);
            if (prepared) throw new InvalidOperationException("The prepared action tick has not been resolved.");
            prepared = true; preparedNow = lastNow = now; preparedStep = step;
            busy = true;
            try
            {
                if (State == MeleeActionState.Preparing && Elapsed(now) + step >= Definition.Impact)
                {
                    State = MeleeActionState.Active;
                    if (!host.TryActivate() && !IsTerminal) { State = MeleeActionState.Failed; Error = "Action activation was rejected."; prepared = false; }
                }
            }
            catch (Exception exception) { Fail(exception); throw; }
            finally { busy = false; }
        }
        public float HorizontalVelocity(float now, float step, int axis, bool grounded, float airSpeed)
        {
            Guard();
            if (axis < -1 || axis > 1 || !Finite(airSpeed) || airSpeed < 0) throw new ArgumentOutOfRangeException(nameof(axis));
            if (IsTerminal) return axis * airSpeed;
            CheckPrepared(now, step);
            // Consume even neutral/airborne intervals: they cannot be donated to
            // a later landing/input sample. This preserves the first partial tick.
            float from = lastAdvance, to = Math.Max(0, Elapsed(now) + step); lastAdvance = to;
            if (!grounded) return axis * airSpeed;
            if (axis == 0 || Definition.AdvanceDistance == 0) return 0;
            float active = Math.Max(0, Math.Min(to, Definition.AdvanceStart + Definition.AdvanceDuration) - Math.Max(from, Definition.AdvanceStart));
            return axis * Definition.AdvanceDistance / Definition.AdvanceDuration * active / step;
        }
        public void Step(float now, float step)
        {
            Guard(); if (IsTerminal) return;
            CheckPrepared(now, step); float to = Elapsed(now) + step;
            busy = true;
            try
            {
                // Preserve the legacy inclusive observation at hit start and the
                // half-open active end, including time zero and a sub-tick hit.
                if (State == MeleeActionState.Active && to >= Definition.Impact && lastEvaluated < Definition.Impact + Definition.Active)
                { sweeping = true; try { host.Sweep(); } finally { sweeping = false; } }
                lastEvaluated = to; prepared = false;
                // The legacy completion check compares the addition directly.
                // Reusing the rounded sweep sample can end a .36s action one
                // observation tick early on the existing Unity float path.
                if (!IsTerminal && Elapsed(now) + step >= Definition.End)
                { State = MeleeActionState.Completed; host.Complete(); }
            }
            catch (Exception exception) { Fail(exception); throw; }
            finally { busy = false; }
        }
        public bool TryClaimTarget(int targetId)
        {
            if (IsTerminal) return false;
            if (!sweeping) throw new InvalidOperationException("A target can be claimed only during this execution's hit sweep.");
            return targets.Add(targetId);
        }
        public bool HasClaimedTarget(int targetId) => targets.Contains(targetId);
        public bool Cancel()
        {
            if (IsTerminal) return false;
            State = MeleeActionState.Cancelled; prepared = false; return true;
        }
        void CheckPrepared(float now, float step)
        {
            Validate(now, step);
            if (!prepared || now != preparedNow || step != preparedStep) throw new InvalidOperationException("The action must resolve its prepared simulation tick.");
        }
        float Elapsed(float now) => now - StartedAt;
        void Validate(float now, float step)
        {
            if (!Finite(now) || now < 0 || !Finite(step) || step <= 0 || !Finite(Elapsed(now) + step) || now < lastNow)
                throw new ArgumentOutOfRangeException(nameof(now), "Action ticks require finite positive steps and a monotonic simulation clock.");
        }
        void Guard() { if (busy) throw new InvalidOperationException("Action advancement cannot reenter a host callback."); }
        void Fail(Exception exception) { State = MeleeActionState.Failed; Error = exception.Message; prepared = false; }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
