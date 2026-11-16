import { Rng, fit, clamp, smooth, TAU } from "../../core/index.js";

// 그림자 난무 연타 이펙트 시안(2026-11-07). 몸은 제자리에서 손만 빠르게 찌르고, 여섯 번 맞는 건 적 자리의 베기로 보여 준다.
// 사용자 참고 영상: 적 위에 가는 베기 선이 여러 방향으로 와다닥 겹친다. 먹·노랑만, 검은 테두리 없음, 화소 단위(번짐 없음).
//   variant="a" 엇갈린 바늘 베기: 타마다 적 몸을 가로지르는 가는 선 하나가 방향을 바꿔 긋고 0.16초에 지워진다(두세 줄이 겹쳐 보임). 6타는 X자 두 줄 + 먹 방울.
//   variant="b" 찌르기 꽂힘: 손이 찌르는 방향(앞→적) 그대로 뾰족한 노란 쐐기가 적 몸에 꽂히고, 끝에 별 하나가 튄다. 높이는 가슴·위·아래를 번갈아. 6타는 길게 관통.
//   variant="d" 번쩍 난도질(화려 1): 타마다 긴 바늘 세 줄이 다른 각도로 동시에 긋고, 가운데 흰 여덟 갈래 섬광 + 퍼지는 고리 + 불티 열댓 개. 6타는 큰 폭발.
//   variant="e" 초승달 폭풍(화려 2, 참고 영상): 타마다 큰 초승달 베기 두 개가 방향을 바꿔 적을 감싸 돌고, 바늘 한 줄·섬광·불티. 6타는 거대한 초승달 X + 폭발.
//   variant="f" 판정 전체 폭풍(화려 3): e 를 판정 상자 전체에 흩뿌린다(적이 없어도 앞이 와다닥). 한 타에 베기 셋.
//   variant="c" 쌓이는 자국: a 의 선이 사라지지 않고 흐린 자국으로 쌓여 몇 대 맞았는지 보인다. 6타에 자국이 모두 한 번 번쩍이고 화소 조각으로 부서진다.
// 원점은 맞은 적의 몸 가운데. 시트 시각 0 = 시전 시작(타격 0.15·0.27·0.39·0.51·0.63·0.75초, melee.md 그림자 난무).
export const meta = {
  name: "Dagger flurry cuts (ink)",
  desc: "Shadow flurry readability: six quick cuts on the struck enemy, each a different angle, ending in a bigger finishing cut.",
  tags: ["ink", "dagger", "flurry", "slash", "one-shot"],
  size: [176, 144],
  origin: [88, 72],
  duration: 1.2,
  loop: false,
  fps: 50,
  bg: "light",
  options: { variant: "a", seed: 7, hits: [0.15, 0.27, 0.39, 0.51, 0.63, 0.75] },
};

const PAL = ["#1b1616", "#7a4a14", "#eba516", "#ffe68a", "#fff6d0"];
// 타마다 선 각도(라디안, 0 = 수평)와 가운데에서 비킨 자리(px). 영상처럼 방향이 매번 확 바뀐다.
const ANG = [-0.55, 0.7, -1.25, 0.15, 1.05, -0.35];
const OFF = [[-2, -4], [3, 3], [0, -8], [-4, 6], [4, -2], [0, 0]];
// b: 찌르기 높이(px, 위가 -). 가슴·위·아래·가슴·위, 6타는 가슴
const STAB_Y = [-2, -12, 9, -4, -14, 0];

// 화소 하나: 가까울수록 밝다. 번짐 대신 단계 색만 쓴다.
function ramp(i, C) {
  const [, AMB, YEL, YEL2, IVORY] = C;
  return i > 0.72 ? IVORY : i > 0.45 ? YEL2 : i > 0.2 ? YEL : AMB;
}

