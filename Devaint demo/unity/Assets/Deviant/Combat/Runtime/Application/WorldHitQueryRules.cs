using System;
using Deviant.Combat.Contracts;

namespace Deviant.Combat.Execution
{
    public static class WorldHitQueryRules
    {
        public static bool TrySelectBox<THost>(ref THost host, ref float distance) where THost : struct, IWorldNearestTargetHost
        {
            if (host.IsPlayer || host.IsDecoy || host.Dead || host.Occluded()) return false;
            float candidate = Distance(host.OwnerCenter, host.TargetCenter);
            return TrySelect(candidate, ref host, ref distance);
        }

        public static bool TrySelectCircle<THost>(float radius, ref THost host, ref float distance, bool ignoreOcclusion = false) where THost : struct, IWorldNearestTargetHost
        {
            if (!host.Exists || host.Dead || host.IsDecoy || host.IsPlayer) return false;
            float candidate = Distance(host.OwnerCenter, host.TargetCenter);
            if (candidate > radius || !ignoreOcclusion && host.Occluded()) return false;
            return TrySelect(candidate, ref host, ref distance);
        }

        static bool TrySelect<THost>(float candidate, ref THost host, ref float distance) where THost : struct, IWorldNearestTargetHost
        {
            if (candidate < distance || Approximately(candidate, distance, host.ApproximationEpsilon) && host.BestExists && host.ActorId < host.BestActorId)
            { distance = candidate; return true; }
            return false;
        }

        static float Distance(WorldHitPoint from, WorldHitPoint to)
        {
            float x = from.X - to.X, y = from.Y - to.Y;
            return (float)Math.Sqrt(x * x + y * y);
        }

        static bool Approximately(float a, float b, float epsilon)
            => Math.Abs(b - a) < Max(.000001f * Max(Math.Abs(a), Math.Abs(b)), epsilon * 8);
        static float Max(float a, float b) => a > b ? a : b;
        static float Min(float a, float b) => a < b ? a : b;

        public static bool Exposed<THost>(WorldHitPoint origin, WorldHitBounds bounds, WorldHitBox box, ref THost host) where THost : struct, IWorldHitVisibilityHost
            => Exposed(origin,bounds,box,ref host,out _);
        public static bool Exposed<THost>(WorldHitPoint origin, WorldHitBounds bounds, WorldHitBox box, ref THost host, out WorldHitPoint accepted) where THost : struct, IWorldHitVisibilityHost
        {
            accepted=default;
            float left = Max(bounds.MinX, box.X - box.Width / 2), right = Min(bounds.MaxX, box.X + box.Width / 2);
            float bottom = Max(bounds.MinY, box.Y - box.Height / 2), top = Min(bounds.MaxY, box.Y + box.Height / 2);
            if (right < left || top < bottom) return false;
            // The inset is intentionally not clamped for touching/narrow
            // intersections. Native linecasts retain center, BL, TL, BR, TR order.
            for(int i=0;i<5;i++)
            {
                var point=i==0?new WorldHitPoint((left+right)/2,(bottom+top)/2):
                    new WorldHitPoint(i<3?left+.001f:right-.001f,i==1||i==3?bottom+.001f:top-.001f);
                if(host.Occluded(origin,point))continue;
                accepted=point;return true;
            }
            return false;
        }
    }
}
