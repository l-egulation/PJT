namespace Deviant.Combat.Contracts
{
    public readonly struct ApproachRecastSnapshot
    {
        public bool Consumed { get; }
        public bool Mode { get; }
        public float MarkedUntil { get; }
        public float Budget { get; }
        public float Duration { get; }
        public ApproachRecastSnapshot(bool consumed, bool mode, float markedUntil, float budget, float duration)
        {
            Consumed = consumed; Mode = mode; MarkedUntil = markedUntil; Budget = budget; Duration = duration;
        }
    }

    // The cast retains cancellation/completion, the current clock, weapon/target
    // identity and Native geometry. Captured geometry belongs to this invocation,
    // not to another target registry or persistent execution snapshot.
    public interface IApproachRecastHost
    {
        bool SameWeapon { get; }
        bool Cancelled { get; }
        bool MotionDone { get; }
        bool TargetHit { get; }
        bool MarkedTargetAlive { get; }
        bool IsRecastMovement { get; }
        float Now { get; }
        float Elapsed { get; }
        float RecastWindow { get; }
        float Distance { get; }
        float Speed { get; }
        float DistanceToDestination { get; }
        float MovedDistance { get; }
        float RequestedDistance { get; }
        void CaptureDestination();
        void CaptureOrigin();
        void StartApproach();
        void ResumeMotion();
        void MoveTowardDestination(float distance);
        void SetMotionEnd(float elapsed);
        void ClearVerticalSpeed();
        void RecordApproach();
    }
}
