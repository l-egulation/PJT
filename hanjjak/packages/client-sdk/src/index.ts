export type ApiClientOptions = { baseUrl?: string; fetch?: typeof globalThis.fetch };

type Envelope<T> = { requestId: string; serverTime: string; stateVersion: number; data: T };
type ErrorEnvelope = { code?: string; messageKey?: string; retryable?: boolean; details?: unknown };

export type AdminIdentity = {
  operatorId: string;
  username: string;
  displayName: string;
  roles: string[];
  permissions: string[];
};

export type AdminSessionState = { authenticated: boolean; operator: AdminIdentity | null };
export type AdminDashboard = {
  service: { status: string; checkedAt: string };
  deployment: { application: string; commitSha: string; contentVersion: string; contentAuthority: string };
  counts: {
    totalAccounts: number;
    activeAccountSessions: number;
    activeGameSessions: number;
    activeBattleSessions: number;
    processingCommands: number;
    pendingOutboxEvents: number;
    failedOutboxEvents: number;
    oldestPendingOutboxAt: string | null;
    activeMarketOrders: number;
    unclaimedMails: number;
  };
};

export type AdminAccountSummary = {
  accountId: string;
  characterId: string;
  email: string | null;
  nickname: string;
  stateVersion: number;
  createdAt: string;
  level: number;
  experience: number;
  rice: number;
  primaryMaterialType: string | null;
  currentStageId: string | null;
  repeatStageId: string | null;
  highestClearedStageId: string | null;
  inventoryStackCount: number;
  inventoryQuantity: number;
  equipmentSlotCount: number;
  skillCount: number;
  gemCount: number;
  cosmeticCount: number;
  activeGameSession: boolean;
  activeBattleSession: boolean;
  activeMarketOrderCount: number;
  unclaimedMailCount: number;
};
export type AdminCatalog = {
  items: { itemId: string; displayName: string; category: string; stackable: boolean; tradeable: boolean }[];
  cosmetics: { cosmeticId: string; displayName: string; grade: string; slot: string }[];
  stages: { stageId: string; displayName: string }[];
};

export type AdminAccountDetail = {
  account: AdminAccountSummary;
  commands: { commandId: string; status: string; expiresAt: string }[];
  walletLedger: { ledgerId: string; delta: number; balanceAfter: number; sourceType: string; sourceId: string; createdAt: string }[];
  battles: { battleSessionId: string; status: string; stageId: string; contentVersion: string; startedAt: string; closedAt: string | null }[];
};

export type AdminInventoryItemState = { itemId: string; displayName: string; category: string; quantity: number; reservedQuantity: number; stackable: boolean };
export type AdminCosmeticState = { cosmeticId: string; displayName: string | null; grade: string; slot: string; registeredQuantity: number; unregisteredQuantity: number; reservedQuantity: number; star: number };
export type AdminEquipmentState = { slot: string; grade: string; enhancementLevel: number };
export type AdminUserState = {
  accountId: string;
  stateVersion: number;
  level: number;
  experience: number;
  rice: number;
  currentStageId: string | null;
  highestUnlockedStageId: string | null;
  items: AdminInventoryItemState[];
  cosmetics: AdminCosmeticState[];
  equipment: AdminEquipmentState[];
};
export type AdminUserMutationResult = { before: AdminUserState; after: AdminUserState; replayed: boolean };

export type AdminOutboxEvent = {
  eventId: string;
  eventType: string;
  aggregateId: string;
  status: "PENDING" | "SENT" | "FAILED";
  attemptCount: number;
  nextRetryAt: string | null;
  createdAt: string;
  ageSeconds: number;
};
export type AdminKafkaOps = {
  outboxClaimed: number;
  outboxSent: number;
  outboxRetry: number;
  outboxFailed: number;
  consumers: { name: string; events: number; duplicates: number; failures: number; batches: number; averageBatchSize: number; processingSeconds: number }[];
};
export type AdminMarketAnomaly = { alertId: string; itemId: string; tradeId: string; alertType: string; unitPrice: number; referencePrice: number; deviationRatio: number; occurredAt: string; createdAt: string };

export type EconomyDailyMetric = {
  metricDate: string;
  itemFamily: string;
  generation: number | null;
  itemId: string;
  listedQuantity: number;
  cancelledQuantity: number;
  tradeCount: number;
  tradedQuantity: number;
  tradeAmount: number;
  feeAmount: number;
  settlementAmount: number;
  droppedQuantity: number;
  consumedQuantity: number;
  riceGenerated: number;
  riceConsumed: number;
};
export type OfflineRewardPending = {
  jobId: string;
  status: "CLAIMABLE";
  stageId: string;
  startedAt: string;
  lastHeartbeatAt: string;
  accrualEndedAt: string | null;
  eligibleSeconds: number;
  offlineSeconds?: number;
  experienceGained: number;
  riceGained: number;
  rewards: { itemId: string; quantity: number }[];
};
export type OfflineRewardClaimed = {
  jobId: string;
  status: "CLAIMED";
  stageId: string;
  eligibleSeconds: number;
  offlineSeconds?: number;
  experienceGained: number;
  riceGained: number;
  rewards: { rewards: { itemId: string; requestedQuantity: number; grantedQuantity: number; discardedQuantity: number; skippedQuantity: number }[]; usedSlots: number; maxSlots: number; isFull: boolean };
  claimedAt: string;
};

