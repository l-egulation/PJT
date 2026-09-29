/*
 * Fixtures for the market plaza preview harness.  They only need to be
 * plausible: shapes come from `api.ts`, numbers come from the design mockups.
 */
import type {
  MarketDelivery, MarketInstrument, MarketOrder, MarketOrderBook, MarketOrderQuote,
  MarketPage, MarketSettlementMail, MarketSummary, MarketTrade,
} from "./api";

const NOW = Date.UTC(2026, 8, 13, 5, 0, 0);
const ago = (minutes: number) => new Date(NOW - minutes * 60_000).toISOString();

function instrument(itemId: string, displayName: string, category: string, bid: number, ask: number, attributes: Record<string, string> = {}): MarketInstrument {
  return { instrumentId: `inst-${itemId}`, canonicalKey: itemId.toLowerCase(), itemId, displayName, category, attributes, status: "ACTIVE", bestBidUnitPrice: bid, bestAskUnitPrice: ask, lastTradeUnitPrice: ask, marketRevision: 12 };
}

/*
 * 미리보기 목록은 실제 카탈로그(StaticItemCatalog)와 같은 폭이어야 한다. 예전에는
 * 계열마다 등급을 한둘만 넣어 두어 목록이 비어 보였고, F~A 를 훑는 화면을 시험할 수
 * 없었다. 재료 3계열 × 5등급, 스킬북 6스킬 × 4등급, 보석 6옵션 × 7레벨을 모두 만든다.
 */
const MATERIAL_NAMES: Array<[family: string, names: string[], base: number]> = [
  ["POTATO", ["감자 한 조각", "미니 감자", "감자", "황금 감자", "전설 감자"], 52],
  ["SWEET_POTATO", ["고구마 한 조각", "미니 고구마", "고구마", "황금 고구마", "전설 고구마"], 47],
  ["CORN", ["옥수수 한 알", "미니 옥수수", "옥수수", "황금 옥수수", "전설 옥수수"], 55],
];
const SKILL_NAMES: Array<[skillId: string, name: string, base: number]> = [
  ["active_heavy", "한짝의 일격", 120],
  ["active_dot", "마! 쫄이나", 105],
  ["active_haste", "잘게 더 잘게!", 135],
  ["active_basic_amp", "화력 최대로!", 150],
  ["passive_critical", "회심의 간", 165],
  ["passive_all_damage", "오늘의 특선", 180],
];
const BOOK_GRADES: Array<[gradeId: string, name: string, multiplier: number]> = [
  ["normal", "노말", 1], ["rare", "희귀", 7], ["epic", "영웅", 28], ["legendary", "전설", 110],
];
/** 옵션마다 한 레벨이 올려 주는 값. 퍼센트 계열은 100 배로 담긴다. */
const GEM_OPTIONS: Array<[optionId: string, name: string, step: number]> = [
  ["flat_attack", "고정 공격력", 4],
  ["flat_hp", "고정 최대 HP", 40],
  ["attack_percent", "공격력%", 150],
  ["flat_penetration", "고정 방어 관통", 3],
  ["critical_chance", "치명타 확률", 120],
  ["attack_speed", "공격속도", 100],
];

/** 파는 값이 먼저고 사는 값은 그보다 조금 낮다. 미리보기라 벌어짐은 한결같게 둔다. */
function priced(ask: number): [bid: number, ask: number] {
  return [Math.round(ask * 0.94), ask];
}

/*
 * 실제 시장에는 지금 파는 사람이 없는 물품이 섞여 있다. 미리보기가 전부 팔리고 있는
 * 상태만 보여 주면, 목록이 그런 줄을 빼는지 눈으로 확인할 수 없다.
 */
const UNSOLD = new Set(["CORN_M5", "SWEET_POTATO_M4", "skillbook:active_heavy:legendary", "gem:7:flat_hp", "gem:6:attack_percent"]);
/* 아직 아무도 사지도 팔지도, 거래한 적도 없는 물품. 첫 거래를 걸 수 있는지 보려면 필요하다. */
const NEVER_TRADED = new Set(["skillbook:passive_all_damage:legendary", "gem:7:critical_chance"]);

