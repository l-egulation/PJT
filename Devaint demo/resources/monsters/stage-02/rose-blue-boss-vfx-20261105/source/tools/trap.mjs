// 1페이즈 패턴 2 속박 함정: 전조와 활성화를 빛 화풍·Blue 색(강철빛 + 남색)으로 만든다. 실행: node tools/trap.mjs (이 폴더 기준)
// 발판 하나(6u = 192px)를 통째로 덮는 효과 하나다. 피벗은 발판 윗면 가운데.
//   trap-warn: 발판 윗면을 따라 빛 솔기가 깔리고, 마름모 룬이 가운데에서 바깥으로 차례로 맥동한다. 셀 208×48, 피벗 (104, 40), 6프레임 80ms 반복.
//   trap-snap: 사슬 네 줄이 발판 가운데 위 한 점으로 모여 조이고 큰 봉인 자물쇠가 잠긴다. 발목 높이 가로 사슬이 발판 끝에서 끝까지 팽팽해진다. 셀 224×144, 피벗 (112, 136), 10프레임.
// 붙잡힌 동안은 급강하 속박과 같은 root-sheet를 쓴다.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { writePng } from './png.mjs';
import { Field, hex, rad, rng } from './glow.mjs';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const RAMP = { steel: [hex('#f4f8fc'), hex('#c4d8ec'), hex('#6890bc'), hex('#3c5c84')], indigo: [hex('#eef0fa'), hex('#b8c0e4'), hex('#5464a4'), hex('#34447c')] };
const SNAP_MS = [40, 40, 40, 60, 50, 60, 70, 80, 90, 100];
const HALF = 92; // 발판 반폭(96px)에서 양끝을 조금 남긴다
const RUNES = [-72, -48, -24, 0, 24, 48, 72];

// 마름모 룬: 위·아래가 짧고 좌우가 긴 빛 마름모
function rune(F, x, s, hue, k) {
  F.needle(x - 9 * s, -1, x, -1 - 5 * s, 1.6, hue, k); F.needle(x, -1 - 5 * s, x + 9 * s, -1, 1.6, hue, k);
  F.needle(x - 9 * s, -1, x, 3 * s - 1, 1.4, hue, k * 0.8); F.needle(x, 3 * s - 1, x + 9 * s, -1, 1.4, hue, k * 0.8);
  F.glow(x, -1, 3 + 2 * s, hue, k * 0.9);
}

function warn() {
  const out = [];
  for (let f = 0; f < 6; f++) {
    const F = new Field(104, 24, 104, 40, RAMP), ph = f / 6;
    // 발판 윗면을 따라 가는 빛 솔기(왼쪽 반은 강철빛, 오른쪽 반은 남색)
    F.needle(-HALF, 0, 0, 0, 2.2, 'steel', 0.95); F.needle(0, 0, HALF, 0, 2.2, 'indigo', 0.95);
    // 룬이 가운데에서 바깥으로 차례로 맥동한다
    RUNES.forEach((x) => {
      const d = Math.abs(x) / 72, p = 0.5 + 0.5 * Math.cos((ph - d * 0.5) * Math.PI * 2);
      rune(F, x, 0.7 + 0.35 * p, x < 0 ? 'steel' : x > 0 ? 'indigo' : 'steel', 0.55 + 0.6 * p);
    });
    // 룬 사이에서 가는 빛 줄기가 위로 오른다
    [-60, -36, -12, 12, 36, 60].forEach((x, i) => {
      const u = (ph + i * 0.37) % 1, y = -4 - 18 * u, k = Math.sin(Math.PI * u);
      if (k > 0.2) F.needle(x, y + 3, x, y - 6, 1.3, i % 2 ? 'indigo' : 'steel', 1.05 * k);
    });
    out.push(F.toFrame());
  }
  return out;
}

