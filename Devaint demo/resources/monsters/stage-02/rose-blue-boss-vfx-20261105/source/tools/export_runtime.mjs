// Rose·Blue 보스 VFX 게임용 세트를 만든다: 빛 화풍 원본(source/)을 다크 판타지로 다시 칠해 runtime/에 쓰고,
// Unity가 시트를 자를 때 쓰는 runtime/sheets.json(셀·프레임·피벗·시간)을 함께 쓴다.
// 실행: node source/tools/export_runtime.mjs (에셋 폴더 기준)
//   - 셀은 왼쪽 위부터 행 우선, 피벗은 셀 왼쪽 위 기준 px, 32px = 1u.
//   - pixelfx 압박 벽은 처음부터 다크 판타지로 그린 1배 격자 시트를 2배로 키우기만 한다. 3페이즈 유도 화살·맺힘·회오리도 처음부터 다크 판타지라
//     그대로 쓰고(맺힘은 2배로 키움), 열 폭발 기둥(p3-pillar)만 빛 화풍 원본을 다시 칠한다.
//   - 회오리 곁들임은 85프레임이라 2배로 키우면 텍스처 한도(4096)를 넘어 반 해상도로 두고, sheets.json의 scale(2)로 2배로 그린다.
//   - *-white는 백색화 덮개: 다크 판타지 결과의 알파만 남긴 흰 실루엣.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { readPng, writePng } from './png.mjs';
import { darkFantasy } from './darkfantasy.mjs';

const GROUP = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const SRC = path.join(GROUP, 'source'), OUT = path.join(GROUP, 'runtime');
const MS = (n, ms) => Array(n).fill(ms);
const PLAT = [30, 40, 50, 60, 60, 70, 80, 90], BURST = [30, 40, 40, 50, 60, 60, 70, 80];
// name, source(source/ 기준), 열, 행, 프레임, 셀 폭·높이, 피벗 x·y, 프레임 ms, 반복, 옵션
const SHEETS = [
  ['rose-arrow-flight', 'rose-arrow-flight.png', 4, 1, 4, 96, 28, 64, 14, MS(4, 50), true, { thin: true, white: true }],
  ['rose-rain-arrow-flight', 'rose-rain-arrow-flight.png', 4, 1, 4, 64, 20, 44, 10, MS(4, 50), true, { thin: true }],
  ['rose-arrow-impact', 'inputs/rose_bolt_impact.png', 7, 1, 7, 64, 64, 32, 32, [40, 40, 50, 50, 60, 60, 70], false],
  ['rose-rain-impact', 'inputs/rose_bolt_rain_impact.png', 5, 1, 5, 24, 24, 12.5, 16.5, MS(5, 30), false],
  ['snipe-hit', 'snipe-hit-b.png', 8, 1, 8, 128, 128, 64, 64, [30, 30, 40, 40, 50, 60, 70, 80], false, { thin: true }],
  ['blue-orb-trail', 'blue-orb-glow-trail.png', 6, 1, 6, 128, 80, 92, 40, MS(6, 70), true, { white: true }],
  ['blue-orb-body', 'blue-orb-glow-body.png', 6, 1, 6, 80, 80, 40, 40, MS(6, 70), true, { white: true }],
  ['orb-hit', 'orb-hit.png', 8, 1, 8, 112, 112, 56, 56, BURST, false],
  ['trap-warn', 'trap-warn.png', 6, 1, 6, 208, 48, 104, 40, MS(6, 80), true],
  ['trap-snap', 'trap-snap.png', 10, 1, 10, 224, 144, 112, 136, [40, 40, 40, 60, 50, 60, 70, 80, 90, 100], false],
  ['machine-warn', 'p1p3-machine-warn.png', 6, 1, 6, 208, 80, 104, 72, MS(6, 80), true],
  ['machine-ink-blast', 'p1p3-ink-blast.png', 8, 1, 8, 288, 160, 144, 112, PLAT, false],
  ['rain-warn', 'p1p3-rain-warn.png', 6, 1, 6, 208, 80, 104, 72, MS(6, 60), true],
  ['sequence-warn-rose', 'p1p4-warn-rose.png', 6, 1, 6, 208, 144, 104, 136, MS(6, 70), true],
  ['sequence-warn-blue', 'p1p4-warn-blue.png', 6, 1, 6, 208, 144, 104, 136, MS(6, 70), true],
  ['sequence-blast-rose', 'p1p4-blast-rose.png', 8, 1, 8, 288, 224, 144, 176, PLAT, false],
  ['sequence-blast-blue', 'p1p4-blast-blue.png', 8, 1, 8, 288, 224, 144, 176, PLAT, false],
  ['ink-charge', 'ink-charge.png', 8, 1, 8, 128, 128, 64, 64, MS(8, 64), false],
  ['ink-fireball', 'ink-fireball.png', 6, 1, 6, 224, 112, 144, 56, MS(6, 70), true],
  ['ink-blast', 'ink-blast.png', 9, 1, 9, 192, 192, 96, 96, [30, 40, 50, 60, 60, 70, 80, 90, 100], false],
  ['blue-vanish', 'blue-vanish.png', 8, 1, 8, 128, 176, 64, 168, [30, 40, 40, 50, 50, 60, 60, 70], false],
  ['dive-trail', 'dive-sheet.png', 6, 1, 6, 96, 224, 48, 214, [20, 20, 20, 20, 30, 30], false],
  ['dive-impact', 'impact-sheet.png', 4, 2, 8, 208, 112, 104, 96, [20, 30, 40, 50, 60, 70, 70, 80], false],
  ['dive-root', 'root-sheet.png', 4, 3, 12, 112, 96, 56, 88, [30, 40, 50, 90, 90, 90, 90, 90, 90, 50, 50, 60], false],
  ['exchange-flash', 'inputs/exchange-flash-v4.png', 4, 4, 16, 512, 256, 256, 128, [20, 20, 30, 40, 40, 50, 50, 60, 60, 70, 70, 80, 80, 90, 90, 100], false],
  ['p3-arrow-flight', 'p3-arrow-glow-flight.png', 4, 1, 4, 96, 28, 64, 14, MS(4, 50), true, { white: true }],
  ['p3-arrow-impact', 'inputs/phase3_bolt_impact.png', 7, 1, 7, 64, 64, 32, 32, [40, 40, 50, 50, 60, 60, 70], false],
  ['p3-blast', 'p3-blast.png', 8, 1, 8, 112, 112, 56, 56, BURST, false],
  ['press-rose', 'pixelfx/press-rose.png', 6, 11, 61, 496, 264, 32, 248, MS(61, 1000 / 30), false, { native: true, upscale: 2 }],
  ['press-blue', 'pixelfx/press-blue.png', 6, 11, 61, 520, 264, 32, 248, MS(61, 1000 / 30), false, { native: true, upscale: 2 }],
  // 3페이즈(2026-11-05 확정): 유도 화살(보라 단색)·전조 맺힘, 순차 낙하 화살의 열 폭발 기둥, 확장 광역기 화살 회오리 곁들임(뒤·앞 두 장).
  ['homing-bolt', 'pixelfx/homing-bolt.png', 2, 2, 4, 128, 48, 64, 24, MS(4, 1000 / 15), true, { native: true }],
  ['homing-form', 'pixelfx/homing-form.png', 4, 4, 16, 240, 144, 120, 72, MS(16, 1000 / 30), false, { native: true, upscale: 2 }],
  ['p3-pillar', 'pixelfx/p3-pillar.png', 7, 3, 18, 208, 488, 104, 478, MS(18, 1000 / 30), false],
  ['aoe-whirl-back', 'pixelfx/aoe-whirl-back.png', 8, 11, 85, 440, 268, 220, 254, MS(85, 1000 / 30), false, { native: true, scale: 2 }],
  ['aoe-whirl-front', 'pixelfx/aoe-whirl-front.png', 8, 11, 85, 440, 268, 220, 254, MS(85, 1000 / 30), false, { native: true, scale: 2 }],
  // 2페이즈 패턴 4 양옆 압박 Blue 잉크 회오리(2026-11-05 확정): aoe-whirl 곁들임을 압박 크기·Blue 색·한 덩어리 띠로(ink-whirl.js). 반 해상도, scale 2.
  ['ink-whirl-back', 'pixelfx/ink-whirl-back.png', 6, 10, 55, 240, 152, 120, 138, MS(55, 1000 / 30), false, { native: true, scale: 2 }],
  ['ink-whirl-front', 'pixelfx/ink-whirl-front.png', 6, 10, 55, 240, 152, 120, 138, MS(55, 1000 / 30), false, { native: true, scale: 2 }],
];

