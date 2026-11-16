import { pop, PALETTE } from "./_bomb-pop.js";
// M2 앞으로 뚫는 펑 — 미사일이 박힌 방향(+x)으로 불덩이가 쏠려 뻗고, 뒤로는 조금만. 같은 몽글 덩어리.
export const meta = {
  name: "Bomb missile hit b", desc: "A puffy blast thrown forward along the missile's path, only a little flung back.",
  tags: ["explosion", "fire", "bomb", "missile", "one-shot"], size: [260, 210], origin: [110, 130], duration: 1.3, loop: false, fps: 30, bg: "light",
  options: { palette: PALETTE, seed: 21 },
};
export const create = (o) => pop(o, { rad: 26, fire: 52, power: 1.2, forward: 0.75, speed: [35, 170], fireSize: [0.32, 0.58], fireLife: [0.42, 0.78], core: 0.6, up: 26, stagger: 0.06, smokes: 0.55, bigSmoke: true, smoke: 9, smokeSize: [0.3, 0.46], smokeLife: [0.6, 0.95], lift: 28, sparks: 22, embers: 7, flash: 17, flare: 40, ring: [16, 62], ringTime: 0.28, glow: true }, meta);
