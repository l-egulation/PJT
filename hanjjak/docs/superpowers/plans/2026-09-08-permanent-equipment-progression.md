# Permanent Equipment Progression Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace repeatable equipment instances and loadout swapping with one permanent unlock, enhancement, and promotion state per account and equipment slot.

**Architecture:** `modules/equipment` owns the permanent slot state and all costs, gates, previews, and command validation. PostgreSQL stores one row per `(account_id, slot)` and the existing account lock plus equipment command record preserves transaction and idempotency semantics. The character calculator consumes permanent slot states directly, while TypeSpec and the web client expose six server-authored growth cards without equipment UUIDs or client-side policy calculations.

**Tech Stack:** Kotlin 2.2.21, Spring Boot 3.5.16, Spring JDBC, PostgreSQL 17/Flyway, TypeSpec 1.15, React 19, TanStack Query 5, Vitest 4, Vite 8.

## Global Constraints

- Slots are exactly `WEAPON`, `GLOVES`, `ARMOR`, `HELMET`, `CAPE`, and `SHOES`.
- First unlock costs 100 rice plus 10 each of `POTATO_M1`, `SWEET_POTATO_M1`, and `CORN_M1`, and creates `NORMAL +1`.
- Grades are `NORMAL → RARE → EPIC → LEGENDARY`; every grade displays enhancement levels 1 through 30.
- `costStep = gradeIndex * 30 + currentEnhancementLevel`; an executable enhancement costs `30 * costStep` rice.
- `bundleCount = ceil(costStep / 10)`; each bundle consumes, for every active generation and each of the three material families, M1 125, M2 25, M3 5, and M4 1.
- Promotion gates are `stage.01-10`, `stage.02-10`, and `stage.03-10`, require current +30, and result in the next grade +1.
- Promotion costs are 1,000/M2 each 10, 5,000/M3 each 10, and 25,000/M4 each 10.
- `Q = gradeIndex * 39 + enhancementLevel`; locked slots retain the existing Q=0 baseline contribution.
- Commands are 100% successful after validation and atomically update balances, slot state, account state version, and the idempotency result.
- Reset only development equipment instance, loadout, and equipment-command data. Do not reset accounts, wallets, inventory, cosmetics, or unrelated state.
- The server authors costs, balances, current/next Q and stats, executable flags, and disabled reasons. The browser does not reproduce these formulas.
- Clean cutover: remove craft/equip/equipment-UUID contracts and callers; do not add compatibility aliases.

---

### Task 1: Propagate the Approved Equipment Policy

**Files:**
- Modify: `docs/30-domain/items/equipment/ssot.md`
- Modify: `docs/80-decisions/README.md`
- Modify: `docs/30-domain/items/equipment/README.md`
- Modify: `docs/30-domain/items/equipment/features/_index.md`

**Interfaces:**
- Consumes: approved design at `docs/superpowers/specs/2026-09-08-permanent-equipment-progression-design.md`.
- Produces: one canonical equipment policy for later code and task evidence; routing documents contain links rather than duplicate numeric rules.

- [ ] **Step 1: Replace the old instance/crafting policy in the equipment SSOT**

Record permanent account×slot ownership, the six-slot lifecycle, exact unlock/enhancement/promotion formulas, promotion stage gates, Q formula, excluded legacy mechanics, and server-authoritative command behavior. Remove the old per-slot recipes and M1-each-2 enhancement rule instead of retaining parallel “working” rules.

- [ ] **Step 2: Confirm the decision and supersede the old working cost row**

Change the decision row to a confirmed permanent-equipment decision linked to the equipment SSOT and approved design. State that it supersedes the 2026-09-03 repeatable instance/crafting working rule without modifying archive or meeting records.

- [ ] **Step 3: Update routing labels only**

Change route labels from instance/loadout/recraft concepts to permanent slot state, unlock/enhance/promote, and progression validation. Keep the SSOT and G-task links as the only policy and status owners.

- [ ] **Step 4: Validate links and commit**

Run a relative Markdown link check over the four files and the approved design. Expected: every relative link resolves and no repeated old numeric policy remains in routing files.

