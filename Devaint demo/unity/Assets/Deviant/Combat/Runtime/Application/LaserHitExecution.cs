using Deviant.Combat.Contracts;

namespace Deviant.Combat.Execution
{
    public static class LaserHitExecution
    {
        public static void Fire<T>(ref T host) where T : struct, ILaserHitHost
        {
            float gap = host.MuzzleGap, length = gap + host.ProjectileDistance;
            int count = host.QueryCast(length);
            for (int i = 0; i < count; i++)
            {
                if (host.IgnoresTerrain || !host.FindSurface(i) || host.SurfaceIsOneWay) continue;
                float margin = host.SurfaceDistance - .001f;
                float allowed = 0 > margin ? 0 : margin;
                length = length < allowed ? length : allowed;
            }
            host.BeginTargets();
            host.QueryTargets(length, .001f > length ? .001f : length);
            while (host.NextTarget())
            {
                if (host.TargetIsOwner || host.TargetIsPlayer || host.TargetIsDecoy || host.TargetDead ||
                    !host.TryClaimTarget() || (!host.IgnoresTerrain && host.TargetOccluded())) continue;
                host.ReceiveBasic();
            }
            // Claims precede occlusion, and entered callbacks keep their partial
            // effects. No owner/death recheck or finally completes a failed fire.
            float remainder = length - gap;
            float visible = 0 > remainder ? 0 : remainder;
            if (visible > 0) host.ShowVisual(visible);
            host.Record(host.ClaimedTargetCount);
        }
    }
}
