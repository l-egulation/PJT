import React, { useEffect, useRef, useState } from "react";
import { createRoot } from "react-dom/client";
import { QueryClient, QueryClientProvider, useQueryClient } from "@tanstack/react-query";
import {
  BattleGrowthNavigation,
  BattleHistoryPopover,
  BattlePlayerStatus,
  BattleProfileSummary,
  BattleRewards,
  BattleStageProgress,
  BattleUtilityNavigation,
  CozySharedNavigation,
  type BattleHudRoute,
} from "./BattleHud";
import { FirstClearRewardScreen } from "../first-clear-rewards/FirstClearRewardScreen";
import type { FirstClearRewardResult, PendingFirstClearRewardPage } from "../first-clear-rewards/api";
import { CharacterWindow } from "../character/CharacterWindow";
import type { CharacterCollection, CharacterStats, CosmeticCatalog } from "../character/api";
import { EquipmentScreen, EquipmentWindow } from "../equipment/EquipmentScreen";
import type { EquipmentSlot, EquipmentState } from "../equipment/api";
import { InventoryScreen, InventoryWindow, type InventoryDataSource } from "../inventory/InventoryScreen";
import type { InventoryItem, InventorySort } from "../inventory/api";
import { MarketPlaza, type MarketMode } from "../market/MarketPlaza";
import { MailScreen, type MailDataSource } from "../mail/MailScreen";
import { ServerResetNotice } from "../notices/ServerResetNotice";
import { selectMarketItemForSale } from "../market/marketUiState";
import { ProfileScreen, type ProfileTab } from "../profile/ProfileScreen";
import { RankingScreen } from "../ranking/RankingScreen";
import type { CombatPowerRanking, MaterialType, RankingEntry } from "../ranking/api";
import { SkillsScreen, SkillWindow } from "../skills/SkillsScreen";
import { PictureInPictureController } from "../picture-in-picture/PictureInPictureController";
import { TutorialDialogue } from "../tutorial/TutorialDialogue";
import { TutorialLauncher } from "../tutorial/TutorialLauncher";
import { TUTORIAL_PREVIEW_CHAPTERS, type TutorialPreviewStep } from "../tutorial/tutorialPreviewCatalog";
import { TutorialScreenReplay } from "../tutorial/TutorialScreenReplay";
import type { TutorialRuntimeSurface } from "../tutorial/tutorialRuntimeCatalog";
import { ChatPanel } from "../chat/ChatPanel";
import type { ChatPage } from "../chat/api";
import { CosmeticGachaBoard } from "../cosmetics/CosmeticGachaBoard";
import { CosmeticDrawReveal } from "../cosmetics/CosmeticDrawReveal";
import { CosmeticSelectorScreen } from "../cosmetics/CosmeticSelectorScreen";
import type { Banner as CosmeticBanner, Catalog as GachaCatalog, Collection as GachaCollection, DrawResponse as CosmeticDrawResponse } from "../cosmetics/api";
import type { SkillVfx } from "../gems/skillVfx";
import type { GemFusionMode, GemFusionResult, GemOption, GemState, GemSummary } from "../gems/api";
import { GemsScreen } from "../gems/GemsScreen";
import { GemManagement } from "../gems/GemManagement";
import { CostumePanel, GalleryPanel } from "../character/CosmeticPanels";
import { BossArrivalWarning, ChapterOneMonster } from "./BattleScreen";
import type { BattleHistoryEvent } from "./api";
import { backgroundAsset, MONSTER_NAMES, stageVisual, type ChapterOneMonsterId } from "./battleVisuals";
import { PlayerBattleCharacter } from "../gems/PlayerBattleCharacter";
import "../../styles.css";
import "../cosmetics/cosmetics.css";
import "./BattleScreen.css";
import "./battle-hud-preview.css";
import "../../app-shell.css";

/* 1챕터는 다 깼고, 2챕터는 도중이며, 3챕터는 막 열렸고, 4챕터는 아직 잠겼다. */
const PREVIEW_CHAPTER_PROGRESS: Record<number, { unlockedThrough: number; clearedThrough: number }> = {
  1: { unlockedThrough: 10, clearedThrough: 10 },
  2: { unlockedThrough: 10, clearedThrough: 6 },
  3: { unlockedThrough: 2, clearedThrough: 1 },
  4: { unlockedThrough: 0, clearedThrough: 0 },
};
const stages = [1, 2, 3, 4].flatMap((chapter) => Array.from({ length: 10 }, (_, index) => {
  const number = index + 1;
  const progress = PREVIEW_CHAPTER_PROGRESS[chapter];
  const stageId = `stage.${String(chapter).padStart(2, "0")}-${String(number).padStart(2, "0")}`;
  const bossOnly = number === 10;
  return {
    stageId,
    unlocked: number <= progress.unlockedThrough,
    clearCount: number <= progress.clearedThrough ? 3 : 0,
    contentVersion: "preview",
    isCurrent: stageId === "stage.01-07",
    isRepeatTarget: false,
    repeatEligible: !bossOnly,
    normalMonsterIds: [],
    bossMonsterId: bossOnly ? "strawberry-siru" : null,
    backgroundId: bossOnly ? "oven-cellar" : null,
    bossOnly,
  };
}));

const previewGemOptionNames: Record<GemOption, string> = {
  FLAT_ATTACK: "고정 공격력",
  FLAT_HP: "고정 최대 HP",
  ATTACK_PERCENT: "공격력%",
  FLAT_PENETRATION: "고정 방어 관통",
  CRITICAL_CHANCE: "치명타 확률",
  HASTE: "공격속도",
};
const previewGem = (gemId: string, level: number, option: GemOption): GemSummary => ({
  gemId,
  slotId: `${gemId}-slot`,
  level,
  option,
  optionName: previewGemOptionNames[option],
  value: level * 100,
  locked: false,
  reservedForSale: false,
  equippedPresets: [],
});
const previewTutorialGemState: GemState = {
  unlocked: true,
  tickets: 3,
  secondsUntilNextTicket: 27_142,
  todayBoss: "SURVIVAL",
  gemBoxQuantity: 12,
  gems: [
    previewGem("tutorial-equip-gem", 1, "FLAT_HP"),
    previewGem("tutorial-fusion-gem-1", 2, "ATTACK_PERCENT"),
    previewGem("tutorial-fusion-gem-2", 2, "ATTACK_PERCENT"),
    previewGem("tutorial-fusion-gem-3", 2, "ATTACK_PERCENT"),
  ],
  presets: {},
  lockedPresets: [],
  contentVersion: "preview-tutorial",
};

const previewSkillAction = (kind: "ENHANCE" | "PROMOTE", riceCost: number, bookCost: number) => ({
  kind,
  targetGrade: kind === "PROMOTE" ? "RARE" : null,
  targetLevel: kind === "PROMOTE" ? 1 : 2,
  books: [{ itemId: "SKILL_BOOK", displayName: "스킬 비법서", requiredQuantity: bookCost, availableQuantity: 38 }],
  riceCost,
  successBasisPoints: kind === "PROMOTE" ? 10_000 : 7_000,
  executable: true,
  disabledReason: null,
});

const previewSkillState = {
  riceBalance: 61_238,
  activeLoadout: ["active_haste", "active_dot", "active_basic_amp"],
  skills: [
    { skillId: "active_heavy", name: "한짝의 일격", active: true, unlocked: true, grade: "NORMAL", gradeName: "노말", level: 10, equippedSlot: null, effectText: "강한 일격으로 공격력의 455% 피해", action: previewSkillAction("PROMOTE", 1_000, 4) },
    { skillId: "active_dot", name: "마! 쫄이나", active: true, unlocked: true, grade: "NORMAL", gradeName: "노말", level: 7, equippedSlot: 2, effectText: "5초 동안 총 865% 지속 피해", action: previewSkillAction("ENHANCE", 300, 2) },
    { skillId: "active_haste", name: "잘게 더 잘게!", active: true, unlocked: true, grade: "NORMAL", gradeName: "노말", level: 7, equippedSlot: 1, effectText: "8초 동안 공격 속도 18% 증가", action: previewSkillAction("ENHANCE", 210, 2) },
    { skillId: "active_basic_amp", name: "화력 최대로", active: true, unlocked: true, grade: "NORMAL", gradeName: "노말", level: 7, equippedSlot: 3, effectText: "10초 동안 기본 공격 피해 24% 증가", action: previewSkillAction("ENHANCE", 210, 2) },
    { skillId: "passive_critical", name: "회심의 간", active: false, unlocked: true, grade: "NORMAL", gradeName: "노말", level: 7, equippedSlot: null, effectText: "치명타 확률 7%, 치명타 피해 14% 증가", action: previewSkillAction("ENHANCE", 210, 2) },
    { skillId: "passive_all_damage", name: "오늘의 특선", active: false, unlocked: true, grade: "NORMAL", gradeName: "노말", level: 7, equippedSlot: null, effectText: "모든 피해 12% 증가", action: previewSkillAction("ENHANCE", 210, 2) },
  ],
};