```bash
git add docs/30-domain/items/equipment docs/80-decisions/README.md
git commit -m "docs: confirm permanent equipment progression"
```

### Task 2: Define Permanent Slot Rules and Views

**Files:**
- Modify: `modules/equipment/src/main/kotlin/com/hanjjak/equipment/domain/Equipment.kt`
- Modify: `modules/equipment/src/test/kotlin/com/hanjjak/equipment/domain/EquipmentRulesTest.kt`

**Interfaces:**
- Produces: `EquipmentSlotState(accountId: UUID, slot: EquipmentSlot, grade: EquipmentGrade, enhancementLevel: Int)`.
- Produces: `EquipmentGrowthSummary`, `EquipmentActionSummary`, `EquipmentPromotionSummary`, `EquipmentSlotSummary`, `EquipmentState`, and `EquipmentCommandResult` with no equipment UUID/equipped/locked fields.
- Produces: `EquipmentRules.unlockCost()`, `enhancementCost(state)`, `promotionCost(grade)`, `requiredPromotionStage(grade)`, `nextGrade(grade)`, `q(state)`, `statSummary(slot, state)`, `referenceStatSummary(slot, q)`, and `aggregateStats(states)`.

- [ ] **Step 1: Write boundary-first failing rule tests**

Cover all six equal unlock costs; enhancement rice boundaries 30, 870, 930, 1,770, 1,830, 2,670, 2,730, and 3,570; bundle boundaries 1/10/11/29/31/119; cumulative generations; equal family quantities; promotion costs/gates; Q transitions 30→40, 69→79, and 108→118; and Q=0 for missing slots.

```kotlin
private fun state(grade: EquipmentGrade, level: Int) =
    EquipmentSlotState(UUID(0, 1), EquipmentSlot.WEAPON, grade, level)

@Test
fun `enhancement cost is continuous across grade boundaries`() {
    assertEquals(870, EquipmentRules.enhancementCost(state(EquipmentGrade.NORMAL, 29)).riceCost)
    assertEquals(930, EquipmentRules.enhancementCost(state(EquipmentGrade.RARE, 1)).riceCost)
    assertEquals(3_570, EquipmentRules.enhancementCost(state(EquipmentGrade.LEGENDARY, 29)).riceCost)
}

@Test
fun `promotion starts ten q above previous grade maximum`() {
    assertEquals(30, EquipmentRules.q(state(EquipmentGrade.NORMAL, 30)))
    assertEquals(40, EquipmentRules.q(state(EquipmentGrade.RARE, 1)))
    assertEquals(79, EquipmentRules.q(state(EquipmentGrade.EPIC, 1)))
    assertEquals(118, EquipmentRules.q(state(EquipmentGrade.LEGENDARY, 1)))
}
```

- [ ] **Step 2: Run the focused test and observe the old-model failure**

Run:

```bash
cmd.exe /c gradlew.bat :modules:equipment:test --tests com.hanjjak.equipment.domain.EquipmentRulesTest --no-configuration-cache
```

Expected: compilation/test failure because permanent state and cost functions do not exist and old Q offsets are 0/10/20/30.

- [ ] **Step 3: Replace the old domain model and formulas**

Use grade indices and fixed generation quantities, rejecting enhancement at +30:

```kotlin
enum class EquipmentGrade(val index: Int, val label: String) {
    NORMAL(0, "노말"), RARE(1, "희귀"), EPIC(2, "영웅"), LEGENDARY(3, "전설")
}

data class EquipmentSlotState(
    val accountId: UUID,
    val slot: EquipmentSlot,
    val grade: EquipmentGrade,
    val enhancementLevel: Int,
)

fun q(state: EquipmentSlotState): Int = state.grade.index * 39 + state.enhancementLevel
fun costStep(state: EquipmentSlotState): Int = state.grade.index * 30 + state.enhancementLevel
fun bundleCount(state: EquipmentSlotState): Int = (costStep(state) + 9) / 10
```

Make `EquipmentGrowthSummary.grade` and `gradeName` nullable so locked cards can carry a Q=0 baseline preview. `EquipmentPromotionSummary` explicitly carries `requiredStageId`, `chapterCleared`, `maxEnhancementReached`, cost, result preview, `executable`, and `disabledReason`.

