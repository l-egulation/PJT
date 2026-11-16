// Deviant 전투 HUD 체력판(검은 판·크림 선·평행사변형 빨간 게이지)을 게임 화소(960x540)로 그리는 공용 코드.
// 효과 파일이 아니다(이름이 _로 시작). 회복·보호막·피해 시안이 같은 판 위에서 비교되도록 쓴다.
import { hex, mix, clamp } from "../../core/index.js";

export const SIZE = [276, 60], ORIGIN = [16, 34];
// 게이지: 왼쪽 아래 (BX, BY+BH), 위쪽은 SK만큼 오른쪽으로 밀린 평행사변형. 1 = BW px.
export const BX = 30, BY = 1, BW = 200, BH = 9, SK = 4;
export const MAXHP = 100;

export const K = {
  edge: hex("#0b0b0a"), plate: hex("#151513"), plateHi: hex("#1d1d1a"), cream: hex("#efe6d4"), creamDim: hex("#9d968a"),
  track: hex("#0d0d0c"), trackHi: hex("#1a1917"), face: hex("#2a2a26"), faceHi: hex("#3a3a34"), muted: hex("#bdb6a6"),
};
// 승인 HUD의 빨강(#ff6b5e)을 가운데 두고, 위 한 줄은 밝게, 아래 두 줄은 어둡게.
export const RED = [hex("#5a1714"), hex("#a8302a"), hex("#d9473e"), hex("#ff6b5e"), hex("#ff9a8c"), hex("#ffd6cc"), hex("#ffffff")];
export const redRow = (v) => (v < 1 ? RED[4] : v < 7 ? RED[3] : RED[2]);
// 보호막 민트(캐릭터 캡슐 #5fe8c0 와 같은 색 계열)
export const MINT = [hex("#0c2a24"), hex("#145244"), hex("#1f8a6c"), hex("#35c49a"), hex("#5fe8c0"), hex("#b4f7e0"), hex("#f2fffb")];
export const mintRow = (v) => (v < 1 ? MINT[5] : v < 7 ? MINT[4] : MINT[3]);

/** bar coordinate of a pixel center: xs = px along the bar (0..BW), v = row (0..BH), or null outside. */
export function barAt(x, y) {
  if (y < BY || y >= BY + BH) return null;
  const v = y - BY, xs = x - BX - SK * (1 - v / BH);
  if (xs < 0 || xs >= BW) return null;
  return { xs, v, u: xs / BW };
}
/** the screen x of bar position u (0..1) on row v. */
export const barX = (u, v = BH / 2) => BX + SK * (1 - v / BH) + u * BW;

const PLATE = [[12, -29], [238, -29], [245, -22], [255, 6], [249, 19], [12, 19], [5, 12], [8, -24]];

/** the plate, the portrait and the bar's empty track; hp shown as numbers (white flash amount optional). */
export function drawPlate(p, hp, o = {}) {
  const jx = o.jx ?? 0, jy = o.jy ?? 0;
  const P = PLATE.map(([x, y]) => [x + jx, y + jy]);
  p.poly(P.map(([x, y], i) => [x + (x > 130 ? 2 : -2), y + (y > 0 ? 2 : -2)]), K.edge);
  p.poly(P.map(([x, y]) => [x + (x > 130 ? 1 : -1), y + (y > 0 ? 1 : -1)]), o.frame ?? K.cream);
  p.poly(P, K.plate);
  // portrait
  p.circle(4 + jx, -6 + jy, 15, K.edge);
  p.circle(4 + jx, -6 + jy, 14, o.portrait ?? K.cream);
  p.circle(4 + jx, -6 + jy, 12, K.face);
  p.circle(1 + jx, -10 + jy, 6, K.faceHi);
  // track frame and track
  p.shade(BX - 2 + jx, BY - 2 + jy, BX + BW + SK + 3 + jx, BY + BH + 2 + jy, (x, y) => {
    const a = barAt(x - jx, y - jy);
    if (a) return a.v < 1 ? K.trackHi : K.track;
    const near = barAt(x - jx - 1, y - jy) || barAt(x - jx + 1, y - jy) || barAt(x - jx, y - jy - 1) || barAt(x - jx, y - jy + 1);
    if (near) return o.track ?? K.cream;
  });
  // numbers
  const n = String(Math.ceil(clamp(hp, 0, MAXHP)));
  p.text(n, 38 + jx, -24 + jy, o.number ?? K.cream, { font: "big", scale: 2 });
  const w = n.length * 12;
  p.text("/ " + MAXHP, 40 + w + jx, -16 + jy, K.muted, { font: "mini" });
}

/** solid red fill up to hp (in hp units), skipping pixels where skip(xs, v) is true. */
export function drawFill(p, hp, o = {}) {
  const jx = o.jx ?? 0, jy = o.jy ?? 0, end = (clamp(hp, 0, MAXHP) / MAXHP) * BW;
  p.shade(BX + jx, BY + jy, BX + BW + SK + 1 + jx, BY + BH + jy, (x, y) => {
    const a = barAt(x - jx, y - jy);
    if (!a || a.xs >= end) return;
    if (o.skip && o.skip(a.xs, a.v)) return;
    return redRow(a.v);
  });
}
export { mix };
