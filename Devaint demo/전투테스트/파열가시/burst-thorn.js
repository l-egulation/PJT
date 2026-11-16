import { fit, clamp, smooth, ease } from "../../core/index.js";
import { glowPath } from "./glow-path.js";

// 파열 가시(빨강) — 표식 격발이 흩어진 뒤 두 번째 타격. 몬스터 발밑에서 큰 가시 하나가 비스듬히 솟아
// 몸통을 꿰뚫고, 작은 가시 둘이 곁에 솟는다. 가시는 끝으로 갈수록 바늘처럼 가늘어지는 빛 선이다.
// 넘침·떨림 없이 촥 뻗어 멈췄다가, 가늘어지며 사라진다.
// 원점은 맞은 몬스터의 몸통 가운데, 바닥은 원점 아래 28px. 시트 0장이 추가타 접촉 장이다.
export const meta = {
  name: "Burst thorn (ink)",
  desc: "One big glowing thorn erupts from the ground at a slant and runs through the body, with two small thorns beside it; it holds, then thins out and fades.",
  tags: ["ink", "mark", "thorn", "one-shot"],
  size: [160, 140],
  origin: [80, 80],
  duration: 0.36,
  loop: false,
  fps: 25,
  bg: "light",
  options: { palette: ["#171716", "#5a1d16", "#9e3424", "#d8664c", "#f09a7c", "#fff3e6"], dir: 1 },
};

const FLOOR = 28;
const GROW = 0.05, HOLD = 0.14, GONE = 0.3;

// 가시 하나: (x0,y0) 밑동 → (x1,y1) 끝. grow 만큼만 뻗고, thin 은 사라질 때 굵기 배율, bend 만큼 휜다.
function thorn(p, x0, y0, x1, y1, w0, glow, alpha, C, { grow = 1, thin = 1, bend = 0 } = {}) {
  if (grow <= 0.01 || alpha <= 0.01) return;
  const dx = x1 - x0, dy = y1 - y0, L = Math.hypot(dx, dy) || 1, nx = -dy / L, ny = dx / L;
  const pts = [];
  for (let i = 0; i <= 8; i++) {
    const v = (i / 8) * grow, off = bend * v * v;
    pts.push([x0 + dx * v + nx * off, y0 + dy * v + ny * off]);
  }
  glowPath(p, pts, (v) => w0 * thin * (1 - v) ** 0.75, glow, alpha, C);
}

// 시작 시각 t0 인 가시의 상태: 0.05초 만에 뻗고(outCubic, 넘침 없음) 0.14초부터 가늘어지며 0.3초에 사라진다.
// 접촉 장(0장)이 비지 않도록 뻗기는 LEAD 만큼 앞당겨 시작한다 → 0장에 큰 가시가 약 80% 솟아 있다
const LEAD = 0.02;
function life(t, t0) {
  const u = t - t0 + 1e-6;
  if (u < 0) return null;
  return {
    grow: ease.outCubic(clamp((u + LEAD) / GROW)),
    thin: 1 - 0.7 * smooth(HOLD, GONE, u),
    alpha: 1 - smooth(HOLD + 0.04, GONE, u),
  };
}

export function draw(p, t, o) {
  const C = fit(o.palette, 6), dir = o.dir < 0 ? -1 : 1;
  // 큰 가시: 맞은 쪽 반대편 발밑에서 맞은 쪽 머리 위로
  const big = life(t, 0);
  if (big) thorn(p, -12 * dir, FLOOR, 20 * dir, -34, 5, 5, big.alpha, C, { ...big, bend: 4 * dir });
  // 작은 가시 둘: [밑동 x, 높이, 기울기, 늦게 솟는 시간]
  for (const [x, h, lean, d] of [[-26, 12, -36, 0.02], [14, 16, -16, 0.04]]) {
    const s = life(t, d);
    if (s) thorn(p, x * dir, FLOOR, (x + lean * 0.3) * dir, FLOOR - h - 6, 2.6, 3.5, s.alpha, C, s);
  }
}
