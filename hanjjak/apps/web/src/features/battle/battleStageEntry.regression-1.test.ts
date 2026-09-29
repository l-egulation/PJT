import { describe, expect, it } from "vitest";
import type { CombatRenderingEvent } from "./sessionApi";
import { heroMotionForEncounter } from "./battleVisuals";

// Regression: ISSUE-004 — the hero was already standing in place when a stage began
// Found by /qa on 2026-09-09
// Report: .gstack/qa-reports/qa-report-127-0-0-1-2026-09-09.md
describe("stage-opening hero motion", () => {
  const spawn = (enemyIndex: number): CombatRenderingEvent => ({
    eventId: `spawn-${enemyIndex}`,
    logicalTick: enemyIndex,
    type: "ENEMY_SPAWNED",
    actor: "ENEMY",
    target: null,
    enemyIndex,
    boss: false,
    skillId: null,
    damage: null,
    critical: false,
    hpBefore: 100,
    hpAfter: 100,
    defeated: false,
  });

  it("runs in for the first enemy, then waits in place for later spawns", () => {
    expect(heroMotionForEncounter([spawn(1)], true, 1)).toBe("run2");
    expect(heroMotionForEncounter([spawn(2)], true, 2)).toBe("rest");
  });
});
