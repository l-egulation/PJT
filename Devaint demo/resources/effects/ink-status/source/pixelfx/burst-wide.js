// 표식 몸통 터짐(확정 1번 잉크 튐을 넓힌 판): 표식이 하나 터질 때 몸통 가운데에서 그 색 잉크 덩이가 터져
// 크고 작은 방울 열이 짧은 꼬리를 끌며 반경 30px 안팎까지 사방으로 튄다. 빛 번짐 없음.
// 원점은 몸통 가운데. 미리보기 시안 2·3은 이 시트를 몬스터 뒤/앞에 두고, 몬스터를 터지는 표식 색으로 물들인다(코드).
import { mix } from "../../core/index.js";
import { markRamp } from "./_lib.js";

export const meta = {
  name: "Burst wide",
  desc: "Mark body burst (wide): a blob of the mark's ink pops in the middle of the body and ten droplets of mixed size fly out about 30 px with short tails. No glow.",
  tags: ["inkstatus", "mark", "particle"],
  size: [80, 80], origin: [40, 40], duration: 0.4, loop: false, fps: 30, bg: "none",
  options: { mark: 0, seed: 1 },
};

// 각도, 거리 배율, 굵기(0 작음·1 큼)
const DROPS = [[0.2, 1, 1], [0.8, 0.75, 0], [1.3, 0.95, 1], [1.9, 0.7, 0], [2.5, 1, 1], [3.1, 0.8, 0], [3.7, 0.95, 1], [4.3, 0.7, 0], [4.9, 1, 1], [5.6, 0.8, 0]];

export function draw(p, t, o) {
  const R = markRamp(o.mark | 0);
  if (t >= meta.duration) return;
  if (t < 0.04) { p.circle(0, 0, 5, R[2]); p.circle(-1, -1, 2.4, R[3]); p.px(-2, -2, R[4]); return; }
  const a = t - 0.04, k = a / (meta.duration - 0.04), out = 1 - (1 - k) ** 2;
  if (k < 0.3) p.circle(0, 0, 4 * (1 - k / 0.3) + 0.5, R[3]);
  for (const [ang, m, big] of DROPS) {
    const d = 6 + 28 * m * out, x = Math.cos(ang) * d, y = Math.sin(ang) * d + 16 * k * k;
    const d0 = Math.max(4, d - 6), tx = Math.cos(ang) * d0, ty = Math.sin(ang) * d0 + 16 * k * k;
    const c = k < 0.45 ? R[3] : R[2], al = 1 - Math.max(0, k - 0.7) / 0.3;
    p.line(tx, ty, x, y, mix(c, R[1], 0.4), al * 0.8);
    if (big && k < 0.6) p.circle(x, y, 1.4, c, al); else p.dot(x, y, c, al);
  }
}
