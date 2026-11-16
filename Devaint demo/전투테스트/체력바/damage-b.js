import { Rng, seg, ease, clamp, hex, WHITE, noise2, Particles, P } from "../../core/index.js";
import { SIZE, ORIGIN, BX, BY, BW, BH, SK, RED, K, barAt, barX, drawPlate, redRow } from "./_bar.js";
import { FROM, TO, T_HIT, hitPlate, flashOn } from "./_hit.js";

export const meta = {
  name: "HUD damage B: evaporate",
  desc: "체력바 피해 B(휘발): 깎인 칸이 하얗게 번쩍인 뒤 잉크가 달아올라 옅어지며, 위쪽부터 구멍이 번져 증기처럼 피어올라 사라진다.",
  tags: ["hud", "damage", "bar", "ink", "one-shot"],
  size: SIZE, origin: ORIGIN, duration: 1.3, loop: false, fps: 30, bg: "dark",
  options: { seed: 6 },
};

const T_EVAP = T_HIT + 0.1, EVAP = 0.75;

export function create(o) {
  const R = new Rng(o.seed), a = (TO / 100) * BW, b = (FROM / 100) * BW;
  const ps = new Particles({ rng: R });
  let t = 0;
  // when each pixel goes: noise plus a lean so the top and the right go first
  const goneAt = (xs, v) => T_EVAP + EVAP * clamp(0.15 + 0.55 * noise2(xs * 0.25, v * 0.35 + 3) + 0.3 * (v / BH) - 0.25 * ((xs - a) / (b - a)));
  return {
    update(dt) {
      t += dt;
      if (t > T_EVAP && t < T_EVAP + EVAP) {
        for (let i = 0; i < 8; i++) {
          const xs = R.range(a, b), v = R.range(0, BH);
          if (Math.abs(goneAt(xs, v) - t) > 0.06) continue;
          const x = barX(xs / BW, v), y = BY + v;
          if (R.chance(0.55)) ps.spawn(P.VAPOUR, x, y, R.range(-6, 10), -R.range(18, 40), R.range(0.35, 0.6), R.pick([RED[3], RED[2], hex("#6a4a46")]), R.range(1.5, 3));
          else ps.spawn(P.MOTE, x, y, R.range(-8, 14), -R.range(30, 60), R.range(0.25, 0.45), R.pick([RED[4], RED[5]]));
        }
      }
      ps.update(dt);
    },
    draw(p) {
      if (t >= meta.duration - 0.02) return;
      const h = hitPlate(t), jx = h.jx, jy = h.jy;
      drawPlate(p, h.hp, h);
      const fl = flashOn(t);
      p.shade(BX + jx, BY + jy, BX + BW + SK + 1 + jx, BY + BH + jy, (x, y, out) => {
        const q = barAt(x - jx, y - jy);
        if (!q || q.xs >= b) return;
        if (q.xs < a || t < T_HIT) return redRow(q.v);
        if (fl) return WHITE;
        const g = goneAt(q.xs, q.v) - t;
        if (g < 0) return;
        // heats up and thins right before it goes
        if (g < 0.05) { out.k = 0.4; out.l = RED[5]; return RED[6]; }
        if (g < 0.14) return RED[5];
        return q.v < 1 ? RED[5] : RED[4];
      });
      ps.draw(p);
      if (t > T_HIT && t < T_HIT + 0.3) p.glow(barX((a + b) / 2 / BW) + jx, BY + 4 + jy, 24, RED[4], 0.35 * (1 - (t - T_HIT) / 0.3));
    },
  };
}
