import { readFile } from "node:fs/promises";
import { resolve } from "node:path";

const source = await readFile(resolve(import.meta.dirname, "../generated/openapi/openapi.yaml"), "utf8");

function assert(condition, message) {
  if (!condition) throw new Error(`raid contract assertion failed: ${message}`);
}

function block(name) {
  const match = source.match(new RegExp(`^    ${name}:\\n([\\s\\S]*?)(?=^    [A-Za-z][A-Za-z0-9]*:|^  /|(?![\\s\\S]))`, "m"));
  assert(match, `missing schema ${name}`);
  return match[0];
}

function pathBlock(path) {
  const escapedPath = path.replace(/[{}]/g, "\\$&");
  const match = source.match(new RegExp(`^  ${escapedPath}:\\n([\\s\\S]*?)(?=^  /|(?![\\s\\S]))`, "m"));
  assert(match, `missing path ${path}`);
  return match[0];
}

const expectedPaths = [
  "/api/v1/raid",
  "/api/v1/raid/ranking",
  "/api/v1/raid/claims",
  "/api/v1/raid/attempts/current",
  "/api/v1/raid/attempts",
  "/api/v1/raid/attempts/{attemptId}/retry",
  "/api/v1/raid/attempts/{attemptId}/confirm",
  "/api/v1/raid/attempts/{attemptId}/discard",
  "/api/v1/raid/claims/{claimId}",
];
for (const path of expectedPaths) {
  const operation = pathBlock(path);
  const method = path.includes("/attempts") && !path.endsWith("/current") || path.endsWith("/{claimId}") ? "post" : "get";
  assert(operation.includes(`\n    ${method}:`), `${path} must define ${method}`);
  if (method === "post") {
    assert(operation.includes("Idempotency-Key") && operation.includes("required: true"), `${path} must require Idempotency-Key`);
  }
}

for (const path of [
  "/api/v1/raid/attempts/{attemptId}/retry",
  "/api/v1/raid/attempts/{attemptId}/confirm",
  "/api/v1/raid/attempts/{attemptId}/discard",
]) {
  const parameter = pathBlock(path).match(/- name: attemptId[\s\S]*?(?=\n        - name:|\n      requestBody:|\n      responses:)/)?.[0] ?? "";
  assert(parameter.includes("in: path") && parameter.includes("required: true"), `${path} attemptId must be a required path parameter`);
  assert(parameter.includes("$ref: '#/components/schemas/Uuid'"), `${path} attemptId must use Uuid`);
}
const claimParameter = pathBlock("/api/v1/raid/claims/{claimId}").match(/- name: claimId[\s\S]*?(?=\n        - name:|\n      requestBody:|\n      responses:)/)?.[0] ?? "";
assert(claimParameter.includes("in: path") && claimParameter.includes("required: true"), "claimId must be a required path parameter");
assert(claimParameter.includes("$ref: '#/components/schemas/Uuid'"), "claimId must use Uuid");

const exactEnums = {
  RaidSessionStatus: ["OPEN", "SETTLING", "SETTLED_SUCCESS", "SETTLED_FAILURE"],
  RaidSlotStatus: ["AVAILABLE", "ACTIVE", "RESULT_HELD", "CONFIRMED", "DISCARDED", "EXPIRED"],
  RaidAttemptStatus: ["RUNNING", "RESULT_HELD", "DISCARDED", "CONFIRMED"],
  RaidAttemptMode: ["REWARD", "PRACTICE"],
  RaidGrade: ["PARTICIPATION", "D", "C", "B", "A", "S", "SS", "SSS"],
  RaidClaimKind: ["AUTO_PERSONAL", "DAILY_RANK"],
  RaidClaimStatus: ["CLAIMABLE", "CLAIMED"],
  RaidCombatEventType: ["PLAYER_ACTION", "PLAYER_IMPACT", "BOSS_HIT", "ESCALATION", "PLAYER_DEATH", "TIME_LIMIT_END"],
  RaidCombatActor: ["PLAYER", "BOSS"],
  RaidErrorCode: [
    "RAID_CONTENT_LOCKED",
    "RAID_NO_AVAILABLE_SLOT",
    "RAID_ATTEMPT_RUNNING",
    "RAID_ATTEMPT_RESULT_HELD",
    "RAID_ATTEMPT_LIMIT_REACHED",
    "RAID_INVALID_TRANSITION",
    "RAID_ZERO_DAMAGE_CANNOT_CONFIRM",
    "RAID_REWARD_CAPACITY",
    "RAID_SESSION_STALE",
    "RAID_SESSION_SETTLED",
    "RAID_MAIN_BATTLE_TRANSITION_FAILED",
    "RAID_CLAIM_NOT_FOUND",
    "RAID_CLAIM_ALREADY_CLAIMED",
    "RAID_STATE_VERSION_CONFLICT",
    "IDEMPOTENCY_KEY_REUSED",
  ],
};
for (const [name, expected] of Object.entries(exactEnums)) {
  const values = [...block(name).matchAll(/^        - (.+)$/gm)].map((match) => match[1]);
  assert(JSON.stringify(values) === JSON.stringify(expected), `${name} members drifted: ${values.join(",")}`);
}

