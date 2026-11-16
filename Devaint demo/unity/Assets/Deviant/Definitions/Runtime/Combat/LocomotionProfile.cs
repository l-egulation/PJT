using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Deviant.Definitions
{
    [Serializable]
    public sealed class WallJumpData
    {
        public float horizontalSpeed, carryDuration, bufferDuration;
        // Zero keeps the original single kick, immediate release and full-control push.
        public float detachDuration, awayHorizontalSpeed, releaseCarryDuration, controlRecoveryDuration;
        // Zero disables the wall-jump arrival cling and the wall-top hop.
        public float arrivalClingDuration, ledgeHopHeight;
        // An air Space kicks from a wall within this distance on either side; 0 keeps held-wall kicks only.
        public float jumpCheckDistance;
        // Wall-top climb played as authored: the hero holds still at the wall for ledgeClimbSeconds, then stands on the top
        // ledgeClimbForward past the wall face. Zero seconds keeps the physics hop over the top.
        public float ledgeClimbSeconds, ledgeClimbForward;
    }

    // Descent while holding a wall without vertical input. maxSpeed 0 keeps the original hang.
    // staminaDuration is the per-wall hold limit; 0 means unlimited.
    [Serializable]
    public sealed class WallSlideData
    {
        public float clingDuration, startSpeed, maxSpeed, acceleration, staminaDuration;
    }

    [Serializable]
    public sealed class JumpAssistData
    {
        public float groundBufferDuration, wallCoyoteDuration, releaseVelocityScale;
    }

    // Seconds from rest to runSpeed (acceleration) and from runSpeed to rest (deceleration).
    [Serializable]
    public sealed class HorizontalControlData
    {
        public float groundAccelerationSeconds, groundDecelerationSeconds, airAccelerationSeconds, airDecelerationSeconds;
    }

    // Speed shape inside the unchanged dash distance and duration. The dash leaves at
    // peak speed, holds it for holdFraction of the duration, then eases out with
    // power (1 = constant speed, 2 = quad, 3 = cubic). A missing block keeps constant speed.
    [Serializable]
    public sealed class DashCurveData
    {
        public float holdFraction, easeOutPower;
    }

    [Serializable]
    public sealed class LocomotionData
    {
        public int schemaVersion;
        public string id;
        public float runSpeed, dashSpeed, dashDuration, dashRecovery, dashInvulnerability;
        public float dashJumpCarrySpeed, dashJumpCarryDuration;
        public float firstJumpVelocity, secondJumpVelocity, riseGravity, fallGravity;
        public float jumpApexSeconds;
        public WallJumpData wallJump;
        public WallSlideData wallSlide;
        public JumpAssistData jumpAssist;
        public HorizontalControlData horizontalControl;
        public DashCurveData dashCurve;
    }

    // Immutable values. Applying a new profile requires a scenario reset; an active
    // trajectory, dash or recoil must never observe a partially edited DTO.
    public sealed class LocomotionProfile
    {
        public const int SchemaVersion = 2;
        public const float LegacyJumpBufferDuration = .08f;
        public string Id { get; }
        public string ContentHash { get; }
        public float RunSpeed { get; }
        public float DashSpeed { get; }
        public float DashDuration { get; }
        public float DashRecovery { get; }
        public float DashInvulnerability { get; }
        public float DashJumpCarrySpeed { get; }
        public float DashJumpCarryDuration { get; }
        public float FirstJumpVelocity { get; }
        public float SecondJumpVelocity { get; }
        public float RiseGravity { get; }
        public float FallGravity { get; }
        public float JumpApexSeconds { get; }
        public float WallJumpHorizontalSpeed { get; }
        public float WallJumpCarryDuration { get; }
        public float WallJumpBufferDuration { get; }
        public float WallDetachDuration { get; }
        public float WallAwayKickSpeed { get; }
        public float WallReleaseKickCarryDuration { get; }
        public float WallKickControlRecoveryDuration { get; }
        public float WallArrivalClingDuration { get; }
        public float WallLedgeHopHeight { get; }
        public float WallJumpCheckDistance { get; }
        public float WallLedgeClimbSeconds { get; }
        public float WallLedgeClimbForward { get; }
        public float WallSlideClingDuration { get; }
        public float WallSlideStartSpeed { get; }
        public float WallSlideMaxSpeed { get; }
        public float WallSlideAcceleration { get; }
        public float WallStaminaDuration { get; }
        public float JumpBufferDuration { get; }
        public float WallCoyoteDuration { get; }
        public float JumpReleaseVelocityScale { get; }
        public float GroundAccelerationSeconds { get; }
        public float GroundDecelerationSeconds { get; }
        public float AirAccelerationSeconds { get; }
        public float AirDecelerationSeconds { get; }
        public float DashHoldFraction { get; }
        public float DashEaseOutPower { get; }
        // dashSpeed is the average speed, so the distance never depends on the curve.
        public float ExpectedDashDistance => DashSpeed * DashDuration;
        public bool DashConstantSpeed => DashHoldFraction == 0 && DashEaseOutPower == 1;
        public float DashPeakSpeed => (float)(DashSpeed / DashCurveArea(DashHoldFraction, DashEaseOutPower));

        // Fraction of the dash distance covered after the given fraction of its duration.
        public float DashDistanceFraction(float timeFraction)
        {
            if (!(timeFraction > 0)) return 0;
            if (timeFraction >= 1) return 1;
            double hold = DashHoldFraction, power = DashEaseOutPower;
            double area = DashCurveArea(DashHoldFraction, DashEaseOutPower);
            if (timeFraction <= hold) return (float)(timeFraction / area);
            double rest = 1 - hold, w = (timeFraction - hold) / rest;
            return (float)((hold + rest / power * (1 - Math.Pow(1 - w, power))) / area);
        }
        // Instantaneous speed at the given fraction of the dash duration.
        public float DashSpeedAt(float timeFraction)
        {
            if (DashConstantSpeed) return DashSpeed;
            if (timeFraction >= 1) return 0;
            if (!(timeFraction > DashHoldFraction)) return DashPeakSpeed;
            double w = (timeFraction - DashHoldFraction) / (1 - DashHoldFraction);
            return (float)(DashPeakSpeed * Math.Pow(1 - w, DashEaseOutPower - 1));
        }
        static double DashCurveArea(double hold, double power) => hold + (1 - hold) / power;

        LocomotionProfile(LocomotionData data)
        {
            Id = data.id;
            RunSpeed = data.runSpeed;
            DashSpeed = data.dashSpeed;
            DashDuration = data.dashDuration;
            DashRecovery = data.dashRecovery;
            DashInvulnerability = data.dashInvulnerability;
            DashJumpCarrySpeed = data.dashJumpCarrySpeed;
            DashJumpCarryDuration = data.dashJumpCarryDuration;
            FirstJumpVelocity = data.firstJumpVelocity; SecondJumpVelocity = data.secondJumpVelocity;
            RiseGravity = data.riseGravity; FallGravity = data.fallGravity;
            JumpApexSeconds = data.jumpApexSeconds;
            // Older schema-2 profiles retain their original wall-motion values.
            WallJumpHorizontalSpeed = data.wallJump?.horizontalSpeed ?? RunSpeed;
            WallJumpCarryDuration = data.wallJump?.carryDuration ?? DashJumpCarryDuration;
            WallJumpBufferDuration = data.wallJump?.bufferDuration ?? 0;
            WallDetachDuration = data.wallJump?.detachDuration ?? 0;
            WallAwayKickSpeed = data.wallJump?.awayHorizontalSpeed ?? 0;
            WallReleaseKickCarryDuration = data.wallJump?.releaseCarryDuration ?? 0;
            WallKickControlRecoveryDuration = data.wallJump?.controlRecoveryDuration ?? 0;
            WallArrivalClingDuration = data.wallJump?.arrivalClingDuration ?? 0;
            WallLedgeHopHeight = data.wallJump?.ledgeHopHeight ?? 0;
            WallJumpCheckDistance = data.wallJump?.jumpCheckDistance ?? 0;
            WallLedgeClimbSeconds = data.wallJump?.ledgeClimbSeconds ?? 0;
            WallLedgeClimbForward = data.wallJump?.ledgeClimbForward ?? 0;
            WallSlideClingDuration = data.wallSlide?.clingDuration ?? 0;
            WallSlideStartSpeed = data.wallSlide?.startSpeed ?? 0;
            WallSlideMaxSpeed = data.wallSlide?.maxSpeed ?? 0;
            WallSlideAcceleration = data.wallSlide?.acceleration ?? 0;
            WallStaminaDuration = data.wallSlide?.staminaDuration ?? 0;
            // Profiles without assist blocks keep the original 0.08s landing buffer,
            // full-height jumps and immediate horizontal control.
            JumpBufferDuration = data.jumpAssist?.groundBufferDuration ?? LegacyJumpBufferDuration;
            WallCoyoteDuration = data.jumpAssist?.wallCoyoteDuration ?? 0;
            JumpReleaseVelocityScale = data.jumpAssist?.releaseVelocityScale ?? 1;
            GroundAccelerationSeconds = data.horizontalControl?.groundAccelerationSeconds ?? 0;
            GroundDecelerationSeconds = data.horizontalControl?.groundDecelerationSeconds ?? 0;
            AirAccelerationSeconds = data.horizontalControl?.airAccelerationSeconds ?? 0;
            AirDecelerationSeconds = data.horizontalControl?.airDecelerationSeconds ?? 0;
            DashHoldFraction = data.dashCurve?.holdFraction ?? 0;
            DashEaseOutPower = data.dashCurve?.easeOutPower ?? 1;
            var canonical = new StringBuilder("2\n").Append(Id).Append('\n');
            foreach (float value in new[] { RunSpeed, DashSpeed, DashDuration, DashRecovery,
                DashInvulnerability, DashJumpCarrySpeed, DashJumpCarryDuration,
                FirstJumpVelocity, SecondJumpVelocity, RiseGravity, FallGravity, JumpApexSeconds,
                WallJumpHorizontalSpeed, WallJumpCarryDuration, WallJumpBufferDuration,
                JumpBufferDuration, WallCoyoteDuration, JumpReleaseVelocityScale,
                GroundAccelerationSeconds, GroundDecelerationSeconds, AirAccelerationSeconds, AirDecelerationSeconds,
                WallDetachDuration, WallAwayKickSpeed, WallReleaseKickCarryDuration,
                WallKickControlRecoveryDuration, WallSlideClingDuration, WallSlideStartSpeed, WallSlideMaxSpeed, WallSlideAcceleration,
                WallArrivalClingDuration, WallLedgeHopHeight, WallStaminaDuration, WallJumpCheckDistance,
                DashHoldFraction, DashEaseOutPower, WallLedgeClimbSeconds, WallLedgeClimbForward })
                canonical.Append(value.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
            using (var hash = SHA256.Create())
                ContentHash = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString())))
                    .Replace("-", "").ToLowerInvariant();
        }

        public LocomotionData ToData() => new LocomotionData {
            schemaVersion = SchemaVersion, id = Id, runSpeed = RunSpeed, dashSpeed = DashSpeed,
            dashDuration = DashDuration, dashRecovery = DashRecovery,
            dashInvulnerability = DashInvulnerability, dashJumpCarrySpeed = DashJumpCarrySpeed,
            dashJumpCarryDuration = DashJumpCarryDuration, firstJumpVelocity = FirstJumpVelocity,
            secondJumpVelocity = SecondJumpVelocity, riseGravity = RiseGravity, fallGravity = FallGravity,
            jumpApexSeconds = JumpApexSeconds,
            wallJump = new WallJumpData { horizontalSpeed = WallJumpHorizontalSpeed,
                carryDuration = WallJumpCarryDuration, bufferDuration = WallJumpBufferDuration,
                detachDuration = WallDetachDuration,
                awayHorizontalSpeed = WallAwayKickSpeed, releaseCarryDuration = WallReleaseKickCarryDuration,
                controlRecoveryDuration = WallKickControlRecoveryDuration,
                arrivalClingDuration = WallArrivalClingDuration, ledgeHopHeight = WallLedgeHopHeight,
                jumpCheckDistance = WallJumpCheckDistance, ledgeClimbSeconds = WallLedgeClimbSeconds,
                ledgeClimbForward = WallLedgeClimbForward },
            wallSlide = new WallSlideData { clingDuration = WallSlideClingDuration, startSpeed = WallSlideStartSpeed,
                maxSpeed = WallSlideMaxSpeed, acceleration = WallSlideAcceleration, staminaDuration = WallStaminaDuration },
            jumpAssist = new JumpAssistData { groundBufferDuration = JumpBufferDuration,
                wallCoyoteDuration = WallCoyoteDuration, releaseVelocityScale = JumpReleaseVelocityScale },
            horizontalControl = new HorizontalControlData { groundAccelerationSeconds = GroundAccelerationSeconds,
                groundDecelerationSeconds = GroundDecelerationSeconds, airAccelerationSeconds = AirAccelerationSeconds,
                airDecelerationSeconds = AirDecelerationSeconds },
            dashCurve = new DashCurveData { holdFraction = DashHoldFraction, easeOutPower = DashEaseOutPower }
        };

        public static bool TryCreate(LocomotionData data, out LocomotionProfile profile, out string error)
        {
            profile = null;
            error = null;
            if (data == null || data.schemaVersion != SchemaVersion || string.IsNullOrWhiteSpace(data.id) ||
                data.id.IndexOfAny(new[] { '\n', '\r' }) >= 0)
                error = "Locomotion requires schemaVersion 2 and a nonempty single-line id.";
            else if (!FiniteNonnegative(data.runSpeed) || !FiniteNonnegative(data.dashSpeed) ||
                !FiniteNonnegative(data.dashDuration) || data.dashDuration == 0 ||
                !FiniteNonnegative(data.dashRecovery) || !FiniteNonnegative(data.dashInvulnerability) ||
                !FiniteNonnegative(data.dashJumpCarrySpeed) || !FiniteNonnegative(data.dashJumpCarryDuration) ||
                !FiniteNonnegative(data.dashSpeed * data.dashDuration))
                error = "Locomotion values must be finite and nonnegative; dashDuration must be positive.";
            else if (!FinitePositive(data.firstJumpVelocity) || !FinitePositive(data.secondJumpVelocity) || !FinitePositive(data.riseGravity) || !FinitePositive(data.fallGravity))
                error = "Jump velocities and gravity must be finite and positive.";
            else if (!FiniteNonnegative(data.jumpApexSeconds) || data.jumpApexSeconds > .06f)
                error = "Jump apex seconds must be finite and between zero and 0.06.";
            else if (data.wallJump != null && (!FiniteNonnegative(data.wallJump.horizontalSpeed) ||
                !FiniteNonnegative(data.wallJump.carryDuration) || !FiniteNonnegative(data.wallJump.bufferDuration)))
                error = "Wall jump values must be finite and nonnegative.";
            else if (data.wallJump != null && (!InRange(data.wallJump.detachDuration, 0, .5f) ||
                !FiniteNonnegative(data.wallJump.awayHorizontalSpeed) ||
                !InRange(data.wallJump.releaseCarryDuration, 0, 1) || !InRange(data.wallJump.controlRecoveryDuration, 0, 1) ||
                !InRange(data.wallJump.arrivalClingDuration, 0, .5f) || !InRange(data.wallJump.ledgeHopHeight, 0, 2) ||
                !InRange(data.wallJump.jumpCheckDistance, 0, .5f) ||
                !InRange(data.wallJump.ledgeClimbSeconds, 0, 2) || !InRange(data.wallJump.ledgeClimbForward, 0, 3)))
                error = "Wall kick variants require detach/arrival seconds in 0..0.5, nonnegative speeds, carry/recovery seconds in 0..1, a ledge hop height in 0..2, a jump check distance in 0..0.5, ledge climb seconds in 0..2 and a ledge climb forward distance in 0..3.";
            else if (data.wallSlide != null && (!InRange(data.wallSlide.clingDuration, 0, 1) ||
                !FiniteNonnegative(data.wallSlide.startSpeed) || !FiniteNonnegative(data.wallSlide.maxSpeed) ||
                !FiniteNonnegative(data.wallSlide.acceleration) || data.wallSlide.startSpeed > data.wallSlide.maxSpeed ||
                !InRange(data.wallSlide.staminaDuration, 0, 10)))
                error = "Wall slide requires cling seconds in 0..1, nonnegative speeds with startSpeed <= maxSpeed and stamina seconds in 0..10.";
            else if (data.jumpAssist != null && (!InRange(data.jumpAssist.groundBufferDuration, 0, .5f) ||
                !InRange(data.jumpAssist.wallCoyoteDuration, 0, .5f) || !FinitePositive(data.jumpAssist.releaseVelocityScale) ||
                data.jumpAssist.releaseVelocityScale > 1))
                error = "Jump assist requires buffer/coyote seconds in 0..0.5 and a release velocity scale in (0, 1].";
            else if (data.horizontalControl != null && (!InRange(data.horizontalControl.groundAccelerationSeconds, 0, 1) ||
                !InRange(data.horizontalControl.groundDecelerationSeconds, 0, 1) || !InRange(data.horizontalControl.airAccelerationSeconds, 0, 1) ||
                !InRange(data.horizontalControl.airDecelerationSeconds, 0, 1)))
                error = "Horizontal control seconds must be finite and between zero and one.";
            else if (data.dashCurve != null && (!InRange(data.dashCurve.holdFraction, 0, .9f) ||
                !InRange(data.dashCurve.easeOutPower, 1, 8)))
                error = "Dash curve requires a hold fraction in 0..0.9 and an ease-out power in 1..8.";
            else if (data.dashInvulnerability > data.dashDuration)
                error = "Dash invulnerability cannot extend beyond dashDuration.";
            if (error != null) return false;
            profile = new LocomotionProfile(data);
            return true;
        }

        static bool FiniteNonnegative(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0;
        static bool InRange(float value, float min, float max) => FiniteNonnegative(value) && value >= min && value <= max;
        static bool FinitePositive(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0;
    }
}
