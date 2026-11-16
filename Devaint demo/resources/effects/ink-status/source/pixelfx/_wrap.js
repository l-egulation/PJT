// 몸통 속박(감싸는 밧줄) 공용. 2026-11-06 사용자 요청: 곡선 B를 바탕으로 양옆 먹물 틈(기둥)과 매듭을 없애고 세 줄로,
// 줄마다 몬스터를 빙 둘러 감싼 느낌. 줄 하나 = 몸통을 감은 기울어진 고리. 앞쪽 반(아래 호)은 몸 앞 시트(layer "front"),
// 뒤쪽 반(위 호)은 몸 뒤 시트(layer "back", 조금 어둡게)로 나눠 그려 몸에 감긴 것처럼 보이게 한다.
// 원점은 몸통 가운데. rx 몸 반폭.
import { ropes } from "./_bind.js";

/** 기울어진 고리의 점들. tilt 라디안, dy 세로 위치, ry 고리 납작함, w 폭 배율, front 면 앞쪽 반. */
export function loop(rx, { tilt, dy, ry = 4, w = 1, bow = 0 }, front, wob = 0) {
  const R = rx * w + 3, c = Math.cos(tilt), s = Math.sin(tilt), pts = [];
  const n = Math.round(R * 3.4);
  for (let i = 0; i <= n; i++) {
    const g = (front ? 0 : Math.PI) + (i / n) * Math.PI;
    const lx = Math.cos(g) * R, ly = Math.sin(g) * (ry + wob) + bow * (1 - (lx / R) ** 2);
    pts.push([lx * c - ly * s, dy + lx * s + ly * c, i / n]);
  }
  return pts;
}

/** 감싼 줄 세 개 한 장. back 이면 어두운 단(C 를 한 단 내린 색)으로 뒤쪽 반만. */
export function wrapBind(p, C, o, specs, ph, w = 1.2) {
  const front = o.layer !== "back";
  const lines = specs.map((sp, i) => loop(o.rx, sp, front, Math.sin(ph + i * 2.1) * 0.5));
  ropes(p, lines, front ? C : [C[0], C[0], C[1], C[2], C[3], C[4]], w);
}
