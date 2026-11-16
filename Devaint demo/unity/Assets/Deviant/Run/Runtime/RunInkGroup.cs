using System;
using System.Collections.Generic;
using System.Linq;

namespace Deviant.Run
{
    // One source weapon + one ink ID. Both HUD and equipment details use this view.
    public sealed class RunInkGroup
    {
        public SketchMount[] Mounts { get; }
        public RunInkDefinition Ink => Mounts[0].Ink;
        public string Title { get; }
        public string Description { get; }
        public float Duration { get; }
        public float Cooldown { get; }
        public float StatusDuration { get; }
        public float Amount { get; }
        public float ActiveRemaining => Mounts.Max(m => m.ActiveRemaining);
        public float CooldownRemaining => Mounts.Max(m => m.Cooldown);
        public int ReadyCount => Mounts.Count(m => m.Cooldown <= .00001f && m.ActiveRemaining <= 0);

        public static IEnumerable<RunInkGroup> Collect(RunInkWeapon weapon, RunInkCatalog catalog) =>
            weapon.Slots.Where(m => m.Ink != null).GroupBy(m => m.Ink.id).Select(g => new RunInkGroup(g.ToArray(), catalog));

        RunInkGroup(SketchMount[] mounts, RunInkCatalog catalog)
        {
            Mounts = mounts;
            var ink = Ink; var tuning = catalog.investment;
            float copies = tuning.Copies(mounts);
            int enhanced = mounts.Count(m => m.Color == ink.color);
            Title = ink.name + " · " + mounts.Length + "칸" + (enhanced > 0 ? " (강화 " + enhanced + "칸)" : "");
            Amount = mounts.Sum(m => m.Value); Duration = ink.duration; Cooldown = ink.cooldown;
            string condition = ink.condition == "stun" ? "기절" : ink.condition == "root" ? "속박" : "슬로우";
            string trigger = ink.trigger switch {
                "basic_attack" => "이 무기로 기본 공격 시", "skill_attack" => "이 무기로 스킬 공격 시",
                "weapon_switch" => "이 무기로 스위칭 공격 시", "dash_use" => "이 무기를 들고 대쉬 시",
                "status_condition" => "이 무기로 상태이상 부여 시", _ => "" };
            if ((ink.trigger == "basic_attack" || ink.trigger == "skill_attack") && (ink.effect == "heal" || ink.effect == "shield"))
                trigger = "이 무기의 " + (ink.trigger == "basic_attack" ? "기본 공격" : "스킬 공격") + "으로 몬스터에게 피해를 입힐 시";
            string effect;
            if (ink.color == "red")
            {
                bool speed = ink.effect == "basic_attack_speed_increase";
                Duration = speed ? Math.Min(catalog.basicAttackSpeedDurationCap, ink.duration * copies) : tuning.redBaseDurationSeconds * copies;
                if (speed) Amount = Math.Min(catalog.basicAttackSpeedCap, Amount);
                Description = $"이 무기로 {condition} 상태의 적에게 기본 공격 적중 시\n모든 무기 기본 공격 {(speed ? "공속" : "피해")} +{Amount * 100:0.#}%\n지속시간 {Duration:0.##}초 · 쿨타임 없음" +
                    $"\n같은 효과 전체 최대 {catalog.maximumContributingInkCopies}칸 기여" +
                    (speed ? $"\n공속 합산 최대 +{catalog.basicAttackSpeedCap * 100:0.#}% · 스킬 가속 없음" : "");
                return;
            }
            if (ink.color == "purple")
            {
                Duration = tuning.Window(ink.effect, copies); Cooldown = tuning.Cooldown(ink.effect, copies);
                StatusDuration = tuning.StatusDuration(copies);
                Amount = ink.effect == "slow" ? tuning.Slow(copies) : 0;
                effect = ink.effect == "slow" ? $"슬로우 {StatusDuration:0.##}초 (이동속도 {Amount * 100:0.#}% 감소)" : ink.effect == "stun" ? $"기절 {StatusDuration:0.##}초" : $"속박 {StatusDuration:0.##}초";
                Description = (ink.trigger == "dash_use" ? trigger : trigger.Replace(" 시", " 적중 시")) +
                    (ink.trigger == "dash_use"
                        ? $"\n{Duration:0.##}초 동안 대쉬 경로의 적에게 {effect} 부여"
                        : $"\n{Duration:0.##}초 동안 이 무기의 공격에 {effect} 효과 부여") +
                    $"\n쿨타임 {Cooldown:0.##}초";
                return;
            }
            switch (ink.effect)
            {
                case "movement_speed_increase":
                    Amount = Math.Min(Amount, catalog.moveCap);
                    effect = $"이동속도 +{Amount * 100:0.#}% · 지속시간 {Duration:0.##}초\n같은 효과 전체 최대 {catalog.maximumContributingInkCopies}칸 기여"; break;
                case "heal": effect = $"체력 {Amount:0.##} 회복"; break;
                case "shield":
                    effect = $"방어막 {Amount:0.##} 생성"; break;
                case "skill_attack_damage_increase":
                    trigger = $"이 무기로 {condition} 상태의 적에게 스킬 적중 시";
                    effect = $"스킬 피해 +{Amount * 100:0.#}%"; break;
                case "cooldown_reduction":
                    trigger = $"이 무기로 {condition} 상태의 적에게 스킬 적중 시";
                    effect = $"스킬 쿨타임 {Amount * 100:0.#}% 감소"; break;
                default: effect = ink.description; break;
            }
            Description = trigger + "\n" + effect + $"\n잉크 쿨타임 {Cooldown:0.##}초";
        }
    }
}