// 바늘 선 하나: 길이 L, 가장 두꺼운 반폭 W, 양끝이 바늘처럼 빠진다. head(0..1) 까지 그어졌고 tail(0..1) 까지 지워졌다.
function needle(p, cx, cy, ang, L, W, head, tail, C, alpha = 1) {
  if (head <= tail || alpha <= 0.02) return;
  const ca = Math.cos(ang), sa = Math.sin(ang), B = L / 2 + W + 2;
  for (let y = Math.floor(cy - B); y <= cy + B; y++)
    for (let x = Math.floor(cx - B); x <= cx + B; x++) {
      const dx = x + 0.5 - cx, dy = y + 0.5 - cy, along = dx * ca + dy * sa, across = -dx * sa + dy * ca;
      const k = along / L + 0.5;
      if (k < tail || k > head) continue;
      const kk = clamp((k - tail) / Math.max(0.05, head - tail));
      const w = W * Math.sin(Math.PI * clamp(k) ** 0.8) * Math.min(1, kk * 5, (1 - kk) * 8);
      const d = Math.abs(across);
      if (w < 0.35 || d > w) continue;
      p.dot(x, y, ramp(1 - d / w, C), alpha);
    }
}

// 노란 별 하나(맞은 자리 반짝임): 십자 + 가운데 흰 점
function star(p, x, y, r, C) {
  const [, , YEL, YEL2, IVORY] = C;
  const R = Math.round(r);
  for (let i = -R; i <= R; i++) {
    const c = Math.abs(i) <= 1 ? IVORY : Math.abs(i) < R * 0.6 ? YEL2 : YEL;
    p.px(x + i, y, c); p.px(x, y + i, c);
  }
  if (R >= 3) for (const [a, b] of [[1, 1], [-1, 1], [1, -1], [-1, -1]]) p.px(x + a, y + b, YEL2);
}

// 뾰족한 쐐기(찌르기): 뒤가 넓고 앞이 바늘. tip 이 적 몸 안쪽.
function wedge(p, tipX, y, L, W, grow, fade, C) {
  const len = L * grow;
  for (let x = Math.floor(tipX - len); x <= tipX; x++) {
    const u = (tipX - x) / L;                     // 0 = 끝, 1 = 뒤
    const w = W * Math.min(1, u * 2.2) * (1 - 0.35 * u);
    for (let dy = -Math.ceil(w); dy <= Math.ceil(w); dy++) {
      if (Math.abs(dy) > w) continue;
      const i = (1 - Math.abs(dy) / Math.max(w, 0.5)) * (1 - 0.5 * u);
      p.dot(x, y + dy, ramp(i, C), (1 - fade) * (u < 0.85 ? 1 : 1 - (u - 0.85) / 0.15));
    }
  }
}


// 노란 빛 번짐: 디더로 찍는 반투명 화소(부드러운 번짐 대신 점묘)
function halo(p, x, y, c, a) { if (a > 0.04) p.dot(x, y, c, a); }

