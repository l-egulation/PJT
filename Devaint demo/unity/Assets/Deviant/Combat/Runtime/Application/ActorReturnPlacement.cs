using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;

namespace Deviant.Combat.Execution
{
    // Geometry only. Return deadlines, weapon/target lifetimes and fallback
    // admission remain in the existing caller, not in a second world state.
    public static class ActorReturnPlacement
    {
        public static ActorPose AdjacentTo(ActorBodyFrame body, ActorBodyFrame target, int side, int desiredFacing, float gap)
        {
            Check(gap); if (side != -1 && side != 1) throw new ArgumentException("A target side is required.", nameof(side));
            float centerX = target.CenterX + side * (target.Width / 2 + body.Width / 2 + gap);
            return new ActorPose(centerX - body.OffsetX * desiredFacing, target.Y, desiredFacing);
        }
        public static bool CanReach(ActorBodyFrame body, ActorPose pose, IActorPlacementQuery world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world)); Validate(pose);
            if (ActorPlacementResolver.Blocked(body, pose.X, pose.Y, pose.Facing, world)) return false;
            float destinationX = pose.X + body.OffsetX * pose.Facing, destinationY = pose.Y + body.OffsetY;
            float deltaX = destinationX - body.CenterX, deltaY = destinationY - body.CenterY;
            float length = (float)Math.Sqrt(deltaX * deltaX + deltaY * deltaY); Check(length);
            if (length < .001f) return true;
            var hits = world.BoxCast(new ActorBoxCast(body.CenterX, body.CenterY, body.Width - .015f, body.Height - .015f, deltaX / length, deltaY / length, length))
                ?? throw new InvalidOperationException("Return sweep provided no contact collection.");
            foreach (var hit in hits)
                if (!hit.OneWay && hit.Distance > .001f && hit.Distance < length - .01f) return false;
            return true;
        }
        public static bool TryNearOrigin(ActorBodyFrame body, ActorPose origin, IActorPlacementQuery world, out ActorPose destination)
        {
            destination = origin;
            if (CanReach(body, origin, world)) return true;
            var xs = new List<float> { origin.X }; var ys = new List<float> { origin.Y };
            var surfaces = world.OverlapBox(new ActorOverlapBox(origin.X + body.OffsetX * origin.Facing, origin.Y + body.OffsetY, body.Width + 2, body.Height + 2))
                ?? throw new InvalidOperationException("Return neighborhood provided no bounds collection.");
            foreach (var surface in surfaces)
            {
                if (surface.OneWay) continue;
                xs.Add(surface.MinX - body.Width / 2 - body.OffsetX * origin.Facing - .01f);
                xs.Add(surface.MaxX + body.Width / 2 - body.OffsetX * origin.Facing + .01f);
                ys.Add(surface.MinY - body.OffsetY - body.Height / 2 - .01f);
                ys.Add(surface.MaxY - body.OffsetY + body.Height / 2 + .01f);
            }
            var candidates = new List<ActorPose>();
            foreach (float x in xs) foreach (float y in ys)
            { var at = new ActorPose(x, y, origin.Facing); if (Squared(at, origin) <= 1.00001f) candidates.Add(at); }
            candidates.Sort((a, b) => { int order = Squared(a, origin).CompareTo(Squared(b, origin)); if (order != 0) return order; order = a.X.CompareTo(b.X); return order != 0 ? order : a.Y.CompareTo(b.Y); });
            foreach (var at in candidates) if (CanReach(body, at, world)) { destination = at; return true; }
            return false;
        }
        static float Squared(ActorPose a, ActorPose b) { float x = a.X - b.X, y = a.Y - b.Y; return x * x + y * y; }
        static void Validate(ActorPose pose) { if (pose.Facing != -1 && pose.Facing != 1) throw new ArgumentException("A valid return pose is required."); }
        static void Check(float value) { if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value)); }
    }
}
