import { fit, clamp, ease } from "../../core/index.js";
import { drawDrop } from "./mark-drop-idle.js";

// 표식 방울 — 쌓일 때. 위에서 길쭉하게 떨어져 자리에서 납작하게 눌렸다가 위로 튀고 자리 잡는다.
// 끝 모양은 mark-drop-idle 의 첫 장과 같아 바로 이어 붙는다.
export const meta = {
  name: "Mark drop (land)",
  desc: "One ink mark drop arrives: it falls in stretched, squashes flat in its slot, springs up tall, and settles into the idle drop.",
  tags: ["ink", "mark", "status", "one-shot"],
  size: [24, 40],
  origin: [12, 28],
  duration: 0.32,
  loop: false,
  fps: 25,
  bg: "light",
  options: { palette: ["#1a1a1a", "#1a1a1a", "#1a1a1a", "#fbf6ec", "#ffffff", "#ffffff"], rim: true, seed: 3 },
};

const FALL = 0.08;

export function draw(p, t, o) {
  const C = fit(o.palette, 6);
  if (t >= meta.duration - 1e-6) return; // 마지막 장은 비운다: 게임이 idle 로 넘긴다
  if (t < FALL) {
    const k = ease.inQuad(clamp(t / FALL));
    drawDrop(p, { x: 0, y: -14 * (1 - k), r: 2.8 + 1 * k, sx: 0.75, sy: 1.6 - 0.3 * k }, C, o.seed, o.rim);
    return;
  }
  // 눌림 → 튀어 오름 → 가라앉음: 감쇠 진동
  const u = (t - FALL) / (meta.duration - FALL), w = Math.cos(u * Math.PI * 2.5) * (1 - u) ** 1.5;
  drawDrop(p, { x: 0, y: w > 0 ? 0 : Math.round(w * 2), sx: 1 + 0.45 * w, sy: 1 - 0.4 * w, boil: u }, C, o.seed, o.rim);
}