function upscale(im, k) {
  const o = { w: im.w * k, h: im.h * k, data: new Uint8Array(im.w * k * im.h * k * 4) };
  for (let y = 0; y < o.h; y++) for (let x = 0; x < o.w; x++) o.data.set(im.data.subarray(((((y / k) | 0) * im.w) + ((x / k) | 0)) * 4, ((((y / k) | 0) * im.w) + ((x / k) | 0)) * 4 + 4), (y * o.w + x) * 4);
  return o;
}
function white(im) { const o = { w: im.w, h: im.h, data: new Uint8Array(im.data) }; for (let i = 0; i < o.data.length; i += 4) if (o.data[i + 3]) o.data[i] = o.data[i + 1] = o.data[i + 2] = 255; return o; }

fs.mkdirSync(OUT, { recursive: true });
const table = [];
for (const [name, src, columns, rows, frames, cellWidth, cellHeight, pivotX, pivotY, ms, loop, opt = {}] of SHEETS) {
  let im = readPng(path.join(SRC, src));
  if (opt.upscale) im = upscale(im, opt.upscale);
  if (!opt.native) im = darkFantasy(im, { thin: !!opt.thin });
  if (im.w < columns * cellWidth || im.h < rows * cellHeight) throw new Error(`${name}: ${im.w}x${im.h} is smaller than ${columns}x${rows} cells of ${cellWidth}x${cellHeight}`);
  writePng(path.join(OUT, name + '.png'), im);
  const row = { name, columns, rows, frames, cellWidth, cellHeight, pivotX, pivotY, durationsMs: ms.map((v) => Math.round(v * 1000) / 1000), loop, ...(opt.scale ? { scale: opt.scale } : {}) };
  table.push(row);
  if (opt.white) { writePng(path.join(OUT, name + '-white.png'), white(im)); table.push({ ...row, name: name + '-white' }); }
}
fs.writeFileSync(path.join(OUT, 'sheets.json'), JSON.stringify({ format: 'deviant-rose-blue-vfx-sheets-v1', pixelsPerUnit: 32, order: 'top-left row-major', pivot: 'px from cell top-left', sheets: table }, null, 2) + '\n');
console.log('runtime sheets', table.length);
