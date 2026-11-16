// 2페이즈 패턴 1 Blue 잉크 파이어볼: 차징, 날아가는 파이어볼, 폭발. 실행: node tools/inkball.mjs (이 폴더 기준)
// 빛 화풍 규칙(외곽선 없음, 계단 투명도, 2px 덩어리)에 짙은 잉크 덩어리를 섞는다. 화면에서는 다크 판타지로 다시 칠해 쓴다.
//   ink-charge: Blue 손 앞. 잉크 방울이 소용돌이로 빨려 들고 고리가 조여들며 가운데 잉크 구슬이 부푼다. 셀 128×128, 피벗 가운데, 8프레임(0.5초에 맞춤, 마지막 프레임 유지)
//   ink-fireball: 2.5u(80px) 명암이 있는 잉크 구체가 불꽃 꼬리를 끄는 파이어볼(머리를 감싸는 불꽃 띠, 남색 가장자리), 꼬리 끝 불꽃 조각과 불씨. 오른쪽을 향하고 진행 방향으로 돌려 쓴다. 셀 224×112, 피벗 (144, 56), 6프레임 70ms 반복
//   ink-blast: 4×4u(128px) 폭발. 흰 섬광 → 잉크 덩어리가 울퉁불퉁 부풀어 터짐 → 잉크 물보라 원호와 방울이 튀고 떨어짐 → 연기 고리. 셀 192×192, 피벗 가운데, 9프레임
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { writePng } from './png.mjs';
import { Field, hex, rad, rng } from './glow.mjs';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
// core: 밝기가 높을수록 짙어지는 잉크 심 전용 색
const RAMP = { core: [hex('#161c44'), hex('#1c2452'), hex('#242e62'), hex('#2e3a72')], ink: [hex('#c8d4f4'), hex('#6878b8'), hex('#3a4888'), hex('#222c60')], steel: [hex('#f4f8fc'), hex('#c4d8ec'), hex('#6890bc'), hex('#3c5c84')], indigo: [hex('#eef0fa'), hex('#b8c0e4'), hex('#5464a4'), hex('#34447c')] };
const outQ = (t) => 1 - (1 - t) ** 3;
const ring = (F, cx, cy, r, w, hue, k, ry = r) => F.crescent(cx, cy, r, ry, 0, Math.PI * 2 - 0.01, w, hue, k);
// 속이 찬 잉크 구슬: 짙은 몸 + 왼쪽 위 빛 테두리 + 작은 반사
function inkOrb(F, cx, cy, r, k = 1) {
  F.glow(cx, cy, r * 1.15, 'ink', 0.62 * k);
  F.glow(cx + r * 0.15, cy + r * 0.15, r * 0.8, 'ink', 0.5 * k);
  F.crescent(cx, cy, r * 0.92, r * 0.92, rad(150), rad(290), Math.max(1.5, r * 0.12), 'steel', 0.95 * k);
  F.crescent(cx, cy, r * 0.92, r * 0.92, rad(-40), rad(80), Math.max(1.2, r * 0.08), 'indigo', 0.75 * k);
  F.glow(cx - r * 0.35, cy - r * 0.38, Math.max(2, r * 0.16), 'steel', 1.05 * k);
}

