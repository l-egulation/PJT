import { fit, noise2, TAU } from "../../core/index.js";

// 표식 방울 — 머리 위에 떠 있는 동안. 격발(mark-burst)과 같은 잉크 농도장 방식으로,
// 둥근 몸 위에 작은 덩어리 둘을 이어 위로 뾰족한 물방울을 만들고, 그 덩어리들이 흔들려 일렁인다.
// 방울 하나만 그린다. 게임이 1~5개를 표식 자리(SLOT)에 놓는다.
export const meta = {
  name: "Mark drop (idle)",
  desc: "One ink mark drop floating over the head: a teardrop that bobs and wobbles, its outline gently boiling. Loops.",
  tags: ["ink", "mark", "status", "loop"],
  size: [24, 28],
  origin: [12, 16],
  duration: 1.2,
  loop: true,
  fps: 25,
  bg: "light",
  options: { palette: ["#1a1a1a", "#1a1a1a", "#1a1a1a", "#fbf6ec", "#ffffff", "#ffffff"], rim: true, seed: 3 },
};

// 표식 자리: 몸통 가운데(격발 이펙트의 원점)에서 i 번째 방울까지. n 은 쌓인 개수
export const SLOT = { dx: 8, y: -42 };
export const slotX = (i, n) => (i - (n - 1) / 2) * SLOT.dx;

const R = 3.8;

// 방울 하나. s = { x, y, r, sx(가로 배율), sy(세로 배율), lean(꼭지 기울기), sway(꼭지 흔들림), boil(잡음 흐름) }
// 몇 화소짜리라 농도 문턱으로 테두리를 고르면 점으로 끊긴다. 먼저 칸 단위 실루엣을 만들고,
// 바깥과 맞닿은 칸만 먹으로 칠해 테두리를 한 줄로 잇는다. rim 이 거짓이면(색 파츠 판) 테두리 없이 몸 색으로 채운다.
export function drawDrop(p, s, C, seed = 0, rim = true) {
  const r = s.r ?? R;
  if (r < 0.5) return;
  const sx = s.sx ?? 1, sy = s.sy ?? 1, lean = s.lean ?? 0, sway = s.sway ?? 0, boil = s.boil ?? 0;
  const balls = [
    { x: 0, y: 0, r },
    { x: lean * 0.5 + sway * 0.5, y: -r * 0.85, r: r * 0.62 },
    { x: lean + sway, y: -r * 1.45, r: r * 0.4 }, // 꼭지가 떨어져 보이지 않게 가깝게
  ];
  const cx = Math.round(s.x), cy = Math.round(s.y), B = Math.ceil(r * 2.4) + 2, W = B * 2 + 1;
  const inside = new Uint8Array(W * W);
  for (let j = 0; j < W; j++)
    for (let i = 0; i < W; i++) {
      const lx = (i - B + 0.5 + (cx - s.x)) / sx, ly = (j - B + 0.5 + (cy - s.y)) / sy;
      const wx = lx + 0.15 * noise2(lx * 0.5, ly * 0.5 + boil, seed), wy = ly + 0.15 * noise2(lx * 0.5 + 7, ly * 0.5 - boil, seed + 1);
      let F = 0;
      for (const b of balls) {
        const q = ((wx - b.x) ** 2 + (wy - b.y) ** 2) / (b.r * b.r);
        if (q < 1) F += (1 - q) ** 2;
      }
      inside[j * W + i] = F >= 0.3 ? 1 : 0;
    }
  const at = (i, j) => (i < 0 || j < 0 || i >= W || j >= W ? 0 : inside[j * W + i]);
  for (let j = 0; j < W; j++)
    for (let i = 0; i < W; i++) {
      if (!at(i, j)) continue;
      const edge = !at(i - 1, j) || !at(i + 1, j) || !at(i, j - 1) || !at(i, j + 1);
      const lx = i - B, ly = j - B;
      const c = edge ? (rim ? C[2] : C[3]) : lx <= -1 && lx >= -2 && ly <= -1 && ly >= -2 ? C[4] : C[3]; // 왼쪽 위 밝은 점
      p.px(cx + lx, cy + ly, c);
    }
}

export function draw(p, t, o) {
  const C = fit(o.palette, 6), ph = (t / meta.duration) * TAU;
  drawDrop(p, {
    x: 0,
    y: Math.round(Math.sin(ph) * 1.2),
    // 가로세로 눌림은 몇 화소짜리 방울을 병처럼 홀쭉하게 만들어 뺐다. 출렁임은 꼭지 흔들림과 위아래 들썩임으로만
    sway: 0.55 * Math.sin(ph + 1),
    boil: Math.sin(ph) * 0.6,
  }, C, o.seed, o.rim);
}
