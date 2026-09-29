import { mkdtemp, readFile, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { resolve } from "node:path";
import { execFile } from "node:child_process";
import { promisify } from "node:util";

const execFileAsync = promisify(execFile);
const projectRoot = resolve(import.meta.dirname, "..");
const tspScript = resolve(projectRoot, "node_modules/@typespec/compiler/cmd/tsp.js");
const committed = resolve(projectRoot, "../../packages/game-content/schema/raids.schema.json");
const temporaryRoot = await mkdtemp(resolve(tmpdir(), "raid-schema-drift-"));
const temporarySchemaDir = resolve(temporaryRoot, "schema");
try {
  await execFileAsync(process.execPath, [
    tspScript,
    "compile", ".",
    "--option", `@typespec/json-schema.emitter-output-dir=${temporarySchemaDir}`,
    "--option", `@typespec/openapi3.emitter-output-dir=${resolve(temporaryRoot, "openapi")}`,
  ], { cwd: projectRoot });
  await execFileAsync(process.execPath, [resolve(import.meta.dirname, "rename-raid-schema.mjs"), temporarySchemaDir], { cwd: projectRoot });
  const actual = await readFile(committed, "utf8");
  const expected = await readFile(resolve(temporarySchemaDir, "raids.schema.json"), "utf8");
  if (actual !== expected) throw new Error("committed raids.schema.json drifted from TypeSpec output");
  console.log("raid schema drift check passed");
} finally {
  await rm(temporaryRoot, { recursive: true, force: true });
}
