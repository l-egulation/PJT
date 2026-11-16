// 고른 폭탄 스킬 이펙트(모음판 all.html 의 다섯 스킬)를 투명 PNG 프레임으로 굽는다.
//   node outputs/bomb-skill-fx/bake.mjs   → outputs/bomb-skill-fx/bake/<clip>/NN.png + meta.json, bake/clips.json
// 게임 1유닛 = 32px(PPU32) 그대로. 클립마다 모든 장의 내용 상자로 잘라 내고, 기준점(이펙트 원점)을 잘린 장 기준 픽셀로 적는다.
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const PFX = path.resolve(HERE, "../../../../../90_도구/pixelfx");
const { loadEffect, renderFrames } = await import(pathToFileURL(path.join(PFX, "tools/lib/fxlib.mjs")).href);
const { encodePNG } = await import(pathToFileURL(path.join(PFX, "tools/lib/png.mjs")).href);
const OUT = path.join(HERE, "bake");

const PINK = ["#2a1240", "#7a1f6e", "#c8378f", "#f05fae", "#ff94c8", "#ffc2e0", "#ffe6f2", "#ffffff"];
const SKY = ["#0f2340", "#1d4f86", "#2f86c8", "#4fb4ea", "#86d6f6", "#bdeafc", "#e6f8ff", "#ffffff"];
// anchor: center = 폭탄(미사일) 중심, ground = 그 아래 땅. facing: 오른쪽을 보고 그렸으니 왼쪽이면 뒤집는다
const CLIPS = [
  { name: "basic", id: "explosions/bomb-pop-a3", anchor: "center" },
  { name: "triple_0", id: "explosions/bomb-triple-hit", opts: { size: 1, seed: 11 }, anchor: "center" },
  { name: "triple_1", id: "explosions/bomb-triple-hit", opts: { size: 1, seed: 29, palette: PINK, shape: "up" }, anchor: "center" },
  { name: "triple_2", id: "explosions/bomb-triple-hit", opts: { size: 1, seed: 47, palette: SKY, shape: "wide" }, anchor: "center" },
  { name: "range", id: "explosions/bomb-big-c", anchor: "ground" },
  { name: "timed_fuse", id: "explosions/bomb-timed-fuse", opts: { mark: false, style: "heat" }, anchor: "ground" },
  { name: "timed", id: "explosions/bomb-big-b", anchor: "ground" },
  { name: "missile_launch", id: "explosions/bomb-missile-launch", anchor: "center", facing: true },
  { name: "missile_body", id: "explosions/bomb-missile-comet", anchor: "center", facing: true, loop: true },
  { name: "missile_trail", id: "explosions/bomb-missile-trail", opts: { len: 64, style: "fire" }, anchor: "center", facing: true, segment: 64 },
  { name: "missile_hit", id: "explosions/bomb-missile-hit-b", anchor: "center", facing: true },
];

fs.rmSync(OUT, { recursive: true, force: true });
const catalog = [];
for (const c of CLIPS) {
  const e = await loadEffect(c.id);
  const r = renderFrames(e, { bg: "none", options: c.opts ?? {} });
  const [OX, OY] = e.meta.origin;
  // 모든 장을 덮는 내용 상자(알파 > 0)
  let x0 = r.w, y0 = r.h, x1 = -1, y1 = -1;
  for (const f of r.frames) for (let y = 0; y < r.h; y++) for (let x = 0; x < r.w; x++) if (f.clear[(y * r.w + x) * 4 + 3]) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
  // 기준점이 상자 안에 들도록 넓힌다(피벗이 0..1 안에 있어야 다루기 쉽다)
  x0 = Math.min(x0, OX); y0 = Math.min(y0, OY); x1 = Math.max(x1, OX); y1 = Math.max(y1, OY);
  const w = x1 - x0 + 1, h = y1 - y0 + 1;
  const dir = path.join(OUT, c.name); fs.mkdirSync(dir, { recursive: true });
  r.frames.forEach((f, i) => {
    const px = new Uint8Array(w * h * 4);
    for (let y = 0; y < h; y++) px.set(f.clear.subarray(((y + y0) * r.w + x0) * 4, ((y + y0) * r.w + x0 + w) * 4), y * w * 4);
    fs.writeFileSync(path.join(dir, String(i).padStart(2, "0") + ".png"), encodePNG(w, h, px));
  });
  // Unity 피벗: 왼쪽 아래 기준 0..1. 원점 픽셀의 가운데가 아니라 모서리(OX, OY)를 그대로 쓴다
  const pivot = [(OX - x0) / w, (y1 + 1 - OY) / h];
  const meta = { name: c.name, effect: c.id, options: c.opts ?? {}, anchor: c.anchor, facing: !!c.facing, loop: !!c.loop, segment: c.segment ?? 0,
    fps: r.fps, frames: r.n, size: [w, h], pivot, crop: { x: x0, y: y0, w, h }, origin: [OX, OY] };
  fs.writeFileSync(path.join(dir, "meta.json"), JSON.stringify(meta, null, 1));
  catalog.push(meta);
  console.log(c.name.padEnd(15), String(r.n).padStart(3), "frames", `${w}x${h}`, "pivot", pivot.map((v) => v.toFixed(3)).join(","));
}
fs.writeFileSync(path.join(OUT, "clips.json"), JSON.stringify(catalog, null, 1));
console.log(catalog.reduce((a, c) => a + c.frames, 0), "frames total");
