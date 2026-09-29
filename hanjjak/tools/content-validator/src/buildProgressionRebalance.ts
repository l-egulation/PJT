import { createHash } from "node:crypto";
import { mkdir, readFile, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";

export const BALANCE_VERSION = "progression-rebalance-v1" as const;
export const CONTENT_FILES = [
  "chapters/chapters.json",
  "stages/stages.json",
  "drops/material-drops.json",
  "progression/progression.json",
  "equipment/equipment.json",
  "skills/skills.json",
  "gem-dungeons/gem-dungeons.json",
  "cosmetics/cosmetics.json",
] as const;
type Authority = "working" | "applied";
type Grade = "NORMAL" | "RARE" | "EPIC" | "LEGENDARY";
export type RewardItem = { itemId: string; quantity: number };
export type FirstClear = { rice: number; items: RewardItem[]; directSkillId: string | null } | null;
export type ProgressionStage = {
  stageId: string;
  normalExperience: number;
  bossExperience: number;
  normalRice: number;
  bossRice: number;
  firstClear: FirstClear;
};
export type ProgressionContent = {
  authority: Authority;
  contentVersion: string;
  levelRange: { min: 1; max: 200 } | null;
  stages: ProgressionStage[];
};
export type EquipmentBalanceContent = {
  authority: Authority;
  contentVersion: string;
  compatibilityOnly: boolean;
  enhancementMaterials: Record<Grade, Record<string, number[]>>;
  enhancementRice: Record<Grade, number[]>;
  promotion: Record<Grade, { requiredStageId: string; materials: Record<string, number>; rice: number; resultGrade: Grade }>;
};
export type MaterialDropChapter = {
  mode: "GUARANTEED" | "CHANCE";
  normalQuantity: number;
  bossQuantity: number;
  generationWeights: number[];
};
export type MaterialDropsContent = {
  authority: Authority;
  contentVersion: string;
  dropChanceBasisPoints: number;
  quantityPerSuccess: number;
  normalEnemyRolls: number;
  bossRolls: number;
  chapterGenerationWeights: Record<string, number[]>;
  chapters: Record<string, MaterialDropChapter>;
};

const firstClear = {
  chapter1M1: [178, 356, 533, 711, 889, 1_067, 1_244, 1_422, 1_600],
  chapter1Rice: [1_392, 2_784, 4_176, 5_568, 6_960, 8_352, 9_744, 11_136, 12_528],
  chapter2M1: [148, 296, 444, 592, 740, 888, 1_036, 1_184, 1_332],
  chapter2M2: [16, 32, 48, 64, 80, 96, 112, 128, 144],
  chapter2Rice: [1_566, 3_132, 4_698, 6_264, 7_830, 9_396, 10_962, 12_528, 14_094],
  chapter3M1: [99, 197, 296, 395, 493, 592, 691, 789, 888],
  chapter3M2: [11, 21, 32, 43, 53, 64, 75, 85, 96],
  chapter3Rice: [1_044, 2_088, 3_132, 4_176, 5_220, 6_264, 7_308, 8_352, 9_396],
  chapter4M1: [412, 824, 1_236, 1_648, 2_060, 2_472, 2_884, 3_296, 3_708],
  chapter4M2: [5, 10, 14, 19, 24, 29, 34, 38, 43],
  chapter4M3: [5, 10, 14, 19, 24, 29, 34, 38, 43],
  chapter4Rice: [2_610, 5_220, 7_830, 10_440, 13_050, 15_660, 18_270, 20_880, 23_490],
} as const;
const materialFamilies = ["POTATO", "SWEET_POTATO", "CORN"] as const;
const grades: Grade[] = ["NORMAL", "RARE", "EPIC", "LEGENDARY"];
const skills = ["active_heavy", "active_dot", "active_haste", "active_basic_amp", "passive_critical", "passive_all_damage"] as const;

function stageId(globalIndex: number): string {
  return `stage.${String(Math.floor((globalIndex - 1) / 10) + 1).padStart(2, "0")}-${String(((globalIndex - 1) % 10) + 1).padStart(2, "0")}`;
}
function rewardMaterials(generation: number, quantity: number): RewardItem[] {
  return materialFamilies.map((family) => ({ itemId: `${family}_M${generation}`, quantity }));
}
function firstClearFor(globalIndex: number): FirstClear {
  const chapter = Math.floor((globalIndex - 1) / 10) + 1;
  const number = ((globalIndex - 1) % 10) + 1;
  if (number === 10) {
    if (chapter === 1) return { rice: 870, items: [...rewardMaterials(1, 74), ...rewardMaterials(2, 1)], directSkillId: null };
    if (chapter === 2) return { rice: 0, items: [{ itemId: "GEM_BOX", quantity: 6 }], directSkillId: null };
    if (chapter === 3) return { rice: 1_770, items: [...rewardMaterials(1, 206), ...rewardMaterials(2, 23), ...rewardMaterials(3, 1), { itemId: "GEM_BOX", quantity: 30 }], directSkillId: null };
    return null;
  }
  if (chapter === 1) return { rice: firstClear.chapter1Rice[number - 1]!, items: rewardMaterials(1, firstClear.chapter1M1[number - 1]!), directSkillId: skills[number - 1] ?? null };
  if (chapter === 2) return { rice: firstClear.chapter2Rice[number - 1]!, items: [...rewardMaterials(1, firstClear.chapter2M1[number - 1]!), ...rewardMaterials(2, firstClear.chapter2M2[number - 1]!)], directSkillId: null };
  if (chapter === 3) return { rice: firstClear.chapter3Rice[number - 1]!, items: [...rewardMaterials(1, firstClear.chapter3M1[number - 1]!), ...rewardMaterials(2, firstClear.chapter3M2[number - 1]!), { itemId: "GEM_BOX", quantity: 10 }], directSkillId: null };
  return { rice: firstClear.chapter4Rice[number - 1]!, items: [...rewardMaterials(1, firstClear.chapter4M1[number - 1]!), ...rewardMaterials(2, firstClear.chapter4M2[number - 1]!), ...rewardMaterials(3, firstClear.chapter4M3[number - 1]!)], directSkillId: null };
}
function buildProgression(authority: Authority, contentVersion: string, rebalance: boolean): ProgressionContent {
  return {
    authority,
    contentVersion,
    levelRange: rebalance ? { min: 1, max: 200 } : null,
    stages: Array.from({ length: rebalance ? 100 : 40 }, (_, offset) => {
      const globalIndex = offset + 1;
      const referenceLevel = 2 * globalIndex - 1;
      const levelMinutes = rebalance ? 2 + (11 / 90) * (2 * referenceLevel - 1) : 6 + (11 / 30) * (2 * referenceLevel - 1);
      const normalExperience = Math.max(1, Math.round(1_000 * referenceLevel / (50 * levelMinutes)));
      return { stageId: stageId(globalIndex), normalExperience, bossExperience: normalExperience * 5, normalRice: globalIndex, bossRice: globalIndex * 10, firstClear: rebalance ? firstClearFor(globalIndex) : null };
    }),
  };
}
export function buildLegacyProgressionContent(): ProgressionContent { return buildProgression("applied", "v1", false); }
export function buildProgressionRebalanceContent(): ProgressionContent { return buildProgression("applied", BALANCE_VERSION, true); }
const BRACKET_COUNTS = [10, 10, 9];
const BRACKET_SHARES = [0.2, 0.3, 0.5];
function splitBracketTotal(total: number): number[] {
  const counts = BRACKET_COUNTS;
  const shares = BRACKET_SHARES;
  const values: number[] = [];
  for (let bracket = 0; bracket < counts.length; bracket += 1) {
    const count = counts[bracket]!;
    const target = Math.round(total * shares[bracket]!);
    const base = Math.floor(target / count);
    const remainder = target - base * count;
    for (let index = 0; index < count; index += 1) values.push(base + (index >= count - remainder ? 1 : 0));
  }
  const difference = total - values.reduce((sum, value) => sum + value, 0);
  for (let index = values.length - 1; index >= 0 && index >= values.length - difference; index -= 1) values[index]! += 1;
  return values;
}
function normalMaterialSteps(): number[] { return [...Array.from({ length: 10 }, () => 40), ...Array.from({ length: 10 }, () => 60), ...Array.from({ length: 9 }, () => 74)]; }
function legacyMaterialSteps(grade: Grade): Record<string, number[]> {
  const gradeIndex = grades.indexOf(grade);
  const result: Record<string, number[]> = {};
  for (let generation = 1; generation <= gradeIndex + 1; generation += 1) result[`M${generation}`] = Array.from({ length: 29 }, (_, offset) => Math.ceil((gradeIndex * 30 + offset + 1) / 10) * GENERATION_WEIGHTS[generation - 1]!);
  return result;
}
function riceSteps(grade: Grade): number[] { const gradeIndex = grades.indexOf(grade); return Array.from({ length: 29 }, (_, offset) => 30 * (gradeIndex * 30 + offset + 1)); }
/** splitBracketTotal hands this share of a total to each step of its first bracket, so a grade
 *  whose total is the previous grade's closing step divided by it opens exactly where that grade
 *  left off, and no generation ever costs less after a promotion than before it. */
const OPENING_STEP_SHARE = BRACKET_SHARES[0]! / BRACKET_COUNTS[0]!;
function carryOverTotal(previous: number[]): number { return Math.round(previous.at(-1)! / OPENING_STEP_SHARE); }
/** Materials drop at 125:25:5:1 by generation, so each generation costs a fifth of the one before it. */
const GENERATION_RATIO = 5;
const GENERATION_WEIGHTS = [0, 1, 2, 3].map((generation) => 125 / GENERATION_RATIO ** generation);
function rebalanceMaterialSteps(): Record<Grade, Record<string, number[]>> {
  const normalM1 = normalMaterialSteps();
  const rareM1 = splitBracketTotal(carryOverTotal(normalM1));
  const epicM1 = splitBracketTotal(carryOverTotal(rareM1));
  const epicTotal = epicM1.reduce((sum, value) => sum + value, 0);
  return {
    NORMAL: { M1: normalM1 },
    RARE: { M1: rareM1, M2: splitBracketTotal(400) },
    EPIC: { M1: epicM1, M2: splitBracketTotal(Math.round(epicTotal / GENERATION_RATIO)), M3: splitBracketTotal(Math.round(epicTotal / GENERATION_RATIO ** 2)) },
    LEGENDARY: legacyMaterialSteps("LEGENDARY"),
  };
}
/** Promotion costs one closing enhancement step of everything the grade already spends, plus the
 *  SSOT's starter of the generation the next grade unlocks: 10% of that grade's opening step,
 *  always rounded up and never zero. */
const UNLOCKED_STARTER_SHARE = 0.1;
function promotionCost(current: Record<string, number[]>, next: Record<string, number[]>, unlocked: string): Record<string, number> {
  const closing = Object.fromEntries(Object.entries(current).map(([generation, values]) => [generation, values.at(-1)!]));
  const starter = Math.max(1, Math.ceil(UNLOCKED_STARTER_SHARE * next[unlocked]![0]!));
  return { ...closing, [unlocked]: starter };
}
/** `steps` is null on the legacy path, whose promotion rows are frozen literals. Passing the
 *  rebalance schedule in keeps the promotion rows and the materials table one and the same object. */
function promotions(steps: Record<Grade, Record<string, number[]>> | null): EquipmentBalanceContent["promotion"] {
  const closingRice = (grade: Grade) => riceSteps(grade).at(-1)!;
  return {
    NORMAL: { requiredStageId: "stage.01-10", materials: steps ? promotionCost(steps.NORMAL, steps.RARE, "M2") : { M2: 10 }, rice: steps ? closingRice("NORMAL") : 1_000, resultGrade: "RARE" },
    RARE: { requiredStageId: steps ? "stage.03-10" : "stage.02-10", materials: steps ? promotionCost(steps.RARE, steps.EPIC, "M3") : { M3: 10 }, rice: steps ? closingRice("RARE") : 5_000, resultGrade: "EPIC" },
    EPIC: { requiredStageId: steps ? "stage.05-10" : "stage.03-10", materials: steps ? promotionCost(steps.EPIC, steps.LEGENDARY, "M4") : { M4: 10 }, rice: steps ? closingRice("EPIC") : 25_000, resultGrade: "LEGENDARY" },
    LEGENDARY: { requiredStageId: "stage.99-99", materials: {}, rice: 0, resultGrade: "LEGENDARY" },
  };
}
export function buildLegacyEquipmentContent(): EquipmentBalanceContent {
  return { authority: "applied", contentVersion: "v1", compatibilityOnly: true, enhancementMaterials: Object.fromEntries(grades.map((grade) => [grade, legacyMaterialSteps(grade)])) as EquipmentBalanceContent["enhancementMaterials"], enhancementRice: Object.fromEntries(grades.map((grade) => [grade, riceSteps(grade)])) as EquipmentBalanceContent["enhancementRice"], promotion: promotions(null) };
}
export function buildProgressionEquipmentContent(): EquipmentBalanceContent {
  const steps = rebalanceMaterialSteps();
  return { authority: "applied", contentVersion: BALANCE_VERSION, compatibilityOnly: false, enhancementMaterials: steps, enhancementRice: Object.fromEntries(grades.map((grade) => [grade, riceSteps(grade)])) as EquipmentBalanceContent["enhancementRice"], promotion: promotions(steps) };
}
export function buildRebalanceMaterialDrops(): MaterialDropsContent {
  // Chapters 5-10 keep the chapter-4 cumulative M1-M4 pool; no new material generation is introduced.
  const chapterGenerationWeights: Record<string, number[]> = { "1": [1, 0, 0, 0], "2": [5, 1, 0, 0], "3": [25, 5, 1, 0], "4": [...GENERATION_WEIGHTS] };
  for (let chapter = 5; chapter <= 10; chapter += 1) chapterGenerationWeights[String(chapter)] = [...GENERATION_WEIGHTS];
  const chapters: Record<string, MaterialDropChapter> = {
    "1": { mode: "GUARANTEED", normalQuantity: 2, bossQuantity: 10, generationWeights: chapterGenerationWeights["1"]! },
  };
  for (let chapter = 2; chapter <= 10; chapter += 1) {
    chapters[String(chapter)] = { mode: "CHANCE", normalQuantity: 10, bossQuantity: 10, generationWeights: chapterGenerationWeights[String(chapter)]! };
  }
  return { authority: "applied", contentVersion: BALANCE_VERSION, dropChanceBasisPoints: 1_500, quantityPerSuccess: 10, normalEnemyRolls: 1, bossRolls: 5, chapterGenerationWeights, chapters };
}
async function writeJson(path: string, value: unknown): Promise<void> { await mkdir(dirname(path), { recursive: true }); await writeFile(path, `${JSON.stringify(value, null, 2)}\n`); }
function stableChecksum(content: string): string { return createHash("sha256").update(content.replaceAll("\r\n", "\n")).digest("hex"); }
export async function regenerateManifests(root: string): Promise<void> {
  const legacyFiles = ["chapters/chapters.json", "stages/stages.json", "cosmetics/cosmetics.json", "drops/material-drops.json", "gem-dungeons/gem-dungeons.json", "gem-dungeons/gem-dungeons-beta.json", "gem-dungeons/gem-dungeons-v2.json", "skills/skills.json", "raids/raids.json"] as const;
  for (const [version, files] of [["v1", legacyFiles], [BALANCE_VERSION, CONTENT_FILES]] as const) {
    const versionRoot = resolve(root, "versions", version);
    const checksums: Record<string, string> = {};
    for (const file of files) checksums[file] = stableChecksum(await readFile(resolve(versionRoot, file), "utf8"));
    const authority = version === "v1" ? "working" : "applied";
    await writeJson(resolve(versionRoot, "manifest.json"), { contentVersion: version, schemaVersion: 1, authority, files: [...files], checksums });
  }
}
export async function writeProgressionPackages(root: string): Promise<void> {
  await writeJson(resolve(root, "versions/v1/progression/progression.json"), buildLegacyProgressionContent());
  await writeJson(resolve(root, "versions/v1/equipment/equipment.json"), buildLegacyEquipmentContent());
  const versionRoot = resolve(root, "versions", BALANCE_VERSION);
  // This generator owns progression, equipment, and drop balance only. Preserve the
  // independently approved stage, skill, dungeon, and cosmetic content in this version.
  await writeJson(resolve(versionRoot, "progression/progression.json"), buildProgressionRebalanceContent());
  await writeJson(resolve(versionRoot, "equipment/equipment.json"), buildProgressionEquipmentContent());
  await writeJson(resolve(versionRoot, "drops/material-drops.json"), buildRebalanceMaterialDrops());
  await regenerateManifests(root);
}
