import { fit, clamp, smooth, mix, noise, rand, WHITE, TAU } from "../../core/index.js";

// 이동 속도 증가(노랑) — 버프가 걸려 있는 동안 플레이어 뒤로 흐르는 빛줄기. 반복.
// 덧베기와 같은 '빛나는 선' 방식: 화소마다 선분까지 거리를 재서 흰 심 → 노랑 → 반투명 번짐으로 칠한다.
// 다리부터 목도리 높이까지 가는 줄기 일곱이 몸 뒤에서 생겨 뒤로 밀려나며 길어졌다가 가늘어져 사라진다.
// 원점은 플레이어 발 가운데(바닥), 앞을 보는 방향이 +x. 뒤로 흐르는 쪽은 -x. 왼쪽을 보면 게임이 시트를 좌우 반전한다
// (캔버스가 뒤쪽으로만 넓어 이펙트 안에서 뒤집으면 줄기가 잘린다).
export const meta = {
  name: "Speed trail (ink)",
  desc: "A speed buff while it lasts: three thin glowing streaks at leg, waist and scarf height peel off behind the body, stretch back and thin away. Loops.",
  tags: ["ink", "buff", "speed", "loop"],
  size: [176, 96],
  origin: [160, 88],
  duration: 0.64,
  loop: true,
  fps: 25,
  bg: "light",
  options: { palette: ["#171716", "#5c4210", "#a87a14", "#e3b030", "#f6d878", "#fffbe8"], lanes: 10, len: 1.4, top: -56, width: 1.5, seed: 2 },
};

const STEP = 1 / 25;

// 빛나는 선분 하나: (x0,y0) 꼬리 → (x1,y1) 머리. 머리 쪽 3분의 1이 가장 두껍고 양끝은 뾰족하다.
// uniform 이면 처음부터 끝까지 같은 굵기에 끝만 둥글다. glow 는 번짐 폭, alpha 는 전체 세기
export function glowLine(p, x0, y0, x1, y1, w, glow, alpha, C, seed = 0, f = 0, uniform = false) {
  const [, , SHADE, BODY, LIT] = C, dx = x1 - x0, dy = y1 - y0, L = Math.hypot(dx, dy);
  if (L < 0.5 || alpha <= 0.01) return;
  const ux = dx / L, uy = dy / L, pad = w + glow + 2;
  const xa = Math.floor(Math.min(x0, x1) - pad), xb = Math.ceil(Math.max(x0, x1) + pad);
  const ya = Math.floor(Math.min(y0, y1) - pad), yb = Math.ceil(Math.max(y0, y1) + pad);
  for (let y = ya; y <= yb; y++)
    for (let x = xa; x <= xb; x++) {
      const px = x + 0.5 - x0, py = y + 0.5 - y0, k = (px * ux + py * uy) / L, d = Math.abs(-px * uy + py * ux);
      if (uniform ? k < -w / L || k > 1 + w / L : k < 0 || k > 1) continue;
      // 고른 굵기: 끝을 넘은 곳은 끝점까지의 거리로 재서 둥글게 닫는다
      const hw = uniform ? w : w * Math.sin(Math.PI * k ** 0.6) ** 1.3;
      const dd = uniform && (k < 0 || k > 1) ? Math.hypot(px - (k < 0 ? 0 : dx), py - (k < 0 ? 0 : dy)) : d;
      const fiber = 0.6 + 0.4 * noise(k * 30 + f * 2, seed);
      if (hw > 0.05 && dd <= hw) {
        const i = 1 - dd / hw;
        const c = i > 0.65 ? mix(LIT, WHITE, (i - 0.65) * 2.2) : mix(SHADE, BODY, Math.min(1, i * 2.2)); // 종이 위에서 띄려면 몸은 진한 색
        p.blend(x, y, c, alpha * clamp(0.55 + i * 2));
      } else if (dd <= hw + glow * fiber) {
        const g = 1 - (dd - hw) / (glow * fiber);
        p.blend(x, y, mix(SHADE, BODY, g), alpha * 0.55 * g * g);
      }
    }
}

// 줄기: 개수(lanes)·길이 배율(len)·가장 높은 줄기 높이(top, 발에서 위로 px)·굵기(width)는 옵션.
// 높이는 발(-4)에서 top 까지 고르게 나누고 조금씩 흔든다. 위상·기본 길이는 seed 로 정해 장마다 같다.
// 한 반복(0.64초) 동안 줄기마다 두 번씩(0.32초 수명) 지나간다. 0.24초·60px 은 너무 빨랐고 0.48초·26px 은 떠다녔다
function lanesOf(o) {
  const n = Math.max(1, Math.round(o.lanes)), out = [];
  for (let i = 0; i < n; i++) {
    const u = n === 1 ? 0.5 : i / (n - 1);
    out.push({ y: -4 + (o.top + 4) * u + (rand(o.seed, i, 3) - 0.5) * 3, d: rand(o.seed, i, 1), L: (20 + 18 * rand(o.seed, i, 2)) * o.len });
  }
  return out;
}
const D = 0.64, RATE = 2, LIFE = D / RATE;

// ending 이면 반복 주기 0에서 버프가 끝난 것으로 보고, 그 뒤에 새로 생기는 줄기는 그리지 않는다
export function drawLanes(p, t, o, ending = false) {
  const C = fit(o.palette, 6), back = -1, f = Math.floor(t / STEP + 1e-6);
  for (const [n, lane] of lanesOf(o).entries()) {
    const k = ((t / LIFE + lane.d) % 1 + 1) % 1;               // 이 줄기의 0..1 수명
    if (ending && t - k * LIFE > -1e-6) continue;              // 주기 0 이후에 태어난 줄기
    const head = 2 + 46 * k ** 0.8;                             // 몸 뒤에서 떨어져 밀려난다
    // 꼬리가 머리를 따라잡으며 짧아진다. 전에는 수명 3분의 2에 길이가 0이 되어 점만 날아가다 뚝 끊겼다
    const len = lane.L * Math.sin(Math.PI * k) ** 0.7 + 1;
    const alpha = smooth(0.02, 0.14, k) * (1 - smooth(0.6, 1, k)); // 흐리게 생겨서 흐리게 사라진다
    glowLine(p, back * (head + len), lane.y, back * head, lane.y, o.width, 3, alpha, C, o.seed + n, f);
  }
}

export function draw(p, t, o) {
  drawLanes(p, t, o);
}