- [ ] **Step 4: Run the domain tests and commit**

Expected: focused `EquipmentRulesTest` passes.

```bash
git add modules/equipment/src/main/kotlin/com/hanjjak/equipment/domain/Equipment.kt modules/equipment/src/test/kotlin/com/hanjjak/equipment/domain/EquipmentRulesTest.kt
git commit -m "feat(equipment): define permanent slot progression rules"
```

### Task 3: Replace Persistence and Commands

**Files:**
- Create: `apps/game-api/src/main/resources/db/migration/V24__permanent_equipment_slots.sql`
- Modify: `modules/equipment/src/main/kotlin/com/hanjjak/equipment/application/EquipmentPorts.kt`
- Modify: `modules/equipment/src/main/kotlin/com/hanjjak/equipment/application/EquipmentService.kt`
- Modify: `modules/equipment/src/main/kotlin/com/hanjjak/equipment/infrastructure/JdbcEquipmentRepository.kt`
- Create: `modules/equipment/src/test/kotlin/com/hanjjak/equipment/application/EquipmentServiceTest.kt`

**Interfaces:**
- Repository produces `slotStates(accountId)`, `findSlotState(accountId, slot)`, `createSlotState(state)`, `updateSlotState(state)`, and `firstClearedStages(accountId, stageIds): Set<String>`.
- Service produces `unlock(accountId, key, slot)`, `enhance(accountId, key, slot)`, and `promote(accountId, key, slot)`.
- Fingerprints are exactly `unlock\u0000<SLOT>`, `enhance\u0000<SLOT>`, and `promote\u0000<SLOT>`.

- [ ] **Step 1: Write failing service tests with an in-memory repository**

The fake repository must record balances, states, first-cleared stage IDs, state-version increments, consumes, and stored command JSON. Tests cover:

```kotlin
@Test fun `unlock charges once and same idempotency key replays result`()
@Test fun `same key with another slot is rejected`()
@Test fun `unlocking an owned slot is rejected before charging`()
@Test fun `enhancement uses current grade costs and stops at thirty`()
@Test fun `promotion requires level and chapter then returns next grade one`()
@Test fun `legendary thirty rejects further growth`()
```

Each test asserts the exact state, consume count, balance delta, account-version delta, and error code applicable to its name. The replay test asserts one state mutation and byte-equivalent command results; the key-reuse test expects `IDEMPOTENCY_KEY_REUSED`; the duplicate unlock test expects `EQUIPMENT_ALREADY_UNLOCKED`; promotion gate tests expect `EQUIPMENT_MAX_ENHANCEMENT_REQUIRED` then `EQUIPMENT_CHAPTER_NOT_CLEARED`; terminal enhancement and promotion expect `EQUIPMENT_MAX_ENHANCEMENT` and `EQUIPMENT_MAX_GRADE`.

- [ ] **Step 2: Run service tests and observe the missing API failure**

Run the equipment module test task. Expected: compile failure for new repository/service methods.

- [ ] **Step 3: Implement repository and service cutover**

`buildState` loads all 12 material balances and the three promotion first-clear stages in one query each, then constructs exactly six `EquipmentSlotSummary` entries in enum order. Cost summaries add `availableQuantity` without changing policy quantities. Disabled-reason precedence is state gate, then rice, then materials. Command bodies validate before `consume`, update one permanent row, increment the account version, rebuild state, and save the result JSON.

- [ ] **Step 4: Add the clean development migration**

```sql
drop table equipment_loadout;
drop table equipment_instance;
drop table equipment_command_record;

create table equipment_slot_state (
  account_id uuid not null references account(id) on delete cascade,
  slot varchar(16) not null check (slot in ('WEAPON','GLOVES','ARMOR','HELMET','CAPE','SHOES')),
  grade varchar(16) not null check (grade in ('NORMAL','RARE','EPIC','LEGENDARY')),
  enhancement_level integer not null check (enhancement_level between 1 and 30),
  unlocked_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  primary key(account_id, slot)
);
```

Recreate `equipment_command_record` with its V12 idempotency schema. Do not touch wallet, inventory, account, stage, or cosmetics tables.

