using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Run;
using Deviant.Combat.Contracts;
using Deviant.Equipment.Contracts;
using UnityEngine;

namespace Deviant.Combat
{
    public partial class CombatArena
    {
        public SketchEquipment Sketch => MvpGrowth?.Sketch;
        static RunInkCatalog runInkCatalog;
        public static RunInkCatalog LoadRunInkCatalog()
        {
            if (runInkCatalog != null) return runInkCatalog;
            var text = Resources.Load<TextAsset>("RunInks");
            if (!text) throw new InvalidOperationException("RunInks.json is missing; run export_run_inks.py.");
            var data = JsonUtility.FromJson<RunInkCatalog>(text.text); data.Validate(); return runInkCatalog = data;
        }
        public void EnableSketchEquipment(int seed, int stage = 1)
        {
            if (MvpGrowth == null || !Player) return;
            if (Sketch == null)
            {
                var forms = catalog.forms.Where(f => f.id != UnarmedFormId && f.specials != null)
                    .Select(f => new { f.id, skills = f.specials.Select((s, i) => new { s, i }).Where(p => p.s != null && !p.s.unavailable).Select(p => p.i).ToArray() })
                    .Where(f => f.skills.Length > 0).ToDictionary(f => f.id, f => f.skills);
                MvpGrowth.Sketch = new SketchEquipment(LoadRunInkCatalog(), MvpGrowth.Economy, seed, forms,
                    catalog.forms.Where(f => f.specials != null).ToDictionary(f => f.id, f => f.specials.Select(skill => skill?.id).ToArray()));
            }
            Sketch.BeginStage(stage); SyncSketchLoadout();
            // 캐릭터 체력 막대 아래 버프칸이 무기별 같은 잉크를 묶어 실제 유지 시간·대기를 읽는다.
            if (!(Player.BuffSlotSource is RunInkBuffSlots)) Player.BuffSlotSource = new RunInkBuffSlots(this);
        }
        readonly Dictionary<Guid, IWeaponInstance> sketchInstances = new Dictionary<Guid, IWeaponInstance>();
        readonly Dictionary<Guid, WeaponState> sketchStates = new Dictionary<Guid, WeaponState>();
        readonly Dictionary<Guid, SketchAttack> sketchAttacks = new Dictionary<Guid, SketchAttack>();
        readonly Dictionary<Guid, SketchBuff> sketchMove = new Dictionary<Guid, SketchBuff>(), sketchRed = new Dictionary<Guid, SketchBuff>(), sketchRedSpeed = new Dictionary<Guid, SketchBuff>();
        readonly Dictionary<long, SketchStatus> sketchStatuses = new Dictionary<long, SketchStatus>();
        readonly List<SketchStatus> sketchStatusStep = new List<SketchStatus>();
        readonly List<TestEnemy> sketchDashTargets = new List<TestEnemy>();
        readonly List<SketchShield> sketchShields = new List<SketchShield>();
        sealed class SketchAttack
        {
            public Guid Weapon;
            public bool Basic, Switch;
            public float RecoveryBonus;
            public readonly HashSet<Guid> Used = new HashSet<Guid>();
            public readonly Dictionary<Guid, HashSet<long>> Targets = new Dictionary<Guid, HashSet<long>>();
            public readonly HashSet<Guid> Blue = new HashSet<Guid>();
            public readonly HashSet<Guid> DashHeals = new HashSet<Guid>();
        }
        sealed class SketchBuff { public float Amount, Until; public bool Enhanced; public long ProcOrder; }
        long sketchBuffProcOrder;
        readonly List<KeyValuePair<Guid, SketchBuff>> sketchContributingBuffs = new List<KeyValuePair<Guid, SketchBuff>>();
        static int CompareSketchBuffs(KeyValuePair<Guid, SketchBuff> a, KeyValuePair<Guid, SketchBuff> b)
        {
            int amount = b.Value.Amount.CompareTo(a.Value.Amount);
            return amount != 0 ? amount : b.Value.ProcOrder.CompareTo(a.Value.ProcOrder);
        }
        // Overflow keeps its own expiry. Re-select on every read so expiry, storage and
        // destruction cannot grant extra copies or restart a suppressed buff's lifetime.
        List<KeyValuePair<Guid, SketchBuff>> ContributingSketchBuffs(Dictionary<Guid, SketchBuff> buffs)
        {
            sketchContributingBuffs.Clear();
            foreach (var pair in buffs) if (pair.Value.Until > Now) sketchContributingBuffs.Add(pair);
            sketchContributingBuffs.Sort(CompareSketchBuffs);
            int cap = Sketch.Catalog.maximumContributingInkCopies;
            if (sketchContributingBuffs.Count > cap) sketchContributingBuffs.RemoveRange(cap, sketchContributingBuffs.Count - cap);
            return sketchContributingBuffs;
        }
        SketchBuff NewSketchBuff(SketchMount mount, float duration) => new SketchBuff {
            Amount = mount.Value, Until = Now + duration, Enhanced = SketchEnhanced(mount), ProcOrder = ++sketchBuffProcOrder };
        sealed class SketchShield { public float Amount, DecayAt; public bool Dash; }
        static bool SketchEnhanced(SketchMount mount) => mount?.Ink != null && mount.Color == mount.Ink.color;
        sealed class SketchSlow { public Guid Mount; public float Amount, Until; }
        sealed class SketchStatus
        {
            public long Key;
            public CombatActor Actor;
            public int Life;
            public readonly List<SketchSlow> Slows = new List<SketchSlow>();
        }
        Guid sketchLastAttack;
        SketchAttack sketchDash;
        Vector2 sketchDashPosition;
        bool sketchWasDashing;
        float sketchLastTime, sketchNormalGrace;
        int sketchLife = -1;
        static long SketchLifeKey(CombatActor actor) => ((long)actor.ActorId << 32) | (uint)actor.LifeVersion;
        SketchStatus SketchTarget(CombatActor target)
        {
            long key = SketchLifeKey(target);
            if (!sketchStatuses.TryGetValue(key, out var state)) sketchStatuses[key] = state = new SketchStatus { Key = key, Actor = target, Life = target.LifeVersion };
            return state;
        }
        public float ReadRunInkSlow(CombatActor target)
        {
            if (Sketch == null || !target || !sketchStatuses.TryGetValue(SketchLifeKey(target), out var state)) return 0;
            return PruneSketchSlows(state, Now);
        }
        // Status strength remains visible on bosses, but their physical motion is unaffected.
        public float ReadRunInkMotionSlow(CombatActor target) => target && target.isBoss ? 0 : ReadRunInkSlow(target);
        static float PruneSketchSlows(SketchStatus state, float now)
        {
            float strongest = 0;
            for (int i = state.Slows.Count - 1; i >= 0; i--)
                if (state.Slows[i].Until <= now) state.Slows.RemoveAt(i);
                else strongest = Mathf.Max(strongest, state.Slows[i].Amount);
            return strongest;
        }
        float SketchBuffTotal(Dictionary<Guid, SketchBuff> buffs)
        {
            float total = 0;
            foreach (var pair in ContributingSketchBuffs(buffs)) total += pair.Value.Amount;
            return total;
        }
        bool SketchCondition(CombatActor target, string condition)
        {
            if (!target || target.Dead) return false;
            return condition switch { "stun" => target.stunUntil > Now, "root" => target.rootUntil > Now, "slow" => ReadRunInkSlow(target) > 0, _ => false };
        }
        public void SyncSketchLoadout()
        {
            if (Sketch == null || !Player) return;
            int activeSlot = Player.selected;
            bool activeWeaponBroken = Sketch.Find(Player.Weapon.InstanceId)?.Destroyed == true;
            // A move or exchange can put a weapon in the opposite slot. Move both
            // combat instances together before replacing any individual slot.
            if (Sketch.Equipped(0)?.Id == Player.weapons[1].InstanceId ||
                Sketch.Equipped(1)?.Id == Player.weapons[0].InstanceId)
                Player.weapons.SwapSlots();
            for (int slot = 0; slot < 2; slot++)
            {
                var want = Sketch.Equipped(slot); var old = Player.weapons[slot];
                if (want != null && old.InstanceId == want.Id)
                {
                    int form = Array.FindIndex(catalog.forms, definition => definition.id == want.Form);
                    if (old.FormIndex != form || old.SpecialIndex != want.Skill)
                    {
                        if (Player.currentCast && Player.currentCast.SourceWeaponId == old.InstanceId) Player.CancelAttack();
                        var skill = catalog.forms[form].specials[want.Skill];
                        var configuration = new WeaponConfiguration(new WeaponSelection(form, want.Skill), skill.capacity, skill.cooldown);
                        var result = Player.weapons.Reconfigure(slot, configuration, Now);
                        if (result.Status != EquipmentCommandStatus.Succeeded) throw new InvalidOperationException(result.Error);
                        old.ExperimentalChargeStamp = false; old.TestFormSpeedScale = TestBasicSpeed(want.Form);
                    }
                    sketchStates[want.Id] = old; sketchInstances[want.Id] = old.Resource; continue;
                }
                // Empty slots keep the same unarmed instance and any attack it already started.
                if (want == null && catalog.forms[old.FormIndex].id == UnarmedFormId) continue;
                if (sketchStates.TryGetValue(old.InstanceId, out var stored)) { RememberSketchSkill(old.InstanceId, stored); stored.Cooldown.Suspend(Now); }
                if (Player.currentCast && Player.currentCast.SourceWeaponId == old.InstanceId) Player.CancelAttack();
                IWeaponInstance instance;
                if (want == null)
                {
                    instance = Deviant.Equipment.WeaponResources.CreateWeapon();
                    instance.Select(new WeaponSelection(Array.FindIndex(catalog.forms, f => f.id == UnarmedFormId), 0));
                }
                else
                {
                    if (!sketchInstances.TryGetValue(want.Id, out instance))
                    {
                        instance = Deviant.Equipment.WeaponResources.CreateWeapon(want.Id);
                        int form = Array.FindIndex(catalog.forms, f => f.id == want.Form);
                        instance.Select(new WeaponSelection(form, want.Skill));
                        var skill = catalog.forms[form].specials[want.Skill];
                        instance.Charges.Reset(skill.capacity, skill.cooldown * (1 - AugmentSkillCooldownReduction));
                        if (want.SkillCapacity > 0)
                        {
                            instance.Charges.Reset(want.SkillCapacity, want.SkillPeriod);
                            for (int i = want.SkillAvailable; i < want.SkillCapacity; i++) instance.Charges.Spend(Now);
                            if (want.SkillAvailable < want.SkillCapacity) instance.Charges.Refund(Mathf.Max(0, want.SkillPeriod - want.SkillRemaining), Now);
                        }
                        sketchInstances[want.Id] = instance;
                    }
                }
                Player.weapons.ReplaceSlot(slot, instance);
                if (want != null)
                {
                    var state = Player.weapons[slot]; sketchStates[want.Id] = state; state.Cooldown.Resume(Now);
                    state.TestFormSpeedScale = TestBasicSpeed(want.Form);
                }
            }
            // Both slots admit basics: an empty slot uses its unarmed form, which has no skill. With no weapon at all A and
            // S both punch (team decision 2026-11-13, replacing the short-lived disabled B).
            Player.SetWeaponSlotReady(0, true);
            Player.SetWeaponSlotReady(1, true);
            if (activeWeaponBroken && Sketch.Equipped(1 - activeSlot) != null) Player.selected = 1 - activeSlot;
        }
        internal void BeginSketchAttack(CombatAttackStamp stamp, Guid execution)
        {
            if (Sketch == null) return;
            CheckSketchLife();
            if (sketchAttacks.ContainsKey(execution)) return;
            var attack = new SketchAttack { Weapon = stamp.WeaponId, Basic = stamp.Basic,
                Switch = sketchLastAttack != Guid.Empty && sketchLastAttack != stamp.WeaponId,
                RecoveryBonus = stamp.Basic ? Player.RecoveryCancelDamageBonus : 0 };
            sketchAttacks[execution] = attack; sketchLastAttack = stamp.WeaponId;
            SketchSelfEffects(attack, stamp.Basic ? "basic_attack" : "skill_attack");
            if (attack.Switch) SketchSelfEffects(attack, "weapon_switch");
        }
        void CheckSketchLife()
        {
            if (!Player || sketchLife == Player.LifeVersion) return;
            sketchLife = Player.LifeVersion; sketchLastTime = Now; sketchLastAttack = Guid.Empty;
            sketchAttacks.Clear(); sketchMove.Clear(); sketchRed.Clear(); sketchRedSpeed.Clear(); sketchStatuses.Clear(); sketchShields.Clear();
            sketchWasDashing = false; sketchDash = null; sketchNormalGrace = Now; sketchBuffProcOrder = 0; sketchContributingBuffs.Clear();
        }
        bool SketchReady(SketchMount mount, SketchAttack attack) => mount.Ink != null && mount.Cooldown <= .00001f && !attack.Used.Contains(mount.Id);
        void UseSketch(SketchMount mount, SketchAttack attack) { mount.Activate(); attack.Used.Add(mount.Id); }
        float RunShieldTotal => sketchShields.Sum(s => s.Amount);
        bool SketchSelf(SketchMount mount, bool dash)
        {
            var ink = mount.Ink;
            switch (ink.effect)
            {
                case "movement_speed_increase": sketchMove[mount.Id] = NewSketchBuff(mount, ink.duration); return true;
                case "heal": if (!(Player.Heal(mount.Value) > 0)) return false; CueRunInk(RunInkFxKind.Heal, mount); return true;
                case "shield":
                    float cap = Player.maxHealth * Sketch.Catalog.shieldCap, total = RunShieldTotal;
                    if (total >= cap || Mathf.Approximately(total, cap)) return false;
                    float added = Mathf.Min(mount.Value, Mathf.Max(0, cap - total));
                    if (!(added > 0)) return false;
                    sketchShields.Add(new SketchShield { Amount = added, Dash = dash, DecayAt = dash ? float.PositiveInfinity : 0 });
                    CueRunInk(dash ? RunInkFxKind.DashShield : RunInkFxKind.Shield, mount); return true;
                default: return false;
            }
        }
        static bool SketchDamageResource(RunInkDefinition ink) => ink != null &&
            (ink.trigger == "basic_attack" || ink.trigger == "skill_attack") &&
            (ink.effect == "heal" || ink.effect == "shield");
        // Called after committed health loss, including a lethal hit. Misses and blocked hits do not spend a proc.
        void SketchDamageResources(CombatHitOrigin origin, CombatActor target, float healthLost)
        {
            if (Sketch == null || !(healthLost > 0) || !target || target.isPlayer || target.isDecoy ||
                origin.Kind == CombatHitKind.Direct || origin.Kind == CombatHitKind.InkPart ||
                !sketchAttacks.TryGetValue(origin.ExecutionId, out var attack)) return;
            var weapon = Sketch.Find(attack.Weapon); if (weapon == null) return;
            string trigger = attack.Basic ? "basic_attack" : "skill_attack";
            foreach (var mount in weapon.Slots)
                if (SketchReady(mount, attack) && SketchDamageResource(mount.Ink) && mount.Ink.trigger == trigger && SketchSelf(mount, false))
                    UseSketch(mount, attack);
        }
        void SketchSelfEffects(SketchAttack attack, string trigger, bool dashContact = false)
        {
            var weapon = Sketch.Find(attack.Weapon); if (weapon == null) return;
            foreach (var mount in weapon.Slots)
                if (SketchReady(mount, attack) && !SketchDamageResource(mount.Ink) && mount.Ink.trigger == trigger &&
                    (trigger != "dash_use" || (mount.Ink.effect == "heal") == dashContact) &&
                    (!dashContact || attack.DashHeals.Contains(mount.Id)) &&
                    (trigger == "status_condition" || trigger == "dash_use" || mount.Ink.effect != "shield") && SketchSelf(mount, trigger == "dash_use"))
                    UseSketch(mount, attack);
        }
        void ActivateSketchControls(SketchAttack attack, string trigger)
        {
            var weapon = Sketch.Find(attack.Weapon); if (weapon == null) return;
            foreach (var group in weapon.Slots.Where(m => m.Ink?.color == "purple").GroupBy(m => m.Ink.id))
            {
                var first = group.First();
                if (first.Ink.trigger != trigger || first.ActiveRemaining > 0 || first.Cooldown > .00001f) continue;
                float copies = Sketch.Catalog.investment.Copies(group);
                foreach (var mount in group) mount.ActivateControl(Sketch.Catalog.investment.Window(first.Ink.effect, copies), Sketch.Catalog.investment.Cooldown(first.Ink.effect, copies));
            }
        }
        void SketchApplyStatuses(SketchAttack attack, CombatActor target, bool dash = false)
        {
            var weapon = Sketch.Find(attack.Weapon); if (weapon == null || !target || target.Dead || !target.AllowsControlEffects) return;
            // Attack-triggered windows begin on the first valid hit; the activating hit is included.
            // A dash activates its own inks on start and never borrows unrelated weapon triggers.
            if (!dash)
            {
                ActivateSketchControls(attack, attack.Basic ? "basic_attack" : "skill_attack");
                if (attack.Switch) ActivateSketchControls(attack, "weapon_switch");
            }
            foreach (var group in weapon.Slots.Where(m => m.Ink?.color == "purple").GroupBy(m => m.Ink.id))
            {
                var first = group.First(); var ink = first.Ink;
                // Dash inks apply only on path contact, never on basic or skill hits.
                if (first.ActiveRemaining <= 0 || dash != (ink.trigger == "dash_use")) continue;
                if (dash)
                {
                    if (!attack.Targets.TryGetValue(first.Id, out var targets)) attack.Targets[first.Id] = targets = new HashSet<long>();
                    if (!targets.Add(SketchLifeKey(target))) continue;
                }
                float copies = Sketch.Catalog.investment.Copies(group);
                float end = Now + Sketch.Catalog.investment.StatusDuration(copies);
                if (ink.effect == "stun")
                {
                    target.stunUntil = Mathf.Max(target.stunUntil, end);
                    if (!target.isBoss) target.CancelAttack();
                }
                else if (ink.effect == "root") target.rootUntil = Mathf.Max(target.rootUntil, end);
                else if (ink.effect == "slow")
                {
                    var state = SketchTarget(target);
                    var slow = state.Slows.Find(value => value.Mount == first.Id);
                    if (slow == null) state.Slows.Add(slow = new SketchSlow { Mount = first.Id });
                    slow.Amount = Sketch.Catalog.investment.Slow(copies); slow.Until = end;
                }
                SketchSelfEffects(attack, "status_condition");
            }
        }
        void RefreshSketchRed(SketchAttack attack, IEnumerable<CombatActor> targets)
        {
            if (!attack.Basic) return;
            var weapon = Sketch.Find(attack.Weapon); if (weapon == null) return;
            foreach (var group in weapon.Slots.Where(m => m.Ink?.color == "red").GroupBy(m => m.Ink.id))
            {
                if (!targets.Any(t => SketchCondition(t, group.First().Ink.condition))) continue;
                bool speed = group.First().Ink.effect == "basic_attack_speed_increase";
                float copies = Sketch.Catalog.investment.Copies(group);
                float duration = speed ? Mathf.Min(Sketch.Catalog.basicAttackSpeedDurationCap, group.First().Ink.duration * copies)
                    : Sketch.Catalog.investment.redBaseDurationSeconds * copies;
                var buffs = speed ? sketchRedSpeed : sketchRed;
                foreach (var mount in group)
                    buffs[mount.Id] = NewSketchBuff(mount, duration);
            }
        }
        internal float SketchDamage(CombatHitOrigin origin, CombatActor target, float damage)
        {
            if (Sketch == null || !target || target.Dead || origin.Kind == CombatHitKind.InkPart) return damage;
            if (!sketchAttacks.TryGetValue(origin.ExecutionId, out var attack)) return damage;
            SketchApplyStatuses(attack, target);
            RefreshSketchRed(attack, new[] { target });
            var weapon = Sketch.Find(attack.Weapon);
            if (weapon != null)
            {
                foreach (var mount in weapon.Slots)
                {
                    var ink = mount.Ink;
                    if (ink?.color == "red" || !SketchReady(mount, attack)) continue;
                    bool directTrigger = ink.trigger == (attack.Basic ? "basic_attack" : "skill_attack") || attack.Switch && ink.trigger == "weapon_switch";
                    if (directTrigger && !SketchDamageResource(ink) && ink.effect == "shield" && SketchSelf(mount, false)) { UseSketch(mount, attack); continue; }
                    if (ink.color != (attack.Basic ? "red" : "blue") || !SketchCondition(target, ink.condition)) continue;
                    if (ink.effect == "skill_attack_damage_increase") { attack.Blue.Add(mount.Id); CueRunInk(RunInkFxKind.SkillDamage, mount); }
                    else if (ink.effect == "cooldown_reduction")
                    {
                        if (!sketchStates.TryGetValue(weapon.Id, out var source) || source.Cooldown.Capacity != 1 || source.Cooldown.Available > 0) continue;
                        // Charges > 1 remain pending design; do not invent recharge rules for them.
                        var skill = catalog.forms[source.FormIndex].specials[source.SpecialIndex];
                        float remaining = source.Cooldown.Remaining(Now);
                        float elapsed = source.Cooldown.Period - remaining;
                        float minimumRemaining = Mathf.Max(0, skill.cooldown * .2f - elapsed);
                        float reduction = Mathf.Min(skill.cooldown * mount.Value, Mathf.Max(0, remaining - minimumRemaining));
                        if (!(reduction > 0)) continue;
                        source.Cooldown.Refund(reduction, Now); CueRunInk(RunInkFxKind.CooldownReduction, mount);
                    }
                    UseSketch(mount, attack);
                }
            }
            sketchNormalGrace = Now + Sketch.Catalog.shieldGrace;
            return damage; // The additive bonus joins existing damage bonuses in ScalePlayerDamage.
        }
        internal void SketchWeaponStatus(CombatHitOrigin origin, CombatActor target)
        {
            if (Sketch != null && target && target.AllowsControlEffects && sketchAttacks.TryGetValue(origin.ExecutionId, out var attack))
                SketchSelfEffects(attack, "status_condition");
        }
        internal void PrepareSketchHit(CombatHitOrigin origin, IEnumerable<CombatActor> targets)
        {
            if (Sketch == null || !sketchAttacks.TryGetValue(origin.ExecutionId, out var attack)) return;
            var all = targets.Where(t => t && !t.Dead && !t.isPlayer && !t.isDecoy).ToArray();
            foreach (var target in all) SketchApplyStatuses(attack, target);
            RefreshSketchRed(attack, all);
        }
        internal void PrepareSketchBlast(CombatHitOrigin origin, Vector2 center, float radius)
        {
            if (Sketch == null) return;
            var targets = Physics2D.OverlapCircleAll(center, radius).Select(c => new { Collider = c, Region = c.GetComponent<HurtRegion>() })
                .Where(p => p.Region && p.Region.actor && !Occluded(center, p.Collider.ClosestPoint(center)))
                .Select(p => p.Region.actor).Distinct();
            PrepareSketchHit(origin, targets);
        }
        public float ReadRunInkBasicSpeedBonus => Sketch == null || !Player || sketchLife != Player.LifeVersion
            ? 0 : Mathf.Min(Sketch.Catalog.basicAttackSpeedCap, SketchBuffTotal(sketchRedSpeed));
        internal float RunBasicSpeedScale(float weaponSpeed, float stealthScale, float augmentScale)
        {
            if (Sketch == null) return weaponSpeed * stealthScale * augmentScale;
            // One additive buff axis; base weapon speed stays separate. Each action snapshots this once at start.
            float buffs = Mathf.Min(Sketch.Catalog.maximumBasicSpeedScale,
                Mathf.Max(.1f, 1 + (stealthScale - 1) + (augmentScale - 1) + ReadRunInkBasicSpeedBonus));
            return weaponSpeed * buffs;
        }
        internal float SketchDamageBonus(CombatHitOrigin origin, CombatActor target)
        {
            if (Sketch == null || origin.Kind == CombatHitKind.InkPart) return 0;
            if (origin.Stamp.Basic) return SketchBuffTotal(sketchRed) +
                (sketchAttacks.TryGetValue(origin.ExecutionId, out var basicAttack) ? basicAttack.RecoveryBonus : 0);
            if (!sketchAttacks.TryGetValue(origin.ExecutionId, out var attack)) return 0;
            var weapon = Sketch.Find(attack.Weapon);
            return weapon == null ? 0 : weapon.Slots.Where(m => attack.Blue.Contains(m.Id) && SketchCondition(target, m.Ink.condition)).Sum(m => m.Value);
        }
        void StepSketch()
        {
            if (Sketch == null || !Player) return;
            CheckSketchLife(); float delta = Mathf.Max(0, Now - sketchLastTime); sketchLastTime = Now;
            Sketch.Tick(delta); StepMaintenanceAugments();
            foreach (var pair in sketchStates)
            {
                var state = pair.Value; var skill = catalog.forms[state.FormIndex].specials[state.SpecialIndex];
                float period = state.CooldownPeriod(skill.cooldown) * (1 - AugmentSkillCooldownReduction);
                if (!Mathf.Approximately(period, state.Cooldown.Period)) state.Cooldown.Reconfigure(skill.capacity, period, Now);
                RememberSketchSkill(pair.Key, state);
            }
            // Status callbacks can edit statuses. Preserve phase-entry membership
            // without allocating a new array on every simulation tick.
            sketchStatusStep.Clear(); sketchStatusStep.AddRange(sketchStatuses.Values);
            foreach (var state in sketchStatusStep)
            {
                if (!state.Actor || state.Actor.Dead || state.Actor.LifeVersion != state.Life)
                { RemoveSketchStatus(state); continue; }
                PruneSketchSlows(state, Now);
                if (state.Actor.Dead || state.Slows.Count == 0) RemoveSketchStatus(state);
            }
            sketchStatusStep.Clear();
            bool dashing = Player.Dashing;
            if (dashing && !sketchWasDashing)
            {
                sketchDash = new SketchAttack { Weapon = Player.Weapon.InstanceId };
                sketchDashPosition = Player.Center; SketchSelfEffects(sketchDash, "dash_use");
                var w = Sketch.Find(sketchDash.Weapon);
                if (w != null) foreach (var mount in w.Slots)
                    if (SketchReady(mount, sketchDash) && mount.Ink.trigger == "dash_use" && mount.Ink.effect == "heal") sketchDash.DashHeals.Add(mount.Id);
                ActivateSketchControls(sketchDash, "dash_use");
            }
            if (sketchDash != null && (dashing || sketchWasDashing))
            {
                Vector2 now = Player.Center;
                var sweep = new Bounds((now + sketchDashPosition) / 2, new Vector3(Mathf.Abs(now.x - sketchDashPosition.x) + Player.size.x + .5f, Mathf.Abs(now.y - sketchDashPosition.y) + Player.size.y + .5f, 1));
                sketchDashTargets.Clear(); sketchDashTargets.AddRange(enemies);
                foreach (var enemy in sketchDashTargets) if (enemy && !enemy.Dead && !enemy.isDecoy && enemy.HurtOverlaps(sweep))
                {
                    SketchSelfEffects(sketchDash, "dash_use", true);
                    SketchApplyStatuses(sketchDash, enemy, true);
                }
                sketchDashTargets.Clear();
                sketchDashPosition = now;
            }
            if (!dashing && sketchWasDashing)
            { foreach (var shield in sketchShields) if (shield.Dash && float.IsPositiveInfinity(shield.DecayAt)) shield.DecayAt = Now + Sketch.Catalog.dashShieldGrace; sketchDash = null; }
            sketchWasDashing = dashing;
            foreach (var shield in sketchShields)
            {
                if (!shield.Dash) continue;
                float start = shield.DecayAt;
                float elapsed = Mathf.Max(0, Now - Mathf.Max(Now - delta, start));
                shield.Amount = Mathf.Max(0, shield.Amount - elapsed * Player.maxHealth * Sketch.Catalog.dashShieldDecay);
            }
            float normalDecay = Mathf.Max(0, Now - Mathf.Max(Now - delta, sketchNormalGrace)) * Player.maxHealth * Sketch.Catalog.shieldDecay;
            foreach (var shield in sketchShields)
            { if (shield.Dash) continue; float take = Mathf.Min(normalDecay, shield.Amount); shield.Amount -= take; normalDecay -= take; if (normalDecay <= 0) break; }
            sketchShields.RemoveAll(s => s.Amount <= 0); Sketch.FinishHit(); SyncSketchLoadout();
        }
        void RemoveSketchStatus(SketchStatus state)
        {
            if (sketchStatuses.TryGetValue(state.Key, out var current) && ReferenceEquals(current, state)) sketchStatuses.Remove(state.Key);
        }
        internal float ProtectSketchDamage(float damage, out float absorbed)
        {
            absorbed = 0;
            foreach (var shield in sketchShields.OrderBy(s => s.Dash ? 0 : 1).ThenBy(s => s.DecayAt))
            { float take = Mathf.Min(shield.Amount, damage); shield.Amount -= take; damage -= take; absorbed += take; if (damage <= 0) break; }
            if (absorbed > 0) CueRunInk(RunInkFxKind.ShieldHit, null);
            return damage;
        }
        void RememberSketchSkill(Guid id, WeaponState state)
        { Sketch.Find(id)?.RememberSkill(state.Cooldown.Capacity, state.Cooldown.Available, state.Cooldown.Period, state.Cooldown.Remaining(Now)); }
        internal void FinishSketchHit()
        { if (Sketch != null) { Sketch.FinishHit(); SyncSketchLoadout(); } }
        // 런 잉크 이펙트 신호(보기 전용). 회복·방어막 생성·쿨타임 감소처럼 실제로 적용된 순간에만 보낸다.
        public event Action<RunInkFxCue> RunInkFxCued;
        void CueRunInk(RunInkFxKind kind, SketchMount mount) => RunInkFxCued?.Invoke(new RunInkFxCue(kind, SketchEnhanced(mount)));
        // 플레이어에게 유지 중인 런 잉크 효과. 상태를 바꾸거나 시간을 진행하지 않는다.
        public RunInkBuffSnapshot ReadRunInkBuffs()
        {
            if (Sketch == null || !Player || sketchLife != Player.LifeVersion) return default;
            float now = Now, speed = 0, red = 0, normal = 0, dash = 0, dashDecay = float.PositiveInfinity; bool speedEnhanced = false, redEnhanced = false;
            foreach (var pair in ContributingSketchBuffs(sketchMove)) { var buff = pair.Value; speed = Mathf.Max(speed, buff.Until - now); speedEnhanced |= buff.Enhanced; }
            foreach (var pair in ContributingSketchBuffs(sketchRed)) { var buff = pair.Value; red = Mathf.Max(red, buff.Until - now); redEnhanced |= buff.Enhanced; }
            foreach (var pair in ContributingSketchBuffs(sketchRedSpeed)) { var buff = pair.Value; red = Mathf.Max(red, buff.Until - now); redEnhanced |= buff.Enhanced; }
            foreach (var shield in sketchShields)
                if (shield.Amount <= 0) continue;
                else if (!shield.Dash) normal += shield.Amount;
                else { dash += shield.Amount; dashDecay = Mathf.Min(dashDecay, shield.DecayAt); }
            return new RunInkBuffSnapshot(speed, speedEnhanced, red, redEnhanced, normal, now >= sketchNormalGrace, dash, dash > 0 && dashDecay <= now ? now - dashDecay : -1);
        }
        // 버프칸 표시용: 장착본 하나가 건 이동속도·빨강 버프의 남은 시간. 보라 부여 창은 SketchMount.ActiveRemaining 이다.
        public float ReadRunInkMountBuff(Guid mount)
        {
            if (Sketch == null || !Player || sketchLife != Player.LifeVersion) return 0;
            if (sketchMove.ContainsKey(mount)) return ContributingSketchBuffTime(sketchMove, mount);
            if (sketchRed.ContainsKey(mount)) return ContributingSketchBuffTime(sketchRed, mount);
            if (sketchRedSpeed.ContainsKey(mount)) return ContributingSketchBuffTime(sketchRedSpeed, mount);
            return 0;
        }
        float ContributingSketchBuffTime(Dictionary<Guid, SketchBuff> buffs, Guid mount)
        {
            foreach (var pair in ContributingSketchBuffs(buffs)) if (pair.Key == mount) return pair.Value.Until - Now;
            return 0;
        }
    }
    public enum RunInkFxKind { Heal, Shield, DashShield, ShieldHit, CooldownReduction, SkillDamage }
    public readonly struct RunInkFxCue
    {
        public RunInkFxKind Kind { get; }
        public bool Enhanced { get; }
        public RunInkFxCue(RunInkFxKind kind, bool enhanced) { Kind = kind; Enhanced = enhanced; }
    }
    // SpeedLeft·RedLeft: 남은 유지 시간(초). ShieldDecaying: 일반 방어막 감소 중. DashDecayAge: 대쉬 방어막 감소 시작 후 경과(없으면 음수).
    public readonly struct RunInkBuffSnapshot
    {
        public readonly float SpeedLeft, RedLeft, Shield, DashShield, DashDecayAge;
        public readonly bool SpeedEnhanced, RedEnhanced, ShieldDecaying;
        public bool Any => SpeedLeft > 0 || RedLeft > 0 || Shield > 0 || DashShield > 0;
        public RunInkBuffSnapshot(float speedLeft, bool speedEnhanced, float redLeft, bool redEnhanced, float shield, bool shieldDecaying, float dashShield, float dashDecayAge)
        { SpeedLeft = speedLeft; SpeedEnhanced = speedEnhanced; RedLeft = redLeft; RedEnhanced = redEnhanced; Shield = shield; ShieldDecaying = shieldDecaying; DashShield = dashShield; DashDecayAge = dashDecayAge; }
    }
}
