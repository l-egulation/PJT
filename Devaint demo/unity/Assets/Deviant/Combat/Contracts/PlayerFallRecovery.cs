namespace Deviant.Combat.Contracts
{
    public interface IPlayerFallRecoveryHost
    {
        void CancelAction();
        void ClearBufferedJump();
        float Health { get; set; }
        float MaxHealth { get; }
        bool Dead { get; }
        void Die();
        float SpawnX { get; }
        float SpawnY { get; }
        float Now { get; }
        float LastFallTime { get; set; }
        float LastRestoreTime { get; set; }
        int Facing { get; }
        bool TrySafeBefore(float cutoff, out SafeGroundSample sample);
        void RecordSample(bool found, SafeGroundSample sample);
        void Place(float x, float y);
        void SyncFacing(int facing);
        void ClearVelocity();
        void ResetJumps();
        void SetProtectionUntil(float deadline);
        void SetLastRestorePosition(float x, float y);
        void ClearHadGround();
        void ClearHitMotionLocks();
        void RecordRestored(float x, float y);
    }
}
