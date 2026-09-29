import type { CombatRenderingEvent } from "./sessionApi";

// 챕터 번호 = 난이도 구간, 폴더 이름 = 그 챕터가 맡은 가게. 2026-09-15 개편 기준.
const CHAPTER_ONE_BASE = "/assets/chapters/chapter-01-fruit";
const CHAPTER_TWO_BASE = "/assets/chapters/chapter-02-drinks";
const CHAPTER_THREE_BASE = "/assets/chapters/chapter-03-cookie-gift-set";
const CHAPTER_FOUR_BASE = "/assets/chapters/chapter-04-sushi";
const CHAPTER_FIVE_BASE = "/assets/chapters/chapter-05-jeon";
const CHAPTER_SEVEN_BASE = "/assets/chapters/chapter-07-chinese";
const CHAPTER_EIGHT_BASE = "/assets/chapters/chapter-08-ballpark";
const CHAPTER_NINE_BASE = "/assets/chapters/chapter-09-sungsimdang";
const CHAPTER_TEN_BASE = "/assets/chapters/chapter-10-ssafy";
const HERO_BASE = "/assets/chapters/chapter-04-sushi";
const HERO_ASSET_VERSION = "basic-wooden-sword-v1-20260911";

export type BattleMonsterId =
  | "tangerine" | "strawberry-scout" | "banana" | "peach" | "cherry" | "grape" | "durian-tyrant" | "melon-noble" | "watermelon-ssireum-king" | "pineapple-general"   // 1. 과일가게
  | "water-drop-slime" | "milk-carton-healer" | "electrolyte-runner" | "coffee-bean-charger" | "soda-bottle-bomber" | "juice-pouch-shooter" | "bubble-tea-kraken" | "steam-teapot-duke" | "cocktail-shaker-jester" | "vending-machine-king"   // 2. 음료
  | "butter-ring-guard" | "person-butter-cookie-fighter" | "sandwich-cookie-shieldbearer" | "jam-thumbprint-rogue" | "checkerboard-cookie-golem" | "chocolate-chip-charger" | "royal-assortment-gift-golem" | "ruby-jam-sand-queen" | "cookie-tin-bulwark-knight" | "tea-time-biscuit-count"   // 3. 쿠키
  | "tamago-nigiri" | "flatfish-nigiri" | "shrimp-nigiri" | "salmon-nigiri" | "inari-sushi" | "tuna-nigiri" | "crab-gunkan-chief" | "mackerel-nigiri-captain" | "fatty-tuna-nigiri-boss" | "futomaki-king"   // 4. 스시
  | "chili-jeon" | "kimchi-jeon" | "perilla-jeon" | "shrimp-jeon-tail-soldier" | "zucchini-jeon" | "meat-jeon" | "mung-bean-jeon-armored-commander" | "chive-jeon-swordsman" | "seafood-scallion-jeon-octopus-general" | "golden-assorted-jeon-king"   // 5. 전
  | "fried-dumpling" | "guobaorou" | "danmuji" | "mapo-tofu" | "menbosha" | "tangsuyuk" | "dongpo-pork-general" | "malatang-cauldron" | "jjajangmyeon-master" | "jjamppong-king"   // 7. 중식당
  | "flat-dumpling" | "pot-ramen" | "dakgangjeong" | "barogejip-tteokbokki" | "fishcake-skewer" | "paopao-shrimp-dumpling" | "wapang-slugger" | "one-shot-chicken-tower" | "cheese-poutine-catcher" | "cream-shrimp-captain"   // 8. 야구장 먹거리
  | "kimchi-rice-ball" | "strawberry-mochi-tart" | "bomunsan-whirlwind" | "curry-croquette" | "toyo-bread" | "fried-soboro" | "strawberry-siru" | "peach-siru" | "pure-roll" | "crepe"   // 9. 성심당
  | "potato-croquette" | "egg-soup" | "pork-kimchi-stir-fry" | "pork-mushroom-stir-fry" | "sausage-vegetable-stir-fry" | "fried-spring-roll" | "ttukbaegi-kimchi-stew-general" | "banquet-noodle-master" | "tuna-mayo-spam-rice-captain" | "dried-pollock-seolleongtang-chief"   // 10. 싸피
  | "cucumber-maki"   // 더 이상 배치되지 않지만 세이브에 남아 있을 수 있다;

export type ChapterOneMonsterId = BattleMonsterId;

export type BattleBackgroundId =
  | "melon-glasshouse" | "pineapple-plantation" | "durian-riverside-packing-house" | "watermelon-irrigation-field"   // 1. 과일가게
  | "steam-teapot-locomotive-roundhouse" | "bubble-tea-after-hours-cafe" | "cocktail-shaker-circus-workshop" | "vending-machine-rainy-delivery-yard"   // 2. 음료
  | "butter-dough-molding-room" | "ruby-jam-injection-room" | "cookie-tin-packaging-warehouse" | "biscuit-royal-banquet-hall"   // 3. 쿠키
  | "sushi-morning" | "sushi-lunch" | "sushi-evening" | "sushi-predawn"   // 4. 스시
  | "jeon-iron-griddle" | "scallion-batter-table" | "makgeolli-crock-courtyard" | "rainy-market-jeon-stall"   // 5. 전
  | "red-lantern-banquet-hall" | "wok-kitchen" | "dim-sum-teahouse" | "mala-night-market"   // 7. 중식당
  | "chimaek-cheering-deck" | "home-run-hotdog-concession" | "bullpen-tteokbokki-pocha" | "night-homeplate-food-festival"   // 8. 야구장 먹거리
  | "fried-station" | "bakery-shop" | "oven-cellar"   // 9. 성심당
  | "yuseong-campus-lobby" | "yuseong-campus-front" | "yuseong-campus-cafeteria" | "yuseong-campus-walking-trail"   // 10. 싸피;

export type ChapterOneStageVisual = {
  background: BattleBackgroundId;
  normals: BattleMonsterId[];
  boss: BattleMonsterId;
};

export type MonsterSpriteMeta = {
  cellSize: number;
  displayScale: number;
  groundFromBottom: number;
  contentLeft?: number;
  contentRight?: number;
  contentTop: number;
  contentBottom: number;
  warningPortrait?: {
    frame: number;
    focusX: number;
    focusY: number;
  };
};

const CHAPTER_ONE_STAGES: Record<number, ChapterOneStageVisual> = {
  1: { background: "melon-glasshouse", normals: ["tangerine"], boss: "durian-tyrant" },
  2: { background: "melon-glasshouse", normals: ["strawberry-scout"], boss: "durian-tyrant" },
  3: { background: "melon-glasshouse", normals: ["tangerine", "strawberry-scout"], boss: "durian-tyrant" },
  4: { background: "pineapple-plantation", normals: ["banana"], boss: "melon-noble" },
  5: { background: "pineapple-plantation", normals: ["peach"], boss: "melon-noble" },
  6: { background: "pineapple-plantation", normals: ["banana", "peach"], boss: "melon-noble" },
  7: { background: "durian-riverside-packing-house", normals: ["cherry"], boss: "watermelon-ssireum-king" },
  8: { background: "durian-riverside-packing-house", normals: ["grape"], boss: "watermelon-ssireum-king" },
  9: { background: "durian-riverside-packing-house", normals: ["cherry", "grape"], boss: "watermelon-ssireum-king" },
  10: { background: "watermelon-irrigation-field", normals: [], boss: "pineapple-general" },
};

