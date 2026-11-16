import { fit, clamp, smooth, ease, mix, WHITE } from "../../core/index.js";
import { glowPath } from "./glow-path.js";

// 끌어당김(보라) — 빛 줄 하나. 손에서 몬스터로 날아가 착 달라붙고, 출렁이며 몬스터를 당겨 온 뒤 떨어져 손으로 감겨 들어간다.
// 줄은 마디를 이은 물리(버렛 적분 + 길이 제약)로 움직인다. 붙는 순간 관성으로 찰랑이다가 길이가 줄며 일자로 팽팽해지고,
// 그 일자 상태 그대로 몬스터를 당긴다.
// 원점은 당기는 쪽(플레이어) 발 가운데, 손은 (0, hand). 몬스터는 오른쪽 dist px 에 있고 pull px 만큼 끌려온다.
// 몬스터를 실제로 옮기는 건 게임이 한다: PULL0~PULL1 동안 ease.outCubic 으로 pull px.
export const meta = {
  name: "Pull rope (ink)",
  desc: "A glowing rope whips from the hand to the target, sticks with a splash, jiggles as it hauls the target in, then lets go and reels back.",
  tags: ["ink", "pull", "cc", "rope", "one-shot"],
  size: [112, 72],
  origin: [12, 60],
  duration: 0.8,
  loop: false,
  fps: 25,
  bg: "light",
  options: { palette: ["#171716", "#3a1d52", "#64348c", "#9a5cc8", "#cfa2ec", "#fbf2ff"], dist: 72, pull: 32, hand: -30, chest: -26, seed: 1 },
};

// 날아감 → 붙음(출렁) → 일자로 팽팽해짐(TAUT) → 일자 그대로 당김 → 놓음
export const SHOOT = 0.1, TAUT = 0.2, PULL0 = 0.26, PULL1 = 0.46, RELEASE = 0.52;
const N = 18;

