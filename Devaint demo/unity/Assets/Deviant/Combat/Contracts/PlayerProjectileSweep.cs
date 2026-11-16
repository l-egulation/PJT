namespace Deviant.Combat.Contracts
{
    // Native keeps the collider/region/actor references and resolves each fact
    // only when the selected eligibility or priority branch requests it.
    public interface IPlayerProjectileContactHost
    {
        bool IgnoresTerrain { get; }
        bool FindSurface();
        bool SurfaceIsOneWay { get; }
        bool FindRegion();
        bool TargetExists { get; }
        bool TargetIsOwner { get; }
        bool TargetIsPlayer { get; }
        bool TargetIsDecoy { get; }
        bool TargetDead { get; }
        int TargetId { get; }
    }

    // List.Sort retains its native comparison order. Actors are captured only
    // for two eligible enemies, then each distance reads the live shot position.
    public interface IPlayerProjectileOverlapHost
    {
        string ProjectileType { get; }
        bool FirstValid { get; }
        bool FirstIsSurface { get; }
        bool SecondValid { get; }
        bool SecondIsSurface { get; }
        void CaptureActors();
        float FirstSquaredDistance { get; }
        float SecondSquaredDistance { get; }
        int FirstPriority { get; }
        int SecondPriority { get; }
    }

    public interface IPlayerProjectileCastOrderHost
    {
        float FirstDistance { get; }
        float SecondDistance { get; }
        int FirstPriority { get; }
        int SecondPriority { get; }
    }

    // One entered sweep owns its native lists, destination, delta and start.
    // Removed belongs to the existing execution and is checked only at entry.
    public interface IPlayerProjectileSweepHost
    {
        bool Removed { get; }
        int QueryOverlaps();
        void SortOverlaps();
        bool ResolveOverlap(int index);
        void SnapshotDelta();
        float DeltaLength { get; }
        void CaptureStart();
        int QueryCast(float length);
        void SortCasts();
        bool CastValid(int index);
        float CastDistance { get; }
        void ApplyContact(float length, float distance);
        float ContactAge(float rawDistance);
        bool ResolveCast(float contactAge);
        void ApplyDestination();
    }
}
