using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Deviant.Combat.Contracts;
using Deviant.Equipment.Contracts;
using UnityEngine;

namespace Deviant.Combat
{
    public partial class CombatArena
    {
        Guid lastStartedInkWeaponId;
        public Guid LastStartedInkWeaponId
        {
            get => lastStartedInkWeaponId != Guid.Empty ? lastStartedInkWeaponId : Player ? Player.weapons[0].InstanceId : Guid.Empty;
            internal set => lastStartedInkWeaponId = value;
        }
        sealed class PartState
        {
            public PartState() { }
            public int Hits, Stored;
            public float ReadyAt, Until, Amount, Distance;
        }
        sealed class WeaponInkState
        {
            public WeaponInkState() { }
            public float Windup, Recovery, ChargeStore;
        }
        internal sealed class InkAction
        {
            public WeaponState Weapon;
            public InkPaletteSnapshot Palette;
            public Guid Execution;
            public int SourceLife;
            public bool Charge, Finisher, Basic, Begun;
            public Deviant.Run.GrowthSlot[] MvpSlots;public float MvpBasicReference;
            public float MvpAllGeometryScale=1, MvpRadiusScale=1, MvpPower, MvpBasePower, MvpHighHealthPower, MvpAttackSpeed, MvpWindupReduction;
            public float DamageScale = 1, FlatDamage, RangeScale = 1, WindupScale = 1, RecoveryScale = 1;
            public float ChargeMove, ChargeShield, ChargeShieldRatio;
        }
        sealed class Mark
        {
            public Guid Id = Guid.NewGuid(), Action, Weapon;
            public float Until;
        }
        sealed class Slow { public float Until, Amount; }
        sealed class TargetInkState
        {
            public TargetInkState() { }
            public int Life = -1, Frost;
            public float FrostUntil, ControlReady, FrozenUntil, MvpBindReady, MvpStunReady;
            public bool KillProcessed, MvpHitOnce;
            public InkHit? LastOriginal, MvpBindCause;
            public InkHit MvpPoisonCause; public InkPartMount MvpPoisonMount;
            public int MvpPoisonStacks;
            public float MvpBindUntil, MvpPoisonUntil, MvpPoisonNext, MvpPoisonInterval, MvpPoisonDamage, MvpStunTickAt, MvpFieldReadyAt;
            // 밑줄 긋기: 밑줄을 그은 무기와 그 무기 기본 공격의 피해 증가량
            public Guid MvpUnderlineWeapon; public float MvpUnderlineUntil, MvpUnderlineAmount;
            public readonly List<Mark> Marks = new List<Mark>();
            public readonly List<Slow> Slows = new List<Slow>();
        }
        sealed class InkDamageRecord { public float At, Loss, Healed; }
        InkDamageRecord currentInkDamage;
        readonly struct InkHit
        {
            public readonly CombatAcceptedHit Hit;
            public readonly CombatActor Target, Source;
            public readonly int TargetLife, SourceLife;
            public readonly bool WasControlled, WasMarked;
            public readonly float SourceHealth;
            public readonly InkDamageRecord RecentDamage;
            public InkHit(CombatAcceptedHit hit, CombatActor target, CombatActor source)
            { Hit = hit; Target = target; Source = source; TargetLife = target.LifeVersion; SourceLife = source.LifeVersion;
                WasControlled = target.arena.InkControlled(target); WasMarked = target.arena.TargetInk(target).Marks.Count>0; SourceHealth = source.health; RecentDamage = source.arena.currentInkDamage; }
            public bool Valid => Target && Source && Target.LifeVersion == TargetLife && Source.LifeVersion == SourceLife;
        }
        sealed class InkDamage
        {
            public InkHit Cause;
            public InkPartMount Mount;
            public float At, Damage, Interval;
            public int Remaining, Generation;
            public bool Burn;
            public InkPartCatalog.Effect Effect;
            public HashSet<Guid> ReservedMarks;
        }
        sealed class Candidate
        {
            public InkPartMount Mount;
            public InkPartCatalog.Part Part;
            public InkHit Cause;
            public bool Cross;
        }
        ConditionalWeakTable<InkPartMount, PartState> inkStates = new ConditionalWeakTable<InkPartMount, PartState>();
        ConditionalWeakTable<InkPaletteSnapshot, HashSet<Guid>> inkActions = new ConditionalWeakTable<InkPaletteSnapshot, HashSet<Guid>>();
        ConditionalWeakTable<InkPaletteSnapshot, InkAction> inkStarts = new ConditionalWeakTable<InkPaletteSnapshot, InkAction>();
        ConditionalWeakTable<WeaponState, WeaponInkState> inkWeapons = new ConditionalWeakTable<WeaponState, WeaponInkState>();
        ConditionalWeakTable<CombatActor, TargetInkState> inkTargets = new ConditionalWeakTable<CombatActor, TargetInkState>();
        readonly Queue<InkHit> inkHits = new Queue<InkHit>();
        readonly List<InkDamage> inkDamage = new List<InkDamage>();
        readonly HashSet<Guid> inkDodgedAttacks = new HashSet<Guid>();
        Guid lastInkSpecialWeapon;
        bool inkOtherSpecialAvailable;
        TargetInkState TargetInk(CombatActor target)
        {
            var state = inkTargets.GetOrCreateValue(target);
            if (state.Life != target.LifeVersion)
            {
                state.Life = target.LifeVersion; state.Frost = 0; state.FrostUntil = state.ControlReady = state.FrozenUntil = state.MvpBindReady = state.MvpStunReady = 0;
                state.KillProcessed = false; state.LastOriginal = null; state.Marks.Clear(); state.Slows.Clear();
                state.MvpHitOnce = false; state.MvpBindCause = null; state.MvpPoisonMount = null; state.MvpPoisonStacks = 0;
                state.MvpBindUntil = state.MvpPoisonUntil = state.MvpPoisonNext = state.MvpStunTickAt = state.MvpFieldReadyAt = 0;
            }
            state.Marks.RemoveAll(m => m.Until <= Now);
            state.Slows.RemoveAll(s => s.Until <= Now);
            if (Now >= state.FrostUntil) state.Frost = 0;
            return state;
        }
        WeaponState InkWeapon(Guid id)
        { if (Player) { foreach (var w in Player.weapons) if (w.InstanceId == id) return w; var found = Player.weapons.Find(id); if (found != null) return found; } return sketchStates.TryGetValue(id, out var stored) ? stored : null; }
        InkAction ActionFor(CombatHitOrigin origin)
        {
            var palette = origin.Stamp.InkPalette;
            if (palette == null) return null;
            if (inkStarts.TryGetValue(palette, out var action)) return action;
            action = new InkAction { Weapon = InkWeapon(origin.Stamp.WeaponId), Palette = palette,
                Execution = origin.ExecutionId, SourceLife = Player.LifeVersion, Basic = origin.Stamp.Basic,
                Charge = (origin.ActionId ?? "").StartsWith("ink_charge_") };
            var form = catalog.forms[ action.Weapon?.FormIndex ?? 0 ];
            action.Finisher = action.Basic && origin.ActionId == form.id + "_basic_" + (form.basics.Length - 1);
            inkStarts.Add(palette, action); return action;
        }
        void QueueInkHit(CombatAcceptedHit hit)
        {
            var source = FindReadActor(hit.Origin.EffectiveSource.ActorId);
            var target = FindReadActor(hit.Target.ActorId);
            if (!source || !target) return;
            if (target == Player)
            {
                float loss = Mathf.Max(0, hit.Before.Health - hit.After.Health);
                if (loss > 0 && !source.isPlayer)
                { currentInkDamage = new InkDamageRecord {At=hit.Now,Loss=loss}; }
                // Capture the active palette at damage time, rather than reconstructing it after later actions.
                var w = InkWeapon(LastStartedInkWeaponId);
                if (loss > 0 && w != null && !source.isPlayer)
                {
                    TryBindActor(Player, out var playerHandle);
                    var cause = new CombatHitOrigin(playerHandle, default, hit.Origin.ExecutionId, -1, "counter", Guid.Empty, CombatHitKind.InkPart,
                        stamp: new CombatAttackStamp(w.InstanceId, catalog.forms[w.FormIndex].id, "ink_counter", "trial", false, -1, w.InkPalette.Read()), sourceFacing: Player.facing);
                    var reflected = new CombatAcceptedHit(0, SimulationTickCount, hit.Now, cause, hit.Origin.EffectiveSource, loss,
                        source.Vitals, new ActorDamageResult(true, 0, 0, source.Vitals), source.Kinematics);
                    inkHits.Enqueue(new InkHit(reflected, source, Player));
                }
                return;
            }
            if (source == Player && !target.isDecoy && hit.Origin.Stamp.InkPalette != null)
                inkHits.Enqueue(new InkHit(hit, target, source));
        }
        bool InkCondition(InkPartCatalog.Part p, InkHit hit, bool controlled, bool marked)
        {
            var action = ActionFor(hit.Hit.Origin);
            switch (p.condition)
            {
                case "basic": return action != null && action.Basic;
                case "special": return action != null && !action.Basic;
                case "finisher": return action != null && action.Finisher;
                case "charge": case "charge_origin": return action != null && action.Charge;
                case "high_health": return hit.SourceHealth >= p.conditionThreshold;
                case "recent_damage": return hit.RecentDamage != null && hit.Hit.Now-hit.RecentDamage.At<=p.conditionWindow && hit.RecentDamage.Loss>hit.RecentDamage.Healed;
                case "controlled_kill": return controlled;
                case "marked_kill": return marked;
                default: return true;
            }
        }
        bool AdmitInk(InkPartMount mount, InkPartCatalog.Part part, InkHit hit, bool once)
        {
            var state = inkStates.GetOrCreateValue(mount);
            if (part.every > 1 && ++state.Hits % part.every != 0) return false;
            if (hit.Hit.Now + .00001f < state.ReadyAt) return false;
            if (once && !inkActions.GetOrCreateValue(hit.Hit.Origin.Stamp.InkPalette).Add(mount.InstanceId)) return false;
            state.ReadyAt = hit.Hit.Now + part.cooldown; return true;
        }
        void CollectInk(List<Candidate> candidates, InkHit cause, string trigger, bool cross, bool controlled, bool marked)
        {
            var palette = cause.Hit.Origin.Stamp.InkPalette;
            if (palette == null) return;
            foreach (var mount in palette.Slots)
            {
                if (mount == null) continue;
                var part = InkPartCatalog.Find(mount.PartId);
                if (part == null || part.trigger != trigger || !InkCondition(part,cause,controlled,marked)) continue;
                if (!AdmitInk(mount,part,cause,part.oncePerAction)) continue;
                candidates.Add(new Candidate { Mount = mount, Part = part, Cause = cause, Cross = cross });
            }
        }
        bool processingInkHits;
        internal void FlushInkHits()
        {
            if(processingInkHits)return;
            processingInkHits=true;
            try
            {
            while (inkHits.Count > 0)
            {
                var hit = inkHits.Dequeue();
                if (Sketch != null) continue;
                if (!hit.Valid || hit.Source.Dead) continue;
                if(MvpGrowth!=null){HandleMvpHit(hit);continue;}
                var state = TargetInk(hit.Target);
                bool controlled = hit.WasControlled, marked = hit.WasMarked || state.Marks.Count > 0;
                bool counter = hit.Hit.Origin.ActionId == "ink_counter";
                var candidates = new List<Candidate>();
                if (counter) CollectInk(candidates,hit,"damage",false,controlled,marked);
                else if (hit.Hit.Origin.Kind != CombatHitKind.InkPart)
                {
                    CollectInk(candidates,hit,"hit",false,controlled,marked);
                    if (state.LastOriginal.HasValue)
                    {
                        var previous = state.LastOriginal.Value;
                        if (previous.Valid && previous.Hit.Origin.Stamp.WeaponId != hit.Hit.Origin.Stamp.WeaponId)
                        {
                            var original = previous.Hit.Origin;
                            var old = new CombatHitOrigin(original.Source, hit.Hit.Origin.Command,hit.Hit.Origin.ExecutionId,original.HitIndex,original.HitId,original.EffectId,original.Kind,
                                stamp:original.Stamp,actionId:original.ActionId,isBasicAction:original.IsBasicAction,sourceFacing:original.SourceFacing);
                            var crossed = new CombatAcceptedHit(0,SimulationTickCount,hit.Hit.Now,old,hit.Hit.Target,hit.Hit.RequestedDamage,
                                hit.Hit.Before,new ActorDamageResult(true,hit.Hit.Damage,hit.Hit.Absorbed,hit.Hit.After),hit.Hit.TargetPose);
                            CollectInk(candidates,new InkHit(crossed,hit.Target,Player),"cross",true,controlled,marked);
                        }
                    }
                    state.LastOriginal = hit;
                }
                // Kill candidates are admitted once per target life, including kills by derived ink damage.
                if (hit.Hit.Before.Health > 0 && hit.Hit.After.Health <= 0 && !state.KillProcessed)
                { state.KillProcessed = true; CollectInk(candidates,hit,"kill",false,controlled,marked); }
                ResolveInkCandidates(candidates,controlled);
            }
            }
            finally { processingInkHits=false; }
        }
        void StepInkParts()
        {
            if (Sketch != null) { inkHits.Clear(); StepSketch(); return; }
            FlushInkHits();
            for (int i = inkDamage.Count - 1; i >= 0; i--)
            {
                var damage = inkDamage[i];
                if (!damage.Cause.Valid || damage.Cause.Target.Dead || damage.Cause.Source.Dead)
                { inkDamage.RemoveAt(i); continue; }
                while (damage.Remaining > 0 && Now + .00001f >= damage.At)
                {
                    if (damage.Effect != null)
                    {
                        if (damage.Effect.op == "mark") AddInkMarks(damage.Cause,damage.Effect.stacks);
                        else DetonateInkMarks(damage.Cause,damage.Mount,damage.Effect,damage.ReservedMarks);
                    }
                    else DealInk(damage.Cause,damage.Mount,damage.Damage,damage.Generation);
                    damage.Remaining--; damage.At += damage.Interval;
                    if (!damage.Cause.Valid || damage.Cause.Target.Dead) break;
                }
                if (damage.Remaining == 0 || !damage.Cause.Valid || damage.Cause.Target.Dead) inkDamage.RemoveAt(i);
            }
            StepInkWorld();
            StepMvp();
        }
        void DealInk(InkHit cause, InkPartMount mount, float damage, int generation = 0)
        {
            if (!cause.Valid || cause.Target.Dead || damage <= 0) return;
            var origin = cause.Hit.Origin.WithEffect(mount.InstanceId,CombatHitKind.InkPart).WithInkGeneration(generation);
            cause.Target.Receive(damage,cause.Source,origin.SourceFacing,false,null,origin:origin);
        }
        void ScheduleInk(InkHit hit, InkPartMount mount, InkPartCatalog.Effect effect, bool burn)
        {
            if (!burn && effect.delay<=0)
            { DealInk(hit,mount,effect.ratio>0?hit.Hit.RequestedDamage*effect.ratio:effect.damage,hit.Hit.Origin.InkGeneration);return; }
            if (burn) inkDamage.RemoveAll(d => d.Burn && d.Mount == mount && d.Cause.Target == hit.Target);
            inkDamage.Add(new InkDamage { Cause = hit, Mount = mount, Burn = burn,
                At = hit.Hit.Now + (burn ? effect.interval : effect.delay),
                Damage = effect.ratio > 0 ? hit.Hit.RequestedDamage * effect.ratio : effect.damage,
                Remaining = burn ? Mathf.FloorToInt(effect.duration / effect.interval) : 1, Interval = effect.interval,
                Generation = hit.Hit.Origin.InkGeneration });
        }
        List<TestEnemy> InkNearby(Vector2 center,float radius,CombatActor exclude=null,int count=0)
        {
            var targets = new List<TestEnemy>();
            // Distance to the nearest point of each enemy's hurt box (Center when it has none enabled).
            var reach = new Dictionary<TestEnemy,float>();
            foreach (var e in enemies)
            {
                if (!e || e.Dead || e.isDecoy || e == exclude) continue;
                float d = (e.ClosestHurtPoint(center) - center).sqrMagnitude;
                if (d <= radius * radius) { targets.Add(e); reach[e] = d; }
            }
            targets.Sort((a,b)=> { int d=reach[a].CompareTo(reach[b]); return d != 0 ? d : a.ActorId.CompareTo(b.ActorId); });
            if (count > 0 && targets.Count > count) targets.RemoveRange(count,targets.Count-count);
            return targets;
        }
        InkHit Retarget(InkHit cause,CombatActor target) => new InkHit(cause.Hit,target,cause.Source);
        void HealInk(float amount) { if (!Player.Dead) Player.Heal(amount); }
        public int ReadInkMarks(CombatActor target) => target ? TargetInk(target).Marks.Count : 0;
        public int ReadInkFrost(CombatActor target) => target ? TargetInk(target).Frost : 0;
        void ResetInkParts()
        {
            inkHits.Clear(); inkDamage.Clear(); ClearInkWorld(); LastStartedInkWeaponId = Guid.Empty;
            inkStates = new ConditionalWeakTable<InkPartMount, PartState>();
            inkActions = new ConditionalWeakTable<InkPaletteSnapshot, HashSet<Guid>>();
            inkStarts = new ConditionalWeakTable<InkPaletteSnapshot, InkAction>();
            inkTargets = new ConditionalWeakTable<CombatActor, TargetInkState>();
            inkWeapons = new ConditionalWeakTable<WeaponState, WeaponInkState>();
            inkDodgedAttacks.Clear();
            lastInkSpecialWeapon = Guid.Empty; inkOtherSpecialAvailable = false; currentInkDamage=null;
        }
    }
}
