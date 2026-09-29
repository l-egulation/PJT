using TypeSpec.Http;
namespace Hanjjak;
model CharacterStatSource {
  sourceId: string; label: string; category: string; value: float64; unit: string;
  applied: boolean; reason: string | null;
}
model CharacterStatRow {
  statId: string; label: string; unit: string; total: int32; base: int32; additional: int32;
  sources: CharacterStatSource[]; calculation: string;
}
model CharacterStatsView {
  nickname: string; level: int32; experience: int64; combatPower: int64; cosmeticsUnlocked: boolean; contentVersion: string;
  stats: CharacterStatRow[]; notices: string[];
}
@route("/api/v1/character") interface CharacterSheet {
  @get @route("/stats") stats(): SuccessEnvelope<CharacterStatsView> | ErrorEnvelope;
}
