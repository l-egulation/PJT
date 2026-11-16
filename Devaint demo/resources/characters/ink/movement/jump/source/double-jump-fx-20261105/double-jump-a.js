import { Rng, hex, clamp, smooth, ease, noise2 } from "../../core/index.js";
import { frameOf, field, stepFade, paint, eaten } from "./_double-jump.js";
// A 뭉게 발판 — 공기를 한 번 밟은 자리. 발밑에서 납작한 구름 덩이가 좌우로 밀려 퍼지고,
// 가운데부터 갉혀 두 갈래로 갈라진 뒤 네 계단으로 옅어진다. 종이색 몸 + 회갈색 그늘 + 먹빛 아래 테.
export const meta = {
  name: "Double jump A (puff step)", desc: "A flat cloud puff kicked out under the feet: spreads sideways, splits from the middle, fades in four steps.",
  tags: ["move", "jump", "double-jump", "puff", "one-shot"], size: [180, 110], origin: [90, 34], duration: 0.48, loop: false, fps: 25, bg: "light",
  options: { palette: ["#fffdf6", "#efe8da", "#cfc5b4", "#8c8273", "#4a433b"], seed: 7 },
};

export function create(o) {
  const [LIT, BODY, SHADE, DEEP, RIM] = o.palette.map(hex), R = new Rng(o.seed);
  const blobs = [];
  // 가운데 덩이 하나 + 좌우로 밀려 나가는 덩이들. 아래로 조금 가라앉는다
  blobs.push({ x: 0, y: 3, vx: 0, vy: 30, r0: 13, grow: 0.35, life: 0.3 });
  for (let i = 0; i < 8; i++) {
    const side = i % 2 ? 1 : -1, sp = R.range(150, 330) * (i < 2 ? 1.15 : 1);
    blobs.push({ x: side * R.range(2, 8), y: R.range(0, 6), vx: side * sp, vy: R.range(20, 90), r0: R.range(7, 12), grow: R.range(0.25, 0.6), life: R.range(0.3, 0.44), d: R.range(0, 0.04) });
  }
  let t = 0;
  for (const b of blobs) (b.age = 0), (b.r = b.r0 * 0.5);
  return {
    update(dt) {
      t += dt;
      for (const b of blobs) {
        if ((b.d ?? 0) > t) continue;
        b.age += dt;
        b.vx *= 1 - 7.5 * dt; b.vy *= 1 - 7 * dt; b.vy -= 25 * dt; // 끝무렵 살짝 떠오른다
        b.x += b.vx * dt; b.y += b.vy * dt;
        const k = clamp(b.age / b.life);
        b.r = b.r0 * (0.55 + 0.45 * ease.outCubic(clamp(k * 4))) * (1 + b.grow * k) * (1 - smooth(0.7, 1, k) * 0.6);
      }
    },
    draw(p) {
      const f = frameOf(t), live = blobs.filter((b) => b.age > 0 && b.age < b.life);
      if (!live.length) return;
      const list = live.map((b) => ({ x: b.x, y: b.y, r: b.r, s: 1.35 + Math.min(1, Math.abs(b.vx) / 260) }));
      const fade = stepFade(smooth(0.2, 0.46, t)), eat = smooth(0.06, 0.32, t);
      p.shade(-90, -34, 90, 76, (x, y) => {
        const wx = x + 1.6 * noise2(x * 0.11, y * 0.11 + f * 0.5, o.seed), wy = y + 1.6 * noise2(x * 0.11 + 7, y * 0.11 - f * 0.45, o.seed + 1);
        const F = field(list, wx, wy);
        if (F < 0.3) return;
        // 가운데부터 갉아 두 갈래로 가른다
        if (eaten(x, y - 4, eat, 30, 14, f, o.seed + 3, noise2)) return;
        let c;
        if (F < 0.4) c = wy > 2 ? RIM : DEEP; // 아래쪽 가장자리만 먹빛 테
        else {
          const lower = field(list, wx - 2.5, wy - 3.5); // 빛은 왼쪽 위에서
          c = lower < F * 0.62 ? SHADE : F > 1.1 ? LIT : BODY;
          if (lower < F * 0.4) c = DEEP;
        }
        paint(p, x, y, c, fade);
      });
    },
  };
}
