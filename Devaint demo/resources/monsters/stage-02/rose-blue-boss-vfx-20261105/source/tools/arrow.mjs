// Rose 마법 화살을 확정 이펙트의 빛 화풍으로 다시 그린다. 실행: node tools/arrow.mjs (이 폴더 기준)
// 판정 1×0.3u(32×9.6px)는 그대로이고, 그림만 바꾼다. 색은 Rose 캐릭터 그림(rose-standing)의 장밋빛(#cc647c·#94445c)과 피부 크림빛(#e4ccb4)에서 고른다.
// 오른쪽을 향한다. 본체는 모든 비행 프레임에서 같은 픽셀이고 잔상만 움직인다.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { writePng } from './png.mjs';
import { Field, hex, rad } from './glow.mjs';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const RAMP = {
  rose: [hex('#fff6f0'), hex('#f4c4cc'), hex('#cc647c'), hex('#94445c')],
  cream: [hex('#fffaf4'), hex('#f0dcc8'), hex('#d8909c'), hex('#a4546c')],
};

// 본체: 피벗(0,0)이 화살 가운데, 촉은 +17k, 꼬리는 -17k. k로 같은 디자인을 작게 다시 그린다(화살비용 0.7).
function body(F, k = 1) {
  const n = (x1, y1, x2, y2, w, hue, s = 1) => F.needle(x1 * k, y1 * k, x2 * k, y2 * k, Math.max(0.9, w * k), hue, s);
  n(-17, 0, 12, 0, 1.8, 'rose');                               // 화살대: 가는 빛 줄기
  n(6, 0, 18, 0, 3.4, 'cream');                                // 촉: 마름모 섬광, 흰 심
  n(9, -1, 3, -4.5, 1.4, 'rose', 0.9);                         // 촉 미늘: 뒤로 젖힌 짧은 바늘
  n(9, 1, 3, 4.5, 1.4, 'rose', 0.9);
  for (const s of [-1, 1]) {                                   // 깃: 꼬리에서 V자로 젖힌 빛 바늘 두 줄씩
    n(-9, s * 1, -17, s * 5.5, 1.7, 'rose', 0.95);
    n(-4, s * 1, -12, s * 5, 1.3, 'rose', 0.8);
  }
  F.glow(13 * k, 0, 4 * k, 'cream', 0.7);
}

function sheet(frames, cols) {
  const fw = frames[0].w, fh = frames[0].h, s = { w: fw * cols, h: fh * Math.ceil(frames.length / cols), data: null };
  s.data = new Uint8Array(s.w * s.h * 4);
  frames.forEach((f, i) => { const ox = (i % cols) * fw, oy = Math.floor(i / cols) * fh; for (let y = 0; y < fh; y++) s.data.set(f.data.subarray(y * fw * 4, (y + 1) * fw * 4), ((oy + y) * s.w + ox) * 4); });
  return s;
}

// 정지 본체: 셀 56×28, 피벗 (28, 14)
const B = new Field(56, 28, 28, 14, RAMP, 1); body(B);
const bodyFrame = B.toFrame();

// 비행: 셀 96×28, 피벗 (64, 14). 꼬리 뒤로 바늘 잔상 세 줄이 4프레임 주기로 흐른다.
const flight = [];
for (let f = 0; f < 4; f++) {
  const F = new Field(96, 28, 64, 14, RAMP, 1), len = [34, 40, 30, 38][f];
  F.needle(-16 - len, 0, -12, 0, 1.8, 'rose', 0.85);
  F.needle(-14 - len * 0.6, -4, -18, -4, 1.0, 'rose', 0.6);
  F.needle(-14 - len * 0.55, 4, -18, 4, 1.0, 'rose', 0.6);
  // 흩어지는 짧은 바늘: 4프레임 × 8px = 32px 주기
  for (const [base, dy] of [[0, -7], [16, 7], [8, -2], [24, 3]]) { const x = -22 - ((base + f * 8) % 32); F.needle(x, dy, x - 5, dy, 0.9, 'cream', 0.6); }
  body(F);
  flight.push(F.toFrame());
}
// 본체 픽셀이 프레임마다 같은지: 잔상이 본체 위에 덮이지 않게 본체를 마지막에 그리고, 같은 세기 버퍼 규칙(최댓값)을 쓴다.

// 화살비용 작은 화살: 셀 64×20, 피벗 (44, 10), 길이 약 24px(0.75u). 4프레임 × 6px = 24px 주기의 짧은 잔상.
const rain = [];
for (let f = 0; f < 4; f++) {
  const F = new Field(64, 20, 44, 10, RAMP, 1), len = [20, 24, 18, 22][f];
  F.needle(-12 - len, 0, -9, 0, 1.3, 'rose', 0.85);
  for (const [base, dy] of [[0, -4], [12, 4]]) { const x = -16 - ((base + f * 6) % 24); F.needle(x, dy, x - 4, dy, 0.9, 'cream', 0.6); }
  body(F, 0.7);
  rain.push(F.toFrame());
}
writePng(path.join(ROOT, 'rose-rain-arrow-flight.png'), sheet(rain, 4));
writePng(path.join(ROOT, 'rose-arrow-body.png'), bodyFrame);
writePng(path.join(ROOT, 'rose-arrow-flight.png'), sheet(flight, 4));
const metaPath = path.join(ROOT, 'metadata.json'), meta = JSON.parse(fs.readFileSync(metaPath, 'utf8'));
meta.assets.roseArrowBody = { file: 'rose-arrow-body.png', columns: 1, rows: 1, frameWidth: 56, frameHeight: 28, frameCount: 1, pivotPx: [28, 14], pivot: [0.5, 0.5], note: 'Rose 마법 화살 빛 화풍, 판정 1×0.3u 그대로' };
meta.assets.roseArrowFlight = { file: 'rose-arrow-flight.png', columns: 4, rows: 1, frameWidth: 96, frameHeight: 28, frameCount: 4, pivotPx: [64, 14], pivot: [64 / 96, 0.5], durationsMs: [50, 50, 50, 50], loop: true, note: '본체 고정, 꼬리 뒤 잔상만 순환' };
meta.assets.roseRainArrowFlight = { file: 'rose-rain-arrow-flight.png', columns: 4, rows: 1, frameWidth: 64, frameHeight: 20, frameCount: 4, pivotPx: [44, 10], pivot: [44 / 64, 0.5], durationsMs: [50, 50, 50, 50], loop: true, note: '화살비용 작은 화살(0.7배 비율로 다시 그림), 본체 고정' };
fs.writeFileSync(metaPath, JSON.stringify(meta, null, 2) + '\n');
console.log('rose arrow done');
