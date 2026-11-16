import { Rng, fit, clamp, ease } from "../../core/index.js";

// 먹안개 시안(2026-11-07). 연막 = 폭 6·높이 3 유닛, 발밑부터, 4초(melee.md). PPU 32 → 192x96 화소 상자.
//   variant="a" 뭉게 먹구름: 몸에서 먹 덩이 열여섯이 터져 나가 상자를 채우고, 3초 동안 천천히 일렁이다 줄어들며 걷힌다.
//   variant="b" 먹 띠 장막: 높이가 다른 먹 띠 셋이 몸에서 좌우로 쭉 펼쳐져 벽을 만들고, 윗 가장자리가 물결치며 흐르다 걷힌다.
// 반투명은 고른 알파 겹(0.5)로만 — 점묘 없음. 원점 = 시전 자리 발밑 가운데, 시각 0 = 연막 생성(시전 0.15초).
export const meta = {
  name: "Dagger ink smoke (ink)",
  desc: "A 6x3 unit smoke screen of ink: puffs burst out to fill the box, churn for three seconds, then thin away.",
  tags: ["ink", "dagger", "smoke", "area"],
  size: [224, 120],
  origin: [112, 112],
  duration: 4.3,
  loop: false,
  fps: 25,
  bg: "light",
  options: { variant: "a", seed: 9, life: 4 },   // 연막 4초(2026-11-07 밸런스 조정, 3→4)
};
const PAL = ["#1b1616", "#3a3230", "#5a4e4a"];
const HW = 96;

// c·d 안개(2026-11-07 사용자 「안개 같은 느낌이 안 난다」): 모양을 그리지 않고 짙기 마당(값 잡음 fBm)을 흘려서
// 짙은 곳·옅은 곳이 계속 흐르고, 가장자리는 잡음으로 해어진다(테두리 띠 없음). 짙기는 알파 두 단계로만 끊는다.
//   variant="c" 흐르는 안개: 바닥에 깔려 옆으로 흐르며 속이 소용돌이친다. 윗면이 뭉게뭉게 오르내린다.
//   variant="d" 피어오르는 안개: c 에 더해 짙기가 위로 피어오르고, 윗면에서 실안개 가닥이 떨어져 나가 흩어진다.
function hash2(s, x, y) { let h = (x * 374761393 + y * 668265263 + s * 2147483647) | 0; h = Math.imul(h ^ (h >>> 13), 1274126177); return ((h ^ (h >>> 16)) >>> 0) / 4294967296; }
function vnoise(s, x, y) {
  const ix = Math.floor(x), iy = Math.floor(y), fx = x - ix, fy = y - iy, u = fx * fx * (3 - 2 * fx), v = fy * fy * (3 - 2 * fy);
  const a = hash2(s, ix, iy), b = hash2(s, ix + 1, iy), c = hash2(s, ix, iy + 1), d = hash2(s, ix + 1, iy + 1);
  return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
}
function fbm(s, x, y) { return 0.7 * vnoise(s, x, y) + 0.3 * vnoise(s + 7, x * 1.9, y * 1.9); }   // 굵은 두 겹만(잘게 쪼개진 겹은 자글자글해서 뺐다)
// 2차(같은 날 「왜 이렇게 지저분해 보이지」): 큰 덩어리만 천천히 흐르게, 단계 경계는 5x5 다수결로 다듬어 작은 얼룩·구멍·부스러기를 지운다.
const FW = 224, FH = 112, LV = new Uint8Array(FW * FH), TMP = new Uint8Array(FW * FH);
function smoothLevel(k) {          // 짙기 k 이상인 칸을 5x5 다수결로 다듬는다(13/25 이상이면 남김)
  for (let y = 0; y < FH; y++) for (let x = 0; x < FW; x++) {
    let n = 0;
    for (let dy = -2; dy <= 2; dy++) { const yy = y + dy; if (yy < 0 || yy >= FH) { if (yy >= FH && k <= LV[y * FW + x]) n += 5; continue; } for (let dx = -2; dx <= 2; dx++) { const xx = x + dx; if (xx >= 0 && xx < FW && LV[yy * FW + xx] >= k) n++; } }
    TMP[y * FW + x] = n >= 13 ? 1 : 0;
  }
  return TMP.slice();
}
function fog(p, g, o, INK, end) {
  const rise = o.variant === "d", s = o.seed;
  const grow = ease.outCubic(clamp(g / 0.35));                       // 몸에서 굴러 나가며 퍼진다
  const thin = ease.inQuad(clamp((g - (end - 0.7)) / 0.9));          // 끝 0.7초 전부터 옅은 곳부터 걷힌다
  if (thin >= 1) return;
  for (let y = -FH; y < 0; y++) for (let x = -112; x < 112; x++) {
    const wx = x + 6 * Math.sin(y * 0.04 + g * 0.6) + (rise ? 0 : g * 7), wy = y + (rise ? g * 10 : 0) + 4 * Math.sin(x * 0.03 - g * 0.5);
    const n = fbm(s, wx * 0.02, wy * 0.032);
    const top = 92 + 10 * (vnoise(s + 3, x * 0.025 + g * 0.35, g * 0.2) - 0.5) * 2, side = 94 + 6 * (vnoise(s + 5, y * 0.03, g * 0.3) - 0.5) * 2;
    // 가장자리로 갈수록 문턱을 올려 짙은 덩이만 남긴다 → 고른 테 없이 해어진 끝
    const ev = clamp((top + y) / 34) * clamp((side - Math.abs(x)) / 40);
    const r = Math.hypot(x, (y + 24) * 1.6), es = clamp((30 + 130 * grow - r) / 28);
    const d = clamp((n - 0.3) / 0.34) - (1 - ev) * 1.1 - (1 - es) * 1.1 - thin * 0.9;
    LV[(y + FH) * FW + x + 112] = d > 0.62 ? 2 : d > 0.22 ? 1 : 0;
  }
  const L1 = smoothLevel(1), L2 = smoothLevel(2);
  for (let i = 0; i < FW * FH; i++) {
    if (!L1[i]) continue;
    const x = (i % FW) - 112, y = Math.floor(i / FW) - FH;
    p.blend(x, y, INK, L2[i] ? 0.6 : 0.36);
  }
}

