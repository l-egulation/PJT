import { createHash } from "node:crypto";
import { cp, mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { join, resolve } from "node:path";
import { describe, expect, it } from "vitest";
import Ajv2020 from "ajv/dist/2020.js";
import { validateContent } from "./validate.js";
import { regenerateManifests } from "./buildProgressionRebalance.js";
async function makeFixtureRoot(prefix: string): Promise<string> {
  const parent = resolve(import.meta.dirname, "../build");
  await mkdir(parent, { recursive: true });
  return mkdtemp(join(parent, prefix));
}
async function refreshChecksum(manifestPath: string, file: string, content: string): Promise<void> {
  const manifest = JSON.parse(await readFile(manifestPath, "utf8"));
  manifest.checksums[file] = createHash("sha256").update(content.replaceAll("\r\n", "\n")).digest("hex");
  await writeFile(manifestPath, `${JSON.stringify(manifest, null, 2)}\n`);
}

async function expectRebalanceDriftRejected(
  prefix: string,
  file: "progression/progression.json" | "equipment/equipment.json",
  mutate: (content: Record<string, any>) => void,
  message: string,
): Promise<void> {
  const sourceRoot = resolve(import.meta.dirname, "../../../packages/game-content");
  const temporaryRoot = await makeFixtureRoot(prefix);
  try {
    await cp(sourceRoot, temporaryRoot, { recursive: true, force: true });
    const contentPath = resolve(temporaryRoot, "versions/progression-rebalance-v1", file);
    const content = JSON.parse(await readFile(contentPath, "utf8")) as Record<string, any>;
    mutate(content);
    await writeFile(contentPath, JSON.stringify(content));
    await regenerateManifests(temporaryRoot);
    await expect(validateContent(temporaryRoot)).rejects.toThrow(message);
  } finally {
    await rm(temporaryRoot, { recursive: true, force: true });
  }
}

describe("approved progression invariants", () => {
  it("rejects chapter 2 first-clear drift after regenerating the fixture manifest", async () => {
    await expectRebalanceDriftRejected("chapter-2-first-clear-drift-", "progression/progression.json", (content) => {
      content.stages[10].firstClear.rice += 1;
    }, "first-clear schedule mismatch");
  });

  it("generates identical manifest checksums for equivalent LF and CRLF fixtures", async () => {
    const sourceRoot = resolve(import.meta.dirname, "../../../packages/game-content");
    const lfRoot = await makeFixtureRoot("checksum-lf-");
    const crlfRoot = await makeFixtureRoot("checksum-crlf-");
    const jsonFiles = [
      "schema/stages.schema.json", "schema/cosmetics.schema.json", "schema/material-drops.schema.json", "schema/gem-dungeons.schema.json", "schema/skills.schema.json", "schema/raids.schema.json",
      "versions/v1/chapters/chapters.json", "versions/v1/stages/stages.json", "versions/v1/cosmetics/cosmetics.json", "versions/v1/drops/material-drops.json", "versions/v1/gem-dungeons/gem-dungeons.json", "versions/v1/gem-dungeons/gem-dungeons-beta.json", "versions/v1/gem-dungeons/gem-dungeons-v2.json", "versions/v1/skills/skills.json", "versions/v1/raids/raids.json",
    ];
    try {
      await cp(sourceRoot, lfRoot, { recursive: true, force: true });
      await cp(sourceRoot, crlfRoot, { recursive: true, force: true });
      for (const file of jsonFiles) {
        const source = await readFile(resolve(sourceRoot, file), "utf8");
        const lf = source.split(String.fromCharCode(13, 10)).join(String.fromCharCode(10));
        await writeFile(resolve(lfRoot, file), lf);
        await writeFile(resolve(crlfRoot, file), lf.split(String.fromCharCode(10)).join(String.fromCharCode(13, 10)));
      }
      await expect(validateContent(lfRoot)).resolves.toBeUndefined();
      await expect(validateContent(crlfRoot)).resolves.toBeUndefined();
      const lfManifest = JSON.parse(await readFile(resolve(lfRoot, "versions/v1/manifest.json"), "utf8"));
      const crlfManifest = JSON.parse(await readFile(resolve(crlfRoot, "versions/v1/manifest.json"), "utf8"));
      expect(crlfManifest.checksums).toEqual(lfManifest.checksums);
      expect(lfManifest.files).toContain("gem-dungeons/gem-dungeons-v2.json");
      expect(lfManifest.checksums["gem-dungeons/gem-dungeons-v2.json"]).toMatch(/^[0-9a-f]{64}$/);
      expect(lfManifest.files).toContain("raids/raids.json");
      expect(lfManifest.checksums["raids/raids.json"]).toMatch(/^[0-9a-f]{64}$/);
      expect(lfManifest.files).toContain("cosmetics/cosmetics.json");
      expect(lfManifest.checksums["cosmetics/cosmetics.json"]).toMatch(/^[0-9a-f]{64}$/);
    } finally {
      await Promise.all([rm(lfRoot, { recursive: true, force: true }), rm(crlfRoot, { recursive: true, force: true })]);
    }
  });

  it("rejects chapter 4 first-clear drift after regenerating the fixture manifest", async () => {
    await expectRebalanceDriftRejected("chapter-4-first-clear-drift-", "progression/progression.json", (content) => {
      content.stages[30].firstClear.items[0].quantity += 1;
    }, "first-clear schedule mismatch");
  });

  it.each([
    ["RARE", "M1", 28],
    ["EPIC", "M2", 28],
  ])("rejects %s %s per-generation total/row drift", async (grade, generation, index) => {
    await expectRebalanceDriftRejected("equipment-material-drift-", "equipment/equipment.json", (content) => {
      content.enhancementMaterials[grade][generation][index] += 1;
    }, "approved equipment material schedule mismatch");
  });

  it("rejects enhancement rice row drift after regenerating the fixture manifest", async () => {
    await expectRebalanceDriftRejected("enhancement-rice-drift-", "equipment/equipment.json", (content) => {
      content.enhancementRice.EPIC[10] += 1;
    }, "approved enhancement rice schedule mismatch");
  });

  it("rejects promotion material and rice cost drift after regenerating the fixture manifest", async () => {
    await expectRebalanceDriftRejected("promotion-cost-drift-", "equipment/equipment.json", (content) => {
      content.promotion.RARE.materials.M2 += 1;
      content.promotion.RARE.rice += 1;
    }, "approved promotion invariant mismatch");
  });

  it("rejects promotion gate drift after regenerating the fixture manifest", async () => {
    await expectRebalanceDriftRejected("promotion-gate-drift-", "equipment/equipment.json", (content) => {
      content.promotion.EPIC.requiredStageId = "stage.04-10";
    }, "approved promotion invariant mismatch");
  });
});

describe("content package", () => {
  it("has valid schemas, references and checksums", async () => { await expect(validateContent()).resolves.toBeUndefined(); });
  it("rejects committed manifest drift without rewriting the manifest", async () => {
    const sourceRoot = resolve(import.meta.dirname, "../../../packages/game-content");
    const temporaryRoot = await makeFixtureRoot("manifest-drift-");
    try {
      await cp(sourceRoot, temporaryRoot, { recursive: true, force: true });
      const manifestPath = resolve(temporaryRoot, "versions/progression-rebalance-v1/manifest.json");
      const contentPath = resolve(temporaryRoot, "versions/progression-rebalance-v1/progression/progression.json");
      const before = await readFile(manifestPath, "utf8");
      const content = JSON.parse(await readFile(contentPath, "utf8"));
      content.stages[0].normalRice += 1;
      await writeFile(contentPath, JSON.stringify(content));
      await expect(validateContent(temporaryRoot)).rejects.toThrow("manifest checksum mismatch");
      await expect(readFile(manifestPath, "utf8")).resolves.toBe(before);
    } finally { await rm(temporaryRoot, { recursive: true, force: true }); }
  });
  it("rejects a first-clear item outside the full material and GEM_BOX catalog", async () => {
    const sourceRoot = resolve(import.meta.dirname, "../../../packages/game-content");
    const temporaryRoot = await makeFixtureRoot("unknown-item-");
    try {
      await cp(sourceRoot, temporaryRoot, { recursive: true, force: true });
      const contentPath = resolve(temporaryRoot, "versions/progression-rebalance-v1/progression/progression.json");
      const manifestPath = resolve(temporaryRoot, "versions/progression-rebalance-v1/manifest.json");
      const content = JSON.parse(await readFile(contentPath, "utf8"));
      content.stages[10].firstClear.items[0].itemId = "UNKNOWN_MATERIAL";
      const serialized = JSON.stringify(content);
      await writeFile(contentPath, serialized);
      await refreshChecksum(manifestPath, "progression/progression.json", serialized);
      await expect(validateContent(temporaryRoot)).rejects.toThrow("unknown first-clear itemId");
    } finally { await rm(temporaryRoot, { recursive: true, force: true }); }
  });
  it("rejects a first-clear direct skill outside the copied skill catalog", async () => {
    const sourceRoot = resolve(import.meta.dirname, "../../../packages/game-content");
    const temporaryRoot = await makeFixtureRoot("unknown-skill-");
    try {
      await cp(sourceRoot, temporaryRoot, { recursive: true, force: true });
      const contentPath = resolve(temporaryRoot, "versions/progression-rebalance-v1/progression/progression.json");
      const manifestPath = resolve(temporaryRoot, "versions/progression-rebalance-v1/manifest.json");
      const content = JSON.parse(await readFile(contentPath, "utf8"));
      content.stages[0].firstClear.directSkillId = "unknown_skill";
      const serialized = JSON.stringify(content);
      await writeFile(contentPath, serialized);
      await refreshChecksum(manifestPath, "progression/progression.json", serialized);
      await expect(validateContent(temporaryRoot)).rejects.toThrow("unknown first-clear directSkillId");
    } finally { await rm(temporaryRoot, { recursive: true, force: true }); }
  });
  it("rejects drift from approved skill tables", async () => {
    const sourceRoot = resolve(import.meta.dirname, "../../../packages/game-content");
    const temporaryRoot = await makeFixtureRoot("skill-drift-");
    try {
      await cp(sourceRoot, temporaryRoot, { recursive: true, force: true });
      const path = resolve(temporaryRoot, "versions/v1/skills/skills.json");
      const content = JSON.parse(await readFile(path, "utf8"));
      content.grades.NORMAL.enhancementRiceCoefficient = 91;
      await writeFile(path, JSON.stringify(content));
      await expect(validateContent(temporaryRoot)).rejects.toThrow("manifest checksum mismatch");
    } finally { await rm(temporaryRoot, { recursive: true, force: true }); }
  });
  it("validates both immutable roots with applied authority", async () => {
    await expect(validateContent()).resolves.toBeUndefined();
    const root = resolve(import.meta.dirname, "../../../packages/game-content");
    const manifest = JSON.parse(await readFile(resolve(root, "versions/progression-rebalance-v1/manifest.json"), "utf8"));
    expect(manifest.authority).toBe("applied");
    expect(manifest.files).toContain("progression/progression.json");
  });
  it("validates raid rewards and rejects threshold drift", async () => {
    const sourceRoot = resolve(import.meta.dirname, "../../../packages/game-content");
    const temporaryRoot = await makeFixtureRoot("raid-content-drift-");
    try {
      await cp(sourceRoot, temporaryRoot, { recursive: true, force: true });
      const contentPath = resolve(temporaryRoot, "versions/v1/raids/raids.json");
      const content = JSON.parse(await readFile(contentPath, "utf8"));
      expect(content.authority).toBe("working");
      expect(content.personalRewards.B).toEqual({ cosmeticTickets: 7, gemBoxes: 4, rice: 7_250 });
      await expect(validateContent(temporaryRoot)).resolves.toBeUndefined();
      content.gradeDamageThresholds.C = content.gradeDamageThresholds.D;
      await writeFile(contentPath, JSON.stringify(content));
      await expect(validateContent(temporaryRoot)).rejects.toThrow("raid damage thresholds must be strictly increasing");
    } finally {
      await rm(temporaryRoot, { recursive: true, force: true });
    }
  });
  it("rejects a raid with an incorrect seal contribution table", async () => {
    const sourceRoot = resolve(import.meta.dirname, "../../../packages/game-content");
    const temporaryRoot = await makeFixtureRoot("raid-contribution-drift-");
    try {
      await cp(sourceRoot, temporaryRoot, { recursive: true, force: true });
      const contentPath = resolve(temporaryRoot, "versions/v1/raids/raids.json");
      const content = JSON.parse(await readFile(contentPath, "utf8"));
      content.sealContributions.B = 234;
      await writeFile(contentPath, JSON.stringify(content));
      await expect(validateContent(temporaryRoot)).rejects.toThrow("raid seal contributions must match the approved table");
    } finally {
      await rm(temporaryRoot, { recursive: true, force: true });
    }
  });

  it("emits RaidContent as the generated schema root", async () => {
    const sourceRoot = resolve(import.meta.dirname, "../../../packages/game-content");
    const schema = JSON.parse(await readFile(resolve(sourceRoot, "schema/raids.schema.json"), "utf8"));
    expect(schema.type).toBe("object");
    expect(schema.required).toContain("bossInitialAttack");
    expect(schema.properties).toHaveProperty("gradeDamageThresholds");
  });
  it("committed raid schema matches the TypeSpec-generated root artifact", async () => {
    const schemaPath = resolve(import.meta.dirname, "../../../packages/game-content/schema/raids.schema.json");
    const committed = JSON.parse(await readFile(schemaPath, "utf8"));
    expect(committed.type).toBe("object");
    expect(committed.$id).toBe("raids.schema.json");
    expect(committed.required).toContain("bossInitialDefense");
    expect(committed.properties).toHaveProperty("gradeDamageThresholds");
    expect(committed.$defs).toBeDefined();
  });
  it("rejects unknown properties through the self-contained raid root schema", async () => {
    const schema = JSON.parse(await readFile(resolve(import.meta.dirname, "../../../packages/game-content/schema/raids.schema.json"), "utf8"));
    const validate = new Ajv2020({ strict: false }).compile(schema);
    const content = JSON.parse(await readFile(resolve(import.meta.dirname, "../../../packages/game-content/versions/v1/raids/raids.json"), "utf8"));
    expect(validate(content)).toBe(true);
    expect(validate({ ...content, unexpected: true })).toBe(false);
    expect(validate({ ...content, gradeDamageThresholds: { ...content.gradeDamageThresholds, unexpected: 1 } })).toBe(false);
  });

});
