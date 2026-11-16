// 급강하 속박 이펙트(낙하 잔상·착지 파열·속박)를 확정 이펙트 rose-blue-exchange-flash-sheet-v4의 화풍으로 그린다.
// 실행: node tools/gen.mjs (이 폴더 기준)
// 화풍 규칙(확정 이펙트에서 측정): 외곽선 없음, 빛 자체가 형태, 단계로 끊은 반투명, 흰 심 → 연한 색 → 청보라·장밋빛,
// 가늘고 날카로운 초승달·바늘 파편·별 섬광. 반 해상도로 그려 2배로 키워 2px 픽셀 덩어리로 만든다. 32px = 1u.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { writePng } from './png.mjs';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
import { Field, hex, rad, rng } from './glow.mjs';
// Blue의 공격이라 장밋빛은 쓰지 않는다. Blue 캐릭터 그림(blue-standing·blue-palm)의 실제 색에서 두 계열을 고른다. [심, 연한 색, 중간, 짙은 색]
// steel: 로브의 강철빛 파랑(#547ca4·#3c5c84), indigo: 그림자의 남색(#34447c). 확정 이펙트처럼 두 계열을 좌우로 나눠 쓴다.
const RAMP = {
  steel: [hex('#f4f8fc'), hex('#c4d8ec'), hex('#6890bc'), hex('#3c5c84')],
  indigo: [hex('#eef0fa'), hex('#b8c0e4'), hex('#5464a4'), hex('#34447c')],
};

const seg = (t, a, b) => Math.max(0, Math.min(1, (t - a) / (b - a)));
const outQ = (t) => 1 - (1 - t) ** 4;

// 1) 낙하 잔상: 셀 96×224, 피벗 = Blue의 발(바닥 쪽 끝). 6프레임, 0.14초.
function dive() {
  const W = 96, H = 224, PX = 48, PY = 214, out = [], R = rng(5);
  const motes = Array.from({ length: 10 }, () => [R() * 28 - 14, R(), R() < 0.3 ? 'indigo' : 'steel']);
  for (let f = 0; f < 6; f++) {
    const F = new Field(W / 2, H / 2, PX, PY, RAMP), t = f / 5, len = 60 + 140 * outQ(Math.min(1, t * 1.6)) * (1 - 0.35 * seg(t, 0.75, 1));
    F.needle(0, 0, 0, -len, 7 - 2 * seg(t, 0.6, 1), 'steel');
    F.needle(-9, -6, -5, -len * 0.75, 3.2, 'steel', 0.8);
    F.needle(9, -6, 5, -len * 0.7, 3.2, 'steel', 0.8);
    F.needle(2, -4, 2, -len * 0.55, 1.6, 'indigo', 0.9);
    F.glow(0, -4, 12, 'steel', 0.95);
    for (const [mx, my, hue] of motes) { const y = -20 - my * len * 0.9 - f * 6; if (-y < len) F.needle(mx, y, mx, y - 8, 1.4, hue, 0.75); }
    out.push(F.toFrame());
  }
  return { frames: out, w: W, h: H, pivot: [PX, PY], ms: [20, 20, 20, 20, 30, 30] };
}

// 2) 착지 파열: 셀 208×112, 피벗 = 착지점(바닥). 8프레임, 0.42초.
function impact() {
  const W = 208, H = 112, PX = 104, PY = 96, out = [], R = rng(9);
  const shards = Array.from({ length: 14 }, (_, i) => ({ a: rad(-180 + (i + R() * 0.6) * (180 / 14)), sp: 0.6 + R() * 0.5, hue: i % 2 ? 'indigo' : 'steel' }));
  const ms = [20, 30, 40, 50, 60, 70, 70, 80];
  let t = 0;
  for (let f = 0; f < 8; f++) {
    const F = new Field(W / 2, H / 2, PX, PY, RAMP);
    if (f <= 3) F.star(0, -8, [16, 52, 42, 26][f], [4, 7, 5.5, 3.5][f], [[0, 1], [180, 1], [-90, 0.7], [-45, 0.6], [-135, 0.6], [-20, 0.45], [-160, 0.45], [-70, 0.35], [-110, 0.35]], 'steel', 1, 0.75);
    if (f >= 1 && f <= 6) {
      const k = seg(f, 1, 6), rx = 18 + 70 * outQ(k), fade = 1 - seg(f, 4, 7);
      // 바닥을 따라 퍼지는 두 초승달: 왼쪽 청보라, 오른쪽 장밋빛(확정 이펙트처럼 색을 나눈다).
      F.crescent(0, -6, rx, rx * 0.3, rad(-205), rad(-105), 7 * fade + 1.5, 'steel', 0.25 + 0.85 * fade);
      F.crescent(0, -6, rx, rx * 0.3, rad(-75), rad(25), 7 * fade + 1.5, 'indigo', 0.25 + 0.85 * fade);
    }
    if (f >= 2) for (const s of shards) {
      const r = 14 + 80 * outQ(seg(f, 2, 7)) * s.sp, x = Math.cos(s.a) * r, y = -6 + Math.sin(s.a) * r * 0.55 + 6 * seg(f, 4, 7) ** 2 * 10;
      const k = 1 - seg(f, 5, 8); if (k <= 0 || y > -1) continue;
      F.needle(x - Math.cos(s.a) * 3, y - Math.sin(s.a) * 2, x + Math.cos(s.a) * 11, y + Math.sin(s.a) * 6, 2.8, s.hue, 0.6 + 0.4 * k);
    }
    out.push(F.toFrame()); t += ms[f];
  }
  return { frames: out, w: W, h: H, pivot: [PX, PY], ms };
}

