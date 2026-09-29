import { describe, expect, it } from "vitest";
import { fillSummaryText, reconstructOrderFills } from "./marketFills";
import type { MarketOrder, MarketTrade } from "./api";

function order(overrides: Partial<MarketOrder> = {}): MarketOrder {
  return {
    orderId: "order-1", instrumentId: "gem-red", itemId: "gem:red", displayName: "붉은 마력석",
    side: "SELL", timeInForce: "GTC", initialQuantity: 5, filledQuantity: 5, remainingQuantity: 0,
    limitUnitPrice: 1_200, reservedRice: 0, status: "FILLED", priorityAt: "2024-10-25T05:30:00Z",
    createdAt: "2024-10-25T05:30:00Z", updatedAt: "2024-10-25T05:35:00Z", expiresAt: null, ...overrides,
  };
}
function trade(overrides: Partial<MarketTrade> = {}): MarketTrade {
  return {
    tradeId: "trade-1", instrumentId: "gem-red", itemId: "gem:red", displayName: "붉은 마력석",
    side: "SELL", quantity: 2, unitPrice: 1_200, totalPrice: 2_400, fee: 0, settlementAmount: 2_400,
    filledAt: "2024-10-25T05:32:00Z", ...overrides,
  };
}

describe("reconstructOrderFills", () => {
  it("splits a fully filled order into the trades that made it up", () => {
    const breakdown = reconstructOrderFills(order(), [
      trade({ tradeId: "t2", quantity: 3, totalPrice: 3_600, settlementAmount: 3_600, filledAt: "2024-10-25T05:35:00Z" }),
      trade({ tradeId: "t1" }),
    ]);
    expect(breakdown.exact).toBe(true);
    expect(breakdown.fills.map(fill => [fill.tradeId, fill.quantity])).toEqual([["t1", 2], ["t2", 3]]);
    expect(fillSummaryText(breakdown, "SELL")).toBe("두 번에 나누어 판매됐어요.");
  });

  it("ignores trades from another instrument, the other side, or outside the order window", () => {
    const breakdown = reconstructOrderFills(order({ filledQuantity: 2, initialQuantity: 2 }), [
      trade({ tradeId: "other-item", instrumentId: "gem-blue" }),
      trade({ tradeId: "other-side", side: "BUY" }),
      trade({ tradeId: "too-late", filledAt: "2024-10-25T06:40:00Z" }),
      trade({ tradeId: "mine" }),
    ]);
    expect(breakdown.fills.map(fill => fill.tradeId)).toEqual(["mine"]);
    expect(breakdown.exact).toBe(true);
  });

  it("reports an inexact breakdown rather than showing a partial list as complete", () => {
    const breakdown = reconstructOrderFills(order(), [trade()]);
    expect(breakdown.exact).toBe(false);
    expect(fillSummaryText(breakdown, "SELL")).toBe("찾은 기록 1건만 보여줘요. 나머지는 기록이 남아 있지 않아요.");
  expect(fillSummaryText({ fills: [], exact: false }, "SELL")).toBe("이 거래의 기록은 남아 있는 범위를 넘어갔어요.");
  });

  it("treats a cancelled order with no fills as an empty breakdown", () => {
    const breakdown = reconstructOrderFills(order({ status: "CANCELLED", filledQuantity: 0, remainingQuantity: 5 }), [trade()]);
    expect(breakdown).toEqual({ fills: [], exact: true });
    expect(fillSummaryText(breakdown, "BUY")).toBe("거래된 수량이 없어요.");
  });
});
