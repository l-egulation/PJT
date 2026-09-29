# Progression Rebalance v1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship `progression-rebalance-v1` for chapters 1–4 with versioned XP, equipment costs, deterministic chapter-1 materials, first-clear rewards, direct skill unlocks, pending reward claims, existing-account backfill, and measured progression acceptance.

**Architecture:** Keep current immutable results intact and add a second complete content package selected by an account-owned balance version. Battle sessions snapshot that version; first-clear completion routes through one transactional service shared by session and compatibility battle APIs. Content drives every number, PostgreSQL owns idempotency and pending claims, and the web only presents server-authored reward state.

**Tech Stack:** Kotlin 2.1, Spring Boot 3.5, Spring JDBC, Flyway, PostgreSQL 17, React 19, TypeScript, TanStack Query, Zustand, TypeSpec, JSON Schema/Ajv, Vitest, JUnit 5, Testcontainers, Gradle balance-lab.

## Global Constraints

- Implement absolute values only for chapters 1–4 and levels 1–79.
- Preserve `XP_next(L)=1,000L`, level stat growth, and the `L_ref(I)=2I-1` reference-level sequence.
- Use `progression-rebalance-v1` as the new balance and first-clear reward version.
- Existing battle sessions, stored command results, and replay snapshots continue using their captured content version.
- Chapter 1 stages 1–9 grant M1 materials deterministically: normal enemy 2, boss 10; only material family uses 80:10:10 RNG.
- Chapter 2–4 random drop rules remain the current 15%/10-item/inverse-generation rules.
- First-clear rice is immediate; item bundles are all-or-nothing and become pending on insufficient capacity.
- First-clear materials remain tradeable and share existing material stacks.
- Stages 1-1 through 1-6 directly unlock the six skills at Normal level 1 without books or rice.
- Do not change cosmetic tickets, cosmetic pity, cosmetic probabilities, gem rules, or marketplace matching rules.
- Keep 4-10 additional rewards, chapters 5–10, level 80+, Legendary/Mythic rebalance values, and raid rewards outside this implementation.
- Every production change starts with a focused failing test and observed RED.
- On this Windows workstation, map the worktree to an ASCII drive before Gradle tests: `subst R: "C:\Users\SSAFY\Desktop\특화 프로젝트\S15P21B107\.worktrees\balance-progression-docs"`.

---

### Task 1: Build and validate both immutable content packages

**Files:**
- Create: `packages/game-content/schema/progression.schema.json`
- Create: `packages/game-content/schema/equipment-balance.schema.json`
- Modify: `packages/game-content/schema/material-drops.schema.json`
- Create: `tools/content-validator/src/buildProgressionRebalance.ts`
- Modify: `tools/content-validator/src/validate.ts`
- Modify: `tools/content-validator/src/validate.test.ts`
- Create: `tools/content-validator/src/buildProgressionRebalance.test.ts`
- Create: `packages/game-content/versions/v1/progression/progression.json`
- Create: `packages/game-content/versions/v1/equipment/equipment.json`
- Modify: `packages/game-content/versions/v1/manifest.json`
- Create: `packages/game-content/versions/progression-rebalance-v1/manifest.json`
- Create: `packages/game-content/versions/progression-rebalance-v1/chapters/chapters.json`
- Create: `packages/game-content/versions/progression-rebalance-v1/stages/stages.json`
- Create: `packages/game-content/versions/progression-rebalance-v1/drops/material-drops.json`
- Create: `packages/game-content/versions/progression-rebalance-v1/progression/progression.json`
- Create: `packages/game-content/versions/progression-rebalance-v1/equipment/equipment.json`
- Create: `packages/game-content/versions/progression-rebalance-v1/skills/skills.json`
- Create: `packages/game-content/versions/progression-rebalance-v1/gem-dungeons/gem-dungeons.json`
- Create: `packages/game-content/versions/progression-rebalance-v1/cosmetics/cosmetics.json`

**Interfaces:**
- Produces JSON `ProgressionContent`:
  ```ts
  type ProgressionContent = {
    authority: "working" | "applied";
    contentVersion: string;
    levelRange: { min: 1; max: 79 } | null;
    stages: Array<{
      stageId: string;
      normalExperience: number;
      bossExperience: number;
      normalRice: number;
      bossRice: number;
      firstClear: {
        rice: number;
        items: Array<{ itemId: string; quantity: number }>;
        directSkillId: string | null;
      } | null;
    }>;
  };
  ```
- Produces JSON `EquipmentBalanceContent` with `enhancementMaterials[grade][level]`, `enhancementRice[grade][level]`, `promotion[grade]`, and `compatibilityOnly` metadata for unchanged current Legendary behavior.
- Produces JSON `MaterialDropsContent.chapters` where chapter 1 is `GUARANTEED` and chapters 2–4 are `CHANCE`.
- The new package is inactive until Tasks 2–8 finish and its manifest authority becomes `applied`.

- [ ] **Step 1: Write failing schema and generator tests**

Add tests that assert:

```ts
expect(rebalance.contentVersion).toBe("progression-rebalance-v1");
expect(rebalance.progression.stages).toHaveLength(40);
expect(rebalance.progression.stages.find((row) => row.stageId === "stage.01-01")?.firstClear)
  .toEqual({
    rice: 1_392,
    items: [
      { itemId: "POTATO_M1", quantity: 178 },
      { itemId: "SWEET_POTATO_M1", quantity: 178 },
      { itemId: "CORN_M1", quantity: 178 },
    ],
    directSkillId: "active_heavy",
  });
expect(rebalance.progression.stages.find((row) => row.stageId === "stage.03-10")?.firstClear.items)
  .toEqual([
    { itemId: "POTATO_M1", quantity: 28 },
    { itemId: "SWEET_POTATO_M1", quantity: 28 },
    { itemId: "CORN_M1", quantity: 28 },
    { itemId: "POTATO_M2", quantity: 23 },
    { itemId: "SWEET_POTATO_M2", quantity: 23 },
    { itemId: "CORN_M2", quantity: 23 },
    { itemId: "POTATO_M3", quantity: 1 },
    { itemId: "SWEET_POTATO_M3", quantity: 1 },
    { itemId: "CORN_M3", quantity: 1 },
    { itemId: "GEM_BOX", quantity: 30 },
  ]);
expect(rebalance.materialDrops.chapters["1"]).toEqual({
  mode: "GUARANTEED",
  normalQuantity: 2,
  bossQuantity: 10,
  generationWeights: [1, 0, 0, 0],
});
```

