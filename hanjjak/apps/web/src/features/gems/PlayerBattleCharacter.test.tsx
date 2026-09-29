import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import type { Catalog, Collection } from "../cosmetics/api";
import { PlayerBattleCharacter } from "./PlayerBattleCharacter";
import { composeCosmeticHeroMotion } from "../cosmetics/cosmetic-art";

const SLOTS = ["HEAD", "TOP", "BOTTOM", "GLOVES", "SHOES", "CAPE"] as const;

/** 한 세트의 여섯 부위를 담은 카탈로그. 조합은 setId와 slot만 보고 레이어를 고른다. */
const catalogFor = (setId: string, ids: string[]): Pick<Catalog, "cosmetics" | "sets"> => ({
  cosmetics: ids.map((cosmeticId, index) => ({ cosmeticId, displayName: null, imageUrl: null, grade: "RARE", slot: SLOTS[index], setId })),
  sets: [{ setId, displayName: null, grade: "RARE", members: {}, effects: {} }],
});

const turtleIds = ["cosmetic-061", "cosmetic-062", "cosmetic-063", "cosmetic-064", "cosmetic-065", "cosmetic-066"];
// 악마는 아직 아트가 붙지 않아 멈춘 조합으로 간다. 정지 경로를 보려면 이쪽을 쓴다.
const stillCatalog = catalogFor("cosmetic-set-05", turtleIds);
const wearing = (ids: Array<string | null>): Pick<Collection, "equipment"> => ({
  equipment: Object.fromEntries(SLOTS.map((slot, index) => [slot, ids[index]])),
}) as Pick<Collection, "equipment">;

describe("PlayerBattleCharacter", () => {
  it("draws the equipped set with the same layers the cosmetics tab uses", () => {
    const html = renderToStaticMarkup(<PlayerBattleCharacter appearance={wearing(turtleIds)} catalog={stillCatalog} motion="rest" hit={false} skillVfx={[]} />);
    expect(html).toContain("/cosmetics/layers/demon-head.png");
    expect(html).toContain("/cosmetics/layers/demon-cape.png");
    expect(html).toContain("/cosmetics/layers/base.png");
    expect(html).not.toContain("master-hero");
  });

  it("draws a part-worn loadout instead of falling back to the bare character", () => {
    const partial = wearing([turtleIds[0], null, null, null, null, null]);
    const html = renderToStaticMarkup(<PlayerBattleCharacter appearance={partial} catalog={stillCatalog} motion="rest" hit={false} skillVfx={[]} />);
    expect(html).toContain("/cosmetics/layers/demon-head.png");
    // 무기 부위가 비어 있으면 기본 나무검을 쥐여 준다.
    expect(html).toContain("/cosmetics/layers/weapon-default.png");
    expect(html).not.toContain("master-hero");
  });

  it("keeps the animated default character when nothing is worn", () => {
    const html = renderToStaticMarkup(<PlayerBattleCharacter appearance={wearing([null, null, null, null, null, null])} catalog={stillCatalog} motion="rest" hit={false} skillVfx={[]} />);
    expect(html).toContain("master-hero");
    expect(html).not.toContain("/cosmetics/layers/");
  });

  it("keeps the animated default character until the catalogue arrives", () => {
    const html = renderToStaticMarkup(<PlayerBattleCharacter appearance={wearing(turtleIds)} motion="rest" hit={false} skillVfx={[]} />);
    expect(html).toContain("master-hero");
  });
});

describe("cosmetic motion frames", () => {
  const motionCatalog = catalogFor("cosmetic-set-01", turtleIds);

  it("swaps every layer as the frame advances", () => {
    const frames = [0, 1, 2, 3].map(frame => composeCosmeticHeroMotion(wearing(turtleIds).equipment, motionCatalog, "strike1", frame));
    expect(frames.every(Boolean)).toBe(true);
    expect(frames[0]).toContain("/cosmetics/motion/leather-guard/strike1_01-hat.png");
    expect(frames[3]).toContain("/cosmetics/motion/leather-guard/strike1_04-hat.png");
    // 맨몸도 같은 프레임을 따라간다. 옷만 움직이면 몸이 따로 논다.
    expect(frames[2]).toContain("/cosmetics/motion/body/strike1_03.png");
    expect(new Set(frames.map(layers => layers!.join("|"))).size).toBe(4);
  });

  it("keeps the cape behind the body and hands a default weapon to an empty weapon slot", () => {
    const noWeapon = wearing([turtleIds[0], turtleIds[1], null, turtleIds[3], turtleIds[4], turtleIds[5]]);
    const layers = composeCosmeticHeroMotion(noWeapon.equipment, motionCatalog, "rest", 0)!;
    expect(layers[0]).toBe("/cosmetics/motion/leather-guard/rest_01-back.png");
    // 기본 나무검도 몸통 뒤에 깔린다. 맨 위에 얹으면 칼이 가슴 앞을 가로지른다.
    expect(layers[1]).toBe("/cosmetics/motion/weapon-default/rest_01.png");
    expect(layers[2]).toBe("/cosmetics/motion/body/rest_01.png");
  });

  it("refuses a set whose motion art is not in yet, so nothing animates half way", () => {
    expect(composeCosmeticHeroMotion(wearing(turtleIds).equipment, stillCatalog, "rest", 0)).toBeNull();
  });

  it("animates the equipped set end to end in battle", () => {
    const html = renderToStaticMarkup(<PlayerBattleCharacter appearance={wearing(turtleIds)} catalog={motionCatalog} motion="strike1" hit={false} skillVfx={[]} />);
    expect(html).toContain("/cosmetics/motion/leather-guard/strike1_01-hat.png");
    expect(html).not.toContain("master-hero");
  });
});