export function create(o) {
  const [INK, INK2, INK3] = fit(PAL, 3), R = new Rng(o.seed);
  const puffs = [];
  for (let j = 0; j < 3; j++) for (let i = 0; i < 6; i++) {
    if (j === 2 && (i === 0 || i === 5)) continue;
    const r = R.range(19, 25);
    puffs.push({ tx: -HW + r + (i + R.range(0.1, 0.9)) * ((2 * HW - 2 * r) / 6), ty: -r - j * 26 - R.range(0, 6), r, d: R.range(0, 0.06), ph: R.range(0, 6.28) });
  }
  const end = o.life;
  let t = 0;
  return {
    update(dt) { t += dt; },
    draw(p) {
      const g = t + 1e-6;
      if (g > end + 0.3) return;
      if (o.variant === "c" || o.variant === "d") return fog(p, g, o, INK, end);
      const out = 1 - ease.inCubic(clamp((g - (end - 0.4)) / 0.7));
      // 덩이·띠를 겹마다 한 판(합집합)에 모은 뒤 한 번만 칠한다 — 겹친 동그라미 테가 비치지 않게
      const W2 = 112, Ht = 112, outer = new Uint8Array(W2 * 2 * Ht), inner = new Uint8Array(W2 * 2 * Ht);
      const mark = (m, x, y) => { x = Math.floor(x); y = Math.floor(y); if (x >= -W2 && x < W2 && y >= -Ht && y < 0) m[(y + Ht) * W2 * 2 + x + W2] = 1; };
      const discM = (m, cx, cy, r) => { for (let y = Math.floor(cy - r); y <= cy + r; y++) for (let x = Math.floor(cx - r); x <= cx + r; x++) if ((x + 0.5 - cx) ** 2 + (y + 0.5 - cy) ** 2 <= r * r) mark(m, x, y); };
      if (out <= 0.02) return;
      if (o.variant === "b") {
        const BANDS = [[-16, 30], [-46, 30], [-74, 26]];
        BANDS.forEach(([cy, h], b) => {
          const reach = HW * ease.outCubic(clamp((g - b * 0.03) / 0.22)) * (0.85 + 0.15 * out), hh = h * (0.5 + 0.5 * out);
          for (let x = Math.floor(-reach); x < reach; x++) {
            const edge = Math.min(1, (reach - Math.abs(x)) / 12), cut = (1 - Math.sqrt(edge)) * hh * 0.5;
            const top = cy - hh / 2 + 4 * Math.sin(x * 0.07 + g * 2.2 + b * 1.7) + cut, bot = cy + hh / 2 + 3 * Math.sin(x * 0.05 - g * 1.6 + b) - cut;
            for (let y = Math.floor(top); y < bot; y++) mark(outer, x, y);
            for (let y = Math.floor(top + 6); y < bot - 6; y++) if (b !== 1) mark(inner, x, y);
          }
        });
      } else {
        for (const f of puffs) {
          const u = ease.outCubic(clamp((g - f.d) / 0.24));
          if (u <= 0) continue;
          const wob = 3 * Math.sin(g * 1.4 + f.ph), x = f.tx * u + wob, y0 = -28 + (f.ty + 28) * u + 2 * Math.sin(g * 1.1 + f.ph * 2);
          const r = (4 + (f.r - 4) * u + 1.5 * Math.sin(g * 2 + f.ph)) * (0.55 + 0.45 * out);
          if (r < 1) continue;
          const y = Math.min(y0, -r * 0.6);
          discM(outer, x, y, r);
        }
      }
      // 진한 속 = 바깥 구름을 안쪽으로 6화소 줄인 한 덩어리(작은 동그라미가 비치지 않게)
      const at = (x, y) => x >= -W2 && x < W2 && y >= -Ht && y < 0 && outer[(y + Ht) * W2 * 2 + x + W2];
      const E = 6, DIRS = [[E, 0], [-E, 0], [0, -E], [0, E], [4, 4], [-4, 4], [4, -4], [-4, -4]];
      const a = Math.min(1, out * 1.5);
      for (let y = -Ht; y < 0; y++) for (let x = -W2; x < W2; x++) {
        if (!at(x, y)) continue;
        // b(확정 2026-11-07): 사용자 「테두리 같은 거 없는 게 좋겠다」 — 연한 겉 겹 없이 한 색 한 알파로만
        if (o.variant === "b") { p.blend(x, y, INK, 0.62 * a); continue; }
        p.blend(x, y, INK2, 0.5 * a);
        if (DIRS.every(([dx, dy]) => at(x + dx, Math.min(-1, y + dy)))) p.blend(x, y, INK, 0.4 * a);
      }
      if (o.variant === "b") return;
      if (g < 0.3) for (let i = 0; i < 18; i++) {
        const an = (i / 18) * Math.PI + Math.PI, v = 160 + (i % 3) * 40, x = Math.cos(an) * v * g, y = -30 + Math.sin(an) * v * g * 0.6 + 200 * g * g;
        if (y < 0) { p.px(x, y, INK); p.px(x + 1, y, INK); p.px(x, y + 1, INK3); }
      }
    },
  };
}
