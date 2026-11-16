using System;

namespace ForestCombatV2
{
    // Observation only: actual health loss, using the same paused simulation clock as combat.
    public sealed class TrainingDamageMeter
    {
        public const float QuietSeconds = 2;
        public float Total { get; private set; }
        public float StartedAt { get; private set; }
        public float LastHitAt { get; private set; }
        public bool HasHits { get; private set; }
        public bool Finished(float now) => HasHits && now >= LastHitAt + QuietSeconds;
        public float Duration(float now) => HasHits ? Math.Max(0, Math.Min(now, LastHitAt + QuietSeconds) - StartedAt) : 0;
        public float Dps(float now) => Duration(now) > 0 ? Total / Duration(now) : 0;
        public void Add(float damage, float now)
        {
            if (float.IsNaN(damage) || float.IsInfinity(damage) || damage <= 0 || float.IsNaN(now) || float.IsInfinity(now) || now < 0) return;
            if (!HasHits || Finished(now)) { Total = 0; StartedAt = now; }
            HasHits = true; Total += damage; LastHitAt = now;
        }
        public void Clear() { Total = StartedAt = LastHitAt = 0; HasHits = false; }
    }
}
