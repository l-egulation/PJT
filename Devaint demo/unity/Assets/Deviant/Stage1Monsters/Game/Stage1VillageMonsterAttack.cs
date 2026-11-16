using System.Collections.Generic;
using Deviant.Combat;
using UnityEngine;

namespace Deviant.Stage1Monsters.Game
{
    // Provisional attacks; every hit still enters the arena's accepted-hit/command boundary.
    public sealed class Stage1VillageMonsterAttack : MonoBehaviour
    {
        public FieldMouseEnemy Actor { get; private set; }
        public VillageMonsterSpec Spec { get; private set; }
        public CombatActor Grabbed { get; private set; }
        public int ShotsFired { get; private set; }
        public int HitsDelivered { get; private set; }
        public float HookDistance { get; private set; }
        public Stage1EliteBurrow Burrow { get; set; }
        public bool DirectClaw { get; private set; }
        public float MotionSpeed { get; private set; }=1;
        public float MotionOffset { get; private set; }
        public float MotionAge=>Authored(MotionOffset+Actor.SpeciesAttackAge*MotionSpeed);
        // Wind-up telegraph: the approved hold frame stays on screen HoldExtra seconds longer, and every authored
        // time after it (frames, damage window, stroke effect) moves with it. Brain prepare grows by the same amount.
        public float HoldExtra=>Spec.prepareHoldFrame<0?0:PrepareHoldCompare.Extra(Spec.prepareHoldExtra);
        float Authored(float age)
        {
            float extra=HoldExtra;if(extra<=0)return age;
            float holdEnd=0;var frames=Spec.definition.attackFrameDurations;
            for(int i=0;i<=Spec.prepareHoldFrame;i++)holdEnd+=frames[i];
            return age<holdEnd?age:age<holdEnd+extra?holdEnd-.0001f:age-extra;
        }
        void BeginHeldAttack(FieldMouseEnemy actor)
        {
            float extra=HoldExtra;
            Spec.definition.prepare+=extra-Spec.appliedHold;Spec.appliedHold=extra;
        }
        int execution=-1;
        double startedAt=-1;
        bool fired,hookConnected;
        readonly HashSet<int> struck=new HashSet<int>();

        public void Bind(FieldMouseEnemy actor,VillageMonsterSpec spec)
        {
            Actor=actor;Spec=spec;actor.SpeciesAttackStep=Step;
            if(spec.attackKind=="hook-claw")actor.SpeciesAttackStarting=BeginEliteAttack;
            else if(spec.prepareHoldFrame>=0)actor.SpeciesAttackStarting=BeginHeldAttack;
            actor.arena.ResetCompleted+=Reset;
        }
        void Reset()
        {
            execution=-1;startedAt=-1;Grabbed=null;HookDistance=0;hookConnected=fired=false;
            DirectClaw=false;MotionSpeed=1;MotionOffset=0;
            struck.Clear();ShotsFired=HitsDelivered=0;
        }
        void OnDestroy() { if(Actor && Actor.arena)Actor.arena.ResetCompleted-=Reset; }
        void OnDisable() { Grabbed=null;HookDistance=0; }

        void BeginEliteAttack(FieldMouseEnemy actor)
        {
            actor.SyncFacing(actor.Brain.Facing);
            var target=actor.Focus;
            DirectClaw=ValidTarget(target) && actor.DamageExposed(target);
            if(Burrow)Burrow.Cancel();
            // A nearby target always gets the existing claw; otherwise retain the burrow cooldown.
            if(!DirectClaw && Burrow)Burrow.BeginExecution(actor.Brain.Execution+1,actor.arena.Now);
            bool underground=Burrow && Burrow.Active;
            MotionSpeed=DirectClaw || underground?1:Spec.hookSpeed;
            MotionOffset=DirectClaw?Spec.clawStart:0;
            var definition=actor.Definition;
            definition.prepare=((DirectClaw?Spec.damageStart:Spec.definition.prepare)-MotionOffset)/MotionSpeed;
            definition.active=(Spec.damageEnd-MotionOffset)/MotionSpeed-definition.prepare;
            definition.recovery=Spec.definition.recovery/MotionSpeed;
        }