const CHAPTER_TWO_STAGES: Record<number, ChapterOneStageVisual> = {
  1: { background: "steam-teapot-locomotive-roundhouse", normals: ["coffee-bean-charger"], boss: "steam-teapot-duke" },
  2: { background: "steam-teapot-locomotive-roundhouse", normals: ["milk-carton-healer"], boss: "steam-teapot-duke" },
  3: { background: "steam-teapot-locomotive-roundhouse", normals: ["coffee-bean-charger", "milk-carton-healer"], boss: "steam-teapot-duke" },
  4: { background: "bubble-tea-after-hours-cafe", normals: ["electrolyte-runner"], boss: "bubble-tea-kraken" },
  5: { background: "bubble-tea-after-hours-cafe", normals: ["soda-bottle-bomber"], boss: "bubble-tea-kraken" },
  6: { background: "bubble-tea-after-hours-cafe", normals: ["electrolyte-runner", "soda-bottle-bomber"], boss: "bubble-tea-kraken" },
  7: { background: "cocktail-shaker-circus-workshop", normals: ["juice-pouch-shooter"], boss: "cocktail-shaker-jester" },
  8: { background: "cocktail-shaker-circus-workshop", normals: ["water-drop-slime"], boss: "cocktail-shaker-jester" },
  9: { background: "cocktail-shaker-circus-workshop", normals: ["juice-pouch-shooter", "water-drop-slime"], boss: "cocktail-shaker-jester" },
  10: { background: "vending-machine-rainy-delivery-yard", normals: [], boss: "vending-machine-king" },
};

const CHAPTER_THREE_STAGES: Record<number, ChapterOneStageVisual> = {
  1: { background: "butter-dough-molding-room", normals: ["person-butter-cookie-fighter"], boss: "tea-time-biscuit-count" },
  2: { background: "butter-dough-molding-room", normals: ["butter-ring-guard"], boss: "tea-time-biscuit-count" },
  3: { background: "butter-dough-molding-room", normals: ["person-butter-cookie-fighter", "butter-ring-guard"], boss: "tea-time-biscuit-count" },
  4: { background: "ruby-jam-injection-room", normals: ["jam-thumbprint-rogue"], boss: "ruby-jam-sand-queen" },
  5: { background: "ruby-jam-injection-room", normals: ["sandwich-cookie-shieldbearer"], boss: "ruby-jam-sand-queen" },
  6: { background: "ruby-jam-injection-room", normals: ["jam-thumbprint-rogue", "sandwich-cookie-shieldbearer"], boss: "ruby-jam-sand-queen" },
  7: { background: "cookie-tin-packaging-warehouse", normals: ["chocolate-chip-charger"], boss: "cookie-tin-bulwark-knight" },
  8: { background: "cookie-tin-packaging-warehouse", normals: ["checkerboard-cookie-golem"], boss: "cookie-tin-bulwark-knight" },
  9: { background: "cookie-tin-packaging-warehouse", normals: ["chocolate-chip-charger", "checkerboard-cookie-golem"], boss: "cookie-tin-bulwark-knight" },
  10: { background: "biscuit-royal-banquet-hall", normals: [], boss: "royal-assortment-gift-golem" },
};

const CHAPTER_FOUR_STAGES: Record<number, ChapterOneStageVisual> = {
  1: { background: "sushi-morning", normals: ["flatfish-nigiri"], boss: "mackerel-nigiri-captain" },
  2: { background: "sushi-morning", normals: ["shrimp-nigiri"], boss: "mackerel-nigiri-captain" },
  3: { background: "sushi-morning", normals: ["flatfish-nigiri", "shrimp-nigiri"], boss: "mackerel-nigiri-captain" },
  4: { background: "sushi-lunch", normals: ["tuna-nigiri"], boss: "fatty-tuna-nigiri-boss" },
  5: { background: "sushi-lunch", normals: ["salmon-nigiri"], boss: "fatty-tuna-nigiri-boss" },
  6: { background: "sushi-lunch", normals: ["tuna-nigiri", "salmon-nigiri"], boss: "fatty-tuna-nigiri-boss" },
  7: { background: "sushi-evening", normals: ["tamago-nigiri"], boss: "crab-gunkan-chief" },
  8: { background: "sushi-evening", normals: ["inari-sushi"], boss: "crab-gunkan-chief" },
  9: { background: "sushi-evening", normals: ["tamago-nigiri", "inari-sushi"], boss: "crab-gunkan-chief" },
  10: { background: "sushi-predawn", normals: [], boss: "futomaki-king" },
};

const CHAPTER_FIVE_STAGES: Record<number, ChapterOneStageVisual> = {
  1: { background: "jeon-iron-griddle", normals: ["zucchini-jeon"], boss: "chive-jeon-swordsman" },
  2: { background: "jeon-iron-griddle", normals: ["kimchi-jeon"], boss: "chive-jeon-swordsman" },
  3: { background: "jeon-iron-griddle", normals: ["zucchini-jeon", "kimchi-jeon"], boss: "chive-jeon-swordsman" },
  4: { background: "scallion-batter-table", normals: ["perilla-jeon"], boss: "mung-bean-jeon-armored-commander" },
  5: { background: "scallion-batter-table", normals: ["chili-jeon"], boss: "mung-bean-jeon-armored-commander" },
  6: { background: "scallion-batter-table", normals: ["perilla-jeon", "chili-jeon"], boss: "mung-bean-jeon-armored-commander" },
  7: { background: "makgeolli-crock-courtyard", normals: ["meat-jeon"], boss: "seafood-scallion-jeon-octopus-general" },
  8: { background: "makgeolli-crock-courtyard", normals: ["shrimp-jeon-tail-soldier"], boss: "seafood-scallion-jeon-octopus-general" },
  9: { background: "makgeolli-crock-courtyard", normals: ["meat-jeon", "shrimp-jeon-tail-soldier"], boss: "seafood-scallion-jeon-octopus-general" },
  10: { background: "rainy-market-jeon-stall", normals: [], boss: "golden-assorted-jeon-king" },
};

const CHAPTER_SEVEN_STAGES: Record<number, ChapterOneStageVisual> = {
  1: { background: "red-lantern-banquet-hall", normals: ["fried-dumpling"], boss: "dongpo-pork-general" },
  2: { background: "red-lantern-banquet-hall", normals: ["guobaorou"], boss: "dongpo-pork-general" },
  3: { background: "red-lantern-banquet-hall", normals: ["fried-dumpling", "guobaorou"], boss: "dongpo-pork-general" },
  4: { background: "wok-kitchen", normals: ["danmuji"], boss: "malatang-cauldron" },
  5: { background: "wok-kitchen", normals: ["mapo-tofu"], boss: "malatang-cauldron" },
  6: { background: "wok-kitchen", normals: ["danmuji", "mapo-tofu"], boss: "malatang-cauldron" },
  7: { background: "dim-sum-teahouse", normals: ["menbosha"], boss: "jjajangmyeon-master" },
  8: { background: "dim-sum-teahouse", normals: ["tangsuyuk"], boss: "jjajangmyeon-master" },
  9: { background: "dim-sum-teahouse", normals: ["menbosha", "tangsuyuk"], boss: "jjajangmyeon-master" },
  10: { background: "mala-night-market", normals: [], boss: "jjamppong-king" },
};

