// 1페이즈 패턴 2 추적 저격 적중 시안 세 가지. 뭉치고 뭉툭했던 v1 적중 효과 대신, 가늘고 날카롭고 속이 빈 형태로 만든다.
// 1px 해상도(scale 1)로 그려 선을 가늘게 유지한다. 색은 Rose(장밋빛·크림빛). 화면에서는 다크 판타지로 다시 칠해 쓴다.
//   snipe-hit-a 관통: 가로로 길게 꿰뚫는 선 + 가는 X 베기 + 사방으로 갈라지는 실금 + 가는 파편
//   snipe-hit-b 깨진 고리: 흰 점 섬광 → 끊긴 원호 두 겹이 반대로 돌며 퍼짐 → 불씨가 꼬리를 끌며 튐(가운데는 비어 있음)
//   snipe-hit-c 가시 개화: 굽은 가시 일곱 갈래가 소용돌이로 뻗고 끝에서 꽃잎 조각이 흩날림
// 셀 128×128, 피벗 (64, 64), 8프레임 30·30·40·40·50·60·70·80ms. 실행: node tools/snipe.mjs (이 폴더 기준)
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { writePng } from './png.mjs';
import { Field, hex, rad, rng } from './glow.mjs';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const ROSE = { rose: [hex('#fff6f0'), hex('#f4c4cc'), hex('#cc647c'), hex('#94445c')], cream: [hex('#fffaf4'), hex('#f0dcc8'), hex('#d8909c'), hex('#a4546c')] };
const MS = [30, 30, 40, 40, 50, 60, 70, 80];
const N = 8, C = 64;
const outQ = (t) => 1 - (1 - t) ** 3;
const field = () => new Field(128, 128, C, C, ROSE, 1);
const ray = (F, a, r0, r1, w, hue, k) => F.needle(Math.cos(a) * r0, Math.sin(a) * r0, Math.cos(a) * r1, Math.sin(a) * r1, w, hue, k);

function pierce() {
  const out = [], R = rng(3), cracks = Array.from({ length: 9 }, (_, i) => ({ a: (i / 9) * Math.PI * 2 + (R() - 0.5) * 0.5, len: 26 + R() * 18, bend: (R() < 0.5 ? -1 : 1) * (0.3 + R() * 0.25) })), bits = Array.from({ length: 14 }, () => ({ a: R() * Math.PI * 2, sp: 0.6 + R() * 0.6 }));
  for (let f = 0; f < N; f++) {
    const F = field(), k = 1 - Math.max(0, (f - 3) / 5);
    // 꿰뚫는 선: 0~2 가장 길고, 이후 양끝부터 짧아진다
    const L = [58, 62, 54, 40, 26, 14, 0, 0][f];
    if (L) { F.needle(-L, 0, L, 0, [1.8, 1.6, 1.3, 1.1, 1, 0.9][f], 'cream', 1.1); F.needle(-L * 0.4, 0, L * 0.4, 0, 1.2, 'rose', 0.8); }
    if (f <= 1) F.glow(0, 0, 6 - f * 2, 'cream', 1.3);
    // 가는 X 베기
    if (f >= 1 && f <= 3) for (const d of [35, -35, 145, -145]) ray(F, rad(d), 3, [22, 30, 26][f - 1], 1, 'rose', [1.1, 0.9, 0.6][f - 1]);
    // 실금: 가운데서 바깥으로 갈라짐(꺾임 하나)
    if (f >= 1 && f <= 6) for (const c of cracks) {
      const g = outQ(Math.min(1, f / 3)) * c.len, m = g * 0.55, b = c.a + c.bend;
      ray(F, c.a, 4, m, 0.8, 'rose', 0.9 * k);
      F.needle(Math.cos(c.a) * m, Math.sin(c.a) * m, Math.cos(c.a) * m + Math.cos(b) * (g - m), Math.sin(c.a) * m + Math.sin(b) * (g - m), 0.7, 'rose', 0.75 * k);
    }
    // 가는 파편
    if (f >= 2) for (const p of bits) { const r = 10 + 44 * outQ((f - 2) / 5) * p.sp; ray(F, p.a, r, r + 4, 0.8, 'cream', 0.9 * k); }
    out.push(F.toFrame());
  }
  return out;
}

