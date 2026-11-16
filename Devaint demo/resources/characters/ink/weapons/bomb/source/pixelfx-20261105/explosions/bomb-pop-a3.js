import { pop, PALETTE } from "./_bomb-pop.js";
// A3 더 몽글 — 판정 반경까지 덩어리가 부풀어 오르고, 연기 덩이도 크고 둥글게 남는다. 불티는 거의 없다.
export const meta = {
  name: "Bomb pop a3", desc: "Round air burst: a white ball flash, fire puffs all round, sparks and a short wisp of smoke.",
  tags: ["explosion", "fire", "bomb", "small", "one-shot"], size: [200, 170], origin: [100, 110], duration: 1.2, loop: false, fps: 30, bg: "light",
  options: { palette: PALETTE, seed: 11 },
};
export const create = (o) => pop(o, { rad: 22, fire: 44, power: 1, speed: [30, 120], fireSize: [0.34, 0.6], fireLife: [0.42, 0.75], core: 0.7, up: 32, stagger: 0.07, smokes: 0.55, bigSmoke: true, smoke: 8, smokeSize: [0.32, 0.48], smokeLife: [0.55, 0.9], lift: 30, sparks: 8, embers: 5, flash: 15, flare: 22, ring: [14, 50], ringTime: 0.28, glow: true }, meta);
