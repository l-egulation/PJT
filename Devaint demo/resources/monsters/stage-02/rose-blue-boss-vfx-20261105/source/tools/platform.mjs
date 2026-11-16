// 1페이즈 발판 전조·폭발을 빛 화풍으로 만든다. 실행: node tools/platform.mjs (이 폴더 기준)
// 모두 발판 하나(6u = 192px)를 통째로 덮는 효과 하나이고, 피벗은 발판 윗면 가운데다.
//   패턴 3: p1p3-machine-warn(기계 발판 1초 전조, Blue·잉크), p1p3-ink-blast(기계 발판 구역 6×2u가 터지는 잉크 폭발), p1p3-rain-warn(나머지 발판 0.5초 화살비 전조, Rose)
//   패턴 4: p1p4-warn-rose/blue(순서 번호 홀수 Rose·짝수 Blue 전조), p1p4-blast-rose(장미 개화·화살 베기)/blue(고리가 퍼지는 구역 폭발)
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { writePng } from './png.mjs';
import { Field, hex, rad, rng } from './glow.mjs';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const BLUE = { steel: [hex('#f4f8fc'), hex('#c4d8ec'), hex('#6890bc'), hex('#3c5c84')], indigo: [hex('#eef0fa'), hex('#b8c0e4'), hex('#5464a4'), hex('#34447c')] };
// 잉크: 빛보다 짙은 남색 덩어리. 심도 연한 남색이라 하얗게 뜨지 않는다.
const INK = { ...BLUE, ink: [hex('#c8d4f4'), hex('#6878b8'), hex('#3a4888'), hex('#222c60')] };
const ROSE = { rose: [hex('#fff6f0'), hex('#f4c4cc'), hex('#cc647c'), hex('#94445c')], cream: [hex('#fffaf4'), hex('#f0dcc8'), hex('#d8909c'), hex('#a4546c')] };
const HALF = 92;
const BLAST_MS = [30, 40, 50, 60, 60, 70, 80, 90];

function strip(frames) {
  const fw = frames[0].w, fh = frames[0].h, s = { w: fw * frames.length, h: fh, data: new Uint8Array(fw * frames.length * fh * 4) };
  frames.forEach((fr, i) => { for (let y = 0; y < fh; y++) s.data.set(fr.data.subarray(y * fw * 4, (y + 1) * fw * 4), (y * s.w + i * fw) * 4); });
  return s;
}
// 발판 윗면 솔기: 왼쪽 반 a, 오른쪽 반 b
function seam(F, a, b, w, k, y = 0) { F.needle(-HALF, y, 0, y, w, a, k); F.needle(0, y, HALF, y, w, b, k); }
// 위험 범위 모서리: 양끝에서 위로 올라가는 가는 기둥
function edges(F, h, hue, k) { for (const x of [-HALF, HALF]) F.needle(x, 2, x, -h, 1.6, hue, k); }

// 패턴 3 기계 발판 전조: 솔기 + 범위 모서리(2u) + 바닥에서 부글거리며 오르는 잉크 방울
function machineWarn() {
  const out = [], R = rng(31), bub = Array.from({ length: 11 }, (_, i) => ({ x: -84 + (168 * i) / 10 + (R() - 0.5) * 8, o: R(), r: 5 + R() * 4 }));
  for (let f = 0; f < 6; f++) {
    const F = new Field(104, 40, 104, 72, INK), ph = f / 6, pulse = 0.75 + 0.25 * Math.cos(ph * Math.PI * 2);
    seam(F, 'steel', 'indigo', 2.6, 1.1 * pulse); edges(F, 60, 'indigo', 0.75 * pulse);
    F.needle(-HALF, -62, HALF, -62, 1.2, 'indigo', 0.5 * pulse);
    for (const b of bub) {
      const u = (ph + b.o) % 1, y = -3 - 50 * u, k = Math.sin(Math.PI * u);
      const bx = b.x + Math.sin(u * 6 + b.o * 9) * 3; F.glow(bx, y, b.r * (1 - 0.4 * u) + 2, 'ink', 1.3 * k); F.glow(bx - 1, y - 1, 3, 'steel', 0.9 * k);
      if (u < 0.3) F.glow(b.x, -2, 5, 'steel', 0.8 * (1 - u / 0.3));
    }
    out.push(F.toFrame());
  }
  return out;
}