const CHAPTER_EIGHT_STAGES: Record<number, ChapterOneStageVisual> = {
  1: { background: "chimaek-cheering-deck", normals: ["paopao-shrimp-dumpling"], boss: "cream-shrimp-captain" },
  2: { background: "chimaek-cheering-deck", normals: ["barogejip-tteokbokki"], boss: "cream-shrimp-captain" },
  3: { background: "chimaek-cheering-deck", normals: ["paopao-shrimp-dumpling", "barogejip-tteokbokki"], boss: "cream-shrimp-captain" },
  4: { background: "home-run-hotdog-concession", normals: ["flat-dumpling"], boss: "cheese-poutine-catcher" },
  5: { background: "home-run-hotdog-concession", normals: ["fishcake-skewer"], boss: "cheese-poutine-catcher" },
  6: { background: "home-run-hotdog-concession", normals: ["flat-dumpling", "fishcake-skewer"], boss: "cheese-poutine-catcher" },
  7: { background: "bullpen-tteokbokki-pocha", normals: ["dakgangjeong"], boss: "wapang-slugger" },
  8: { background: "bullpen-tteokbokki-pocha", normals: ["pot-ramen"], boss: "wapang-slugger" },
  9: { background: "bullpen-tteokbokki-pocha", normals: ["dakgangjeong", "pot-ramen"], boss: "wapang-slugger" },
  10: { background: "night-homeplate-food-festival", normals: [], boss: "one-shot-chicken-tower" },
};

const CHAPTER_NINE_STAGES: Record<number, ChapterOneStageVisual> = {
  1: { background: "fried-station", normals: ["fried-soboro"], boss: "pure-roll" },
  2: { background: "fried-station", normals: ["kimchi-rice-ball"], boss: "pure-roll" },
  3: { background: "fried-station", normals: ["fried-soboro", "kimchi-rice-ball"], boss: "pure-roll" },
  4: { background: "bakery-shop", normals: ["bomunsan-whirlwind"], boss: "peach-siru" },
  5: { background: "bakery-shop", normals: ["curry-croquette"], boss: "peach-siru" },
  6: { background: "bakery-shop", normals: ["bomunsan-whirlwind", "curry-croquette"], boss: "peach-siru" },
  7: { background: "oven-cellar", normals: ["toyo-bread"], boss: "crepe" },
  8: { background: "oven-cellar", normals: ["strawberry-mochi-tart"], boss: "crepe" },
  9: { background: "oven-cellar", normals: ["toyo-bread", "strawberry-mochi-tart"], boss: "crepe" },
  10: { background: "oven-cellar", normals: [], boss: "strawberry-siru" },
};

const CHAPTER_TEN_STAGES: Record<number, ChapterOneStageVisual> = {
  1: { background: "yuseong-campus-lobby", normals: ["pork-kimchi-stir-fry"], boss: "tuna-mayo-spam-rice-captain" },
  2: { background: "yuseong-campus-lobby", normals: ["fried-spring-roll"], boss: "tuna-mayo-spam-rice-captain" },
  3: { background: "yuseong-campus-lobby", normals: ["pork-kimchi-stir-fry", "fried-spring-roll"], boss: "tuna-mayo-spam-rice-captain" },
  4: { background: "yuseong-campus-front", normals: ["egg-soup"], boss: "ttukbaegi-kimchi-stew-general" },
  5: { background: "yuseong-campus-front", normals: ["potato-croquette"], boss: "ttukbaegi-kimchi-stew-general" },
  6: { background: "yuseong-campus-front", normals: ["egg-soup", "potato-croquette"], boss: "ttukbaegi-kimchi-stew-general" },
  7: { background: "yuseong-campus-cafeteria", normals: ["pork-mushroom-stir-fry"], boss: "banquet-noodle-master" },
  8: { background: "yuseong-campus-cafeteria", normals: ["sausage-vegetable-stir-fry"], boss: "banquet-noodle-master" },
  9: { background: "yuseong-campus-cafeteria", normals: ["pork-mushroom-stir-fry", "sausage-vegetable-stir-fry"], boss: "banquet-noodle-master" },
  10: { background: "yuseong-campus-walking-trail", normals: [], boss: "dried-pollock-seolleongtang-chief" },
};

const CHAPTER_STAGES: Record<number, Record<number, ChapterOneStageVisual>> = {
  1: CHAPTER_ONE_STAGES,
  2: CHAPTER_TWO_STAGES,
  3: CHAPTER_THREE_STAGES,
  4: CHAPTER_FOUR_STAGES,
  5: CHAPTER_FIVE_STAGES,
  7: CHAPTER_SEVEN_STAGES,
  8: CHAPTER_EIGHT_STAGES,
  9: CHAPTER_NINE_STAGES,
  10: CHAPTER_TEN_STAGES,
};

const BACKGROUND_ASSETS: Record<BattleBackgroundId, string> = {
  "melon-glasshouse": `${CHAPTER_ONE_BASE}/backgrounds/melon-glasshouse.png`,
  "pineapple-plantation": `${CHAPTER_ONE_BASE}/backgrounds/pineapple-plantation.png`,
  "durian-riverside-packing-house": `${CHAPTER_ONE_BASE}/backgrounds/durian-riverside-packing-house.png`,
  "watermelon-irrigation-field": `${CHAPTER_ONE_BASE}/backgrounds/watermelon-irrigation-field.png`,
  "steam-teapot-locomotive-roundhouse": `${CHAPTER_TWO_BASE}/backgrounds/steam-teapot-locomotive-roundhouse.png`,
  "bubble-tea-after-hours-cafe": `${CHAPTER_TWO_BASE}/backgrounds/bubble-tea-after-hours-cafe.png`,
  "cocktail-shaker-circus-workshop": `${CHAPTER_TWO_BASE}/backgrounds/cocktail-shaker-circus-workshop.png`,
  "vending-machine-rainy-delivery-yard": `${CHAPTER_TWO_BASE}/backgrounds/vending-machine-rainy-delivery-yard.png`,
  "butter-dough-molding-room": `${CHAPTER_THREE_BASE}/backgrounds/butter-dough-molding-room.png`,
  "ruby-jam-injection-room": `${CHAPTER_THREE_BASE}/backgrounds/ruby-jam-injection-room.png`,
  "cookie-tin-packaging-warehouse": `${CHAPTER_THREE_BASE}/backgrounds/cookie-tin-packaging-warehouse.png`,
  "biscuit-royal-banquet-hall": `${CHAPTER_THREE_BASE}/backgrounds/biscuit-royal-banquet-hall.png`,
  "sushi-morning": `${CHAPTER_FOUR_BASE}/backgrounds/morning.png`,
  "sushi-lunch": `${CHAPTER_FOUR_BASE}/backgrounds/lunch.png`,
  "sushi-evening": `${CHAPTER_FOUR_BASE}/backgrounds/evening.png`,
  "sushi-predawn": `${CHAPTER_FOUR_BASE}/backgrounds/predawn.png`,
  "jeon-iron-griddle": `${CHAPTER_FIVE_BASE}/backgrounds/jeon-iron-griddle.png`,
  "scallion-batter-table": `${CHAPTER_FIVE_BASE}/backgrounds/scallion-batter-table.png`,
  "makgeolli-crock-courtyard": `${CHAPTER_FIVE_BASE}/backgrounds/makgeolli-crock-courtyard.png`,
  "rainy-market-jeon-stall": `${CHAPTER_FIVE_BASE}/backgrounds/rainy-market-jeon-stall.png`,
  "red-lantern-banquet-hall": `${CHAPTER_SEVEN_BASE}/backgrounds/red-lantern-banquet-hall.png`,
  "wok-kitchen": `${CHAPTER_SEVEN_BASE}/backgrounds/wok-kitchen.png`,
  "dim-sum-teahouse": `${CHAPTER_SEVEN_BASE}/backgrounds/dim-sum-teahouse.png`,
  "mala-night-market": `${CHAPTER_SEVEN_BASE}/backgrounds/mala-night-market.png`,
  "chimaek-cheering-deck": `${CHAPTER_EIGHT_BASE}/backgrounds/chimaek-cheering-deck.png`,
  "home-run-hotdog-concession": `${CHAPTER_EIGHT_BASE}/backgrounds/home-run-hotdog-concession.png`,
  "bullpen-tteokbokki-pocha": `${CHAPTER_EIGHT_BASE}/backgrounds/bullpen-tteokbokki-pocha.png`,
  "night-homeplate-food-festival": `${CHAPTER_EIGHT_BASE}/backgrounds/night-homeplate-food-festival.png`,
  "fried-station": `${CHAPTER_NINE_BASE}/backgrounds/fried-station.png`,
  "bakery-shop": `${CHAPTER_NINE_BASE}/backgrounds/bakery-shop.png`,
  "oven-cellar": `${CHAPTER_NINE_BASE}/backgrounds/oven-cellar.png`,
  "yuseong-campus-lobby": `${CHAPTER_TEN_BASE}/backgrounds/yuseong-campus-lobby.png`,
  "yuseong-campus-front": `${CHAPTER_TEN_BASE}/backgrounds/yuseong-campus-front.png`,
  "yuseong-campus-cafeteria": `${CHAPTER_TEN_BASE}/backgrounds/yuseong-campus-cafeteria.png`,
  "yuseong-campus-walking-trail": `${CHAPTER_TEN_BASE}/backgrounds/yuseong-campus-walking-trail.png`,
};

