// 미사일 변신 발사 — 주인공이 미사일로 바뀌며 뒤로 뿜는 펑. 흰 고리 섬광, 뒤쪽으로 쏠린 불꽃·연기 덩이, 뒤로 튀는 불티.
// 원점 = 발사 순간 몸통 가운데(땅에서 1유닛). 앞 = +x. 연기는 그 자리에 남아 흩어진다(월드 고정).
import { Particles, Lights, P, PF, Rng, fit, smooth, ease, TAU, WHITE, hex, mix } from "../../core/index.js";
import { fireRamp, drawPs, cull } from "./_lib.js";
import { PALETTE } from "./_bomb-pop.js";

export const meta = {
  name: "Bomb missile launch", desc: "A backblast as the hero turns into a missile: a white pop ring, fire and smoke shoved backwards, sparks flung behind.",
  tags: ["explosion", "missile", "launch", "one-shot"], size: [200, 120], origin: [130, 70], duration: 0.9, loop: false, fps: 30, bg: "light",
  options: { palette: PALETTE, seed: 4 },
};
const SMK = ["#8c8289", "#6c636d"].map(hex);
export function create(o) {
  const F = fit(o.palette, 8), FR = fireRamp(F), R = new Rng(o.seed);
  const ps = new Particles({ rng: R, ground: () => 32, sparks: [F[6], F[5], F[3], F[2]] });
  const lights = new Lights();
  let t = 0, done = false;
  const back = () => Math.PI + R.range(-0.55, 0.55);
  function go() {
    done = true;
    lights.add(-6, 0, 50, mix(F[3], F[6], 0.5), 0.4, 0.25);
    for (let i = 0; i < 16; i++) {
      const a = back(), sp = R.range(60, 200);
      ps.spawn(P.FIRE, -4 + R.range(-2, 2), R.range(-3, 3), Math.cos(a) * sp, Math.sin(a) * sp * 0.6 - 10, R.range(0.22, 0.42), 0, R.range(4, 8), R.range(0, 0.04), R.chance(0.5) ? PF.SMOKES : 0);
    }
    for (let i = 0; i < 10; i++) {
      const a = back(), sp = R.range(30, 110);
      ps.spawn(P.SMOKE, -6 + R.range(-3, 3), R.range(-4, 4), Math.cos(a) * sp, Math.sin(a) * sp * 0.4 - 12, R.range(0.5, 0.8), R.pick(SMK), R.range(5, 9), R.range(0.03, 0.12), PF.BIG);
    }
    for (let i = 0; i < 18; i++) {
      const a = back(), sp = R.range(150, 320);
      ps.spawn(P.SPARK, -4, 0, Math.cos(a) * sp, Math.sin(a) * sp * 0.7 - 30, R.range(0.12, 0.3));
    }
  }
  return {
    update(dt) { t += dt; if (!done) go(); ps.update(dt); cull(ps, -128, -68, 68, 48); lights.update(dt); },
    drawBack(p) { drawPs(p, ps, "back", FR); },
    draw(p) {
      if (t < 0.035) { p.circle(0, 0, 12, F[3]); p.circle(0, 0, 10, F[6]); p.circle(0, 0, 7, WHITE); }
      const u = t / 0.2;
      if (u < 1) p.ring(-2, 0, 8 + 22 * ease.outCubic(u), Math.max(1, 2.2 * (1 - u) + 0.4), u < 0.4 ? WHITE : F[6], Math.min(1, (1 - u) * 1.8));
      drawPs(p, ps, "front", FR);
      lights.draw(p);
    },
  };
}
