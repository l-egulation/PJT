import { Rng, fit, clamp, smooth, TAU } from "../../core/index.js";
import { FLURRY_PAL, crescent, burst, ring, sparks, needleGlow, star } from "./dagger-flurry-cuts.js";

// 레이피어 특수 4종 이펙트 4차 시안(2026-11-08). 사용자: 「하나같이 비슷비슷하다 → 여러 시안」.
// 3차는 넷 다 끝이 「금빛 터짐 + 금 고리 + 불티」라 비슷했다. 4차는 스킬마다 대표 모양을 달리하고 그 묶음은 모아찌르기 하나에만 남긴다.
// 크기는 3차(다른 무기 스킬 크기), 초승달·섬광은 얇게. 먹·노랑·상아, 화소 단위.
//   charge  c1 금빛 모임 → 금빛 터짐(3차 a)        c2 먹 응축 → 먹 창 + 먹 튐 + 금 반짝   c3 도는 고리 모임 → 나선 창(두 줄이 꼬여 칼끝으로)
//   needle  n1 관통 화살촉(적을 뚫고 화면 밖까지, 지나간 자리 금선)   n2 공기 꿰뚫기(가는 섬광 + 모여드는 바람줄 + 꽂힌 자리 별 셋)   n3 바늘비(가는 금 바늘 다섯이 부채꼴로 날아감)
//   slip    s1 그림자 스밈(먹 소용돌이 → 먹 X 자국이 갈라짐)   s2 금빛 소용돌이 → 금 X(터짐·고리 없이)   s3 일섬 통과(떠날 때 앞으로 금줄, 도착하면 뒤에서 온 긴 금줄)
//   flurry  f1 난무 초승달(타마다 터짐·고리 없이, 5타만 십자 + 고리)   f2 별무리(타마다 큰 네 갈래 별, 5타 큰 별 터짐)   f3 먹·금 난도질(먹 초승달·금 초승달 번갈아, 5타 먹 튐 + 금 X)
// 5차(사용자 「1·1·2·1, 좀 더 차별점」): 고른 시안에 스킬마다 다른 재질·대표 모양을 더한 판(…x).
//   c1x 모아: 칼끝에 하얗게 달아오르는 구슬이 커지다 → 흰 심 큰 터짐 + 여덟 갈래 긴 빛살 + 발밑 납작 먼지 고리(무거운 한 방)
//   n1x 장침: 화살촉을 은백(먹 회색·상아·흰색)으로 + 칼끝에서 뒤로 벌어지는 V자 충격선(음속 돌파 느낌)
//   s2x 흘려: 떠날 때 금빛 소용돌이에서 금 깃털 조각이 흩날림 → 도착 금 X 에 먹 그림자 X 가 겹쳐 잠깐 남았다가 금 조각으로 부서짐
//   f1x 연침: 타마다 가는 초승달 둘이 다른 각도로 + 짧은 찌르기, 5타는 초승달 여덟 장이 꽃잎처럼 피어남 + 고리
// 원점 = 시전자 발밑(slip_out 떠난 자리, slip_in 도착 자리), 32px = 1유닛. 시각 0 = 시전 시작(slip_in 은 순간이동 0.10초).
export const meta = {
  name: "Rapier special fx v4 (ink)",
  desc: "Rapier specials, four distinct signatures per skill (gather-burst, pierce, shadow slip, flurry).",
  tags: ["ink", "rapier", "one-shot"],
  size: [320, 184],
  origin: [44, 150],
  duration: 1.2,
  loop: false,
  fps: 50,
  bg: "light",
  options: { skill: "charge", variant: "c1", seed: 3 },
};
const INKS = ["#1b1616", "#3a3230", "#5a4e4a", "#8a7d77"];
const Y = -32, HOLD_TIP = [44, -15];
const DUR = { charge: 1.2, needle: .75, slip_out: .4, slip_in: .45, flurry: 1.0 };

