using System.Collections.Generic;
using Deviant.Combat.Contracts;
using UnityEngine;

namespace Deviant.Combat
{
    public partial class TestEnemy
    {
        struct EnemyTickHost : IEnemyTickHost
        {
            readonly TestEnemy enemy;
            CombatActor target;
            public EnemyTickHost(TestEnemy enemy) { this.enemy = enemy; target = null; }
            public bool Dead => enemy.Dead;
            public void CaptureTarget() => target = enemy.arena.EnemyTarget(enemy);
            public float Now => enemy.arena.Now;
            public float StunUntil => enemy.stunUntil;
            public float StaggerUntil => enemy.staggerUntil;
            public EnemyAttackSnapshot ReadAttack() => enemy.attack.Read();
            public int Profile => enemy.profile;
            public bool Aggressive => enemy.aggressive;
            public bool HasTarget => target;
            public float TargetDeltaX => target.Center.x - enemy.Center.x;
            public float TargetDeltaY => target.Center.y - enemy.Center.y;
            public float ApplyIntent(EnemyIntent intent) => enemy.SubmitIntent(intent).Horizontal;
            public void PhysicsStep(float step, float horizontal, bool freezeVertical)
            {
                try { enemy.PhysicsStep(step, horizontal, freezeVertical); enemy.EndChaseCommand(CombatCommandState.Completed); }
                catch (System.Exception exception) { enemy.EndChaseCommand(CombatCommandState.Failed, CombatCommandReason.Exception, exception.Message); throw; }
            }
            public void StepAttack(float step, bool controlled) => enemy.attack.Step(step, controlled, enemy);
        }

        struct EnemyMeleeHitHost : IEnemyMeleeHitHost
        {
            readonly TestEnemy enemy;
            Vector2 center;
            List<CombatActor>.Enumerator targets;
            CombatActor target;
            readonly CombatHitOrigin hitOrigin;
            public EnemyMeleeHitHost(TestEnemy enemy, CombatHitOrigin hitOrigin) { this.enemy = enemy; this.hitOrigin = hitOrigin; center = default; targets = default; target = null; }
            public void BeginTargets()
            {
                center = enemy.Center + Vector2.right * enemy.facing * (enemy.size.x / 2 + .65f);
                targets = enemy.arena.TargetsInBox(center, new Vector2(1.6f, 2)).GetEnumerator();
            }
            public bool NextTarget()
            {
                if (!targets.MoveNext()) return false;
                target = targets.Current; return true;
            }
            public bool TargetIsNotPlayer => target != enemy.arena.Player;
            public bool TargetIsDecoy => target.isDecoy;
            public bool TargetDead => target.Dead;
            public bool TargetOccluded => enemy.arena.Occluded(enemy.Center, target.Center);
            public bool FirstHit(int hitIndex) => enemy.attack.FirstHit(hitIndex, target.ActorId);
            public int Profile => enemy.profile;
            public void Receive(float damage) => target.Receive(damage, enemy, enemy.facing, false, null, origin: hitOrigin);
            public void EndTargets() => targets.Dispose();
            public bool ShowBoxes => enemy.arena.showBoxes;
            public void AddFlash() => enemy.arena.AddFlash(center, new Vector2(1.6f, 2), .08f, Color.red);
        }
    }
}
