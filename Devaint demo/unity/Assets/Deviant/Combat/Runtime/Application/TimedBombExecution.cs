using Deviant.Combat.Contracts;

namespace Deviant.Combat.Execution
{
    // Embedded as a private field: never copy this mutable owner or box it.
    // Only Read returns a value. Native callbacks mutate that same field.
    public struct TimedBombExecution
    {
        float age, landedAt;
        bool landed, stopped;
        public bool Stopped => stopped;
        public TimedBombSnapshot Read() => new TimedBombSnapshot(age, landed ? landedAt : -1, stopped);
        public bool IsLanded(float fuseSeconds) => fuseSeconds > 0 && landed && landedAt >= 0;
        public float FuseRemaining(float fuseSeconds)
        {
            if (!IsLanded(fuseSeconds)) return 0;
            float remaining = landedAt + fuseSeconds - age;
            return 0 > remaining ? 0 : remaining;
        }
        public void Land(float duration, float contactDistance, float sweepLength)
        {
            if (stopped) return;
            // Keep the division, multiplication, then addition in the original
            // float order. The adapter only reports nonzero downward sweeps.
            float fraction = contactDistance / sweepLength;
            if (fraction < 0) fraction = 0;
            else if (fraction > 1) fraction = 1;
            landedAt = age + duration * fraction;
            landed = true;
        }
        // Also claims ordinary contact-bomb termination. No second native flag.
        public bool Stop()
        {
            if (stopped) return false;
            stopped = true; return true;
        }
        float Deadline(ITimedBombHost host)
            => IsLanded(host.FuseSeconds) ? landedAt + host.FuseSeconds : host.FlightLifetime;
        public void Step(float dt, ITimedBombHost host)
        {
            float remaining = dt;
            while (!stopped && remaining > .000001f)
            {
                // Landing supersedes flight timeout, even within its last step.
                float deadline = Deadline(host);
                if (age >= deadline - .000001f) { Detonate(host); return; }
                float step = remaining;
                float untilDeadline = deadline - age;
                if (untilDeadline < step) step = untilDeadline;
                if (.02f < step) step = .02f;
                if (!IsLanded(host.FuseSeconds)) host.AdvanceFlight(step);
                age += step; remaining -= step;
                deadline = Deadline(host);
                if (age >= deadline - .000001f) { Detonate(host); return; }
            }
        }
        void Detonate(ITimedBombHost host)
        { if (Stop()) host.Detonate(); }
    }
}
