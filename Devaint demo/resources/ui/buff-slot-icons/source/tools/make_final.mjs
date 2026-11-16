// 버프칸 아이콘 확정본(재기 정리판). 24×24, 1배.
// 재기(jaggies) 규칙: 직선은 수평·수직·1:1(45°)·1:2 기울기만 쓰고 계단 길이를 일정하게 둔다.
// 곡선은 정수 격자 경계에 중심을 둔 원으로 그려 계단 길이가 대칭으로 고르게 늘고 준다.
// 외톨이 픽셀·한 칸 홈·한 칸 돌기는 지우고, 외곽선은 4방향 1px로만 둘러 이중 픽셀을 만들지 않는다.
// pixelfx 코어(프레임·페인터·합성)와 PNG 인코더를 쓴다.
// 실행: node make_final.mjs <pixelfx 저장소 경로> <출력 폴더> [--all]
// 기본은 회복·방패만 쓴다. --all 은 정리 시안 전체(비교용)를 쓴다.
import { writeFileSync, mkdirSync } from "node:fs";
import { join } from "node:path";
import { pathToFileURL } from "node:url";

const [, , PFX, OUT] = process.argv;
const core = await import(pathToFileURL(join(PFX, "src/core/index.js")).href);
const { encodePNG } = await import(pathToFileURL(join(PFX, "tools/lib/png.mjs")).href);
const { hex, mix, scale, createFrame, painterFor, composite } = core;
mkdirSync(OUT, { recursive: true });

const S = 24, N = S * S;
const INK = hex("#140c1c"), WHITE = hex("#ffffff");
const ramp = (h) => { const c = hex(h); return [INK, scale(c, 0.55), c, mix(c, WHITE, 0.42), mix(c, WHITE, 0.8)]; };
const Y = ramp("#ffd54a"), GR = ramp("#7ee08a"), PU = ramp("#c690f0"), GOLD = ramp("#ffcf40"), RD = ramp("#ff6b5e"), BL = ramp("#6cb8ff"), ST = ramp("#b8c0d0"), WD = ramp("#b07a48");
const GROUP = { yellow: "#f2c230", green: "#43a047", purple: "#8e44c8", red: "#e53935", blue: "#1e88e5" };

// ---- 격자 마스크 ----
const blank = () => new Uint8Array(N);
const at = (m, x, y) => x >= 0 && y >= 0 && x < S && y < S && m[y * S + x] === 1;
const mask = (test) => { const m = blank(); for (let y = 0; y < S; y++) for (let x = 0; x < S; x++) if (test(x, y)) m[y * S + x] = 1; return m; };
const or = (...ms) => { const m = blank(); for (const a of ms) for (let i = 0; i < N; i++) m[i] |= a[i]; return m; };
const minus = (a, ...bs) => { const m = Uint8Array.from(a); for (const b of bs) for (let i = 0; i < N; i++) if (b[i]) m[i] = 0; return m; };
const rect = (x0, y0, x1, y1) => mask((x, y) => x >= x0 && x <= x1 && y >= y0 && y <= y1);
const pts = (list) => { const m = blank(); for (const [x, y] of list) if (x >= 0 && y >= 0 && x < S && y < S) m[y * S + x] = 1; return m; };
// 원: 중심 (cx, cy)는 격자 경계(정수) 또는 픽셀 중심(.5)에 둔다. 픽셀 중심 거리로 판정해 좌우·상하 대칭.
const disk = (cx, cy, r) => mask((x, y) => (x + .5 - cx) ** 2 + (y + .5 - cy) ** 2 <= r * r);
const ring = (cx, cy, r0, r1, keep = () => true) => mask((x, y) => { const d = Math.hypot(x + .5 - cx, y + .5 - cy); return d >= r0 && d < r1 && keep(x + .5 - cx, y + .5 - cy); });
// 1:1 띠: x+y(오른쪽 위로 올라가는 대각) 또는 x−y(오른쪽 아래로 내려가는 대각)가 범위 안.
const rise = (s0, s1, x0, x1) => mask((x, y) => x + y >= s0 && x + y <= s1 && x >= x0 && x <= x1);
const fall = (d0, d1, x0, x1) => mask((x, y) => x - y >= d0 && x - y <= d1 && x >= x0 && x <= x1);
// 다각형: 꼭짓점을 1:1·1:2 기울기가 되게 고르고 픽셀 중심으로 채운다.
const polygon = (P) => mask((px, py) => {
  const x = px + .5, y = py + .5; let inside = false;
  for (let i = 0, j = P.length - 1; i < P.length; j = i++) {
    const [xi, yi] = P[i], [xj, yj] = P[j];
    if ((yi > y) !== (yj > y) && x < ((xj - xi) * (y - yi)) / (yj - yi) + xi) inside = !inside;
  }
  return inside;
});
const bitmap = (rows, ox, oy) => { const l = []; rows.forEach((r, y) => [...r].forEach((c, x) => { if (c === "#") l.push([ox + x, oy + y]); })); return pts(l); };

