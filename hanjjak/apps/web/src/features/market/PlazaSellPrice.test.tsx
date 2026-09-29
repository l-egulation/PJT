// @vitest-environment happy-dom
import { cleanup, render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { afterEach, expect, it, vi } from "vitest";
import { MarketTradeBoard, type MarketTradeBoardProps } from "./MarketTradeBoard";
import type { MarketInstrument, MarketOrderSide, MarketTimeInForce } from "./api";

afterEach(cleanup);

const potato: MarketInstrument = {
  instrumentId: "potato-1", canonicalKey: "material:potato_m1", itemId: "POTATO_M1", displayName: "감자 한 조각",
  category: "MATERIAL", attributes: {}, status: "ACTIVE",
  bestBidUnitPrice: 50, bestAskUnitPrice: 52, lastTradeUnitPrice: 51, marketRevision: 1,
};

function renderBoard(side: MarketOrderSide, timeInForce: MarketTimeInForce) {
  const props: MarketTradeBoardProps = {
    catalog: [potato], loading: false, error: null, selected: potato, onSelect: vi.fn(),
    book: undefined, bookLoading: false, availableQuantity: 240, availableRice: 9_000,
    side, setSide: vi.fn(), timeInForce, setTimeInForce: vi.fn(),
    quantity: 1, setQuantity: vi.fn(), unitPrice: 50, setUnitPrice: vi.fn(),
    quote: undefined, quotePending: false, quoteError: null, confirmation: null,
    busy: false, locked: false, onCancelConfirmation: vi.fn(), onSubmit: vi.fn(),
    boardView: "list", setBoardView: vi.fn(), mySellInstrumentIds: new Set<string>(),
  };
  /* 판매목록이 매물을 펼치려 주문장을 물어본다. 물어볼 곳은 있어야 화면이 선다. */
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, refetchOnWindowFocus: false } } });
  render(<QueryClientProvider client={client}><MarketTradeBoard {...props} /></QueryClientProvider>);
}

it("lets the seller name their own price in the simple list", () => {
  // 값을 스스로 매겨 내놓는 것이 파는 쪽이 본래 하는 일이다. 읽기 전용 시세만 보이면
  // 시세 보기로 건너가야만 팔 값을 정할 수 있었다.
  renderBoard("SELL", "GTC");
  /* 칸 이름이 "판매 가격" 그대로라 감싼 label 이 −·+ 단추까지 가리킨다. 적는 칸만 집는다. */
  const price = screen.getByLabelText("판매 가격", { selector: "input" }) as HTMLInputElement;
  expect(price.value).toBe("50");
  expect(price.disabled).toBe(false);
  // 값 칸 말고는 아무것도 두지 않는다. 기준이 될 시세는 첫 값으로 이미 채워져 있고,
  // 무슨 일이 일어나는지 설명하는 줄은 시세 보기의 몫이다.
  expect(screen.queryByText(/지금 사겠다는 값/)).toBeNull();
  expect(document.querySelector(".market-plaza-mode-hint")).toBeNull();
});

it("keeps buying in the simple list to one tap at the going price", () => {
  renderBoard("BUY", "IOC");
  expect(screen.queryByLabelText("판매 가격")).toBeNull();
  expect(screen.queryByLabelText("예약 가격")).toBeNull();
  expect(screen.getByText("지금 사는 가격")).toBeTruthy();
});
