import { fit, clamp, smooth, ease } from "../../core/index.js";
import { glowLine } from "./speed-trail.js";

// 이동 속도 증가(노랑) — 버프가 걸리는 순간. 몸 뒤로 긴 빛줄기 둘이 '휙' 뻗고, 발밑에 가로로 납작한 섬광이 스친다.
// 끝나면 게임이 speed-trail 반복으로 넘긴다. 원점은 플레이어 발 가운데, 앞이 +x. 왼쪽을 보면 시트를 좌우 반전한다.
export const meta = {
  name: "Speed burst (ink)",
  desc: "A speed buff kicks in: two long glowing streaks whip back from the body and a flat flash skims under the feet, then hand over to the trail loop.",
  tags: ["ink", "buff", "speed", "one-shot"],
  size: [128, 72],
  origin: [88, 60],
  duration: 0.28,
  loop: false,
  fps: 25,
  bg: "light",
  options: { palette: ["#171716", "#5c4210", "#a87a14", "#e3b030", "#f6d878", "#fffbe8"], seed: 4 },
};

export function draw(p, t, o) {
  const C = fit(o.palette, 6), back = -1, f = Math.floor(t / (1 / 25) + 1e-6);
  if (t >= meta.duration - 1e-4) return;
  // 긴 줄기 둘: 한 장 만에 뻗고 머리부터 몸 쪽으로 줄어든다
  for (const [n, s] of [{ y: -8, L: 58, d: 0.02 }, { y: -18, L: 72, d: 0 }, { y: -29, L: 64, d: 0.01 }, { y: -39, L: 44, d: 0.03 }].entries()) {
    const u = t - s.d;
    if (u < 0) continue;
    const reach = s.L * ease.outCubic(clamp(u / 0.03)), cut = s.L * ease.inQuad(smooth(0.04, 0.2, u)); // 한 장 만에 뻗고 바로 빨려 든다
    glowLine(p, back * (4 + reach), s.y, back * (4 + cut), s.y, n === 1 ? 2.8 : 2, 4, 1 - smooth(0.16, 0.26, u), C, o.seed + n, f);
  }
  // 발밑 섬광: 가로로 납작하게 앞뒤로 벌어진다
  const g = ease.outCubic(clamp(t / 0.08)), a = 1 - smooth(0.06, 0.2, t);
  glowLine(p, -18 * g, -1, 22 * g, -1, 1.6, 3, a, C, o.seed + 7, f);
}