// 외톨이 픽셀·한 칸 돌기는 지우고 한 칸 홈은 메운다(4방향 이웃 기준).
function tidy(m) {
  const n4 = (x, y) => (at(m, x + 1, y) ? 1 : 0) + (at(m, x - 1, y) ? 1 : 0) + (at(m, x, y + 1) ? 1 : 0) + (at(m, x, y - 1) ? 1 : 0);
  for (let pass = 0; pass < 4; pass++) {
    let changed = false;
    for (let y = 0; y < S; y++) for (let x = 0; x < S; x++) {
      const i = y * S + x, k = n4(x, y);
      if (m[i] && k <= 1 && !(at(m, x + 1, y + 1) || at(m, x - 1, y - 1) || at(m, x + 1, y - 1) || at(m, x - 1, y + 1))) { m[i] = 0; changed = true; }
      else if (!m[i] && k >= 3) { m[i] = 1; changed = true; }
    }
    if (!changed) break;
  }
  return m;
}

// ---- 칠하기: 몸, 왼쪽 위 밝은 면, 오른쪽 아래 그림자, 4방향 1px 외곽선 ----
function render(group, parts) {
  const g = hex(GROUP[group]);
  const grid = new Array(N).fill(0), solid = blank();
  for (const part of parts) {
    const m = part.raw ? part.m : tidy(Uint8Array.from(part.m));
    for (let y = 0; y < S; y++) for (let x = 0; x < S; x++) {
      const i = y * S + x; if (!m[i]) continue;
      if (part.color !== undefined) { grid[i] = part.color; if (part.solid !== false) solid[i] = 1; continue; }
      const R = part.ramp; let c = R[2];
      if (!at(m, x + 1, y) || !at(m, x, y + 1)) c = R[1];
      if (!at(m, x - 1, y) || !at(m, x, y - 1)) c = R[3];
      grid[i] = c; solid[i] = 1;
    }
    for (const [x, y] of part.shine ?? []) grid[y * S + x] = part.ramp[4];
  }
  const frame = createFrame(S, S), p = painterFor(frame);
  // 바탕 칸: 모서리를 2-1 계단으로 깎은 둥근 네모와 1px 테두리.
  const corner = (x, y) => { const cx = Math.min(x, S - 1 - x), cy = Math.min(y, S - 1 - y); return cx + cy < 2 || (cx === 0 && cy === 1) || (cx === 1 && cy === 0); };
  const cornerIn = (x, y) => { const cx = Math.min(x, S - 1 - x), cy = Math.min(y, S - 1 - y); return cx + cy < 3; };
  const bg = mix(INK, g, 0.2), edge = scale(g, 0.8), bevel = mix(bg, WHITE, 0.1);
  for (let y = 0; y < S; y++) for (let x = 0; x < S; x++) {
    if (corner(x, y)) continue;
    const border = x === 0 || y === 0 || x === S - 1 || y === S - 1 || cornerIn(x, y);
    p.px(x, y, border ? edge : (x === 1 || y === 1) ? bevel : bg);
  }
  for (let y = 0; y < S; y++) for (let x = 0; x < S; x++) {
    const i = y * S + x;
    if (grid[i]) { p.px(x, y, grid[i]); continue; }
    if (x < 2 || y < 2 || x > S - 3 || y > S - 3) continue;
    if (at(solid, x + 1, y) || at(solid, x - 1, y) || at(solid, x, y + 1) || at(solid, x, y - 1)) p.px(x, y, INK);
  }
  const rgba = new Uint8Array(N * 4); composite(frame, rgba, null);
  return rgba;
}