const INSTRUMENTS: MarketInstrument[] = ([
  ...MATERIAL_NAMES.flatMap(([family, names, base]) => names.map((displayName, index) => {
    const [bid, ask] = priced(Math.round(base * 4.6 ** index));
    return instrument(`${family}_M${index + 1}`, displayName, "MATERIAL", bid, ask);
  })),
  ...SKILL_NAMES.flatMap(([skillId, name, base]) => BOOK_GRADES.map(([gradeId, gradeName, multiplier]) => {
    const [bid, ask] = priced(Math.round(base * multiplier));
    return instrument(`skillbook:${skillId}:${gradeId}`, `${gradeName} ${name} 비법서`, "SKILL_BOOK", bid, ask, { grade: gradeId, skillId });
  })),
  ...GEM_OPTIONS.flatMap(([optionId, optionName, step]) => Array.from({ length: 7 }, (_, index) => {
    const level = index + 1;
    const [bid, ask] = priced(Math.round(90 * 2.4 ** index));
    return instrument(`gem:${level}:${optionId}`, `${level}레벨 ${optionName} 보석`, "GEM", bid, ask, { level: String(level), option: optionId, value: String(step * level) });
  })),
] as MarketInstrument[]).map(item => {
  if (NEVER_TRADED.has(item.itemId)) return { ...item, bestAskUnitPrice: null, bestBidUnitPrice: null, lastTradeUnitPrice: null };
  return UNSOLD.has(item.itemId) ? { ...item, bestAskUnitPrice: null } : item;
});

const ORDER_BOOK: MarketOrderBook = {
  instrumentId: INSTRUMENTS[0].instrumentId, marketRevision: 12,
  asks: [{ unitPrice: 52, totalQuantity: 210, cumulativeQuantity: 210, myQuantity: 0 }, { unitPrice: 53, totalQuantity: 120, cumulativeQuantity: 330, myQuantity: 0 }, { unitPrice: 56, totalQuantity: 64, cumulativeQuantity: 394, myQuantity: 0 }],
  bids: [{ unitPrice: 50, totalQuantity: 200, cumulativeQuantity: 200, myQuantity: 0 }, { unitPrice: 49, totalQuantity: 180, cumulativeQuantity: 380, myQuantity: 0 }, { unitPrice: 46, totalQuantity: 90, cumulativeQuantity: 470, myQuantity: 0 }],
  bestBidUnitPrice: 50, bestAskUnitPrice: 52, spread: 2,
  recentTrades: [{ tradeId: "recent-1", quantity: 12, unitPrice: 52, totalPrice: 624, filledAt: ago(4) }],
};

function order(overrides: Partial<MarketOrder> & Pick<MarketOrder, "orderId" | "itemId" | "displayName" | "side" | "status">): MarketOrder {
  return {
    instrumentId: `inst-${overrides.itemId}`, timeInForce: "GTC", initialQuantity: 10, filledQuantity: 0, remainingQuantity: 10,
    limitUnitPrice: 250, reservedRice: 0, priorityAt: ago(240), createdAt: ago(240), updatedAt: ago(30), expiresAt: null, ...overrides,
  };
}

const ACTIVE_ORDERS: MarketOrder[] = [
  order({ orderId: "a1", itemId: "gem:3:flat_attack", displayName: "3레벨 공격력 보석", side: "SELL", status: "ACTIVE", initialQuantity: 10, remainingQuantity: 10, limitUnitPrice: 250 }),
  order({ orderId: "a2", itemId: "POTATO_M3", displayName: "감자", side: "BUY", status: "PARTIALLY_FILLED", initialQuantity: 20, filledQuantity: 12, remainingQuantity: 8, limitUnitPrice: 52 }),
  order({ orderId: "a3", itemId: "POTATO_M1", displayName: "감자 조각", side: "BUY", status: "ACTIVE", initialQuantity: 50, remainingQuantity: 50, limitUnitPrice: 30 }),
  order({ orderId: "a4", itemId: "gem:2:flat_hp", displayName: "2레벨 체력 보석", side: "SELL", status: "PARTIALLY_FILLED", initialQuantity: 15, filledQuantity: 10, remainingQuantity: 5, limitUnitPrice: 180 }),
];