const CHAPTER_ONE_MONSTER_IDS = new Set<BattleMonsterId>([
  "tangerine", "strawberry-scout", "banana", "peach", "cherry", "grape", "durian-tyrant", "melon-noble", "watermelon-ssireum-king", "pineapple-general",
]);

const CHAPTER_TWO_MONSTER_IDS = new Set<BattleMonsterId>([
  "water-drop-slime", "milk-carton-healer", "electrolyte-runner", "coffee-bean-charger", "soda-bottle-bomber", "juice-pouch-shooter", "bubble-tea-kraken", "steam-teapot-duke", "cocktail-shaker-jester", "vending-machine-king",
]);

const CHAPTER_THREE_MONSTER_IDS = new Set<BattleMonsterId>([
  "butter-ring-guard", "person-butter-cookie-fighter", "sandwich-cookie-shieldbearer", "jam-thumbprint-rogue", "checkerboard-cookie-golem", "chocolate-chip-charger", "royal-assortment-gift-golem", "ruby-jam-sand-queen", "cookie-tin-bulwark-knight", "tea-time-biscuit-count",
]);

const CHAPTER_FOUR_MONSTER_IDS = new Set<BattleMonsterId>([
  "tamago-nigiri", "flatfish-nigiri", "shrimp-nigiri", "salmon-nigiri", "inari-sushi", "tuna-nigiri", "crab-gunkan-chief", "mackerel-nigiri-captain", "fatty-tuna-nigiri-boss", "futomaki-king",
]);

const CHAPTER_FIVE_MONSTER_IDS = new Set<BattleMonsterId>([
  "chili-jeon", "kimchi-jeon", "perilla-jeon", "shrimp-jeon-tail-soldier", "zucchini-jeon", "meat-jeon", "mung-bean-jeon-armored-commander", "chive-jeon-swordsman", "seafood-scallion-jeon-octopus-general", "golden-assorted-jeon-king",
]);

const CHAPTER_SEVEN_MONSTER_IDS = new Set<BattleMonsterId>([
  "fried-dumpling", "guobaorou", "danmuji", "mapo-tofu", "menbosha", "tangsuyuk", "dongpo-pork-general", "malatang-cauldron", "jjajangmyeon-master", "jjamppong-king",
]);

const CHAPTER_EIGHT_MONSTER_IDS = new Set<BattleMonsterId>([
  "flat-dumpling", "pot-ramen", "dakgangjeong", "barogejip-tteokbokki", "fishcake-skewer", "paopao-shrimp-dumpling", "wapang-slugger", "one-shot-chicken-tower", "cheese-poutine-catcher", "cream-shrimp-captain",
]);

const CHAPTER_NINE_MONSTER_IDS = new Set<BattleMonsterId>([
  "kimchi-rice-ball", "strawberry-mochi-tart", "bomunsan-whirlwind", "curry-croquette", "toyo-bread", "fried-soboro", "strawberry-siru", "peach-siru", "pure-roll", "crepe",
]);

const CHAPTER_TEN_MONSTER_IDS = new Set<BattleMonsterId>([
  "potato-croquette", "egg-soup", "pork-kimchi-stir-fry", "pork-mushroom-stir-fry", "sausage-vegetable-stir-fry", "fried-spring-roll", "ttukbaegi-kimchi-stew-general", "banquet-noodle-master", "tuna-mayo-spam-rice-captain", "dried-pollock-seolleongtang-chief",
]);

