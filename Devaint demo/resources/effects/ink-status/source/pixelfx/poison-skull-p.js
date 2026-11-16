// 중독 A 파티클: 해골 거품 하나의 한살이. 점으로 맺혀 해골이 되고, 흔들리며 떠올라 펑 터진다.
// 원점은 해골이 맺히는 자리. Unity(와 미리보기)가 몬스터 몸 전체 범위 안 무작위 위치에 중첩 수에 비례한 빈도로 뿌린다.
// 2026-11-06 사용자 요청: 고정 배치가 아니라 몸 전체 기준 무작위 파티클.
import { TAU } from "../../core/index.js";
import { icon, bitmap } from "../status/_lib.js";
import { INKLINE } from "./_lib.js";
import { ramp } from "./_ink.js";
import { TOXIC, SKULL7, SKULL5 } from "./_poison.js";

export const meta = {
  name: "Poison skull particle",
  desc: "Poison A particle: one poison skull forms from a dot, wobbles up about 16 px and bursts. Big and small rows.",
  tags: ["inkstatus", "poison", "particle"],
  size: [24, 32], origin: [12, 26], duration: 0.8, loop: false, fps: 15, bg: "none",
  options: { palette: TOXIC, big: 0, seed: 8 },
};

const BIG = bitmap(SKULL7), SMALL = bitmap(SKULL5);

export function draw(p, t, o) {
  const C = ramp(o.palette), u = Math.min(1, t / 0.75), big = !!o.big;
  if (t >= 0.75) return;
  const y = -Math.round(u * 16), x = Math.round(Math.sin(u * 8) * 1.2);
  if (u < 0.12) { p.circle(x, y, 1 + u * 12, C[3]); p.px(x, y, C[4]); }
  else if (u < 0.78) {
    icon(p, x, y, big ? 4 : 3, big ? BIG : SMALL, [INKLINE, C[2], C[3], C[4]], 1, { e: C[0], n: C[1], m: C[1] });
    p.light(x, y, C[4], 0.15);
  } else {
    const k = (u - 0.78) / 0.22, rr = (big ? 4 : 3) + 4 * k;
    for (let j = 0; j < 7; j++) { const a = (j / 7) * TAU; p.dot(x + Math.cos(a) * rr, y + Math.sin(a) * rr * 0.8, j % 2 ? C[5] : C[4], 1 - k); }
    if (k < 0.35) { p.px(x, y, C[5]); p.light(x, y, C[4], 0.6 * (1 - k)); }
  }
}
