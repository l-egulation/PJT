// 다음 공격 강화 이펙트를 스프라이트 시트(투명 PNG + Aseprite 형식 JSON)로 뽑는다.
//   node 전투테스트/다음공격강화/export.mjs --pixelfx <pixelfx 체크아웃> [--out outputs/pixelfx/empower]
// 빛 선·꺾은선은 ../이동속도/speed-trail.js 와 ../속박/glow-path.js 를 함께 쓰므로 모두 pixelfx 의 src/fx/ink/ 에 복사한다.
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
const OUT = resolve(flag("--out", join(HERE, "..", "..", "outputs", "pixelfx", "empower")));

// 잉크 팔레트(임시값, 표식 격발과 같다)
const RED = ["#171716", "#5a1d16", "#9e3424", "#d8664c", "#f09a7c", "#fff3e6"];
// 어느 파츠가 어느 시트를 쓰는가
//   승전의 칼끝(빨강): charge-rise(기다림 반복) → charge-release(공격 시작) → emp-hit(맞는 순간)
//   인계된 칼날(빨강): emp-hit 만. 무기를 바꾸면 곧바로 자동 공격이라 기다림 표식이 보일 틈이 없다
//   인계된 사거리(파랑): 없음. 더 멀리 있는 몬스터가 맞는 것이 표시다
const SHEETS = [["charge-rise", "red", RED], ["charge-release", "red", RED], ["emp-hit", "red", RED]];

const deps = [
  [join(HERE, "glow-dot.js"), "glow-dot.js"],
  [join(HERE, "..", "이동속도", "speed-trail.js"), "speed-trail.js"],
  [join(HERE, "..", "속박", "glow-path.js"), "glow-path.js"],
  ...["charge-rise.js", "charge-release.js", "emp-hit.js"].map((f) => [join(HERE, f), f]),
];
for (const [src, name] of deps) copyFileSync(src, join(PFX, "src", "fx", "ink", name));
const { loadEffect, renderFrames, spriteSheet, save } = await import(pathToFileURL(join(PFX, "tools", "lib", "fxlib.mjs")).href);

mkdirSync(OUT, { recursive: true });
for (const [id, color, palette] of SHEETS) {
  const fx = await loadEffect(`ink/${id}`);
  const name = `${id}_${color}`;
  const r = renderFrames(fx, { bg: "none", options: { palette } });
  const s = spriteSheet(fx, r, name);
  Object.assign(s.json.meta.pixelfx, { color, facing: "right", anchor: id === "emp-hit" ? "hit-point" : "player-feet" });
  save(join(OUT, `${name}.png`), s.png);
  save(join(OUT, `${name}.json`), JSON.stringify(s.json, null, 1));
  console.log(name, r.n, "frames", `${r.w}x${r.h}`);
}
console.log("->", OUT);
