using Deviant.Combat.Contracts;

namespace Deviant.Combat.Execution
{
    public static class ActorFacingExecution
    {
        public static bool Sync<T>(int direction, ref T host) where T : struct, IActorFacingHost
        {
            if (direction == 0) return true;
            direction = direction < 0 ? -1 : 1;
            if (direction != host.Facing)
            {
                // Blocked receives the old body and the requested direction. A
                // synchronous query may change the live pose, offset or facing.
                host.ReadPosition(out float x, out float y);
                if (ActorPlacementResolver.Blocked(host.BodyFrame, x, y, direction, host.SurfaceQuery))
                {
                    // Preserve Position += evaluation order and the body centre
                    // at a wall; do not apply a pre-query pose after callbacks.
                    host.ReadPosition(out x, out y);
                    float correction = host.OffsetX * (host.Facing - direction);
                    host.SetPosition(x + correction, y + 0);
                }
            }
            host.Facing = direction;
            float offsetX = host.OffsetX * host.Facing, offsetY = host.OffsetY;
            if (host.HasBody) host.SetBodyOffset(offsetX, offsetY);
            if (host.HasHurt) host.SetHurtOffset(offsetX, offsetY);
            if (host.HasVisual) host.SetVisualFlip(host.Facing < 0);
            if (host.HasVisual && !host.IsPlayer) host.SetVisualPosition(host.OffsetX * host.Facing, host.OffsetY);
            return true;
        }
    }
}
