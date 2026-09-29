// 콘텐츠 `imageUrl`이 아직 null인 동안 사용하는 로컬 치장 아트 매핑이다.
// 서버가 이미지를 내려주면 그 값이 항상 우선한다.
//
// 파일은 `public/cosmetics/item-icons`에 두고 경로 문자열로만 참조한다.
// 번들러 모듈로 import하면 66종 전부가 화면과 무관하게 로드되어,
// 개발 서버에서 다른 이미지 요청을 밀어내기 때문이다.

const ICON_BASE = "/cosmetics/item-icons";

const SET_ART_SLUGS: Record<string, string> = {
  // 노말
  "cosmetic-set-01": "leather-guard",
  "cosmetic-set-02": "scrap-knight",
  "cosmetic-set-03": "bamboo-spear",
  "cosmetic-set-04": "nurse",
  // 희귀
  "cosmetic-set-05": "demon",
  "cosmetic-set-06": "angel",
  "cosmetic-set-07": "turtle-guardian",
  // 영웅
  "cosmetic-set-08": "chef",
  "cosmetic-set-09": "mage",
  "cosmetic-set-10": "yakgwa",
  // 전설
  "cosmetic-set-11": "sushi",
};

// 콘텐츠의 여섯 부위를 원본 아이콘 파일 이름에 연결한다. `BOTTOM`은 화면에서 무기로 쓴다.
const SLOT_SUFFIXES: Record<string, string> = {
  HEAD: "head",
  TOP: "top",
  BOTTOM: "bottom",
  WEAPON: "bottom",
  GLOVES: "gloves",
  SHOES: "shoes",
  CAPE: "cape",
};


/** 부위 아이콘. 콘텐츠 이미지가 있으면 그것을, 없으면 로컬 아트를 쓴다. */
export function cosmeticArtUrl(cosmetic?: { imageUrl?: string | null; setId?: string; slot?: string } | null): string | null {
  if (!cosmetic) return null;
  if (cosmetic.imageUrl) return cosmetic.imageUrl;
  const slug = cosmetic.setId ? SET_ART_SLUGS[cosmetic.setId] : undefined;
  const suffix = cosmetic.slot ? SLOT_SUFFIXES[cosmetic.slot] : undefined;
  return slug && suffix ? `${ICON_BASE}/${slug}-${suffix}.png` : null;
}

/** 세트 전신 프리뷰. 원본에 완성 프레임이 없는 세트는 null이다. */
export function cosmeticSetArtUrl(set?: { setId?: string; previewImageUrl?: string | null } | null): string | null {
  if (!set) return null;
  if (set.previewImageUrl) return set.previewImageUrl;
  const slug = set.setId ? SET_ART_SLUGS[set.setId] : undefined;
  return slug ? `${ICON_BASE}/${slug}-set.png` : null;
}

// 세트 폴더 이름을 그대로 쓴 한글 세트명. 콘텐츠 `displayName`이 비어 있을 때만 사용한다.
const SET_NAMES: Record<string, string> = {
  "leather-guard": "가죽경갑",
  "scrap-knight": "고물기사",
  "bamboo-spear": "죽창무사",
  nurse: "간호사",
  chef: "요리사",
  demon: "악마",
  fishbread: "붕어빵",
  yakgwa: "약과",
  sushi: "스시야",
  mage: "마법사",
  "turtle-guardian": "거북이 수호자",
  angel: "천사",
};
const SLOT_NAMES: Record<string, string> = { HEAD: "모자", TOP: "상의", BOTTOM: "무기", WEAPON: "무기", GLOVES: "장갑", SHOES: "신발", CAPE: "망토" };

/** 세트 이름. 콘텐츠 표시명이 없으면 원본 세트 폴더 이름을 쓴다. */
export function cosmeticSetName(set?: { setId?: string; displayName?: string | null } | null): string | null {
  if (set?.displayName) return set.displayName;
  const slug = set?.setId ? SET_ART_SLUGS[set.setId] : undefined;
  return slug ? SET_NAMES[slug] ?? null : null;
}

/** 치장 이름. 콘텐츠 표시명이 없으면 `세트 이름 + 부위 이름`으로 만든다. */
export function cosmeticDisplayName(cosmetic?: { cosmeticId?: string; displayName?: string | null; setId?: string; slot?: string } | null): string {
  if (cosmetic?.displayName) return cosmetic.displayName;
  const setName = cosmeticSetName(cosmetic);
  const slotName = cosmetic?.slot ? SLOT_NAMES[cosmetic.slot] : undefined;
  if (setName && slotName) return `${setName} ${slotName}`;
  return `치장 #${(cosmetic?.cosmeticId ?? "").slice(-3)}`;
}

