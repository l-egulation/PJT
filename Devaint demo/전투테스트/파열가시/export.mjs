// 파열 가시(빨강) 추가타 이펙트를 스프라이트 시트(투명 PNG + Aseprite 형식 JSON)로 뽑는다.
//   node 전투테스트/파열가시/export.mjs --pixelfx <pixelfx 체크아웃> [--out outputs/pixelfx/burst-thorn]
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
const OUT = resolve(flag("--out", join(HERE, "..", "..", "outputs", "pixelfx", "burst-thorn")));

// 빨강 잉크(임시값, 표식 격발의 red 와 같다). 흰 심 → 빨강 → 반투명 번짐 순으로 칠한다.
const RED = ["#171716", "#5a1d16", "#9e3424", "#d8664c", "#f09a7c", "#fff3e6"];

for (const f of ["burst-thorn.js", "glow-path.js"]) copyFileSync(join(HERE, f), join(PFX, "src", "fx", "ink", f));
const { loadEffect, renderFrames, spriteSheet, save } = await import(pathToFileURL(join(PFX, "tools", "lib", "fxlib.mjs")).href);
const fx = await loadEffect("ink/burst-thorn");

mkdirSync(OUT, { recursive: true });
const name = "burst-thorn_red";
const r = renderFrames(fx, { bg: "none", options: { palette: RED, dir: 1 } });
const s = spriteSheet(fx, r, name);
// 미리보기에서 쓴 연결 시점: 격발 접촉 장 + 피격 멈춤 + 0.3초 뒤에 이 시트 0장(가시 접촉)을 튼다
Object.assign(s.json.meta.pixelfx, { color: "red", facing: "right", impactFrame: 0, floor: 28, afterBurstContact: 0.375 });
save(join(OUT, `${name}.png`), s.png);
save(join(OUT, `${name}.json`), JSON.stringify(s.json, null, 1));
console.log(name, r.n, "frames", `${r.w}x${r.h}`);
console.log("->", OUT);