const previewMarketInstrument = {
  instrumentId: "preview-potato-m1",
  canonicalKey: "material:potato_m1",
  itemId: "POTATO_M1",
  displayName: "감자 한 조각",
  category: "MATERIAL",
  attributes: {},
  status: "ACTIVE",
  bestBidUnitPrice: 90,
  bestAskUnitPrice: 95,
  lastTradeUnitPrice: 92,
  marketRevision: 1,
};

const MATERIAL_FAMILIES: Array<[string, string[]]> = [
  ["POTATO", ["감자 한 조각", "미니 감자", "감자", "황금 감자", "전설 감자"]],
  ["SWEET_POTATO", ["고구마 한 조각", "미니 고구마", "고구마", "황금 고구마", "전설 고구마"]],
  ["CORN", ["옥수수 한 알", "미니 옥수수", "옥수수", "황금 옥수수", "전설 옥수수"]],
];
const SKILL_BOOK_TITLES: Array<[string, string]> = [
  ["active_heavy", "한짝의 일격"], ["active_dot", "마! 쫄이나"], ["active_haste", "잘게 더 잘게!"],
  ["active_basic_amp", "화력 최대로!"], ["passive_critical", "회심의 간"], ["passive_all_damage", "오늘의 특선"],
];
const SKILL_BOOK_GRADE_NAMES: Array<[string, string]> = [["normal", "노말"], ["rare", "희귀"], ["epic", "영웅"], ["legendary", "전설"]];
const GEM_OPTIONS: Array<[string, string, number[]]> = [
  ["flat_attack", "고정 공격력", [0, 3, 7, 15, 32, 70, 150, 320]],
  ["flat_hp", "고정 최대 HP", [0, 30, 70, 150, 320, 700, 1500, 3200]],
  ["attack_percent", "공격력%", [0, 1500, 1500, 1500, 1500, 1500, 800, 1500]],
  ["flat_penetration", "고정 방어 관통", [0, 120, 120, 120, 120, 120, 60, 120]],
  ["critical_chance", "치명타 확률", [0, 1200, 1200, 1200, 1200, 1200, 1200, 1200]],
  ["attack_speed", "공격속도", [0, 1200, 1200, 1200, 1200, 1200, 1200, 1200]],
];

/** 서버가 돌려주는 거래 품목 목록. 매물이 없어도 목록에는 늘 들어 있다. */
function previewMarketCatalog() {
  const catalog: Array<Record<string, unknown>> = [];
  const add = (canonicalKey: string, itemId: string, displayName: string, category: string, attributes: Record<string, string>) => catalog.push({
    instrumentId: `preview-${canonicalKey.replace(/[:]/g, "-")}`, canonicalKey, itemId, displayName, category, attributes,
    status: "ACTIVE", bestBidUnitPrice: null, bestAskUnitPrice: null, lastTradeUnitPrice: null, marketRevision: 1,
  });
  for (const [family, names] of MATERIAL_FAMILIES) for (let generation = 1; generation <= 5; generation += 1) {
    const itemId = `${family}_M${generation}`;
    add(`material:${itemId.toLowerCase()}`, itemId, names[generation - 1], "MATERIAL", { itemId });
  }
  for (const [skillId, title] of SKILL_BOOK_TITLES) for (const [gradeId, gradeName] of SKILL_BOOK_GRADE_NAMES) {
    add(`skillbook:${skillId}:${gradeId}`, `skillbook:${skillId}:${gradeId}`, `${gradeName} ${title} 비법서`, "SKILL_BOOK", { skillId, grade: gradeId });
  }
  for (let level = 1; level <= 7; level += 1) for (const [option, optionName, values] of GEM_OPTIONS) {
    const value = values[level];
    add(`gem:${level}:${option}:${value}`, `gem:${level}:${option}`, `${level}레벨 ${optionName} 보석`, "GEM", { level: String(level), option, value: String(value) });
  }
  /* 시세가 붙어 있는 하나는 그대로 둔다. 사고파는 흐름을 미리보기에서 눌러 볼 수 있어야 한다. */
  return catalog.map(item => item.itemId === previewMarketInstrument.itemId ? previewMarketInstrument : item);
}

const previewMails = [
  { mailId: "mail-1", type: "OPERATOR_GIFT", riceAmount: 12_400, claimed: false, createdAt: "2026-09-13T09:20:00Z", claimedAt: null },
  { mailId: "mail-2", type: "OPERATOR_GIFT", riceAmount: 3_150, claimed: false, createdAt: "2026-09-12T18:05:00Z", claimedAt: null },
  { mailId: "mail-3", type: "OPERATOR_GIFT", riceAmount: 880, claimed: true, createdAt: "2026-09-11T07:40:00Z", claimedAt: "2026-09-11T08:00:00Z" },
];
const previewMailDataSource: MailDataSource = {
  list: async () => ({ items: previewMails, nextCursor: null, totalItems: previewMails.length }),
  claim: async (mailId: string) => ({ commandId: "preview", idempotencyKey: "preview", status: "SUCCEEDED", result: { mailId, riceAmount: 0, claimedAt: "", walletBalance: 0 } }),
  claimAll: async () => ({ commandId: "preview", idempotencyKey: "preview", status: "SUCCEEDED", result: { claimedMailIds: [], totalRiceAmount: 0, walletBalance: 0 } }),
};

const previewAccount = { accountId: "preview", characterId: "preview", email: "player@example.com", nickname: "한짝", level: 24, experience: 18_720, rice: 15_820 };
const previewInventorySeeds: Array<[string, string, number]> = [
  ["POTATO_M1", "감자 한 조각", 530],
  ["POTATO_M2", "미니 감자", 999],
  ["POTATO_M3", "감자", 430],
  ["POTATO_M4", "황금 감자", 88],
  ["POTATO_M5", "전설 감자", 12],
  ["SWEET_POTATO_M1", "고구마 한 조각", 777],
  ["SWEET_POTATO_M2", "미니 고구마", 999],
  ["SWEET_POTATO_M3", "고구마", 412],
  ["SWEET_POTATO_M4", "황금 고구마", 71],
  ["SWEET_POTATO_M5", "전설 고구마", 9],
  ["CORN_M1", "옥수수 한 알", 650],
  ["CORN_M2", "미니 옥수수", 265],
  ["CORN_M3", "옥수수", 321],
  ["CORN_M4", "황금 옥수수", 54],
  ["CORN_M5", "전설 옥수수", 7],
];
const previewMaterialItems: InventoryItem[] = previewInventorySeeds.map(([itemId, name, quantity], index) => {
  const generation = Number(itemId.match(/_M([1-5])$/)?.[1] ?? 1);
  return {
    itemId,
    instanceId: null,
    slotId: `stack:${itemId}:0`,
    members: [],
    acquiredSequence: previewInventorySeeds.length - index,
    name,
    icon: "",
    category: "MATERIAL",
    description: `장비 제작과 강화에 사용하는 ${generation}세대 재료`,
    acquisitionSources: ["자동 파밍"],
    usages: ["장비 제작", "장비 강화"],
    tradeable: true,
    totalQuantity: quantity,
    reservedQuantity: 0,
    availableQuantity: quantity,
  };
});
const previewSkillBookSeeds: Array<[string, string]> = [
  ["active_heavy", "한짝의 일격"],
  ["active_dot", "마! 쫄이나"],
  ["active_haste", "잘게 더 잘게!"],
  ["active_basic_amp", "화력 최대로!"],
  ["passive_critical", "회심의 간"],
  ["passive_all_damage", "오늘의 특선"],
];
const previewSkillBookItems: InventoryItem[] = previewSkillBookSeeds.map(([skillId, name], index) => ({
  itemId: `skillbook:${skillId}:normal`,
  instanceId: null,
  slotId: `stack:skillbook:${skillId}:normal:0`,
  members: [],
  acquiredSequence: 100 - index,
  name: `노말 ${name} 비법서`,
  icon: "",
  category: "SKILL_BOOK",
  description: `${name} 스킬의 노말 등급 성장에 사용하는 전용 스킬북입니다.`,
  acquisitionSources: ["자동 파밍"],
  usages: ["스킬 강화"],
  tradeable: true,
  totalQuantity: 10 + index,
  reservedQuantity: 0,
  availableQuantity: 10 + index,
}));
const previewInventoryItems = [...previewSkillBookItems, ...previewMaterialItems];
const previewInventorySort = (items: InventoryItem[], sort: InventorySort) => [...items].sort((left, right) => {
  if (sort === "NAME_ASC") return left.name.localeCompare(right.name, "ko");
  if (sort === "NAME_DESC") return right.name.localeCompare(left.name, "ko");
  if (sort === "QUANTITY_ASC") return left.totalQuantity - right.totalQuantity;
  if (sort === "QUANTITY_DESC") return right.totalQuantity - left.totalQuantity;
  return right.acquiredSequence - left.acquiredSequence;
});
const previewInventoryDataSource: InventoryDataSource = {
  list: async (category, sort, cursor) => {
    const offset = Number(cursor ?? 0);
    const filtered = previewInventoryItems.filter((item) => category === "ALL" || item.category === category);
    const sorted = previewInventorySort(filtered, sort);
    return {
      items: sorted.slice(offset, offset + 12),
      usedSlots: previewInventoryItems.length,
      maxSlots: 200,
      isFull: false,
      nextCursor: offset + 12 < sorted.length ? String(offset + 12) : null,
    };
  },
  detail: async (itemId) => {
    const item = previewInventoryItems.find((entry) => entry.itemId === itemId);
    if (!item) throw new Error("PREVIEW_INVENTORY_ITEM_NOT_FOUND");
    return { ...item, slotId: null };
  },
};
/* 칸마다 보여 줄 그림은 "젓가락이 그 부위를 착용한 전신"이 아니라 부위 아이콘이다.
   전신 그림을 넣으면 여섯 칸이 전부 비슷해져서 무엇을 고르는지 알 수 없었다.
   실제 화면이 쓰는 `public/cosmetics/item-icons` 를 그대로 가리킨다. */
