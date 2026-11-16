// 표식 방울(쌓임·대기·만료)과 표식 격발 이펙트를 기본(흰 몸 + 먹 테두리)과 파츠 색 다섯 판·여러 색 섞임 판(격발만) 스프라이트 시트(투명 PNG + Aseprite 형식 JSON)로 뽑는다.
//   node 전투테스트/표식격발/export.mjs --pixelfx <pixelfx 체크아웃> [--out outputs/pixelfx/mark-burst]
// 이펙트는 pixelfx 의 src/core 를 상대 경로로 가져오므로, 실행할 때 이 폴더의 이펙트 파일을 pixelfx 의 src/fx/ink/ 에 복사해 쓴다.
import { copyFileSync, mkdirSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const HERE = dirname(fileURLToPath(import.meta.url));
const args = process.argv.slice(2);
const flag = (name, fallback) => (args.includes(name) ? args[args.indexOf(name) + 1] : fallback);
const PFX = flag("--pixelfx");
if (!PFX) {
  console.error("usage: node export.mjs --pixelfx <pixelfx checkout> [--out DIR]");
  process.exit(1);
}
const OUT = resolve(flag("--out", join(HERE, "..", "..", "outputs", "pixelfx", "mark-burst")));

// 0번은 쓰지 않고, 2 그림자(가장자리 테) · 3 몸 · 4 밝음 · 5 번쩍임 (어두운 → 밝은).
// 기본(base): 파츠 없이 터질 때. 흰 몸에 먹 테두리(게임 피격 효과의 paperOutlineColor #1A1A1A).
// 색 판: 그 색 「표식을 터트릴 시」 파츠(파열 가시·가속·생기·여백·족쇄)를 끼우고 터트릴 때. 테두리 없이 몸 색으로 채운다(rim: false).
// 색 팔레트는 각 파츠 이펙트(파열가시·이동속도·회복·속박/둔화 export.mjs)와 같은 임시값이다. 표식 방울은 색과 관계없이 쌓이므로 기본 판만 뽑는다.
const BASE = ["#1a1a1a", "#1a1a1a", "#1a1a1a", "#fbf6ec", "#ffffff", "#ffffff"];
const INKS = {
  base: BASE,
  red: ["#171716", "#5a1d16", "#9e3424", "#d8664c", "#f09a7c", "#fff3e6"],
  yellow: ["#171716", "#5c4210", "#a87a14", "#e3b030", "#f6d878", "#fffbe8"],
  green: ["#171716", "#1d4128", "#2f7444", "#5fb37a", "#a6dcb1", "#f4fff4"],
  blue: ["#171716", "#123a55", "#1f6690", "#3a9cc4", "#8fd0ea", "#f0fbff"],
  purple: ["#171716", "#3a1d52", "#64348c", "#9a5cc8", "#cfa2ec", "#fbf2ff"],
};

const ID = "ink/mark-burst";
// mark-burst 는 머리 위 표식 방울 모양을 mark-drop-idle 에서 가져온다
const FILES = ["mark-burst.js", "mark-drop-idle.js", "mark-drop-land.js", "mark-drop-expire.js"];
for (const f of FILES) copyFileSync(join(HERE, f), join(PFX, "src", "fx", "ink", f));
const { loadEffect, renderFrames, spriteSheet, save } = await import(pathToFileURL(join(PFX, "tools", "lib", "fxlib.mjs")).href);
const fx = await loadEffect(ID);
const { HIT } = await import(pathToFileURL(join(PFX, "src", "fx", "ink", "mark-burst.js")).href);
const { SLOT } = await import(pathToFileURL(join(PFX, "src", "fx", "ink", "mark-drop-idle.js")).href);

mkdirSync(OUT, { recursive: true });
// 표식 방울: 방울 하나짜리 세 동작(쌓일 때 · 떠 있는 동안 반복 · 만료). 게임이 1~5개를 SLOT 자리에 놓는다
for (const motion of ["land", "idle", "expire"]) {
  const drop = await loadEffect(`ink/mark-drop-${motion}`);
  const name = `mark-drop-${motion}_base`;
  const r = renderFrames(drop, { bg: "none", options: { palette: BASE, rim: true } });
  const s = spriteSheet(drop, r, name);
  Object.assign(s.json.meta.pixelfx, { color: "base", slot: SLOT });
  save(join(OUT, `${name}.png`), s.png);
  save(join(OUT, `${name}.json`), JSON.stringify(s.json, null, 1));
  console.log(name, r.n, "frames", `${r.w}x${r.h}`);
}
for (const [color, palette] of Object.entries(INKS))
  for (let marks = 1; marks <= 5; marks++) {
    const name = `mark-burst_${color}_m${marks}`;
    const r = renderFrames(fx, { bg: "none", options: { palette, marks, dir: 1, rim: color === "base" } });
    const s = spriteSheet(fx, r, name);
    // 게임 쪽 연결에 필요한 시점: 접촉 장, 피격 멈춤을 걸 장. 왼쪽으로 맞으면 시트를 좌우 반전한다
    const impact = Math.round(HIT * r.fps);
    Object.assign(s.json.meta.pixelfx, { color, marks, facing: "right", impactFrame: impact, holdFrame: impact + 1 });
    save(join(OUT, `${name}.png`), s.png);
    save(join(OUT, `${name}.json`), JSON.stringify(s.json, null, 1));
    console.log(name, r.n, "frames", `${r.w}x${r.h}`);
  }
// 여러 색: 「표식을 터트릴 시」 파츠를 여러 색 끼웠을 때. 덩어리마다 색을 하나씩 돌려 주는 방식(mixMode "blob", 사용자 선택).
// 색 조합(둘~다섯 색, 빨강→노랑→초록→파랑→보라 순서) 26가지 × 표식 1~5개. 이름은 mark-burst_<색-색…>_m<표식 수>
const COLORS = ["red", "yellow", "green", "blue", "purple"];
const combos = [];
for (let bits = 1; bits < 1 << COLORS.length; bits++) {
  const cs = COLORS.filter((_, i) => bits & (1 << i));
  if (cs.length > 1) combos.push(cs);
}
for (const cs of combos)
  for (let marks = 1; marks <= 5; marks++) {
    const name = `mark-burst_${cs.join("-")}_m${marks}`;
    const r = renderFrames(fx, { bg: "none", options: { palette: INKS[cs[0]], mix: cs.map((c) => INKS[c]), mixMode: "blob", marks, dir: 1, rim: false } });
    const s = spriteSheet(fx, r, name);
    const impact = Math.round(HIT * r.fps);
    Object.assign(s.json.meta.pixelfx, { color: cs.join("-"), colors: cs, marks, facing: "right", impactFrame: impact, holdFrame: impact + 1 });
    save(join(OUT, `${name}.png`), s.png);
    save(join(OUT, `${name}.json`), JSON.stringify(s.json, null, 1));
  }
console.log("mixed", combos.length, "combos x 5");
console.log("->", OUT);
