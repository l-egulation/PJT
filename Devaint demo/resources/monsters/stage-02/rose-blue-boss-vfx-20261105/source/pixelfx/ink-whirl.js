// Blue 잉크 회오리(2페이즈 패턴 4 양옆 압박의 Blue 쪽 시안 B3): 확정 3P5 화살 회오리 곁들임(aoe-whirl.js)을 Blue 압박 크기·시간·색으로 쓴 것.
// 반 폭 6.09u(97.5px)·높이 7.03u(112.5px), 0.5초 전조 뒤 0.8초 동안 커지고 1.4초에 끝나 0.3초 동안 흩어진다. 빨강·보라 대신 Blue 색(심연 남색→파랑)으로만 칠한다.
// 도는 물체 없이 잉크 바람 띠만으로 회오리를 보여 주도록 띠를 22가닥, 1.5배 굵게, 1.4배 길게 한다(사용자: 잉크 구체는 넣지 않음).
// 원점은 영역 가운데 바닥이다. 영역이 벽에서 넓어지는 동안 게임이 원점을 영역 가운데로 옮긴다. 반 해상도(1px = 게임 2px)로 그려 2배로 쓴다.
import * as whirl from "./aoe-whirl.js";

export const meta = {
  ...whirl.meta,
  name: "Blue ink whirl (swap press)",
  desc: "The phase-3 arrow-whirlwind dressing as Blue's side of the swap press: ink ribbons, a floor vortex and violet motes, sized and timed to the press.",
  size: [240, 152],
  origin: [120, 138],
  duration: 1.8,
  options: { layer: "back", seed: 13, hw: 97.5, hh: 112.5, tell: 0.5, grow: 0.8, end: 1.4, ink: true, blue: true, solid: true, ribbons: 22, ribbonW: 1.5, ribbonLen: 1.4 },
};
export const draw = whirl.draw;
