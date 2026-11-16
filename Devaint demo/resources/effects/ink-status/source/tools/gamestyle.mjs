// 게임 화풍 다시 칠하기: 2스테이지 보스 VFX에서 확정한 다크 판타지 칠하기(darkfantasy.mjs)를 상태이상 표시에 맞게 고친 것.
//  1) 색상(hue)은 그대로 두고 밝기를 5계단으로 끊어 어둡고 무거운 단계 색으로 다시 칠한다(표식 7색 구분 유지).
//     채도는 조금 낮추고, 가장 밝은 계단만 바랜 밝은 색으로 남긴다.
//  2) 어두운 반투명은 더 진하게, 옅은 빛 번짐은 더 옅게 해 빛보다 면이 읽히게 한다.
//  3) 형태 바깥에 검붉은 그림자 테두리(1px 진하게, 2px 옅게)를 두른다. rim:false 면 두르지 않는다(표식 방울).
const SHADOW = [10, 4, 12];
const LSTEP = [0.08, 0.2, 0.35, 0.52, 0.82];

function rgb2hsl(r, g, b) {
  r /= 255; g /= 255; b /= 255;
  const mx = Math.max(r, g, b), mn = Math.min(r, g, b), l = (mx + mn) / 2;
  if (mx === mn) return [0, 0, l];
  const d = mx - mn, s = l > 0.5 ? d / (2 - mx - mn) : d / (mx + mn);
  const h = mx === r ? (g - b) / d + (g < b ? 6 : 0) : mx === g ? (b - r) / d + 2 : (r - g) / d + 4;
  return [h / 6, s, l];
}
function hsl2rgb(h, s, l) {
  if (!s) return [l * 255, l * 255, l * 255].map(Math.round);
  const q = l < 0.5 ? l * (1 + s) : l + s - l * s, p = 2 * l - q;
  const f = (t) => { t = (t + 1) % 1; return t < 1 / 6 ? p + (q - p) * 6 * t : t < 1 / 2 ? q : t < 2 / 3 ? p + (q - p) * (2 / 3 - t) * 6 : p; };
  return [f(h + 1 / 3), f(h), f(h - 1 / 3)].map((v) => Math.round(v * 255));
}

export function gameStyle(data, w, h, { rim = true } = {}) {
  const out = new Uint8ClampedArray(data.length), solid = new Uint8Array(w * h);
  for (let i = 0, p = 0; i < data.length; i += 4, p++) {
    const a = data[i + 3]; if (!a) continue;
    const [hh, s, l] = rgb2hsl(data[i], data[i + 1], data[i + 2]);
    const step = l > 0.86 ? 4 : l > 0.64 ? 3 : l > 0.42 ? 2 : l > 0.22 ? 1 : 0;
    const c = hsl2rgb(hh, Math.min(0.78, s * (step === 4 ? 0.45 : 0.82)), LSTEP[step]);
    let na = a;
    if (step <= 1) na = Math.min(255, Math.round(a * 1.25));
    else if (a < 70) na = Math.round(a * 0.55);
    out[i] = c[0]; out[i + 1] = c[1]; out[i + 2] = c[2]; out[i + 3] = na;
    if (na > 40) solid[p] = 1;
  }
  if (rim) for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
    const p = y * w + x; if (out[p * 4 + 3] > 40) continue;
    let d = 9;
    for (let dy = -2; dy <= 2; dy++) { const yy = y + dy; if (yy < 0 || yy >= h) continue; for (let dx = -2; dx <= 2; dx++) { const xx = x + dx; if (xx < 0 || xx >= w || !solid[yy * w + xx]) continue; d = Math.min(d, Math.max(Math.abs(dx), Math.abs(dy))); } }
    if (d <= 1) { out.set(SHADOW, p * 4); out[p * 4 + 3] = 165; } else if (d <= 2) { out.set(SHADOW, p * 4); out[p * 4 + 3] = 80; }
  }
  return out;
}
