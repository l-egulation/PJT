using System;

namespace Deviant.Combat.Execution
{
    // Ordinary horizontal control only. Dash, wall carry, recoil and attack
    // motions keep their own velocities; their result seeds the next tick.
    public static class HorizontalRunControl
    {
        // Seconds are measured from rest to runSpeed and back; zero is immediate.
        // A reversal stops first and spends the rest of the step accelerating.
        public static float Approach(float current, float target, float runSpeed, float step, float accelerationSeconds, float decelerationSeconds)
        {
            if (runSpeed <= 0 || step <= 0) return target;
            if (current != 0 && (target == 0 || Math.Sign(target) != Math.Sign(current) || Math.Abs(target) < Math.Abs(current)))
            {
                float floor = target != 0 && Math.Sign(target) == Math.Sign(current) ? target : 0;
                float slow = Rate(runSpeed, decelerationSeconds), gap = Math.Abs(current - floor);
                if (!float.IsPositiveInfinity(slow) && slow * step < gap) return current - Math.Sign(current) * slow * step;
                step -= float.IsPositiveInfinity(slow) ? 0 : gap / slow;
                current = floor;
                if (current == target) return target;
            }
            float rise = Rate(runSpeed, accelerationSeconds);
            if (float.IsPositiveInfinity(rise) || Math.Abs(target - current) <= rise * step) return target;
            return current + Math.Sign(target - current) * rise * step;
        }
        static float Rate(float runSpeed, float seconds) => seconds <= 0 ? float.PositiveInfinity : runSpeed / seconds;
    }
}
