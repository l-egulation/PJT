export const SCREEN_STORAGE_KEY = "hanjjak.active-management-screen";

export const MANAGEMENT_SCREENS = ["battle", "battleHistory", "inventory", "equipment", "skills", "gems", "market", "mail", "dungeon", "raid", "ranking", "arena", "firstClearRewards"] as const;

export type ManagementScreen = typeof MANAGEMENT_SCREENS[number];
export type AppScreen = ManagementScreen | "cosmetics";

export function openCosmeticsFrom(currentScreen: AppScreen, existingGachaReturn: AppScreen | null = null): { screen: "cosmetics"; gachaReturn: AppScreen | null } {
  return {
    screen: "cosmetics",
    gachaReturn: currentScreen === "cosmetics" ? existingGachaReturn : currentScreen,
  };
}

export function openCharacterGachaFrom(currentScreen: AppScreen, existingGachaReturn: AppScreen | null = null): { screen: "cosmetics"; gachaReturn: AppScreen | null } {
  return openCosmeticsFrom(currentScreen, existingGachaReturn);
}
export const PRIMARY_NAV_ITEMS: Array<{ screen: AppScreen; label: string }> = [
  { screen: "battle", label: "전투" },
  { screen: "battleHistory", label: "전투 기록" },
  { screen: "inventory", label: "아이템" },
  { screen: "equipment", label: "장비" },
  { screen: "skills", label: "스킬" },
  { screen: "gems", label: "보석" },
  { screen: "market", label: "거래소" },
  { screen: "mail", label: "메시지함" },
  { screen: "arena", label: "아레나" },
  { screen: "ranking", label: "랭킹" },
  { screen: "firstClearRewards", label: "첫 클리어 보상" },
  { screen: "cosmetics", label: "치장 뽑기" },
];

const COZY_SHARED_SCREENS = ["battleHistory", "dungeon", "raid", "ranking", "arena", "firstClearRewards", "cosmetics"] as const;
export type CozySharedScreen = typeof COZY_SHARED_SCREENS[number];

export function usesCozySharedNavigation(screen: AppScreen): screen is CozySharedScreen {
  return (COZY_SHARED_SCREENS as readonly AppScreen[]).includes(screen);
}

/* 이 세 화면은 화면 전체를 그림으로 쓴다. 아래 성장 메뉴와 프로필 카드까지 얹으면
   그림이 가려져, 위쪽 관리 메뉴만 남긴다. */
const TOP_BAR_ONLY_SCREENS = ["ranking", "cosmetics", "dungeon"] as const;

export function usesTopBarOnlyNavigation(screen: AppScreen): boolean {
  return (TOP_BAR_ONLY_SCREENS as readonly AppScreen[]).includes(screen);
}

const STANDALONE_MANAGEMENT_SCREENS = ["inventory", "equipment", "skills", "market", "mail", "gems"] as const;

export function usesLegacyAppHeader(screen: AppScreen): boolean {
  return screen !== "battle"
    && !usesCozySharedNavigation(screen)
    && !(STANDALONE_MANAGEMENT_SCREENS as readonly AppScreen[]).includes(screen);
}

export function keepsBattleStageMounted(screen: AppScreen): boolean {
  return screen === "battle" || screen === "inventory" || screen === "equipment" || screen === "skills" || screen === "gems" || screen === "market" || screen === "mail";
}

export function isRaidRouteAvailable(raid: { featureAvailable: boolean; unlocked: boolean } | undefined): boolean {
  return raid?.featureAvailable === true && raid.unlocked === true;
}

export function resolveScreenForRaidAvailability(screen: AppScreen, raid: { featureAvailable: boolean; unlocked: boolean } | undefined): AppScreen {
  return screen === "raid" && !isRaidRouteAvailable(raid) ? "battle" : screen;
}

export function readStoredScreen(storage: Pick<Storage, "getItem">): ManagementScreen {
  const stored = storage.getItem(SCREEN_STORAGE_KEY);
  return MANAGEMENT_SCREENS.includes(stored as ManagementScreen) ? stored as ManagementScreen : "battle";
}
