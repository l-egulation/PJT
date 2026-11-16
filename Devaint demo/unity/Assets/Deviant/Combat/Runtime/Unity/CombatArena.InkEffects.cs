using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Equipment.Contracts;
using UnityEngine;

namespace Deviant.Combat
{
    public partial class CombatArena
    {
        void ResolveInkCandidates(List<Candidate> candidates,bool controlled)
        {
            Candidate winner = null; InkPartCatalog.Effect detonation = null;
            foreach (var c in candidates) foreach (var fx in c.Part.effects)
                if (fx.op == "detonate" && EligibleInkMarks(c.Cause,null).Count >= fx.minimum &&
                    (detonation == null || fx.damagePerStack > detonation.damagePerStack || fx.damagePerStack == detonation.damagePerStack && fx.heal > detonation.heal))
                { winner = c; detonation = fx; }
            bool detonated = winner != null && DetonateInkMarks(winner.Cause,winner.Mount,detonation,null);
            foreach (var c in candidates) foreach (var fx in c.Part.effects)
            {
                if (!c.Cause.Valid || c.Cause.Source.Dead) continue;
                if (fx.op == "detonate" || fx.op == "mark" || fx.op == "timed_detonate") continue;
                ExecuteInkEffect(c.Cause,c.Mount,fx,controlled,c.Cross);
            }
            // New marks are added only after arbitration/consumption, independent of slot ordering.
            foreach (var c in candidates) foreach (var fx in c.Part.effects)
            {
                if (fx.op != "mark") continue;
                bool fallback = c.Part.id == "p_detonate" || c.Part.id == "p_heavy" || c.Part.id == "p_charge";
                if (fallback && detonated) continue;
                if (fx.delay > 0) inkDamage.Add(new InkDamage { Cause=c.Cause,Mount=c.Mount,Effect=fx,At=Now+fx.delay,Remaining=1 });
                else AddInkMarks(c.Cause,fx.stacks);
            }
            foreach (var c in candidates) foreach (var fx in c.Part.effects)
                if (fx.op == "timed_detonate")
                    inkDamage.Add(new InkDamage { Cause=c.Cause,Mount=c.Mount,Effect=fx,At=Now+fx.delay,Remaining=1,
                        ReservedMarks=new HashSet<Guid>(TargetInk(c.Cause.Target).Marks.ConvertAll(m=>m.Id)) });
        }
        List<Mark> EligibleInkMarks(InkHit hit,HashSet<Guid> reserved)
        {
            var result = new List<Mark>();
            foreach (var mark in TargetInk(hit.Target).Marks)
                if (reserved != null ? reserved.Contains(mark.Id) : mark.Action != hit.Hit.Origin.ExecutionId) result.Add(mark);
            return result;
        }
        void AddInkMarks(InkHit hit,int count)
        {
            if (!hit.Valid || hit.Target.Dead) return;
            var state = TargetInk(hit.Target);
            for (int i=0;i<count && state.Marks.Count<InkPartCatalog.Settings.markCap;i++)
                state.Marks.Add(new Mark { Action=hit.Hit.Origin.ExecutionId,Until=Now+InkPartCatalog.Settings.markDuration });
        }
        bool DetonateInkMarks(InkHit hit,InkPartMount mount,InkPartCatalog.Effect effect,HashSet<Guid> reserved)
        {
            if (!hit.Valid || hit.Target.Dead) return false;
            var eligible = EligibleInkMarks(hit,reserved);
            if (eligible.Count < Math.Max(1,effect.minimum)) return false;
            var state = TargetInk(hit.Target); foreach (var mark in eligible) state.Marks.Remove(mark);
            DealInk(hit,mount,eligible.Count*effect.damagePerStack,hit.Hit.Origin.InkGeneration);
            HealInk(effect.heal); return true;
        }
        void ExecuteInkEffect(InkHit hit,InkPartMount mount,InkPartCatalog.Effect effect,bool controlled=false,bool cross=false)
        {
            var action = ActionFor(hit.Hit.Origin);
            var state = inkStates.GetOrCreateValue(mount);
            switch (effect.op)
            {
                case "dot": ScheduleInk(hit,mount,effect,true); break;
                case "damage": ScheduleInk(hit,mount,effect,false); break;
                case "leech":
                    HealInk(Mathf.Min(effect.cap,Mathf.Max(0,hit.Hit.Before.Health-hit.Hit.After.Health)*effect.ratio)); break;
                case "heal": HealInk(effect.amount); break;
                case "overheal":
                    float missing=Mathf.Max(0,Player.maxHealth-Player.health); HealInk(effect.amount);
                    AddInkShield(mount,Mathf.Max(0,effect.amount-missing),effect.duration); break;
                case "counter_heal":
                    var record=hit.RecentDamage;if(record==null)break;
                    float healing=Mathf.Min(Mathf.Max(0,record.Loss-record.Healed),record.Loss*effect.ratio);
                    record.Healed+=healing;HealInk(healing);break;
                case "shield": AddInkShield(mount,effect.amount,effect.duration); break;
                case "guard": AddInkGuard(mount,effect.amount,effect.duration); break;
                case "shield_damage":
                    float consumed=SpendInkShield(effect.cap);DealInk(hit,mount,consumed*effect.ratio); break;
                case "buff": state.Amount=effect.amount;state.Until=Now+effect.duration;break;
                case "next_recovery":
                    var beneficiary=cross && TargetInk(hit.Target).LastOriginal.HasValue ? InkWeapon(TargetInk(hit.Target).LastOriginal.Value.Hit.Origin.Stamp.WeaponId) : action?.Weapon;
                    if (beneficiary!=null) inkWeapons.GetOrCreateValue(beneficiary).Recovery=Mathf.Min(.8f,inkWeapons.GetOrCreateValue(beneficiary).Recovery+effect.amount);break;
                case "refund": if(action?.Weapon!=null)RefundInkOther(action.Weapon,effect.seconds);break;
                case "charge_refund":
                    if(action?.Weapon!=null)inkWeapons.GetOrCreateValue(action.Weapon).ChargeStore=Mathf.Min(InkPartCatalog.Settings.chargeStoreCap,inkWeapons.GetOrCreateValue(action.Weapon).ChargeStore+effect.amount);break;
                case "conditional_damage": if(controlled)DealInk(hit,mount,effect.damage);break;
                case "slow": AddInkSlow(hit.Target,effect.amount,effect.duration);break;
                case "root": AddInkControl(hit.Target,effect.duration,mount.PartId=="b_prison");break;
                case "frost":
                    var target=TargetInk(hit.Target);target.Frost=Mathf.Min((int)effect.threshold,target.Frost+effect.stacks);target.FrostUntil=Now+InkPartCatalog.Settings.frostDuration;
                    if(target.Frost>=effect.threshold){target.Frost=0;AddInkControl(hit.Target,effect.duration,true);}break;
                case "slow_area":
                    if(mount.PartId=="b_zone") SpawnInkField(hit,mount,effect);
                    else foreach(var e in InkNearby(hit.Target.Center,effect.radius))AddInkSlow(e,effect.amount,effect.duration);break;
                case "area":
                    if(mount.PartId=="r_wave")SpawnInkWave(hit,mount,effect.damage,effect.radius);
                    else foreach(var e in InkNearby(hit.Target.Center,effect.radius,effect.excludeTarget?hit.Target:null,effect.count))ScheduleInk(Retarget(hit,e),mount,effect,false);break;
                case "death_area":
                    if(hit.Hit.Origin.InkGeneration>=1)break;
                    foreach(var e in InkNearby(hit.Target.Center,effect.radius,hit.Target))DealInk(Retarget(hit,e),mount,effect.damage,1);break;
                case "spread":
                    foreach(var e in InkNearby(hit.Target.Center,effect.radius,hit.Target,effect.count))AddInkMarks(Retarget(hit,e),effect.stacks);break;
                case "seed": SpawnInkSeed(hit.Target.Center,effect.amount);break;
            }
        }
        void AddInkSlow(CombatActor target,float amount,float duration)
        {
            if(!target || target.Dead || !target.AllowsControlEffects)return;
            TargetInk(target).Slows.Add(new Slow {Amount=Mathf.Clamp(amount,0,InkPartCatalog.Settings.maxSlow),Until=Now+duration});
        }
        void AddInkControl(CombatActor target,float duration,bool freeze)
        {
            if(!target || target.Dead || !target.AllowsControlEffects)return;
            var state=TargetInk(target);if(Now<state.ControlReady)return;
            // 빙결은 기절과 같은 보스 내부 쿨타임을 쓴다.
            if(freeze?!target.TryAcceptBossStun():!target.TryAcceptBossRoot())return;
            float end=Now+Mathf.Min(duration,InkPartCatalog.Settings.maxControlSeconds);
            if(freeze){state.FrozenUntil=Mathf.Max(state.FrozenUntil,end);target.stunUntil=Mathf.Max(target.stunUntil,end);target.CancelAttack();}
            else target.rootUntil=Mathf.Max(target.rootUntil,end);
            state.ControlReady=end+InkPartCatalog.Settings.controlRecoverySeconds;
        }
        bool InkControlled(CombatActor target)
        { return target.rootUntil>Now || target.stunUntil>Now || InkSlow(target)>0; }
        float InkSlow(CombatActor actor)
        {
            float max=ReadRunInkSlow(actor);
            if(Sketch!=null)
            {
                if(inkTargets.TryGetValue(actor,out var existing) && existing.Life==actor.LifeVersion)
                    foreach(var slow in existing.Slows)if(slow.Until>Now)max=Mathf.Max(max,slow.Amount);
                return max;
            }
            foreach(var slow in TargetInk(actor).Slows)max=Mathf.Max(max,slow.Amount);return max;
        }
        // Village move-speed unlock as a fraction (0.05 per tier) from the run's departure snapshot; 0 outside a
        // profile run. It adds to the inks' movement buffs and stays outside their cap.
        public float PermanentMoveSpeedBonus {get;set;}
        // The player's walk/run speed scale: 1 + the permanent bonus + the inks' movement buffs. Monsters slow down.
        public float InkMovementScale(CombatActor actor)
        {
            if(!actor)return 1;
            if(!actor.isPlayer)return actor.isBoss && Sketch != null ? 1 : 1-InkSlow(actor);
            return 1+PermanentMoveSpeedBonus+AugmentWalkBonus+PlayerInkMoveBonus();
        }
        float PlayerInkMoveBonus()
        {
            if (Sketch != null) return Mathf.Min(Sketch.Catalog.moveCap, SketchBuffTotal(sketchMove));
            var weapon=InkWeapon(LastStartedInkWeaponId);if(weapon==null)return 0;
            if(MvpGrowth!=null)return MvpMoveBonus(weapon);
            float bonus=0;foreach(var mount in weapon.InkPalette.Read().Slots)
            {
                if(mount==null || !inkStates.TryGetValue(mount,out var state) || state.Until<=Now)continue;
                var part=InkPartCatalog.Find(mount.PartId);
                if(part!=null && Array.Exists(part.effects,e=>e.op=="buff" && e.stat=="move"))bonus+=state.Amount;
            }
            return bonus;
        }
        void RefundInkOther(WeaponState weapon,float seconds)
        { foreach(var other in Player.weapons)if(other!=weapon)other.Cooldown.Refund(seconds,Now); }
        internal void RecordInkDodge(CombatActor attacker,CombatHitOrigin origin)
        {
            if(!attacker || attacker.isPlayer || !Player.Dashing || !Player.Invulnerable || !origin.IsSpecified || !inkDodgedAttacks.Add(origin.ExecutionId))return;
            // The bounded event ring is not the lifetime owner of an attack. Retain this set until the player/arena reset.
            var w=InkWeapon(LastStartedInkWeaponId);if(w==null)return;
            TryBindActor(Player,out var source);TryBindActor(attacker,out var target);
            var own=new CombatHitOrigin(source,default,origin.ExecutionId,-1,"dodge",Guid.Empty,CombatHitKind.InkPart,
                stamp:new CombatAttackStamp(w.InstanceId,catalog.forms[w.FormIndex].id,"ink_dodge","trial",false,-1,w.InkPalette.Read()));
            var h=new CombatAcceptedHit(0,SimulationTickCount,Now,own,target,0,attacker.Vitals,new ActorDamageResult(true,0,0,attacker.Vitals),attacker.Kinematics);
            var hit=new InkHit(h,attacker,Player);var candidates=new List<Candidate>();CollectInk(candidates,hit,"dodge",false,false,false);ResolveInkCandidates(candidates,false);
        }
    }
}