// 구역 폭발: 발판 위 위험 구역(폭 6u × 높이 zh) 한가운데가 터진다.
// 흰 심 + 별 섬광 → 구역 여기저기서 작은 폭발이 연달아 터짐 → 구역 모양(가로로 긴 타원)으로 좌우 색이 나뉜 고리가 퍼짐 → 바늘 파편이 사방으로 흩어짐.
// ink가 있으면 잉크 덩어리가 부풀고 잉크 방울이 사방으로 튄다.
function zoneBlast(ramps, a, b, { W, H, PX, PY, zh, ink = false, seed }) {
  const out = [], R = rng(seed), cy = -zh / 2, sy = zh / 192;
  const subs = Array.from({ length: 6 }, (_, i) => ({ x: (i % 2 ? 1 : -1) * (24 + R() * 54), y: cy + (R() - 0.5) * zh * 0.7, d: 0.5 + i * 0.35, r: 10 + R() * 8, hue: i % 2 ? b : a }));
  const shards = Array.from({ length: 22 }, (_, i) => ({ ang: (i / 22) * Math.PI * 2 + (R() - 0.5) * 0.3, sp: 0.65 + R() * 0.45, hue: Math.cos((i / 22) * Math.PI * 2) < 0 ? a : b }));
  const blobs = Array.from({ length: 14 }, (_, i) => ({ ang: (i / 14) * Math.PI * 2 + R() * 0.4, sp: 0.6 + R() * 0.5, r: 5 + R() * 4 }));
  const outQ = (t) => 1 - (1 - t) ** 3;
  for (let f = 0; f < 8; f++) {
    const F = new Field(W / 2, H / 2, PX, PY, ramps);
    // 잉크 덩어리: 가운데에서 구역 모양으로 부풀었다가 흩어진다
    if (ink && f <= 4) { const g = outQ(Math.min(1, (f + 1) / 3)), k = f <= 2 ? 1.1 : 1.1 - (f - 2) * 0.35; for (const x of [-48, 0, 48]) F.glow(x * g, cy, (zh * 0.55 + 10) * g, 'ink', k * (x ? 0.85 : 1)); }
    // 흰 심과 별 섬광
    if (f <= 2) {
      const k = [1.35, 1.05, 0.7][f], L = [70, 120, 100][f];
      F.glow(0, cy, [zh * 0.45, zh * 0.35, zh * 0.22][f] + 8, a, k);
      F.star(0, cy, L, [5, 4.5, 3][f], [[0, 1], [180, 1], [90, zh / 140], [270, zh / 140], [35, 0.4], [145, 0.4], [215, 0.4], [325, 0.4]], a, k * 0.95);
    }
    // 구역 안 작은 폭발들
    for (const p of subs) {
      const t = f - p.d; if (t < 0 || t > 3) continue;
      const k = 1 - t / 3;
      if (t < 1.2) { F.glow(p.x, p.y, p.r * (0.6 + t * 0.4), p.hue, 1.25 * k + 0.2); F.star(p.x, p.y, p.r * 2.2, 2.4, [[0, 1], [180, 1], [90, 0.7], [270, 0.7]], p.hue, k + 0.2); }
      else F.crescent(p.x, p.y, p.r * (1 + t * 0.5), p.r * (1 + t * 0.5) * 0.8, 0, Math.PI * 2 - 0.01, 2.6 * k + 0.6, p.hue, 0.5 + 0.6 * k);
    }
    // 큰 고리 두 겹: 바깥은 좌 a·우 b, 안쪽은 위 b·아래 a
    if (f >= 1 && f <= 6) {
      const k = (f - 1) / 5, fade = 1 - Math.max(0, (f - 3) / 4), w = 7 * fade + 1.2;
      const rx = 40 + 80 * outQ(k), ry = Math.max(14, rx * sy * 1.15);
      F.crescent(0, cy, rx, ry, rad(100), rad(260), w, a, 0.35 + 0.8 * fade);
      F.crescent(0, cy, rx, ry, rad(-80), rad(80), w, b, 0.35 + 0.8 * fade);
      if (f <= 4) { const rx2 = rx * 0.62, ry2 = ry * 0.62; F.crescent(0, cy, rx2, ry2, rad(190), rad(350), w * 0.7, b, 0.8 * fade); F.crescent(0, cy, rx2, ry2, rad(10), rad(170), w * 0.7, a, 0.8 * fade); }
    }
    // 바늘 파편: 사방으로 흩어진다(구역 모양을 따라 위아래는 덜 간다)
    if (f >= 2) for (const sh of shards) {
      const r = 30 + 100 * outQ((f - 2) / 5) * sh.sp, k = 1 - Math.max(0, (f - 5) / 3); if (k <= 0) continue;
      const c = Math.cos(sh.ang), sn = Math.sin(sh.ang) * Math.max(0.55, sy), x = c * r, y = cy + sn * r;
      F.needle(x - c * 4, y - sn * 4, x + c * 11, y + sn * 11, 2.4, sh.hue, 0.55 + 0.45 * k);
    }
    // 잉크 방울: 사방으로 튀고 조금씩 떨어진다
    if (ink && f >= 1 && f <= 6) for (const bl of blobs) {
      const t = (f - 1) / 5, r = 24 + 96 * outQ(t) * bl.sp, k = 1 - Math.max(0, (f - 4) / 3);
      F.glow(Math.cos(bl.ang) * r, cy + Math.sin(bl.ang) * r * Math.max(0.5, sy) + 40 * t * t, bl.r * (1 - 0.3 * t) + 1, 'ink', 1.25 * k);
    }
    out.push(F.toFrame());
  }
  return out;
}

