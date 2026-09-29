import { describe, expect, it } from "vitest";
import { renderToStaticMarkup } from "react-dom/server";
import { readFileSync } from "node:fs";
import type { CombatRenderingEvent } from "../battle/sessionApi";
import type { GemDungeonCombatEvent } from "./api";
import { HEAVY_VFX_DURATION_TICKS, SkillVfxSprite, skillVfxForEvents, skillVfxForMainBattle, skillVfxSpriteStyle } from "./skillVfx";

function event(type: GemDungeonCombatEvent["type"], tick: number, skillId: string | null): GemDungeonCombatEvent {
  return { sequence: tick, tick, type, skillId, amount: 1, playerHp: 1, bossHp: 1, critical: false };
}

function mainEvent(type: string, tick: number, skillId: string | null, enemyIndex = 1): CombatRenderingEvent {
  return { eventId: `event-${tick}-${enemyIndex}`, logicalTick: tick, type, actor: "PLAYER", target: "ENEMY", enemyIndex, boss: false, skillId, damage: 1, critical: false, hpBefore: 2, hpAfter: 1, defeated: false };
}

describe("battle skill VFX timeline", () => {
  it.each(["active-basic-amp-caster-8f.png", "active-heavy-target-8f.png"])("keeps %s in eight undistorted square cells", (fileName) => {
    const png = readFileSync(new URL(`../../../public/assets/effects/skills-v1/${fileName}`, import.meta.url));
    expect(png.readUInt32BE(16)).toBe(png.readUInt32BE(20) * 8);
  });

  it("plays the confirmed eight-frame heavy impact on the target once", () => {
    const events = [event("PLAYER_HIT", 10, "active_heavy")];
    expect(skillVfxForEvents(events, 10)).toMatchObject({
      caster: [],
      target: [{ kind: "heavy-target", frame: 0, frameCount: 8 }],
    });
    expect(skillVfxForEvents(events, 13).target[0]?.frame).toBe(7);
    expect(skillVfxForEvents(events, 10 + HEAVY_VFX_DURATION_TICKS - 1).target[0]?.frame).toBe(7);
    expect(skillVfxForEvents(events, 10 + HEAVY_VFX_DURATION_TICKS)).toEqual({ caster: [], target: [] });
    expect(readFileSync(new URL("../../styles.css", import.meta.url), "utf8")).toContain("skill-vfx-intro-8 1200ms");
  });

  it("keeps the confirmed dot animation on the target for the existing five-second timeline", () => {
    const cast = event("SKILL_CAST", 10, "active_dot");
    const hit = event("DOT_HIT", 20, "active_dot");
    const finalHit = event("DOT_HIT", 60, "active_dot");
    expect(skillVfxForEvents([cast], 10)).toMatchObject({ caster: [], target: [{ kind: "dot-target", frame: 0, frameCount: 8, loop: true }] });
    expect(skillVfxForEvents([cast, hit], 20).target[0]).toMatchObject({ kind: "dot-target", frame: 2, frameCount: 8, loop: true });
    expect(skillVfxForEvents([cast, hit], 20).target[0]?.impact).toBe(true);
    expect(skillVfxForEvents([cast, hit], 23).target[0]?.impact).toBe(false);
    expect(skillVfxForEvents([cast, hit], 59).target[0]).toMatchObject({ kind: "dot-target", frame: 1, frameCount: 8, loop: true });
    expect(skillVfxForEvents([cast, hit], 60).target).toEqual([]);
    expect(skillVfxForEvents([cast, finalHit], 60).target[0]).toMatchObject({ kind: "dot-target", frame: 2, frameCount: 8, loop: true });
    expect(skillVfxForEvents([cast, finalHit], 62).target[0]?.kind).toBe("dot-target");
    expect(skillVfxForEvents([cast, finalHit], 63).target).toEqual([]);
  });

  it.each([
    ["active_haste", "haste-caster"],
    ["active_basic_amp", "basic-amp-caster"],
  ] as const)("loops %s for five seconds and blinks during its final three seconds", (skillId, kind) => {
    const events = [event("SKILL_CAST", 5, skillId)];
    expect(skillVfxForEvents(events, 5).caster[0]).toMatchObject({ kind, frame: 0, frameCount: 8, loop: true, expiring: false });
    expect(skillVfxForEvents(events, 24).caster[0]).toMatchObject({ kind, frame: 3, loop: true, expiring: false });
    expect(skillVfxForEvents(events, 25).caster[0]).toMatchObject({ kind, frame: 4, loop: true, expiring: true });
    expect(skillVfxForEvents(events, 54).caster[0]).toMatchObject({ kind, frame: 1, frameCount: 8, loop: true, expiring: true });
    expect(skillVfxForEvents(events, 55).caster).toEqual([]);
  });

  it("addresses every cell in an eight-frame horizontal sheet", () => {
    expect(skillVfxSpriteStyle({ kind: "dot-target", frame: 7, frameCount: 8 })).toMatchObject({
      backgroundSize: "800% 100%",
      backgroundPosition: "100% center",
    });
  });

  it.each([
    ["heavy-target", "active-heavy-target-8f.png?v=20260911"],
    ["dot-target", "active-dot-target-8f.png"],
    ["haste-caster", "haste-caster-8f.png"],
    ["basic-amp-caster", "active-basic-amp-caster-8f.png"],
  ] as const)("maps %s to the requested eight-frame sheet", (kind, fileName) => {
    expect(skillVfxSpriteStyle({ kind, frame: 0, frameCount: 8 })).toMatchObject({
      backgroundImage: `url(/assets/effects/skills-v1/${fileName})`,
      backgroundSize: "800% 100%",
    });
  });

  it("marks persistent effects as looping and expiring in their final three seconds", () => {
    const markup = renderToStaticMarkup(SkillVfxSprite({ effect: { kind: "haste-caster", frame: 4, frameCount: 8, loop: true, expiring: true } }));
    expect(markup).toContain("skill-vfx-animated-8 skill-vfx-looping-8 skill-vfx-expiring");
  });

  it("marks a dot damage frame with the brief impact-emphasis class", () => {
    const markup = renderToStaticMarkup(SkillVfxSprite({ effect: { kind: "dot-target", frame: 4, frameCount: 8, loop: true, impact: true } }));
    expect(markup).toContain("skill-vfx-dot-target");
    expect(markup).toContain("skill-vfx-impact");
  });

  it("adapts the authoritative main-battle skill events to the shared VFX timeline", () => {
    const events = [
      mainEvent("PLAYER_SKILL_CAST_STARTED", 10, "active_dot"),
      mainEvent("BUFF_STARTED", 14, "active_haste"),
      mainEvent("BUFF_STARTED", 18, "active_basic_amp"),
      mainEvent("DOT_TICK", 20, "active_dot"),
      mainEvent("PLAYER_SKILL_IMPACT", 22, "active_heavy"),
    ];

    expect(skillVfxForMainBattle(events, 22)).toMatchObject({
      caster: [{ kind: "haste-caster" }, { kind: "basic-amp-caster" }],
      target: [{ kind: "heavy-target" }, { kind: "dot-target", frame: 4, frameCount: 8, loop: true }],
    });
    expect(skillVfxForMainBattle(events, 59).target[0]?.kind).toBe("dot-target");
    expect(skillVfxForMainBattle(events, 60).target).toEqual([]);
    expect(skillVfxForMainBattle(events, 67).caster.map((effect) => effect.kind)).toEqual(["basic-amp-caster"]);
    expect(skillVfxForMainBattle(events, 68).caster).toEqual([]);
    expect(skillVfxForMainBattle(events, 22, 2).target).toMatchObject([{ kind: "dot-target" }]);
  });

  it("plays alternating target-scaled slash VFX for main-battle basic attacks", () => {
    const first = mainEvent("PLAYER_ATTACK_IMPACT", 10, null, 1);
    const second = mainEvent("PLAYER_ATTACK_IMPACT", 20, null, 1);
    const otherEnemy = mainEvent("PLAYER_ATTACK_IMPACT", 21, null, 2);

    expect(skillVfxForMainBattle([first], 10, 1).target).toMatchObject([
      { kind: "basic-target", direction: "up-right" },
    ]);
    expect(skillVfxForMainBattle([first, first], 10, 1).target).toMatchObject([
      { kind: "basic-target", direction: "up-right" },
    ]);
    expect(skillVfxForMainBattle([first], 13, 1).target).toEqual([]);
    expect(skillVfxForMainBattle([first, second], 20, 1).target).toMatchObject([
      { kind: "basic-target", direction: "up-left" },
    ]);
    expect(skillVfxForMainBattle([otherEnemy], 21, 1).target).toEqual([]);
    expect(renderToStaticMarkup(SkillVfxSprite({ effect: { kind: "basic-target", frame: 0, frameCount: 1, direction: "up-left" } }))).toContain("skill-vfx-basic-target skill-vfx-up-left");
    const css = readFileSync(new URL("../battle/BattleScreen.css", import.meta.url), "utf8");
    expect(css).toContain("width: var(--monster-basic-vfx-size)");
    expect(css).toContain("basic-strike-up-right 240ms");
    expect(css).toContain("basic-strike-up-left 240ms");
  });

  it("moves the dot mark to the current event target without restarting its duration", () => {
    const events = [
      mainEvent("PLAYER_SKILL_CAST_STARTED", 10, "active_dot", 1),
      mainEvent("DOT_TICK", 20, "active_dot", 1),
      mainEvent("DOT_TICK", 30, "active_dot", 2),
      mainEvent("PLAYER_SKILL_IMPACT", 31, "active_heavy", 1),
    ];

    expect(skillVfxForMainBattle(events, 30, 2).target).toMatchObject([{ kind: "dot-target", frame: 4, frameCount: 8, loop: true }]);
    expect(skillVfxForMainBattle(events, 59, 2).target[0]?.kind).toBe("dot-target");
    expect(skillVfxForMainBattle(events, 60, 2).target).toEqual([]);
  });
});