function charge() {
  const out = [], R = rng(17), motes = Array.from({ length: 14 }, (_, i) => ({ a: (i / 14) * Math.PI * 2 + R() * 0.4, r0: 44 + R() * 16, o: R() }));
  for (let f = 0; f < 8; f++) {
    const F = new Field(64, 64, 64, 64, RAMP), T = f / 7;
    // 잉크 방울이 소용돌이로 빨려 든다
    for (const m of motes) {
      const u = (T * 1.6 + m.o) % 1, r = m.r0 * (1 - u) + 8, a = m.a + u * 2.4;
      F.glow(Math.cos(a) * r, Math.sin(a) * r, 3.5 + 2 * (1 - u), 'ink', 1.15);
      F.needle(Math.cos(a) * r, Math.sin(a) * r, Math.cos(a - 0.35) * (r + 9), Math.sin(a - 0.35) * (r + 9), 1.4, m.o > 0.5 ? 'steel' : 'indigo', 0.8 * (1 - u * 0.5));
    }
    // 조여드는 고리 두 조각
    const rr = 50 - 26 * T;
    for (const off of [0, Math.PI]) F.crescent(0, 0, rr, rr, off + T * 3, off + T * 3 + rad(120), 2.2, off ? 'indigo' : 'steel', 0.7 + 0.3 * T);
    // 가운데 잉크 구슬이 부푼다
    inkOrb(F, 0, 0, 6 + 16 * outQ(T), 1);
    if (f >= 5) F.star(0, 0, 14 + 10 * T, 1.8, [[0, 1], [180, 1], [90, 0.7], [270, 0.7], [45, 0.4], [135, 0.4], [225, 0.4], [315, 0.4]], 'steel', 0.7 + 0.4 * T);
    out.push(F.toFrame());
  }
  return out;
}