const previewItemIcon = (slug: string, suffix: string) => `/cosmetics/item-icons/${slug}-${suffix}.png`;
const PREVIEW_SLOT_SUFFIX: Record<string, string> = { HEAD: "head", TOP: "top", GLOVES: "gloves", SHOES: "shoes", CAPE: "cape", WEAPON: "bottom" };

const previewLegendarySets = [
  { setId: "cosmetic-set-01", displayName: "거북이 수호자 세트", prefix: "거북이", weaponName: "파도 삼지창", slug: "turtle-guardian" },
  { setId: "preview-legendary-set-02", displayName: "붕어빵 세트", prefix: "붕어빵", weaponName: "붕어빵 집게", slug: "fishbread" },
  { setId: "preview-legendary-set-03", displayName: "요리사 세트", prefix: "요리사", weaponName: "대왕 프라이팬", slug: "chef" },
  { setId: "preview-legendary-set-04", displayName: "약과 세트", prefix: "약과", weaponName: "약과 떡메", slug: "yakgwa" },
];
const previewSlots = [
  ["HEAD", "모자"], ["TOP", "상의"], ["GLOVES", "장갑"], ["SHOES", "신발"], ["CAPE", "망토"], ["WEAPON", "무기"],
] as const;
const previewCosmetics: CosmeticCatalog["cosmetics"] = previewLegendarySets.flatMap((set, setIndex) => previewSlots.map(([slot, slotName], slotIndex) => ({
  cosmeticId: setIndex === 0 ? `cosmetic-06${slotIndex + 1}` : `preview-${setIndex + 1}-${slot.toLowerCase()}`,
  displayName: slot === "WEAPON" ? set.weaponName : `${set.prefix} ${slotName}`,
  slot,
  grade: "LEGENDARY",
  setId: set.setId,
  imageUrl: previewItemIcon(set.slug, PREVIEW_SLOT_SUFFIX[slot]),
})));
const previewCosmeticCatalog: CosmeticCatalog = {
  contentVersion: "preview-cosmetics-v1",
  cosmetics: previewCosmetics,
  sets: previewLegendarySets.map((set, setIndex) => ({
    setId: set.setId,
    displayName: set.displayName,
    previewImageUrl: previewItemIcon(set.slug, "set"),
    grade: "LEGENDARY" as const,
    members: Object.fromEntries(previewSlots.map(([slot], slotIndex) => [slot, setIndex === 0 ? `cosmetic-06${slotIndex + 1}` : `preview-${setIndex + 1}-${slot.toLowerCase()}`])),
    effects: { 1: [{ statId: "attackPercent", value: 1200, unit: "PERCENT_X100" }], 3: [{ statId: "maxHpPercent", value: 1500, unit: "PERCENT_X100" }] },
  })),
};
const previewCosmeticCollection: CharacterCollection = {
  contentVersion: "preview-cosmetics-v1",
  states: previewCosmetics.map((item, index) => ({
    ...item,
    registeredQuantity: 1,
    unregisteredQuantity: index % 3,
    reservedQuantity: 0,
    availableUnregisteredQuantity: index % 3,
    cosmeticStar: index < 2 ? 3 - index : 1,
    nextStarThreshold: 2,
    neededForNextStar: index % 3 === 0 ? 1 : 2,
    canUpgrade: index % 3 > 0,
    upgradeDisabledReason: index % 3 > 0 ? null : "INSUFFICIENT_DUPLICATES",
  })),
  equipment: { HEAD: "cosmetic-061", TOP: "cosmetic-062", GLOVES: "cosmetic-063", SHOES: "cosmetic-064", CAPE: "cosmetic-065", WEAPON: "cosmetic-066" },
  uniqueRegisteredCount: previewCosmetics.length,
  setStars: { "cosmetic-set-01": 1 },
  setEffects: { "cosmetic-set-01": [{ statId: "attackPercent", value: 1200, unit: "PERCENT_X100" }] },
  totalEffects: [{ statId: "attackPercent", value: 1200, unit: "PERCENT_X100" }],
};
const previewCharacterStat = (statId: string, label: string, total: number, unit = "FLAT"): CharacterStats["stats"][number] => ({
  statId, label, total, unit, base: total, additional: 0, calculation: `${label} ${total}`,
  sources: [{ sourceId: "preview", label: "미리보기", category: "BASE", value: total, unit, applied: true, reason: null }],
});
const previewCharacterStats: CharacterStats = {
  nickname: "한짝", level: 24, experience: 290_976, combatPower: 15_820,
  cosmeticsUnlocked: true, contentVersion: "preview-cosmetics-v1",
  stats: [
    previewCharacterStat("maxHp", "최대 HP", 3_680),
    previewCharacterStat("attack", "공격력", 151),
    previewCharacterStat("penetration", "관통력", 45),
    previewCharacterStat("criticalChance", "치명타 확률", 500, "BASIS_POINTS"),
    previewCharacterStat("basicAttackDamage", "기본공격 피해", 20_000, "BASIS_POINTS"),
    previewCharacterStat("allDamage", "피해량 증가", 0, "BASIS_POINTS"),
    previewCharacterStat("attackSpeed", "공격속도", 100, "BASIS_POINTS"),
    previewCharacterStat("buffDuration", "버프 지속시간", 0, "SECONDS"),
  ],
};
const previewCharacterData = { stats: previewCharacterStats, collection: previewCosmeticCollection, catalog: previewCosmeticCatalog };
const previewCosmeticBanner: CosmeticBanner = {
  bannerId: "cosmetic-banner-01",
  setId: "cosmetic-set-01",
  displayName: "거북이 수호자 세트",
  singleRiceCost: 5_000,
  ticketBalance: 4,
  riceBalance: 50_000,
  oneDraw: { ticketCost: 1, riceCost: 0, executable: true },
  tenDraw: { ticketCost: 4, riceCost: 30_000, executable: true },
  milestone: { totalSuccessfulDraws: 199, claimedBoxCount: 0, claimableBoxCount: 0, drawsUntilNextBox: 1, boxItemId: "cosmetic-selector-box-01", ownedBoxQuantity: 0 },
};
const previewChat: ChatPage = {
  items: [
    { messageId: "preview-chat-3", accountId: "preview-corn", nickname: "옥수수왕", primaryMaterialType: "CORN", body: "반가워요 :)", createdAt: "2026-09-10T10:00:00+09:00", eventId: "preview-chat-event-3" },
    { messageId: "preview-chat-2", accountId: "preview-potato", nickname: "고구마한테그러지마", primaryMaterialType: "POTATO", body: "오늘도 끝까지 화이팅!", createdAt: "2026-09-10T09:59:00+09:00", eventId: "preview-chat-event-2" },
    { messageId: "preview-chat-1", accountId: "preview", nickname: "한짝", primaryMaterialType: "POTATO", body: "안녕하세요!", createdAt: "2026-09-10T09:58:00+09:00", eventId: "preview-chat-event-1" },
    { messageId: "preview-chat-0", accountId: "preview-sweet", nickname: "달콤고구마", primaryMaterialType: "SWEET_POTATO", body: "새 스테이지 열렸어요", createdAt: "2026-09-10T09:57:00+09:00", eventId: "preview-chat-event-0" },
    { messageId: "preview-chat-old-2", accountId: "preview-bread", nickname: "빵굽는날", primaryMaterialType: "CORN", body: "다들 좋은 아침!", createdAt: "2026-09-10T09:56:00+09:00", eventId: "preview-chat-event-old-2" },
    { messageId: "preview-chat-old-1", accountId: "preview-rice", nickname: "쌀한톨", primaryMaterialType: "SWEET_POTATO", body: "보스 잡으러 갑니다", createdAt: "2026-09-10T09:55:00+09:00", eventId: "preview-chat-event-old-1" },
  ],
  nextCursor: null,
  unreadCount: 0,
};
const previewBattleHistory: BattleHistoryEvent[] = [
  {
    eventId: "preview-victory",
    type: "STAGE_CLEARED",
    occurredAt: "2026-09-10T03:14:52Z",
    stageId: "stage.01-06",
    dungeonId: null,
    resultCode: "STAGE_CLEARED",
    messageKey: "battle.history.stage.cleared",
    contentVersion: null,
    combatSnapshot: {
      attack: 277,
      maxHp: 3_680,
      penetration: 48,
      stageEnteredAt: "2026-09-10T03:14:18Z",
      remainingHp: 2_680,
      defeatedNormals: 20,
      lastEnemyRemainingHp: 0,
      elapsedTicks: 340,
      experienceGained: 1_120,
      riceGained: 640,
      rewards: [
        { itemId: "POTATO_M1", displayName: "감자 M1", quantity: 7 },
        { itemId: "SWEET_POTATO_M1", displayName: "고구마 M1", quantity: 12 },
      ],
    },
  },
  {
    eventId: "preview-defeat",
    type: "STAGE_FAILED",
    occurredAt: "2026-09-10T03:12:20Z",
    stageId: "stage.01-07",
    dungeonId: null,
    resultCode: "PLAYER_DEFEATED",
    messageKey: "battle.history.player.defeated",
    contentVersion: "enemy-v1-applied",
    combatSnapshot: {
      attack: 277,
      maxHp: 3_680,
      penetration: 48,
      stageEnteredAt: "2026-09-10T03:11:55Z",
      remainingHp: 0,
      defeatedNormals: 17,
      lastEnemyRemainingHp: 382,
      elapsedTicks: 250,
      experienceGained: 816,
      riceGained: 408,
      rewards: [{ itemId: "CORN_M1", displayName: "옥수수 M1", quantity: 3 }],
    },
  },
];

