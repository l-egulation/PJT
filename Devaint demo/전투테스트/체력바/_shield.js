// 보호막 시안 공용: 생김 → 유지 → 한 대 막음 → 마지막 한 대에 깨짐의 시간표와 보호막 칸 좌표.
import { seg } from "../../core/index.js";
import { BW } from "./_bar.js";

export const HP = 72, SHIELD = 12, HIT1 = 5, T_DRAW = 0.15, DRAW = 0.42, T_HIT = 1.15, T_BREAK = 1.75;
export const DUR = 2.5;
/** shield left on the bar at time t */
export const shieldAt = (t) => (t < T_HIT ? SHIELD : t < T_BREAK ? SHIELD - HIT1 : 0);
/** bar px range [a, b) of the shield at time t */
export const span = (t) => { const a = (HP / 100) * BW; return [a, a + (shieldAt(t) / 100) * BW]; };
export const drawProg = (t) => seg(t, T_DRAW, T_DRAW + DRAW);
