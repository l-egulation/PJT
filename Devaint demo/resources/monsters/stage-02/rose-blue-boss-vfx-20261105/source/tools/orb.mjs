// Blue 잉크 구체를 확정 이펙트의 빛 화풍으로 다시 그린다. 실행: node tools/orb.mjs (이 폴더 기준)
// 판정 2×2u(64px)는 그대로. 색은 Blue 캐릭터의 로브 강철빛 파랑(steel)과 그림자 남색(indigo).
// 몸체: 왼쪽 위 밝은 초승달 테두리 + 오른쪽 아래 남색 테두리 + 반투명 빛 덩어리 + 왼쪽 위 흰 별 섬광. 회전하지 않는다.
// 꼬리: 진행 반대쪽(왼쪽)으로 끌리는 바늘 줄기. 진행 방향으로 돌려 쓴다. 둘 다 6프레임, 70ms 루프.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { writePng } from './png.mjs';
import { Field, hex, rad } from './glow.mjs';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const RAMP = {
  steel: [hex('#f4f8fc'), hex('#c4d8ec'), hex('#6890bc'), hex('#3c5c84')],
  indigo: [hex('#eef0fa'), hex('#b8c0e4'), hex('#5464a4'), hex('#34447c')],
};
const F_N = 6, R = 29;

function sheet(frames, cols) {
  const fw = frames[0].w, fh = frames[0].h, s = { w: fw * cols, h: fh * Math.ceil(frames.length / cols) };
  s.data = new Uint8Array(s.w * s.h * 4);
  frames.forEach((f, i) => { const ox = (i % cols) * fw, oy = Math.floor(i / cols) * fh; for (let y = 0; y < fh; y++) s.data.set(f.data.subarray(y * fw * 4, (y + 1) * fw * 4), ((oy + y) * s.w + ox) * 4); });
  return s;
}

// 구 안쪽: 투명하지 않게 Blue 캐릭터의 남색 계열로 채운다. 2px 계단 명암, 빛은 왼쪽 위.
// 빛 화풍 그림(테두리·반사선·별 섬광)은 이 바탕 위에 겹쳐진다.
const INSIDE = [hex('#4c6c98'), hex('#3c5884'), hex('#2e4470'), hex('#22325a')];
function fillInside(frame, cx, cy, r) {
  for (let y = 0; y < frame.h; y++) for (let x = 0; x < frame.w; x++) {
    const qx = Math.floor(x / 2) * 2 + 1 - cx, qy = Math.floor(y / 2) * 2 + 1 - cy;
    if (Math.hypot(qx, qy) > r) continue;
    const k = Math.hypot(qx + r * 0.38, qy + r * 0.38) / (r * 1.75), base = INSIDE[k < 0.3 ? 0 : k < 0.55 ? 1 : k < 0.8 ? 2 : 3];
    const o = (y * frame.w + x) * 4, a = frame.data[o + 3] / 255;
    for (let c = 0; c < 3; c++) frame.data[o + c] = Math.round(frame.data[o + c] * a + base[c] * (1 - a));
    frame.data[o + 3] = 255;
  }
  return frame;
}

// 몸체: 셀 80×80, 피벗 (40, 40). 테두리 초승달이 6프레임에 걸쳐 조금씩 숨쉬고, 별 섬광이 깜박인다.
const body = [];
for (let f = 0; f < F_N; f++) {
  const ph = (f / F_N) * Math.PI * 2, F = new Field(40, 40, 40, 40, RAMP);
  F.glow(-6, -6, R * 0.55, 'steel', 0.32);                                           // 왼쪽 위 은은한 빛
  F.crescent(0, 0, R, R, rad(150) + 0.08 * Math.sin(ph), rad(300) + 0.08 * Math.sin(ph), 5.2, 'steel', 0.98);   // 왼쪽 위 밝은 테두리
  F.crescent(0, 0, R - 1, R - 1, rad(-70), rad(155), 3.6, 'indigo', 0.62 + 0.06 * Math.cos(ph));               // 오른쪽 아래 남색 테두리
  F.crescent(0, 0, R * 0.62, R * 0.62, rad(190), rad(260), 2.4, 'steel', 0.7);                                   // 안쪽 짧은 반사선
  F.star(-9, -9, 9 + 2 * Math.cos(ph), 2.6, [[0, 1], [90, 1], [180, 1], [270, 1], [45, 0.45], [135, 0.45], [225, 0.45], [315, 0.45]], 'steel', 1);
  body.push(fillInside(F.toFrame(), 40, 40, R - 2));
}
// 꼬리: 셀 128×80, 피벗 (92, 40) = 구 중심. 구 뒤 가장자리(-26)부터 왼쪽으로 바늘 줄기.
const trail = [];
for (let f = 0; f < F_N; f++) {
  const F = new Field(64, 40, 92, 40, RAMP), len = [62, 70, 58, 66, 60, 68][f];
  F.needle(-22, 0, -22 - len, 0, 4.2, 'steel', 0.85);
  F.needle(-24, -12, -24 - len * 0.7, -15, 2.4, 'indigo', 0.75);
  F.needle(-24, 12, -24 - len * 0.65, 15, 2.4, 'indigo', 0.75);
  F.crescent(-20, 0, 10, 24, rad(110), rad(250), 2.6, 'steel', 0.55);              // 구 뒤를 감싸는 물결 초승달
  // 흩어지는 짧은 바늘: 6프레임 × 10px = 60px 주기
  for (const [base, dy, hue] of [[0, -20, 'steel'], [20, 21, 'indigo'], [40, -6, 'steel'], [10, 8, 'indigo'], [30, -26, 'indigo'], [50, 26, 'steel']]) {
    const x = -34 - ((base + f * 10) % 60); F.needle(x, dy, x - 9, dy, 1.6, hue, 0.7);
  }
  trail.push(F.toFrame());
}
writePng(path.join(ROOT, 'blue-orb-glow-body.png'), sheet(body, F_N));
writePng(path.join(ROOT, 'blue-orb-glow-trail.png'), sheet(trail, F_N));
const metaPath = path.join(ROOT, 'metadata.json'), meta = JSON.parse(fs.readFileSync(metaPath, 'utf8'));
meta.assets.blueOrbBody = { file: 'blue-orb-glow-body.png', columns: F_N, rows: 1, frameWidth: 80, frameHeight: 80, frameCount: F_N, pivotPx: [40, 40], pivot: [0.5, 0.5], durationsMs: Array(F_N).fill(70), loop: true, note: 'Blue 구체 빛 화풍 몸체, 판정 2×2u 그대로, 회전하지 않음' };
meta.assets.blueOrbTrail = { file: 'blue-orb-glow-trail.png', columns: F_N, rows: 1, frameWidth: 128, frameHeight: 80, frameCount: F_N, pivotPx: [92, 40], pivot: [92 / 128, 0.5], durationsMs: Array(F_N).fill(70), loop: true, note: '꼬리만, 진행 방향으로 회전' };
fs.writeFileSync(metaPath, JSON.stringify(meta, null, 2) + '\n');
console.log('orb done');
