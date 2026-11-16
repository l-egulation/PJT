import { Rng, seg, ease, clamp, sparkle, hex, WHITE, noise2 } from "../../core/index.js";
import { SIZE, ORIGIN, BX, BY, BW, BH, SK, RED, K, barAt, barX, drawPlate, redRow } from "./_bar.js";

export const meta = {
  name: "HUD heal B: watercolor bleed",
  desc: "체력바 회복 B(수채 번짐): 기존 끝에서 물감이 빈 칸으로 스며 번지고, 번지는 앞쪽은 진한 테두리가 지며, 앞에 작은 물감 꽃이 먼저 피었다가 합쳐지고 마른다.",
  tags: ["hud", "heal", "bar", "ink", "one-shot"],
  size: SIZE, origin: ORIGIN, duration: 1.5, loop: false, fps: 30, bg: "dark",
  options: { from: 46, to: 72, seed: 7 },
};

const T0 = 0.15, SPREAD = 0.5, DRY = 0.55;
const GREEN = hex("#a6dcb1");

export function create(o) {
  const R = new Rng(o.seed), u0 = o.from / 100, u1 = o.to / 100, span = (u1 - u0) * BW;
  // pigment blooms that open a little ahead of the front and get swallowed by it
  const blooms = Array.from({ length: 6 }, (_, i) => ({ xs: u0 * BW + span * (0.25 + 0.14 * i) + R.range(-3, 3), v: R.range(1.5, BH - 1.5), r: R.range(1.6, 2.8) }));
  let t = 0;
  const base = (t) => span * ease.outQuad(seg(t, T0, T0 + SPREAD));
  // front of row v at time t: noisy, so the paint creeps forward unevenly
  const frontAt = (v, t) => {
    const b = base(t), k = 1 - seg(t, T0 + SPREAD - 0.12, T0 + SPREAD);
    return b + k * (noise2(v * 0.45, t * 6 + o.seed) - 0.5) * 9;
  };
  return {
    update(dt) { t += dt; },
    draw(p) {
      if (t >= meta.duration - 0.02) return;
      const prog = seg(t, T0, T0 + SPREAD);
      drawPlate(p, o.from + (o.to - o.from) * ease.outQuad(prog), { number: t > T0 && t < T0 + SPREAD + 0.3 ? GREEN : K.cream, portrait: t > T0 + SPREAD - 0.1 && t < T0 + SPREAD + 0.15 ? GREEN : K.cream });
      p.shade(BX, BY, BX + BW + SK + 1, BY + BH, (x, y) => {
        const a = barAt(x, y);
        if (!a) return;
        if (a.u < u0) return redRow(a.v);
        if (t < T0 || a.u >= u1) return;
        const d = a.xs - u0 * BW, fr = Math.min(span, frontAt(a.v, t));
        // blooms ahead of the front: small round wet spots
        if (d > fr) {
          for (const b of blooms) {
            const age = t - T0 - (b.xs - u0 * BW) / span * SPREAD * 0.6;
            if (age <= 0) continue;
            const r = b.r * ease.outCubic(clamp(age / 0.15));
            const dd = Math.hypot(a.xs - b.xs, (a.v - b.v) * 1.3);
            if (dd < r) return dd > r - 1 ? RED[2] : RED[4];
          }
          return;
        }
        const dry = seg(t - T0 - (d / span) * SPREAD, 0.1, DRY);
        // the wet front: a dark pooled rim, a pale wash behind it
        if (fr - d < 1.5 && dry < 0.6) return RED[2];
        if (dry < 0.5) return (noise2(a.xs * 0.35, a.v * 0.5) > 0.62 ? RED[3] : RED[4]);
        if (dry < 1) return a.v < 1 ? RED[4] : noise2(a.xs * 0.35, a.v * 0.5) > 0.7 ? RED[4] : RED[3];
        return redRow(a.v);
      });
      const g = t - T0 - SPREAD;
      if (g > 0 && g < 0.32) sparkle(p, barX(u1) + 1, BY + 2, Math.round(3 * (1 - g / 0.32)) + 1, WHITE, RED[5], 0.5);
      if (t > T0 && t < T0 + SPREAD) p.glow(barX(u0) + base(t), BY + BH / 2, 10, RED[4], 0.18);
      const ra = 1 - seg(g, 0.45, 0.8);
      if (t > T0 + 0.2 && ra > 0) p.text("+" + (o.to - o.from), 90, -22 - Math.round(6 * ease.outCubic(seg(t, T0 + 0.2, T0 + 0.8))), GREEN, { font: "big", outline: K.edge, a: ra });
    },
  };
}