Assert chapter totals exactly match the SSOT and that the legacy generated files reproduce current runtime values.

- [ ] **Step 2: Run focused tests and verify RED**

Run:

```bash
corepack pnpm --filter @hanjjak/content-validator exec vitest run src/buildProgressionRebalance.test.ts src/validate.test.ts
```

Expected: FAIL because the new schemas, generator, version directories, and manifest entries do not exist.

- [ ] **Step 3: Implement deterministic content generation**

In `buildProgressionRebalance.ts`, export these stable entry points:

```ts
export const BALANCE_VERSION = "progression-rebalance-v1";
export function buildLegacyProgressionContent(): ProgressionContent;
export function buildProgressionRebalanceContent(): ProgressionContent;
export function buildLegacyEquipmentContent(): EquipmentBalanceContent;
export function buildProgressionEquipmentContent(): EquipmentBalanceContent;
export function buildRebalanceMaterialDrops(): MaterialDropsContent;
export async function writeProgressionPackages(root: string): Promise<void>;
```

Use the SSOT arrays verbatim:

```ts
const firstClear = {
  chapter1M1: [178, 356, 533, 711, 889, 1_067, 1_244, 1_422, 1_600],
  chapter1Rice: [1_392, 2_784, 4_176, 5_568, 6_960, 8_352, 9_744, 11_136, 12_528],
  chapter2M1: [20, 40, 60, 80, 100, 120, 140, 160, 180],
  chapter2M2: [16, 32, 48, 64, 80, 96, 112, 128, 144],
  chapter2Rice: [1_566, 3_132, 4_698, 6_264, 7_830, 9_396, 10_962, 12_528, 14_094],
  chapter3M1: [13, 27, 40, 53, 67, 80, 93, 107, 120],
  chapter3M2: [11, 21, 32, 43, 53, 64, 75, 85, 96],
  chapter3Rice: [1_044, 2_088, 3_132, 4_176, 5_220, 6_264, 7_308, 8_352, 9_396],
  chapter4M1: [6, 12, 18, 24, 30, 36, 42, 48, 54],
  chapter4M2: [5, 10, 14, 19, 24, 29, 34, 38, 43],
  chapter4M3: [5, 10, 14, 19, 24, 29, 34, 38, 43],
  chapter4Rice: [2_610, 5_220, 7_830, 10_440, 13_050, 15_660, 18_270, 20_880, 23_490],
} as const;
```

Generate XP with:

```ts
const referenceLevel = 2 * globalIndex - 1;
const levelMinutes = 2 + (11 / 90) * (2 * referenceLevel - 1);
const normalExperience = Math.max(1, Math.round(1_000 * referenceLevel / (50 * levelMinutes)));
const bossExperience = normalExperience * 5;
```

Generate equipment 29-step arrays by assigning each per-slot generation total to 20%/30%/50% brackets and placing integer remainders on the latest levels in each bracket. Preserve current Legendary values under `compatibilityOnly: true`; lock Epic→Legendary promotion behind `stage.05-10` so chapters 1–4 cannot newly enter the unapproved tier.

- [ ] **Step 4: Extend validation and manifests**

Validate both version roots, exact file checksums, all 40 stage rows, all item/skill references, reward totals, monotone equipment costs, 29-step sums, promotion rounding, and chapter drop modes. Reject a new package unless all changed-domain files share `progression-rebalance-v1`.

- [ ] **Step 5: Run focused tests and content validation**

Run:

```bash
corepack pnpm --filter @hanjjak/content-validator test
corepack pnpm --filter @hanjjak/content-validator validate
```

Expected: all validator tests pass; both manifests have stable SHA-256 checksums; the new manifest remains `working` until Task 8.

- [ ] **Step 6: Commit**

```bash
git add packages/game-content tools/content-validator
git commit -m "feat(content): add progression rebalance v1 package"
```

---

### Task 2: Select versioned stage, progression, and drop content

**Files:**
- Create: `apps/game-api/src/main/resources/db/migration/V52__account_balance_version.sql`
- Create: `modules/account/src/main/kotlin/com/hanjjak/account/application/BalanceVersionService.kt`
- Create: `modules/account/src/main/kotlin/com/hanjjak/account/infrastructure/JdbcBalanceVersionRepository.kt`
- Modify: `modules/account/src/main/kotlin/com/hanjjak/account/infrastructure/AccountConfiguration.kt`
- Modify: `modules/account/src/main/kotlin/com/hanjjak/account/application/AuthenticationService.kt`
- Modify: `modules/account/src/main/kotlin/com/hanjjak/account/application/SocialLoginService.kt`
- Modify: `modules/account/src/test/kotlin/com/hanjjak/account/application/AuthenticationServiceTest.kt`
- Modify: `modules/account/src/test/kotlin/com/hanjjak/account/application/SocialLoginServiceTest.kt`
- Modify: `apps/game-api/src/main/resources/application.yml`
- Modify: `modules/stage/src/main/kotlin/com/hanjjak/stage/application/StageCatalog.kt`
- Modify: `modules/stage/src/main/kotlin/com/hanjjak/stage/infrastructure/JsonStageCatalog.kt`
- Create: `modules/stage/src/main/kotlin/com/hanjjak/stage/infrastructure/VersionedStageCatalog.kt`
- Modify: `modules/battle/src/main/kotlin/com/hanjjak/battle/api/BattleConfiguration.kt`
- Modify: `modules/battle/src/main/kotlin/com/hanjjak/battle/application/StageBattleService.kt`
- Modify: `modules/battle/src/main/kotlin/com/hanjjak/battle/application/BattleSessionService.kt`
- Modify: `modules/battle/src/main/kotlin/com/hanjjak/battle/api/StageController.kt`
- Modify: `modules/battle/src/main/kotlin/com/hanjjak/battle/api/RuntimeStateController.kt`
- Create: `modules/progression/src/main/kotlin/com/hanjjak/progression/domain/ProgressionContent.kt`
- Create: `modules/progression/src/main/kotlin/com/hanjjak/progression/infrastructure/JsonProgressionContent.kt`
- Create: `modules/progression/src/main/kotlin/com/hanjjak/progression/application/ProgressionRewardCatalog.kt`
- Modify: `modules/progression/src/main/kotlin/com/hanjjak/progression/domain/Progression.kt`
- Modify: `modules/progression/src/main/kotlin/com/hanjjak/progression/api/ProgressionConfiguration.kt`
- Modify: `modules/battle/src/main/kotlin/com/hanjjak/battle/application/BattleEnemySettlementService.kt`
- Modify: `modules/battle/src/main/kotlin/com/hanjjak/battle/api/BattleController.kt`
- Modify: `modules/inventory/src/main/kotlin/com/hanjjak/inventory/domain/DropTable.kt`
- Modify: `modules/inventory/src/main/kotlin/com/hanjjak/inventory/infrastructure/JsonMaterialDropContent.kt`
- Create: `modules/inventory/src/main/kotlin/com/hanjjak/inventory/application/MaterialDropCatalog.kt`
- Modify: `modules/inventory/src/main/kotlin/com/hanjjak/inventory/application/BattleRewardService.kt`
- Modify: `modules/inventory/src/main/kotlin/com/hanjjak/inventory/api/InventoryConfiguration.kt`
- Create: `modules/stage/src/test/kotlin/com/hanjjak/stage/infrastructure/VersionedStageCatalogTest.kt`
- Test: `modules/progression/src/test/kotlin/com/hanjjak/progression/domain/ProgressionRulesTest.kt`
- Test: `modules/inventory/src/test/kotlin/com/hanjjak/inventory/domain/DropTableTest.kt`
- Test: `apps/game-api/src/test/kotlin/com/hanjjak/gameapi/BattleSessionE2ETest.kt`

