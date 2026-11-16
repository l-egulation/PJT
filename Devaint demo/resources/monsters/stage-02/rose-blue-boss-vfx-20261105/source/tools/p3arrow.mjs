// 3페이즈 융합 Blue 화살(튕김·낙하·부채꼴, 1×0.3u)과 유도 화살 후보(3×1u)를 확정 이펙트의 빛 화풍으로 다시 그린다.
// 실행: node tools/p3arrow.mjs (이 폴더 기준). 판정 크기는 그대로, 그림만 바꾼다.
// 색: 융합 Blue라서 확정 이펙트(교환탄 섬광 v4)와 같은 청보라·장밋빛 두 계열. 화살대·촉은 청보라, 깃·미늘은 장밋빛으로 나눈다.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { writePng } from './png.mjs';
import { Field, hex } from './glow.mjs';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const RAMP = {
  violet: [hex('#f8f8f8'), hex('#d8d0f8'), hex('#6858b8'), hex('#4838a8')],
  rose: [hex('#f8f8f8'), hex('#f8d8e0'), hex('#c85898'), hex('#983888')],
};

// 본체: 피벗(0,0)이 가운데, 오른쪽을 향한다. k배로 같은 디자인을 키운다(유도 화살 후보는 2.8).
function body(F, k = 1) {
  const n = (x1, y1, x2, y2, w, hue, s = 1) => F.needle(x1 * k, y1 * k, x2 * k, y2 * k, Math.max(0.9, w * k), hue, s);
  n(-17, 0, 12, 0, 2, 'violet');                               // 화살대
  n(5, 0, 19, 0, 3.8, 'violet');                               // 촉: 청보라 섬광, 흰 심
  n(9, -1, 2, -5, 1.5, 'rose', 0.95);                          // 촉 미늘: 장밋빛
  n(9, 1, 2, 5, 1.5, 'rose', 0.95);
  for (const s of [-1, 1]) {                                   // 깃: 바깥은 장밋빛, 안쪽은 청보라
    n(-9, s * 1, -17, s * 6, 1.8, 'rose');
    n(-4, s * 1, -12, s * 5, 1.4, 'violet', 0.85);
  }
  n(-2, 0, 2, 0, 2.6, 'rose', 0.9);                            // 가운데 장밋빛 룬 매듭
  F.glow(14 * k, 0, 4.5 * k, 'violet', 0.75);
}
function trail(F, len, f, k = 1) {
  F.needle((-16 - len) * k, 0, -12 * k, 0, 1.8 * k, 'violet', 0.85);
  F.needle((-14 - len * 0.6) * k, -4 * k, -18 * k, -4 * k, Math.max(0.9, 1 * k), 'rose', 0.65);
  F.needle((-14 - len * 0.55) * k, 4 * k, -18 * k, 4 * k, Math.max(0.9, 1 * k), 'violet', 0.65);
  for (const [base, dy, hue] of [[0, -7, 'rose'], [16, 7, 'violet'], [8, -2, 'violet'], [24, 3, 'rose']]) { const x = (-22 - ((base + f * 8) % 32)) * k; F.needle(x, dy * k, x - 5 * k, dy * k, Math.max(0.9, 0.9 * k), hue, 0.6); }
}
function sheet(frames, cols) {
  const fw = frames[0].w, fh = frames[0].h, s = { w: fw * cols, h: fh * Math.ceil(frames.length / cols) };
  s.data = new Uint8Array(s.w * s.h * 4);
  frames.forEach((fr, i) => { const ox = (i % cols) * fw, oy = Math.floor(i / cols) * fh; for (let y = 0; y < fh; y++) s.data.set(fr.data.subarray(y * fw * 4, (y + 1) * fw * 4), ((oy + y) * s.w + ox) * 4); });
  return s;
}

const out = {};
// 1×0.3u 화살: 본체 56×28 (28,14), 비행 96×28 (64,14), 4프레임 50ms
{ const B = new Field(56, 28, 28, 14, RAMP, 1); body(B); out.p3Body = B.toFrame(); }
out.p3Flight = sheet([0, 1, 2, 3].map((f) => { const F = new Field(96, 28, 64, 14, RAMP, 1); trail(F, [34, 40, 30, 38][f], f); body(F); return F.toFrame(); }), 4);
// 3×1u 유도 화살 후보: 같은 디자인을 2.8배 비율로 1px 단위로 다시 그린다. 본체 128×56 (64,28), 비행 288×56 (184,28)
{ const B = new Field(128, 56, 64, 28, RAMP, 1); body(B, 2.8); out.homingBody = B.toFrame(); }
out.homingFlight = sheet([0, 1, 2, 3].map((f) => { const F = new Field(288, 56, 184, 28, RAMP, 1); trail(F, [34, 40, 30, 38][f], f, 2.8); body(F, 2.8); return F.toFrame(); }), 4);

writePng(path.join(ROOT, 'p3-arrow-glow-body.png'), out.p3Body);
writePng(path.join(ROOT, 'p3-arrow-glow-flight.png'), out.p3Flight);
writePng(path.join(ROOT, 'p3-homing-glow-body.png'), out.homingBody);
writePng(path.join(ROOT, 'p3-homing-glow-flight.png'), out.homingFlight);
const metaPath = path.join(ROOT, 'metadata.json'), meta = JSON.parse(fs.readFileSync(metaPath, 'utf8'));
meta.assets.p3ArrowBody = { file: 'p3-arrow-glow-body.png', columns: 1, rows: 1, frameWidth: 56, frameHeight: 28, frameCount: 1, pivotPx: [28, 14], pivot: [0.5, 0.5], note: '3페이즈 화살 빛 화풍, 판정 1×0.3u 그대로' };
meta.assets.p3ArrowFlight = { file: 'p3-arrow-glow-flight.png', columns: 4, rows: 1, frameWidth: 96, frameHeight: 28, frameCount: 4, pivotPx: [64, 14], pivot: [64 / 96, 0.5], durationsMs: [50, 50, 50, 50], loop: true, note: '본체 고정, 잔상만 순환' };
meta.assets.p3HomingBody = { file: 'p3-homing-glow-body.png', columns: 1, rows: 1, frameWidth: 128, frameHeight: 56, frameCount: 1, pivotPx: [64, 28], pivot: [0.5, 0.5], note: '유도 화살 후보(디자인 보류), 판정 3×1u' };
meta.assets.p3HomingFlight = { file: 'p3-homing-glow-flight.png', columns: 4, rows: 1, frameWidth: 288, frameHeight: 56, frameCount: 4, pivotPx: [184, 28], pivot: [184 / 288, 0.5], durationsMs: [50, 50, 50, 50], loop: true, note: '유도 화살 후보, 본체 고정' };
fs.writeFileSync(metaPath, JSON.stringify(meta, null, 2) + '\n');
console.log('p3 arrows done');
