// 끌어당김(보라) 줄 이펙트를 스프라이트 시트(투명 PNG + Aseprite 형식 JSON)로 뽑는다.
//   node 전투테스트/끌어당김/export.mjs --pixelfx <pixelfx 체크아웃> [--out outputs/pixelfx/pull]
// 줄은 ../속박/glow-path.js 로 그리므로 둘 다 pixelfx 의 src/fx/ink/ 에 복사한다.
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
const OUT = resolve(flag("--out", join(HERE, "..", "..", "outputs", "pixelfx", "pull")));

// 보라 잉크(임시값, 표식 격발의 purple 과 같다)
const PURPLE = ["#171716", "#3a1d52", "#64348c", "#9a5cc8", "#cfa2ec", "#fbf2ff"];

copyFileSync(join(HERE, "pull-rope.js"), join(PFX, "src", "fx", "ink", "pull-rope.js"));
copyFileSync(join(HERE, "..", "속박", "glow-path.js"), join(PFX, "src", "fx", "ink", "glow-path.js"));
const { loadEffect, renderFrames, spriteSheet, save } = await import(pathToFileURL(join(PFX, "tools", "lib", "fxlib.mjs")).href);
const { SHOOT, TAUT, PULL0, PULL1, RELEASE } = await import(pathToFileURL(join(PFX, "src", "fx", "ink", "pull-rope.js")).href);
const fx = await loadEffect("ink/pull-rope");

mkdirSync(OUT, { recursive: true });
const name = "pull-rope_purple";
const r = renderFrames(fx, { bg: "none", options: { palette: PURPLE } });
const s = spriteSheet(fx, r, name);
const o = fx.meta.options;
// 게임이 몬스터를 옮길 때 맞출 시점과 거리. 시트는 몬스터가 오른쪽 dist px 에 있는 경우 하나로만 구워진다
Object.assign(s.json.meta.pixelfx, {
  color: "purple", anchor: "puller-feet", facing: "right",
  timing: { shoot: SHOOT, taut: TAUT, pullStart: PULL0, pullEnd: PULL1, release: RELEASE },
  targetDistance: o.dist, pullDistance: o.pull, handY: o.hand, targetChestY: o.chest,
});
save(join(OUT, `${name}.png`), s.png);
save(join(OUT, `${name}.json`), JSON.stringify(s.json, null, 1));
console.log(name, r.n, "frames", `${r.w}x${r.h}`, "->", OUT);
