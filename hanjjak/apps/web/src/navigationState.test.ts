import { describe, expect, it } from "vitest";
import {
  keepsBattleStageMounted,
  MANAGEMENT_SCREENS,
  openCharacterGachaFrom,
  openCosmeticsFrom,
  PRIMARY_NAV_ITEMS,
  readStoredScreen,
  SCREEN_STORAGE_KEY,
  isRaidRouteAvailable,
  resolveScreenForRaidAvailability,
  usesLegacyAppHeader,
  usesCozySharedNavigation,
} from "./navigationState";

describe("management screen persistence", () => {
  it("exposes cosmetics as a direct entry without persisting it as a management screen", () => {
    expect(PRIMARY_NAV_ITEMS).toContainEqual({ screen: "cosmetics", label: "치장 뽑기" });
    expect(MANAGEMENT_SCREENS).not.toContain("cosmetics");
  });

  it("persists the first-clear reward inbox through a browser refresh", () => {
    expect(PRIMARY_NAV_ITEMS).toContainEqual({ screen: "firstClearRewards", label: "첫 클리어 보상" });
    expect(MANAGEMENT_SCREENS).toContain("firstClearRewards");
    expect(readStoredScreen({ getItem: () => "firstClearRewards" })).toBe("firstClearRewards");
  });

  it("restores the marketplace after a browser refresh", () => {
    const storage = { getItem: (key: string) => key === SCREEN_STORAGE_KEY ? "market" : null };
    expect(readStoredScreen(storage)).toBe("market");
  });

  it("falls back to battle for transient or invalid screens", () => {
    expect(readStoredScreen({ getItem: () => "cosmetics" })).toBe("battle");
    expect(readStoredScreen({ getItem: () => "profile" })).toBe("battle");
    expect(readStoredScreen({ getItem: () => "settings" })).toBe("battle");
    expect(readStoredScreen({ getItem: () => "invalid" })).toBe("battle");
    expect(readStoredScreen({ getItem: () => null })).toBe("battle");
  });

  it("restores the dungeon content route", () => {
    expect(readStoredScreen({ getItem: () => "dungeon" })).toBe("dungeon");
  });

  it("restores the battle history menu", () => {
    expect(readStoredScreen({ getItem: () => "battleHistory" })).toBe("battleHistory");
  });

  it("restores the combat power ranking route", () => {
    expect(readStoredScreen({ getItem: () => "ranking" })).toBe("ranking");
  });
  it("returns a stored raid route to battle until the authoritative raid state permits it", () => {
    const storedRaid = readStoredScreen({ getItem: () => "raid" });
    expect(storedRaid).toBe("raid");
    expect(resolveScreenForRaidAvailability(storedRaid, undefined)).toBe("battle");
    expect(resolveScreenForRaidAvailability(storedRaid, { featureAvailable: false, unlocked: true })).toBe("battle");
    expect(resolveScreenForRaidAvailability(storedRaid, { featureAvailable: true, unlocked: false })).toBe("battle");
    expect(resolveScreenForRaidAvailability(storedRaid, { featureAvailable: true, unlocked: true })).toBe("raid");
    expect(isRaidRouteAvailable({ featureAvailable: true, unlocked: true })).toBe(true);
  });

  it("keeps the shared HUD on full-page connected management pages", () => {
    expect(MANAGEMENT_SCREENS.filter(usesCozySharedNavigation)).toEqual(["battleHistory", "dungeon", "raid", "ranking", "arena", "firstClearRewards"]);
    expect(usesCozySharedNavigation("cosmetics")).toBe(true);
  });

  it("never shows the legacy header on the screens served by the shared HUD", () => {
    for (const screen of ["battleHistory", "dungeon", "ranking", "arena", "firstClearRewards", "cosmetics"] as const) {
      expect(usesLegacyAppHeader(screen)).toBe(false);
    }
  });

  it("keeps the battle stage mounted behind a growth or marketplace overlay", () => {
    expect(keepsBattleStageMounted("battle")).toBe(true);
    expect(keepsBattleStageMounted("equipment")).toBe(true);
    expect(keepsBattleStageMounted("skills")).toBe(true);
    expect(keepsBattleStageMounted("market")).toBe(true);
    expect(keepsBattleStageMounted("inventory")).toBe(true);
    expect(keepsBattleStageMounted("gems")).toBe(true);
  });
});

describe("cosmetic gacha navigation", () => {
  it("preserves every management origin for the return action", () => {
    const origins = ["battle", "battleHistory", "gems", "dungeon", "inventory", "skills", "market", "ranking"] as const;
    expect(origins.map((origin) => openCosmeticsFrom(origin))).toEqual(
      origins.map((origin) => ({ screen: "cosmetics", gachaReturn: origin })),
    );
  });

  it("preserves the existing origin when cosmetics is already active", () => {
    expect(openCosmeticsFrom("cosmetics", "inventory")).toEqual({
      screen: "cosmetics",
      gachaReturn: "inventory",
    });
  });

  it("preserves the character gacha origin on active re-entry and captures normal entry", () => {
    expect(openCharacterGachaFrom("cosmetics", "inventory")).toEqual({
      screen: "cosmetics",
      gachaReturn: "inventory",
    });
    expect(openCharacterGachaFrom("battle", "inventory")).toEqual({
      screen: "cosmetics",
      gachaReturn: "battle",
    });
  });
});
