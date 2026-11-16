// 2단 점프 시안 공용 — 원점 = 공중에서 두 번째로 뛴 순간의 발끝(신발 밑). y 는 아래가 +.
// 그림 1px = 게임 1/64 유닛(미리보기 2배 결). 이펙트는 그 자리(세계)에 남고 주인공만 위로 떠난다.
// 이 프로젝트에서 통한 것: 메타볼 농도장 + 잡음 좌표 비틀기(장마다 흐름) + 가운데부터 갉기, 투명도는 4단계로 꽉 채운 면.
// 퇴짜 맞은 것: 점 디더, 매끈한 원·고리, 넘치고 출렁이는 「푸딩」, 바닥에 깔린 딱딱한 네모.
import { clamp } from "../../core/index.js";

export const STEP = 1 / 25;
export const frameOf = (t) => Math.floor(t / STEP + 1e-6);

/** 길쭉한 메타볼 하나의 농도: (ux,uy) 방향으로 s 배 늘어난 타원, 가장자리에서 0 */
export function ball(x, y, b) {
  const dx = x - b.x, dy = y - b.y, ux = b.ux ?? 1, uy = b.uy ?? 0, s = b.s ?? 1;
  const al = dx * ux + dy * uy, pe = -dx * uy + dy * ux;
  const q = (al * al) / (b.r * b.r * s * s) + (pe * pe) / (b.r * b.r);
  return q < 1 ? (1 - q) * (1 - q) : 0;
}
export const field = (list, x, y) => { let F = 0; for (const b of list) F += ball(x, y, b); return F; };

/** 사라질 때 투명도: 매끈하게 줄이지 않고 1 → .75 → .5 → .25 네 계단 */
export const stepFade = (k) => (k <= 0 ? 1 : k >= 1 ? 0 : 1 - Math.ceil(k * 4 - 1e-6) / 4);

/** 색을 칠하되 a<1 이면 디더 대신 반투명으로 겹친다 */
export function paint(p, x, y, c, a) {
  if (!c || a <= 0) return;
  if (a >= 0.999) p.px(x, y, c);
  else p.blend(x, y, c, a);
}

/** 가운데부터 들쭉날쭉 번지는 구멍. eat 0→1 로 반경이 커진다. 직선으로 잘리지 않게 잡음 경계 */
export function eaten(x, y, eat, rx, ry, f, seed, noise2) {
  if (eat <= 0) return false;
  const r = Math.hypot(x / rx, y / ry) + 0.5 * noise2(x * 0.17 + f * 0.3, y * 0.17 - f * 0.2, seed);
  return r < eat * 1.3 - 0.1;
}
