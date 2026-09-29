// @vitest-environment happy-dom

import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { MARKET_UI_STORAGE_KEY, marketCategoryForItemId, selectMarketItemForSale } from "../market/marketUiState";
import { InventoryScreen, type InventoryDataSource } from "./InventoryScreen";
import type { InventoryItem, InventoryPage } from "./api";

const material: InventoryItem = {
  itemId: "POTATO_M1",
  instanceId: null,
  slotId: "stack:POTATO_M1:0",
  members: [],
  acquiredSequence: 1,
  name: "감자 한 조각",
  icon: "/potato.png",
  category: "MATERIAL",
  description: "장비 제작과 강화에 사용하는 1세대 재료",
  acquisitionSources: ["자동 파밍"],
  usages: ["장비 제작", "장비 강화"],
  tradeable: true,
  totalQuantity: 530,
  reservedQuantity: 0,
  availableQuantity: 530,
};

const page: InventoryPage = {
  items: [material],
  usedSlots: 1,
  maxSlots: 200,
  isFull: false,
  nextCursor: null,
};

const dataSource: InventoryDataSource = {
  list: async () => page,
  detail: async () => material,
};

afterEach(cleanup);

describe("inventory marketplace shortcut", () => {
  it("passes the selected tradeable item to the sell shortcut", async () => {
    const onSell = vi.fn();
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });

    render(
      <QueryClientProvider client={client}>
        <InventoryScreen dataSource={dataSource} onSell={onSell} />
      </QueryClientProvider>,
    );

    fireEvent.click(await screen.findByRole("button", { name: "감자 한 조각 거래소에서 바로 판매" }, { timeout: 5_000 }));
    expect(onSell).toHaveBeenCalledOnce();
    expect(onSell).toHaveBeenCalledWith("POTATO_M1");
  });

  it("updates the marketplace selection without clearing the user's sale inputs", () => {
    let stored = JSON.stringify({
      selectedItemId: "CORN_M1",
      sellQuantity: 17,
      sellUnitPrice: 230,
      purchaseQuantity: 4,
    });
    const storage = {
      getItem: () => stored,
      setItem: vi.fn((_key: string, value: string) => { stored = value; }),
    };

    selectMarketItemForSale(storage, "POTATO_M1");

    expect(storage.setItem).toHaveBeenCalledWith(MARKET_UI_STORAGE_KEY, expect.any(String));
    expect(JSON.parse(stored)).toEqual({
      selectedItemId: "POTATO_M1",
      sellQuantity: 17,
      sellUnitPrice: 230,
      purchaseQuantity: 4,
      boardView: "list",
    });
  });

  it.each([
    ["POTATO_M1", "material"],
    ["skillbook:active_heavy:normal", "skillbook"],
    ["gem:1:flat_attack", "gem"],
  ] as const)("opens %s in its matching marketplace category", (itemId, category) => {
    expect(marketCategoryForItemId(itemId)).toBe(category);
  });

  it("restores the first item detail when returning to a cached inventory", async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    client.setQueryData(["inventory", "ALL", "ACQUIRED_DESC", null], page);

    render(
      <QueryClientProvider client={client}>
        <InventoryScreen dataSource={dataSource} onSell={() => undefined} />
      </QueryClientProvider>,
    );

    expect(await screen.findByRole("button", { name: "감자 한 조각 거래소에서 바로 판매" })).toBeTruthy();
  });
});
