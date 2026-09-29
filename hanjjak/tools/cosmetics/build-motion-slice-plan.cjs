const fs = require("fs");
const path = require("path");

const ROOT = "C:/hjw/apps/web/public";
const SETS = {
  angel: "/assets/characters/angel/runtime-equipment-v1",
  yakgwa: "/assets/characters/yakgwa/runtime-equipment-v1",
};
const MANIFEST = { angel: "angel-equipment-manifest.json", yakgwa: "yakgwa-equipment-manifest.json" };

/**
 * 매니페스트 토큰을 우리 낱장 부위로 묶는다.
 *
 * 마네킹 조각(팔·손·앞발)은 어느 장비도 아니다. 예전에는 팔을 상의에, 손을 장갑에 얹어
 * 두었는데, 그러면 다른 세트 장갑을 껴도 이 세트의 손이 따라오고 상의만 바꿔도 팔이
 * 따라왔다. 따로 떼어 `arms` 한 장으로 모으고, 그 세트의 상의를 입었을 때만 올린다.
 * 겹치는 차례는 아트가 적어 둔 그대로다: 망토 → 몸통 → 상의 → 신발 → 팔 → 장갑 → 모자.
 */
const BUCKET = token => {
  if (token === "base.body") return null;
  if (token === "base.arms" || token === "base.hands") return "arms";
  // 앞발은 신발 위에 덧그리는 마네킹 조각이다(샌들 밖으로 나온 발가락). 신발과 한 몸으로
  // 묶어야 다른 세트 신발을 신었을 때 이 세트의 맨발이 따라 나오지 않는다.
  if (token === "base.feet-front") return "shoes";
  if (token.startsWith("slot.cape")) return "back";
  if (token.startsWith("slot.clothing")) return "body";
  if (token.startsWith("slot.shoe")) return "shoes";
  if (token.startsWith("slot.weapon")) return "weapon";
  if (token.startsWith("slot.glove")) return "gloves";
  if (token.startsWith("slot.hat")) return "hat";
  throw new Error("모르는 토큰: " + token);
};

const jobs = [];
const summary = {};
for (const [slug, baseUrl] of Object.entries(SETS)) {
  const manifest = JSON.parse(fs.readFileSync(path.join(ROOT, baseUrl, MANIFEST[slug]), "utf8"));
  const itemBySlot = Object.fromEntries(manifest.items.map(item => [item.slot, item]));
  const atlasFor = token => {
    if (token.startsWith("base.")) return "base-" + token.slice(5);
    const [, slot, sub] = token.split(".");
    const item = itemBySlot[slot];
    if (item.atlas) return item.atlas;
    const found = item.atlases.find(id => manifest.atlases[id]?.sublayer === sub);
    if (!found) throw new Error(`${slug} ${token} 아틀라스 못 찾음`);
    return found;
  };
  summary[slug] = {};
  for (const [frameKey, order] of Object.entries(manifest.drawOrderByFrame)) {
    const frame = manifest.frames[frameKey];
    const parts = { back: [], body: [], shoes: [], arms: [], weapon: [], gloves: [], hat: [] };
    for (const token of order) {
      const bucket = BUCKET(token);
      if (bucket) parts[bucket].push(atlasFor(token));
    }
    summary[slug][frameKey] = Object.fromEntries(Object.entries(parts).map(([k, v]) => [k, v.join(" + ")]));
    for (const [part, atlasIds] of Object.entries(parts)) {
      jobs.push({
        out: `${ROOT}/cosmetics/motion/${slug}/${frameKey}-${part}.png`,
        ops: atlasIds.map(id => ({
          image: path.join(ROOT, baseUrl, manifest.atlases[id].image).split(path.sep).join("/"),
          sx: frame.x, sy: frame.y, sw: frame.width, sh: frame.height,
        })),
      });
    }
  }
}
fs.writeFileSync("C:/hjw/slice-jobs.json", JSON.stringify(jobs, null, 1));
console.log(`작업 ${jobs.length}개 (세트 2 × 프레임 20 × 부위 7)`);
for (const slug of Object.keys(SETS)) {
  for (const frameKey of ["rest_01", "strike1_01"]) {
    console.log(` ${slug} ${frameKey}:`, JSON.stringify(summary[slug][frameKey]));
  }
}
