using TypeSpec.Http;
namespace Hanjjak;

union GemPreset {
  MAIN: "MAIN",
  SURVIVAL: "SURVIVAL",
  BERSERK: "BERSERK",
  ARMORED: "ARMORED"
}

model GemSummary {
  gemId: Uuid;
  level: int32;
  option: string;
  optionName: string;
  value: int32;
  locked: boolean;
  reservedForSale: boolean;
  equippedPresets: GemPreset[];
}

model GemState {
  unlocked: boolean;
  tickets: int32;
  secondsUntilNextTicket: int64;
  todayBoss: GemPreset;
  gemBoxQuantity: int64;
  gems: GemSummary[];
  presets: Record<GemSummary[]>;
  lockedPresets: GemPreset[];
  contentVersion: string;
}

model GemOpenRequest { quantity: int32; }
model GemSlotLockRequest { locked: boolean; }
model GemPresetRequest { gemIds: Uuid[]; }
model GemPresetUpdateResult { preset: GemPreset; mainBattleRestarted: boolean; state: GemState; }
union GemFusionMode { MANUAL: "MANUAL", SAFE_BATCH: "SAFE_BATCH" }
model GemFusionSelection { option: string; quantity: int32; }
model GemFusionPreviewRequest { mode: GemFusionMode; level?: int32; gemIds?: Uuid[]; selections?: GemFusionSelection[]; }
model GemFusionExecuteRequest { mode: GemFusionMode; gemIds?: Uuid[]; targetLevel?: int32; allowedOptions?: string[]; }
model GemFusionConsumption { option: string; optionName: string; quantity: int32; }
model GemFusionPreview { consumedGemIds: Uuid[]; consumption: GemFusionConsumption[]; fusionCount: int32; inputLevel: int32; resultLevel: int32; }
model GemOpenResult { granted: GemSummary[]; state: GemState; }
model GemFusionResult { consumedGemIds: Uuid[]; granted: GemSummary[]; state: GemState; }

union GemDungeonChallengeStatus {
  ACTIVE: "ACTIVE",
  SUCCEEDED: "SUCCEEDED",
  FAILED: "FAILED",
  ABORTED: "ABORTED",
  EXPIRED: "EXPIRED"
}
union GemDungeonEventType {
  BATTLE_START: "BATTLE_START",
  SKILL_CAST: "SKILL_CAST",
  PLAYER_HIT: "PLAYER_HIT",
  DOT_HIT: "DOT_HIT",
  BOSS_HIT: "BOSS_HIT",
  SURVIVAL_STRIKE: "SURVIVAL_STRIKE",
  VICTORY: "VICTORY",
  DEFEAT: "DEFEAT"
}
model GemDungeonCombatEvent {
  sequence: int32;
  tick: int32;
  type: GemDungeonEventType;
  skillId: string | null;
  amount: int32;
  playerHp: int32;
  bossHp: int32;
  critical: boolean;
}
model GemDungeonCombatResult {
  success: boolean;
  failureCode: string | null;
  elapsedTicks: int32;
  remainingPlayerHp: int32;
  remainingBossHp: int32;
  events: GemDungeonCombatEvent[];
}
model GemDungeonChallenge {
  challengeId: Uuid;
  boss: GemPreset;
  stage: int32;
  status: GemDungeonChallengeStatus;
  minimumCompleteAt: Instant;
  expiresAt: Instant;
  rewardGemBoxes: int64;
  contentVersion: string;
  battle: GemDungeonCombatResult;
}
model GemDungeonProgress { boss: GemPreset; highestClearedStage: int32; }
model GemDungeonToday {
  boss: GemPreset;
  tickets: int32;
  secondsUntilNextTicket: int64;
  progress: GemDungeonProgress[];
  nextChallengeStage: int32 | null;
  sweepStage: int32 | null;
  activeChallenge: GemDungeonChallenge | null;
  testBossSelectionEnabled: boolean;
}
model GemDungeonCompletion { challenge: GemDungeonChallenge; state: GemState; }
model GemDungeonStartRequest { boss?: GemPreset; }
model GemDungeonSweepRequest { count: int32; }
model GemDungeonSweepResult { boss: GemPreset; stage: int32; count: int32; rewardGemBoxes: int64; state: GemState; }

@route("/api/v1/gems")
interface Gems {
  @put @route("/slots/{slotId}/lock") lockSlot(@path slotId: Uuid, @header("Idempotency-Key") idempotencyKey: Uuid, @body request: GemSlotLockRequest): SuccessEnvelope<GemState> | ErrorEnvelope;
  @get state(): SuccessEnvelope<GemState> | ErrorEnvelope;
  @post @route("/boxes/open") openBoxes(@header("Idempotency-Key") idempotencyKey: Uuid, @body request: GemOpenRequest): SuccessEnvelope<GemOpenResult> | ErrorEnvelope;
  @put @route("/presets/{preset}") updatePreset(@path preset: GemPreset, @header("Idempotency-Key") idempotencyKey: Uuid, @body request: GemPresetRequest): SuccessEnvelope<GemPresetUpdateResult> | ErrorEnvelope;
}

@route("/api/v1/gem-fusions")
interface GemFusions {
  @post @route("/preview") previewFusion(@body request: GemFusionPreviewRequest): SuccessEnvelope<GemFusionPreview> | ErrorEnvelope;
  @post fuse(@header("Idempotency-Key") idempotencyKey: Uuid, @body request: GemFusionExecuteRequest): SuccessEnvelope<GemFusionResult> | ErrorEnvelope;
}

@route("/api/v1/gem-dungeons")
interface GemDungeons {
  @get @route("/today") today(@query boss?: GemPreset): SuccessEnvelope<GemDungeonToday> | ErrorEnvelope;
  @post @route("/challenges") start(@header("Idempotency-Key") idempotencyKey: Uuid, @body request: GemDungeonStartRequest): SuccessEnvelope<GemDungeonChallenge> | ErrorEnvelope;
  @post @route("/challenges/{challengeId}/complete") complete(@path challengeId: Uuid, @header("Idempotency-Key") idempotencyKey: Uuid, @body request: EmptyRequest): SuccessEnvelope<GemDungeonCompletion> | ErrorEnvelope;
  @post @route("/challenges/{challengeId}/abort") abort(@path challengeId: Uuid, @header("Idempotency-Key") idempotencyKey: Uuid, @body request: EmptyRequest): SuccessEnvelope<GemDungeonCompletion> | ErrorEnvelope;
  @post @route("/sweeps") sweep(@header("Idempotency-Key") idempotencyKey: Uuid, @body request: GemDungeonSweepRequest): SuccessEnvelope<GemDungeonSweepResult> | ErrorEnvelope;
}
