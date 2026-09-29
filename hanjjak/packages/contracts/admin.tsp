using TypeSpec.Http;

namespace Hanjjak;

union AdminRole {
  VIEWER: "VIEWER",
  CS_OPERATOR: "CS_OPERATOR",
  GAME_OPERATOR: "GAME_OPERATOR",
  ENGINEER: "ENGINEER",
  ADMIN: "ADMIN",
}

model AdminIdentity {
  operatorId: Uuid;
  username: string;
  displayName: string;
  roles: AdminRole[];
  permissions: string[];
}

model AdminSessionState {
  authenticated: boolean;
  operator: AdminIdentity | null;
}

model AdminAuthorization {
  authorizationUrl: url;
}

model AdminOAuthRedirect {
  @statusCode statusCode: 302;
  @header location: url;
}

model AdminServiceHealth {
  status: string;
  checkedAt: Instant;
}

model AdminDeploymentInfo {
  application: string;
  commitSha: string;
  contentVersion: string;
  contentAuthority: string;
}

model AdminDashboardCounts {
  totalAccounts: int64;
  activeAccountSessions: int64;
  activeGameSessions: int64;
  activeBattleSessions: int64;
  processingCommands: int64;
  pendingOutboxEvents: int64;
  failedOutboxEvents: int64;
  oldestPendingOutboxAt: Instant | null;
  activeMarketOrders: int64;
  unclaimedMails: int64;
}

model AdminDashboard {
  service: AdminServiceHealth;
  deployment: AdminDeploymentInfo;
  counts: AdminDashboardCounts;
}

model AdminAccountSummary {
  accountId: Uuid;
  characterId: Uuid;
  email: string | null;
  nickname: string;
  stateVersion: int64;
  createdAt: Instant;
  level: int32;
  experience: int64;
  rice: int64;
  primaryMaterialType: string | null;
  currentStageId: string | null;
  repeatStageId: string | null;
  highestClearedStageId: string | null;
  inventoryStackCount: int64;
  inventoryQuantity: int64;
  equipmentSlotCount: int64;
  skillCount: int64;
  gemCount: int64;
  cosmeticCount: int64;
  activeGameSession: boolean;
  activeBattleSession: boolean;
  activeMarketOrderCount: int64;
  unclaimedMailCount: int64;
}

model AdminCommandSummary { commandId: Uuid; status: string; expiresAt: Instant; }
model AdminWalletLedgerSummary { ledgerId: Uuid; delta: int64; balanceAfter: int64; sourceType: string; sourceId: Uuid; createdAt: Instant; }
model AdminBattleSessionSummary { battleSessionId: Uuid; status: string; stageId: string; contentVersion: string; startedAt: Instant; closedAt: Instant | null; }
model AdminAccountDetail { account: AdminAccountSummary; commands: AdminCommandSummary[]; walletLedger: AdminWalletLedgerSummary[]; battles: AdminBattleSessionSummary[]; }

union AdminNumericMode { SET: "SET", ADD: "ADD" }
union AdminEquipmentSlot { WEAPON: "WEAPON", GLOVES: "GLOVES", ARMOR: "ARMOR", HELMET: "HELMET", CAPE: "CAPE", SHOES: "SHOES" }
union AdminEquipmentGrade { NORMAL: "NORMAL", RARE: "RARE", EPIC: "EPIC", LEGENDARY: "LEGENDARY" }
union AdminGemOption { FLAT_ATTACK: "FLAT_ATTACK", FLAT_HP: "FLAT_HP", ATTACK_PERCENT: "ATTACK_PERCENT", FLAT_PENETRATION: "FLAT_PENETRATION", CRITICAL_CHANCE: "CRITICAL_CHANCE", HASTE: "HASTE" }