// 초승달 베기: 중심 (cx,cy), 반지름 R, 시작각 a0 에서 dir 방향으로 span 만큼. head/tail 은 0..1 진행.
function crescent(p, cx, cy, R, a0, span, dir, W, head, tail, C, glow = 3) {
  if (head <= tail) return;
  const B = R + W + glow + 2;
  for (let y = Math.floor(cy - B); y <= cy + B; y++)
    for (let x = Math.floor(cx - B); x <= cx + B; x++) {
      const dx = x + 0.5 - cx, dy = y + 0.5 - cy, r = Math.hypot(dx, dy);
      let a = (Math.atan2(dy, dx) - a0) * dir; a = ((a % TAU) + TAU) % TAU;
      const k = a / span;
      if (k > 1 || k < tail || k > head) continue;
      const kk = clamp((k - tail) / Math.max(0.05, head - tail));
      const w = W * Math.sin(Math.PI * clamp(k) ** 0.7) * Math.min(1, kk * 4, (1 - kk) * 7);
      // 바깥 쪽이 두껍고 안쪽은 날카롭게(초승달)
      const d = r - R, inner = d < 0 ? w * 0.45 : w;
      if (w < 0.4) continue;
      if (Math.abs(d) <= inner) p.dot(x, y, ramp(1 - Math.abs(d) / inner, C), 1);
      else if (d > 0 && d <= inner + glow) halo(p, x, y, C[2], 0.6 * (1 - (d - inner) / glow));
    }
}
// 바늘 + 노란 번짐
function needleGlow(p, cx, cy, ang, L, W, head, tail, C, glow = 2.5) {
  if (head <= tail) return;
  const ca = Math.cos(ang), sa = Math.sin(ang), B = L / 2 + W + glow + 2;
  for (let y = Math.floor(cy - B); y <= cy + B; y++)
    for (let x = Math.floor(cx - B); x <= cx + B; x++) {
      const dx = x + 0.5 - cx, dy = y + 0.5 - cy, along = dx * ca + dy * sa, d = Math.abs(-dx * sa + dy * ca);
      const k = along / L + 0.5;
      if (k < tail || k > head) continue;
      const kk = clamp((k - tail) / Math.max(0.05, head - tail));
      const w = W * Math.sin(Math.PI * clamp(k) ** 0.8) * Math.min(1, kk * 5, (1 - kk) * 8);
      if (w < 0.3) continue;
      if (d <= w) p.dot(x, y, ramp(1 - d / w, C), 1);
      else if (d <= w + glow) halo(p, x, y, C[2], 0.5 * (1 - (d - w) / glow));
    }
}
// 여덟 갈래 섬광: 긴 네 갈래 + 짧은 대각 네 갈래, 가운데 흰 원
function burst(p, x, y, g, size, C) {
  const life = 0.07 + size * 0.004, k = g / life;
  if (k >= 1) return;
  const s = size * (k < 0.25 ? 0.6 + 1.6 * k : 1 - 0.7 * (k - 0.25) / 0.75);
  const [, , YEL, YEL2, IVORY] = C;
  for (let i = 0; i < 8; i++) {
    const a = (i * TAU) / 8 + 0.2, L = (i % 2 ? 0.5 : 1) * s, ca = Math.cos(a), sa = Math.sin(a);
    for (let r = 0; r <= L; r += 0.5) {
      const w = (1 - r / L) * (i % 2 ? 1 : 1.8);
      for (let q = -w; q <= w; q += 0.5) p.px(x + ca * r - sa * q, y + sa * r + ca * q, r < L * 0.35 ? IVORY : r < L * 0.7 ? YEL2 : YEL);
    }
  }
  const core = Math.max(1, s * 0.28);
  for (let yy = -core; yy <= core; yy++) for (let xx = -core; xx <= core; xx++) if (xx * xx + yy * yy <= core * core) p.px(x + xx, y + yy, IVORY);
}
// 퍼지는 고리
function ring(p, x, y, g, R1, life, C) {
  if (g < 0 || g >= life) return;
  const k = g / life, r = 3 + (R1 - 3) * (1 - (1 - k) ** 3), th = 2 * (1 - k) + 0.6;
  for (let yy = Math.floor(-r - th); yy <= r + th; yy++)
    for (let xx = Math.floor(-r - th); xx <= r + th; xx++) {
      const d = Math.abs(Math.hypot(xx + 0.5, yy + 0.5) - r);
      if (d <= th) p.dot(x + xx, y + yy, d < th * 0.4 ? C[3] : C[2], 1 - k);
    }
}
// 불티: 속도 방향으로 꼬리가 달린 짧은 줄
function sparks(p, x, y, g, list, C) {
  for (const s of list) {
    const q = g - s.d;
    if (q < 0 || q > s.life) continue;
    const k = q / s.life, sp = s.v * (1 - 0.6 * k), px = x + s.ca * s.v * q * (1 - 0.3 * k), py = y + s.sa * s.v * q * (1 - 0.3 * k) + 120 * q * q;
    const tl = Math.max(1, sp * 0.03);
    for (let i = 0; i <= tl; i++) p.dot(px - s.ca * i, py - s.sa * i, i < 1 ? (k < 0.4 ? C[4] : C[3]) : C[2], 1 - k);
  }
}

