using TypeSpec.Http;
namespace Hanjjak;

model SkillBookRequirement {
  itemId: string;
  displayName: string;
  requiredQuantity: int64;
  availableQuantity: int64;
}
enum SkillActionKind { UNLOCK, ENHANCE, PROMOTE, LOCKED, COMPLETE }
model SkillActionSummary {
  kind: SkillActionKind;
  targetGrade: string | null;
  targetLevel: int32 | null;
  books: SkillBookRequirement[];
  riceCost: int64;
  successBasisPoints: int32;
  executable: boolean;
  disabledReason: string | null;
}
model SkillSummary {
  skillId: string;
  name: string;
  active: boolean;
  unlocked: boolean;
  grade: string | null;
  gradeName: string | null;
  level: int32;
  equippedSlot: int32 | null;
  effectText: string;
  action: SkillActionSummary;
}

model SkillScreenState { skills: SkillSummary[]; activeLoadout: string[]; riceBalance: int64; }
model SkillLoadoutUpdateRequest { skillIds: string[]; }
model SkillEnhanceResult { skill: SkillSummary; success: boolean; state: SkillScreenState; }

@route("/api/v1/skills")
interface Skills {
  @get state(): SuccessEnvelope<SkillScreenState> | ErrorEnvelope;
  @post @route("/{skillId}/enhance") enhance(@path skillId: string, @header("Idempotency-Key") idempotencyKey: Uuid, @body request: EmptyRequest): SuccessEnvelope<SkillEnhanceResult> | ErrorEnvelope;
  @post @route("/{skillId}/promote") promote(@path skillId: string, @header("Idempotency-Key") idempotencyKey: Uuid, @body request: EmptyRequest): SuccessEnvelope<SkillEnhanceResult> | ErrorEnvelope;
  @put @route("/loadout") updateLoadout(@header("Idempotency-Key") idempotencyKey: Uuid, @body request: SkillLoadoutUpdateRequest): SuccessEnvelope<SkillScreenState> | ErrorEnvelope;
}
