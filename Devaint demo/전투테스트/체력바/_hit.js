// 피해 시안 공용: 72 → 50, 맞는 순간 판 흔들림·숫자 빨강·초상 번쩍.
import { seg } from "../../core/index.js";
import { K, RED } from "./_bar.js";

export const FROM = 72, TO = 50, T_HIT = 0.15;
export function hitPlate(t) {
  const k = t - T_HIT;
  const jx = k > 0 && k < 0.24 ? Math.round(Math.cos(k * 105) * 2.5 * (1 - k / 0.24) ** 2) : 0;
  const jy = k > 0 && k < 0.24 ? Math.round(Math.sin(k * 132) * 1.4 * (1 - k / 0.24) ** 2) : 0;
  return { jx, jy, number: k > 0 && k < 0.35 ? RED[4] : K.cream, portrait: k > 0 && k < 0.1 ? RED[5] : K.cream, hp: k > 0 ? TO : FROM };
}
export const flashOn = (t) => t >= T_HIT && t < T_HIT + 0.07;
export { seg };
