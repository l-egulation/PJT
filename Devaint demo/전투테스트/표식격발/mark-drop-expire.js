import { fit, clamp, ease, rand } from "../../core/index.js";
import { drawDrop } from "./mark-drop-idle.js";

// 표식 방울 — 시간이 다 되어 사라질 때. 마르듯 쪼그라들며 아래로 조금 처지고, 마지막엔 부스러기 두어 점만 남았다 꺼진다.
export const meta = {
  name: "Mark drop (expire)",
  desc: "One ink mark drop runs out: it shrivels and sags, leaves a couple of dry flecks that fall away, and is gone.",
  tags: ["ink", "mark", "status", "one-shot"],
  size: [24, 36],
  origin: [12, 16],
  duration: 0.44,
  loop: false,
  fps: 25,
  bg: "light",
  options: { palette: ["#1a1a1a", "#1a1a1a", "#1a1a1a", "#fbf6ec", "#ffffff", "#ffffff"], rim: true, seed: 3 },
};

export function draw(p, t, o) {
  const C = fit(o.palette, 6), k = clamp(t / 0.3);
  if (k < 1) drawDrop(p, { x: 0, y: 2 * ease.inQuad(k), r: 3.8 * (1 - ease.inQuad(k)), sx: 1 + 0.25 * k, sy: 1 - 0.3 * k, boil: k * 2 }, C, o.seed, o.rim);
  // 부스러기: 쪼그라드는 동안 떨어져 나와 아래로 진다
  for (let i = 0; i < 2; i++) {
    const s = t - 0.12 - i * 0.06;
    if (s < 0 || s > 0.26) continue;
    const x = (rand(i, o.seed) - 0.5) * 6, y = 2 + 140 * s * s + 6 * s;
    const dry = o.rim ? C[2] : C[3];
    p.px(x, y, s < 0.18 ? C[3] : dry);
    p.px(x + 1, y, dry);
  }
}