type PreviewRoute = "battle" | Extract<BattleHudRoute, "equipment" | "inventory" | "skills" | "gems" | "dungeon" | "market" | "mail" | "ranking" | "firstClearRewards" | "settings" | "cosmetics">;

const previewFirstClearReward: FirstClearRewardResult = {
  rewardId: "preview-first-clear-reward",
  stageId: "stage.01-05",
  rewardVersion: "progression-rebalance-v1",
  firstClear: true,
  riceGranted: 900,
  grantedItems: [{ itemId: "POTATO_M1", displayName: "감자 한 조각", quantity: 4 }],
  pendingItems: [{ itemId: "skillbook:active_heavy:rare", displayName: "희귀 한짝의 일격 비법서", quantity: 1 }],
  unlockedSkillId: "active_heavy",
  itemStatus: "PENDING",
  requiredSlots: 1,
  availableSlots: 0,
  missingSlots: 1,
};
const previewFirstClearRewardDataSource = {
  list: async (): Promise<PendingFirstClearRewardPage> => ({ rewards: [previewFirstClearReward], count: 1 }),
  claim: async (): Promise<FirstClearRewardResult> => ({ ...previewFirstClearReward, grantedItems: [...previewFirstClearReward.pendingItems], pendingItems: [], itemStatus: "CLAIMED", availableSlots: 1, missingSlots: 0 }),
};

const previewAppearance = { head: null, top: null, bottom: null, gloves: null, shoes: null, cape: null };
const previewRankingEntry = (overallRank: number, rank: number, nickname: string, materialType: MaterialType, combatPower: number, level: number): RankingEntry => ({
  overallRank,
  rank,
  nickname,
  materialType,
  combatPower,
  level,
  displayName: materialType === "POTATO" ? "감자 전문" : materialType === "SWEET_POTATO" ? "고구마 전문" : "옥수수 전문",
  appearance: previewAppearance,
  updatedAt: "2026-09-10T09:00:00Z",
});
const previewRankingEntries = [
  previewRankingEntry(1, 1, "고구마한테그러지마", "SWEET_POTATO", 219_999, 52),
  previewRankingEntry(2, 1, "감자만세", "POTATO", 198_240, 49),
  previewRankingEntry(3, 1, "옥수수기사단", "CORN", 186_810, 47),
  previewRankingEntry(4, 2, "버터감자", "POTATO", 174_500, 45),
  previewRankingEntry(5, 2, "군고구마", "SWEET_POTATO", 163_900, 43),
  previewRankingEntry(6, 2, "소금옥수수", "CORN", 152_300, 41),
  previewRankingEntry(7, 3, "마스터감자", "POTATO", 141_220, 38),
  previewRankingEntry(8, 3, "왕고구마", "SWEET_POTATO", 133_700, 36),
  previewRankingEntry(9, 3, "옥수수수염", "CORN", 125_610, 33),
  previewRankingEntry(10, 4, "한짝", "POTATO", 118_200, 24),
];
const previewCombatPowerRanking: CombatPowerRanking = {
  formulaVersion: "combat-power-v1",
  generatedAt: "2026-09-10T09:00:00Z",
  sourceStateVersion: 1,
  overallTop: previewRankingEntries,
  specializations: (["POTATO", "SWEET_POTATO", "CORN"] as MaterialType[]).map((materialType) => ({
    materialType,
    displayName: previewRankingEntries.find((entry) => entry.materialType === materialType)!.displayName,
    entries: previewRankingEntries.filter((entry) => entry.materialType === materialType).map((entry, index) => ({ ...entry, rank: index + 1 })),
  })),
  myEntry: previewRankingEntries.at(-1)!,
};

const previewEquipmentRows: Array<{ slot: EquipmentSlot; slotName: string; attack: number; maxHp: number; penetration: number }> = [
  { slot: "WEAPON", slotName: "무기", attack: 52, maxHp: 0, penetration: 0 },
  { slot: "GLOVES", slotName: "장갑", attack: 31, maxHp: 0, penetration: 0 },
  { slot: "ARMOR", slotName: "갑옷", attack: 0, maxHp: 427, penetration: 0 },
  { slot: "HELMET", slotName: "투구", attack: 0, maxHp: 285, penetration: 0 },
  { slot: "CAPE", slotName: "망토", attack: 0, maxHp: 0, penetration: 25 },
  { slot: "SHOES", slotName: "신발", attack: 0, maxHp: 0, penetration: 17 },
];

const previewEquipmentState: EquipmentState = {
  riceBalance: 2_000,
  slots: previewEquipmentRows.map((row, index) => {
    const enhancementLevel = index === 0 ? 30 : index < 2 ? 15 : 7;
    const statIncrease = { attack: row.attack ? 1 : 0, maxHp: row.maxHp ? 11 : 0, penetration: row.penetration ? 2 : 0 };
    const materials = [
      { itemId: "POTATO_M1", displayName: "감자 M1", requiredQuantity: 125, availableQuantity: 116 },
      { itemId: "SWEET_POTATO_M1", displayName: "고구마 M1", requiredQuantity: 125, availableQuantity: 925 },
      { itemId: "CORN_M1", displayName: "옥수수 M1", requiredQuantity: 125, availableQuantity: 391 },
    ];
    return {
      slot: row.slot,
      slotName: row.slotName,
      unlocked: true,
      current: { grade: "NORMAL", gradeName: "노말", enhancementLevel, q: enhancementLevel, stats: { attack: row.attack, maxHp: row.maxHp, penetration: row.penetration } },
      unlock: null,
      enhance: index === 0 ? null : {
        cost: { riceCost: 210 + index * 60, materials },
        result: { grade: "NORMAL", gradeName: "노말", enhancementLevel: enhancementLevel + 1, q: enhancementLevel + 1, stats: { attack: row.attack + statIncrease.attack, maxHp: row.maxHp + statIncrease.maxHp, penetration: row.penetration + statIncrease.penetration } },
        statIncrease,
        executable: true,
        disabledReason: null,
      },
      promote: index === 0 ? {
        requiredStageId: "stage.01-10",
        chapterCleared: true,
        maxEnhancementReached: true,
        cost: { riceCost: 1_000, materials },
        result: { grade: "RARE", gradeName: "희귀", enhancementLevel: 1, q: 40, stats: { attack: 96, maxHp: 0, penetration: 0 } },
        executable: true,
        disabledReason: null,
      } : null,
      growthComplete: false,
    };
  }),
};

previewEquipmentState.slots[0].current = { grade: "RARE", gradeName: "희귀", enhancementLevel: 1, q: 40, stats: { attack: 96, maxHp: 0, penetration: 0 } };
previewEquipmentState.slots[0].enhance = {
  cost: {
    riceCost: 930,
    materials: [
      { itemId: "POTATO_M1", displayName: "감자 한 조각", requiredQuantity: 500, availableQuantity: 520 },
      { itemId: "SWEET_POTATO_M1", displayName: "고구마 한 조각", requiredQuantity: 500, availableQuantity: 500 },
      { itemId: "CORN_M1", displayName: "옥수수 한 알", requiredQuantity: 500, availableQuantity: 500 },
      { itemId: "POTATO_M2", displayName: "미니 감자", requiredQuantity: 100, availableQuantity: 100 },
      { itemId: "SWEET_POTATO_M2", displayName: "미니 고구마", requiredQuantity: 100, availableQuantity: 100 },
      { itemId: "CORN_M2", displayName: "미니 옥수수", requiredQuantity: 100, availableQuantity: 100 },
    ],
  },
  result: { grade: "RARE", gradeName: "희귀", enhancementLevel: 2, q: 41, stats: { attack: 98, maxHp: 0, penetration: 0 } },
  statIncrease: { attack: 2, maxHp: 0, penetration: 0 },
  executable: true,
  disabledReason: null,
};
previewEquipmentState.slots[0].promote = null;

