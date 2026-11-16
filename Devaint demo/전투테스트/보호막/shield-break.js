import { clamp, smooth, ease } from "../../core/index.js";
import { dome } from "./shield.js";

// 보호막(초록) — 깨질 때(다 닳거나 시간이 끝남). 막이 한 번 밝게 번쩍이고 살짝 부풀면서
// 잡음 구멍이 번져 부서지듯 사라진다. shield 반복의 어느 장에서든 넘겨받아도 된다.
export const meta = {
  name: "Shield break (barrier dome)",
  desc: "The barrier dome flashes once, swells slightly and crumbles away as holes spread through the shell.",
  tags: ["ink", "shield", "buff", "one-shot"],
  duration: 0.36,
  loop: false,
  size: [152, 96],
  origin: [76, 86],
  fps: 25,
  bg: "light",
  options: { palette: ["#171716", "#1d4128", "#2f7444", "#5fb37a", "#a6dcb1", "#f4fff4"], rx: 60, ry: 70, power: 2, fill: 0.16, edge: 0.32, rim: 0.2, pattern: "waves", stars: 16, seed: 4 },
};

export function draw(p, t, o) {
  if (t >= meta.duration - 1e-4) return;
  dome(p, t, o, { grow: 1 + 0.06 * ease.outCubic(clamp(t / 0.2)), flash: 1 - smooth(0, 0.12, t), erode: smooth(0.04, 0.32, t), fade: 1 - smooth(0.24, 0.34, t), stars: t < 0.1 });
}