// 패턴 3 화살비 전조(Rose): 솔기 + 범위 모서리(2u) + 위에서 내려꽂히는 짧은 빛 바늘 그림자
function rainWarn() {
  const out = [], R = rng(51), pins = Array.from({ length: 9 }, (_, i) => ({ x: -80 + i * 20 + (R() - 0.5) * 8, o: R() }));
  for (let f = 0; f < 6; f++) {
    const F = new Field(104, 40, 104, 72, ROSE), ph = f / 6, pulse = 0.75 + 0.25 * Math.cos(ph * Math.PI * 2);
    seam(F, 'rose', 'cream', 2.4, 1.1 * pulse); edges(F, 60, 'rose', 0.7 * pulse);
    for (const p of pins) {
      const u = (ph + p.o) % 1, y = -64 + 60 * u, k = Math.sin(Math.PI * u);
      F.needle(p.x + 3, y - 10, p.x, y, 1.5, 'rose', 1.05 * k);
      if (u > 0.8) F.glow(p.x, -1, 4, 'cream', 1);
    }
    out.push(F.toFrame());
  }
  return out;
}

// 패턴 4 전조: 솔기 + 4u 범위 모서리 + 가운데 문양(Rose는 별, Blue는 마름모 고리)
function seqWarn(ramps, a, b, sigil) {
  const out = [];
  for (let f = 0; f < 6; f++) {
    const F = new Field(104, 72, 104, 136, ramps), ph = f / 6, pulse = 0.72 + 0.28 * Math.cos(ph * Math.PI * 2);
    seam(F, a, b, 2.8, 1.15 * pulse); edges(F, 126, b, 0.8 * pulse);
    F.needle(-HALF, -128, HALF, -128, 1.2, b, 0.45 * pulse);
    // 범위 안쪽을 타고 오르는 가는 줄기(양끝 모서리 쪽)
    [-80, -60, 60, 80].forEach((x, i) => { const u = (ph + i * 0.27) % 1, y = -6 - 110 * u, k = Math.sin(Math.PI * u); if (k > 0.2) F.needle(x, y + 5, x, y - 9, 1.4, i % 2 ? a : b, 1.05 * k); });
    sigil(F, ph, pulse);
    out.push(F.toFrame());
  }
  return out;
}
const roseSigil = (F, ph, pulse) => { F.star(0, -58, 26 + 6 * pulse, 2.8, [[90, 1], [270, 1], [0, 0.7], [180, 0.7], [45, 0.4], [135, 0.4], [225, 0.4], [315, 0.4]], 'rose', 1.1 * pulse, 1, ph * 45); };
const blueSigil = (F, ph, pulse) => {
  const s = 22 + 5 * pulse;
  [[0, -s, s, 0], [s, 0, 0, s], [0, s, -s, 0], [-s, 0, 0, -s]].forEach(([x0, y0, x1, y1], i) => F.needle(x0, y0 - 58, x1, y1 - 58, 2, i % 2 ? 'indigo' : 'steel', 1.05 * pulse));
  for (const off of [0, Math.PI]) F.crescent(0, -58, s * 0.55, s * 0.55, off + ph * Math.PI, off + ph * Math.PI + rad(130), 2.2, off ? 'indigo' : 'steel', 0.95 * pulse);
  F.glow(0, -58, 6, 'steel', 1.1 * pulse);
};