// ---- 아이콘 ----
// 회복·방패는 정리판(사용자 채택 2026-11-12). 나머지는 처음 고른 후보의 픽셀 격자를 그대로 따르고
// 계단이 어긋난 칸만 행 단위로 고쳤다(spans: 행 → [시작, 끝] 구간 목록).
const mid = (r) => r[1];
const spans = (rowsMap) => { const m = blank(); for (const [y, list] of Object.entries(rowsMap)) for (const [x0, x1] of list) for (let x = x0; x <= x1; x++) m[+y * S + x] = 1; return m; };
const STAR6 = ["..##..", "######", ".####.", ".####.", ".#..#."];
function chevron(o) {
  const r = { 4: [[o + 3, o + 4]], 19: [[o + 3, o + 4]] };
  for (let y = 5; y <= 11; y++) r[y] = [[o + y - 2, o + y + 1]];
  for (let y = 12; y <= 18; y++) r[y] = [[o + 21 - y, o + 24 - y]];
  return spans(r);
}
function chainLink(u0) {
  // 대각 좌표 u=x+y, v=x−y. 바깥 |u−u0|≤8·|v|≤4(끝 모서리 깎음), 구멍 |u−u0|≤4·|v|≤1. 변은 모두 1:1.
  return mask((x, y) => {
    const du = Math.abs(x + y - u0), dv = Math.abs(x - y);
    return du <= 8 && dv <= 4 && du + dv <= 10 && !(du <= 4 && dv <= 1);
  });
}
const SWORD = [
  { m: spans({ 8: [[13, 14]], 9: [[12, 15]], 10: [[11, 15]], 11: [[10, 14]], 12: [[9, 13]], 13: [[8, 12]], 14: [[7, 11]], 15: [[6, 10]], 16: [[5, 9]], 17: [[6, 8]], 18: [[6, 7]] }), ramp: ST, raw: true, shine: [[12, 10]] },
  { m: spans({ 16: [[2, 3]], 17: [[3, 4]], 18: [[4, 5]], 19: [[5, 6]], 20: [[6, 7]], 21: [[7, 8]] }), ramp: WD, raw: true },
  { m: spans({ 19: [[3, 4]], 20: [[2, 3]], 21: [[2, 2]] }), ramp: WD, raw: true },
];
const ICONS = {
  // 겹 화살촉: 원래의 두께 4 1:1 팔, 끝 캡만 계단(2칸→4칸)에 맞춤.
  "move-speed": ["yellow", [{ m: chevron(0), ramp: Y, raw: true }, { m: chevron(7), ramp: Y, raw: true }]],
  // 방패(정리판): 수직 옆면, 1:1로 모이는 아래 끝, 2px 십자 무늬.
  shield: ["green", [
    { m: minus(or(rect(5, 4, 18, 13), mask((x, y) => y >= 14 && y <= 19 && x >= 5 + (y - 13) && x <= 18 - (y - 13))), pts([[5, 4], [18, 4]])), ramp: GR, shine: [[7, 6]] },
    { m: or(rect(11, 6, 12, 16), rect(7, 8, 16, 9)), color: mid(GR), raw: true },
  ]],
  heal: ["green", [{ m: or(rect(9, 4, 14, 19), rect(4, 9, 19, 14)), ramp: GR, shine: [[10, 5], [5, 10]] }]],
  // 기절: 원래 자리의 별 셋(6×5 점 그림으로 대칭), 기울어진 궤도는 계단 1-1-2-3-7 / 1-2-4-10으로.
  stun: ["purple", [
    { m: spans({ 10: [[10, 16]], 11: [[7, 9], [17, 19]], 12: [[5, 6], [20, 20]], 13: [[4, 4], [21, 21]], 14: [[3, 3], [21, 21]], 15: [[2, 2], [19, 20]], 16: [[3, 4], [15, 18]], 17: [[5, 14]] }), color: mid(PU), raw: true },
    { m: bitmap(STAR6, 10, 6), ramp: GOLD, raw: true },
    { m: bitmap(STAR6, 3, 11), ramp: GOLD, raw: true },
    { m: bitmap(STAR6, 15, 13), ramp: GOLD, raw: true },
  ]],
  // 사슬: 원래의 45° 고리 두 개 크기·자리 그대로, 변을 1:1로.
  bind: ["purple", [{ m: chainLink(17), ramp: ST, raw: true }, { m: chainLink(29), ramp: ST, raw: true }]],
  // 해골: 원래 윤곽에서 윗머리(2-1-0)와 턱으로 넘어가는 계단(1-1)만 고침. 눈·코·이빨 틈은 원래대로.
  poison: ["purple", [{ m: minus(spans({ 4: [[9, 14]], 5: [[7, 16]], 6: [[6, 17]], 7: [[6, 17]], 8: [[6, 17]], 9: [[6, 17]], 10: [[6, 17]], 11: [[6, 17]], 12: [[6, 17]], 13: [[7, 16]], 14: [[8, 15]], 15: [[8, 15]], 16: [[8, 15]], 17: [[8, 15]], 18: [[8, 15]] }),
    spans({ 9: [[8, 10], [13, 15]], 10: [[7, 10], [13, 16]], 11: [[8, 10], [13, 15]], 13: [[11, 12]], 18: [[10, 10], [13, 13]] })), ramp: PU, raw: true, shine: [[9, 5], [10, 5]] }]],
  // 소용돌이: 원래 꼬리·두께 2·가운데 정렬을 유지하고 꼬리 꺾임과 바깥 곡선 계단(1-1-1-1-2)만 고침.
  slow: ["purple", [{ m: spans({ 5: [[4, 5]], 6: [[4, 5], [11, 16]], 7: [[4, 5], [10, 17]], 8: [[4, 5], [9, 11], [16, 18]], 9: [[4, 5], [9, 10], [17, 19]], 10: [[4, 5], [9, 10], [13, 14], [18, 19]], 11: [[4, 5], [9, 10], [13, 14], [18, 19]], 12: [[4, 5], [9, 14], [18, 19]], 13: [[4, 5], [10, 14], [18, 19]], 14: [[5, 7], [17, 19]], 15: [[6, 8], [16, 18]], 16: [[7, 9], [15, 17]], 17: [[8, 16]], 18: [[10, 14]] }), ramp: PU, raw: true }]],
  // 검과 위 화살표: 칼날은 원래 그대로, 가로막이·손잡이를 두께 2의 1:1 띠로.
  "basic-damage": ["red", [...SWORD, { m: spans({ 3: [[17, 18]], 4: [[16, 19]], ...Object.fromEntries([5, 6, 7, 8, 9, 10, 11, 12, 13, 14].map((y) => [y, [[17, 18]]])) }), ramp: RD, raw: true }]],
  // 검과 속도선: 같은 검, 속도선 세 줄은 원래 길이로 오른쪽 끝을 맞춤.
  "basic-speed": ["red", [...SWORD, { m: spans({ 3: [[12, 21]], 4: [[12, 21]], 7: [[16, 21]], 8: [[16, 21]], 11: [[17, 21]], 12: [[17, 21]] }), ramp: RD, raw: true }]],
  // 시계: 원래 두께의 고리를 격자 경계 중심으로 대칭, 바늘 2px(긴 바늘 수직·짧은 바늘 2:1), 되감기 화살 머리 1:1.
  "skill-cooldown": ["blue", [
    { m: ring(12, 12, 6.3, 8.9), ramp: BL, raw: true },
    { m: spans({ 6: [[11, 12]], 7: [[11, 12]], 8: [[11, 12]], 9: [[11, 12]], 10: [[11, 12]], 11: [[11, 12]], 12: [[11, 14]], 13: [[13, 16]] }), color: BL[3], raw: true },
    { m: spans({ 7: [[4, 5]], 8: [[3, 5]], 9: [[2, 5]], 10: [[3, 5]], 11: [[4, 5]] }), ramp: BL, raw: true },
  ]],
  // 번개: 윗줄기 왼쪽 1:1·오른쪽 1:2, 가운데 띠 1:1(원래 그대로), 아랫줄기 오른쪽 1:1·왼쪽 1:2.
  "skill-damage": ["blue", [{ m: spans({ 3: [[13, 13]], 4: [[12, 13]], 5: [[11, 12]], 6: [[10, 12]], 7: [[9, 11]], 8: [[8, 11]], 9: [[8, 18]], 10: [[7, 17]], 11: [[6, 16]], 12: [[5, 15]], 13: [[11, 14]], 14: [[11, 13]], 15: [[10, 12]], 16: [[10, 11]], 17: [[9, 10]], 18: [[9, 9]], 19: [[8, 8]] }), ramp: BL, raw: true, shine: [[12, 5]] }]],
};

