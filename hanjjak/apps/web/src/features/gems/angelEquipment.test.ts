import { describe, expect, it } from "vitest";
import {
  createEquipmentCatalog,
  defaultAngelLoadout,
  dungeonMotionForEvent,
  frameIndexForMotion,
  resolveFrameLayers,
  type EquipmentManifest,
} from "./angelEquipment";
import type { GemDungeonCombatEvent } from "./api";

const manifest = {
  packageId: "angel",
  frames: { rest_01: { motion: "rest", frame: 1, x: 0, y: 672, width: 928, height: 672 } },
  drawOrderByFrame: { rest_01: ["slot.cape", "base.body", "slot.glove.screen-left", "slot.glove.screen-right", "slot.hat"] },
  atlases: {
    cape: {
      slot: "cape",
      image: "cape.png",
      motionSheets: {
        rest: "motion/cape-rest.png",
        run2: "motion/cape-run2.png",
        strike1: "motion/cape-strike1.png",
        thrust1: "motion/cape-thrust1.png",
        death1: "motion/cape-death1.png",
      },
    },
    "base-body": { technical: true, image: "body.png" },
    "glove-left": { slot: "glove", sublayer: "screen-left", image: "glove-left.png" },
    "glove-right": { slot: "glove", sublayer: "screen-right", image: "glove-right.png" },
    hat: { slot: "hat", image: "hat.png" },
  },
  items: [
    { id: "angel-wings", setId: "angel", slot: "cape", atlas: "cape" },
    { id: "angel-wrist-rings", setId: "angel", slot: "glove", atlases: ["glove-left", "glove-right"] },
    { id: "angel-halo", setId: "angel", slot: "hat", atlas: "hat" },
  ],
} as unknown as EquipmentManifest;

describe("angel equipment runtime", () => {
  it("keeps manifest draw order and resolves both glove sublayers", () => {
    const catalog = createEquipmentCatalog({ baseUrl: "/angel", manifest });
    const layers = resolveFrameLayers("angel", catalog, "rest", 1, defaultAngelLoadout);
    expect(layers.map((layer) => layer.atlasId)).toEqual(["cape", "base-body", "glove-left", "glove-right", "hat"]);
    expect(layers[0]).toMatchObject({ imageUrl: "/angel/motion/cape-rest.png", frame: { y: 0 } });
    expect(layers[1]).toMatchObject({ imageUrl: "/angel/body.png", frame: { y: 672 } });
  });

  it("can resolve an item from another registered set without replacing other slots", () => {
    const demonManifest = {
      ...manifest,
      packageId: "demon",
      atlases: { "demon-hat": { slot: "hat", image: "demon-hat.png" } },
      items: [{ id: "demon-horns", setId: "demon", slot: "hat", atlas: "demon-hat" }],
    } as unknown as EquipmentManifest;
    const catalog = createEquipmentCatalog(
      { baseUrl: "/angel", manifest },
      { baseUrl: "/demon", manifest: demonManifest },
    );
    const layers = resolveFrameLayers("angel", catalog, "rest", 1, {
      ...defaultAngelLoadout,
      hat: "demon-horns",
    });
    expect(layers.find((layer) => layer.token === "base.body")?.packageId).toBe("angel");
    expect(layers.find((layer) => layer.token === "slot.hat")).toMatchObject({
      packageId: "demon",
      imageUrl: "/demon/demon-hat.png",
    });
  });

  it("unequips one slot without changing the others", () => {
    const catalog = createEquipmentCatalog({ baseUrl: "/angel", manifest });
    const layers = resolveFrameLayers("angel", catalog, "rest", 1, { ...defaultAngelLoadout, glove: null });
    expect(layers.map((layer) => layer.atlasId)).toEqual(["cape", "base-body", "hat"]);
  });

  it("maps live dungeon events to the exported motions", () => {
    const event = (type: GemDungeonCombatEvent["type"], tick: number, skillId: string | null = null): GemDungeonCombatEvent => ({ sequence: 1, tick, type, amount: 0, playerHp: 1, bossHp: 1, critical: false, skillId });
    expect(dungeonMotionForEvent(event("PLAYER_HIT", 10), 12).motion).toBe("strike1");
    expect(dungeonMotionForEvent(event("SKILL_CAST", 10, "active_dot"), 12).motion).toBe("thrust1");
    expect(dungeonMotionForEvent(event("SKILL_CAST", 10, "active_haste"), 12).motion).toBe("rest");
    expect(dungeonMotionForEvent(event("SKILL_CAST", 10, "active_basic_amp"), 12).motion).toBe("rest");
    expect(dungeonMotionForEvent(event("DEFEAT", 10), 30).motion).toBe("death1");
    expect(dungeonMotionForEvent(event("BOSS_HIT", 10), 12).motion).toBe("rest");
    expect(dungeonMotionForEvent(event("PLAYER_HIT", 10), 16).motion).toBe("run2");
    expect(dungeonMotionForEvent(event("PLAYER_HIT", 10), 20).motion).toBe("rest");
  });

  it("plays four frames and holds the final death frame", () => {
    expect([0, 120, 240, 360].map((elapsed) => frameIndexForMotion("strike1", elapsed, 0))).toEqual([0, 1, 2, 3]);
    expect(frameIndexForMotion("death1", 999, 0)).toBe(3);
  });
});
