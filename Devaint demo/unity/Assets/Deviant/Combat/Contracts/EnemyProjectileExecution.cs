namespace Deviant.Combat.Contracts
{
    public readonly struct EnemyProjectileSnapshot
    {
        public float Expires { get; }
        public float Damage { get; }
        public bool Reflected { get; }
        public bool Removed { get; }
        public EnemyProjectileSnapshot(float expires, float damage, bool reflected, bool removed)
        { Expires = expires; Damage = damage; Reflected = reflected; Removed = removed; }
    }

    // The native adapter supplies the existing clock/source and synchronous
    // sweep. No velocity, geometry or second timer is retained by the execution.
    public interface IEnemyProjectileExecutionHost
    {
        float Now { get; }
        bool SourceExists { get; }
        bool SourceDead { get; }
        bool SourceIsPlayer { get; }
        void Sweep(float duration);
        void Remove();
    }

    // One stack contact keeps the target and native sweep origin together.
    // SpecialBonus is read only after the source and velocity have flipped.
    public interface IEnemyProjectileContactHost
    {
        bool TryParry();
        void ReflectSourceAndVelocity();
        float SpecialBonus { get; }
        void RestoreOrigin();
        void Remove();
        void Receive(float damage);
    }
}