- [ ] **Step 5: Run equipment tests and commit**

Expected: domain and service tests pass.

```bash
git add apps/game-api/src/main/resources/db/migration/V24__permanent_equipment_slots.sql modules/equipment/src
git commit -m "feat(equipment): persist permanent slot commands"
```

### Task 4: Cut Over HTTP and TypeSpec Contracts

**Files:**
- Modify: `modules/equipment/src/main/kotlin/com/hanjjak/equipment/api/EquipmentController.kt`
- Modify: `packages/contracts/equipment.tsp`
- Regenerate: `packages/contracts/generated/openapi/openapi.yaml`
- Modify: `docs/70-plans/api-contract/README.md`

**Interfaces:**
- HTTP produces `GET /api/v1/equipment` and `POST /api/v1/equipment/{slot}/unlock|enhance|promote`.
- Each command accepts `Idempotency-Key: Uuid`, an `EmptyRequest`, and returns `EquipmentCommandResult { slot, state }`.
- Removes `/craft`, `/{equipmentId}/equip`, and UUID-based enhancement.

- [ ] **Step 1: Replace controller routes and path types**

```kotlin
@PostMapping("/{slot}/unlock")
fun unlock(@PathVariable slot: EquipmentSlot, @RequestHeader("Idempotency-Key") key: UUID,
           @RequestBody request: EmptyRequest = EmptyRequest(), session: HttpSession): Envelope<EquipmentCommandResult> {
    val accountId = accountId(session)
    return envelope(accountId, equipment.unlock(accountId, key, slot))
}

@PostMapping("/{slot}/enhance")
fun enhance(@PathVariable slot: EquipmentSlot, @RequestHeader("Idempotency-Key") key: UUID,
            @RequestBody request: EmptyRequest = EmptyRequest(), session: HttpSession): Envelope<EquipmentCommandResult> {
    val accountId = accountId(session)
    return envelope(accountId, equipment.enhance(accountId, key, slot))
}

@PostMapping("/{slot}/promote")
fun promote(@PathVariable slot: EquipmentSlot, @RequestHeader("Idempotency-Key") key: UUID,
            @RequestBody request: EmptyRequest = EmptyRequest(), session: HttpSession): Envelope<EquipmentCommandResult> {
    val accountId = accountId(session)
    return envelope(accountId, equipment.promote(accountId, key, slot))
}
```

- [ ] **Step 2: Replace TypeSpec models and operations**

Model six slot summaries with nullable current grade/name, optional action results represented as `EquipmentActionSummary | null`, and promotion gate fields. Keep int64 for rice and material quantities.

- [ ] **Step 3: Regenerate and validate OpenAPI**

Run:

```bash
corepack pnpm --filter @hanjjak/contracts build
```

Expected: TypeSpec succeeds; generated OpenAPI contains the three `{slot}` commands and contains no equipment UUID, craft, or equip operation.

- [ ] **Step 4: Update the API plan and commit**

Describe only the four current endpoints and link numeric policy to the equipment SSOT.

```bash
git add modules/equipment/src/main/kotlin/com/hanjjak/equipment/api/EquipmentController.kt packages/contracts docs/70-plans/api-contract/README.md
git commit -m "feat(api): expose slot equipment progression"
```

### Task 5: Migrate Character and Combat Equipment Inputs

**Files:**
- Modify: `apps/game-api/src/main/kotlin/com/hanjjak/gameapi/character/CharacterStatsCalculator.kt`
- Modify: `apps/game-api/src/main/kotlin/com/hanjjak/gameapi/character/CharacterStatsService.kt`
- Modify: `apps/game-api/src/test/kotlin/com/hanjjak/gameapi/character/CharacterStatsCalculatorTest.kt`

**Interfaces:**
- `CharacterStatsCalculator.calculate` consumes `Map<EquipmentSlot, EquipmentSlotState>`.
- Equipment source IDs are stable slot IDs such as `equipment.WEAPON`, not removed instance UUIDs.
- Missing slots still produce the existing Q=0 base equipment contribution.

- [ ] **Step 1: Change calculator tests to permanent states**

