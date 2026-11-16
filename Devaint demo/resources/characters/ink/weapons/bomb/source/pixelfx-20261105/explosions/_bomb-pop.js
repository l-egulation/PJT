// 폭탄 평타용 작은 폭발 — big-explosion 의 불덩이·불티·고리 방식(Sprite Fusion explosion, MIT, Hugo Duprez)을
// 평타 폭발 반경 1.5유닛(게임 48px) 안에서 0.7초 남짓으로 줄였다. 버섯 기둥·바위·그을음 없이 한 번 「펑」.
// 원점 = 폭탄 중심. 바닥 판(ground)은 원점 아래 6px(폭탄 반지름 0.2유닛).
import { Particles, Lights, P, PF, Rng, fit, flare, smooth, ease, clamp, noise2, TAU, WHITE, hex, mix } from "../../core/index.js";
import { fireRamp, drawPs, cull, dome, glowDome } from "./_lib.js";

export const PALETTE = ["#4a1712", "#c9281f", "#f2561d", "#ff8a2a", "#ffc23d", "#ffe26b", "#fff3a6", "#ffffff"];
export const FLOOR = 6;

export function pop(o, cfg, meta) {
  const F = fit(o.palette, 8), FR = fireRamp(F), R = new Rng(o.seed), WARM = mix(F[3], F[6], 0.45);
  const [W, H] = meta.size, [OX, OY] = meta.origin;
  const ground = cfg.ground ? () => FLOOR : () => Infinity;
  const ps = new Particles({ rng: R, ground, sparks: [F[6], F[5], F[3], F[2]] });
  const lights = new Lights();
  const RAD = cfg.rad, SMOKE = hex(cfg.smokeCol ?? "#5d5560"), SMOKE2 = hex(cfg.smokeCol2 ?? "#3f3947");
  let t = 0, done = false;

  // 위로만 터지는 바닥 폭발이면 아래쪽 각도를 막는다
  const angle = () => {
    if (cfg.forward && R.chance(cfg.forward)) return R.range(-0.95, 0.75); // 앞(+x)으로 쏠림, 위로 조금 더
    if (cfg.pillar && R.chance(cfg.pillar)) return -Math.PI / 2 + R.range(-0.32, 0.32); // 위로 좁게 치솟는 기둥
    if (cfg.flat && R.chance(cfg.flat)) return (R.chance(0.5) ? 0 : Math.PI) + R.range(-0.38, 0.22) * (R.chance(0.5) ? 1 : -1); // 옆으로 납작하게
    return cfg.ground ? R.range(-Math.PI - 0.2, 0.2) : R.range(0, TAU);
  };
  function blast() {
    done = true;
    lights.add(0, -4, RAD * 3, WARM, 0.45, 0.35);
    for (let i = 0; i < 4; i++) ps.spawn(P.FIRE, R.range(-2, 2), R.range(-2, 2), R.range(-15, 15), R.range(-30, -5), R.range(0.14, 0.24), 0, R.range(RAD * 0.42, RAD * 0.58));
    for (let i = 0; i < cfg.fire; i++) {
      const a = angle(), d = R.range(0, RAD * (cfg.core ?? 0.5)), sp = R.range(...(cfg.speed ?? [50, 170])) * cfg.power, sz = cfg.fireSize ?? [0.24, 0.44];
      ps.spawn(P.FIRE, Math.cos(a) * d, Math.sin(a) * d, Math.cos(a) * sp, Math.sin(a) * sp * 0.85 - (cfg.up ?? 25), R.range(...(cfg.fireLife ?? [0.32, 0.62])), 0, R.range(RAD * sz[0], RAD * sz[1]), R.range(0, cfg.stagger ?? 0), R.chance(cfg.smokes) ? PF.SMOKES | (cfg.bigSmoke ? PF.BIG : 0) : 0);
    }
    for (let i = 0; i < cfg.smoke; i++) {
      const a = angle(), d = R.range(RAD * 0.1, RAD * 0.55), sp = R.range(10, 50);
      ps.spawn(P.SMOKE, Math.cos(a) * d, Math.sin(a) * d - 3, Math.cos(a) * sp, Math.sin(a) * sp * 0.5 - cfg.lift, R.range(cfg.smokeLife[0], cfg.smokeLife[1]), R.chance(0.5) ? SMOKE : SMOKE2, R.range(RAD * (cfg.smokeSize ?? [0.2, 0.34])[0], RAD * (cfg.smokeSize ?? [0.2, 0.34])[1]), R.range(0.08, 0.2), PF.BIG);
    }
    for (let i = 0; i < cfg.sparks; i++) {
      const a = angle(), sp = R.range(140, 330) * cfg.power;
      ps.spawn(P.SPARK, Math.cos(a) * 3, Math.sin(a) * 3, Math.cos(a) * sp, Math.sin(a) * sp - 50, R.range(0.14, 0.4));
    }
    for (let i = 0; i < cfg.embers; i++) {
      const a = angle(), sp = R.range(30, 120);
      ps.spawn(P.EMBER, Math.cos(a) * RAD * 0.35, Math.sin(a) * RAD * 0.35, Math.cos(a) * sp, Math.sin(a) * sp - 60, R.range(0.5, 1.0), F[3]);
    }
    if (cfg.grit) for (let i = 0; i < cfg.grit; i++) {
      const u = R.range(-1, 1), a = -Math.PI / 2 + u * 0.9, sp = R.range(120, 260);
      ps.spawn(P.GRIT, u * 4, FLOOR - 1, Math.cos(a) * sp, Math.sin(a) * sp, R.range(0.5, 0.9), R.pick([hex("#6b5d52"), hex("#4b4038"), hex("#8a7a6a")]), R.chance(0.4) ? 2 : 1);
    }
  }


  // merge: 불·연기 덩어리를 하나하나 테두리 친 공으로 그리지 않고, 한 덩어리의 농도로 합쳐 칠한다.
  // 색은 덩어리마다가 아니라 전체 시간(식는 정도)과 그 자리의 두께로 정한다. 테두리는 바깥 윤곽에만.
  const MERGED = new Set([P.FIRE, P.SMOKE]);
  function drawRest(p, layer) {
    const all = ps.list;
    ps.list = all.filter((q) => !MERGED.has(q.k));
    ps.draw(p, layer);
    ps.list = all;
  }
  const SMK = [hex(cfg.smokeLit ?? "#8c8289"), hex(cfg.smokeBody ?? "#6c636d"), hex(cfg.smokeRim ?? "#4a434f")];
  function mass(p) {
    const fire = [], smoke = [];
    for (const q of ps.list) {
      if (q.delay > 0) continue;
      const age = 1 - q.life / q.max;
      if (q.k === P.FIRE) { const r = q.size * (0.55 + 0.45 * Math.min(1, age * 7)) * (1 - age * 0.45); if (r > 0.6) fire.push([q.x, q.y, r * 1.15]); }
      else if (q.k === P.SMOKE) { const r = q.size * (0.9 + age * 0.6) * (1 - smooth(0.35, 1, age)) * (1 - smooth(meta.duration * 0.62, meta.duration * 0.97, t)); if (r > 3) smoke.push([q.x, q.y, r]); }
    }
    if (!fire.length && !smoke.length) return;
    const f = Math.floor(t * 30), cool = clamp((t - 0.06) / (cfg.coolTime ?? 0.5));
    const sum = (L, x, y) => { let F = 0; for (const [bx, by, r] of L) { const d = ((x - bx) ** 2 + (y - by) ** 2) / (r * r); if (d < 1) F += (1 - d) * (1 - d); } return F; };
    const n = FR.length, seams = cfg.seams ?? 0;
    p.shade(-90, -105, 90, 55, (x, y) => {
      const wx = x + 0.9 * noise2(x * 0.2, y * 0.2 + f * 0.3, 7), wy = y + 0.9 * noise2(x * 0.2 + 5, y * 0.2 - f * 0.3, 8);
      let Fi = 0, best = 0, second = 0, bi = -1;
      for (let i = 0; i < fire.length; i++) {
        const [bx, by, r] = fire[i], d = ((wx - bx) ** 2 + (wy - by) ** 2) / (r * r);
        if (d >= 1) continue;
        const v = (1 - d) * (1 - d);
        Fi += v;
        if (v > best) { second = best; best = v; bi = i; } else if (v > second) second = v;
      }
      if (Fi > 0.3) {
        // 두꺼운 속은 뜨겁고 얇은 가장자리는 식은 색. 바깥 한 줄만 더 어두운 테두리
        const thick = clamp((Fi - 0.3) / 1.1);
        let idx = Math.round(cool * 5.2 + (1 - thick) * 2.2);
        if (Fi < 0.42) idx += 1;
        else if (seams && bi >= 0) {
          // 중간: 덩어리마다 아래·오른쪽 경계에 한 단계 어두운 결, (강하게면) 위·왼쪽 속에 한 단계 밝은 면
          const [bx, by, r] = fire[bi], dx = (wx - bx) / r, dy = (wy - by) / r, d = Math.sqrt(dx * dx + dy * dy);
          if (d > 0.68 && second > 0.04 && dx * 0.45 + dy > 0.1) idx += 1;
          else if (seams > 1 && d < 0.5 && dx * 0.6 + dy < -0.2) idx -= 1;
        }
        return FR[clamp(idx, 0, n - 1)];
      }
      const S = smoke.length ? sum(smoke, wx, wy) : 0;
      if (S > 0.3) return S < 0.42 ? SMK[2] : sum(smoke, wx + 1.5, wy + 1.5) < S * 0.85 ? SMK[0] : SMK[1];
    });
  }

  return {
    update(dt) {
      t += dt;
      if (!done) blast();
      ps.update(dt);
      cull(ps, -OX + 2, -OY + 2, W - OX - 2, H - OY - 2);
      lights.update(dt);
    },
    drawBack(p) {
      if (cfg.merge) return drawRest(p, "back");
      drawPs(p, ps, "back", FR);
    },
    draw(p) {
      // 첫 장: 하얀 섬광(바닥이면 반구 돔, 공중이면 공), 그다음 두 장은 식어 가는 테두리
      const fl = cfg.flash;
      if (t < 0.04) {
        if (cfg.ground) dome(p, F, 0, fl, 1);
        else { p.circle(0, 0, fl + 2, F[3]); p.circle(0, 0, fl, F[6]); p.circle(0, 0, fl * 0.7, WHITE); }
      } else if (t < 0.1) {
        const k = 1 - (t - 0.04) / 0.06, r = fl * (0.85 + 0.25 * (1 - k));
        if (cfg.ground) dome(p, F, 0, r, k);
        else { p.circle(0, 0, r + 1.5, F[2]); p.circle(0, 0, r, k > 0.5 ? F[6] : F[5]); p.circle(0, 0, r * 0.55, k > 0.5 ? WHITE : F[6]); }
      }
      if (cfg.merge) { mass(p); drawRest(p, "front"); } else drawPs(p, ps, "front", FR);
      if (t < 0.16) flare(p, 0, cfg.ground ? -6 : 0, cfg.flare * Math.pow(1 - t / 0.16, 1.4), 1 - t / 0.16, F);
      // 충격 고리: 판정 반경(48px) 근처까지 퍼지며 끊긴다
      const RT = cfg.ringTime;
      if (t < RT) {
        const u = t / RT, r = cfg.ring[0] + (cfg.ring[1] - cfg.ring[0]) * ease.outCubic(u);
        const hide = cfg.ground ? (v, a) => (Math.sin(a) > 0.15 ? 0 : Math.min(1, (1 - u) * 1.7)) : () => Math.min(1, (1 - u) * 1.7);
        if (cfg.ringFlat) p.ellipse(0, 0, r, r * cfg.ringFlat, u < 0.3 ? F[6] : u < 0.6 ? F[5] : F[3], (d) => { return d > 1 - Math.max(1, 2.4 * (1 - u) + 0.4) / (r * cfg.ringFlat) ? Math.min(1, (1 - u) * 1.7) : 0; });
        else p.ring(0, 0, r, Math.max(1, 2.4 * (1 - u) + 0.4), u < 0.3 ? F[6] : u < 0.6 ? F[5] : F[3], hide);
        if (cfg.glow) glowDome(p, 0, 0, r, 4, F[4], 0.25 * (1 - u));
      }
      lights.draw(p);
    },
  };
}
