// 버프칸 아이콘 후보: 효과 10종 × 후보 3개, 24×24 픽셀(1배). pixelfx 코어(프레임·페인터·합성·PNG)로 그린다.
// 실행: node make_icons.mjs <pixelfx 경로> <후보 출력 폴더> [확정본 폴더]
// 확정본 폴더를 주면 고른 후보를 게임용으로 쓴다. 회복·방패는 그 뒤 make_final.mjs가 정리판으로 덮어쓴다.
import { writeFileSync, mkdirSync } from "node:fs";
import { join } from "node:path";
import { pathToFileURL } from "node:url";

const [, , PFX, OUT] = process.argv;
const core = await import(pathToFileURL(join(PFX, "src/core/index.js")).href);
const { encodePNG } = await import(pathToFileURL(join(PFX, "tools/lib/png.mjs")).href);
const { hex, mix, scale, createFrame, painterFor, composite } = core;
mkdirSync(OUT, { recursive: true });

const S = 24;
const INK = hex("#140c1c");
const WHITE = hex("#ffffff");
const ramp = (h) => { const c = hex(h); return [INK, scale(c, 0.55), c, mix(c, WHITE, 0.42), mix(c, WHITE, 0.8)]; };

// ---- 거리 함수 ----
const circ = (cx, cy, r) => (x, y) => Math.hypot(x - cx, y - cy) - r;
const box = (cx, cy, hw, hh, r = 0) => (x, y) => {
  const qx = Math.abs(x - cx) - hw + r, qy = Math.abs(y - cy) - hh + r;
  return Math.hypot(Math.max(qx, 0), Math.max(qy, 0)) + Math.min(Math.max(qx, qy), 0) - r;
};
const seg = (ax, ay, bx, by, r) => (x, y) => {
  const px = x - ax, py = y - ay, dx = bx - ax, dy = by - ay;
  const h = Math.max(0, Math.min(1, (px * dx + py * dy) / (dx * dx + dy * dy)));
  return Math.hypot(px - dx * h, py - dy * h) - r;
};
const poly = (pts) => (x, y) => {
  let d = Infinity, inside = false;
  for (let i = 0, j = pts.length - 1; i < pts.length; j = i++) {
    const [xi, yi] = pts[i], [xj, yj] = pts[j];
    d = Math.min(d, seg(xi, yi, xj, yj, 0)(x, y));
    if ((yi > y) !== (yj > y) && x < ((xj - xi) * (y - yi)) / (yj - yi) + xi) inside = !inside;
  }
  return inside ? -d : d;
};
const starPts = (cx, cy, R, r, n = 5, rot = -Math.PI / 2) => {
  const p = [];
  for (let i = 0; i < n * 2; i++) { const a = rot + (i * Math.PI) / n, q = i % 2 ? r : R; p.push([cx + Math.cos(a) * q, cy + Math.sin(a) * q]); }
  return p;
};
const star = (cx, cy, R, r, n, rot) => poly(starPts(cx, cy, R, r, n, rot));
const ellRing = (cx, cy, rx, ry, w, rot = 0) => (x, y) => {
  const c = Math.cos(-rot), s = Math.sin(-rot), dx = x - cx, dy = y - cy;
  const u = dx * c - dy * s, v = dx * s + dy * c;
  return Math.abs((Math.hypot(u / rx, v / ry) - 1) * Math.min(rx, ry)) - w;
};
const ring = (cx, cy, r, w) => (x, y) => Math.abs(Math.hypot(x - cx, y - cy) - r) - w;
const arc = (cx, cy, r, w, a0, a1) => (x, y) => {
  let a = Math.atan2(y - cy, x - cx); while (a < a0) a += Math.PI * 2;
  if (a <= a1) return Math.abs(Math.hypot(x - cx, y - cy) - r) - w;
  const e0 = [cx + Math.cos(a0) * r, cy + Math.sin(a0) * r], e1 = [cx + Math.cos(a1) * r, cy + Math.sin(a1) * r];
  return Math.min(Math.hypot(x - e0[0], y - e0[1]), Math.hypot(x - e1[0], y - e1[1])) - w;
};
const U = (...f) => (x, y) => Math.min(...f.map((g) => g(x, y)));
const SUB = (a, ...b) => (x, y) => Math.max(a(x, y), ...b.map((g) => -g(x, y)));
const path = (pts, r) => U(...pts.slice(1).map((p, i) => seg(pts[i][0], pts[i][1], p[0], p[1], r)));

