// 몬스터 상태이상 표시(잉크 파츠) 공용 도우미. 32px = 1u, 게임 몬스터 그림과 같은 1배 척도.
import { hex, mix, scale } from "../../core/index.js";

/** 잉크 외곽선. 게임 몬스터의 1px 잉크 외곽선과 맞춘다.
 *  2026-11-06 사용자 요청으로 게임용 시트는 외곽선 없이 뽑는다: setOutline(false) 면 0(그리지 않음)이 된다.
 *  외곽선이 아닌 안쪽 어두운 무늬(해골 눈, 열쇠 구멍)는 INKDARK 를 쓴다. */
export const INKDARK = hex("#140c1c");
export let INKLINE = INKDARK;
export function setOutline(on) { INKLINE = on ? INKDARK : 0; }

/** 표식 7색(CombatArena.MvpMarkColors와 같은 값): 빨·주·노·초·파·남·보. */
export const MARK_HEX = ["#e53935", "#fb8c00", "#fdd835", "#43a047", "#1e88e5", "#3949ab", "#8e24aa"];

/** 한 색에서 [외곽, 그림자, 몸, 밝음, 하이라이트] 5단. */
export function ramp5(h) {
  const c = hex(h);
  return [INKLINE, scale(c, 0.55), c, mix(c, hex("#ffffff"), 0.42), mix(c, hex("#ffffff"), 0.82)];
}
export const markRamp = (i) => ramp5(MARK_HEX[((i % 7) + 7) % 7]);

/** 같은 계단 시간: 픽셀 그림답게 흔들림은 fps 계단으로 끊는다. */
export const stepT = (t, fps) => Math.floor(t * fps) / fps;

/**
 * 거리 함수 덩어리: d < 0 안쪽은 fill(x, y, d), 0..line 은 외곽선.
 */
export function blob(p, box, d, fill, outline, line = 1) {
  p.shade(box[0], box[1], box[2], box[3], (x, y) => {
    const v = d(x, y);
    if (v < 0) return fill(x, y, v);
    if (v < line) return outline;
    return 0;
  });
}
