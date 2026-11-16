// 사용자가 고른 잉크 버프·상태이상 FX를 게임용 시트(runtime/)로 내보낸다.
// 효과 코드는 미리보기와 같은 source/preview_template.html 의 스크립트를 그대로 실행하고(디더 반투명, 1배 32px = 1u),
// 캔버스 대신 RGBA 버퍼에 그린다. 결정적 난수만 쓰므로 같은 원본에서 바이트 그대로 같은 시트가 나온다.
// 사용: node effects/ink-buffs/source/tools/export_runtime.mjs   (리소스 저장소 루트 기준)
import fs from "node:fs";
import path from "node:path";
import vm from "node:vm";
import zlib from "node:zlib";
import { fileURLToPath } from "node:url";

const ASSET = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..", "..");
const ROOT = path.resolve(ASSET, "..", "..");
const PLAYER_FRAME = path.join(ROOT, "characters/ink/common/runtime/forest-character/idle/01.png");

// ---------- PNG ----------
function decodePng(file) {
  const d = fs.readFileSync(file); let o = 8, w = 0, h = 0; const idat = [];
  while (o < d.length) {
    const len = d.readUInt32BE(o), type = d.toString("ascii", o + 4, o + 8), body = d.subarray(o + 8, o + 8 + len);
    if (type === "IHDR") { w = body.readUInt32BE(0); h = body.readUInt32BE(4); if (body[8] !== 8 || body[9] !== 6 || body[12] !== 0) throw new Error("RGBA8 non-interlaced PNG expected: " + file); }
    if (type === "IDAT") idat.push(body);
    o += 12 + len;
  }
  const raw = zlib.inflateSync(Buffer.concat(idat)), out = Buffer.alloc(w * h * 4), bpp = 4, stride = w * 4;
  for (let y = 0; y < h; y++) {
    const f = raw[y * (stride + 1)], src = y * (stride + 1) + 1;
    for (let x = 0; x < stride; x++) {
      const a = x >= bpp ? out[y * stride + x - bpp] : 0, b = y > 0 ? out[(y - 1) * stride + x] : 0, c = x >= bpp && y > 0 ? out[(y - 1) * stride + x - bpp] : 0;
      let v = raw[src + x];
      if (f === 1) v += a; else if (f === 2) v += b; else if (f === 3) v += (a + b) >> 1;
      else if (f === 4) { const p = a + b - c, pa = Math.abs(p - a), pb = Math.abs(p - b), pc = Math.abs(p - c); v += pa <= pb && pa <= pc ? a : pb <= pc ? b : c; }
      out[y * stride + x] = v & 255;
    }
  }
  return { w, h, data: out };
}
const CRC = new Uint32Array(256).map((_, n) => { let c = n; for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1; return c >>> 0; });
function crc32(buf) { let c = 0xffffffff; for (const b of buf) c = CRC[(c ^ b) & 255] ^ (c >>> 8); return (c ^ 0xffffffff) >>> 0; }
function chunk(type, body) { const len = Buffer.alloc(4); len.writeUInt32BE(body.length); const tb = Buffer.concat([Buffer.from(type, "ascii"), body]); const crc = Buffer.alloc(4); crc.writeUInt32BE(crc32(tb)); return Buffer.concat([len, tb, crc]); }
function encodePng(w, h, rgba) {
  const raw = Buffer.alloc((w * 4 + 1) * h);
  for (let y = 0; y < h; y++) { raw[y * (w * 4 + 1)] = 0; rgba.copy(raw, y * (w * 4 + 1) + 1, y * w * 4, (y + 1) * w * 4); }
  const ihdr = Buffer.alloc(13); ihdr.writeUInt32BE(w, 0); ihdr.writeUInt32BE(h, 4); ihdr[8] = 8; ihdr[9] = 6;
  return Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), chunk("IHDR", ihdr), chunk("IDAT", zlib.deflateSync(raw, { level: 9 })), chunk("IEND", Buffer.alloc(0))]);
}