export const MONSTER_NAMES: Record<BattleMonsterId, string> = {
  /* 1. 과일가게 */
  "tangerine": "귤",
  "strawberry-scout": "딸기",
  "banana": "바나나",
  "peach": "복숭아",
  "cherry": "체리",
  "grape": "포도알",
  "durian-tyrant": "두리안 폭군",
  "melon-noble": "멜론 귀족",
  "watermelon-ssireum-king": "수박 씨름왕",
  "pineapple-general": "파인애플 장군",
  /* 2. 음료 */
  "water-drop-slime": "생수방울 슬라임",
  "milk-carton-healer": "우유팩 치유병",
  "electrolyte-runner": "이온음료 전기병",
  "coffee-bean-charger": "커피콩 돌격병",
  "soda-bottle-bomber": "탄산병 폭탄병",
  "juice-pouch-shooter": "파우치 주스 사수",
  "bubble-tea-kraken": "버블티 크라켄",
  "steam-teapot-duke": "증기 티포트 공작",
  "cocktail-shaker-jester": "칵테일 셰이커 광대",
  "vending-machine-king": "폭주 자판기 대왕",
  /* 3. 쿠키 */
  "butter-ring-guard": "버터링 방패병",
  "person-butter-cookie-fighter": "사람 모양 버터쿠키",
  "sandwich-cookie-shieldbearer": "샌드쿠키 방패병",
  "jam-thumbprint-rogue": "잼 썸프린트 도적",
  "checkerboard-cookie-golem": "체커쿠키 골렘",
  "chocolate-chip-charger": "초코칩 돌격병",
  "royal-assortment-gift-golem": "로열 어소트먼트 선물 골렘",
  "ruby-jam-sand-queen": "루비잼 샌드 여왕",
  "cookie-tin-bulwark-knight": "쿠키통 철벽기사",
  "tea-time-biscuit-count": "티타임 비스킷 백작",
  /* 4. 스시 */
  "tamago-nigiri": "계란초밥",
  "flatfish-nigiri": "광어초밥",
  "shrimp-nigiri": "새우초밥",
  "salmon-nigiri": "연어초밥",
  "inari-sushi": "유부초밥",
  "tuna-nigiri": "참치초밥",
  "crab-gunkan-chief": "게살군함 대장",
  "mackerel-nigiri-captain": "고등어초밥 대장",
  "fatty-tuna-nigiri-boss": "대뱃살초밥 보스",
  "futomaki-king": "후토마끼 왕",
  /* 5. 전 */
  "chili-jeon": "고추전",
  "kimchi-jeon": "김치전",
  "perilla-jeon": "깻잎전",
  "shrimp-jeon-tail-soldier": "새우전 꼬리병",
  "zucchini-jeon": "애호박전",
  "meat-jeon": "육전",
  "mung-bean-jeon-armored-commander": "녹두전 철갑대장",
  "chive-jeon-swordsman": "부추전 검객",
  "seafood-scallion-jeon-octopus-general": "해물파전 문어장군",
  "golden-assorted-jeon-king": "황금 모둠전 대왕",
  /* 7. 중식당 */
  "fried-dumpling": "군만두",
  "guobaorou": "꿔바로우",
  "danmuji": "단무지",
  "mapo-tofu": "마파두부",
  "menbosha": "멘보샤",
  "tangsuyuk": "탕수육",
  "dongpo-pork-general": "동파육 장군",
  "malatang-cauldron": "마라탕 가마",
  "jjajangmyeon-master": "짜장면 사부",
  "jjamppong-king": "짬뽕 대왕",
  /* 8. 야구장 먹거리 */
  "flat-dumpling": "납작만두",
  "pot-ramen": "냄비라면",
  "dakgangjeong": "닭강정",
  "barogejip-tteokbokki": "바로그집 떡볶이",
  "fishcake-skewer": "어묵꼬치",
  "paopao-shrimp-dumpling": "파오파오 새우만두",
  "wapang-slugger": "와팡 홈런거인",
  "one-shot-chicken-tower": "원샷치킨 타워",
  "cheese-poutine-catcher": "치즈푸틴 포수",
  "cream-shrimp-captain": "크림새우 지휘관",
  /* 9. 성심당 */
  "kimchi-rice-ball": "김치찹쌀주먹밥",
  "strawberry-mochi-tart": "딸기모찌타르트",
  "bomunsan-whirlwind": "보문산회오리",
  "curry-croquette": "카레고로케",
  "toyo-bread": "토요빵",
  "fried-soboro": "튀김소보로",
  "strawberry-siru": "딸기 시루",
  "peach-siru": "복숭아 시루",
  "pure-roll": "순수롤",
  "crepe": "크레이프",
  /* 10. 싸피 */
  "potato-croquette": "감자크로켓",
  "egg-soup": "계란국",
  "pork-kimchi-stir-fry": "돈육김치볶음",
  "pork-mushroom-stir-fry": "돈육버섯볶음",
  "sausage-vegetable-stir-fry": "소시지야채볶음",
  "fried-spring-roll": "춘권튀김",
  "ttukbaegi-kimchi-stew-general": "뚝배기김치찌개 장군",
  "banquet-noodle-master": "잔치국수 면발도사",
  "tuna-mayo-spam-rice-captain": "참치마요스팸덮밥 대장",
  "dried-pollock-seolleongtang-chief": "황태설렁탕 설산대장",
  /* 배치에서 빠졌지만 저장된 세이브가 참조할 수 있어 남긴다 */
  "cucumber-maki": "오이마키",
};

