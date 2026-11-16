import { Rng, fit, clamp } from "../../core/index.js";
import { crescent, needleGlow, burst, ring, sparks, star, FLURRY_PAL } from "./dagger-flurry-cuts.js";

// 단검 평타 맞음 시안(2026-11-07). 몸 그림에 휘두른 궤적이 이미 있어서, 이펙트는 맞은 적 자리만 짧게 보여 준다.
//   variant="a" 작은 초승달: 그림자 난무 e 를 줄인 꼴. 1타 올려·2타 내려·3타 가로로 초승달 하나 + 섬광 + 불티, 4타 찌르기는 바늘 관통 + 큰 섬광·고리.
//   variant="b" 먹 튐: 먹 방울이 튀고 노란 별 하나. 4타는 방울 두 배 + 별 크게.
// 원점 = 맞은 적 몸 가운데, 시각 0 = 타격 순간. options.hit = 1~4. 먹·노랑만, 검은 테두리 없음.
export const meta = {
  name: "Dagger basic hit (ink)",
  desc: "Dagger basic attack: a short hit mark on the struck enemy per combo step, bigger on the 4th thrust.",
  tags: ["ink", "dagger", "basic", "hit", "one-shot"],
  size: [128, 104],
  origin: [64, 52],
  duration: 0.34,
  loop: false,
  fps: 50,
  bg: "light",
  options: { variant: "a", hit: 1, seed: 3 },
};
// 타마다 초승달 시작 각·도는 방향·길이(몸 그림의 휘두름과 맞춤: 1 올려 베기, 2 내려찍기, 3 회전 가로 베기)
const CRES = { 1: [2.2, -1, 2.3], 2: [-2.2, 1, 2.3], 3: [Math.PI * 0.92, 1, 2.6] };

export function create(o) {
  const C = fit(FLURRY_PAL, 5), INK = C[0], YEL = C[2], R = new Rng(o.seed * 10 + o.hit), fin = o.hit === 4;
  const sp = Array.from({ length: fin ? 18 : 8 }, () => {
    const a = R.range(-1.2, 1.2) + (R.chance(0.3) ? Math.PI : 0);
    return { ca: Math.cos(a), sa: Math.sin(a), v: R.range(fin ? 140 : 90, fin ? 280 : 180), life: R.range(0.08, fin ? 0.26 : 0.16), d: R.range(0, 0.02) };
  });
  const drops = Array.from({ length: fin ? 14 : 7 }, () => {
    const a = R.range(-1.4, 1.1), v = R.range(60, fin ? 170 : 120);
    return { vx: Math.cos(a) * v, vy: Math.sin(a) * v - 25, s: R.range(1.6, fin ? 3.4 : 2.6), life: R.range(0.16, 0.28) };
  });
  let t = 0;
  return {
    update(dt) { t += dt; },
    draw(p) {
      const g = t + 1e-6;
      if (o.variant === "b") {
        for (const d of drops) {
          if (g > d.life) continue;
          const k = g / d.life, x = d.vx * g, y = d.vy * g + 260 * g * g, s = Math.max(1, Math.round(d.s * (1 - 0.6 * k)));
          for (let yy = 0; yy < s; yy++) for (let xx = 0; xx < s; xx++) p.px(x + xx - s / 2, y + yy - s / 2, INK);
          if (k < 0.5) p.px(x + s / 2, y - s / 2, YEL);
        }
        const sl = fin ? 0.12 : 0.08;
        if (g < sl) star(p, 0, 0, (fin ? 8 : 5) * (1 - g / sl) + 2, C);
        return;
      }
      const grow = clamp(g / 0.05), tail = clamp((g - 0.06) / 0.1);
      if (fin) {
        needleGlow(p, 0, 0, -0.08, 92, 4, grow, tail, C, 3);
        burst(p, 8, 0, g, 18, C); ring(p, 8, 0, g - 0.02, 30, 0.2, C);
      } else {
        const [a0, dir, span] = CRES[o.hit];
        crescent(p, 0, 0, 18, a0, span, dir, 4.5, grow, tail, C, 2.5);
        burst(p, 0, 0, g, 10, C);
      }
      sparks(p, 0, 0, g, sp, C);
    },
  };
}
