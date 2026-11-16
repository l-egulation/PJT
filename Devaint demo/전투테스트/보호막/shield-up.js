import { smooth } from "../../core/index.js";
import { dome } from "./shield.js";

// 보호막(초록) — 생길 때. 크기는 그대로 두고 막이 슥 페이드인한다(솟거나 출렁이면 푸딩처럼 보인다).
// 끝나면 게임이 shield 반복으로 넘긴다.
export const meta = {
  name: "Shield up (barrier dome)",
  desc: "The barrier dome simply fades in at full size.",
  tags: ["ink", "shield", "buff", "one-shot"],
  duration: 0.28,
  loop: false,
  size: [152, 96],
  origin: [76, 86],
  fps: 25,
  bg: "light",
  options: { palette: ["#171716", "#1d4128", "#2f7444", "#5fb37a", "#a6dcb1", "#f4fff4"], rx: 60, ry: 70, power: 2, fill: 0.16, edge: 0.32, rim: 0.2, pattern: "waves", stars: 16, seed: 4 },
};

export function draw(p, t, o) {
  if (t >= meta.duration - 1e-4) return;
  dome(p, t, o, { fade: smooth(0, meta.duration, t), stars: t > 0.12 });
}
