// 3페이즈 패턴 1 유도 화살(판정 3×1u) 비행 그림. 공허 보라 한 가지 색 계열로만 그린다(사용자: 빨강·파랑을 섞지 말 것).
// 로켓처럼 보이지 않도록: 크고 뾰족한 미늘 화살촉, 가늘고 어두운 화살대(빛나는 심 없음), 꼬리 끝에만 뒤로 젖힌 깃털 한 묶음과 시위 홈.
// 게임 1배(32px = 1u). 화살은 오른쪽(+x)을 겨눈다. 원점은 화살 가운데(판정 중심). 4프레임 반복.
import { fit } from "../../core/index.js";

export const meta = {
  name: "Homing bolt (violet)",
  desc: "Blue's phase-3 homing bolt in a single void-violet range: a big barbed broadhead, a thin dark shaft, swept feathers and a nock at the tail, wrapped in no aura.",
  tags: ["projectile", "arrow", "roseblue", "loop"],
  size: [128, 48],
  origin: [64, 24],
  duration: 4 / 15,
  loop: true,
  fps: 15,
  bg: "dark",
  options: { seed: 2 },
};

const SH = fit(["#0a040c"], 1)[0];
const V = fit(["#0c0618", "#180a2c", "#26123e", "#361a56", "#4c2a74", "#6a42a0", "#9270cc", "#c4aef0", "#f0e8ff"], 9);

export function draw(p, t, o) {
  const f = Math.floor(t * 15 + 1e-6) % 4;
  // 화살대
  p.rect(-44, -2, 68, 5, SH); p.rect(-43, -1, 66, 3, V[2]); p.rect(-43, -1, 66, 1, V[4]);
  // 깃: 꼬리 끝에만, 뒤로 젖힌 위·아래 깃털과 깃살
  const fl = f % 2 ? 1 : 0;
  for (const s of [-1, 1]) {
    const tip = -50, root = -30;
    p.poly([[root + 2, s * 1], [root - 4, s * (8 + fl)], [tip - 2, s * (11 + fl)], [tip + 2, s * 2]], SH);
    p.poly([[root, s * 1], [root - 5, s * (7 + fl)], [tip, s * (9 + fl)], [tip + 3, s * 2]], s < 0 ? V[5] : V[4]);
    for (let k = 0; k < 4; k++) { const x0 = root - 3 - k * 4; p.line(x0, s * 2, x0 - 3, s * (6 + fl), s < 0 ? V[7] : V[6]); }
  }
  // 시위 홈
  p.rect(-48, -2, 5, 5, SH); p.rect(-47, -1, 3, 3, V[5]); p.dot(-47, 0, V[1]);
  // 촉: 크고 뾰족한 미늘 화살촉(위 면이 한 단계만 밝다)
  p.poly([[55, 0], [27, -13], [32, -3], [24, -2], [24, 2], [32, 3], [27, 13]], SH);
  p.poly([[52, 0], [29, -11], [33, -2], [26, -1], [26, 1], [33, 2], [29, 11]], V[5]);
  p.poly([[52, 0], [29, -11], [33, -2], [27, -1], [27, 0]], V[6]);
  p.line(30, -10, 51, 0, V[8]); p.line(34, 0, 51, 0, V[7]);
  p.rect(22, -3, 4, 7, SH); p.rect(23, -2, 2, 5, V[5]);
}
