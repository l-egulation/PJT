// @vitest-environment happy-dom

import { act, renderHook } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { CombatRenderingEvent } from "./sessionApi";
import { useBossArrivalWarning } from "./BattleScreen";

function bossSpawn(eventId: string): CombatRenderingEvent {
  return {
    eventId,
    logicalTick: 400,
    type: "BOSS_SPAWNED",
    actor: "ENEMY",
    target: "PLAYER",
    enemyIndex: 21,
    boss: true,
    skillId: null,
    damage: null,
    critical: false,
    hpBefore: null,
    hpAfter: null,
    defeated: false,
  };
}

describe("boss arrival warning event flow", () => {
  afterEach(() => vi.useRealTimers());

  it.each([
    ["stage.03-04", "ruby-jam-sand-queen", false, 2800],
    ["stage.04-10", "futomaki-king", true, 4200],
  ] as const)("shows and automatically hides the %s boss warning", (stageId, monsterId, finalBoss, duration) => {
    vi.useFakeTimers();
    const event = bossSpawn(`boss-${stageId}`);
    const { result, rerender } = renderHook(
      ({ events }) => useBossArrivalWarning(events, stageId),
      { initialProps: { events: [] as CombatRenderingEvent[] } },
    );

    expect(result.current).toBeNull();
    rerender({ events: [event] });
    expect(result.current).toEqual({ eventId: event.eventId, monsterId, finalBoss });

    act(() => vi.advanceTimersByTime(duration - 1));
    expect(result.current).not.toBeNull();
    act(() => vi.advanceTimersByTime(1));
    expect(result.current).toBeNull();

    rerender({ events: [event] });
    expect(result.current).toBeNull();
  });
});
