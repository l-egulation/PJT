// Blue 압박 벽(2페이즈 패턴 4, 오른쪽): 잉크 해일. 반 해상도(1px = 게임 2px)로 그려 2배로 키우고, 게임에서는 좌우를 뒤집어 오른쪽 벽에 둔다.
// 몸통을 칸마다 깊이·결로 칠하는 방식, 마루에서 던져지는 입술(바깥 가장자리 밝고 아래 짙은 굵은 호), 거품·물보라·안개는
// pixelfx water/wave-crash를 따랐고, 그 입술은 Sprite Fusion's open-source slash (https://www.spritefusion.com/pixel-art-vfx/slash),
// MIT License, Copyright (c) 2026 Hugo Duprez 를 각색한 것이다.
//
// 시간: 0~0.5초 전조(벽에서 잉크가 스며 바닥에 고이고 거품이 오르며, 바닥 솔기가 최종 앞면까지 달림, 앞면 자리에 세로 점선),
//       0.5~1.3초 해일이 0→6.09u(195px)로 밀려오며 앞면에서 마루가 말려 쏟아진다, 1.4초까지 유지,
//       1.4초부터 무너져 거품 레이스로 번지며 2.0초에 비어 끝난다.
import { Particles, Lights, P, Rng, fit, sparkle, clamp, smooth, ease, noise, noise2, TAU } from "../../core/index.js";
import { Mist } from "../water/_lib.js";

export const meta = {
  name: "Ink tide",
  desc: "Blue's side press: ink seeps from the wall and bubbles, then a black ink tide surges in, its crest curling over and breaking in pale foam; it holds, then collapses into lace.",
  tags: ["water", "ink", "wave", "wall", "roseblue", "one-shot"],
  size: [260, 132],
  origin: [16, 124],
  duration: 2.0,
  loop: false,
  fps: 30,
  bg: "ground",
  options: { palette: ["#020309", "#050a16", "#0a1328", "#111e3c", "#1b3058", "#2c4a7c", "#6488b4", "#c4dcf0"], seed: 9 },
};

const ZW = 195, ZH = 112, TELL = 0.5, GROW = 0.8, END = 1.4, FALL = 0.45;
const front = (t) => clamp((t - TELL) / GROW) * ZW;
// 무너짐 0..1
const fall = (t) => ease.inOutSine(clamp((t - END) / FALL));

