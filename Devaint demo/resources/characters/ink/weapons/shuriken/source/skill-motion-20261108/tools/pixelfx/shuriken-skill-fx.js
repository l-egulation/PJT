import { Rng, fit, clamp, ease, TAU } from "../../core/index.js";
import { crescent, burst, ring, sparks, star as glint, FLURRY_PAL } from "./dagger-flurry-cuts.js";

// 수리검 특수 이펙트 시안(2026-11-08). 먹·노랑만, 검은 테두리·점묘 없음. PPU 32(1unit = 32px).
//  kind="star"   큰 수리검 탄(지름 0.8u = 26px), 반복. 원점 = 탄 중심. a 잔상 별 둘 / b 노란 바람 줄
//  kind="sweep"  폭발 수리검 휘두름(던지기 0.10~0.20초에 몸을 감는 납작한 호). 원점 = 발 가운데, 앞 +x. a 노란 호 / b 먹 호 + 노란 머리
//  kind="blast"  폭발(반경 3u = 96px). 원점 = 터진 자리. a 먹 펑 + 노란 섬광 + 범위 고리 / b 별 조각이 사방으로 + 노란 섬광
//  kind="step"   적 뒤 순간이동. 원점 = 발 가운데. mode out(사라짐)·in(나타남). a 먹 연기 / b 작은 별 조각으로 흩어짐·모임
//  kind="giant"  전진 회전 수리검(지름 3u = 96px), 반복. 원점 = 별 중심(바닥은 +48). a 먹 큰 별 + 바닥 먼지 / b 노란 회전 호 + 바닥 불티
export const meta = {
  name: "Shuriken skill fx (ink)",
  desc: "Shuriken specials: big spinning star, sweeping throw arc, star blast, shadow-step puff and a giant rolling star.",
  tags: ["ink", "shuriken", "projectile", "blast"],
  size: [240, 240],
  origin: [120, 120],
  duration: 0.6,
  loop: false,
  fps: 50,
  bg: "light",
  options: { kind: "star", variant: "a", mode: "out", seed: 5 },
};

const INKS = ["#1b1616", "#3a3230", "#5a4e4a"];
function starShape(p, cx, cy, R, ang, c, inner = 0.34) {      // 네 갈래 별(꽉 찬 화소)
  const B = Math.ceil(R) + 1;
  for (let y = -B; y <= B; y++) for (let x = -B; x <= B; x++) {
    const dx = x + 0.5, dy = y + 0.5, r = Math.hypot(dx, dy); if (r > R) continue;
    const a = Math.atan2(dy, dx) - ang, k = Math.abs(Math.cos(2 * a));           // 1 = 갈래 끝 방향
    const lim = R * (inner + (1 - inner) * Math.pow(k, 6));
    if (r <= lim) p.px(cx + x, cy + y, c);
  }
}
function sharpStar(p, cx, cy, R, r, ang, c) {             // 곧은 변의 뾰족한 네 갈래 수리검(꼭짓점 8개: 끝 R, 안쪽 r)
  const pts = []; for (let i = 0; i < 8; i++) { const a = ang + i * Math.PI / 4, rr = i % 2 ? r : R; pts.push([Math.cos(a) * rr, Math.sin(a) * rr]); }
  const B = Math.ceil(R) + 1;
  for (let y = -B; y <= B; y++) for (let x = -B; x <= B; x++) {
    const px = x + 0.5, py = y + 0.5; let inside = false;
    for (let i = 0, j = 7; i < 8; j = i++) { const [xi, yi] = pts[i], [xj, yj] = pts[j]; if ((yi > py) !== (yj > py) && px < ((xj - xi) * (py - yi)) / (yj - yi) + xi) inside = !inside; }
    if (inside) p.px(cx + x, cy + y, c);
  }
}
function disc(p, cx, cy, r, c) { for (let y = Math.floor(-r); y <= r; y++) for (let x = Math.floor(-r); x <= r; x++) if (x * x + y * y <= r * r) p.px(cx + x, cy + y, c); }

