import { pop, PALETTE } from "./_bomb-pop.js";
// 폭탄 3연투 한 방 — 평타 A3 「더 몽글」과 같은 결. size 0·1·2 로 작게·중간·크게(연쇄 펑), palette 로 색을 바꾼다(색색 펑).
export const meta = {
  name: "Bomb triple hit", desc: "One of the triple throw's blasts: the puffy basic blast in three sizes and any palette.",
  tags: ["explosion", "fire", "bomb", "triple", "one-shot"], size: [220, 190], origin: [110, 120], duration: 2.0, loop: false, fps: 30, bg: "light",
  options: { palette: PALETTE, seed: 11, size: 1, shape: "round" },
};
const SIZES = [
  { rad: 14, fire: 22, flash: 10, flare: 16, ring: [8, 34], ringTime: 0.2, sparks: 6, smoke: 5 },
  { rad: 18, fire: 32, flash: 13, flare: 22, ring: [12, 46], ringTime: 0.26, sparks: 10, smoke: 7 },
  { rad: 25, fire: 54, flash: 19, flare: 40, ring: [18, 64], ringTime: 0.32, sparks: 20, smoke: 10 },
];
export const create = (o) => {
  const s = SIZES[Math.max(0, Math.min(2, o.size | 0))];
  // shape: round(둥글게) · up(위로 솟구침) · wide(옆으로 넓게, 납작한 고리)
  const sh = o.shape === "up" ? { pillar: 0.65, up: 55, speed: [40, 200] } : o.shape === "wide" ? { flat: 0.6, up: 14, speed: [40, 190], ringFlat: 0.45 } : {};
  return pop(o, { ...s, power: 1, speed: [28, 125], fireSize: [0.34, 0.6], fireLife: [0.42, 0.75], core: 0.7, up: 32, stagger: 0.06, smokes: 0.55, bigSmoke: true,
    smokeSize: [0.32, 0.48], smokeLife: [0.55, 0.85], lift: 30, embers: 5, glow: true, ...sh }, meta);
};
