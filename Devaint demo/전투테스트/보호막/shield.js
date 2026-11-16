import { fit, clamp, smooth, mix, rand, noise2, TAU, WHITE } from "../../core/index.js";

// 보호막(초록) — 캐릭터를 덮는 반투명 돔(방벽). 딱딱한 테·바닥 고리 없이:
//   속은 옅게(fill), 가장자리로 갈수록 진해지고(edge), 테 자리에는 흐린 빛 띠(rim)가 번진다.
//   power 가 2 면 타원, 클수록 위가 납작한 완만한 돔. pattern 이 "waves" 면 막 위로 옅은 물결 결이 흐른다.
//   막 안에는 별이 깜빡이고, 빛 띠가 아래에서 위로 훑는다. 반복. 원점은 플레이어 발 가운데.
export const meta = {
  name: "Shield (barrier dome)",
  desc: "A translucent dome over the character with no hard outline: faint fill, denser soft edge, a blurred glow band at the rim, twinkling stars and a soft band sweeping up. Loops.",
  tags: ["ink", "shield", "buff", "loop"],
  size: [152, 96],
  origin: [76, 86],
  duration: 0.96,
  loop: true,
  fps: 25,
  bg: "light",
  options: {
    palette: ["#171716", "#1d4128", "#2f7444", "#5fb37a", "#a6dcb1", "#f4fff4"],
    rx: 60, ry: 70, power: 2, fill: 0.16, edge: 0.32, rim: 0.2, pattern: "waves", stars: 16, seed: 4,
  },
};

function star(p, x, y, s, a, C) {
  const [, , , BODY, LIT] = C;
  if (a <= 0.02) return;
  p.blend(x, y, WHITE, a);
  for (let k = 1; k <= s; k++) {
    const c = k === 1 ? LIT : BODY, b = a * (1 - (k - 1) / s) * 0.85;
    p.blend(x + k, y, c, b); p.blend(x - k, y, c, b); p.blend(x, y + k, c, b); p.blend(x, y - k, c, b);
  }
}

// 돔 한 장. grow 는 세로 크기 배율(생길 때), fade 는 전체 세기, erode 는 0..1 로 잡음 구멍이 번져 막이 부서지는 정도,
// flash 는 막 전체가 밝게 번쩍이는 정도. 유지·생길 때·깨질 때가 함께 쓴다
export function dome(p, t, o, { grow = 1, fade = 1, erode = 0, flash = 0, stars = true } = {}) {
  const C = fit(o.palette, 6), [, , , BODY, LIT] = C, ph = (t % meta.duration) / meta.duration;
  const RX = o.rx * (0.85 + 0.15 * grow), RY = o.ry * grow, N = o.power, FEATHER = 0.08, M = 1 + FEATHER + 0.06;
  const sweep = -RY * (ph * 1.3 - 0.15);
  for (let y = Math.floor(-RY * M) - 1; y <= 0; y++)
    for (let x = Math.floor(-RX * M) - 1; x <= RX * M; x++) {
      const dx = (x + 0.5) / RX, dy = (y + 0.5) / RY;
      const e = (Math.abs(dx) ** N + Math.abs(dy) ** N) ** (1 / N); // 1 = 막 표면
      if (e > M) continue;
      const inner = o.fill + o.edge * Math.min(1, e) ** 5;
      const out = e > 1 ? 1 - smooth(1, 1 + FEATHER, e) : 1;
      // 흐린 빛 띠: 표면 근처에 부드럽게 퍼진 밝은 띠(선이 아니라 번짐)
      const rim = o.rim * Math.exp(-(((e - 0.985) / 0.035) ** 2));
      const band = Math.exp(-(((y + 0.5 - sweep) / 7) ** 2)) * 0.13 * (e < 1 ? 1 : 0);
      const sheen = Math.exp(-(((dx + 0.45) / 0.18) ** 2 + ((dy + 0.62) / 0.16) ** 2)) * 0.22;
      const wave = o.pattern === "waves" && e < 1 ? 0.07 * Math.max(0, Math.sin(dy * 9 + dx * dx * 3 - ph * TAU)) ** 6 : 0;
      const bottom = smooth(0, 4, -y); // 바닥에 닿는 곳은 살짝 흐리게 녹인다
      if (erode > 0 && noise2(x * 0.18, y * 0.18, o.seed) * 0.5 + 0.5 < erode * 1.15 - 0.1 + (1 - Math.min(1, e)) * erode * 0.3) continue;
      const a = ((inner * out + rim + band + sheen + wave) * bottom + flash * 0.14 * (0.4 + Math.min(1, e) ** 3) * out * bottom) * fade; // 번쩍임은 가장자리 위주: 속까지 칠하면 캐릭터가 초록에 묻힌다
      if (a <= 0.004) continue;
      p.blend(x, y, mix(BODY, LIT, Math.min(1, 0.3 + band * 4 + sheen * 3 + rim * 1.5 + wave * 6 + flash)), Math.min(1, a));
    }
  // 막 안 별: 각자 박자로 깜빡인다
  if (stars) for (let i = 0; i < o.stars; i++) {
    const u = rand(o.seed, i, 1), v = rand(o.seed, i, 2);
    const ang = Math.PI * (0.08 + 0.84 * u), r = 0.25 + 0.6 * v;
    const x = Math.round(Math.cos(ang) * RX * r), y = Math.round(-Math.sin(ang) * RY * r);
    const blink = Math.max(0, Math.sin(TAU * (ph * 2 + rand(o.seed, i, 3)))) ** 3;
    star(p, x, y, 2 + (rand(o.seed, i, 4) > 0.6 ? 1 : 0), blink * fade, C);
  }
}

export function draw(p, t, o) {
  dome(p, t, o);
}
