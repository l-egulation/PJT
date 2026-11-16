namespace Deviant.Combat.Contracts
{
    // Live facts and native operations only. The adapter captures the target
    // reference; attack/status/motor state and the simulation clock stay owned.
    public interface IEnemyTickHost
    {
        bool Dead { get; }
        void CaptureTarget();
        float Now { get; }
        float StunUntil { get; }
        float StaggerUntil { get; }
        EnemyAttackSnapshot ReadAttack();
        int Profile { get; }
        bool Aggressive { get; }
        bool HasTarget { get; }
        float TargetDeltaX { get; }
        float TargetDeltaY { get; }
        float ApplyIntent(EnemyIntent intent);
        void PhysicsStep(float step, float horizontal, bool freezeVertical);
        void StepAttack(float step, bool controlled);
    }

    // The native cursor owns its original box center and current actor reference.
    // Eligibility is live and a successful claim precedes damage, even on error.
    public interface IEnemyMeleeHitHost
    {
        void BeginTargets();
        bool NextTarget();
        bool TargetIsNotPlayer { get; }
        bool TargetIsDecoy { get; }
        bool TargetDead { get; }
        bool TargetOccluded { get; }
        bool FirstHit(int hitIndex);
        int Profile { get; }
        void Receive(float damage);
        void EndTargets();
        bool ShowBoxes { get; }
        void AddFlash();
    }
}