export type AdminAuditEvent = {
  auditId: string;
  occurredAt: string;
  operatorId: string | null;
  username: string;
  action: string;
  targetType: string;
  targetId: string | null;
  outcome: "SUCCEEDED" | "FAILED";
  requestId: string;
  remoteAddress: string;
  mutation: boolean;
  reason: string | null;
  beforeSummary: string | null;
  afterSummary: string | null;
  idempotencyKey: string | null;
};
export type AdminChatReport = { reportId: string; reporterAccountId: string; targetType: "MESSAGE" | "BOARD_POST"; targetId: string; reason: string; createdAt: string; targetBody: string | null; targetAccountId: string | null; targetNickname: string | null };
export type AdminMarketOrder = { orderId: string; accountId: string; instrumentId: string; itemId: string; displayName: string; side: "BUY" | "SELL"; initialQuantity: number; remainingQuantity: number; limitUnitPrice: number; status: string; createdAt: string; updatedAt: string; expiresAt: string | null; systemOrder: boolean };
export type AdminMarketOrderPage = { items: AdminMarketOrder[]; nextCursor: string | null };
export type AdminMarketCancelResult = { orderId: string; itemId: string; cancelledQuantity: number; status: string; marketRevision: number };
export type AdminMarketPurchaseResult = { tradeId: string; orderId: string; itemId: string; quantity: number; unitPrice: number; totalPrice: number; fee: number; settlementAmount: number; remainingOrderQuantity: number; orderStatus: string; marketRevision: number };
export type AdminMarketSellResult = { itemId: string; totalQuantity: number; unitPrice: number; orderIds: string[]; marketRevision: number };

export class ApiError extends Error {
  readonly status: number;
  readonly code: string;
  readonly retryable: boolean;
  readonly details?: unknown;

  constructor(status: number, code: string, retryable: boolean, details?: unknown) {
    super(code);
    this.status = status;
    this.code = code;
    this.retryable = retryable;
    this.details = details;
  }
}

