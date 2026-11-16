// 3페이즈 패턴 1 유도 화살의 전조 맺힘(0.5초): Blue 몸 앞 화살 자리로 둘레의 잉크 줄기·방울이 빨려 들어 모이고,
// 화살 길이(3u)만큼 잉크 선이 굳어 가다 마지막에 밝은 보라 촉과 흰 한 칸이 번쩍인다(0.5초에 실제 화살로 바뀜).
// 화살은 오른쪽(+x)을 겨눈 모양으로 그린다. 게임에서 겨누는 방향으로 돌려 화살 그림 밑에 깐다.
// 반 해상도(1px = 게임 2px, 1u = 16px)로 그려 2배로 쓴다. 원점은 화살 가운데. 공허 보라 단색.
import { Rng, fit, seg, ease, clamp, TAU } from "../../core/index.js";

export const meta = {
  name: "Homing bolt forming",
  desc: "Half a second of ink being pulled in from all around into the shape of the homing bolt: tendrils and droplets converge, a line of ink hardens along its length, and a wine tip flashes as it is loosed.",
  tags: ["charge", "ink", "summon", "roseblue", "one-shot"],
  size: [120, 72],
  origin: [60, 36],
  duration: 0.5,
  loop: false,
  fps: 30,
  bg: "dark",
  options: { seed: 7 },
};

const SH = fit(["#0a040c"], 1)[0];
// 공허 보라 한 가지 색 계열로만 그린다(사용자: 빨강·파랑을 섞지 말 것). N·R8도 보라 단계로 둔다.
const N = fit(["#0c0618", "#180a2c", "#26123e", "#361a56", "#4c2a74"], 5);
const V = fit(["#361a56", "#6a42a0", "#9270cc", "#c4aef0", "#f0e8ff"], 5);
const R8 = fit(["#26123e", "#4c2a74", "#6a42a0", "#9270cc", "#c4aef0"], 5);
const L = 24; // 화살 반 길이(1.5u)

export function draw(p, t, o) {
  const R = new Rng(o.seed), k = seg(t, 0, 0.5);
  // 둘레에서 빨려 드는 잉크 방울: 화살 선 위의 목표 자리로 곡선을 그리며 모인다
  for (let i = 0; i < 18; i++) {
    const tx = R.range(-L, L), a0 = R.range(0, TAU), r0 = R.range(26, 44), d0 = R.range(0, 0.25), u = ease.inCubic(clamp((k - d0) / (1 - d0 - 0.05)));
    if (u <= 0 || u >= 1) continue;
    const sw = (1 - u) * 1.4, x = tx + Math.cos(a0 + sw) * r0 * (1 - u), y = Math.sin(a0 + sw) * r0 * 0.6 * (1 - u), r = 0.6 + 0.9 * (1 - u);
    p.circle(x, y, r + 1, SH); p.circle(x, y, r, i % 4 ? N[2] : R8[1]); p.dot(x - r * 0.4, y - r * 0.4, i % 4 ? V[1] : R8[3]);
  }
  // 잉크 줄기: 위아래에서 화살 선으로 휘어 들어온다
  for (let s = 0; s < 6; s++) {
    const side = s % 2 ? 1 : -1, x0 = R.range(-L, L), ph = R.range(0, TAU), u = ease.inOutCubic(seg(k, 0.05 + s * 0.04, 0.8));
    if (u <= 0) continue;
    let prev = null;
    for (let q = 0; q <= 12; q++) {
      const v = q / 12, reach = 1 - u, y = side * (4 + 24 * v * reach + 2), x = x0 + Math.sin(v * 3 + ph + t * 10) * 6 * reach;
      if (v > 1 - u * 0.2) break;
      if (prev) { p.line(prev[0], prev[1] + 1, x, y + 1, SH); p.line(prev[0], prev[1], x, y, q % 4 ? N[3] : V[1]); }
      prev = [x, y];
    }
  }
  // 굳어 가는 화살 선: 가운데에서 양끝으로 자란다
  const grow = ease.outCubic(seg(k, 0.2, 0.85)), half = L * grow;
  if (half > 1) {
    for (let x = -half; x <= half; x++) { p.dot(x, -2, SH); p.dot(x, 2, SH); p.dot(x, -1, N[3]); p.dot(x, 0, N[2]); p.dot(x, 1, N[1]); }
    if (grow > 0.6) { const hx = half + 1; p.tri(hx, -4, hx, 4, hx + 7, 0, SH); p.tri(hx, -3, hx, 3, hx + 6, 0, R8[2]); p.line(hx, -1, hx + 5, 0, R8[4]); }
    p.glow(0, 0, 12 + 18 * grow, V[1], 0.3 * grow);
  }
  // 마지막 순간: 밝은 보라 촉과 흰 한 칸
  if (k > 0.86) { const f = (k - 0.86) / 0.14; p.rect(-half, -1, half * 2, 2, f > 0.5 ? V[4] : V[3]); p.glow(L, 0, 14, R8[3], 0.5 * f); }
}