// 착용 조합 미리보기. 원본 세트의 부위별 레이어를 같은 좌표계에 겹쳐 그린다.
// 레이어가 없는 세트/부위는 null이고, 호출부가 완성 이미지로 폴백한다.
const LAYER_BASE = "/cosmetics/layers";
// 아래에서 위로 쌓는 순서. 망토는 등 뒤, 무기는 손 앞이다.
// 그리는 차례다. 무기는 장갑보다 먼저 와야 손이 무기를 쥔 것처럼 보인다. 맨 뒤에 두면
// 칼자루가 장갑 위를 덮는다. 아트 매니페스트도 무기 다음에 손·장갑을 둔다.
export const COSMETIC_LAYER_ORDER = ["CAPE", "TOP", "SHOES", "BOTTOM", "GLOVES", "HEAD"] as const;
// 등에 걸치는 부위는 몸통보다 먼저 그린다. 맨몸 위에 얹으면 날개나 가방이
// 젓가락 몸을 뚫고 앞으로 나온다.
export const COSMETIC_BEHIND_BODY_SLOTS: ReadonlySet<string> = new Set(["CAPE"]);
// rest 자세 레이어가 없어 조합을 만들 수 없는 세트와 부위.
const LAYERS_MISSING: Record<string, ReadonlySet<string> | "all"> = {
  mage: new Set(["top", "gloves", "cape", "bottom"]),
};

/** 맨몸 베이스(장비 없는 젓가락). 조합 미리보기의 맨 아래 칸이다. */
export const COSMETIC_BASE_LAYER = `${LAYER_BASE}/base.png`;

/** 공통 팔 레이어. 민소매 상의가 맨몸의 팔을 덮을 때 다시 올린다. */
export const COSMETIC_ARMS_LAYER = `${LAYER_BASE}/arms.png`;
// 상의가 소매 없이 몸통만 덮는 세트. 그대로 두면 맨몸의 팔이 상의에 가려 사라진다.
// 팔 픽셀을 기준으로 상의가 덮는 비율을 재어 골랐다(간호사 50%, 천사 45%, 약과 41%, 악마 38%).
// 스시야는 27%지만 상의에 흰 소매가 그려져 있어 제외한다.
const SLEEVELESS_TOP_SLUGS: ReadonlySet<string> = new Set(["nurse", "angel", "yakgwa", "demon"]);

/** 이 상의를 입으면 맨몸의 팔이 가려져 팔 레이어를 다시 올려야 하는지. */
export function cosmeticTopHidesArms(cosmetic?: { setId?: string; slot?: string } | null): boolean {
  if (cosmetic?.slot !== "TOP") return false;
  const slug = cosmetic.setId ? SET_ART_SLUGS[cosmetic.setId] : undefined;
  return Boolean(slug && SLEEVELESS_TOP_SLUGS.has(slug));
}

// 자루가 손에 겹치게 그려진 무기. 손 위에 얹으면 젓가락 손을 덮어 버리므로
// 몸통보다 먼저 그려 손이 자루를 쥔 것처럼 보이게 한다.
const WEAPON_BEHIND_HANDS_SLUGS: ReadonlySet<string> = new Set(["sushi", "angel", "demon"]);

/** 이 무기를 몸통 뒤에 그려야 하는지. */
export function cosmeticWeaponBehindHands(cosmetic?: { setId?: string; slot?: string } | null): boolean {
  if (cosmetic?.slot !== "BOTTOM" && cosmetic?.slot !== "WEAPON") return false;
  const slug = cosmetic.setId ? SET_ART_SLUGS[cosmetic.setId] : undefined;
  return Boolean(slug && WEAPON_BEHIND_HANDS_SLUGS.has(slug));
}

/** 기본 무기(나무검). 무기 치장을 끼지 않았을 때 손에 쥐여 준다. */
export const COSMETIC_DEFAULT_WEAPON_LAYER = `${LAYER_BASE}/weapon-default.png`;

// 상의가 몸통과 소매로 나뉜 세트. 소매는 팔보다 위에 올려야 팔에 덮이지 않는다.
const TOP_OVER_ARMS_SLUGS: ReadonlySet<string> = new Set(["nurse"]);

