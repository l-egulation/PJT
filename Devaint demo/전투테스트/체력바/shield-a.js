import { Rng, seg, ease, clamp, sparkle, WHITE } from "../../core/index.js";
import { SIZE, ORIGIN, BX, BY, BW, BH, SK, MINT, K, barAt, barX, drawPlate, drawFill } from "./_bar.js";
import { HP, SHIELD, HIT1, T_DRAW, DRAW, T_HIT, T_BREAK, DUR, span, shieldAt } from "./_shield.js";

export const meta = {
  name: "HUD shield A: pen outline and hatching",
  desc: "보호막 A(펜 윤곽+빗금): 민트 펜이 체력 끝 뒤에 보호막 칸 윤곽을 한 바퀴 그리고 빗금으로 채운다. 막으면 깎인 칸이 펜 선째 끊겨 날아가고, 깨지면 윤곽선이 조각나 흩어진다.",
  tags: ["hud", "shield", "bar", "ink", "one-shot"],
  size: SIZE, origin: ORIGIN, duration: DUR, loop: false, fps: 30, bg: "dark",
  options: { seed: 5 },
};

const OUTLINE = 0.2; // the first part of the draw traces the outline, the rest hatches

export function create(o) {
  const R = new Rng(o.seed), bits = [];
  let t = 0, hit = false, broke = false;
  // the outline as a closed path of bar-space points (xs, v), traced from the hp edge's top clockwise
  const path = (a, b) => [[a, -1], [b, -1], [b, BH], [a, BH], [a, -1]];
  const toXY = ([xs, v]) => [BX + SK * (1 - v / BH) + xs, BY + v];
  const shatter = (a, b, n, dense) => {
    for (let i = 0; i < n; i++) {
      const xs = R.range(a, b), v = R.chance(0.5) ? -1 : BH, [x, y] = toXY([xs, v]), an = R.range(-Math.PI, 0) + (v > 0 ? Math.PI : 0) * 0.3;
      const sp = R.range(40, dense ? 140 : 100);
      bits.push({ x, y, vx: Math.cos(an) * sp + R.range(10, 40), vy: Math.sin(an) * sp, age: 0, life: R.range(0.3, 0.6), len: R.int(2, 4), a: R.range(0, 3), va: R.range(-14, 14), c: R.pick([MINT[5], MINT[4], MINT[3]]) });
    }
  };
  return {
    update(dt) {
      t += dt;
      if (!hit && t >= T_HIT) { hit = true; const [, b] = span(T_HIT - 0.01); shatter(b - (HIT1 / 100) * BW, b, 10); }
      if (!broke && t >= T_BREAK) { broke = true; const [a, b] = span(T_BREAK - 0.01); shatter(a, b, 22, true); }
      for (const k of bits) { k.age += dt; k.x += k.vx * dt; k.y += k.vy * dt; k.vy += 380 * dt; k.vx *= 1 - 2 * dt; k.a += k.va * dt; }
      for (let i = bits.length - 1; i >= 0; i--) if (bits[i].age > bits[i].life) bits.splice(i, 1);
    },
    draw(p) {
      if (t >= DUR - 0.02) return;
      const tk = t - T_HIT, shake = tk > 0 && tk < 0.16 ? Math.round(Math.cos(tk * 90) * 2 * (1 - tk / 0.16)) : 0;
      drawPlate(p, HP, { number: K.cream });
      drawFill(p, HP);
      const [a, b] = span(t), s = shieldAt(t);
      const dp = seg(t, T_DRAW, T_DRAW + DRAW), op = clamp(dp / OUTLINE), hp = seg(dp, OUTLINE, 1);
      if (s > 0 && t > T_DRAW) {
        // hatching (diagonal mint strokes, laid left to right) inside the outline
        const hatchTo = a + (b - a) * ease.outQuad(hp);
        const blink = T_BREAK - t < 0.35 && t < T_BREAK ? (Math.floor(t * 20) % 2 ? 0.45 : 1) : 1;
        p.shade(BX + shake, BY, BX + BW + SK + 1 + shake, BY + BH, (x, y, out) => {
          const q = barAt(x - shake, y);
          if (!q || q.xs < a || q.xs >= b) return;
          const sh = Math.floor(q.xs + q.v * 0.8 - t * 6);
          out.a = blink;
          if (q.xs < hatchTo && ((sh % 4) + 4) % 4 === 0) return MINT[4];
          if (q.xs < hatchTo) return q.v < 1 ? MINT[2] : MINT[1];
        });
        // the outline, traced by the pen
        const pts = path(a, b).map(toXY).map(([x, y]) => [x + shake, y]);
        const lens = []; let tot = 0;
        for (let i = 1; i < pts.length; i++) { const l = Math.hypot(pts[i][0] - pts[i - 1][0], pts[i][1] - pts[i - 1][1]); lens.push(l); tot += l; }
        let left = tot * ease.inOutQuad(op), tip = pts[0];
        for (let i = 1; i < pts.length && left > 0; i++) {
          const k = Math.min(1, left / lens[i - 1]), [x0, y0] = pts[i - 1], x1 = x0 + (pts[i][0] - x0) * k, y1 = y0 + (pts[i][1] - y0) * k;
          const oy = y0 === y1 ? (y0 < BY ? -1 : 1) : 0, ox = y0 === y1 ? 0 : x0 > barX(a / BW, 4) + 1 ? 1 : -1;
          p.line(x0 + ox, y0 + oy, x1 + ox, y1 + oy, MINT[3], blink);
          p.line(x0, y0, x1, y1, MINT[5], blink);
          tip = [x1, y1]; left -= lens[i - 1];
        }
        if (op < 1) { p.px(tip[0], tip[1], WHITE); p.glow(tip[0], tip[1], 5, MINT[5], 0.5); for (let i = 1; i < 9; i++) p.px(tip[0] - i * 0.6, tip[1] - i, i < 3 ? MINT[3] : K.cream); }
        else if (hp < 1) { const hx = barX(hatchTo / BW, 4); p.glow(hx, BY + 4, 6, MINT[5], 0.35); }
        p.glow(barX((a + b) / 2 / BW), BY + BH / 2, (b - a) * 0.7, MINT[3], 0.12);
        if (dp >= 1 && t - T_DRAW - DRAW < 0.25) sparkle(p, barX(b / BW, 0), BY - 1, Math.round(4 * (1 - (t - T_DRAW - DRAW) / 0.25)) + 1, WHITE, MINT[5], 0.6);
      }
      // the shield amount, small and mint, beside the numbers while it lasts
      if (s > 0 && dp > 0.4) p.text("+" + s, 96 + shake, -18, MINT[4], { font: "mini", outline: K.edge });
      // broken pen strokes: short mint dashes that tumble
      for (const k of bits) {
        const al = 1 - k.age / k.life, dx = Math.cos(k.a), dy = Math.sin(k.a);
        for (let i = 0; i < k.len; i++) p.dot(k.x + dx * i, k.y + dy * i, k.c, clamp(al * 1.5));
      }
      if (t >= T_BREAK && t < T_BREAK + 0.06) { const [a0, b0] = span(T_BREAK - 0.01); p.glow(barX((a0 + b0) / 2 / BW), BY + 4, 18, MINT[5], 0.6); }
    },
  };
}