const CLOSED_ORDERS: MarketOrder[] = [
  order({ orderId: "c1", itemId: "gem:3:flat_attack", displayName: "붉은 마력석", side: "SELL", status: "FILLED", initialQuantity: 5, filledQuantity: 5, remainingQuantity: 0, limitUnitPrice: 1_200, createdAt: ago(600), updatedAt: ago(560) }),
  order({ orderId: "c2", itemId: "CORN_M2", displayName: "원목", side: "BUY", status: "CANCELLED", initialQuantity: 20, filledQuantity: 0, remainingQuantity: 20, limitUnitPrice: 52, createdAt: ago(1_500), updatedAt: ago(1_440) }),
  order({ orderId: "c3", itemId: "SWEET_POTATO_M4", displayName: "가죽 갑옷", side: "BUY", status: "EXPIRED", initialQuantity: 1, filledQuantity: 0, remainingQuantity: 1, limitUnitPrice: 2_500, createdAt: ago(3_000), updatedAt: ago(2_880) }),
  order({ orderId: "c4", itemId: "POTATO_M1", displayName: "체력 물약", side: "BUY", status: "FILLED", initialQuantity: 10, filledQuantity: 10, remainingQuantity: 0, limitUnitPrice: 300, createdAt: ago(4_400), updatedAt: ago(4_320) }),
];

const TRADES: MarketTrade[] = [
  { tradeId: "t1", instrumentId: "inst-gem:3:flat_attack", itemId: "gem:3:flat_attack", displayName: "붉은 마력석", side: "SELL", quantity: 2, unitPrice: 1_200, totalPrice: 2_400, fee: 0, settlementAmount: 2_400, filledAt: ago(575) },
  { tradeId: "t2", instrumentId: "inst-gem:3:flat_attack", itemId: "gem:3:flat_attack", displayName: "붉은 마력석", side: "SELL", quantity: 3, unitPrice: 1_200, totalPrice: 3_600, fee: 0, settlementAmount: 3_600, filledAt: ago(562) },
  { tradeId: "t3", instrumentId: "inst-POTATO_M1", itemId: "POTATO_M1", displayName: "체력 물약", side: "BUY", quantity: 10, unitPrice: 300, totalPrice: 3_000, fee: 0, settlementAmount: 3_000, filledAt: ago(4_330) },
  { tradeId: "t4", instrumentId: "inst-POTATO_M3", itemId: "POTATO_M3", displayName: "감자", side: "BUY", quantity: 12, unitPrice: 52, totalPrice: 624, fee: 0, settlementAmount: 624, filledAt: ago(35) },
];

const DELIVERIES: MarketDelivery[] = [
  { deliveryId: "d1", instrumentId: "inst-POTATO_M1", orderId: "c4", tradeId: "t3", source: "BUY_FILL", itemId: "POTATO_M1", instanceIds: [], quantity: 20, displayName: "감자 한 조각", createdAt: ago(120), claimedAt: null },
  { deliveryId: "d2", instrumentId: "inst-SWEET_POTATO_M1", orderId: "c3", tradeId: null, source: "BUY_FILL", itemId: "SWEET_POTATO_M1", instanceIds: [], quantity: 15, displayName: "고구마 한 조각", createdAt: ago(200), claimedAt: null },
  { deliveryId: "d3", instrumentId: "inst-gem:3:flat_attack", orderId: "c1", tradeId: null, source: "SELL_CANCEL_RETURN", itemId: "gem:3:flat_attack", instanceIds: [], quantity: 2, displayName: "공격력 보석", createdAt: ago(260), claimedAt: null },
];

const SETTLEMENTS: MarketSettlementMail[] = [
  { mailId: "m1", type: "MARKET_SETTLEMENT", riceAmount: 3_395, claimed: false, createdAt: ago(90), claimedAt: null },
  { mailId: "m2", type: "MARKET_SETTLEMENT", riceAmount: 1_425, claimed: false, createdAt: ago(150), claimedAt: null },
];

const SUMMARY: MarketSummary = {
  activeOrders: ACTIVE_ORDERS.length, closedOrders: CLOSED_ORDERS.length, recoveryReviewOrders: 0,
  claimableDeliveries: DELIVERIES.length, claimableSettlements: SETTLEMENTS.length,
  unread: [{ stream: "FILLS", latestSequence: 8, readSequence: 8, unreadCount: 0 }],
};