// ---- 그림 한 장: 바탕 칸 + 부품들(음영·외곽선) ----
const GROUP = { yellow: "#f2c230", green: "#43a047", purple: "#8e44c8", red: "#e53935", blue: "#1e88e5" };

function render(group, parts) {
  const g = hex(GROUP[group]);
  const grid = new Array(S * S).fill(0), solid = new Array(S * S).fill(false);
  const inside = (f) => { const m = new Array(S * S); for (let y = 0; y < S; y++) for (let x = 0; x < S; x++) m[y * S + x] = f(x + 0.5, y + 0.5) < 0; return m; };
  for (const part of parts) {
    const m = inside(part.f);
    for (let y = 0; y < S; y++) for (let x = 0; x < S; x++) {
      const i = y * S + x; if (!m[i]) continue;
      if (part.color !== undefined) { grid[i] = part.color; if (part.solid) solid[i] = true; continue; }
      const R = part.ramp, at = (xx, yy) => xx >= 0 && yy >= 0 && xx < S && yy < S && m[yy * S + xx];
      let c = R[2];
      if (!at(x + 1, y) || !at(x, y + 1)) c = R[1];
      if (!at(x - 1, y) || !at(x, y - 1)) c = R[3];
      grid[i] = c; solid[i] = true;
    }
    for (const [x, y] of part.shine ?? []) grid[y * S + x] = part.ramp[4];
  }
  const frame = createFrame(S, S), p = painterFor(frame);
  // 바탕 칸
  const bg = mix(INK, g, 0.2), edge = scale(g, 0.8), bevel = mix(bg, WHITE, 0.1), tile = box(12, 12, 12, 12, 3), tileIn = box(12, 12, 11, 11, 2);
  for (let y = 0; y < S; y++) for (let x = 0; x < S; x++) {
    const cx = x + 0.5, cy = y + 0.5;
    if (tile(cx, cy) >= 0) continue;
    let c = tileIn(cx, cy) < 0 ? bg : edge;
    if (c === bg && (x === 1 || y === 1)) c = bevel;
    p.px(x, y, c);
  }
  // 그림 외곽선과 몸
  for (let y = 0; y < S; y++) for (let x = 0; x < S; x++) {
    const i = y * S + x;
    if (grid[i]) { p.px(x, y, grid[i]); continue; }
    const n = [[1, 0], [-1, 0], [0, 1], [0, -1]].some(([dx, dy]) => { const xx = x + dx, yy = y + dy; return xx >= 0 && yy >= 0 && xx < S && yy < S && solid[yy * S + xx]; });
    if (n && tile(x + 0.5, y + 0.5) < -0.5) p.px(x, y, INK);
  }
  const rgba = new Uint8Array(S * S * 4); composite(frame, rgba, null);
  return rgba;
}

// ---- 색 ----
const Y = ramp("#ffd54a"), GR = ramp("#7ee08a"), PU = ramp("#c690f0"), GOLD = ramp("#ffcf40"), RD = ramp("#ff6b5e"), BL = ramp("#6cb8ff"), ST = ramp("#b8c0d0"), WD = ramp("#b07a48");
const dark = (r) => r[0], mid = (r) => r[1];

