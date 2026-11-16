using System;

namespace Deviant.Combat.Contracts
{
    public readonly struct ActorJumpApexState
    {
        public bool Armed { get; }
        public float Remaining { get; }
        public ActorJumpApexState(bool armed, float remaining)
        {
            if (float.IsNaN(remaining) || float.IsInfinity(remaining) || remaining < 0 || remaining > .06f)
                throw new ArgumentOutOfRangeException(nameof(remaining));
            Armed = armed; Remaining = remaining;
        }
    }
    // Values after the current ground probe. No actor/collider/clock is retained.
    public readonly struct ActorMotionInput
    {
        public float Now { get; }
        public float Step { get; }
        public float DesiredX { get; }
        public float VelocityY { get; }
        public bool Grounded { get; }
        public bool FreezeVertical { get; }
        public bool ForcedMovement { get; }
        public bool Dead { get; }
        public bool Decoy { get; }
        public float KnockSpeed { get; }
        public float KnockUntil { get; }
        public float InputLockUntil { get; }
        public float StunUntil { get; }
        public float RootUntil { get; }
        public float StaggerUntil { get; }
        public float RiseGravity { get; }
        public float FallGravity { get; }
        public float? ControlledVertical { get; }
        public ActorJumpApexState Apex { get; }
        public float JumpApexSeconds { get; }
        public ActorMotionInput(float now, float step, float desiredX, float velocityY, bool grounded, bool freezeVertical,
            bool forcedMovement, bool dead, bool decoy, float knockSpeed, float knockUntil, float inputLockUntil,
            float stunUntil, float rootUntil, float staggerUntil, float riseGravity, float fallGravity, float? controlledVertical = null,
            ActorJumpApexState apex = default, float jumpApexSeconds = 0)
        {
            if (!Finite(now) || now < 0 || !Finite(step) || step <= 0 || !Finite(desiredX) || !Finite(velocityY) ||
                !Finite(knockSpeed) || !Finite(knockUntil) || !Finite(inputLockUntil) || !Finite(stunUntil) || !Finite(rootUntil) || !Finite(staggerUntil) ||
                !Finite(riseGravity) || riseGravity <= 0 || !Finite(fallGravity) || fallGravity <= 0)
                throw new ArgumentOutOfRangeException(nameof(step), "Motion requires finite values, a nonnegative clock and positive step/gravity.");
            if (controlledVertical.HasValue && !Finite(controlledVertical.Value)) throw new ArgumentOutOfRangeException(nameof(controlledVertical));
            if (!Finite(jumpApexSeconds) || jumpApexSeconds < 0 || jumpApexSeconds > .06f) throw new ArgumentOutOfRangeException(nameof(jumpApexSeconds));
            Apex = apex; JumpApexSeconds = jumpApexSeconds;
            ControlledVertical = controlledVertical; Now = now; Step = step; DesiredX = desiredX; VelocityY = velocityY; Grounded = grounded; FreezeVertical = freezeVertical;
            ForcedMovement = forcedMovement; Dead = dead; Decoy = decoy; KnockSpeed = knockSpeed; KnockUntil = knockUntil;
            InputLockUntil = inputLockUntil; StunUntil = stunUntil; RootUntil = rootUntil; StaggerUntil = staggerUntil;
            RiseGravity = riseGravity; FallGravity = fallGravity;
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
    // Proposed velocity/displacement before contacts. The motor owns the actual
    // movement and may stop velocity or change grounding after a collision.
    public readonly struct ActorMotionProposal
    {
        public float VelocityX { get; }
        public float VelocityY { get; }
        public float DeltaX { get; }
        public float DeltaY { get; }
        public ActorJumpApexState Apex { get; }
        public ActorMotionProposal(float velocityX, float velocityY, float deltaX, float deltaY, ActorJumpApexState apex = default)
        {
            if (!Finite(velocityX) || !Finite(velocityY) || !Finite(deltaX) || !Finite(deltaY))
                throw new ArgumentOutOfRangeException(nameof(deltaX), "Motion proposal must remain finite.");
            VelocityX = velocityX; VelocityY = velocityY; DeltaX = deltaX; DeltaY = deltaY; Apex = apex;
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
