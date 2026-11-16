// 채찍 전용 감김 표시 시안(pixelfx ink/whip-bind, w1·w2·w3 × front·back)을 투명 PNG 로 굽는다. PPU32, 묶임 1.2초(휘감기).
//   node bake_bind.mjs → bindfx/{w1,w2,w3}/{front,back}/NN.png + meta.json
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
const HERE = path.dirname(fileURLToPath(import.meta.url));
const PFX = path.resolve(HERE, "../../../../../90_도구/pixelfx");
const { loadEffect, renderFrames } = await import(pathToFileURL(path.join(PFX, "tools/lib/fxlib.mjs")).href);
const { encodePNG } = await import(pathToFileURL(path.join(PFX, "tools/lib/png.mjs")).href);
const OUT = path.join(HERE, "bindfx");
fs.rmSync(OUT, { recursive: true, force: true });
for (const variant of ["w1", "w2", "w3"]) for (const size of ["small", "large"]) for (const part of ["front", "back"]) {
  const e = await loadEffect("ink/whip-bind");
  const r = renderFrames(e, { bg: "none", options: { variant, part, size, hold: 1.2 } });
  const dir = path.join(OUT, variant, size, part); fs.mkdirSync(dir, { recursive: true });
  r.frames.forEach((f, i) => fs.writeFileSync(path.join(dir, String(i).padStart(2, "0") + ".png"), encodePNG(r.w, r.h, f.clear)));
  fs.writeFileSync(path.join(dir, "meta.json"), JSON.stringify({ effect: "ink/whip-bind", variant, size, part, fps: r.fps, frames: r.n, size: [r.w, r.h], origin_px: e.meta.origin }, null, 1));
  console.log(variant, size, part, r.n, `${r.w}x${r.h}`);
}
