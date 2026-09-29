// @vitest-environment happy-dom

import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderToStaticMarkup } from "react-dom/server";
import { afterEach, expect, it, vi } from "vitest";
import { EquipmentScreen, equipmentMaterialDisplayName, shortagePurchaseQuantity } from "./EquipmentScreen";
import { equipmentApi, type EquipmentActionSummary, type EquipmentGrowthSummary, type EquipmentMaterialCost, type EquipmentSlotSummary, type EquipmentState, type EquipmentStatSummary } from "./api";
import { rankingApi, type CombatPowerRanking } from "../ranking/api";

afterEach(() => { cleanup(); vi.restoreAllMocks(); });

const material = (itemId: string, displayName: string, requiredQuantity: number, availableQuantity: number): EquipmentMaterialCost => ({ itemId, displayName, requiredQuantity, availableQuantity });
const growth = (grade: "NORMAL" | "RARE" | "EPIC" | "LEGENDARY" | null, gradeName: string | null, enhancementLevel: number, q: number, attack: number, maxHp: number, penetration: number): EquipmentGrowthSummary => ({ grade, gradeName, enhancementLevel, q, stats: { attack, maxHp, penetration } });
const action = (riceCost: number, materials: EquipmentMaterialCost[], result: EquipmentGrowthSummary, statIncrease: EquipmentStatSummary, executable: boolean, disabledReason: string | null): EquipmentActionSummary => ({ cost: { riceCost, materials }, result, statIncrease, executable, disabledReason });


const slots: EquipmentSlotSummary[] = [
  { slot: "WEAPON", slotName: "무기", unlocked: false, current: growth(null, null, 0, 0, 36, 0, 0), unlock: action(100, [material("POTATO_M1", "감자 M1", 10, 10), material("SWEET_POTATO_M1", "고구마 M1", 10, 10), material("CORN_M1", "옥수수 M1", 10, 10)], growth("NORMAL", "노말", 1, 1, 37, 0, 0), { attack: 37, maxHp: 0, penetration: 0 }, true, null), enhance: null, promote: null, growthComplete: false },
  { slot: "GLOVES", slotName: "장갑", unlocked: true, current: growth("NORMAL", "노말", 1, 1, 25, 0, 0), unlock: null, enhance: action(30, [material("POTATO_M1", "감자 M1", 125, 125)], growth("NORMAL", "노말", 2, 2, 26, 0, 0), { attack: 1, maxHp: 0, penetration: 0 }, true, null), promote: null, growthComplete: false },
  { slot: "ARMOR", slotName: "갑옷", unlocked: true, current: growth("NORMAL", "노말", 29, 29, 0, 1_480, 0), unlock: null, enhance: action(870, [], growth("NORMAL", "노말", 30, 30, 0, 1_560, 0), { attack: 0, maxHp: 80, penetration: 0 }, true, null), promote: { requiredStageId: "stage.01-10", chapterCleared: false, maxEnhancementReached: false, cost: { riceCost: 1_000, materials: [material("POTATO_M2", "감자 M2", 10, 10)] }, result: growth("RARE", "희귀", 1, 40, 0, 2_490, 0), executable: false, disabledReason: "EQUIPMENT_MAX_ENHANCEMENT_REQUIRED" }, growthComplete: false },
  { slot: "HELMET", slotName: "투구", unlocked: true, current: growth("RARE", "희귀", 30, 69, 0, 7_700, 0), unlock: null, enhance: null, promote: { requiredStageId: "stage.02-10", chapterCleared: true, maxEnhancementReached: true, cost: { riceCost: 5_000, materials: [] }, result: growth("EPIC", "영웅", 1, 79, 0, 18_900, 0), executable: true, disabledReason: null }, growthComplete: false },
  { slot: "CAPE", slotName: "망토", unlocked: true, current: growth("LEGENDARY", "전설", 30, 147, 0, 0, 529), unlock: null, enhance: null, promote: null, growthComplete: true },
  { slot: "SHOES", slotName: "신발", unlocked: true, current: growth("NORMAL", "노말", 1, 1, 0, 0, 2), unlock: null, enhance: action(30, [material("CORN_M1", "옥수수 M1", 125, 0)], growth("NORMAL", "노말", 2, 2, 0, 0, 5), { attack: 0, maxHp: 0, penetration: 3 }, false, "INSUFFICIENT_MATERIALS"), promote: null, growthComplete: false },
];

it("buys the complete shortage above the old marketplace cap", () => {
  expect(shortagePurchaseQuantity(material("CORN_M1", "옥수수 M1", 1_500, 100))).toBe(1_400);
  expect(shortagePurchaseQuantity(material("CORN_M1", "옥수수 M1", 125, 25))).toBe(100);
});

