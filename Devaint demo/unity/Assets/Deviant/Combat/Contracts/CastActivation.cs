namespace Deviant.Combat.Contracts
{
    // Per-call Native facts and effects, not an activation snapshot. The existing
    // lifecycle owns the activation claim; resources, poses and targets stay live.
    public interface ICastActivationHost
    {
        bool ProbeGround();
        void SelectTeleportTargetInCircle(float radius);
        void SelectTeleportTargetInBox();
        void SelectFirstHitTarget();
        bool MarkedTargetExists { get; }
        void SaveOwnerPose();
        void CaptureInstallCenter(float x, float y);
        float? FindInstallFloor(float distance);
        void SetInstallHeight(float height);
        bool InstallationOverlapsSolid(float width, float height, float inset);
        bool InstallationOccluded { get; }
        bool CommitResources();
        void Fail();
        void ClaimActivation();
        void LaunchMissile();
        void ReleaseDueBombs();
        void FireRanged();
        bool OwnerGrounded { get; }
        float BeginRangedRecoil(float duration, float hopHeight);
        void SetOwnerVerticalVelocity(float velocity);
        void ActivateSkillWave();
        float Now { get; }
        void SetStealthUntil(float until);
        void SetShield(float hp);
        float GuardUntil { get; }
        void SetGuardUntil(float until);
        void SetGuardReduction(float reduction);
        void ReplaceSmoke();
        float Activation { get; }
        void ActivateSmoke(float activation);
        void SpawnDecoy(float centerToFeet);
        void RecordActivation();
    }
}
