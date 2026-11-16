using System;
using Deviant.Combat.Contracts;

namespace Deviant.Combat.Execution
{
    public static class ActorMotionResolver
    {
        public static ActorMotionProposal Resolve(ActorMotionInput input)
        {
            // Reject the default struct as well as constructor-invalid inputs.
            if (input.Step <= 0 || input.RiseGravity <= 0 || input.FallGravity <= 0) throw new ArgumentException("A validated motion input is required.", nameof(input));
            float x = input.DesiredX, y = input.VelocityY;
            if (input.Grounded && y <= 0) y = 0;
            if (!input.ForcedMovement)
            {
                // Only the remaining part of the final push tick contributes
                // displacement; desired input does not fill the expired part.
                if (input.Now < input.KnockUntil)
                    x = input.KnockSpeed * Math.Max(0, Math.Min(1, (input.KnockUntil - input.Now) / Math.Max(input.Step, .000001f)));
                else if (input.Now < input.InputLockUntil || input.Now < input.StunUntil || input.Now < input.RootUntil || input.Now < input.StaggerUntil) x = 0;
            }
            if (input.Dead || input.Decoy) x = 0;
            float dx = x * input.Step, dy = 0;
            var apex = input.Apex;
            if (input.Grounded || input.FreezeVertical || input.ForcedMovement || input.Dead || input.Decoy || input.ControlledVertical.HasValue)
                apex = default;
            if (input.ControlledVertical.HasValue && !input.Dead && !input.Decoy &&
                input.Now >= input.KnockUntil && input.Now >= input.InputLockUntil && input.Now >= input.StunUntil &&
                input.Now >= input.RootUntil && input.Now >= input.StaggerUntil)
            {
                y = input.ControlledVertical.Value; dy = y * input.Step;
            }
            else if (input.FreezeVertical) y = 0;
            else if (!input.Grounded || y > 0)
            {
                if ((apex.Armed || apex.Remaining > 0) && input.JumpApexSeconds > 0)
                {
                    float remaining = input.Step;
                    if (apex.Armed && y > 0)
                    {
                        float toApex = y / input.RiseGravity;
                        bool reachesApex = toApex <= remaining;
                        float up = Math.Min(remaining, toApex);
                        dy = y * up - .5f * input.RiseGravity * up * up;
                        remaining -= up;
                        y = reachesApex ? 0 : y - input.RiseGravity * up;
                        if (reachesApex) apex = new ActorJumpApexState(false, input.JumpApexSeconds);
                    }
                    else if (apex.Armed) apex = default;
                    if (apex.Remaining > 0)
                    {
                        float hold = Math.Min(remaining, apex.Remaining);
                        remaining -= hold;
                        apex = new ActorJumpApexState(false, apex.Remaining - hold);
                        y = 0;
                    }
                    if (remaining > 0)
                    {
                        dy += y * remaining - .5f * input.FallGravity * remaining * remaining;
                        y -= input.FallGravity * remaining;
                    }
                    return new ActorMotionProposal(x, y, dx, dy, apex);
                }
                float gravity = y > 0 ? input.RiseGravity : input.FallGravity;
                // Keep the previous float operation order, including the strict
                // apex boundary and exact integration on both sides of it.
                if (y > 0 && y / gravity < input.Step)
                {
                    float up = y / gravity, down = input.Step - up;
                    dy = y * up - .5f * gravity * up * up - .5f * input.FallGravity * down * down;
                    y = -input.FallGravity * down;
                }
                else { dy = y * input.Step - .5f * gravity * input.Step * input.Step; y -= gravity * input.Step; }
            }
            return new ActorMotionProposal(x, y, dx, dy, apex);
        }
    }
}
