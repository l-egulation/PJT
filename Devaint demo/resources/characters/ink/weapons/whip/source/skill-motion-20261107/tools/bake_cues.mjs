// 채찍 스킬 표시 시안 굽기(2026-11-07): ink/whip-cues(knot-mark·knot-tighten·net-mark), ink/whip-bind(loosen w1, 가시 w3 0.8초)
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
const HERE = path.dirname(fileURLToPath(import.meta.url));
const PFX = path.resolve(HERE, "../../../../../90_도구/pixelfx");
const { loadEffect, renderFrames } = await import(pathToFileURL(path.join(PFX, "tools/lib/fxlib.mjs")).href);
const { encodePNG } = await import(pathToFileURL(path.join(PFX, "tools/lib/png.mjs")).href);
const OUT = path.join(HERE, "cuefx");
fs.rmSync(OUT, { recursive: true, force: true });
async function bake(effect, name, options) {
  const e = await loadEffect(effect);
  const r = renderFrames(e, { bg: "none", options });
  const dir = path.join(OUT, name); fs.mkdirSync(dir, { recursive: true });
  r.frames.forEach((f, i) => fs.writeFileSync(path.join(dir, String(i).padStart(3, "0") + ".png"), encodePNG(r.w, r.h, f.clear)));
  fs.writeFileSync(path.join(dir, "meta.json"), JSON.stringify({ effect, options, fps: r.fps, frames: r.n, size: [r.w, r.h], origin_px: e.meta.origin }, null, 1));
  console.log(name, r.n, `${r.w}x${r.h}`);
}
await bake("ink/whip-cues", "knot-mark", { kind: "knot-mark", window: 3 });
await bake("ink/whip-cues", "knot-tighten", { kind: "knot-tighten" });
await bake("ink/whip-cues", "net-mark", { kind: "net-mark", width: 166 });
for (const size of ["small", "large"]) for (const part of ["front", "back"]) {
  await bake("ink/whip-bind", `loosen_${size}_${part}`, { variant: "w1", part, size, hold: 1.2, loosen: true });
  await bake("ink/whip-bind", `thorn_${size}_${part}`, { variant: "w3", part, size, hold: 0.8 });
}
