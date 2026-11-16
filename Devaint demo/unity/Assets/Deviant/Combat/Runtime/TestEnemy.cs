using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using UnityEngine;

namespace Deviant.Combat
{
    public partial class TestEnemy : CombatActor, IEnemyAttackHost
    {
        public int profile;
        public bool aggressive;
        public Vector2 spawn;
        // Colour of a boss's long health bar along the bottom of the view; a boss may change it per phase.
        public Color bossBarColor = new Color(.86f, .2f, .17f);
        readonly EnemyAttackExecution attack = new EnemyAttackExecution();
        public float AttackElapsed => attack.Read().Elapsed(arena.Now);
        public virtual float Telegraph { get { var state = attack.Read(); return state.Attacking ? state.Telegraph(profile, arena.Now) : -1; } }
        public override void CancelAttack() { base.CancelAttack(); attack.Cancel(arena.Now); EndAttackCommand(CombatCommandState.Cancelled, resettingCommands ? CombatCommandReason.Reset : CombatCommandReason.Interrupted); }
        public virtual void Step(float dt)
        {
            var host = new EnemyTickHost(this);
            try { EnemyTickExecution.Step(dt, ref host); }
            catch (System.Exception exception) { EndAttackCommand(CombatCommandState.Failed, CombatCommandReason.Exception, exception.Message); throw; }
            if (!attack.Read().Attacking) EndAttackCommand(CombatCommandState.Completed);
        }
        public virtual void DelayRunInkMotion(float seconds) => attack.DelayMotion(seconds);
        int IEnemyAttackHost.Profile => profile;
        float IEnemyAttackHost.Now => arena.Now;
        void IEnemyAttackHost.FireProjectile()
        { arena.FireEnemyProjectile(this, Center + Vector2.right * facing * .8f, new Vector2(facing * 8, 0), 8, AttackHitOrigin(0, CombatHitKind.Projectile)); }
        void IEnemyAttackHost.MeleeHit(int hitIndex)
        {
            var host = new EnemyMeleeHitHost(this, AttackHitOrigin(hitIndex));
            EnemyTickExecution.MeleeHit(hitIndex, ref host);
        }
        public virtual void ResetEnemy()
        { resettingCommands = true; ResetActor(spawn); attack.Reset(arena.Now); EndChaseCommand(CombatCommandState.Cancelled, CombatCommandReason.Reset); resettingCommands = false; if (visual) visual.color = profile == 2 ? new Color(.8f,.35f,.3f) : profile == 1 ? Color.magenta : new Color(.95f,.65f,.2f); }
    }

    public partial class ArenaProjectile : MonoBehaviour, IEnemyProjectileExecutionHost
    {
        public CombatArena arena;
        public CombatActor source;
        public Vector2 velocity;
        // Per-instance opt-in; legacy enemy shots retain their 0.25-unit square.
        public Vector2 CollisionSize { get; set; } = Vector2.one * .25f;
        internal CombatHitOrigin HitOrigin { get; private set; }
        EnemyProjectileExecution execution;
        public EnemyProjectileSnapshot Execution => execution.Read();
        public void Initialize(CombatArena ownerArena, CombatActor owner, Vector2 at, Vector2 initialVelocity, float damage, float expires, CombatHitOrigin hitOrigin=default)
        {
            arena = ownerArena; source = owner; transform.position = at; velocity = initialVelocity;
            CollisionSize = Vector2.one * .25f;
            execution = new EnemyProjectileExecution(damage, expires);
            HitOrigin = (hitOrigin.IsSpecified ? hitOrigin : arena.DirectHitOrigin(owner)).WithEffect(System.Guid.NewGuid(), CombatHitKind.Projectile);
        }
        public void Step(float dt) => execution.Step(dt, this);
        public void Release() => execution.Release(this);
        float IEnemyProjectileExecutionHost.Now => arena.Now;
        bool IEnemyProjectileExecutionHost.SourceExists => source;
        bool IEnemyProjectileExecutionHost.SourceDead => source.Dead;
        bool IEnemyProjectileExecutionHost.SourceIsPlayer => source.isPlayer;
        void IEnemyProjectileExecutionHost.Sweep(float dt)
        {
            var host = new SweepHost(this);
            EnemyProjectileSweepExecution.Sweep(dt, ref host);
        }
        struct Contact : IEnemyProjectileContactHost
        {
            readonly ArenaProjectile projectile;
            readonly CombatActor target;
            readonly Vector2 previous;
            CombatHitOrigin reflector;
            public Contact(ArenaProjectile projectile, CombatActor target, Vector2 previous)
            { this.projectile = projectile; this.target = target; this.previous = previous; reflector = default; }
            public bool TryParry()
            {
                var cast = target.currentCast;
                reflector = cast ? cast.HitOrigin(-1, CombatHitKind.Parry) : default;
                return cast && cast.TryParry(projectile.source, true, previous);
            }
            public void ReflectSourceAndVelocity()
            {
                projectile.HitOrigin = projectile.HitOrigin.ReflectedBy(reflector);
                projectile.source = target; projectile.velocity = -projectile.velocity;
            }
            public float SpecialBonus => projectile.arena.Player.Weapon.SpecialBonus;
            public void RestoreOrigin() => projectile.transform.position = previous;
            public void Receive(float damage) => target.Receive(damage, projectile.source, projectile.velocity.x < 0 ? -1 : 1, false, null, false, true, projectile.HitOrigin);
            public void Remove() => projectile.Remove();
        }
        // 반송 봉투 (maintenance augment): the player's hit box returned this shot.
        internal void ReflectByAugment(CombatActor player, CombatHitOrigin reflector, float damageMultiplier)
        {
            if (!execution.TryReflect(damageMultiplier * CombatArena.LoadRunInkCatalog().damageUnitScale)) return;
            HitOrigin = HitOrigin.ReflectedBy(reflector);
            source = player; velocity = -velocity;
        }
        void IEnemyProjectileExecutionHost.Remove() => Remove();
        void Remove() { arena.projectiles.Remove(this); Destroy(gameObject); }
    }
}
