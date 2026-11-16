using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Definitions;

namespace Deviant.Combat.Execution
{
    // Embedded privately by Cast. Only the last fully evaluated time belongs
    // here; lifecycle, charge, Native target claims and geometry stay live.
    public struct SkillHitTrackExecution
    {
        float lastEvaluated;
        public SkillHitTrackExecution(float previousEvaluation) { lastEvaluated = previousEvaluation; }

        // Called inside the cast's already-entered activation region, after its
        // wave and before smoke. Do not add a pass-wide cancellation guard.
        public void Evaluate<T>(IReadOnlyList<SkillHitDefinition> hits, float step, ref T host) where T : struct, ISkillHitTrackHost
        {
            for (int i = 0; i < hits.Count; i++)
            {
                var hit = hits[i];
                if (hit.TrackAnchor.HasValue)
                {
                    if (hit.TrackAnchor.Value == ActionHitAnchor.ProjectileWave) continue; // The live wave owner performs this sweep, not direct hit time admission.
                    if (!DirectAllowed(hit, ref host) || !IntersectsTime(lastEvaluated, host.Elapsed + step, hit.Time + host.ChargeShift, hit.Active)) continue;
                    host.ApplyBox(i, hit.TrackAnchor.Value == ActionHitAnchor.Actor ? SkillHitBoxAnchor.Actor : SkillHitBoxAnchor.SavedPrimaryWorldPose);
                    continue;
                }
                if (hit.Anchor == "projectile" || hit.Anchor == "ranged_projectile" || hit.Anchor == "bomb_projectile" ||
                    !DirectAllowed(hit, ref host) || !IntersectsTime(lastEvaluated, host.Elapsed + step, hit.Time + host.ChargeShift, hit.Active)) continue;
                if (hit.Anchor == "marked_target")
                {
                    if (host.TargetHit && host.MarkedTargetAlive) host.ApplyMarkedTarget(i);
                    continue;
                }
                if (hit.Anchor == "teleport_target")
                {
                    if (host.TeleportClaimed && host.MarkedTargetAlive) host.ApplyTeleportTarget(i);
                    continue;
                }
                var anchor = hit.Anchor == "saved_primary_world_pose" ? SkillHitBoxAnchor.SavedPrimaryWorldPose :
                    hit.Anchor == "saved_spawn_world_pose" ? SkillHitBoxAnchor.SavedSpawnWorldPose : SkillHitBoxAnchor.Actor;
                host.ApplyBox(i, anchor);
            }
        }

        // The caller commits only after wave, every direct hit, smoke and
        // EndPrimary have returned, immediately before motion completion.
        public void CommitEvaluation(float now) { lastEvaluated = now; }

        static bool DirectAllowed<T>(SkillHitDefinition hit, ref T host) where T : struct, ISkillHitTrackHost
        {
            bool actor = hit.TrackAnchor.HasValue ? hit.TrackAnchor.Value == ActionHitAnchor.Actor : hit.Anchor == "actor" || hit.Anchor == "teleport_target";
            if (!actor) return host.Activated;
            if (host.Cancelled || host.MotionDone) return false;
            return host.DirectHitAllowed;
        }
        static bool IntersectsTime(float previous, float now, float start, float duration)
            => now >= start && previous < start + duration;
    }
}
