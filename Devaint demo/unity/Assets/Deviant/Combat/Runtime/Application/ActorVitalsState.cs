using System;
using Deviant.Combat.Contracts;

namespace Deviant.Combat.Execution
{
    // Sole scalar owner for one runtime actor. Eligibility/parry and hit/death
    // callbacks remain in their caller and are never invoked by this state.
    public sealed class ActorVitalsState : IActorVitals
    {
        float maximum, health, shield, guardUntil, guardReduction;
        int receivedHits;
        public ActorVitalsState(float maximum) { Check(maximum); this.maximum = health = maximum; }
        public ActorVitalsSnapshot Read() => new ActorVitalsSnapshot(maximum, health, shield, guardUntil, guardReduction, receivedHits);
        public void WriteMaximum(float value) { Check(value); maximum = value; }
        public void WriteHealth(float value) { Check(value); health = value; }
        public void WriteShield(float value) { Check(value); shield = value; }
        public void WriteGuardUntil(float value) { Check(value); guardUntil = value; }
        public void WriteGuardReduction(float value) { Check(value); guardReduction = value; }
        public ActorDamageResult ApplyDamage(float damage, float now, bool bypass)
        {
            Check(damage); CheckTime(now);
            if (health <= 0) return new ActorDamageResult(false, 0, 0, Read());
            float nextShield = shield, absorbed = 0;
            if (!bypass && now < guardUntil) damage *= 1 - guardReduction;
            if (!bypass && nextShield > 0) { absorbed = Math.Min(nextShield, damage); nextShield -= absorbed; damage -= absorbed; }
            float nextHealth = Math.Max(0, health - damage);
            Check(damage); Check(absorbed); Check(nextShield); Check(nextHealth);
            health = nextHealth; shield = nextShield; receivedHits++;
            return new ActorDamageResult(true, damage, absorbed, Read());
        }
        public void ExpireShield(float now)
        {
            CheckTime(now); if (shield > 0 && now >= guardUntil) shield = 0;
        }
        public void Reset()
        {
            health = maximum; shield = 0; guardUntil = 0; receivedHits = 0;
            // Legacy reset retains the reduction value; with no guard/shield it
            // has no active effect and is replaced by the next guard binding.
        }
        static void Check(float value)
        { if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value), "Vitals values must be finite."); }
        static void CheckTime(float now) { Check(now); if (now < 0) throw new ArgumentOutOfRangeException(nameof(now)); }
    }
}