function page<T>(items: T[]): MarketPage<T> {
  return { items, nextCursor: null, totalItems: items.length, readThroughSequence: items.length };
}

export const marketPlazaFixtures = { instruments: INSTRUMENTS, orderBook: ORDER_BOOK, activeOrders: ACTIVE_ORDERS, closedOrders: CLOSED_ORDERS, trades: TRADES, deliveries: DELIVERIES, settlements: SETTLEMENTS, summary: SUMMARY };

export function marketPlazaFixtureResponse(url: string, method: string): unknown {
  const path = url.startsWith("http") ? new URL(url).pathname + new URL(url).search : url;
  if (method !== "GET") return { commandId: "preview", idempotencyKey: "preview", status: "APPLIED", result: {} };
  if (path.startsWith("/api/v1/auth/session")) return { authenticated: true, account: { accountId: "preview", characterId: "preview", email: "preview@hanjjak.dev", nickname: "미리보기", level: 12, experience: 0, rice: 15_820 } };
  if (path.startsWith("/api/v1/market/instruments") && path.includes("/candles")) return { instrumentId: INSTRUMENTS[0].instrumentId, interval: "15m", from: ago(720), to: ago(0), marketRevision: 12, items: candles() };
  if (path.startsWith("/api/v1/market/instruments") && path.includes("/order-quote")) return quote(path);
  if (path.startsWith("/api/v1/market/instruments") && path.includes("/price-levels")) return { instrumentId: INSTRUMENTS[0].instrumentId, side: "SELL", marketRevision: 12, items: [], nextUnitPrice: null, totalLevels: 0 };
  if (path === "/api/v1/market/instruments") return INSTRUMENTS;
  if (path.startsWith("/api/v1/market/order-books/")) return ORDER_BOOK;
  if (path.startsWith("/api/v1/market/orders/me")) return page(path.includes("CLOSED") ? CLOSED_ORDERS : ACTIVE_ORDERS);
  if (path.startsWith("/api/v1/market/deliveries/me")) return page(DELIVERIES);
  if (path.startsWith("/api/v1/market/trades")) return page(TRADES);
  if (path.startsWith("/api/v1/market/summary/me")) return SUMMARY;
  if (path.startsWith("/api/v1/mails")) return page(SETTLEMENTS);
  if (path.startsWith("/api/v1/inventory/items/")) return { itemId: "POTATO_M1", displayName: "감자 조각", quantity: 240, availableQuantity: 240 };
  if (path.startsWith("/api/v1/gems")) return { unlocked: true, gems: [] };
  return undefined;
}

function candles() {
  const base = [60, 49, 40, 46, 52, 61, 53, 58];
  return base.map((close, index) => ({
    openedAt: new Date(NOW - (base.length - index) * 90 * 60_000).toISOString(),
    open: index === 0 ? close : base[index - 1], high: close + 4, low: close - 4, close, quantity: 40 + index * 6, totalPrice: close * (40 + index * 6),
  }));
}

function quote(path: string): MarketOrderQuote {
  const params = new URLSearchParams(path.slice(path.indexOf("?")));
  const quantity = Number(params.get("quantity") ?? 1);
  const limitUnitPrice = Number(params.get("limitUnitPrice") ?? 52);
  const side = (params.get("side") ?? "BUY") as MarketOrderQuote["side"];
  const filled = params.get("timeInForce") === "IOC" ? Math.min(quantity, 210) : 0;
  return {
    instrumentId: INSTRUMENTS[0].instrumentId, side, timeInForce: (params.get("timeInForce") ?? "IOC") as MarketOrderQuote["timeInForce"],
    requestedQuantity: quantity, limitUnitPrice, expectedFilledQuantity: filled, expectedRemainingQuantity: quantity - filled,
    lowestFilledUnitPrice: filled ? limitUnitPrice : null, highestFilledUnitPrice: filled ? limitUnitPrice : null,
    weightedAverageUnitPrice: filled ? limitUnitPrice : null, totalPrice: quantity * limitUnitPrice,
    expectedFee: 0, expectedSettlementAmount: quantity * limitUnitPrice, availableRice: 15_820, availableQuantity: 240, marketRevision: 12,
  };
}