        void Step(FieldMouseEnemy actor,float now,float dt)
        {
            if(!enabled || actor.Dead || !actor.Brain.Executing)
            { Grabbed=null;HookDistance=0;return; }
            if(execution!=actor.Brain.Execution || startedAt!=actor.Brain.StartedAt)
            {
                execution=actor.Brain.Execution;startedAt=actor.Brain.StartedAt;
                Grabbed=null;HookDistance=0;hookConnected=fired=false;struck.Clear();
            }
            float from=Mathf.Max(0,now-(float)startedAt),to=from+dt;
            if(Burrow && Burrow.Active) { Burrow.Step(from,to);return; }
            from=Authored(MotionOffset+from*MotionSpeed);to=Authored(MotionOffset+to*MotionSpeed);
            if(Spec.attackKind=="spit")
            {
                if(!fired && to>=Spec.damageStart)
                {
                    fired=true;
                    var muzzle=actor.Position+new Vector2(actor.facing*Spec.muzzleOffset[0],Spec.muzzleOffset[1]);
                    // Do not emit through a wall between the torso and the nozzle.
                    if(actor.arena.Occluded(actor.Center,muzzle,false))return;
                    actor.FireSpeciesProjectile(muzzle,new Vector2(actor.facing*Spec.projectileSpeed,0),actor.Definition.damage,0);
                    var projectiles=actor.arena.projectiles;
                    if(projectiles.Count>0)
                    {
                        var shot=projectiles[projectiles.Count-1];
                        if(Spec.projectileSize!=null && Spec.projectileSize.Length==2)
                            shot.CollisionSize=new Vector2(Spec.projectileSize[0],Spec.projectileSize[1]);
                        Stage1VillageProjectileVisual.Attach(shot);
                    }
                    ShotsFired++;
                }
                return;
            }
            if(Spec.attackKind=="hook-claw" && !DirectClaw)
            {
                Hook(from,to,dt*MotionSpeed);
            }
            bool window=from<Spec.damageEnd && to>Spec.damageStart;
            if(window && (Spec.attackKind!="hook-claw" || DirectClaw || hookConnected))
            {
                var box=actor.AttackBounds;
                foreach(var target in actor.arena.TargetsInBox(box.center,box.size))
                {
                    if(!actor.Brain.Executing)break;
                    if(!ValidTarget(target) || struck.Contains(target.ActorId) ||
                       Spec.attackKind=="hook-claw" && !DirectClaw && target!=Grabbed)continue;
                    if(actor.arena.Occluded(actor.Center,target.Center,false))continue;
                    // A parried hit is consumed too, so one claw is countered once.
                    int parries=target.ParryCount;
                    if(actor.DeliverSpeciesHit(target,actor.Definition.damage,Spec.attackKind=="hook-claw"?1:0))
                    { struck.Add(target.ActorId);HitsDelivered++; }
                    else if(target.ParryCount!=parries)struck.Add(target.ActorId);
                }
            }
        }
        bool ValidTarget(CombatActor target)=>target && !target.Dead && !target.Hidden &&
            (target==Actor.arena.Player || target.isDecoy);
        float Reach(float age)
        {
            if(age<Spec.hookStart || age>Spec.hookReturn)return 0;
            if(age<=Spec.hookPeak)return Spec.hookReach*Mathf.InverseLerp(Spec.hookStart,Spec.hookPeak,age);
            return Spec.hookReach*(1-Mathf.InverseLerp(Spec.hookPeak,Spec.hookReturn,age));
        }
        void Hook(float from,float to,float dt)
        {
            float before=Reach(from),after=Reach(to);HookDistance=after;
            if(!hookConnected && to>Spec.hookStart && from<Spec.hookPeak)
            {
                var socket=Actor.Position+Vector2.up*Spec.hookHeight;
                float low=Mathf.Min(before,after),high=Mathf.Max(before,after);
                var center=socket+Vector2.right*(Actor.facing*(low+high)/2);
                var size=new Vector2(high-low+Spec.hookSize,Spec.hookSize);
                foreach(var target in Actor.arena.TargetsInBox(center,size))
                {
                    if(!ValidTarget(target) || target==Actor.arena.Player && Actor.arena.Player.Invulnerable ||
                       Actor.arena.Occluded(socket,target.Center,false))continue;
                    Grabbed=target;hookConnected=true;target.CancelAttack();break;
                }
            }
            if(ValidTarget(Grabbed) && to>=Spec.hookPeak && from<Spec.hookReturn)
            {
                if(Actor.arena.Occluded(Actor.Center,Grabbed.Center,false)) { Grabbed=null;return; }
                float goal=Actor.Position.x+Actor.facing*Spec.pullGap;
                // Existing placement sweep prevents the pull from teleporting through solid terrain.
                Grabbed.TeleportToward(new Vector2(Mathf.MoveTowards(Grabbed.Position.x,goal,Spec.pullSpeed*dt),Grabbed.Position.y));
                Grabbed.stunUntil=Mathf.Max(Grabbed.stunUntil,Actor.arena.Now+dt);
            }
        }
    }
}
