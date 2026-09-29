import React, { useState } from "react";
import { createRoot } from "react-dom/client";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { InventoryWindow } from "../src/features/inventory/InventoryScreen";
import { inventoryApi, type InventoryItem } from "../src/features/inventory/api";
import { gemsApi } from "../src/features/gems/api";
import { backgroundAsset } from "../src/features/battle/battleVisuals";
import "../src/styles.css";
import "./inventory.css";

// Standalone QA entry only: no authentication, network requests or database writes.
const base: InventoryItem = { itemId: "POTATO_M1", slotId: "stack:POTATO_M1:0", instanceId: null, members: [], acquiredSequence: 1, name: "강화 재료", icon: "/assets/items/POTATO_M1.png", category: "MATERIAL", description: "강화 재료", acquisitionSources: ["전투"], usages: ["강화"], tradeable: true, totalQuantity: 999, reservedQuantity: 1, availableQuantity: 998 };
const rows: InventoryItem[] = Array.from({ length: 41 }, (_, index) => ({ ...base, slotId: `stack:POTATO_M1:${index}` }));
rows.unshift({ ...base, itemId: "gem:1:flat_attack", slotId: "instance:gem-0", category: "GEM", name: "같은 옵션과 수치를 가진 아주 긴 이름의 공격력 보석", totalQuantity: 99, reservedQuantity: 1, availableQuantity: 98, members: Array.from({ length: 99 }, (_, index) => ({ instanceId: `gem-${index}`, reservedForSale: index === 0 })) });
inventoryApi.list = async (category, sort, cursor) => {
  const filtered = rows.filter((item) => category === "ALL" || item.category === category);
  filtered.sort((a, b) => sort === "QUANTITY_ASC" ? a.totalQuantity - b.totalQuantity : sort === "QUANTITY_DESC" ? b.totalQuantity - a.totalQuantity : a.name.localeCompare(b.name));
  const offset = Number(cursor ?? 0);
  return { items: filtered.slice(offset, offset + 12), usedSlots: rows.length, maxSlots: 200, isFull: false, nextCursor: offset + 12 < filtered.length ? String(offset + 12) : null };
};
inventoryApi.detail = async (id) => {
  const owned = rows.filter((item) => item.itemId === id);
  return { ...owned[0]!, slotId: null, members: [], totalQuantity: owned.reduce((n, item) => n + item.totalQuantity, 0), reservedQuantity: owned.reduce((n, item) => n + item.reservedQuantity, 0), availableQuantity: owned.reduce((n, item) => n + item.availableQuantity, 0) };
};
gemsApi.state = async () => ({ unlocked: true, tickets: 0, secondsUntilNextTicket: 0, todayBoss: "MAIN", gemBoxQuantity: 0, presets: {}, contentVersion: "qa", gems: Array.from({ length: 99 }, (_, index) => ({ gemId: `gem-${index}`, level: 1, option: "FLAT_ATTACK", optionName: "공격력", value: 3, locked: !!rows.find((item) => item.category === "GEM")?.locked, equippedPresets: [] })) });
gemsApi.lockSlot = async (slotId, locked) => {
  const row = rows.find((item) => item.slotId === `instance:${slotId}`);
  if (!row) throw new Error("GEM_NOT_FOUND");
  rows[rows.indexOf(row)] = { ...row, locked };
  return gemsApi.state();
};
function InventoryQaApp() {
  const [open, setOpen] = useState(true);
  return <main className="inventory-qa-stage" style={{ backgroundImage: `url(${backgroundAsset(null, "stage.01-07")})` }}>
    <div className="inventory-qa-stage-copy" aria-hidden="true"><strong>STAGE 01-07</strong><span>전투 화면 유지 미리보기</span></div>
    {!open && <button type="button" className="inventory-qa-open" onClick={() => setOpen(true)}>인벤토리 열기</button>}
    <InventoryWindow open={open} onClose={() => setOpen(false)} />
  </main>;
}

createRoot(document.getElementById("root")!).render(<QueryClientProvider client={new QueryClient()}><InventoryQaApp /></QueryClientProvider>);
