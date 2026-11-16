using Deviant.Combat.Contracts;
using UnityEngine;

namespace Deviant.Combat
{
    public partial class CombatActor
    {
        IActorVitals inkVitals;
        IActorVitals InkVitals => inkVitals ?? (inkVitals = new InkVitalsAdapter(this));
        sealed class InkVitalsAdapter : IActorVitals
        {
            readonly CombatActor actor;
            public InkVitalsAdapter(CombatActor actor) { this.actor=actor; }
            public ActorVitalsSnapshot Read()
            {
                var s=actor.vitals.Read();float ink=actor.arena && actor.isPlayer ? actor.arena.ReadInkShield() : 0;
                if(ink==0)return s;
                return new ActorVitalsSnapshot(s.Maximum,s.Health,s.Shield+ink,s.GuardUntil,s.GuardReduction,s.ReceivedHits);
            }
            public ActorDamageResult ApplyDamage(float damage,float now,bool bypass)
            {
                if(float.IsNaN(damage) || float.IsInfinity(damage))throw new System.ArgumentOutOfRangeException(nameof(damage));
                // Explicit bypass is scripted execution, not a combat roll. Fall recovery
                // subtracts its fixed maximum-health cost through a separate path.
                if(!bypass && damage>0 && (!actor.isPlayer || CombatDamageVariance.Settings.varyPlayerReceivedDamage))
                    damage=CombatDamageVariance.Settings.Apply(damage,UnityEngine.Random.value);
                float absorbed=0;
                if(actor.arena && actor.isPlayer && !bypass)
                {
                    var before=actor.vitals.Read();
                    // 농묵: 받는 피해 증가는 보호막에 막히기 전 피해에 곱한다.
                    damage*=actor.arena.MvpIncomingDamageScale();
                    damage=actor.arena.ProtectInkDamage(damage,now<before.GuardUntil?before.GuardReduction:0,out absorbed);
                }
                var result=actor.vitals.ApplyDamage(damage,now,bypass);
                return new ActorDamageResult(result.Accepted,result.Damage,result.Absorbed+absorbed,Read());
            }
            public void WriteMaximum(float v)=>actor.vitals.WriteMaximum(v);
            public void WriteHealth(float v)=>actor.vitals.WriteHealth(v);
            public void WriteShield(float v)=>actor.vitals.WriteShield(v);
            public void WriteGuardUntil(float v)=>actor.vitals.WriteGuardUntil(v);
            public void WriteGuardReduction(float v)=>actor.vitals.WriteGuardReduction(v);
            public void ExpireShield(float now)=>actor.vitals.ExpireShield(now);
            public void Reset()=>actor.vitals.Reset();
        }
    }
    public partial class CombatArena
    {
        sealed class InkProtection
        {
            public Deviant.Equipment.Contracts.InkPartMount Mount;
            public int Life;
            public float Amount, Until;
            // 다음 피격 한 번에 남은 양과 관계없이 사라지는 보호막(단단한 껍질)
            public bool OneHit;
        }
        readonly System.Collections.Generic.List<InkProtection> inkShields=new System.Collections.Generic.List<InkProtection>();
        readonly System.Collections.Generic.List<InkProtection> inkGuards=new System.Collections.Generic.List<InkProtection>();
        void PruneInkProtection()
        {
            if(!Player){inkShields.Clear();inkGuards.Clear();return;}
            inkShields.RemoveAll(s=>s.Life!=Player.LifeVersion || s.Until<=Now || s.Amount<=0);
            inkGuards.RemoveAll(s=>s.Life!=Player.LifeVersion || s.Until<=Now || s.Amount<=0);
        }
        public float ReadInkShield()
        { if (Sketch != null) return RunShieldTotal; PruneInkProtection();float sum=0;foreach(var s in inkShields)sum+=s.Amount;return sum; }
        void AddInkShield(Deviant.Equipment.Contracts.InkPartMount mount,float amount,float duration,float? cap=null,bool oneHit=false)
        {
            PruneInkProtection();
            var own=inkShields.Find(s=>s.Mount==mount);
            float free=Mathf.Max(0,(cap??InkPartCatalog.Settings.shieldCap)-Player.shield-ReadInkShield()+(own?.Amount??0));
            if(own==null){own=new InkProtection{Mount=mount,Life=Player.LifeVersion};inkShields.Add(own);}
            own.Amount=Mathf.Min(free,Mathf.Max(0,amount));own.Until=Now+duration;own.OneHit=oneHit;
        }
        void AddInkGuard(Deviant.Equipment.Contracts.InkPartMount mount,float amount,float duration)
        {
            PruneInkProtection();var own=inkGuards.Find(s=>s.Mount==mount);
            if(own==null){own=new InkProtection{Mount=mount,Life=Player.LifeVersion};inkGuards.Add(own);}
            own.Amount=amount;own.Until=Now+duration;
        }
        float SpendInkShield(float maximum)
        {
            PruneInkProtection();float spent=0;
            // Consume the same player resource once, including an existing native skill shield.
            float native=Mathf.Min(maximum,Player.shield);Player.shield-=native;spent+=native;
            foreach(var s in inkShields){float take=Mathf.Min(s.Amount,maximum-spent);s.Amount-=take;spent+=take;if(spent>=maximum)break;}
            return spent;
        }
        internal float ProtectInkDamage(float damage,float nativeReduction,out float absorbed)
        {
            if (Sketch != null) return ProtectSketchDamage(damage, out absorbed);
            absorbed=0;PruneInkProtection();
            if(damage<=0 || inkShields.Count==0 && inkGuards.Count==0)return damage;
            float reduction=0;foreach(var g in inkGuards)reduction+=g.Amount;
            float nativeFactor=1-Mathf.Clamp01(nativeReduction);
            float combined=Mathf.Max(nativeReduction,Mathf.Min(InkPartCatalog.Settings.maxDamageReduction,nativeReduction+reduction));
            damage*=1-combined;if(reduction>0)inkGuards.Clear();
            foreach(var s in inkShields){float take=Mathf.Min(s.Amount,damage);s.Amount-=take;damage-=take;absorbed+=take;if(damage<=0)break;}
            inkShields.RemoveAll(s=>s.OneHit);
            return nativeFactor>0?damage/nativeFactor:0;
        }
    }
}