function executePreviewEquipmentCommand(command: { kind: "unlock" | "enhance" | "promote"; slot: EquipmentSlot }, currentState: EquipmentState) {
  const selected = currentState.slots.find((slot) => slot.slot === command.slot);
  const growth = command.kind === "enhance" ? selected?.enhance : command.kind === "promote" ? selected?.promote : selected?.unlock;
  if (!selected || !growth) return { state: currentState, powerIncrease: 0 };
  const nextCurrent = growth.result;
  const nextEnhance = command.kind === "enhance" && selected.enhance ? {
    ...selected.enhance,
    result: {
      ...selected.enhance.result,
      enhancementLevel: nextCurrent.enhancementLevel + 1,
      q: nextCurrent.q + 1,
      stats: {
        attack: nextCurrent.stats.attack + selected.enhance.statIncrease.attack,
        maxHp: nextCurrent.stats.maxHp + selected.enhance.statIncrease.maxHp,
        penetration: nextCurrent.stats.penetration + selected.enhance.statIncrease.penetration,
      },
    },
  } : selected.enhance;
  return {
    state: {
      ...currentState,
      riceBalance: Math.max(0, currentState.riceBalance - growth.cost.riceCost),
      slots: currentState.slots.map((slot) => slot.slot === command.slot ? { ...slot, current: nextCurrent, enhance: nextEnhance } : slot),
    },
    powerIncrease: 5,
  };
}

function CosmeticsPreviewScreen({ tutorialRewardId }: { tutorialRewardId?: string }) {
  const [drawResult, setDrawResult] = useState<CosmeticDrawResponse | null>(null);
  const [resultSource, setResultSource] = useState<"draw" | "selector">("draw");
  const [banner, setBanner] = useState(previewCosmeticBanner);
  const [revealSequence, setRevealSequence] = useState(0);
  const [milestoneClaimed, setMilestoneClaimed] = useState(false);
  const [selectorOpen, setSelectorOpen] = useState(false);
  const previewDraw = (count: 1 | 10) => {
    const tutorialReward = tutorialRewardId
      ? previewCosmeticCatalog.cosmetics.find((item) => item.cosmeticId === tutorialRewardId)
      : undefined;
    const items = Array.from({ length: count }, (_, index) => count === 1 && tutorialReward
      ? tutorialReward
      : previewCosmeticCatalog.cosmetics[index % previewCosmeticCatalog.cosmetics.length]);
    const cost = count === 1 ? banner.oneDraw : banner.tenDraw;
    const totalSuccessfulDraws = banner.milestone.totalSuccessfulDraws + count;
    const claimableBoxCount = Math.floor(totalSuccessfulDraws / 200) - banner.milestone.claimedBoxCount;
    const nextTicketBalance = Math.max(0, banner.ticketBalance - cost.ticketCost);
    const nextRiceBalance = Math.max(0, banner.riceBalance - cost.riceCost);
    const costFor = (nextCount: 1 | 10) => {
      const ticketCost = Math.min(nextTicketBalance, nextCount);
      const riceCost = (nextCount - ticketCost) * banner.singleRiceCost;
      return { ticketCost, riceCost, executable: nextRiceBalance >= riceCost };
    };
    setBanner((current) => ({
      ...current,
      ticketBalance: nextTicketBalance,
      riceBalance: nextRiceBalance,
      oneDraw: costFor(1),
      tenDraw: costFor(10),
      milestone: { ...current.milestone, totalSuccessfulDraws, claimableBoxCount, drawsUntilNextBox: claimableBoxCount > 0 ? 0 : 200 - (totalSuccessfulDraws % 200 || 200) },
    }));
    setMilestoneClaimed(false);
    setResultSource("draw");
    setRevealSequence((current) => current + 1);
    setDrawResult({
      bannerId: banner.bannerId,
      ticketCost: cost.ticketCost,
      riceCost: cost.riceCost,
      results: items.map((item, index) => ({ cosmeticId: item.cosmeticId, grade: item.grade, isNew: index % 3 !== 2 })),
      collection: previewCosmeticCollection as GachaCollection,
    });
  };
  const claimMilestone = () => {
    setBanner((current) => ({ ...current, milestone: { ...current.milestone, claimedBoxCount: current.milestone.claimedBoxCount + 1, claimableBoxCount: Math.max(0, current.milestone.claimableBoxCount - 1), drawsUntilNextBox: 200, ownedBoxQuantity: current.milestone.ownedBoxQuantity + 1 } }));
    setMilestoneClaimed(true);
    setSelectorOpen(true);
  };
  const revealSelectedCosmetic = (cosmeticId: string) => {
    const selectedItem = previewCosmeticCatalog.cosmetics.find((item) => item.cosmeticId === cosmeticId);
    if (!selectedItem) return;
    setBanner((current) => ({ ...current, milestone: { ...current.milestone, ownedBoxQuantity: Math.max(0, current.milestone.ownedBoxQuantity - 1) } }));
    setMilestoneClaimed(false);
    setResultSource("selector");
    setRevealSequence((current) => current + 1);
    setDrawResult({
      bannerId: banner.bannerId,
      ticketCost: 0,
      riceCost: 0,
      results: [{ cosmeticId: selectedItem.cosmeticId, grade: selectedItem.grade, isNew: true }],
      collection: previewCosmeticCollection as GachaCollection,
    });
    setSelectorOpen(false);
  };
  if (selectorOpen) return <CosmeticSelectorScreen banner={banner} catalog={previewCosmeticCatalog as GachaCatalog} onBack={() => setSelectorOpen(false)} onSelect={revealSelectedCosmetic} />;
  if (drawResult) return <section key="preview-reveal" className="cosmetic-screen cosmetic-reveal-screen" aria-label="치장 뽑기 결과 화면">
    <CosmeticDrawReveal key={revealSequence} draw={drawResult} catalog={previewCosmeticCatalog as GachaCatalog} banner={banner} milestoneClaimed={milestoneClaimed} onRedraw={resultSource === "draw" ? previewDraw : undefined} onClaimMilestone={banner.milestone.claimableBoxCount ? claimMilestone : undefined} onOpenSelector={banner.milestone.ownedBoxQuantity ? () => setSelectorOpen(true) : undefined} onContinue={() => { setDrawResult(null); setMilestoneClaimed(false); }} />
  </section>;
  return <section key="preview-main" className="cosmetic-screen battle-preview-cosmetics" aria-labelledby="cosmetics-preview-title">
    <header className="cosmetic-screen-heading">
      <h2 id="cosmetics-preview-title">치장 뽑기</h2>
      <div className="cosmetic-collection-count"><strong>8</strong><span>종 등록</span></div>
    </header>
    <CosmeticGachaBoard banners={[banner]} catalog={previewCosmeticCatalog as GachaCatalog} claimedBannerId={milestoneClaimed ? banner.bannerId : null} onDraw={(_, count) => previewDraw(count)} onOpenSelector={() => setSelectorOpen(true)} onCommand={(command) => { if (command.kind === "claim") claimMilestone(); }} />
  </section>;
}

