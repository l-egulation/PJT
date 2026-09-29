using TypeSpec.Http;
namespace Hanjjak;

model OfflineRewardItem { itemId: string; quantity: int64; }
model OfflineRewardPending {
  jobId: Uuid;
  status: "CLAIMABLE";
  stageId: string;
  startedAt: Instant;
  lastHeartbeatAt: Instant;
  accrualEndedAt: Instant | null;
  eligibleSeconds: int64;
  offlineSeconds?: int64;
  experienceGained: int64;
  riceGained: int64;
  rewards: OfflineRewardItem[];
}
model OfflineRewardClaimed {
  jobId: Uuid;
  status: "CLAIMED";
  stageId: string;
  eligibleSeconds: int64;
  offlineSeconds?: int64;
  experienceGained: int64;
  riceGained: int64;
  rewards: RewardResult;
  claimedAt: Instant;
}

@route("/api/v1/offline-rewards")
interface OfflineRewards {
  @get @route("/pending") pending(): SuccessEnvelope<OfflineRewardPending | null> | ErrorEnvelope;
  @post @route("/claim") claim(@header("Idempotency-Key") idempotencyKey: Uuid): SuccessEnvelope<OfflineRewardClaimed> | ErrorEnvelope;
}
