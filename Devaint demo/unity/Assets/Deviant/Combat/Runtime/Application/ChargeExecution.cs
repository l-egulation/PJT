using System;
using Deviant.Definitions;

namespace Deviant.Combat.Execution
{
    // The caller supplies the existing simulation clock and sampled hold input.
    // It owns no actor, resource, physics or presentation callback.
    public sealed class ChargeExecution
    {
        float lastNow = -1;
        public Guid ExecutionId { get; }
        public float StartedAt { get; }
        public ChargeDefinition Definition { get; }
        public ChargeReleaseTiming? Release { get; private set; }
        public bool IsActive { get; private set; } = true;
        public ChargeExecution(Guid executionId, ChargeDefinition definition, float startedAt)
        {
            if (executionId == Guid.Empty) throw new ArgumentException("A charge execution identity is required.", nameof(executionId));
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (!Finite(startedAt) || startedAt < 0 || !Finite(startedAt + definition.BaseEnd + (definition.Maximum - definition.Minimum))) throw new ArgumentOutOfRangeException(nameof(startedAt));
            ExecutionId = executionId; StartedAt = startedAt;
        }
        float Elapsed(float now) => now - StartedAt;
        public bool TryRelease(float now, float step, bool held)
        {
            if (!IsActive) return false;
            if (!Finite(now) || now < 0 || now < lastNow || !Finite(step) || step <= 0 || !Finite(Elapsed(now) + step)) throw new ArgumentOutOfRangeException(nameof(now));
            lastNow = now;
            if (Release.HasValue) return true;
            if (!Definition.HoldUntilRelease && Elapsed(now) >= Definition.Maximum || !held && Elapsed(now) + step >= Definition.Minimum)
            { Release = Definition.Resolve(Elapsed(now)); return true; }
            return false;
        }
        public void Cancel() { IsActive = false; }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
