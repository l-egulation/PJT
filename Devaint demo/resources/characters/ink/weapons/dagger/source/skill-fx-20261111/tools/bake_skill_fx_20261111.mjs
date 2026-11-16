// 2026-11-11 판정 확대 대응 이펙트 시안 굽기 → ../viewer/drafts/<name>/NN.png + drafts.json (재생 비교용)
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { loadEffect, renderFrames } from "./tools/lib/fxlib.mjs";
import { encodePNG } from "./tools/lib/png.mjs";
const HERE = path.dirname(fileURLToPath(import.meta.url));
const OUT = path.join(HERE, "..", "viewer", "drafts");
const PINK = ["#2a1240", "#7a1f6e", "#c8378f", "#f05fae", "#ff94c8", "#ffc2e0", "#ffe6f2", "#ffffff"];
const SKY = ["#0f2340", "#1d4f86", "#2f86c8", "#4fb4ea", "#86d6f6", "#bdeafc", "#e6f8ff", "#ffffff"];
const JOBS = [
  { name: "bomb_basic", id: "explosions/bomb-pop-r3", opts: {} },
  { name: "triple_0", id: "explosions/bomb-triple-r3", opts: { seed: 11 } },
  { name: "triple_1", id: "explosions/bomb-triple-r3", opts: { seed: 29, palette: PINK, shape: "up" } },
  { name: "triple_2", id: "explosions/bomb-triple-r3", opts: { seed: 47, palette: SKY, shape: "wide" } },
  { name: "ink_smoke", id: "ink/dagger-ink-smoke-wide", opts: {} },
  { name: "net_mark", id: "ink/whip-net-wide", opts: {} },
  { name: "star_arc", id: "ink/shuriken-big-star", opts: { trail: "arc" } },
  { name: "star_smear", id: "ink/shuriken-big-star", opts: { trail: "smear" } },
  { name: "star_outline", id: "ink/shuriken-big-star", opts: { trail: "outline" } },
  { name: "star_ghost", id: "ink/shuriken-big-star", opts: { trail: "ghost" } },
  { name: "smoke_dome", id: "ink/dagger-ink-smoke-alt", opts: { shape: "dome" } },
  { name: "smoke_rain", id: "ink/dagger-ink-smoke-alt", opts: { shape: "rain" } },
  { name: "smoke_pool", id: "ink/dagger-ink-smoke-alt", opts: { shape: "pool" } },
  { name: "gs_impact", id: "ink/greatsword-impact", opts: {} },
  { name: "star_dash", id: "ink/shuriken-big-star", opts: { trail: "dash" } },
  { name: "star_body", id: "ink/shuriken-big-star", opts: { trail: "none" } },
  { name: "smoke_bomb", id: "explosions/ink-smoke-bomb", opts: {} },
  { name: "gs_slam", id: "impacts/greatsword-slam", opts: {} },
  { name: "smoke_toon_ink", id: "ink/dagger-cartoon-smoke", opts: { tone: "ink" } },
  { name: "smoke_toon_light", id: "ink/dagger-cartoon-smoke", opts: { tone: "light" } },
  { name: "star_fast", id: "ink/shuriken-big-star", opts: { trail: "dash", quarter: 0.06, sparks: 340, sparkPower: 1.35 } },
  { name: "star_fast_body", id: "ink/shuriken-big-star", opts: { trail: "none", quarter: 0.06 } },
  { name: "smoke_ref_cel", id: "ink/dagger-smoke-ref", opts: { style: "cel" } },
  { name: "smoke_ref_fog", id: "ink/dagger-smoke-ref", opts: { style: "fog" } },
  { name: "smoke_ref_fog_ink", id: "ink/dagger-smoke-ref", opts: { style: "fog", tone: "fogInk" } },
  { name: "smoke_v_fade", id: "ink/dagger-smoke-ref", opts: { style: "fog", vanish: "fade" } },
  { name: "smoke_v_fade_ink", id: "ink/dagger-smoke-ref", opts: { style: "fog", vanish: "fade", tone: "fogInk" } },
  { name: "smoke_v_rise", id: "ink/dagger-smoke-ref", opts: { style: "fog", vanish: "rise" } },
  { name: "smoke_v_rise_ink", id: "ink/dagger-smoke-ref", opts: { style: "fog", vanish: "rise", tone: "fogInk" } },
  { name: "smoke_v_thin", id: "ink/dagger-smoke-ref", opts: { style: "fog", vanish: "thin" } },
  { name: "smoke_v_thin_ink", id: "ink/dagger-smoke-ref", opts: { style: "fog", vanish: "thin", tone: "fogInk" } },
  { name: "smoke_ref_inkcel", id: "ink/dagger-smoke-ref", opts: { style: "inkcel" } },
  { name: "star_fast_sparks", id: "ink/shuriken-big-star", opts: { trail: "sparksOnly", quarter: 0.06, sparks: 340, sparkPower: 1.35 } },
];
const ONLY = process.argv.slice(2);
if (!ONLY.length) fs.rmSync(OUT, { recursive: true, force: true });
const prev = (() => { try { return JSON.parse(fs.readFileSync(path.join(OUT, "..", "drafts.js"), "utf8").replace(/^window.DRAFTS = /, "").replace(/;\s*$/, "")); } catch { return {}; } })();
const list = ONLY.length ? prev : {};
for (const j of (ONLY.length ? JOBS.filter((x) => ONLY.includes(x.name)) : JOBS)) {
  const e = await loadEffect(j.id);
  fs.rmSync(path.join(OUT, j.name), { recursive: true, force: true });
  const r = renderFrames(e, { bg: "none", options: j.opts });
  const dir = path.join(OUT, j.name); fs.mkdirSync(dir, { recursive: true });
  r.frames.forEach((f, i) => fs.writeFileSync(path.join(dir, String(i).padStart(2, "0") + ".png"), encodePNG(r.w, r.h, f.clear)));
  list[j.name] = { id: j.id, opts: j.opts, w: r.w, h: r.h, origin: e.meta.origin, fps: r.fps, n: r.n, loop: !!e.meta.loop };
  console.log(j.name, r.n, `${r.w}x${r.h}`);
}
fs.writeFileSync(path.join(OUT, "..", "drafts.js"), "window.DRAFTS = " + JSON.stringify(list) + ";\n");
