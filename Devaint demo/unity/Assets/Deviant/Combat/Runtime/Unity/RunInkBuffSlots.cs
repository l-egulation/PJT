using System.Collections.Generic;
using System.Linq;
using Deviant.Run;

namespace Deviant.Combat
{
    // Same weapon + ink ID shares one HUD view; combat execution is unchanged.
    public sealed class RunInkBuffSlots : IBuffSlotSource
    {
        readonly CombatArena arena;
        SketchEquipment cachedSketch;
        int revision = -1;
        readonly List<(int Slot, RunInkWeapon Weapon, RunInkGroup Group, string Tooltip)> groups = new List<(int, RunInkWeapon, RunInkGroup, string)>();
        public RunInkBuffSlots(CombatArena arena) => this.arena = arena;
        public void Collect(List<BuffSlot> into)
        {
            var sketch = arena ? arena.Sketch : null;
            if (sketch == null) return;
            float now = arena.Now;
            if (cachedSketch != sketch || revision != sketch.Revision)
            {
                cachedSketch = sketch; revision = sketch.Revision; groups.Clear();
                for (int slot = 0; slot < 2; slot++)
                {
                    var weapon = sketch.Equipped(slot);
                    if (weapon == null || weapon.Destroyed) continue;
                    foreach (var group in RunInkGroup.Collect(weapon, sketch.Catalog))
                        groups.Add((slot, weapon, group, $"무기 {slot + 1} · {group.Title}\n{group.Description}"));
                }
            }
            foreach (var entry in groups)
            {
                if (entry.Weapon.Destroyed || sketch.Equipped(entry.Slot) != entry.Weapon) continue;
                var group = entry.Group;
                if (!TryIcon(group.Ink.effect, out var icon)) continue;
                // 지속: 보라의 부여 창(장착본) 또는 이동속도·빨강의 플레이어 버프(전투). 쿨타임은 따로 넘긴다.
                float active = group.ActiveRemaining;
                int contributing = 0;
                foreach (var mount in group.Mounts)
                {
                    float remaining = arena.ReadRunInkMountBuff(mount.Id);
                    if (remaining > 0) contributing++;
                    active = System.Math.Max(active, remaining);
                }
                string tooltip = entry.Tooltip;
                if (group.Ink.color == "red" || group.Ink.effect == "movement_speed_increase")
                    tooltip += $"\n현재 기여 {contributing}/{group.Mounts.Length}칸 · 같은 효과 전체 최대 {sketch.Catalog.maximumContributingInkCopies}칸";
                // Capped healing/shields/skill refunds can leave only some copies on cooldown.
                // Keep their real execution state and explain partial readiness instead of changing combat.
                int ready = group.ReadyCount;
                if (ready > 0 && ready < group.Mounts.Length)
                    tooltip += $"\n현재 {ready}/{group.Mounts.Length}칸 준비 · 아이콘은 가장 긴 남은 대기 표시";
                into.Add(new BuffSlot(icon, Trigger(group.Ink.trigger), now + group.CooldownRemaining, group.Cooldown, tooltip,
                    active > 0 ? now + active : 0, System.Math.Max(group.Duration, active)));
            }
        }
        public static bool TryIcon(string effect, out BuffSlotIcon icon)
        {
            switch (effect)
            {
                case "movement_speed_increase": icon = BuffSlotIcon.MoveSpeed; return true;
                case "shield": icon = BuffSlotIcon.Shield; return true;
                case "heal": icon = BuffSlotIcon.Heal; return true;
                case "stun": icon = BuffSlotIcon.Stun; return true;
                case "root": icon = BuffSlotIcon.Bind; return true;
                case "poison": icon = BuffSlotIcon.Poison; return true;
                case "slow": icon = BuffSlotIcon.Slow; return true;
                case "basic_attack_damage_increase": icon = BuffSlotIcon.BasicDamage; return true;
                case "basic_attack_speed_increase": icon = BuffSlotIcon.BasicSpeed; return true;
                case "cooldown_reduction": icon = BuffSlotIcon.SkillCooldown; return true;
                case "skill_attack_damage_increase": icon = BuffSlotIcon.SkillDamage; return true;
                default: icon = default; return false;
            }
        }
        public static BuffSlotTrigger Trigger(string trigger)
        {
            switch (trigger)
            {
                case "basic_attack": return BuffSlotTrigger.Basic;
                case "skill_attack": return BuffSlotTrigger.Skill;
                case "weapon_switch": return BuffSlotTrigger.Switch;
                case "dash_use": return BuffSlotTrigger.Dash;
                case "status_condition": return BuffSlotTrigger.Status;
                default: return BuffSlotTrigger.None;
            }
        }
    }
}
