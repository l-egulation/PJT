using Deviant.Combat.Contracts;

namespace Deviant.Combat.Execution
{
    public static class RangedLaunchExecution
    {
        public static void Fire<T>(ref T host) where T : struct, IRangedLaunchHost
        {
            host.CaptureOrigin();
            float muzzleX = host.SpawnX > 0 ? host.SpawnX :
                host.OwnerOffsetX + host.OwnerWidth / 2 + host.ProjectileRadius + host.SpawnGap;
            host.CaptureMuzzle(muzzleX);
            if (host.ProjectileType == "laser") { host.FireLaser(); return; }
            if (IsShuriken(host.ProjectileType))
            {
                host.CaptureMuzzleDelta();
                float nearestSolid = float.PositiveInfinity;
                int count = host.QueryMuzzlePath();
                for (int i = 0; i < count; i++)
                {
                    host.SelectMuzzleHit(i);
                    if (host.MuzzleSurfaceExists && !host.MuzzleSurfaceIsOneWay && !host.IgnoresTerrain)
                    {
                        float distance = host.MuzzleHitDistance;
                        nearestSolid = nearestSolid < distance ? nearestSolid : distance;
                    }
                }
                if (!float.IsPositiveInfinity(nearestSolid))
                {
                    if (host.BlastRadius > 0)
                    {
                        host.SpawnBlocked(nearestSolid);
                        host.DetonateShot();
                    }
                    host.RecordBlocked(); return;
                }
                host.SpawnAtMuzzle();
                host.AssignSourceWeapon(!host.HasSourceWeapon && host.CapturePlayerOwner());
                host.SweepShotToMuzzle();
                host.RecordFired(1); return;
            }
            for (int i = 0; i < host.AngleCount; i++)
            {
                float angle = host.AngleAt(i);
                host.SpawnAtOrigin(angle);
                host.SweepShotToMuzzle();
            }
            host.RecordFired(host.AngleCount);
        }

        static bool IsShuriken(string type) => type == "shuriken" || type == "shuriken_special" ||
            type == "shuriken_range" || type == "shuriken_move" || type == "shuriken_followup";

        public static void SpawnBomb<T>(float fragmentMultiplier, ref T host) where T : struct, IBombLaunchHost
        {
            host.CaptureRadians();
            host.CaptureOrigin();
            host.Spawn();
            host.SetFragmentMultiplier(fragmentMultiplier);
            float distance = host.OwnerWidth / 2 + host.ProjectileRadius + host.SpawnGap;
            host.SweepToMuzzle(distance);
        }
    }
}
