// 수리검 특수 이펙트 시안 굽기(2026-11-08): ink/shuriken-skill-fx → shuriken-fx/<kind>-<variant>[-<mode>]/NNN.png + meta.json
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
const HERE = path.dirname(fileURLToPath(import.meta.url));
const PFX = path.resolve(HERE, "../../../../../../90_도구/pixelfx");
const { loadEffect, renderFrames } = await import(pathToFileURL(path.join(PFX, "tools/lib/fxlib.mjs")).href);
const { encodePNG } = await import(pathToFileURL(path.join(PFX, "tools/lib/png.mjs")).href);
const OUT = path.join(HERE, "shuriken-fx");
fs.rmSync(OUT, { recursive: true, force: true });
const JOBS = [];
for (const v of ["a", "b"]) {
  JOBS.push({ kind: "star", variant: v }, { kind: "sweep", variant: v }, { kind: "blast", variant: v }, { kind: "giant", variant: v });
  if (v === "b") for (const g of ["c", "c1", "c2", "c3", "c4"]) JOBS.push({ kind: "giant", variant: g });   // 11-08 빠른 회전 후보 + 바닥 스파크
  for (const mode of ["out", "in"]) JOBS.push({ kind: "step", variant: v, mode });
}
JOBS.push({ kind: "star", variant: "b", r: 6.5, tag: "star-b-basic" });   // 평타 탄(지름 0.4u)도 b 로 통일
const e = await loadEffect("ink/shuriken-skill-fx");
for (const options of JOBS) {
  const r = renderFrames(e, { bg: "none", options });
  const tag = options.tag || [options.kind, options.variant, options.mode].filter(Boolean).join("-");
  const dir = path.join(OUT, tag); fs.mkdirSync(dir, { recursive: true });
  r.frames.forEach((f, i) => fs.writeFileSync(path.join(dir, String(i).padStart(3, "0") + ".png"), encodePNG(r.w, r.h, f.clear)));
  fs.writeFileSync(path.join(dir, "meta.json"), JSON.stringify({ effect: "ink/shuriken-skill-fx", options, fps: r.fps, frames: r.n, size: [r.w, r.h], origin_px: e.meta.origin }, null, 1));
  console.log(tag, r.n);
}
