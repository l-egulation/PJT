// @vitest-environment happy-dom

import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { InventoryScreen } from "./InventoryScreen";
import type { InventoryItem, InventoryPage } from "./api";

const stack = (slotId: string, totalQuantity: number): InventoryItem => ({
  itemId: "SWEET_POTATO_M2",
  instanceId: null,
  slotId,
  members: [],
  acquiredSequence: 10,
  name: "미니 고구마",
  icon: "/sweet-potato.png",
  category: "MATERIAL",
  description: "장비 제작과 강화에 사용하는 2세대 재료",
  acquisitionSources: ["챕터 2 이후 자동 파밍"],
  usages: ["장비 제작", "장비 강화"],
  tradeable: true,
  totalQuantity,
  reservedQuantity: 0,
  availableQuantity: totalQuantity,
});

const skillBook = (skillId: string, name: string): InventoryItem => ({
  itemId: `skillbook:${skillId}:normal`,
  instanceId: null,
  slotId: `stack:skillbook:${skillId}:normal:0`,
  members: [],
  acquiredSequence: 1,
  name: `노말 ${name} 비법서`,
  icon: "/generic-skillbook.png",
  category: "SKILL_BOOK",
  description: `${name} 스킬의 노말 등급 성장에 사용하는 전용 스킬북입니다.`,
  acquisitionSources: ["자동 파밍"],
  usages: ["스킬 강화"],
  tradeable: true,
  totalQuantity: 10,
  reservedQuantity: 0,
  availableQuantity: 10,
});

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("inventory total quantity detail", () => {
  it("keeps 999-unit stacks in the grid and shows their aggregate in the detail panel", async () => {
    const page: InventoryPage = {
      items: [
        stack("stack:SWEET_POTATO_M2:0", 999),
        stack("stack:SWEET_POTATO_M2:1", 999),
        stack("stack:SWEET_POTATO_M2:2", 662),
      ],
      usedSlots: 3,
      maxSlots: 200,
      isFull: false,
      nextCursor: null,
    };
    const detail = { ...stack("detail", 2_660), slotId: null };
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => Response.json({
      data: String(input).includes("/items/") ? detail : page,
    })));

    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const { container } = render(<QueryClientProvider client={client}><InventoryScreen /></QueryClientProvider>);

    expect(await screen.findAllByText("999개")).toHaveLength(2);
    expect(screen.getByText("662개")).toBeTruthy();
    await waitFor(() => expect(container.querySelector(".inventory-v2-quantity strong")?.textContent).toBe("2,660개"));
  });
});

describe("skill-specific skillbook presentation", () => {
  it("uses a distinct skill badge and separates category, grade, and book name", async () => {
    const haste = skillBook("active_haste", "잘게 더 잘게!");
    const page: InventoryPage = { items: [haste], usedSlots: 1, maxSlots: 200, isFull: false, nextCursor: null };
    const dataSource = {
      list: vi.fn(async () => page),
      detail: vi.fn(async () => ({ ...haste, slotId: null })),
    };
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const { container } = render(<QueryClientProvider client={client}><InventoryScreen dataSource={dataSource} /></QueryClientProvider>);

    await screen.findByText("잘게 더 잘게! 비법서", { selector: ".inventory-v2-detail-name" });
    expect(container.querySelector(".inventory-v2-detail-heading")).toBeNull();
    expect(container.querySelector(".inventory-v2-detail-tags")?.textContent).toBe("거래 가능노말");
    const detailBadge = container.querySelector<HTMLImageElement>(".inventory-v2-detail-hero .inventory-v2-skillbook-badge");
    const gridBadge = container.querySelector<HTMLImageElement>(".inventory-v2-grid .inventory-v2-skillbook-badge");
    expect(detailBadge?.src).toContain("skill-active-haste");
    expect(gridBadge?.src).toContain("skill-active-haste");
    expect(detailBadge?.src).not.toContain("generic-skillbook");
    expect(screen.queryByText("노말 잘게 더 잘게! 비법서", { selector: ".inventory-v2-detail-name" })).toBeNull();
  });

  it("maps every skillbook id to a different badge", async () => {
    const ids = ["active_heavy", "active_dot", "active_haste", "active_basic_amp", "passive_critical", "passive_all_damage"];
    const books = ids.map((id) => skillBook(id, id));
    const page: InventoryPage = { items: books, usedSlots: books.length, maxSlots: 200, isFull: false, nextCursor: null };
    const dataSource = { list: vi.fn(async () => page), detail: vi.fn(async () => ({ ...books[0], slotId: null })) };
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const { container } = render(<QueryClientProvider client={client}><InventoryScreen dataSource={dataSource} /></QueryClientProvider>);

    await screen.findByText("active_heavy 비법서", { selector: ".inventory-v2-detail-name" });
    const sources = Array.from(container.querySelectorAll<HTMLImageElement>(".inventory-v2-grid .inventory-v2-skillbook-badge"), image => image.src);
    expect(new Set(sources).size).toBe(ids.length);
  });
});

