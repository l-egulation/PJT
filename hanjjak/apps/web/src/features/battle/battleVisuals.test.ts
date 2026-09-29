import { existsSync, readFileSync } from "node:fs";
import { resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import { backgroundAsset, damagePresentation, damageText, heroFrame, heroMotion, heroMotionFrame, heroSkillMotion, monsterFor, monsterRenderKey, monsterSheet, monsterSpriteMeta, stageMonsterFor, stageVisual } from "./battleVisuals";

const PUBLIC_DIR = fileURLToPath(new URL("../../../public/", import.meta.url));
const EXPECTED_MOTIONS = ["idle", "move", "attack", "hit", "defeat"];

function publicFile(assetUrl: string): string {
  return resolve(PUBLIC_DIR, ...assetUrl.replace(/^\//, "").split("/"));
}

function pngSize(path: string): [number, number] {
  const header = readFileSync(path).subarray(0, 24);
  expect(header.subarray(1, 4).toString("ascii")).toBe("PNG");
  return [header.readUInt32BE(16), header.readUInt32BE(20)];
}

type SpriteManifest = {
  image?: string;
  grid: [number, number] | { columns: number; rows: number; cellWidth: number; cellHeight: number };
  frameSize?: [number, number];
  sheetSize?: [number, number];
  order: string[];
  frames?: string[];
  framesPerMotion?: number;
};

describe("chapter battle visuals", () => {
  it("versions the shared wooden-sword hero assets for immutable production caches", () => {
    expect(heroFrame("rest", 0)).toBe("/assets/chapters/chapter-04-sushi/hero/rest_01.png?v=basic-wooden-sword-v1-20260911");
    expect(heroFrame("strike1", 3)).toBe("/assets/chapters/chapter-04-sushi/hero/strike1_04.png?v=basic-wooden-sword-v1-20260911");
  });

  it.each([
    // 1. 과일가게
    ["stage.01-01", "melon-glasshouse", ["tangerine"], "durian-tyrant"],
    ["stage.01-02", "melon-glasshouse", ["strawberry-scout"], "durian-tyrant"],
    ["stage.01-03", "melon-glasshouse", ["tangerine", "strawberry-scout"], "durian-tyrant"],
    ["stage.01-04", "pineapple-plantation", ["banana"], "melon-noble"],
    ["stage.01-05", "pineapple-plantation", ["peach"], "melon-noble"],
    ["stage.01-06", "pineapple-plantation", ["banana", "peach"], "melon-noble"],
    ["stage.01-07", "durian-riverside-packing-house", ["cherry"], "watermelon-ssireum-king"],
    ["stage.01-08", "durian-riverside-packing-house", ["grape"], "watermelon-ssireum-king"],
    ["stage.01-09", "durian-riverside-packing-house", ["cherry", "grape"], "watermelon-ssireum-king"],
    ["stage.01-10", "watermelon-irrigation-field", [], "pineapple-general"],
    // 2. 음료
    ["stage.02-01", "steam-teapot-locomotive-roundhouse", ["coffee-bean-charger"], "steam-teapot-duke"],
    ["stage.02-02", "steam-teapot-locomotive-roundhouse", ["milk-carton-healer"], "steam-teapot-duke"],
    ["stage.02-03", "steam-teapot-locomotive-roundhouse", ["coffee-bean-charger", "milk-carton-healer"], "steam-teapot-duke"],
    ["stage.02-04", "bubble-tea-after-hours-cafe", ["electrolyte-runner"], "bubble-tea-kraken"],
    ["stage.02-05", "bubble-tea-after-hours-cafe", ["soda-bottle-bomber"], "bubble-tea-kraken"],
    ["stage.02-06", "bubble-tea-after-hours-cafe", ["electrolyte-runner", "soda-bottle-bomber"], "bubble-tea-kraken"],
    ["stage.02-07", "cocktail-shaker-circus-workshop", ["juice-pouch-shooter"], "cocktail-shaker-jester"],
    ["stage.02-08", "cocktail-shaker-circus-workshop", ["water-drop-slime"], "cocktail-shaker-jester"],
    ["stage.02-09", "cocktail-shaker-circus-workshop", ["juice-pouch-shooter", "water-drop-slime"], "cocktail-shaker-jester"],
    ["stage.02-10", "vending-machine-rainy-delivery-yard", [], "vending-machine-king"],
    // 3. 쿠키
    ["stage.03-01", "butter-dough-molding-room", ["person-butter-cookie-fighter"], "tea-time-biscuit-count"],
    ["stage.03-02", "butter-dough-molding-room", ["butter-ring-guard"], "tea-time-biscuit-count"],
    ["stage.03-03", "butter-dough-molding-room", ["person-butter-cookie-fighter", "butter-ring-guard"], "tea-time-biscuit-count"],
    ["stage.03-04", "ruby-jam-injection-room", ["jam-thumbprint-rogue"], "ruby-jam-sand-queen"],
    ["stage.03-05", "ruby-jam-injection-room", ["sandwich-cookie-shieldbearer"], "ruby-jam-sand-queen"],
    ["stage.03-06", "ruby-jam-injection-room", ["jam-thumbprint-rogue", "sandwich-cookie-shieldbearer"], "ruby-jam-sand-queen"],
    ["stage.03-07", "cookie-tin-packaging-warehouse", ["chocolate-chip-charger"], "cookie-tin-bulwark-knight"],
    ["stage.03-08", "cookie-tin-packaging-warehouse", ["checkerboard-cookie-golem"], "cookie-tin-bulwark-knight"],
    ["stage.03-09", "cookie-tin-packaging-warehouse", ["chocolate-chip-charger", "checkerboard-cookie-golem"], "cookie-tin-bulwark-knight"],
    ["stage.03-10", "biscuit-royal-banquet-hall", [], "royal-assortment-gift-golem"],
  ] as const)("maps %s to its designed encounter", (stageId, background, normals, boss) => {
    expect(stageVisual(stageId)).toEqual({ background, normals, boss });
  });

  it("keeps 성심당 on its three bakery backgrounds now that it is chapter 9", () => {
    expect(stageVisual("stage.09-03").background).toBe("fried-station");
    expect(stageVisual("stage.09-04").background).toBe("bakery-shop");
    expect(stageVisual("stage.09-08").background).toBe("oven-cellar");
    expect(stageVisual("stage.09-10")).toMatchObject({ background: "oven-cellar", boss: "strawberry-siru" });
  });

  it("alternates mixed normal formations deterministically", () => {
    expect(monsterFor("stage.01-03", 1, false)).toBe("tangerine");
    expect(monsterFor("stage.01-03", 2, false)).toBe("strawberry-scout");
    expect(monsterFor("stage.01-03", 3, false)).toBe("tangerine");
  });

  it("maps the four cookie gift set spaces and bosses, now chapter 3", () => {
    expect(stageVisual("stage.03-01")).toMatchObject({ background: "butter-dough-molding-room", boss: "tea-time-biscuit-count" });
    expect(stageVisual("stage.03-04")).toMatchObject({ background: "ruby-jam-injection-room", boss: "ruby-jam-sand-queen" });
    expect(stageVisual("stage.03-07")).toMatchObject({ background: "cookie-tin-packaging-warehouse", boss: "cookie-tin-bulwark-knight" });
    expect(stageVisual("stage.03-10")).toMatchObject({ background: "biscuit-royal-banquet-hall", boss: "royal-assortment-gift-golem" });
    expect(monsterFor("stage.03-09", 2, false)).toBe("checkerboard-cookie-golem");
    expect(monsterSheet("ruby-jam-sand-queen")).toContain("/chapter-03-cookie-gift-set/monsters/ruby-jam-sand-queen/");
  });

  it("maps the sushi shop shifts and formations, now chapter 4", () => {
    expect(stageVisual("stage.04-01")).toMatchObject({ background: "sushi-morning", normals: ["flatfish-nigiri"], boss: "mackerel-nigiri-captain" });
    expect(stageVisual("stage.04-06")).toMatchObject({ background: "sushi-lunch", normals: ["tuna-nigiri", "salmon-nigiri"], boss: "fatty-tuna-nigiri-boss" });
    expect(stageVisual("stage.04-09")).toMatchObject({ background: "sushi-evening", normals: ["tamago-nigiri", "inari-sushi"], boss: "crab-gunkan-chief" });
    expect(stageVisual("stage.04-10")).toMatchObject({ background: "sushi-predawn", normals: [], boss: "futomaki-king" });
    expect(monsterFor("stage.04-03", 2, false)).toBe("shrimp-nigiri");
    expect(monsterSheet("futomaki-king")).toContain("/chapter-04-sushi/monsters/futomaki-king/");
  });

  it("keeps every stage background and 5x4 monster sheet backed by valid runtime files", () => {
    const checkedBackgrounds = new Set<string>();
    const checkedMonsters = new Set<string>();

    for (const chapter of [1, 2, 3, 4, 5, 7, 8, 9, 10]) {
      for (let stage = 1; stage <= 10; stage += 1) {
        const stageId = `stage.${String(chapter).padStart(2, "0")}-${String(stage).padStart(2, "0")}`;
        const visual = stageVisual(stageId);
        const backgroundUrl = backgroundAsset(null, stageId);
        const backgroundPath = publicFile(backgroundUrl);
        expect(existsSync(backgroundPath), `${stageId}: missing ${backgroundUrl}`).toBe(true);
        checkedBackgrounds.add(backgroundUrl);

        for (const monsterId of [...visual.normals, visual.boss]) {
          if (checkedMonsters.has(monsterId)) continue;
          checkedMonsters.add(monsterId);
          const sheetUrl = monsterSheet(monsterId);
          const sheetPath = publicFile(sheetUrl);
          const manifestPath = sheetPath.replace(/\.png$/, ".json");
          expect(existsSync(sheetPath), `${stageId}: missing ${sheetUrl}`).toBe(true);
          expect(existsSync(manifestPath), `${stageId}: missing manifest for ${monsterId}`).toBe(true);

          const manifest = JSON.parse(readFileSync(manifestPath, "utf8")) as SpriteManifest;
          const grid = Array.isArray(manifest.grid)
            ? manifest.grid
            : [manifest.grid.columns, manifest.grid.rows];
          const frameSize = manifest.frameSize ?? (Array.isArray(manifest.grid)
            ? undefined
            : [manifest.grid.cellWidth, manifest.grid.cellHeight] as [number, number]);
          expect(grid, `${monsterId}: sprite grid`).toEqual([4, 5]);
          expect(frameSize, `${monsterId}: frame size`).toEqual([monsterSpriteMeta(monsterId).cellSize, monsterSpriteMeta(monsterId).cellSize]);
          expect(manifest.order, `${monsterId}: motion order`).toEqual(EXPECTED_MOTIONS);
          expect(manifest.frames?.length ?? EXPECTED_MOTIONS.length * (manifest.framesPerMotion ?? 0), `${monsterId}: frame count`).toBe(20);
          expect(pngSize(sheetPath), `${monsterId}: PNG dimensions`).toEqual([frameSize![0] * 4, frameSize![1] * 5]);
          if (manifest.sheetSize) expect(manifest.sheetSize, `${monsterId}: manifest sheet size`).toEqual([frameSize![0] * 4, frameSize![1] * 5]);
          if (manifest.image) expect(manifest.image, `${monsterId}: manifest image`).toBe(sheetUrl.split("/").at(-1));
        }
      }
    }

    expect(checkedBackgrounds.size).toBe(35);
    expect(checkedMonsters.size).toBe(90);
  });

  it("prefers server stage presentation and safely falls back", () => {
    expect(stageMonsterFor("stage.09-01", 2, false, ["bomunsan-whirlwind", "curry-croquette"], "peach-siru")).toBe("curry-croquette");
    expect(stageMonsterFor("stage.09-01", 21, true, [], "peach-siru")).toBe("peach-siru");
    expect(backgroundAsset("oven-cellar", "stage.09-01")).toContain("/oven-cellar.png");
    expect(monsterFor("stage.04-10", 1, false)).toBe("futomaki-king");
  });

  it("derives hero attacks from authoritative rendering events", () => {
    expect(heroMotion([{ eventId: "1", logicalTick: 4, type: "PLAYER_BASIC_ATTACK_STARTED", actor: "PLAYER", target: "ENEMY", enemyIndex: 1, boss: false, skillId: null, damage: null, critical: false, hpBefore: null, hpAfter: null, defeated: false }], true)).toBe("strike1");
  });

  it("uses each skill's main-battle motion and rests after a defeated enemy", () => {
    expect(heroSkillMotion("active_heavy")).toBe("strike1");
    expect(heroSkillMotion("active_dot")).toBe("thrust1");
    expect(heroSkillMotion("active_haste")).toBe("rest");
    expect(heroSkillMotion("active_basic_amp")).toBe("rest");
    expect(heroMotion([{ eventId: "2", logicalTick: 5, type: "ENEMY_DEFEATED", actor: "PLAYER", target: "ENEMY", enemyIndex: 1, boss: false, skillId: null, damage: null, critical: false, hpBefore: 0, hpAfter: 0, defeated: true }], true)).toBe("rest");
  });

  it("plays each strike exactly once from frame one through frame four", () => {
    expect([0, 99, 100, 199, 200, 299, 300, 399, 400].map((elapsed) => heroMotionFrame("strike1", elapsed))).toEqual([0, 0, 1, 1, 2, 2, 3, 3, 3]);
  });

  it("keeps the monster mounted across events for one enemy", () => {
    expect(monsterRenderKey(3, false)).toBe(monsterRenderKey(3, false));
    expect(monsterRenderKey(4, false)).not.toBe(monsterRenderKey(3, false));
    expect(monsterRenderKey(3, true)).not.toBe(monsterRenderKey(3, false));
  });

  it("distinguishes the five player damage presentations", () => {
    const event = (type: string, skillId: string | null, critical: boolean) => ({ eventId: `${type}-${skillId}-${critical}`, logicalTick: 1, type, actor: "PLAYER", target: "ENEMY", enemyIndex: 1, boss: false, skillId, damage: 100, critical, hpBefore: 200, hpAfter: 100, defeated: false });

    expect(damagePresentation(event("PLAYER_ATTACK_IMPACT", null, false))).toBe("basic");
    expect(damagePresentation(event("PLAYER_ATTACK_IMPACT", null, true))).toBe("basic-critical");
    expect(damagePresentation(event("PLAYER_SKILL_IMPACT", "active_heavy", false))).toBe("heavy");
    expect(damagePresentation(event("PLAYER_SKILL_IMPACT", "active_heavy", true))).toBe("heavy-critical");
    expect(damagePresentation(event("DOT_TICK", "active_dot", false))).toBe("dot");
    expect(damageText(1_289)).toBe("1289");
    expect(damageText(-42)).toBe("0");
  });
});
