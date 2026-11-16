import { drawLanes } from "./speed-trail.js";

// 이동 속도 증가(노랑) — 버프가 끝날 때. speed-trail 반복이 주기 0으로 돌아온 장에서 넘겨받아,
// 이미 흐르던 줄기만 끝까지 흘려 보내고 새 줄기는 만들지 않는다. 마지막 장은 빈다.
export const meta = {
  name: "Speed end (ink)",
  desc: "A speed buff runs out: the streaks already in flight drift back and thin away, and no new ones start.",
  tags: ["ink", "buff", "speed", "one-shot"],
  size: [176, 96],
  origin: [160, 88],
  duration: 0.36,
  loop: false,
  fps: 25,
  bg: "light",
  options: { palette: ["#171716", "#5c4210", "#a87a14", "#e3b030", "#f6d878", "#fffbe8"], lanes: 10, len: 1.4, top: -56, width: 1.5, seed: 2 },
};

export function draw(p, t, o) {
  if (t >= meta.duration - 1e-4) return;
  drawLanes(p, t, o, true);
}
