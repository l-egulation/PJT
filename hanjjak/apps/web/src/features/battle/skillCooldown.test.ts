import { describe, expect, it } from "vitest";
import { skillCooldowns, SKILL_COOLDOWN_TICKS } from "./skillCooldown";
import type { CombatRenderingEvent } from "./sessionApi";

function cast(skillId: string, logicalTick: number): CombatRenderingEvent {
  return {
    eventId: `${skillId}-${logicalTick}`, logicalTick, type: "PLAYER_SKILL_CAST_STARTED", actor: "PLAYER",
    target: null, enemyIndex: null, boss: false, skillId, damage: null, critical: false,
    hpBefore: null, hpAfter: null, defeated: false,
  };
}
const ORDER = ["active_haste", "active_heavy"];

describe("skillCooldowns", () => {
  it("reports a skill as ready before it has ever been cast", () => {
    const [haste] = skillCooldowns(ORDER, [], 0, 100);
    expect(haste).toMatchObject({ skillId: "active_haste", onCooldown: false, remainingTicks: 0, progress: 1 });
  });

  it("puts a skill on cooldown the moment its cast plays", () => {
    const [haste, heavy] = skillCooldowns(ORDER, [cast("active_haste", 40)], 40, 100);
    expect(haste).toMatchObject({ onCooldown: true, remainingTicks: SKILL_COOLDOWN_TICKS, remainingSeconds: 10, progress: 0 });
    expect(heavy.onCooldown).toBe(false);
  });

  it("counts the cooldown down and rounds the seconds up so it never reads zero early", () => {
    const [haste] = skillCooldowns(ORDER, [cast("active_haste", 40)], 135, 100);
    expect(haste.remainingTicks).toBe(5);
    expect(haste.remainingSeconds).toBe(1);
    expect(haste.onCooldown).toBe(true);
  });

  it("becomes ready again once the full cooldown has passed", () => {
    const [haste] = skillCooldowns(ORDER, [cast("active_haste", 40)], 140, 100);
    expect(haste).toMatchObject({ onCooldown: false, remainingTicks: 0, remainingSeconds: 0, progress: 1 });
  });

  it("ignores casts the screen has not played yet and keeps only the latest one", () => {
    const events = [cast("active_haste", 10), cast("active_haste", 60), cast("active_haste", 400)];
    const [haste] = skillCooldowns(ORDER, events, 100, 100);
    expect(haste.remainingTicks).toBe(SKILL_COOLDOWN_TICKS - 40);
  });
});

describe("cast tick", () => {
  it("reports the cast the clock should count from, and nothing before the first cast", () => {
    const [haste, heavy] = skillCooldowns(ORDER, [cast("active_haste", 10), cast("active_haste", 60)], 100, 100);
    expect(haste.castTick).toBe(60);
    expect(heavy.castTick).toBeNull();
  });
});
