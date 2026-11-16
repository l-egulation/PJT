using System;

namespace Deviant.Presentation
{
    // Real presentation time follows the death animation's simulation clock, including existing slow motion.
    public sealed class PlayerDeathSequence
    {
        public const float KickSeconds = .28f, FadeSeconds = 1.8f, PoseHoldSeconds = .1f, BlackHoldSeconds = .25f;
        float duration, elapsed, previousSimulationAge, fadeAt = -1;
        public bool Active { get; private set; }
        // A chapter clear reuses only the blackout and black hold: no death pose to wait for and no hit kick.
        public bool Clearing { get; private set; }
        public float Elapsed => elapsed;
        public float FadeOpacity
        {
            get
            {
                if (!Active || fadeAt < 0) return 0;
                float t = Math.Max(0, Math.Min(1, (elapsed - fadeAt) / FadeSeconds));
                return t * t * (3 - 2 * t);
            }
        }
        public bool ResultReady => Active && fadeAt >= 0 && elapsed >= fadeAt + FadeSeconds + BlackHoldSeconds;
        public bool Begin(float animationSeconds)
        {
            if (float.IsNaN(animationSeconds) || float.IsInfinity(animationSeconds) || animationSeconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(animationSeconds));
            if (Active) return false;
            Reset(); duration = animationSeconds; Active = true; return true;
        }
        public bool BeginClear()
        {
            if (Active) return false;
            Reset(); Active = Clearing = true; fadeAt = 0; return true;
        }
        public void Advance(float realDelta, float simulationAge)
        {
            if (float.IsNaN(realDelta) || float.IsInfinity(realDelta) || realDelta < 0 ||
                float.IsNaN(simulationAge) || float.IsInfinity(simulationAge) || simulationAge < 0)
                throw new ArgumentOutOfRangeException(nameof(realDelta));
            if (!Active) return;
            float before = elapsed; elapsed += realDelta;
            if (fadeAt < 0 && simulationAge >= duration)
            {
                float span = simulationAge - previousSimulationAge;
                float fraction = span > 0 ? Math.Max(0, Math.Min(1, (duration - previousSimulationAge) / span)) : 1;
                fadeAt = before + realDelta * fraction + PoseHoldSeconds;
            }
            previousSimulationAge = simulationAge;
        }
        public FeedbackOffset ReadKick(int facing)
        {
            if (!Active || Clearing || elapsed >= KickSeconds) return new FeedbackOffset(0, 0);
            float envelope = 1 - elapsed / KickSeconds;
            float x = -(facing < 0 ? -1 : 1) * (float)Math.Sin(Math.PI * Math.Min(1, elapsed / .055f) / 2) * 10 * envelope;
            float y = (float)Math.Sin(elapsed * 28) * 2.5f * envelope;
            return new FeedbackOffset(x, y);
        }
        public void Reset() { Active = Clearing = false; duration = elapsed = previousSimulationAge = 0; fadeAt = -1; }
    }
}