// 함정 활성화: 터지는 게 아니라 "붙잡힌다". 발판 전체가 하나의 속박이다.
// 발판 양끝과 안쪽 룬 고리 네 곳에서 사슬이 솟아 발판 가운데 위 한 점으로 모여 조이고, 그 자리에 큰 봉인 자물쇠가 잠긴다.
// 발목 높이에는 사슬 하나가 발판 끝에서 끝까지 가로질러 팽팽해진다. 버티다 바늘로 풀려 사라진다.
const ANCHORS = [-84, -40, 40, 84];
const LOCK = [0, -50]; // 사슬이 모여 잠기는 자리(플레이어 몸높이)
// 사슬 고리 하나: 짝수는 옆에서 본 고리(짧은 바늘), 홀수는 정면 고리(작은 원)
function link(F, x, y, dir, n, hue, k) {
  if (n % 2) F.crescent(x, y, 4.5, 4.5, 0, Math.PI * 2 - 0.01, 1.6, hue, k);
  else F.needle(x - Math.cos(dir) * 5, y - Math.sin(dir) * 5, x + Math.cos(dir) * 5, y + Math.sin(dir) * 5, 2.2, hue, k);
}
// (x0, y0)에서 (x1, y1)로 이어지는 사슬. len 0..1만큼만 그리고, sag는 처짐(+는 아래로)
function chain(F, x0, y0, x1, y1, len, sag, hue, k) {
  const mx = (x0 + x1) / 2, my = (y0 + y1) / 2 + sag;
  const P = (t) => [(1 - t) ** 2 * x0 + 2 * (1 - t) * t * mx + t * t * x1, (1 - t) ** 2 * y0 + 2 * (1 - t) * t * my + t * t * y1];
  const L = Math.hypot(x1 - x0, y1 - y0), N = Math.max(4, Math.round(L / 9)), upto = Math.floor(N * len);
  for (let n = 1; n <= upto; n++) { const t = n / N, [x, y] = P(t), [x2, y2] = P(Math.min(1, t + 0.02)); link(F, x, y, Math.atan2(y2 - y, x2 - x), n, hue, k * (n === upto && len < 1 ? 1.15 : 0.9)); }
}
const SNAP_N = 10;
function snap() {
  const out = [], R = rng(9);
  const shards = Array.from({ length: 20 }, () => ({ x: -88 + R() * 176, y: -6 - R() * 66, vx: (R() - 0.5) * 16 }));
  for (let f = 0; f < SNAP_N; f++) {
    const F = new Field(112, 72, 112, 136, RAMP);
    // 바닥 솔기와 사슬이 나오는 룬 고리
    const seamK = f < 7 ? 1 : 1 - (f - 6) / 4;
    F.needle(-HALF, 0, 0, 0, 2, 'steel', 0.8 * seamK); F.needle(0, 0, HALF, 0, 2, 'indigo', 0.8 * seamK);
    for (const ax of ANCHORS) { F.crescent(ax, 0, 9, 3, 0, Math.PI * 2 - 0.01, 2, 'steel', (f < 2 ? 1.2 : 0.9) * seamK); if (f < 2) F.glow(ax, -1, 7, 'steel', 1.2); }
    // 사슬: 1~3 솟음(처짐 → 팽팽), 4 조여짐, 5~7 버팀, 8~9 풀림
    if (f >= 1 && f <= 7) {
      const len = Math.min(1, f / 3), sag = f <= 3 ? 14 * (1 - f / 4) : 0, k = f <= 6 ? 1 : 0.7;
      ANCHORS.forEach((ax, i) => chain(F, ax, 0, LOCK[0] + (ax < 0 ? -6 : 6), LOCK[1] + 4, len, -sag * (Math.abs(ax) > 60 ? 1 : 0.5), i % 2 ? 'indigo' : 'steel', k));
      // 발목 높이 가로 사슬: 양끝에서 가운데로 뻗어 만나고 팽팽해진다
      if (f >= 2) { const l2 = Math.min(1, (f - 1) / 2), sg = f <= 3 ? 8 : 0; chain(F, -HALF + 4, -12, 0, -12, l2, sg, 'steel', k); chain(F, HALF - 4, -12, 0, -12, l2, sg, 'indigo', k); }
    }
    // 4~7: 사슬이 모인 자리에 큰 봉인 자물쇠가 잠기며 번쩍
    if (f >= 4 && f <= 7) {
      const [cx, cy] = LOCK, k = f === 4 ? 1.35 : 1.1 - (f - 5) * 0.2;
      F.crescent(cx, cy, 18, 14, 0, Math.PI * 2 - 0.01, 2.8, 'steel', k);
      F.crescent(cx, cy, 11, 8, rad(200), rad(340), 2, 'indigo', k); F.crescent(cx, cy, 11, 8, rad(20), rad(160), 2, 'indigo', k * 0.8);
      F.needle(cx, cy - 6, cx, cy + 6, 2.4, 'steel', k); F.glow(cx, cy, 4, 'steel', k);
      if (f <= 5) F.star(cx, cy, f === 4 ? 40 : 24, 2.6, [[0, 1], [180, 1], [90, 0.55], [270, 0.55], [45, 0.3], [135, 0.3], [225, 0.3], [315, 0.3]], 'steel', k);
      // 가로 사슬 가운데 작은 고정 고리
      F.crescent(0, -12, 7, 5, 0, Math.PI * 2 - 0.01, 2, 'indigo', k * 0.9);
    }
    // 8~9: 사슬이 바늘로 풀려 흩어진다
    if (f >= 8) for (const sh of shards) { const u = (f - 7) / 3; F.needle(sh.x + sh.vx * u, sh.y - 14 * u + 4, sh.x + sh.vx * u, sh.y - 14 * u - 5, 1.6, sh.x < 0 ? 'steel' : 'indigo', 1 - u * 0.6); }
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
writePng(path.join(ROOT, 'trap-warn.png'), strip(warn()));
writePng(path.join(ROOT, 'trap-snap.png'), strip(snap()));
meta.assets.trapWarn = { file: 'trap-warn.png', columns: 6, rows: 1, frameWidth: 208, frameHeight: 48, frameCount: 6, pivotPx: [104, 40], pivot: [0.5, 8 / 48], durationsMs: Array(6).fill(80), loop: true, note: '1페이즈 패턴 2 속박 함정 전조, 발판 하나(6u)를 통째로 덮음, Blue 강철빛·남색' };
meta.assets.trapSnap = { file: 'trap-snap.png', columns: SNAP_N, rows: 1, frameWidth: 224, frameHeight: 144, frameCount: SNAP_N, pivotPx: [112, 136], pivot: [0.5, 8 / 144], durationsMs: SNAP_MS, loop: false, note: '1페이즈 패턴 2 속박 함정 활성화: 사슬 네 줄이 가운데 한 점으로 모여 봉인 자물쇠가 잠기고, 발목 높이 가로 사슬이 발판 전체를 묶음. 붙잡힌 동안은 root-sheet' };
fs.writeFileSync(metaPath, JSON.stringify(meta, null, 2) + '\n');
console.log('trap done');
