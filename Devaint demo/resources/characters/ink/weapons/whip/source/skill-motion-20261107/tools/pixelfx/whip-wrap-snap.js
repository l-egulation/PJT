import { Rng, fit, clamp, smooth, ease, noise2, TAU } from "../../core/index.js";

// 채찍 휘감기(미장착 특수) 「감기는 터짐」 시안 — 고리가 적에게 감기는 순간(0.25초) 한 번 터진다. 그 뒤 속박 밧줄은 게임의 기존 시트가 맡는다.
// 원점 = 고리 가운데(적 몸 높이 1유닛). dir=1 이면 플레이어는 왼쪽(-x)이고 채찍은 그쪽으로 당겨진다.
// 원·막대 도형 대신 메타볼 농도장 + 좌표 비틀기(표식 격발 G 와 같은 방식)로 그린다. 검은 테두리 없음, 넘침·출렁임 없음.
//   a 「고리 조임」: 먹 고리 두 겹이 확 조여 작은 매듭이 되고, 조여 짜낸 먹이 위아래로 튄다.
//   b 「채찍 섬광」: 노랑·아이보리 바늘 섬광이 짧게 번쩍이고, 당기는 쪽(플레이어 쪽)으로 먹 방울이 부채꼴로 튄다.
//   c 「매듭 터짐」: 먹 덩어리 넷이 감겨 들어 매듭으로 뭉쳤다가, 아이보리 속을 드러내며 터져 찢긴 고리와 방울로 흩어진다.
export const INK_WHIP = ["#1b1616", "#3a3230", "#7a4a14", "#eba516", "#ffe68a", "#fff6d0"];
export const meta = {
  name: "Whip wrap snap",
  desc: "Whip wrap hit: a one-shot ink snap where the coil closes on the enemy (three variants).",
  tags: ["ink", "whip", "hit", "one-shot"],
  size: [140, 120],
  origin: [70, 64],
  duration: 0.6,
  loop: false,
  fps: 25,
  bg: "light",
  options: { palette: INK_WHIP, variant: "a", dir: 1, seed: 7 },
};

const STEP = 1 / 25, BOX = 56, FLOOR = 32;   // 원점은 땅에서 1유닛(32px) 위

function ball(x, y, b) {
  const dx = x - b.x, dy = y - b.y, al = dx * b.ux + dy * b.uy, pe = -dx * b.uy + dy * b.ux;
  const q = (al * al) / (b.r * b.r * b.s * b.s) + (pe * pe) / (b.r * b.r);
  return q < 1 ? (1 - q) * (1 - q) : 0;
}

// 기울어진 타원 고리의 농도(두께 w, 잡음으로 끊김)
function coil(x, y, c) {
  const cs = Math.cos(c.tilt), sn = Math.sin(c.tilt);
  const lx = x * cs + y * sn, ly = -x * sn + y * cs;
  const d = Math.hypot(lx / c.rx, ly / c.ry) - 1, w = c.w / Math.min(c.rx, c.ry);
  return Math.exp(-((d / w) ** 2));
}