function ring() {
  const out = [], R = rng(5), sparks = Array.from({ length: 12 }, (_, i) => ({ a: (i / 12) * Math.PI * 2 + R() * 0.3, sp: 0.7 + R() * 0.5 }));
  for (let f = 0; f < N; f++) {
    const F = field(), k = 1 - Math.max(0, (f - 3) / 5);
    if (f <= 1) { F.glow(0, 0, 5, 'cream', 1.3); F.star(0, 0, f ? 22 : 14, 1.2, [[0, 1], [90, 1], [180, 1], [270, 1]], 'cream', 1.1); }
    // 끊긴 원호 두 겹: 바깥 장밋빛 6조각, 안쪽 크림빛 4조각, 서로 반대로 돈다
    if (f >= 1 && f <= 7) {
      const t = (f - 1) / 6, r1 = 12 + 42 * outQ(t), r2 = 8 + 26 * outQ(t), w = 1.6 - t;
      for (let i = 0; i < 6; i++) { const a = (i / 6) * Math.PI * 2 + t * 0.8; F.crescent(0, 0, r1, r1, a, a + rad(38), w + 0.4, 'rose', 1 * k + 0.1); }
      if (f <= 5) for (let i = 0; i < 4; i++) { const a = (i / 4) * Math.PI * 2 - t * 1.4 + 0.4; F.crescent(0, 0, r2, r2, a, a + rad(50), w, 'cream', 1 * k); }
    }
    // 불씨: 꼬리를 끌며 튐
    if (f >= 1) for (const s of sparks) { const r = 8 + 50 * outQ((f - 1) / 6) * s.sp; ray(F, s.a, r - 7 * k, r, 0.9, 'cream', 1 * k); F.glow(Math.cos(s.a) * r, Math.sin(s.a) * r, 1.6, 'cream', 1.1 * k); }
    out.push(F.toFrame());
  }
  return out;
}

function thorn() {
  const out = [], R = rng(7), petals = Array.from({ length: 10 }, () => ({ a: R() * Math.PI * 2, sp: 0.6 + R() * 0.5, spin: (R() - 0.5) * 2 }));
  for (let f = 0; f < N; f++) {
    const F = field(), k = 1 - Math.max(0, (f - 4) / 4);
    if (f <= 1) F.glow(0, 0, 5, 'cream', 1.3);
    // 굽은 가시 일곱 갈래: 짧은 마디를 이어 소용돌이로 뻗는다(뿌리 굵고 끝 가늚)
    if (f >= 0 && f <= 6) for (let i = 0; i < 7; i++) {
      const g = outQ(Math.min(1, (f + 1) / 3)), segs = 5; let x = 0, y = 0, a = (i / 7) * Math.PI * 2;
      for (let n = 0; n < segs; n++) {
        const l = 8 * g, nx = x + Math.cos(a) * l, ny = y + Math.sin(a) * l;
        F.needle(x, y, nx, ny, 1.6 - n * 0.25, n < 2 ? 'rose' : 'cream', (1 - n * 0.08) * k); x = nx; y = ny; a += 0.28;
        if (n === 2) F.needle(x, y, x + Math.cos(a - 0.9) * 5 * g, y + Math.sin(a - 0.9) * 5 * g, 0.8, 'rose', 0.9 * k); // 곁가시
      }
    }
    // 꽃잎 조각
    if (f >= 3) for (const p of petals) { const r = 30 + 26 * outQ((f - 3) / 4) * p.sp, x = Math.cos(p.a) * r, y = Math.sin(p.a) * r + 4 * (f - 3), a = p.a + p.spin * (f - 3); F.needle(x - Math.cos(a) * 3, y - Math.sin(a) * 2, x + Math.cos(a) * 3, y + Math.sin(a) * 2, 1.8, 'rose', 1 * k + 0.1); }
    out.push(F.toFrame());
  }
  return out;
}

function strip(frames) {
  const fw = frames[0].w, fh = frames[0].h, s = { w: fw * frames.length, h: fh, data: new Uint8Array(fw * frames.length * fh * 4) };
  frames.forEach((fr, i) => { for (let y = 0; y < fh; y++) s.data.set(fr.data.subarray(y * fw * 4, (y + 1) * fw * 4), (y * s.w + i * fw) * 4); });
  return s;
}
const metaPath = path.join(ROOT, 'metadata.json'), meta = JSON.parse(fs.readFileSync(metaPath, 'utf8'));
for (const [name, fn, note] of [['snipe-hit-a', pierce, '관통: 꿰뚫는 선 + X 베기 + 실금 + 가는 파편'], ['snipe-hit-b', ring, '깨진 고리: 끊긴 원호 두 겹 + 꼬리 불씨, 가운데 빔'], ['snipe-hit-c', thorn, '가시 개화: 굽은 가시 일곱 갈래 + 꽃잎 조각']]) {
  writePng(path.join(ROOT, name + '.png'), strip(fn()));
  meta.assets[name] = { file: name + '.png', columns: N, rows: 1, frameWidth: 128, frameHeight: 128, frameCount: N, pivotPx: [C, C], pivot: [0.5, 0.5], durationsMs: MS, loop: false, note: '1페이즈 패턴 2 추적 저격 적중 시안 ' + note };
}
fs.writeFileSync(metaPath, JSON.stringify(meta, null, 2) + '\n');
console.log('snipe done');
