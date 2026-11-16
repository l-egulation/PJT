using System;
using Deviant.Combat.Contracts;
using Deviant.Equipment.Contracts;
using UnityEngine;

namespace Deviant.Combat
{
    public partial class CombatArena
    {
        readonly System.Collections.Generic.HashSet<Guid> startedExecutions=new System.Collections.Generic.HashSet<Guid>();
        // Every player attack execution, once, as it starts (sketch-weapon durability attribution, ink switching).
        public event Action<CombatAttackStamp,Guid> PlayerAttackStarted;
        internal InkAction BeginInkAction(CombatAttackStamp stamp,Guid execution,CombatActor owner)
        {
            if(owner==Player&&execution!=Guid.Empty&&startedExecutions.Add(execution))
            {
                if(startedExecutions.Count>1024)startedExecutions.Clear();
                PlayerAttackStarted?.Invoke(stamp,execution);
            }
            if(owner!=Player || stamp.InkPalette==null)return null;
            var origin=new CombatHitOrigin(default,default,execution,0,null,Guid.Empty,CombatHitKind.Melee,stamp:stamp);
            var action=ActionFor(origin);if(action.Begun)return action;
            if (Sketch != null) BeginSketchAttack(stamp, execution); else BeginMvpAction(action);
            action.Begun=true;LastStartedInkWeaponId=stamp.WeaponId;
            var next=inkWeapons.GetOrCreateValue(action.Weapon);
            float prepare=next.Windup,recovery=next.Recovery;next.Windup=next.Recovery=0;
            bool afterOther=inkOtherSpecialAvailable && lastInkSpecialWeapon!=stamp.WeaponId;
            inkOtherSpecialAvailable=false;
            if(!stamp.Basic){lastInkSpecialWeapon=stamp.WeaponId;inkOtherSpecialAvailable=true;}
            foreach(var mount in stamp.InkPalette.Slots)
            {
                if(mount==null)continue;var part=InkPartCatalog.Find(mount.PartId);if(part==null)continue;
                var state=inkStates.GetOrCreateValue(mount);
                foreach(var effect in part.effects)
                {
                    if(effect.op=="buff" && effect.stat=="haste" && state.Until>Now && action.Finisher)recovery+=effect.amount;
                    if(effect.op=="distance_windup" && state.Stored>0){prepare+=effect.amount;state.Stored=0;}
                    if(effect.op=="movement_wave" && stamp.Basic && state.Stored>0)
                    {
                        var cause=InkSyntheticHit(InkOrigin(action.Weapon,stamp.InkPalette,stamp.ActionId,true,execution),Player);
                        for(int i=0;i<state.Stored;i++)SpawnInkWave(cause,mount,effect.damage,effect.range);
                        state.Stored=0;
                    }
                }
                if(part.trigger!="start" || part.condition=="charge" && !action.Charge || part.condition=="after_other_special" && !afterOther || Now<state.ReadyAt)continue;
                state.ReadyAt=Now+part.cooldown;
                foreach(var effect in part.effects)switch(effect.op)
                {
                    case "low_health":if(Player.health<=effect.threshold)action.DamageScale*=1+effect.amount;break;
                    case "blood":float paid=Mathf.Min(effect.cost,Mathf.Max(0,Player.health-1));Player.health-=paid;action.FlatDamage+=paid*effect.ratio;break;
                    case "range":action.RangeScale*=1+effect.amount;break;
                    case "recovery":recovery+=effect.amount;break;
                    case "windup":prepare+=effect.amount;break;
                    case "charge_move":action.ChargeMove+=effect.amount;break;
                    case "charge_shield":action.ChargeShield+=effect.amount;action.ChargeShieldRatio=effect.ratio;break;
                }
            }
            action.WindupScale=1-Mathf.Min(InkPartCatalog.Settings.maxWindupReduction,prepare);action.RecoveryScale=1-Mathf.Min(InkPartCatalog.Settings.maxRecoveryReduction,recovery);
            // 색상 잉크의 공격 속도는 준비·회수 구간만 줄이고 판정 구간은 유지한다.
            if(action.MvpAttackSpeed>0){action.WindupScale/=1+action.MvpAttackSpeed;action.RecoveryScale/=1+action.MvpAttackSpeed;}
            // 내달린 획: 대시 직후 시작한 공격의 준비 구간만 줄인다.
            if(action.MvpWindupReduction>0)action.WindupScale*=1-Mathf.Clamp01(action.MvpWindupReduction);
            return action;
        }
        internal float InkOriginalDamage(CombatHitOrigin origin,float damage,CombatActor target=null)
        {
            if (Sketch != null) return SketchDamage(origin, target, damage);
            damage=MvpTargetDamage(origin,target,damage);
            if(origin.Kind==CombatHitKind.InkPart || origin.Stamp.InkPalette==null || !inkStarts.TryGetValue(origin.Stamp.InkPalette,out var action))return damage;
            damage=action.DamageScale==1 && action.FlatDamage==0 ? damage : damage*action.DamageScale+action.FlatDamage;
            return damage+MvpRetaliation(origin,target);
        }
        internal float TakeInkChargeStore(WeaponState weapon)
        {var state=inkWeapons.GetOrCreateValue(weapon);float value=state.ChargeStore;state.ChargeStore=0;return value;}
        public float ReadInkChargeStore(int slot)=>slot<0||slot>=2?0:inkWeapons.GetOrCreateValue(Player.weapons[slot]).ChargeStore;
    }
    public partial class CombatCast
    {
        internal CombatArena.InkAction InkAction;
        // Weapon switch windup reduction, fixed at start. It multiplies the ink
        // windup scale and shares the same action-time mapping below.
        float switchWindupScale=1;
        float WindupScale=>(InkAction?.WindupScale ?? 1)*switchWindupScale;
        // A reactive shield window and its success recovery use combat seconds,
        // independently of offensive windup/recovery bonuses.
        bool InkUntimed=>Definition.Id=="shield_default" || WindupScale==1 && (InkAction==null || InkAction.RecoveryScale==1);
        float InkWindup=>charge!=null?(charge.Release?.ReleaseTime??charge.Definition.Maximum):meleeAction?.Definition.Impact ?? (Definition.Hits.Count>0?Definition.Hits[0].Time:Definition.Activation);
        float InkActiveEnd
        {
            get{if(charge!=null)return InkWindup+(chargedHit?.Active ?? (Definition.Hits.Count>0?Definition.Hits[0].Active:0));float end=InkWindup;foreach(var hit in Definition.Hits)end=Mathf.Max(end,hit.Time+hit.Active);return Mathf.Min(end,Definition.End);}
        }
        float InkRecoveryScale
        {
            get
            {
                if(InkAction==null)return 1;
                float logicalEnd=meleeAction?.Definition.End ?? charge?.Release?.MotionEnd ?? Definition.End;
                float tail=Mathf.Max(0,logicalEnd-InkActiveEnd);
                float needed=InkPartCatalog.Settings.minimumActionSeconds-InkWindup*WindupScale-(InkActiveEnd-InkWindup);
                return tail>0?Mathf.Max(InkAction.RecoveryScale,needed/tail):InkAction.RecoveryScale;
            }
        }
        float InkMap(float age)
        {
            if(InkUntimed)return age;
            float prep=Mathf.Max(.001f,WindupScale),recovery=InkAction==null?1:Mathf.Max(1-InkPartCatalog.Settings.maxRecoveryReduction,InkRecoveryScale);
            float windup=InkWindup,active=InkActiveEnd-windup,realPrep=windup*prep;
            if(age<realPrep)return age/prep;
            if(age<realPrep+active)return windup+age-realPrep;
            return InkActiveEnd+(age-realPrep-active)/recovery;
        }
        float InkUnmap(float age)
        {
            if(InkUntimed)return age;
            float windup=InkWindup,active=InkActiveEnd-windup;
            if(age<windup)return age*WindupScale;
            if(age<InkActiveEnd)return windup*WindupScale+age-windup;
            return windup*WindupScale+active+(age-InkActiveEnd)*InkRecoveryScale;
        }
        float InkNow=>InkUntimed?arena.Now:started+InkMap(arena.Now-started);
        float InkStep(float step)=>InkUntimed ? step : InkMap(arena.Now-started+step)-InkMap(arena.Now-started);
    }
}
