using System.Collections.Generic;

namespace Deviant.Combat
{
    // 버프칸에 그릴 그림(resources/ui/buff-slot-icons). 효과의 종류만 고르며 수치는 담지 않는다.
    public enum BuffSlotIcon { MoveSpeed, Shield, Heal, Stun, Bind, Poison, Slow, BasicDamage, BasicSpeed, SkillCooldown, SkillDamage }
    // 칸 모서리 배지: 잉크의 발동 조건. 보스 내부 쿨타임 칸은 None.
    public enum BuffSlotTrigger { None, Basic, Skill, Switch, Dash, Status }

    // 한 칸 = 같은 무기의 같은 잉크 묶음(또는 보스 제어 하나)의 내부 쿨타임과 효과 지속.
    // ReadyAt·ActiveUntil 은 전투 시계(arena.Now) 기준이다. 지속이 없는 효과(즉시 회복 등)는 ActiveUntil 을 0으로 둔다.
    public readonly struct BuffSlot
    {
        public readonly BuffSlotIcon Icon;
        public readonly BuffSlotTrigger Trigger;
        public readonly float ReadyAt, Cooldown, ActiveUntil, ActiveDuration;
        public readonly string Tooltip;
        public BuffSlot(BuffSlotIcon icon, BuffSlotTrigger trigger, float readyAt, float cooldown, string tooltip = null, float activeUntil = 0, float activeDuration = 0)
        { Icon = icon; Trigger = trigger; ReadyAt = readyAt; Cooldown = cooldown; Tooltip = tooltip; ActiveUntil = activeUntil; ActiveDuration = activeDuration; }
        /// <summary>남은 내부 쿨타임(초).</summary>
        public float Remaining(float now) => ReadyAt > now ? ReadyAt - now : 0;
        // 1 = 방금 발동해 칸 전체가 어둡다, 0 = 준비됨.
        public float DarkFraction(float now) => Cooldown > 0 ? System.Math.Min(1, Remaining(now) / Cooldown) : 0;
        /// <summary>남은 효과 지속(초). 지속 중에는 HUD가 쿨타임 덮개 대신 지속 연출을 그린다.</summary>
        public float ActiveRemaining(float now) => ActiveUntil > now ? ActiveUntil - now : 0;
    }

    // 캐릭터 버프칸을 채우는 쪽(잉크 런타임)이 구현한다. HUD는 매 프레임 순서대로 읽기만 한다.
    public interface IBuffSlotSource
    {
        void Collect(List<BuffSlot> into);
    }

    public static class BossBuffSlots
    {
        // 보스 체력바 아래 두 칸: 기절, 속박.
        public static void Collect(CombatActor boss, List<BuffSlot> into)
        {
            if (!boss || !boss.isBoss) return;
            var c = boss.BossControl;
            into.Add(new BuffSlot(BuffSlotIcon.Stun, BuffSlotTrigger.None, c.StunReadyAt, c.StunSeconds));
            into.Add(new BuffSlot(BuffSlotIcon.Bind, BuffSlotTrigger.None, c.RootReadyAt, c.RootSeconds));
        }
    }
}
