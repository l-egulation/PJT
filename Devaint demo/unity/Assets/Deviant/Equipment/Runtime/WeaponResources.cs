using System;
using Deviant.Equipment.Contracts;

namespace Deviant.Equipment
{
    // Public composition entry; consumers receive commands/immutable queries,
    // never the mutable charge owner. No Combat or engine dependency.
    public static partial class WeaponResources
    {
        public static IWeaponCharges CreateCharges(int capacity, float period) => new SequentialCharges(capacity, period);
        sealed class SequentialCharges : IWeaponCharges, IWeaponChargeClock, IWeaponChargeRearm
        {
            int capacity, available;
            float nextReady, period, minimumReady, lastNow;
            public SequentialCharges(int capacity, float period) { Reset(capacity, period); }
            public ChargeState Read() => new ChargeState(capacity, lastNow < minimumReady ? 0 : available,
                period, Math.Max(nextReady, minimumReady));
            public void Rebase(float previousNow, float nextNow)
            {
                if (float.IsNaN(previousNow) || float.IsInfinity(previousNow) || previousNow < 0 ||
                    float.IsNaN(nextNow) || float.IsInfinity(nextNow) || nextNow < 0) throw new ArgumentOutOfRangeException(nameof(nextNow));
                if (available < capacity) nextReady += nextNow - previousNow;
                if (minimumReady > 0) minimumReady = Math.Max(0, minimumReady + nextNow - previousNow);
                lastNow = nextNow;
            }
            public void Reset(int capacity, float period)
            { this.capacity = Math.Max(1, capacity); available = this.capacity; this.period = Math.Max(.001f, period); nextReady = minimumReady = lastNow = 0; }
            public void Tick(float now)
            {
                lastNow = now;
                while (available < capacity && now + .00001f >= Math.Max(nextReady, minimumReady))
                { available++; nextReady += period; }
            }
            public bool Spend(float now)
            {
                Tick(now);
                if (available <= 0 || now < minimumReady) return false;
                if (available == capacity) nextReady = now + period;
                available--;
                return true;
            }
            public void Refund(float seconds, float now)
            {
                if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0 || float.IsNaN(now) || float.IsInfinity(now) || now < 0)
                    throw new ArgumentOutOfRangeException(nameof(seconds));
                Tick(now); if (available == capacity) return;
                nextReady -= seconds; Tick(now);
            }
            public void Rearm(float seconds, float now)
            {
                if (!(seconds > 0) || float.IsInfinity(seconds) || float.IsNaN(now) || float.IsInfinity(now) || now < 0)
                    throw new ArgumentOutOfRangeException(nameof(seconds));
                Tick(now);
                minimumReady = now + seconds;
                if (available < capacity) nextReady = minimumReady;
            }
            public float Debt(float now)
            {
                Tick(now);
                if (available == capacity) return 0;
                return (capacity - available - 1 + Math.Max(0, nextReady - now) / period) / capacity;
            }
            public void Reconfigure(int capacity, float period, float now)
            {
                float debt = Debt(now);
                this.capacity = Math.Max(1, capacity); this.period = Math.Max(.001f, period);
                float units = debt * this.capacity;
                int missing = units < .00001f ? 0 : (int)Math.Ceiling(units - .00001f);
                available = this.capacity - missing;
                nextReady = missing == 0 ? 0 : now + Math.Max(0, units - (missing - 1)) * this.period;
            }
        }
    }
}