model AdminInventoryItemState { itemId: string; displayName: string; category: string; quantity: int64; reservedQuantity: int64; stackable: boolean; }
model AdminCosmeticState { cosmeticId: string; displayName: string | null; grade: string; slot: string; registeredQuantity: int32; unregisteredQuantity: int32; reservedQuantity: int32; star: int32; }
model AdminEquipmentState { slot: string; grade: string; enhancementLevel: int32; }
model AdminCatalogItem { itemId: string; displayName: string; category: string; stackable: boolean; tradeable: boolean; }
model AdminCatalogCosmetic { cosmeticId: string; displayName: string; grade: string; slot: string; }
model AdminCatalogStage { stageId: string; displayName: string; }
model AdminCatalog { items: AdminCatalogItem[]; cosmetics: AdminCatalogCosmetic[]; stages: AdminCatalogStage[]; }

model AdminUserState {
  accountId: Uuid;
  stateVersion: int64;
  level: int32;
  experience: int64;
  rice: int64;
  currentStageId: string | null;
  highestUnlockedStageId: string | null;
  items: AdminInventoryItemState[];
  cosmetics: AdminCosmeticState[];
  equipment: AdminEquipmentState[];
}
model AdminUserMutationResult { before: AdminUserState; after: AdminUserState; replayed: boolean; }
model AdminProgressionRequest { level?: int32; experience?: int64; reason: string; }
model AdminRiceRequest { mode: AdminNumericMode; amount: int64; reason: string; }
model AdminItemRequest { itemId: string; mode: AdminNumericMode; quantity: int64; reason: string; }
model AdminGemRequest { level: int32; option: AdminGemOption; delta: int32; reason: string; }
model AdminStageUnlockRequest { throughStageId: string; reason: string; }
model AdminCosmeticRequest { cosmeticId: string; registeredQuantity: int32; unregisteredQuantity: int32; reason: string; }
model AdminEquipmentRequest { slot: AdminEquipmentSlot; grade?: AdminEquipmentGrade; enhancementLevel?: int32; reason: string; }

model AdminOutboxEventSummary {
  eventId: Uuid;
  eventType: string;
  aggregateId: Uuid;
  status: string;
  attemptCount: int32;
  nextRetryAt: Instant | null;
  createdAt: Instant;
  ageSeconds: int64;
}

model AdminAuditEvent {
  auditId: Uuid;
  occurredAt: Instant;
  operatorId: Uuid | null;
  username: string;
  action: string;
  targetType: string;
  targetId: string | null;
  outcome: string;
  requestId: Uuid;
  remoteAddress: string;
  mutation: boolean;
  reason: string | null;
  beforeSummary: string | null;
  afterSummary: string | null;
  idempotencyKey: Uuid | null;
}

model AdminMarketOrderSummary {
  orderId: Uuid;
  accountId: Uuid;
  instrumentId: Uuid;
  itemId: string;
  displayName: string;
  side: "BUY" | "SELL";
  initialQuantity: int64;
  remainingQuantity: int64;
  limitUnitPrice: int64;
  status: string;
  createdAt: Instant;
  updatedAt: Instant;
  expiresAt: Instant | null;
  systemOrder: boolean;
}

model AdminMarketOrderPage { items: AdminMarketOrderSummary[]; nextCursor: string | null; }
model AdminMarketCancelRequest { reason: string; }
model AdminMarketCancelResult { orderId: Uuid; itemId: string; cancelledQuantity: int64; status: string; marketRevision: int64; }
model AdminMarketPurchaseRequest { quantity?: int64; reason: string; }
model AdminMarketPurchaseResult { tradeId: Uuid; orderId: Uuid; itemId: string; quantity: int64; unitPrice: int64; totalPrice: int64; fee: int64; settlementAmount: int64; remainingOrderQuantity: int64; orderStatus: string; marketRevision: int64; }
model AdminMarketSellRequest { itemId: string; quantity: int64; unitPrice: int64; reason: string; }
model AdminMarketSellResult { itemId: string; totalQuantity: int64; unitPrice: int64; orderIds: Uuid[]; marketRevision: int64; }

@route("/api/admin/v1/auth")
interface AdminAuthentication {
  @post @route("/gitlab/begin") beginGitlab(): SuccessEnvelope<AdminAuthorization> | ErrorEnvelope;
  @get @route("/gitlab/callback") gitlabCallback(@query code?: string, @query state?: string, @query error?: string): AdminOAuthRedirect;
  @get @route("/session") session(): SuccessEnvelope<AdminSessionState> | ErrorEnvelope;
  @post @route("/logout") logout(): SuccessEnvelope<AdminSessionState> | ErrorEnvelope;
}

