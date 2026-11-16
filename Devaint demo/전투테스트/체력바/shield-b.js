import { Rng, seg, ease, clamp, sparkle, WHITE } from "../../core/index.js";
import { SIZE, ORIGIN, BX, BY, BW, BH, SK, MINT, K, barAt, barX, drawPlate, drawFill, mintRow } from "./_bar.js";
import { HP, HIT1, T_DRAW, DRAW, T_HIT, T_BREAK, DUR, span, shieldAt } from "./_shield.js";

export const meta = {
  name: "HUD shield B: frame trace and brush",
  desc: "보호막 B(틀 덧그리기+붓칠): 민트 펜이 게이지 틀을 한 바퀴 덧그리는 동시에 붓이 체력 끝 뒤 칸을 민트로 칠한다. 막으면 칸 끝이 번쩍이며 깎이고, 깨지면 덧그린 틀이 거꾸로 풀리며 민트 조각이 흩어진다.",
  tags: ["hud", "shield", "bar", "ink", "one-shot"],
  size: SIZE, origin: ORIGIN, duration: DUR, loop: false, fps: 30, bg: "dark",
  options: { seed: 9 },
};

export function create(o) {
  const R = new Rng(o.seed), flakes = [];
  const lag = Array.from({ length: BH }, () => R.range(0, 5));
  // the track frame as a loop of pixels, starting at the hp edge's top and running right
  const ring = [];
  {
    const x0 = BX - 1, x1 = BX + BW + SK + 2;
    for (let x = x0; x <= x1; x++) for (let y = BY - 1; y <= BY + BH; y++) {
      if (barAt(x, y)) continue;
      if (barAt(x - 1, y) || barAt(x + 1, y) || barAt(x, y - 1) || barAt(x, y + 1)) ring.push([x, y]);
    }
    const cx = barX(0.5), cy = BY + BH / 2, start = Math.atan2(BY - 1 - cy, barX(HP / 100, 0) - cx);
    // order by the angle round an ellipse that fits the bar, starting at the hp edge
    ring.forEach((q) => (q.k = (((Math.atan2((q[1] - cy) * 20, q[0] - cx) - Math.atan2((BY - 1 - cy) * 20, barX(HP / 100, 0) - cx)) % (Math.PI * 2)) + Math.PI * 2) % (Math.PI * 2)));
    ring.sort((a, b) => a.k - b.k);
  }
  let t = 0, broke = false, hit = false;
  return {
    update(dt) {
      t += dt;
      const burst = (a, b, n) => { for (let i = 0; i < n; i++) { const xs = R.range(a, b), v = R.range(0, BH), an = R.range(-2.6, -0.4), sp = R.range(30, 110); flakes.push({ x: barX(xs / BW, v), y: BY + v, vx: Math.cos(an) * sp, vy: Math.sin(an) * sp, age: 0, life: R.range(0.3, 0.7), c: R.pick([MINT[5], MINT[4], MINT[3]]), s: R.chance(0.3) ? 2 : 1 }); } };
      if (!hit && t >= T_HIT) { hit = true; const [, b] = span(T_HIT - 0.01); burst(b - (HIT1 / 100) * BW, b, 10); }
      if (!broke && t >= T_BREAK) { broke = true; const [a, b] = span(T_BREAK - 0.01); burst(a, b, 26); }
      for (const f of flakes) { f.age += dt; f.x += f.vx * dt; f.y += f.vy * dt; f.vx *= 1 - 3 * dt; f.vy = f.vy * (1 - 3 * dt) - 20 * dt; }
      for (let i = flakes.length - 1; i >= 0; i--) if (flakes[i].age > flakes[i].life) flakes.splice(i, 1);
    },
    draw(p) {
      if (t >= DUR - 0.02) return;
      const dp = seg(t, T_DRAW, T_DRAW + DRAW), unravel = seg(t, T_BREAK, T_BREAK + 0.3);
      const blink = T_BREAK - t < 0.35 && t < T_BREAK ? (Math.floor(t * 20) % 2 ? 0.45 : 1) : 1;
      drawPlate(p, HP);
      // the traced frame: drawn round from the hp edge, unravelled backwards when it breaks
      const n = Math.round(ring.length * ease.inOutQuad(dp) * (1 - ease.inQuad(unravel)));
      for (let i = 0; i < n; i++) p.dot(ring[i][0], ring[i][1], MINT[4], blink);
      if (n > 0 && n < ring.length) { const [x, y] = ring[n - 1]; p.px(x, y, WHITE); p.glow(x, y, 5, MINT[5], 0.5); }
      drawFill(p, HP);
      const [a, b] = span(t), s = shieldAt(t);
      const tk = t - T_HIT;
      if (s > 0 && t > T_DRAW) {
        const fr = a + (b - a) * ease.outCubic(dp);
        p.shade(BX, BY, BX + BW + SK + 1, BY + BH, (x, y, out) => {
          const q = barAt(x, y);
          if (!q || q.xs < a || q.xs >= b || q.xs > fr - (dp < 1 ? lag[Math.floor(q.v)] : 0)) return;
          out.a = blink;
          if (tk > 0 && tk < 0.08 && q.xs > b - 3) return WHITE;
          if (dp < 1 && fr - q.xs < 4) return MINT[6];
          // a sheen that drifts along the paint while it holds
          const sh = (((q.xs - a) + q.v * 0.6 - t * 30) % 40 + 40) % 40;
          if (sh < 1.5) return MINT[6];
          return mintRow(q.v);
        });
        p.glow(barX((a + b) / 2 / BW), BY + BH / 2, (b - a) * 0.8, MINT[4], 0.15);
        if (dp >= 1 && t - T_DRAW - DRAW < 0.25) sparkle(p, barX(b / BW, 0), BY, Math.round(4 * (1 - (t - T_DRAW - DRAW) / 0.25)) + 1, WHITE, MINT[5], 0.6);
      }
      if (s > 0 && dp > 0.4) p.text("+" + s, 96, -18, MINT[4], { font: "mini", outline: K.edge });
      for (const f of flakes) p.rect(Math.round(f.x), Math.round(f.y), f.s, f.s, f.c, clamp((1 - f.age / f.life) * 1.5));
      if (t >= T_BREAK && t < T_BREAK + 0.06) { const [a0, b0] = span(T_BREAK - 0.01); p.glow(barX((a0 + b0) / 2 / BW), BY + 4, 18, MINT[5], 0.6); }
    },
  };
}
