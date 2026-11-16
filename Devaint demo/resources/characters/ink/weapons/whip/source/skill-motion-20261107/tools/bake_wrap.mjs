// 채찍 휘감기 터짐 시안(pixelfx ink/whip-wrap-snap, variant a·b·c)을 투명 PNG 프레임으로 굽는다. PPU32 그대로.
//   node bake_wrap.mjs → wrapfx/{a,b,c}/NN.png + meta.json
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
const HERE = path.dirname(fileURLToPath(import.meta.url));
const PFX = path.resolve(HERE, "../../../../../90_도구/pixelfx");
const { loadEffect, renderFrames } = await import(pathToFileURL(path.join(PFX, "tools/lib/fxlib.mjs")).href);
const { encodePNG } = await import(pathToFileURL(path.join(PFX, "tools/lib/png.mjs")).href);
const OUT = path.join(HERE, "wrapfx");
fs.rmSync(OUT, { recursive: true, force: true });
for (const variant of ["a", "b", "c"]) {
  const e = await loadEffect("ink/whip-wrap-snap");
  const r = renderFrames(e, { bg: "none", options: { variant } });
  const [OX, OY] = e.meta.origin, dir = path.join(OUT, variant);
  fs.mkdirSync(dir, { recursive: true });
  let clipped = 0;
  r.frames.forEach((f, i) => {
    for (let y = 0; y < r.h; y++) for (let x = 0; x < r.w; x++) if (f.clear[(y * r.w + x) * 4 + 3] && (x === 0 || y === 0 || x === r.w - 1 || y === r.h - 1)) clipped++;
    fs.writeFileSync(path.join(dir, String(i).padStart(2, "0") + ".png"), encodePNG(r.w, r.h, f.clear));
  });
  fs.writeFileSync(path.join(dir, "meta.json"), JSON.stringify({ effect: "ink/whip-wrap-snap", variant, fps: r.fps, frames: r.n, size: [r.w, r.h], origin_px: [OX, OY] }, null, 1));
  console.log(variant, r.n, "frames", `${r.w}x${r.h}`, clipped ? `EDGE ${clipped}px` : "");
}
