// 단검 평타·숨죽이기·먹안개·삼연 도약 이펙트 시안 굽기(2026-11-07) → dagger-fx/<이펙트>/<변형>[-<타>]/NNN.png + meta.json
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
const HERE = path.dirname(fileURLToPath(import.meta.url));
const PFX = path.resolve(HERE, "../../../../../../90_도구/pixelfx");
const { loadEffect, renderFrames } = await import(pathToFileURL(path.join(PFX, "tools/lib/fxlib.mjs")).href);
const { encodePNG } = await import(pathToFileURL(path.join(PFX, "tools/lib/png.mjs")).href);
const OUT = path.join(HERE, "dagger-fx");
fs.rmSync(OUT, { recursive: true, force: true });
const JOBS = [];
for (const v of ["a", "b", "c", "d"]) {
  if (v === "c" || v === "d") { JOBS.push(["dagger-ink-smoke", v, { variant: v }]); continue; }
  for (const hit of [1, 2, 3, 4]) JOBS.push(["dagger-basic-hit", `${v}-${hit}`, { variant: v, hit }]);
  for (const n of ["dagger-hold-breath", "dagger-ink-smoke", "dagger-triple-leap"]) JOBS.push([n, v, { variant: v }]);
}
for (const [name, tag, options] of JOBS) {
  const e = await loadEffect("ink/" + name);
  const r = renderFrames(e, { bg: "none", options });
  const dir = path.join(OUT, name, tag); fs.mkdirSync(dir, { recursive: true });
  r.frames.forEach((f, i) => fs.writeFileSync(path.join(dir, String(i).padStart(3, "0") + ".png"), encodePNG(r.w, r.h, f.clear)));
  fs.writeFileSync(path.join(dir, "meta.json"), JSON.stringify({ effect: "ink/" + name, options, fps: r.fps, frames: r.n, size: [r.w, r.h], origin_px: e.meta.origin }, null, 1));
  console.log(name, tag, r.n, `${r.w}x${r.h}`);
}
