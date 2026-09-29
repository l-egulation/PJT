import React from "react";
import { createRoot } from "react-dom/client";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { EquipmentScreen } from "./EquipmentScreen";
import type { EquipmentMaterialCost, EquipmentSlot, EquipmentState } from "./api";
import "../../styles.css";

const material = (itemId: string, displayName: string, requiredQuantity: number, availableQuantity: number): EquipmentMaterialCost => ({ itemId, displayName, requiredQuantity, availableQuantity });
const rows: Array<[EquipmentSlot, string, number, number, number, number, number]> = [
  ["WEAPON", "무기", 15, 52, 0, 0, 450], ["GLOVES", "장갑", 10, 31, 0, 0, 300],
  ["ARMOR", "갑옷", 7, 0, 427, 0, 210], ["HELMET", "투구", 7, 0, 285, 0, 210],
  ["CAPE", "망토", 7, 0, 0, 25, 210], ["SHOES", "신발", 7, 0, 0, 17, 210],
];
const data: EquipmentState = { riceBalance: 61_238, slots: rows.map(([slot, slotName, level, attack, maxHp, penetration, riceCost], index) => {
  const stats = { attack, maxHp, penetration };
  const statIncrease = { attack: attack ? 1 : 0, maxHp: maxHp ? (index === 2 ? 11 : 7) : 0, penetration: penetration ? (index === 4 ? 4 : 2) : 0 };
  const materials = [material("POTATO_M1", "감자 한 조각", index === 0 ? 250 : 125, 116), material("SWEET_POTATO_M1", "고구마 한 조각", index === 0 ? 250 : 125, 925), material("CORN_M1", "옥수수 한 알", index === 0 ? 250 : 125, 391)];
  return { slot, slotName, unlocked: true, current: { grade: "NORMAL", gradeName: "노말", enhancementLevel: level, q: level, stats }, unlock: null, enhance: { cost: { riceCost, materials }, result: { grade: "NORMAL", gradeName: "노말", enhancementLevel: level + 1, q: level + 1, stats: { attack: attack + statIncrease.attack, maxHp: maxHp + statIncrease.maxHp, penetration: penetration + statIncrease.penetration } }, statIncrease, executable: index !== 0, disabledReason: index === 0 ? "INSUFFICIENT_MATERIALS" : null }, promote: null, growthComplete: false };
}) };
data.slots[0].current = { grade: "RARE", gradeName: "희귀", enhancementLevel: 1, q: 40, stats: { attack: 96, maxHp: 0, penetration: 0 } };
data.slots[0].enhance = {
  cost: {
    riceCost: 930,
    materials: [
      material("POTATO_M1", "감자 한 조각", 500, 420), material("SWEET_POTATO_M1", "고구마 M1", 500, 420), material("CORN_M1", "옥수수 한 알", 500, 391),
      material("POTATO_M2", "미니 감자", 100, 72), material("SWEET_POTATO_M2", "미니 고구마", 100, 114), material("CORN_M2", "미니 옥수수", 100, 87),
    ],
  },
  result: { grade: "RARE", gradeName: "희귀", enhancementLevel: 2, q: 41, stats: { attack: 98, maxHp: 0, penetration: 0 } },
  statIncrease: { attack: 2, maxHp: 0, penetration: 0 },
  executable: false,
  disabledReason: "INSUFFICIENT_MATERIALS",
};
data.slots[0].promote = null;

const nativeFetch = window.fetch.bind(window);
const previewResponse = (value: unknown) => new Response(JSON.stringify({ data: value }), { status: 200, headers: { "Content-Type": "application/json" } });
window.fetch = async (input: RequestInfo | URL, init?: RequestInit) => {
  const requestUrl = new URL(input instanceof Request ? input.url : input.toString(), window.location.origin);
  const method = (init?.method ?? (input instanceof Request ? input.method : "GET")).toUpperCase();
  if (method === "GET" && requestUrl.pathname === "/api/v1/equipment") return previewResponse(data);

  const quoteMatch = requestUrl.pathname.match(/^\/api\/v1\/market\/materials\/([^/]+)\/purchase-quote$/);
  if (method === "GET" && quoteMatch) {
    const itemId = decodeURIComponent(quoteMatch[1]);
    const requestedQuantity = Math.max(Number(requestUrl.searchParams.get("quantity")) || 1, 1);
    return previewResponse({ itemId, requestedQuantity, lowestMarketUnitPrice: 12, lowestPurchasableUnitPrice: 14, purchasableQuantity: requestedQuantity, riceBalance: data.riceBalance, marketRevision: 1 });
  }

  const purchaseMatch = requestUrl.pathname.match(/^\/api\/v1\/market\/materials\/([^/]+)\/purchase$/);
  if (method === "POST" && purchaseMatch) {
    const itemId = decodeURIComponent(purchaseMatch[1]);
    const body = JSON.parse(init?.body?.toString() ?? "{}") as { quantity?: number; maxUnitPrice?: number };
    const requestedQuantity = Math.max(Math.trunc(body.quantity ?? 0), 0);
    const unitPrice = 14;
    const affordableQuantity = Math.floor(data.riceBalance / unitPrice);
    const purchasedQuantity = body.maxUnitPrice !== undefined && body.maxUnitPrice >= unitPrice ? Math.min(requestedQuantity, affordableQuantity) : 0;
    data.riceBalance -= purchasedQuantity * unitPrice;
    data.slots.forEach(slot => slot.enhance?.cost.materials.forEach(materialCost => {
      if (materialCost.itemId === itemId) materialCost.availableQuantity += purchasedQuantity;
    }));
    return previewResponse({ result: { itemId, requestedQuantity, purchasedQuantity, remainingRequestedQuantity: requestedQuantity - purchasedQuantity, totalPrice: purchasedQuantity * unitPrice, highestFilledUnitPrice: purchasedQuantity > 0 ? unitPrice : 0 } });
  }

  return nativeFetch(input, init);
};

const client = new QueryClient({ defaultOptions: { queries: { staleTime: Number.POSITIVE_INFINITY } } });
client.setQueryData(["equipment"], data);
const previewQuotes: Array<[string, number]> = [
  ["POTATO_M1", 80], ["SWEET_POTATO_M1", 80], ["CORN_M1", 109], ["POTATO_M2", 28], ["CORN_M2", 13],
];
previewQuotes.forEach(([itemId, requestedQuantity]) => {
  client.setQueryData(["equipment-market-quote", itemId, requestedQuantity], { itemId, requestedQuantity, lowestMarketUnitPrice: 12, lowestPurchasableUnitPrice: 14, purchasableQuantity: requestedQuantity, riceBalance: 61_238, marketRevision: 1 });
});

const closePreview = () => {
  if (window.history.length > 1) {
    window.history.back();
    return;
  }
  window.location.assign("/");
};

createRoot(document.getElementById("root")!).render(<React.StrictMode><QueryClientProvider client={client}><EquipmentScreen onClose={closePreview} /></QueryClientProvider></React.StrictMode>);

if (new URLSearchParams(window.location.search).has("market")) {
  window.setTimeout(() => document.querySelector<HTMLButtonElement>(".equipment-material-cell.short")?.click(), 300);
}
