import "@typespec/openapi3";
import "@typespec/http";
import "@typespec/json-schema";

using TypeSpec.Http;
using TypeSpec.OpenAPI;
using TypeSpec.JsonSchema;

namespace Hanjjak;

@friendlyName("RaidSessionStatus")
enum RaidSessionStatus {
  OPEN,
  SETTLING,
  SETTLED_SUCCESS,
  SETTLED_FAILURE,
}

@friendlyName("RaidSlotStatus")
enum RaidSlotStatus {
  AVAILABLE,
  ACTIVE,
  RESULT_HELD,
  CONFIRMED,
  DISCARDED,
  EXPIRED,
}

@friendlyName("RaidAttemptStatus")
enum RaidAttemptStatus {
  RUNNING,
  RESULT_HELD,
  DISCARDED,
  CONFIRMED,
}

@friendlyName("RaidAttemptMode")
enum RaidAttemptMode {
  REWARD,
  PRACTICE,
}

@friendlyName("RaidGrade")
enum RaidGrade {
  PARTICIPATION,
  D,
  C,
  B,
  A,
  S,
  SS,
  SSS,
}

@friendlyName("RaidClaimKind")
enum RaidClaimKind {
  AUTO_PERSONAL,
  DAILY_RANK,
}

@friendlyName("RaidClaimStatus")
enum RaidClaimStatus {
  CLAIMABLE,
  CLAIMED,
}

@friendlyName("RaidCombatEventType")
enum RaidCombatEventType {
  PLAYER_ACTION,
  PLAYER_IMPACT,
  BOSS_HIT,
  ESCALATION,
  PLAYER_DEATH,
  TIME_LIMIT_END,
}

@friendlyName("RaidCombatActor")
enum RaidCombatActor {
  PLAYER,
  BOSS,
}

@friendlyName("RaidErrorCode")
enum RaidErrorCode {
  RAID_CONTENT_LOCKED,
  RAID_NO_AVAILABLE_SLOT,
  RAID_ATTEMPT_RUNNING,
  RAID_ATTEMPT_RESULT_HELD,
  RAID_ATTEMPT_LIMIT_REACHED,
  RAID_INVALID_TRANSITION,
  RAID_ZERO_DAMAGE_CANNOT_CONFIRM,
  RAID_REWARD_CAPACITY,
  RAID_SESSION_STALE,
  RAID_SESSION_SETTLED,
  RAID_MAIN_BATTLE_TRANSITION_FAILED,
  RAID_CLAIM_NOT_FOUND,
  RAID_CLAIM_ALREADY_CLAIMED,
  RAID_STATE_VERSION_CONFLICT,
  IDEMPOTENCY_KEY_REUSED,
}

@error model RaidErrorEnvelope {
  requestId: Uuid;
  serverTime: Instant;
  code: RaidErrorCode;
  messageKey: string;
  retryable: boolean;
  details: ErrorDetail[] | null;
}

model RaidRewardBundle {
  @minValue(0) cosmeticTickets: int64;
  @minValue(0) gemBoxes: int64;
  @minValue(0) rice: int64;
}

model RaidSessionView {
  sessionId: Uuid;
  contentVersion: string;
  rewardVersion: string;
  settlesAt: Instant;
  status: RaidSessionStatus;
  @minValue(0) sealTarget: int64;
  @minValue(0) sealedContribution: int64;
  @minValue(0) sealProgressBasisPoints: int64;
  sealSuccessScheduled: boolean;
  serverNow: Instant;
}

model RaidSlotView {
  @minValue(0) ordinal: int64;
  status: RaidSlotStatus;
  @minValue(0) attemptsStarted: int64;
  @minValue(0) attemptsRemaining: int64;
  currentAttemptId: Uuid | null;
  resultHeld: boolean;
}

model RaidBossSnapshot {
  bossId: string;
  displayName: string;
  contentVersion: string;
  @minValue(0) initialAttack: int32;
  @minValue(0) initialDefense: int32;
  @minValue(0) maxTicks: int64;
  @minValue(1) tickDurationMilliseconds: int64;
  @minValue(1) escalationIntervalTicks: int64;
  @minValue(0) escalationBasisPoints: int64;
}

model RaidPlayerSnapshot {
  level: int32;
  @minValue(0) maxHp: int64;
  @minValue(0) attack: int64;
  @minValue(0) defense: int64;
  @minValue(0) defensePenetration: int64;
  skillIds: string[];
  skillLoadout: string[];
  mainGemPreset: string;
  cosmeticEffectIds: string[];
}

model RaidCombatInputSnapshot {
  contentVersion: string;
  rewardVersion: string;
  @minValue(0) seed: int64;
  player: RaidPlayerSnapshot;
  boss: RaidBossSnapshot;
  capturedAt: Instant;
}

model RaidAttemptResultView {
  @minValue(0) damage: int64;
  grade: RaidGrade;
  @minValue(0) sealContribution: int64;
  endedAt: Instant | null;
  playerDied: boolean;
  timeLimitReached: boolean;
  reward: RaidRewardBundle | null;
}

