import { fit, clamp, smooth, ease, mix, rand, WHITE } from "../../core/index.js";
import { glowPath } from "./glow-path.js";

// 속박(보라) — 발 둘레 바닥에 빛이 먼저 번지고, 그 자리에서 빛 말뚝 다섯이 비스듬히 솟아 발을 붙잡는다.
// 1초 동안 버티다가 다시 땅속으로 빠져 사라진다. 원점은 몬스터 발 가운데(바닥).
export const meta = {
  name: "Root (light stakes)",
  desc: "Light spreads on the floor around the feet, five glowing stakes snap up from it at an angle to grip, hold, then sink back into the ground.",
  tags: ["ink", "root", "debuff", "one-shot"],
  size: [104, 80],
  origin: [52, 70],
  duration: 1.0,
  loop: false,
  fps: 25,
  bg: "light",
  options: { palette: ["#171716", "#3a1d52", "#64348c", "#9a5cc8", "#cfa2ec", "#fbf2ff"], count: 5, order: "out", rise: 0.05, over: 0, shake: 0, len: 26, lenVar: 0, width: 1.9, spread: 0.5, bend: 0, seed: 3 },
};

// 솟는 방식은 옵션: count(개수), order(out 바깥→안 / in 안→바깥 / all 한꺼번에 / rand 제각각),
// rise(솟는 시간), over(넘침 정도)·shake(박힌 뒤 떨림), len·lenVar(길이·편차), width(굵기), spread(바깥 말뚝 기울기), bend(안쪽으로 휨)
function stakesOf(o) {
  const n = Math.max(1, Math.round(o.count)), out = [];
  for (let i = 0; i < n; i++) {
    const u = n === 1 ? 0 : (i / (n - 1)) * 2 - 1, x = u * 26 + (rand(o.seed, i, 5) - 0.5) * 3;
    const order = { out: 1 - Math.abs(u), in: Math.abs(u), all: 0, rand: rand(o.seed, i, 1) }[o.order] ?? 0;
    out.push({ x, a: -u * o.spread, d: 0.03 + order * 0.08, L: o.len * (1 - o.lenVar * rand(o.seed, i, 2)) });
  }
  return out;
}

export function draw(p, t, o) {
  if (t >= meta.duration - 1e-4) return;
  const C = fit(o.palette, 6), [, , , BODY, LIT] = C;
  const sink = ease.inCubic(smooth(0.8, 0.96, t)), fade = 1 - smooth(0.9, 1, t);
  for (const s of stakesOf(o)) {
    // 바닥 빛: 말뚝보다 먼저 번져 솟을 자리를 알린다
    const pre = smooth(s.d - 0.06, s.d, t), burst = 1 - smooth(s.d + o.rise, s.d + o.rise + 0.3, t);
    const g = (0.35 + 0.45 * burst) * pre * fade * (1 - sink * 0.6);
    if (g > 0.01)
      for (let y = -2; y <= 3; y++)
        for (let x = -8; x <= 8; x++) {
          const d = Math.hypot(x / 8, y / 2.5);
          if (d < 1) p.blend(s.x + x, 1 + y, mix(BODY, LIT, 1 - d), g * (1 - d) ** 2);
        }
    // 말뚝: 끝이 땅에서 튀어나와 솟고, 끝무렵 다시 땅으로 빠진다
    const k = clamp((t - s.d) / o.rise);
    // 넘침·떨림을 주면 「또잉」 하는 푸딩 느낌이 난다. 기본은 한 번에 촥 박히고 멈춘다
    const u = (o.over > 0 ? ease.outBack(k, o.over) : ease.outExpo(k)) * (1 - sink);
    if (u <= 0.02) continue;
    const q = t - s.d - o.rise, shake = o.shake && q > 0 && q < 0.15 ? Math.sin(q * 90) * (1 - q / 0.15) * 1.2 * o.shake : 0;
    const len = s.L * u, pts = [];
    for (let j = 0; j <= 6; j++) {
      const v = j / 6, a = s.a + Math.sign(s.a || 1) * o.bend * v * v;
      pts.push([s.x + shake * v + Math.sin(a) * len * v, 2 - Math.cos(a) * len * v]);
    }
    glowPath(p, pts.reverse(), (v) => o.width * (0.35 + 0.65 * v), 3, fade, C);
  }
}
