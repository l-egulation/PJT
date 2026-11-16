import { Rng, fit, clamp, ease } from "../../core/index.js";

// 숨죽이기 시안(2026-11-07). 0.10초에 은신(게임은 몸을 35% 로 흐리게 한다)에 들어가는 순간만 보인다.
//   variant="a" 먹 물결: 발밑에서 납작한 먹 물결 두 겹이 퍼지고, 몸에서 먹 조각이 위로 스며 올라가 사라진다.
//   variant="b" 그림자 덮기: 발밑 먹이 혀처럼 솟아 몸을 한 번 덮었다가, 위에서부터 조각으로 부서져 흩어지며 흐려진 몸이 드러난다.
// 원점 = 플레이어 발 가운데, 앞이 +x, 시각 0 = 시전 시작(효과 0.10초). 먹색만(흰 빛 없음).
export const meta = {
  name: "Dagger hold breath (ink)",
  desc: "Entering stealth: an ink ripple at the feet or a brief ink shroud that crumbles away as the body fades.",
  tags: ["ink", "dagger", "stealth", "one-shot"],
  size: [112, 104],
  origin: [56, 96],
  duration: 0.6,
  loop: false,
  fps: 50,
  bg: "light",
  options: { variant: "a", seed: 5, at: 0.1 },
};
const PAL = ["#1b1616", "#3a3230", "#5a4e4a"];

export function create(o) {
  const [INK, INK2, INK3] = fit(PAL, 3), R = new Rng(o.seed);
  const flakes = Array.from({ length: 14 }, () => ({ x: R.range(-14, 16), y: R.range(-58, -6), vy: -R.range(30, 70), vx: R.range(-12, 12), s: R.range(2, 4), d: R.range(0, 0.08), life: R.range(0.22, 0.36) }));
  // b: 3x3 칸 덮개. 칸마다 부서지는 시각(위가 먼저)과 날아갈 방향
  const cells = [];
  for (let y = -66; y < 0; y += 3) for (let x = -18; x < 21; x += 3) {
    const k = -y / 66;
    cells.push({ x, y, tb: 0.05 + (1 - k) * 0.12 + R.range(0, 0.05), vx: (x + R.range(-6, 6)) * R.range(2, 4), vy: -R.range(20, 70), life: R.range(0.1, 0.2), top: R.range(0.8, 1) });
  }
  let t = 0;
  return {
    update(dt) { t += dt; },
    draw(p) {
      const g = t - o.at + 1e-6;
      if (o.variant === "b") {
        // 몸이 먹빛 실루엣이 되는 건 게임 코드(몸 그림 먹색 칠, 0.10~0.25초 1→0). 여기는 그 실루엣에서 떨어져 나가는 조각만.
        for (const c of cells) {
          if (((c.x + 1.5) / 20) ** 2 + ((c.y + 30) / 34) ** 2 > 1) continue;   // 몸 둘레 타원 안 칸만
          const q = g - c.tb;
          if (q < 0 || q > c.life) continue;
          const k = q / c.life, s = Math.max(1, Math.round(3 * (1 - k))), x = c.x + c.vx * q, y = c.y + c.vy * q;
          for (let yy = 0; yy < s; yy++) for (let xx = 0; xx < s; xx++) p.px(x + xx, y + yy, k < 0.5 ? INK : INK2);
        }
        return;
      }
      for (let i = 0; i < 2; i++) {
        const q = g - i * 0.07, life = 0.34;
        if (q < 0 || q > life) continue;
        const k = ease.outCubic(q / life), rx = 6 + 42 * k, ry = 1.5 + 5 * k, th = 2.2 * (1 - k) + 0.8;
        for (let y = Math.floor(-ry - 3); y <= ry + 3; y++) for (let x = Math.floor(-rx - 3); x <= rx + 3; x++) {
          const d = Math.hypot((x + 0.5) / rx, (y + 0.5) / ry) - 1;
          if (Math.abs(d) * Math.min(rx, ry * 4) <= th) p.px(x, y - 1, k < 0.6 ? INK : INK2);
        }
      }
      for (const f of flakes) {
        const q = g - f.d;
        if (q < 0 || q > f.life) continue;
        const k = q / f.life, s = Math.max(1, Math.round(f.s * (1 - k))), x = f.x + f.vx * q, y = f.y + f.vy * q;
        for (let yy = 0; yy < s; yy++) for (let xx = 0; xx < s; xx++) p.px(x + xx, y + yy, k < 0.4 ? INK : k < 0.75 ? INK2 : INK3);
      }
    },
  };
}