export function BattleHudPreview() {
  const client = useQueryClient();
  const previewParams = new URLSearchParams(globalThis.location.search);
  const requestedRoute = previewParams.get("route");
  const tutorialPreviewMode = previewParams.get("tutorial");
  const requestedTutorialChapter = TUTORIAL_PREVIEW_CHAPTERS.find((chapter) => chapter.id === tutorialPreviewMode)
    ?? TUTORIAL_PREVIEW_CHAPTERS[0];
  const tutorialPreviewEnabled = tutorialPreviewMode !== null;
  const requestedCharacterTab = previewParams.get("character-tab") === "cosmetics" ? "cosmetics" : "stats";
  const initialRoute: PreviewRoute = requestedRoute === "equipment" || requestedRoute === "inventory" || requestedRoute === "skills" || requestedRoute === "gems" || requestedRoute === "dungeon" || requestedRoute === "market" || requestedRoute === "mail" || requestedRoute === "ranking" || requestedRoute === "firstClearRewards" || requestedRoute === "settings" || requestedRoute === "cosmetics" ? requestedRoute : "battle";
  const [activeRoute, setActiveRoute] = useState<PreviewRoute>(initialRoute);
  const [characterOpen, setCharacterOpen] = useState(requestedRoute === "character");
  const [characterInitialTab, setCharacterInitialTab] = useState<"stats" | "cosmetics">(requestedCharacterTab);
  const [profileOpen, setProfileOpen] = useState(false);
  const [profileInitialTab, setProfileInitialTab] = useState<ProfileTab>("profile");
  const [tutorialVisible, setTutorialVisible] = useState(tutorialPreviewEnabled);
  const [tutorialChapterId, setTutorialChapterId] = useState(requestedTutorialChapter.id);
  const [tutorialMenuOpen, setTutorialMenuOpen] = useState(tutorialPreviewMode === "all");
  const [cosmeticTutorialView, setCosmeticTutorialView] = useState<"gacha" | "wardrobe" | "gallery">("gacha");
  const [previewCollection, setPreviewCollection] = useState<CharacterCollection>(previewCosmeticCollection);
  const [marketInitialMode, setMarketInitialMode] = useState<MarketMode>("buy");
  const tutorialInitialized = useRef(false);
  const bossWarningMode = previewParams.get("boss-warning");
  const skillVfxMode = previewParams.get("skill-vfx");
  const expiringVfxPreview = previewParams.get("expiring") === "1";
  const liveWarningPreview = previewParams.get("animate") === "1";
  const bossFightPreview = previewParams.get("boss-fight") === "1";
  const finalBossPreview = bossWarningMode === "final";
  const bossWarningPreview = finalBossPreview || bossWarningMode === "regular";
  const bossPreview = bossWarningPreview || bossFightPreview;
  const [warningVisible, setWarningVisible] = useState(bossWarningPreview);
  const requestedStageId = previewParams.get("stage");
  const previewStageId = /^stage\.(0[1-9]|10)-(0[1-9]|10)$/.test(requestedStageId ?? "")
    ? requestedStageId!
    : finalBossPreview ? "stage.01-10" : "stage.01-07";
  const finalStageOnly = previewStageId.endsWith("-10");
  const previewIsBoss = bossPreview || finalStageOnly;
  const previewVisual = stageVisual(previewStageId);
  const previewMonsterId: ChapterOneMonsterId = previewIsBoss || previewVisual.normals.length === 0 ? previewVisual.boss : previewVisual.normals[0];
  const previewCasterVfx: SkillVfx[] = [
    ...(skillVfxMode === "buffs" || skillVfxMode === "all" ? [
      { kind: "haste-caster" as const, frame: 0, frameCount: 8, loop: true, expiring: expiringVfxPreview },
      { kind: "basic-amp-caster" as const, frame: 0, frameCount: 8, loop: true, expiring: expiringVfxPreview },
    ] : []),
  ];
  const previewTargetVfx: SkillVfx[] = [
    ...(skillVfxMode === "heavy" || skillVfxMode === "all" ? [{ kind: "heavy-target" as const, frame: 0, frameCount: 8 }] : []),
    ...(skillVfxMode === "dot" || skillVfxMode === "all" ? [{ kind: "dot-target" as const, frame: 0, frameCount: 8, loop: true }] : []),
  ];
  const tutorialChapter = TUTORIAL_PREVIEW_CHAPTERS.find((chapter) => chapter.id === tutorialChapterId)
    ?? TUTORIAL_PREVIEW_CHAPTERS[0];
  const applyPreviewCosmeticCommand = (command: { kind: "equip"; slot: string; cosmeticId: string | null } | { kind: "register"; cosmeticId: string }) => {
    if (command.kind !== "equip") return;
    setPreviewCollection((current) => {
      const next = { ...current, equipment: { ...current.equipment, [command.slot]: command.cosmeticId } };
      client.setQueryData(["cosmetic-collection"], next);
      return next;
    });
  };
  const executePreviewGemFusion = async (mode: GemFusionMode, gemIds: string[], _options?: { targetLevel: number; allowedOptions: GemOption[] }): Promise<GemFusionResult> => {
    const current = client.getQueryData<GemState>(["gems"]) ?? previewTutorialGemState;
    const consumed = current.gems.filter((gem) => gemIds.includes(gem.gemId));
    const source = consumed[0];
    if (!source || gemIds.length !== 3) throw new Error("연습용 보석 세 개를 선택해 주세요.");
    const granted = previewGem(`tutorial-fusion-result-${mode.toLowerCase()}`, source.level + 1, source.option);
    const nextState = { ...current, gems: [...current.gems.filter((gem) => !gemIds.includes(gem.gemId)), granted] };
    client.setQueryData(["gems"], nextState);
    return { consumedGemIds: gemIds, granted: [granted], state: nextState };
  };
  function presentTutorialStep(step: TutorialPreviewStep) {
    globalThis.scrollTo(0, 0);
    setCharacterOpen(false);
    setProfileOpen(false);
    if (step.surface !== "cosmetics" && step.surface !== "character-cosmetics" && step.surface !== "character-gallery") {
      setCosmeticTutorialView("gacha");
    }
    switch (step.surface) {
      case "battle": setActiveRoute("battle"); break;
      case "inventory": setActiveRoute("inventory"); break;
      case "equipment": setActiveRoute("equipment"); break;
      case "skills": setActiveRoute("skills"); break;
      case "dungeon": setActiveRoute("dungeon"); break;
      case "gems": setActiveRoute("gems"); break;
      case "gems-fusion":
        setActiveRoute("gems");
        globalThis.setTimeout(() => document.querySelector<HTMLButtonElement>(".gem-management-tabs button:nth-of-type(2)")?.click(), 0);
        break;
      case "cosmetics":
        setCosmeticTutorialView("gacha");
        setActiveRoute("cosmetics");
        break;
      case "character-cosmetics":
        setCosmeticTutorialView("wardrobe");
        setActiveRoute("cosmetics");
        break;
      case "character-gallery":
        setCosmeticTutorialView("gallery");
        setActiveRoute("cosmetics");
        break;
      case "market-buy":
        setMarketInitialMode("buy");
        setActiveRoute("market");
        break;
      case "market-sell":
        setMarketInitialMode("sell");
        setActiveRoute("market");
        break;
      case "market-history":
        setMarketInitialMode("history");
        setActiveRoute("market");
        break;
      case "ranking": setActiveRoute("ranking"); break;
      case "settings":
        setProfileInitialTab("settings");
        setActiveRoute("settings");
        break;
    }
  }
  const moveToNextTutorialChapter = () => {
    const currentIndex = TUTORIAL_PREVIEW_CHAPTERS.findIndex((chapter) => chapter.id === tutorialChapter.id);
    const nextChapter = TUTORIAL_PREVIEW_CHAPTERS[(currentIndex + 1) % TUTORIAL_PREVIEW_CHAPTERS.length];
    setTutorialChapterId(nextChapter.id);
    presentTutorialStep(nextChapter.steps[0]);
    setTutorialVisible(true);
  };
  useEffect(() => {
    if (!tutorialPreviewEnabled || tutorialInitialized.current) return;
    tutorialInitialized.current = true;
    presentTutorialStep(requestedTutorialChapter.steps[0]);
  }, [requestedTutorialChapter, tutorialPreviewEnabled]);
  useEffect(() => {
    client.setQueryDefaults(["equipment"], { staleTime: Number.POSITIVE_INFINITY, retry: false });
    client.setQueryDefaults(["battle-history"], { staleTime: Number.POSITIVE_INFINITY, retry: false });
    client.setQueryDefaults(["combat-power-ranking"], { staleTime: Number.POSITIVE_INFINITY, retry: false });
    /* 미리보기에는 로그인이 없다. 세션을 다시 물어보면 랭킹이 로그인 안내로 덮인다. */
    client.setQueryDefaults(["auth", "session"], { staleTime: Number.POSITIVE_INFINITY, retry: false });
    if (!client.getQueryData(["equipment"])) client.setQueryData(["equipment"], previewEquipmentState);
    if (!client.getQueryData(["battle-history"])) client.setQueryData(["battle-history"], previewBattleHistory);
    if (!client.getQueryData(["combat-power-ranking"])) client.setQueryData(["combat-power-ranking"], previewCombatPowerRanking);
    // 캐릭터 탭의 전투력 줄은 한 명치만 따로 받는다. 미리보기에서도 같은 값이 보이게 둔다.
    client.setQueryDefaults(["combat-power", "me"], { staleTime: Number.POSITIVE_INFINITY, retry: false });
    if (!client.getQueryData(["combat-power", "me"])) client.setQueryData(["combat-power", "me"], previewCombatPowerRanking);
    client.setQueryData(["auth", "session"], { authenticated: true, account: previewAccount });
    // 미리보기에서도 착용한 치장이 전투·던전 캐릭터에 그대로 올라가야 한다.
    if (!client.getQueryData(["cosmetic-collection"])) client.setQueryData(["cosmetic-collection"], previewCosmeticCollection);
    if (!client.getQueryData(["cosmetic-catalog"])) client.setQueryData(["cosmetic-catalog"], previewCosmeticCatalog);
    if (!client.getQueryData(["gems"])) client.setQueryData(["gems"], previewTutorialGemState);
  }, [client]);
  useEffect(() => {
    setWarningVisible(bossWarningPreview);
    if (!bossWarningPreview || !liveWarningPreview) return;
    const timer = globalThis.setTimeout(() => setWarningVisible(false), finalBossPreview ? 4_200 : 2_800);
    return () => globalThis.clearTimeout(timer);
  }, [bossWarningPreview, finalBossPreview, liveWarningPreview]);
  const navigate = (route: BattleHudRoute) => {
    if (route === "character") {
      setCharacterOpen(true);
      return;
    }
    if (route === "settings") {
      setProfileInitialTab("settings");
      setProfileOpen(true);
      return;
    }
    if (route === "market") setMarketInitialMode("buy");
    if (route === "battle" || route === "equipment" || route === "inventory" || route === "skills" || route === "gems" || route === "dungeon" || route === "market" || route === "mail" || route === "ranking" || route === "firstClearRewards" || route === "cosmetics") setActiveRoute(route);
  };

  const noticeWindow = <ServerResetNotice forceOpen={previewParams.get("notice") === "1"} />;
  const characterWindow = <CharacterWindow open={characterOpen} initialTab={characterInitialTab} previewData={{ ...previewCharacterData, collection: previewCollection }} onClose={() => setCharacterOpen(false)} onGacha={() => { setCharacterOpen(false); setActiveRoute("cosmetics"); }} />;
  const profileWindow = <ProfileScreen open={profileOpen} initialTab={profileInitialTab} onClose={() => setProfileOpen(false)} />;
  /* 실제 게임과 같은 화면별 다시 보기 단추를 검수 화면에서도 그대로 보여 준다. */
  const previewReplaySurface: TutorialRuntimeSurface | null =
    activeRoute === "equipment" || activeRoute === "skills" || activeRoute === "gems"
      || activeRoute === "dungeon" || activeRoute === "market" ? activeRoute
      : activeRoute === "cosmetics" && cosmeticTutorialView === "gacha" ? "cosmetics"
      : null;
  const tutorialControls = <>
    <TutorialScreenReplay surface={previewReplaySurface} />
    {tutorialPreviewEnabled && <nav className={`tutorial-preview-navigator ${tutorialMenuOpen ? "is-open" : "is-collapsed"}`} aria-label="튜토리얼 장 미리보기">
      <button type="button" className="tutorial-preview-menu-toggle" onClick={() => setTutorialMenuOpen((current) => !current)}>
        {tutorialMenuOpen ? "튜토리얼 목록 접기" : `${tutorialChapter.shortLabel} · 목록`}
      </button>
      {tutorialMenuOpen && <>
        <strong>튜토리얼 전체 보기</strong>
        <div>
          {TUTORIAL_PREVIEW_CHAPTERS.map((chapter) => <button
            key={chapter.id}
            type="button"
            className={chapter.id === tutorialChapter.id ? "active" : ""}
            aria-pressed={chapter.id === tutorialChapter.id}
            onClick={() => {
              setTutorialChapterId(chapter.id);
              presentTutorialStep(chapter.steps[0]);
              setTutorialVisible(true);
              setTutorialMenuOpen(false);
            }}
          >{chapter.shortLabel}</button>)}
        </div>
        <button type="button" className="tutorial-preview-toggle" onClick={() => setTutorialVisible((current) => !current)}>
          {tutorialVisible ? "대화 숨기기" : "대화 다시 보기"}
        </button>
      </>}
    </nav>}
    {tutorialVisible && <TutorialDialogue
      key={tutorialChapter.id}
      chapterLabel={tutorialChapter.label}
      characterSrc={tutorialChapter.characterSrc}
      characterAlt={tutorialChapter.characterAlt}
      characterSprite={{ columns: 3, rows: 2, frames: [0, 1, 2, 5] }}
      steps={tutorialChapter.steps}
      isTargetAction={(step) => (step as TutorialPreviewStep).advanceMode === "target"}
      onStepChange={(step, index) => {
        presentTutorialStep(step as TutorialPreviewStep);
        if (index > 0) setTutorialMenuOpen(false);
      }}
      onComplete={moveToNextTutorialChapter}
      onDismiss={() => setTutorialVisible(false)}
    />}
  </>;

  const usesPreviewWindowStage = activeRoute === "equipment" || activeRoute === "inventory" || activeRoute === "skills" || activeRoute === "gems" || activeRoute === "settings";
  const usesWindowSurface = activeRoute === "equipment" || activeRoute === "inventory" || activeRoute === "skills" || activeRoute === "gems" || activeRoute === "market" || activeRoute === "mail" || activeRoute === "settings";
  const usesManagementPage = activeRoute !== "battle" && !usesWindowSurface;
  if (usesManagementPage) return <>
    <main className={`battle-hud-preview game-shell cozy-management-shell${activeRoute === "ranking" ? " cozy-ranking-shell" : ""}${activeRoute === "cosmetics" ? " cozy-cosmetics-shell" : ""}`}>
      <CozySharedNavigation account={previewAccount} activeRoute={activeRoute} onOpenProfile={() => {
        setProfileInitialTab("profile");
        setProfileOpen(true);
      }} onNavigate={navigate} gemContentUnlocked cosmeticContentUnlocked={previewCharacterStats.cosmeticsUnlocked} topBarOnly={activeRoute === "ranking" || activeRoute === "cosmetics" || activeRoute === "dungeon"} />
      {activeRoute === "ranking" && <RankingScreen />}
      {activeRoute === "firstClearRewards" && <div className="cozy-management-content"><FirstClearRewardScreen dataSource={previewFirstClearRewardDataSource} /></div>}
      {activeRoute === "cosmetics" && cosmeticTutorialView === "gacha" && <CosmeticsPreviewScreen tutorialRewardId={tutorialChapterId === "stylist" ? "preview-2-head" : undefined} />}
      {activeRoute === "cosmetics" && cosmeticTutorialView === "wardrobe" && <div className="tutorial-character-content"><CostumePanel collection={previewCollection} catalog={previewCharacterData.catalog} pending={false} send={applyPreviewCosmeticCommand} sendBatch={(commands) => commands.forEach(applyPreviewCosmeticCommand)} onGacha={() => setCosmeticTutorialView("gacha")} /></div>}
      {activeRoute === "cosmetics" && cosmeticTutorialView === "gallery" && <div className="tutorial-character-content"><GalleryPanel collection={previewCollection} catalog={previewCharacterData.catalog} pending={false} send={applyPreviewCosmeticCommand} /></div>}
      {activeRoute === "dungeon" && <GemsScreen mode="dungeon" />}
      {characterWindow}
      {profileWindow}
      {noticeWindow}
    </main>
    {tutorialControls}
  </>;

  return <>
  <main className={`battle-hud-preview ${liveWarningPreview ? "is-live-warning-preview" : ""}`}>
    <section className="battle-stage-shell cozy-battle-stage-shell">
      <BattleProfileSummary account={previewAccount} onOpen={() => {
        setProfileInitialTab("profile");
        setProfileOpen(true);
      }} />
      <BattleHistoryPopover />
      <TutorialLauncher />
      <BattleStageProgress stages={stages} selectedStageId={previewStageId} remainingSeconds={21} defeatedNormals={finalStageOnly ? 0 : bossPreview ? 20 : 7} bossOnly={finalStageOnly} bossPhase={previewIsBoss} stageComplete={false} idleMode="AUTO_PROGRESS" idleModeSaving={false} repeatEligible={!finalStageOnly} bossHealth={previewIsBoss && !warningVisible ? { name: MONSTER_NAMES[previewMonsterId], hp: 517, maxHp: 782 } : null} onSelectStage={() => undefined} onChangeMode={() => undefined} />
      <BattleUtilityNavigation onNavigate={navigate} gemContentUnlocked cosmeticContentUnlocked={previewCharacterStats.cosmeticsUnlocked} />
      <PictureInPictureController />
      <div className={`battle-kitchen ${warningVisible ? "has-boss-warning" : ""}`} style={{ "--battle-background": `url(${backgroundAsset(null, previewStageId)})` } as React.CSSProperties}>
        <div className="kitchen-backdrop" aria-hidden="true" />
        <div className="battle-floor is-engaged" aria-label="자동전투 장면 미리보기">
          <div className="hero-position"><PlayerBattleCharacter appearance={previewCollection} catalog={previewCosmeticCatalog} motion="rest" skillVfx={previewCasterVfx} /></div>
          <div className="monster-party"><ChapterOneMonster id={previewMonsterId} motion="idle" boss={previewIsBoss} hp={bossFightPreview ? 517 : 782} maxHp={782} skillVfx={previewTargetVfx} /></div>
        </div>
        <BattleRewards stageId={previewStageId} rice={1_284} rewards={[
          { itemId: "SWEET_POTATO_M1", requestedQuantity: 12, grantedQuantity: 12, discardedQuantity: 0 },
          { itemId: "CORN_M1", requestedQuantity: 3, grantedQuantity: 3, discardedQuantity: 0 },
          { itemId: "POTATO_M1", requestedQuantity: 7, grantedQuantity: 7, discardedQuantity: 0 },
          { itemId: "SKILL_BOOK", requestedQuantity: 1, grantedQuantity: 1, discardedQuantity: 0 },
        ]} />
        <BattlePlayerStatus level={24} hp={2_680} maxHp={3_680} experience={14_976} experienceRequired={24_000} experiencePercent={62.4} />
        <BattleGrowthNavigation onNavigate={navigate} gemContentUnlocked />
      </div>
      <ChatPanel />
      {warningVisible && <BossArrivalWarning monsterId={previewMonsterId} stageId={previewStageId} finalBoss={finalBossPreview} />}
    </section>
    {/*
      * 거래소·메시지함은 dialog 가 아니라 화면을 덮는 칸이라 이 무대가 필요 없다.
      * 무대를 씌우면 빈 장막만 덮여 창이 열리지 않은 것처럼 보였다.
      */}
    {tutorialPreviewEnabled && usesPreviewWindowStage && <div className="tutorial-preview-window-stage">
      {activeRoute === "equipment" && <div className="equipment-window tutorial-preview-window"><EquipmentScreen previewData={previewEquipmentState} previewCommand={executePreviewEquipmentCommand} /></div>}
      {activeRoute === "inventory" && <div className="inventory-window tutorial-preview-window"><InventoryScreen dataSource={previewInventoryDataSource} /></div>}
      {activeRoute === "skills" && <div className="skills-window tutorial-preview-window"><SkillsScreen /></div>}
      {activeRoute === "gems" && <div className="gem-management-dialog tutorial-preview-window"><GemManagement previewFuse={executePreviewGemFusion} /></div>}
      {activeRoute === "settings" && <div className="tutorial-preview-window"><ProfileScreen open inline initialTab="settings" onClose={() => setActiveRoute("battle")} /></div>}
    </div>}
    {!tutorialPreviewEnabled && activeRoute === "equipment" && <EquipmentWindow open previewData={previewEquipmentState} previewCommand={executePreviewEquipmentCommand} onClose={() => setActiveRoute("battle")} />}
    {!tutorialPreviewEnabled && activeRoute === "inventory" && <InventoryWindow open dataSource={previewInventoryDataSource} onClose={() => setActiveRoute("battle")} onSell={(itemId) => {
      selectMarketItemForSale(window.sessionStorage, itemId);
      setMarketInitialMode("sell");
      setActiveRoute("market");
    }} />}
    {!tutorialPreviewEnabled && activeRoute === "skills" && <SkillWindow open onClose={() => setActiveRoute("battle")} />}
    {!tutorialPreviewEnabled && activeRoute === "gems" && <GemsScreen mode="manage" onClose={() => setActiveRoute("battle")} />}
    {activeRoute === "market" && <MarketPlaza key={marketInitialMode} initialMode={marketInitialMode} pollingEnabled={false} onClose={() => setActiveRoute("battle")} />}
    {activeRoute === "mail" && <MailScreen dataSource={previewMailDataSource} onClose={() => setActiveRoute("battle")} />}
    {!tutorialPreviewEnabled && activeRoute === "settings" && <ProfileScreen open initialTab="settings" onClose={() => setActiveRoute("battle")} />}
    {characterWindow}
    {profileWindow}
    {noticeWindow}
  </main>
  {tutorialControls}
  </>;
}

