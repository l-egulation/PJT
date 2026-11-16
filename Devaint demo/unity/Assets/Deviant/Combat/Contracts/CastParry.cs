namespace Deviant.Combat.Contracts
{
    // Each attempt reads the live cast/Native source. No window ledger, copied
    // lifecycle state or second clock is retained across incoming attacks.
    public interface ICastParryHost
    {
        bool Activated { get; }
        bool Cancelled { get; }
        bool EffectExists { get; }
        string EffectType { get; }
        float ParryWindow { get; }
        float ParryDamage { get; }
        float Elapsed { get; }
        bool SourceExists { get; }
        float AttackOriginX { get; }
        float OwnerCenterX { get; }
        int Direction { get; }
        float DamageMultiplier { get; }
        bool Receive(float damage);
        void Record(bool projectileAttack);
    }
    // Optional single-use admission. Claim before counter damage callbacks so a
    // synchronous second attack cannot consume the same shield window again.
    public interface ICastParryClaimHost { bool TryClaimParry(); }
}