export function create(o) {
  const C = fit(FLURRY_PAL, 5), [INK, INK2, INK3, INK4] = fit(INKS, 4), R = new Rng(o.seed);
  const IC = [INK, INK3, INK2, INK, INK];                  // 먹 초승달·먹 바늘용(ramp 가 진한 심을 고른다)
  const sk = o.skill, v = o.variant;
  const spray = (n, a0, spread, v0, v1, l0, l1) => Array.from({ length: n }, () => { const a = a0 + R.range(-spread, spread); return { ca: Math.cos(a), sa: Math.sin(a), v: R.range(v0, v1), life: R.range(l0, l1), d: R.range(0, .03) }; });
  const big = spray(30, 0, Math.PI, 140, 340, .12, .3);
  const suck = Array.from({ length: 20 }, (_, i) => ({ a: i * 2.39996, r: R.range(34, 58), ph: R.range(0, 1) }));
  const drops = Array.from({ length: 12 }, () => { const a = R.range(-1.1, 1.1); return { ca: Math.cos(a), sa: Math.sin(a), v: R.range(90, 200), r: R.range(2.5, 4.5), life: R.range(.2, .32) }; });
  const ring360 = Array.from({ length: 14 }, () => { const a = R.range(0, TAU); return { ca: Math.cos(a), sa: Math.sin(a), v: R.range(70, 160), r: R.range(2.5, 5), life: R.range(.18, .3) }; });
  const wind = Array.from({ length: 10 }, (_, i) => ({ y: (i % 2 ? -1 : 1) * (8 + (i >> 1) * 7), x0: R.range(60, 200), l: R.range(18, 34), d: R.range(0, .05) }));
  const FH = [.15, .27, .39, .51, .63];
  const SP = [[80, -34], [92, -44], [84, -26], [96, -40], [88, -34]];
  const CR = FH.map(() => [R.range(0, TAU), R.range(0, TAU)]);
  function blob(p, x, y, r, c) { for (let yy = -Math.ceil(r); yy <= r; yy++) for (let xx = -Math.ceil(r); xx <= r; xx++) if (xx * xx + yy * yy <= r * r) p.px(x + xx, y + yy, c); }
  function inkSplash(p, x, y, q, list) { for (const d of list) { if (q < 0 || q > d.life) continue; const k = q / d.life; blob(p, x + d.ca * d.v * q, y + d.sa * d.v * q + 200 * q * q, d.r * (1 - .6 * k), k < .5 ? INK : INK2); } }
  function helix(p, x0, x1, y, amp, ph, w) {
    for (const s of [0, Math.PI]) { let px = null, py = null;
      for (let i = 0; i <= 40; i++) { const u = i / 40, x = x0 + (x1 - x0) * u, yy = y + amp * (1 - u) * Math.sin(u * 4.5 * Math.PI + ph + s);
        if (px !== null) { const n = Math.max(1, Math.ceil(Math.hypot(x - px, yy - py))); for (let k = 0; k <= n; k++) { const xx = px + (x - px) * k / n, y2 = py + (yy - py) * k / n; for (let ww = 0; ww < w; ww++) p.px(xx, y2 + ww, k % 3 ? C[3] : C[4]); } }
        px = x; py = yy; } }
  }
  const SIL = fit(["#1b1616", "#6e6460", "#b8b0a8", "#e8e2d8", "#ffffff"], 5);   // 은백(장침 차별). fit 을 거쳐야 그려진다
  const flakes = Array.from({ length: 16 }, () => { const a = R.range(0, TAU); return { ca: Math.cos(a), sa: Math.sin(a), v: R.range(40, 130), life: R.range(.25, .4), spin: R.range(0, TAU) }; });
  function diamond(p, x, y, r, c, c2) { for (let yy = -r; yy <= r; yy++) for (let xx = -r; xx <= r; xx++) if (Math.abs(xx) + Math.abs(yy) <= r) p.px(x + xx, y + yy, Math.abs(xx) + Math.abs(yy) < r * .5 ? c2 : c); }
  function ellipse(p, cx, cy, rx, ry, w, c) { const n = Math.ceil(rx * 4); for (let i = 0; i < n; i++) { const a = i / n * TAU; for (let k = 0; k < w; k++) p.px(cx + Math.cos(a) * (rx - k), cy + Math.sin(a) * (ry - k * ry / rx), c); } }
  let t = 0;
  return {
    update(dt) { t += dt; },
    draw(p) {
      const g = t + 1e-6;
      if (g > DUR[sk]) return;
      if (sk === "charge") {
        const [hx, hy] = HOLD_TIP, q = g - .8, k = clamp((g - .2) / .6), charging = g >= .2 && g < .8;
        if (v === "c1x") {
          if (charging) {
            for (const s of suck) { const ph = (g * 1.8 + s.ph) % 1, r = s.r * (1 - ph) + 4, x = hx + Math.cos(s.a) * r, y = hy + Math.sin(s.a) * r * .8;
              for (let i = 0; i < 5; i++) p.px(x + Math.cos(s.a) * i, y + Math.sin(s.a) * i * .8, i < 2 ? C[4] : C[2]); }
            const rr = 2 + 6 * k; blob(p, hx, hy, rr + 1.5, C[2]); blob(p, hx, hy, rr, C[3]); blob(p, hx, hy, rr * .55, C[4]);
            if (k > .4) ring(p, hx, hy, (g * 3) % .33, 12 + 10 * k, .33, C);
          }
          if (q >= 0) {
            needleGlow(p, 80, Y, 0, 110, 3.4, clamp(q / .03), clamp((q - .1) / .12), C, 3);
            for (let j = 0; j < 8; j++) { const a = j * TAU / 8 + .2; needleGlow(p, 128 + Math.cos(a) * 36, Y + Math.sin(a) * 36, a, j % 2 ? 44 : 72, j % 2 ? 1.4 : 2, clamp(q / .03), clamp((q - .05) / .1), C, 2); }
            burst(p, 128, Y, q, 34, C); sparks(p, 128, Y, q, big, C);
            const kk = clamp(q / .22); if (kk < 1) ellipse(p, 128, 0, 16 + 70 * kk, 4 + 12 * kk, kk < .5 ? 2 : 1, kk < .4 ? C[3] : C[2]);
          }
        } else if (v === "c1") {
          if (charging) {
            for (const s of suck) { const ph = (g * 1.8 + s.ph) % 1, r = s.r * (1 - ph) + 4, x = hx + Math.cos(s.a) * r, y = hy + Math.sin(s.a) * r * .8;
              for (let i = 0; i < 5; i++) p.px(x + Math.cos(s.a) * i, y + Math.sin(s.a) * i * .8, i < 2 ? C[4] : C[2]); }
            star(p, hx, hy, 3 + 8 * k + (Math.floor(g * 25) % 2), C);
          }
          if (q >= 0) { needleGlow(p, 80, Y, 0, 110, 3.4, clamp(q / .03), clamp((q - .1) / .12), C, 3); burst(p, 124, Y, q, 28, C); ring(p, 124, Y, q - .01, 48, .26, C); sparks(p, 124, Y, q, big, C); }
        } else if (v === "c2") {
          if (charging) {
            for (const s of suck) { const ph = (g * 1.4 + s.ph) % 1, r = s.r * (1 - ph) + 5, a = s.a + ph * 2; blob(p, hx + Math.cos(a) * r, hy + Math.sin(a) * r * .8, 1.5 + 1.5 * (1 - ph), ph > .6 ? INK : INK2); }
            blob(p, hx, hy, 2 + 6 * k, INK); if (k > .3) star(p, hx, hy - 1, 2 + 3 * k, C);
          }
          if (q >= 0) {
            needleGlow(p, 84, Y, 0, 118, 4.2, clamp(q / .025), clamp((q - .1) / .14), IC, 0);
            needleGlow(p, 86, Y - 1, 0, 100, 1.4, clamp(q / .025), clamp((q - .08) / .1), C, 2);
            inkSplash(p, 130, Y, q, drops); star(p, 132, Y, q < .07 ? 12 - 120 * q : 0, C);
          }
        } else {
          if (charging) {
            for (let j = 0; j < 3; j++) crescent(p, hx, hy, 8 + 5 * j + 4 * k, g * (9 + 10 * k) * (j % 2 ? -1 : 1) + j * 2, 2.2, 1, 1.4 + .6 * k, 1, 0, C, 2);
            star(p, hx, hy, 2 + 4 * k, C);
          }
          if (q >= 0 && q < .3) { const kk = clamp(q / .04), fade = clamp((q - .1) / .16);
            helix(p, 60, 60 + 90 * kk, Y, 14 * (1 - fade), q * 50, fade < .5 ? 2 : 1);
            needleGlow(p, 100, Y, 0, 90, 2.4, kk, fade, C, 2);
            star(p, 150, Y, q < .08 ? 14 - 120 * q : 0, C);
            sparks(p, 150, Y, q, big.slice(0, 16), C); }
        }
        return;
      }
      if (sk === "needle") {
        const q = g - .3;
        if (q < 0) return;
        if (v === "n1x") {   // 은백 화살촉 + 칼끝 V자 충격선
          for (let j = 0; j < 3; j++) { const qq = q - .025 * j; if (qq < 0) continue;
            const x = 56 + 1500 * qq; if (x < 320) crescent(p, x - 22, Y, 24 - j * 3, -1.15, 2.3, 1, 3.8 - j * .6, 1, 0, SIL, 3); }
          needleGlow(p, 150, Y, 0, 210, 1.3, clamp(q / .06), clamp((q - .1) / .14), SIL, 2);
          const k = clamp(q / .14);
          if (k < 1) for (const s of [-1, 1]) { const L = 20 + 50 * k; for (let i = 0; i < L; i++) { const x = 70 - i * Math.cos(.35), y = Y + s * i * Math.sin(.35) * (1 + k); p.px(x + 30 * k, y, i < 6 ? SIL[4] : SIL[2]); } }
        } else if (v === "n1") {   // 화살촉 셋이 적을 뚫고 화면 밖까지, 지나간 자리에 가는 금선
          for (let j = 0; j < 3; j++) { const qq = q - .025 * j; if (qq < 0) continue;
            const x = 56 + 1500 * qq; if (x < 300) crescent(p, x - 22, Y, 22 - j * 3, -1.15, 2.3, 1, 3.6 - j * .6, 1, 0, C, 3); }
          needleGlow(p, 150, Y, 0, 210, 1.3, clamp(q / .06), clamp((q - .1) / .14), C, 2);
        } else if (v === "n2") {   // 가는 섬광 + 모여드는 바람줄 + 꽂힌 자리 별 셋
          needleGlow(p, 112, Y, 0, 140, 2, clamp(q / .015), clamp((q - .08) / .1), C, 2);
          for (const w of wind) { const qq = q - w.d; if (qq < 0 || qq > .16) continue; const k = qq / .16, x = w.x0 - 120 * qq, y = Y + w.y * (1 - k);
            for (let i = 0; i < w.l * (1 - k); i++) p.px(x + i, y, i < 4 ? C[4] : C[2]); }
          [[100, 0], [128, .025], [156, .05]].forEach(([x, d]) => star(p, x, Y, q - d >= 0 && q - d < .08 ? 11 - 120 * (q - d) : 0, C));
        } else {            // 바늘비: 가는 금 바늘 다섯이 부채꼴로 날아간다
          for (let j = 0; j < 5; j++) { const qq = q - .018 * j; if (qq < 0) continue; const a = (j - 2) * .045, d = 900 * qq;
            if (d < 170) { const cx = 60 + Math.cos(a) * (d + 20), cy = Y + Math.sin(a) * (d + 20); needleGlow(p, cx, cy, a, 44, 1.6, 1, 0, C, 2); }
            else if (d < 230) star(p, 60 + Math.cos(a) * 190, Y + Math.sin(a) * 190, 5 - (d - 170) / 15, C); }
        }
        return;
      }
      if (sk === "slip_out") {
        const q = g - .05;
        if (q < 0) return;
        const k = clamp(q / .22);
        if (v === "s2x") {
          for (let j = 0; j < 2; j++) crescent(p, 0, -34, 34 * (1 - .6 * k), q * 18 + j * Math.PI, 2.6, 1, 3.4 * (1 - k) + .8, 1, k, C, 3);
          for (const f of flakes) { if (q > f.life) continue; const u = q / f.life; diamond(p, f.ca * (14 + f.v * q), -34 + f.sa * 14 + 60 * q + 40 * q * q, u < .6 ? 2 : 1, C[2], C[4]); }
        } else if (v === "s1") { for (let j = 0; j < 2; j++) crescent(p, 0, -34, 32 * (1 - .6 * k), -q * 16 + j * Math.PI, 2.6, -1, 3.6 * (1 - k) + .8, 1, k, IC, 0); inkSplash(p, 0, -34, q, ring360); }
        else if (v === "s2") { for (let j = 0; j < 2; j++) crescent(p, 0, -34, 34 * (1 - .6 * k), q * 18 + j * Math.PI, 2.6, 1, 3.4 * (1 - k) + .8, 1, k, C, 3); star(p, 0, -34, q < .1 ? 12 - 110 * q : 0, C); }
        else { needleGlow(p, 50, Y, 0, 90, 2.4, clamp(q / .04), clamp((q - .06) / .1), C, 3); }
        return;
      }
      if (sk === "slip_in") {
        const q = g, cx = 46;
        if (v === "s2x") {   // 금 X + 먹 그림자 X, 잠깐 남았다 금 조각으로 부서진다
          const hold = q < .22, sh = smooth(.2, .26, q);
          if (hold) for (const [ang, d] of [[-.7, 0], [.7, .035]]) {
            needleGlow(p, cx + 3, Y + 3, ang, 110, 3, clamp((q - d) / .025), 0, IC, 0);
            needleGlow(p, cx, Y, ang, 110, 2.6, clamp((q - d) / .025), 0, C, 3);
          }
          if (q >= .2) for (const f of flakes) { const qq = q - .2; if (qq > f.life) continue; diamond(p, cx + f.ca * (f.v * 1.6 * qq + 10), Y + f.sa * (f.v * 1.6 * qq + 10) + 80 * qq * qq, qq < f.life * .5 ? 2 : 1, C[2], C[4]); }
          star(p, cx, Y, q < .08 ? 12 - 120 * q : 0, C);
        } else if (v === "s1") {   // 먹 X 자국 + 가는 금 테 → 위아래로 갈라지며 사라진다
          const sp = 10 * smooth(.08, .26, q), fade = smooth(.12, .34, q);
          for (const [ang, d, s] of [[-.7, 0, -1], [.7, .03, 1]]) {
            needleGlow(p, cx, Y + s * sp, ang, 100, 3, clamp((q - d) / .025), fade, IC, 0);
            needleGlow(p, cx, Y + s * sp - 1, ang, 90, 1, clamp((q - d) / .025), fade, C, 1.5);
          }
          inkSplash(p, cx, Y, q - .03, drops.slice(0, 6));
        } else if (v === "s2") {
          for (const [ang, d] of [[-.7, 0], [.7, .035]]) needleGlow(p, cx, Y, ang, 110, 2.8, clamp((q - d) / .025), clamp((q - d - .1) / .1), C, 3);
          star(p, cx, Y, q < .08 ? 12 - 120 * q : 0, C);
        } else {            // 뒤에서 달려온 긴 금줄이 적 자리까지 꽂히고 뒤에서부터 지워진다
          needleGlow(p, cx - 70, Y, 0, 200, 2.6, clamp(q / .03), smooth(.04, .22, q), C, 3);
          star(p, cx + 20, Y, q < .08 ? 12 - 120 * q : 0, C); sparks(p, cx + 20, Y, q, big.slice(0, 12), C);
        }
        return;
      }
      if (sk === "flurry") {
        FH.forEach((h, i) => {
          const gg = g - h; if (gg < -.03) return;
          const fin = i === 4, [sx, sy] = SP[i], head = .5 + .5 * clamp(gg / .025), tail = smooth(.05, fin ? .3 : .2, gg), dir = i % 2 ? -1 : 1;
          if (v === "f1x") {
            if (gg < 0) return;
            if (!fin) {
              for (let n = 0; n < 2; n++) crescent(p, sx + n * 6, sy - n * 4, 26 - n * 6, CR[i][n], 2.3, n ? -dir : dir, 2.4 - n * .6, .5 + .5 * clamp((gg - n * .015) / .02), smooth(.04 + n * .015, .16 + n * .015, gg), C, 2.5);
              needleGlow(p, sx - 16, sy, [-.18, .14, -.1, .2][i], 64, 1.6, head, smooth(.03, .12, gg), C, 2);
            } else {
              const k = clamp(gg / .1);
              for (let n = 0; n < 8; n++) { const a = n * TAU / 8, r = 10 + 30 * (1 - (1 - k) ** 2); crescent(p, sx + Math.cos(a) * r, sy + Math.sin(a) * r, 16, a - .9, 1.8, 1, 3, 1, smooth(.12, .3, gg), C, 2.5); }
              ring(p, sx, sy, gg - .02, 62, .28, C); star(p, sx, sy, gg < .1 ? 16 - 140 * gg : 0, C);
            }
          } else if (v === "f1") {
            if (gg < 0) return;
            crescent(p, sx, sy, fin ? 48 : 32, CR[i][0], 2.6, dir, fin ? 5.2 : 3.4, head, tail, C, 3);
            crescent(p, sx + 4, sy - 3, (fin ? 48 : 32) * .75, CR[i][1], 2.2, -dir, fin ? 4 : 2.6, .5 + .5 * clamp((gg - .02) / .025), smooth(.07, fin ? .32 : .22, gg), C, 2.5);
            needleGlow(p, sx - 10, sy, [-.18, .14, -.1, .2, 0][i], fin ? 140 : 100, fin ? 2.6 : 1.8, head, tail, C, 3);
            if (fin) { needleGlow(p, sx, sy, 1.45, 110, 2.2, .5 + .5 * clamp((gg - .03) / .025), smooth(.08, .32, gg), C, 3); ring(p, sx, sy, gg - .01, 58, .26, C); }
          } else if (v === "f2") {
            if (gg < 0) return;
            needleGlow(p, sx - 30, sy, 0, 70, 1.6, head, tail, C, 2);
            star(p, sx, sy, gg < (fin ? .14 : .08) ? (fin ? 24 : 11) * (1 - gg / (fin ? .14 : .08)) + 2 : 0, C);
            if (fin) { burst(p, sx, sy, gg, 26, C); sparks(p, sx, sy, gg, big, C); }
          } else {
            if (gg < 0) return;
            const ink = i % 2 === 0;
            crescent(p, sx, sy, fin ? 44 : 30, CR[i][0], 2.5, dir, fin ? 5 : 3.4, head, tail, ink && !fin ? IC : C, ink && !fin ? 0 : 3);
            if (fin) { inkSplash(p, sx, sy, gg, ring360); for (const [ang, d] of [[-.75, 0], [.75, .03]]) needleGlow(p, sx, sy, ang, 110, 2.4, clamp((gg - d) / .025), smooth(.1, .3, gg), C, 3); }
          }
        });
      }
    },
  };
}