export function create(o) {
  const C = fit(FLURRY_PAL, 5), [INK, INK2, INK3] = fit(INKS, 3), YEL = C[2], YEL2 = C[3], IVO = C[4];
  const R = new Rng(o.seed);
  const shards = Array.from({ length: 12 }, (_, i) => { const a = (i / 10) * TAU + R.range(-0.2, 0.2); return { ca: Math.cos(a), sa: Math.sin(a), v: R.range(170, 260), spin: R.range(-14, 14), life: R.range(0.3, 0.42) }; });
  const puffs = Array.from({ length: 9 }, () => ({ x: R.range(-14, 14), y: R.range(-56, -6), vx: R.range(-40, 40), vy: -R.range(10, 50), r: R.range(5, 9), life: R.range(0.22, 0.34) }));
  const sp = Array.from({ length: 16 }, () => { const a = R.range(0, TAU); return { ca: Math.cos(a), sa: Math.sin(a), v: R.range(120, 260), life: R.range(0.1, 0.26), d: R.range(0, 0.03) }; });
  const drops = Array.from({ length: 34 }, () => { const a = R.range(0, TAU); return { ca: Math.cos(a), sa: Math.sin(a), v: R.range(120, 300), s: R.range(3, 7), life: R.range(0.28, 0.42) }; });
  const sparkLoop = Array.from({ length: 56 }, () => ({ b: R.range(0, 0.6), x: R.range(-2, 12), vx: -R.range(140, 320), vy: -R.range(60, 230), life: R.range(0.12, 0.3) }));
  const bigSparks = Array.from({ length: 90 }, (_, i) => ({ b: R.range(0, 0.6), x: R.range(-2, 14), vx: -R.range(160, 420), vy: -R.range(80, 320), life: R.range(0.14, 0.34), star: i % 9 === 0 }));
  const dust = Array.from({ length: 14 }, () => ({ ph: R.range(0, 1), vx: -R.range(30, 80), vy: -R.range(20, 60), s: R.range(2, 4) }));
  let t = 0;
  return {
    update(dt) { t += dt; },
    draw(p) {
      const g = t + 1e-6, V = o.variant;
      if (o.kind === "star") {                      // 반복: 0.12초에 90도
        const ang = (g / 0.12) * (Math.PI / 2);
        const sr = o.r || 13, sk = sr / 13;                // r = 별 반지름(px). 특수 13(지름 0.8u), 평타 6.5(지름 0.4u) — 11-08 「날아가는 건 다 b 로 통일」
        if (V === "a") { starShape(p, -22 * sk, 0, 10 * sk, ang - 0.5, INK3); starShape(p, -12 * sk, 0, 11.5 * sk, ang - 0.25, INK2); }
        else for (let k = 0; k < 3; k++) { const y = Math.round((-6 + k * 6) * sk), L = Math.round((24 + ((k * 7 + Math.floor(g * 50)) % 3) * 4) * sk), x0 = Math.round(12 * sk); for (let x = -L - x0; x < -x0; x++) p.px(x, y, x > -x0 - 8 * sk ? YEL2 : YEL); }
        starShape(p, 0, 0, sr, ang, INK); disc(p, 0, 0, Math.max(1.2, 2.6 * sk), YEL); p.px(0, -1, YEL2);
        return;
      }
      if (o.kind === "sweep") {                      // 0.10~0.20초 휘두름, 0.32초에 사라짐
        const q = g - 0.10; if (q < 0 || q > 0.24) return;
        const head = clamp(q / 0.10), tail = clamp((q - 0.08) / 0.16);
        const cy = -26, rx = 58, ry = 14;
        for (let s = Math.floor(tail * 90); s <= head * 90; s++) {   // 몸 뒤(왼쪽)에서 앞(오른쪽)으로, 아래쪽 반 바퀴
          const u = s / 90, a = Math.PI - u * Math.PI, x = Math.cos(a) * rx, y = cy + Math.sin(a) * ry;
          const w = 5 * Math.sin(Math.PI * clamp((u - tail) / Math.max(0.05, head - tail))) + 0.6;
          for (let k = -Math.ceil(w); k <= w; k++) {
            const c = V === "a" ? (Math.abs(k) < w * 0.4 ? IVO : Math.abs(k) < w * 0.75 ? YEL2 : YEL) : (u > head - 0.08 ? YEL2 : Math.abs(k) < w * 0.5 ? INK : INK2);
            p.px(x, y + k, c);
          }
        }
        if (V === "b" && head < 1) glint(p, Math.cos(Math.PI - head * Math.PI) * rx, cy + Math.sin(Math.PI - head * Math.PI) * ry, 4, C);
        return;
      }
      if (o.kind === "blast") {
        if (V === "a") {                              // 먹 방울이 반경 3u 안으로 튀고 노란 섬광·범위 고리
          for (const d of drops) { if (g > d.life) continue; const u = g / d.life, x = d.ca * d.v * g, y = d.sa * d.v * g + 160 * g * g;
            const s = Math.max(1, Math.round(d.s * (1 - 0.6 * u))); for (let yy = 0; yy < s; yy++) for (let xx = 0; xx < s; xx++) p.px(x + xx - s / 2, y + yy - s / 2, u < 0.55 ? INK : INK2);
            if (u < 0.35) p.px(x + s / 2, y - s / 2, YEL2); }
          burst(p, 0, 0, g, 30, C); ring(p, 0, 0, g - 0.02, 96, 0.3, C);
          sparks(p, 0, 0, g, sp, C);
        } else {
          burst(p, 0, 0, g, 36, C); ring(p, 0, 0, g - 0.03, 96, 0.28, C);
          for (const s of shards) { if (g > s.life) continue; const u = g / s.life, d = s.v * g * (1 - 0.45 * u); starShape(p, s.ca * d, s.sa * d, 9 * (1 - 0.4 * u), s.spin * g, u < 0.6 ? INK : INK2); if (u < 0.5) p.px(s.ca * d, s.sa * d, YEL); }
        }
        return;
      }
      if (o.kind === "step") {
        const inn = o.mode === "in";
        if (V === "a") {
          for (const pf of puffs) { const q = g; if (q > pf.life) continue; const u = q / pf.life, uu = inn ? 1 - u : u;
            for (let q2 = 0; q2 < 4; q2++) {                     // 동그란 덩이 대신 네모난 먹 조각 넷(점묘·도형 느낌 없이)
              const ox = pf.x + pf.vx * uu * (0.25 + q2 * 0.12) + (q2 - 1.5) * 4, oy = pf.y + pf.vy * uu * (0.25 + q2 * 0.1) + ((q2 * 7) % 5 - 2) * 3;
              const s = Math.max(1, Math.round(pf.r * 0.55 * (inn ? 0.5 + 0.5 * (1 - u) : 1 - 0.75 * u)));
              for (let yy = 0; yy < s; yy++) for (let xx = 0; xx < s; xx++) p.px(ox + xx, oy + yy, u < 0.5 ? INK : INK2); } }
        } else {
          for (const s of shards) { const L = 0.28; if (g > L) continue; const u = g / L, uu = inn ? 1 - u : u, d = 6 + 40 * ease.outCubic(uu);
            starShape(p, s.ca * d, -30 + s.sa * d * 0.9, 8 * (1 - 0.4 * u), s.spin * g, INK); if (u < 0.5) p.px(s.ca * d, -30 + s.sa * d * 0.9, YEL); }
          if (g < 0.08) glint(p, 0, -30, 7 * (1 - g / 0.08) + 2, C);
        }
        return;
      }
      if (o.kind === "giant" && V[0] === "c") {      // c 계열(11-08 「엄청 빠르게 돌고 바닥에 스파크」): 장마다 36도(초당 5바퀴), 0.6초 반복
        const f = Math.round(g * 50), ang = f * (36 * Math.PI / 180);
        const hub = () => { starShape(p, 0, 0, 30, ang, INK2, 0.46); disc(p, 0, 0, 10, INK); disc(p, 0, 0, 7, YEL); disc(p, 0, 0, 3, YEL2); };
        const tipArcs = (len, wMax) => { for (let k = 0; k < 4; k++) for (let s = 0; s < 34; s++) {
          const a = ang + k * Math.PI / 2 - (s / 34) * len, w = s < 10 ? wMax : s < 22 ? Math.max(1, wMax - 2) : 1;
          for (let rr = 47 - w; rr <= 47; rr++) p.px(Math.cos(a) * rr, Math.sin(a) * rr, s < 6 ? IVO : s < 14 ? YEL2 : s < 24 ? YEL : C[1]); } };
        if (V === "c") {                              // 원판 흐림 + 날 끝 호 + 잔상 날 둘(사용자 「원 같은 건 안 보여도 된다」로 보류)
          for (let y = -47; y <= 47; y++) for (let x = -47; x <= 47; x++) { const r = Math.hypot(x + 0.5, y + 0.5); if (r >= 22 && r <= 46) p.px(x, y, r > 40 ? INK2 : INK3); }
          tipArcs(1.25, 5); starShape(p, 0, 0, 44, ang - 0.42, INK3, 0.46); starShape(p, 0, 0, 45, ang - 0.21, INK2, 0.46); starShape(p, 0, 0, 46, ang, INK, 0.46); hub();
        } else if (V === "c1") {                      // c1 잔상 날: 원판 없이 뒤로 처지는 먹빛 잔상 날 셋
          starShape(p, 0, 0, 43, ang - 0.63, INK3, 0.42); starShape(p, 0, 0, 44, ang - 0.42, INK3, 0.44); starShape(p, 0, 0, 45, ang - 0.21, INK2, 0.46); starShape(p, 0, 0, 46, ang, INK, 0.46); hub();
        } else if (V === "c2") {                      // c2(확정 방향, 11-08 2차): 고리처럼 이어지던 노란 꼬리는 빼고 날 끝 바로 뒤 짧은 꼬리만,
          // 날은 곧은 변의 뾰족한 수리검 날(꼭짓점 8개 별), 바닥 스파크는 더 많고 굵고 밝게
          for (let k = 0; k < 4; k++) for (let s = 0; s < 14; s++) {         // 짧고 가늘어지는 꼬리(0.45rad), 날 끝 바깥으로 안 나간다
            const a = ang + k * Math.PI / 2 - (s / 14) * 0.45, w = Math.max(1, Math.round(4 * (1 - s / 14)));
            for (let rr = 44 - w; rr <= 44; rr++) p.px(Math.cos(a) * rr, Math.sin(a) * rr, s < 4 ? IVO : s < 9 ? YEL2 : YEL);
          }
          sharpStar(p, 0, 0, 48, 17, ang, INK); sharpStar(p, 0, 0, 30, 12, ang, INK2);
          disc(p, 0, 0, 9, INK); disc(p, 0, 0, 6, YEL); disc(p, 0, 0, 2.5, YEL2);
          for (const s of bigSparks) {                // 굵은 불꽃 줄(2px) + 하얀 머리, 가끔 십자 반짝
            let q = g - s.b; q = ((q % 0.6) + 0.6) % 0.6; if (q > s.life) continue;
            const u = q / s.life, vx = s.vx, vy = s.vy + 1300 * q, x = s.x + s.vx * q, y = 48 + s.vy * q + 650 * q * q, L = Math.hypot(vx, vy);
            const n = Math.max(3, Math.round(14 * (1 - u))), ux = vx / L, uy = vy / L;
            for (let i = 0; i < n; i++) { const c = i < 2 ? (u < 0.5 ? IVO : YEL2) : i < 5 ? YEL2 : i < 9 ? YEL : C[1];
              p.px(x - ux * i, y - uy * i, c); p.px(x - ux * i, y - uy * i + 1, c); }
            if (s.star && u < 0.3) { for (let k = -3; k <= 3; k++) { p.px(x + k, y, Math.abs(k) < 2 ? IVO : YEL2); p.px(x, y + k, Math.abs(k) < 2 ? IVO : YEL2); } }
          }
          { const fl = f % 3, R2 = 4 + fl; for (let k = -R2 - 3; k <= R2 + 3; k++) p.px(6 + k, 47, Math.abs(k) < R2 ? IVO : YEL2);       // 닿은 자리 반짝(크게)
            for (let k = 1; k <= R2; k++) { p.px(6, 47 - k, k < 3 ? IVO : YEL2); p.px(6 - k, 47 - k, YEL); p.px(6 + k, 47 - k, YEL); } }
          return;
        } else if (V === "c3") {                      // c3 겹쳐 보이는 날: 장마다 45도 어긋난 별을 번갈아 겹쳐 팔 갈래처럼(바퀴살 착시)
          starShape(p, 0, 0, 44, ang + Math.PI / 4, f % 2 ? INK2 : INK3, 0.4); starShape(p, 0, 0, 46, ang, INK, 0.46); hub();
          if (f % 2) for (let k = 0; k < 4; k++) { const a = ang + k * Math.PI / 2; p.px(Math.cos(a) * 45, Math.sin(a) * 45, IVO); p.px(Math.cos(a) * 44, Math.sin(a) * 44, YEL2); }
        } else {                                      // c4 휘어진 날(스미어): 날 뒤쪽이 회전 방향 반대로 늘어진 초승달 모양
          for (let y = -47; y <= 47; y++) for (let x = -47; x <= 47; x++) {
            const dx = x + 0.5, dy = y + 0.5, r = Math.hypot(dx, dy); if (r > 46 || r < 8) continue;
            let a = Math.atan2(dy, dx) - ang; a = ((a % (Math.PI / 2)) + Math.PI / 2) % (Math.PI / 2);   // 날 하나 기준 각도(0 = 날 앞)
            const back = Math.PI / 2 - a;              // 날 앞에서 뒤로 잰 각
            const lead = a < 0.10 + 0.3 * (1 - r / 46), smear = back < 0.55 * (r / 46);
            if (lead || smear) p.px(x, y, lead ? INK : back < 0.25 * (r / 46) ? INK : INK2);
          }
          hub();
        }
        // 바닥 스파크: 닿은 자리(0,+48) 조금 앞에서 뒤·위로 튀는 불꽃 줄. 0.6초 주기로 끊김 없이
        for (const s of sparkLoop) {
          let q = g - s.b; q = ((q % 0.6) + 0.6) % 0.6; if (q > s.life) continue;
          const u = q / s.life, x = s.x + s.vx * q, y = 48 + s.vy * q + 700 * q * q;
          const n = Math.max(2, Math.round(9 * (1 - u))), ux = s.vx / Math.hypot(s.vx, s.vy + 700 * q * 2), uy = (s.vy + 1400 * q) / Math.hypot(s.vx, s.vy + 1400 * q);
          for (let i = 0; i < n; i++) { const c = i === 0 ? (u < 0.4 ? IVO : YEL2) : i < 3 ? YEL2 : i < 6 ? YEL : C[1]; p.px(x - s.vx / Math.abs(s.vx) * i * Math.abs(ux), y - uy * i, c); if (i < 2) p.px(x - s.vx / Math.abs(s.vx) * i * Math.abs(ux), y - uy * i + 1, c); }
        }
        { const fl = f % 3; for (let k = -fl - 2; k <= fl + 2; k++) p.px(6 + k, 47, Math.abs(k) < 2 ? IVO : YEL2); p.px(6, 46, IVO); }   // 닿은 자리 반짝
        return;
      }
      if (o.kind === "giant") {                       // 반복: 0.4초에 90도(무겁게), 바닥 = +48
        const ang = (g / 0.4) * (Math.PI / 2);
        if (V === "b") for (let k = 0; k < 2; k++) {
          const a0 = ang * 1.5 + k * Math.PI; for (let s = 0; s < 40; s++) { const a = a0 + (s / 40) * 1.4, r = 46; const x = Math.cos(a) * r, y = Math.sin(a) * r; p.px(x, y, s > 30 ? IVO : s > 18 ? YEL2 : YEL); p.px(x * 0.97, y * 0.97, YEL); }
        }
        starShape(p, 0, 0, 46, ang, INK, 0.46); starShape(p, 0, 0, 30, ang, INK2, 0.46); disc(p, 0, 0, 10, INK); disc(p, 0, 0, 7, YEL); disc(p, 0, 0, 3, YEL2);
        for (const d of dust) { const q = ((g + d.ph * 0.5) % 0.5); const x = -10 + d.vx * q, y = 48 + d.vy * q + 200 * q * q;
          if (y > 48) continue;
          if (V === "a") { const s = Math.max(1, Math.round(d.s * (1 - q / 0.5))); for (let yy = 0; yy < s; yy++) for (let xx = 0; xx < s; xx++) p.px(x + xx, y + yy, q < 0.25 ? INK2 : INK3); }
          else { p.px(x, y, q < 0.15 ? IVO : q < 0.3 ? YEL2 : YEL); p.px(x + 1, y, YEL); }
        }
        return;
      }
    },
  };
}