const closedObject = (schema) => schema.includes("additionalProperties: false") || /additionalProperties:\n\s+not: \{\}/.test(schema);
const startRequest = block("RaidAttemptStartRequest");
assert(closedObject(startRequest), "attempt start must be a closed object");
const startProperties = [...startRequest.matchAll(/^        ([A-Za-z][A-Za-z0-9]*):$/gm)].map((match) => match[1]);
assert(JSON.stringify(startProperties) === JSON.stringify(["mode"]), `attempt start properties drifted: ${startProperties.join(",")}`);
assert(startRequest.includes("RaidAttemptMode"), "attempt start mode must use the closed enum");
const emptyRequest = block("RaidEmptyRequest");
assert(closedObject(emptyRequest), "raid empty command body must be closed");
assert(!emptyRequest.includes("properties:"), "raid empty command body must define no properties");
for (const path of ["/api/v1/raid/attempts/{attemptId}/retry", "/api/v1/raid/attempts/{attemptId}/confirm", "/api/v1/raid/attempts/{attemptId}/discard", "/api/v1/raid/claims/{claimId}"]) {
  assert(pathBlock(path).includes("#/components/schemas/RaidEmptyRequest"), `${path} must use the closed empty request`);
}

for (const path of ["/api/v1/raid/attempts", "/api/v1/raid/attempts/{attemptId}/retry", "/api/v1/raid/attempts/current"]) {
  assert(pathBlock(path).includes("#/components/schemas/RaidAttemptView"), `${path} must return RaidAttemptView`);
}

const ranking = block("RaidRankingView");
assert(ranking.includes("currentRank:") && ranking.includes("nullable: true") && ranking.includes("minimum: 1"), "currentRank must be nullable with minimum 1");
assert(ranking.includes("topEntries:") && !/topEntries:[\s\S]{0,500}maxItems:/.test(ranking), "topEntries must allow competitive rank-100 ties");

const errorEnvelope = block("RaidErrorEnvelope");
assert(errorEnvelope.includes("code:") && errorEnvelope.includes("$ref: '#/components/schemas/RaidErrorCode'"), "raid error envelope code must link exactly to RaidErrorCode");
for (const path of expectedPaths) assert(pathBlock(path).includes("#/components/schemas/RaidErrorEnvelope"), `${path} must use RaidErrorEnvelope`);
const raidState = block("RaidStateView");
for (const field of ["featureAvailable", "unlockStageId", "unlocked"]) {
  assert(raidState.includes(`        ${field}:`), `RaidStateView must expose ${field}`);
}
assert(raidState.includes("featureAvailable:\n          type: boolean"), "featureAvailable must be boolean");
assert(raidState.includes("unlockStageId:\n          type: string"), "unlockStageId must be string");
assert(raidState.includes("unlocked:\n          type: boolean"), "unlocked must be boolean");
const counterFields = {
  RaidSessionView: [["sealTarget", 0], ["sealedContribution", 0], ["sealProgressBasisPoints", 0]],
  RaidSlotView: [["ordinal", 0], ["attemptsStarted", 0], ["attemptsRemaining", 0]],
  RaidBossSnapshot: [["maxTicks", 0], ["tickDurationMilliseconds", 1], ["escalationIntervalTicks", 1], ["escalationBasisPoints", 0]],
  RaidPlayerSnapshot: [["maxHp", 0], ["attack", 0], ["defense", 0], ["defensePenetration", 0]],
  RaidCombatInputSnapshot: [["seed", 0]],
  RaidAttemptResultView: [["damage", 0], ["sealContribution", 0]],
  RaidCombatEvent: [["sequence", 0], ["logicalTick", 0], ["damage", 0], ["hpBefore", 0], ["hpAfter", 0], ["escalationStage", 0], ["bossAttack", 0], ["bossDefense", 0]],
  RaidRenderingTimeline: [["tickDurationMilliseconds", 1], ["durationMilliseconds", 0]],
  RaidAttemptView: [["slotOrdinal", 0], ["tickDurationMilliseconds", 1], ["durationMilliseconds", 0]],
  RaidRankingEntry: [["rank", 1], ["sealContribution", 0]],
  RaidRankingView: [["currentRank", 1], ["currentSealContribution", 0], ["totalEligibleAccounts", 0]],
  RaidClaimsView: [["claimableCount", 0], ["claimedCount", 0]],
};
for (const [schemaName, fields] of Object.entries(counterFields)) {
  const schema = block(schemaName);
  for (const [field, minimum] of fields) {
    const fieldSchema = schema.match(new RegExp(`        ${field}:[\\s\\S]*?(?=\\n        [A-Za-z][A-Za-z0-9]*:|\\n    [A-Za-z]|(?![\\s\\S]))`))?.[0] ?? "";
    assert(fieldSchema.includes("type: integer") && fieldSchema.includes("format: int64"), `${schemaName}.${field} must be int64`);
    assert(fieldSchema.includes(`minimum: ${minimum}`), `${schemaName}.${field} must have minimum ${minimum}`);
  }
}

console.log("raid OpenAPI assertions passed");
