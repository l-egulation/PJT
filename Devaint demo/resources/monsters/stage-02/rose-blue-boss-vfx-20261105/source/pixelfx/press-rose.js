// Rose 압박 벽(2페이즈 패턴 4, 왼쪽): 가시 장미 덩굴 벽. 반 해상도(1px = 게임 2px)로 그려 2배로 키워 쓴다.
// 덩굴이 솟는 방식(앞면을 따라 차례로, 넘치게 솟았다 자리 잡음, 흰 한 칸, 흙 파편과 빛)은 pixelfx nature/thorns를 따랐고,
// 그 효과는 Sprite Fusion's open-source ice spikes (https://www.spritefusion.com/pixel-art-vfx/ice), MIT License,
// Copyright (c) 2026 Hugo Duprez 를 각색한 것이다.
//
// 시간: 0~0.5초 전조(바닥 솔기가 벽에서 최종 앞면까지 달리며 가시 싹이 돋음, 앞면 자리에 세로 점선),
//       0.5~1.3초 앞면이 0→6.09u(195px)로 밀려오며 지나간 자리마다 가시 덩굴이 솟고 장미가 핀다, 1.4초까지 유지,
//       1.4초부터 덩굴이 가라앉고 꽃잎이 흩날리며 2.0초에 비어 끝난다.
import { Particles, Lights, P, Rng, fit, clamp, smooth, ease, rand, TAU, WHITE } from "../../core/index.js";

export const meta = {
  name: "Rose thorn wall",
  desc: "Rose's side press: a seam sprouts thorns, then hooked bramble vines punch up behind an advancing front, roses open at their tips; it holds, then sinks in falling petals.",
  tags: ["nature", "thorns", "rose", "wall", "roseblue", "one-shot"],
  size: [248, 132],
  origin: [16, 124],
  duration: 2.0,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#0e0408", "#1e0710", "#360c1a", "#521428", "#721c36", "#962a48", "#c04a64", "#eaa0b0"],
    glow: ["#2a0610", "#6a1230", "#a83652", "#f08aa4"],
    soil: ["#0c080a", "#1a1214", "#2a1e20", "#3c2c2c", "#523c38", "#6c5048", "#8c6a5c", "#b8968a"],
    seed: 5,
  },
};

const ZW = 195, ZH = 112, TELL = 0.5, GROW = 0.8, END = 1.4, RISE = 0.13, SINK = 0.34;
const front = (t) => clamp((t - TELL) / GROW) * ZW;

// 덩굴 줄기: 밑동에서 끝으로 가늘어지고, 앞(+x)으로 기울며 끝이 갈고리처럼 휜다. 왼쪽이 밝고 능선이 서며 오른쪽은 그늘.
// 가시가 양쪽으로 번갈아 돋고, 어두운 테두리를 두른다.
function stem(p, s, h, C, { base = 0, white = false } = {}) {
  if (h < 2) return;
  const cx = (u) => s.x + s.lean * u + Math.sin(u * 4.2 + s.ph) * s.sway * u + s.hook * Math.pow(u, 2.6);
  const hw = (u) => (s.w / 2) * Math.pow(1 - u, 0.75) + 0.4;
  const inside = (px, py) => {
    const u = (base - (py + 0.5)) / h;
    if (u < 0 || u > 1 || py + 0.5 > 0) return false;
    return Math.abs(px + 0.5 - cx(u)) <= hw(u);
  };
  const X0 = Math.floor(s.x - s.w - Math.abs(s.sway) - 4), X1 = Math.ceil(s.x + s.w + Math.abs(s.lean) + Math.abs(s.hook) + Math.abs(s.sway) + 4);
  for (let py = Math.floor(base - h) - 1; py <= Math.min(0, Math.ceil(base)); py++)
    for (let px = X0; px <= X1; px++) {
      if (!inside(px, py)) {
        if (py <= 0 && (inside(px - 1, py) || inside(px + 1, py) || inside(px, py + 1) || inside(px, py - 1))) p.px(px, py, C[0]);
        continue;
      }
      if (white) { p.px(px, py, WHITE); continue; }
      const u = (base - (py + 0.5)) / h, c = cx(u), r = hw(u), q = (px + 0.5 - (c - r)) / (2 * r);
      let i = q < 0.18 ? 5 : q < 0.34 ? 4 : q < 0.44 ? 6 : q < 0.7 ? 3 : q < 0.86 ? 2 : 1;
      if (i > 1 && i < 6 && rand(px, Math.floor((py + 0.5 - base) / 3), s.seed) < 0.14) i -= 1; // 결
      p.px(px, py, C[i]);
    }
  // 가시: 줄기를 따라 양쪽으로 번갈아, 위로 갈수록 작다. 밝은 끝.
  for (const [u, side] of s.barbs) {
    const y = base - u * h; if (y > 0) continue;
    const ex = cx(u) + side * hw(u), L = (2.5 + s.w * 0.35) * (1 - u * 0.5);
    const tx = ex + side * L, ty = y - L * 0.8;
    p.tri(ex, y + 1, ex, y - 1.5, tx, ty, white ? WHITE : C[3]);
    p.line(ex, y - 1, tx, ty, white ? WHITE : C[5]);
    p.px(tx, ty, white ? WHITE : C[7]);
  }
}