// Rose 구역 폭발: 장미가 피었다 흩날리는 모양. Blue(고리가 퍼지는 폭발)와 형태를 다르게 한다.
// 화살 베기 X자 섬광 → 꽃잎 두 겹(바깥 장밋빛, 안쪽 크림빛)이 서로 반대로 돌며 펼쳐짐 → 꽃잎 조각이 돌며 흩날리고 천천히 떨어짐.
function roseBlast({ W, H, PX, PY, zh, seed }) {
  const out = [], R = rng(seed), cy = -zh / 2, sy = zh / 192;
  const outQ = (t) => 1 - (1 - t) ** 3;
  const petals = Array.from({ length: 22 }, (_, i) => ({ ang: (i / 22) * Math.PI * 2 + R() * 0.3, sp: 0.6 + R() * 0.5, spin: (R() - 0.5) * 2.4, hue: i % 3 ? 'rose' : 'cream' }));
  // 꽃잎: 양끝이 뾰족한 렌즈(두꺼운 바늘). 가운데에서 바깥쪽을 향한다.
  const petal = (F, a, r0, r1, w, hue, k) => { const c = Math.cos(a), sn = Math.sin(a); F.needle(c * r0, cy + sn * r0 * sy * 1.3, c * r1, cy + sn * r1 * sy * 1.3, w, hue, k); };
  for (let f = 0; f < 8; f++) {
    const F = new Field(W / 2, H / 2, PX, PY, ROSE);
    // 0~2: 화살 베기 — 구역을 가로지르는 X자와 가로 섬광
    if (f <= 2) {
      const k = [1.3, 1, 0.55][f], L = [120, 130, 120][f], w = [4.5, 3.5, 2][f];
      for (const deg of [32, -32]) { const a = rad(deg), c = Math.cos(a) * L, sn = Math.sin(a) * L * sy * 1.2; F.needle(-c, cy - sn, c, cy + sn, w, 'rose', k); }
      F.needle(-L * 0.9, cy, L * 0.9, cy, w * 0.8, 'cream', k * 0.9);
      F.glow(0, cy, [30, 22, 12][f], 'cream', k * 1.05);
    }
    // 1~5: 꽃잎 두 겹이 반대로 돌며 펼쳐진다
    if (f >= 1 && f <= 5) {
      const t = (f - 1) / 4, g = outQ(Math.min(1, (f) / 3)), fade = f <= 3 ? 1 : 1 - (f - 3) / 3;
      for (let i = 0; i < 6; i++) { const a = (i / 6) * Math.PI * 2 + t * 0.9; petal(F, a, 10 + 20 * g, 46 + 62 * g, 9 * fade + 2, 'rose', 0.55 + 0.6 * fade); }
      for (let i = 0; i < 6; i++) { const a = ((i + 0.5) / 6) * Math.PI * 2 - t * 1.2; petal(F, a, 6 + 10 * g, 26 + 34 * g, 6 * fade + 1.5, 'cream', 0.6 + 0.6 * fade); }
      F.glow(0, cy, 12 * fade + 4, 'cream', 1.1 * fade + 0.2);
    }
    // 3~7: 꽃잎 조각이 돌며 흩날리고 천천히 떨어진다
    if (f >= 3) for (const p of petals) {
      const t = (f - 3) / 4, r = 50 + 70 * outQ(t) * p.sp, k = 1 - Math.max(0, (f - 5) / 3); if (k <= 0) continue;
      const x = Math.cos(p.ang) * r, y = cy + Math.sin(p.ang) * r * Math.max(0.6, sy) + 26 * t * t, a = p.ang + p.spin * (f - 3);
      F.needle(x - Math.cos(a) * 5, y - Math.sin(a) * 3, x + Math.cos(a) * 5, y + Math.sin(a) * 3, 3, p.hue, 0.55 + 0.45 * k);
    }
    out.push(F.toFrame());
  }
  return out;
}