describe("mixed loadouts still animate", () => {
  // 천사·약과를 살릴 때 이 둘만 낱장 모션이 없어, 다른 세트와 섞어 입으면 캐릭터가
  // 대기 자세로 굳었다. 섞어 입기는 낱장 경로가 맡으므로 모든 활성 세트에 낱장이 있어야 한다.
  const mixedCatalog: Pick<Catalog, "cosmetics" | "sets"> = {
    cosmetics: [
      { cosmeticId: "a-head", displayName: null, imageUrl: null, grade: "RARE", slot: "HEAD", setId: "cosmetic-set-06" },
      { cosmeticId: "a-top", displayName: null, imageUrl: null, grade: "RARE", slot: "TOP", setId: "cosmetic-set-06" },
      { cosmeticId: "a-cape", displayName: null, imageUrl: null, grade: "RARE", slot: "CAPE", setId: "cosmetic-set-06" },
      { cosmeticId: "y-weapon", displayName: null, imageUrl: null, grade: "EPIC", slot: "BOTTOM", setId: "cosmetic-set-10" },
      { cosmeticId: "y-gloves", displayName: null, imageUrl: null, grade: "EPIC", slot: "GLOVES", setId: "cosmetic-set-10" },
      { cosmeticId: "c-shoes", displayName: null, imageUrl: null, grade: "EPIC", slot: "SHOES", setId: "cosmetic-set-08" },
    ],
    sets: ["cosmetic-set-06", "cosmetic-set-10", "cosmetic-set-08"].map(setId => ({ setId, displayName: null, grade: "RARE", members: {}, effects: {} })),
  };
  const mixed = { HEAD: "a-head", TOP: "a-top", CAPE: "a-cape", BOTTOM: "y-weapon", GLOVES: "y-gloves", SHOES: "c-shoes" };

  it("animates a loadout mixing three sets", () => {
    const layers = composeCosmeticHeroMotion(mixed, mixedCatalog, "strike1", 2);
    expect(layers).not.toBeNull();
    expect(layers!.some(l => l.includes("/motion/angel/strike1_03-hat.png"))).toBe(true);
    expect(layers!.some(l => l.includes("/motion/yakgwa/strike1_03-weapon.png"))).toBe(true);
    expect(layers!.some(l => l.includes("/motion/chef/strike1_03-shoes.png"))).toBe(true);
  });

  it("lays the top's own arms over the clothing and under the gloves", () => {
    // 천사·약과 상의는 마네킹 팔을 덮는다. 그 세트의 팔을 상의 위에 다시 얹되 장갑보다는
    // 아래여야, 다른 세트 장갑을 껴도 손목에 제대로 걸린다.
    const layers = composeCosmeticHeroMotion(mixed, mixedCatalog, "strike1", 2)!;
    const top = layers.indexOf("/cosmetics/motion/angel/strike1_03-body.png");
    const shoes = layers.indexOf("/cosmetics/motion/chef/strike1_03-shoes.png");
    const arms = layers.indexOf("/cosmetics/motion/angel/strike1_03-arms.png");
    const gloves = layers.indexOf("/cosmetics/motion/yakgwa/strike1_03-gloves.png");
    expect(top).toBeGreaterThanOrEqual(0);
    expect(arms).toBeGreaterThan(shoes);
    expect(shoes).toBeGreaterThan(top);
    expect(gloves).toBeGreaterThan(arms);
  });

  it("leaves the arms out when the top does not cover them", () => {
    // 요리사 상의에 천사 장갑. 팔을 한 겹 더 얹으면 마네킹 팔과 두 겹이 된다.
    const catalog = { ...mixedCatalog, cosmetics: mixedCatalog.cosmetics.map(item => item.slot === "TOP" ? { ...item, setId: "cosmetic-set-08" } : item) };
    const layers = composeCosmeticHeroMotion(mixed, catalog, "rest", 0)!;
    expect(layers.some(layer => layer.includes("-arms.png"))).toBe(false);
  });
});

describe("motion coverage matches the sets the game ships", () => {
  const SETS_IN_GAME: Array<[string, string]> = [
    ["cosmetic-set-01", "leather-guard"], ["cosmetic-set-02", "scrap-knight"], ["cosmetic-set-03", "bamboo-spear"],
    ["cosmetic-set-07", "turtle-guardian"], ["cosmetic-set-08", "chef"], ["cosmetic-set-11", "sushi"],
  ];

  it("animates every set that is active in the content file", () => {
    for (const [setId, slug] of SETS_IN_GAME) {
      const layers = composeCosmeticHeroMotion(wearing(turtleIds).equipment, catalogFor(setId, turtleIds), "rest", 0);
      expect(layers, `${setId} (${slug})`).not.toBeNull();
      expect(layers!.some(layer => layer.includes(`/motion/${slug}/`))).toBe(true);
    }
  });

  it("puts the sushi weapon behind the body the way the cosmetics tab does", () => {
    const layers = composeCosmeticHeroMotion(wearing(turtleIds).equipment, catalogFor("cosmetic-set-11", turtleIds), "strike1", 1)!;
    expect(layers.indexOf("/cosmetics/motion/sushi/strike1_02-weapon.png")).toBeLessThan(layers.indexOf("/cosmetics/motion/body/strike1_02.png"));
  });
});
