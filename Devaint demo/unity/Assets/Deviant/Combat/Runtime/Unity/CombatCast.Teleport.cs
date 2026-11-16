using Deviant.Combat.Contracts;
using UnityEngine;

namespace Deviant.Combat
{
    public partial class CombatCast
    {
        struct CastTeleportHost : ICastTeleportHost
        {
            readonly CombatCast cast;
            Vector2 destination;
            public CastTeleportHost(CombatCast cast) { this.cast = cast; destination = default; }
            public float Elapsed => cast.Elapsed;
            public bool MarkedTargetAlive => cast.marked && !cast.marked.Dead;
            public bool TargetHit => cast.targetHit;
            // A strike that found no target at activation whiffs where the owner stands: no turn, no teleport.
            public void FaceMarkedTarget()
            {
                if (!cast.marked) return;
                // Selection is radial; arrival is in front of the enemy's
                // facing. Turn before computing the owner's offset body.
                cast.direction = -cast.marked.facing; cast.owner.SyncFacing(cast.direction);
            }
            public void CaptureStrikeDestination() => destination = cast.marked ? cast.TargetDestination() : cast.owner.Position;
            public void TeleportToStrikeDestination()
            {
                if (cast.marked) MoveWithSound(destination, cast.Definition.Movement.WallPolicy == "teleport_endpoint_clear");
            }
            void MoveWithSound(Vector2 destination, bool crossWalls = false)
            {
                var before = cast.owner.Position;
                cast.owner.TeleportToward(destination, crossWalls);
                if (cast.owner == cast.arena.Player && (cast.owner.Position - before).sqrMagnitude > .000001f)
                    cast.arena.Audio?.EmitSkillEvent(cast.Definition.Id, "teleport", cast.owner.Position);
            }
            public void CapturePreviousOwner() => cast.previousOwner = cast.owner.Position;
            public void RecordStrike() => cast.arena.Record("rapier instant target teleport at=" + cast.owner.Position);
            public void TeleportToMarkedTarget()
            {
                var m = cast.Definition.Movement;
                float side = cast.owner.Center.x <= cast.marked.Center.x ? -1 : 1;
                float x = cast.marked.Center.x + side * (cast.marked.size.x / 2 + cast.owner.size.x / 2 + m.Gap) - cast.owner.offset.x * cast.owner.facing;
                Vector2 at = new Vector2(x, cast.owner.Position.y);
                at = cast.owner.Position + Vector2.ClampMagnitude(at - cast.owner.Position, m.Distance);
                MoveWithSound(at);
            }
            public void TeleportForward() => MoveWithSound(cast.owner.Position + Vector2.right * cast.direction * cast.Definition.Movement.Distance);
        }
    }
}
