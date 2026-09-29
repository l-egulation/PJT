import type { BattleCycleResult, ProgressionRewardResult, RewardResult } from "./api";

import type { FirstClearRewardResult } from "../first-clear-rewards/api";
export type BattleFighterSnapshot = { attack: number; maxHp: number; penetration: number };
export type BattleEnemySnapshot = { hp: number; attack: number; defense: number };
export type BattleSkillSnapshot = {
  heavyBasisPoints: number;
  dotTotalBasisPoints: number;
  hasteBasisPoints: number;
  basicAmplificationBasisPoints: number;
  criticalChanceBasisPoints: number;
  allDamageBasisPoints: number;
  permanentHasteBasisPoints: number;
  permanentBasicAmplificationBasisPoints: number;
  buffDurationBonusTicks: number;
  activeOrder: string[];
};
export type BattleInputSnapshot = {
  contentVersion: string;
  seed: number;
  player: BattleFighterSnapshot;
  normal: BattleEnemySnapshot;
  boss: BattleEnemySnapshot;
  skills: BattleSkillSnapshot;
  normalCount: number;
  bossTimeLimitTicks: number | null;
  scheduledStrikes: Record<string, number>;
};
export type CombatRenderingEvent = {
  eventId: string;
  logicalTick: number;
  type: string;
  actor: string | null;
  target: string | null;
  enemyIndex: number | null;
  boss: boolean;
  skillId: string | null;
  damage: number | null;
  critical: boolean;
  hpBefore: number | null;
  hpAfter: number | null;
  defeated: boolean;
};
export type BattleSessionStarted = {
  battleSessionId: string;
  battleToken: string;
  status: "ACTIVE";
  stageId: string;
  idleMode: "AUTO_PROGRESS" | "REPEAT_STAGE";
  repeatStageId: string | null;
  logicalTickBasis: number;
  tickDurationMilliseconds: number;
  durationMilliseconds: number;
  completableAt: string;
  input: BattleInputSnapshot;
  renderingTimeline?: CombatRenderingEvent[];
};
export type BattleRenderingCheckpoint = { logicalTick: number; defeatedNormals: number; playerHp: number };
export type BattleEnemySettlement = {
  settlementId: string;
  battleSessionId: string;
  enemyIndex: number;
  boss: boolean;
  settledAt: string;
  reward: RewardResult;
  progression: ProgressionRewardResult;
};
export type BattleEnemySettlementResult = {
  battleSessionId: string;
  throughEnemyIndex: number;
  settlements: BattleEnemySettlement[];
};
export type BattleSessionResult = {
  battleSessionId: string;
  status: "COMPLETED";
  stageId: string;
  nextStageId: string;
  idleMode: "AUTO_PROGRESS" | "REPEAT_STAGE";
  repeatStageId: string | null;
  battle: BattleCycleResult;
  reward: RewardResult;
  progression: ProgressionRewardResult;
  firstClearReward: FirstClearRewardResult | null;
  settlements: BattleEnemySettlement[];
  predictionMatched: boolean | null;
};
export type BattleSessionState = {
  battleSessionId: string;
  status: "ACTIVE" | "ABORTED" | "EXPIRED";
  lastHeartbeatAt: string;
};

type Envelope<T> = { data: T };
type ErrorEnvelope = { code?: string };

function commandHeaders(gameSessionId: string, battleToken?: string, idempotencyKey: `${string}-${string}-${string}-${string}-${string}` = crypto.randomUUID()): HeadersInit {
  const headers: Record<string, string> = {
    "Accept": "application/json",
    "Content-Type": "application/json",
    "Idempotency-Key": idempotencyKey,
    "X-Game-Session-Id": gameSessionId,
  };
  if (battleToken) headers["X-Battle-Token"] = battleToken;
  return headers;
}

async function json<T>(response: Response): Promise<T> {
  const body = await response.json().catch(() => ({})) as Envelope<T> & ErrorEnvelope;
  if (!response.ok) throw new Error(body.code || `HTTP ${response.status}`);
  return body.data;
}

export const battleSessionApi = {
  start: (stageId: string, gameSessionId: string) => fetch("/api/v1/battles/sessions", {
    method: "POST",
    credentials: "include",
    headers: commandHeaders(gameSessionId),
    body: JSON.stringify({ stageId }),
  }).then(json<BattleSessionStarted>),
  heartbeat: (session: BattleSessionStarted, gameSessionId: string) => fetch(`/api/v1/battles/sessions/${session.battleSessionId}/heartbeat`, {
    method: "POST",
    credentials: "include",
    headers: commandHeaders(gameSessionId, session.battleToken),
    body: "{}",
  }).then(json<BattleSessionState>),
  settle: (session: BattleSessionStarted, gameSessionId: string, throughEnemyIndex: number, renderingCheckpoint: BattleRenderingCheckpoint, idempotencyKey: `${string}-${string}-${string}-${string}-${string}`) => fetch(`/api/v1/battles/sessions/${session.battleSessionId}/settlements`, {
    method: "POST",
    credentials: "include",
    headers: commandHeaders(gameSessionId, session.battleToken, idempotencyKey),
    body: JSON.stringify({ throughEnemyIndex, renderingCheckpoint }),
  }).then(json<BattleEnemySettlementResult>),
  complete: (session: BattleSessionStarted, gameSessionId: string, predictedHash: string | null = null, renderingCheckpoint: BattleRenderingCheckpoint | null = null) => fetch(`/api/v1/battles/sessions/${session.battleSessionId}/complete`, {
    method: "POST",
    credentials: "include",
    headers: commandHeaders(gameSessionId, session.battleToken),
    body: JSON.stringify({ predictedHash, renderingCheckpoint }),
  }).then(json<BattleSessionResult>),
  abort: (session: BattleSessionStarted, gameSessionId: string, renderingCheckpoint: BattleRenderingCheckpoint | null = null, keepalive = false) => fetch(`/api/v1/battles/sessions/${session.battleSessionId}/abort`, {
    method: "POST",
    credentials: "include",
    headers: commandHeaders(gameSessionId, session.battleToken),
    body: JSON.stringify({ renderingCheckpoint }),
    keepalive,
  }).then(json<BattleSessionState>),
};
