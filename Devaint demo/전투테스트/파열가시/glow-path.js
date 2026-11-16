import { clamp, mix, WHITE } from "../../core/index.js";

// 빛나는 꺾은선 하나: 화소마다 선까지의 가장 가까운 거리를 재서 흰 심 → 색 → 반투명 번짐으로 칠한다.
// 선분을 따로 그리면 이음매가 두 번 칠해져 진해지므로 경로 전체를 한 번에 잰다.
// w 는 반폭(수 또는 0..1 위치를 받는 함수), glow 는 번짐 폭, alpha 는 전체 세기.
export function glowPath(p, pts, w, glow, alpha, C) {
  if (pts.length < 2 || alpha <= 0.01) return;
  const [, , SHADE, BODY, LIT] = C, wf = typeof w === "function" ? w : () => w;
  let len = 0;
  const seg = [];
  for (let i = 1; i < pts.length; i++) {
    const [ax, ay] = pts[i - 1], [bx, by] = pts[i], L = Math.hypot(bx - ax, by - ay);
    seg.push({ ax, ay, dx: bx - ax, dy: by - ay, L, s: len });
    len += L;
  }
  const W = Math.max(...pts.map((_, i) => wf(i / (pts.length - 1)))) + glow + 2;
  const xs = pts.map((q) => q[0]), ys = pts.map((q) => q[1]);
  for (let y = Math.floor(Math.min(...ys) - W); y <= Math.max(...ys) + W; y++)
    for (let x = Math.floor(Math.min(...xs) - W); x <= Math.max(...xs) + W; x++) {
      let best = 1e9, at = 0;
      for (const g of seg) {
        const px = x + 0.5 - g.ax, py = y + 0.5 - g.ay, k = g.L ? clamp((px * g.dx + py * g.dy) / (g.L * g.L)) : 0;
        const d = Math.hypot(px - g.dx * k, py - g.dy * k);
        if (d < best) (best = d, at = (g.s + k * g.L) / (len || 1));
      }
      const hw = wf(at);
      if (hw > 0.05 && best <= hw) {
        const i = 1 - best / hw;
        p.blend(x, y, i > 0.6 ? mix(LIT, WHITE, (i - 0.6) * 2.5) : mix(SHADE, BODY, Math.min(1, i * 2.2)), alpha * clamp(0.7 + i * 2));
      } else if (best <= hw + glow) {
        const g = 1 - (best - hw) / glow;
        p.blend(x, y, mix(SHADE, BODY, g), alpha * 0.55 * g * g);
      }
    }
}
