using System;
using Deviant.Combat.Contracts;
using UnityEngine;

namespace Deviant.Combat
{
    // Opt-in per enemy; dedicated knockback is applied only after an accepted hit.
    public interface ICustomPlayerHitReactionSource { bool OverridesPlayerHitReaction {get;} }

    [Serializable] public sealed class PlayerHitReactionSettings
    {
        public int schema;
        public bool enabled,meleeAttackBoxExit;
        public float seconds,minimumDistance,maximumDistance,attackBoxMargin;
        public void Validate()
        {
            if(schema!=1||!Finite(seconds)||seconds<.05f||seconds>.4f||!Finite(minimumDistance)||minimumDistance<0||
                !Finite(maximumDistance)||maximumDistance<minimumDistance||maximumDistance>6||!Finite(attackBoxMargin)||attackBoxMargin<0||attackBoxMargin>1)
                throw new ArgumentException("Invalid player hit reaction.");
        }
        static bool Finite(float n)=>!float.IsNaN(n)&&!float.IsInfinity(n);
        public static PlayerHitReactionSettings Load()
        {
            var text=Resources.Load<TextAsset>("PlayerHitReaction");if(!text)throw new InvalidOperationException("Export the player hit reaction source.");
            var result=JsonUtility.FromJson<PlayerHitReactionSettings>(text.text);result.Validate();return result;
        }
        public float Distance(Bounds player,Bounds attack,int direction)
        {
            float exit=direction>0?attack.max.x-player.min.x+attackBoxMargin:player.max.x-attack.min.x+attackBoxMargin;
            return Mathf.Clamp(exit,minimumDistance,maximumDistance);
        }
    }
    public partial class CombatArena
    {
        public PlayerHitReactionSettings PlayerHitReaction {get;private set;}
        public float LastPlayerKnockDistance {get;private set;}
        internal void ApplyPlayerHitReaction(CombatActor target,CombatActor source,CombatHitOrigin hit)
        {
            if(source is ICustomPlayerHitReactionSource custom&&custom.OverridesPlayerHitReaction)return;
            var policy=PlayerHitReaction;
            if(policy==null||!policy.enabled||target!=Player||!source||target.IsForcedMoving||target.Dead||Now<target.knockUntil)return;
            int direction=target.Center.x>=source.Center.x?1:-1;
            float distance=policy.minimumDistance;
            if(policy.meleeAttackBoxExit&&hit.Kind==CombatHitKind.Melee&&source is FieldMouseEnemy monster)
                distance=policy.Distance(target.hurtCollider.bounds,monster.AttackBounds,direction);
            distance*=PlayerKnockbackTakenScale;
            LastPlayerKnockDistance=distance;
            // Existing swept movement still limits each step against solid walls.
            // PlayerHit observes this future deadline and retains this knock motion.
            target.SetKnockMotion(Now+policy.seconds,direction*distance/policy.seconds);
        }
    }
}
