// 몬스터 상태이상 표시 게임용 시트를 만든다(2026-11-06 사용자 확정).
//   1) source/pixelfx/*.js 를 pixelfx 저장소의 src/fx/inkstatus/ 에 둔다(pixelfx 의 core 와 status/_lib.js 를 쓴다).
//   2) node source/tools/export_runtime.mjs <pixelfx 저장소 경로>   (이 에셋 폴더 기준)
// 효과마다 1배(32px = 1u)로 그려 게임 화풍(gamestyle.mjs)으로 다시 칠하고, 변형(표식 색·크기)을 행으로 쌓아 runtime/<이름>.png 와
// runtime/sheets.json 을 쓴다. 셀은 열 = 프레임, 행 = 변형이고 피벗은 셀 왼쪽 위 기준 px다.
// 외곽선: 표식 방울·방울 터짐·몸통 터짐만 외곽선과 그림자 테두리가 없고(사용자 요청), 나머지는 잉크 외곽선과 그림자 테두리가 있다.
import { join, resolve, dirname } from "node:path";
import { writeFileSync, mkdirSync } from "node:fs";
import { pathToFileURL, fileURLToPath } from "node:url";
import { gameStyle } from "./gamestyle.mjs";

const PFX = resolve(process.argv[2] ?? "../../../external/pixelfx");
const GROUP = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..");
const OUT = join(GROUP, "runtime");
const lib = await import(pathToFileURL(join(PFX, "tools/lib/fxlib.mjs")).href);
mkdirSync(OUT, { recursive: true });

const COLORS = ["red", "orange", "yellow", "green", "blue", "indigo", "violet"];
const MARKS = COLORS.map((c, mark) => [c, { mark }]);
const SIZES = [["small", { rx: 13 }], ["large", { rx: 22 }]];
// 게임 이름, pixelfx 효과, 변형 [이름, 옵션], 그림자 테두리, 용도
const SHEETS = [
  ["mark-drop", "mark-drop", MARKS, false, "쌓인 표식 하나(머리 위 줄, 새 표식이 오른쪽). 행 = 쌓인 순번 색"],
  ["mark-pop", "mark-pop", MARKS, false, "표식 하나가 터질 때 머리 위 그 자리"],
  ["mark-burst", "burst-wide", MARKS, false, "표식 하나가 터질 때 몸통 가운데, 몬스터 뒤 레이어(몸을 그 색으로 0.2초 물들이는 것은 Unity 코드)"],
  ["bind-front", "wrap-c", SIZES.map(([n, o]) => [n, { ...o, layer: "front" }]), true, "속박: 몸통을 감은 밧줄 세 줄의 앞쪽 반(몬스터 앞). 몸 폭 2u 미만 small, 이상 large"],
  ["bind-back", "wrap-c", SIZES.map(([n, o]) => [n, { ...o, layer: "back" }]), true, "속박: 같은 밧줄의 뒤쪽 반(몬스터 뒤)"],
  ["stun-stars", "stun-stars", [["default", {}]], true, "기절: 머리 위 별 고리(표식 줄이 있으면 그 위)"],
  ["poison-skull", "poison-skull-p", [["small", { big: 0 }], ["big", { big: 1 }]], true, "중독: 해골 거품 하나의 한살이. 몸 전체 범위 안 무작위 위치에 중첩당 초당 2.5개(몸을 보라로 물들이는 것은 Unity 코드)"],
];

const table = { format: "deviant-ink-status-sheets-v1", pixelsPerUnit: 32, sheets: [] };
for (const [name, id, variants, rim, use] of SHEETS) {
  const fx = await lib.loadEffect("inkstatus/" + id, join(PFX, "src", "fx"));
  // 외곽선 켜기(표식 계열은 효과 코드가 처음부터 외곽선을 그리지 않는다)
  (await import(pathToFileURL(join(PFX, "src/fx/inkstatus/_lib.js")).href)).setOutline(true);
  const renders = variants.map(([, options]) => lib.renderFrames(fx, { bg: "none", actor: false, options }));
  const { w, h, fps } = renders[0], n = renders[0].frames.length;
  const img = lib.canvas(w * n, h * variants.length, [0, 0, 0, 0]);
  renders.forEach((r, row) => r.frames.forEach((f, i) => img.paste(gameStyle(f.clear, w, h, { rim }), w, h, i * w, row * h)));
  lib.save(join(OUT, name + ".png"), img.png());
  table.sheets.push({
    name, effect: "inkstatus/" + id, use, columns: n, rows: variants.length, frames: n, cellWidth: w, cellHeight: h,
    pivotX: fx.meta.origin[0], pivotY: fx.meta.origin[1], durationsMs: Array(n).fill(Math.round((1000 / fps) * 1000) / 1000),
    loop: !!fx.meta.loop, variants: variants.map(([v]) => v),
  });
  console.log(name, `${w}x${h}`, n, "frames", variants.length, "rows");
}
writeFileSync(join(OUT, "sheets.json"), JSON.stringify(table, null, 2) + "\n");