**Interfaces:**
- Produces `BalanceVersionService.current(accountId: UUID): String` and `assignNewAccount(accountId: UUID): String`.
- `hanjjak.balance.active-version` defaults to `enemy-v1-applied` until Task 8 promotes and validates the new package.
- Produces `VersionedStageCatalog.require(balanceVersion: String, stageKey: String): StageDefinition`; `enemy-v1-applied` maps to `/v1`, and `progression-rebalance-v1` maps to its same-named resource directory.
- Produces `ProgressionRewardCatalog.grant(balanceVersion: String, stageId: String, boss: Boolean): ProgressionGrant`.
- Produces `ProgressionRewardCatalog.firstClear(balanceVersion: String, stageId: String): FirstClearRewardDefinition`, where the definition contains rice, normalized items and nullable direct skill ID captured from Task 1 content.
- Produces `MaterialDropCatalog.table(balanceVersion: String): DropTable`; each table retains its domain content version in prepared settlement records.
- `BattleRewardService.prepare` gains `balanceVersion: String` and snapshots the selected domain drop version and prepared items into settlement rows.
- `apps/game-api` keeps `packages/game-content/versions/v1` as a resource root for unchanged `/skills`, `/cosmetics`, `/gem-dungeons` consumers and adds `packages/game-content/versions` for `/v1/...` and `/progression-rebalance-v1/...` catalogs. Do not replace the existing root.

- [ ] **Step 1: Write failing version-selection tests**

Cover an existing account on `enemy-v1-applied`, a new account on `progression-rebalance-v1`, a battle session that keeps its captured version after the account flips, and exact new XP boundaries:

```kotlin
assertEquals(9, catalog.grant("progression-rebalance-v1", "stage.01-01", false).experience)
assertEquals(45, catalog.grant("progression-rebalance-v1", "stage.01-01", true).experience)
assertEquals(75, catalog.grant("progression-rebalance-v1", "stage.04-10", false).experience)
assertEquals(375, catalog.grant("progression-rebalance-v1", "stage.04-10", true).experience)
```

- [ ] **Step 2: Run focused tests and verify RED**
```bat
R:\gradlew.bat :modules:stage:test :modules:progression:test :modules:inventory:test --no-configuration-cache
R:\gradlew.bat :apps:game-api:test --tests "*BattleSessionE2ETest" --no-configuration-cache
```

Expected: at least one focused test fails because balance-version persistence and versioned catalogs do not exist; existing unrelated tests must still compile.

- [ ] **Step 3: Add account balance-version persistence**

`V46` creates:

```sql
create table account_balance_state (
  account_id uuid primary key references account(id) on delete cascade,
  balance_version varchar(80) not null,
  applied_at timestamptz not null,
  retroactive_completed_at timestamptz
);
insert into account_balance_state(account_id,balance_version,applied_at)
select id,'enemy-v1-applied',now() from account;
```

Do not add a database trigger with a future hardcoded version. Inject `BalanceVersionService` into password and social signup, then call `assignNewAccount` immediately after `AccountRepository.save`. It inserts the configured `hanjjak.balance.active-version`; Task 2 keeps that property at `enemy-v1-applied`. Repository reads fail closed on missing or unknown rows instead of silently choosing a version.

- [ ] **Step 4: Load stage and progression data by version**

Keep `packages/game-content/versions/v1` in `apps/game-api` resources and add `packages/game-content/versions`; do not modify the skills resource root. The application then exposes unchanged `/skills`, `/cosmetics`, and `/gem-dungeons` resources plus version-qualified `/v1/...` and `/progression-rebalance-v1/...` resources. Load both stage catalogs and both progression files from qualified paths. Remove `BattleSessionService.CONTENT_VERSION`; read the account balance version only at `start`, store it in `battle_session.content_version` and `CycleInput`, and use the stored input for settlement/completion. Preserve domain file versions separately where current audit/settlement fields require them.

- [ ] **Step 5: Select material-drop policy from the session version**

Pass `input.contentVersion` into `BattleEnemySettlementService.prepare`, persist the selected drop content version and prepared items, and never reroll from the account’s later version. Compatibility `/cycles` and `/auto-run` paths read the account version once per command and pass it to both progression and drop catalogs.

- [ ] **Step 6: Run focused tests and verify GREEN**

Run the Step 2 command again. Expected: all selected tests pass and a started legacy session completes with legacy grants after the account version changes.

- [ ] **Step 7: Commit**

```bash
git add apps/game-api modules/account modules/stage modules/battle modules/progression modules/inventory
git commit -m "feat(server): select versioned progression content"
```

---