// 파이어볼: 둥근 잉크 구체가 불꽃 꼬리를 끌고 날아간다. 머리는 완전한 공처럼, 꼬리는 그 뒤로 한 덩어리로 가늘어진다(갈래 없음).
// 구체 느낌: 머리를 원 그대로 뒤쪽까지 부풀리고(꼬리는 원의 뒤쪽 40%부터 시작), 왼쪽 위에서 빛을 받는 4단 명암,
// 왼쪽 위 반짝임, 오른쪽 아래 되비침 테두리, 머리를 한 바퀴 감싸는 밝은 불꽃 띠. 꼬리 쪽은 짙은 심·불꽃 띠·남색 가장자리가 뒤로 흐르며 일렁인다.
// q: 안쪽 거리(축 0, 경계 1). 머리는 반지름 r의 원, 꼬리는 x = -0.4r부터 길이 L에 걸쳐 가늘어지며 ph로 일렁인다.
function teardrop(x, y, r, L, ph, wob, tips = 0) {
  const x0 = -0.4 * r;
  if (x >= x0) return Math.hypot(x, y) / r;
  const u = (x0 - x) / L; if (u >= 1) return 9;
  // tips: 위아래 가장자리에서 뒤로 흐르는 뾰족한 불꽃 끝
  const tip = tips * Math.pow(Math.max(0, Math.sin(x * 0.11 + ph * 2 + (y > 0 ? 1.7 : 0))), 4);
  const h = r * 0.917 * Math.pow(1 - u, 1.0) * (1 + 0.08 * Math.sin(x * 0.18 + ph * 2) + tip * Math.min(1, u * 3));
  const mid = wob * u * u * Math.sin(x * 0.07 + ph);
  return Math.abs(y - mid) / Math.max(0.5, h);
}
function fireball() {
  const out = [], R = rng(23), embers = Array.from({ length: 9 }, () => ({ y: (R() - 0.5) * 26, o: R(), sp: 0.7 + R() * 0.5 }));
  const LX = -0.55, LY = -0.62, LZ = 0.56; // 빛 방향(왼쪽 위 앞)
  for (let f = 0; f < 6; f++) {
    const F = new Field(112, 56, 144, 56, RAMP), ph = (f / 6) * Math.PI * 2;
    // 바깥 남색 불꽃 가장자리(머리 둘레는 얇게, 꼬리에서 넓게)
    F.paint(-140, -50, 46, 50, 'indigo', (x, y) => { const q = teardrop(x + 2, y, 40, 104, ph + 0.6, 9, 0.34); return q < 1 ? 0.18 + 0.42 * q : 0; });
    // 밝은 강철빛 불꽃 띠: 머리를 한 바퀴 감싸고 꼬리로 이어진다
    F.paint(-110, -40, 42, 40, 'steel', (x, y) => { const q = teardrop(x, y, 36, 82, ph, 6); return q < 1.0 ? Math.max(0, 0.98 - Math.abs(q - 0.87) * 2.2) : 0; });
    // 짙은 잉크 심(그늘): 머리와 꼬리 속을 채운다
    F.paint(-80, -34, 34, 34, 'core', (x, y) => teardrop(x, y, 31, 54, ph - 0.4, 3) < 0.8 ? 0.5 : 0);
    // 구체 명암: 왼쪽 위에서 빛을 받는 쪽만 남색으로 밝아진다(그늘 쪽은 심이 이긴다)
    const rr = 31 * 0.8;
    F.paint(-rr, -rr, rr, rr, 'indigo', (x, y) => { const d = Math.hypot(x, y) / rr; if (d >= 1) return 0; const nz = Math.sqrt(1 - d * d), lit = Math.max(0, (x / rr) * LX + (y / rr) * LY + nz * LZ); return 0.22 + 0.72 * Math.pow(lit, 1.3); });
    // 꼬리 속에서 뒤로 흐르는 불꽃 줄기 세 가닥(머리 뒤에서 시작)
    [[-8, 0], [0, 2.1], [8, 4.2]].forEach(([oy, o]) => F.paint(-95, oy - 12, -14, oy + 12, 'indigo', (x, y) => { const q = teardrop(x + 16, y - oy - Math.sin(x * 0.09 + ph + o) * 3, 6, 64, ph + o, 3); return q < 1 ? 0.62 + 0.12 * (1 - q) : 0; }));
    // 구체 표면을 천천히 도는 잉크 결 두 줄(공의 곡면을 따라 휜다)
    for (let k = 0; k < 2; k++) { const a = ph * 0.5 + k * Math.PI; F.crescent(0, 0, 22, 22 * Math.abs(Math.cos(a)) + 3, rad(200), rad(330), 1.4, 'indigo', 0.66); }
    // 왼쪽 위 반짝임과 오른쪽 아래 되비침 테두리
    F.glow(-10, -12, 5, 'steel', 1.1); F.glow(-5, -16, 2.2, 'steel', 1.2);
    F.crescent(0, 0, 25, 25, rad(15), rad(95), 1.8, 'indigo', 0.85);
    // 앞쪽 밝은 테두리
    F.crescent(0, 0, 37, 37, rad(-75), rad(75), 2.6, 'steel', 1.08);
    // 꼬리 끝에서 떨어져 나가는 작은 불꽃 조각과 불씨
    for (let k = 0; k < 2; k++) { const u = (f / 6 + k * 0.5) % 1, cx = -104 - 26 * u, cy = Math.sin(ph + k * 3) * 5; F.paint(cx - 16, cy - 8, cx + 10, cy + 8, 'steel', (x, y) => { const q = teardrop(x - cx, y - cy, 5.5 * (1 - u * 0.6), 14, ph, 0); return q < 1 ? (0.75 - 0.5 * u) * (1 - q * 0.5) : 0; }); }
    for (const e of embers) { const u = (f / 6 + e.o) % 1, x = -60 - 64 * u * e.sp, y = e.y + Math.sin(u * 7 + e.o * 9) * 4 - 6 * u; F.glow(x, y, 2.2, u > 0.5 ? 'indigo' : 'steel', 1.05 * (1 - u * 0.6)); }
    out.push(F.toFrame());
  }
  return out;
}

