using TypeSpec.Http;
namespace Hanjjak;

union InventoryCategory {
  MATERIAL: "MATERIAL",
  SKILL_BOOK: "SKILL_BOOK",
  GEM: "GEM",
  GEM_BOX: "GEM_BOX",
  COSMETIC_BOX: "COSMETIC_BOX"
}

union InventorySort {
  NAME_ASC: "NAME_ASC",
  NAME_DESC: "NAME_DESC",
  QUANTITY_ASC: "QUANTITY_ASC",
  QUANTITY_DESC: "QUANTITY_DESC",
  ACQUIRED_DESC: "ACQUIRED_DESC"
}

model InventoryItem {
  itemId: string;
  instanceId: string | null;
  slotId: string | null;
  locked: boolean;
  members: InventoryMember[];
  acquiredSequence: int64;
  name: string;
  icon: string;
  category: InventoryCategory;
  description: string;
  acquisitionSources: string[];
  usages: string[];
  tradeable: boolean;
  totalQuantity: int64;
  reservedQuantity: int64;
  availableQuantity: int64;
}

model InventoryMember {
  instanceId: string;
  reservedForSale: boolean;
}

model InventoryPage {
  items: InventoryItem[];
  usedSlots: int32;
  maxSlots: int32;
  isFull: boolean;
  nextCursor: string | null;
}

model InventoryStatus {
  usedSlots: int32;
  maxSlots: int32;
  isFull: boolean;
}

@route("/api/v1/inventory")
interface Inventory {
  @get list(
    @query category?: InventoryCategory,
    @query sort?: InventorySort,
    @query cursor?: string,
    @query limit?: int32
  ): SuccessEnvelope<InventoryPage> | ErrorEnvelope;

  @get @route("/items/{itemId}") detail(@path itemId: string): SuccessEnvelope<InventoryItem> | ErrorEnvelope;
  @get @route("/status") status(): SuccessEnvelope<InventoryStatus> | ErrorEnvelope;
}