describe("gem box inventory action", () => {
  it("uses the treasure chest asset and opens boxes directly from the detail panel", async () => {
    const box: InventoryItem = {
      itemId: "GEM_BOX", instanceId: null, slotId: "stack:GEM_BOX:0", members: [], acquiredSequence: 1,
      name: "보석함", icon: "/old-gem.png", category: "GEM_BOX", description: "보석이 든 상자",
      acquisitionSources: ["보석 던전"], usages: ["보석 획득"], tradeable: false,
      totalQuantity: 3, reservedQuantity: 0, availableQuantity: 3,
    };
    const dataSource = { list: vi.fn(async () => ({ items: [box], usedSlots: 1, maxSlots: 200, isFull: false, nextCursor: null })), detail: vi.fn(async () => ({ ...box, slotId: null })) };
    const fetch = vi.fn(async () => Response.json({ data: { granted: [{ gemId: "gem-1", level: 1, option: "FLAT_ATTACK", optionName: "고정 공격력", value: 60, locked: false, reservedForSale: false, equippedPresets: [] }], state: { gems: [] } } }));
    vi.stubGlobal("fetch", fetch);
    const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
    const { container } = render(<QueryClientProvider client={client}><InventoryScreen dataSource={dataSource} /></QueryClientProvider>);

    const open = await screen.findByRole("button", { name: "보석함 1개 열기" });
    expect(container.querySelector<HTMLImageElement>(".inventory-v2-grid .inventory-v2-icon")?.src).toContain("assets-gem-box-chest");
    fireEvent.click(open);
    await waitFor(() => expect(fetch).toHaveBeenCalledWith("/api/v1/gems/boxes/open", expect.objectContaining({ method: "POST", body: JSON.stringify({ quantity: 1 }) })));
    expect(await screen.findByText("보석 1개를 획득했습니다.")).toBeTruthy();
  });

  it("caps a bulk open request at the server limit of one hundred boxes", async () => {
    const box: InventoryItem = {
      itemId: "GEM_BOX", instanceId: null, slotId: "stack:GEM_BOX:0", members: [], acquiredSequence: 1,
      name: "보석함", icon: "", category: "GEM_BOX", description: "보석이 든 상자",
      acquisitionSources: ["보석 던전"], usages: ["보석 획득"], tradeable: false,
      totalQuantity: 101, reservedQuantity: 0, availableQuantity: 101,
    };
    const dataSource = { list: vi.fn(async () => ({ items: [box], usedSlots: 1, maxSlots: 200, isFull: false, nextCursor: null })), detail: vi.fn(async () => ({ ...box, slotId: null })) };
    const fetch = vi.fn(async () => Response.json({ data: { granted: [], state: { gems: [] } } }));
    vi.stubGlobal("fetch", fetch);
    const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
    render(<QueryClientProvider client={client}><InventoryScreen dataSource={dataSource} /></QueryClientProvider>);

    fireEvent.click(await screen.findByRole("button", { name: "보석함 최대 100개 열기" }));

    await waitFor(() => expect(fetch).toHaveBeenCalledWith("/api/v1/gems/boxes/open", expect.objectContaining({ body: JSON.stringify({ quantity: 100 }) })));
  });
});
