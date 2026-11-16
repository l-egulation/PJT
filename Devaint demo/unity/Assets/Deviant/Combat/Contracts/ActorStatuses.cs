namespace Deviant.Combat.Contracts
{
    public readonly struct ActorStatusSnapshot
    {
        public float StunUntil { get; }
        public float RootUntil { get; }
        public float StaggerUntil { get; }
        public float InputLockUntil { get; }
        public float KnockUntil { get; }
        public float KnockSpeed { get; }
        public float StealthUntil { get; }
        public float SmokeUntil { get; }
        public ActorStatusSnapshot(float stun, float root, float stagger, float inputLock, float knock, float knockSpeed, float stealth, float smoke)
        { StunUntil = stun; RootUntil = root; StaggerUntil = stagger; InputLockUntil = inputLock; KnockUntil = knock; KnockSpeed = knockSpeed; StealthUntil = stealth; SmokeUntil = smoke; }
        public bool HiddenAt(float now) => now < StealthUntil || now < SmokeUntil;
        public bool EnemyControlledAt(float now) => now < StunUntil || now < StaggerUntil;
    }
    public interface IActorStatuses
    {
        ActorStatusSnapshot Read();
        void WriteStunUntil(float value);
        void WriteRootUntil(float value);
        void WriteStaggerUntil(float value);
        void WriteInputLockUntil(float value);
        void WriteKnockUntil(float value);
        void WriteKnockSpeed(float value);
        void WriteStealthUntil(float value);
        void WriteSmokeUntil(float value);
        void PlayerHit(float now, int direction, bool forcedMovement);
        void BasicStagger(float now, float duration);
        void RefreshRoot(float now, float duration);
        void RefreshStun(float now, float duration);
        void Push(float now, int direction, float distance, float seconds);
        // Same distance and deadline as Push, travelled on a cubic ease-out (fast snap, soft stop).
        void PushEased(float now, int direction, float distance, float seconds);
        // Horizontal knock speed for the step starting at now; equals KnockSpeed unless an eased push is active.
        float KnockSpeedAt(float now, float step);
        void SetKnockMotion(float until, float speed);
        void ClearHitMotionLocks();
        void ClearConcealment();
        void Reset();
    }
}
