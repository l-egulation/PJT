namespace Deviant.Combat.Contracts
{
    // One entered launch retains its native origin/muzzle, query array and shot.
    // Actor geometry and the fallback weapon are read only at their old points.
    public interface IRangedLaunchHost
    {
        void CaptureOrigin();
        float SpawnX { get; }
        float OwnerOffsetX { get; }
        float OwnerWidth { get; }
        float ProjectileRadius { get; }
        float SpawnGap { get; }
        void CaptureMuzzle(float offsetX);
        string ProjectileType { get; }
        bool IgnoresTerrain { get; }
        void FireLaser();
        void CaptureMuzzleDelta();
        int QueryMuzzlePath();
        void SelectMuzzleHit(int index);
        bool MuzzleSurfaceExists { get; }
        bool MuzzleSurfaceIsOneWay { get; }
        float MuzzleHitDistance { get; }
        float BlastRadius { get; }
        void SpawnBlocked(float distance);
        void DetonateShot();
        void RecordBlocked();
        void SpawnAtMuzzle();
        bool HasSourceWeapon { get; }
        bool CapturePlayerOwner();
        void AssignSourceWeapon(bool useCurrentPlayerWeapon);
        void SweepShotToMuzzle();
        int AngleCount { get; }
        float AngleAt(int index);
        void SpawnAtOrigin(float angle);
        void RecordFired(int count);
    }

    // Native holds the actual returned bomb. Its initial sweep still reads the
    // current owner's width after spawning and assigning the fragment multiplier.
    public interface IBombLaunchHost
    {
        void CaptureRadians();
        void CaptureOrigin();
        void Spawn();
        void SetFragmentMultiplier(float multiplier);
        float OwnerWidth { get; }
        float ProjectileRadius { get; }
        float SpawnGap { get; }
        void SweepToMuzzle(float distance);
    }
}
