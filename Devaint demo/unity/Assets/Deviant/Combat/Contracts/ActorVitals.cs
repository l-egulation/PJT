namespace Deviant.Combat.Contracts
{
    // Runtime actor state, not a second world/save record or item identity.
    public readonly struct ActorVitalsSnapshot
    {
        public float Maximum { get; }
        public float Health { get; }
        public float Shield { get; }
        public float GuardUntil { get; }
        public float GuardReduction { get; }
        public int ReceivedHits { get; }
        public bool Dead => Health <= 0;
        public ActorVitalsSnapshot(float maximum, float health, float shield, float guardUntil, float guardReduction, int receivedHits)
        { Maximum = maximum; Health = health; Shield = shield; GuardUntil = guardUntil; GuardReduction = guardReduction; ReceivedHits = receivedHits; }
    }
    public readonly struct ActorDamageResult
    {
        public bool Accepted { get; }
        // Damage after reduction/absorption, including overkill. This is not
        // necessarily the health delta, which is clamped at zero.
        public float Damage { get; }
        public float Absorbed { get; }
        public ActorVitalsSnapshot State { get; }
        public ActorDamageResult(bool accepted, float damage, float absorbed, ActorVitalsSnapshot state)
        { Accepted = accepted; Damage = damage; Absorbed = absorbed; State = state; }
    }
    public interface IActorVitals
    {
        ActorVitalsSnapshot Read();
        // Low-level compatibility writes for the existing composed test profile.
        // These do not decide healing, equipment, respawn or save policies.
        void WriteMaximum(float value);
        void WriteHealth(float value);
        void WriteShield(float value);
        void WriteGuardUntil(float value);
        void WriteGuardReduction(float value);
        ActorDamageResult ApplyDamage(float damage, float now, bool bypass);
        void ExpireShield(float now);
        void Reset();
    }
}
