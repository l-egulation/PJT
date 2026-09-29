import { createHash } from "node:crypto";
import { readFile } from "node:fs/promises";
import { resolve } from "node:path";
import AjvModule from "ajv/dist/2020.js";
import { BALANCE_VERSION, CONTENT_FILES } from "./buildProgressionRebalance.js";

type StageContent = { authority: "working" | "applied"; contentVersion: string; stages: Array<{ id: string; chapter: number; number: number }> };
type ProgressionContent = { authority: "working" | "applied"; contentVersion: string; levelRange: { min: number; max: number } | null; stages: Array<{ stageId: string; normalExperience: number; bossExperience: number; normalRice: number; bossRice: number; firstClear: { rice: number; items: Array<{ itemId: string; quantity: number }>; directSkillId: string | null } | null }> };
type EquipmentBalanceContent = { authority: "working" | "applied"; contentVersion: string; compatibilityOnly: boolean; enhancementMaterials: Record<string, Record<string, number[]>>; enhancementRice: Record<string, number[]>; promotion: Record<string, { requiredStageId: string; materials: Record<string, number>; rice: number; resultGrade: string }> };
type CosmeticGrade = "NORMAL" | "RARE" | "EPIC" | "LEGENDARY";
type SkillGrade = "NORMAL" | "RARE" | "EPIC" | "LEGENDARY";
type SkillContent = {
  authority: "applied";
  contentVersion: string;
  mvpMaxGrade: SkillGrade;
  levelRange: { min: number; max: number };
  successRatesBasisPoints: Record<string, number>;
  grades: Record<SkillGrade, { enhancementRiceCoefficient: number; promotionRiceCost: number; bookRequirements: Partial<Record<SkillGrade, number>> }>;
  skills: Array<{ skillId: string; name: string; active: boolean; effects: Record<SkillGrade, { baseValue: number; stepValue: number }> }>;
};
type Cosmetic = { cosmeticId: string; displayName: string | null; imageUrl: string | null; grade: CosmeticGrade; slot: string; setId: string };
type Effect = { statId: string; value: number; unit: string };
type CosmeticSet = { setId: string; displayName: string | null; grade: CosmeticGrade; bannerId: string | null; boxItemId: string | null; members: Record<string, string>; effects: Array<{ star: number; effects: Effect[] }> };
type CosmeticsContent = {
  authority: "working" | "applied";
  contentVersion: string;
  singleRiceCost: number;
  gradeProbabilityMillionths: Record<CosmeticGrade, number>;
  starThresholdsByGrade: Record<CosmeticGrade, number[]>;
  cosmetics: Cosmetic[];
  sets: CosmeticSet[];
};
type MaterialDropsContent = {
  authority: "working" | "applied";
  contentVersion: string;
  dropChanceBasisPoints: number;
  quantityPerSuccess: number;
  normalEnemyRolls: number;
  bossRolls: number;
  chapterGenerationWeights: Record<string, number[]>;
  chapters?: Record<string, { mode: "GUARANTEED" | "CHANCE"; normalQuantity: number; bossQuantity: number; generationWeights: number[] }>;
};
type GemDungeonsContent = {
  authority: "applied" | "beta";
  contentVersion: string;
  durationTicks: number;
  completionWindowSeconds: number;
  bosses: Array<{
    bossType: "SURVIVAL" | "BERSERK" | "ARMORED";
    stages: Array<{ stage: number; bossHp: number; bossAttack: number; defense: number; strikeDamage: number }>;
  }>;
};
type RaidReward = { cosmeticTickets: number; gemBoxes: number; rice: number };
type RaidContent = {
  authority: "working" | "applied";
  contentVersion: string;
  unlockStageId: string;
  settlementTimeZone: "Asia/Seoul";
  settlementLocalTime: "17:30";
  sealTarget: number;
  attemptLimitTicks: number;
  escalationIntervalTicks: number;
  escalationBasisPoints: number;
  bossInitialAttack: number;
  bossInitialDefense: number;
  gradeDamageThresholds: Record<string, number>;
  sealContributions: Record<string, number>;
  personalRewards: Record<string, RaidReward>;
  successfulRankRewards: Record<string, RaidReward>;
  failedRankRewards: Record<string, RaidReward>;
};
type VersionManifest = { contentVersion: string; schemaVersion: number; authority: "working" | "applied"; files: string[]; checksums: Record<string, string> };