/** 팔 위에 덧그리는 상의 소매. 없으면 null이다. */
export function cosmeticTopOverArmsUrl(cosmetic?: { setId?: string; slot?: string } | null): string | null {
  if (cosmetic?.slot !== "TOP") return null;
  const slug = cosmetic.setId ? SET_ART_SLUGS[cosmetic.setId] : undefined;
  return slug && TOP_OVER_ARMS_SLUGS.has(slug) ? `${LAYER_BASE}/${slug}-top-over.png` : null;
}

/** 한 부위의 착용 레이어. 원본에 레이어가 없으면 null이다. */
export function cosmeticLayerUrl(cosmetic?: { setId?: string; slot?: string } | null): string | null {
  const slug = cosmetic?.setId ? SET_ART_SLUGS[cosmetic.setId] : undefined;
  const suffix = cosmetic?.slot ? SLOT_SUFFIXES[cosmetic.slot] : undefined;
  if (!slug || !suffix) return null;
  const missing = LAYERS_MISSING[slug];
  if (missing === "all" || missing?.has(suffix)) return null;
  return `${LAYER_BASE}/${slug}-${suffix}.png`;
}

type ComposeCosmetic = { cosmeticId: string; setId: string; slot: string; imageUrl?: string | null };
type ComposeSet = { setId: string; previewImageUrl?: string | null };
type ComposeCatalog = { cosmetics: ComposeCosmetic[]; sets: ComposeSet[] };

/** 무기 부위의 데이터 슬롯 이름. 콘텐츠 판에 따라 BOTTOM과 WEAPON 중 하나를 쓴다. */
export function cosmeticWeaponSlot(catalog: ComposeCatalog): string {
  const hasBottom = catalog.cosmetics.some(item => item.slot === "BOTTOM");
  const hasWeapon = catalog.cosmetics.some(item => item.slot === "WEAPON");
  return !hasBottom && hasWeapon ? "WEAPON" : "BOTTOM";
}

/**
 * 착용한 치장을 그릴 순서대로 쌓은 결과. 치장을 하나도 걸치지 않았으면 null이라
 * 부르는 쪽이 기본 젓가락을 그대로 쓰면 된다.
 *
 * 이 조합을 치장 탭만 쓰고 나머지 화면은 고정 그림을 쓰던 탓에, 같은 캐릭터가
 * 화면마다 다르게 보였다. 이제 모든 화면이 이 함수 하나를 거친다.
 */
export function composeCosmeticHero(
  equipment: Record<string, string | null | undefined> | undefined,
  catalog: ComposeCatalog | undefined,
): { layers: string[] } | { flat: string } | null {
  if (!equipment || !catalog) return null;
  const cosmetics = new Map(catalog.cosmetics.map(item => [item.cosmeticId, item]));
  const weaponSlot = cosmeticWeaponSlot(catalog);
  const worn = COSMETIC_LAYER_ORDER.map(uiSlot => {
    const id = equipment[uiSlot === "BOTTOM" ? weaponSlot : uiSlot];
    const item = id ? cosmetics.get(id) : undefined;
    return item ? { uiSlot, item } : undefined;
  }).filter((entry): entry is { uiSlot: (typeof COSMETIC_LAYER_ORDER)[number]; item: ComposeCosmetic } => Boolean(entry));
  if (worn.length === 0) return null;

  // 여섯 부위를 한 세트로 다 채웠는데 그 세트에 레이어가 없으면 완성 전신 그림으로 물러선다.
  const setIds = new Set(worn.map(entry => entry.item.setId));
  const fullSet = worn.length === COSMETIC_LAYER_ORDER.length && setIds.size === 1
    ? catalog.sets.find(item => item.setId === worn[0].item.setId)
    : undefined;
  if (fullSet && worn.some(entry => !cosmeticLayerUrl(entry.item))) {
    const flat = cosmeticSetArtUrl(fullSet);
    return flat ? { flat } : null;
  }

  const layered = worn
    .map(entry => ({ ...entry, layer: cosmeticLayerUrl(entry.item) }))
    .filter((entry): entry is typeof entry & { layer: string } => Boolean(entry.layer));
  if (layered.length === 0) return null;
  const behindBody = (entry: (typeof layered)[number]) =>
    COSMETIC_BEHIND_BODY_SLOTS.has(entry.uiSlot) || cosmeticWeaponBehindHands(entry.item);
  const front = layered
    .filter(entry => !behindBody(entry))
    .flatMap(entry => (cosmeticTopHidesArms(entry.item)
      ? [entry.layer, COSMETIC_ARMS_LAYER, cosmeticTopOverArmsUrl(entry.item)].filter((layer): layer is string => Boolean(layer))
      : [entry.layer]));
  // 기본 나무검은 몸통 뒤에 깔린다. 맨 위에 얹으면 젓가락 몸과 옷을 가로질러
  // 칼이 가슴 앞에 떠 있는 것처럼 보인다.
  const holdsWeapon = worn.some(entry => entry.uiSlot === "BOTTOM");
  return {
    layers: [
      ...layered.filter(behindBody).map(entry => entry.layer),
      ...(holdsWeapon ? [] : [COSMETIC_DEFAULT_WEAPON_LAYER]),
      COSMETIC_BASE_LAYER,
      ...front,
    ],
  };
}