export function create(o) {
  const C = fit(o.palette, 6), [INK, INK2, , YEL, YEL2, IVORY] = C, R = new Rng(o.seed), dir = o.dir < 0 ? -1 : 1, V = o.variant ?? "a";
  const blobs = [];
  let t = 0, burst = false;
  const T0 = V === "c" ? 0.12 : V === "a" ? 0.1 : 0.0;   // 터지는 시각(원점 시각 0 = 고리가 닿는 순간)
  const needles = Array.from({ length: 6 }, (_, i) => ({ a: (i / 6) * TAU + R.range(-0.3, 0.3), L: R.range(10, 18) }));
  const knot = Array.from({ length: 4 }, (_, i) => ({ a0: (i / 4) * TAU + 0.6, r0: 16 + R.range(-2, 3) }));

  function spray() {
    burst = true;
    if (V === "a") {
      // 조여서 위아래로 짜내는 먹: 수직 방향 위주, 조금씩 당기는 쪽으로
      for (let i = 0; i < 14; i++) {
        const up = i % 2 ? -1 : 1, a = (up < 0 ? -Math.PI / 2 : Math.PI / 2) + R.range(-0.7, 0.7) - dir * 0.25, sp = R.range(120, 230), r = R.range(2.2, 4.6);
        blobs.push({ x: 0, y: 0, vx: Math.cos(a) * sp, vy: Math.sin(a) * sp, r, r0: r, life: R.range(0.35, 0.5), age: 0, delay: R.range(0, 0.04) });
      }
      blobs.push({ x: 0, y: 0, vx: -dir * 20, vy: 0, r: 7, r0: 7, life: 0.3, age: 0, core: true });
    } else if (V === "b") {
      // 당기는 쪽(플레이어 쪽) 부채꼴로 튀는 먹 방울
      for (let i = 0; i < 16; i++) {
        const a = (dir > 0 ? Math.PI : 0) + R.range(-0.75, 0.75), sp = R.range(140, 280), r = R.range(1.8, 4);
        blobs.push({ x: 0, y: 0, vx: Math.cos(a) * sp, vy: Math.sin(a) * sp - 30, r, r0: r, life: R.range(0.3, 0.48), age: 0, delay: R.range(0.02, 0.07) });
      }
    } else {
      // 매듭이 터져 사방으로(맞은 쪽 = 플레이어 반대쪽으로 조금 더)
      for (let i = 0; i < 16; i++) {
        const a = R.range(0, TAU), sp = R.range(110, 240) * (1 + 0.3 * Math.cos(a) * dir), r = R.range(2.4, 5.5);
        blobs.push({ x: Math.cos(a) * 3, y: Math.sin(a) * 3, vx: Math.cos(a) * sp, vy: Math.sin(a) * sp * 0.8 - 30, r, r0: r, life: R.range(0.38, 0.55), age: 0, delay: R.range(0, 0.04) });
      }
      blobs.push({ x: 0, y: 0, vx: 0, vy: 0, r: 5.5, r0: 5.5, life: 0.2, age: 0, core: true });
    }
  }

  return {
    update(dt) {
      t += dt;
      if (!burst && t >= T0) spray();
      for (const b of blobs) {
        if ((b.delay -= dt) > 0) continue;
        b.age += dt;
        b.vx *= 1 - (b.core ? 8 : 1.6) * dt;
        b.vy = b.vy * (1 - 2.2 * dt) + (b.age > 0.06 ? 820 : 0) * dt;
        b.x += b.vx * dt; b.y += b.vy * dt;
        if (!b.core && b.y + b.r * 0.3 > FLOOR) (b.y = FLOOR - b.r * 0.3, b.vy = 0, b.vx *= 0.4, b.land = b.land ?? b.age);
        const k = b.age / b.life;
        b.r = b.r0 * (b.core ? 1 - ease.inQuad(clamp(k)) * 0.95 : 1 - smooth(0.4, 1, k));
      }
    },
    draw(p) {
      const f = Math.floor(t / STEP + 1e-6), items = [], coils = [];
      let flash = 0;
      if (V === "a") {
        // 고리 두 겹이 0.1초 동안 확 조인다(바깥에서 안으로, 넘침 없음)
        if (t < T0 + 0.02) {
          const k = ease.inCubic(clamp(t / T0));
          coils.push({ rx: 17 - 9 * k, ry: 22 - 12 * k, tilt: 0.35, w: 2.6 - 0.6 * k });
          coils.push({ rx: 13 - 7 * k, ry: 18 - 10 * k, tilt: -0.25, w: 2.2 - 0.5 * k });
        }
        if (t >= T0 && t < T0 + 0.08) flash = 1 - (t - T0) / 0.08;
      } else if (V === "b") {
        if (t < 0.1) flash = 1 - t / 0.1;
      } else {
        // 덩어리 넷이 돌며 가운데로 감겨 들어 매듭이 된다
        if (t < T0 + 0.02) {
          const k = ease.inCubic(clamp(t / T0));
          for (const n of knot) {
            const a = n.a0 + k * 2.2, r = n.r0 * (1 - 0.8 * k);
            items.push({ x: Math.cos(a) * r, y: Math.sin(a) * r * 1.2, r: 4.5 + 2 * k, s: 1.6, ux: -Math.sin(a), uy: Math.cos(a) });
          }
        }
        if (t >= T0 && t < T0 + 0.07) flash = 1 - (t - T0) / 0.07;
      }
      for (const b of blobs) {
        if (b.delay > 0 || b.r < 0.6) continue;
        const v = Math.hypot(b.vx, b.vy), landed = b.land !== undefined;
        items.push({ x: b.x, y: b.y, r: b.r, s: landed ? 1.8 : 1 + Math.min(2.2, v / 90), ux: landed ? 1 : v ? b.vx / v : 1, uy: landed ? 0 : v ? b.vy / v : 0 });
      }
      // b·a 의 바늘 섬광(노랑·아이보리), c 는 찢긴 고리
      const ringOn = V === "c" && t >= T0 && t < T0 + 0.22, rk = (t - T0) / 0.22;
      const ringR = 8 + 20 * ease.outCubic(clamp(rk)), ringW = 1.8 * (1 - rk) + 0.6;
      if (!items.length && !coils.length && !flash && !ringOn) return;
      const warp = 1.6;
      p.shade(-BOX, -BOX, BOX, BOX, (x, y) => {
        const wx = x + warp * noise2(x * 0.17, y * 0.17 + f * 0.45, o.seed);
        const wy = y + warp * noise2(x * 0.17 + 9, y * 0.17 - f * 0.4, o.seed + 1);
        // 섬광: 바늘(가늘고 끝이 뾰족) — 아이보리 심, 노랑 가장자리
        if (flash > 0) {
          const d = Math.hypot(x, y), ang = Math.atan2(y, x);
          let nd = 0;
          for (const n of needles) {
            let da = Math.abs(((ang - n.a + Math.PI) % TAU + TAU) % TAU - Math.PI);
            const L = n.L * (V === "b" ? 1.2 : 0.8) * (0.55 + 0.45 * flash);
            if (d < L) nd = Math.max(nd, 1 - da / (0.22 * (1 - d / L) + 0.02));
          }
          const core = d < (V === "b" ? 5 : 4) * (0.6 + 0.4 * flash);
          if (core || nd > 0.5) return core || nd > 0.85 ? IVORY : YEL2;
          if (nd > 0.15) return YEL;
        }
        let F = 0;
        for (const b of items) F += ball(wx, wy, b);
        let K = 0;
        for (const c of coils) K = Math.max(K, coil(wx, wy, c));
        if (K > 0.5 && noise2(Math.atan2(wy, wx) * 1.4, 5, o.seed + 4) > -0.55) return K > 0.85 ? INK : INK2;
        let ring = 0;
        if (ringOn) {
          const d = Math.hypot(wx, wy);
          ring = Math.exp(-(((d - ringR) / ringW) ** 2)) * 0.9;
          if (noise2(Math.atan2(wy, wx) * 2.2, 3, o.seed + 2) < rk * 2.4 - 1.0) ring = 0;
        }
        const T = F + ring;
        if (T < 0.32) return;
        if (V === "c" && t >= T0 && t < T0 + 0.1 && F > 1.0 && x * x + y * y < 30) return IVORY;   // 터지는 순간 매듭 속이 드러난다
        if (T < 0.45) return INK2;
        return INK;
      });
    },
  };
}
