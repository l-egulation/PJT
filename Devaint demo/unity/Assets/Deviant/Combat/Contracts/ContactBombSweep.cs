namespace Deviant.Combat.Contracts
{
    // Native keeps the current collider/actor references. Only the selected
    // qualification branch reads its bounds, position or actor facts.
    public interface IContactBombObstacleHost
    {
        bool FindSurface();
        bool SurfaceIsOneWay { get; }
        float MovementY { get; }
        float NormalY { get; }
        float BombBottom { get; }
        float SurfaceTop { get; }
        bool FindRegion();
        bool TargetExists { get; }
        bool TargetIsOwner { get; }
        bool TargetIsPlayer { get; }
        bool TargetIsDecoy { get; }
        bool TargetDead { get; }
    }

    // A host belongs to one entered sweep. Native owns the destination/delta,
    // ordered query arrays and Vector2 operations; Stopped is the existing
    // TimedBombExecution claim, not a second contact-sweep lifetime.
    public interface IContactBombSweepHost
    {
        bool Stopped { get; }
        float FuseSeconds { get; }
        void SweepTimed();
        void SnapshotDelta();
        int QueryOverlaps();
        bool OverlapIsObstacle(int index);
        float DeltaLength { get; }
        int QueryCast(float length);
        bool CastIsObstacle(int index);
        float CastDistance { get; }
        void ApplyTravel(float length, float distance);
        void Explode();
    }
}