// ---- 발동 조건 배지(오른쪽 아래 1:1 삼각형 색띠 + 5×5 글리프) ----
const GLYPH = {
  basic: ["....#", "...#.", "..#..", "##...", "##..."],
  skill: ["..#..", "#####", ".###.", ".#.#.", "#...#"],
  switch: ["...#.", "#####", ".....", "#####", ".#..."],
  dash: ["#.#..", ".#.#.", "..#.#", ".#.#.", "#.#.."],
  status: ["..#..", ".###.", "#####", "#####", ".###."],
};
const TCOL = { basic: "#ff6b5e", skill: "#6cb8ff", switch: "#ffd54a", dash: "#e8e8f0", status: "#c690f0" };
function badge(id) {
  const rgba = new Uint8Array(N * 4), col = hex(TCOL[id]), glyph = bitmap(GLYPH[id], 17, 17);
  for (let y = 0; y < S; y++) for (let x = 0; x < S; x++) {
    if (x + y < 34 || x > S - 1 || y > S - 1) continue;
    const cx = Math.min(x, S - 1 - x), cy = Math.min(y, S - 1 - y);
    if (cx + cy < 2 || (cx === 0 && cy === 1) || (cx === 1 && cy === 0)) continue; // 칸 모서리 깎기와 맞춘다
    const c = at(glyph, x, y) ? INK : col, j = (y * S + x) * 4;
    rgba[j] = c & 255; rgba[j + 1] = (c >>> 8) & 255; rgba[j + 2] = (c >>> 16) & 255; rgba[j + 3] = 255;
  }
  return rgba;
}

// 게임에 쓰는 정리판은 회복·방패뿐이다(사용자 결정 2026-11-12). 나머지 정리 시안은 비교용으로 코드에만 남긴다.
const ONLY = process.argv[4] === "--all" ? [...Object.keys(ICONS), "badges"] : ["heal", "shield"];
const files = [];
for (const [id, [group, parts]] of Object.entries(ICONS)) {
  if (!ONLY.includes(id)) continue; writeFileSync(join(OUT, `${id}.png`), encodePNG(S, S, render(group, parts))); files.push(`${id}.png`); }
for (const id of ONLY.includes("badges") ? Object.keys(GLYPH) : []) { writeFileSync(join(OUT, `trigger-${id}.png`), encodePNG(S, S, badge(id))); files.push(`trigger-${id}.png`); }
console.log(files.join(" "));