/*
 * 프리뷰가 쓰는 자료는 전부 아래에서 심어 둔 가짜다. react-query 는 화면을 닫고
 * 5분(gcTime 기본값)이 지나면 이 자료를 치우는데, 그러면 보석·스킬·거래소를 다시 열 때
 * 없는 서버로 진짜 요청이 나가 영영 "불러오는 중"에 머문다. 캐릭터·장비·아이템은
 * 값을 props 로 받아 멀쩡해 보이는 탓에 세 화면만 안 열리는 것처럼 보였다.
 */
const queryClient = new QueryClient({
  defaultOptions: {
    queries: { retry: false, gcTime: Number.POSITIVE_INFINITY, staleTime: Number.POSITIVE_INFINITY },
    mutations: { retry: false },
  },
});
const dungeonPreviewBattle = new URLSearchParams(globalThis.location.search).get("battle") === "1";
const dungeonPreviewNow = Date.now();
const dungeonPreviewChallenge = dungeonPreviewBattle ? {
  challengeId: "preview-dungeon-udon",
  boss: "SURVIVAL" as const,
  stage: 5,
  status: "ACTIVE" as const,
  minimumCompleteAt: new Date(dungeonPreviewNow + 15_000).toISOString(),
  expiresAt: new Date(dungeonPreviewNow + 30_000).toISOString(),
  rewardGemBoxes: 1,
  contentVersion: "preview",
  battle: {
    success: true,
    failureCode: null,
    elapsedTicks: 150,
    remainingPlayerHp: 1_540,
    remainingBossHp: 0,
    events: [
      { sequence: 1, tick: 0, type: "BATTLE_START" as const, skillId: null, amount: 0, playerHp: 2_008, bossHp: 44_308, critical: false },
      { sequence: 2, tick: 12, type: "SKILL_CAST" as const, skillId: "active_haste", amount: 0, playerHp: 2_008, bossHp: 44_308, critical: false },
      { sequence: 3, tick: 24, type: "SKILL_CAST" as const, skillId: "active_heavy", amount: 0, playerHp: 2_008, bossHp: 44_308, critical: false },
      { sequence: 4, tick: 25, type: "PLAYER_HIT" as const, skillId: "active_heavy", amount: 551, playerHp: 2_008, bossHp: 43_757, critical: true },
      { sequence: 5, tick: 41, type: "SKILL_CAST" as const, skillId: "active_basic_amp", amount: 0, playerHp: 2_008, bossHp: 43_757, critical: false },
      { sequence: 6, tick: 67, type: "SURVIVAL_STRIKE" as const, skillId: null, amount: 468, playerHp: 1_540, bossHp: 32_104, critical: false },
      { sequence: 7, tick: 87, type: "SKILL_CAST" as const, skillId: "active_dot", amount: 0, playerHp: 1_540, bossHp: 32_104, critical: false },
      { sequence: 8, tick: 121, type: "PLAYER_HIT" as const, skillId: null, amount: 551, playerHp: 1_540, bossHp: 8_420, critical: false },
      { sequence: 9, tick: 150, type: "VICTORY" as const, skillId: null, amount: 8_420, playerHp: 1_540, bossHp: 0, critical: false },
    ],
  },
} : null;
queryClient.setQueryDefaults(["gems"], { staleTime: Number.POSITIVE_INFINITY, retry: false });
queryClient.setQueryData(["gems"], previewTutorialGemState);
queryClient.setQueryDefaults(["gem-dungeons", "today"], { staleTime: Number.POSITIVE_INFINITY, retry: false });
queryClient.setQueryData(["gem-dungeons", "today", undefined], { boss: "SURVIVAL", tickets: 3, secondsUntilNextTicket: 27_142, progress: [{ boss: "SURVIVAL", highestClearedStage: 4 }, { boss: "BERSERK", highestClearedStage: 2 }, { boss: "ARMORED", highestClearedStage: 3 }], nextChallengeStage: 5, sweepStage: 4, activeChallenge: dungeonPreviewChallenge, testBossSelectionEnabled: false });
queryClient.setQueryDefaults(["combat-power-ranking"], { staleTime: Number.POSITIVE_INFINITY, retry: false });
queryClient.setQueryDefaults(["auth", "session"], { staleTime: Number.POSITIVE_INFINITY, retry: false });
queryClient.setQueryData(["auth", "session"], { authenticated: true, account: previewAccount });
queryClient.setQueryData(["combat-power-ranking"], previewCombatPowerRanking);
queryClient.setQueryDefaults(["chat", "messages"], { staleTime: Number.POSITIVE_INFINITY, retry: false });
queryClient.setQueryData(["chat", "messages"], previewChat);
queryClient.setQueryDefaults(["skills"], { staleTime: Number.POSITIVE_INFINITY, retry: false });
queryClient.setQueryData(["skills"], previewSkillState);
queryClient.setQueryDefaults(["market-instruments"], { staleTime: Number.POSITIVE_INFINITY, retry: false });
queryClient.setQueryData(["market-instruments"], previewMarketCatalog());
queryClient.setQueryData(["market-order-book", previewMarketInstrument.instrumentId], {
  instrumentId: previewMarketInstrument.instrumentId,
  marketRevision: 1,
  bids: [{ unitPrice: 90, totalQuantity: 80, myQuantity: 0 }],
  asks: [{ unitPrice: 95, totalQuantity: 120, myQuantity: 0 }],
  bestBidUnitPrice: 90,
  bestAskUnitPrice: 95,
  spread: 5,
  recentTrades: [],
});
queryClient.setQueryData(["inventory", "market", previewMarketInstrument.itemId], { availableQuantity: 530 });
queryClient.setQueryData(["market-orders"], { items: [], nextCursor: null, totalItems: 0, readThroughSequence: 0 });
queryClient.setQueryData(["market-orders", "active"], { items: [], nextCursor: null, totalItems: 0, readThroughSequence: 0 });
queryClient.setQueryData(["market-orders", "closed"], { items: [], nextCursor: null, totalItems: 0, readThroughSequence: 0 });
queryClient.setQueryData(["market-deliveries"], { items: [], nextCursor: null, totalItems: 0, readThroughSequence: 0 });
queryClient.setQueryData(["market-mails"], { items: [], nextCursor: null, totalItems: 0, readThroughSequence: 0 });
queryClient.setQueryData(["mails"], { items: previewMails, nextCursor: null, totalItems: previewMails.length });
queryClient.setQueryData(["market-summary"], { activeOrders: 0, closedOrders: 0, recoveryReviewOrders: 0, claimableDeliveries: 0, claimableSettlements: 0, unread: [] });
queryClient.setQueryData(["market-trades", previewMarketInstrument.instrumentId], { items: [], nextCursor: null, totalItems: 0, readThroughSequence: 0 });
queryClient.setQueryData(["market-trades", "me"], []);

const root = document.getElementById("root");
if (root) createRoot(root).render(
  <React.StrictMode><QueryClientProvider client={queryClient}><BattleHudPreview /></QueryClientProvider></React.StrictMode>,
);
