import { cp, mkdir, mkdtemp, readFile, rm } from "node:fs/promises";
import { join, resolve } from "node:path";
import { describe, expect, it } from "vitest";
import {
  buildLegacyEquipmentContent,
  buildLegacyProgressionContent,
  buildProgressionEquipmentContent,
  buildProgressionRebalanceContent,
  buildRebalanceMaterialDrops,
  writeProgressionPackages,
} from "./buildProgressionRebalance.js";
import { validateContent } from "./validate.js";

async function makeFixtureRoot(): Promise<string> {
  const parent = resolve(import.meta.dirname, "../build");
  await mkdir(parent, { recursive: true });
  return mkdtemp(join(parent, "generated-content-"));
}

describe("progression content packages", () => {
  it("builds the approved progression rebalance first-clear and drop tables", () => {
    const rebalance = {
      contentVersion: "progression-rebalance-v1",
      progression: buildProgressionRebalanceContent(),
      materialDrops: buildRebalanceMaterialDrops(),
    };
    expect(rebalance.contentVersion).toBe("progression-rebalance-v1");
    expect(rebalance.progression.stages).toHaveLength(100);
    expect(rebalance.progression.stages.find((row) => row.stageId === "stage.01-01")?.firstClear).toEqual({
      rice: 1_392,
      items: [
        { itemId: "POTATO_M1", quantity: 178 },
        { itemId: "SWEET_POTATO_M1", quantity: 178 },
        { itemId: "CORN_M1", quantity: 178 },
      ],
      directSkillId: "active_heavy",
    });
    expect(rebalance.progression.stages.find((row) => row.stageId === "stage.03-10")?.firstClear?.items).toEqual([
      { itemId: "POTATO_M1", quantity: 206 },
      { itemId: "SWEET_POTATO_M1", quantity: 206 },
      { itemId: "CORN_M1", quantity: 206 },
      { itemId: "POTATO_M2", quantity: 23 },
      { itemId: "SWEET_POTATO_M2", quantity: 23 },
      { itemId: "CORN_M2", quantity: 23 },
      { itemId: "POTATO_M3", quantity: 1 },
      { itemId: "SWEET_POTATO_M3", quantity: 1 },
      { itemId: "CORN_M3", quantity: 1 },
      { itemId: "GEM_BOX", quantity: 30 },
    ]);
    expect(rebalance.materialDrops.chapters["1"]).toEqual({
      mode: "GUARANTEED",
      normalQuantity: 2,
      bossQuantity: 10,
      generationWeights: [1, 0, 0, 0],
    });
    expect(rebalance.materialDrops.chapters["2"]?.mode).toBe("CHANCE");
    expect(rebalance.materialDrops.chapters["4"]?.generationWeights).toEqual([125, 25, 5, 1]);
  });

  it("uses the approved NORMAL per-slot schedule instead of generic brackets", () => {
    expect(buildProgressionEquipmentContent().enhancementMaterials.NORMAL.M1).toEqual([
      ...Array.from({ length: 10 }, () => 40),
      ...Array.from({ length: 10 }, () => 60),
      ...Array.from({ length: 9 }, () => 74),
    ]);
    expect(buildProgressionEquipmentContent().enhancementMaterials.NORMAL.M1!.reduce((sum, value) => sum + value, 0)).toBe(1_666);
    expect(buildProgressionEquipmentContent().enhancementMaterials.RARE.M1!.reduce((sum, value) => sum + value, 0)).toBe(3_700);
    expect(buildProgressionEquipmentContent().enhancementMaterials.EPIC.M1!.reduce((sum, value) => sum + value, 0)).toBe(10_300);
  });

  it("charges a fifth per material generation from EPIC upward", () => {
    const materials = buildProgressionEquipmentContent().enhancementMaterials;
    const total = (steps: number[] | undefined) => steps!.reduce((sum, value) => sum + value, 0);
    for (const grade of ["EPIC", "LEGENDARY"] as const) {
      const m1 = total(materials[grade].M1);
      for (const [generation, steps] of Object.entries(materials[grade])) {
        const power = Number(generation.slice(1)) - 1;
        expect([grade, generation, total(steps)]).toEqual([grade, generation, Math.round(m1 / 5 ** power)]);
      }
    }
  });

  it("never lets a material cost less after a promotion than before it", () => {
    const materials = buildProgressionEquipmentContent().enhancementMaterials;
    const order = ["NORMAL", "RARE", "EPIC", "LEGENDARY"] as const;
    let compared = 0;
    for (let index = 0; index < order.length - 1; index += 1) {
      const lower = materials[order[index]!];
      const upper = materials[order[index + 1]!];
      for (const [generation, steps] of Object.entries(upper)) {
        const closing = lower[generation]?.at(-1);
        if (closing === undefined) continue;
        compared += 1;
        expect([order[index + 1], generation, steps[0]! >= closing]).toEqual([order[index + 1], generation, true]);
      }
    }
    // M1 + (M1,M2) + (M1,M2,M3). Without this the invariant passes vacuously when a generation vanishes.
    expect(compared).toBe(6);
    expect(materials.EPIC.M2!.reduce((sum, value) => sum + value, 0)).toBe(2_060);
    expect(materials.EPIC.M3!.reduce((sum, value) => sum + value, 0)).toBe(412);
  });

  it("carries each grade's closing M1 step into the next grade as fifty times that step", () => {
    const materials = buildProgressionEquipmentContent().enhancementMaterials;
    const total = (steps: number[]) => steps.reduce((sum, value) => sum + value, 0);
    for (const [lower, upper] of [["NORMAL", "RARE"], ["RARE", "EPIC"]] as const) {
      const closing = materials[lower].M1!.at(-1)!;
      expect([upper, total(materials[upper].M1!)]).toEqual([upper, 50 * closing]);
      expect([upper, materials[upper].M1![0]]).toEqual([upper, closing]);
    }
  });

  it("promotes with one closing step of every generation the grade spends plus one new generation", () => {
    const content = buildProgressionEquipmentContent();
    const unlocked = { NORMAL: "M2", RARE: "M3", EPIC: "M4" } as const;
    for (const grade of ["NORMAL", "RARE", "EPIC"] as const) {
      const steps = content.enhancementMaterials[grade] as Record<string, number[]>;
      expect([grade, Object.keys(steps).includes(unlocked[grade])]).toEqual([grade, false]);
      const expected: Record<string, number> = Object.fromEntries(Object.entries(steps).map(([generation, values]) => [generation, values.at(-1)!]));
      expected[unlocked[grade]] = 1;
      expect([grade, content.promotion[grade].materials]).toEqual([grade, expected]);
    }
  });

  it("keeps the current legacy generated values and uses immutable rebalance metadata", () => {
    const legacy = buildLegacyProgressionContent();
    expect(legacy.authority).toBe("applied");
    expect(legacy.contentVersion).toBe("v1");
    expect(legacy.levelRange).toBeNull();
    expect(legacy.stages).toHaveLength(40);
    expect(legacy.stages[0]).toMatchObject({ stageId: "stage.01-01", normalExperience: 3, bossExperience: 15, normalRice: 1, bossRice: 10 });
    expect(buildLegacyEquipmentContent().compatibilityOnly).toBe(true);
    expect(buildProgressionEquipmentContent().compatibilityOnly).toBe(false);
    expect(buildProgressionRebalanceContent().authority).toBe("applied");
  });

  it("covers one hundred stages and the raised level cap", () => {
    const content = buildProgressionRebalanceContent();
    expect(content.stages).toHaveLength(100);
    expect(content.levelRange).toEqual({ min: 1, max: 200 });
    expect(content.stages[99]!.stageId).toBe("stage.10-10");
    expect(content.stages[99]!.bossExperience).toBe(content.stages[99]!.normalExperience * 5);
    expect(content.stages[99]!.bossRice).toBe(1_000);
    expect(content.stages[44]!.firstClear).toEqual(content.stages[34]!.firstClear);
  });

  it("keeps material generations at M1-M4 for every chapter", () => {
    const drops = buildRebalanceMaterialDrops();
    expect(Object.keys(drops.chapterGenerationWeights)).toHaveLength(10);
    expect(drops.chapterGenerationWeights["10"]).toEqual([125, 25, 5, 1]);
    expect(drops.chapters["10"]!.mode).toBe("CHANCE");
    expect(drops.chapters["1"]!.mode).toBe("GUARANTEED");
  });
  it("generates exact legacy and rebalance promotion rows", () => {
    expect(buildLegacyEquipmentContent().promotion).toEqual({
      NORMAL: { requiredStageId: "stage.01-10", materials: { M2: 10 }, rice: 1_000, resultGrade: "RARE" },
      RARE: { requiredStageId: "stage.02-10", materials: { M3: 10 }, rice: 5_000, resultGrade: "EPIC" },
      EPIC: { requiredStageId: "stage.03-10", materials: { M4: 10 }, rice: 25_000, resultGrade: "LEGENDARY" },
      LEGENDARY: { requiredStageId: "stage.99-99", materials: {}, rice: 0, resultGrade: "LEGENDARY" },
    });
    expect(buildProgressionEquipmentContent().promotion).toEqual({
      NORMAL: { requiredStageId: "stage.01-10", materials: { M1: 74, M2: 1 }, rice: 870, resultGrade: "RARE" },
      RARE: { requiredStageId: "stage.03-10", materials: { M1: 206, M2: 23, M3: 1 }, rice: 1_770, resultGrade: "EPIC" },
      EPIC: { requiredStageId: "stage.05-10", materials: { M1: 573, M2: 115, M3: 23, M4: 1 }, rice: 2_670, resultGrade: "LEGENDARY" },
      LEGENDARY: { requiredStageId: "stage.99-99", materials: {}, rice: 0, resultGrade: "LEGENDARY" },
    });
  });

  it("writes both version roots without mutating the committed content package", async () => {
    const sourceRoot = resolve(import.meta.dirname, "../../../packages/game-content");
    const temporaryRoot = await makeFixtureRoot();
    try {
      await cp(sourceRoot, temporaryRoot, { recursive: true, force: true });
      const protectedFiles = [
        "versions/progression-rebalance-v1/stages/stages.json",
        "versions/progression-rebalance-v1/cosmetics/cosmetics.json",
      ] as const;
      const protectedContent = Object.fromEntries(
        await Promise.all(protectedFiles.map(async (file) => [file, await readFile(resolve(temporaryRoot, file), "utf8")] as const)),
      ) as Record<(typeof protectedFiles)[number], string>;
      await writeProgressionPackages(temporaryRoot);
      const required = [
        "versions/v1/progression/progression.json",
        "versions/v1/equipment/equipment.json",
        "versions/progression-rebalance-v1/manifest.json",
        "versions/progression-rebalance-v1/chapters/chapters.json",
        "versions/progression-rebalance-v1/stages/stages.json",
        "versions/progression-rebalance-v1/drops/material-drops.json",
        "versions/progression-rebalance-v1/progression/progression.json",
        "versions/progression-rebalance-v1/equipment/equipment.json",
        "versions/progression-rebalance-v1/skills/skills.json",
        "versions/progression-rebalance-v1/gem-dungeons/gem-dungeons.json",
        "versions/progression-rebalance-v1/cosmetics/cosmetics.json",
      ];
      for (const file of required) await expect(readFile(resolve(temporaryRoot, file), "utf8")).resolves.toMatch(/\S/);
      await expect(validateContent(temporaryRoot)).resolves.toBeUndefined();
      for (const file of protectedFiles) {
        await expect(readFile(resolve(temporaryRoot, file), "utf8")).resolves.toBe(protectedContent[file]);
      }
    } finally {
      await rm(temporaryRoot, { recursive: true, force: true });
    }
  });
});
