using System;
using Deviant.Combat.Contracts;

namespace Deviant.Combat.Execution
{
    // Only the previous-ground transition belongs here. All other state stays
    // with the existing input, arbiter, dash, jump, cast and motor owners.
    public struct PlayerTickExecution
    {
        public bool HadGround { get; set; }

        public void Step<T>(float step, ref T host) where T : struct, IPlayerTickHost
        {
            if (host.InputBlocked) host.ClearInputState();
            for (int i = 0; i < host.WeaponCount; i++) host.TickWeaponCooldown(i);
            if (host.Dead) { host.ClearPressed(); return; }
            host.ResolveShurikenReturns();
            var input = host.CurrentInput;
            host.EvaluateActions(input);
            host.ApplyJumpRelease(input);
            host.ClearPressed();
            bool wasDash = host.Dashing;
            float horizontal = OrdinaryHorizontal(step, ref host);
            bool freezeVertical = false;
            if (host.HasCurrentCast)
            {
                if (host.CastIsBasic && host.Axis != 0) { host.SyncFacing(host.Axis); host.FollowBasicFacing(); }
                host.BeforeCastMovement(step);
                horizontal = host.HasCurrentCast ? host.CastHorizontalVelocity(host.Axis) : horizontal;
                freezeVertical = host.HasCurrentCast && host.CastFreezeVertical;
                if (host.HasCurrentCast && !host.CastHasDirectMeleeSpecial && !host.CastHasChargePolicy && !host.Grounded &&
                    host.CastVerticalPolicy == "normal" && !host.CastRecoiling && !host.CastApproaching && !host.CastMissileFlying)
                    horizontal = OrdinaryHorizontal(step, ref host);
            }
            host.ApplyDashMotion(step, ref horizontal, ref freezeVertical);
            if (!host.HasCurrentCast && !host.Dashing && host.Axis != 0) host.SyncFacing(host.Axis);
            host.PhysicsStep(step, horizontal, freezeVertical);
            if (host.EndDashAtWall()) host.ClearVerticalVelocity();
            if (wasDash && !host.Dashing) host.ClearVerticalVelocity();
            if (host.HasCurrentCast) host.AfterCastMovement();
            if (HadGround == false && host.Grounded) host.Landed();
            HadGround = host.Grounded;
            // Re-probe current geometry after movement, not the grounded flag.
            if (!host.Dashing && host.ProbeSafeFloor(out float safeY) && host.RecoveryRecordingSafe()) host.RecordSafeFloor(safeY);
            host.BeginPits();
            try
            {
                while (host.NextPit())
                    if (host.CurrentPitIntersects()) { host.Fall(); break; }
            }
            finally { host.EndPits(); }
            host.AfterMovementActions();
        }
        // Reads live axis/speed like the former Axis * RunSpeed. The previous
        // committed velocity seeds it, so special motions hand over without state.
        static float OrdinaryHorizontal<T>(float step, ref T host) where T : struct, IPlayerTickHost
        {
            float speed = host.RunSpeed;
            return HorizontalRunControl.Approach(Math.Max(-speed, Math.Min(speed, host.VelocityX)), host.Axis * speed, speed, step,
                host.Grounded ? host.GroundAccelerationSeconds : host.AirAccelerationSeconds,
                host.Grounded ? host.GroundDecelerationSeconds : host.AirDecelerationSeconds);
        }
    }
}
