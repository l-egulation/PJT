import { Rng, seg, clamp, hex, WHITE, noise2, mix } from "../../core/index.js";
import { SIZE, ORIGIN, BX, BY, BW, BH, SK, RED, K, barAt, barX, drawPlate, redRow } from "./_bar.js";
import { FROM, TO, T_HIT, hitPlate, flashOn } from "./_hit.js";

export const meta = {
  name: "HUD damage C: blown away",
  desc: "체력바 피해 C(흩날림): 깎인 칸이 하얗게 번쩍인 뒤 오른쪽 끝부터 한 화소씩 떨어져 나가 바람에 날리는 잉크 가루가 되고, 날리며 먹색으로 바래 사라진다.",
  tags: ["hud", "damage", "bar", "ink", "one-shot"],
  size: SIZE, origin: ORIGIN, duration: 1.4, loop: false, fps: 30, bg: "dark",
  options: { seed: 8 },
};

const T_BLOW = T_HIT + 0.1, BLOW = 0.38;
const ASH = hex("#3b3633");

export function create(o) {
  const R = new Rng(o.seed), a = (TO / 100) * BW, b = (FROM / 100) * BW;
  // every lost pixel becomes a speck; it lets go at its own time, right end first
  const specks = [];
  for (let xs = Math.floor(a); xs < b; xs++) for (let v = 0; v < BH; v++) {
    specks.push({ xs, v, go: T_BLOW + BLOW * clamp((b - xs) / (b - a) * 0.8 + R.range(0, 0.2)), vx: R.range(40, 110), vy: -R.range(10, 45), life: R.range(0.35, 0.7), ph: R.range(0, 6), x: 0, y: 0, on: false });
  }
  let t = 0;
  return {
    update(dt) {
      t += dt;
      for (const s of specks) {
        if (!s.on && t >= s.go) { s.on = true; s.x = barX(s.xs / BW, s.v); s.y = BY + s.v; }
        if (!s.on) continue;
        const age = t - s.go;
        s.x += s.vx * dt; s.y += (s.vy + Math.sin(age * 14 + s.ph) * 18) * dt; s.vx *= 1 - 1.5 * dt; s.vy -= 20 * dt;
      }
    },
    draw(p) {
      if (t >= meta.duration - 0.02) return;
      const h = hitPlate(t), jx = h.jx, jy = h.jy;
      drawPlate(p, h.hp, h);
      const fl = flashOn(t);
      p.shade(BX + jx, BY + jy, BX + BW + SK + 1 + jx, BY + BH + jy, (x, y) => {
        const q = barAt(x - jx, y - jy);
        if (!q || q.xs >= b) return;
        if (q.xs < a || t < T_HIT) return redRow(q.v);
        if (fl) return WHITE;
        const s = specks[(Math.floor(q.xs) - Math.floor(a)) * BH + Math.floor(q.v)];
        if (s && t >= s.go) return;
        // the edge about to blow off goes pale first
        if (s && s.go - t < 0.08) return RED[5];
        return q.v < 1 ? RED[5] : RED[4];
      });
      for (const s of specks) {
        if (!s.on) continue;
        const k = (t - s.go) / s.life;
        if (k >= 1) continue;
        const c = k < 0.25 ? RED[4] : k < 0.55 ? RED[2] : ASH;
        p.dot(s.x + jx, s.y + jy, c, clamp((1 - k) * 1.6));
      }
    },
  };
}
