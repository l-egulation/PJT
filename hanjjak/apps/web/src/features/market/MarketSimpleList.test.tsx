// @vitest-environment happy-dom
import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { afterEach, expect, it, vi } from "vitest";
import { MarketSimpleList } from "./MarketSimpleList";
import type { FilterState } from "./MarketScreen";
import { toggleFocusInstrument, type MarketFocus } from "./MarketTradeBoard";
import type { MarketInstrument } from "./api";

afterEach(cleanup);

function instrument(itemId: string, displayName: string, category: string, ask: number | null, bid: number | null, attributes: Record<string, string> = {}): MarketInstrument {
  return {
    instrumentId: `inst-${itemId}`, canonicalKey: itemId.toLowerCase(), itemId, displayName, category, attributes,
    status: "ACTIVE", bestBidUnitPrice: bid, bestAskUnitPrice: ask, lastTradeUnitPrice: ask, marketRevision: 1,
  };
}

const catalog = [
  instrument("POTATO_M1", "감자 조각", "MATERIAL", 52, 50),
  instrument("CORN_M2", "작은 옥수수", "MATERIAL", 12, 10),
  instrument("SWEET_POTATO_M1", "고구마 조각", "MATERIAL", null, null),
  instrument("gem:3:flat_attack", "3레벨 공격력 보석", "GEM", 1_200, 1_150, { level: "3", option: "flat_attack", value: "12" }),
];

function rows() {
  return within(document.querySelector(".market-simple-rows")!).getAllByRole("button");
}
function names() {
  return rows().map(row => row.querySelector("strong")?.textContent);
}

/** 주문장이 없는 화면은 품목이 알려 준 가장 싼 값 한 줄로 버틴다. 그 자리를 채우려면 넣어 준다. */
function ask(unitPrice: number, totalQuantity: number, myQuantity = 0) {
  return { unitPrice, totalQuantity, cumulativeQuantity: totalQuantity, myQuantity };
}

function renderList(
  filters: FilterState = { category: "MATERIAL", query: "" },
  focus: MarketFocus = null,
  mine: ReadonlySet<string> = new Set(),
  books: Record<string, ReturnType<typeof ask>[]> = {},
  onSelect = vi.fn(),
) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, refetchOnWindowFocus: false } } });
  for (const [instrumentId, asks] of Object.entries(books)) {
    client.setQueryData(["market-order-book", instrumentId], { instrumentId, marketRevision: 1, bids: [], asks, bestBidUnitPrice: null, bestAskUnitPrice: asks[0]?.unitPrice ?? null, spread: null, recentTrades: [] });
  }
  render(<QueryClientProvider client={client}>
    <MarketSimpleList catalog={catalog} filters={filters} focus={focus} mine={mine} selected={catalog[0]} onSelect={onSelect} toggle={<button type="button">시세 보기</button>} />
  </QueryClientProvider>);
  return onSelect;
}

it("adds and removes grades one click at a time, and lets go when the last one leaves", () => {
  const d = toggleFocusInstrument(null, "inst-POTATO_M2");
  expect(d).toEqual({ kind: "instruments", ids: ["inst-POTATO_M2"] });

  const dc = toggleFocusInstrument(d, "inst-POTATO_M3");
  expect(dc).toEqual({ kind: "instruments", ids: ["inst-POTATO_M2", "inst-POTATO_M3"] });

  expect(toggleFocusInstrument(dc, "inst-POTATO_M2")).toEqual({ kind: "instruments", ids: ["inst-POTATO_M3"] });
  expect(toggleFocusInstrument(d, "inst-POTATO_M2")).toBeNull();
});

it("shows every category at once when the picker is on 전체", () => {
  renderList({ category: "ALL", query: "" });

  expect(names()).toEqual(["감자 조각", "공격력", "작은 옥수수"]);
});

it("marks the rows whose listing is my own — the market will not sell them to me", () => {
  renderList({ category: "MATERIAL", query: "" }, null, new Set(["inst-CORN_M2"]));

  const rows = within(document.querySelector(".market-simple-rows")!).getAllByRole("button");
  const corn = rows.find(row => row.textContent?.includes("작은 옥수수"))!;
  const potato = rows.find(row => row.textContent?.includes("감자 조각"))!;
  expect(corn.querySelector(".market-simple-mine")?.textContent).toBe("내 매물");
  expect(potato.querySelector(".market-simple-mine")).toBeNull();
});

