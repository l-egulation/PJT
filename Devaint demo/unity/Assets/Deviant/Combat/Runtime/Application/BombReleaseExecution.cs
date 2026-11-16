using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Definitions;

namespace Deviant.Combat.Execution
{
    // Embedded privately by the cast. The only mutable fact is the claimed
    // authored-hit cursor; no second activation, cancellation or clock exists.
    public struct BombReleaseExecution
    {
        int nextBomb;
        public BombReleaseSnapshot Read() => new BombReleaseSnapshot(nextBomb);
        public void ReleaseDue<T>(IReadOnlyList<SkillHitDefinition> hits, ref T host) where T : struct, IBombReleaseHost
        {
            if (host.Cancelled || !host.OwnerExists || host.OwnerDead) return;
            while (nextBomb < hits.Count && host.Elapsed + .000001f >= hits[nextBomb].Time + host.ChargeShift)
            {
                // Claim before entering Native spawn/damage callbacks. Reentry
                // and exceptions retain it, while the next condition reads live time.
                int index = nextBomb++; var hit = hits[index];
                host.SpawnBomb(index, hit.Damage);
            }
        }
    }
}
