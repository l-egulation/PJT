// 3페이즈 패턴 4 순차 폭발 화살의 열 폭발(폭 2.5u × 높이 15u, 판정 0.15초, 게임 1배 32px = 1u). 폭발 순간 t = 0.
// 확정 3P2 지연 폭발(p3-blast)의 흰 심·불티를 한 줄기 기둥으로 녹였다: 바닥에서 0.05초 만에 꼭대기까지 솟구치고,
// 안쪽은 위로 흐르는 에너지 결과 위로 솟는 짧은 빛줄기, 흰 심이 판정 동안 버티다 가늘어진다. 바닥에는 그을음만 남는다.
// 빛 화풍 색으로 그려 확정 다크 판타지 변환(blue-dive-root-glow/tools/darkfantasy.mjs)으로 다시 칠해 쓴다(p3-blast와 같은 과정).
import { Rng, seg, ease, fit, clamp, noise2 } from "../../core/index.js";

export const meta = {
  name: "Phase-3 arrow pillar",
  desc: "A single pillar the full height of the arena: it erupts from a planted bolt in a blink, light streaks race up around a white core while it hurts, then it thins and unravels upward in sparks.",
  tags: ["pillar", "explosion", "roseblue"],
  size: [208, 488],
  origin: [104, 478],
  duration: 0.55,
  loop: false,
  fps: 30,
  bg: "ground",
  options: { palette: ["#1c0f2a", "#3a2470", "#7a5ad0", "#b0507a", "#e8789e", "#f8b8d6", "#ffffff"], seed: 11 },
};

const HALF = 40, TOP = 470, ACTIVE = 0.15;

export function draw(p, t, o) {
  const [O, DV, MV, FM, TM, TL, W] = fit(o.palette, 7), R = new Rng(o.seed), frame = Math.floor(t * 30);
  const rise = ease.outQuart(seg(t, 0, 0.05)), thin = ease.inQuad(seg(t, ACTIVE, 0.42)), lift = ease.inQuad(seg(t, ACTIVE + 0.05, 0.55));
  const half = HALF * (0.55 + 0.45 * ease.outQuart(seg(t, 0, 0.04))) * (1 - thin), top = TOP * rise, bottom = -lift * 260;
  const hero = t < 0.034;

  // 몸: 가장자리는 6px 단위로 들쭉날쭉(칸마다 바뀜), 안쪽은 위로 흐르는 결. 가운데가 가장 밝다.
  if (half > 1.5 && top > 2) {
    const jag = (y) => { const b = Math.floor(-y / 6); return ((b * 7 + frame * 3) % 5) - 2; };
    p.shade(-HALF - 8, -top - 12, HALF + 8, 1, (x, y) => {
      if (y > bottom) return;
      const w = half + jag(y) * (half / HALF) * 1.2, crown = -top + (((Math.floor(x / 6) * 5 + frame) % 4) * 3) * rise;
      const d = Math.max(Math.abs(x) - w, crown - y);
      if (d > 2) return;
      if (d > 0) return O;
      const k = Math.abs(x) / Math.max(1, w), flow = noise2(x * 0.12, y * 0.035 + t * 22, o.seed);
      if (hero) return k < 0.7 ? W : TL;
      if (k < 0.16 + 0.08 * flow) return W;
      if (k < 0.38 + 0.12 * flow) return TL;
      if (k < 0.68 + 0.1 * flow) return flow > 0.25 ? TL : TM;
      return flow > 0.3 ? TM : FM;
    });
    // 기둥 안을 따라 위로 빠르게 솟는 짧은 빛줄기(사용자 의견으로 초승달 나선 문양은 뺐다)
    for (let i = 0; i < 9; i++) {
      const x = (R.range(-0.7, 0.7)) * half, len = R.range(24, 60), y = -((R.range(0, TOP) + t * 1400) % TOP);
      if (y > bottom || y - len < -top) continue;
      p.line(x, y, x, y - len, i % 3 ? TL : W);
    }
  }

  // 불티: 기둥 둘레에서 위로 빠르게 솟는다
  for (let i = 0; i < 26; i++) {
    const side = i % 2 ? 1 : -1, x = side * (HALF * R.range(0.4, 1.1)), y0 = -R.range(0, TOP * 0.9), vy = R.range(500, 900), age = t - R.range(0, 0.12);
    if (age < 0) continue;
    const y = y0 - vy * age, life = 1 - seg(age, 0.15, 0.4);
    if (y < -TOP - 6 || life <= 0) continue;
    p.rect(x - 1, y - 3, 3, 6, O); p.rect(x, y - 2, 1, 4, i % 3 ? TL : W, life);
  }

  // 바닥: 그을음(충격 고리·파편은 사용자 의견으로 뺐다)
  const scorch = 1 - seg(t, 0.32, 0.52);
  if (scorch > 0) { p.ellipse(0, -1, 50 * scorch + 1, 4, O); p.ellipse(0, -1, 50 * scorch, 3, t < ACTIVE ? TM : FM); }
}
