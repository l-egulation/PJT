import { fit, Rng } from "../../core/index.js";
import { FLURRY_PAL } from "./dagger-flurry-cuts.js";
// 큰 관통 수리검 탄, 지름 2유닛(64px, 2026-11-10 밸런스) 시안 — 곧은 변의 뾰족한 네 날 수리검(날빛·칼끝 반짝·가운데 구멍).
// 반복: 0.12초에 90도. 원점 = 탄 중심, 앞 = +x. trail 로 뒤따르는 잔상 표현을 고른다(2026-11-11 사용자 「잔상을 다르게」).
//   trail="ghost"   예전: 옅은 먹 잔상 별 하나 + 노란 바람 줄 다섯
//   trail="arc"     칼끝 궤적: 네 칼끝이 지나간 자리를 뒤로 늘어지는 노란·먹 호(회전 원판 잔상)로 그린다
//   trail="smear"   먹 꼬리: 별 뒤로 가늘어지는 먹 띠가 출렁이며 끌리고, 먹 방울이 떨어져 나간다
//   trail="outline" 빈 윤곽 잔상: 노란 테두리만 남은 별 셋이 뒤로 갈수록 작고 옅게, 회전이 조금씩 늦다
export const meta = {
  name: "Shuriken big star", desc: "Big piercing shuriken: a sharp four-blade star with a choice of after-image trail.",
  tags: ["ink", "shuriken", "projectile"], size: [440, 120], origin: [220, 60], duration: 0.48, loop: true, fps: 50, bg: "light",
  options: { R: 32, r: 10, seed: 5, trail: "arc" },
};
const INKS = ["#1b1616", "#3a3230", "#5a4e4a"];
function blades(R, r, ang) { const pts = []; for (let i = 0; i < 8; i++) { const a = ang + i * Math.PI / 4, rr = i % 2 ? r : R; pts.push([Math.cos(a) * rr, Math.sin(a) * rr]); } return pts; }
function inside(pts, x, y) { let c = false; for (let i = 0, j = pts.length - 1; i < pts.length; j = i++) { const [xi, yi] = pts[i], [xj, yj] = pts[j]; if ((yi > y) !== (yj > y) && x < ((xj - xi) * (y - yi)) / (yj - yi) + xi) c = !c; } return c; }
function star(p, cx, cy, R, r, ang, body, edge, hole, glint) {
  const pts = blades(R, r, ang), B = Math.ceil(R) + 1;
  for (let y = -B; y <= B; y++) for (let x = -B; x <= B; x++) {
    const px = x + 0.5, py = y + 0.5; if (!inside(pts, px, py)) continue;
    const d = Math.hypot(px, py);
    if (hole && d < r * 0.6) { if (d > r * 0.6 - 1.6) p.px(cx + x, cy + y, glint); continue; }
    let c = body;
    if (edge) {
      const a = (((Math.atan2(py, px) - ang) % (Math.PI / 2)) + Math.PI / 2) % (Math.PI / 2);
      if (a > Math.PI / 4 && a < Math.PI / 4 + 0.3 && d > r * 1.1) c = edge;
      if (d > R - 4 && (a < 0.1 || a > Math.PI / 2 - 0.1)) c = glint;
    }
    p.px(cx + x, cy + y, c);
  }
}
// 테두리만(안쪽 비움): 별 안이면서 한 칸 바깥이 별 밖인 칸
function outline(p, cx, cy, R, r, ang, c, w = 1.5) {
  const pts = blades(R, r, ang), inner = blades(Math.max(1, R - w * 1.6), Math.max(1, r - w), ang), B = Math.ceil(R) + 1;
  for (let y = -B; y <= B; y++) for (let x = -B; x <= B; x++) { const px = x + 0.5, py = y + 0.5; if (inside(pts, px, py) && !inside(inner, px, py)) p.px(cx + x, cy + y, c); }
}
export function create(o) {
  const C = fit(FLURRY_PAL, 5), [INK, INK2, INK3] = fit(INKS, 3), YEL = C[2], YEL2 = C[3], IVO = C[4];
  const Rn = new Rng(o.seed), drops = Array.from({ length: 10 }, (_, i) => ({ ph: i / 10, y: Rn.range(-10, 10), s: Rn.range(1.5, 3), v: Rn.range(90, 160) }));
  // 바닥 스파크(trail="dash"): 탄 아래 땅(+R)에 칼끝이 긁히며 뒤로 튀는 불티. 0.48초 고리로 반복되게 태어난 시각을 나눈다
  const sparks = Array.from({ length: o.sparks ?? 150 }, () => ({ b: Rn.range(0, 0.48), x: Rn.range(-14, 14), vx: -Rn.range(240, 700) * (o.sparkPower ?? 1), vy: -Rn.range(80, 340) * (o.sparkPower ?? 1), life: Rn.range(0.12, 0.3) * (o.sparkPower ?? 1), big: Rn.chance ? Rn.chance(0.2) : false }));
  let t = 0;
  return {
    update(dt) { t += dt; },
    draw(p) {
      const g = t + 1e-6, w = (Math.PI / 2) / (o.quarter ?? 0.12), ang = g * w, R = o.R, r = o.r, k = R / 32, T = o.trail;
      if (T === "ghost") {
        for (let i = 0; i < 5; i++) {
          const y = Math.round((-20 + i * 10) * k), L = Math.round((70 + ((i * 7 + Math.floor(g * 50)) % 4) * 14 - Math.abs(i - 2) * 12) * k), x0 = Math.round(R * 0.7);
          for (let x = -L - x0; x < -x0; x++) p.px(x, y, x > -x0 - 18 * k ? YEL2 : YEL);
        }
        star(p, -Math.round(40 * k), 0, R * 0.86, r * 0.86, ang - 0.6, INK3, null, false, INK3);
      }
      if (T === "arc") {
        // 앞으로 날아가는 회전 별의 칼끝은 사이클로이드를 그린다: 지난 0.1초의 칼끝 자리를 이어 호 꼬리로
        const speed = 18 * 32, N = 40;
        for (let b = 0; b < 4; b++) for (let s = N; s >= 1; s--) {
          const dt = (s / N) * 0.1, a = ang - dt * w + b * Math.PI / 2, x = -speed * dt * 0.55 + Math.cos(a) * (R - 2), y = Math.sin(a) * (R - 2);
          const u = s / N, wdt = (1 - u) * 3.6 + 1;
          for (let q = -Math.floor(wdt); q <= wdt; q++) for (let qx = -1; qx <= 1; qx++) p.px(x + qx, y + q * 0.7, u < 0.3 ? YEL2 : u < 0.65 ? YEL : INK3);
        }
        for (let i = 0; i < 3; i++) { const y = Math.round((-12 + i * 12) * k), L = Math.round((54 + ((i * 5 + Math.floor(g * 50)) % 3) * 10) * k); for (let x = -L - R; x < -R; x++) { p.px(x, y, x > -R - 14 ? YEL2 : YEL); p.px(x, y + 1, YEL); } }
      }
      if (T === "smear") {
        // 먹 띠: 별 뒤 120px 동안 굵기가 줄며 위아래로 출렁이는 먹 꼬리
        for (let x = -R * 0.3; x > -R * 0.3 - 130 * k; x--) {
          const u = (-x - R * 0.3) / (130 * k), half = (R * 0.62) * Math.pow(1 - u, 1.4) + 0.6, cy = Math.sin(x * 0.07 + g * 26) * 5 * u * k;
          for (let y = Math.floor(cy - half); y <= cy + half; y++) { const e = Math.abs(y - cy) / half; p.px(x, y, e > 0.8 ? INK3 : u > 0.6 ? INK2 : INK); }
          if (u < 0.5 && Math.abs(Math.floor(x) % 23) === 0) p.px(x, cy - half - 1, YEL2);
        }
        for (const d of drops) { const a = ((g * 2.1 + d.ph) % 1), x = -R * 0.6 - a * d.v * k, y = d.y * k + a * a * 40 * k; if (a < 0.9) for (let yy = -d.s; yy <= d.s; yy++) for (let xx = -d.s; xx <= d.s; xx++) if (xx * xx + yy * yy <= d.s * d.s) p.px(x + xx, y + yy, INK); }
      }
      if (T === "outline") {
        // 노란 윤곽만 남은 별 셋: 뒤로 갈수록 작고 옅고, 회전이 늦다
        const L = [[-34, 0.88, 0.25, YEL2], [-64, 0.74, 0.5, YEL], [-90, 0.6, 0.75, C[1]]];
        for (const [dx, s, lag, c] of L) outline(p, Math.round(dx * k), 0, R * s, r * s, ang - lag, c, 1.6);
        for (let i = 0; i < 2; i++) { const y = Math.round((-R * 0.95 + i * R * 1.9)), L2 = Math.round(80 * k); for (let x = -L2 - R * 0.4; x < -R * 0.4; x++) p.px(x, y, x > -R * 0.4 - 16 ? YEL2 : YEL); }
      }
      if (T === "dash" || T === "sparksOnly") {
        for (const s of sparks) for (const k of [0, 1]) {
          const age = g - s.b - k * 0.48; if (age < 0 || age > s.life) continue;
          const x = s.x + s.vx * age, y = R - 1 + s.vy * age + 900 * age * age; if (y > R) continue;
          const u = age / s.life, c = u < 0.25 ? IVO : u < 0.6 ? YEL2 : YEL, L = Math.max(2, Math.round((1 - u) * 6));
          for (let q = 0; q < L; q++) p.px(x + q, y + q * (s.vy / s.vx) * 0.6, c); if (s.big || u < 0.3) { p.px(x, y - 1, c); p.px(x + 1, y - 1, c); }
        }
        for (let x = -10; x <= 10; x++) if (Math.abs(x) < 6 || (x + Math.floor(g * 50)) % 3 === 0) p.px(x, R - 1, x * x < 9 ? IVO : YEL2);   // 긁히는 자리 빛
      }
      if (T !== "sparksOnly") star(p, 0, 0, R, r, ang, INK, IVO, true, YEL2);
    },
  };
}
