// 보호막(초록) 생길 때·유지·깨질 때 이펙트를 스프라이트 시트(투명 PNG + Aseprite 형식 JSON)로 뽑는다.
//   node 전투테스트/보호막/export.mjs --pixelfx <pixelfx 체크아웃> [--out outputs/pixelfx/shield]
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
const OUT = resolve(flag("--out", join(HERE, "..", "..", "outputs", "pixelfx", "shield")));

// 초록 잉크(임시값, 표식 격발의 green 과 같다)
const GREEN = ["#171716", "#1d4128", "#2f7444", "#5fb37a", "#a6dcb1", "#f4fff4"];

for (const f of ["shield.js", "shield-up.js", "shield-break.js"]) copyFileSync(join(HERE, f), join(PFX, "src", "fx", "ink", f));
const { loadEffect, renderFrames, spriteSheet, save } = await import(pathToFileURL(join(PFX, "tools", "lib", "fxlib.mjs")).href);

mkdirSync(OUT, { recursive: true });
for (const id of ["shield-up", "shield", "shield-break"]) {
  const fx = await loadEffect(`ink/${id}`);
  const name = `${id}_green`;
  const r = renderFrames(fx, { bg: "none", options: { palette: GREEN } });
  const s = spriteSheet(fx, r, name);
  Object.assign(s.json.meta.pixelfx, { color: "green", anchor: "player-feet" });
  save(join(OUT, `${name}.png`), s.png);
  save(join(OUT, `${name}.json`), JSON.stringify(s.json, null, 1));
  console.log(name, r.n, "frames", `${r.w}x${r.h}`);
}
console.log("->", OUT);
