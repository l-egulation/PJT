using System;

namespace Deviant.Run
{
    [Serializable] public sealed class StageRecoveryData
    {
        public int usesPerStage;
        public int[] prices;
        public float healthFraction, holdSeconds, reuseDelaySeconds, interactionReach;
    }

    // One scene's reservoir. Revisiting or hiding the facility never refills it.
    public sealed class StageRecovery
    {
        public int Capacity { get; }
        public int Remaining { get; private set; }
        public float HealthFraction { get; }
        public float HoldSeconds { get; }
        public float ReuseDelaySeconds { get; }
        public float Reach { get; }
        readonly int[] prices;
        public static StageRecoveryData WithExtraUses(StageRecoveryData data, int extra)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (extra < 0 || extra > 10 - data.usesPerStage) throw new ArgumentOutOfRangeException(nameof(extra));
            // Validate the original before extending it; never mutate the shared Resources tuning.
            _ = new StageRecovery(data);
            var expanded = new int[data.usesPerStage + extra];
            Array.Copy(data.prices, expanded, data.prices.Length);
            for (int i = data.prices.Length; i < expanded.Length; i++) expanded[i] = data.prices[data.prices.Length - 1];
            return new StageRecoveryData { usesPerStage = expanded.Length, prices = expanded, healthFraction = data.healthFraction,
                holdSeconds = data.holdSeconds, reuseDelaySeconds = data.reuseDelaySeconds, interactionReach = data.interactionReach };
        }
        public int Price => Remaining > 0 ? prices[Capacity - Remaining] : 0;
        float nextUse = float.NegativeInfinity;

        public StageRecovery(StageRecoveryData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.usesPerStage < 1 || data.usesPerStage > 10 ||
                !Finite(data.healthFraction) || data.healthFraction <= 0 || data.healthFraction > 1 ||
                !Finite(data.holdSeconds) || data.holdSeconds <= 0 || data.holdSeconds > 10 ||
                !Finite(data.reuseDelaySeconds) || data.reuseDelaySeconds < 0 || data.reuseDelaySeconds > 60 ||
                !Finite(data.interactionReach) || data.interactionReach <= 0 || data.interactionReach > 10)
                throw new ArgumentException("Invalid stage recovery tuning.", nameof(data));
            if (data.prices == null || data.prices.Length != data.usesPerStage || Array.Exists(data.prices, p => p < 0))
                throw new ArgumentException("Recovery prices must specify every use.", nameof(data));
            Capacity = data.usesPerStage; HealthFraction = data.healthFraction;
            prices = (int[])data.prices.Clone();
            HoldSeconds = data.holdSeconds;
            ReuseDelaySeconds = data.reuseDelaySeconds; Reach = data.interactionReach;
            Reset();
        }

        public bool Ready(float now) => Remaining > 0 && Finite(now) && now >= nextUse;

        // Returns the actual heal. No charge or cooldown is spent on full health, death or invalid input.
        public bool TryUse(float health, float maximum, float now, out float restored, Func<int, bool> tryPay = null)
        {
            restored = 0;
            if (!Ready(now) || !Finite(health) || !Finite(maximum) || health <= 0 || maximum <= health) return false;
            float amount = Math.Min(maximum - health, maximum * HealthFraction);
            if (amount <= 0 || tryPay != null && !tryPay(Price)) return false;
            restored = amount;
            Remaining--; nextUse = now + ReuseDelaySeconds;
            return true;
        }

        public void Reset() { Remaining = Capacity; nextUse = float.NegativeInfinity; }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    // Completing a hold reports once; releasing, losing admission or receiving any hit cancels without spending.
    public sealed class StageRecoveryHold
    {
        readonly float seconds;
        float started;
        int receivedHits;
        public bool Holding { get; private set; }
        public StageRecoveryHold(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0 || seconds > 10)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            this.seconds = seconds;
        }
        public bool Begin(float now, int hits)
        {
            if (Holding || float.IsNaN(now) || float.IsInfinity(now)) return false;
            started = now; receivedHits = hits; Holding = true; return true;
        }
        public float Progress(float now) => Holding ? Math.Max(0, Math.Min(1, (now - started) / seconds)) : 0;
        public bool Advance(float now, bool held, bool allowed, int hits)
        {
            if (!Holding) return false;
            if (!held || !allowed || hits != receivedHits || float.IsNaN(now) || float.IsInfinity(now) || now < started)
            { Cancel(); return false; }
            if (now - started < seconds) return false;
            Cancel(); return true;
        }
        public void Cancel() => Holding = false;
    }
}