@route("/api/admin/v1")
interface AdminOperations {
  @get @route("/dashboard") dashboard(): SuccessEnvelope<AdminDashboard> | ErrorEnvelope;
  @get @route("/catalog") catalog(): SuccessEnvelope<AdminCatalog> | ErrorEnvelope;
  @get @route("/accounts") searchAccounts(@query query?: string): SuccessEnvelope<AdminAccountSummary[]> | ErrorEnvelope;
  @get @route("/accounts/{accountId}") accountDetail(@path accountId: Uuid): SuccessEnvelope<AdminAccountDetail> | ErrorEnvelope;
  @get @route("/outbox") outbox(@query status: string, @query limit: int32): SuccessEnvelope<AdminOutboxEventSummary[]> | ErrorEnvelope;
  @get @route("/metrics/economy/daily") economyDaily(@query from: string, @query to: string): SuccessEnvelope<EconomyDailyMetric[]> | ErrorEnvelope;
  @get @route("/audit") audit(@query limit: int32, @query operatorId?: Uuid, @query action?: string, @query mutationsOnly?: boolean): SuccessEnvelope<AdminAuditEvent[]> | ErrorEnvelope;
  @get @route("/market/orders") marketOrders(@query query?: string, @query cursor?: string): SuccessEnvelope<AdminMarketOrderPage> | ErrorEnvelope;
  @post @route("/market/orders/{orderId}/cancel") cancelMarketOrder(@path orderId: Uuid, @header idempotencyKey: Uuid, @body request: AdminMarketCancelRequest): SuccessEnvelope<AdminMarketCancelResult> | ErrorEnvelope;
  @post @route("/market/orders/{orderId}/purchase") purchaseMarketOrder(@path orderId: Uuid, @header idempotencyKey: Uuid, @body request: AdminMarketPurchaseRequest): SuccessEnvelope<AdminMarketPurchaseResult> | ErrorEnvelope;
  @post @route("/market/system-orders") createSystemMarketOrder(@header idempotencyKey: Uuid, @body request: AdminMarketSellRequest): SuccessEnvelope<AdminMarketSellResult> | ErrorEnvelope;
}

@route("/api/admin/v1/accounts/{accountId}/management")
interface AdminUserManagement {
  @get state(@path accountId: Uuid): SuccessEnvelope<AdminUserState> | ErrorEnvelope;
  @post @route("/progression") progression(@path accountId: Uuid, @header("Idempotency-Key") key: Uuid, @body request: AdminProgressionRequest): SuccessEnvelope<AdminUserMutationResult> | ErrorEnvelope;
  @post @route("/rice") rice(@path accountId: Uuid, @header("Idempotency-Key") key: Uuid, @body request: AdminRiceRequest): SuccessEnvelope<AdminUserMutationResult> | ErrorEnvelope;
  @post @route("/items") item(@path accountId: Uuid, @header("Idempotency-Key") key: Uuid, @body request: AdminItemRequest): SuccessEnvelope<AdminUserMutationResult> | ErrorEnvelope;
  @post @route("/gems") gem(@path accountId: Uuid, @header("Idempotency-Key") key: Uuid, @body request: AdminGemRequest): SuccessEnvelope<AdminUserMutationResult> | ErrorEnvelope;
  @post @route("/stages") stage(@path accountId: Uuid, @header("Idempotency-Key") key: Uuid, @body request: AdminStageUnlockRequest): SuccessEnvelope<AdminUserMutationResult> | ErrorEnvelope;
  @post @route("/cosmetics") cosmetic(@path accountId: Uuid, @header("Idempotency-Key") key: Uuid, @body request: AdminCosmeticRequest): SuccessEnvelope<AdminUserMutationResult> | ErrorEnvelope;
  @post @route("/equipment") equipment(@path accountId: Uuid, @header("Idempotency-Key") key: Uuid, @body request: AdminEquipmentRequest): SuccessEnvelope<AdminUserMutationResult> | ErrorEnvelope;
}
