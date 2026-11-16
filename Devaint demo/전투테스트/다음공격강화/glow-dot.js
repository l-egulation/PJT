import { clamp, mix, WHITE } from "../../core/index.js";

// 빛 방울: 흰 속 → 색 → 반투명 번짐
export function glowDot(p, x, y, r, glow, alpha, C) {
  const [, , SHADE, BODY, LIT] = C, R = r + glow;
  for (let yy = Math.floor(y - R); yy <= y + R; yy++)
    for (let xx = Math.floor(x - R); xx <= x + R; xx++) {
      const d = Math.hypot(xx + 0.5 - x, yy + 0.5 - y);
      if (d <= r) {
        const i = 1 - d / r;
        p.blend(xx, yy, i > 0.6 ? mix(LIT, WHITE, (i - 0.6) * 2.5) : mix(SHADE, BODY, Math.min(1, i * 2.2)), alpha * clamp(0.7 + i * 2));
      } else if (d <= R) {
        const g = 1 - (d - r) / glow;
        p.blend(xx, yy, mix(SHADE, BODY, g), alpha * 0.6 * g * g);
      }
    }
}
