import { fit, clamp, smooth, ease, noise, mix, rand, WHITE } from "../../core/index.js";

// 덧베기(빨강) — 원타격 직후 같은 자리를 한 번 더 '솩' 베는 추가타.
// 몸보다 긴 얇은 일직선 하나. 덩어리를 더하지 않고 화소마다 선까지의 거리를 재서 칠한다:
//   가운데 흰 심 → 빨강 → 반투명 번짐으로 매끄럽게 이어지고, 양끝은 바늘처럼 길게 빠진다.
//   위쪽 가장자리는 날카롭게, 아래쪽은 결이 진 채 흐리게 풀린다.
// 종이색 배경에서는 더하는 빛이 안 보이므로 번짐은 반투명 색(blend)이다.
// 접촉 장에 이미 60% 그어져 있고 다음 장에 끝까지 그어진 뒤, 그은 방향대로 꼬리부터 지워진다.
// 시트 0장이 추가타 접촉 장이다(원타격 뒤 언제 들어가는지는 게임이 정한다). 피격 멈춤은 걸지 않는다.
export const meta = {
  name: "Follow cut (ink)",
  desc: "A quick extra slash: a long, thin, sharp straight cut with a white-hot core fading to red and a soft feathered glow, swept in two frames and wiped away from its tail.",
  tags: ["ink", "melee", "slash", "one-shot"],
  size: [160, 120],
  origin: [80, 64],
  duration: 0.24,
  loop: false,
  fps: 25,
  bg: "light",
  options: { palette: ["#171716", "#5a1d16", "#9e3424", "#d8664c", "#f09a7c", "#fff3e6"], dir: 1, tilt: -0.45, seed: 5 },
};

const STEP = 1 / 25;
const HALF = 47;        // 선 길이의 절반 → 약 94px
const W = 3.4;          // 가장 두꺼운 곳의 반폭(px)
const GLOW = 5;         // 번짐 폭(px)
const SWEEP = 0.03, WIPE0 = 0.08, WIPE1 = 0.22;

// 칼끝 쪽 3분의 2 지점이 가장 두껍고, 꼬리는 길게 가늘어진다
const width = (k) => W * Math.sin(Math.PI * clamp(k) ** 0.75) ** 1.6;

export function draw(p, t, o) {
  const C = fit(o.palette, 6), [, , SHADE, BODY, LIT] = C, dir = o.dir < 0 ? -1 : 1;
  const f = Math.floor(t / STEP + 1e-6), u = t + 1e-6;
  if (u >= meta.duration - 1e-4) return;
  const head = 0.6 + 0.4 * ease.outCubic(clamp(u / SWEEP));   // 칼끝이 지나간 자리 (0→1)
  const tail = ease.inQuad(smooth(WIPE0, WIPE1, u));           // 꼬리부터 지워진 자리 (0→1)
  const heat = 1 - smooth(0.04, WIPE1, u);                     // 처음이 가장 뜨겁고 지워지며 식는다
  // 일직선: 원점(몸통 가운데)을 지난다. 기울이고 맞은 방향으로 뒤집는다
  const a = o.tilt + (rand(o.seed, 1) - 0.5) * 0.3, ca = Math.cos(a), sa = Math.sin(a);
  const toLocal = (x, y) => {
    const X = x * dir, Y = y;
    return [X * ca + Y * sa, -X * sa + Y * ca];
  };
  const B = HALF + W + GLOW + 4;
  for (let y = -Math.ceil(B); y <= B; y++)
    for (let x = -Math.ceil(B); x <= B; x++) {
      const [lx, ly] = toLocal(x + 0.5, y + 0.5);
      const k = (lx + HALF) / (2 * HALF);                    // 0 = 긋기 시작한 끝, 1 = 칼끝
      if (k < tail - 0.02 || k > head + 0.02) continue;
      // 지금 보이는 구간 안에서 다시 끝을 뾰족하게: 칼끝·지워지는 꼬리 모두 바늘처럼
      const kk = clamp((k - tail) / Math.max(0.05, head - tail));
      const w = width(k) * Math.min(1, kk * 6, (1 - kk) * 10);
      const d = -ly;                                           // + 위쪽(날카로운 쪽) / - 아래쪽
      const out = d > 0;
      // 바깥은 날카롭게(번짐이 짧다), 안쪽은 결을 따라 길게 풀린다
      const fiber = 0.55 + 0.45 * noise(k * 70 + f * 3, o.seed);
      const glow = out ? GLOW * 0.45 : GLOW * fiber;
      const ad = Math.abs(d);
      if (w <= 0.05) continue;
      if (ad <= w) {
        const i = 1 - ad / w;                                  // 1 가운데 … 0 가장자리
        const core = 0.45 + 0.25 * (1 - heat);
        const c = i > core ? mix(LIT, WHITE, clamp((i - core) / (1 - core) * (0.6 + 0.6 * heat))) : mix(BODY, LIT, i / core);
        p.blend(x, y, c, clamp(0.55 + i * 2));
      } else if (ad <= w + glow) {
        const g = 1 - (ad - w) / glow;
        p.blend(x, y, out ? BODY : mix(SHADE, BODY, g), 0.5 * g * g * (0.6 + 0.4 * heat));
      }
    }
}
