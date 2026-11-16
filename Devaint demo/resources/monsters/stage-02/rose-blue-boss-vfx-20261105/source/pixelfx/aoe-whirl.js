// 3페이즈 패턴 5 확장 광역기 — 화살 회오리의 곁들임 효과(화살 자체는 게임·시뮬레이션이 확정 3페이즈 화살 스프라이트로 따로 그린다).
// layer: back = 화면 안쪽 절반(화살 뒤), front = 화면 앞쪽 절반(화살 앞). 두 장을 화살 앞뒤로 겹쳐 띠가 화살 사이로 감기게 한다.
//   잉크 바람 띠: 여러 높이에서 깔때기를 따라 휘도는 짙은 잉크 띠(공허 보라 테, 앞 끝 핏빛)
//   바닥 소용돌이: 바닥의 잉크 고리와 돌아가는 핏빛 점선, 둘레에서 접선 방향으로 튀는 먼지
//   가운데 기둥: 바닥에서 위로 나선을 그리며 빨려 오르는 공허 보라 입자
//   불티: 깔때기 가장자리에서 접선 방향으로 튀는 핏빛 불티
// 판정(사각형, 0.5초 전조 뒤 2초 동안 가운데 바닥에서 반 폭 0 → 12.19u, 높이 0 → 15u)은 그대로이고 그림만 그린다.
// 깔때기: 높이 y에서 반지름 hw·(0.5 + 0.5·y/hh), 원근 납작함 0.45u(시뮬레이션의 화살 배치와 같다). 2.5초에 판정이 끝나면 0.3초 동안 바깥으로 퍼지며 사라진다.
// 반 해상도(1px = 게임 2px, 1u = 16px)로 그려 2배로 쓴다. 원점은 광역기 가운데 바닥, 위가 -y.
import { Rng, fit, clamp, seg, ease, rand, TAU } from "../../core/index.js";

export const meta = {
  name: "Arrow whirlwind dressing",
  desc: "The dressing around Blue's arrow whirlwind: ink wind ribbons wrapping a funnel, a spinning ink ring on the floor kicking dust, violet motes sucked up the middle and wine sparks flung off the rim.",
  tags: ["whirlwind", "vortex", "ink", "roseblue", "one-shot"],
  size: [440, 268],
  origin: [220, 254],
  duration: 2.8,
  loop: false,
  fps: 30,
  bg: "dark",
  options: { layer: "back", seed: 13 },
};

const SH = fit(["#0a040c"], 1)[0];
const VD = fit(["#0c0618", "#180a2c", "#26123e", "#361a56", "#4c2a74", "#6a42a0", "#9270cc", "#c4aef0", "#f0e8ff"], 9);
const N = fit(["#020309", "#050a16", "#0a1328", "#111e3c", "#1b3058"], 5);
const R8W = fit(["#12040a", "#3a0a1c", "#6a1230", "#a0223e", "#d04050", "#f0a0a8"], 6);
const FLAT = 7.2;
// Blue 압박 잉크 회오리(ink-whirl.js)처럼 다른 크기·시간·색으로도 쓴다: hw·hh(px), tell·grow·end(초), ink(빨강 대신 보라 불티). 기본은 3P5 확정 값.
// blue: 보라 대신 Blue 색(심연 남색→파랑)으로 칠한다(Blue 압박 잉크 회오리).
const VB = fit(["#060a18", "#0a1328", "#111e3c", "#1b3058", "#24386c", "#30487e", "#4870a8", "#6a8cc0", "#a8c4e4"], 9);
const R8B = fit(["#050a16", "#111e3c", "#1b3058", "#2c4a7c", "#4870a8", "#a8c4e4"], 6);
const R8I = fit(["#0c0618", "#26123e", "#4c2a74", "#7a58b8", "#9270cc", "#c4aef0"], 6);

