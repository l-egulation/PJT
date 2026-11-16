import { Rng, clamp, ease, smooth, hex, TAU } from "../../core/index.js";
// 먹안개 5차 후보(2026-11-11 사용자 참고 두 게임): 판정 10×5 유닛(320×160px)을 가득 덮는 연막.
//   style="cel"     청백 셀 연막: 큰 덩어리를 납작한 3~4단 색 띠로 끊고(위왼쪽 밝음), 가로 속도선이 훑고 지나간다. 바닥에 짙은 연기 띠.
//   style="fog"     회색 소용돌이 안개: 비틀린 잡음 농도를 여러 단계 반투명으로 칠해 부드럽게 흐르고 소용돌이친다. 불티가 떠오른다.
//   style="inkcel"  먹 셀 연막: cel 과 같은 결을 먹색으로.
// 원점 = 발밑 가운데. 0.3초 안에 상자를 덮고 4초 유지 뒤 걷힘.
export const meta = {
  name: "Dagger smoke ref", desc: "Smoke screen candidates after two reference games: flat cel bands with speed lines, soft swirling fog with embers, ink cel.",
  tags: ["ink", "dagger", "smoke", "area"], size: [460, 260], origin: [230, 236], duration: 4.6, loop: false, fps: 25, bg: "light",
  options: { seed: 9, life: 4, style: "cel" },
};
const PAL = {
  // 2026-11-11 「색 음영이 별로, 밝게」: 몸통은 거의 흰색, 그늘은 옅은 한두 단계만
  cel: ["#ffffff", "#f0f4fd", "#dbe3f6", "#c2cdec", "#a9b6e0"],
  inkcel: ["#ffffff", "#f4f1ef", "#e3dedb", "#cdc5c1", "#b3aaa6"],
  fog: ["#ffffff", "#f5f4f7", "#e8e6ec", "#d6d3dc", "#c2bfc9"],
  // 1스테이지(밝은 종이 배경)용: 배경보다 짙은 옅은 먹색. 2스테이지(어두운 수로)는 위의 밝은 fog
  fogInk: ["#7d6f66", "#685b54", "#544841", "#41372f", "#2f2723"],   // 2026-11-11 「더 어둡게, 아랫부분이 너무 밝다」
};
function hash2(s, x, y) { let h = (x * 374761393 + y * 668265263 + s * 2147483647) | 0; h = Math.imul(h ^ (h >>> 13), 1274126177); return ((h ^ (h >>> 16)) >>> 0) / 4294967296; }
function vn(s, x, y) {
  const ix = Math.floor(x), iy = Math.floor(y), fx = x - ix, fy = y - iy, u = fx * fx * (3 - 2 * fx), v = fy * fy * (3 - 2 * fy);
  const a = hash2(s, ix, iy), b = hash2(s, ix + 1, iy), c = hash2(s, ix, iy + 1), d = hash2(s, ix + 1, iy + 1);
  return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
}
const fbm = (s, x, y) => 0.5 * vn(s, x, y) + 0.3 * vn(s + 7, x * 2.03, y * 2.03) + 0.2 * vn(s + 13, x * 4.1, y * 4.1);
const W2 = 230, Ht = 236;
export function create(o) {
  const C = PAL[o.tone || o.style].map(hex), R = new Rng(o.seed), S = o.seed, end = o.life, cel = o.style !== "fog";
  // 큰 덩어리(메타볼): 상자 전체에 엇갈려 깔고, 발밑에서 바깥으로 부풀어 나간다
  const blobs = [];
  // 사각형이 아니라 둥근 언덕(뭉게 더미): 아래 줄은 넓고, 위로 갈수록 좁아지며 꼭대기는 들쭉날쭉 솟는다
  // 속 덩이: 윤곽 초타원 안에 위아래 대칭으로 고르게 깐다(겉모양은 윤곽이, 속 명암은 덩이가 만든다)
  for (const cy of [-26, -53, -80, -107, -134]) {
    const k = Math.abs(cy + 80) / 80, span = 150 * Math.pow(1 - Math.pow(k, 2.6), 1 / 2.6), n = Math.max(2, Math.round(span / 30));
    for (let i = 0; i <= n; i++) { const x = -span + i * (2 * span / n) + R.range(-8, 8); blobs.push({ x, y: cy + R.range(-6, 6), r: R.range(26, 34), d: Math.hypot(x, cy + 80) / 900, ph: R.range(0, TAU), out: R.range(0, 0.6) }); }
  }
  const streaks = Array.from({ length: 14 }, () => ({ y: -R.range(10, 170), len: R.range(60, 170), v: R.range(420, 700), ph: R.range(0, 1), w: R.chance(0.35) ? 2 : 1, dir: R.sign() }));
  const embers = Array.from({ length: 30 }, () => ({ x: R.range(-140, 140), y: -R.range(10, 120), vy: -R.range(8, 22), ph: R.range(0, TAU), t0: R.range(0.3, 3.5) }));
  const F = new Float32Array(W2 * 2 * Ht);
  // B(안개) 2026-11-11 「구름처럼, 연막탄처럼 뭉게뭉게」: 크고 작은 둥근 덩이를 겹쳐(속은 크게, 가장자리는 작게) 농도로 합친다.
  // 겉 윤곽이 덩이들의 둥근 호로 이어져 뭉게구름처럼 된다. 덩이는 원 전체가 판정 상자(±158, 높이 2~158) 안에 들도록 둔다.
  const fogPuffs = [];
  { const Q = new Rng(S + 77);
    for (let k = 0; k < 300 && fogPuffs.length < 95; k++) {
      const a = Q.range(0, TAU), rr = Math.sqrt(Q.range(0, 1)), rim = rr > 0.7;
      const r = rim ? Q.range(20, 30) : Q.range(32, 46);
      let x = Math.cos(a) * rr * 150, y = -80 + Math.sin(a) * rr * 76;
      x = Math.max(-158 + r, Math.min(158 - r, x)); y = Math.max(-158 + r, Math.min(-2 - r, y));
      fogPuffs.push({ x, y, r, d: Math.hypot(x, y + 80) / 700 + Q.range(0, 0.05), ph: Q.range(0, TAU), out: Q.range(0, 0.6), vx: Q.range(-4, 4), vy: Q.range(-3, 3) });
    } }
  const FP = new Float32Array(W2 * 2 * Ht);
  // 판정 10×5(±160px, 높이 160px) 밖으로는 절대 나가지 않는다(2026-11-11 사용자). 사각형으로 안 보이게 윗모서리를 둥글게 깎고,
  // 가장자리 잡음은 안쪽으로만 파 들어간다. inside(x,y,g) = 0(밖)..1(충분히 안쪽)
  const HB = 158;
  // 하나의 매끈한 둥근 구름 윤곽(위·아래·양옆이 같은 성질): 상자 안에 꼭 맞는 초타원에, 각도를 따라 완만한 불룩함만 준다.
  // (2026-11-11 「위아래가 너무 다르다, 꺾인 부분이 인위적」) 경계는 안쪽으로만 파인다.
  const inside = (x, y, g) => {
    if (Math.abs(x) > HB || y < -HB || y >= 0) return 0;
    const gr = ease.outCubic(clamp(g / 0.32)), shrink = 1 - 0.75 * ease.inCubic(smooth(end - 0.6, end + 0.45, g));   // 걷힐 때는 구멍 대신 통째로 줄어든다
    const sz = (0.25 + 0.75 * gr) * shrink, nx = x / (HB * sz), ny = (y + 80) / (78 * sz);
    const r = Math.pow(Math.pow(Math.abs(nx), 2.6) + Math.pow(Math.abs(ny), 2.6), 1 / 2.6);
    const th = Math.atan2(ny, nx);
    const bulge = 0.075 * vn(S + 21, Math.cos(th) * 2.2 + g * 0.22, Math.sin(th) * 2.2 + 3) + 0.03 * vn(S + 23, Math.cos(th) * 5 - g * 0.3, Math.sin(th) * 5);
    return clamp((1 - bulge - r) / 0.035);
  };
  // B(안개) 전용 윤곽(2026-11-11 「너무 둥글고 겉이 매끈」): 크고 작은 뭉게 굴곡을 겹치고, 가장자리 넓은 띠에서 가닥으로 해어지며 옅어진다.
  // 굴곡은 안쪽으로만 파이므로 판정 밖으로는 여전히 나가지 않는다.
  const insideFog = (x, y, g) => {
    if (Math.abs(x) > HB || y < -HB || y >= 0) return 0;
    const gr = ease.outCubic(clamp(g / 0.32)), shrink = 1 - 0.75 * ease.inCubic(smooth(end - 0.6, end + 0.45, g));
    const sz = (0.25 + 0.75 * gr) * shrink, nx = x / (HB * sz), ny = (y + 80) / (79 * sz);
    const r = Math.pow(Math.pow(Math.abs(nx), 3.4) + Math.pow(Math.abs(ny), 3.4), 1 / 3.4);   // 덜 둥글게(어깨가 넓은 구름)
    const th = Math.atan2(ny, nx), cx = Math.cos(th), sy = Math.sin(th);
    const bulge = 0.16 * vn(S + 21, cx * 1.8 + g * 0.18, sy * 1.8 + 3) + 0.09 * vn(S + 25, cx * 4.2 - g * 0.25, sy * 4.2 + 1) + 0.05 * vn(S + 29, cx * 9 + g * 0.4, sy * 9);
    const fray = 0.07 * fbm(S + 31, x * 0.09 + g * 0.6, y * 0.05 - g * 0.3);              // 가닥: 가장자리를 잘게 헤집는다
    return clamp((1 - bulge - fray - r) / 0.16);
  };
  let t = 0;
  return {
    update(dt) { t += dt; },
    draw(p) {
      const g = t + 1e-6; if (g > end + 0.6) return;
      const out = 1 - smooth(end - 0.4, end + 0.5, g);
      if (out <= 0) return;
      F.fill(0);
      for (const b of blobs) {
        const u = clamp((g - b.d) / 0.26); if (u <= 0) continue;
        const fade = 1 - smooth(end - 0.5 + b.out * 0.4, end + 0.4 + b.out * 0.3, g);
        const r = b.r * ease.outBack(u) * (1 + 0.06 * Math.sin(g * 2.2 + b.ph)) * (0.4 + 0.6 * fade);
        const cx = b.x + 4 * Math.sin(g * 0.9 + b.ph), cy = b.y + 3 * Math.cos(g * 1.1 + b.ph);
        for (let y = Math.max(-Ht, Math.floor(cy - r)); y <= Math.min(-1, cy + r); y++) for (let x = Math.max(-W2, Math.floor(cx - r)); x <= Math.min(W2 - 1, cx + r); x++) {
          const d = ((x - cx) ** 2 + (y - cy) ** 2) / (r * r); if (d < 1) F[(y + Ht) * W2 * 2 + x + W2] += (1 - d) * (1 - d);
        }
      }
      // 바닥 연기 띠: 발밑 높이에 짙게 깔린다
      // 바닥 연기 띠 없음: 공중에서도 쓰므로 땅을 전제하지 않는다
      const floor = 0, onFloor = () => false;
      if (cel) {
        for (let y = -Ht; y < 0; y++) for (let x = -W2; x < W2; x++) {
          const i = (y + Ht) * W2 * 2 + x + W2;
          const n = (fbm(S, x * 0.035 + g * 0.4, y * 0.045) - 0.5) * 0.45;
          const inn = inside(x, y, g); if (inn <= 0) continue;
          const fl = false;   // 윤곽 안은 빈틈없이 채운다(구멍 없음). 속 명암만 덩이가 만든다
          // 위왼쪽에서 오는 빛: 위왼쪽 이웃보다 짙으면 그늘
          const j = Math.max(0, i - 5 * W2 * 2 - 4), lit = (F[j] ?? 0) - F[i] + n * 0.25;
          let k = lit > 0.06 ? 0 : lit > -0.1 ? 1 : lit > -0.3 ? 2 : 3;
          if (inn < 1) k = Math.min(k, 1);   // 가장자리 한 겹은 밝은 테로 깔끔하게
          if (fl) k = Math.max(k, y > -floor * 0.45 ? 3 : 2);
          p.blend(x, y, C[k], [0.44, 0.5, 0.56, 0.62, 0.68][k] * Math.min(1, out * 1.6));   // 반투명: 연막 뒤 캐릭터·몬스터가 비친다
        }
        return;
      }
      // fog: 뭉게 덩이 농도 → 낮은 알파 여러 단계 + 위왼쪽 빛
      FP.fill(0);
      for (const b of fogPuffs) {
        const u = clamp((g - b.d) / 0.3); if (u <= 0) continue;
        // 걷힘(o.vanish, 2026-11-11 「너무 쪼개져 사라진다」): split=예전(덩이별로 줄어듦) · fade=모양 그대로 고르게 옅어짐
        //   rise=옅어지며 위로 떠오르고 조금 퍼짐 · thin=옅은 곳부터 부드럽게 걷히고 짙은 속이 마지막까지 남음
        const V = o.vanish || "split", kOut = smooth(end - 0.7, end + 0.5, g);
        const fade = V === "split" ? 1 - smooth(end - 0.6 + b.out * 0.4, end + 0.3 + b.out * 0.3, g) : 1;
        const grow = V === "rise" ? 1 + 0.18 * kOut : 1;
        const r = b.r * ease.outBack(u) * (1 + 0.07 * Math.sin(g * 2 + b.ph)) * (0.3 + 0.7 * fade) * grow; if (r < 2) continue;
        const lift = V === "rise" ? 26 * kOut * kOut : 0;
        const sx = Math.min(158 - r, Math.max(-158 + r, b.x + b.vx * g)), sy = Math.min(-2 - r, Math.max(-158 + r, b.y + b.vy * g - lift));
        for (let y = Math.floor(sy - r); y <= sy + r; y++) for (let x = Math.floor(sx - r); x <= sx + r; x++) {
          const d = ((x - sx) ** 2 + (y - sy) ** 2) / (r * r); if (d < 1) FP[(y + Ht) * W2 * 2 + x + W2] += (1 - d) * (1 - d);
        }
      }
      for (let y = -159; y < -1; y++) for (let x = -159; x < 159; x++) {
        const i = (y + Ht) * W2 * 2 + x + W2, v0 = FP[i]; if (v0 < 0.3) continue;
        const sw = fbm(S + 3, x * 0.03 + g * 0.25, y * 0.04 - g * 0.15);              // 속에서 천천히 도는 결
        const lit = FP[Math.max(0, i - 4 * W2 * 2 - 4)] - v0 + (sw - 0.5) * 0.35;      // 위왼쪽이 밝은 뭉게 명암
        let k = v0 < 0.4 ? 0 : lit > 0.08 ? 0 : lit > -0.08 ? 1 : lit > -0.28 ? 2 : 3;
        if (o.tone === "fogInk") k = Math.min(3, k + (y > -45 ? 2 : y > -90 ? 1 : 0));   // 먹색은 아래로 갈수록 짙게(밑이 뜨지 않게)
        const V = o.vanish || "split", kOut = smooth(end - 0.7, end + 0.5, g);
        let fa = Math.min(1, out * 1.6);
        if (V === "fade" || V === "rise") fa = 1 - smooth(end - 0.9, end + 0.5, g);                    // 전체가 고르게 서서히
        if (V === "thin") fa = clamp((v0 - (0.3 + 1.5 * kOut)) / 0.7) * (1 - smooth(end + 0.2, end + 0.55, g));   // 옅은 곳부터
        if (fa <= 0) continue;
        p.blend(x, y, C[k], [0.12, 0.16, 0.21, 0.27][k] * fa);    // 더 투명하게
      }
      for (const e of embers) {   // 불티: 천천히 떠오르며 깜빡인다
        const a = g - e.t0; if (a < 0 || a > 1.6) continue;
        const x = e.x + 6 * Math.sin(a * 2 + e.ph), y = e.y + e.vy * a; if (Math.sin(g * 18 + e.ph) < -0.3) continue;
        p.px(x, y, hex(a < 0.8 ? "#ffb35a" : "#d9532b")); if (a < 0.5) p.px(x + 1, y, hex("#ffe6a0"));
      }
    },
  };
}
