import { Rng, seg, ease, clamp, sparkle, hex, WHITE } from "../../core/index.js";
import { SIZE, ORIGIN, BX, BY, BW, BH, SK, RED, K, barAt, barX, drawPlate, redRow } from "./_bar.js";

export const meta = {
  name: "HUD heal A: brush stroke",
  desc: "체력바 회복 A(붓질): 붓끝이 비어 있는 칸을 한 번에 칠하며 지나가고, 젖은 결이 남았다가 마르며, 붓끝에서 잉크가 앞으로 튄다.",
  tags: ["hud", "heal", "bar", "ink", "one-shot"],
  size: SIZE, origin: ORIGIN, duration: 1.4, loop: false, fps: 30, bg: "dark",
  options: { from: 46, to: 72, seed: 4 },
};

const T0 = 0.18, STROKE = 0.26, DRY = 0.55;
const GREEN = hex("#a6dcb1"), GREEN_HI = hex("#e3ffe6");

export function create(o) {
  const R = new Rng(o.seed), u0 = o.from / 100, u1 = o.to / 100;
  // bristles: each row trails the brush front by its own amount, so the stroke's front is ragged
  const lag = Array.from({ length: BH }, () => R.range(0, 7));
  const streak = Array.from({ length: BH }, (_, v) => v > 0 && v < BH - 1 && R.chance(0.35));
  const flecks = [];
  let t = 0, lastFront = u0;
  const front = (t) => u0 + (u1 - u0) * ease.outCubic(seg(t, T0, T0 + STROKE));
  // when the brush front passed bar position u (for drying)
  const paintedAt = (u) => T0 + STROKE * invOutCubic((u - u0) / (u1 - u0));
  return {
    update(dt) {
      t += dt;
      const f = front(t);
      if (t > T0 && t < T0 + STROKE + 0.02 && f > lastFront) {
        // ink flicks forward and outward off the brush tip
        const n = R.int(0, 2);
        for (let i = 0; i < n; i++) {
          const a = R.range(-1.1, 1.1), sp = R.range(60, 150);
          flecks.push({ x: barX(f) + 2, y: BY + R.range(1, BH - 1), vx: Math.cos(a) * sp, vy: Math.sin(a) * sp * 0.7, age: 0, life: R.range(0.14, 0.3), s: R.chance(0.3) ? 2 : 1 });
        }
      }
      lastFront = f;
      for (const k of flecks) { k.age += dt; k.x += k.vx * dt; k.y += k.vy * dt; k.vx *= 1 - 6 * dt; k.vy *= 1 - 6 * dt; }
      for (let i = flecks.length - 1; i >= 0; i--) if (flecks[i].age > flecks[i].life) flecks.splice(i, 1);
    },
    draw(p) {
      if (t >= meta.duration - 0.02) return;
      const f = front(t), done = t >= T0 + STROKE;
      const hpNow = 100 * (t < T0 ? u0 : f);
      const greenNum = t > T0 && t < T0 + STROKE + 0.35;
      drawPlate(p, hpNow, { number: greenNum ? (t < T0 + STROKE + 0.08 ? GREEN_HI : GREEN) : K.cream, portrait: t > T0 + STROKE * 0.7 && t < T0 + STROKE + 0.12 ? GREEN : K.cream });
      // old health + the fresh stroke
      p.shade(BX, BY, BX + BW + SK + 1, BY + BH, (x, y) => {
        const a = barAt(x, y);
        if (!a) return;
        if (a.u < u0) return redRow(a.v);
        if (t < T0) return;
        const edge = (f - a.u) * BW - lag[Math.floor(a.v)] * (done ? 0 : 1);
        if (edge < 0 || a.u >= u1) return;
        const age = t - paintedAt(a.u);
        if (age < 0.05) return RED[5]; // the wettest pixels right behind the brush
        const wet = 1 - seg(age, 0.05, DRY);
        if (wet > 0.66) return streak[Math.floor(a.v)] ? RED[5] : RED[4];
        if (wet > 0.2) return streak[Math.floor(a.v)] ? RED[4] : a.v < 1 ? RED[5] : RED[3];
        return redRow(a.v);
      });
      // a glossy wet sheen slides along the new paint once, then it's dry
      const sh = seg(t, T0 + STROKE + 0.05, T0 + STROKE + 0.45);
      if (sh > 0 && sh < 1) {
        const sx = barX(u0) + (u1 - u0) * BW * ease.inOutQuad(sh);
        for (let v = 0; v < BH; v++) { const x = Math.round(sx + (BH - v) * 0.6); p.px(x, BY + v, RED[6]); p.px(x - 1, BY + v, RED[5]); }
        p.glow(sx, BY + BH / 2, 8, RED[5], 0.25);
      }
      for (const k of flecks) { const a = 1 - k.age / k.life; p.rect(Math.round(k.x), Math.round(k.y), k.s, k.s, a > 0.5 ? RED[4] : RED[3], clamp(a * 1.6)); }
      // the new end glints and a +N rises by the numbers
      if (done) {
        const g = t - T0 - STROKE;
        if (g < 0.3) sparkle(p, barX(u1) + 1, BY + 1, Math.round(4 * (1 - g / 0.3)) + 1, WHITE, RED[5], 0.6);
        const ra = 1 - seg(g, 0.5, 0.85);
        if (ra > 0) p.text("+" + (o.to - o.from), 90, -22 - Math.round(6 * ease.outCubic(seg(g, 0, 0.6))), GREEN, { font: "big", outline: K.edge, a: ra });
      }
    },
  };
}

function invOutCubic(y) { y = clamp(y); return 1 - Math.cbrt(1 - y); }
