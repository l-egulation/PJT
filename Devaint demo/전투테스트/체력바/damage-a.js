import { Rng, seg, ease, clamp, hex, WHITE } from "../../core/index.js";
import { SIZE, ORIGIN, BX, BY, BW, BH, SK, RED, K, barAt, barX, drawPlate, redRow } from "./_bar.js";
import { FROM, TO, T_HIT, hitPlate, flashOn } from "./_hit.js";

export const meta = {
  name: "HUD damage A: eraser",
  desc: "체력바 피해 A(지우개): 깎인 칸이 하얗게 번쩍여 연해지고, 지우개가 오른쪽에서 왼쪽으로 문질러 지운다. 지운 자리엔 옅은 얼룩이 잠깐 남고 지우개 가루가 옆으로 튄다.",
  tags: ["hud", "damage", "bar", "ink", "one-shot"],
  size: SIZE, origin: ORIGIN, duration: 1.2, loop: false, fps: 30, bg: "dark",
  options: { seed: 3 },
};

const T_RUB = T_HIT + 0.1, RUB = 0.36;
const PINK = hex("#e9b7b0"), PINK_D = hex("#a77870"), SMUDGE = hex("#3a1c1a");

export function create(o) {
  const R = new Rng(o.seed), crumbs = [], a = (TO / 100) * BW, b = (FROM / 100) * BW;
  const rag = Array.from({ length: BH }, () => R.range(0, 3));
  let t = 0;
  // where the eraser is: it scrubs right to left, wiggling back and forth
  const rub = (t) => { const k = seg(t, T_RUB, T_RUB + RUB); return b - (b - a) * ease.inOutQuad(k) + Math.sin(k * Math.PI * 7) * 3 * (1 - k); };
  // the furthest left it has reached (pixels right of it are erased)
  const reached = (t) => b - (b - a) * ease.inOutQuad(seg(t, T_RUB, T_RUB + RUB));
  return {
    update(dt) {
      t += dt;
      if (t > T_RUB && t < T_RUB + RUB && R.chance(0.6)) {
        const x = barX(rub(t) / BW, BH), an = R.range(-2.9, -0.2);
        crumbs.push({ x, y: BY + BH, vx: Math.cos(an) * R.range(30, 80), vy: Math.sin(an) * R.range(20, 60), age: 0, life: R.range(0.2, 0.4), c: R.pick([PINK, PINK_D, RED[2]]) });
      }
      for (const c of crumbs) { c.age += dt; c.x += c.vx * dt; c.y += c.vy * dt; c.vx *= 1 - 4 * dt; c.vy *= 1 - 4 * dt; }
      for (let i = crumbs.length - 1; i >= 0; i--) if (crumbs[i].age > crumbs[i].life) crumbs.splice(i, 1);
    },
    draw(p) {
      if (t >= meta.duration - 0.02) return;
      const h = hitPlate(t), jx = h.jx, jy = h.jy;
      drawPlate(p, h.hp, h);
      const er = reached(t), fl = flashOn(t);
      p.shade(BX + jx, BY + jy, BX + BW + SK + 1 + jx, BY + BH + jy, (x, y, out) => {
        const q = barAt(x - jx, y - jy);
        if (!q || q.xs >= b) return;
        if (q.xs < a) return redRow(q.v);
        if (t < T_HIT) return redRow(q.v);
        if (fl) return WHITE;
        // still there: faded to a pale pink once hit
        if (q.xs < er - rag[Math.floor(q.v)] || t < T_RUB) return q.v < 1 ? RED[6] : RED[5];
        // erased: a faint smudge streak that fades
        const age = t - T_RUB - RUB * ((b - q.xs) / (b - a));
        const s = 1 - seg(age, 0.05, 0.5);
        if (s > 0 && (q.v + Math.floor(q.xs / 3)) % 3 !== 0) { out.a = s * 0.7; return SMUDGE; }
      });
      for (const c of crumbs) p.px(Math.round(c.x) + jx, Math.round(c.y) + jy, c.c);
    },
  };
}