it("gives every listing its own row — the same item at three prices is three rows", () => {
  renderList({ category: "MATERIAL", query: "" }, { kind: "instruments", ids: ["inst-POTATO_M1"] }, new Set(), {
    "inst-POTATO_M1": [ask(31, 7), ask(34, 40), ask(38, 120, 120)],
  });

  const listed = rows().map(row => [row.querySelector("strong")?.textContent, row.querySelector(".market-simple-ask")?.textContent, row.querySelector(".market-simple-stock")?.textContent]);
  expect(listed).toEqual([
    ["감자 조각", "31", "7개"],
    ["감자 조각", "34", "40개"],
    ["감자 조각", "38", "120개"],
  ]);
  /* 먼저 나가는 값은 맨 위 한 줄뿐이고, 내가 올린 매물은 그 줄에만 적힌다. */
  expect(rows().filter(row => row.classList.contains("is-cheapest"))).toHaveLength(1);
  expect(rows()[0].classList.contains("is-cheapest")).toBe(true);
  expect(rows()[2].querySelector(".market-simple-mine")?.textContent).toBe("내 매물");
  expect(rows()[0].querySelector(".market-simple-mine")).toBeNull();
});

it("shows one price per row — what the item is selling for", () => {
  renderList();

  const row = within(document.querySelector(".market-simple-rows")!).getByRole("button", { pressed: true });
  expect(row.querySelector("strong")?.textContent).toBe("감자 조각");
  expect(row.querySelector(".market-simple-ask")?.textContent).toBe("52");
  expect(row.querySelector(".market-simple-ask img")).toBeTruthy();
  expect(row.querySelector(".market-simple-bid")).toBeNull();
});

it("picks the row the player taps so the trade panel follows along", () => {
  const onSelect = renderList();

  fireEvent.click(within(document.querySelector(".market-simple-rows")!).getByRole("button", { name: /작은 옥수수/ }));

  expect(onSelect).toHaveBeenCalledWith("inst-CORN_M2");
});

it("leaves out what nobody is selling — this is a list of what is actually for sale", () => {
  /* 고구마 조각은 파는 사람이 없다. 살 수 없는 줄을 보여 주면 골라 놓고 막힌다. */
  renderList();

  expect(names()).toEqual(["감자 조각", "작은 옥수수"]);
});

it("says why the list is empty instead of quietly widening it", () => {
  renderList({ category: "MATERIAL", query: "" }, { kind: "instruments", ids: ["inst-SWEET_POTATO_M1"] });

  expect(document.querySelector(".market-simple-rows")).toBeNull();
  expect(screen.getByText("지금 이걸 파는 사람이 없어요.")).toBeTruthy();
});

it("orders by name first, and by price when asked", () => {
  renderList();
  expect(names()).toEqual(["감자 조각", "작은 옥수수"]);

  fireEvent.click(screen.getByRole("button", { name: "싼 값순" }));

  expect(names()).toEqual(["작은 옥수수", "감자 조각"]);
});

it("narrows to the one row the picker drilled into", () => {
  /* 왼쪽에서 계열을 펼치면 그 계열만, 등급 하나를 고르면 그 하나만 남는다. */
  renderList({ category: "MATERIAL", query: "" }, { kind: "group", key: "MATERIAL:POTATO" });
  expect(names()).toEqual(["감자 조각"]);

  cleanup();
  renderList({ category: "MATERIAL", query: "" }, { kind: "instruments", ids: ["inst-CORN_M2"] });
  expect(names()).toEqual(["작은 옥수수"]);

  /* 등급을 여럿 골라 두면 고른 만큼 나란히 본다. */
  cleanup();
  renderList({ category: "MATERIAL", query: "" }, { kind: "instruments", ids: ["inst-CORN_M2", "inst-POTATO_M1"] });
  expect(names()).toEqual(["감자 조각", "작은 옥수수"]);
});

it("follows the category and search the picker on the left is holding", () => {
  renderList({ category: "GEM", query: "" });
  /* 레벨과 옵션 값은 오른쪽 이름표가 말해 주므로 이름에서는 뗀다. */
  expect(names()).toEqual(["공격력"]);
  expect(rows()[0].querySelector(".market-simple-tag")?.textContent).toBe("Lv.3 · +12");

  cleanup();
  renderList({ category: "MATERIAL", query: "옥수수" });
  expect(names()).toEqual(["작은 옥수수"]);
});