// ---- 아이콘 정의 ----
const chevron = (x, y, h, r) => U(seg(x, y - h, x + h, y, r), seg(x + h, y, x, y + h, r));
const downChevron = (x, y, h, r) => U(seg(x - h, y, x, y + h, r), seg(x, y + h, x + h, y, r));
const upArrow = (x, y, h) => U(seg(x, y, x, y + h, 1), poly([[x, y - 3], [x - 3, y + 0.5], [x + 3, y + 0.5]]));
const plus = (x, y, a, t) => U(box(x, y, t, a), box(x, y, a, t));
// 점 목록의 상하좌우 범위 가운데를 칸 가운데(12, 12)에 맞춘다.
const centered = (pts) => {
  const xs = pts.map((p) => p[0]), ys = pts.map((p) => p[1]);
  const dx = 12 - (Math.min(...xs) + Math.max(...xs)) / 2, dy = 12 - (Math.min(...ys) + Math.max(...ys)) / 2;
  return pts.map(([x, y]) => [x + dx, y + dy]);
};
// 손잡이(hx, hy)에서 칼끝(tx, ty)으로 뻗은 검: 날·가로막이·손잡이.
const sword = (hx, hy, tx, ty) => {
  const len = Math.hypot(tx - hx, ty - hy), ux = (tx - hx) / len, uy = (ty - hy) / len, nx = -uy, ny = ux;
  return [
    { f: seg(hx, hy, tx, ty, 1.6), ramp: ST, shine: [[Math.round(hx + ux * len * 0.6 - 1), Math.round(hy + uy * len * 0.6)]] },
    { f: seg(hx + nx * 2.6, hy + ny * 2.6, hx - nx * 2.6, hy - ny * 2.6, 1), ramp: WD },
    { f: seg(hx - ux * 1.2, hy - uy * 1.2, hx - ux * 2.6, hy - uy * 2.6, 0.9), ramp: WD },
  ];
};

