using System;
using System.Collections.Generic;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using UnityEngine;

namespace Deviant.Chapter2
{
    [Serializable] public sealed class C2PixelBox
    {
        public float x,y,w,h;
        public Vector2 Size=>new Vector2(w,h)/32;
        public Vector2 FacingOffset=>new Vector2(-(x+w/2-320),260-y-h/2)/32;
        public Bounds At(Vector2 feet,int direction)=>new Bounds(feet+new Vector2(FacingOffset.x*direction,FacingOffset.y),Size);
    }
    [Serializable] public sealed class C2ApprovedNormal
    {
        public string id,species,name;
        public float hp,moveSpeed,startRange,maxStartHeight,cooldown,windup,active,recovery,damage,playerLock,enemyStagger,firstDelayMin,firstDelayMax,hitInterval,pushDistance,pushSeconds;
        public int maxHits;
        public float chargeDamage,chargeSpeed,chargeDistance,chargeSeconds,slamWindup,slamActive;
        public float projectileSpeed,minimumFlightSeconds,apex,projectileLifetime,launchX,launchY,hazardDamage,hazardDuration,hazardFirst,hazardTick;
        public C2PixelBox hurt,attack,charge,hazard;
        public float[] hitWindowStart,hitWindowEnd;
        public bool Ranged=>id=="R-selected"||id=="H3";
        public float Duration=>id=="B3"?windup+chargeSeconds+slamWindup+slamActive+recovery:Ranged?windup+recovery:windup+active+recovery;
        public void Validate()
        {
            if(string.IsNullOrEmpty(id)||hp<=0||moveSpeed<0||startRange<=0||cooldown<0||windup<=0||recovery<0||hurt==null||attack==null||hurt.w<=0||hurt.h<=0||attack.w<=0||attack.h<=0)
                throw new InvalidOperationException("Invalid approved normal: "+id);
            if(id=="M3"&&(hitWindowStart.Length!=3||hitWindowEnd.Length!=3))throw new InvalidOperationException("M3 requires three damage windows");
            if(Ranged&&(float.IsNaN(minimumFlightSeconds)||float.IsInfinity(minimumFlightSeconds)||minimumFlightSeconds<=0||minimumFlightSeconds>projectileLifetime))
                throw new InvalidOperationException("Invalid projectile minimum flight time: "+id);
        }
    }
    [Serializable] public sealed class C2ApprovedNormalBank
    {
        public int schema;public C2ApprovedNormal[] monsters;
        public C2ApprovedNormal Find(string id)=>Array.Find(monsters,m=>m.id==id)??throw new InvalidOperationException("Unknown normal: "+id);
        public static C2ApprovedNormalBank Load()
        {
            var text=Resources.Load<TextAsset>("Chapter2ApprovedNormals");if(!text)throw new InvalidOperationException("Export approved chapter2 normal rules first");
            var bank=JsonUtility.FromJson<C2ApprovedNormalBank>(text.text);if(bank.schema!=1||bank.monsters.Length!=5)throw new InvalidOperationException("Approved normal bank must contain five species");
            foreach(var m in bank.monsters)m.Validate();return bank;
        }
    }
    // Only these sources opt out of the common player's larger knockback.
    // Movement lock is replaced after the accepted-hit boundary; invulnerability, shields and hit ledger stay shared.
    public static class C2NormalHit
    {
        public static List<CombatActor> Targets(CombatArena arena)
        {
            // Accepted player hits may remove owned decoys; preserve the collision snapshot.
            var targets=new List<CombatActor>();
            if(arena.Player&&!arena.Player.Dead&&!arena.Player.Hidden)targets.Add(arena.Player);
            foreach(var decoy in arena.decoys)if(decoy&&!decoy.Dead&&!decoy.Hidden)targets.Add(decoy);
            return targets;
        }
        public static bool Apply(Chapter2NormalEnemy source,CombatActor target,float damage,int direction,float movementLock,float push,float pushSeconds,bool dedicated,CombatHitOrigin origin,bool projectile=false)
        {
            if(!target||target.Dead||target.Hidden)return false;
            float oldLock=target.moveLockUntil,oldKnock=target.knockUntil,oldSpeed=target.knockSpeed;
            source.DedicatedReaction=dedicated;
            bool accepted;
            try{accepted=target.Receive(damage,source,direction,false,null,projectileHit:projectile,origin:origin);}
            finally{source.DedicatedReaction=false;}
            if(!accepted)return false;
            target.moveLockUntil=Mathf.Max(oldLock,target.arena.Now+movementLock);
            if(dedicated)
            {
                target.SetKnockMotion(oldKnock,oldSpeed);
                if(push>0&&!target.IsForcedMoving)target.SetKnockMotion(target.arena.Now+pushSeconds,direction*push/pushSeconds);
            }
            return true;
        }
    }
}
