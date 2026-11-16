namespace Deviant.Combat.Contracts
{
    // The host supplies current facts at each callback boundary. Native target
    // admission, resources, geometry and effects are not lifecycle snapshots.
    public interface ICastLifecycleHost
    {
        float Now { get; }
        float Elapsed { get; }
        bool ChargeHeld { get; }
        bool Approaching { get; }
        void AdvanceApproach(float step);
        void Activate();
        void PrepareMissile(float step);
        void ReleaseDueBombs();
        void PrepareTeleport(float step);
        void ReleaseResources();
        bool CancelMeleeAction();
        void Failed();
        bool MissileEnded { get; }
        void Complete();
        float SmokeEnd { get; }
        float RetainSkillWaveUntil(float last, float elapsed, float step);
        float RecastRemaining { get; }
        float RecastDeadline { get; }
        void Dispose();
    }
}