const ICONS = [
  { id: "speed", name: "이동속도 증가", group: "yellow", c: [
    { name: "겹 화살촉", parts: [{ f: chevron(5, 12, 6.5, 1.7), ramp: Y }, { f: chevron(12, 12, 6.5, 1.7), ramp: Y }] },
    { name: "날개", parts: [{ f: U(seg(5, 18, 18, 6, 2.3), seg(6, 19, 20, 11, 2), seg(8, 20, 20, 16, 1.6)), ramp: Y, shine: [[16, 7]] }, { f: seg(6, 18.5, 18, 8, 0.4), color: mid(Y) }] },
    { name: "질주 구슬", parts: [{ f: U(seg(3, 8, 9, 8, 1), seg(2, 12, 9, 12, 1), seg(4, 16, 9, 16, 1)), ramp: Y }, { f: circ(15, 12, 5), ramp: Y, shine: [[13, 9], [14, 9]] }] },
  ] },
  { id: "shield", name: "방어막 생성", group: "green", c: [
    { name: "방패", parts: [{ f: poly([[12, 3], [20, 6], [19, 14], [12, 21], [5, 14], [4, 6]]), ramp: GR }, { f: seg(12, 6, 12, 18, 0.9), color: mid(GR) }, { f: seg(7, 9.5, 17, 9.5, 0.9), color: mid(GR) }] },
    { name: "거품 막", parts: [{ f: ring(12, 12, 8, 1.4), ramp: GR }, { f: arc(12, 12, 5.5, 0.7, Math.PI * 1.05, Math.PI * 1.45), color: GR[4] }, { f: circ(12, 12, 2.2), ramp: GR }] },
    { name: "육각 결계", parts: [{ f: SUB(poly([0, 1, 2, 3, 4, 5].map((i) => [12 + Math.cos(i * Math.PI / 3 + Math.PI / 6) * 9, 12 + Math.sin(i * Math.PI / 3 + Math.PI / 6) * 9])), poly([0, 1, 2, 3, 4, 5].map((i) => [12 + Math.cos(i * Math.PI / 3 + Math.PI / 6) * 5.6, 12 + Math.sin(i * Math.PI / 3 + Math.PI / 6) * 5.6]))), ramp: GR }, { f: circ(12, 12, 2), ramp: GR }] },
  ] },
  { id: "heal", name: "체력 회복", group: "green", c: [
    { name: "십자", parts: [{ f: plus(12, 12, 7.5, 2.6), ramp: GR, shine: [[10, 6]] }] },
    { name: "하트", parts: [{ f: U(circ(8.5, 9.5, 4.3), circ(15.5, 9.5, 4.3), poly([[4.3, 11], [19.7, 11], [12, 20]])), ramp: RD, shine: [[7, 7], [8, 7]] }, { f: plus(12, 11.5, 3, 0.8), color: RD[4] }] },
    { name: "물방울", parts: [{ f: U(circ(10, 14.5, 5.5), poly([[10, 3], [5, 12], [15, 12]])), ramp: GR, shine: [[8, 12]] }, { f: plus(18, 6, 3, 1), ramp: GR }] },
  ] },
  { id: "stun", name: "기절 부여", group: "purple", c: [
    { name: "별 세 개", parts: [{ f: ellRing(12, 14, 8.5, 3.2, 0.6, -0.25), color: mid(PU), solid: true }, { f: star(6, 13, 3.6, 1.6), ramp: GOLD }, { f: star(13, 8.5, 3.8, 1.7), ramp: GOLD }, { f: star(18, 15.5, 3.4, 1.5), ramp: GOLD }] },
    { name: "큰 별", parts: [{ f: star(12, 13, 7.5, 3.2), ramp: GOLD, shine: [[11, 9]] }, { f: U(seg(3, 4, 5, 6, 0.6), seg(21, 4, 19, 6, 0.6), seg(3, 21, 5, 19, 0.6)), color: GOLD[3] }] },
  ] },
  { id: "bind", name: "속박 부여", group: "purple", decided: true, c: [
    { key: "B", name: "사슬", parts: [{ f: ellRing(8.5, 8.5, 5.2, 3, 1.1, Math.PI / 4), ramp: ST }, { f: ellRing(15.5, 15.5, 5.2, 3, 1.1, Math.PI / 4), ramp: ST }, { f: seg(11, 12.5, 12.5, 11, 1.1), ramp: ST }] },
  ] },
  { id: "poison", name: "중독 부여", group: "purple", c: [
    { name: "해골", parts: [{ f: SUB(U(circ(12, 10, 6.5), box(12, 16, 4, 3.2, 1)), circ(9.3, 10.5, 1.9), circ(14.7, 10.5, 1.9), poly([[12, 12.5], [10.8, 14.5], [13.2, 14.5]]), seg(10.5, 18.5, 10.5, 19.5, 0.4), seg(13.5, 18.5, 13.5, 19.5, 0.4)), ramp: PU, shine: [[9, 5], [10, 5]] }] },
    { name: "독 거품", parts: [{ f: circ(9, 15, 5.5), ramp: PU, shine: [[7, 12]] }, { f: circ(16.5, 8, 3.8), ramp: PU, shine: [[15, 6]] }, { f: circ(18, 17.5, 2.2), ramp: PU }] },
    { name: "독병", parts: [{ f: U(circ(12, 15, 6.5), box(12, 6, 2.4, 4)), ramp: ST }, { f: SUB(circ(12, 15, 5.2), box(12, 9, 8, 4.5)), ramp: PU, shine: [[9, 15]] }, { f: box(12, 3.2, 3.3, 1.2), ramp: WD }] },
  ] },
  { id: "slow", name: "슬로우 부여", group: "purple", decided: true, c: [
    { key: "확정", name: "소용돌이(가운데)", parts: [{ f: path(centered(Array.from({ length: 30 }, (_, i) => { const t = i / 29, a = t * Math.PI * 3.2, r = 1 + 7.5 * t; return [Math.cos(a) * r, Math.sin(a) * r]; })), 1), ramp: PU }] },
  ] },
  { id: "red", name: "기본 공격 피해 증가", group: "red", c: [
    { name: "검과 위 화살표", parts: [...sword(5, 19, 14, 10), { f: upArrow(18, 5, 9), ramp: RD }] },
    { name: "검 끝 타격", parts: [...sword(4, 20, 14, 10), { f: star(17, 7, 6, 2.6, 8, 0), ramp: RD, shine: [[16, 5]] }] },
    { name: "붉은 대검", parts: [{ f: poly([[12, 2], [16.5, 6.5], [16.5, 15], [7.5, 15], [7.5, 6.5]]), ramp: RD, shine: [[10, 6], [10, 7]] }, { f: seg(12, 5, 12, 13, 0.5), color: RD[1] }, { f: box(12, 16.5, 6.5, 1.3, 0.5), ramp: WD }, { f: box(12, 20, 1.5, 2.6, 0.5), ramp: WD }] },
  ] },
  { id: "red-speed", name: "기본 공격 속도 증가", group: "red", c: [
    { name: "검과 속도선", parts: [...sword(5, 19, 14, 10), { f: U(seg(13, 4, 21, 4, 1), seg(16, 8, 22, 8, 1), seg(18, 12, 22, 12, 1)), ramp: RD }] },
    { name: "연속 베기", parts: [0, 1, 2].map((k) => ({ f: arc(20 - k * 3, 4 + k * 3, 9, 1.2, Math.PI * 0.55, Math.PI * 0.98), ramp: k === 1 ? ST : RD }))},
    { name: "날개 검", parts: [{ f: U(seg(8, 13, 2, 6, 1.2), seg(8, 14.5, 2, 10.5, 1), seg(8, 16, 3, 14.5, 0.9), seg(16, 13, 22, 6, 1.2), seg(16, 14.5, 22, 10.5, 1), seg(16, 16, 21, 14.5, 0.9)), ramp: RD }, { f: seg(12, 3, 12, 14, 1.7), ramp: ST, shine: [[11, 5]] }, { f: seg(8, 15.5, 16, 15.5, 1.1), ramp: WD }, { f: seg(12, 16.5, 12, 21, 1.2), ramp: WD }] },
  ] },
  { id: "blue-cd", name: "스킬 쿨타임 감소", group: "blue", c: [
    { name: "시계", parts: [{ f: ring(12, 12, 7.6, 1.3), ramp: BL }, { f: U(seg(12, 12, 12, 7, 0.8), seg(12, 12, 15.5, 13.5, 0.8)), color: BL[3] }, { f: poly([[1.5, 9], [6, 7], [5.5, 12]]), ramp: BL }] },
    { name: "되감기 화살", parts: [{ f: arc(12, 12, 7, 1.6, -Math.PI * 0.35, Math.PI * 1.35), ramp: BL }, { f: poly([[8.5, 1.5], [15, 4.5], [9.5, 9.5]]), ramp: BL }] },
    { name: "모래시계", parts: [{ f: U(box(12, 3.5, 7, 1.3), box(12, 20.5, 7, 1.3)), ramp: WD }, { f: U(poly([[6, 5], [18, 5], [12, 12]]), poly([[12, 12], [18, 19], [6, 19]])), ramp: BL }, { f: poly([[9, 19], [15, 19], [12, 15]]), color: BL[4] }] },
  ] },
  { id: "blue-dmg", name: "스킬 피해 증가", group: "blue", decided: true, c: [
    { key: "B", name: "번개", parts: [{ f: poly([[14, 2], [5, 13], [11, 13], [8, 22], [19, 9], [13, 9]]), ramp: BL, shine: [[12, 5]] }] },
  ] },
];

