namespace Deviant.Combat.Contracts
{
    public readonly struct SmokeEffectSnapshot
    {
        public float End { get; }
        public bool Spent { get; }
        public SmokeEffectSnapshot(float end, bool spent) { End = end; Spent = spent; }
    }
}