function blast() {
  const out = [], R = rng(29);
  const lumps = Array.from({ length: 9 }, (_, i) => ({ a: (i / 9) * Math.PI * 2 + R() * 0.4, r: 0.65 + R() * 0.35 }));
  const arcs = Array.from({ length: 8 }, (_, i) => ({ a: (i / 8) * Math.PI * 2 + R() * 0.3, hue: i % 2 ? 'indigo' : 'steel' }));
  const drops = Array.from({ length: 18 }, () => ({ a: R() * Math.PI * 2, sp: 0.6 + R() * 0.6, r: 4 + R() * 4 }));
  for (let f = 0; f < 9; f++) {
    const F = new Field(96, 96, 96, 96, RAMP), k = 1 - Math.max(0, (f - 5) / 4);
    if (f <= 1) { F.glow(0, 0, f ? 30 : 38, 'steel', 1.3 - f * 0.3); F.star(0, 0, f ? 80 : 60, 4, [[0, 1], [180, 1], [90, 1], [270, 1], [45, 0.5], [135, 0.5], [225, 0.5], [315, 0.5]], 'steel', 1.1 - f * 0.2); }
    // 잉크 덩어리가 울퉁불퉁 부풀어 터진다
    if (f >= 1 && f <= 4) { const g = outQ(Math.min(1, f / 3)); const kk = f <= 3 ? 1 : 0.6; F.glow(0, 0, 50 * g + 10, 'ink', 0.74 * kk); for (const l of lumps) F.glow(Math.cos(l.a) * 40 * g * l.r, Math.sin(l.a) * 40 * g * l.r, 22 * g + 6, 'ink', 0.7 * kk); F.crescent(0, 0, 40 * g + 6, 40 * g + 6, rad(140), rad(300), 2.4, 'steel', 0.8 * kk); }
    // 잉크 물보라 원호: 굵게 튀어 나갔다가 가늘어진다
    if (f >= 2 && f <= 7) for (const a of arcs) { const t = (f - 2) / 5, r = 30 + 48 * outQ(t); F.crescent(Math.cos(a.a) * 6, Math.sin(a.a) * 6, r, r, a.a - 0.32, a.a + 0.32, 7 * (1 - t) + 1.2, 'ink', 1.1 * k); F.crescent(0, 0, r - 4, r - 4, a.a - 0.2, a.a + 0.2, 2 * (1 - t) + 0.6, a.hue, 0.95 * k); }
    // 잉크 방울: 사방으로 튀고 떨어진다
    if (f >= 2) for (const d of drops) { const t = (f - 2) * 0.08, r = 36 + 120 * t * d.sp, x = Math.cos(d.a) * r, y = Math.sin(d.a) * r + 260 * t * t; if (k > 0) F.glow(x, y, d.r * (1 - t) + 1.5, 'ink', 1.2 * k); }
    // 연기 고리
    if (f >= 4) { const t = (f - 4) / 4; ring(F, 0, 0, 50 + 26 * t, 2.4 * (1 - t) + 0.8, 'indigo', 0.75 * (1 - t) + 0.15, (50 + 26 * t) * 0.85); }
    out.push(F.toFrame());
  }
  return out;
}

function strip(frames) {
  const fw = frames[0].w, fh = frames[0].h, s = { w: fw * frames.length, h: fh, data: new Uint8Array(fw * frames.length * fh * 4) };
  frames.forEach((fr, i) => { for (let y = 0; y < fh; y++) s.data.set(fr.data.subarray(y * fw * 4, (y + 1) * fw * 4), (y * s.w + i * fw) * 4); });
  return s;
}
const SHEETS = {
  'ink-charge': [charge(), [64, 64], Array(8).fill(64), false, '2페이즈 패턴 1 Blue 차징(0.5초, 마지막 프레임 유지)'],
  'ink-fireball': [fireball(), [144, 56], Array(6).fill(70), true, '2페이즈 패턴 1 잉크 파이어볼(본체 2.5u, 오른쪽 향함, 진행 방향으로 회전)'],
  'ink-blast': [blast(), [96, 96], [30, 40, 50, 60, 60, 70, 80, 90, 100], false, '2페이즈 패턴 1 잉크 폭발(4×4u)'],
};
const metaPath = path.join(ROOT, 'metadata.json'), meta = JSON.parse(fs.readFileSync(metaPath, 'utf8'));
for (const [name, [frames, pv, ms, loop, note]] of Object.entries(SHEETS)) {
  writePng(path.join(ROOT, name + '.png'), strip(frames));
  const w = frames[0].w, h = frames[0].h;
  meta.assets[name] = { file: name + '.png', columns: frames.length, rows: 1, frameWidth: w, frameHeight: h, frameCount: frames.length, pivotPx: pv, pivot: [pv[0] / w, (h - pv[1]) / h], durationsMs: ms, loop, note };
}
fs.writeFileSync(metaPath, JSON.stringify(meta, null, 2) + '\n');
console.log('inkball done');
