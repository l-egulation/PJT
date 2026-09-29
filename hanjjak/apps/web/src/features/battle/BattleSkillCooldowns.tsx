import { useEffect, useRef, useState, type CSSProperties } from "react";
import { SkillArtwork } from "../skills/SkillsScreen";
import { SKILL_COOLDOWN_TICKS, type SkillCooldown } from "./skillCooldown";

/*
 * A small rail of the active skills the auto battle is cycling through.  A
 * skill greys out the moment its cast plays and counts back to ready, so the
 * fight reads as a rotation rather than a wall of effects.
 *
 * The countdown runs on the clock rather than on the battle's tick number.
 * Ticks only advance when an event arrives and can even step back when the
 * screen falls back to an older event, which made the seconds stutter and
 * sometimes climb — 6, then 7, then 6 again.
 */
type Deadline = { castTick: number; readyAt: number; totalMilliseconds: number };

function useCooldownClock(cooldowns: SkillCooldown[], tickDurationMilliseconds: number) {
  const deadlines = useRef(new Map<string, Deadline>());
  const [now, setNow] = useState(() => Date.now());

  for (const cooldown of cooldowns) {
    const known = deadlines.current.get(cooldown.skillId);
    if (cooldown.castTick === null) {
      deadlines.current.delete(cooldown.skillId);
      continue;
    }
    // A new cast restarts the clock; the same cast keeps the deadline it had.
    if (!known || known.castTick !== cooldown.castTick) {
      deadlines.current.set(cooldown.skillId, {
        castTick: cooldown.castTick,
        readyAt: Date.now() + cooldown.remainingTicks * tickDurationMilliseconds,
        totalMilliseconds: SKILL_COOLDOWN_TICKS * tickDurationMilliseconds,
      });
    }
  }

  const counting = cooldowns.some(cooldown => (deadlines.current.get(cooldown.skillId)?.readyAt ?? 0) > now);
  useEffect(() => {
    if (!counting) return;
    const timer = window.setInterval(() => setNow(Date.now()), 100);
    return () => window.clearInterval(timer);
  }, [counting]);

  return cooldowns.map(cooldown => {
    const deadline = deadlines.current.get(cooldown.skillId);
    const remaining = deadline ? Math.max(0, deadline.readyAt - now) : 0;
    return {
      skillId: cooldown.skillId,
      onCooldown: remaining > 0,
      /* Round up so anything still running reads at least 1초. */
      remainingSeconds: Math.ceil(remaining / 1_000),
      progress: deadline ? Math.min(1, Math.max(0, 1 - remaining / deadline.totalMilliseconds)) : 1,
    };
  });
}

export function BattleSkillCooldowns({ cooldowns, names, tickDurationMilliseconds }: {
  cooldowns: SkillCooldown[];
  names: Record<string, string>;
  tickDurationMilliseconds: number;
}) {
  const live = useCooldownClock(cooldowns, tickDurationMilliseconds);
  if (live.length === 0) return null;
  return <ul className="battle-skill-cooldowns" aria-label="사용한 스킬과 재사용 시간">
    {live.map(cooldown => {
      const name = names[cooldown.skillId] ?? cooldown.skillId;
      return <li
        key={cooldown.skillId}
        className={cooldown.onCooldown ? "is-cooling" : "is-ready"}
        style={{ "--skill-cooldown-progress": cooldown.progress } as CSSProperties}
      >
        <SkillArtwork skillId={cooldown.skillId} name={name} compact />
        <i aria-hidden="true" />
        {cooldown.onCooldown
          ? <b aria-label={`${name} 재사용까지 ${cooldown.remainingSeconds}초`}>{cooldown.remainingSeconds}</b>
          : <span className="battle-skill-ready" aria-label={`${name} 사용 가능`} />}
      </li>;
    })}
  </ul>;
}
