namespace Deviant.Combat.Contracts
{
    // Per-call live cast facts and Native operations. Specialized execution
    // owners retain their state; the smoke area is only temporary host geometry.
    public interface ICastTickHost
    {
        void SetStepDuration(float step);
        bool OwnerExists { get; }
        bool OwnerDead { get; }
        void Dispose();
        void ClearDebugBoxes();
        bool HasChargedMelee { get; }
        void StepChargedMelee(float step);
        bool HasMeleeAction { get; }
        void StepMeleeAction(float step);
        bool MeleeActionTerminal { get; }
        bool Activated { get; }
        void StepSkillWave(float step);
        void EvaluateHitTrack(float step);
        float Elapsed { get; }
        bool SmokeIsActive(float elapsed);
        void CaptureSmokeArea();
        void AddSmokeDebugBox();
        bool SmokeAreaIntersectsOwner { get; }
        float Now { get; }
        bool TryRefreshSmoke(float elapsed, bool ownerOverlaps, float now, float step, out float until);
        float StartedAt { get; }
        float SmokeEnd { get; }
        void SetOwnerSmokeUntil(float until);
        bool MovementDone { get; }
        void EndPrimary();
        void CommitHitEvaluation(float elapsed);
        void AfterHits(float step);
    }
}
