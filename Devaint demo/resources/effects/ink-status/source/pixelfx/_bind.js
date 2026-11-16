// 몸통 속박(사선 교차형) 공용: 양옆 허공의 먹물 틈, 사슬 줄, 꼰 잉크 밧줄, 매듭.
// 원점은 몸통 가운데. rx 는 몸 반폭, ry 는 교차선이 닿는 위아래 높이, GAP 은 몸 가장자리에서 틈까지.
import { INKLINE, INKDARK, blob } from "./_lib.js";

export const GAP = 13;

/** 세로로 찢어진 먹물 틈 둘(양옆). */
export function rifts(p, C, rx, ry) {
  for (const side of [-1, 1]) {
    const sx = side * (rx + GAP), h = ry + 4;
    blob(p, [sx - 4, -h - 2, sx + 4, h + 2], (x, y) => (Math.hypot((x - sx) / 2.4, y / h) - 1) * 2.4, (x, y) => (Math.abs(y) < h * 0.4 ? C[0] : C[1]), INKLINE, 1);
    p.px(sx - side, -Math.round(h * 0.5), C[3]); p.px(sx, 0, C[4]); p.light(sx, 0, C[3], 0.4);
  }
}

/** 두 점 사이 직선 위 점들(떨림 jit(u) 은 선에 수직 방향 px). */
export function line(x0, y0, x1, y1, n, jit = () => 0) {
  const L = Math.hypot(x1 - x0, y1 - y0), nx = -(y1 - y0) / L, ny = (x1 - x0) / L, out = [];
  for (let i = 0; i <= n; i++) { const u = i / n, j = jit(u); out.push([x0 + (x1 - x0) * u + nx * j, y0 + (y1 - y0) * u + ny * j, u]); }
  return out;
}

export function link(p, x, y, front, C) {
  x = Math.round(x); y = Math.round(y);
  if (front) {
    p.rect(x - 2, y - 2, 5, 5, INKLINE);
    p.px(x - 1, y - 1, C[4]); p.px(x, y - 1, C[3]); p.px(x + 1, y - 1, C[3]);
    p.px(x - 1, y, C[3]); p.px(x + 1, y, C[2]);
    p.px(x - 1, y + 1, C[2]); p.px(x, y + 1, C[2]); p.px(x + 1, y + 1, C[1]);
  } else { p.rect(x - 1, y - 1, 3, 3, INKLINE); p.px(x, y, C[3]); p.px(x, y - 1, C[4]); }
}

/** 사슬 줄: 고리를 일정 간격으로. */
export function chain(p, pts, C) { pts.forEach(([x, y], i) => link(p, x, y, i % 2 === 0, C)); }

/** 꼰 잉크 밧줄: 먼저 모든 줄의 외곽선, 다음 몸, 다음 꼬임 무늬. lines 는 점 목록들의 목록. */
export function ropes(p, lines, C, w = 1.6) {
  for (const pts of lines) for (const [x, y] of pts) p.circle(x, y, w + 1, INKLINE);
  for (const pts of lines) for (const [x, y] of pts) p.circle(x, y, w, C[2]);
  for (const pts of lines) pts.forEach(([x, y], i) => {
    const k = i % 4;
    if (k === 0) { p.px(x - 1, y - 1, C[4]); p.px(x, y, C[3]); }
    else if (k === 1) p.px(x, y - 1, C[3]);
    else if (k === 3) p.px(x + 1, y + 1, C[1]);
  });
}

/** 가운데 매듭. */
export function knot(p, C, x = 0, y = 0) {
  p.circle(x, y, 4.2, INKLINE); p.circle(x, y, 3.2, C[2]);
  p.circle(x - 0.8, y - 0.8, 1.8, C[3]); p.px(x - 1, y - 2, C[5]); p.px(x + 1, y + 1, C[1]);
  // 매듭에서 늘어진 짧은 끝 둘
  for (const [dx, dy] of [[2, 4], [-1, 5]]) { p.line(x + dx, y + 3, x + dx + (dx > 0 ? 1 : -1), y + dy + 2, INKLINE); p.px(x + dx, y + 3, C[3]); }
  p.light(x - 1, y - 1, C[4], 0.4);
}
