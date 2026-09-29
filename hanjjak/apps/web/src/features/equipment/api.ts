export type EquipmentSlot = "WEAPON" | "GLOVES" | "ARMOR" | "HELMET" | "CAPE" | "SHOES";
export type EquipmentGrade = "NORMAL" | "RARE" | "EPIC" | "LEGENDARY";
export type EquipmentMaterialCost = { itemId: string; displayName: string; requiredQuantity: number; availableQuantity: number };
export type MaterialCost = EquipmentMaterialCost;
export type EquipmentCost = { riceCost: number; materials: MaterialCost[] };
export type EquipmentStatSummary = { attack: number; maxHp: number; penetration: number };
export type EquipmentGrowthSummary = {
  grade: EquipmentGrade | null;
  gradeName: string | null;
  enhancementLevel: number;
  q: number;
  stats: EquipmentStatSummary;
};
export type EquipmentActionSummary = {
  cost: EquipmentCost;
  result: EquipmentGrowthSummary;
  statIncrease: EquipmentStatSummary;
  executable: boolean;
  disabledReason: string | null;
};
export type EquipmentPromotionSummary = {
  requiredStageId: string;
  chapterCleared: boolean;
  maxEnhancementReached: boolean;
  cost: EquipmentCost;
  result: EquipmentGrowthSummary;
  executable: boolean;
  disabledReason: string | null;
};
export type EquipmentSlotSummary = {
  slot: EquipmentSlot;
  slotName: string;
  unlocked: boolean;
  current: EquipmentGrowthSummary;
  unlock: EquipmentActionSummary | null;
  enhance: EquipmentActionSummary | null;
  promote: EquipmentPromotionSummary | null;
  growthComplete: boolean;
};
export type EquipmentState = { slots: EquipmentSlotSummary[]; riceBalance: number };
export type EquipmentCommandResult = { slot: EquipmentSlotSummary; state: EquipmentState };
export type EquipmentCommand = { kind: "unlock" | "enhance" | "promote"; slot: EquipmentSlot; key: string };

export type EquipmentMarketInstrument = { instrumentId: string; itemId: string; bestAskUnitPrice: number | null };
export type EquipmentMarketPurchaseQuote = { instrumentId: string; requestedQuantity: number; expectedFilledQuantity: number; expectedRemainingQuantity: number; lowestFilledUnitPrice: number | null; highestFilledUnitPrice: number | null; weightedAverageUnitPrice: number | null; totalPrice: number; availableRice: number; marketRevision: number };
export type EquipmentMarketPurchaseResult = { instrumentId: string; initialQuantity: number; filledQuantity: number; remainingQuantity: number; totalPrice: number; highestFilledUnitPrice: number | null };
export type EquipmentMaterialPurchaseCommand = { instrumentId: string; quantity: number; maxUnitPrice: number; key: string };
type Envelope<T> = { data: T };
type ErrorEnvelope = { code?: string; messageKey?: string };

export class EquipmentApiError extends Error {
  constructor(public status: number, public code: string) { super(code); }
}

async function json<T>(response: Response): Promise<T> {
  const body = await response.json().catch(() => ({})) as Envelope<T> & ErrorEnvelope;
  if (!response.ok) throw new EquipmentApiError(response.status, body.code || body.messageKey || `HTTP_${response.status}`);
  return body.data;
}

function commandHeaders(key: string): HeadersInit {
  return { Accept: "application/json", "Content-Type": "application/json", "Idempotency-Key": key };
}

export const equipmentApi = {
  state: () => fetch("/api/v1/equipment", { credentials: "include", headers: { Accept: "application/json" } }).then(json<EquipmentState>),
  command: (command: EquipmentCommand) => fetch(`/api/v1/equipment/${encodeURIComponent(command.slot)}/${command.kind}`, {
    method: "POST",
    credentials: "include",
    headers: commandHeaders(command.key),
    body: "{}",
  }).then(json<EquipmentCommandResult>),
  marketInstruments: () => fetch("/api/v1/market/instruments", { credentials: "include", headers: { Accept: "application/json" } }).then(json<EquipmentMarketInstrument[]>),
  purchaseQuote: (instrumentId: string, quantity: number, maxUnitPrice: number) => fetch(`/api/v1/market/instruments/${instrumentId}/order-quote?side=BUY&timeInForce=IOC&quantity=${quantity}&limitUnitPrice=${maxUnitPrice}`, { credentials: "include", headers: { Accept: "application/json" } }).then(json<EquipmentMarketPurchaseQuote>),
  purchaseMaterial: (command: EquipmentMaterialPurchaseCommand) => fetch("/api/v1/market/orders", {
    method: "POST",
    credentials: "include",
    headers: commandHeaders(command.key),
    body: JSON.stringify({ instrumentId: command.instrumentId, side: "BUY", quantity: command.quantity, limitUnitPrice: command.maxUnitPrice, timeInForce: "IOC" }),
  }).then(json<{ result: EquipmentMarketPurchaseResult }>),
};

export function isUncertainEquipmentCommandError(error: unknown): boolean {
  return !(error instanceof EquipmentApiError) || error.status >= 500;
}
