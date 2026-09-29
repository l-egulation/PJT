import type { CombatRenderingEvent } from "./sessionApi";

/*
 * The battle HUD shows which active skills just fired and how long until they
 * come round again.  Nothing in the session payload carries a cooldown, but the
 * simulator's rule is fixed — every active skill goes on cooldown for
 * SKILL_COOLDOWN_TICKS from the tick it was cast — so the whole thing can be
 * read back out of the rendering events the screen is already playing.
 */
export const SKILL_COOLDOWN_TICKS = 100;

export type SkillCooldown = {
  skillId: string;
  /** The tick of the most recent cast the screen has played, or null if never. */
  castTick: number | null;
  /** True from the moment the cast plays until the cooldown runs out. */
  onCooldown: boolean;
  remainingTicks: number;
  remainingSeconds: number;
  /** 0 when just cast, 1 when ready again — for a sweep or bar. */
  progress: number;
};

export function skillCooldowns(
  activeOrder: string[],
  events: CombatRenderingEvent[],
  playedTick: number,
  tickDurationMilliseconds: number,
): SkillCooldown[] {
  const castAt = new Map<string, number>();
  for (const event of events) {
    if (event.type !== "PLAYER_SKILL_CAST_STARTED" || !event.skillId) continue;
    if (event.logicalTick > playedTick) continue;
    castAt.set(event.skillId, event.logicalTick);
  }
  return activeOrder.map(skillId => {
    const cast = castAt.get(skillId);
    const elapsed = cast === undefined ? SKILL_COOLDOWN_TICKS : playedTick - cast;
    const remainingTicks = Math.max(0, SKILL_COOLDOWN_TICKS - elapsed);
    return {
      skillId,
      castTick: cast ?? null,
      onCooldown: remainingTicks > 0,
      remainingTicks,
      /* Round up: a skill with any time left should never read "0초". */
      remainingSeconds: Math.ceil(remainingTicks * tickDurationMilliseconds / 1_000),
      progress: Math.min(1, Math.max(0, elapsed / SKILL_COOLDOWN_TICKS)),
    };
  });
}
