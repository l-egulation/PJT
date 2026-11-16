namespace Deviant.Combat.Contracts
{
    // A value view of one temporary enemy's existing attack state. Deadlines
    // use the caller's simulation clock; reset deliberately retains history.
    public readonly struct EnemyAttackSnapshot
    {
        public bool Attacking { get; }
        public float StartedAt { get; }
        public float NextAttackAt { get; }
        public int Sequence { get; }
        public int ProjectileHitIndex { get; }
        public EnemyAttackSnapshot(bool attacking, float startedAt, float nextAttackAt, int sequence, int projectileHitIndex)
        { Attacking = attacking; StartedAt = startedAt; NextAttackAt = nextAttackAt; Sequence = sequence; ProjectileHitIndex = projectileHitIndex; }
        public float Elapsed(float now)
        {
            float elapsed = now - StartedAt;
            return 0 > elapsed ? 0 : elapsed;
        }
        public float Telegraph(int profile, float now)
        {
            if (!Attacking) return -1;
            float fraction = (now - StartedAt) / (profile == 2 ? .45f : .35f);
            if (fraction < 0) return 0;
            if (fraction > 1) return 1;
            return fraction;
        }
    }
    // Synchronous native effects only. Profile and Now remain live reads, as in
    // the original adapter; callbacks may cancel/reset or start another attack.
    public interface IEnemyAttackHost
    {
        int Profile { get; }
        float Now { get; }
        void MeleeHit(int hitIndex);
        void FireProjectile();
    }
}
