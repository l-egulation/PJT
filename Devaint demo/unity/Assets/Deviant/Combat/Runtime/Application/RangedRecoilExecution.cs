using System;
using Deviant.Combat.Contracts;

namespace Deviant.Combat.Execution
{
    // Owns only the launch recoil hop (ground or air). The cast owns cancellation,
    // action completion, immutable launch input and the caller's simulation clock.
    // Embed privately; do not copy or box this mutable execution state.
    public struct RangedRecoilExecution
    {
        float started, duration;
        int direction;
        bool begun, stopped, airborne;
        public RangedRecoilSnapshot Read() => new RangedRecoilSnapshot(begun ? started : -1, duration, direction, stopped);
        public float Begin(float now, int facing, float configuredDuration, float hopHeight, float riseGravity, float fallGravity)
        {
            begun = true; started = now; direction = facing; duration = configuredDuration; airborne = false;
            if (hopHeight > 0)
                duration = (float)Math.Sqrt(2 * hopHeight / riseGravity) + (float)Math.Sqrt(2 * hopHeight / fallGravity);
            return hopHeight > 0 ? (float)Math.Sqrt(2 * riseGravity * hopHeight) : 0;
        }
        public bool IsActive(float now, bool cancelled) => begun && !cancelled && !stopped && started >= 0 && now < started + duration - .000001f;
        public float Velocity(float now, float step, bool grounded, float distance)
        {
            // A hop that starts on the ground ends only after it has actually left the ground.
            if (grounded && airborne) { stopped = true; return 0; }
            if (!grounded) airborne = true;
            float portion = (started + duration - now) / step;
            portion = portion < 0 ? 0 : portion > 1 ? 1 : portion;
            return -direction * distance / duration * portion;
        }
        public void AfterMovement(float now, bool cancelled, bool wallHit, bool grounded)
        {
            if (!grounded) airborne = true;
            if (IsActive(now, cancelled) && (wallHit || grounded && airborne)) stopped = true;
        }
    }
}