// ---------- 효과 코드 불러오기 ----------
const html = fs.readFileSync(path.join(ASSET, "source/preview_template.html"), "utf8");
let code = html.slice(html.indexOf("<script>") + 8, html.indexOf("// ---------- 몸 그림"));
const swap = (a, b) => { if (!code.includes(a)) throw new Error("template changed: " + a); code = code.replace(a, b); };
swap("const IMAGES = /*__IMAGES__*/;", "");
swap("const W = 112, H = 100, OX = 56, OY = 90, TAU = Math.PI * 2;", "let W = 112, H = 100, OX = 56, OY = 90; const TAU = Math.PI * 2;");
code += "\nglobalThis.__fx = { EFFECTS, S, B, R, setCell(w, h, ox, oy) { W = w; H = h; OX = ox; OY = oy; }, Painter };";
const sandbox = { Math, JSON, Object, Array, Set, Map, Number, String, parseInt, console };
sandbox.globalThis = sandbox; vm.createContext(sandbox); vm.runInContext(code, sandbox);
const FX = sandbox.__fx; FX.S.dither = true;

// 캔버스 대용: Painter 는 fillStyle·globalAlpha·fillRect 만 쓴다. 디더 반투명이라 알파는 늘 1이다.
class Buffer2D {
  constructor(w, h) { this.w = w; this.h = h; this.data = Buffer.alloc(w * h * 4); this.fillStyle = "#000000"; this.globalAlpha = 1; }
  fillRect(x, y, w, h) {
    const c = this.fillStyle, r = parseInt(c.slice(1, 3), 16), g = parseInt(c.slice(3, 5), 16), b = parseInt(c.slice(5, 7), 16);
    for (let j = y; j < y + h; j++) for (let i = x; i < x + w; i++) { if (i < 0 || j < 0 || i >= this.w || j >= this.h) continue; const o = (j * this.w + i) * 4; this.data[o] = r; this.data[o + 1] = g; this.data[o + 2] = b; this.data[o + 3] = 255; }
  }
}

// 플레이어 몸 정보(미리보기 prep 과 같은 계산): 발밑 가운데 원점, 윤곽선 점.
function actorInfo(file) {
  const { w, h, data } = decodePng(file); const op = (x, y) => x >= 0 && y >= 0 && x < w && y < h && data[(y * w + x) * 4 + 3] > 60;
  let bb = [1e9, 1e9, -1, -1]; const pts = [];
  for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) { if (op(x, y)) { bb = [Math.min(bb[0], x), Math.min(bb[1], y), Math.max(bb[2], x), Math.max(bb[3], y)]; } else if (op(x - 1, y) || op(x + 1, y) || op(x, y - 1) || op(x, y + 1)) pts.push([x, y]); }
  const cx = Math.round((bb[0] + bb[2]) / 2), by = bb[3];
  return { H: bb[3] - bb[1], Wb: bb[2] - bb[0], outline: pts.map(([x, y]) => [x - cx, y - by]) };
}
const PLAYER = actorInfo(PLAYER_FRAME);
const MONSTER = { small: { H: 68, Wb: 38, outline: [] }, large: { H: 100, Wb: 80, outline: [] } };
const HEAD = { H: 0, Wb: 38, outline: [] };

