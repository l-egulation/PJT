// 착지 시한폭탄 예고 1.5초 — 땅에 붙은 폭탄 위로 심지 불꽃이 튀고, 폭탄에 빨간 깜빡임이 점점 빨라진다.
// 바닥에는 폭발 반경(3유닛 = 96px) 경고 원이 납작하게 깔리고, 안쪽 원이 바깥에서 가운데로 줄어들며 남은 시간을 보여 준다.
// 원점 = 폭탄 아래 땅. 폭탄 그림은 게임이 그린다(반지름 0.2유닛, 중심은 원점 위 6px).
import { Rng, fit, rand, clamp, hex, WHITE } from "../../core/index.js";
import { PALETTE } from "./_bomb-pop.js";
import { haze } from "./_lib.js";

export const meta = {
  name: "Bomb timed fuse", desc: "A landed timed bomb: fuse sparks, a red blink that speeds up, a flat warning ring at the blast radius and an inner ring closing in as the fuse runs down.",
  tags: ["bomb", "fuse", "warning", "one-shot"], size: [240, 70], origin: [120, 50], duration: 1.5, loop: false, fps: 30, bg: "light",
  options: { palette: PALETTE, seed: 2, radius: 96, fuse: 1.5, mark: true, style: "blink" },
};
// 타원 둘레까지의 화소 거리(기울기로 나눠 위아래도 같은 굵기)
const ringDist = (x, y, a, b) => { const d = Math.hypot(x / a, y / b) || 1e-6, g = Math.hypot(x / (a * a), y / (b * b)) / d; return Math.abs(d - 1) / Math.max(g, 1e-6); };
const SM1 = hex("#8c8289"), SM2 = hex("#a49ca2");
const RED = hex("#d9302a"), DEEP = hex("#8f1d1a");
export function create(o) {
  const F = fit(o.palette, 8), RAD = o.radius, FUSE = o.fuse;
  let t = 0;
  // 깜빡임: 남은 시간이 줄수록 간격이 짧아진다(0.4초 → 0.08초)
  const blinkOn = (tt) => { let s = 0, i = 0; while (s <= tt) { const gap = Math.max(0.08, 0.4 * Math.pow(1 - s / FUSE, 1.3)); if (tt < s + gap * 0.45) return true; s += gap; i++; } return false; };
  return {
    update(dt) { t += dt; },
    drawBack(p) {
      if (!o.mark) return;
      const k = clamp(t / FUSE), ry = RAD * 0.16;
      // 바깥 경고 원: 점선, 끝무렵엔 끊김 없이
      p.shade(-RAD - 2, -ry - 2, RAD + 2, ry + 2, (x, y) => {
        if (ringDist(x, y, RAD, ry) > 1.0) return;
        const a = Math.atan2(y / ry, x / RAD);
        if (k < 0.75 && Math.floor((a + Math.PI) * 9) % 2) return;
        return blinkOn(t) ? RED : DEEP;
      });
      // 안쪽 원: 반경 → 0 으로 줄어들며 남은 시간을 보여 준다
      const r = RAD * (1 - k);
      if (r > 3) p.shade(-r - 2, -r * 0.16 - 2, r + 2, r * 0.16 + 2, (x, y) => {
        if (ringDist(x, y, r, r * 0.16) > 0.8) return;
        return RED;
      });
    },
    draw(p) {
      const f = Math.floor(t * 30 + 1e-6);
      const k = clamp(t / FUSE);
      if (o.style === "heat") {
        // 달아오름: 폭탄이 점점 붉게 달아오르고 맥박치며, 마지막 0.15초는 하얗게 부푼다
        const pulse = 0.5 + 0.5 * Math.sin(t * (8 + 40 * k * k));
        haze(p, 0, -6, 7.5, 7.5, RED, Math.min(0.85, 0.12 + 0.6 * k + 0.2 * pulse * k));
        if (k > 0.55) haze(p, -1, -7, 4 + 2 * pulse, 4 + 2 * pulse, F[5], 0.35 + 0.4 * (k - 0.55) / 0.45);
        if (t > FUSE - 0.15) { const u = (t - FUSE + 0.15) / 0.15; haze(p, 0, -6, 7.5 + 4 * u, 7.5 + 4 * u, F[6], 0.6 + 0.4 * u); haze(p, 0, -6, 5 + 3 * u, 5 + 3 * u, WHITE, 0.8); }
        p.glow(0, -6, 14 + 10 * k, F[3], 0.15 + 0.35 * k);
      } else if (blinkOn(t)) { haze(p, 0, -6, 7.5, 7.5, RED, 0.55); haze(p, -1.5, -7.5, 3, 3, WHITE, 0.6); }
      if (o.style === "smoke") {
        // 심지 연기: 심지에서 회색 연기 실이 꼬불꼬불 올라가고, 끝무렵 더 굵고 잦아진다
        // 0.04초마다 한 가닥씩 태어난다고 보고, 끝무렵일수록 더 많이 살린다
        const gap = 0.04, last = Math.floor(t / gap);
        for (let id = Math.max(0, last - 22); id <= last; id++) {
          const born = id * gap, age = t - born, kb = clamp(born / FUSE);
          if (age < 0 || age > 0.9 || (id % 3 !== 0 && rand(id, 1, 12) > kb * 1.2)) continue;
          const x = 3 + Math.sin(age * 7 + id) * (2 + age * 5), y = -15 - age * 34, r = 1.2 + age * 3.2 * (0.6 + kb);
          haze(p, x, y, r, r, age < 0.3 ? SM1 : SM2, 0.75 * (1 - age / 0.9));
        }
      }
      // 심지 불꽃: 폭탄 위 오른쪽 위로 튄다, 끝무렵 더 많이
      const n = 3 + Math.floor(t / FUSE * (o.style === "heat" ? 9 : 5));
      for (let i = 0; i < n; i++) {
        const a = -1.9 + rand(f, i, 3) * 1.6, d = 2 + rand(f, i, 4) * (5 + 5 * t / FUSE);
        p.px(3 + Math.cos(a) * d, -14 + Math.sin(a) * d, rand(f, i, 5) < 0.5 ? F[6] : F[4]);
      }
      p.circle(3, -14, 1.7, F[6]); p.px(3, -14, WHITE);
    },
  };
}