### Task 3: Drive equipment costs and promotion gates from version content

**Files:**
- Create: `modules/equipment/src/main/kotlin/com/hanjjak/equipment/domain/EquipmentBalanceContent.kt`
- Create: `modules/equipment/src/main/kotlin/com/hanjjak/equipment/infrastructure/JsonEquipmentBalanceCatalog.kt`
- Create: `modules/equipment/src/main/kotlin/com/hanjjak/equipment/application/EquipmentBalanceCatalog.kt`
- Modify: `modules/equipment/src/main/kotlin/com/hanjjak/equipment/domain/Equipment.kt`
- Modify: `modules/equipment/src/main/kotlin/com/hanjjak/equipment/application/EquipmentService.kt`
- Modify: `modules/equipment/src/main/kotlin/com/hanjjak/equipment/application/EquipmentPorts.kt`
- Modify: `modules/equipment/src/main/kotlin/com/hanjjak/equipment/infrastructure/JdbcEquipmentRepository.kt`
- Modify: `modules/equipment/src/main/kotlin/com/hanjjak/equipment/api/EquipmentConfiguration.kt`
- Modify: `modules/equipment/build.gradle.kts`
- Test: `modules/equipment/src/test/kotlin/com/hanjjak/equipment/domain/EquipmentRulesTest.kt`
- Test: `modules/equipment/src/test/kotlin/com/hanjjak/equipment/application/EquipmentServiceTest.kt`

**Interfaces:**
- Produces `EquipmentBalanceCatalog.policy(contentVersion: String): EquipmentBalancePolicy`.
- Produces `EquipmentBalancePolicy.enhancementCost(state: EquipmentSlotState): EquipmentCost`.
- Produces `EquipmentBalancePolicy.promotionCost(grade: EquipmentGrade): EquipmentCost` and `requiredPromotionStage(grade)`.
- `EquipmentRepository.balanceVersion(accountId)` returns the account-owned version from Task 2.

- [ ] **Step 1: Replace legacy cost assertions with failing versioned assertions**

Test representative bracket and promotion boundaries:

```kotlin
assertMaterials(newPolicy.enhancementCost(state(RARE, 1)), m1 = 10, m2 = 8)
assertMaterials(newPolicy.enhancementCost(state(RARE, 29)), m1 = 28, m2 = 23)
assertMaterials(newPolicy.enhancementCost(state(EPIC, 1)), m1 = 3, m2 = 2, m3 = 2)
assertMaterials(newPolicy.enhancementCost(state(EPIC, 29)), m1 = 9, m2 = 7, m3 = 7)
assertEquals(930, newPolicy.enhancementCost(state(RARE, 1)).riceCost)
assertEquals(1_770, newPolicy.enhancementCost(state(RARE, 29)).riceCost)
assertPromotion(newPolicy.promotionCost(NORMAL), m1 = 74, m2 = 1, rice = 870)
assertPromotion(newPolicy.promotionCost(RARE), m1 = 28, m2 = 23, m3 = 1, rice = 1_770)
assertEquals("stage.03-10", newPolicy.requiredPromotionStage(RARE))
assertEquals("stage.05-10", newPolicy.requiredPromotionStage(EPIC))
```

Also assert all 29 steps are non-decreasing per generation and sum to the SSOT totals across six slots.

- [ ] **Step 2: Run equipment tests and verify RED**

```bat
R:\gradlew.bat :modules:equipment:test --no-configuration-cache
```

Expected: FAIL on current bundle-based material costs and old promotion stages.

- [ ] **Step 3: Implement versioned equipment policies**

Keep Q/stat calculations in `EquipmentRules`; move only costs and promotion gates into content-backed policy. `EquipmentService` reads the account version once under its existing account lock and uses one policy for validation, preview, debit, material consumption, and response. Existing Legendary rows use the new package’s explicit `compatibilityOnly` current-cost rows; Epic→Legendary remains locked until `stage.05-10` exists.

- [ ] **Step 4: Run equipment tests and verify GREEN**

Run Step 2 again. Expected: all domain/service tests pass; previews and commands return identical costs; insufficient material/rice remains atomic.

- [ ] **Step 5: Commit**

```bash
git add modules/equipment
git commit -m "feat(equipment): apply progression rebalance costs"
```

---

### Task 4: Persist and grant atomic first-clear rewards

**Files:**
- Create: `apps/game-api/src/main/resources/db/migration/V53__first_clear_reward_inbox.sql`
- Create: `modules/battle/src/main/kotlin/com/hanjjak/battle/domain/FirstClearReward.kt`
- Create: `modules/battle/src/main/kotlin/com/hanjjak/battle/application/FirstClearRewardPorts.kt`
- Create: `modules/battle/src/main/kotlin/com/hanjjak/battle/application/FirstClearRewardService.kt`
- Create: `modules/battle/src/main/kotlin/com/hanjjak/battle/infrastructure/JdbcFirstClearRewardRepository.kt`
- Modify: `modules/inventory/src/main/kotlin/com/hanjjak/inventory/application/InventoryReservationService.kt`
- Modify: `modules/wallet/src/main/kotlin/com/hanjjak/wallet/application/WalletService.kt`
- Modify: `modules/skills/src/main/kotlin/com/hanjjak/skills/application/SkillService.kt`
- Modify: `modules/battle/build.gradle.kts`
- Test: `modules/inventory/src/test/kotlin/com/hanjjak/inventory/application/InventoryServicesTest.kt`
- Create: `modules/skills/src/test/kotlin/com/hanjjak/skills/application/SkillServiceTest.kt`
- Create: `apps/game-api/src/test/kotlin/com/hanjjak/gameapi/FirstClearRewardIntegrationTest.kt`

**Interfaces:**
- Produces `FirstClearRewardService.apply(accountId: UUID, stageId: String, rewardVersion: String, sourceId: UUID): FirstClearRewardResult?`; returns null when the versioned reward already exists.
- Produces `FirstClearRewardService.pending(accountId: UUID): List<PendingFirstClearReward>`.
- Produces `FirstClearRewardService.claim(accountId: UUID, rewardId: UUID, idempotencyKey: UUID): PendingFirstClearReward`.
- Adds `InventoryReservationService.planStackGrant(accountId: UUID, items: List<ItemReward>): StackGrantPlan` and `applyStackGrant(accountId: UUID, plan: StackGrantPlan, bumpStateVersion: Boolean = true)`; capacity shortage is a normal `canGrant=false` result, not an exception caught inside the first-clear transaction.
- Adds `SkillService.unlockFromFirstClear(accountId: UUID, skillId: String, bumpStateVersion: Boolean = true): Boolean`; it inserts only missing skills and never downgrades existing state.
- Adds `WalletService.credit(accountId: UUID, amount: Long, sourceType: String, sourceId: UUID, bumpStateVersion: Boolean = true): Long`.