const MOTION_BASE = "/cosmetics/motion";
/** 부위별 모션 레이어가 갖춰진 세트. 나머지는 대기 자세 한 장짜리 조합으로 남는다. */
const MOTION_READY_SLUGS: ReadonlySet<string> = new Set(["leather-guard", "scrap-knight", "bamboo-spear", "turtle-guardian", "chef", "sushi", "angel", "yakgwa"]);
/** 원본 부위 파일 이름. 콘텐츠 슬롯과 짝이 다르다. */
const MOTION_PART_SUFFIXES: Record<string, string> = {
  HEAD: "hat", TOP: "body", BOTTOM: "weapon", WEAPON: "weapon", GLOVES: "gloves", SHOES: "shoes", CAPE: "back",
};

function motionFile(motion: string, frame: number) {
  return `${motion}_${String(Math.min(4, Math.max(1, frame + 1))).padStart(2, "0")}`;
}

/** 모션 프레임에 맞춘 맨몸과 기본 무기. 치장 레이어와 같은 928x672 규격이다. */
export function cosmeticMotionBodyUrl(motion: string, frame: number): string {
  return `${MOTION_BASE}/body/${motionFile(motion, frame)}.png`;
}
export function cosmeticMotionDefaultWeaponUrl(motion: string, frame: number): string {
  return `${MOTION_BASE}/weapon-default/${motionFile(motion, frame)}.png`;
}

// 상의가 마네킹의 팔을 덮는 세트. 아트가 상의 다음에 팔을 그리도록 만들어져 있어서,
// 상의를 올린 뒤 그 세트의 팔을 다시 얹어야 팔이 옷 속으로 사라지지 않는다.
// 팔을 상의 파일에 합쳐 두면 다른 세트 장갑을 껴도 이 세트의 손이 따라오므로 따로 둔다.
const MOTION_ARMS_SLUGS: ReadonlySet<string> = new Set(["angel", "yakgwa"]);

/** 상의 위에 덧그리는 그 세트의 팔. 필요 없는 세트는 null이다. */
export function cosmeticMotionArmsUrl(cosmetic: { setId?: string; slot?: string } | undefined, motion: string, frame: number): string | null {
  if (cosmetic?.slot !== "TOP") return null;
  const slug = cosmetic.setId ? SET_ART_SLUGS[cosmetic.setId] : undefined;
  return slug && MOTION_ARMS_SLUGS.has(slug) ? `${MOTION_BASE}/${slug}/${motionFile(motion, frame)}-arms.png` : null;
}

/** 한 부위의 모션 레이어. 그 세트에 모션 아트가 없으면 null이다. */
export function cosmeticMotionLayerUrl(cosmetic: { setId?: string; slot?: string } | undefined, motion: string, frame: number): string | null {
  const slug = cosmetic?.setId ? SET_ART_SLUGS[cosmetic.setId] : undefined;
  const part = cosmetic?.slot ? MOTION_PART_SUFFIXES[cosmetic.slot] : undefined;
  if (!slug || !part || !MOTION_READY_SLUGS.has(slug)) return null;
  return `${MOTION_BASE}/${slug}/${motionFile(motion, frame)}-${part}.png`;
}

/**
 * 한 프레임의 치장 조합. 착용한 부위 중 하나라도 모션 아트가 없으면 null을 돌려주어
 * 부르는 쪽이 멈춘 대기 자세 조합으로 물러서게 한다. 세트마다 아트가 갖춰진 정도가
 * 달라, 반만 움직이는 모습을 만들지 않으려는 것이다.
 */
