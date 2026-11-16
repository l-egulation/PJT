import { Rng, seg, ease, clamp, sparkle, hex, WHITE } from "../../core/index.js";
import { SIZE, ORIGIN, BX, BY, BW, BH, SK, RED, K, barAt, barX, drawPlate, redRow } from "./_bar.js";

export const meta = {
  name: "HUD heal C: pencil hatching",
  desc: "체력바 회복 C(색연필 해칭): 펜촉이 지그재그로 빗금을 그어 빈 칸을 채우고, 두 번째 빗금이 틈을 메운 뒤 한 번 번쩍여 꽉 찬 색이 된다.",
  tags: ["hud", "heal", "bar", "ink", "one-shot"],
  size: SIZE, origin: ORIGIN, duration: 1.3, loop: false, fps: 30, bg: "dark",
  options: { from: 46, to: 72, seed: 2 },
};

const T0 = 0.15, PASS = 0.22, GAP = 0.04, SOLID = T0 + PASS * 2 + GAP + 0.03;
const GREEN = hex("#a6dcb1");

export function create(o) {
  const R = new Rng(o.seed), u0 = o.from / 100, u1 = o.to / 100, x0 = u0 * BW, span = (u1 - u0) * BW;
  const jit = Array.from({ length: 64 }, () => R.int(-1, 1));
  const crumbs = [];
  let t = 0, lastPen = -1;
  // pass k (0, 1) has reached bar position xs by time t
  const passAt = (k, t) => span * seg(t, T0 + k * (PASS + GAP), T0 + k * (PASS + GAP) + PASS);
  // a pixel is on a hatch line of pass k when its slanted coordinate falls on the stripe
  // pass 0 leans one way, pass 1 crosses it: cross-hatching
  const onHatch = (k, xs, v) => { const s = Math.floor(k ? xs - v * 0.8 + 2 : xs + v * 0.8) + (jit[Math.floor(xs / 5) & 63] === 1 && v > 6 ? 1 : 0); return ((s % 5) + 5) % 5 === 0; };
  const pen = (t) => { const k = t < T0 + PASS + GAP ? 0 : 1; const d = passAt(k, t); const ph = ((d / 3) % 1) * 2; return { k, xs: x0 + d, v: ph < 1 ? ph * BH : (2 - ph) * BH }; };
  return {
    update(dt) {
      t += dt;
      if (t > T0 && t < SOLID - 0.03) {
        const q = pen(t);
        if (R.chance(0.35)) crumbs.push({ x: barX(q.xs / BW, q.v), y: BY + q.v, vx: R.range(-30, 40), vy: R.range(-60, -20), age: 0, life: R.range(0.15, 0.3) });
        lastPen = q.xs;
      }
      for (const c of crumbs) { c.age += dt; c.x += c.vx * dt; c.y += c.vy * dt; c.vy += 300 * dt; }
      for (let i = crumbs.length - 1; i >= 0; i--) if (crumbs[i].age > crumbs[i].life) crumbs.splice(i, 1);
    },
    draw(p) {
      if (t >= meta.duration - 0.02) return;
      const prog = seg(t, T0, SOLID);
      drawPlate(p, o.from + (o.to - o.from) * prog, { number: t > T0 && t < SOLID + 0.3 ? GREEN : K.cream, portrait: t > SOLID - 0.05 && t < SOLID + 0.15 ? GREEN : K.cream });
      const flash = t >= SOLID && t < SOLID + 0.07;
      p.shade(BX, BY, BX + BW + SK + 1, BY + BH, (x, y) => {
        const a = barAt(x, y);
        if (!a) return;
        if (a.u < u0) return redRow(a.v);
        if (t < T0 || a.u >= u1) return;
        if (t >= SOLID) return flash ? RED[5] : redRow(a.v);
        const d = a.xs - x0;
        if (d < passAt(0, t) && onHatch(0, a.xs, a.v)) return a.v < 1 ? RED[4] : RED[3];
        if (d < passAt(1, t) && onHatch(1, a.xs, a.v)) return RED[4];
      });
      // the pen tip: a bright nib on the current stroke, its shaft leaning up and back
      if (t > T0 && t < SOLID) {
        const q = pen(t), nx = Math.round(barX(q.xs / BW, q.v)), ny = Math.round(BY + q.v);
        p.px(nx, ny, WHITE); p.px(nx - 1, ny - 1, RED[5]); p.px(nx - 2, ny - 2, RED[4]);
        for (let i = 3; i < 13; i++) p.px(nx - i + 1, ny - i, i < 5 ? K.creamDim : K.cream);
        p.glow(nx, ny, 5, RED[5], 0.35);
      }
      for (const c of crumbs) p.px(Math.round(c.x), Math.round(c.y), c.age / c.life < 0.5 ? RED[4] : RED[2]);
      const g = t - SOLID;
      if (g > 0 && g < 0.3) sparkle(p, barX(u1) + 1, BY + 1, Math.round(4 * (1 - g / 0.3)) + 1, WHITE, RED[5], 0.6);
      const ra = 1 - seg(g, 0.4, 0.75);
      if (t > T0 + 0.2 && ra > 0) p.text("+" + (o.to - o.from), 90, -22 - Math.round(6 * ease.outCubic(seg(t, T0 + 0.2, T0 + 0.8))), GREEN, { font: "big", outline: K.edge, a: ra });
    },
  };
}