model RaidCombatEvent {
  @minValue(0) sequence: int64;
  @minValue(0) logicalTick: int64;
  type: RaidCombatEventType;
  actor: RaidCombatActor | null;
  target: RaidCombatActor | null;
  @minValue(0) damage: int64 | null;
  critical: boolean | null;
  @minValue(0) hpBefore: int64 | null;
  @minValue(0) hpAfter: int64 | null;
  @minValue(0) escalationStage: int64 | null;
  @minValue(0) bossAttack: int64 | null;
  @minValue(0) bossDefense: int64 | null;
}

model RaidRenderingTimeline {
  @minValue(1) tickDurationMilliseconds: int64;
  @minValue(0) durationMilliseconds: int64;
  events: RaidCombatEvent[];
}

model RaidAttemptView {
  attemptId: Uuid;
  sessionId: Uuid;
  @minValue(0) slotOrdinal: int64;
  mode: RaidAttemptMode;
  status: RaidAttemptStatus;
  inputSnapshot: RaidCombatInputSnapshot;
  @minValue(1) tickDurationMilliseconds: int64;
  @minValue(0) durationMilliseconds: int64;
  startedAt: Instant;
  completableAt: Instant;
  endedAt: Instant | null;
  currentResult: RaidAttemptResultView;
  terminalResult: RaidAttemptResultView | null;
  renderingTimeline: RaidRenderingTimeline;
}

model RaidRankingEntry {
  @minValue(1) rank: int64;
  nickname: string;
  @minValue(0) sealContribution: int64;
}

model RaidRankingView {
  sessionId: Uuid;
  settled: boolean;
  @minValue(1) currentRank: int64 | null;
  @minValue(0) currentSealContribution: int64;
  topEntries: RaidRankingEntry[];
  nextCursor: string | null;
  @minValue(0) totalEligibleAccounts: int64;
}

model RaidClaimView {
  claimId: Uuid;
  sessionId: Uuid;
  kind: RaidClaimKind;
  status: RaidClaimStatus;
  reward: RaidRewardBundle;
  createdAt: Instant;
  claimedAt: Instant | null;
}

model RaidClaimsView {
  items: RaidClaimView[];
  @minValue(0) claimableCount: int64;
  @minValue(0) claimedCount: int64;
  nextCursor: string | null;
}

model RaidStateView {
  featureAvailable: boolean;
  unlockStageId: string;
  unlocked: boolean;
  session: RaidSessionView;
  slots: RaidSlotView[];
  currentAttempt: RaidAttemptView | null;
  ranking: RaidRankingView;
  claims: RaidClaimsView;
}

model RaidAttemptStartRequest {
  mode: RaidAttemptMode;
  ...Record<never>;
}

model RaidEmptyRequest {
  ...Record<never>;
}

model RaidClaimResult {
  claim: RaidClaimView;
  state: RaidClaimsView;
}

@route("/api/v1/raid")
interface Raid {
  @get state(): SuccessEnvelope<RaidStateView> | RaidErrorEnvelope;

  @get @route("/ranking") ranking(
    @query cursor?: string,
    @query @minValue(1) @maxValue(100) limit?: int32,
    @query sessionId?: Uuid
  ): SuccessEnvelope<RaidRankingView> | RaidErrorEnvelope;

  @get @route("/claims") claims(@query cursor?: string): SuccessEnvelope<RaidClaimsView> | RaidErrorEnvelope;

  @get @route("/attempts/current") currentAttempt(): SuccessEnvelope<RaidAttemptView | null> | RaidErrorEnvelope;

  @post @route("/attempts") startAttempt(
    @header("Idempotency-Key") idempotencyKey: Uuid,
    @body request: RaidAttemptStartRequest
  ): SuccessEnvelope<CommandResult<RaidAttemptView>> | RaidErrorEnvelope;

  @post @route("/attempts/{attemptId}/retry") retryAttempt(
    @path attemptId: Uuid,
    @header("Idempotency-Key") idempotencyKey: Uuid,
    @body request: RaidEmptyRequest
  ): SuccessEnvelope<CommandResult<RaidAttemptView>> | RaidErrorEnvelope;

  @post @route("/attempts/{attemptId}/confirm") confirmAttempt(
    @path attemptId: Uuid,
    @header("Idempotency-Key") idempotencyKey: Uuid,
    @body request: RaidEmptyRequest
  ): SuccessEnvelope<CommandResult<RaidStateView>> | RaidErrorEnvelope;

  @post @route("/attempts/{attemptId}/discard") discardAttempt(
    @path attemptId: Uuid,
    @header("Idempotency-Key") idempotencyKey: Uuid,
    @body request: RaidEmptyRequest
  ): SuccessEnvelope<CommandResult<RaidStateView>> | RaidErrorEnvelope;

  @post @route("/claims/{claimId}") claimReward(
    @path claimId: Uuid,
    @header("Idempotency-Key") idempotencyKey: Uuid,
    @body request: RaidEmptyRequest
  ): SuccessEnvelope<CommandResult<RaidClaimResult>> | RaidErrorEnvelope;
}
