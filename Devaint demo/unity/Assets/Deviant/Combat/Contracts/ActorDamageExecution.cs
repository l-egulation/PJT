namespace Deviant.Combat.Contracts
{
    public interface IActorDeathInterceptor { bool TryPreventDeath(); }
    // Optional per-actor encounter policy. Ordinary combat hosts keep their existing behavior.
    public interface IActorDamagePolicy
    {
        bool AcceptsIncomingDamage { get; }
        bool AcceptsIncomingControl { get; }
        float MinimumHealthAfterDamage { get; }
    }
    // Optional per-actor gate for positive stun/root durations (the boss internal cooldowns).
    // A refused control is a failed application: no refresh and no stun cancellation.
    public interface IActorControlGate
    {
        bool TryAcceptStun(float now);
        bool TryAcceptRoot(float now);
    }
    // Facts are read at their phase, not captured as a second actor state.
    public interface IActorDamageHost
    {
        IActorVitals Vitals { get; }
        IActorStatuses Statuses { get; }
        float Now { get; }
        bool IsPlayer { get; }
        bool IsBoss { get; }
        bool IsDecoy { get; }
        bool SourceIsPlayer { get; }
        float BasicStaggerSeconds { get; }
        float BasicPushDistance { get; }
        float BasicPushSeconds { get; }
        // Action-specific push from the player's hit (0 = none). It replaces the basic push.
        float ActionPushDistance { get; }
        float ActionPushSeconds { get; }
        // Caps only automatic hit reaction; explicit skill displacement bypasses this limit.
        float LimitAutomaticPush(float distance);
        bool PlayerInvulnerable { get; }
        bool IsForcedMoving { get; }
        bool TryParryMelee();
        void AcceptedHit(float requested, ActorVitalsSnapshot before, ActorDamageResult applied, float now);
        void CancelAttack();
        // Record the current health after hit/control callbacks, not the
        // ApplyDamage result snapshot. Native text/name formatting stays here.
        void RecordDamage(float damage);
        void DiePlayer();
        void ApplyDeathVisual();
    }
}
