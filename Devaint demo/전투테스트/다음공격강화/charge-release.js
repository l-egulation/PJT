import { fit, clamp, smooth, ease, rand, TAU } from "../../core/index.js";
import { glowDot } from "./glow-dot.js";
import { glowLine } from "./speed-trail.js";

// 다음 공격 강화(빨강) — 쓰는 순간(공격 시작). 몸 둘레 기운이 가는 줄기가 되어 손(무기)으로 빨려 들고, 손에서 한 번 번쩍인다.
// 원점은 플레이어 발 가운데, hand 는 손 자리(오른쪽을 볼 때).
export const meta = {
  name: "Charge release (ink)",
  desc: "The stored aura streams into the hand as thin streaks and flashes once at the weapon as the attack starts.",
  tags: ["ink", "buff", "empower", "one-shot"],
  size: [80, 112],
  origin: [40, 100],
  duration: 0.28,
  loop: false,
  fps: 25,
  bg: "light",
  options: { palette: ["#171716", "#5a1d16", "#9e3424", "#d8664c", "#f09a7c", "#fff3e6"], hand: [6, -17], seed: 4 },
};

export function draw(p, t, o) {
  if (t >= meta.duration - 1e-4) return;
  const C = fit(o.palette, 6), [hx, hy] = o.hand, f = Math.floor(t * 25 + 1e-6);
  const k = ease.inQuad(clamp(t / 0.12));
  for (let i = 0; i < 8; i++) {
    const a = (i / 8) * TAU + rand(o.seed, i, 1), sx = Math.cos(a) * 20, sy = -42 + Math.sin(a) * 36;
    const x1 = sx + (hx - sx) * k, y1 = sy + (hy - sy) * k, x0 = sx + (hx - sx) * Math.max(0, k - 0.35), y0 = sy + (hy - sy) * Math.max(0, k - 0.35);
    glowLine(p, x0, y0, x1, y1, 0.8, 2.5, 1 - smooth(0.1, 0.14, t), C, o.seed + i, f, true);
  }
  const fl = smooth(0.08, 0.12, t) * (1 - smooth(0.14, 0.28, t));
  if (fl > 0.01) glowDot(p, hx, hy, 2 + 4 * smooth(0.08, 0.2, t), 5, fl, C);
}
