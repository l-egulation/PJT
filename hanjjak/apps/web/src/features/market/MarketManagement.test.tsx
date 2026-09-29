import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { DeliveryPanel, OrderManagement, SettlementPanel, TradeList } from "./MarketManagement";
import type { MarketDelivery, MarketOrder, MarketSettlementMail, MarketTrade } from "./api";

const createdAt = "2026-09-12T09:40:00Z";
const nextDay = "2026-09-12T17:40:00Z";

const order: MarketOrder = {
  orderId: "order-1", instrumentId: "instrument-1", itemId: "POTATO_M1", displayName: "감자 한 조각",
  side: "SELL", timeInForce: "GTC", initialQuantity: 10, filledQuantity: 0, remainingQuantity: 10,
  limitUnitPrice: 100, reservedRice: 0, status: "ACTIVE", priorityAt: createdAt, createdAt, updatedAt: createdAt, expiresAt: nextDay,
};
const delivery: MarketDelivery = {
  deliveryId: "delivery-1", instrumentId: "instrument-1", orderId: "order-1", tradeId: null,
  source: "SELL_CANCEL_RETURN", itemId: "POTATO_M1", instanceIds: [], quantity: 10, displayName: "감자 한 조각", createdAt, claimedAt: nextDay,
};
const settlement: MarketSettlementMail = { mailId: "mail-1", type: "MARKET_SETTLEMENT", riceAmount: 900, claimed: true, createdAt, claimedAt: nextDay };
const trade: MarketTrade = {
  tradeId: "trade-1", instrumentId: "instrument-1", itemId: "POTATO_M1", displayName: "감자 한 조각",
  side: "SELL", quantity: 10, unitPrice: 100, totalPrice: 1_000, fee: 100, settlementAmount: 900, filledAt: createdAt,
};

describe("market timestamp rendering", () => {
  it("uses the same full date and minute format across market record panels", () => {
    const markup = [
      renderToStaticMarkup(<OrderManagement active={[order]} closed={[]} busy={false} onUpdate={() => undefined} onCancel={() => undefined} />),
      renderToStaticMarkup(<DeliveryPanel items={[delivery]} busy={false} onClaim={() => undefined} onClaimAll={() => undefined} />),
      renderToStaticMarkup(<SettlementPanel items={[settlement]} busy={false} onClaim={() => undefined} onClaimAll={() => undefined} />),
      renderToStaticMarkup(<TradeList trades={[trade]} selectedName="감자 한 조각" />),
    ].join("\n");

    expect(markup).toContain("등록 2026-09-12 18:40");
    expect(markup).toContain("만료 2026-09-13 02:40");
    expect(markup).toContain("도착 2026-09-12 18:40");
    expect(markup).toContain("수령 2026-09-13 02:40");
    expect(markup).toContain("발생 2026-09-12 18:40");
    expect(markup).toContain(">2026-09-12 18:40</time>");
    expect(markup).not.toMatch(/09\.\s*12\.\s*18:40/);
  });
});
