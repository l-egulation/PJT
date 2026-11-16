using Deviant.Combat.Contracts;

namespace Deviant.Combat.Execution
{
    public static class PlayerProjectileSweepExecution
    {
        public static bool IsValid<T>(ref T host) where T : struct, IPlayerProjectileContactHost
        {
            if (host.FindSurface()) return !host.IgnoresTerrain && !host.SurfaceIsOneWay;
            return host.FindRegion() && host.TargetExists && !host.TargetIsOwner &&
                !host.TargetIsPlayer && !host.TargetIsDecoy && !host.TargetDead;
        }

        public static int Priority<T>(ref T host) where T : struct, IPlayerProjectileContactHost
        {
            if (host.FindSurface()) return int.MinValue;
            return host.FindRegion() && host.TargetExists ? host.TargetId : int.MaxValue;
        }

        public static int CompareOverlap<T>(ref T host) where T : struct, IPlayerProjectileOverlapHost
        {
            string type = host.ProjectileType;
            if (type == "shuriken" || type == "shuriken_special" || type == "shuriken_range" ||
                type == "shuriken_move" || type == "shuriken_followup")
            {
                int firstRank = host.FirstValid ? (host.FirstIsSurface ? 0 : 1) : 2;
                int secondRank = host.SecondValid ? (host.SecondIsSurface ? 0 : 1) : 2;
                if (firstRank != secondRank) return firstRank.CompareTo(secondRank);
                if (firstRank == 1)
                {
                    host.CaptureActors();
                    int distance = host.FirstSquaredDistance.CompareTo(host.SecondSquaredDistance);
                    if (distance != 0) return distance;
                }
            }
            return host.FirstPriority.CompareTo(host.SecondPriority);
        }

        public static int CompareCast<T>(ref T host) where T : struct, IPlayerProjectileCastOrderHost
        {
            int distance = host.FirstDistance.CompareTo(host.SecondDistance);
            return distance != 0 ? distance : host.FirstPriority.CompareTo(host.SecondPriority);
        }

        public static void Sweep<T>(ref T host) where T : struct, IPlayerProjectileSweepHost
        {
            if (host.Removed) return;
            int count = host.QueryOverlaps();
            host.SortOverlaps();
            for (int i = 0; i < count; i++)
                if (host.ResolveOverlap(i)) return;
            // Overlap callbacks may move the shot or re-enter with other lists.
            host.SnapshotDelta();
            float length = host.DeltaLength;
            if (length < .000001f) return;
            host.CaptureStart();
            count = host.QueryCast(length);
            host.SortCasts();
            for (int i = 0; i < count; i++)
            {
                if (!host.CastValid(i)) continue;
                float distance = host.CastDistance;
                if (distance < 0) distance = 0;
                else if (distance > length) distance = length;
                host.ApplyContact(length, distance);
                if (host.ResolveCast(host.ContactAge(host.CastDistance))) return;
            }
            // Release in a nonterminal callback does not unwind an entered sweep.
            host.ApplyDestination();
        }
    }
}