export function createApiClient(options: ApiClientOptions = {}) {
  const request = options.fetch ?? globalThis.fetch;
  const base = (options.baseUrl ?? "").replace(/\/$/, "");

  async function json<T>(path: string, init?: RequestInit): Promise<T> {
    const response = await request(`${base}${path}`, {
      credentials: "include",
      headers: { Accept: "application/json", ...init?.headers },
      ...init,
    });
    const body = await response.json().catch(() => ({})) as Envelope<T> & ErrorEnvelope;
    if (!response.ok) throw new ApiError(response.status, body.code ?? `HTTP_${response.status}`, body.retryable ?? false, body.details);
    return body.data;
  }

  function adminMutation(body: unknown): RequestInit {
    return {
      method: "POST",
      headers: { "Content-Type": "application/json", "Idempotency-Key": crypto.randomUUID() },
      body: JSON.stringify(body),
    };
  }

  const admin = {
    session: () => json<AdminSessionState>("/api/admin/v1/auth/session"),
    beginGitlab: () => json<{ authorizationUrl: string }>("/api/admin/v1/auth/gitlab/begin", { method: "POST" }),
    logout: () => json<AdminSessionState>("/api/admin/v1/auth/logout", { method: "POST" }),
    dashboard: () => json<AdminDashboard>("/api/admin/v1/dashboard"),
    catalog: () => json<AdminCatalog>("/api/admin/v1/catalog"),
    searchAccounts: (query?: string) => {
      const normalized = query?.trim();
      return json<AdminAccountSummary[]>(normalized ? `/api/admin/v1/accounts?query=${encodeURIComponent(normalized)}` : "/api/admin/v1/accounts");
    },
    accountDetail: (accountId: string) => json<AdminAccountDetail>(`/api/admin/v1/accounts/${encodeURIComponent(accountId)}`),
    outbox: (status?: string, limit = 50) => {
      const query = new URLSearchParams({ limit: String(Math.max(1, Math.min(100, limit))) });
      if (status) query.set("status", status);
      return json<AdminOutboxEvent[]>(`/api/admin/v1/outbox?${query}`);
    },
    retryOutbox: (eventId: string) => json<void>(`/api/admin/v1/outbox/${encodeURIComponent(eventId)}/retry`, { method: "POST" }),
    kafkaOps: () => json<AdminKafkaOps>("/api/admin/v1/kafka/ops"),
    marketAnomalies: (limit = 50) => json<AdminMarketAnomaly[]>(`/api/admin/v1/market/anomalies?limit=${Math.max(1, Math.min(100, limit))}`),
    economyDaily: (from: string, to: string) => json<EconomyDailyMetric[]>(`/api/admin/v1/metrics/economy/daily?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`),
    audit: (limit = 100, filters: { operatorId?: string; action?: string; mutationsOnly?: boolean } = {}) => {
      const query = new URLSearchParams({ limit: String(Math.max(1, Math.min(200, limit))) });
      if (filters.operatorId) query.set("operatorId", filters.operatorId);
      if (filters.action) query.set("action", filters.action);
      if (filters.mutationsOnly) query.set("mutationsOnly", "true");
      return json<AdminAuditEvent[]>(`/api/admin/v1/audit?${query}`);
    },
    userManagement: (accountId: string) => json<AdminUserState>(`/api/admin/v1/accounts/${encodeURIComponent(accountId)}/management`),
    adjustUserProgression: (accountId: string, level: number | null, experience: number | null, reason: string) => json<AdminUserMutationResult>(`/api/admin/v1/accounts/${encodeURIComponent(accountId)}/management/progression`, adminMutation({ level, experience, reason })),
    adjustUserRice: (accountId: string, mode: "SET" | "ADD", amount: number, reason: string) => json<AdminUserMutationResult>(`/api/admin/v1/accounts/${encodeURIComponent(accountId)}/management/rice`, adminMutation({ mode, amount, reason })),
    adjustUserItem: (accountId: string, itemId: string, mode: "SET" | "ADD", quantity: number, reason: string) => json<AdminUserMutationResult>(`/api/admin/v1/accounts/${encodeURIComponent(accountId)}/management/items`, adminMutation({ itemId, mode, quantity, reason })),
    adjustUserGem: (accountId: string, level: number, option: string, delta: number, reason: string) => json<AdminUserMutationResult>(`/api/admin/v1/accounts/${encodeURIComponent(accountId)}/management/gems`, adminMutation({ level, option, delta, reason })),
    unlockUserStages: (accountId: string, throughStageId: string, reason: string) => json<AdminUserMutationResult>(`/api/admin/v1/accounts/${encodeURIComponent(accountId)}/management/stages`, adminMutation({ throughStageId, reason })),
    adjustUserCosmetic: (accountId: string, cosmeticId: string, registeredQuantity: number, unregisteredQuantity: number, reason: string) => json<AdminUserMutationResult>(`/api/admin/v1/accounts/${encodeURIComponent(accountId)}/management/cosmetics`, adminMutation({ cosmeticId, registeredQuantity, unregisteredQuantity, reason })),
    adjustUserEquipment: (accountId: string, slot: string, grade: string | null, enhancementLevel: number | null, reason: string) => json<AdminUserMutationResult>(`/api/admin/v1/accounts/${encodeURIComponent(accountId)}/management/equipment`, adminMutation({ slot, grade, enhancementLevel, reason })),
    chatReports: (limit = 50) => json<AdminChatReport[]>(`/api/admin/v1/chat/reports?limit=${Math.max(1, Math.min(100, limit))}`),
    marketOrders: (query?: string, cursor?: string) => {
      const params = new URLSearchParams();
      if (query) params.set("query", query);
      if (cursor) params.set("cursor", cursor);
      const suffix = params.size ? `?${params}` : "";
      return json<AdminMarketOrderPage>(`/api/admin/v1/market/orders${suffix}`);
    },
    cancelMarketOrder: (orderId: string, reason: string) => json<AdminMarketCancelResult>(`/api/admin/v1/market/orders/${encodeURIComponent(orderId)}/cancel`, adminMutation({ reason })),
    purchaseMarketOrder: (orderId: string, quantity: number | null, reason: string) => json<AdminMarketPurchaseResult>(`/api/admin/v1/market/orders/${encodeURIComponent(orderId)}/purchase`, adminMutation({ quantity, reason })),
    createSystemMarketOrder: (itemId: string, quantity: number, unitPrice: number, reason: string) => json<AdminMarketSellResult>("/api/admin/v1/market/system-orders", adminMutation({ itemId, quantity, unitPrice, reason })),
    deleteChatMessage: (messageId: string) => json<{ targetId: string; status: string }>(`/api/admin/v1/chat/messages/${messageId}/delete`, { method: "POST" }),
    deleteChatBoard: (postId: string) => json<{ targetId: string; status: string }>(`/api/admin/v1/chat/board/${postId}/delete`, { method: "POST" }),
    banChatAccount: (accountId: string, reason?: string) => json<{ targetId: string; status: string }>(`/api/admin/v1/chat/accounts/${accountId}/ban${reason ? `?reason=${encodeURIComponent(reason)}` : ""}`, { method: "POST" }),
    unbanChatAccount: (accountId: string) => json<{ targetId: string; status: string }>(`/api/admin/v1/chat/accounts/${accountId}/ban`, { method: "DELETE" }),
  };

  return {
    session: () => request(`${base}/api/v1/auth/session`, { credentials: "include" }),
    admin,
  };
}
