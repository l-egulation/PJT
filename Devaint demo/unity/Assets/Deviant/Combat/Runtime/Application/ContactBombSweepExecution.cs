using Deviant.Combat.Contracts;

namespace Deviant.Combat.Execution
{
    public static class ContactBombSweepExecution
    {
        public static bool IsObstacle<T>(ref T host) where T : struct, IContactBombObstacleHost
        {
            if (host.FindSurface())
            {
                if (!host.SurfaceIsOneWay) return true;
                return host.MovementY < 0 && host.NormalY > .5f && host.BombBottom >= host.SurfaceTop - .03f;
            }
            return host.FindRegion() && host.TargetExists && !host.TargetIsOwner &&
                !host.TargetIsPlayer && !host.TargetIsDecoy && !host.TargetDead;
        }

        public static void Sweep<T>(ref T host) where T : struct, IContactBombSweepHost
        {
            if (host.Stopped) return;
            if (host.FuseSeconds > 0) { host.SweepTimed(); return; }
            host.SnapshotDelta();
            // Starting overlaps precede even a zero-length flight, in Native order.
            int count = host.QueryOverlaps();
            for (int i = 0; i < count; i++)
                if (host.OverlapIsObstacle(i)) { host.Explode(); return; }
            float length = host.DeltaLength;
            if (length < .000001f) return;
            float nearest = length; bool hit = false;
            count = host.QueryCast(length);
            for (int i = 0; i < count; i++)
            {
                if (host.CastIsObstacle(i) && (!hit || host.CastDistance < nearest))
                {
                    float distance = host.CastDistance;
                    if (distance < 0) distance = 0;
                    else if (distance > length) distance = length;
                    nearest = distance; hit = true;
                }
            }
            host.ApplyTravel(length, nearest);
            if (hit) host.Explode();
        }
    }
}