export function composeCosmeticHeroMotion(
  equipment: Record<string, string | null | undefined> | undefined,
  catalog: ComposeCatalog | undefined,
  motion: string,
  frame: number,
): string[] | null {
  if (!equipment || !catalog) return null;
  const cosmetics = new Map(catalog.cosmetics.map(item => [item.cosmeticId, item]));
  const weaponSlot = cosmeticWeaponSlot(catalog);
  const worn = COSMETIC_LAYER_ORDER.map(uiSlot => {
    const id = equipment[uiSlot === "BOTTOM" ? weaponSlot : uiSlot];
    const item = id ? cosmetics.get(id) : undefined;
    return item ? { uiSlot, item } : undefined;
  }).filter((entry): entry is { uiSlot: (typeof COSMETIC_LAYER_ORDER)[number]; item: ComposeCosmetic } => Boolean(entry));
  if (worn.length === 0) return null;

  const layered = worn.map(entry => ({ ...entry, layer: cosmeticMotionLayerUrl(entry.item, motion, frame) }));
  if (layered.some(entry => !entry.layer)) return null;
  const ready = layered as Array<(typeof layered)[number] & { layer: string }>;
  const behindBody = (entry: (typeof ready)[number]) =>
    COSMETIC_BEHIND_BODY_SLOTS.has(entry.uiSlot) || cosmeticWeaponBehindHands(entry.item);
  const holdsWeapon = worn.some(entry => entry.uiSlot === "BOTTOM");
  const frontEntries = ready.filter(entry => !behindBody(entry));
  const armsLayer = cosmeticMotionArmsUrl(worn.find(entry => entry.uiSlot === "TOP")?.item, motion, frame);
  // 아트가 정한 차례는 상의 → 신발 → 팔 → 장갑이다. 신발을 안 신었으면 상의 바로 다음이다.
  const armsAfter = frontEntries.some(entry => entry.uiSlot === "SHOES") ? "SHOES" : "TOP";
  const front = frontEntries.flatMap(entry => (armsLayer && entry.uiSlot === armsAfter ? [entry.layer, armsLayer] : [entry.layer]));
  return [
    ...ready.filter(behindBody).map(entry => entry.layer),
    ...(holdsWeapon ? [] : [cosmeticMotionDefaultWeaponUrl(motion, frame)]),
    cosmeticMotionBodyUrl(motion, frame),
    ...front,
  ];
}

const COSMETIC_MOTION_FRAMES = 4;
const preloadedMotions = new Set<string>();
const preloadedImages: HTMLImageElement[] = [];

/**
 * Warms every frame of a motion before it plays.  Without this the battle
 * swaps each layer's `src` to a file the browser has never seen, and the
 * character blinks once per frame while the new art decodes.
 */
export function preloadCosmeticHeroMotion(
  equipment: Record<string, string | null | undefined> | undefined,
  catalog: ComposeCatalog | undefined,
  motion: string,
): void {
  if (!equipment || !catalog || typeof Image === "undefined") return;
  const key = `${motion}:${Object.entries(equipment).filter(([, id]) => id).map(([slot, id]) => `${slot}=${id}`).sort().join("&")}`;
  if (preloadedMotions.has(key)) return;
  preloadedMotions.add(key);
  for (let frame = 0; frame < COSMETIC_MOTION_FRAMES; frame += 1) {
    for (const url of composeCosmeticHeroMotion(equipment, catalog, motion, frame) ?? []) {
      const image = new Image();
      image.src = url;
      preloadedImages.push(image);
    }
  }
}

/** 착용한 부위마다 그 세트의 아트 슬러그. 매니페스트로 그릴 세트인지 가릴 때 쓴다. */
export function cosmeticSlugsBySlot(
  equipment: Record<string, string | null | undefined> | undefined,
  catalog: ComposeCatalog | undefined,
): Record<string, string | undefined> | null {
  if (!equipment || !catalog) return null;
  const cosmetics = new Map(catalog.cosmetics.map(item => [item.cosmeticId, item]));
  const weaponSlot = cosmeticWeaponSlot(catalog);
  const slugs: Record<string, string | undefined> = {};
  let worn = 0;
  for (const uiSlot of COSMETIC_LAYER_ORDER) {
    const id = equipment[uiSlot === "BOTTOM" ? weaponSlot : uiSlot];
    const item = id ? cosmetics.get(id) : undefined;
    if (!item) continue;
    slugs[uiSlot] = SET_ART_SLUGS[item.setId];
    worn++;
  }
  return worn > 0 ? slugs : null;
}