// ---- 발동 조건 표시(모서리 배지) 3안 ----
// 5×5 손그림 글리프: 작은 배지는 거리 함수보다 점 그림이 또렷하다.
const bitmap = (rows) => (cx, cy) => (x, y) => {
  const gx = Math.floor(x - (Math.floor(cx) - 2)), gy = Math.floor(y - (Math.floor(cy) - 2));
  return gx >= 0 && gy >= 0 && gx < 5 && gy < 5 && rows[gy][gx] === "#" ? -1 : 1;
};
const TRIG = [
  { id: "basic", name: "기본 공격", glyph: bitmap(["....#", "...#.", "..#..", "##...", "##..."]) },
  { id: "skill", name: "스킬 공격", glyph: bitmap(["..#..", "#####", ".###.", ".#.#.", "#...#"]) },
  { id: "switch", name: "스위칭", glyph: bitmap(["...#.", "#####", ".....", "#####", ".#..."]) },
  { id: "dash", name: "대쉬", glyph: bitmap(["#.#..", ".#.#.", "..#.#", ".#.#.", "#.#.."]) },
  { id: "status", name: "상태이상 부여", glyph: bitmap(["..#..", ".###.", "#####", "#####", ".###."]) },
];
const TCOL = { basic: "#ff6b5e", skill: "#6cb8ff", switch: "#ffd54a", dash: "#e8e8f0", status: "#c690f0" };
function badge(rgba, style, t) {
  const put = (x, y, c) => { if (x < 0 || y < 0 || x >= S || y >= S) return; const j = (y * S + x) * 4; rgba[j] = c & 255; rgba[j + 1] = (c >>> 8) & 255; rgba[j + 2] = (c >>> 16) & 255; rgba[j + 3] = 255; };
  const col = hex(TCOL[t.id]);
  if (style === "A") { // 오른쪽 아래 동그라미 배지
    const bg = circ(19, 19, 4.6), rim = circ(19, 19, 5.6), gl = t.glyph(19, 19);
    for (let y = 13; y < S; y++) for (let x = 13; x < S; x++) { const cx = x + .5, cy = y + .5; if (rim(cx, cy) < 0) put(x, y, bg(cx, cy) < 0 ? (gl(cx, cy) < 0 ? col : INK) : col); }
  } else if (style === "B") { // 오른쪽 아래 모서리 삼각형 색띠 + 흰 그림
    const tri = poly([[24, 11], [24, 24], [11, 24]]), gl = t.glyph(19.5, 19.5);
    for (let y = 10; y < S; y++) for (let x = 10; x < S; x++) { const cx = x + .5, cy = y + .5; if (tri(cx, cy) < 0) put(x, y, gl(cx, cy) < 0 ? INK : col); }
  } else { // 아래쪽 가로 띠
    const gl = t.glyph(12, 21.5);
    for (let y = 19; y < S; y++) for (let x = 0; x < S; x++) { const cx = x + .5, cy = y + .5; if (box(12, 21.5, 12, 2.5, 1)(cx, cy) < 0) put(x, y, gl(cx, cy) < 0 ? INK : col); }
  }
  return rgba;
}


