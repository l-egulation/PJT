// 표식 터짐 파티클: 머리 위 잉크 방울 하나가 터질 때(연쇄 0.08초 간격). 다른 피격 이펙트와 겹치므로 작고 짧게:
// 방울이 한 번 눌렸다가 작은 방울 다섯으로 튀어 떨어지며 사라진다. 빛 번짐·고리 없음.
// 원점은 방울 가운데(mark-drop 과 같은 자리). 색은 mark(0..6).
import { mix } from "../../core/index.js";
import { markRamp } from "./_lib.js";

export const meta = {
  name: "Mark pop",
  desc: "Mark pop: the ink drop squashes once and splits into five small droplets that fly out, fall and vanish. Small, short, no glow.",
  tags: ["inkstatus", "mark", "particle"],
  size: [28, 28], origin: [14, 12], duration: 0.34, loop: false, fps: 30, bg: "none",
  options: { mark: 0, seed: 1 },
};

const DROPS = [[-42, -70], [-18, -92], [14, -95], [38, -68], [3, -48]];

export function draw(p, t, o) {
  const R = markRamp(o.mark | 0);
  if (t >= meta.duration) return;
  if (t < 0.05) {
    // 눌림: 납작한 방울
    p.ellipse(0, 1, 4.5, 3, R[2]); p.ellipse(-1, 0, 2.5, 1.5, R[3]); p.px(-2, 0, R[4]);
    return;
  }
  const a = t - 0.05, k = a / (meta.duration - 0.05);
  DROPS.forEach(([vx, vy], i) => {
    const x = vx * a, y = vy * a + 0.5 * 420 * a * a, big = i % 2 === 0 && k < 0.5;
    const c = k < 0.35 ? R[3] : k < 0.7 ? R[2] : R[1];
    p.dot(x, y, c, 1 - Math.max(0, k - 0.75) / 0.25);
    if (big) { p.dot(x + 1, y, c, 1); p.dot(x, y + 1, mix(c, R[1], 0.5), 1); }
  });
}
