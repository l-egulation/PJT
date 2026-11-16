// 속박(보라) 이펙트를 스프라이트 시트(투명 PNG + Aseprite 형식 JSON)로 뽑는다.
//   node 전투테스트/속박/export.mjs --pixelfx <pixelfx 체크아웃> [--out outputs/pixelfx/root]
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
const OUT = resolve(flag("--out", join(HERE, "..", "..", "outputs", "pixelfx", "root")));

// 보라 잉크(임시값, 표식 격발의 purple 과 같다)
const PURPLE = ["#171716", "#3a1d52", "#64348c", "#9a5cc8", "#cfa2ec", "#fbf2ff"];

for (const f of ["root.js", "glow-path.js"]) copyFileSync(join(HERE, f), join(PFX, "src", "fx", "ink", f));
const { loadEffect, renderFrames, spriteSheet, save } = await import(pathToFileURL(join(PFX, "tools", "lib", "fxlib.mjs")).href);
const fx = await loadEffect("ink/root");

mkdirSync(OUT, { recursive: true });
const name = "root_purple";
const r = renderFrames(fx, { bg: "none", options: { palette: PURPLE } });
const s = spriteSheet(fx, r, name);
Object.assign(s.json.meta.pixelfx, { color: "purple", anchor: "monster-feet" });
save(join(OUT, `${name}.png`), s.png);
save(join(OUT, `${name}.json`), JSON.stringify(s.json, null, 1));
console.log(name, r.n, "frames", `${r.w}x${r.h}`, "->", OUT);
