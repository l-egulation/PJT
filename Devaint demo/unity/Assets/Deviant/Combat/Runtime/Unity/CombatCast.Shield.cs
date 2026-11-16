using UnityEngine;

namespace Deviant.Combat
{
    public partial class CombatCast
    {
        bool shieldParrySucceeded;
        internal bool ShieldParrySucceeded => shieldParrySucceeded;
        bool TryClaimShieldParry()
        {
            if (Definition.Id != "shield_default") return true;
            if (shieldParrySucceeded) return false;
            shieldParrySucceeded = true;
            var rules = ShieldRuntimeRules.Current;
            weapon.Cooldown.Rearm(rules.parrySuccessCooldownSeconds, arena.Now);
            lifecycle.SetMotionEnd(Mathf.Min(lifecycle.MotionEnd, Elapsed + rules.parrySuccessRecoverySeconds));
            return true;
        }
        public bool TryGetShieldParryBounds(out Bounds bounds)
        {
            bounds = default;
            if (!owner || Definition.Id != "shield_default" || shieldParrySucceeded || !CastActivated || CastCancelled || Definition.Effect == null || Definition.Effect.Type != "parry" ||
                Elapsed < 0 || Elapsed >= Definition.Effect.ParryWindow || !owner.hurtCollider) return false;
            bounds = owner.hurtCollider.bounds;
            float reach = ShieldRuntimeRules.Current.parryProjectileReach;
            bounds.center += Vector3.right * (direction * reach * .5f);
            bounds.size += Vector3.right * reach;
            return true;
        }

        internal void KeepShieldTargetAhead(TestEnemy enemy)
        {
            if (Definition.Id != "shield_movement" || !Moving || CastCancelled || !enemy || enemy.Dead || enemy.isBoss || enemy.Immovable) return;
            var hit = Definition.Hits[0].Box;
            var body = enemy.bodyCollider.bounds;
            float face = owner.Position.x + direction * (hit.X + hit.Width * .5f);
            float near = direction > 0 ? body.min.x : body.max.x;
            float amount = (face - near) * direction;
            if (amount > 0)
            {
                var delta = Vector2.right * direction * amount;
                float travel = arena.SolidTravel(body.center, body.size, delta.normalized, amount);
                enemy.Position += Vector2.right * direction * travel;
                Physics2D.SyncTransforms();
            }
        }
    }
}
