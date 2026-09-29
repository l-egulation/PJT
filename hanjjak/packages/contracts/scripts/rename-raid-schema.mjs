import { readFile, rm, writeFile } from "node:fs/promises";
import { resolve } from "node:path";

const schemaDir = resolve(process.argv[2] ?? process.env.RAID_SCHEMA_DIR ?? resolve(import.meta.dirname, "../../../packages/game-content/schema"));
const artifactNames = [
  "ContentAuthority.json",
  "RaidContent.json",
  "RaidGradeThresholds.json",
  "RaidPersonalRewards.json",
  "RaidRankRewards.json",
  "RaidRewardBundleContent.json",
  "RaidSealContributions.json",
];
const artifactByRef = new Map(artifactNames.map((name) => [name, name.slice(0, -".json".length)]));
const source = JSON.parse(await readFile(resolve(schemaDir, "RaidContent.json"), "utf8"));

function rewriteRefs(value) {
  if (Array.isArray(value)) return value.map(rewriteRefs);
  if (value === null || typeof value !== "object") return value;
  if (typeof value.$ref === "string" && artifactByRef.has(value.$ref)) {
    return { ...value, $ref: `#/$defs/${artifactByRef.get(value.$ref)}` };
  }
  return Object.fromEntries(Object.entries(value).map(([key, child]) => [key, rewriteRefs(child)]));
}

const defs = {};
for (const name of artifactNames) {
  if (name === "RaidContent.json") continue;
  const { $schema, $id, ...body } = JSON.parse(await readFile(resolve(schemaDir, name), "utf8"));
  defs[name.slice(0, -".json".length)] = rewriteRefs(body);
}
const { $schema, ...root } = rewriteRefs(source);
root.$defs = defs;
await writeFile(resolve(schemaDir, "raids.schema.json"), `${JSON.stringify({ $schema: "https://json-schema.org/draft/2020-12/schema", ...root }, null, 2)}\n`);
await Promise.all(artifactNames.flatMap((name) => [name, name.replace(/\.json$/, ".yaml")]).map((name) => rm(resolve(schemaDir, name), { force: true })));
