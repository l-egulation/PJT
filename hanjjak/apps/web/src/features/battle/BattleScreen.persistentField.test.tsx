import { renderToStaticMarkup } from "react-dom/server";
import { readFileSync } from "node:fs";
import { describe, expect, it, vi } from "vitest";
import type { CombatRenderingEvent } from "./sessionApi";
import { invalidateBattleCompletionQueries } from "./AutoBattleRuntime";
import { appendDamageStackEntries, consumeNewDamageEvents, persistentDamageLayout, persistentDotFieldLayout, PersistentBattleField, PersistentDamageStack, resetDamageEventTracking } from "./BattleScreen";

describe("PersistentBattleField", () => {
  it("owns the dot field independently from the replaceable monster", () => {
    const markup = renderToStaticMarkup(<PersistentBattleField effects={[{ kind: "dot-target", frame: 3, frameCount: 8, loop: true }]} />);

    expect(markup).toContain("persistent-battle-field");
    expect(markup).toContain("skill-vfx-dot-target");
    expect(markup).not.toContain("battle-monster");
  });

  it("uses a fixed Y position, size, and horizontal center", () => {
    expect(persistentDotFieldLayout()).toEqual({
      bottom: 138,
      size: 200,
    });
    const css = readFileSync(new URL("./BattleScreen.css", import.meta.url), "utf8");
    expect(css).toMatch(/\.persistent-battle-field \.skill-vfx-dot-target[^}]*left: calc\(50% \+ 35px\)[^}]*transform: translate\(-50%, 80%\)/);
  });
});

describe("battle completion cache refresh", () => {
  it("refreshes gem unlock state immediately after a main-stage clear", () => {
    const invalidateQueries = vi.fn().mockResolvedValue(undefined);

    invalidateBattleCompletionQueries({ invalidateQueries } as never);

    expect(invalidateQueries).toHaveBeenCalledWith({ queryKey: ["gems"] });
    expect(invalidateQueries).toHaveBeenCalledWith({ queryKey: ["cosmetic-collection"] });
  });
});

describe("PersistentDamageStack", () => {
  it("uses a fixed vertical position that does not depend on monster size", () => {
    expect(persistentDamageLayout()).toEqual({ bottom: 170 });
  });

  it("replaces regular and active-dot damage independently in one slot per lane", () => {
    const events = [
      { eventId: "damage-old", logicalTick: 10, type: "PLAYER_ATTACK_IMPACT", actor: "PLAYER", target: "ENEMY", enemyIndex: 1, boss: false, skillId: null, damage: 630, critical: false, hpBefore: 1180, hpAfter: 550, defeated: false },
      { eventId: "damage-new", logicalTick: 11, type: "DOT_TICK", actor: "PLAYER", target: "ENEMY", enemyIndex: 1, boss: false, skillId: "active_dot", damage: 315, critical: false, hpBefore: 550, hpAfter: 235, defeated: false },
      { eventId: "damage-replacement", logicalTick: 12, type: "PLAYER_SKILL_IMPACT", actor: "PLAYER", target: "ENEMY", enemyIndex: 1, boss: false, skillId: "active_heavy", damage: 720, critical: false, hpBefore: 235, hpAfter: 0, defeated: true },
    ] satisfies CombatRenderingEvent[];
    const initialEntries = appendDamageStackEntries([], events.slice(0, 2));
    const entries = appendDamageStackEntries(initialEntries, [events[2]]);
    const markup = renderToStaticMarkup(<PersistentDamageStack entries={entries} />);
    expect(markup).toContain("persistent-damage-stack");
    expect(markup).toContain("data-event-id=\"damage-new\"");
    expect(markup).toContain("data-damage-lane=\"dot\"");
    expect(markup).toContain("data-event-id=\"damage-replacement\"");
    expect(markup).toContain("data-damage-lane=\"regular\"");
    expect(markup).not.toContain("damage-old");
    expect(entries).toHaveLength(2);

    const css = readFileSync(new URL("./BattleScreen.css", import.meta.url), "utf8");
    expect(css).toMatch(/\.monster-damage\.damage-lane-dot[^}]*left: 80px/);
  });

  it("does not replay expired damage when a later rendering event arrives", () => {
    const seen = new Set<string>();
    const first = { eventId: "damage-first", logicalTick: 10, type: "PLAYER_ATTACK_IMPACT", actor: "PLAYER", target: "ENEMY", enemyIndex: 1, boss: false, skillId: null, damage: 100, critical: false, hpBefore: 200, hpAfter: 100, defeated: false } satisfies CombatRenderingEvent;
    const second = { ...first, eventId: "damage-second", logicalTick: 11, damage: 101 } satisfies CombatRenderingEvent;

    expect(consumeNewDamageEvents([first], seen)).toEqual([first]);
    expect(consumeNewDamageEvents([first, second], seen)).toEqual([second]);
    expect(consumeNewDamageEvents([first, second], seen)).toEqual([]);
  });

  it("accepts reused simulator event ids after the battle session changes", () => {
    const seen = new Set(["event-00001"]);
    const timers = new Map([["event-00001", 1]]);
    const event = { eventId: "event-00001", logicalTick: 0, type: "PLAYER_ATTACK_IMPACT", actor: "PLAYER", target: "ENEMY", enemyIndex: 1, boss: false, skillId: null, damage: 100, critical: false, hpBefore: 200, hpAfter: 100, defeated: false } satisfies CombatRenderingEvent;

    resetDamageEventTracking(seen, timers);

    expect(consumeNewDamageEvents([event], seen)).toEqual([event]);
    expect(timers.size).toBe(0);
  });
});