Assert normal +3 remains the existing base result, missing equipment remains Q=0, and rare +1 uses Q40 rather than Q11. Assert the source label and source ID no longer depend on an instance UUID.

- [ ] **Step 2: Run the calculator test and observe type/Q failures**

Run the focused `CharacterStatsCalculatorTest`. Expected: compile failure until calculator inputs migrate.

- [ ] **Step 3: Migrate calculator and service callers**

Replace `equipment.equipped(accountId)` with `equipment.slotStates(accountId).associateBy { it.slot }`. In `equipmentSource`, calculate `item?.let(EquipmentRules::q) ?: 0` and use `equipment.${slot.name}` as the stable source ID.

- [ ] **Step 4: Run focused equipment and character tests and commit**

```bash
git add apps/game-api/src/main/kotlin/com/hanjjak/gameapi/character apps/game-api/src/test/kotlin/com/hanjjak/gameapi/character
git commit -m "refactor(character): consume permanent equipment slots"
```

### Task 6: Build the Six Permanent Growth Cards

**Files:**
- Modify: `apps/web/src/features/equipment/api.ts`
- Modify: `apps/web/src/features/equipment/api.test.ts`
- Modify: `apps/web/src/features/equipment/EquipmentScreen.tsx`
- Create: `apps/web/src/features/equipment/EquipmentScreen.test.tsx`
- Modify: `apps/web/src/styles.css`

**Interfaces:**
- `EquipmentCommand = { kind: "unlock" | "enhance" | "promote"; slot: EquipmentSlot; key: string }`.
- `equipmentApi.command(command)` sends the exact supplied idempotency key, allowing uncertain retries to replay one request.
- The screen consumes server-authored `slots`, current/action previews, costs, `executable`, and `disabledReason` only.

- [ ] **Step 1: Write failing API route/idempotency tests**

For all three commands, assert encoded slot paths, `{}` bodies, and the caller-provided stable key. Assert a retry of the same command produces two requests with the same header and body.

- [ ] **Step 2: Write failing static-render card tests**

Seed React Query with six representative slot summaries. Assert markup distinguishes `최초 제작`, enhancement costs/next stat, `30강 필요`, `1-10 클리어 필요`, executable `승급`, and `최종 성장 완료`; assert old `장착 슬롯`, `보유 장비`, and `제작하고 장착` text is absent.

- [ ] **Step 3: Replace web API types and commands**

Add an error carrying HTTP status and code so client-known 4xx failures refresh state, while network/5xx failures retain the exact command and block new commands until `같은 요청 다시 확인` replays it.

- [ ] **Step 4: Replace the screen with exactly six cards**

On success, set `['equipment']` to the returned state and invalidate `['inventory']`, `['auth', 'session']`, and `['character-stats']`. Card buttons submit only server-provided actions and never calculate Q, prices, material bundles, grade gates, or promotion stage IDs.

- [ ] **Step 5: Replace obsolete loadout styles and verify responsive layout**

Use a responsive card grid with readable cost rows, shortfall emphasis, promotion preview, and a terminal visual state. Preserve existing palette and break the grid to one column on narrow screens.

- [ ] **Step 6: Run web tests, typecheck, and build; commit**

```bash
corepack pnpm --filter @hanjjak/web test
corepack pnpm --filter @hanjjak/web typecheck
corepack pnpm --filter @hanjjak/web build
git add apps/web/src/features/equipment apps/web/src/styles.css
git commit -m "feat(web): add permanent equipment growth cards"
```

### Task 7: Synchronize Delivery State and Evidence

**Files:**
- Modify: `docs/wiki/06-delivery/tasks/G-equipment-crafting/_index.md`
- Modify: `docs/wiki/06-delivery/tasks/G-equipment-crafting/g-01-equipment-slot-state-schema.md`
- Modify: `docs/wiki/06-delivery/tasks/G-equipment-crafting/g-02-chapter-equipment-recipes.md`
- Modify: `docs/wiki/06-delivery/tasks/G-equipment-crafting/g-04-equipment-crafting-material-consumption.md`
- Modify: `docs/wiki/06-delivery/tasks/G-equipment-crafting/g-06-equip-lock-dismantle-tradeability.md`
- Modify: `docs/wiki/06-delivery/tasks/G-equipment-crafting/g-07-crafting-enhancement-economy-validation.md`
- Modify: `docs/wiki/06-delivery/tasks/G-equipment-crafting/g-08-enhancement-slots-stat-table.md`
- Modify: `docs/wiki/06-delivery/tasks/G-equipment-crafting/g-09-equipment-replacement-balance.md`
- Modify: `docs/wiki/06-delivery/tasks/K-client-ux/k-10-equipment-crafting-ui.md`
- Modify: `docs/wiki/06-delivery/tasks/K-client-ux/k-11-enhancement-equipment-sale-ui.md`
- Modify: `docs/wiki/06-delivery/tasks/_index.md`

