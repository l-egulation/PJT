using System;
using Deviant.Combat.Contracts;
using Deviant.Definitions;

namespace Deviant.Combat.Execution
{
    // Embedded privately by the cast; mutable execution state is never copied
    // or boxed. Only the immutable Read snapshot is returned by value.
    public struct MissileExecution
    {
        readonly SkillMissileDefinition definition;
        readonly float riseGravity, fallGravity;
        bool ended, endPending, recoilStopped;
        float launchedAt, travel, move, recoilStartedAt, recoilDuration;
        public bool Initialized => definition != null;
        public bool Ended => ended;
        public float Move => move;
        public MissileExecution(SkillMissileDefinition definition, float riseGravity, float fallGravity)
        {
            this.definition = definition; this.riseGravity = riseGravity; this.fallGravity = fallGravity;
            ended = endPending = recoilStopped = false;
            launchedAt = travel = move = recoilDuration = 0; recoilStartedAt = -1;
        }
        public MissileSnapshot Read() => new MissileSnapshot(ended, endPending, recoilStopped, launchedAt, travel, move, recoilStartedAt, recoilDuration);
        public void Launch(float now) => launchedAt = now;
        public float FlightAge(float now) => Max(0, now - launchedAt);
        public bool IsFlying<T>(ref T host) where T : struct, IMissileExecutionHost
            => host.Activated && !ended && !host.Cancelled && !host.MotionDone && host.OwnerAlive;
        public bool IsRecoiling<T>(ref T host) where T : struct, IMissileExecutionHost
            => recoilStartedAt >= 0 && !recoilStopped && !host.Cancelled && !host.MotionDone && host.Now < recoilStartedAt + recoilDuration - .000001f;
        public bool Prepare<T>(float step, ref T host) where T : struct, IMissileExecutionHost
        {
            if (!IsFlying(ref host)) return false;
            float remaining = Max(0, definition.Distance - travel);
            move = Min(remaining, definition.Speed * step);
            endPending = remaining <= move + .000001f;
            return true;
        }
        // Native filtering and <= nearest-contact selection stay in their
        // original order. A contact claims pending before the sweep completes.
        public void Contact() => endPending = true;
        public void SetMove(float nearest) => move = nearest;
        public float FlightVelocity(int direction, float step) => direction * move / Max(.000001f, step);
        public bool TryManualDetonation<T>(ref T host) where T : struct, IMissileExecutionHost
        {
            if (!IsFlying(ref host) || FlightAge(host.Now) + .000001f < definition.ManualDelay) return false;
            End(false, ref host); return true;
        }
        public void AfterMovement<T>(ref T host) where T : struct, IMissileExecutionHost
        {
            if (IsFlying(ref host))
            {
                travel += Math.Abs(host.MovedX);
                host.UpdateVisual();
                if (endPending || host.WallHit || travel >= definition.Distance - .000001f) End(true, ref host);
            }
            else if (IsRecoiling(ref host) && (host.WallHit || host.Grounded && host.Now > recoilStartedAt + .000001f))
                recoilStopped = true;
        }
        void End<T>(bool afterMovement, ref T host) where T : struct, IMissileExecutionHost
        {
            if (!IsFlying(ref host)) return;
            // End flight/invulnerability before entering Native damage. Reentry,
            // cancellation and exceptions do not retract this terminal claim.
            ended = true;
            host.Explode();
            float height = definition.RecoilHeight;
            host.StartHop((float)Math.Sqrt(2 * riseGravity * height));
            recoilDuration = (float)Math.Sqrt(2 * height / riseGravity) + (float)Math.Sqrt(2 * height / fallGravity);
            // Read the current clock after the explosion and hop callbacks.
            recoilStartedAt = host.Now + (afterMovement ? host.StepDuration : 0);
            host.SetMotionEnd(recoilStartedAt - host.StartedAt + definition.Recovery);
            host.RecordEnd();
        }
        public float RecoilVelocity(int direction, float now, float step)
        {
            float portion = Clamp01((recoilStartedAt + recoilDuration - now) / Max(.000001f, step));
            return -direction * definition.RecoilDistance / Max(.000001f, recoilDuration) * portion;
        }
        static float Max(float a, float b) => a > b ? a : b;
        static float Min(float a, float b) => a < b ? a : b;
        static float Clamp01(float value) => value < 0 ? 0 : value > 1 ? 1 : value;
    }
}
