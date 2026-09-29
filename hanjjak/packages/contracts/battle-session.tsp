using TypeSpec.Http;
namespace Hanjjak;

union BattleSessionStatus {
  ACTIVE: "ACTIVE",
  COMPLETED: "COMPLETED",
  ABORTED: "ABORTED",
  EXPIRED: "EXPIRED"
}

model BattleFighterSnapshot {
  attack: int32;
  maxHp: int32;
  penetration: int32;
}

model BattleEnemySnapshot {
  hp: int32;
  attack: int32;
  defense: int32;
}

model BattleSkillSnapshot {
  heavyBasisPoints: int32;
  dotTotalBasisPoints: int32;
  hasteBasisPoints: int32;
  basicAmplificationBasisPoints: int32;
  criticalChanceBasisPoints: int32;
  allDamageBasisPoints: int32;
  permanentHasteBasisPoints: int32;
  permanentBasicAmplificationBasisPoints: int32;
  buffDurationBonusTicks: int32;
  activeOrder: string[];
}

model BattleInputSnapshot {
  contentVersion: string;
  seed: int64;
  player: BattleFighterSnapshot;
  normal: BattleEnemySnapshot;
  boss: BattleEnemySnapshot;
  skills: BattleSkillSnapshot;
  normalCount: int32;
  bossTimeLimitTicks: int32 | null;
  scheduledStrikes: Record<int32>;
}

model BattleSessionStartRequest { stageId: string; }
model CombatRenderingEvent {
  eventId: string;
  logicalTick: int32;
  type: string;
  actor: string | null;
  target: string | null;
  enemyIndex: int32 | null;
  boss: boolean;
  skillId: string | null;
  damage: int32 | null;
  critical: boolean;
  hpBefore: int32 | null;
  hpAfter: int32 | null;
  defeated: boolean;
}
model BattleSessionStartResponse {
  battleSessionId: Uuid;
  durationMilliseconds: int64;
  battleToken: Uuid;
  status: BattleSessionStatus;
  stageId: string;
  idleMode: "AUTO_PROGRESS" | "REPEAT_STAGE";
  repeatStageId: string | null;
  logicalTickBasis: int32;
  tickDurationMilliseconds: int32;
  completableAt: Instant;
  input: BattleInputSnapshot;
  renderingTimeline: CombatRenderingEvent[];
}

model BattleRenderingCheckpoint {
  logicalTick: int32;
  defeatedNormals: int32;
  playerHp: int32;
}

model BattleSessionCompleteRequest {
  predictedHash: string | null;
  renderingCheckpoint: BattleRenderingCheckpoint | null;
}
model BattleEnemySettlementRequest {
  throughEnemyIndex: int32;
  renderingCheckpoint: BattleRenderingCheckpoint;
}

model BattleEnemySettlement {
  settlementId: Uuid;
  battleSessionId: Uuid;
  enemyIndex: int32;
  boss: boolean;
  settledAt: Instant;
  reward: RewardResult;
  progression: ProgressionRewardResult;
}

model BattleEnemySettlementResult {
  battleSessionId: Uuid;
  throughEnemyIndex: int32;
  settlements: BattleEnemySettlement[];
}

model BattleSessionAbortRequest {
  renderingCheckpoint: BattleRenderingCheckpoint | null;
}

model BattleSessionResult {
  battleSessionId: Uuid;
  status: BattleSessionStatus;
  stageId: string;
  idleMode: "AUTO_PROGRESS" | "REPEAT_STAGE";
  repeatStageId: string | null;
  nextStageId: string;
  battle: BattleCycleResult;
  reward: RewardResult;
  progression: ProgressionRewardResult;
  firstClearReward: FirstClearRewardResult | null;
  settlements: BattleEnemySettlement[];
  predictionMatched: boolean | null;
}

model BattleSessionState {
  battleSessionId: Uuid;
  status: BattleSessionStatus;
  lastHeartbeatAt: Instant;
}

@route("/api/v1/battles/sessions")
interface BattleSessions {
  @post start(
    @header("Idempotency-Key") idempotencyKey: Uuid,
    @header("X-Game-Session-Id") gameSessionId: Uuid,
    @body request: BattleSessionStartRequest
  ): SuccessEnvelope<BattleSessionStartResponse> | ErrorEnvelope;

  @post @route("/{battleSessionId}/heartbeat") heartbeat(
    @path battleSessionId: Uuid,
    @header("Idempotency-Key") idempotencyKey: Uuid,
    @header("X-Game-Session-Id") gameSessionId: Uuid,
    @header("X-Battle-Token") battleToken: Uuid
  ): SuccessEnvelope<BattleSessionState> | ErrorEnvelope;

  @post @route("/{battleSessionId}/settlements") settle(
    @path battleSessionId: Uuid,
    @header("Idempotency-Key") idempotencyKey: Uuid,
    @header("X-Game-Session-Id") gameSessionId: Uuid,
    @header("X-Battle-Token") battleToken: Uuid,
    @body request: BattleEnemySettlementRequest
  ): SuccessEnvelope<BattleEnemySettlementResult> | ErrorEnvelope;

  @post @route("/{battleSessionId}/complete") complete(
    @path battleSessionId: Uuid,
    @header("Idempotency-Key") idempotencyKey: Uuid,
    @header("X-Game-Session-Id") gameSessionId: Uuid,
    @header("X-Battle-Token") battleToken: Uuid,
    @body request: BattleSessionCompleteRequest
  ): SuccessEnvelope<BattleSessionResult> | ErrorEnvelope;

  @post @route("/{battleSessionId}/abort") abort(
    @path battleSessionId: Uuid,
    @header("Idempotency-Key") idempotencyKey: Uuid,
    @header("X-Game-Session-Id") gameSessionId: Uuid,
    @header("X-Battle-Token") battleToken: Uuid,
    @body request: BattleSessionAbortRequest
  ): SuccessEnvelope<BattleSessionState> | ErrorEnvelope;
}

union BattleHistoryEventType {
  STAGE_ENTERED: "STAGE_ENTERED",
  STAGE_CLEARED: "STAGE_CLEARED",
  STAGE_FAILED: "STAGE_FAILED",
  DUNGEON_ENTERED: "DUNGEON_ENTERED",
  RETURNED: "RETURNED"
}

model BattleHistoryReward {
  itemId: string;
  displayName: string;
  quantity: int64;
}

model BattleHistoryCombatSnapshot {
  attack: int32;
  maxHp: int32;
  penetration: int32;
  stageEnteredAt?: Instant;
  remainingHp?: int32;
  defeatedNormals?: int32;
  lastEnemyRemainingHp?: int32;
  normalCount?: int32;
  elapsedTicks?: int32;
  experienceGained?: int64;
  riceGained?: int64;
  rewards?: BattleHistoryReward[];
}

model BattleHistoryEvent {
  eventId: Uuid;
  type: BattleHistoryEventType;
  occurredAt: Instant;
  stageId: string | null;
  dungeonId: string | null;
  resultCode: string;
  messageKey: string;
  contentVersion: string | null;
  combatSnapshot: BattleHistoryCombatSnapshot | null;
}

model LatestStageFailure {
  latestFailure: BattleHistoryEvent | null;
}

@route("/api/v1/battle-history")
interface BattleHistory {
  @get recent(): SuccessEnvelope<BattleHistoryEvent[]> | ErrorEnvelope;

  @get @route("/stages/{stageId}/latest-failure") latestStageFailure(
    @path stageId: string
  ): SuccessEnvelope<LatestStageFailure> | ErrorEnvelope;
}
