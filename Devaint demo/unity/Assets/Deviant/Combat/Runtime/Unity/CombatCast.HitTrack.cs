using Deviant.Combat.Contracts;
using UnityEngine;

namespace Deviant.Combat
{
    public partial class CombatCast
    {
        struct SkillHitTrackHost : ISkillHitTrackHost
        {
            readonly CombatCast cast;
            public SkillHitTrackHost(CombatCast cast) { this.cast = cast; }
            public bool Activated => cast.CastActivated;
            public bool Cancelled => cast.CastCancelled;
            public bool MotionDone => cast.CastMotionDone;
            public float Elapsed => cast.Elapsed;
            public float ChargeShift => cast.charge?.Release?.Shift ?? 0;
            public bool DirectHitAllowed => cast.forcedDash.DirectHitAllowed(cast.Elapsed);
            public bool TargetHit => cast.targetHit;
            public bool TeleportClaimed => cast.teleport.Claimed;
            public bool MarkedTargetAlive => cast.Definition.Effect?.Type == "target_mark"
                ? cast.HasLivingPrimaryTarget : cast.marked && !cast.marked.Dead;
            public void ApplyMarkedTarget(int index) => cast.ApplyMarkedTargets(index);
            public void ApplyTeleportTarget(int index)
            {
                var hit = cast.Definition.Hits[index];
                Vector2 center = cast.owner.Position + new Vector2(hit.Box.X * cast.direction, hit.Box.Y), size = new Vector2(hit.Box.Width, hit.Box.Height);
                if (cast.arena.TargetsInBox(center, size).Contains(cast.marked) && cast.arena.Exposed(cast.owner.Center, cast.marked, center, size))
                    cast.ApplyTarget(index, hit, cast.marked);
            }
            public void ApplyBox(int index, SkillHitBoxAnchor anchor)
            {
                Vector2 from, to;
                if (anchor == SkillHitBoxAnchor.SavedPrimaryWorldPose) from = to = cast.savedPose;
                else if (anchor == SkillHitBoxAnchor.SavedSpawnWorldPose) from = to = cast.installCenter;
                else { from = cast.previousOwner; to = cast.owner.Position; }
                cast.ApplyBox(index, cast.Definition.Hits[index], from, to, false);
            }
        }
    }
}
