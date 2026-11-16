// 유탄 v2 폭발(pixelfx explosions/launcher-grenade-blast)을 투명 PNG 프레임으로 굽는다. PPU32 그대로.
//   node outputs/launcher-grenade-review-20261106/bake_pfx.mjs → pfx/{main_air,main_ground,frag_air,frag_ground}/NN.png + meta.json
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
const HERE = path.dirname(fileURLToPath(import.meta.url));
const PFX = path.resolve(HERE, "../../../../../90_도구/pixelfx");
const { loadEffect, renderFrames } = await import(pathToFileURL(path.join(PFX, "tools/lib/fxlib.mjs")).href);
const { encodePNG } = await import(pathToFileURL(path.join(PFX, "tools/lib/png.mjs")).href);
const OUT = path.join(HERE, "pfx");
fs.rmSync(OUT, { recursive: true, force: true });
const CLIPS = [
  ["main_air", { size: "main", ground: false, seed: 41 }], ["main_ground", { size: "main", ground: true, seed: 41 }],
  ["frag_air", { size: "frag", ground: false, seed: 7 }], ["frag_ground", { size: "frag", ground: true, seed: 7 }],
];
for (const [name, opts] of CLIPS) {
  const e = await loadEffect("explosions/launcher-grenade-blast");
  const r = renderFrames(e, { bg: "none", options: opts });
  const [OX, OY] = e.meta.origin;
  let x0 = r.w, y0 = r.h, x1 = -1, y1 = -1, edge = 0;
  for (const f of r.frames) for (let y = 0; y < r.h; y++) for (let x = 0; x < r.w; x++) if (f.clear[(y * r.w + x) * 4 + 3]) {
    if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y;
    if (x === 0 || y === 0 || x === r.w - 1 || y === r.h - 1) edge++;
  }
  x0 = Math.min(x0, OX); y0 = Math.min(y0, OY); x1 = Math.max(x1, OX); y1 = Math.max(y1, OY);
  // 끝의 빈 장은 버린다
  let n = r.frames.length;
  while (n > 1 && !r.frames[n - 1].clear.some((v, i) => i % 4 === 3 && v)) n--;
  const w = x1 - x0 + 1, h = y1 - y0 + 1, dir = path.join(OUT, name);
  fs.mkdirSync(dir, { recursive: true });
  r.frames.slice(0, n).forEach((f, i) => {
    const px = new Uint8Array(w * h * 4);
    for (let y = 0; y < h; y++) px.set(f.clear.subarray(((y + y0) * r.w + x0) * 4, ((y + y0) * r.w + x0 + w) * 4), y * w * 4);
    fs.writeFileSync(path.join(dir, String(i).padStart(2, "0") + ".png"), encodePNG(w, h, px));
  });
  const meta = { effect: "explosions/launcher-grenade-blast", options: opts, fps: r.fps, frames: n, size: [w, h], origin_px: [OX - x0, OY - y0], pivot: [(OX - x0) / w, (y1 + 1 - OY) / h] };
  fs.writeFileSync(path.join(dir, "meta.json"), JSON.stringify(meta, null, 1));
  console.log(name.padEnd(12), n, "frames", `${w}x${h}`, "origin", meta.origin_px.join(","), edge ? `EDGE ${edge}px` : "");
}