const RAID_GRADES = ["D", "C", "B", "A", "S", "SS", "SSS"] as const;
const RAID_REWARD_GRADES = ["PARTICIPATION", ...RAID_GRADES] as const;
const RAID_RANKS = ["FIRST", "SECOND_TO_TENTH", "ELEVENTH_TO_HUNDREDTH", "REST"] as const;
export async function validateContent(root = resolve(import.meta.dirname, "../../../packages/game-content")): Promise<void> {
  const schemaTexts = await Promise.all(["stages.schema.json", "cosmetics.schema.json", "material-drops.schema.json", "gem-dungeons.schema.json", "skills.schema.json", "progression.schema.json", "equipment-balance.schema.json", "raids.schema.json"].map((name) => readFile(resolve(root, "schema", name), "utf8")));
  const Ajv2020 = AjvModule.default;
  const ajv = new Ajv2020({ allErrors: true, strict: false });
  const validators = schemaTexts.map((schema) => ajv.compile(JSON.parse(schema)));
  const roots = [
    { version: "v1" as const, files: ["chapters/chapters.json", "stages/stages.json", "cosmetics/cosmetics.json", "drops/material-drops.json", "gem-dungeons/gem-dungeons.json", "gem-dungeons/gem-dungeons-beta.json", "gem-dungeons/gem-dungeons-v2.json", "skills/skills.json", "raids/raids.json"] as const },
    { version: BALANCE_VERSION, files: CONTENT_FILES },
  ];
  for (const { version, files } of roots) {
    const versionRoot = resolve(root, "versions", version);
    const raidText = version === "v1" ? await readFile(resolve(versionRoot, "raids/raids.json"), "utf8") : undefined;
    const manifest = JSON.parse(await readFile(resolve(versionRoot, "manifest.json"), "utf8")) as { contentVersion: string; schemaVersion: number; authority: "working" | "applied"; files: string[]; checksums: Record<string, string> };
    if (manifest.contentVersion !== version) throw new Error(`manifest contentVersion mismatch for ${version}`);
    if (manifest.schemaVersion !== 1) throw new Error(`manifest schemaVersion mismatch for ${version}`);
    if (manifest.files.length !== files.length || manifest.files.some((file, index) => file !== files[index])) throw new Error(`manifest file list mismatch for ${version}`);
    if (Object.keys(manifest.checksums).sort().join(",") !== [...files].sort().join(",")) throw new Error(`manifest checksum map mismatch for ${version}`);
    const entries = await Promise.all(files.map(async (file) => {
      const content = await readFile(resolve(versionRoot, file), "utf8");
      if (!(version === "v1" && file === "raids/raids.json") && manifest.checksums[file] !== stableChecksum(content)) throw new Error(`manifest checksum mismatch: ${version}/${file}`);
      return [file, JSON.parse(content) as Record<string, any>] as const;
    }));
    const values = Object.fromEntries(entries);
    const stage = values["stages/stages.json"] as StageContent;
    const cosmetics = values["cosmetics/cosmetics.json"] as CosmeticsContent;
    const skills = values["skills/skills.json"] as SkillContent;
    const drops = values["drops/material-drops.json"] as MaterialDropsContent;
    const gemDungeons = values["gem-dungeons/gem-dungeons.json"] as GemDungeonsContent;
    const chapters = values["chapters/chapters.json"] as { chapters: Array<{ stageIds: string[] }> };
    const raids = values["raids/raids.json"] as RaidContent | undefined;
    const checks = [validators[0]!(stage), validators[1]!(cosmetics), validators[2]!(drops), validators[3]!(gemDungeons), validators[4]!(skills)];
    if (version === BALANCE_VERSION) checks.push(validators[5]!(values["progression/progression.json"]), validators[6]!(values["equipment/equipment.json"]));
    if (version === "v1" && raids) checks.push(validators[7]!(raids));
    if (checks.some((valid) => !valid)) throw new Error(`content schema invalid for ${version}`);
    validateSkillContent(skills);
    validateCosmeticReferences(cosmetics, version === "v1");
    validateMaterialDropsContent(drops);
    validateGemDungeonsContent(gemDungeons);
    if (version === "v1" && raids) {
      validateRaidContent(raids, stage);
      if (raidText !== undefined && manifest.checksums["raids/raids.json"] !== stableChecksum(raidText)) throw new Error("manifest checksum mismatch: v1/raids/raids.json");
    }
    const stageIds = stage.stages.map((row) => row.id);
    const referencedStages = new Set(chapters.chapters.flatMap((chapter) => chapter.stageIds));
    if (stageIds.some((id) => !referencedStages.has(id)) || referencedStages.size !== stageIds.length) throw new Error("chapter-stage reference mismatch");
    if (version === BALANCE_VERSION) {
      const progression = values["progression/progression.json"] as ProgressionContent;
      const equipment = values["equipment/equipment.json"] as EquipmentBalanceContent;
      if (entries.some(([, content]) => content.contentVersion !== version)) throw new Error(`${version} files must share contentVersion`);
      const expectedStageCount = 100;
      if (progression.stages.length !== expectedStageCount) throw new Error(`${version} progression must contain ${expectedStageCount} stages`);
      const allowedItems = new Set([...materialFamilies.flatMap((family) => Array.from({ length: 5 }, (_, index) => `${family}_M${index + 1}`)), "GEM_BOX"]);
      const allowedSkills = new Set(skills.skills.map((skill) => skill.skillId));
      for (const row of progression.stages) if (row.firstClear) {
        for (const item of row.firstClear.items) if (!allowedItems.has(item.itemId)) throw new Error(`unknown first-clear itemId: ${item.itemId}`);
        if (row.firstClear.directSkillId !== null && !allowedSkills.has(row.firstClear.directSkillId)) throw new Error(`unknown first-clear directSkillId: ${row.firstClear.directSkillId}`);
      }
      for (let index = 0; index < expectedStageCount; index += 1) {
        const row = progression.stages[index]!;
        const expected = `stage.${String(Math.floor(index / 10) + 1).padStart(2, "0")}-${String((index % 10) + 1).padStart(2, "0")}`;
        if (row.stageId !== expected) throw new Error(`unstable progression stage id: ${row.stageId}`);
        if (row.bossExperience !== row.normalExperience * 5 || row.bossRice !== row.normalRice * 10) throw new Error(`invalid progression reward ratio: ${row.stageId}`);
        const globalIndex = index + 1;
        const referenceLevel = 2 * globalIndex - 1;
        const levelMinutes = 2 + (11 / 90) * (2 * referenceLevel - 1);
        const expectedExperience = Math.max(1, Math.round(1_000 * referenceLevel / (50 * levelMinutes)));
        if (row.normalExperience !== expectedExperience) throw new Error(`progression XP schedule mismatch: ${row.stageId}`);
        if (JSON.stringify(row.firstClear) !== JSON.stringify(expectedFirstClear(index))) throw new Error(`first-clear schedule mismatch: ${row.stageId}`);
      }
      const expectedMaterials = approvedMaterialSchedules();
      for (const grade of ["NORMAL", "RARE", "EPIC", "LEGENDARY"] as const) {
        if (JSON.stringify(equipment.enhancementMaterials[grade]) !== JSON.stringify(expectedMaterials[grade])) throw new Error(`approved equipment material schedule mismatch: ${grade}`);
        if (JSON.stringify(equipment.enhancementRice[grade]) !== JSON.stringify(approvedRiceSchedule(grade))) throw new Error(`approved enhancement rice schedule mismatch: ${grade}`);
      }
      const expectedPromotions: EquipmentBalanceContent["promotion"] = {
        NORMAL: { requiredStageId: "stage.01-10", materials: { M1: 74, M2: 1 }, rice: 870, resultGrade: "RARE" },
        RARE: { requiredStageId: "stage.03-10", materials: { M1: 206, M2: 23, M3: 1 }, rice: 1_770, resultGrade: "EPIC" },
        EPIC: { requiredStageId: "stage.05-10", materials: { M1: 573, M2: 115, M3: 23, M4: 1 }, rice: 2_670, resultGrade: "LEGENDARY" },
        LEGENDARY: { requiredStageId: "stage.99-99", materials: {}, rice: 0, resultGrade: "LEGENDARY" },
      };
      for (const grade of ["NORMAL", "RARE", "EPIC", "LEGENDARY"] as const) if (JSON.stringify(equipment.promotion[grade]) !== JSON.stringify(expectedPromotions[grade])) throw new Error(`approved promotion invariant mismatch: ${grade}`);
    }
  }
}
const materialFamilies = ["POTATO", "SWEET_POTATO", "CORN"] as const;
const firstClearTables = {
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
const directSkills = ["active_heavy", "active_dot", "active_haste", "active_basic_amp", "passive_critical", "passive_all_damage"] as const;
function expectedRewardMaterials(generation: number, quantity: number): Array<{ itemId: string; quantity: number }> {
  return materialFamilies.map((family) => ({ itemId: `${family}_M${generation}`, quantity }));
}
function expectedFirstClear(index: number): ProgressionContent["stages"][number]["firstClear"] {
  const chapter = Math.floor(index / 10) + 1;
  const number = (index % 10) + 1;
  if (number === 10) {
    if (chapter === 1) return { rice: 870, items: [...expectedRewardMaterials(1, 74), ...expectedRewardMaterials(2, 1)], directSkillId: null };
    if (chapter === 2) return { rice: 0, items: [{ itemId: "GEM_BOX", quantity: 6 }], directSkillId: null };
    if (chapter === 3) return { rice: 1_770, items: [...expectedRewardMaterials(1, 206), ...expectedRewardMaterials(2, 23), ...expectedRewardMaterials(3, 1), { itemId: "GEM_BOX", quantity: 30 }], directSkillId: null };
    return null;
  }
  if (chapter === 1) return { rice: firstClearTables.chapter1Rice[number - 1]!, items: expectedRewardMaterials(1, firstClearTables.chapter1M1[number - 1]!), directSkillId: directSkills[number - 1] ?? null };
  if (chapter === 2) return { rice: firstClearTables.chapter2Rice[number - 1]!, items: [...expectedRewardMaterials(1, firstClearTables.chapter2M1[number - 1]!), ...expectedRewardMaterials(2, firstClearTables.chapter2M2[number - 1]!)], directSkillId: null };
  if (chapter === 3) return { rice: firstClearTables.chapter3Rice[number - 1]!, items: [...expectedRewardMaterials(1, firstClearTables.chapter3M1[number - 1]!), ...expectedRewardMaterials(2, firstClearTables.chapter3M2[number - 1]!), { itemId: "GEM_BOX", quantity: 10 }], directSkillId: null };
  return { rice: firstClearTables.chapter4Rice[number - 1]!, items: [...expectedRewardMaterials(1, firstClearTables.chapter4M1[number - 1]!), ...expectedRewardMaterials(2, firstClearTables.chapter4M2[number - 1]!), ...expectedRewardMaterials(3, firstClearTables.chapter4M3[number - 1]!)], directSkillId: null };
}
function bracketSteps(total: number): number[] {
  const values: number[] = [];
  for (const [count, share] of [[10, 0.2], [10, 0.3], [9, 0.5]] as const) {
    const target = Math.round(total * share);
    const base = Math.floor(target / count);
    const remainder = target - base * count;
    for (let index = 0; index < count; index += 1) values.push(base + (index >= count - remainder ? 1 : 0));
  }
  return values;
}
function approvedMaterialSchedules(): EquipmentBalanceContent["enhancementMaterials"] {
  const legacyLegendary: Record<string, number[]> = {};
  for (const generation of [1, 2, 3, 4]) legacyLegendary[`M${generation}`] = Array.from({ length: 29 }, (_, index) => Math.ceil((90 + index + 1) / 10) * (125 / 5 ** (generation - 1)));
  return { NORMAL: { M1: [...Array.from({ length: 10 }, () => 40), ...Array.from({ length: 10 }, () => 60), ...Array.from({ length: 9 }, () => 74)] }, RARE: { M1: bracketSteps(3700), M2: bracketSteps(400) }, EPIC: { M1: bracketSteps(10300), M2: bracketSteps(2060), M3: bracketSteps(412) }, LEGENDARY: legacyLegendary };
}
function approvedRiceSchedule(grade: "NORMAL" | "RARE" | "EPIC" | "LEGENDARY"): number[] {
  const gradeIndex = ["NORMAL", "RARE", "EPIC", "LEGENDARY"].indexOf(grade);
  return Array.from({ length: 29 }, (_, index) => 30 * (gradeIndex * 30 + index + 1));
}
function validateRaidContent(content: RaidContent, stages: StageContent): void {
  if (content.authority !== "working") throw new Error("raid content must remain working until vertical slice verification");
  if (content.contentVersion !== "raid-mvp-v1-working") throw new Error("raid content version must remain stable");
  if (!stages.stages.some((stage) => stage.id === content.unlockStageId)) throw new Error(`raid unlock stage reference missing: ${content.unlockStageId}`);
  if (content.unlockStageId !== "stage.01-02") throw new Error("raid unlock stage must remain stage.01-02");
  if (content.sealTarget !== 50_000 || content.attemptLimitTicks !== 3_000 || content.escalationIntervalTicks !== 50 || content.escalationBasisPoints !== 500) throw new Error("raid session values must match the approved table");
  if (content.bossInitialAttack <= 0 || content.bossInitialDefense <= 0) throw new Error("raid boss initial stats must be positive");
  const thresholds = RAID_GRADES.map((grade) => content.gradeDamageThresholds[grade]);
  if (thresholds.some((value) => !Number.isInteger(value) || value <= 0) || !strictlyIncreasing(thresholds)) throw new Error("raid damage thresholds must be strictly increasing positive integers");
  const b = content.gradeDamageThresholds.B;
  const expectedMultipliers: Record<(typeof RAID_GRADES)[number], number> = { D: 0.35, C: 0.65, B: 1, A: 1.5, S: 2.2, SS: 3.2, SSS: 4.5 };
  for (const grade of RAID_GRADES) if (content.gradeDamageThresholds[grade] !== Math.floor(b * expectedMultipliers[grade] + 0.5)) throw new Error(`raid threshold multiplier mismatch for ${grade}`);
  const contributions = [content.sealContributions.PARTICIPATION, ...RAID_GRADES.map((grade) => content.sealContributions[grade])];
  if (JSON.stringify(contributions) !== JSON.stringify([0, 100, 167, 233, 300, 367, 433, 500])) throw new Error("raid seal contributions must match the approved table");
  if (contributions.some((value) => !Number.isInteger(value) || value < 0) || contributions[0] !== 0 || !contributions.slice(1).every((value, index) => value > contributions[index]!)) throw new Error("raid seal contributions must be monotonic");
  const expectedPersonal: Record<(typeof RAID_REWARD_GRADES)[number], RaidReward> = {
    PARTICIPATION: { cosmeticTickets: 5, gemBoxes: 3, rice: 5_000 }, D: { cosmeticTickets: 6, gemBoxes: 3, rice: 5_750 }, C: { cosmeticTickets: 7, gemBoxes: 4, rice: 6_500 }, B: { cosmeticTickets: 7, gemBoxes: 4, rice: 7_250 },
    A: { cosmeticTickets: 8, gemBoxes: 5, rice: 8_000 }, S: { cosmeticTickets: 9, gemBoxes: 5, rice: 8_750 }, SS: { cosmeticTickets: 10, gemBoxes: 6, rice: 9_500 }, SSS: { cosmeticTickets: 10, gemBoxes: 6, rice: 10_000 },
  };
  for (const grade of RAID_REWARD_GRADES) if (JSON.stringify(content.personalRewards[grade]) !== JSON.stringify(expectedPersonal[grade])) throw new Error(`raid personal reward table mismatch for ${grade}`);
  const expectedSuccessful: Record<(typeof RAID_RANKS)[number], RaidReward> = {
    FIRST: { cosmeticTickets: 11, gemBoxes: 7, rice: 11_000 }, SECOND_TO_TENTH: { cosmeticTickets: 10, gemBoxes: 6, rice: 10_000 }, ELEVENTH_TO_HUNDREDTH: { cosmeticTickets: 9, gemBoxes: 5, rice: 9_000 }, REST: { cosmeticTickets: 8, gemBoxes: 5, rice: 8_000 },
  };
  for (const rank of RAID_RANKS) {
    const success = content.successfulRankRewards[rank];
    const failure = content.failedRankRewards[rank];
    if (JSON.stringify(success) !== JSON.stringify(expectedSuccessful[rank])) throw new Error(`raid successful rank reward table mismatch for ${rank}`);
    if (!success || !failure) throw new Error(`missing raid rank reward for ${rank}`);
    const expected = (value: number) => Math.floor(value * 0.7 + 0.5);
    if (Object.values(success).some((value) => !Number.isInteger(value) || value < 0) || failure.cosmeticTickets !== expected(success.cosmeticTickets) || failure.gemBoxes !== expected(success.gemBoxes) || failure.rice !== expected(success.rice)) throw new Error(`failed raid rank reward must be the rounded 70% of success reward for ${rank}`);
  }
}

function stableChecksum(content: string): string {
  return createHash("sha256").update(content.replaceAll("\r\n", "\n")).digest("hex");
}

function validateSkillContent(content: SkillContent): void {
  const grades: SkillGrade[] = ["NORMAL", "RARE", "EPIC", "LEGENDARY"];
  if (content.mvpMaxGrade !== "EPIC") throw new Error("skill MVP max grade must be EPIC");
  if (content.levelRange.min !== 1 || content.levelRange.max !== 10) throw new Error("skill level range must be 1 through 10");
  const expectedSuccess: Record<string, number> = { "2": 7000, "3": 6500, "4": 6000, "5": 5000, "6": 4500, "7": 4000, "8": 3000, "9": 2500, "10": 2000 };
  if (JSON.stringify(content.successRatesBasisPoints) !== JSON.stringify(expectedSuccess)) throw new Error("skill success rates must match the approved table");
  const expectedCoefficients: Record<SkillGrade, number> = { NORMAL: 90, RARE: 180, EPIC: 360, LEGENDARY: 720 };
  const expectedPromotion: Record<SkillGrade, number> = { NORMAL: 0, RARE: 4050, EPIC: 8100, LEGENDARY: 16200 };
  const expectedBooks: Record<SkillGrade, Partial<Record<SkillGrade, number>>> = {
    NORMAL: { NORMAL: 1 }, RARE: { NORMAL: 2, RARE: 1 }, EPIC: { NORMAL: 4, RARE: 2, EPIC: 1 }, LEGENDARY: { NORMAL: 8, RARE: 4, EPIC: 2, LEGENDARY: 1 },
  };
  const expectedIds = ["active_heavy", "active_dot", "active_haste", "active_basic_amp", "passive_critical", "passive_all_damage"];
  const expectedActiveById: Record<string, boolean> = { active_heavy: true, active_dot: true, active_haste: true, active_basic_amp: true, passive_critical: false, passive_all_damage: false };
  if (content.skills.map((skill) => skill.skillId).join(",") !== expectedIds.join(",")) throw new Error("skill ids must match the approved catalog");
  if (content.skills.some((skill) => skill.active !== expectedActiveById[skill.skillId])) throw new Error("skill active classification must match the approved catalog");
  for (const grade of grades) {
    if (content.grades[grade].enhancementRiceCoefficient !== expectedCoefficients[grade]) throw new Error("skill enhancement coefficients must match the approved table");
    if (content.grades[grade].promotionRiceCost !== expectedPromotion[grade]) throw new Error("skill promotion costs must match the approved table");
    if (JSON.stringify(content.grades[grade].bookRequirements) !== JSON.stringify(expectedBooks[grade])) throw new Error("skill book requirements must match the approved table");
  }
  const expectedEffects: Record<string, Record<SkillGrade, [number, number]>> = {
    active_heavy: { NORMAL: [200, 10], RARE: [320, 15], EPIC: [480, 20], LEGENDARY: [700, 25] },
    active_dot: { NORMAL: [250, 15], RARE: [420, 20], EPIC: [640, 25], LEGENDARY: [920, 30] },
    active_haste: { NORMAL: [20, 1], RARE: [32, 1], EPIC: [45, 1], LEGENDARY: [60, 1] },
    active_basic_amp: { NORMAL: [20, 1], RARE: [32, 1], EPIC: [45, 1], LEGENDARY: [60, 1] },
    passive_critical: { NORMAL: [5, 1], RARE: [16, 1], EPIC: [28, 1], LEGENDARY: [40, 1] },
    passive_all_damage: { NORMAL: [5, 1], RARE: [16, 1], EPIC: [28, 1], LEGENDARY: [40, 1] },
  };
  for (const skill of content.skills) for (const grade of grades) {
    const actual = skill.effects[grade];
    const expected = expectedEffects[skill.skillId]![grade];
    if (actual.baseValue !== expected[0] || actual.stepValue !== expected[1]) throw new Error("skill effects must match the approved table");
  }
}

function validateGemDungeonsContent(content: GemDungeonsContent): void {
  const expectedBosses = ["SURVIVAL", "BERSERK", "ARMORED"];
  if (content.bosses.map((boss) => boss.bossType).sort().join(",") !== [...expectedBosses].sort().join(",")) throw new Error("gem dungeons must define each boss exactly once");
  for (const boss of content.bosses) {
    if (boss.stages.map((stage) => stage.stage).join(",") !== "1,2,3,4,5,6,7,8,9,10") throw new Error(`invalid gem dungeon stage sequence: ${boss.bossType}`);
    if (boss.bossType === "SURVIVAL" && boss.stages.some((stage) => stage.strikeDamage <= 0 || stage.bossAttack !== 0)) throw new Error("survival boss must use unavoidable strikes only");
    if (boss.bossType !== "SURVIVAL" && boss.stages.some((stage) => stage.strikeDamage !== 0)) throw new Error(`${boss.bossType} cannot use survival strikes`);
  }
}

function validateMaterialDropsContent(content: MaterialDropsContent): void {
  // Chapters 1-4 unlock one generation each; chapters 5+ keep the full M1-M4 pool.
  const expected = [[1, 0, 0, 0], [5, 1, 0, 0], [25, 5, 1, 0], [125, 25, 5, 1]];
  const chapters = Object.keys(content.chapterGenerationWeights).map(Number).sort((left, right) => left - right);
  if (chapters.some((chapter, index) => chapter !== index + 1)) throw new Error("material chapters must be a 1..n run");
  for (const chapter of chapters) {
    const weights = content.chapterGenerationWeights[String(chapter)];
    if (!weights) throw new Error(`missing material weights in chapter ${chapter}`);
    if (chapter <= 4 && weights.some((weight, index) => index >= chapter && weight !== 0)) throw new Error(`locked material generation in chapter ${chapter}`);
    const row = expected[Math.min(chapter, 4) - 1]!;
    if (weights.some((weight, index) => weight !== row[index])) throw new Error(`unexpected material weights in chapter ${chapter}`);
  }
  if (content.normalEnemyRolls * 20 + content.bossRolls !== 25) throw new Error("material cycle must have 25 independent rolls");
}

function validateCosmeticReferences(content: CosmeticsContent, legacy = false): void {
  const grades: CosmeticGrade[] = ["NORMAL", "RARE", "EPIC", "LEGENDARY"];
  const probabilities = Object.values(content.gradeProbabilityMillionths);
  if (probabilities.reduce((sum, value) => sum + value, 0) !== 1_000_000) throw new Error("cosmetic probabilities must total 1,000,000");
  if (content.singleRiceCost !== 5_000) throw new Error("cosmetic single draw cost must be 5,000");
  const expectedProbabilities: Record<CosmeticGrade, number> = { NORMAL: 500_000, RARE: 350_000, EPIC: 140_000, LEGENDARY: 10_000 };
  if (grades.some((grade) => content.gradeProbabilityMillionths[grade] !== expectedProbabilities[grade])) throw new Error("cosmetic probabilities must match the approved table");
  const expectedThresholds: Record<CosmeticGrade, number[]> = legacy ? {
    NORMAL: [1, 3, 6, 10, 18], RARE: [1, 3, 6, 12, 24], EPIC: [1, 2, 4, 8, 16], LEGENDARY: [1, 2, 3, 5, 10],
  } : {
    NORMAL: [1, 480, 720, 1200, 2400], RARE: [1, 168, 252, 420, 840], EPIC: [1, 134, 202, 336, 672], LEGENDARY: [1, 2, 3, 5, 10],
  };
  for (const grade of grades) {
    const thresholds = content.starThresholdsByGrade[grade];
    if (!thresholds || thresholds.join(",") !== expectedThresholds[grade].join(",")) throw new Error(`invalid ${grade} star thresholds`);
  }
  const expectedIds = Array.from({ length: 66 }, (_, index) => `cosmetic-${String(index + 1).padStart(3, "0")}`);
  const cosmeticIds = content.cosmetics.map((cosmetic) => cosmetic.cosmeticId);
  if (cosmeticIds.join(",") !== expectedIds.join(",")) throw new Error("cosmetic ids must be cosmetic-001 through cosmetic-066 in order");
  const expectedSetIds = Array.from({ length: 11 }, (_, index) => `cosmetic-set-${String(index + 1).padStart(2, "0")}`);
  const setIds = content.sets.map((set) => set.setId);
  if (setIds.join(",") !== expectedSetIds.join(",")) throw new Error("cosmetic set ids must be cosmetic-set-01 through cosmetic-set-11 in order");
  const gradeCounts = Object.fromEntries(grades.map((grade) => [grade, content.sets.filter((set) => set.grade === grade).length]));
  if (JSON.stringify(gradeCounts) !== JSON.stringify({ NORMAL: 4, RARE: 3, EPIC: 3, LEGENDARY: 1 })) throw new Error("cosmetic set grade counts must be 4:3:3:1");
  const byId = new Map(content.cosmetics.map((cosmetic) => [cosmetic.cosmeticId, cosmetic]));
  const requiredSlots = ["HEAD", "TOP", "BOTTOM", "GLOVES", "SHOES", "CAPE"];
  const memberIds: string[] = [];
  for (const set of content.sets) {
    if (Object.keys(set.members).sort().join(",") !== [...requiredSlots].sort().join(",")) throw new Error(`invalid set slots: ${set.setId}`);
    const ids = Object.values(set.members);
    if (new Set(ids).size !== 6) throw new Error(`duplicate set member: ${set.setId}`);
    memberIds.push(...ids);
    for (const [slot, cosmeticId] of Object.entries(set.members)) {
      const cosmetic = byId.get(cosmeticId);
      if (!cosmetic || cosmetic.grade !== set.grade || cosmetic.slot !== slot || cosmetic.setId !== set.setId) throw new Error(`invalid set member mapping: ${set.setId}/${slot}`);
    }
    if (set.effects.map((effect) => effect.star).join(",") !== "1,2,3,4,5") throw new Error(`invalid set effect stars: ${set.setId}`);
    validateMonotonicEffects(set.effects, `set ${set.setId}`);
    for (const row of set.effects) for (const effect of row.effects) {
      const expectedUnit = effect.statId === "buffDurationSeconds" ? "SECONDS" : effect.statId === "criticalChancePoint" ? "PERCENTAGE_POINTS" : "BASIS_POINTS";
      if (effect.unit !== expectedUnit) throw new Error(`invalid cosmetic effect unit: ${effect.statId}/${effect.unit}`);
    }
  }
  if ([...memberIds].sort().join(",") !== [...cosmeticIds].sort().join(",")) throw new Error("every cosmetic must belong to exactly one set");
  const mappedSets = content.sets.filter((set) => set.bannerId !== null || set.boxItemId !== null);
  if (mappedSets.length !== 1 || mappedSets[0]?.grade !== "LEGENDARY" || !mappedSets[0].bannerId || !mappedSets[0].boxItemId) throw new Error("exactly one legendary set must map to a banner and selector box");
}

function strictlyIncreasing(values: number[]): boolean {
  return values.every((value, index) => index === 0 || value > values[index - 1]!);
}

function validateMonotonicEffects(rows: Array<{ effects: Effect[] }>, label: string): void {
  const previous = new Map<string, number>();
  for (const row of rows) {
    for (const effect of row.effects) {
      const key = `${effect.statId}:${effect.unit}`;
      if (effect.value < (previous.get(key) ?? 0)) throw new Error(`${label} must be cumulative`);
      previous.set(key, effect.value);
    }
  }
}

if (process.argv[1]?.endsWith("validate.ts")) await validateContent();
