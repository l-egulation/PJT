import { Rng, fit, clamp, smooth, ease, noise2, mix, TAU } from "../../core/index.js";
import { drawDrop } from "./mark-drop-idle.js";

// 표식 격발 G — 원·막대를 하나도 쓰지 않는다. 화면 전체를 하나의 잉크 '농도장'으로 보고
// 움직이는 덩어리(메타볼)를 더한 뒤, 잡음으로 좌표를 비틀어(domain warp) 문턱 위만 칠한다.
// 덩어리가 서로 붙었다 떨어지며 실루엣의 뼈대가 장마다 바뀌고, 잡음이 장마다 흘러 윤곽이 일렁인다.
// 피격 멈춤에 걸리는 장은 작고 날카로운 불꽃 하나다(크게 핀 모양이 멈추면 박제처럼 보인다).
export const meta = {
  name: "Mark burst (ink field)",
  desc: "A warped metaball ink field: a small sharp spark holds through the hit-stop, then liquid blobs burst, neck, split, get eaten by holes and splat; a ragged ring tears open.",
  tags: ["ink", "mark", "burst", "splash", "one-shot"],
  size: [200, 170],
  origin: [100, 108],
  duration: 1.0,
  loop: false,
  fps: 25,
  bg: "light",
  options: { palette: ["#171716", "#3a1d52", "#64348c", "#9a5cc8", "#cfa2ec", "#fbf2ff"], marks: 5, dir: 1, rim: true, seed: 11, mix: [], mixMode: "blob" },
};

const FLOOR = 28, STEP = 1 / 25, BOX = 70;
export const HIT = 0.2;
const SPARK = 0.04, BURST = 0.08; // 멈춤에 걸리는 장(u≈0.04) → 터짐 시작

// 길쭉한 메타볼 하나의 농도: 속도 방향으로 s 배 늘어난 타원, 가장자리에서 0
function ball(x, y, b) {
  const dx = x - b.x, dy = y - b.y, al = dx * b.ux + dy * b.uy, pe = -dx * b.uy + dy * b.ux;
  const q = (al * al) / (b.r * b.r * b.s * b.s) + (pe * pe) / (b.r * b.r);
  return q < 1 ? (1 - q) * (1 - q) : 0;
}