// ---------- 시트 정의 ----------
// layer: back/front/both. times: 프레임별 효과 시각. rows: 행마다 {variant, e(강화), body, c 덮어쓰기}.
const frames = (t0, len, fps) => Array.from({ length: Math.round(len * fps) }, (_, i) => t0 + i / fps);
const both = [{ variant: "base", e: false }, { variant: "enhanced", e: true }];
const SHEETS = [
  { name: "speed-wind", fx: "speed", cand: 0, layer: "both", times: frames(1, 1, 20), loop: true, wrap: 2, body: PLAYER, rows: both, anchor: "feet", facing: true,
    use: "이동속도 증가 유지 중. 몸 뒤로 흐르는 노란 바람 줄과 뒤꿈치 >> 표시. 몸 뒤 레이어, 바라보는 방향으로 뒤집는다" },
  { name: "shield-front", fx: "shield", cand: 0, layer: "front", times: frames(0.3, 2 * Math.PI / 1.5, 42 / (2 * Math.PI / 1.5)), loop: true, body: PLAYER, anchor: "feet",
    rows: [{ variant: "steady", c: { amt: 1, hit: -1, decay: false } }, { variant: "decaying", c: { amt: 1, hit: -1, decay: true } }],
    use: "일반 방어막 보유 중 거품막 테두리. 행 steady = 유예 중, decaying = 감소 중(점선)" },
  { name: "shield-gain", fx: "shield", cand: 0, layer: "front", times: frames(0, 0.25, 20), loop: false, body: PLAYER, anchor: "feet", rows: [{ variant: "gain", c: { amt: 1, hit: -1, decay: false } }],
    use: "방어막이 생길 때 한 번. 막이 밖에서 조여 든다" },
  { name: "shield-hit", fx: "shield", cand: 0, layer: "front", times: frames(0, 0.25, 20), loop: false, body: PLAYER, anchor: "feet", rows: [{ variant: "hit", c: { amt: 0.55, decay: false }, hitFromTime: true }],
    use: "방어막이 피해를 막을 때 한 번. 하얗게 번쩍이며 금이 간다" },
  { name: "dash-shield", fx: "dashshield", cand: 0, layer: "front", times: [1], loop: true, body: PLAYER, anchor: "feet", facing: true, rows: [{ variant: "steady", c: { still: true } }],
    use: "대쉬 방어막 유지 중. 앞쪽 민트 초승달. 바라보는 방향으로 뒤집는다" },
  { name: "dash-shield-break", fx: "dashshield", cand: 0, layer: "front", times: frames(3.25, 0.8, 20), loop: false, body: PLAYER, anchor: "feet", facing: true, rows: [{ variant: "break", c: { still: true } }],
    use: "대쉬 방어막 감소 시작부터. 초승달이 조각나 뒤로 흩어진다" },
  { name: "heal-crosses", fx: "heal", cand: 0, layer: "front", times: frames(0, 1.4, 20), loop: false, body: PLAYER, anchor: "feet", rows: both,
    use: "체력 회복 한 번. 밝은 초록 큰 십자와 회복량(+3 기본, +5 강화). 몸 초록 번쩍임은 Unity 코드" },
  { name: "red-flames-back", fx: "atk", cand: 0, layer: "back", times: frames(0, 1.8, 20), loop: true, wrap: 4, body: PLAYER, anchor: "feet", facing: true, rows: both,
    use: "기본 공격력 증가 유지 중. 어깨·머리의 빨간 불꽃(몸 뒤 레이어)" },
  { name: "red-flames-front", fx: "atk", cand: 0, layer: "front", times: frames(0, 1.8, 20), loop: true, body: PLAYER, anchor: "feet", rows: both,
    use: "기본 공격력 증가 유지 중. 양옆으로 오르는 빨간 위 화살표" },
  { name: "blue-flames-back", fx: "skilldmg", cand: 2, layer: "back", times: frames(0, 1.8, 20), loop: true, wrap: 4, body: PLAYER, anchor: "feet", facing: true,
    rows: [{ variant: "base", e: false, c: { loop: true } }, { variant: "enhanced", e: true, c: { loop: true } }], use: "스킬 피해 증가가 붙은 스킬 사용 동안. 파란 불꽃(몸 뒤 레이어)" },
  { name: "blue-flames-front", fx: "skilldmg", cand: 2, layer: "front", times: frames(0, 1.8, 20), loop: true, body: PLAYER, anchor: "feet",
    rows: [{ variant: "base", e: false, c: { loop: true } }, { variant: "enhanced", e: true, c: { loop: true } }], use: "스킬 피해 증가 동안 양옆 파란 위 화살표" },
  { name: "cdr-clock", fx: "cdr", cand: 0, layer: "front", times: frames(0, 1.3, 20), loop: false, body: PLAYER, anchor: "feet", rows: both,
    use: "스킬 쿨타임 감소 한 번. 머리 위 시계가 되감기고 깨진다(-15% 기본, -20% 강화)" },
  { name: "chain-front", fx: "bind", cand: 1, layer: "front", times: [1], loop: true, anchor: "feet", rows: [{ variant: "small", body: MONSTER.small, c: { D: 10 } }, { variant: "large", body: MONSTER.large, c: { D: 10 } }],
    use: "속박 유지 중. 사슬 두 줄 앞쪽 반과 자물쇠. 몸 폭 2u 미만 small, 이상 large" },
  { name: "chain-back", fx: "bind", cand: 1, layer: "back", times: [1], loop: true, anchor: "feet", rows: [{ variant: "small", body: MONSTER.small, c: { D: 10 } }, { variant: "large", body: MONSTER.large, c: { D: 10 } }],
    use: "속박 유지 중. 사슬 뒤쪽 반(몸 뒤 레이어)" },
  { name: "chain-in", fx: "bind", cand: 1, layer: "both", times: frames(0, 0.15, 20), loop: false, anchor: "feet", rows: [{ variant: "small", body: MONSTER.small, c: { D: 10 } }, { variant: "large", body: MONSTER.large, c: { D: 10 } }],
    use: "속박이 걸릴 때 사슬이 밖에서 조여 드는 한 번" },
  { name: "chain-snap", fx: "bind", cand: 1, layer: "both", times: frames(9.85, 0.15, 20), loop: false, anchor: "feet", rows: [{ variant: "small", body: MONSTER.small, c: { D: 10 } }, { variant: "large", body: MONSTER.large, c: { D: 10 } }],
    use: "속박이 풀릴 때 사슬이 튕겨 나가는 한 번" },
  { name: "slow-snail", fx: "slow", cand: 0, layer: "front", times: frames(0, 3.6, 10), loop: true, body: HEAD, anchor: "head", rows: [{ variant: "slow" }],
    use: "슬로우 유지 중. 머리 위를 기어가는 보라 달팽이. 피벗은 머리 꼭대기 가운데" },
];

