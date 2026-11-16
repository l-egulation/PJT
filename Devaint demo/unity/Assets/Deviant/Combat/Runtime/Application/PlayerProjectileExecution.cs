using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Definitions;

namespace Deviant.Combat.Execution
{
    // One reference owner is shared by Step and synchronous native contacts.
    // Entered sweeps and exceptions retain the old partial effects; claims are
    // never rolled back and Release does not unwind an entered callback.
    public sealed class PlayerProjectileExecution
    {
        readonly HashSet<int> hitActors = new HashSet<int>();
        readonly Dictionary<int, float> nextHits = new Dictionary<int, float>();
        public RangedProjectileDefinition Definition { get; }
        public bool Removed { get; private set; }
        public float Age { get; private set; }
        public float Traveled { get; private set; }
        bool visitClaimed;
        public bool TryClaimVisit()
        {
            if (visitClaimed) return false;
            visitClaimed = true; return true;
        }

        public PlayerProjectileExecution(RangedProjectileDefinition definition) => Definition = definition;

        public float ContactAge(float contactDistance, float actualSpeed)
            => Age + contactDistance / Max(.000001f, actualSpeed);

        // Overlaps use the current age; cast contacts supply their raw native
        // distance age. Claim before Receive, including rejection or exception.
        public bool TryClaimActor(int actorId, float actualSpeed, float contactAge = -1)
        {
            if (Definition.HitInterval > 0)
            {
                float at = contactAge < 0 ? Age : contactAge;
                if (at >= Definition.Distance / Max(.000001f, actualSpeed) - .00001f) return false;
                if (nextHits.TryGetValue(actorId, out float next) && at + .000001f < next) return false;
                nextHits[actorId] = at + Definition.HitInterval;
                return true;
            }
            return hitActors.Add(actorId);
        }
        public void Release(IPlayerProjectileExecutionHost host)
        {
            if (Removed) return;
            Removed = true;
            host.Remove();
        }
        public void Step(float dt, bool ownerAlive, float actualSpeed, IPlayerProjectileExecutionHost host)
        {
            if (Removed) return;
            if (!ownerAlive) { Release(host); return; }
            if (Definition.HitInterval > 0)
            {
                // Speed is captured once per Step, unlike contact-time speed.
                float remaining = dt, speed = actualSpeed;
                if (speed <= .000001f) { Release(host); return; }
                while (!Removed && remaining > .000001f)
                {
                    float step = Min(remaining, .02f, (Definition.Distance - Traveled) / speed);
                    foreach (float next in nextHits.Values)
                        if (next - Age > .000001f) step = Min(step, next - Age);
                    if (step <= .000001f) { Release(host); break; }
                    host.SweepByTime(step);
                    Traveled += speed * step; Age += step; remaining -= step;
                    host.Rotate(step);
                    if (!Removed && Traveled >= Definition.Distance - .000001f) Release(host);
                    else if (!Removed) host.SweepCurrent();
                }
                return;
            }
            float length = Min(Definition.Distance - Traveled, actualSpeed * dt);
            if (length <= 0) { Release(host); return; }
            host.SweepByDistance(length); Traveled += length;
            if (!Removed && Traveled >= Definition.Distance - .000001f) Release(host);
        }
        // Match Mathf's float comparisons without its params-array allocation.
        static float Min(float first, float second) => first < second ? first : second;
        static float Max(float first, float second) => first > second ? first : second;
        static float Min(float first, float second, float third)
        {
            float result = first;
            if (second < result) result = second;
            if (third < result) result = third;
            return result;
        }
    }
}
