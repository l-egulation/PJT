using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using Deviant.Equipment.Contracts;
using UnityEngine;

namespace Deviant.Combat
{
    public partial class CombatPlayer
    {
        public bool TrySetChargeStamp(int slot,bool enabled,out string error)
        {
            error=null;if(!EditingAllowed || slot<0 || slot>=weapons.Count){error="장비를 편집할 수 없습니다.";return false;}
            if(enabled && InkPartCatalog.Profile(arena.catalog.forms[weapons[slot].FormIndex].id)==null){error="이 무기의 시험 차징 프로필이 없습니다.";return false;}
            weapons[slot].ExperimentalChargeStamp=enabled;return true;
        }
        CombatActionAttempt BeginInkCharge(CombatCommandKey command)
        {
            if(currentCast && !currentCast.isBasic && weaponSwitchStart==WeaponSwitchStart.None)return CombatActionAttempt.Reject(CombatCommandReason.Busy);
            if(currentCast)CancelAttack();combo.Clear();jumps.ClearBuffer();ClearBufferedJump(CombatCommandReason.Interrupted);
            if(axis!=0)SyncFacing(axis);
            var source=new SkillData{id="ink_charge_prepare_"+Form.id,name="시험 차징",activation=1000000,end=1000000,
                inputSpeed=arena.Movement.RunSpeed,vertical="normal",facing="windup_start",hits=Array.Empty<HitData>(),invulStart=Array.Empty<float>(),invulEnd=Array.Empty<float>()};
            currentCast=CombatCast.Create(arena,this,source,Weapon,false,resourceSource:new InkFreeResources(Weapon),command:command);
            currentCast.BeginFullInkCharge(InkPartCatalog.Profile(Form.id),arena.TakeInkChargeStore(Weapon));
            return CombatActionAttempt.Start(currentCast.ExecutionId,outcomeRecorded:true);
        }
        public float InkChargeFraction=>currentCast?currentCast.InkChargeFraction:0;
        public bool InkCharging=>currentCast && currentCast.IsInkPreparing;
    }
    sealed class InkFreeResources : IActionResourceSource
    {
        readonly Guid weaponId;readonly float multiplier;
        public InkFreeResources(WeaponState weapon){weaponId=weapon.InstanceId;multiplier=1+weapon.SpecialBonus;}
        public ActionResourceResult TryActivate(ActionResourceKey key,float now)
            =>key.WeaponId!=weaponId?new ActionResourceResult(ActionResourceStatus.Rejected):
                new ActionResourceResult(ActionResourceStatus.Succeeded,new ActionResourceSnapshot(key,now,multiplier));
    }
    public partial class CombatCast
    {
        sealed class FullInkCharge
        {
            public InkPartCatalog.ChargeProfile Profile;
            public float Start,Stored,Moved;
            public readonly Dictionary<InkPartMount,float> Generated=new Dictionary<InkPartMount,float>();
        }
        FullInkCharge fullInkCharge;
        public bool IsInkPreparing=>fullInkCharge!=null;
        public float InkChargeFraction=>fullInkCharge==null?0:Mathf.Clamp01(fullInkCharge.Stored+
            (arena.Now-fullInkCharge.Start)/Mathf.Max(InkPartCatalog.Settings.minimumChargeSeconds,InkPartCatalog.Settings.chargeSeconds*InkAction.WindupScale)+fullInkCharge.Moved*InkAction.ChargeMove);
        internal void BeginFullInkCharge(InkPartCatalog.ChargeProfile profile,float stored)
        {fullInkCharge=new FullInkCharge{Profile=profile,Start=arena.Now,Stored=stored};}
        void StepInkCharge(float step)
        {
            if(!owner || owner.Dead || CastCancelled){CancelInkCharge();Dispose();return;}
            var pending=fullInkCharge;
            if(owner.axisForInk()!=0 && arena.Now>=owner.knockUntil && arena.Now>=owner.moveLockUntil && arena.Now>=owner.stunUntil)
                pending.Moved+=Mathf.Abs(owner.Position.x-previousOwner.x);
            foreach(var mount in InkAction.Palette.Slots)
            {
                if(mount==null)continue;var part=InkPartCatalog.Find(mount.PartId);if(part==null)continue;
                foreach(var fx in part.effects)
                {
                    if(fx.op=="charge_shield")
                    {
                        float generated=fx.amount*Mathf.Clamp01((arena.Now-pending.Start)/InkPartCatalog.Settings.chargeSeconds);
                        pending.Generated.TryGetValue(mount,out float previous);arena.GrowInkChargeShield(mount,Mathf.Max(0,generated-previous));pending.Generated[mount]=generated;
                    }
                    if(part.id=="b_charge")arena.RefreshInkChargeFrost(owner.Center,fx);
                }
            }
            if(arena.Player.CurrentInput.IsSpecialHeld(arena.Player.selected))return;
            if(InkChargeFraction<1 || arena.Now-pending.Start<InkPartCatalog.Settings.minimumChargeSeconds)
            {Cancel();arena.Player.CancelAttack();return;}
            float shieldDamage=arena.FinishInkChargeShields(InkAction.Palette,true);InkAction.FlatDamage+=shieldDamage;
            float reach=pending.Profile.reach*InkAction.RangeScale;
            var source=new SkillData{id="ink_charge_"+pending.Profile.form,name="완충 일격",activation=InkPartCatalog.Settings.chargeImpactSeconds,end=InkPartCatalog.Settings.chargeImpactSeconds+InkPartCatalog.Settings.chargeRecovery,
                inputSpeed=0,vertical="normal",facing="windup_start",invulStart=Array.Empty<float>(),invulEnd=Array.Empty<float>(),
                hits=new[]{new HitData{time=InkPartCatalog.Settings.chargeImpactSeconds,active=InkPartCatalog.Settings.chargeActiveSeconds,damage=pending.Profile.damage,anchor="actor",targets="all",
                    box=new BoxData{width=reach,height=InkPartCatalog.Settings.chargeHeight*InkAction.RangeScale,x=reach*.5f,y=1}}}};
            Definition=new Deviant.Definitions.SkillExecutionDefinition(source);DefinitionVersion="experimental-full-charge-v1";
            attackStamp=new CombatAttackStamp(weapon.InstanceId,pending.Profile.form,source.id,DefinitionVersion,false,-1,InkAction.Palette);
            fullInkCharge=null;started=arena.Now;InkAction.WindupScale=1;
            lifecycle=new CastLifecycleExecution(Definition.Activation,Definition.End);hitTrack=new SkillHitTrackExecution(-.000001f);
            arena.CaptureCharacterAttack(this);
        }
        void CancelInkCharge()
        {if(fullInkCharge==null)return;arena.FinishInkChargeShields(InkAction.Palette,false);fullInkCharge=null;}
    }
    public partial class CombatActor
    {internal int axisForInk()=>isPlayer?arena.Player.axis:0;}
    public partial class CombatArena
    {
        internal void GrowInkChargeShield(InkPartMount mount,float amount)
        {
            var own=inkShields.Find(s=>s.Mount==mount && s.Life==Player.LifeVersion);
            AddInkShield(mount,(own?.Amount??0)+amount,InkPartCatalog.Settings.chargeSeconds+1);
        }
        internal float FinishInkChargeShields(InkPaletteSnapshot palette,bool release)
        {
            float damage=0;PruneInkProtection();
            foreach(var mount in palette.Slots)
            {
                if(mount==null || mount.PartId!="g_charge_shield")continue;
                var own=inkShields.Find(s=>s.Mount==mount);if(own==null)continue;
                var part=InkPartCatalog.Find(mount.PartId);if(release)foreach(var fx in part.effects)if(fx.op=="charge_shield")damage+=own.Amount*fx.ratio;
                inkShields.Remove(own);
            }
            return damage;
        }
        internal void RefreshInkChargeFrost(Vector2 center,InkPartCatalog.Effect effect)
        {foreach(var enemy in InkNearby(center,effect.radius))AddInkSlow(enemy,effect.amount,effect.duration);}
    }
}
