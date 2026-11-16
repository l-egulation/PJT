// 미사일 꼬리 — 미사일이 len px 를 초당 speed px 로 지나가며 남긴 자국. 원점 = 꼬리 끝(부딪친 자리), 자국은 -len..0 (월드 고정).
// style: "beads"(처음 판, 연기 구슬) · "band"(끊기지 않는 굵은 연기 띠 + 처음 불길) · "fire"(불덩이가 깔렸다 연기로 식음) · "lines"(흰 속도선만)
import { Particles, P, PF, Rng, fit, hex, WHITE, rand } from "../../core/index.js";
import { fireRamp, drawPs } from "./_lib.js";
import { PALETTE } from "./_bomb-pop.js";

export const meta = {
  name: "Bomb missile trail", desc: "What a missile leaves as it streaks len px: smoke beads, a solid smoke band, a line of fire cooling to smoke, or white speed lines.",
  tags: ["missile", "trail", "smoke", "one-shot"], size: [520, 70], origin: [500, 35], duration: 1.3, loop: false, fps: 30, bg: "light",
  options: { palette: PALETTE, seed: 6, len: 420, speed: 1920, style: "beads" },
};
const SMK = ["#9a9097", "#7d747e", "#615a65"].map(hex);
export function create(o) {
  const F = fit(o.palette, 8), FR = fireRamp(F), R = new Rng(o.seed), L = o.len, V = o.speed, st = o.style;
  const ps = new Particles({ rng: R, sparks: [F[6], F[5], F[3], F[2]] });
  let t = 0, laid = -L;
  const step = st === "band" ? 3 : st === "fire" ? 4 : st === "lines" ? 14 : 7;
  return {
    update(dt) {
      t += dt;
      const head = Math.min(0, -L + V * t);
      for (; laid <= head; laid += step) {
        const k = (laid + L) / L; // 0 출발 .. 1 부딪친 자리
        if (st === "beads") {
          ps.spawn(P.SMOKE, laid + R.range(-2, 2), R.range(-2.5, 2.5), R.range(-8, 8), R.range(-14, -2), R.range(0.45, 0.75), R.pick(SMK), R.range(2.2, 4.2), 0, PF.BIG);
          if (R.chance(0.35)) ps.spawn(P.EMBER, laid, R.range(-1.5, 1.5), R.range(-30, 10), R.range(-20, 10), R.range(0.15, 0.3), F[4]);
        } else if (st === "band") {
          ps.spawn(P.SMOKE, laid + R.range(-1, 1), R.range(-1.5, 1.5), R.range(-6, 6), R.range(-12, -3), R.range(0.5, 0.8), R.pick(SMK), R.range(4, 6) * (0.75 + 0.35 * k), 0, PF.BIG);
          if (R.chance(0.5)) ps.spawn(P.FIRE, laid, R.range(-1, 1), R.range(-20, 0), R.range(-10, 5), R.range(0.08, 0.16), 0, R.range(2.2, 3.4));
        } else if (st === "fire") {
          ps.spawn(P.FIRE, laid + R.range(-1.5, 1.5), R.range(-2, 2), R.range(-15, 15), R.range(-25, -5), R.range(0.2, 0.36), 0, R.range(3.2, 5.4) * (0.7 + 0.45 * k), 0, PF.SMOKES);
          if (R.chance(0.3)) ps.spawn(P.SPARK, laid, R.range(-2, 2), R.range(-120, -20), R.range(-90, 60), R.range(0.1, 0.25));
        } else if (st === "lines" && R.chance(0.25)) {
          ps.spawn(P.SMOKE, laid, R.range(-2, 2), R.range(-5, 5), R.range(-8, 0), R.range(0.3, 0.5), SMK[0], R.range(2, 3.2), 0, PF.BIG);
        }
      }
      ps.update(dt);
    },
    drawBack(p) { drawPs(p, ps, "back", FR); },
    draw(p) {
      const head = Math.min(0, -L + V * t);
      const lines = st === "lines" ? [[-9, 0.16, 1], [-3, 0.22, 1.6], [3, 0.2, 1.4], [9, 0.14, 1]] : [[0, 0.12, 1.6]];
      // 흰 속도선: 지나간 직후가 굵고 밝고, 뒤에서부터 사라진다. lines 는 네 줄이 길이를 달리해 흩어진다
      for (const [yy, life, w0] of lines) {
        p.shade(-L - 2, yy - 3, 2, yy + 3, (x, y) => {
          if (x > head) return;
          const since = t - (x + L) / V, lf = life * (0.7 + 0.6 * rand(Math.floor(x / 23), yy, 3));
          if (since > lf) return;
          const w = w0 * (1 - since / lf) + 0.35;
          if (Math.abs(y - yy) > w) return;
          return since < lf * 0.35 ? WHITE : F[6];
        });
      }
      drawPs(p, ps, "front", FR);
    },
  };
}
