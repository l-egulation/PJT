// 다크 판타지 버전(stage2-all-patterns/simulation/build.mjs가 불러 dark-fantasy/에 쓴다): 빛 화풍 시트를 보스 디자인처럼 어둡고 무겁게 다시 칠한다(비교용, 원본은 그대로 둔다).
//  1) 색 계열을 나눠 어두운 팔레트로 다시 칠한다. 밝기를 계단으로 끊어 픽셀 덩어리를 유지한다.
//     장밋빛 → 핏빛 와인, 파랑 → 심연 남색, 흰색·보라 → 공허 보라. 흰 심은 아주 밝은 곳만 남기고 차갑게 바랜 색으로.
//  2) 반투명한 어두운 부분은 조금 더 불투명하게 해서 검은 불꽃처럼 무게를 준다.
//  3) 형태 바깥에 검붉은 그림자 테두리(기본 2px 진하게, 4px 옅게)를 둘러 밝은 배경 위에서도 윤곽이 서게 한다.
//     1px로 가늘게 그린 시트는 thin으로 1px·2px만 둘러 선이 뭉툭해지지 않게 한다.

const hex = (h) => [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16)];
export const DF_RAMPS = {
  rose: ['#1a0610', '#4a0c22', '#8a1838', '#c8304c', '#f2b4b8'].map(hex),
  blue: ['#060a18', '#121c3c', '#24386c', '#4870a8', '#c8e0f2'].map(hex),
  void: ['#0c0618', '#241040', '#44287a', '#7a58b8', '#e4d8f4'].map(hex),
};
const SHADOW = hex('#0a040c');

export function darkFantasy(img, { thin = false } = {}) {
  const near = thin ? 1 : 2, far = thin ? 2 : 4;
  const { w, h, data } = img, out = new Uint8Array(data.length), solid = new Uint8Array(w * h);
  for (let i = 0, p = 0; i < data.length; i += 4, p++) {
    const a = data[i + 3]; if (!a) continue;
    const r = data[i], g = data[i + 1], b = data[i + 2], L = (0.3 * r + 0.55 * g + 0.15 * b) / 255, warm = (r - b) / 255;
    const fam = warm > 0.08 ? 'rose' : warm < -0.06 && b - g < 60 ? 'blue' : warm < -0.06 ? 'void' : r > 200 && g > 200 && b > 200 ? 'void' : 'blue';
    const step = L > 0.93 ? 4 : L > 0.72 ? 3 : L > 0.48 ? 2 : L > 0.28 ? 1 : 0;
    const c = DF_RAMPS[fam][step];
    out.set([c[0], c[1], c[2], step <= 1 ? Math.min(255, Math.round(a * 1.25)) : a], i);
    if (a > 40) solid[p] = 1;
  }
  // 그림자 테두리: 가까운 형태까지의 체비셰프 거리 ≤2는 진하게, ≤4는 옅게
  for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
    const p = y * w + x; if (out[p * 4 + 3]) continue;
    let d = 9;
    for (let dy = -far; dy <= far && d > near; dy++) { const yy = y + dy; if (yy < 0 || yy >= h) continue; for (let dx = -far; dx <= far; dx++) { const xx = x + dx; if (xx < 0 || xx >= w || !solid[yy * w + xx]) continue; d = Math.min(d, Math.max(Math.abs(dx), Math.abs(dy))); } }
    if (d <= near) out.set([...SHADOW, 165], p * 4); else if (d <= far) out.set([...SHADOW, 80], p * 4);
  }
  return { w, h, data: out };
}