// 여러 색 섞기(mix: 팔레트 둘 이상): 「표식을 터트릴 시」 파츠를 여러 색 끼웠을 때 한 격발에 색을 나눠 칠한다.
//   blob   덩어리마다 색을 하나씩 돌려 준다. 화소는 가장 진하게 더해 준 덩어리의 색을 따른다
//   sector 가운데를 축으로 부채꼴로 나눈다. 경계는 잡음으로 일렁인다
//   blend  화소마다 덩어리 농도 비율로 색을 섞는다
export function create(o) {
  const Cs = (o.mix && o.mix.length > 1 ? o.mix : [o.palette]).map((c) => fit(c, 6)), NC = Cs.length;
  const C = Cs[0], R = new Rng(o.seed), M = clamp(Math.round(o.marks), 1, 5), dir = o.dir < 0 ? -1 : 1;
  const mode = o.mixMode || "blob";
  // 머리 위 표식 방울(mark-drop-*)과 같은 자리에서 출발해야 쌓임 → 격발이 이어진다
  const marks = Array.from({ length: M }, (_, i) => ({ x: (i - (M - 1) / 2) * 8, y: -42, d: i * 0.025 }));
  // 멈춤 장의 불꽃: 비대칭 바늘 서너 개
  const needles = Array.from({ length: 4 }, (_, i) => {
    const a = (i / 4) * TAU + R.range(-0.5, 0.5) + 0.4;
    return { a, L: R.range(6, 12) * (1 + 0.4 * Math.cos(a) * dir) };
  });
  const blobs = [];
  let t = 0, born = false;

  function burst() {
    born = true;
    blobs.push({ x: 0, y: 0, vx: dir * 10, vy: 0, r: 9, r0: 9, life: 0.32, age: 0, core: true, c: 0 });
    for (let i = 0, N = 9 + M; i < N; i++) {
      const a = R.range(0, TAU), sp = R.range(110, 260) * (1 + 0.35 * Math.cos(a) * dir), r = R.range(3.5, 7.5);
      blobs.push({ x: Math.cos(a) * 3, y: Math.sin(a) * 3, vx: Math.cos(a) * sp, vy: Math.sin(a) * sp * 0.8 - 20, r, r0: r, life: R.range(0.65, 0.95), age: 0, delay: R.range(0, 0.05), c: (i + 1) % NC });
    }
    // 맞은 쪽으로 멀리 튀는 잔방울: 터진 뒤에도 화면에 오래 남아 여운을 만든다
    for (let i = 0; i < 8 + M; i++) {
      const a = R.range(-1.3, 1.3) + (dir < 0 ? Math.PI : 0), sp = R.range(180, 290), r = R.range(2, 3.2);
      blobs.push({ x: 0, y: 0, vx: Math.cos(a) * sp, vy: Math.sin(a) * sp - 60, r, r0: r, life: R.range(0.55, 0.8), age: 0, delay: R.range(0.02, 0.09), c: i % NC });
    }
  }

  return {
    update(dt) {
      t += dt;
      const u = t - HIT;
      if (!born && u >= BURST) burst();
      for (const b of blobs) {
        if ((b.delay -= dt) > 0) continue;
        b.age += dt;
        const g = b.age > 0.08 ? 900 : 0;
        b.vx *= 1 - (b.core ? 6 : 1.4) * dt;
        b.vy = b.vy * (1 - 2.4 * dt) + g * dt;
        b.x += b.vx * dt;
        b.y += b.vy * dt;
        if (b.y + b.r * 0.3 > FLOOR && !b.core) (b.y = FLOOR - b.r * 0.3, b.vy = 0, b.vx *= 0.5, b.land = b.land ?? b.age);
        const k = b.age / b.life;
        b.r = b.r0 * (b.core ? 1 - ease.inQuad(clamp(k)) * 0.9 : 1 - smooth(0.35, 1, k));
      }
    },
    draw(p) {
      const f = Math.floor(t / STEP + 1e-6), u = t - HIT + 1e-6;
      // 0) 머리 위 방울이 몸통으로 빨려 든다 (농도장으로 그린다)
      const pre = t < HIT + 0.01;
      const items = [];
      // 머리 위 표식 방울(mark-drop-idle)이 그대로 몸통으로 떨어지며 아래로 길게 늘어난다
      if (pre)
        marks.forEach((m, i) => {
          const k = ease.inCubic(clamp((t - m.d) / (HIT - m.d)));
          if (k >= 0.98) return;
          drawDrop(p, { x: m.x * (1 - k), y: m.y * (1 - k), r: 3.8 * (1 - 0.35 * k), sx: 1 - 0.3 * k, sy: 1 + 1.3 * k, lean: -m.x * 0.08 * k }, Cs[i % NC], o.seed, o.rim);
        });
      // 1) 멈춤 장: 작고 날카로운 불꽃
      const spark = u >= 0 && u < BURST;
      if (spark) {
        items.push({ x: 0, y: 0, r: u < SPARK ? 5 : 6.5, s: 1, ux: 1, uy: 0, c: 0 });
        needles.forEach((n, i) => {
          const L = n.L * (u < SPARK ? 0.6 : 1);
          items.push({ x: Math.cos(n.a) * L * 0.55, y: Math.sin(n.a) * L * 0.55, r: 2.4, s: L / 3.6, ux: Math.cos(n.a), uy: Math.sin(n.a), c: i % NC });
        });
      }
      // 2) 터진 덩어리: 속도만큼 늘어나고, 서로 붙었다 끊어진다
      for (const b of blobs) {
        if (b.delay > 0 || b.r < 0.6) continue;
        const v = Math.hypot(b.vx, b.vy), landed = b.land !== undefined;
        items.push({ x: b.x, y: b.y, r: b.r, s: landed ? 1.8 : 1 + Math.min(2.2, v / 90), ux: landed ? 1 : v ? b.vx / v : 1, uy: landed ? 0 : v ? b.vy / v : 0, c: b.c });
      }
      const ringOn = u >= BURST && u < BURST + 0.26, rk = (u - BURST) / 0.26;
      if (!items.length && !ringOn) return;

      const warp = u < BURST ? 0.8 : 2.4, eat = smooth(BURST + 0.12, BURST + 0.55, u);
      const ringR = 12 + 26 * ease.outCubic(clamp(rk)), ringW = 3.2 * (1 - rk) + 0.9;
      p.shade(-BOX, -BOX, BOX, BOX, (x, y) => {
        // 좌표 비틀기: 장마다 잡음이 흘러 윤곽이 끓는다
        const wx = x + warp * noise2(x * 0.16, y * 0.16 + f * 0.45, o.seed);
        const wy = y + warp * noise2(x * 0.16 + 9, y * 0.16 - f * 0.4, o.seed + 1);
        let F = 0;
        const S = NC > 1 ? new Array(NC).fill(0) : null;
        for (const b of items) {
          const v = ball(wx, wy, b);
          F += v;
          if (S) S[b.c] += v;
        }
        let ring = 0;
        if (ringOn) {
          const d = Math.hypot(wx, wy);
          ring = Math.exp(-(((d - ringR) / ringW) ** 2)) * 0.9;
          if (noise2(Math.atan2(wy, wx) * 2.2, 3, o.seed + 2) < rk * 2.4 - 1.1) ring = 0; // 끝무렵 고리가 끊긴다
        }
        const T = F + ring;
        if (T < 0.32) return;
        // 구멍: 진할수록(가운데일수록) 먼저 뚫린다
        // 날아간 덩어리까지 갉으면 속 빈 테두리만 남는다. 가운데 덩어리 근처만 갉는다
        if (eat > 0 && F > 0.55 && x * x + y * y < 22 * 22 && noise2(x * 0.3 + f * 0.25, y * 0.3, o.seed + 3) < eat * 2.2 - 1.6 + Math.min(1.2, F) * eat * 1.1) return;
        // 명암: 오른쪽 아래 가장자리는 어둡게, 진한 속은 밝게. 멈춤 장은 가장 밝게
        const below = F - (ball(wx - 1.5, wy - 1.5, items[0] ?? { x: 1e9, y: 1e9, r: 1, s: 1, ux: 1, uy: 0 }));
        // 칠할 명암 단계: 2 그림자 · 3 몸 · 4 밝음 · 5 번쩍임
        const role = spark ? (T > 0.7 ? 5 : 4)
          : T < 0.45 ? (o.rim ? 2 : 3) // 색 파츠 판은 가장자리 테 없이
          : T > 1.1 && u < BURST + 0.12 ? 5 : T > 1.25 ? 4 : below > 0.15 ? 2 : 3;
        if (!S) return C[role];
        // 고리와 부채꼴은 각도로 색을 정한다
        const sector = () => {
          const a = Math.atan2(wy, wx) / TAU + 0.5 + 0.06 * noise2(Math.hypot(wx, wy) * 0.08, f * 0.2, o.seed + 4);
          return Math.floor(((a % 1) + 1) % 1 * NC) % NC;
        };
        if (mode === "sector" || F < ring) return Cs[sector()][role];
        if (mode === "blend") {
          let c = Cs[0][role], acc = S[0];
          for (let i = 1; i < NC; i++) if (S[i] > 0) (c = mix(c, Cs[i][role], S[i] / (acc + S[i])), acc += S[i]);
          return c;
        }
        let best = 0;
        for (let i = 1; i < NC; i++) if (S[i] > S[best]) best = i;
        return Cs[best][role];
      });
    },
  };
}
