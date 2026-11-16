using System;

namespace Deviant.Combat.Execution
{
    // Only CombatSimulation advances this clock. Float accumulation deliberately
    // matches the legacy arena, including its time-interval boundary behavior.
    public sealed class SimulationClock
    {
        public float Now { get; private set; }
        public long TickCount { get; private set; }
        internal void ValidateStep(float deltaTime)
        {
            float next = Now + deltaTime;
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0 ||
                float.IsInfinity(next) || next <= Now || TickCount == long.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "Step must advance finite simulation time.");
        }
        internal void Advance(float deltaTime) { Now += deltaTime; TickCount++; }
        internal void Reset() { Now = 0; TickCount = 0; }
    }
}