export function create(o) {
  const C = fit(PAL, 5), INK = C[0], R = new Rng(o.seed), V = o.variant, H = o.hits;
  const last = H[H.length - 1];
  // 6타 먹 방울 / c 의 화소 조각
  const drops = Array.from({ length: 12 }, () => {
    const a = R.range(0, TAU), sp = R.range(70, 150);
    return { vx: Math.cos(a) * sp, vy: Math.sin(a) * sp * 0.75 - 30, r: R.range(1, 2.2), life: R.range(0.22, 0.34) };
  });
  const chips = Array.from({ length: 26 }, () => {
    const k = R.int(0, 5), s = R.range(-0.45, 0.45), a = R.range(0, TAU), sp = R.range(40, 110);
    return { k, s, vx: Math.cos(a) * sp, vy: Math.sin(a) * sp - 20, life: R.range(0.18, 0.3) };
  });
  // 화려안(d·e·f): 타마다 불티·각도 묶음을 미리 뽑는다
  const mkSparks = (n, fin) => Array.from({ length: n }, () => {
    const a = R.range(0, TAU);
    return { ca: Math.cos(a), sa: Math.sin(a), v: R.range(fin ? 140 : 110, fin ? 340 : 240), life: R.range(0.1, fin ? 0.32 : 0.2), d: R.range(0, 0.02) };
  });
  const FL = H.map((h, i) => {
    const fin = i === H.length - 1;
    return {
      sparks: mkSparks(fin ? 34 : 14, fin),
      ang: [ANG[i], ANG[i] + R.range(0.8, 1.2), ANG[i] - R.range(0.9, 1.3)],
      cres: [R.range(0, TAU), R.range(0, TAU)], dir: i % 2 ? -1 : 1,
      spot: Array.from({ length: 3 }, () => [R.range(-6, 40), R.range(-24, 22)]),   // 판정 가운데 기준, 몸 쪽(왼쪽)은 비운다
    };
  });
  let t = 0;
  return {
    update(dt) { t += dt; },
    draw(p) {
      const u = t + 1e-6;
      H.forEach((h, i) => {
        const g = u - h, fin = i === H.length - 1;
        if (g < 0) return;
        const [ox, oy] = OFF[i];
        if (V === "d" || V === "e" || V === "f") {
          const F = FL[i], spots = V === "f" ? F.spot : [[ox, oy]];
          spots.forEach(([sx, sy], j) => {
            const gg = g - j * 0.025;
            if (gg < 0) return;
            const head = 0.5 + 0.5 * clamp(gg / 0.025), tail = smooth(0.05, fin ? 0.3 : 0.2, gg);
            if (V === "d") {
              F.ang.forEach((a, n) => needleGlow(p, sx, sy, a, (fin ? 130 : 96) - n * 14, fin ? 4.2 : 3, 0.5 + 0.5 * clamp((gg - n * 0.012) / 0.025), smooth(0.05 + n * 0.012, (fin ? 0.3 : 0.2) + n * 0.012, gg), C));
            } else {
              const R0 = (fin ? 46 : 30) * (V === "f" ? 0.85 : 1);
              crescent(p, sx, sy, R0, F.cres[0], 2.6, F.dir, fin ? 9 : 6, head, tail, C, 4);
              crescent(p, sx + 4, sy - 3, R0 * 0.75, F.cres[1], 2.2, -F.dir, fin ? 7 : 4.5, 0.5 + 0.5 * clamp((gg - 0.02) / 0.025), smooth(0.07, fin ? 0.32 : 0.22, gg), C, 3);
              needleGlow(p, sx, sy, F.ang[0], fin ? 120 : 84, fin ? 3.6 : 2.4, head, tail, C);
              if (fin) needleGlow(p, sx, sy, F.ang[0] + 1.5, 120, 3.6, 0.5 + 0.5 * clamp((gg - 0.03) / 0.025), smooth(0.08, 0.32, gg), C);
            }
            burst(p, sx, sy, gg, fin ? 26 : (V === "f" ? 12 : 15), C);
            ring(p, sx, sy, gg - 0.01, fin ? 56 : 22, fin ? 0.26 : 0.14, C);
            sparks(p, sx, sy, gg, V === "f" ? F.sparks.slice(0, 8) : F.sparks, C);
          });
        } else if (V === "a" || V === "c") {
          const L = fin ? 84 : 62, W = fin ? 3.2 : 2.1;
          const head = 0.55 + 0.45 * clamp(g / 0.02), tail = smooth(0.06, fin ? 0.26 : 0.16, g);
          if (V === "a" || g < 0.16 || fin) {
            needle(p, ox, oy, ANG[i], L, W, head, tail, C);
            if (fin) needle(p, ox, oy, ANG[i] + 1.4, L, W, 0.55 + 0.45 * clamp((g - 0.04) / 0.02), smooth(0.1, 0.3, g), C);
          }
          if (V === "c" && !fin) {
            // 지워진 뒤에도 흐린 자국으로 남는다(마지막 타까지). 6타에 한 번 번쩍이고 부서진다.
            const flash = u >= last && u < last + 0.06;
            if (g >= 0.12 && u < last + 0.06) needle(p, ox, oy, ANG[i], 46, flash ? 2.2 : 1.1, 1, 0, flash ? C : [C[0], C[1], C[1], C[2], C[2]], flash ? 1 : 0.55);
          }
          if (g < 0.06) star(p, ox, oy, fin ? 6 : 3 + (g < 0.03 ? 1 : 0), C);
        } else {
          const y = STAB_Y[i], tip = fin ? 30 : 10, L = fin ? 72 : 40, Wd = fin ? 4 : 2.6;
          if (g < (fin ? 0.24 : 0.14)) wedge(p, tip, y, L, Wd, clamp(g / 0.02), smooth(fin ? 0.1 : 0.06, fin ? 0.24 : 0.14, g), C);
          if (g < 0.07) star(p, tip - (fin ? 26 : 2), y, fin ? 6 : 3 + (g < 0.03 ? 1 : 0), C);
        }
      });
      const q = u - last;
      if (q > 0 && V !== "c") for (const d of drops) {
        if (q > d.life) continue;
        const x = d.vx * q, y = d.vy * q + 260 * q * q, r = d.r * (1 - 0.5 * q / d.life);
        for (let yy = -1; yy <= 1; yy++) for (let xx = -1; xx <= 1; xx++) if (xx * xx + yy * yy <= r * r) p.dot(x + xx, y + yy, INK, 1 - smooth(d.life * 0.6, d.life, q));
      }
      if (q > 0.04 && V === "c") for (const c of chips) {
        const g = q - 0.04;
        if (g > c.life) continue;
        const [ox, oy] = OFF[c.k], a = ANG[c.k], bx = ox + Math.cos(a) * c.s * 46, by = oy + Math.sin(a) * c.s * 46;
        const x = bx + c.vx * g, y = by + c.vy * g + 220 * g * g;
        p.dot(x, y, g < c.life * 0.4 ? C[3] : C[2], 1 - smooth(c.life * 0.5, c.life, g)); p.dot(x + 1, y, C[2], 1 - smooth(c.life * 0.5, c.life, g));
      }
    },
  };
}

// 단검 다른 스킬 이펙트(dagger-*.js)가 같은 붓질을 쓴다
export { ramp, needle, star, crescent, needleGlow, burst, ring, sparks, PAL as FLURRY_PAL };
