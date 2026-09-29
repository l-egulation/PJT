using TypeSpec.Http;
namespace Hanjjak;

union EquipmentSlot {
  WEAPON: "WEAPON",
  GLOVES: "GLOVES",
  ARMOR: "ARMOR",
  HELMET: "HELMET",
  CAPE: "CAPE",
  SHOES: "SHOES"
}

union EquipmentGrade {
  NORMAL: "NORMAL",
  RARE: "RARE",
  EPIC: "EPIC",
  LEGENDARY: "LEGENDARY"
}

model EquipmentMaterialCost {
  itemId: string;
  displayName: string;
  requiredQuantity: int64;
  availableQuantity: int64;
}

model EquipmentCost {
  riceCost: int64;
  materials: EquipmentMaterialCost[];
}

model EquipmentStatSummary {
  attack: int32;
  maxHp: int32;
  penetration: int32;
}

model EquipmentGrowthSummary {
  grade: EquipmentGrade | null;
  gradeName: string | null;
  enhancementLevel: int32;
  q: int32;
  stats: EquipmentStatSummary;
}

model EquipmentActionSummary {
  cost: EquipmentCost;
  result: EquipmentGrowthSummary;
  statIncrease: EquipmentStatSummary;
  executable: boolean;
  disabledReason: string | null;
}

model EquipmentPromotionSummary {
  requiredStageId: string;
  chapterCleared: boolean;
  maxEnhancementReached: boolean;
  cost: EquipmentCost;
  result: EquipmentGrowthSummary;
  executable: boolean;
  disabledReason: string | null;
}

model EquipmentSlotSummary {
  slot: EquipmentSlot;
  slotName: string;
  unlocked: boolean;
  current: EquipmentGrowthSummary;
  unlock: EquipmentActionSummary | null;
  enhance: EquipmentActionSummary | null;
  promote: EquipmentPromotionSummary | null;
  growthComplete: boolean;
}

model EquipmentState {
  slots: EquipmentSlotSummary[];
  riceBalance: int64;
}

model EquipmentCommandResult {
  slot: EquipmentSlotSummary;
  state: EquipmentState;
}

@route("/api/v1/equipment")
interface Equipment {
  @get state(): SuccessEnvelope<EquipmentState> | ErrorEnvelope;
  @post @route("/{slot}/unlock") unlock(@path slot: EquipmentSlot, @header("Idempotency-Key") idempotencyKey: Uuid, @body request: EmptyRequest): SuccessEnvelope<EquipmentCommandResult> | ErrorEnvelope;
  @post @route("/{slot}/enhance") enhance(@path slot: EquipmentSlot, @header("Idempotency-Key") idempotencyKey: Uuid, @body request: EmptyRequest): SuccessEnvelope<EquipmentCommandResult> | ErrorEnvelope;
  @post @route("/{slot}/promote") promote(@path slot: EquipmentSlot, @header("Idempotency-Key") idempotencyKey: Uuid, @body request: EmptyRequest): SuccessEnvelope<EquipmentCommandResult> | ErrorEnvelope;
}
