// 보라 잉크(속박·기절 파츠 색) 팔레트와 굵은 잉크 줄기 도우미.
import { hex } from "../../core/index.js";
import { INKLINE } from "./_lib.js";

export const PURPLE = ["#140c1c", "#2c1340", "#4c2272", "#7a3aa8", "#b070e0", "#e8c8ff"];
export const ramp = (list) => list.map(hex);

/**
 * 굵은 잉크 줄기: pts 를 따라 원을 이어 그린다. 먼저 외곽선, 다음 그림자, 몸, 왼쪽 위 밝은 면.
 * w(u) 는 굵기(반지름), u = 0..1 은 뿌리부터 끝까지.
 */
export function strand(p, pts, w, C) {
  const n = pts.length - 1;
  for (let i = 0; i <= n; i++) { const [x, y] = pts[i]; p.circle(x, y, w(i / n) + 1, INKLINE); }
  for (let i = 0; i <= n; i++) { const [x, y] = pts[i]; p.circle(x, y, w(i / n), C[1]); }
  for (let i = 0; i <= n; i++) { const [x, y] = pts[i], r = w(i / n); if (r > 0.9) p.circle(x - 0.4, y - 0.3, r - 0.7, C[2]); }
  for (let i = 0; i <= n; i++) { const [x, y] = pts[i], r = w(i / n); if (r > 1.4) p.px(x - r * 0.5, y - r * 0.4, C[3]); }
}
