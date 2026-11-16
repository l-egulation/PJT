// 미사일 몸체 — 불꽃 혜성. 몽글한 불덩이 머리(흰 심)가 앞에 있고, 뒤로 불덩이가 짧게 끌린다. 원점 = 머리 가운데, 앞 = +x. 비행 중 반복.
import { Particles, P, Rng, fit, WHITE } from "../../core/index.js";
import { fireRamp, drawPs } from "./_lib.js";
import { PALETTE } from "./_bomb-pop.js";

export const meta = {
  name: "Bomb missile comet", desc: "A puffy fireball head with a white core and a short tail of fire balls streaming behind.",
  tags: ["missile", "fire", "comet", "loop"], size: [120, 60], origin: [92, 30], duration: 0.5, loop: true, fps: 30, bg: "light",
  options: { palette: PALETTE, seed: 3 },
};
export function create(o) {
  const F = fit(o.palette, 8), FR = fireRamp(F), R = new Rng(o.seed);
  const ps = new Particles({ rng: R, sparks: [F[6], F[5], F[3], F[2]] });
  let t = 0, acc = 0;
  // 미리 0.3초 돌려 꼬리를 채워 둔다
  const emit = () => {
    ps.spawn(P.FIRE, R.range(-3, 2), R.range(-4, 4), -R.range(140, 300), R.range(-25, 25), R.range(0.12, 0.24), 0, R.range(4, 8));
    if (R.chance(0.3)) ps.spawn(P.SPARK, -4, R.range(-3, 3), -R.range(150, 300), R.range(-80, 80), R.range(0.08, 0.18));
  };
  for (let i = 0; i < 18; i++) { for (let j = 0; j < 3; j++) emit(); ps.update(1 / 60); }
  return {
    update(dt) { t += dt; acc += dt * 160; while (acc >= 1) { emit(); acc--; } ps.update(dt); },
    drawBack(p) { drawPs(p, ps, "back", FR); },
    draw(p) {
      drawPs(p, ps, "front", FR);
      const w = 1 + 0.12 * Math.sin(t * 40);
      p.circle(1, 0, 9 * w, F[3]); p.circle(1.5, -0.5, 7.5 * w, F[5]); p.circle(2.5, -1, 5 * w, F[6]); p.circle(3, -1.5, 3, WHITE);
    },
  };
}
