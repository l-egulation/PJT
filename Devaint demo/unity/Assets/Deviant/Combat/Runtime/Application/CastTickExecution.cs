using Deviant.Combat.Contracts;
using Deviant.Definitions;

namespace Deviant.Combat.Execution
{
    // Stateless ordering only. The existing lifecycle, hits, charge, melee,
    // movement and smoke remain their sole owners, including during reentry.
    public static class CastTickExecution
    {
        public static void Step<T>(float step, SkillExecutionDefinition definition, ref T host) where T : struct, ICastTickHost
        {
            host.SetStepDuration(step);
            if (!host.OwnerExists || host.OwnerDead) { host.Dispose(); return; }
            host.ClearDebugBoxes();
            if (host.HasChargedMelee) { host.StepChargedMelee(step); return; }
            if (host.HasMeleeAction)
            {
                host.StepMeleeAction(step);
                if (host.MeleeActionTerminal) host.Dispose();
                return;
            }
            if (host.Activated)
            {
                var effect = definition.Effect;
                if (definition.HasProjectileWave) host.StepSkillWave(step);
                host.EvaluateHitTrack(step);
                if (effect != null && effect.Type == "smoke")
                {
                    bool active = host.SmokeIsActive(host.Elapsed);
                    host.CaptureSmokeArea();
                    if (active) host.AddSmokeDebugBox();
                    host.TryRefreshSmoke(host.Elapsed, active && host.SmokeAreaIntersectsOwner, host.Now, step, out float until);
                    host.SetOwnerSmokeUntil(Min(until, host.StartedAt + host.SmokeEnd));
                }
            }
            // Resolve the final sweep before the primary target's end-of-rush hit.
            if (host.MovementDone) host.EndPrimary();
            host.CommitHitEvaluation(host.Elapsed + step);
            host.AfterHits(step);
        }
        static float Min(float a, float b) => a < b ? a : b;
    }
}
