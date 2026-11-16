import { fit, clamp, smooth, ease } from "../../core/index.js";
import { glowPath } from "./glow-path.js";
import { glowDot } from "./glow-dot.js";

// 강해진 한 방(빨강) — 강화된 공격이 맞는 순간. 맞은 자리에 날카로운 네 갈래 빛 별이 박히고 바로 사라진다.
// 일반 피격 위에 겹쳐 「더 세게 들어갔다」를 보인다. 원점은 맞은 자리(몬스터 몸 가운데).
export const meta = {
  name: "Empowered hit (ink)",
  desc: "A sharp four-point light star stamps on the hit point and vanishes, on top of the normal hit.",
  tags: ["ink", "impact", "empower", "one-shot"],
  size: [80, 80],
  origin: [40, 40],
  duration: 0.24,
  loop: false,
  fps: 25,
  bg: "light",
  options: { palette: ["#171716", "#5a1d16", "#9e3424", "#d8664c", "#f09a7c", "#fff3e6"], size: 22, seed: 2 },
};

export function draw(p, t, o) {
  if (t >= meta.duration - 1e-4) return;
  const C = fit(o.palette, 6), g = ease.outExpo(clamp(t / 0.04)), fade = 1 - smooth(0.08, 0.22, t);
  const L = o.size * g * (1 - 0.3 * smooth(0.04, 0.2, t));
  for (const [dx, dy, s] of [[1, 0, 1], [0, 1, 0.8], [0.7, -0.7, 0.55], [0.7, 0.7, 0.55]])
    glowPath(p, [[-dx * L * s, -dy * L * s], [0, 0], [dx * L * s, dy * L * s]], (v) => 0.4 + 1.6 * Math.sin(Math.PI * v) ** 2, 3, fade, C);
  glowDot(p, 0, 0, 2.5 * g, 4, fade, C);
}