**Interfaces:**
- Consumes: actual code paths and verification outputs from Tasks 2–6.
- Produces: delivery status only; all numeric rules remain links to the equipment SSOT.

- [ ] **Step 1: Rewrite stale acceptance language**

Mark instance/loadout/sale/recraft acceptance as superseded by the confirmed permanent-slot decision. Reframe G-01/G-02/G-04/G-06/G-08/G-09 and K-10/K-11 around permanent state, one-time unlock, enhancement/promotion, and six-card UI without copying numeric policy.

- [ ] **Step 2: Record development evidence precisely**

Link migration, domain/service, controller/contract, character calculation, web screen, and focused test paths. Use `개발 완료 / 부분 검증` before the live DB/HTTP/browser checks.

- [ ] **Step 3: Align G, K, and global indexes**

Ensure each status in an index exactly matches its task front matter and state table.

### Task 8: Verify Migration, HTTP Commands, and Browser Surface

**Files:**
- Modify after evidence exists: the task files and indexes from Task 7.

**Interfaces:**
- Uses the ASCII junction `C:/hanjjak-main` for Gradle.
- Uses local PostgreSQL, Spring Boot on port 8080, and Vite on port 5173.

- [ ] **Step 1: Run clean focused JVM verification**

```bash
cmd.exe /c gradlew.bat :modules:equipment:clean :apps:game-api:clean :modules:equipment:test :apps:game-api:test --no-configuration-cache
```

Expected: equipment rules/service tests and character calculator tests pass.

- [ ] **Step 2: Run contract and web verification**

```bash
corepack pnpm --filter @hanjjak/contracts test
corepack pnpm --filter @hanjjak/web test
corepack pnpm --filter @hanjjak/web typecheck
corepack pnpm --filter @hanjjak/web build
```

Expected: all commands exit zero.

- [ ] **Step 3: Restart the API and verify Flyway V23 against PostgreSQL**

Expected: application readiness succeeds, `equipment_slot_state` exists, legacy `equipment_instance` and `equipment_loadout` do not exist, and account/wallet/inventory/cosmetics data remain present.

- [ ] **Step 4: Exercise authenticated HTTP behavior**

With a local test account and seeded development balances, verify:

1. `GET /api/v1/equipment` returns six locked cards with Q=0 previews.
2. `POST /WEAPON/unlock` consumes exactly one unlock cost and returns normal +1.
3. Replaying the same key returns the same result without another debit; a new key returns `EQUIPMENT_ALREADY_UNLOCKED`.
4. `POST /WEAPON/enhance` consumes the returned server-authored cost and advances exactly one level.
5. Promotion before +30 returns the level gate error; +30 without chapter clear returns the chapter gate error; adding the first-clear row permits promotion to rare +1/Q40 with exact promotion debit.
6. A reused key with another command returns `IDEMPOTENCY_KEY_REUSED`.

- [ ] **Step 5: Verify the actual browser surface**

Open the equipment screen in Chromium. Confirm six cards, locked/unlocked layouts, exact available/required material rows, enhancement preview, chapter gate, promotion preview, terminal copy, disabled reasons, and character-stat refresh after a successful command. Check desktop and narrow viewport layouts.

- [ ] **Step 6: Finalize evidence and commit**

Only claims actually observed in Steps 1–5 may be promoted to verification complete. Retain partial verification for any unexercised terminal or failure path.

```bash
git add docs/wiki/06-delivery/tasks
git commit -m "docs: record permanent equipment implementation"
```
