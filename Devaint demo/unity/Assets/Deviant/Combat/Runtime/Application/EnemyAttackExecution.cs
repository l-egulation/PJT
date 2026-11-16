using System.Collections.Generic;
using Deviant.Combat.Contracts;

namespace Deviant.Combat.Execution
{
    // Owns timing and duplicate-hit history, but neither a second clock nor
    // target/native state. The adapter calls Step only after its existing motor.
    public sealed class EnemyAttackExecution
    {
        float nextAttack, attackStart = -100;
        int hitIndex, attackSequence;
        bool attacking;
        readonly HashSet<long> hits = new HashSet<long>();
        public EnemyAttackSnapshot Read() => new EnemyAttackSnapshot(attacking, attackStart, nextAttack, attackSequence, hitIndex);
        public void Begin(float now)
        { attacking = true; attackStart = now; hitIndex = 0; attackSequence++; }
        public void DelayMotion(float seconds) { if (attacking && seconds > 0) attackStart += seconds; }
        public void Cancel(float now) { attacking = false; nextAttack = now + .3f; }
        public void Reset(float now) { attacking = false; nextAttack = now + .5f; }
        public bool FirstHit(int hit, int targetId)
        {
            int key = attackSequence * 4 + hit;
            return hits.Add(((long)key << 32) | (uint)targetId);
        }
        public void Step(float dt, bool controlled, IEnemyAttackHost host)
        {
            if (!attacking || controlled) return;
            float elapsed = host.Now - attackStart;
            int count = host.Profile == 2 ? 3 : 1;
            float first = host.Profile == 2 ? .45f : .35f;
            for (int i = 0; i < count; i++)
            {
                float at = first + i * .2f;
                if (!(elapsed + dt >= at && elapsed < at + .08f)) continue;
                if (host.Profile == 1)
                {
                    // Keep assignment after emission: callbacks observe the
                    // original unclaimed flag, including synchronous reentry.
                    if (hitIndex == 0) { host.FireProjectile(); hitIndex = 1; }
                }
                else host.MeleeHit(i);
            }
            // Do not stop an already-entered loop after callback cancellation.
            // Legacy completion compares the cached current elapsed, not dt's
            // end, and obtains cooldown time/profile after all native effects.
            if (elapsed >= first + (count - 1) * .2f + .6f)
            { attacking = false; nextAttack = host.Now + (host.Profile == 2 ? .7f : .5f); }
        }
    }
}
