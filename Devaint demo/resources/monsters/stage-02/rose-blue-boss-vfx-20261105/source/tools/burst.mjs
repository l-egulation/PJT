// 빛 화풍 폭발 두 가지를 같은 모양 규칙으로 색만 다르게 만든다. 실행: node tools/burst.mjs (이 폴더 기준)
//   p3-blast: 3페이즈 패턴 2 튕기는 화살의 T=5 지연 폭발(2×2u). 융합 Blue 색(청보라 + 장밋빛), 백색화한 화살에서 이어지도록 흰 섬광으로 시작.
//   orb-hit: 1페이즈 패턴 1 유도 구체가 기계에 닿을 때. Blue 색(강철빛 + 남색).
// 흰 섬광 → 별 섬광과 좌우로 색이 나뉜 초승달 고리가 퍼짐 → 바늘 파편이 흩어지며 사라짐. 셀 112×112, 피벗 가운데, 8프레임.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { writePng } from './png.mjs';
import { Field, hex, rad, rng } from './glow.mjs';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const SETS = {
  'p3-blast': { a: 'violet', b: 'rose', ramps: { violet: [hex('#f8f8f8'), hex('#d8d0f8'), hex('#6858b8'), hex('#4838a8')], rose: [hex('#f8f8f8'), hex('#f8d8e0'), hex('#c85898'), hex('#983888')] }, whiteStart: true },
  'orb-hit': { a: 'steel', b: 'indigo', ramps: { steel: [hex('#f4f8fc'), hex('#c4d8ec'), hex('#6890bc'), hex('#3c5c84')], indigo: [hex('#eef0fa'), hex('#b8c0e4'), hex('#5464a4'), hex('#34447c')] }, whiteStart: false },
};
const MS = [30, 40, 40, 50, 60, 60, 70, 80];
const outQ = (t) => 1 - (1 - t) ** 4;

function burst({ a, b, ramps, whiteStart }) {
  const R = rng(21), frames = [];
  const shards = Array.from({ length: 12 }, (_, i) => ({ ang: (i / 12) * Math.PI * 2 + R() * 0.4, sp: 0.7 + R() * 0.4, hue: i % 2 ? b : a }));
  for (let f = 0; f < 8; f++) {
    const F = new Field(56, 56, 56, 56, ramps);
    // 흰 섬광 덩어리: 첫 프레임은 크고 하얗게(백색화한 화살에서 이어짐)
    if (f === 0) { F.glow(0, 0, whiteStart ? 26 : 18, a, 1.25); F.glow(0, 0, 10, b, 1.3); }
    // 별 섬광: 좌우로 길게, 위아래는 짧게. 1~3프레임
    if (f >= 0 && f <= 3) {
      const L = [14, 40, 34, 20][f], W = [3, 6, 5, 3][f];
      F.star(0, 0, L, W, [[0, 1], [180, 1], [90, 0.6], [270, 0.6], [45, 0.5], [135, 0.5], [225, 0.5], [315, 0.5]], a, 1);
    }
    // 좌우로 색이 나뉜 초승달 고리: 왼쪽 a, 오른쪽 b
    if (f >= 1 && f <= 6) {
      const k = (f - 1) / 5, r = 10 + 34 * outQ(k), fade = 1 - Math.max(0, (f - 4) / 3), w = 6 * fade + 1.4;
      F.crescent(0, 0, r, r * 0.82, rad(110), rad(250), w, a, 0.3 + 0.8 * fade);
      F.crescent(0, 0, r, r * 0.82, rad(-70), rad(70), w, b, 0.3 + 0.8 * fade);
    }
    // 바늘 파편
    if (f >= 2) for (const s of shards) {
      const r = 14 + 38 * outQ((f - 2) / 5) * s.sp, k = 1 - Math.max(0, (f - 5) / 3); if (k <= 0) continue;
      const x = Math.cos(s.ang) * r, y = Math.sin(s.ang) * r;
      F.needle(x - Math.cos(s.ang) * 3, y - Math.sin(s.ang) * 3, x + Math.cos(s.ang) * 9, y + Math.sin(s.ang) * 9, 2.4, s.hue, 0.55 + 0.45 * k);
    }
    frames.push(F.toFrame());
  }
  const s = { w: 112 * 8, h: 112, data: new Uint8Array(112 * 8 * 112 * 4) };
  frames.forEach((fr, i) => { for (let y = 0; y < 112; y++) s.data.set(fr.data.subarray(y * 112 * 4, (y + 1) * 112 * 4), (y * s.w + i * 112) * 4); });
  return s;
}

const metaPath = path.join(ROOT, 'metadata.json'), meta = JSON.parse(fs.readFileSync(metaPath, 'utf8'));
for (const [name, set] of Object.entries(SETS)) {
  writePng(path.join(ROOT, name + '.png'), burst(set));
  meta.assets[name === 'p3-blast' ? 'p3Blast' : 'orbHit'] = { file: name + '.png', columns: 8, rows: 1, frameWidth: 112, frameHeight: 112, frameCount: 8, pivotPx: [56, 56], pivot: [0.5, 0.5], durationsMs: MS, loop: false,
    note: name === 'p3-blast' ? '3페이즈 튕기는 화살 지연 폭발(2×2u), 청보라·장밋빛, 흰 섬광 시작' : '1페이즈 유도 구체가 기계에 닿을 때, Blue 강철빛·남색' };
}
fs.writeFileSync(metaPath, JSON.stringify(meta, null, 2) + '\n');
console.log('bursts done');
