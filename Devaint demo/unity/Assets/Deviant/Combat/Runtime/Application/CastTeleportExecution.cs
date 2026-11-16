using Deviant.Combat.Contracts;
using Deviant.Definitions;

namespace Deviant.Combat.Execution
{
    // One embedded cast field owns the claim. Constrained ref calls mutate that
    // same field across synchronous callbacks without a boxed/copied runner.
    public struct CastTeleportExecution
    {
        readonly SkillMovementDefinition movement;
        bool claimed;
        public bool Claimed => claimed;
        public CastTeleportExecution(SkillMovementDefinition movement)
        {
            this.movement = movement; claimed = false;
        }
        public void Prepare<T>(float step, ref T host) where T : struct, ICastTeleportHost
        {
            if (movement == null || movement.Type == "dash" || movement.Type == "target_dash_recast" || claimed || !(host.Elapsed + step >= movement.Start)) return;
            // Even a no-op target attempt or an interrupted callback has spent
            // the one shot. Reentry must observe it before any Native effects.
            claimed = true;
            if (movement.Type == "target_teleport_strike")
            {
                host.FaceMarkedTarget();
                host.CaptureStrikeDestination();
                host.TeleportToStrikeDestination();
                host.CapturePreviousOwner();
                host.RecordStrike();
            }
            else if (movement.Type == "target_teleport")
            {
                if (host.MarkedTargetAlive && host.TargetHit) host.TeleportToMarkedTarget();
            }
            else host.TeleportForward();
        }
    }
}
