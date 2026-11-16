// 기절 시안 A: 잉크 별 고리. 머리 위 기울어진 고리를 따라 금빛 별 셋이 빙글빙글 돈다.
// 원점은 머리 꼭대기 가운데. 뒤쪽을 지나는 별은 작고 어둡게, 앞을 지날 때 반짝인다.
import { TAU, fit, hex, mix, sparkle } from "../../core/index.js";
import { icon, bitmap, shaped, starShape } from "../status/_lib.js";
import { INKLINE, stepT } from "./_lib.js";

export const meta = {
  name: "Stun stars",
  desc: "Stun A: three gold stars circle a tilted ring over the head, small and dim behind, bright with a glint in front, leaving glitter.",
  tags: ["inkstatus", "stun", "loop"],
  size: [44, 28], origin: [22, 20], duration: 0.8, loop: true, fps: 20, bg: "none",
  options: { palette: ["#140c1c", "#7a4a12", "#d88a1c", "#f5c030", "#ffe36e", "#fff6c8"], seed: 2 },
};

const SMALL = bitmap([".#.", "###", ".#."]);
export function draw(p, t, o) {
  const C = fit(o.palette.map ? o.palette.map(hex) : o.palette, 6), D = meta.duration, s = stepT(t, 20);
  const RX = 12, RY = 3.5, CY = -6;
  const stars = [0, 1, 2].map((i) => {
    const th = TAU * (s / D + i / 3), sn = Math.sin(th);
    return { i, th, x: Math.cos(th) * RX, y: CY + sn * RY, near: sn >= 0, depth: 0.5 + 0.5 * sn };
  }).sort((a, b) => a.depth - b.depth);
  // 궤도 흔적: 앞쪽 반원만 점선
  for (let k = 0; k < 40; k++) { const g = (k / 40) * TAU; if (Math.sin(g) > 0.2 && k % 2 === 0) p.dot(Math.cos(g) * RX, CY + Math.sin(g) * RY, C[3], 0.35); }
  for (const it of stars) {
    // 반짝이 꼬리
    for (let k = 1; k <= 5; k++) {
      const g = it.th - k * 0.22;
      p.dot(Math.cos(g) * RX, CY + Math.sin(g) * RY, k < 3 ? C[5] : C[4], (0.9 - k * 0.16) * (0.4 + 0.6 * it.depth));
    }
    const R = it.near ? [INKLINE, C[2], C[3], C[5]] : [INKLINE, C[1], C[2], C[3]];
    if (it.depth > 0.45) icon(p, it.x, it.y - 1, 6, shaped(starShape(0.45), 3.4 + 1.2 * it.depth, (s / D) * TAU * 2 + it.i), R);
    else icon(p, it.x, it.y - 1, 3, SMALL, R);
    p.glow(it.x, it.y - 1, 6, C[3], 0.2 + 0.25 * it.depth);
    if (it.near && Math.abs(Math.cos(it.th)) < 0.3) sparkle(p, it.x + 2, it.y - 4, 2, C[5], C[4], 0.6);
  }
}
