// 모아쏘기 폭발(pixelfx explosions/charge-shot-burst, step 1·2·3)을 투명 PNG 프레임으로 굽는다. PPU32 그대로.
//   node outputs/launcher-charge-review-20261106/bake_pfx.mjs → pfx/step{1,2,3}/NN.png + meta.json
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
const HERE = path.dirname(fileURLToPath(import.meta.url));
const PFX = path.resolve(HERE, "../../../../../90_도구/pixelfx");
const { loadEffect, renderFrames } = await import(pathToFileURL(path.join(PFX, "tools/lib/fxlib.mjs")).href);
const { encodePNG } = await import(pathToFileURL(path.join(PFX, "tools/lib/png.mjs")).href);
const OUT = path.join(HERE, "pfx");
fs.rmSync(OUT, { recursive: true, force: true });
for (const step of [1, 2, 3]) {
  const e = await loadEffect("explosions/charge-shot-burst");
  const r = renderFrames(e, { bg: "none", options: { step } });
  const [OX, OY] = e.meta.origin;
  let x0 = r.w, y0 = r.h, x1 = -1, y1 = -1, clipped = 0;
  for (const f of r.frames) for (let y = 0; y < r.h; y++) for (let x = 0; x < r.w; x++) if (f.clear[(y * r.w + x) * 4 + 3]) {
    if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y;
    if (x === 0 || y === 0 || x === r.w - 1 || y === r.h - 1) clipped++;
  }
  x0 = Math.min(x0, OX); y0 = Math.min(y0, OY); x1 = Math.max(x1, OX); y1 = Math.max(y1, OY);
  const w = x1 - x0 + 1, h = y1 - y0 + 1, dir = path.join(OUT, "step" + step);
  fs.mkdirSync(dir, { recursive: true });
  r.frames.forEach((f, i) => {
    const px = new Uint8Array(w * h * 4);
    for (let y = 0; y < h; y++) px.set(f.clear.subarray(((y + y0) * r.w + x0) * 4, ((y + y0) * r.w + x0 + w) * 4), y * w * 4);
    fs.writeFileSync(path.join(dir, String(i).padStart(2, "0") + ".png"), encodePNG(w, h, px));
  });
  const meta = { effect: "explosions/charge-shot-burst", step, fps: r.fps, frames: r.n, size: [w, h], origin_px: [OX - x0, OY - y0], pivot: [(OX - x0) / w, (y1 + 1 - OY) / h] };
  fs.writeFileSync(path.join(dir, "meta.json"), JSON.stringify(meta, null, 1));
  console.log("step" + step, r.n, "frames", `${w}x${h}`, "origin", meta.origin_px.join(","), clipped ? `EDGE ${clipped}px` : "");
}