export function draw(p, t, o) {
  const HW = o.hw ?? 195, HH = o.hh ?? 240, TELL = o.tell ?? 0.5, END = o.end ?? 2.5, GROWN = TELL + (o.grow ?? END - TELL), R8 = o.ink ? (o.blue ? R8B : R8I) : R8W, V = o.blue ? VB : VD;
  const front = o.layer === "front", g = seg(t, TELL, GROWN), end = seg(t, END, END + 0.3), fade = 1 - end;
  const hw = HW * g * (1 + end * 0.6), hh = HH * g, Rg = new Rng(o.seed);
  const side = (a) => (Math.sin(a) < 0) === front; // 앞(sin < 0, 화면 아래쪽 = 관찰자 쪽)만 또는 뒤만. 화면 높이는 -sin(a)(시뮬레이션의 화살 배치와 같은 방향)
  const rad = (y) => hw * (0.5 + 0.5 * clamp(y / Math.max(1, hh)));

  // 전조 동안 바닥 가운데로 잉크가 모여 돌기 시작한다
  if (t < TELL + 0.1) {
    const k = seg(t, 0.05, TELL), rr = 10 + 26 * k;
    for (let a = 0; a < TAU; a += 0.06) if (side(a)) { const q = a + t * 9, d = Math.sin(q * 3) > 0; p.dot(Math.cos(a) * rr, -2 - Math.sin(a) * rr * 0.25, d ? R8[3] : N[3], k); }
    if (!front) p.glow(0, -6, 20 + 20 * k, V[4], 0.35 * k);
  }
  if (g <= 0 || fade <= 0) return;

  // 바닥 소용돌이: 잉크 고리 세 겹과 돌아가는 핏빛 점선
  for (const [f, w, c] of [[1.0, 3, N[2]], [0.72, 2, N[3]], [0.45, 2, V[3]]]) {
    const rx = hw * f, ry = Math.max(3, 14 * g) * f;
    for (let a = 0; a < TAU; a += 1.2 / Math.max(20, rx)) {
      if (!side(a)) continue;
      const x = Math.cos(a) * rx, y = -2 - Math.sin(a) * ry, dash = Math.sin(a * 9 - t * 14 * (1.4 - f)) > 0.55;
      for (let k = 0; k < w; k++) p.dot(x, y - k, k === 0 ? SH : dash ? R8[4] : c, fade);
    }
  }
  // 둘레 먼지: 접선 방향으로 튀어 흩어진다
  for (let i = 0; i < 34; i++) {
    const ph = Rg.next(), life = (t * Rg.range(1.2, 2.2) + ph) % 1, a0 = Rg.range(0, TAU) + t * 3, a = a0 + life * 0.9;
    if (!side(a)) continue;
    const r = hw * (1 + life * 0.25), x = Math.cos(a) * r, y = -2 - Math.sin(a) * 14 * g - life * Rg.range(6, 22), s = (1 - life) * Rg.range(2, 4.5);
    if (s > 0.8) { p.circle(x, y, s + 1, SH, fade * 0.9); p.circle(x, y, s, N[3], fade * 0.9); p.dot(x - s * 0.4, y - s * 0.4, N[4], fade); }
  }

  // 잉크 바람 띠: 기본 9가닥(ribbons), 높이·길이·속도·두께가 제각각. ribbonW·ribbonLen으로 굵기·길이를 키운다(Blue 압박 잉크 회오리).
  for (let i = 0; i < (o.ribbons ?? 9); i++) {
    const y0 = Rg.range(0.08, 0.95), len = Rg.range(1.2, 2.6) * (o.ribbonLen ?? 1), w = Rg.range(3, 6.5) * (o.ribbonW ?? 1), sp = Rg.range(5.5, 8.5), ph = Rg.range(0, TAU), bob = Rg.range(0, TAU);
    const yb = hh * y0 + Math.sin(t * 3 + bob) * 6, r = rad(yb) * Rg.range(0.82, 1.12), head = ph + t * sp;
    const steps = Math.ceil(len * r / 2) + 6;
    if (o.solid) {
      // 한 덩어리 띠: 마디마다 원을 겹쳐 그림자 테 → 몸 → 윗면 → 위 가장자리 빛 순서로 칠한다(세로 줄무늬 없이).
      const pts = [];
      for (let k = 0; k <= steps * 2; k++) {
        const u = k / (steps * 2), a = head - u * len; if (!side(a)) continue;
        // 사라질 때는 흐리게(체크무늬) 하지 않고 가늘어지며 끊어진다.
        const th = w * Math.sin(Math.PI * Math.min(1, (1 - u) * 1.3 + 0.05)) * (0.6 + 0.4 * g) * fade;
        if (th >= 0.8 && (fade > 0.6 || rand(i, k, 9) < fade * 1.4)) pts.push([Math.cos(a) * r, -yb - Math.sin(a) * FLAT, th, u]);
      }
      for (const [x, y, th] of pts) p.circle(x, y, th + 1, SH);
      for (const [x, y, th, u] of pts) p.circle(x, y, th, u < 0.08 ? R8[3] : N[2]);
      for (const [x, y, th, u] of pts) p.circle(x, y - th * 0.3, th * 0.55, u < 0.08 ? R8[4] : N[3]);
      for (const [x, y, th, u] of pts) p.dot(x, y - th + 1, u < 0.08 ? R8[5] : V[5]);
      continue;
    }
    for (let k = 0; k <= steps; k++) {
      const u = k / steps, a = head - u * len; // u 0 = 앞 끝
      if (!side(a)) continue;
      const x = Math.cos(a) * r, y = -yb - Math.sin(a) * FLAT, th = w * Math.sin(Math.PI * Math.min(1, (1 - u) * 1.3 + 0.05)) * (0.6 + 0.4 * g);
      if (th < 0.8) continue;
      for (let q = -th; q <= th; q += 1) {
        const e = Math.abs(q) / th, c = e > 0.82 ? SH : u < 0.08 ? (e < 0.4 ? R8[5] : R8[3]) : q < -th * 0.35 ? V[5] : e < 0.3 ? N[3] : N[2];
        p.dot(x, y + q, c, fade * (u > 0.85 ? (1 - u) / 0.15 : 1));
      }
    }
  }

  // 가운데 기둥: 나선을 그리며 빨려 오르는 입자
  for (let i = 0; i < 46; i++) {
    const life = (t * Rg.range(0.5, 0.9) + Rg.next()) % 1, a = Rg.range(0, TAU) + life * 9 + t * 4, y = life * hh, r = (1 - life * 0.6) * Rg.range(8, 30) * (0.4 + 0.6 * g);
    if (!side(a)) continue;
    const x = Math.cos(a) * r, yy = -y - Math.sin(a) * 3;
    p.dot(x, yy, i % 4 ? V[6] : V[8], fade * (1 - life)); if (i % 3 === 0) p.dot(x, yy + 1, V[4], fade * (1 - life));
  }
  if (!front) p.glowLine(0, -2, 0, -hh * 0.9, 18, V[4], 0.18 * fade);

  // 불티: 깔때기 가장자리에서 접선으로 튄다
  for (let i = 0; i < 40; i++) {
    const life = (t * Rg.range(1.5, 3) + Rg.next()) % 1, y = hh * Rg.range(0.1, 1), a0 = Rg.range(0, TAU) + t * 7, r = rad(y);
    if (!side(a0)) continue;
    const x0 = Math.cos(a0) * r, y0 = -y - Math.sin(a0) * FLAT, tx = -Math.sin(a0), d = life * Rg.range(20, 45);
    const x = x0 + tx * d + Math.cos(a0) * d * 0.4, yy = y0 + d * 0.15;
    p.dot(x, yy, life < 0.3 ? R8[5] : R8[4], fade * (1 - life)); if (life < 0.5) p.dot(x - tx * 2, yy, R8[3], fade * (1 - life));
  }
  if (front && t < END) p.glow(0, -hh * 0.45, Math.max(20, hw * 0.6), R8[2], 0.08 * fade);
}
