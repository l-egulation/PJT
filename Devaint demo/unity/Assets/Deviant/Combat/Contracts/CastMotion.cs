namespace Deviant.Combat.Contracts
{
    // Each access reads the current Native fact. Specialized movement owners
    // remain behind these calls; nested Moving/Recoiling queries are not cached.
    public interface ICastMotionHost
    {
        bool HasMeleeAction { get; }
        float MeleeHorizontalVelocity(int axis);
        bool HasChargedMelee { get; }
        bool HasChargedMeleeExecution { get; }
        bool HasDirectMeleeSpecial { get; }
        bool Cancelled { get; }
        bool MotionDone { get; }
        bool MissileFlying { get; }
        float MissileFlightVelocity();
        bool MissileRecoiling { get; }
        float MissileRecoilVelocity();
        bool Approaching { get; }
        bool Recoiling { get; }
        float RangedRecoilVelocity();
        bool Moving { get; }
        bool ForcedDashMoving { get; }
        bool TryForcedDashVelocity(out float velocity);
        bool IsBasic { get; }
        float Elapsed { get; }
        float StepDuration { get; }
        bool OwnerGrounded { get; }
        float RunSpeed { get; }
    }
}
