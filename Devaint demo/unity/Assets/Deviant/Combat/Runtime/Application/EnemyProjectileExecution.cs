using System.Collections.Generic;
using Deviant.Combat.Contracts;

namespace Deviant.Combat.Execution
{
    public sealed class EnemyProjectileExecution
    {
        readonly float expires;
        float damage;
        bool reflected, removed;
        readonly HashSet<int> hits = new HashSet<int>();

        public EnemyProjectileExecution(float damage, float expires)
        { this.damage = damage; this.expires = expires; }
        public EnemyProjectileSnapshot Read() => new EnemyProjectileSnapshot(expires, damage, reflected, removed);

        public bool AcceptsTarget(bool dead, bool isSource, bool isPlayer, bool isDecoy)
            => !(dead || isSource || reflected && (isPlayer || isDecoy) || !reflected && !isPlayer && !isDecoy);

        public void Step(float duration, IEnemyProjectileExecutionHost host)
        {
            if (removed) return;
            if (host.Now >= expires || !host.SourceExists || host.SourceDead && host.SourceIsPlayer)
            { Release(host); return; }
            host.Sweep(duration);
        }

        // Native filtering/occlusion precede this claim. A rejected Receive or a
        // callback exception retains it, just like the original hit ledger.
        public bool TryContact<T>(int actorId, bool isPlayer, ref T host, float reflectionDamageScale = 1) where T : struct, IEnemyProjectileContactHost
        {
            if (removed || !hits.Add(actorId)) return false;
            if (!reflected && isPlayer && host.TryParry())
            {
                reflected = true;
                host.ReflectSourceAndVelocity();
                damage *= reflectionDamageScale * (1 + host.SpecialBonus);
                hits.Clear(); hits.Add(actorId);
                host.RestoreOrigin();
                return true;
            }
            host.Receive(damage);
            if (!removed) { removed = true; host.Remove(); }
            return true;
        }

        // A run augment returns the shot without a parry: it keeps flying, now against enemies, with scaled damage.
        public bool TryReflect(float damageMultiplier)
        {
            if (removed || reflected) return false;
            reflected = true; damage *= damageMultiplier; hits.Clear();
            return true;
        }

        public void Release(IEnemyProjectileExecutionHost host)
        {
            if (removed) return;
            removed = true;
            host.Remove();
        }
    }
}
