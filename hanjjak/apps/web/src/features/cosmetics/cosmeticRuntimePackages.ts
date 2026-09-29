import { ANGEL_RUNTIME_BASE_URL, type AppearanceSlot, type EquippedAppearance } from "../gems/angelEquipment";

/**
 * 매니페스트로 그리는 치장 세트.
 *
 * 낱장 레이어로 그리는 세트(`/cosmetics/motion/...`)와 달리, 이쪽은 아트가 프레임마다
 * 겹침 순서를 따로 정해 둔 세트다. 천사는 공격 프레임에서 날개가 몸 앞으로 오고 약과는
 * 무기 잡은 손이 반대 손보다 먼저 온다. 고정 순서 하나로는 그릴 수 없어 매니페스트의
 * `drawOrderByFrame`을 그대로 따라간다.
 */
export type CosmeticRuntimePackage = {
  packageId: string;
  baseUrl: string;
  manifestUrl: string;
  /** 콘텐츠 슬롯을 매니페스트 아이템으로 잇는다. 여섯 부위가 모두 있어야 한다. */
  items: Record<AppearanceSlot, string>;
};

const YAKGWA_RUNTIME_BASE_URL = "/assets/characters/yakgwa/runtime-equipment-v1";

/** 세트 슬러그 → 런타임 패키지. 여기 없는 세트는 낱장 레이어 경로로 간다. */
export const COSMETIC_RUNTIME_PACKAGES: Readonly<Record<string, CosmeticRuntimePackage>> = {
  angel: {
    packageId: "angel-final-v1-runtime",
    baseUrl: ANGEL_RUNTIME_BASE_URL,
    manifestUrl: `${ANGEL_RUNTIME_BASE_URL}/angel-equipment-manifest.json`,
    items: {
      hat: "angel-halo", clothing: "angel-white-robe", cape: "angel-wings",
      glove: "angel-wrist-rings", shoe: "angel-ankle-rings", weapon: "angel-feather-sword",
    },
  },
  yakgwa: {
    packageId: "moonlit-yakgwa-guardian-runtime",
    baseUrl: YAKGWA_RUNTIME_BASE_URL,
    manifestUrl: `${YAKGWA_RUNTIME_BASE_URL}/yakgwa-equipment-manifest.json`,
    items: {
      hat: "moonlit-yakgwa-hat", clothing: "moonlit-yakgwa-clothing", cape: "moonlit-yakgwa-cape",
      glove: "moonlit-yakgwa-glove", shoe: "moonlit-yakgwa-shoe", weapon: "moonlit-yakgwa-weapon",
    },
  },
};

/** 콘텐츠의 여섯 부위를 장비 매니페스트의 슬롯 이름으로 옮긴다. */
const SLOT_BY_COSMETIC_SLOT: Record<string, AppearanceSlot> = {
  HEAD: "hat", TOP: "clothing", CAPE: "cape", GLOVES: "glove", SHOES: "shoe", BOTTOM: "weapon", WEAPON: "weapon",
};

/**
 * 착용한 부위를 그 세트의 매니페스트 아이템으로 바꾼다. 한 벌을 통째로 입은 경우에만
 * 값을 돌려준다. 매니페스트의 겹침 순서가 자기 세트의 여섯 부위를 전제로 짜여 있어,
 * 섞어 입은 채로 이 경로를 타면 빈 자리가 순서에서 빠지며 무기가 몸을 가로지른다.
 */
export function runtimeLoadoutFor(
  slugBySlot: Record<string, string | undefined>,
): { runtimePackage: CosmeticRuntimePackage; equipped: EquippedAppearance } | null {
  const slugs = new Set(Object.values(slugBySlot).filter(Boolean) as string[]);
  if (slugs.size !== 1) return null;
  const runtimePackage = COSMETIC_RUNTIME_PACKAGES[[...slugs][0]];
  if (!runtimePackage) return null;
  const equipped: Partial<EquippedAppearance> = {};
  for (const [cosmeticSlot, slug] of Object.entries(slugBySlot)) {
    const slot = SLOT_BY_COSMETIC_SLOT[cosmeticSlot];
    if (!slot || !slug) continue;
    equipped[slot] = runtimePackage.items[slot];
  }
  const filled = Object.keys(runtimePackage.items).every(slot => equipped[slot as AppearanceSlot]);
  return filled ? { runtimePackage, equipped: equipped as EquippedAppearance } : null;
}
