import { describe, expect, it } from "vitest";
import type { Time } from "lightweight-charts";
import { filterMarketItems, MARKET_FALL_COLOR, MARKET_FLAT_COLOR, MARKET_RISE_COLOR, marketDepthChart, marketDirectionColor, maximumAffordableQuantity, nextPriceTrendPeriod, priceTrendCrosshairTimeText } from "./MarketScreen";
import type { MarketInstrument, MarketOrderBook } from "./api";

const instrument: MarketInstrument = { instrumentId: "00000000-0000-5000-8000-000000000001", canonicalKey: "material:potato_m1", itemId: "POTATO_M1", displayName: "감자 한 조각", category: "MATERIAL", attributes: {}, status: "ACTIVE", bestBidUnitPrice: 90, bestAskUnitPrice: 100, lastTradeUnitPrice: 95, marketRevision: 2 };

describe("order-book market", () => {
  it("browses one category but searches across categories with every word", () => {
    const gem = { ...instrument, instrumentId: "gem", itemId: "gem:3:flat_attack", category: "GEM", canonicalKey: "gem:3:flat_attack:15", displayName: "3레벨 고정 공격력 보석", attributes: { level: "3", option: "flat_attack", value: "15" } };
    const catalog = [instrument, gem];
    expect(filterMarketItems(catalog, { category: "MATERIAL", query: "  " })).toEqual([instrument]);
    expect(filterMarketItems(catalog, { category: "MATERIAL", query: " 공격력   3레벨 " })).toEqual([gem]);
    expect(filterMarketItems(catalog, { category: "GEM", query: "감자 F등급" })).toEqual([instrument]);
    expect(filterMarketItems(catalog, { category: "MATERIAL", query: "공격력 4레벨" })).toEqual([]);
  });

  it("excludes own depth from affordable IOC quantity", () => {
    expect(maximumAffordableQuantity([{ unitPrice: 100, totalQuantity: 7, cumulativeQuantity: 7, myQuantity: 2 }], 100, 450)).toBe(4);
  });

  it("uses domestic market colors for rise fall and flat candles", () => {
    expect(marketDirectionColor(100, 110)).toBe(MARKET_RISE_COLOR);
    expect(marketDirectionColor(110, 100)).toBe(MARKET_FALL_COLOR);
    expect(marketDirectionColor(100, 100)).toBe(MARKET_FLAT_COLOR);
    expect([MARKET_RISE_COLOR, MARKET_FALL_COLOR, MARKET_FLAT_COLOR]).toEqual(["#D84A3A", "#3976C5", "#806956"]);
  });

  it("plots bids and asks together by price and cumulative quantity", () => {
    const book: MarketOrderBook = {
      instrumentId: "instrument", marketRevision: 1, bestBidUnitPrice: 90, bestAskUnitPrice: 100, spread: 10, recentTrades: [],
      bids: [
        { unitPrice: 90, totalQuantity: 40, cumulativeQuantity: 40, myQuantity: 0 },
        { unitPrice: 80, totalQuantity: 60, cumulativeQuantity: 100, myQuantity: 0 },
      ],
      asks: [
        { unitPrice: 100, totalQuantity: 150, cumulativeQuantity: 150, myQuantity: 0 },
        { unitPrice: 110, totalQuantity: 250, cumulativeQuantity: 400, myQuantity: 0 },
      ],
    };

    const chart = marketDepthChart(book);

    const points = [...chart.bids, ...chart.asks];
    expect(points.every(point => point.x > 0 && point.x < 100 && point.y >= 0 && point.y < 100)).toBe(true);
    expect(points.map(point => point.x)).toEqual(points.map(point => point.x).sort((a, b) => a - b));
    expect(chart.bids[0].y).toBeLessThan(chart.bids[1].y);
    expect(chart.asks[0].y).toBeGreaterThan(chart.asks[1].y);
    const bidHeight = 100 - chart.bids[1].y;
    const askHeight = 100 - chart.asks[1].y;
    expect(askHeight / bidHeight).toBeCloseTo(400 / 40);
    expect(chart.spread!.left).toBe(chart.bestBid!.x);
    expect(chart.spread!.left + chart.spread!.width).toBeCloseTo(chart.bestAsk!.x);
    expect(chart.quantityTicks.every(tick => Number.isInteger(tick.value))).toBe(true);
    expect(chart.quantityCeiling).toBeGreaterThanOrEqual(400);
  });

  it("keeps single-price and empty books finite without duplicate axis labels", () => {
    const empty: MarketOrderBook = { instrumentId: "instrument", marketRevision: 1, bestBidUnitPrice: null, bestAskUnitPrice: null, spread: null, recentTrades: [], bids: [], asks: [] };
    const single = marketDepthChart({ ...empty, asks: [{ unitPrice: 999999, totalQuantity: 1, cumulativeQuantity: 1, myQuantity: 1 }] });
    expect(single.asks[0].x).toBeCloseTo(50);
    expect(single.quantityTicks.every(tick => Number.isInteger(tick.value) && Number.isFinite(tick.y))).toBe(true);
    expect(new Set(single.priceTicks.map(tick => tick.value)).size).toBe(single.priceTicks.length);
    expect(single.spread).toBeNull();
    const chart = marketDepthChart(empty);
    expect(chart.levels).toEqual([]);
    expect(chart.spread).toBeNull();
    expect(chart.quantityTicks.every(tick => Number.isFinite(tick.y))).toBe(true);
  });

  it("centers an asymmetric order book on the best-quote midpoint", () => {
    const chart = marketDepthChart({ instrumentId: "instrument", marketRevision: 1, bestBidUnitPrice: 85, bestAskUnitPrice: 95, spread: 10, recentTrades: [],
      bids: [{ unitPrice: 85, totalQuantity: 100, cumulativeQuantity: 100, myQuantity: 0 }, { unitPrice: 75, totalQuantity: 60, cumulativeQuantity: 160, myQuantity: 0 }],
      asks: [{ unitPrice: 95, totalQuantity: 180, cumulativeQuantity: 180, myQuantity: 0 }, { unitPrice: 140, totalQuantity: 990, cumulativeQuantity: 1170, myQuantity: 0 }],
    });
    expect(chart.midpoint).toBe(90);
    expect(chart.midpointX).toBeCloseTo(50);
    expect(chart.midpoint! - chart.priceFrom).toBeCloseTo(chart.priceTo - chart.midpoint!);
    expect(chart.bestBid!.x + chart.bestAsk!.x).toBeCloseTo(100);
    expect(chart.priceFrom).toBeLessThan(75);
    expect(chart.priceTo).toBeGreaterThan(140);
  });

  it("bounds full depth at the last returned level without extrapolating unknown orders", () => {
    const bids = Array.from({ length: 20 }, (_, i) => ({ unitPrice: 99 - i, totalQuantity: 1, cumulativeQuantity: i + 1, myQuantity: 0 }));
    const asks = Array.from({ length: 20 }, (_, i) => ({ unitPrice: 101 + i, totalQuantity: 2, cumulativeQuantity: (i + 1) * 2, myQuantity: 0 }));
    const book: MarketOrderBook = { instrumentId: "instrument", marketRevision: 1, bestBidUnitPrice: 99, bestAskUnitPrice: 101, spread: 2, recentTrades: [], bids, asks };
    const full = marketDepthChart(book);
    expect(full.bidFrom).toBe(full.bids[0].x);
    expect(full.askTo).toBe(full.asks.at(-1)!.x);
    expect(full.bidFrom).toBeGreaterThan(0);
    expect(full.askTo).toBeLessThan(100);
    const partial = marketDepthChart({ ...book, bids: bids.slice(0, 2), asks: asks.slice(0, 2) });
    expect(partial.bidFrom).toBe(0);
    expect(partial.askTo).toBe(100);
  });

  it("keeps all prices visible without negative labels when symmetry reaches zero", () => {
    const chart = marketDepthChart({ instrumentId: "instrument", marketRevision: 1, bestBidUnitPrice: 10, bestAskUnitPrice: 11, spread: 1, recentTrades: [],
      bids: [{ unitPrice: 10, totalQuantity: 1, cumulativeQuantity: 1, myQuantity: 0 }],
      asks: [{ unitPrice: 11, totalQuantity: 1, cumulativeQuantity: 1, myQuantity: 0 }, { unitPrice: 999999, totalQuantity: 1, cumulativeQuantity: 2, myQuantity: 0 }],
    });
    expect(chart.centered).toBe(false);
    expect(chart.priceFrom).toBe(0);
    expect(chart.priceTo).toBeGreaterThan(999999);
    expect(chart.priceTicks.every(tick => tick.value >= 0)).toBe(true);
    expect(chart.levels.every(point => point.x >= 0 && point.x <= 100)).toBe(true);
  });

  it("formats a UTC crosshair timestamp in Korea time", () => {
    const timestamp = Date.parse("2026-09-12T06:05:00Z");

    expect(priceTrendCrosshairTimeText(Math.floor(timestamp / 1_000) as Time)).toBe("2026-09-12 15:05");
  });

  it("widens an empty price trend through every supported period", () => {
    expect(nextPriceTrendPeriod("12h")).toBe("1d");
    expect(nextPriceTrendPeriod("1d")).toBe("2d");
    expect(nextPriceTrendPeriod("2d")).toBeNull();
  });

});
