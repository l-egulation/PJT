import { fit, mix, noise2, smooth, TAU } from "../../core/index.js";

// 다음 공격 강화(빨강) — 승전의 칼끝: 처치 뒤 다음 공격까지 몸 전체에서 붉은 기운이 아래에서 위로 쭉 피어오른다.
// 양옆 가장자리에 몰리지 않고 몸 폭 전체를 채우며, 결이 위로 빠르게 흘러 올라가 머리 위에서 흩어진다. 선 없이 반투명 번짐. 반복.
// 원점은 플레이어 발 가운데.
export const meta = {
  name: "Charge rise (ink)",
  desc: "A red aura rises from the feet up through the whole body width and thins out above the head, its texture streaming upward. Loops.",
  tags: ["ink", "buff", "empower", "loop"],
  size: [80, 120],
  origin: [40, 108],
  duration: 0.8,
  loop: true,
  fps: 25,
  bg: "light",
  options: { palette: ["#171716", "#5a1d16", "#9e3424", "#d8664c", "#f09a7c", "#fff3e6"], width: 20, top: 96, seed: 3 },
};

export function draw(p, t, o) {
  const C = fit(o.palette, 6), [, , SHADE, BODY, LIT] = C, ph = t / meta.duration;
  for (let y = -o.top - 6; y <= 2; y++)
    for (let x = -o.width - 8; x <= o.width + 8; x++) {
      const h = -y / o.top;                                   // 0 발 … 1 머리 위
      const w = o.width * (1 - 0.35 * h), d = Math.abs(x) / w;
      if (d > 1.35) continue;
      // 결이 위로 흐른다: y 에 위상을 더해 끌어올린다(반복 이음매가 없게 두 겹을 섞는다)
      const n1 = noise2(x * 0.18, y * 0.09 + ph * 7.2, o.seed) * 0.5 + 0.5;
      const n2 = noise2(x * 0.18 + 31, y * 0.09 + (ph - 1) * 7.2, o.seed) * 0.5 + 0.5;
      const n = n1 * (1 - ph) + n2 * ph;
      const streak = Math.max(0, n - 0.42) * 2.2;            // 위로 흐르는 결
      const body = (1 - smooth(0.8, 1.35, d)) * (1 - smooth(0.55, 1.05, h)) * smooth(-0.04, 0.06, h);
      const a = body * (0.1 + 0.5 * streak);
      if (a > 0.01) p.blend(x, y, mix(SHADE, mix(BODY, LIT, Math.min(1, streak)), 0.4 + 0.6 * (1 - h)), Math.min(0.6, a));
    }
}
