import { Rng, fit, clamp } from "../../core/index.js";
import { needleGlow, sparks, FLURRY_PAL } from "./dagger-flurry-cuts.js";

// 삼연 도약 시안(2026-11-07). 0.08~0.23초에 6유닛(192화소, PPU 32) 일직선 돌진, 적 관통(melee.md).
//   variant="a" 먹 잔상줄: 출발 발밑 먹 뭉치 + 몸 뒤로 끌리는 먹 속도선 + 도착 미끄럼 먼지. (몸 그림 먹빛 잔상 셋은 게임 코드가 겹친다)
//   variant="b" 일섬: 지나간 자리에 허리 높이 가는 노란 선이 따라붙고, 도착 순간 굵게 번쩍인 뒤 출발점부터 지워지며 불티가 튄다.
// 원점 = 출발 발밑 가운데, 앞이 +x, 시각 0 = 시전 시작.
export const meta = {
  name: "Dagger triple leap (ink)",
  desc: "A six-unit dash: ink kick-off, trailing speed lines or a golden slash line along the path, and a landing skid.",
  tags: ["ink", "dagger", "dash", "one-shot"],
  size: [288, 104],
  origin: [40, 96],
  duration: 0.6,
  loop: false,
  fps: 50,
  bg: "light",
  options: { variant: "a", seed: 4, start: 0.08, dur: 0.15, dist: 192 },
};
const INKS = ["#1b1616", "#3a3230", "#5a4e4a"];

function puff(p, x, y, r, c) {
  for (let yy = Math.floor(-r); yy <= r; yy++) for (let xx = Math.floor(-r); xx <= r; xx++) if (xx * xx + yy * yy <= r * r && y + yy <= 0) p.px(x + xx, y + yy, c);
}

export function create(o) {
  const C = fit(FLURRY_PAL, 5), [INK, INK2, INK3] = fit(INKS, 3), R = new Rng(o.seed);
  const X = (t) => o.dist * clamp((t - o.start) / o.dur), T1 = o.start + o.dur;
  const lines = Array.from({ length: 6 }, (_, i) => ({ y: -10 - i * 9 - R.range(0, 4), len: R.range(34, 70), lag: R.range(6, 16), w: R.chance(0.5) ? 2 : 1 }));
  const kick = Array.from({ length: 6 }, () => ({ x: R.range(-4, 6), vx: -R.range(40, 110), vy: -R.range(10, 40), r: R.range(2.5, 4.5), life: R.range(0.14, 0.24) }));
  const skid = Array.from({ length: 6 }, () => ({ x: R.range(-6, 4), vx: R.range(20, 80), vy: -R.range(8, 30), r: R.range(2, 3.5), life: R.range(0.12, 0.2) }));
  const sp = Array.from({ length: 22 }, () => {
    const a = R.range(-Math.PI, 0);
    return { ca: Math.cos(a), sa: Math.sin(a), v: R.range(60, 170), life: R.range(0.08, 0.2), d: R.range(0, 0.05), x: R.range(0, o.dist) };
  });
  let t = 0;
  return {
    update(dt) { t += dt; },
    draw(p) {
      const g = t + 1e-6;
      for (const k of kick) { const q = g - o.start; if (q < 0 || q > k.life) continue; const u = q / k.life; puff(p, k.x + k.vx * q, -k.r + k.vy * q, k.r * (1 - 0.6 * u), u < 0.5 ? INK : INK2); }
      for (const k of skid) { const q = g - T1; if (q < 0 || q > k.life) continue; const u = q / k.life; puff(p, o.dist + k.x + k.vx * q, -k.r + k.vy * q, k.r * (1 - 0.6 * u), u < 0.5 ? INK : INK2); }
      if (o.variant === "b") {
        const L = o.dist + 24, cx = L / 2 - 12, y = -30;
        if (g >= o.start && g < T1) { const h = X(g); for (let x = 0; x < h - 8; x++) p.px(x, y, x > h - 40 ? C[3] : C[2]); }
        const q = g - T1;
        if (q >= 0) {
          needleGlow(p, cx, y, 0, L, q < 0.05 ? 3.5 : 2.5, 1, clamp((q - 0.03) / 0.14), C, 3);
          for (const s of sp) sparks(p, s.x, y, q - s.d * 2, [s], C);
        }
        return;
      }
      const now = X(g);
      for (const l of lines) {
        if (g < o.start) continue;
        const head = now - l.lag, tl = g < T1 ? head - l.len * clamp((g - o.start) / 0.05) : head - l.len * (1 - clamp((g - T1) / 0.12));
        if (head <= tl) continue;
        for (let x = Math.floor(Math.max(0, tl)); x < head; x++) for (let w = 0; w < l.w; w++) p.px(x, l.y + w, x < tl + 6 ? INK3 : INK);
      }
    },
  };
}