export function create(o) {
  const C = fit(o.palette, 8), R = new Rng(o.seed), S = o.seed;
  const ps = new Particles({ rng: R, ground: () => 0 }), mist = new Mist(), lights = new Lights(), puffs = [], drops = [], bubbles = [];
  let t = 0;
  // 수면 높이: 뒤쪽은 낮게 출렁이고 앞쪽으로 갈수록 솟아 앞면에서 마루가 된다. 앞면 너머로는 가파른 얼굴로 떨어진다.
  const height = (x, tt) => {
    const fr = front(tt); if (fr < 2) return 0;
    const r = clamp(x / fr), swell = ZH * (0.6 + 0.36 * Math.pow(r, 2.4)) + 5 * Math.sin(x * 0.11 - tt * 9) + 3 * Math.sin(x * 0.27 + tt * 13);
    const face = x <= fr ? 1 : Math.exp(-(((x - fr) / 7) ** 2));
    const grow = smooth(TELL, TELL + 0.12, tt);
    return Math.max(0, swell * face * grow * (1 - 0.94 * fall(tt)) * (1 - smooth(END + FALL - 0.05, meta.duration - 0.12, tt)));
  };
  // 입술: 마루에서 앞으로 던져져 아래로 말린다. s 0이 마루, 1이 끝. 두께 w.
  const lip = (s, tt) => {
    const fr = front(tt), A = height(fr - 4, tt), k = (1 - fall(tt)) * smooth(TELL + 0.05, TELL + 0.25, tt);
    const wob = Math.sin(tt * 11) * 1.5;
    return [fr - 6 + s * 17 * k, -A - 6 * s * (1 - s) * (1 + 2 * k) + s * s * A * 0.3 * k + wob * s, (13 - 9 * s) * (0.55 + 0.45 * k), k];
  };
  // 거품 덩어리: 그늘진 아래, 창백한 몸, 밝은 위
  const foam = (p, x, y, r, a = 1) => {
    if (r < 0.9) return p.dot(x, y, C[6], a);
    p.circle(x + r * 0.12, y + r * 0.2, r, C[4], a);
    p.circle(x, y, r * 0.84, C[6], a);
    if (r > 1.6) p.circle(x - r * 0.3, y - r * 0.32, r * 0.46, C[7], a);
  };
  const puff = (x, y, vx, vy, r0, r1, life) => puffs.push({ x, y, vx, vy, r0, r1, life: Math.min(life, meta.duration - 0.05 - t), max: life });

  // 몸통: 칸마다. 수면 바로 아래 밝은 테, 그 아래 결(위로 끌려 올라가는 줄)과 해류 소용돌이, 깊을수록 짙다.
  function body(p) {
    const fr = front(t);
    for (let X = 0; X < Math.min(ZW + 20, fr + 18); X++) {
      const x = X + 0.5, h = height(x, t);
      if (h < 0.6) continue;
      const near = Math.exp(-(((x - fr) / 14) ** 2)), A = h;
      for (let Y = Math.floor(-h); Y < 0; Y++) {
        const y = Y + 0.5, depth = y + h, u = depth / h;
        let c;
        if (depth < 1) c = noise(x * 0.5 + t * 8, S) > -0.2 ? C[7] : C[6];
        else if (depth < 2.6) c = near > 0.3 && noise2(x * 0.5, y * 0.5 + t * 6, S + 1) > -0.1 ? C[7] : C[5];
        else {
          const up = noise2((x * 0.8 - y * 0.3) * 0.7, y * 0.1 + t * 4, S + 2), swirl = noise2(x * 0.09 - t * 1.6, y * 0.09 + t * 0.7, S + 3);
          let i = u < 0.08 ? 4 : u < 0.2 ? 3 : u < 0.42 ? 2 : u < 0.72 ? 1 : 0;
          if (up > 0.55 && u < 0.55) i = Math.min(5, i + 1);
          if (Math.abs(swirl) < 0.025 && u > 0.25 && u < 0.85) i = 3; // 해류 소용돌이 선
          if (near > 0.6 && u < 0.4) i = Math.min(5, i + 1); // 앞면 얼굴은 빛이 비친다
          c = C[i];
        }
        p.px(X, Y, c);
      }
      // 얼굴 가장자리 어두운 테
      if (x > fr - 2) p.px(X + 1, -Math.round(h * 0.5), C[0]);
    }
  }
  // 입술 그리기
  function curl(p) {
    const [, , , k] = lip(0, t);
    if (k <= 0.05) return;
    const cells = new Map();
    for (let s = 0; s <= 1; s += 0.012) {
      const [x, y, w] = lip(s, t), [x2, y2] = lip(Math.min(1, s + 0.012), t), tx = x2 - x, ty = y2 - y, l = Math.hypot(tx, ty) || 1, nx = ty / l, ny = -tx / l;
      for (let q = 0; q <= 1; q += 0.5 / Math.max(1, w)) {
        const px = x + nx * (q - 0.5) * w, py = y + ny * (q - 0.5) * w, key = Math.floor(py) * 1024 + Math.floor(px);
        const c = q < 0.18 ? C[7] : q < 0.36 ? (s > 0.8 ? C[7] : C[6]) : q < 0.56 ? C[4] : q < 0.76 ? C[3] : C[1];
        const cur = cells.get(key);
        if (!cur || q < cur.q) cells.set(key, { x: px, y: py, c, q });
      }
    }
    for (const c of cells.values()) p.px(c.x, c.y, c.c);
    for (let s = 0; s <= 1; s += 0.2) { const [x, y, w] = lip(s, t); p.glow(x, y, 5 + w, C[5], 0.18 * k); }
    const [tx, ty] = lip(1, t);
    sparkle(p, tx, ty, 4 * k, C[7], C[6], 0.35 * k);
  }

  return {
    update(dt) {
      t += dt;
      const fr = front(t), f = Math.min(1, dt * 120);
      // 전조: 바닥 잉크에서 거품이 오른다
      if (t < TELL + 0.1 && R.chance(0.5 * f)) bubbles.push({ x: R.range(4, ZW - 4) * clamp(t / 0.3), y: -1, vy: -R.range(12, 30), r: R.range(1, 2.4), life: R.range(0.25, 0.5), ph: R.range(0, TAU) });
      // 밀려오는 동안: 마루에서 물보라, 입술 끝에서 방울, 얼굴에 거품
      if (t > TELL + 0.05 && t < END) {
        const [lx, ly] = lip(1, t), A = height(fr - 4, t);
        if (R.chance(0.6 * f)) ps.spawn(P.FLAKE, fr - 4 + R.range(-4, 3), -A - R.range(0, 3), R.range(-10, 50), -R.range(10, 40), R.range(0.35, 0.6), R.chance(0.5) ? C[7] : C[6], R.range(0.6, 1.3));
        if (R.chance(0.45 * f)) drops.push({ x: lx, y: ly, vx: R.range(30, 90), vy: R.range(-40, 20), r: R.chance(0.3) ? 1.6 : 1, life: R.range(0.3, 0.55) });
        if (R.chance(0.5 * f)) puff(fr + R.range(-4, 8), -R.range(2, A * 0.6), R.range(10, 60), -R.range(10, 40), R.range(0.9, 1.8), R.range(1.8, 3.4), R.range(0.3, 0.6));
        if (R.chance(0.25 * f)) puff(fr + R.range(-2, 10), -R.range(1, 4), R.range(20, 70), -R.range(5, 20), R.range(1, 2), R.range(2, 3.6), R.range(0.3, 0.6));
      }
      // 무너질 때: 앞면에서 거품이 크게 일고 안개가 퍼진다
      if (t > END && t < END + FALL && R.chance(0.7 * f)) { const x = R.range(0, ZW); puff(x, -R.range(1, height(x, t) * 0.7 + 3), R.range(-20, 40), -R.range(10, 50), R.range(1, 2), R.range(2, 4), R.range(0.3, 0.5)); }
      if (t > END && t < END + 0.15 && R.chance(0.4 * f)) mist.add(R.range(0, ZW), -R.range(10, 50), R.range(-10, 30), -R.range(4, 12), 4, R.range(10, 16), R.range(0.35, 0.5), C[5], 0.22);
      if (Math.abs(t - (TELL + GROW)) < dt) { lights.add(ZW, -ZH * 0.8, 50, C[5], 0.5, 0.35); }
      for (let i = puffs.length - 1; i >= 0; i--) { const q = puffs[i]; if ((q.life -= dt) <= 0) { puffs.splice(i, 1); continue; } q.vx -= q.vx * 2.5 * dt; q.vy += (140 - q.vy * 2.5) * dt; q.x += q.vx * dt; q.y = Math.min(q.y + q.vy * dt, -1); }
      for (let i = drops.length - 1; i >= 0; i--) { const d = drops[i]; if ((d.life -= dt) <= 0 || d.y > 0) { drops.splice(i, 1); continue; } d.vy += 520 * dt; d.x += d.vx * dt; d.y += d.vy * dt; }
      for (let i = bubbles.length - 1; i >= 0; i--) { const b = bubbles[i]; if ((b.life -= dt) <= 0) { bubbles.splice(i, 1); continue; } b.y += b.vy * dt; b.x += Math.sin(t * 9 + b.ph) * 6 * dt; }
      mist.update(dt);
      ps.update(dt);
      lights.update(dt);
    },
    drawBack(p) {
      // 전조: 벽에서 스며 나온 잉크가 바닥에 고여 앞으로 번지고, 솔기가 최종 앞면까지 달린다
      const seep = clamp(t / 0.45) * (1 - smooth(TELL + 0.1, TELL + 0.3, t)), run = ease.outCubic(clamp(t / 0.36)) * ZW, fade = 1 - smooth(END + FALL, meta.duration - 0.1, t);
      for (let X = 0; X <= run; X++) {
        const x = X + 0.5, puddle = seep * Math.max(0, 1 - x / (30 + 60 * seep)) * (5 + 2 * Math.sin(x * 0.4 + t * 6)), hot = clamp(1 - (run - x) / 14) * (1 - smooth(0.4, 0.6, t));
        for (let Y = -Math.ceil(puddle); Y < 0; Y++) p.px(X, Y, Y === -Math.ceil(puddle) ? C[4] : C[1]);
        p.dot(X, 0, hot > 0.4 ? C[6] : hot > 0.1 ? C[5] : C[2], fade);
        if (noise(x * 0.3, S + 9) > 0.3) p.dot(X, 1, C[1], fade * 0.7);
        if (hot > 0.3 && (X & 3) === 0) p.glow(X, 0, 6, C[5], 0.3 * hot);
      }
      if (t > 0.3 && t < TELL + 0.1) { const k = smooth(0.3, 0.42, t) * (1 - smooth(TELL, TELL + 0.1, t)) * (0.6 + 0.4 * Math.sin(t * 40)); for (let y = -ZH; y < 0; y += 3) p.dot(ZW, y, C[5], k); p.glowLine(ZW, -ZH, ZW, 0, 5, C[4], 0.12 * k); }
      for (const b of bubbles) { p.ring?.(b.x, b.y, b.r, 1, C[5]); p.px(b.x - b.r * 0.4, b.y - b.r * 0.4, C[7]); }
      ps.draw(p, "back");
    },
    draw(p) {
      body(p);
      curl(p);
      // 무너진 뒤 바닥에 남는 거품 레이스
      if (t > END) {
        const k = 1 - smooth(END + 0.2, meta.duration - 0.1, t);
        for (let X = 0; X < ZW + 10; X++) { const x = X + 0.5, n = 1 - Math.abs(noise2(x * 0.3, t * 0.8, S + 7)) * 4; if (n > 1.15 - k * 1.1) p.px(X, -1, n > 1.4 - k ? C[7] : C[6]); if (n > 1.4 - k * 0.9) p.px(X, 0, C[5]); }
      }
      for (const q of puffs) { if (q.y < -1 && q.y > -height(q.x, t) + 3 && q.x < front(t) - 3) continue; const g = 1 - q.life / q.max, r = (q.r0 + (q.r1 - q.r0) * ease.outCubic(g)) * (1 - smooth(0.7, 1, g)); if (r > 0.6) foam(p, q.x, q.y, r); }
      for (const d of drops) { p.px(d.x, d.y, C[7]); if (d.r > 1) p.px(d.x - 1, d.y, C[6]); p.px(d.x - Math.sign(d.vx), d.y - Math.sign(d.vy), C[4]); }
      ps.draw(p, "front");
      mist.draw(p, C[4]);
      lights.draw(p);
      const fr = front(t);
      if (t > TELL && t < END + FALL) { const A = height(fr - 4, t); p.glow(fr - 4, -A + 4, 26, C[4], 0.2 * (1 - fall(t))); p.glowLine(fr - 40, -A * 0.7, fr - 4, -A, 8, C[5], 0.1 * (1 - fall(t))); }
    },
  };
}
