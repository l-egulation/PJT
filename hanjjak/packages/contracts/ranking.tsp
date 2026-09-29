using TypeSpec.Http;
namespace Hanjjak;

model RankingAppearance {
  head: string | null;
  top: string | null;
  bottom: string | null;
  gloves: string | null;
  shoes: string | null;
  cape: string | null;
}

model CombatPowerRankingEntry {
  rank: int32;
  overallRank: int32;
  nickname: string;
  level: int32;
  materialType: MaterialType;
  displayName: string;
  combatPower: int64;
  appearance: RankingAppearance;
  updatedAt: Instant;
}

model MaterialRanking {
  materialType: MaterialType;
  displayName: string;
  entries: CombatPowerRankingEntry[];
}

model CombatPowerRankingView {
  formulaVersion: string;
  generatedAt: Instant;
  sourceStateVersion: int64;
  overallTop: CombatPowerRankingEntry[];
  specializations: MaterialRanking[];
  myEntry: CombatPowerRankingEntry | null;
}

@route("/api/v1/rankings")
interface Rankings {
  @get @route("/combat-power") combatPower(@query limit?: int32): SuccessEnvelope<CombatPowerRankingView> | ErrorEnvelope;
}