const MONSTER_SPRITES: Record<BattleMonsterId, MonsterSpriteMeta> = {
  /* 1. 과일가게 */
  "tangerine": { cellSize: 492, displayScale: 0.3157, groundFromBottom: 18, contentLeft: 22, contentRight: 469, contentTop: 142, contentBottom: 474 },
  "strawberry-scout": { cellSize: 490, displayScale: 0.2895, groundFromBottom: 20, contentLeft: 22, contentRight: 467, contentTop: 108, contentBottom: 470 },
  "banana": { cellSize: 492, displayScale: 0.3316, groundFromBottom: 17, contentLeft: 22, contentRight: 469, contentTop: 159, contentBottom: 475 },
  "peach": { cellSize: 458, displayScale: 0.3185, groundFromBottom: 17, contentLeft: 20, contentRight: 437, contentTop: 112, contentBottom: 441 },
  "cherry": { cellSize: 492, displayScale: 0.3055, groundFromBottom: 18, contentLeft: 22, contentRight: 469, contentTop: 131, contentBottom: 474 },
  "grape": { cellSize: 492, displayScale: 0.2936, groundFromBottom: 18, contentLeft: 22, contentRight: 469, contentTop: 117, contentBottom: 474 },
  "durian-tyrant": { cellSize: 310, displayScale: 1.2254, groundFromBottom: 11, contentLeft: 14, contentRight: 295, contentTop: 101, contentBottom: 299 },
  "melon-noble": { cellSize: 348, displayScale: 1.0832, groundFromBottom: 13, contentLeft: 15, contentRight: 332, contentTop: 111, contentBottom: 335 },
  "watermelon-ssireum-king": { cellSize: 322, displayScale: 1.1779, groundFromBottom: 12, contentLeft: 14, contentRight: 306, contentTop: 104, contentBottom: 310 },
  "pineapple-general": { cellSize: 312, displayScale: 1.0736, groundFromBottom: 13, contentLeft: 14, contentRight: 296, contentTop: 73, contentBottom: 299 },
  /* 2. 음료 */
  "water-drop-slime": { cellSize: 450, displayScale: 0.3584, groundFromBottom: 17, contentLeft: 20, contentRight: 428, contentTop: 129, contentBottom: 433 },
  "milk-carton-healer": { cellSize: 394, displayScale: 0.3509, groundFromBottom: 17, contentLeft: 17, contentRight: 376, contentTop: 75, contentBottom: 377 },
  "electrolyte-runner": { cellSize: 422, displayScale: 0.2994, groundFromBottom: 17, contentLeft: 19, contentRight: 402, contentTop: 68, contentBottom: 405 },
  "coffee-bean-charger": { cellSize: 424, displayScale: 0.361, groundFromBottom: 16, contentLeft: 19, contentRight: 403, contentTop: 114, contentBottom: 408 },
  "soda-bottle-bomber": { cellSize: 416, displayScale: 0.3311, groundFromBottom: 17, contentLeft: 18, contentRight: 397, contentTop: 83, contentBottom: 399 },
  "juice-pouch-shooter": { cellSize: 408, displayScale: 0.3344, groundFromBottom: 16, contentLeft: 18, contentRight: 389, contentTop: 94, contentBottom: 392 },
  "bubble-tea-kraken": { cellSize: 534, displayScale: 0.617, groundFromBottom: 20, contentLeft: 24, contentRight: 509, contentTop: 127, contentBottom: 514 },
  "steam-teapot-duke": { cellSize: 844, displayScale: 0.7164, groundFromBottom: 19, contentLeft: 38, contentRight: 805, contentTop: 481, contentBottom: 825 },
  "cocktail-shaker-jester": { cellSize: 500, displayScale: 0.6452, groundFromBottom: 20, contentLeft: 22, contentRight: 476, contentTop: 93, contentBottom: 480 },
  "vending-machine-king": { cellSize: 554, displayScale: 0.678, groundFromBottom: 20, contentLeft: 25, contentRight: 527, contentTop: 162, contentBottom: 534 },
  /* 3. 쿠키 */
  "butter-ring-guard": { cellSize: 386, displayScale: 0.46, groundFromBottom: 12, contentLeft: 17, contentRight: 368, contentTop: 162, contentBottom: 374 },
  "person-butter-cookie-fighter": { cellSize: 386, displayScale: 0.5, groundFromBottom: 13, contentLeft: 17, contentRight: 368, contentTop: 132, contentBottom: 373 },
  "sandwich-cookie-shieldbearer": { cellSize: 386, displayScale: 0.4, groundFromBottom: 14, contentLeft: 17, contentRight: 368, contentTop: 122, contentBottom: 372 },
  "jam-thumbprint-rogue": { cellSize: 386, displayScale: 0.4, groundFromBottom: 11, contentLeft: 17, contentRight: 368, contentTop: 158, contentBottom: 375 },
  "checkerboard-cookie-golem": { cellSize: 386, displayScale: 0.41, groundFromBottom: 13, contentLeft: 17, contentRight: 368, contentTop: 137, contentBottom: 373 },
  "chocolate-chip-charger": { cellSize: 386, displayScale: 0.4, groundFromBottom: 14, contentLeft: 17, contentRight: 368, contentTop: 132, contentBottom: 372 },
  "royal-assortment-gift-golem": { cellSize: 518, displayScale: 0.74, groundFromBottom: 20, contentLeft: 23, contentRight: 494, contentTop: 109, contentBottom: 498 },
  "ruby-jam-sand-queen": { cellSize: 492, displayScale: 0.86, groundFromBottom: 17, contentLeft: 22, contentRight: 469, contentTop: 144, contentBottom: 475 },
  "cookie-tin-bulwark-knight": { cellSize: 474, displayScale: 0.83, groundFromBottom: 15, contentLeft: 21, contentRight: 452, contentTop: 170, contentBottom: 459 },
  "tea-time-biscuit-count": { cellSize: 466, displayScale: 1.05, groundFromBottom: 13, contentLeft: 21, contentRight: 444, contentTop: 196, contentBottom: 453 },
  /* 4. 스시 */
  "tamago-nigiri": { cellSize: 386, displayScale: 0.45, groundFromBottom: 13, contentLeft: 17, contentRight: 368, contentTop: 139, contentBottom: 373 },
  "flatfish-nigiri": { cellSize: 386, displayScale: 0.45, groundFromBottom: 11, contentLeft: 17, contentRight: 368, contentTop: 164, contentBottom: 375 },
  "shrimp-nigiri": { cellSize: 386, displayScale: 0.42, groundFromBottom: 15, contentLeft: 17, contentRight: 368, contentTop: 111, contentBottom: 371 },
  "salmon-nigiri": { cellSize: 386, displayScale: 0.45, groundFromBottom: 13, contentLeft: 17, contentRight: 368, contentTop: 145, contentBottom: 373 },
  "inari-sushi": { cellSize: 386, displayScale: 0.42, groundFromBottom: 15, contentLeft: 17, contentRight: 368, contentTop: 109, contentBottom: 371 },
  "tuna-nigiri": { cellSize: 386, displayScale: 0.48, groundFromBottom: 12, contentLeft: 17, contentRight: 368, contentTop: 170, contentBottom: 374 },
  "crab-gunkan-chief": { cellSize: 492, displayScale: 0.83, groundFromBottom: 18, contentLeft: 22, contentRight: 469, contentTop: 152, contentBottom: 474 },
  "mackerel-nigiri-captain": { cellSize: 448, displayScale: 0.86, groundFromBottom: 15, contentLeft: 20, contentRight: 427, contentTop: 157, contentBottom: 433 },
  "fatty-tuna-nigiri-boss": { cellSize: 466, displayScale: 0.92, groundFromBottom: 14, contentLeft: 21, contentRight: 444, contentTop: 202, contentBottom: 452 },
  "futomaki-king": { cellSize: 554, displayScale: 0.84, groundFromBottom: 16, contentLeft: 25, contentRight: 528, contentTop: 246, contentBottom: 538 },
  /* 5. 전 */
  "chili-jeon": { cellSize: 306, displayScale: 0.7813, groundFromBottom: 11, contentLeft: 13, contentRight: 292, contentTop: 88, contentBottom: 295 },
  "kimchi-jeon": { cellSize: 304, displayScale: 0.495, groundFromBottom: 11, contentLeft: 13, contentRight: 290, contentTop: 92, contentBottom: 293 },
  "perilla-jeon": { cellSize: 302, displayScale: 0.5882, groundFromBottom: 9, contentLeft: 13, contentRight: 288, contentTop: 114, contentBottom: 293 },
  "shrimp-jeon-tail-soldier": { cellSize: 398, displayScale: 0.4082, groundFromBottom: 13, contentLeft: 18, contentRight: 379, contentTop: 126, contentBottom: 385 },
  "zucchini-jeon": { cellSize: 294, displayScale: 0.5682, groundFromBottom: 10, contentLeft: 13, contentRight: 280, contentTop: 103, contentBottom: 284 },
  "meat-jeon": { cellSize: 306, displayScale: 0.5263, groundFromBottom: 11, contentLeft: 13, contentRight: 292, contentTop: 80, contentBottom: 295 },
  "mung-bean-jeon-armored-commander": { cellSize: 552, displayScale: 0.7038, groundFromBottom: 20, contentLeft: 25, contentRight: 525, contentTop: 166, contentBottom: 532 },
  "chive-jeon-swordsman": { cellSize: 582, displayScale: 0.75, groundFromBottom: 20, contentLeft: 26, contentRight: 555, contentTop: 169, contentBottom: 562 },
  "seafood-scallion-jeon-octopus-general": { cellSize: 590, displayScale: 0.8108, groundFromBottom: 18, contentLeft: 26, contentRight: 563, contentTop: 217, contentBottom: 572 },
  "golden-assorted-jeon-king": { cellSize: 558, displayScale: 0.6685, groundFromBottom: 21, contentLeft: 25, contentRight: 531, contentTop: 145, contentBottom: 537 },
  /* 7. 중식당 */
  "fried-dumpling": { cellSize: 316, displayScale: 0.4874, groundFromBottom: 11, contentLeft: 14, contentRight: 300, contentTop: 90, contentBottom: 305 },
  "guobaorou": { cellSize: 326, displayScale: 0.4313, groundFromBottom: 13, contentLeft: 14, contentRight: 311, contentTop: 70, contentBottom: 313 },
  "danmuji": { cellSize: 326, displayScale: 0.4658, groundFromBottom: 12, contentLeft: 14, contentRight: 311, contentTop: 89, contentBottom: 314 },
  "mapo-tofu": { cellSize: 342, displayScale: 0.337, groundFromBottom: 16, contentLeft: 17, contentRight: 324, contentTop: 15, contentBottom: 326 },
  "menbosha": { cellSize: 320, displayScale: 0.5112, groundFromBottom: 11, contentLeft: 14, contentRight: 304, contentTop: 104, contentBottom: 309 },
  "tangsuyuk": { cellSize: 328, displayScale: 0.3529, groundFromBottom: 15, contentLeft: 14, contentRight: 312, contentTop: 16, contentBottom: 313 },
  "dongpo-pork-general": { cellSize: 344, displayScale: 1.1029, groundFromBottom: 13, contentLeft: 15, contentRight: 327, contentTop: 111, contentBottom: 331 },
  "malatang-cauldron": { cellSize: 340, displayScale: 0.8454, groundFromBottom: 15, contentLeft: 15, contentRight: 324, contentTop: 38, contentBottom: 325 },
  "jjajangmyeon-master": { cellSize: 364, displayScale: 0.8635, groundFromBottom: 15, contentLeft: 16, contentRight: 347, contentTop: 68, contentBottom: 349 },
  "jjamppong-king": { cellSize: 342, displayScale: 1.0195, groundFromBottom: 13, contentLeft: 15, contentRight: 326, contentTop: 91, contentBottom: 329 },
  /* 8. 야구장 먹거리 */
  "flat-dumpling": { cellSize: 404, displayScale: 0.4762, groundFromBottom: 14, contentLeft: 18, contentRight: 384, contentTop: 134, contentBottom: 390 },
  "pot-ramen": { cellSize: 388, displayScale: 0.4115, groundFromBottom: 14, contentLeft: 17, contentRight: 370, contentTop: 130, contentBottom: 374 },
  "dakgangjeong": { cellSize: 406, displayScale: 0.4975, groundFromBottom: 11, contentLeft: 18, contentRight: 387, contentTop: 184, contentBottom: 395 },
  "barogejip-tteokbokki": { cellSize: 378, displayScale: 0.463, groundFromBottom: 11, contentLeft: 17, contentRight: 359, contentTop: 150, contentBottom: 367 },
  "fishcake-skewer": { cellSize: 450, displayScale: 0.3891, groundFromBottom: 15, contentLeft: 20, contentRight: 428, contentTop: 161, contentBottom: 435 },
  "paopao-shrimp-dumpling": { cellSize: 426, displayScale: 0.6211, groundFromBottom: 11, contentLeft: 19, contentRight: 405, contentTop: 235, contentBottom: 415 },
  "wapang-slugger": { cellSize: 544, displayScale: 0.7869, groundFromBottom: 15, contentLeft: 24, contentRight: 519, contentTop: 242, contentBottom: 529 },
  "one-shot-chicken-tower": { cellSize: 546, displayScale: 0.7921, groundFromBottom: 15, contentLeft: 24, contentRight: 520, contentTop: 246, contentBottom: 531 },
  "cheese-poutine-catcher": { cellSize: 844, displayScale: 0.8247, groundFromBottom: 16, contentLeft: 38, contentRight: 805, contentTop: 546, contentBottom: 828 },
  "cream-shrimp-captain": { cellSize: 536, displayScale: 0.7973, groundFromBottom: 16, contentLeft: 24, contentRight: 511, contentTop: 236, contentBottom: 520 },
  /* 9. 성심당 */
  "kimchi-rice-ball": { cellSize: 84, displayScale: 1.4, groundFromBottom: 4, contentLeft: 3, contentRight: 80, contentTop: 24, contentBottom: 80 },
  "strawberry-mochi-tart": { cellSize: 130, displayScale: 1.12, groundFromBottom: 6, contentLeft: 5, contentRight: 124, contentTop: 44, contentBottom: 124 },
  "bomunsan-whirlwind": { cellSize: 152, displayScale: 0.96, groundFromBottom: 5, contentLeft: 6, contentRight: 145, contentTop: 64, contentBottom: 147 },
  "curry-croquette": { cellSize: 142, displayScale: 1.08, groundFromBottom: 4, contentLeft: 6, contentRight: 135, contentTop: 61, contentBottom: 138 },
  "toyo-bread": { cellSize: 162, displayScale: 1.04, groundFromBottom: 4, contentLeft: 7, contentRight: 154, contentTop: 93, contentBottom: 158 },
  "fried-soboro": { cellSize: 152, displayScale: 1.08, groundFromBottom: 5, contentLeft: 6, contentRight: 145, contentTop: 73, contentBottom: 147 },
  "strawberry-siru": { cellSize: 322, displayScale: 1.2771, groundFromBottom: 13, contentLeft: 14, contentRight: 306, contentTop: 68, contentBottom: 309 },
  "peach-siru": { cellSize: 312, displayScale: 1.1275, groundFromBottom: 12, contentLeft: 14, contentRight: 296, contentTop: 84, contentBottom: 300 },
  "pure-roll": { cellSize: 362, displayScale: 0.9404, groundFromBottom: 12, contentLeft: 16, contentRight: 344, contentTop: 140, contentBottom: 350 },
  "crepe": { cellSize: 390, displayScale: 1.275, groundFromBottom: 11, contentLeft: 17, contentRight: 371, contentTop: 172, contentBottom: 379 },
  /* 10. 싸피 */
  "potato-croquette": { cellSize: 436, displayScale: 0.4115, groundFromBottom: 14, contentLeft: 19, contentRight: 415, contentTop: 180, contentBottom: 422 },
  "egg-soup": { cellSize: 380, displayScale: 0.3571, groundFromBottom: 15, contentLeft: 17, contentRight: 362, contentTop: 95, contentBottom: 365 },
  "pork-kimchi-stir-fry": { cellSize: 400, displayScale: 0.6098, groundFromBottom: 11, contentLeft: 18, contentRight: 381, contentTop: 178, contentBottom: 389 },
  "pork-mushroom-stir-fry": { cellSize: 416, displayScale: 0.4237, groundFromBottom: 14, contentLeft: 18, contentRight: 397, contentTop: 133, contentBottom: 402 },
  "sausage-vegetable-stir-fry": { cellSize: 446, displayScale: 0.4065, groundFromBottom: 15, contentLeft: 20, contentRight: 424, contentTop: 159, contentBottom: 431 },
  "fried-spring-roll": { cellSize: 412, displayScale: 0.3597, groundFromBottom: 14, contentLeft: 18, contentRight: 392, contentTop: 121, contentBottom: 398 },
  "ttukbaegi-kimchi-stew-general": { cellSize: 602, displayScale: 0.8276, groundFromBottom: 15, contentLeft: 27, contentRight: 573, contentTop: 298, contentBottom: 587 },
  "banquet-noodle-master": { cellSize: 582, displayScale: 0.8, groundFromBottom: 17, contentLeft: 26, contentRight: 555, contentTop: 226, contentBottom: 565 },
  "tuna-mayo-spam-rice-captain": { cellSize: 544, displayScale: 0.8163, groundFromBottom: 16, contentLeft: 24, contentRight: 518, contentTop: 213, contentBottom: 528 },
  "dried-pollock-seolleongtang-chief": { cellSize: 546, displayScale: 0.7317, groundFromBottom: 18, contentLeft: 24, contentRight: 520, contentTop: 200, contentBottom: 528 },
  /* 배치에서 빠졌지만 저장된 세이브가 참조할 수 있어 남긴다 */
  "cucumber-maki": {cellSize: 256, displayScale: 0.78, groundFromBottom: 28, contentLeft: 71, contentRight: 185, contentTop: 103, contentBottom: 228},
};