const CW = 200, CH = 200, OXC = 100, OYC = 150;
FX.setCell(CW, CH, OXC, OYC);
const outDir = path.join(ASSET, "runtime"); fs.mkdirSync(outDir, { recursive: true });
const manifest = { format: "deviant-ink-buff-sheets-v1", pixelsPerUnit: 32, note: "열 = 프레임, 행 = 변형. 피벗은 셀 왼쪽 위 기준 px. anchor feet = 발밑 가운데, head = 머리 꼭대기 가운데. facing = 바라보는 방향(+x)이 오른쪽인 그림", sheets: [] };
for (const sh of SHEETS) {
  const fx = FX.EFFECTS.find((f) => f.id === sh.fx), cand = fx.cands[sh.cand];
  const cells = sh.rows.map((row) => sh.times.map((t) => {
    sandbox.EMIT_WRAP = sh.wrap || undefined;
    const body = row.body || sh.body, buf = new Buffer2D(CW, CH); const p = new FX.Painter(buf);
    FX.S.enh = !!row.e;
    const c = { t, D: fx.D(!!row.e), e: !!row.e, H: body.H, Wb: body.Wb, alive: true, rp: FX.R, outline: () => body.outline.map((q) => q.slice()), ...(row.c || {}) };
    if (row.hitFromTime) { c.hit = t; c.t = 1; }
    if (sh.layer !== "front" && cand.back) cand.back.call(cand, p, c);
    if (sh.layer !== "back" && cand.front) cand.front.call(cand, p, c);
    return buf;
  }));
  let x0 = CW, y0 = CH, x1 = -1, y1 = -1;
  for (const row of cells) for (const b of row) for (let y = 0; y < CH; y++) for (let x = 0; x < CW; x++) if (b.data[(y * CW + x) * 4 + 3]) { x0 = Math.min(x0, x); y0 = Math.min(y0, y); x1 = Math.max(x1, x); y1 = Math.max(y1, y); }
  if (x1 < 0) throw new Error("empty sheet " + sh.name);
  if (x0 === 0 || y0 === 0 || x1 === CW - 1 || y1 === CH - 1) throw new Error("sheet clipped by work canvas: " + sh.name);
  x0 -= 1; y0 -= 1; x1 += 1; y1 += 1;
  const cw = x1 - x0 + 1, ch = y1 - y0 + 1, cols = sh.times.length, rows = sh.rows.length, W = cw * cols, Hh = ch * rows, out = Buffer.alloc(W * Hh * 4);
  cells.forEach((row, r) => row.forEach((b, i) => { for (let y = 0; y < ch; y++) b.data.copy(out, ((r * ch + y) * W + i * cw) * 4, ((y0 + y) * CW + x0) * 4, ((y0 + y) * CW + x0 + cw) * 4); }));
  fs.writeFileSync(path.join(outDir, sh.name + ".png"), encodePng(W, Hh, out));
  const step = sh.times.length > 1 ? sh.times[1] - sh.times[0] : 1;
  manifest.sheets.push({ name: sh.name, effect: `${fx.id}/${cand.n}`, use: sh.use, anchor: sh.anchor, facing: !!sh.facing, columns: cols, rows, frames: cols, cellWidth: cw, cellHeight: ch,
    pivotX: OXC - x0, pivotY: OYC - y0, durationsMs: sh.times.map(() => Math.round(step * 1e6) / 1e3), loop: sh.loop, variants: sh.rows.map((r) => r.variant) });
}
fs.writeFileSync(path.join(outDir, "sheets.json"), JSON.stringify(manifest, null, 2) + "\n");
console.log(manifest.sheets.map((s) => `${s.name} ${s.columns}x${s.rows} ${s.cellWidth}x${s.cellHeight}`).join("\n"));
