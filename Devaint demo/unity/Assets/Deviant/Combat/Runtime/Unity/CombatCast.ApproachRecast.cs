using Deviant.Combat.Contracts;
using UnityEngine;

namespace Deviant.Combat
{
    public partial class CombatCast
    {
        struct ApproachRecastHost : IApproachRecastHost
        {
            readonly CombatCast cast;
            readonly WeaponState selectedWeapon;
            readonly CombatCommandKey command;
            Vector2 destination, from, next;
            public ApproachRecastHost(CombatCast cast, WeaponState selectedWeapon = null, CombatCommandKey command = default)
            {
                this.cast = cast; this.selectedWeapon = selectedWeapon; this.command = command;
                destination = from = next = default;
            }
            public bool SameWeapon => selectedWeapon == cast.weapon;
            public bool Cancelled => cast.CastCancelled;
            public bool MotionDone => cast.CastMotionDone;
            public bool TargetHit => cast.targetHit;
            public bool MarkedTargetAlive => cast.marked && !cast.marked.Dead;
            public bool IsRecastMovement => cast.Definition.Movement != null && cast.Definition.Movement.Type == "target_dash_recast";
            public float Now => cast.arena.Now;
            public float Elapsed => cast.Elapsed;
            public float RecastWindow => cast.Definition.Movement.RecastWindow;
            public float Distance => cast.Definition.Movement.Distance;
            public float Speed => cast.Definition.Movement.Speed;
            public float DistanceToDestination => Vector2.Distance(cast.owner.Position, destination);
            public float MovedDistance => Vector2.Distance(from, cast.owner.Position);
            public float RequestedDistance => Vector2.Distance(from, next);
            public void CaptureDestination() => destination = cast.TargetDestination();
            public void CaptureOrigin() => from = cast.owner.Position;
            public void StartApproach() => cast.arena.Player.StartApproach(cast);
            public void ResumeMotion() => cast.lifecycle.ResumeMotion();
            public void MoveTowardDestination(float distance)
            {
                next = Vector2.MoveTowards(from, destination, distance);
                cast.owner.TeleportToward(next);
            }
            public void SetMotionEnd(float elapsed) => cast.lifecycle.SetMotionEnd(elapsed);
            public void ClearVerticalSpeed() => cast.owner.VelocityY = 0;
            public void RecordApproach()
            {
                cast.StartCommand(command);
                cast.arena.Record("whip D recast: approach speed=" + cast.Definition.Movement.Speed);
            }
        }
    }
}