export function stageVisual(stageId: string): ChapterOneStageVisual {
  const match = /^stage\.(\d{2})-(\d{2})$/.exec(stageId);
  const chapter = Number(match?.[1] ?? 1);
  const stage = Number(match?.[2] ?? 1);
  return CHAPTER_STAGES[chapter]?.[stage] ?? CHAPTER_ONE_STAGES[stage] ?? CHAPTER_ONE_STAGES[1];
}

export function backgroundUrl(stageId: string): string {
  return BACKGROUND_ASSETS[stageVisual(stageId).background];
}

export function backgroundAsset(backgroundId: string | null | undefined, stageId: string): string {
  const fallback = stageVisual(stageId).background;
  const id = isBattleBackgroundId(backgroundId) ? backgroundId : fallback;
  return BACKGROUND_ASSETS[id];
}

export function monsterFor(stageId: string, enemyIndex: number, boss: boolean): ChapterOneMonsterId {
  const visual = stageVisual(stageId);
  if (boss || visual.normals.length === 0) return visual.boss;
  return visual.normals[(Math.max(1, enemyIndex) - 1) % visual.normals.length];
}

export function stageMonsterFor(
  stageId: string,
  enemyIndex: number,
  boss: boolean,
  normalMonsterIds?: string[],
  bossMonsterId?: string | null,
): ChapterOneMonsterId {
  const fallback = stageVisual(stageId);
  const normals = normalMonsterIds?.filter(isChapterOneMonsterId) ?? [];
  if (boss || (normalMonsterIds?.length === 0 && fallback.normals.length === 0)) return isChapterOneMonsterId(bossMonsterId) ? bossMonsterId : fallback.boss;
  const sequence = normals.length > 0 ? normals : fallback.normals;
  if (sequence.length === 0) return fallback.boss;
  return sequence[(Math.max(1, enemyIndex) - 1) % sequence.length];
}