// ---- 저장 ----
const b64 = (rgba, w = S, h = S) => Buffer.from(encodePNG(w, h, rgba)).toString("base64");
const manifest = { size: S, icons: [], badges: [] };
for (const ic of ICONS) {
  const entry = { id: ic.id, name: ic.name, group: ic.group, decided: !!ic.decided, candidates: [] };
  ic.c.forEach((cand, k) => {
    const key = cand.key ?? "ABC"[k], rgba = render(ic.group, cand.parts), file = `${ic.id}-${ic.decided ? "final" : "abc"[k]}.png`;
    writeFileSync(join(OUT, file), encodePNG(S, S, rgba));
    entry.candidates.push({ key, name: cand.name, file, png: b64(rgba) });
  });
  manifest.icons.push(entry);
}
const sample = ICONS.find((i) => i.id === "stun");
for (const style of ["B"]) {
  const row = { style, items: [] };
  for (const t of TRIG) { const rgba = badge(render(sample.group, sample.c[0].parts), style, t); row.items.push({ id: t.id, name: t.name, png: b64(rgba) }); }
  manifest.badges.push(row);
}
writeFileSync(join(OUT, "icons.json"), JSON.stringify(manifest));

// ---- 확정본(사용자 선택 2026-11-12) ----
// 게임 아이콘 ID → 효과 정의와 고른 후보. 배지는 B(모서리 삼각형 색띠)를 투명 바탕의 덧그림으로 따로 뽑는다.
const FINAL = process.argv[4];
if (FINAL) {
  mkdirSync(FINAL, { recursive: true });
  const chosen = { "move-speed": ["speed", "A"], shield: ["shield", "A"], heal: ["heal", "A"], stun: ["stun", "A"], bind: ["bind", "B"],
    poison: ["poison", "A"], slow: ["slow", "확정"], "basic-damage": ["red", "A"], "basic-speed": ["red-speed", "A"], "skill-cooldown": ["blue-cd", "A"], "skill-damage": ["blue-dmg", "B"] };
  const files = [];
  for (const [id, [src, key]] of Object.entries(chosen)) {
    const ic = ICONS.find((i) => i.id === src), k = ic.c.findIndex((c, n) => (c.key ?? "ABC"[n]) === key);
    if (k < 0) throw new Error(`missing ${src} ${key}`);
    writeFileSync(join(FINAL, `${id}.png`), encodePNG(S, S, render(ic.group, ic.c[k].parts))); files.push(`${id}.png`);
  }
  for (const t of TRIG) {
    const rgba = badge(new Uint8Array(S * S * 4), "B", t);
    writeFileSync(join(FINAL, `trigger-${t.id}.png`), encodePNG(S, S, rgba)); files.push(`trigger-${t.id}.png`);
  }
  console.log("final", files.join(" "));
}
console.log("icons", manifest.icons.length * 3, "badges", manifest.badges.length * TRIG.length);
