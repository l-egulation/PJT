// 회복(초록) 이펙트를 스프라이트 시트(투명 PNG + Aseprite 형식 JSON)로 뽑는다.
//   node 전투테스트/회복/export.mjs --pixelfx <pixelfx 체크아웃> [--out outputs/pixelfx/heal]
// 빛나는 선(glowLine)은 이동속도의 speed-trail.js 를 함께 쓰므로 둘 다 pixelfx 의 src/fx/ink/ 에 복사한다.
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
const OUT = resolve(flag("--out", join(HERE, "..", "..", "outputs", "pixelfx", "heal")));

// 초록 잉크(임시값, 표식 격발의 green 과 같다)
const GREEN = ["#171716", "#1d4128", "#2f7444", "#5fb37a", "#a6dcb1", "#f4fff4"];

copyFileSync(join(HERE, "heal.js"), join(PFX, "src", "fx", "ink", "heal.js"));
copyFileSync(join(HERE, "..", "이동속도", "speed-trail.js"), join(PFX, "src", "fx", "ink", "speed-trail.js"));
const { loadEffect, renderFrames, spriteSheet, save } = await import(pathToFileURL(join(PFX, "tools", "lib", "fxlib.mjs")).href);
const fx = await loadEffect("ink/heal");

mkdirSync(OUT, { recursive: true });
const name = "heal_green";
const r = renderFrames(fx, { bg: "none", options: { palette: GREEN } });
const s = spriteSheet(fx, r, name);
Object.assign(s.json.meta.pixelfx, { color: "green", anchor: "player-feet" });
save(join(OUT, `${name}.png`), s.png);
save(join(OUT, `${name}.json`), JSON.stringify(s.json, null, 1));
console.log(name, r.n, "frames", `${r.w}x${r.h}`, "->", OUT);
