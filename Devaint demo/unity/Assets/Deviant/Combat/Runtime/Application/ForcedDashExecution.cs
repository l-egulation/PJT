using Deviant.Combat.Contracts;
using Deviant.Definitions;

namespace Deviant.Combat.Execution
{
    // Embedded privately by Cast; never copy or box this mutable owner. The
    // caller supplies its current elapsed time/step and retains all lifecycle,
    // activation, resource, native movement and hit/callback ordering authority.
    public struct ForcedDashExecution
    {
        const string ContinueSpin = "stop_translation_continue_spin";
        readonly SkillMovementDefinition definition;
        bool movementDone;
        float wallStopTime;
        public bool MovementDone => movementDone;
        public ForcedDashExecution(SkillMovementDefinition definition)
        { this.definition = definition; movementDone = false; wallStopTime = float.PositiveInfinity; }
        public ForcedDashSnapshot Read() => new ForcedDashSnapshot(movementDone, wallStopTime);
        public bool IsMoving(bool activated, float elapsed, float step)
            => !movementDone && activated && IsDash &&
                elapsed + step > definition.Start && elapsed < definition.Start + definition.Duration;
        public bool TryHorizontalVelocity(bool activated, float elapsed, float step, int direction, out float velocity)
        {
            velocity = 0;
            if (!IsDash || movementDone || !activated) return false;
            float overlap = Max(0, Min(elapsed + step, definition.Start + definition.Duration) - Max(elapsed, definition.Start));
            if (!(overlap > 0)) return false;
            velocity = direction * definition.Speed * overlap / step;
            return true;
        }
        // Deliberately no cancellation/completion check: the original Native
        // AfterMovement admission only tests definition, activation and done.
        public ForcedDashMovementEnd AfterMovement(bool activated, float elapsed, float step, bool wallHit)
        {
            if (!IsDash || movementDone || !activated) return default;
            if (wallHit && elapsed + step >= definition.Start)
            {
                movementDone = true;
                wallStopTime = elapsed + step;
                bool shorten = definition.WallPolicy != ContinueSpin;
                return new ForcedDashMovementEnd(true, shorten, shorten ? elapsed + step + definition.WallRecovery : 0);
            }
            if (elapsed + step >= definition.Start + definition.Duration)
            {
                movementDone = true;
                return new ForcedDashMovementEnd(true, false, 0);
            }
            return default;
        }
        // The accepted primary boss hit ends translation/recovery immediately,
        // but must not install (or clear) the wall's direct-hit cutoff. This may
        // run after an earlier contact; do not add a movementDone admission gate.
        public float EndAtPrimaryBoss(float elapsed, float step)
        {
            movementDone = true;
            return elapsed + step + definition.WallRecovery;
        }
        public bool DirectHitAllowed(float elapsed)
            => !(definition != null && definition.WallPolicy != ContinueSpin && elapsed >= wallStopTime);
        bool IsDash => definition != null && (definition.UsesCompiledTrack || definition.Type == "dash");
        // Match the two-operand Native float comparisons and operation order.
        static float Min(float a, float b) => a < b ? a : b;
        static float Max(float a, float b) => a > b ? a : b;
    }
}
