namespace Deviant.Combat.Contracts
{
    // Read-only observation of one bomb. The flight deadline and landing fuse
    // read live data, while Age and LandedAt have exactly one execution owner.
    public readonly struct TimedBombSnapshot
    {
        public float Age { get; }
        public float LandedAt { get; }
        public bool Stopped { get; }
        public TimedBombSnapshot(float age, float landedAt, bool stopped)
        { Age = age; LandedAt = landedAt; Stopped = stopped; }
        public bool IsLanded(float fuseSeconds) => fuseSeconds > 0 && LandedAt >= 0;
        public float FuseRemaining(float fuseSeconds)
        {
            if (!IsLanded(fuseSeconds)) return 0;
            float remaining = LandedAt + fuseSeconds - Age;
            return 0 > remaining ? 0 : remaining;
        }
        public float Deadline(float flightLifetime, float fuseSeconds)
            => IsLanded(fuseSeconds) ? LandedAt + fuseSeconds : flightLifetime;
    }
    // Native flight integration and sweeps stay synchronous. AdvanceFlight may
    // report landing on the same execution; Detonate observes a claimed Stop.
    public interface ITimedBombHost
    {
        float FlightLifetime { get; }
        float FuseSeconds { get; }
        void AdvanceFlight(float duration);
        void Detonate();
    }
}