const SHEETS = {
  'p1p3-machine-warn': [machineWarn(), 208, 80, [104, 72], Array(6).fill(80), true, '1페이즈 패턴 3 기계 발판 1초 전조(범위 6×2u), Blue·잉크'],
  'p1p3-ink-blast': [zoneBlast(INK, 'steel', 'indigo', { W: 288, H: 160, PX: 144, PY: 112, zh: 64, ink: true, seed: 41 }), 288, 160, [144, 112], BLAST_MS, false, '1페이즈 패턴 3 기계 발판 잉크 폭발(구역 6×2u 한가운데가 터짐), 짙은 잉크 + Blue 빛'],
  'p1p3-rain-warn': [rainWarn(), 208, 80, [104, 72], Array(6).fill(60), true, '1페이즈 패턴 3 나머지 발판 0.5초 화살비 전조, Rose'],
  'p1p4-warn-rose': [seqWarn(ROSE, 'rose', 'cream', roseSigil), 208, 144, [104, 136], Array(6).fill(70), true, '1페이즈 패턴 4 홀수 번호 발판 전조(범위 6×4u), Rose 별 문양'],
  'p1p4-warn-blue': [seqWarn(BLUE, 'steel', 'indigo', blueSigil), 208, 144, [104, 136], Array(6).fill(70), true, '1페이즈 패턴 4 짝수 번호 발판 전조(범위 6×4u), Blue 마름모 문양'],
  'p1p4-blast-rose': [roseBlast({ W: 288, H: 224, PX: 144, PY: 176, zh: 129, seed: 61 }), 288, 224, [144, 176], BLAST_MS, false, '1페이즈 패턴 4 홀수 번호 발판 폭발(구역 6×4.03u), Rose: 화살 베기 X자 → 장미 꽃잎 개화 → 꽃잎 흩날림'],
  'p1p4-blast-blue': [zoneBlast(BLUE, 'steel', 'indigo', { W: 288, H: 224, PX: 144, PY: 176, zh: 129, seed: 62 }), 288, 224, [144, 176], BLAST_MS, false, '1페이즈 패턴 4 짝수 번호 발판 폭발(구역 6×4.03u 한가운데가 터짐), Blue'],
};
const metaPath = path.join(ROOT, 'metadata.json'), meta = JSON.parse(fs.readFileSync(metaPath, 'utf8'));
for (const [name, [frames, w, h, pv, ms, loop, note]] of Object.entries(SHEETS)) {
  writePng(path.join(ROOT, name + '.png'), strip(frames));
  meta.assets[name] = { file: name + '.png', columns: frames.length, rows: 1, frameWidth: w, frameHeight: h, frameCount: frames.length, pivotPx: pv, pivot: [0.5, (h - pv[1]) / h], durationsMs: ms, loop, note };
}
fs.writeFileSync(metaPath, JSON.stringify(meta, null, 2) + '\n');
console.log('platform done');
