import "@typespec/json-schema";

using TypeSpec.JsonSchema;

namespace Hanjjak.RaidContentSchema;

enum ContentAuthority { working, applied }

@extension("additionalProperties", false)
model RaidRewardBundleContent {
  @minValue(0) cosmeticTickets: int64;
  @minValue(0) gemBoxes: int64;
  @minValue(0) rice: int64;
}

@extension("additionalProperties", false)
model RaidGradeThresholds {
  @minValue(1) D: int64;
  @minValue(1) C: int64;
  @minValue(1) B: int64;
  @minValue(1) A: int64;
  @minValue(1) S: int64;
  @minValue(1) SS: int64;
  @minValue(1) SSS: int64;
}

@extension("additionalProperties", false)
model RaidSealContributions {
  @minValue(0) PARTICIPATION: int32;
  @minValue(0) D: int32;
  @minValue(0) C: int32;
  @minValue(0) B: int32;
  @minValue(0) A: int32;
  @minValue(0) S: int32;
  @minValue(0) SS: int32;
  @minValue(0) SSS: int32;
}

@extension("additionalProperties", false)
model RaidPersonalRewards {
  PARTICIPATION: RaidRewardBundleContent;
  D: RaidRewardBundleContent;
  C: RaidRewardBundleContent;
  B: RaidRewardBundleContent;
  A: RaidRewardBundleContent;
  S: RaidRewardBundleContent;
  SS: RaidRewardBundleContent;
  SSS: RaidRewardBundleContent;
}

@extension("additionalProperties", false)
model RaidRankRewards {
  FIRST: RaidRewardBundleContent;
  SECOND_TO_TENTH: RaidRewardBundleContent;
  ELEVENTH_TO_HUNDREDTH: RaidRewardBundleContent;
  REST: RaidRewardBundleContent;
}
@jsonSchema
@id("raids.schema.json")
@extension("additionalProperties", false)
model RaidContent {
  authority: ContentAuthority;
  @minLength(1) contentVersion: string;
  @minLength(1) unlockStageId: string;
  settlementTimeZone: "Asia/Seoul";
  settlementLocalTime: "17:30";
  @minValue(1) sealTarget: int64;
  @minValue(1) attemptLimitTicks: int32;
  @minValue(1) escalationIntervalTicks: int32;
  @minValue(1) escalationBasisPoints: int32;
  @minValue(1) bossInitialAttack: int32;
  @minValue(1) bossInitialDefense: int32;
  gradeDamageThresholds: RaidGradeThresholds;
  sealContributions: RaidSealContributions;
  personalRewards: RaidPersonalRewards;
  successfulRankRewards: RaidRankRewards;
  failedRankRewards: RaidRankRewards;
}
