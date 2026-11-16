import { Rng, fit, clamp, smooth, ease, noise2, TAU } from "../../core/index.js";

// 채찍 스킬마다 「무엇을 알아야 하는지」를 보여 주는 표시 시안(2026-11-07). 먹·노랑만, 검은 테두리·출렁임 없음.
//   kind="knot-mark"    매듭 접근: 머리 위 매듭 표식 + 3초 동안 줄어드는 노란 고리(남은 재입력 시간). 끝 0.5초 깜빡, 끝나면 매듭이 풀려 사라짐.
//   kind="knot-tighten" 매듭 조이기: 몸 가운데 먹 고리가 0.6초 동안 조여들고(끝무렵 노란 불티), 0.6초에 확 조여 크게 터짐.
//   kind="net-mark"     가시 그물: 바닥에 판정 폭만큼 먹 자국이 왼→오로 긋고, 짧은 가시가 솟았다 0.5초에 갉아먹히듯 사라짐.
// 원점: knot-mark 는 표식 가운데, knot-tighten 은 몸 가운데, net-mark 는 자국 가운데 바닥.
export const meta = {
  name: "Whip cues", desc: "Whip skill readability cues: recast knot timer, tightening knot burst, net ground mark.",
  tags: ["ink", "whip", "cue"], size: [200, 120], origin: [100, 60], duration: 3.2, loop: false, fps: 25, bg: "light",
  options: { kind: "knot-mark", window: 3, width: 166, seed: 9 },
};
const PAL = ["#1b1616", "#3a3230", "#7a4a14", "#eba516", "#ffe68a", "#fff6d0"];

function ball(x, y, b) {
  const dx = x - b.x, dy = y - b.y, al = dx * b.ux + dy * b.uy, pe = -dx * b.uy + dy * b.ux;
  const q = (al * al) / (b.r * b.r * b.s * b.s) + (pe * pe) / (b.r * b.r);
  return q < 1 ? (1 - q) * (1 - q) : 0;
}
// 8자 매듭(두 고리가 맞물린 모양)의 농도
function knot(x, y, s) {
  const a = Math.hypot((x + 3.2 * s) / (3.6 * s), y / (3.0 * s)), b = Math.hypot((x - 3.2 * s) / (3.6 * s), y / (3.0 * s));
  const w = 0.32;
  return Math.max(Math.exp(-(((a - 1) / w) ** 2)), Math.exp(-(((b - 1) / w) ** 2)), Math.exp(-((x / (1.6 * s)) ** 2 + (y / (1.8 * s)) ** 2) * 2) * 0.9);
}

