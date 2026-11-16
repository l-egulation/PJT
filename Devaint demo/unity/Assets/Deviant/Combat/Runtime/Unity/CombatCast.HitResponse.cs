using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using Deviant.Definitions;

namespace Deviant.Combat
{
    public partial class CombatCast
    {
        CastHitResponseExecution hitResponse;
        float ambushBonus;
        public float AmbushBonus => ambushBonus;

        struct CastHitResponseHost : ICastHitResponseHost
        {
            readonly CombatCast cast;
            readonly CombatActor target;
            readonly SkillHitDefinition hit;
            readonly int index;
            readonly CombatHitOrigin hitOrigin;
            readonly CombatHitContact contact;
            public CastHitResponseHost(CombatCast cast, int index = 0, SkillHitDefinition hit = null, CombatActor target = null, CombatHitOrigin hitOrigin = default, CombatHitContact contact = default)
            { this.cast = cast; this.index = index; this.hit = hit; this.target = target; this.hitOrigin = hitOrigin.IsSpecified ? hitOrigin : cast.HitOrigin(index); this.contact = contact; }
            public bool TryClaimTarget() => cast.chargedMeleeExecution != null ? cast.chargedMeleeExecution.TryClaimTarget(target.ActorId) :
                cast.meleeAction != null ? cast.meleeAction.TryClaimTarget(target.ActorId) : cast.ledger.First(index, target.ActorId);
            public float HitDamage => cast.charge?.Release?.Damage ?? hit.Damage;
            public float DamageMultiplier => cast.damageMultiplier;
            public bool ReceiveControl(float damage)
            {
                // 기습 피해는 기준 피해에 더해 같은 잉크 배율을 받고, 처음 받아들여진 적중 한 번에만 쓴다.
                float ambush = cast.ambushBonus;
                bool accepted = target.ReceiveControl(damage + ambush * cast.damageMultiplier, cast.owner, cast.direction, cast.isBasic, hit, origin: hitOrigin, contact: contact);
                if (accepted && ambush > 0) cast.ambushBonus = 0;
                // 표시 전용: 채찍 스킬이 건 속박의 출처만 기록한다(감김 그림·땡기기 유지). 속박 값은 위 호출이 이미 정했다.
                if (accepted && hit != null && hit.Root > 0) cast.arena.RecordWhipRoot(target, cast);
                if (accepted) cast.RememberPrimaryTarget(index, target);
                return accepted;
            }
            public bool TrackPrimaryTarget => hit.Targets == "nearest" || !cast.targetHit && index == 0 &&
                (cast.Definition.Effect?.Type == "target_mark" || cast.Definition.Movement?.Type == "target_dash_recast");
            public void MarkTarget() { if (!cast.targetHit) cast.marked = target; cast.targetHit = true; }
            public void MarkApproachAccepted()
            {
                var host = new ApproachRecastHost(cast); cast.approachRecast.MarkAccepted(ref host);
            }
            public bool HasResponseMovement => cast.ChargedMelee == null && cast.Definition.Movement != null;
            public bool PrimaryPush => cast.Definition.Movement.PrimaryPush;
            public bool MarkedTargetExists => cast.marked;
            public bool TargetIsBoss => target.isBoss;
            public bool TargetIsMarked => target == cast.marked;
            public int TargetActorId => target.ActorId;
            public bool PushWithCaster => hit.PushWithCaster;
            public float EndForcedDashAtBoss() => cast.forcedDash.EndAtPrimaryBoss(cast.Elapsed, cast.dt);
            public void SetMotionEnd(float end) => cast.lifecycle.SetMotionEnd(end);
            public bool Cancelled => cast.CastCancelled;
            public bool MovementExists => cast.Definition.Movement != null;
            public bool MarkedTargetAlive => cast.Definition.Movement?.PrimaryPush == true
                ? cast.HasLivingPrimaryTarget : cast.marked && !cast.marked.Dead;
            public float EndDamage => cast.Definition.Movement.EndDamage;
            public void ReceiveMarked(float damage) => cast.ReceivePrimaryEnds(damage);
        }

        struct CastPushHost : ICastPushHost
        {
            readonly CombatCast cast;
            List<TestEnemy>.Enumerator enemies;
            public CastPushHost(CombatCast cast) { this.cast = cast; enemies = default; }
            public bool Moving => cast.Moving;
            public bool Cancelled => cast.CastCancelled;
            public void BeginEnemies() => enemies = cast.arena.enemies.GetEnumerator();
            public bool NextEnemy() => enemies.MoveNext();
            public void EndEnemies() => enemies.Dispose();
            public bool EnemyExists => enemies.Current;
            public bool EnemyDead => enemies.Current.Dead;
            public int EnemyActorId => enemies.Current.ActorId;
            public float Now => cast.arena.Now;
            public float MovementSpeed => cast.Definition.Movement.Speed;
            public int Direction => cast.direction;
            public void SetEnemyKnockMotion(float until, float speed)
            {
                var enemy = enemies.Current;
                enemy.SetKnockMotion(until, speed);
                if (cast.Definition.Id == "shield_movement")
                { enemy.CancelAttack(); cast.KeepShieldTargetAhead(enemy); }
            }
        }
    }
}
