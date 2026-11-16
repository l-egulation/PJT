import { pop } from "./_bomb-pop.js";
import { INK_YELLOW } from "./charge-shot-burst.js";
// 발사기 유탄 폭발. 사용자: 유탄이 「스킬 같지 않게 안 화려하다」 → 모아쏘기와 같은 pixelfx 몽글 폭발로.
// size: main = 본체, frag = 파편. 사용자 「조오금만 줄이자」로 판정 반경(1.2유닛 38px·0.6유닛 19px)보다 약 80%(32px·16px)로 줄였다. ground: 바닥에 맞으면 위로만 솟는 반구(원점 = 땅에 닿은 자리).
export const meta = {
  name: "Launcher grenade blast", desc: "Launcher grenade: an ink-and-yellow blast, main or fragment, in the air or on the ground.",
  tags: ["explosion", "ink", "launcher", "one-shot"], size: [280, 240], origin: [140, 140], duration: 1.1, loop: false, fps: 30, bg: "light",
  options: { palette: INK_YELLOW, seed: 41, size: "main", ground: false },
};
const SIZE = {
  main: { rad: 32, fire: 65, power: 1.15, speed: [45, 160], fireSize: [0.17, 0.3], fireLife: [0.4, 0.68], core: 0.7, up: 30, stagger: 0.07, smokes: 0.55, smoke: 10, smokeSize: [0.22, 0.34], smokeLife: [0.55, 0.9], lift: 30, sparks: 14, embers: 9, flash: 17, flare: 28, ring: [15, 50], ringTime: 0.28, grit: 0 },
  frag: { rad: 16, fire: 16, power: 0.85, speed: [25, 85], fireSize: [0.24, 0.38], fireLife: [0.26, 0.46], core: 0.55, up: 22, stagger: 0.03, smokes: 0.4, smoke: 3, smokeSize: [0.26, 0.38], smokeLife: [0.36, 0.56], lift: 22, sparks: 4, embers: 2, flash: 8, flare: 11, ring: [7, 22], ringTime: 0.16, grit: 0 },
};
export const create = (o) => {
  const c = { ...SIZE[o.size ?? "main"], bigSmoke: false, glow: true, smokeCol: "#3a3230", smokeCol2: "#1b1616" };
  if (o.ground) Object.assign(c, { ground: true, grit: o.size === "frag" ? 4 : 12 });
  return pop(o, c, meta);
};
