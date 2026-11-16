using Deviant.Combat.Contracts;
using Deviant.Definitions;

namespace Deviant.Combat.Execution
{
    // Embed privately and call in place. Only the fallback basic-motion cursor
    // belongs here; lifecycle, clocks and specialized motion retain their owners.
    public struct CastMotionExecution
    {
        float lastBasicMotionTime;

        public static bool Moving<T>(ref T host) where T : struct, ICastMotionHost
            => !host.HasChargedMelee && (host.MissileFlying || host.Approaching || !host.Cancelled && !host.MotionDone && host.ForcedDashMoving);

        public static bool FreezeVertical<T>(SkillExecutionDefinition definition, ref T host) where T : struct, ICastMotionHost
        {
            if (host.HasDirectMeleeSpecial || host.HasChargedMelee) return false;
            if (host.Cancelled || host.MotionDone) return false;
            if (host.MissileFlying) return true;
            if (host.Approaching) return true;
            if (definition.UsesCompiledTracks) return definition.Tracks.Vertical == ActionVerticalControl.FreezeDuringMotionThenFallFromZero && host.Moving;
            if (definition.Authored != null) return definition.VerticalFreeze.Count == 2 && host.Elapsed >= definition.VerticalFreeze[0] && host.Elapsed < definition.VerticalFreeze[1] || definition.Vertical == "freeze_during_movement_then_fall_from_zero" && host.Moving;
            if (definition.Vertical == "freeze_during_movement_then_fall_from_zero") return host.Moving;
            return definition.Vertical == "freeze_0.10_to_0.67_then_fall_from_zero" && host.Elapsed >= .1f && host.Elapsed < .67f;
        }

        public static bool Invulnerable<T>(SkillExecutionDefinition definition, ref T host) where T : struct, ICastMotionHost
        {
            if (host.HasDirectMeleeSpecial || host.HasChargedMelee) return false;
            if (host.Cancelled || host.MotionDone) return false;
            if (host.MissileFlying) return true;
            for (int i = 0; i < definition.InvulStart.Count; i++)
                if (host.Elapsed >= definition.InvulStart[i] && host.Elapsed < definition.InvulEnd[i]) return true;
            return false;
        }

        public float HorizontalVelocity<T>(int axis, SkillExecutionDefinition definition, ref T host) where T : struct, ICastMotionHost
        {
            if (host.HasMeleeAction) return host.MeleeHorizontalVelocity(axis);
            if (host.HasChargedMeleeExecution) return host.Cancelled || host.MotionDone ? axis * host.RunSpeed : 0;
            if (host.Cancelled || host.MotionDone) return axis * host.RunSpeed;
            if (host.MissileFlying) return host.MissileFlightVelocity();
            if (host.MissileRecoiling) return host.MissileRecoilVelocity();
            if (host.Approaching) return 0;
            if (host.Recoiling) return host.RangedRecoilVelocity();
            if (host.IsBasic)
            {
                // Queued attacks can start between fixed frames. Consume the
                // first partial interval even when airborne or input is neutral.
                float from = lastBasicMotionTime, to = Max(0, host.Elapsed + host.StepDuration); lastBasicMotionTime = to;
                if (!host.OwnerGrounded) return axis * host.RunSpeed;
                if (definition.Kind == "ranged") return axis * definition.InputSpeed;
                if (definition.BasicLungeDuration <= 0 || axis == 0) return 0;
                float active = Max(0, Min(to, definition.BasicLungeStart + definition.BasicLungeDuration) - Max(from, definition.BasicLungeStart));
                return axis * definition.BasicLungeDistance / definition.BasicLungeDuration * active / host.StepDuration;
            }
            if (host.TryForcedDashVelocity(out float forcedVelocity)) return forcedVelocity;
            if (definition.UsesCompiledTracks) return axis * 0f; // Keep Native signed zero while the typed control span locks horizontal input.
            return axis * definition.InputSpeed;
        }

        // Preserve the Native two-operand Mathf comparison and operation order.
        static float Min(float a, float b) => a < b ? a : b;
        static float Max(float a, float b) => a > b ? a : b;
    }
}