it("uses player-facing material names instead of internal M-level names", () => {
  expect(equipmentMaterialDisplayName(material("SWEET_POTATO_M1", "고구마 M1", 125, 0))).toBe("고구마 한 조각");
  expect(equipmentMaterialDisplayName(material("CORN_M2", "옥수수 M2", 100, 0))).toBe("미니 옥수수");
});

it("renders six equipment cards and every required material in a single-screen list", () => {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  const rareWeapon = {
    ...slots[0],
    unlocked: true,
    current: growth("RARE", "희귀", 1, 40, 96, 0, 0),
    unlock: null,
    enhance: action(930, [
      material("POTATO_M1", "감자 조각", 500, 420), material("SWEET_POTATO_M1", "고구마 조각", 500, 620), material("CORN_M1", "옥수수 낱알", 500, 391),
      material("POTATO_M2", "미니 감자", 100, 72), material("SWEET_POTATO_M2", "미니 고구마", 100, 114), material("CORN_M2", "미니 옥수수", 100, 87),
    ], growth("RARE", "희귀", 2, 41, 98, 0, 0), { attack: 2, maxHp: 0, penetration: 0 }, false, "INSUFFICIENT_MATERIALS"),
  };
  const previewData = { slots: [rareWeapon, ...slots.slice(1)], riceBalance: 2_000 } as EquipmentState;
  const html = renderToStaticMarkup(<QueryClientProvider client={client}><EquipmentScreen previewData={previewData} /></QueryClientProvider>);

  expect((html.match(/class="equipment-slot-card/g) ?? []).length).toBe(6);
  const root = document.createElement("div");
  root.innerHTML = html;
  const selectionMarker = root.querySelector(".equipment-slot-card[aria-pressed='true'] .equipment-slot-selection");
  expect(selectionMarker).not.toBeNull();
  expect(selectionMarker?.textContent).toBe("");
  expect((html.match(/class="equipment-material-row/g) ?? []).length).toBe(6);
  expect(html).not.toContain("보유 재화");
  expect(html).toContain("장비 부위 선택");
  expect(html).toContain("기본 나무검을 든 젓가락 캐릭터");
  expect(html).toContain("/assets/chapters/chapter-04-sushi/hero/rest_01.png?v=basic-wooden-sword-v1-20260911");
  expect(html).toContain("현재 상태");
  expect(html).toContain("다음 강화");
  expect(html).toContain("필요 재료");
  expect(html).toContain("필요 쌀");
  expect(html).toContain("현재 보유 2,000, 필요 930");
  expect(html).toContain("항목이 많으면 위아래로 드래그할 수 있습니다.");
  expect(html).toContain('tabindex="0"');
  expect(html).not.toContain("누적 강화 재료");
  expect(html).not.toContain("현재 등급까지 필요한 모든 재료예요.");
  expect(html).not.toContain("F + D 등급 사용");
  expect(html).not.toContain("장비 상태를 불러오지 못했습니다.");
  expect(html).toContain("고구마 한 조각");
  expect(html).toContain("미니 감자");
  expect(html).toContain(">420</strong><span>/ 500</span>");
  expect(html).toContain(">114</strong><span>/ 100</span>");
  expect(html).not.toContain("부족 80");
  expect((html.match(/class="equipment-material-market"/g) ?? []).length).toBe(4);
  expect(html).toContain("거래소 구매");
  expect(html).toContain('aria-label="재료 충족"');
  expect(html).toContain(">강화</button>");
  expect(html).toContain('aria-label="증가 2"');
  expect(html).not.toContain('aria-label="증가 0"');
});

it("renders an empty market quote without blanking the equipment screen", () => {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  client.setQueryData(["equipment"], { slots, riceBalance: 2_000 });
  client.setQueryData(["equipment-market-quote", "CORN_M1", 125], { itemId: "CORN_M1", requestedQuantity: 125, purchasableQuantity: 0, riceBalance: 2_000, marketRevision: 1 });
  const html = renderToStaticMarkup(<QueryClientProvider client={client}><EquipmentScreen /></QueryClientProvider>);

  expect(html).toContain("id=\"equipment-title\">장비</h2>");
  expect(html).toContain("aria-label=\"무기 상세\"");
});

it("updates equipment without a top success banner and shows the server combat-power gain in the center", async () => {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  const nextWeapon = { ...slots[0], unlocked: true, current: growth("NORMAL", "노말", 1, 1, 37, 0, 0), unlock: null };
  const nextState = { slots: [nextWeapon, ...slots.slice(1)], riceBalance: 1_900 } as EquipmentState;
  const ranking = (combatPower: number): CombatPowerRanking => ({
    formulaVersion: "combat-power-v2",
    generatedAt: "2026-09-11T00:00:00Z",
    sourceStateVersion: 1,
    overallTop: [],
    specializations: [],
    myEntry: {
      rank: 1,
      overallRank: 1,
      nickname: "테스터",
      level: 1,
      materialType: "POTATO",
      displayName: "감자 전문",
      combatPower,
      appearance: { head: null, top: null, bottom: null, gloves: null, shoes: null, cape: null },
      updatedAt: "2026-09-11T00:00:00Z",
    },
  });
  vi.spyOn(rankingApi, "combatPower").mockResolvedValueOnce(ranking(100)).mockResolvedValueOnce(ranking(105));
  vi.spyOn(equipmentApi, "command").mockResolvedValue({ slot: nextWeapon, state: nextState });

  const { container } = render(<QueryClientProvider client={client}><EquipmentScreen previewData={{ slots, riceBalance: 2_000 }} /></QueryClientProvider>);
  fireEvent.click(screen.getByRole("button", { name: "제작" }));

  await waitFor(() => expect(screen.getByLabelText("전투력 5 상승")).toBeTruthy());
  expect(container.querySelector(".equipment-result")).toBeNull();
  expect(screen.queryByText(/상태를 업데이트했습니다/)).toBeNull();
});

it("keeps the equipment close control visible and calls the close handler", () => {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  const onClose = vi.fn();
  render(<QueryClientProvider client={client}><EquipmentScreen previewData={{ slots, riceBalance: 2_000 }} onClose={onClose} /></QueryClientProvider>);

  const closeButton = screen.getByRole("button", { name: "장비 화면 닫기" });
  expect(closeButton.querySelector("img")).not.toBeNull();
  fireEvent.click(closeButton);
  expect(onClose).toHaveBeenCalledOnce();
});

it("shows purchase results in a confirm modal without replacing the equipment layout", async () => {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  vi.spyOn(equipmentApi, "marketInstruments").mockResolvedValue([{ instrumentId: "corn-m1", itemId: "CORN_M1", bestAskUnitPrice: 10 }]);
  vi.spyOn(equipmentApi, "purchaseQuote").mockResolvedValue({ instrumentId: "corn-m1", requestedQuantity: 125, expectedFilledQuantity: 125, expectedRemainingQuantity: 0, lowestFilledUnitPrice: 10, highestFilledUnitPrice: 10, weightedAverageUnitPrice: 10, totalPrice: 1_250, availableRice: 2_000, marketRevision: 1 });
  vi.spyOn(equipmentApi, "purchaseMaterial").mockResolvedValue({ result: { instrumentId: "corn-m1", initialQuantity: 125, filledQuantity: 125, remainingQuantity: 0, totalPrice: 1_250, highestFilledUnitPrice: 10 } });
  vi.spyOn(equipmentApi, "state").mockResolvedValue({ slots, riceBalance: 750 });

  const { container } = render(<QueryClientProvider client={client}><EquipmentScreen previewData={{ slots, riceBalance: 2_000 }} /></QueryClientProvider>);
  const originalLayout = container.querySelector(".equipment-layout");
  fireEvent.click(screen.getByRole("button", { name: /신발 노말 \+1/ }));
  fireEvent.click(screen.getByRole("button", { name: "옥수수 한 알 거래소에서 구매" }));
  await screen.findByText("현재 최저가");
  fireEvent.click(screen.getByRole("button", { name: /^구매$/ }));

  // 누를 것이 없는 결과는 창이 아니라 스스로 사라지는 가운데 알림으로 뜬다.
  const feedback = await screen.findByRole("status");
  expect(feedback.textContent).toContain("옥수수 한 알 125개를 1,250쌀에 구매했습니다.");
  expect(container.querySelector(".equipment-layout")).toBe(originalLayout);
  expect(container.querySelector(".equipment-result")).toBeNull();
  expect(screen.queryByRole("button", { name: "확인" })).toBeNull();
  await waitFor(() => expect(screen.queryByRole("status")).toBeNull(), { timeout: 4_000 });
});

it("answers a shortage with a centred notice when the market carries no listing", async () => {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  client.setQueryData(["equipment"], { slots, riceBalance: 2_000 });
  // 거래소에 해당 품목이 아예 없는 상태. 예전에는 빈 구매 창이 떠 아무 일도 없는 것처럼 보였다.
  client.setQueryData(["market-instruments"], []);
  render(<QueryClientProvider client={client}><EquipmentScreen /></QueryClientProvider>);

  fireEvent.click(screen.getByRole("button", { name: "신발 노말 +1" }));
  fireEvent.click(screen.getByRole("button", { name: `${equipmentMaterialDisplayName(material("CORN_M1", "옥수수 M1", 125, 0))} 거래소에서 구매` }));

  const notice = await screen.findByRole("status");
  expect(notice.textContent).toContain("거래소에 올라온 매물이 없습니다");
  expect(screen.queryByRole("dialog", { name: "거래소" })).toBeNull();
});
