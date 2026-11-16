import { makeBig } from "./_bomb-big.js";
import { meta as bm } from "./big-explosion.js";
// R2 영상 속도 그대로, 짧게 — 흰 돔·불덩이·버섯 연기까지 원래 속도로 가되 모든 수명을 줄여 2.4초에 사라진다.
export const meta = { ...bm, name: "Bomb big b", desc: "big-explosion at normal speed with every lifetime cut so it is gone in 2.4 s.", duration: 2.8, fps: 30, bg: "light" };
export const create = (o) => makeBig(o, { life: 0.42, stop: 1.2, plumeFade: [1.1, 1.9], mushroom: true, rocks: 14, scorchCool: 1.5 }, meta);
