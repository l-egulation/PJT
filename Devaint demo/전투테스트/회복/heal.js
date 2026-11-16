import { fit, clamp, smooth, ease, mix, WHITE } from "../../core/index.js";
import { glowLine } from "./speed-trail.js";

// 회복(초록) — 발밑에서 빛 고리가 생겨 몸을 훑고 가슴 높이까지 올라가며 좁아지고, 고리를 따라 줄기 몇 개가 솟는다.
// 원점은 플레이어 발 가운데.
export const meta = {
  name: "Heal (rising ring)",
  desc: "A glowing ring forms at the feet, sweeps up the body narrowing to chest height, with a few streaks lifting off it.",
  tags: ["ink", "heal", "buff", "one-shot"],
  size: [72, 112],
  origin: [36, 100],
  duration: 0.64,
  loop: false,
  fps: 25,
  bg: "light",
  options: { palette: ["#171716", "#1d4128", "#2f7444", "#5fb37a", "#a6dcb1", "#f4fff4"], seed: 2 },
};

export function draw(p, t, o) {
  if (t >= meta.duration - 1e-4) return;
  const C = fit(o.palette, 6), [, , SHADE, BODY, LIT] = C, f = Math.floor(t * 25 + 1e-6);
  const u = clamp(t / 0.48), k = ease.outCubic(u);
  const cy = -2 - 40 * k, rx = 22 - 8 * k, ry = 5 - 2 * k, w = 2, glow = 4, a = smooth(0, 0.1, u) * (1 - smooth(0.65, 1, u));
  if (a > 0.01)
    for (let y = Math.floor(cy - ry - w - glow - 1); y <= cy + ry + w + glow + 1; y++)
      for (let x = Math.floor(-rx - w - glow - 1); x <= rx + w + glow + 1; x++) {
        const dx = (x + 0.5) / rx, dy = (y + 0.5 - cy) / ry, e = Math.hypot(dx, dy);
        const d = Math.abs(e - 1) * Math.min(rx, ry * 3);
        // 몸 뒤로 가는 위쪽 반은 흐리게
        const back = y + 0.5 < cy ? 0.45 : 1;
        if (d <= w) {
          const i = 1 - d / w;
          p.blend(x, y, i > 0.65 ? mix(LIT, WHITE, (i - 0.65) * 2.2) : mix(SHADE, BODY, Math.min(1, i * 2.2)), a * back * clamp(0.7 + i * 2));
        } else if (d <= w + glow) {
          const g = 1 - (d - w) / glow;
          p.blend(x, y, mix(SHADE, BODY, g), a * back * 0.5 * g * g);
        }
      }
  // 고리를 따라 솟는 줄기
  for (const [i, s] of [{ x: -14, d: 0.06 }, { x: 9, d: 0.12 }, { x: -4, d: 0.2 }, { x: 15, d: 0.26 }].entries()) {
    const v = (t - s.d) / 0.36;
    if (v <= 0 || v >= 1) continue;
    const head = -10 - (16 * i) % 20 - 44 * ease.outCubic(v), len = 16;
    glowLine(p, s.x, head + len, s.x, head, 0.9, 3, smooth(0, 0.15, v) * (1 - smooth(0.6, 1, v)), C, o.seed + i, f, true);
  }
}
