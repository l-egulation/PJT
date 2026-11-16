// 레이피어 특수 이펙트 4차 시안 굽기(2026-11-08): ink/rapier-special-fx-v4 → special-fx4/<skill>-<variant>/NNN.png + meta.json
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
const HERE = path.dirname(fileURLToPath(import.meta.url));
const PFX = path.resolve(HERE, "../../../../../../90_도구/pixelfx");
const { loadEffect, renderFrames } = await import(pathToFileURL(path.join(PFX, "tools/lib/fxlib.mjs")).href);
const { encodePNG } = await import(pathToFileURL(path.join(PFX, "tools/lib/png.mjs")).href);
const OUT = path.join(HERE, "special-fx4");
fs.rmSync(OUT, { recursive: true, force: true });
const e = await loadEffect("ink/rapier-special-fx-v4");
const JOBS = { charge: ["c1", "c2", "c3", "c1x"], needle: ["n1", "n2", "n3", "n1x"], slip_out: ["s1", "s2", "s3", "s2x"], slip_in: ["s1", "s2", "s3", "s2x"], flurry: ["f1", "f2", "f3", "f1x"] };
for (const [skill, vs] of Object.entries(JOBS)) for (const variant of vs) {
  const r = renderFrames(e, { bg: "none", options: { skill, variant } });
  const dir = path.join(OUT, `${skill}-${variant}`); fs.mkdirSync(dir, { recursive: true });
  r.frames.forEach((f, i) => fs.writeFileSync(path.join(dir, String(i).padStart(3, "0") + ".png"), encodePNG(r.w, r.h, f.clear)));
  fs.writeFileSync(path.join(dir, "meta.json"), JSON.stringify({ effect: "ink/rapier-special-fx-v4", options: { skill, variant }, fps: r.fps, frames: r.n, size: [r.w, r.h], origin_px: e.meta.origin, ppu: 32, t0: skill === "slip_in" ? 0.10 : 0 }, null, 1));
}
console.log("baked", fs.readdirSync(OUT).length);
