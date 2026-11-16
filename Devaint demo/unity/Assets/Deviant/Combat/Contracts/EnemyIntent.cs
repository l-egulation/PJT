using System;

namespace Deviant.Combat.Contracts
{
    // One synchronous observation of the existing sandbox enemy. Profile is the
    // current compatibility index, not a new monster catalog or persistent ID.
    public readonly struct EnemyObservation
    {
        public int Profile { get; }
        public bool Aggressive { get; }
        public bool HasTarget { get; }
        public bool Controlled { get; }
        public bool Attacking { get; }
        public float Now { get; }
        public float NextAttack { get; }
        public float DeltaX { get; }
        public float DeltaY { get; }
        public EnemyObservation(int profile, bool aggressive, bool hasTarget, bool controlled, bool attacking,
            float now, float nextAttack, float deltaX, float deltaY)
        {
            if (!Finite(now) || now < 0 || !Finite(nextAttack) || !Finite(deltaX) || !Finite(deltaY))
                throw new ArgumentOutOfRangeException(nameof(now), "Enemy observations require finite coordinates/times and a nonnegative current time.");
            Profile = profile; Aggressive = aggressive; HasTarget = hasTarget; Controlled = controlled; Attacking = attacking;
            Now = now; NextAttack = nextAttack; DeltaX = deltaX; DeltaY = deltaY;
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
    public enum EnemyIntentKind { None, BeginAttack, Chase }
    public readonly struct EnemyIntent
    {
        public EnemyIntentKind Kind { get; }
        public int Facing { get; }
        public float Speed { get; }
        public EnemyIntent(EnemyIntentKind kind, int facing, float speed)
        {
            if (!Enum.IsDefined(typeof(EnemyIntentKind), kind) || float.IsNaN(speed) || float.IsInfinity(speed) ||
                (kind == EnemyIntentKind.None ? facing != 0 || speed != 0 : facing != -1 && facing != 1) ||
                (kind == EnemyIntentKind.Chase ? speed <= 0 : speed != 0)) throw new ArgumentException("An enemy intent must have a valid direction and speed for its kind.");
            Kind = kind; Facing = facing; Speed = speed;
        }
    }
}
