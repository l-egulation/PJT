// 표식 시안 A: 잉크 방울. 표식 하나 = 방울 하나(색은 쌓인 순번 빨~보). 머리 위에 가로로 늘어선다.
// 원점은 방울 가운데. 천천히 1px 들썩이고, 가끔 윗면에 빛이 스친다.
import { fit, TAU, mix } from "../../core/index.js";
import { icon } from "../status/_lib.js";
import { markRamp, stepT } from "./_lib.js";

export const meta = {
  name: "Mark drop",
  desc: "Ink mark A: a coloured ink droplet that bobs a pixel above the head, with a glint sliding over its top.",
  tags: ["inkstatus", "mark", "loop"],
  size: [20, 24], origin: [10, 12], duration: 1.2, loop: true, fps: 15, bg: "none",
  options: { mark: 0, seed: 1 },
};

const inside = (dx, dy) => {
  const x = dx + 0.5, y = dy + 0.5;
  if (y > 0.5) return Math.hypot(x, y - 1.5) < 4.3 ? "#" : "";
  const w = 4.3 * Math.pow(Math.max(0, (y + 6.5) / 8), 1.25);
  return Math.abs(x) < w && y > -6.5 ? "#" : "";
};

export function draw(p, t, o) {
  const R = markRamp(o.mark | 0), D = meta.duration, s = stepT(t, 15);
  const bob = Math.round(Math.sin((s / D) * TAU) * 0.8);
  p.glow(0, 1 + bob, 9, R[2], 0.22);
  // 2026-11-06 사용자 요청: 방울마다 검정 테두리 없이 색 면만
  icon(p, 0, bob, 8, inside, [0, R[1], R[2], R[3]]);
  // 하이라이트: 왼쪽 위 두 칸, 반짝일 때 흰 점 하나 더
  p.px(-2, -1 + bob, R[4]);
  p.px(-2, 0 + bob, R[3]);
  const g = (s / D) % 1;
  if (g > 0.55 && g < 0.75) { p.px(-1, -3 + bob, R[4]); p.light(-1, -3 + bob, R[3], 0.6); }
  // 바닥 쪽 반사광
  p.px(2, 4 + bob, mix(R[2], R[3], 0.5));
}