export function create(o) {
  const C = fit(o.palette, 6), [, , , BODY, LIT] = C;
  const H = [0, o.hand], rest0 = (o.dist * 1.08) / (N - 1);
  // 마디: 처음엔 모두 손에 겹쳐 있다
  const P = Array.from({ length: N }, () => ({ x: H[0], y: H[1], px: H[0], py: H[1] }));
  let t = 0, stuck = false, hitT = -1;
  const target = (time) => {
    const k = ease.outCubic(clamp((time - PULL0) / (PULL1 - PULL0)));
    return [o.dist - o.pull * k, o.chest];
  };

  let snap = null; // 놓는 순간의 줄 끝
  function step(dt) {
    t += dt;
    // 놓은 뒤에는 물리 대신 줄이 일자 그대로 손으로 감겨 들어간다(길이 제약으로 줄이면 지그재그로 접힌다)
    if (t >= RELEASE) {
      if (!snap) snap = [P[N - 1].x, P[N - 1].y];
      const k = ease.inQuad(clamp((t - RELEASE) / 0.16));
      for (let i = 0; i < N; i++) {
        const v = (i / (N - 1)) * (1 - k);
        P[i].x = H[0] + (snap[0] - H[0]) * v;
        P[i].y = H[1] + (snap[1] - H[1]) * v + 2 * Math.sin(Math.PI * v / Math.max(0.01, 1 - k)) * k;
      }
      return;
    }
    // 팽팽해질수록 중력·출렁임이 줄어든다
    const tight = smooth(SHOOT, TAUT, t) * (t < RELEASE ? 1 : 0);
    const g = 520 * (1 - 0.9 * tight), damp = 0.985 - 0.08 * tight;
    // 줄 길이: 붙은 뒤 몬스터까지 거리보다 살짝 짧게 줄어 일자로 펴지고, 당기는 동안 그 길이를 따라가며, 놓은 뒤 빠르게 감긴다
    const [tx, ty] = target(t), span = Math.hypot(tx - H[0], ty - H[1]);
    let rest = rest0;
    if (t > SHOOT) rest = rest0 + ((span * 0.985) / (N - 1) - rest0) * tight;
    if (t > RELEASE) rest *= 1 - ease.inQuad(clamp((t - RELEASE) / 0.18));
    for (const q of P.slice(1)) {
      const vx = (q.x - q.px) * damp, vy = (q.y - q.py) * damp;
      q.px = q.x; q.py = q.y;
      q.x += vx; q.y += vy + g * dt * dt;
    }
    // 끝 마디: 날아갈 땐 살짝 위로 휜 궤적을 따라 끌려가고, 붙은 뒤엔 몬스터 가슴에 고정된다
    const tip = P[N - 1];
    if (t < SHOOT) {
      // 날아가는 동안은 줄이 손에서 풀려 나가며 끝을 따라 펼쳐진다(마디가 손에 뭉쳐 있으면 꼬인다)
      const u = ease.outCubic(t / SHOOT);
      tip.x = H[0] + (tx - H[0]) * u; tip.y = H[1] + (ty - H[1]) * u - 10 * Math.sin(Math.PI * u);
      for (let i = 1; i < N - 1; i++) {
        const v = i / (N - 1), q = P[i], nx = H[0] + (tip.x - H[0]) * v, ny = H[1] + (tip.y - H[1]) * v + 4 * Math.sin(Math.PI * v) * (1 - u);
        q.px = q.x; q.py = q.y; q.x = nx; q.y = ny;
      }
    } else if (t < RELEASE) {
      if (!stuck) (stuck = true, hitT = t);
      tip.x = tx; tip.y = ty;
    }
    P[0].x = H[0]; P[0].y = H[1];
    if (t < SHOOT) return; // 펼쳐지는 동안은 길이 제약을 걸지 않는다: 남는 길이가 지그재그로 접힌다
    for (let it = 0; it < 14; it++) {
      for (let i = 1; i < N; i++) {
        const a = P[i - 1], b = P[i], dx = b.x - a.x, dy = b.y - a.y, d = Math.hypot(dx, dy) || 1e-6, diff = (d - rest) / d;
        const wa = i - 1 === 0 ? 0 : 0.5, wb = i === N - 1 && t < RELEASE ? 0 : 0.5, w = wa + wb || 1;
        a.x += dx * diff * (wa / w); a.y += dy * diff * (wa / w);
        b.x -= dx * diff * (wb / w); b.y -= dy * diff * (wb / w);
      }
      P[0].x = H[0]; P[0].y = H[1];
      if (t < RELEASE && t >= SHOOT) (tip.x = tx, tip.y = ty);
    }
  }

  return {
    update(dt) {
      // 줄 물리는 잘게 나눠 돌린다
      const n = Math.ceil(dt / (1 / 240));
      for (let i = 0; i < n; i++) step(dt / n);
    },
    draw(p) {
      if (t >= meta.duration - 1e-4) return;
      const fade = 1 - smooth(RELEASE + 0.1, RELEASE + 0.22, t);
      glowPath(p, P.map((q) => [q.x, q.y]), (v) => 1.25 + 0.35 * Math.sin(Math.PI * v), 2.5, fade, C); // 너무 가늘면 흰 심이 점선처럼 끊긴다
      // 붙는 순간 끝에 잉크가 탁 번진다
      if (hitT >= 0) {
        const u = (t - hitT) / 0.16;
        if (u < 1) {
          const [tx, ty] = target(t), r = 3 + 5 * ease.outCubic(u);
          for (let y = Math.floor(ty - r - 1); y <= ty + r + 1; y++)
            for (let x = Math.floor(tx - r - 1); x <= tx + r + 1; x++) {
              const d = Math.hypot(x + 0.5 - tx, y + 0.5 - ty) / r;
              if (d < 1) p.blend(x, y, d < 0.4 ? mix(LIT, WHITE, 0.5) : BODY, (1 - u) * (1 - d * d) * 0.8);
            }
        }
      }
    },
  };
}
