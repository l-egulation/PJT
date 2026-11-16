// 덧베기(빨강) 추가타 이펙트를 스프라이트 시트(투명 PNG + Aseprite 형식 JSON)로 뽑는다.
//   node 전투테스트/덧베기/export.mjs --pixelfx <pixelfx 체크아웃> [--out outputs/pixelfx/follow-cut]
// 이펙트는 pixelfx 의 src/core 를 상대 경로로 가져오므로, 실행할 때 follow-cut.js 를 pixelfx 의 src/fx/ink/ 에 복사해 쓴다.
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
const OUT = resolve(flag("--out", join(HERE, "..", "..", "outputs", "pixelfx", "follow-cut")));

// 빨강 잉크(임시값, 표식 격발의 red 와 같다). 흰 심 → 빨강 → 반투명 번짐 순으로 칠한다.
const RED = ["#171716", "#5a1d16", "#9e3424", "#d8664c", "#f09a7c", "#fff3e6"];
// 같은 칼자국이 연달아 나오지 않게 각도가 조금씩 다른 판 세 개
const VARIANTS = [5, 8, 13];

copyFileSync(join(HERE, "follow-cut.js"), join(PFX, "src", "fx", "ink", "follow-cut.js"));
const { loadEffect, renderFrames, spriteSheet, save } = await import(pathToFileURL(join(PFX, "tools", "lib", "fxlib.mjs")).href);
const fx = await loadEffect("ink/follow-cut");

mkdirSync(OUT, { recursive: true });
VARIANTS.forEach((seed, i) => {
  const name = `follow-cut_red_v${i + 1}`;
  const r = renderFrames(fx, { bg: "none", options: { palette: RED, dir: 1, seed } });
  const s = spriteSheet(fx, r, name);
  Object.assign(s.json.meta.pixelfx, { color: "red", variant: i + 1, facing: "right", impactFrame: 0 });
  save(join(OUT, `${name}.png`), s.png);
  save(join(OUT, `${name}.json`), JSON.stringify(s.json, null, 1));
  console.log(name, r.n, "frames", `${r.w}x${r.h}`);
});
console.log("->", OUT);