// 장미: 어두운 바깥 꽃잎 → 붉은 속 꽃잎 → 감긴 속 → 밝은 심. open 0..1로 벌어진다.
function rose(p, x, y, r, open, C, G, a = 1) {
  if (open <= 0.02) return;
  const R = r * (0.45 + 0.55 * ease.outBack(open, 1.6));
  for (let k = 0; k < 5; k++) {
    const ang = k * (TAU / 5) - 1.2, px = x + Math.cos(ang) * R * 0.5, py = y + Math.sin(ang) * R * 0.42;
    p.circle(px, py, R * 0.62 + 0.8, C[1], a);
    p.circle(px, py, R * 0.62, k < 2 ? C[5] : C[4], a);
  }
  p.circle(x, y, R * 0.62, C[4], a);
  p.circle(x - R * 0.12, y - R * 0.12, R * 0.42, C[5], a);
  p.arc(x, y, R * 0.36, 1, -2.2, 1.6, C[6], a);
  p.arc(x + 0.5, y + 0.3, R * 0.18, 1, 0.5, 3.8, C[3], a);
  p.px(x - R * 0.2, y - R * 0.3, C[7]);
  p.glow(x, y, R + 4, G[2], 0.14 * open * a);
}

export function create(o) {
  const C = fit(o.palette, 8), G = fit(o.glow, 4), S = fit(o.soil, 8), R = new Rng(o.seed), floor = () => 0;
  const ps = new Particles({ rng: R, ground: floor, sparks: [G[3], G[2], G[1], G[0]] }), lights = new Lights();
  // 덩굴: 벽 쪽은 촘촘하고 크며, 앞면 쪽 마지막 두세 줄기는 가장 높게 솟아 안쪽으로 휜다.
  const N = 17;
  const stems = Array.from({ length: N }, (_, i) => {
    const x = 4 + (i / (N - 1)) * (ZW - 10) + R.range(-3, 3), nearFront = i >= N - 3;
    const h = (nearFront ? 0.9 : R.range(0.5, 0.86)) * ZH, w = R.range(5, 8) + (nearFront ? 2 : 0);
    const nb = 3 + Math.round(h / 22), barbs = Array.from({ length: nb }, (_, k) => [0.16 + (k / nb) * 0.7 + R.range(-0.03, 0.03), k % 2 ? 1 : -1]);
    return { x, h, w, lean: R.range(2, 8) + (nearFront ? 6 : 0), sway: R.range(1.5, 4), hook: nearFront ? R.range(12, 18) : R.range(3, 9), ph: R.range(0, TAU),
      barbs, bloom: nearFront || R.chance(0.42), br: R.range(4.5, 6.5) + (nearFront ? 1 : 0), at: TELL + (x / ZW) * GROW, sink: END + R.range(0, 0.16), seed: R.int(1, 999), up: false, down: false };
  });
  // 덤불: 줄기 사이를 낮게 엇갈려 잇는 가는 가시 줄
  // 뒤쪽 줄: 앞줄 사이사이에 낮고 어두운 덩굴(한 단 어두운 색으로 그린다)
  const back = Array.from({ length: N - 1 }, (_, i) => {
    const x = (stems[i].x + stems[i + 1].x) / 2 + R.range(-3, 3), h = R.range(0.32, 0.6) * ZH, nb = 2 + Math.round(h / 22);
    return { x, h, w: R.range(4, 6), lean: R.range(-4, 5), sway: R.range(1, 3), hook: R.range(-6, 6), ph: R.range(0, TAU), barbs: Array.from({ length: nb }, (_, k) => [0.2 + (k / nb) * 0.65, k % 2 ? 1 : -1]),
      at: TELL + (x / ZW) * GROW + 0.04, sink: END + R.range(0, 0.12), seed: R.int(1, 999) };
  });
  const CB = [C[0], C[0], C[1], C[1], C[2], C[3], C[3], C[5]];
  const brambles = Array.from({ length: 2 * N }, (_, i) => ({ a: i % N, b: Math.min(N - 1, (i % N) + 1 + (i >= N ? 1 : 0)), y0: -R.range(6, 26), y1: -R.range(6, 30), sag: R.range(-6, 6) }));
  const sprouts = Array.from({ length: 22 }, (_, i) => ({ x: 6 + i * ((ZW - 8) / 21) + R.range(-3, 3), h: R.range(6, 13), lean: R.range(-1, 3), at: 0.04 + (i / 21) * 0.32 }));
  const petals = [];
  const petal = (x, y, vx, vy, life) => petals.push({ x, y, vx, vy, a: R.range(0, TAU), va: R.range(-9, 9), life, max: life, c: R.chance(0.5) ? 5 : 6, ph: R.range(0, TAU) });
  const grown = (s) => (t < s.at ? 0 : ease.outBack(clamp((t - s.at) / RISE), 2.2));
  const sunk = (s) => smooth(s.sink, s.sink + SINK, t);
  const tipOf = (s, g, k) => { const u = 1, base = k * s.h * 1.05, h = s.h * g; return [s.x + s.lean * u + Math.sin(u * 4.2 + s.ph) * s.sway * u + s.hook, base - h]; };
  let t = 0;
  return {
    update(dt) {
      t += dt;
      const f = Math.min(1, dt * 120);
      for (const s of stems) {
        const k = s.h / ZH;
        if (!s.up && t >= s.at) {
          s.up = true;
          lights.add(s.x, -s.h * 0.5, 16 + 16 * k, WHITE, 0.4, 0.07);
          lights.add(s.x, -s.h * 0.4, 20 + 18 * k, G[2], 0.28, 0.3);
          for (let i = 0; i < 8 + 12 * k; i++) {
            const a = -Math.PI / 2 + R.range(-1, 1), v = R.range(50, 190) * (0.6 + 0.5 * k);
            ps.spawn(P.GRIT, s.x + R.range(-s.w / 2, s.w / 2), -1, Math.cos(a) * v, Math.sin(a) * v, R.range(0.5, 0.9), R.pick([S[5], S[4], S[3]]), R.chance(0.3) ? 2 : 1);
          }
          for (let i = 0; i < 2 + 3 * k; i++) ps.spawn(P.SMOKE, s.x + R.range(-4, 4), -R.range(1, 4), R.sign() * R.range(15, 60), -R.range(5, 20), R.range(0.5, 0.9), S[4], R.range(2.5, 4.5), 0, 0);
          for (let i = 0; i < 3; i++) ps.spawn(P.MOTE, s.x + R.range(-3, 3), -R.range(4, s.h * 0.7), R.range(-15, 15), -R.range(25, 60), R.range(0.35, 0.7), G[3], 0.7);
        }
        if (!s.down && t >= s.sink) {
          s.down = true;
          for (let i = 0; i < 2 + 2 * k; i++) ps.spawn(P.DUST, s.x + R.range(-s.w / 2, s.w / 2), -R.range(0, 3), R.sign() * R.range(10, 40), -R.range(2, 8), Math.min(R.range(0.35, 0.55), meta.duration - 0.05 - t), S[4], R.range(2, 3.5));
          if (s.bloom) { const [bx, by] = tipOf(s, 1, 0); for (let i = 0; i < 7; i++) petal(bx + R.range(-3, 3), by + R.range(-3, 3), R.range(-30, 50), R.range(-40, 5), Math.min(R.range(0.4, 0.6), meta.duration - 0.04 - t)); }
        }
        // 피어 있는 동안 장미에서 꽃잎이 이따금 떨어지고, 가시 끝에서 빛이 반짝인다
        if (s.up && !s.down && s.bloom && t > s.at + 0.2 && R.chance(0.045 * f)) { const [bx, by] = tipOf(s, 1, 0); petal(bx, by, R.range(5, 35), R.range(-15, 5), Math.min(R.range(0.5, 0.8), meta.duration - 0.04 - t)); }
        if (s.up && !s.down && t > s.at + 0.15 && R.chance(0.03 * f)) ps.spawn(P.STAR, s.x + s.lean * 0.4 - s.w * 0.2, -s.h * R.range(0.3, 0.8), 0, -3, R.range(0.2, 0.4), C[7], R.range(0.8, 1.5));
      }
      // 전조: 솔기 머리에서 흙먼지
      if (t < 0.38 && R.chance(0.5 * f)) ps.spawn(P.DUST, clamp(t / 0.36) * ZW, -1, R.range(-10, 10), -R.range(3, 10), R.range(0.25, 0.45), S[3], R.range(1.5, 2.5));
      for (let i = petals.length - 1; i >= 0; i--) {
        const q = petals[i];
        if ((q.life -= dt) <= 0) { petals.splice(i, 1); continue; }
        q.vx -= q.vx * 1.6 * dt; q.vy += (60 - q.vy * 1.6) * dt;
        q.x += (q.vx + Math.sin(t * 7 + q.ph) * 10) * dt; q.y = Math.min(-1, q.y + q.vy * dt); q.a += q.va * dt;
      }
      ps.update(dt);
      lights.update(dt);
    },
    drawBack(p) {
      // 바닥 솔기: 전조 동안 벽에서 최종 앞면까지 달리고, 덩굴이 서 있는 동안 은은히 남았다 닫힌다
      const run = ease.outCubic(clamp(t / 0.36)) * ZW, fade = 1 - smooth(END + 0.25, meta.duration - 0.1, t), fr = front(t);
      for (let x = 0; x <= run; x++) {
        const hot = clamp(1 - (run - x) / 16) * (1 - smooth(0.4, 0.6, t)) + clamp(1 - Math.abs(fr - x) / 10) * (t > TELL && t < END ? 1 : 0), h = rand(x, 17);
        p.dot(x, 0, hot > 0.45 ? G[3] : hot > 0.12 ? G[2] : C[1], fade);
        if (h < 0.5) p.dot(x, 1, S[1], fade * 0.8);
        if (h < 0.16) p.dot(x, -1, C[3], fade);
        if (hot > 0.3 && (x & 3) === 0) p.glow(x, 0, 6, G[2], 0.3 * hot);
      }
      // 최종 앞면 자리: 전조 동안 맥동하는 세로 점선
      if (t > 0.3 && t < TELL + 0.1) { const k = smooth(0.3, 0.42, t) * (1 - smooth(TELL, TELL + 0.1, t)) * (0.6 + 0.4 * Math.sin(t * 40)); for (let y = -ZH; y < 0; y += 3) p.dot(ZW, y, G[2], k); p.glowLine(ZW, -ZH, ZW, 0, 5, G[1], 0.12 * k); }
      // 덤불: 줄기 사이를 낮게 엇갈려 잇는 가시 줄(뒤쪽)
      for (const b of brambles) {
        const A = stems[b.a], B = stems[b.b], g = Math.min(grown(A), grown(B)), k = Math.max(sunk(A), sunk(B));
        if (g < 0.4 || k >= 1) continue;
        const pts = [];
        for (let i = 0; i <= 10; i++) { const u = i / 10, x = A.x + (B.x - A.x) * u, y = (b.y0 + (b.y1 - b.y0) * u + b.sag * Math.sin(u * Math.PI)) * g + k * 30; pts.push([x, Math.min(-1, y)]); }
        p.polyline(pts.map(([x, y]) => [x, y + 1]), C[0]);
        p.polyline(pts, C[2]);
        for (let i = 1; i < 10; i += 2) { const [x, y] = pts[i]; p.px(x, y - 1, C[4]); }
      }
      ps.draw(p, "back");
    },
    draw(p) {
      // 전조의 가시 싹
      for (const s of sprouts) {
        const g = ease.outBack(clamp((t - s.at) / 0.1), 2), k = smooth(TELL - 0.05, TELL + 0.25, t);
        if (g <= 0 || k >= 1) continue;
        const h = s.h * g * (1 - k);
        p.tri(s.x - 1.5, 0, s.x + 1.5, 0, s.x + s.lean, -h, C[3]); p.line(s.x - 1, 0, s.x + s.lean, -h, C[5]); p.px(s.x + s.lean, -h, C[7]);
        if (h > 6) { const y = -h * 0.5, x = s.x + s.lean * 0.5; p.line(x, y, x + 2.5, y - 2.5, C[4]); p.px(x + 2.5, y - 2.5, C[6]); }
        p.glow(s.x, -2, 5, G[1], 0.2 * g * (1 - k));
      }
      // 뒤쪽 줄 덩굴(어둡게)
      for (const s of back) { const g = grown(s), k = sunk(s); if (g > 0 && k < 1) stem(p, s, s.h * g, CB, { base: k * s.h * 1.05 }); }
      // 덩굴과 장미
      for (const s of stems) {
        const g = grown(s), k = sunk(s);
        if (g <= 0 || k >= 1) continue;
        stem(p, s, s.h * g, C, { white: t - s.at < 0.035, base: k * s.h * 1.05 });
        if (t - s.at < 0.22) p.glow(s.x, -s.h * 0.5 * g, s.h * 0.35 + 6, G[2], 0.2 * (1 - (t - s.at) / 0.22));
        if (s.bloom) { const [bx, by] = tipOf(s, g, k); if (by < -2) rose(p, bx, by, s.br, clamp((t - s.at - 0.08) / 0.2) * (1 - k), C, G); }
        p.glow(s.x, -2, s.w + 5, G[1], 0.16 * g * (1 - k));
      }
      // 꽃잎: 짧은 렌즈, 어두운 한 칸과 밝은 한 칸, 돌며 떨어진다
      for (const q of petals) {
        const a = Math.min(1, q.life / 0.2), c = Math.cos(q.a), s = Math.sin(q.a);
        p.dot(q.x - c * 1.5, q.y - s, C[q.c], a); p.dot(q.x, q.y, C[q.c], a); p.dot(q.x + c * 1.5, q.y + s, C[q.c - 2], a); p.dot(q.x - c * 0.5, q.y - 1, C[7], a * 0.8);
      }
      ps.draw(p, "front");
      lights.draw(p);
      // 앞면을 따라 붉은 빛 웅덩이
      const fr = front(t);
      if (t > TELL && t < END + 0.1) p.glow(fr, -ZH * 0.45, 34, G[1], 0.12 * (1 - smooth(END, END + 0.1, t)));
    },
  };
}
