import { fit, mix, smooth, ease, TAU } from "../../core/index.js";

// 둔화(보라) — 몸 가운데에서 부드러운 보라 물결이 천천히 바깥으로 퍼져 나가며 옅어진다(선 없이 번짐만).
// 몬스터 몸을 보라로 물들이는 건 게임이 스프라이트 색으로 한다. 반복. 원점은 몬스터 발 가운데, cy 는 몸 가운데 높이.
export const meta = {
  name: "Slow (spreading ripples)",
  desc: "Soft violet ripples spread slowly outward from the body and fade, glow only with no lines. Loops.",
  tags: ["ink", "slow", "debuff", "loop"],
  size: [160, 104],
  origin: [80, 92],
  duration: 3.0,
  loop: true,
  fps: 25,
  bg: "light",
  options: { palette: ["#171716", "#3a1d52", "#64348c", "#9a5cc8", "#cfa2ec", "#fbf2ff"], cy: -28, waves: 3, strength: 1.0, width: 3.2, reach: 26, shape: "wavy", seed: 2 },
};

// waves: 한 반복에 겹쳐 퍼지는 물결 수, strength: 물결 진하기, width: 물결 두께(px), reach: 물결이 퍼지는 거리(px).
// shape: "wavy" 몸 둘레로 퍼지는 테가 물결치듯 일렁임 / "ground" 발밑 바닥에 납작한 물결 / "side" 몸 양옆으로 세로 물결이 밀려남.
// 물결 하나가 퍼지는 시간 = duration(시안 비교 때는 period 옵션으로 바꿔 본다)
function distOf(o, x, y, ph, k) {
  const wob = (a) => Math.sin(a + ph * TAU + k * 2.1);
  if (o.shape === "ground") return { d: Math.hypot(x, y * 4.5) + 1.5 * wob(Math.atan2(y * 4.5, x) * 6), fade: y > 4 || y < -10 ? 0 : 1 };
  if (o.shape === "side") {
    const fy = 1 - Math.min(1, Math.abs(y - o.cy) / 30) ** 2;   // 몸 높이 안에서만
    return { d: Math.abs(x) - 10 + 2.2 * wob(y * 0.32), fade: fy };
  }
  const dx = x / 1.1, dy = y - o.cy, a = Math.atan2(dy, dx);
  return { d: Math.hypot(dx, dy) + 2.2 * wob(a * 5) + 1.2 * wob(a * 9 + 1), fade: y > 6 ? 0 : 1 };
}

export function draw(p, t, o) {
  const C = fit(o.palette, 6), [, , SHADE, BODY, LIT] = C, ph = t / (o.period || meta.duration);
  const R0 = o.shape === "side" ? 0 : 14;
  for (let y = -80; y <= 8; y++)
    for (let x = -76; x <= 76; x++) {
      let a = 0, lit = 0;
      for (let k = 0; k < o.waves; k++) {                // 물결들이 같은 간격으로 엇갈려 퍼진다
        const u = (ph + k / o.waves) % 1, { d, fade } = distOf(o, x, y, ph, k);
        if (!fade) continue;
        const r = R0 + o.reach * ease.outQuad(u), w = o.width * (0.8 + 0.6 * u);
        const life = (1 - u) ** 1.2 * smooth(0, 0.12, u) * fade;
        a += Math.exp(-(((d - r) / w) ** 2)) * life;
        lit += Math.exp(-(((d - r + w * 0.4) / (w * 0.4)) ** 2)) * life; // 물결 안쪽 가장자리가 조금 밝다
      }
      const dc = Math.hypot(x / 1.1, y - o.cy);
      const core = Math.exp(-((dc / 22) ** 2)) * 0.12;    // 몸 둘레에 머무는 은은한 빛
      const A = o.strength * a + core;
      if (A > 0.01) p.blend(x, y, mix(mix(SHADE, BODY, Math.min(1, a)), LIT, Math.min(0.6, lit * 0.6)), Math.min(0.85, A));
    }
}
