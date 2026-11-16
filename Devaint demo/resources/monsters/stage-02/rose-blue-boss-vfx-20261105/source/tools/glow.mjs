// 확정 이펙트(rose-blue-exchange-flash-sheet-v4) 화풍의 빛 그리기 도구. gen.mjs(Blue 급강하)와 arrow.mjs(Rose 화살)가 같이 쓴다.
// 외곽선 없음, 빛 자체가 형태, 단계로 끊은 반투명, 반 해상도로 그려 2배로 키운 2px 픽셀 덩어리.
export const hex = (h) => [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16)];
// 세기(0..1)를 단계로 끊는다: [최소 세기, 색 단계, 알파]
const STEPS = [[0.86, 0, 1], [0.7, 1, 0.95], [0.48, 2, 0.85], [0.28, 3, 0.7], [0.14, 3, 0.35]];
export const rad = (d) => (d * Math.PI) / 180;
export function rng(seed) { return () => { seed |= 0; seed = (seed + 0x6d2b79f5) | 0; let t = Math.imul(seed ^ (seed >>> 15), 1 | seed); t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t; return ((t ^ (t >>> 14)) >>> 0) / 4294967296; }; }

// 반 해상도 세기 버퍼. 도형마다 세기와 색(blue/rose)을 넣고, 픽셀마다 가장 센 도형의 색을 쓴다.
export class Field {
  // ramps: { 색 이름: [심, 연한 색, 중간, 짙은 색] }. 도형마다 색 이름을 넣고, 픽셀마다 가장 센 도형의 색을 쓴다.
  // scale: 버퍼 1칸이 몇 px인지. 기본 2(2px 덩어리), 작은 화살처럼 세밀해야 하면 1.
  constructor(w, h, ox, oy, ramps, scale = 2) {
    this.ramps = ramps; this.names = Object.keys(ramps); this.s = scale; this.w = w; this.h = h; this.ox = ox; this.oy = oy; this.i = new Float32Array(w * h); this.c = new Uint8Array(w * h); }
  // 지역 좌표(게임 px, 피벗 기준)를 받아 칠한다. fn(x, y) → 세기
  paint(x0, y0, x1, y1, hue, fn) {
    const c = Math.max(0, this.names.indexOf(hue));
    const s = this.s;
    for (let py = Math.max(0, Math.floor((y0 + this.oy) / s)); py <= Math.min(this.h - 1, Math.ceil((y1 + this.oy) / s)); py++)
      for (let px = Math.max(0, Math.floor((x0 + this.ox) / s)); px <= Math.min(this.w - 1, Math.ceil((x1 + this.ox) / s)); px++) {
        const v = fn(px * s + s / 2 - this.ox, py * s + s / 2 - this.oy); const k = py * this.w + px;
        if (v > this.i[k]) { this.i[k] = v; this.c[k] = c; }
      }
  }
  // 가운데가 두껍고 양끝이 뾰족한 바늘(빛 줄기). 심이 가장 밝다.
  needle(x0, y0, x1, y1, width, hue, k = 1) {
    const dx = x1 - x0, dy = y1 - y0, L = Math.hypot(dx, dy) || 1, m = width + 4;
    this.paint(Math.min(x0, x1) - m, Math.min(y0, y1) - m, Math.max(x0, x1) + m, Math.max(y0, y1) + m, hue, (x, y) => {
      const u = ((x - x0) * dx + (y - y0) * dy) / (L * L); if (u < 0 || u > 1) return 0;
      const d = Math.abs((x - x0) * dy - (y - y0) * dx) / L, w = width * Math.pow(Math.sin(Math.PI * u), 0.6) + 0.5;
      return k * Math.pow(Math.max(0, 1 - d / w), 0.55) * (0.6 + 0.4 * Math.sin(Math.PI * u));
    });
  }
  // 타원 위의 초승달: 각도 a0..a1, 가운데가 두껍고 양끝이 가늘다.
  crescent(cx, cy, rx, ry, a0, a1, width, hue, k = 1) {
    const m = width + 4;
    this.paint(cx - rx - m, cy - ry - m, cx + rx + m, cy + ry + m, hue, (x, y) => {
      const a = Math.atan2((y - cy) / ry, (x - cx) / rx); let u = (a - a0) / (a1 - a0);
      if (u < 0) u += (2 * Math.PI) / (a1 - a0); if (u < 0 || u > 1) return 0;
      const ex = cx + Math.cos(a) * rx, ey = cy + Math.sin(a) * ry, d = Math.hypot(x - ex, y - ey);
      const w = width * Math.pow(Math.sin(Math.PI * u), 0.7) + 0.4;
      return k * Math.pow(Math.max(0, 1 - d / w), 0.55) * (0.62 + 0.38 * Math.sin(Math.PI * u));
    });
  }
  glow(cx, cy, r, hue, k) { this.paint(cx - r, cy - r, cx + r, cy + r, hue, (x, y) => k * Math.max(0, 1 - Math.hypot(x - cx, y - cy) / r) ** 1.6); }
  // 별 섬광: 바늘 여러 개 + 흰 심. sy로 위아래를 눌러 바닥에 붙은 섬광을 만든다.
  star(cx, cy, len, width, rays, hue, k = 1, sy = 1, rot = 0) {
    rays.forEach(([deg, rel], i) => { const a = rad(deg + rot); this.needle(cx, cy, cx + Math.cos(a) * len * rel, cy + Math.sin(a) * len * rel * sy, width * (rel > 0.8 ? 1 : 0.7), i % 3 === 2 && this.names.length > 1 ? this.names[(this.names.indexOf(hue) + 1) % this.names.length] : hue, k); });
    this.glow(cx, cy, width * 2.2, hue, k * 1.1);
  }
  // scale배로 키운 RGBA 프레임
  toFrame() {
    const s = this.s, W = this.w * s, H = this.h * s, data = new Uint8Array(W * H * 4);
    for (let y = 0; y < this.h; y++) for (let x = 0; x < this.w; x++) {
      const v = this.i[y * this.w + x], st = STEPS.find(([min]) => v >= min); if (!st) continue;
      const col = this.ramps[this.names[this.c[y * this.w + x]]][st[1]], a = Math.round(st[2] * 255);
      for (let dy = 0; dy < s; dy++) for (let dx = 0; dx < s; dx++) data.set([...col, a], ((y * s + dy) * W + x * s + dx) * 4);
    }
    return { w: W, h: H, data };
  }
}

