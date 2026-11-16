using UnityEngine;

namespace Deviant.Combat
{
    public sealed class ShieldDecoyRuntime : MonoBehaviour
    {
        CombatActor actor, owner;
        CombatArena arena;
        int ownerLife;
        bool finished;
        public bool BurstDone { get; private set; }
        public void Bind(CombatActor value, CombatActor source)
        { actor = value; owner = source; arena = value.arena; ownerLife = source.LifeVersion; }
        public void Discard() { finished = true; }
        public void Evaluate(bool expired)
        {
            if (finished || !actor || !owner || owner.Dead || owner.LifeVersion != ownerLife) return;
            var rules = ShieldRuntimeRules.Current;
            if (!(expired ? rules.decoyBurstOnExpiry : actor.Dead && rules.decoyBurstOnDamageDeath)) return;
            finished = BurstDone = true;
            var center = actor.Center;
            arena.ShieldPresentation?.Burst(actor.Position, owner.facing);
            arena.Record("shield decoy burst");
            // Snapshot membership: damage callbacks may mutate the arena's live list.
            foreach (var enemy in arena.enemies.ToArray())
            {
                if (!enemy || enemy.Dead || !enemy.hurtCollider) continue;
                if (Vector2.Distance(center, enemy.hurtCollider.ClosestPoint(center)) <= rules.decoyRadius)
                    enemy.ReceiveShieldRetaliation(rules.decoyDamage, owner, enemy.Center.x >= center.x ? 1 : -1);
            }
        }
    }
}
