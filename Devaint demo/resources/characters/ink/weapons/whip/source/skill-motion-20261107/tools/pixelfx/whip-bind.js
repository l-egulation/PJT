import { Rng, fit, clamp, smooth, ease, noise2, TAU } from "../../core/index.js";

// 채찍 속박 전용 감김 표시 시안 — 파츠 속박(보라 밧줄 bind-front/back)과 따로, 채찍 스킬로 묶였을 때만 쓴다.
// 채찍과 같은 먹색 가는 줄이 몸통을 감는다. 기존 밧줄 시트처럼 앞 반(몸 앞에 그림)·뒤 반(몸 뒤에 그림)으로 나눠 굽는다.
// 시간: 0~0.1초 감기며 조인다(밖에서 안으로, 넘침 없음) → 묶인 동안 그대로(먹 윤곽만 살짝 끓음) → 마지막 0.16초 풀리며 조각나 사라진다.
// 원점 = 몸 가운데. dir=1 이면 플레이어(채찍 손잡이)는 왼쪽.
//   w1 「채찍 세 바퀴」: 가는 먹 줄 세 바퀴 + 플레이어 쪽으로 늘어진 짧은 끝.
//   w2 「먹 매듭」: 두 바퀴 + 몸 앞의 작은 매듭과 짧은 두 끝.
//   w3 「가시 채찍」: w1 에 작은 가시가 돋은 줄(가시 그물 속박 후보).
export const meta = {
  name: "Whip bind",
  desc: "Whip-only root: a thin ink whip coiled round the body (front/back halves), cinches in, holds, then unwinds.",
  tags: ["ink", "whip", "status", "root"],
  size: [128, 96],
  origin: [64, 48],
  duration: 1.2,
  loop: false,
  fps: 25,
  bg: "light",
  options: { variant: "w1", part: "front", hold: 1.2, size: "small", dir: 1, seed: 5 },
};

const [INK, INK2] = fit(["#1b1616", "#3a3230"], 2), IN = 0.1, OUT = 0.16;

export function create(o) {
  const V = o.variant ?? "w1", FRONT = (o.part ?? "front") === "front", dir = o.dir < 0 ? -1 : 1, R = new Rng(o.seed);
  // 크기: 기존 밧줄 시트처럼 몸 폭 2유닛 미만 small, 이상 large(줄 간격·굵기도 키운다)
  const K = (o.size ?? "small") === "large" ? 1.65 : 1, RX = 21 * K, HOLD = o.hold ?? 1.2;
  const turns = V === "w2"
    ? [{ y: -5 * K, ry: 7 * K, tilt: 0.12 }, { y: 5 * K, ry: 7 * K, tilt: -0.08 }]
    : [{ y: -9 * K, ry: 7 * K, tilt: 0.1 }, { y: 0, ry: 7.5 * K, tilt: -0.06 }, { y: 9 * K, ry: 7 * K, tilt: 0.08 }];
  const W = K > 1 ? 1.5 : 1.1;                                   // 줄 반두께(px) → 2~3px 줄
  const breaks = Array.from({ length: 10 }, () => R.range(0, TAU));
  let t = 0;
  return {
    update(dt) { t += dt; },
    draw(p) {
      const f = Math.floor(t * 25 + 1e-6);
      const kin = ease.inCubic(clamp(t / IN));                 // 감기며 조임
      const kout = clamp((t - (HOLD - OUT)) / OUT);           // 풀림
      if (t >= HOLD) return;
      const grow = 1 + 0.3 * (1 - kin) + 0.2 * ease.outCubic(kout);
      const fade = kout;
      p.shade(-64, -48, 64, 48, (x, y) => {
        const wx = x + 0.5 * noise2(x * 0.3, y * 0.3 + f * 0.5, o.seed), wy = y + 0.5 * noise2(x * 0.3 + 7, y * 0.3 - f * 0.5, o.seed + 1);
        let hit = 0;
        for (let i = 0; i < turns.length; i++) {
          // loosen(휘감기 시안): 남은 시간이 보이게 위 바퀴는 묶인 시간 45%, 아래 바퀴는 75% 에 끊겨 사라진다(가운데는 끝까지)
          if (o.loosen && turns.length === 3 && i !== 1) {
            const bt = (i === 0 ? 0.45 : 0.75) * HOLD;
            if (t > bt + 0.08) continue;
            if (t > bt && noise2(wx * 0.5, wy * 0.5 + f, o.seed + 7) < ((t - bt) / 0.08) * 2 - 1) continue;
          }
          const c = turns[i], rx = RX * grow, ry = c.ry * grow, yy = wy - c.y - c.tilt * wx;
          const q = Math.hypot(wx / rx, yy / ry);
          const gx = wx / (rx * rx), gy = yy / (ry * ry), gq = Math.hypot(gx, gy) / Math.max(q, 1e-3);
          const d = Math.abs(q - 1) / Math.max(gq, 1e-3);   // 타원 둘레까지 거리(1차 근사)
          if (d > W + (V === "w3" ? 2.2 * K : 0)) continue;
          // 앞 반 = 아래쪽 호(위에서 비스듬히 내려다본 고리), 뒤 반 = 위쪽 호
          const front = yy > 0;
          if (front !== FRONT) continue;
          const th = Math.atan2(yy / ry, wx / rx);
          // 감기는 동안은 한쪽 끝부터 둘러지는 만큼만 보인다
          const swept = ((th + Math.PI + i * 0.9) % TAU) / TAU;
          if (swept > 0.25 + 0.75 * kin + 0.001 && kin < 1) continue;
          // 풀릴 때는 줄이 조각나며 사라진다
          if (fade > 0 && noise2(th * 2.4, i * 3 + f * 0.2, o.seed + 3) < fade * 2.2 - 1.0) continue;
          if (d <= W) { hit = Math.max(hit, 2); continue; }
          // 가시(w3): 바깥쪽으로 짧게 돋은 삼각 가시, 일정 간격
          const sp = (th * 7 + i) % 1, on = Math.abs(((th * 9 + i * 0.5) / TAU * 9) % 1 - 0.5) < 0.12;
          if (V === "w3" && on && q > 1 && d <= W + 2.2 * K) hit = Math.max(hit, 1);
        }
        // 끝: w1·w3 은 플레이어 쪽으로 늘어진 짧은 끝, w2 는 몸 앞의 매듭과 두 끝
        if (FRONT && kin > 0.6 && fade < 0.6) {
          if (V === "w2") {
            const kx = -dir * 3 * K, ky = 4 * K;
            if (Math.hypot(wx - kx, (wy - ky) * 1.2) < 3.6 * K) hit = 2;
            for (const s of [-1, 1]) {           // 매듭에서 아래로 늘어진 짧은 두 끝
              const ex = kx + s * 2 + s * 0.35 * (wy - ky), along = wy - ky;
              if (along > 0 && along < 9 * K && Math.abs(wx - ex) < W) hit = Math.max(hit, 2);
            }
          } else {
            const sx = -dir * RX * grow * 0.92, along = (wx - sx) * -dir;   // 고리 옆에서 플레이어 쪽으로
            if (along > 0 && along < 10 * K) {
              const cy = 3 * K + along * 0.55 + 0.05 * along * along / K;
              if (Math.abs(wy - cy) < W) hit = Math.max(hit, 2);
            }
          }
        }
        if (!hit) return;
        return hit === 2 ? INK : INK2;
      });
    },
  };
}
