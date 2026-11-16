// 속박 감싸기 시안 C: 밧줄 세 줄이 몸에 바짝 조여 납작하게 감기고, 줄마다 앞쪽이 살짝 처져(곡선) 무게가 느껴진다. 기울기는 완만하게 제각각.
// 원점은 몸통 가운데. layer "front"·"back" 두 장.
import { TAU } from "../../core/index.js";
import { stepT } from "./_lib.js";
import { PURPLE, ramp } from "./_ink.js";
import { wrapBind } from "./_wrap.js";

export const meta = {
  name: "Wrap C",
  desc: "Root wrap C: three ropes cinched flat and tight round the body, each sagging a little at the front, at gently differing slants.",
  tags: ["inkstatus", "root", "loop"],
  size: [80, 60], origin: [40, 30], duration: 0.8, loop: true, fps: 15, bg: "none",
  options: { palette: PURPLE, rx: 13, layer: "front", seed: 36 },
};

export function draw(p, t, o) {
  const C = ramp(o.palette), ph = (stepT(t, 15) / meta.duration) * TAU;
  wrapBind(p, C, o, [{ tilt: -0.25, dy: -7, ry: 2.5, w: 0.95, bow: 2 }, { tilt: 0.18, dy: -1, ry: 2.5, bow: 2.5 }, { tilt: -0.08, dy: 6, ry: 2, w: 0.9, bow: 2 }], ph, 1.4);
}
