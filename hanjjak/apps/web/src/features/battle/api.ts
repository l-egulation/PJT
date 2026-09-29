export type StageSummary = {
  stageId: string;
  unlocked: boolean;
  clearCount: number;
  contentVersion: string;
  isCurrent: boolean;
  isRepeatTarget: boolean;
  repeatEligible: boolean;
  normalMonsterIds: string[];
  bossMonsterId: string | null;
  backgroundId: string | null;
  bossOnly: boolean;
};
export type BattleCycleResult = { success: boolean; failureCode: string | null; remainingHp: number; defeatedNormals: number; elapsedTicks: number };
export type RewardLine = { itemId: string; requestedQuantity: number; grantedQuantity: number; discardedQuantity: number; skippedQuantity?: number };
export type RewardResult = { rewards: RewardLine[]; usedSlots: number; maxSlots: number; isFull: boolean };
export type ProgressionRewardResult = {
  experienceGained: number;
  riceGained: number;
  levelBefore: number;
  levelAfter: number;
  experienceBefore: number;
  experienceAfter: number;
  experienceToNextLevel: number;
  riceBalance: number;
};
export type StageRun = { stageId: string; battle: BattleCycleResult; reward: RewardResult; progression: ProgressionRewardResult; firstClearReward: FirstClearRewardResult | null };
export type ChapterProgressionSummary = Pick<ProgressionRewardResult, "experienceGained" | "riceGained" | "levelBefore" | "levelAfter" | "experienceAfter" | "experienceToNextLevel" | "riceBalance">;
export type ChapterAutoRunResponse = {
  chapter: number;
  runs: StageRun[];
  stoppedReason: "CHAPTER_COMPLETE" | "BATTLE_FAILED" | "STAGE_LOCKED" | "NO_STAGE";
  stoppedStageId: string | null;
  rewards: RewardLine[];
  progression: ChapterProgressionSummary | null;
  finalInventory: Pick<RewardResult, "usedSlots" | "maxSlots" | "isFull"> | null;
};
import type { FirstClearRewardResult } from "../first-clear-rewards/api";
export type BattleCycleResponse = { battle: BattleCycleResult; reward: RewardResult; progression: ProgressionRewardResult; firstClearReward: FirstClearRewardResult | null };
export type BattleModeUpdateResult = {
  idleMode: "AUTO_PROGRESS" | "REPEAT_STAGE";
  repeatStageId: string | null;
  appliesAfterCurrentCycle: boolean;
  stateVersion: number;
};
export type BattleHistoryEventType = "STAGE_ENTERED" | "STAGE_CLEARED" | "STAGE_FAILED" | "DUNGEON_ENTERED" | "RETURNED";
export type BattleHistoryReward = {
  itemId: string;
  displayName: string;
  quantity: number;
};

export type BattleHistoryCombatSnapshot = {
  attack: number;
  maxHp: number;
  penetration: number;
  stageEnteredAt?: string;
  remainingHp?: number;
  defeatedNormals?: number;
  lastEnemyRemainingHp?: number;
  normalCount?: number;
  elapsedTicks?: number;
  experienceGained?: number;
  riceGained?: number;
  rewards?: BattleHistoryReward[];
};
export type BattleHistoryEvent = {
  eventId: string;
  type: BattleHistoryEventType;
  occurredAt: string;
  stageId: string | null;
  dungeonId: string | null;
  resultCode: string;
  messageKey: string;
  contentVersion: string | null;
  combatSnapshot: BattleHistoryCombatSnapshot | null;
};
export type LatestStageFailure = { latestFailure: BattleHistoryEvent | null };

type Envelope<T> = { data: T };
type ErrorEnvelope = { code?: string; messageKey?: string };

async function json<T>(response: Response): Promise<T> {
  const body = await response.json().catch(() => ({})) as Envelope<T> & ErrorEnvelope;
  if (!response.ok) throw Object.assign(new Error(body.code || `HTTP ${response.status}`), body);
  return body.data;
}

function commandHeaders(gameSessionId: string): HeadersInit {
  return {
    "Accept": "application/json",
    "Content-Type": "application/json",
    "Idempotency-Key": crypto.randomUUID(),
    "X-Game-Session-Id": gameSessionId,
  };
}

export const battleApi = {
  stages: () => fetch("/api/v1/stages", { credentials: "include", headers: { Accept: "application/json" } }).then(json<StageSummary[]>),
  updateRepeatStage: (stageId: string | null, gameSessionId: string) => fetch("/api/v1/battle/repeat-stage", {
    method: "PATCH",
    credentials: "include",
    headers: commandHeaders(gameSessionId),
    body: JSON.stringify({ stageId }),
  }).then(json<BattleModeUpdateResult>),
  runCycle: (stageId: string, gameSessionId: string) => fetch("/api/v1/battles/cycles", {
    method: "POST",
    credentials: "include",
    headers: commandHeaders(gameSessionId),
    body: JSON.stringify({ stageId }),
  }).then(json<BattleCycleResponse>),
  runChapter: (chapter: number, gameSessionId: string) => fetch(`/api/v1/battles/chapters/${chapter}/auto-run`, {
    method: "POST",
    credentials: "include",
    headers: commandHeaders(gameSessionId),
  }).then(json<ChapterAutoRunResponse>),
  history: () => fetch("/api/v1/battle-history", { credentials: "include", headers: { Accept: "application/json" } }).then(json<BattleHistoryEvent[]>),
  latestStageFailure: (stageId: string) => fetch(`/api/v1/battle-history/stages/${encodeURIComponent(stageId)}/latest-failure`, {
    credentials: "include",
    headers: { Accept: "application/json" },
  }).then(json<LatestStageFailure>),
};