- [ ] **Step 1: Write failing storage and atomicity tests**

Cover immediate item delivery, full-inventory pending creation, no partial stacks, immediate rice despite pending items, direct skill unlock, existing skill preservation, duplicate apply returning null, claim replay, concurrent claim winner, and rollback on an injected item write failure.

- [ ] **Step 2: Run focused tests and verify RED**
```bat
R:\gradlew.bat :modules:inventory:test :modules:skills:test --no-configuration-cache
R:\gradlew.bat :apps:game-api:test --tests "*FirstClearRewardIntegrationTest" --no-configuration-cache
```

Expected: the new focused tests fail because reward tables and first-clear services do not exist; existing unrelated tests must still compile.

- [ ] **Step 3: Create first-clear reward tables**

`V53` creates:

```sql
create table stage_first_clear_reward (
  reward_id uuid primary key,
  account_id uuid not null references account(id) on delete cascade,
  stage_id varchar(32) not null,
  reward_version varchar(80) not null,
  source_id uuid not null,
  rice_granted bigint not null check (rice_granted >= 0),
  unlocked_skill_id varchar(80),
  item_status varchar(16) not null check (item_status in ('CLAIMED','PENDING')),
  items_json jsonb not null,
  required_slots integer not null check (required_slots >= 0),
  claimed_at timestamptz,
  created_at timestamptz not null,
  unique(account_id,stage_id,reward_version)
);
create table first_clear_reward_claim_command (
  account_id uuid not null references account(id) on delete cascade,
  idempotency_key uuid not null,
  reward_id uuid not null references stage_first_clear_reward(reward_id),
  fingerprint varchar(128) not null,
  result_json jsonb not null,
  created_at timestamptz not null,
  primary key(account_id,idempotency_key)
);
```

Index pending rows by `(account_id, created_at, reward_id)` with `where item_status='PENDING'`.

- [ ] **Step 4: Implement all-or-nothing item bundles**

Normalize and sort stack rewards, lock the account and affected stacks, and return a `StackGrantPlan` containing normalized items, resulting stacks, `requiredSlots`, `availableSlots`, and `canGrant`. `planStackGrant` performs no writes. `applyStackGrant` requires `canGrant=true` and saves the complete write set. Do not call `InventoryRewardService`, because its partial/skip semantics are correct for battle drops but wrong for first-clear bundles.

- [ ] **Step 5: Implement direct skill unlock and reward application**

`unlockFromFirstClear` executes `insert ... on conflict do nothing`, appends a newly unlocked active skill to the first free loadout slot, and leaves existing grade/level/failure bonus unchanged. `FirstClearRewardService.apply` credits rice with `sourceType=STAGE_FIRST_CLEAR`, performs the skill unlock, obtains one `StackGrantPlan`, and either applies the complete item write set with `CLAIMED` or records `PENDING` without throwing. Do not catch `INVENTORY_CAPACITY_EXCEEDED` to choose pending: an exception from a nested transactional service can mark the outer transaction rollback-only. The stage-completion caller increments account `state_version` once.

- [ ] **Step 6: Implement pending claim idempotency**

Lock the reward row `for update`; replay an existing command before checking current reward state. For a new key, require owner and `PENDING`, recompute one `StackGrantPlan`, fail without writes when `canGrant=false`, apply the complete plan when true, change the row to `CLAIMED`, and save the command result. A second new key after claim returns `FIRST_CLEAR_REWARD_ALREADY_CLAIMED`; the same key returns the stored result.

- [ ] **Step 7: Run focused tests and verify GREEN**

Run Step 2 again. Expected: all focused tests pass with no partial reward mutation.

- [ ] **Step 8: Commit**

```bash
git add apps/game-api/src/main/resources/db/migration/V53__first_clear_reward_inbox.sql modules/battle modules/inventory modules/wallet modules/skills
git commit -m "feat(server): persist first-clear reward inbox"
```

---

### Task 5: Route every stage completion through one first-clear transaction

**Files:**
- Create: `modules/battle/src/main/kotlin/com/hanjjak/battle/application/StageCompletionService.kt`
- Modify: `modules/battle/src/main/kotlin/com/hanjjak/battle/application/BattleSessionService.kt`
- Modify: `modules/battle/src/main/kotlin/com/hanjjak/battle/api/BattleController.kt`
- Modify: `modules/battle/src/main/kotlin/com/hanjjak/battle/application/BattleHistoryService.kt`
- Modify: `modules/battle/src/main/kotlin/com/hanjjak/battle/api/StageController.kt`
- Modify: `apps/game-api/src/test/kotlin/com/hanjjak/gameapi/BattleSessionE2ETest.kt`
- Create: `apps/game-api/src/test/kotlin/com/hanjjak/gameapi/StageFirstClearE2ETest.kt`

**Interfaces:**
- Produces `StageCompletionService.complete(accountId, stageId, contentVersion, sourceId): StageCompletionResult`.
- `StageCompletionResult` contains `firstClear: Boolean` and `firstClearReward: FirstClearRewardResult?`.
- `BattleSessionService.Completion` and compatibility `StageRunResponse` gain `firstClearReward`.

- [ ] **Step 1: Write failing end-to-end tests**

Exercise session completion and `/cycles` with the same account. Assert first success grants exactly once, a repeated clear returns `firstClearReward=null`, completion replay returns the original response, 1-5 still grants cosmetics tickets exactly once, and a pending item bundle does not prevent next-stage unlock.

- [ ] **Step 2: Run focused E2E and verify RED**

```bat
R:\gradlew.bat :apps:game-api:test --tests "*StageFirstClearE2ETest" --tests "*BattleSessionE2ETest" --no-configuration-cache
```

Expected: FAIL because both controllers still own duplicate `persistClear` code and return no first-clear result.

