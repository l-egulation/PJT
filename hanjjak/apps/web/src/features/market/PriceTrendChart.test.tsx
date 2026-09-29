// @vitest-environment happy-dom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { createChart } from "lightweight-charts";
import { MarketScreen } from "./MarketScreen";
import type { MarketCandles, MarketInstrument, MarketOrderBook } from "./api";

vi.mock("lightweight-charts", () => ({
  CandlestickSeries: {},
  HistogramSeries: {},
  LineSeries: {},
  ColorType: { Solid: "solid" },
  CrosshairMode: { Normal: 0 },
  createChart: vi.fn((host: HTMLElement) => {
    const chartElement = document.createElement("div");
    chartElement.className = "tv-lightweight-charts";
    host.append(chartElement);
    return {
      addSeries: () => ({ setData: vi.fn() }),
      priceScale: () => ({ applyOptions: vi.fn() }),
      timeScale: () => ({ fitContent: vi.fn() }),
      subscribeCrosshairMove: vi.fn(),
      unsubscribeCrosshairMove: vi.fn(),
      remove: () => chartElement.remove(),
    };
  }),
}));

function candles(instrumentId: string, close: number): MarketCandles {
  return {
    instrumentId,
    interval: "15m:12h",
    from: "2026-09-12T00:00:00Z",
    to: "2026-09-12T12:00:00Z",
    marketRevision: 1,
    items: [{ openedAt: "2026-09-12T11:45:00Z", open: close, high: close, low: close, close, quantity: 1, totalPrice: close }],
  };
}

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
  vi.mocked(createChart).mockClear();
});

it("reuses one chart while selecting detailed items that each have trade history", async () => {
  const instruments: MarketInstrument[] = [
    { instrumentId: "potato-1", canonicalKey: "material:potato_m1", itemId: "POTATO_M1", displayName: "감자 한 조각", category: "MATERIAL", attributes: {}, status: "ACTIVE", bestBidUnitPrice: 90, bestAskUnitPrice: 95, lastTradeUnitPrice: 90, marketRevision: 1 },
    { instrumentId: "potato-2", canonicalKey: "material:potato_m2", itemId: "POTATO_M2", displayName: "미니 감자", category: "MATERIAL", attributes: {}, status: "ACTIVE", bestBidUnitPrice: 175, bestAskUnitPrice: 180, lastTradeUnitPrice: 175, marketRevision: 1 },
    { instrumentId: "potato-3", canonicalKey: "material:potato_m3", itemId: "POTATO_M3", displayName: "감자", category: "MATERIAL", attributes: {}, status: "ACTIVE", bestBidUnitPrice: 270, bestAskUnitPrice: 280, lastTradeUnitPrice: 275, marketRevision: 1 },
  ];
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, staleTime: Number.POSITIVE_INFINITY } } });
  client.setQueryData(["auth", "session"], { authenticated: true, account: { accountId: "account", rice: 10_000 } });
  client.setQueryData(["market-instruments"], instruments);
  client.setQueryData(["market-orders"], { items: [], nextCursor: null, totalItems: 0, readThroughSequence: 0 });
  client.setQueryData(["market-deliveries"], { items: [], nextCursor: null, totalItems: 0, readThroughSequence: 0 });
  client.setQueryData(["market-mails"], { items: [], nextCursor: null, totalItems: 0, readThroughSequence: 0 });
  client.setQueryData(["market-summary"], { activeOrders: 0, closedOrders: 0, recoveryReviewOrders: 0, claimableDeliveries: 0, claimableSettlements: 0, unread: [] });
  instruments.forEach((instrument, index) => {
    const price = 100 + index * 100;
    const history = candles(instrument.instrumentId, price);
    const book: MarketOrderBook = { instrumentId: instrument.instrumentId, marketRevision: 1, bids: [], asks: [], bestBidUnitPrice: instrument.bestBidUnitPrice, bestAskUnitPrice: instrument.bestAskUnitPrice, spread: 5, recentTrades: [{ tradeId: `trade-${index}`, quantity: 1, unitPrice: price, totalPrice: price, filledAt: history.items[0].openedAt }] };
    client.setQueryData(["market-candles", instrument.instrumentId, "15m", "12h"], history);
    client.setQueryData(["market-order-book", instrument.instrumentId], book);
    client.setQueryData(["inventory", "market", instrument.itemId], { availableQuantity: 100 });
  });

  render(<QueryClientProvider client={client}><MarketScreen /></QueryClientProvider>);
  fireEvent.click(screen.getByRole("checkbox", { name: "호가 기반 거래" }));
  await waitFor(() => expect(document.querySelectorAll(".tv-lightweight-charts")).toHaveLength(1));

  fireEvent.click(screen.getByRole("button", { name: "미니 감자 · D등급" }));
  await screen.findByText("200쌀", { selector: ".market-price-trend-summary strong" });
  fireEvent.click(screen.getByRole("button", { name: "감자 · C등급" }));
  await screen.findByText("300쌀", { selector: ".market-price-trend-summary strong" });

  expect(document.querySelectorAll(".tv-lightweight-charts")).toHaveLength(1);
  client.clear();
});
