using TypeSpec.Http;
namespace Hanjjak;

model FirstClearRewardItem {
  itemId: string;
  displayName: string;
  quantity: int64;
}

model FirstClearRewardResult {
  rewardId: Uuid;
  stageId: string;
  rewardVersion: string;
  firstClear: boolean;
  riceGranted: int64;
  grantedItems: FirstClearRewardItem[];
  pendingItems: FirstClearRewardItem[];
  unlockedSkillId: string | null;
  itemStatus: string;
  requiredSlots: int32;
  availableSlots: int32;
  missingSlots: int32;
}

model PendingFirstClearRewardPage {
  rewards: FirstClearRewardResult[];
  count: int32;
}

@route("/api/v1/first-clear-rewards")
interface FirstClearRewards {
  @get list(): SuccessEnvelope<PendingFirstClearRewardPage> | ErrorEnvelope;
  @post @route("/{rewardId}/claim") claim(
    @path rewardId: Uuid,
    @header("Idempotency-Key") idempotencyKey: Uuid,
  ): SuccessEnvelope<FirstClearRewardResult> | ErrorEnvelope;
}
