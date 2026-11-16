using System;
using Deviant.Combat.Contracts;

namespace Deviant.Combat.Execution
{
    public static class ActorPlacementResolver
    {
        public static bool Blocked(ActorBodyFrame body, float x, float y, int direction, IActorPlacementQuery world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world)); Check(x); Check(y);
            float centerX = x + body.OffsetX * direction, centerY = y + body.OffsetY;
            float width = body.Width - .015f, height = body.Height - .015f;
            float halfX = width * .5f, halfY = height * .5f;
            float minX = centerX - halfX, maxX = centerX + halfX, minY = centerY - halfY, maxY = centerY + halfY;
            var surfaces = world.OverlapBox(new ActorOverlapBox(centerX, centerY, width, height))
                ?? throw new InvalidOperationException("Actor overlap query returned no bounds collection.");
            for (int i = 0; i < surfaces.Count; i++)
            {
                var surface = surfaces[i];
                // Native overlaps include a contact margin. Retain the exact
                // bounds/foot/Z recheck before rejecting a floor-touching pose.
                if (!surface.OneWay && surface.MaxY > y + .02f && minX <= surface.MaxX && maxX >= surface.MinX &&
                    minY <= surface.MaxY && maxY >= surface.MinY && surface.MinZ <= 0 && surface.MaxZ >= 0) return true;
            }
            return false;
        }
        public static bool ProbeSafeFloor(ActorBodyFrame body, float velocityY, IActorPlacementQuery world, out float height)
        {
            height = 0;
            if (!ActorContactResolver.ProbeGround(body, velocityY, world)) return false;
            float first = 0;
            for (int i = 0; i < 3; i++)
            {
                float x = body.CenterX + (i - 1) * (body.Width / 2 - .025f); bool found = false;
                var hits = world.Raycast(new ActorRayCast(x, body.Y + .04f, 0, -1, .10f))
                    ?? throw new InvalidOperationException("Actor floor query returned no contact collection.");
                for (int j = 0; j < hits.Count; j++)
                {
                    var hit = hits[j];
                    if (hit.Ignored || hit.NormalY < .99f) continue;
                    if (i == 0) first = hit.Y;
                    if (Math.Abs(first - hit.Y) > .02f) return false;
                    found = true; break;
                }
                if (!found) return false;
            }
            height = first; return true;
        }
        public static ActorTeleportMotion TeleportToward(ActorBodyFrame body, float destinationX, float destinationY, IActorPlacementQuery world, bool crossWalls = false)
        {
            if (world == null) throw new ArgumentNullException(nameof(world)); Check(destinationX); Check(destinationY);
            float deltaX = destinationX - body.X, deltaY = destinationY - body.Y;
            float length = (float)Math.Sqrt(deltaX * deltaX + deltaY * deltaY); Check(length);
            if (length < .001f) return new ActorTeleportMotion(body.X, body.Y, false, true);
            if (crossWalls)
                return Blocked(body, destinationX, destinationY, body.Facing, world)
                    ? new ActorTeleportMotion(body.X, body.Y, false, false)
                    : new ActorTeleportMotion(destinationX, destinationY, true, false);
            float directionX = deltaX / length, directionY = deltaY / length, allowed = length;
            var hits = world.BoxCast(new ActorBoxCast(body.CenterX, body.CenterY, body.Width - .005f, body.Height - .005f, directionX, directionY, length))
                ?? throw new InvalidOperationException("Actor teleport query returned no contact collection.");
            for (int i = 0; i < hits.Count; i++)
            {
                var hit = hits[i];
                if (!hit.OneWay && hit.Distance > .001f) allowed = Math.Min(allowed, Math.Max(0, hit.Distance - .01f));
            }
            float x = body.X + directionX * allowed, y = body.Y + directionY * allowed;
            if (Blocked(body, x, y, body.Facing, world)) return new ActorTeleportMotion(body.X, body.Y, false, false);
            return new ActorTeleportMotion(x, y, true, false);
        }
        static void Check(float value)
        { if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value)); }
    }
}
