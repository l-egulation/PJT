import { pop } from "./_bomb-pop.js";
// 발사기 모아쏘기 명중 폭발(1·2·3단). 폭탄 평타 A3 「더 몽글」 틀(_bomb-pop)에 발사기 색(먹·노랑·아이보리)을 입혔다.
// 사용자: 「덩어리가 너무 커」 → 불덩이 하나는 작게(fireSize) 하고 개수를 늘려 잘게 몽글거리게. 「눈에 확 띄게, 엄청 커도 돼」 → 단계마다 반경·개수·세기를 키운다.
// 판정은 어느 단이든 2x2유닛(64px 상자). 그림은 2·3단에서 상자를 넘는다. 원점 = 맞은 자리(공중).
// 흰 종이 배경에서 하얀 섬광은 안 보여 고리로 읽힌다 → 가장 밝은 두 칸을 연노랑·아이보리로
export const INK_YELLOW = ["#1b1616", "#3a3230", "#7a4a14", "#c07a12", "#eba516", "#f6c94a", "#ffe68a", "#fff6d0"];
export const meta = {
  name: "Charge shot burst", desc: "Launcher charge shot hit: an ink-and-yellow air burst in three strengths.",
  tags: ["explosion", "ink", "launcher", "one-shot"], size: [300, 260], origin: [150, 150], duration: 1.1, loop: false, fps: 30, bg: "light",
  options: { palette: INK_YELLOW, seed: 23, step: 3, floor: 32 },   // floor: 맞은 자리에서 땅까지 px(발 위 1유닛 = 32px)
};
const STEP = {
  1: { rad: 24, fire: 34, power: 0.9, speed: [30, 110], fireSize: [0.2, 0.34], fireLife: [0.32, 0.55], core: 0.6, up: 26, stagger: 0.05, smokes: 0.4, smoke: 6, smokeSize: [0.24, 0.36], smokeLife: [0.45, 0.7], lift: 26, sparks: 6, embers: 4, flash: 12, flare: 18, ring: [12, 34], ringTime: 0.22 },
  2: { rad: 34, fire: 60, power: 1.1, speed: [40, 150], fireSize: [0.18, 0.32], fireLife: [0.38, 0.65], core: 0.65, up: 30, stagger: 0.07, smokes: 0.5, smoke: 10, smokeSize: [0.22, 0.34], smokeLife: [0.55, 0.85], lift: 30, sparks: 12, embers: 8, flash: 17, flare: 28, ring: [16, 50], ringTime: 0.26 },
  3: { rad: 48, fire: 110, power: 1.35, speed: [50, 190], fireSize: [0.15, 0.28], fireLife: [0.42, 0.75], core: 0.7, up: 34, stagger: 0.1, smokes: 0.55, smoke: 16, smokeSize: [0.2, 0.32], smokeLife: [0.6, 0.95], lift: 36, sparks: 22, embers: 14, flash: 23, flare: 40, ring: [20, 72], ringTime: 0.32 },
};
export const create = (o) => pop(o, { ...STEP[o.step ?? 3], bigSmoke: false, glow: true, floor: o.floor ?? 32, smokeCol: "#3a3230", smokeCol2: "#1b1616" }, meta);
