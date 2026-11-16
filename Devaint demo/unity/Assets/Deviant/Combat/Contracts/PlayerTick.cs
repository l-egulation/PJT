namespace Deviant.Combat.Contracts
{
    // Live player/cast facts and Native operations. The host retains scene
    // references, movement owners, geometry, recovery history and pit iteration.
    public interface IPlayerTickHost
    {
        bool InputBlocked { get; }
        void ClearInputState();
        int WeaponCount { get; }
        void TickWeaponCooldown(int index);
        bool Dead { get; }
        void ResolveShurikenReturns();
        CombatInput CurrentInput { get; }
        void EvaluateActions(CombatInput input);
        void ApplyJumpRelease(CombatInput input);
        void ClearPressed();
        bool Dashing { get; }
        int Axis { get; }
        float RunSpeed { get; }
        float VelocityX { get; }
        float GroundAccelerationSeconds { get; }
        float GroundDecelerationSeconds { get; }
        float AirAccelerationSeconds { get; }
        float AirDecelerationSeconds { get; }
        bool HasCurrentCast { get; }
        bool CastIsBasic { get; }
        void SyncFacing(int direction);
        void FollowBasicFacing();
        void BeforeCastMovement(float step);
        float CastHorizontalVelocity(int axis);
        bool CastFreezeVertical { get; }
        bool CastHasDirectMeleeSpecial { get; }
        bool CastHasChargePolicy { get; }
        bool Grounded { get; }
        string CastVerticalPolicy { get; }
        bool CastRecoiling { get; }
        bool CastApproaching { get; }
        bool CastMissileFlying { get; }
        void ApplyDashMotion(float step, ref float horizontal, ref bool freezeVertical);
        void PhysicsStep(float step, float horizontal, bool freezeVertical);
        bool EndDashAtWall();
        void ClearVerticalVelocity();
        void AfterCastMovement();
        void Landed();
        bool ProbeSafeFloor(out float safeY);
        bool RecoveryRecordingSafe();
        void RecordSafeFloor(float safeY);
        void BeginPits();
        bool NextPit();
        bool CurrentPitIntersects();
        void EndPits();
        void Fall();
        void AfterMovementActions();
    }
}