- [ ] **Step 3: Implement `StageCompletionService`**

Within the caller transaction, lock the account, detect whether `first_cleared_at` was previously null, upsert `stage_progress`, retain current 1-5 cosmetics unlock semantics, call `FirstClearRewardService.apply` only for the first clear, and increment account state version once. Remove both private `persistClear` implementations.

- [ ] **Step 4: Include first-clear facts in response and history**

Add the result to session and compatibility responses. Record an explicit `STAGE_FIRST_CLEAR_REWARD` history/domain event with immediate rice, granted items, pending items, unlocked skill and reward version; do not merge pending quantities into ordinary battle drop totals.

- [ ] **Step 5: Run E2E and verify GREEN**

Run Step 2 again. Expected: all first-clear, repeat, replay, pending, and 1-5 regression assertions pass.

- [ ] **Step 6: Commit**

```bash
git add modules/battle apps/game-api/src/test
git commit -m "feat(battle): apply versioned first-clear rewards"
```

---

### Task 6: Publish contracts and build the first-clear reward inbox UI

**Files:**
- Create: `packages/contracts/first-clear-rewards.tsp`
- Modify: `packages/contracts/main.tsp`
- Modify: `packages/contracts/battle-session.tsp`
- Modify: `packages/contracts/generated/openapi/openapi.yaml`
- Create: `modules/battle/src/main/kotlin/com/hanjjak/battle/api/FirstClearRewardController.kt`
- Modify: `modules/account/src/main/kotlin/com/hanjjak/account/api/ApiExceptionHandler.kt`
- Create: `apps/web/src/features/first-clear-rewards/api.ts`
- Create: `apps/web/src/features/first-clear-rewards/FirstClearRewardScreen.tsx`
- Create: `apps/web/src/features/first-clear-rewards/FirstClearRewardScreen.css`
- Create: `apps/web/src/features/first-clear-rewards/FirstClearRewardScreen.test.tsx`
- Create: `apps/web/src/features/first-clear-rewards/api.test.ts`
- Modify: `apps/web/src/features/battle/sessionApi.ts`
- Modify: `apps/web/src/features/battle/runtimeStore.ts`
- Modify: `apps/web/src/features/battle/runtimeStore.test.ts`
- Modify: `apps/web/src/features/battle/BattleHud.tsx`
- Modify: `apps/web/src/features/battle/BattleHud.css`
- Modify: `apps/web/src/features/battle/BattleHud.test.tsx`
- Modify: `apps/web/src/navigationState.ts`
- Modify: `apps/web/src/navigationState.test.ts`
- Modify: `apps/web/src/main.tsx`

**Interfaces:**
- GET `/api/v1/first-clear-rewards` returns `{ rewards, count }` for pending rewards only.
- POST `/api/v1/first-clear-rewards/{rewardId}/claim` requires `Idempotency-Key` and returns the claimed reward.
- TypeSpec defines `FirstClearRewardItem { itemId, displayName, quantity }`, `FirstClearRewardResult { rewardId, stageId, rewardVersion, firstClear, riceGranted, grantedItems, pendingItems, unlockedSkillId, itemStatus, requiredSlots, availableSlots, missingSlots }`, and `PendingFirstClearRewardPage { rewards, count }`. Every Kotlin and TypeScript field uses these exact names.
- `BattleSessionResult.firstClearReward` and compatibility `BattleCycleResponse.firstClearReward` are nullable and server-authored.
- Adds app screen `firstClearRewards` and a persistent navigation entry labeled `첫 클리어 보상`.
- [ ] **Step 1: Write failing contract and UI tests**

Test the exact API paths/headers, pending list rendering, required/missing slot text, claim success invalidations, capacity error recovery, first-clear battle notice, and access after reload through primary/shared navigation.

- [ ] **Step 2: Run contract and web tests to verify RED**

```bash
corepack pnpm --filter @hanjjak/contracts build
corepack pnpm --filter @hanjjak/web exec vitest run src/features/first-clear-rewards src/features/battle/BattleHud.test.tsx src/features/battle/runtimeStore.test.ts src/navigationState.test.ts
```

Expected: contract build or tests fail because the new endpoints, types, route, and components are absent.

- [ ] **Step 3: Implement the server controller and error codes**

Return common envelopes with account state version. Map `FIRST_CLEAR_REWARD_NOT_FOUND` to 404, `FIRST_CLEAR_REWARD_ALREADY_CLAIMED` and `FIRST_CLEAR_REWARD_STATE_CONFLICT` to 409, and `INVENTORY_CAPACITY_EXCEEDED` to 409 with details containing `requiredSlots`, `availableSlots`, and `missingSlots`.

- [ ] **Step 4: Implement the web inbox**

Show stage, reward version, item names/quantities, required slots, current missing slots, and one `전부 수령` button per reward. Disable during mutation, show a specific capacity message, keep the row on failure, remove it after success, and invalidate `first-clear-rewards`, `inventory`, `equipment`, `skills`, and `auth/session` queries.

- [ ] **Step 5: Integrate battle feedback and navigation**

On first clear, show separate rows for immediate rice, immediate items, pending items, and direct skill unlock. Do not add pending quantities to `residenceRewards`. Add `firstClearRewards` to the stored app-screen allowlist, management rendering, header/shared navigation, and preview route.

- [ ] **Step 6: Run contract, focused web tests, typecheck and build**

```bash
corepack pnpm --filter @hanjjak/contracts build
corepack pnpm --filter @hanjjak/web exec vitest run src/features/first-clear-rewards src/features/battle/BattleHud.test.tsx src/features/battle/runtimeStore.test.ts src/navigationState.test.ts
corepack pnpm --filter @hanjjak/web typecheck
corepack pnpm --filter @hanjjak/web build
```

Expected: all commands pass without console warnings from the focused tests.

- [ ] **Step 7: Commit**

```bash
git add packages/contracts modules/account modules/battle apps/web
git commit -m "feat(web): add first-clear reward inbox"
```

---

### Task 7: Add dry-run and resumable existing-account backfill