function isChapterOneMonsterId(value: string | null | undefined): value is BattleMonsterId {
  return value !== null && value !== undefined && Object.hasOwn(MONSTER_NAMES, value);
}

function isBattleBackgroundId(value: string | null | undefined): value is BattleBackgroundId {
  return value !== null && value !== undefined && Object.hasOwn(BACKGROUND_ASSETS, value);
}

const MONSTER_BASES: Array<[Set<BattleMonsterId>, string]> = [
  [CHAPTER_TEN_MONSTER_IDS, CHAPTER_TEN_BASE],
  [CHAPTER_NINE_MONSTER_IDS, CHAPTER_NINE_BASE],
  [CHAPTER_EIGHT_MONSTER_IDS, CHAPTER_EIGHT_BASE],
  [CHAPTER_SEVEN_MONSTER_IDS, CHAPTER_SEVEN_BASE],
  [CHAPTER_FIVE_MONSTER_IDS, CHAPTER_FIVE_BASE],
  [CHAPTER_FOUR_MONSTER_IDS, CHAPTER_FOUR_BASE],
  [CHAPTER_THREE_MONSTER_IDS, CHAPTER_THREE_BASE],
  [CHAPTER_TWO_MONSTER_IDS, CHAPTER_TWO_BASE],
  [CHAPTER_ONE_MONSTER_IDS, CHAPTER_ONE_BASE],
];

export function monsterSheet(id: BattleMonsterId): string {
  const base = MONSTER_BASES.find(([ids]) => ids.has(id))?.[1] ?? CHAPTER_ONE_BASE;
  return `${base}/monsters/${id}/${id}-sprite-sheet-5x4.png`;
}

export function monsterSpriteMeta(id: BattleMonsterId): MonsterSpriteMeta {
  return MONSTER_SPRITES[id];
}

export type HeroMotion = "rest" | "run2" | "strike1" | "thrust1" | "death1";
export type MonsterMotion = "idle" | "move" | "attack" | "hit" | "death";
export type DamagePresentationKind = "basic" | "basic-critical" | "heavy" | "heavy-critical" | "dot";

export function damagePresentation(event: CombatRenderingEvent): DamagePresentationKind | undefined {
  if (event.damage == null || event.damage <= 0) return undefined;
  if (event.type === "DOT_TICK" && event.skillId === "active_dot") return "dot";
  if (event.type === "PLAYER_SKILL_IMPACT" && event.skillId === "active_heavy") return event.critical ? "heavy-critical" : "heavy";
  if (event.type === "PLAYER_ATTACK_IMPACT") return event.critical ? "basic-critical" : "basic";
  return undefined;
}

export function damageText(amount: number): string {
  return String(Math.max(0, Math.trunc(amount)));
}

export function monsterRenderKey(enemyIndex: number, boss: boolean): string {
  return `${enemyIndex}-${boss}`;
}

/*
 * 모든 모션을 미리 받는다. 예전에는 strike1·thrust1 만 받아서, 등장에 쓰는 run2 는
 * 프레임마다 그때그때 내려받느라 젓가락이 깨지며 나타났다.
 */
const heroCombatFrameUrls = (["rest", "run2", "strike1", "thrust1", "death1"] as const).flatMap((motion) =>
  Array.from({ length: 4 }, (_, frame) => heroFrame(motion, frame)),
);
const heroPreloadImages: HTMLImageElement[] = [];
let heroFramesPreloaded = false;
const monsterPreloadImages = new Map<BattleMonsterId, HTMLImageElement>();

export function preloadHeroCombatFrames(): void {
  if (heroFramesPreloaded || typeof Image === "undefined") return;
  heroFramesPreloaded = true;
  heroCombatFrameUrls.forEach((url) => {
    const image = new Image();
    image.src = url;
    heroPreloadImages.push(image);
  });
}

export function preloadMonsterSheet(id: BattleMonsterId): void {
  if (monsterPreloadImages.has(id) || typeof Image === "undefined") return;
  const image = new Image();
  image.src = monsterSheet(id);
  monsterPreloadImages.set(id, image);
  void image.decode?.().catch(() => undefined);
}

export function heroMotionFrame(motion: HeroMotion, elapsedMilliseconds: number): number {
  const interval = motion === "rest" ? 180 : motion === "run2" ? 110 : motion === "death1" ? 150 : 100;
  const frame = Math.floor(Math.max(0, elapsedMilliseconds) / interval);
  return motion === "rest" || motion === "run2" ? frame % 4 : Math.min(3, frame);
}

export function heroMotion(events: CombatRenderingEvent[], fighting: boolean): HeroMotion {
  if (!fighting) return "rest";
  const has = (type: string) => events.some((event) => event.type === type);
  if (has("PLAYER_DEFEATED") || has("BATTLE_FAILED")) return "death1";
  if (has("PLAYER_BASIC_ATTACK_STARTED")) return "strike1";
  const skillCast = events.findLast((event) => event.type === "PLAYER_SKILL_CAST_STARTED");
  if (skillCast) return heroSkillMotion(skillCast.skillId);
  return "rest";
}

export function heroMotionForEncounter(events: CombatRenderingEvent[], fighting: boolean, enemyIndex: number): HeroMotion {
  const latestType = events.at(-1)?.type;
  if (enemyIndex === 1 && (latestType === "ENEMY_SPAWNED" || latestType === "BOSS_SPAWNED")) return "run2";
  return heroMotion(events, fighting);
}

export function heroSkillMotion(skillId: string | null): HeroMotion {
  if (skillId === "active_heavy") return "strike1";
  if (skillId === "active_dot") return "thrust1";
  return "rest";
}

export function monsterMotion(events: CombatRenderingEvent[], fighting: boolean): MonsterMotion {
  if (!fighting) return "idle";
  const type = events.at(-1)?.type;
  if (type === "ENEMY_DEFEATED") return "death";
  if (type === "PLAYER_ATTACK_IMPACT" || type === "PLAYER_SKILL_IMPACT" || type === "DOT_TICK") return "hit";
  if (type === "ENEMY_ATTACK_STARTED" || type === "PLAYER_HIT") return "attack";
  if (type === "ENEMY_SPAWNED" || type === "BOSS_SPAWNED") return "move";
  return "idle";
}

export function heroFrame(motion: HeroMotion, frame: number): string {
  return `${HERO_BASE}/hero/${motion}_${String(frame + 1).padStart(2, "0")}.png?v=${HERO_ASSET_VERSION}`;
}
