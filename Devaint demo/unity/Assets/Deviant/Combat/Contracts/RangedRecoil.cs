namespace Deviant.Combat.Contracts
{
    public readonly struct RangedRecoilSnapshot
    {
        public float StartedAt { get; }
        public float Duration { get; }
        public int Direction { get; }
        public bool Stopped { get; }
        public RangedRecoilSnapshot(float startedAt, float duration, int direction, bool stopped)
        { StartedAt = startedAt; Duration = duration; Direction = direction; Stopped = stopped; }
    }
}
