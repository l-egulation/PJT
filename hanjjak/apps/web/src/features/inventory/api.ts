export type InventoryCategory = "MATERIAL" | "SKILL_BOOK" | "GEM" | "GEM_BOX" | "COSMETIC_BOX";
export type InventorySort = "ACQUIRED_DESC" | "NAME_ASC" | "NAME_DESC" | "QUANTITY_ASC" | "QUANTITY_DESC";

export type InventoryItem = {
  itemId: string;
  instanceId: string | null;
  slotId: string | null;
  locked?: boolean;
  members: { instanceId: string; reservedForSale: boolean }[];
  acquiredSequence: number;
  name: string;
  icon: string;
  category: InventoryCategory;
  description: string;
  acquisitionSources: string[];
  usages: string[];
  tradeable: boolean;
  totalQuantity: number;
  reservedQuantity: number;
  availableQuantity: number;
};

export type InventoryPage = {
  items: InventoryItem[];
  usedSlots: number;
  maxSlots: number;
  isFull: boolean;
  nextCursor: string | null;
};

type Envelope<T> = { data: T };

async function json<T>(response: Response): Promise<T> {
  if (!response.ok) throw new Error((await response.text()) || `HTTP ${response.status}`);
  return (await response.json() as Envelope<T>).data;
}

export const inventoryApi = {
  list: (category: InventoryCategory | "ALL", sort: InventorySort, cursor: string | null) => {
    const query = new URLSearchParams({ sort, limit: "12" });
    if (category !== "ALL") query.set("category", category);
    if (cursor) query.set("cursor", cursor);
    return fetch(`/api/v1/inventory?${query}`, { credentials: "include", headers: { Accept: "application/json" } }).then(json<InventoryPage>);
  },
  detail: (itemId: string) => fetch(`/api/v1/inventory/items/${encodeURIComponent(itemId)}`, { credentials: "include", headers: { Accept: "application/json" } }).then(json<InventoryItem>),
};