**Files:**
- Create: `modules/battle/src/main/kotlin/com/hanjjak/battle/application/ProgressionRebalanceBackfillService.kt`
- Create: `modules/battle/src/main/kotlin/com/hanjjak/battle/application/ProgressionRebalanceAccountProcessor.kt`
- Create: `modules/battle/src/main/kotlin/com/hanjjak/battle/domain/ProgressionBackfillReport.kt`
- Modify: `apps/game-api/src/main/kotlin/com/hanjjak/gameapi/GameApiApplication.kt`
- Create: `apps/game-api/src/test/kotlin/com/hanjjak/gameapi/ProgressionBackfillIntegrationTest.kt`
- Create: `apps/game-api/src/test/kotlin/com/hanjjak/gameapi/ProgressionBackfillCommandTest.kt`

**Interfaces:**
- Produces `ProgressionRebalanceBackfillService.preview(): ProgressionBackfillReport`.
- Produces `ProgressionRebalanceBackfillService.apply(batchSize: Int): ProgressionBackfillReport`.
- Produces `ProgressionRebalanceAccountProcessor.applyOne(accountId: UUID): AccountBackfillResult`, a separate Spring bean with `@Transactional(propagation = REQUIRES_NEW)`.
- CLI commands:
  - `backfill-progression-rewards --dry-run`
  - `backfill-progression-rewards --apply --batch-size=100`
- Report fields: target accounts/stages, already applied, rice total, quantities by item, immediate candidates, pending candidates, failed account IDs, and switched account count.

- [ ] **Step 1: Write failing dry-run and resume tests**

Seed accounts with no clears, partial clears, all 40 clears, an existing reward row, full inventory, and an injected failure. Assert preview performs no writes; apply uses saved clear rows; rerun skips completed work; failed accounts remain on `enemy-v1-applied`; successful accounts switch only after all rewards commit.

- [ ] **Step 2: Run focused integration tests and verify RED**

```bat
R:\gradlew.bat :apps:game-api:test --tests "*ProgressionBackfill*" --no-configuration-cache
```

Expected: FAIL because no backfill service or CLI exists.

- [ ] **Step 3: Implement preview and account-transaction apply**

Preview reads only. Apply first verifies that the `progression-rebalance-v1` manifest exists with `authority='applied'`; otherwise it fails with `BALANCE_VERSION_NOT_APPLIED` before writes. It then selects account IDs in stable UUID order and calls the separate `ProgressionRebalanceAccountProcessor` bean for each ID. `applyOne` uses `@Transactional(propagation = REQUIRES_NEW)`, calls the same `FirstClearRewardService.apply` used by live clears, then sets `balance_version='progression-rebalance-v1'` and `retroactive_completed_at=now()` only after every recorded clear succeeds or already exists. Do not use self-invocation for per-account transactions; it bypasses the Spring proxy and would put the full batch in one transaction.

- [ ] **Step 4: Implement a non-web CLI path**

Use `SpringApplicationBuilder(GameApiApplication::class.java).web(WebApplicationType.NONE)` for the backfill command, print one JSON report to stdout, return nonzero when `failedAccountIds` is non-empty, and close the context. Keep normal server startup and the existing `migrate` command unchanged.

- [ ] **Step 5: Run focused tests and verify GREEN**

Run Step 2 again. Expected: dry-run is read-only, apply is resumable, and account switching is all-or-nothing per account.

- [ ] **Step 6: Smoke the command against disposable PostgreSQL**

Run:

```bat
R:\gradlew.bat :apps:game-api:bootRun --args="backfill-progression-rewards --dry-run" --no-configuration-cache
```

Expected: valid JSON report, no changed rows, exit 0.

- [ ] **Step 7: Commit**

```bash
git add modules/battle apps/game-api
git commit -m "feat(ops): add progression reward backfill"
```

---

### Task 8: Generate enemy values and prove progression/economy acceptance

**Files:**
- Create: `apps/balance-lab/src/main/kotlin/com/hanjjak/balancelab/ProgressionRebalance.kt`
- Create: `apps/balance-lab/src/main/kotlin/com/hanjjak/balancelab/ProgressionRebalanceReport.kt`
- Create: `apps/balance-lab/src/test/kotlin/com/hanjjak/balancelab/ProgressionRebalanceTest.kt`
- Modify: `apps/balance-lab/src/main/kotlin/com/hanjjak/balancelab/Main.kt`
- Modify: `apps/balance-lab/src/main/kotlin/com/hanjjak/balancelab/EconomySimulation.kt`
- Modify: `packages/game-content/versions/progression-rebalance-v1/stages/stages.json`
- Modify: `packages/game-content/versions/progression-rebalance-v1/manifest.json`
- Modify: `apps/game-api/src/main/resources/application.yml`
- Create: `docs/70-plans/mvp-release/verification/progression-rebalance-v1.csv`
- Create: `docs/70-plans/mvp-release/verification/progression-rebalance-v1-economy.json`

**Interfaces:**
- CLI `:apps:balance-lab:run --args="progression-rebalance --write"` writes stage content and both reports.
- `simulateProgressionRebalance(seeds: LongRange): ProgressionRebalanceReport` returns P50/P90 arrival time, equipment state, promotion stage, market volume, rice supply/consumption, and boss success by chapter.
- Generated 10-stage reference Q values are fixed at 25, 54, 64, and 93; intermediate Q uses monotone linear interpolation between chapter endpoints.
- Guaranteed skill baseline is only the directly unlocked Normal-1 set. Stage 3-10 uses gem levels `[2,2,2,3,3,3]`; stage 4-10 uses `[4,4,4,4,4,4]`.

- [ ] **Step 1: Write failing acceptance tests**

Assert:

```kotlin
assertInRange(report.chapter(1).p50Minutes, 70.0, 80.0)
assertTrue(report.chapter(1).p90Minutes <= 94.5)
assertInRange(report.chapter(2).p50Minutes, 240.0, 265.0)
assertTrue(report.chapter(2).p90Minutes <= 315.62)
assertInRange(report.chapter(3).p50Minutes, 510.0, 545.0)
assertTrue(report.chapter(3).p90Minutes <= 658.95)
assertInRange(report.chapter(4).p50Minutes, 880.0, 920.0)
assertTrue(report.chapter(4).p90Minutes <= 1_124.5)
assertTrue(report.bosses.all { it.referenceSuccessRate >= 0.90 })
assertTrue(report.bosses.all { it.deficientSuccessRate < it.referenceSuccessRate })
assertTrue(report.promotion("NORMAL_TO_RARE").p50StageIndex <= 15)
assertTrue(report.promotion("RARE_TO_EPIC").p50StageIndex <= 35)
```