export function create(o) {
  const [INK, INK2, , YEL, YEL2, IVORY] = fit(PAL, 6), R = new Rng(o.seed), K = o.kind;
  let t = 0;
  const blobs = []; let burst = false;
  const TIGHT = 0.6;
  const spikes = Array.from({ length: 14 }, (_, i) => ({ x: (i + 0.5) / 14, h: R.range(8, 15), lean: R.range(-0.4, 0.4), d: R.range(0, 0.04) }));
  return {
    update(dt) {
      t += dt;
      if (K === "knot-tighten" && !burst && t >= TIGHT) {
        burst = true;
        for (let i = 0; i < 22; i++) {
          const a = R.range(0, TAU), sp = R.range(140, 300), r = R.range(2.5, 6);
          blobs.push({ x: Math.cos(a) * 4, y: Math.sin(a) * 4, vx: Math.cos(a) * sp, vy: Math.sin(a) * sp * 0.8 - 40, r, r0: r, life: R.range(0.35, 0.55), age: 0 });
        }
      }
      for (const b of blobs) {
        b.age += dt; b.vx *= 1 - 1.8 * dt; b.vy = b.vy * (1 - 2 * dt) + (b.age > 0.05 ? 800 : 0) * dt;
        b.x += b.vx * dt; b.y += b.vy * dt; b.r = b.r0 * (1 - smooth(0.4, 1, b.age / b.life));
      }
    },
    draw(p) {
      const f = Math.floor(t * 25 + 1e-6);
      if (K === "knot-mark") {
        const W = o.window ?? 3, k = clamp(t / W);
        if (t >= W + 0.2) return;
        const blinkOff = t > W - 0.5 && t < W && f % 2 === 1;
        const undo = clamp((t - W) / 0.2);                       // 끝: 매듭이 풀리며 두 고리가 벌어져 사라짐
        const R0 = 13, startA = -Math.PI / 2, span = (1 - k) * TAU;
        p.shade(-20, -20, 20, 20, (x, y) => {
          if (undo > 0) {
            const s = 1 + undo * 0.8; const v = knot(x * (1 - undo * 0.3), y, s);
            if (v > 0.55 && noise2(x * 0.5, y * 0.5 + f, o.seed) > undo * 1.6 - 0.6) return INK;
            return;
          }
          if (!blinkOff) {
            const d = Math.hypot(x, y), ang = (Math.atan2(y, x) - startA + TAU * 2) % TAU;
            if (Math.abs(d - R0) < 1.3 && ang <= span) return d < R0 ? YEL : YEL2;   // 남은 시간 고리(시계 방향으로 줄어듦)
          }
          const v = knot(x, y, 1);
          if (v > 0.55) return INK;
          if (v > 0.38) return INK2;
        });
        return;
      }
      if (K === "knot-tighten") {
        const items = [];
        if (t < TIGHT + 0.04) {
          const k = ease.inCubic(clamp(t / TIGHT));
          const r = 15 - 9 * k, w = 1.6 + 1.8 * k;
          const strain = t > TIGHT - 0.18;
          p.shade(-24, -24, 24, 24, (x, y) => {
            const wx = x + (strain ? 0.6 * noise2(x * 0.4, y * 0.4 + f, o.seed) : 0);
            const d = Math.hypot(wx, y * 1.15);
            if (Math.abs(d - r) < w) return INK;
            // 매듭 마디(위쪽): 두 고리가 맞물린 작은 덩어리
            if (knot(x, y + r, 0.7) > 0.6) return INK;
            if (strain && Math.abs(d - r) < w + 2.2 && noise2(x * 0.9, y * 0.9 + f * 1.7, o.seed + 5) > 0.55) return f % 2 ? YEL : YEL2;  // 조임 불티
          });
          return;
        }
        const u = t - TIGHT;
        for (const b of blobs) if (b.r > 0.6) { const v = Math.hypot(b.vx, b.vy); items.push({ x: b.x, y: b.y, r: b.r, s: 1 + Math.min(2.2, v / 90), ux: v ? b.vx / v : 1, uy: v ? b.vy / v : 0 }); }
        const flash = u < 0.08 ? 1 - u / 0.08 : 0, ringOn = u < 0.24, rk = u / 0.24;
        p.shade(-60, -60, 60, 60, (x, y) => {
          if (flash > 0) {
            const d = Math.hypot(x, y);
            if (d < 9 * (0.5 + 0.5 * flash)) return IVORY;
            if (d < 13 * (0.5 + 0.5 * flash)) return YEL2;
          }
          const wx = x + 1.8 * noise2(x * 0.15, y * 0.15 + f * 0.45, o.seed), wy = y + 1.8 * noise2(x * 0.15 + 9, y * 0.15 - f * 0.4, o.seed + 1);
          let F = 0; for (const b of items) F += ball(wx, wy, b);
          if (ringOn) {
            const d = Math.hypot(wx, wy), rr = 10 + 34 * ease.outCubic(clamp(rk)), rw = 2.6 * (1 - rk) + 0.7;
            if (Math.abs(d - rr) < rw && noise2(Math.atan2(wy, wx) * 2.4, 3, o.seed + 2) > rk * 2.2 - 1.1) return INK;
          }
          if (F > 0.45) return INK; if (F > 0.32) return INK2;
        });
        return;
      }
      if (K === "net-mark") {
        const Wd = o.width ?? 166, draw = clamp(t / 0.08), eat = smooth(0.25, 0.5, t);
        if (t > 0.55) return;
        p.shade(-Wd / 2, -14, Wd / 2, 2, (x, y) => {
          const q = (x + Wd / 2) / Wd; if (q > draw) return;
          const n = noise2(x * 0.35, f * 0.3, o.seed);
          if (eat > 0 && noise2(x * 0.25, y * 0.4 + f * 0.6, o.seed + 3) < eat * 2.2 - 1.1) return;
          // 바닥 먹 자국: 두께 2~3, 끝이 해진다
          const th = 1.6 + 1.0 * n;
          if (y <= 0 && y > -th) return INK;
          // 가시: 자국 위로 짧은 삼각 가시
          for (const s of spikes) {
            const sx = s.x * Wd - Wd / 2, h = s.h * smooth(0, 0.06, t - s.d) * (1 - eat);
            if (y < 0 && y > -h) { const half = 1.6 * (1 + y / h); const cx = sx + s.lean * -y; if (Math.abs(x - cx) <= half) return y > -2 ? INK : INK2; }
          }
        });
      }
    },
  };
}
