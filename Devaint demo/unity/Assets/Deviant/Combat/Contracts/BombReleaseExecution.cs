namespace Deviant.Combat.Contracts
{
    public readonly struct BombReleaseSnapshot
    {
        public int NextBomb { get; }
        public BombReleaseSnapshot(int nextBomb) { NextBomb = nextBomb; }
    }

    // Admission is checked once per release pass. Elapsed and the existing
    // charge policy's shift remain live at each authored hit, including reentry.
    public interface IBombReleaseHost
    {
        bool Cancelled { get; }
        bool OwnerExists { get; }
        bool OwnerDead { get; }
        float Elapsed { get; }
        float ChargeShift { get; }
        // The host reads current direction and activation damage when invoked.
        void SpawnBomb(int hitIndex, float damage);
    }
}
