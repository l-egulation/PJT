namespace Hanjjak;

union DomainEventType {
  BATTLE_CYCLE_COMPLETED: "BATTLE_CYCLE_COMPLETED",
  BATTLE_ENEMY_SETTLED: "BATTLE_ENEMY_SETTLED",
  ITEM_DROPPED: "ITEM_DROPPED",
  STAGE_FIRST_CLEAR_REWARD: "STAGE_FIRST_CLEAR_REWARD",
  STAGE_FIRST_CLEAR_REWARD_CLAIMED: "STAGE_FIRST_CLEAR_REWARD_CLAIMED",
  EQUIPMENT_CRAFTED: "EQUIPMENT_CRAFTED",
  EQUIPMENT_ENHANCEMENT_ATTEMPTED: "EQUIPMENT_ENHANCEMENT_ATTEMPTED",
  EQUIPMENT_PROMOTED: "EQUIPMENT_PROMOTED",
  EQUIPMENT_DISMANTLED: "EQUIPMENT_DISMANTLED",
  MARKET_ORDER_CREATED: "MARKET_ORDER_CREATED",
  MARKET_ORDER_UPDATED: "MARKET_ORDER_UPDATED",
  MARKET_ORDER_CANCELLED: "MARKET_ORDER_CANCELLED",
  MARKET_TRADE_COMPLETED: "MARKET_TRADE_COMPLETED",
  MATERIAL_PREFERENCE_SELECTED: "MATERIAL_PREFERENCE_SELECTED",
}

union DomainEventProducer {
  GAME_API: "game-api",
}

model DomainEventEnvelope {
  eventId: Uuid;
  eventType: DomainEventType;
  schemaVersion: int32;
  occurredAt: Instant;
  producer: DomainEventProducer;
  accountId?: Uuid;
  characterId?: Uuid;
  chapter?: int32;
  stageId?: string;
  itemId?: string;
  quantity?: int64;
  unitPrice?: int64;
  totalPrice?: int64;
  commandId?: Uuid;
  correlationId?: Uuid;
}

model BattleEnemySettledPayload {
  settlementId: Uuid;
  battleSessionId: Uuid;
  stageId: string;
  chapter: int32;
  enemyIndex: int32;
  boss: boolean;
  experienceGained: int64;
  riceGained: int64;
}

model BattleCycleCompletedPayload {
  stageId: string;
  chapter: int32;
  success: boolean;
  failureCode: string | null;
  elapsedTicks: int32;
  defeatedNormals: int32;
  remainingHp: int32;
  experienceGained: int64;
  riceGained: int64;
}

model EventItemQuantity {
  itemId: string;
  quantity: int64;
}

model FirstClearRewardPayload {
  stageId: string;
  rewardId: Uuid;
  rewardVersion: string;
  riceGranted: int64;
  grantedItems: EventItemQuantity[];
  pendingItems: EventItemQuantity[];
  unlockedSkillId: string | null;
  itemStatus: string;
  requiredSlots: int32;
}
model EquipmentCraftedPayload {
  equipmentSlot: EquipmentSlot;
  itemId: string;
  quantity: int64;
  gradeAfter: EquipmentGrade;
  levelAfter: int32;
  consumedItems: EventItemQuantity[];
  riceCost: int64;
}

model EquipmentEnhancementAttemptedPayload {
  equipmentSlot: EquipmentSlot;
  itemId: string;
  gradeBefore: EquipmentGrade;
  gradeAfter: EquipmentGrade;
  levelBefore: int32;
  levelAfter: int32;
  success: boolean;
  consumedItems: EventItemQuantity[];
  riceCost: int64;
}

model EquipmentPromotedPayload {
  equipmentSlot: EquipmentSlot;
  itemId: string;
  gradeBefore: EquipmentGrade;
  gradeAfter: EquipmentGrade;
  levelBefore: int32;
  levelAfter: int32;
  consumedItems: EventItemQuantity[];
  riceCost: int64;
}

model EquipmentDismantledPayload {
  equipmentSlot: EquipmentSlot;
  itemId: string;
  returnedItems: EventItemQuantity[];
}

model MarketOrderChangedPayload {
  orderId: Uuid;
  instrumentId: Uuid;
  side: "BUY" | "SELL";
  timeInForce: "GTC" | "IOC";
  marketRevision: int64;
}

