export type FirstClearRewardItem = {
  itemId: string;
  displayName: string;
  quantity: number;
};

export type FirstClearRewardResult = {
  rewardId: string;
  stageId: string;
  rewardVersion: string;
  firstClear: boolean;
  riceGranted: number;
  grantedItems: FirstClearRewardItem[];
  pendingItems: FirstClearRewardItem[];
  unlockedSkillId: string | null;
  itemStatus: string;
  requiredSlots: number;
  availableSlots: number;
  missingSlots: number;
};

export type PendingFirstClearRewardPage = {
  rewards: FirstClearRewardResult[];
  count: number;
};

type ErrorDetail = { field?: string; code: string; messageKey: string; value?: unknown };
type Envelope<T> = { data: T; code?: string; details?: ErrorDetail[] };

export class FirstClearRewardApiError extends Error {
  constructor(public status: number, public code: string, public details: ErrorDetail[]) { super(code); }
  number(field: string): number | null {
    const value = this.details.find((detail) => detail.field === field)?.value;
    return typeof value === "number" && Number.isFinite(value) ? value : null;
  }
}

export const isUncertainFirstClearRewardError = (error: unknown) =>
  !(error instanceof FirstClearRewardApiError) || error.status >= 500;

async function json<T>(response: Response): Promise<T> {
  const body = await response.json().catch(() => ({})) as Envelope<T>;
  if (!response.ok) throw new FirstClearRewardApiError(response.status, body.code ?? `HTTP_${response.status}`, body.details ?? []);
  return body.data;
}

export const firstClearRewardsApi = {
  list: () => fetch("/api/v1/first-clear-rewards", {
    credentials: "include",
    headers: { Accept: "application/json" },
  }).then(json<PendingFirstClearRewardPage>),
  claim: (rewardId: string, idempotencyKey: `${string}-${string}-${string}-${string}-${string}` = crypto.randomUUID()) => fetch(`/api/v1/first-clear-rewards/${encodeURIComponent(rewardId)}/claim`, {
    method: "POST",
    credentials: "include",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json",
      "Idempotency-Key": idempotencyKey,
    },
    body: "{}",
  }).then(json<FirstClearRewardResult>),
};
