namespace Deviant.Combat.Contracts
{
    // Actor references, hit definitions and the existing hit-claim owners stay
    // Native. Facts following Receive must be read from those same live owners.
    public interface ICastHitResponseHost
    {
        bool TryClaimTarget();
        float HitDamage { get; }
        float DamageMultiplier { get; }
        bool ReceiveControl(float damage);
        bool TrackPrimaryTarget { get; }
        void MarkTarget();
        void MarkApproachAccepted();
        bool HasResponseMovement { get; }
        bool PrimaryPush { get; }
        bool MarkedTargetExists { get; }
        bool TargetIsBoss { get; }
        bool TargetIsMarked { get; }
        int TargetActorId { get; }
        bool PushWithCaster { get; }
        float EndForcedDashAtBoss();
        void SetMotionEnd(float end);
        bool Cancelled { get; }
        bool MovementExists { get; }
        bool MarkedTargetAlive { get; }
        float EndDamage { get; }
        void ReceiveMarked(float damage);
    }

    // A live Native list enumerator, not an enemy snapshot. Begin/End preserve
    // foreach lifetime and invalidation behavior without boxing the enumerator.
    public interface ICastPushHost
    {
        bool Moving { get; }
        bool Cancelled { get; }
        void BeginEnemies();
        bool NextEnemy();
        void EndEnemies();
        bool EnemyExists { get; }
        bool EnemyDead { get; }
        int EnemyActorId { get; }
        float Now { get; }
        float MovementSpeed { get; }
        int Direction { get; }
        void SetEnemyKnockMotion(float until, float speed);
    }
}
