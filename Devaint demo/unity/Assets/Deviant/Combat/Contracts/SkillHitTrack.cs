namespace Deviant.Combat.Contracts
{
    public enum SkillHitBoxAnchor
    {
        Actor,
        SavedPrimaryWorldPose,
        SavedSpawnWorldPose
    }

    // Every query is live. The cast remains the sole owner of lifecycle,
    // charge, target claims and Native geometry/damage callbacks.
    public interface ISkillHitTrackHost
    {
        bool Activated { get; }
        bool Cancelled { get; }
        bool MotionDone { get; }
        float Elapsed { get; }
        float ChargeShift { get; }
        bool DirectHitAllowed { get; }
        bool TargetHit { get; }
        bool TeleportClaimed { get; }
        bool MarkedTargetAlive { get; }
        void ApplyMarkedTarget(int index);
        void ApplyTeleportTarget(int index);
        void ApplyBox(int index, SkillHitBoxAnchor anchor);
    }
}
