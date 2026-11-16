using Deviant.Combat.Contracts;

namespace Deviant.Combat.Execution
{
    public static class PlayerFallRecoveryExecution
    {
        public static void Fall<T>(ref T host) where T : struct, IPlayerFallRecoveryHost
        {
            host.CancelAction();
            host.ClearBufferedJump();
            host.Health = NativeMaximum(0, host.Health - host.MaxHealth * .1f);
            if (host.Dead) { host.Die(); if (host.Dead) return; }
            float restoreX = host.SpawnX, restoreY = host.SpawnY;
            host.LastFallTime = host.Now; host.LastRestoreTime = -1;
            int facing = host.Facing;
            bool found = host.TrySafeBefore(host.Now - 3, out var sample);
            host.RecordSample(found, sample);
            if (found) { restoreX = sample.X; restoreY = sample.Y; facing = sample.Facing; host.LastRestoreTime = sample.Time; }
            host.Place(restoreX, restoreY); host.SyncFacing(facing);
            host.Place(restoreX, restoreY); host.ClearVelocity(); host.ResetJumps(); host.SetProtectionUntil(host.Now + .6f);
            host.SetLastRestorePosition(restoreX, restoreY); host.ClearHadGround();
            host.ClearHitMotionLocks();
            host.RecordRestored(restoreX, restoreY);
        }
        static float NativeMaximum(float first, float second) => first > second ? first : second;
    }
}
