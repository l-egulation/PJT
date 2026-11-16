import { makeBig } from "./_bomb-big.js";
import { meta as bm } from "./big-explosion.js";
// R3 버섯 없이 — 흰 돔·반원 고리·불덩이·바위·흙먼지만. 기둥 없이 1.8초.
export const meta = { ...bm, name: "Bomb big c", desc: "No mushroom column: dome, half ring, fireball, rocks and dust, gone in 1.8 s.", duration: 1.8, fps: 30, bg: "light" };
export const create = (o) => makeBig(o, { life: 0.4, stop: 0.8, plumeFade: [1, 1.6], mushroom: false, rocks: 14, scorchCool: 1.1 }, meta);