// 3) 속박: 셀 112×96, 피벗 = 플레이어 발. 출현 1~3, 유지 4~9(반복), 해제 10~12.
function root() {
  const W = 112, H = 96, PX = 56, PY = 88, out = [], R = rng(13);
  const sparks = Array.from({ length: 8 }, (_, i) => ({ a: (i / 8) * Math.PI * 2 + R(), y: -6 - R() * 26 }));
  for (let f = 0; f < 12; f++) {
    const F = new Field(W / 2, H / 2, PX, PY, RAMP);
    const appear = f < 3 ? (f + 1) / 3 : 1, hold = f >= 3 && f < 9, phase = (f - 3) / 6, release = f >= 9 ? (f - 8) / 3 : 0;
    if (release === 0) {
      const spin = hold ? phase * Math.PI * 2 : 0, pulse = hold ? 0.88 + 0.12 * Math.cos(phase * Math.PI * 2) : 0.5 + 0.5 * appear;
      // 발목을 감는 빛 고리 세 겹: 아래부터 청보라·장밋빛·청보라, 위로 갈수록 좁고 늦게 나타난다. 고리마다 초승달 두 조각이 반대로 돈다.
      [[-4, 30, 'steel', 1, 0, 1], [-14, 24, 'indigo', -1.3, 1, 0.92], [-24, 17, 'steel', 1.6, 2, 0.78]].forEach(([y, rx, hue, sp, order, bright]) => {
        if (f < order) return;
        const grow = f < 3 ? Math.min(1, (f - order + 1) / 2) : 1;
        for (const off of [0, Math.PI]) F.crescent(0, y, rx * (0.6 + 0.4 * grow), rx * 0.3, off + spin * sp, off + spin * sp + rad(160), 3.6, hue, bright * pulse * (0.7 + 0.3 * grow));
      });
      if (hold) for (const s of sparks) { const a = s.a + phase * 6.28; F.needle(Math.cos(a) * 28, s.y + Math.sin(a) * 3, Math.cos(a) * 28, s.y - 6, 1.4, Math.sin(a) > 0 ? 'indigo' : 'steel', 0.75); }
    } else {
      // 해제: 고리가 바늘 조각으로 부서져 밖으로 흩어진다.
      for (let i = 0; i < 12; i++) {
        const a = (i / 12) * Math.PI * 2, r = 22 + 30 * release, y = -8 + Math.sin(a) * r * 0.3 - 10 * release;
        const x = Math.cos(a) * r; F.needle(x, y, x + Math.cos(a) * 6, y + Math.sin(a) * 2 - 3, 2.2 * (1 - release * 0.6), i % 2 ? 'indigo' : 'steel', 1 - release * 0.55);
      }
    }
    out.push(F.toFrame());
  }
  return { frames: out, w: W, h: H, pivot: [PX, PY] };
}

function sheet(frames, cols) {
  const fw = frames[0].w, fh = frames[0].h, rows = Math.ceil(frames.length / cols), s = { w: fw * cols, h: fh * rows, data: new Uint8Array(fw * cols * fh * rows * 4) };
  frames.forEach((f, i) => { const ox = (i % cols) * fw, oy = Math.floor(i / cols) * fh; for (let y = 0; y < fh; y++) s.data.set(f.data.subarray(y * fw * 4, (y + 1) * fw * 4), ((oy + y) * s.w + ox) * 4); });
  return s;
}
const meta = { style: 'rose-blue-exchange-flash-sheet-v4에 맞춘 빛 화풍, Blue 캐릭터 색(강철빛 파랑·남색)', pixelsPerUnit: 32, order: 'top-left row-major', assets: {} };
const R = root(), D = dive(), I = impact();
for (const [name, a, cols, extra] of [['dive', D, 6, { durationsMs: D.ms, attachTo: 'Blue feet during the 0.14s descent' }], ['impact', I, 4, { durationsMs: I.ms, attachTo: 'locked landing point' }],
  ['root', R, 4, { appearFrames: [1, 2, 3], appearDurationsMs: [30, 40, 50], holdFrames: [4, 5, 6, 7, 8, 9], holdFrameDurationMs: 90, releaseFrames: [10, 11, 12], releaseDurationsMs: [50, 50, 60], attachTo: 'rooted player feet' }]]) {
  writePng(path.join(ROOT, `${name}-sheet.png`), sheet(a.frames, cols));
  meta.assets[name] = { file: `${name}-sheet.png`, columns: cols, rows: Math.ceil(a.frames.length / cols), frameWidth: a.w, frameHeight: a.h, frameCount: a.frames.length, pivot: [a.pivot[0] / a.w, 1 - a.pivot[1] / a.h], pivotPx: a.pivot, loop: false, ...extra };
}
fs.writeFileSync(path.join(ROOT, 'metadata.json'), JSON.stringify(meta, null, 2) + '\n');
console.log('done', Object.keys(meta.assets).join(', '));
