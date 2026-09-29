import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { GemOptions, inventoryGemGlyph, inventoryItemAsset, inventoryMaterialRank } from "./InventoryScreen";
import type { InventoryItem } from "./api";

const item: InventoryItem = {
  itemId: "gem:1:flat_attack", instanceId: null, slotId: "instance:one", acquiredSequence: 1,
  name: "보석", icon: "/gem.png", category: "GEM", description: "", acquisitionSources: [], usages: [],
  tradeable: true, totalQuantity: 2, reservedQuantity: 1, availableQuantity: 1,
  members: [{ instanceId: "one", reservedForSale: true }, { instanceId: "two", reservedForSale: false }],
};

describe("inventory gem options", () => {
  it("shows the shared option once without individual ids or status summaries", () => {
    const html = renderToStaticMarkup(<GemOptions item={item} gems={[
      { gemId: "one", level: 1, option: "FLAT_ATTACK", optionName: "공격력", value: 3, locked: true, reservedForSale: true, equippedPresets: ["MAIN"] },
      { gemId: "two", level: 1, option: "FLAT_ATTACK", optionName: "공격력", value: 3, locked: false, reservedForSale: false, equippedPresets: [] },
    ]} />);
    expect(html).toContain("Lv.1 공격력 3");
    for (const text of ["판매 예약", "개별 보석", "잠금", "장착", "보석 ID", "<button"]) expect(html).not.toContain(text);
    expect(html.match(/Lv\.1/g)).toHaveLength(1);
  });

  it("does not invent states for legacy or missing gem records", () => {
    const html = renderToStaticMarkup(<GemOptions item={item} gems={[]} />);
    expect(html).toContain("보석 정보 미확인");
    expect(html).not.toContain("미장착");
  });
});

describe("inventory material grade art", () => {
  const material = (itemId: string): InventoryItem => ({
    ...item,
    itemId,
    name: itemId,
    icon: "/legacy-generic-material.png",
    category: "MATERIAL",
    members: [],
  });

  it("uses distinct rank artwork instead of the legacy generic material icon", () => {
    const assets = ["POTATO", "SWEET_POTATO", "CORN"].flatMap((family) =>
      [1, 2, 3, 4, 5].map((generation) => inventoryItemAsset(material(`${family}_M${generation}`))),
    );

    expect(new Set(assets).size).toBe(assets.length);
    expect(assets).not.toContain("/legacy-generic-material.png");
  });

  it.each([
    ["M1", "F"],
    ["M2", "D"],
    ["M3", "C"],
    ["M4", "B"],
    ["M5", "A"],
  ] as const)("maps %s materials to the %s display rank across every family", (generation, rank) => {
    for (const family of ["POTATO", "SWEET_POTATO", "CORN"]) {
      expect(inventoryMaterialRank(`${family}_${generation}`)).toBe(rank);
    }
  });

  it("keeps the API icon for non-material inventory items", () => {
    expect(inventoryItemAsset({ ...item, category: "COSMETIC_BOX", icon: "/cosmetic.png" })).toBe("/cosmetic.png");
  });
});

describe("inventory material grade label", () => {
  it("maps material generations M1-M5 to F-A ranks", () => {
    expect([
      inventoryMaterialRank("POTATO_M1"),
      inventoryMaterialRank("SWEET_POTATO_M2"),
      inventoryMaterialRank("CORN_M3"),
      inventoryMaterialRank("POTATO_M4"),
      inventoryMaterialRank("SWEET_POTATO_M5"),
    ]).toEqual(["F", "D", "C", "B", "A"]);
  });

  it("does not assign a material rank to unrelated item ids", () => {
    expect(inventoryMaterialRank("skillbook:active:normal")).toBeNull();
  });
});

it("draws each gem with its own level shape and option colour instead of one shared icon", () => {
  expect(inventoryGemGlyph("gem:2:flat_hp")).toEqual({ level: 2, option: "FLAT_HP" });
  expect(inventoryGemGlyph("gem:7:attack_speed")).toEqual({ level: 7, option: "HASTE" });
  expect(inventoryGemGlyph("gem:3:unknown_option")).toBeNull();
  expect(inventoryGemGlyph("POTATO_M1")).toBeNull();
});