Also assert chapter 1 produces exactly 50 M1 items per full 1–9 cycle and stage 10 produces no repeat items.

- [ ] **Step 2: Run balance tests and verify RED**

```bat
R:\gradlew.bat :apps:balance-lab:test --tests "*ProgressionRebalanceTest" --no-configuration-cache
```

Expected: FAIL because the rebalance simulator and generated enemy values do not exist.

- [ ] **Step 3: Implement deterministic reference builds and enemy synthesis**

Use levels `2I-1`. Interpolate Q monotonically between `(1,0)`, `(10,25)`, `(20,54)`, `(30,64)`, `(40,93)`. Use guaranteed Normal-1 skills only. For attack checks, binary-search boss HP to the greatest value with at least 90% success over seeds 1–1,000. For HP checks, binary-search scheduled strike damage so the reference build succeeds at least 90% and the zero-HP-equipment deficient build fails more often. For penetration checks, derive defense around the approved reference penetration, then binary-search HP under the same 90% gate. Standard stages must have no intentional deficient-build gate and must not become slower than the next 10-stage target budget.

- [ ] **Step 4: Implement cohort progression/economy simulation**

Run 10,000 deterministic accounts, uniformly split across primary materials. Apply first-clear rewards, chapter-1 guaranteed drops, chapter-2–4 chance drops, equipment spending, direct skill unlocks and fixed boss gem presets. Model the marketplace by matching supply and demand between simulated accounts at generation parity `1:5:25`, applying the existing 10% seller fee; do not add inventory from a system seller or assume unmatched orders fill. Report market-use, no-market and market-liquidity-shortage cohorts separately; acceptance P50/P90 uses the market-use cohort only when peer supply actually fills its orders.
- [ ] **Step 5: Generate content and reports**

```bat
R:\gradlew.bat :apps:balance-lab:run --args="progression-rebalance --write" --no-configuration-cache
```

Expected: writes 40 stage rows, CSV and economy JSON; exits nonzero if any P50/P90, promotion, or boss gate fails.

- [ ] **Step 6: Validate generated content and rerun tests**

Do not change the active application version before these gates pass.

```bash
corepack pnpm --filter @hanjjak/content-validator validate
```

```bat
R:\gradlew.bat :apps:balance-lab:test :modules:stage:test :modules:progression:test :modules:inventory:test :modules:equipment:test --no-configuration-cache
```

Expected: all commands pass. Change the new manifest authority from `working` to `applied`, regenerate its checksum, rerun both commands, then change `hanjjak.balance.active-version` default from `enemy-v1-applied` to `progression-rebalance-v1`. Re-run the content validator once more after the property change. Production backfill `--apply` remains blocked until this sequence completes.

- [ ] **Step 7: Commit**

```bash
git add apps/balance-lab packages/game-content docs/70-plans/mvp-release/verification
git commit -m "feat(balance): apply progression rebalance enemy curve"
```

---

### Task 9: Run end-to-end acceptance and synchronize delivery evidence

**Files:**
- Modify: `docs/wiki/06-delivery/tasks/A-release-scope/a-05-progression-rebalance.md`
- Modify: `docs/wiki/06-delivery/tasks/A-release-scope/_index.md`
- Modify: `docs/wiki/06-delivery/tasks/_index.md`
- Modify: `docs/70-plans/progression-rebalance/verification.md`

**Interfaces:**
- Final evidence records exact source/content/migration/contract paths, test counts, simulation report paths, PostgreSQL scenarios, browser scenarios, and unresolved exclusions.
- A-05 becomes development `완료` only if content, server, backfill, contracts and web inbox are all implemented; verification becomes `완료` only if every acceptance gate below passes.

- [ ] **Step 1: Run focused server and content suites**

```bash
corepack pnpm --filter @hanjjak/content-validator test
corepack pnpm --filter @hanjjak/content-validator validate
corepack pnpm --filter @hanjjak/contracts build
```

```bat
R:\gradlew.bat :modules:stage:test :modules:progression:test :modules:inventory:test :modules:equipment:test :modules:skills:test :modules:battle:test :apps:balance-lab:test :apps:game-api:test --no-configuration-cache
```

Expected: all suites pass with zero failed tests.

- [ ] **Step 2: Run web regression**

```bash
corepack pnpm --filter @hanjjak/web test
corepack pnpm --filter @hanjjak/web typecheck
corepack pnpm --filter @hanjjak/web build
```

Expected: all tests pass, TypeScript has zero errors, and Vite production build succeeds.

- [ ] **Step 3: Run PostgreSQL behavioral smoke**

Against a disposable database, execute: new 1-1 clear immediate grant; duplicate completion; 1-5 cosmetics regression; full inventory pending; failed partial claim; successful claim after capacity; simultaneous claims; old-version active session completion; dry-run no writes; apply and replay backfill. Record response IDs and final table counts without storing credentials.

- [ ] **Step 4: Run actual browser acceptance on port 5173**

Launch game API and web from this worktree using `http://127.0.0.1:5173`. Verify first-clear result sections, direct skill unlock, immediate inventory grant, pending inbox entry, capacity error, claim success, query refresh, reload persistence, responsive 390×844 and desktop layout, no horizontal overflow, and no console errors. Use port 5173 because mutation CORS allows that origin.

- [ ] **Step 5: Update task and verification evidence**

Record only observed commands/results. Keep chapters 5–10, level 80+, 4-10 extra reward, raid reward and cosmetic balance as explicit exclusions. If any simulation/PostgreSQL/browser gate is missing, leave `verification_status: '부분 검증'`.

- [ ] **Step 6: Validate docs and diff**

Run the changed-document relative-link validator, reward/equipment arithmetic validator, `git diff --check`, and `git status --short`. Confirm A-05 status matches both indexes and no source file sits outside the plan’s listed paths.

- [ ] **Step 7: Commit**

```bash
git add docs/wiki/06-delivery/tasks/A-release-scope docs/wiki/06-delivery/tasks/_index.md docs/70-plans/progression-rebalance docs/superpowers/specs/2026-09-11-progression-rebalance-v1-design.md
git commit -m "docs: record progression rebalance implementation evidence"
```