model MarketTradeCompletedPayload {
  tradeId: Uuid;
  makerOrderId: Uuid;
  takerOrderId: Uuid;
  instrumentId: Uuid;
  buyerAccountId: Uuid;
  sellerAccountId: Uuid;
  itemId: string;
  quantity: int64;
  unitPrice: int64;
  totalPrice: int64;
  fee: int64;
  settlementAmount: int64;
  marketRevision: int64;
}

model MaterialPreferenceSelectedPayload {
  materialType: string;
  dropRates: MaterialPreferenceSelectedDropRates;
}

model MaterialPreferenceSelectedDropRates {
  potato: int32;
  sweetPotato: int32;
  corn: int32;
}

model BattleEnemySettledEvent extends DomainEventEnvelope {
  eventType: "BATTLE_ENEMY_SETTLED";
  payload: BattleEnemySettledPayload;
}

model BattleCycleCompletedEvent extends DomainEventEnvelope {
  eventType: "BATTLE_CYCLE_COMPLETED";
  payload: BattleCycleCompletedPayload;
}


model FirstClearRewardEvent extends DomainEventEnvelope {
  eventType: "STAGE_FIRST_CLEAR_REWARD";
  payload: FirstClearRewardPayload;
}

model FirstClearRewardClaimedEvent extends DomainEventEnvelope {
  eventType: "STAGE_FIRST_CLEAR_REWARD_CLAIMED";
  payload: FirstClearRewardClaimedPayload;
}

model FirstClearRewardClaimedPayload {
  stageId: string;
  rewardId: Uuid;
  rewardVersion: string;
  grantedItems: EventItemQuantity[];
  itemStatus: string;
}

model EquipmentCraftedEvent extends DomainEventEnvelope {
  eventType: "EQUIPMENT_CRAFTED";
  payload: EquipmentCraftedPayload;
}

model EquipmentEnhancementAttemptedEvent extends DomainEventEnvelope {
  eventType: "EQUIPMENT_ENHANCEMENT_ATTEMPTED";
  payload: EquipmentEnhancementAttemptedPayload;
}

model EquipmentPromotedEvent extends DomainEventEnvelope {
  eventType: "EQUIPMENT_PROMOTED";
  payload: EquipmentPromotedPayload;
}

model EquipmentDismantledEvent extends DomainEventEnvelope {
  eventType: "EQUIPMENT_DISMANTLED";
  payload: EquipmentDismantledPayload;
}

model MarketOrderCreatedEvent extends DomainEventEnvelope {
  eventType: "MARKET_ORDER_CREATED";
  payload: MarketOrderChangedPayload;
}

model MarketOrderUpdatedEvent extends DomainEventEnvelope {
  eventType: "MARKET_ORDER_UPDATED";
  payload: MarketOrderChangedPayload;
}

model MarketOrderCancelledEvent extends DomainEventEnvelope {
  eventType: "MARKET_ORDER_CANCELLED";
  payload: MarketOrderChangedPayload;
}

model MarketTradeCompletedEvent extends DomainEventEnvelope {
  eventType: "MARKET_TRADE_COMPLETED";
  payload: MarketTradeCompletedPayload;
}

model MaterialPreferenceSelectedEvent extends DomainEventEnvelope {
  eventType: "MATERIAL_PREFERENCE_SELECTED";
  payload: MaterialPreferenceSelectedPayload;
}

union DomainEvent {
  battleEnemySettled: BattleEnemySettledEvent,
  battleCycleCompleted: BattleCycleCompletedEvent,
  firstClearReward: FirstClearRewardEvent,
  firstClearRewardClaimed: FirstClearRewardClaimedEvent,
  equipmentEnhancementAttempted: EquipmentEnhancementAttemptedEvent,
  equipmentPromoted: EquipmentPromotedEvent,
  equipmentDismantled: EquipmentDismantledEvent,
  marketOrderCreated: MarketOrderCreatedEvent,
  marketOrderUpdated: MarketOrderUpdatedEvent,
  marketOrderCancelled: MarketOrderCancelledEvent,
  marketTradeCompleted: MarketTradeCompletedEvent,
  materialPreferenceSelected: MaterialPreferenceSelectedEvent,
}
