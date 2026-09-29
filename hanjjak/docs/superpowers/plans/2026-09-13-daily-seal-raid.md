# Daily Seal Raid Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the approved serverwide daily seal raid, including deterministic personal combat, reward-slot retries, KST 17:30 settlement, contribution ranking, atomic rewards, API/UI, and verified content values.

**Architecture:** Add a `modules:raid` domain module to the existing Kotlin/Spring modular monolith. `sim-core` owns deterministic raid combat, immutable game-content owns raid definitions and derived fixed thresholds, PostgreSQL owns daily sessions/attempts/contributions/claims, TypeSpec owns HTTP contracts, and the React feature renders server-authoritative state. Build in four vertical stages: content simulation, domain persistence, transaction/API integration, then web and long-run verification.

**Tech Stack:** Kotlin 2.2/JVM 21, Spring Boot 3.5, Gradle Kotlin DSL, PostgreSQL 17/Flyway, Testcontainers, TypeSpec/OpenAPI, React 19, TypeScript 7, TanStack Query, Zustand, Vitest/Testing Library, Kotlin `sim-core` and `balance-lab`

## Global Constraints

- The sole normative game-rule source is `docs/30-domain/raid/ssot.md`; `docs/superpowers/specs/2026-09-13-daily-seal-raid-design.md` is rationale only.
- Current UI remains `준비 중` until the raid vertical slice and content version are fully verified; then activation follows server `unlockStageId`, initially `stage.01-02`.
- One serverwide raid session settles daily at KST 17:30 and immediately opens the next session.
- Fixed seal target is `50,000`; display caps at 100%, while confirmed contribution continues to accumulate for ranking.
- Accounts receive three sequential reward slots per session; each slot permits at most three attempts; live retry discards the current result; no-reward exit terminates that slot.
- After all three slots are terminal by confirmation or discard, unlimited practice is allowed and produces no reward, contribution, rank, or slot mutation.
- Raid combat snapshots level, equipment, skills/loadout, MAIN gem preset, cosmetic effects, content versions, seed, boss values, and grade thresholds at attempt start.
- Main battle pauses for raid and resumes from the current stage cycle start without compensating rewards.
- Raid combat ends at player death or five minutes; boss attack and defense use positive round-half-up of `initial × 1.05^floor(elapsedSeconds/5)` with escalation applied before same-tick actions.
- Grades are `PARTICIPATION, D, C, B, A, S, SS, SSS`; zero damage cannot confirm; the fixed contribution table is `0,100,167,233,300,367,433,500`.
- Reference build must reach B on at least 90% of verification seeds and target approximately 60 seconds survival.
- Ranking orders seal contribution sum, actual damage sum, then highest single confirmed damage; exact ties share competitive rank. Public ranking includes every account with competitive rank <=100 plus the caller.
- Personal and rank reward tables must exactly match the raid SSOT. Failure rewards apply 70% to each final successful integer quantity, then positive round-half-up; 101+ failure is `6 tickets, 4 gem boxes, 5,600 rice`.
- Tickets, rice, and gem boxes are one atomic reward bundle. Capacity validates only the gem-box inventory component; integer overflow/credit failure for ticket/rice blocks or defers the whole bundle.
- Manual personal confirmation rejects with no state change when the full bundle cannot be credited. Boundary auto-confirm stores a non-expiring whole-bundle personal claim when immediate credit fails. Rank rewards are always non-expiring per-session claims.
- All state-changing commands require UUIDv4 idempotency keys. PostgreSQL transaction boundaries own rewards, contributions, slots, settlement, claims, account state version, ledger, and Outbox.
- Do not use mail for raid rewards. Do not add offline/main-battle compensation, dynamic seal targets, guild/group raids, multiple result selection, or reward-point currency.
- TypeSpec is the sole HTTP and raid-content schema source. Add pinned `@typespec/json-schema` 1.15.0 beside the existing TypeSpec 1.15.0 packages, emit committed raid JSON Schema, and keep generated client/server source out of scope because this repository has no such emitter pipeline.
- Kotlin `sim-core` is authoritative. The current repository has no separate TypeScript combat predictor; web replay consumes the server-authored rendering timeline and does not independently confirm outcomes.
- Any Flyway migration added after current main uses the next unused version discovered at execution time; never assume the plan-time version number and never rename an applied migration.
- Every implementation task updates its connected `docs/wiki/06-delivery/tasks/<group>/<code>.md` evidence and both delivery indexes. If no raid task exists, create the task before implementation and keep policy out of task/index docs.

---

## File Map

### New module and simulation

- `modules/raid/build.gradle.kts`: raid domain dependencies and test setup.
- `modules/raid/src/main/kotlin/com/hanjjak/raid/domain/RaidRules.kt`: grades, contributions, rewards, session-time and slot invariants.
- `modules/raid/src/main/kotlin/com/hanjjak/raid/domain/RaidModels.kt`: session, account slot, attempt result, contribution, claim models.
- `modules/raid/src/main/kotlin/com/hanjjak/raid/application/RaidService.kt`: transactional use cases and settlement orchestration.
- `modules/raid/src/main/kotlin/com/hanjjak/raid/application/RaidPorts.kt`: repositories and cross-module reward/combat ports.
- `modules/raid/src/main/kotlin/com/hanjjak/raid/infrastructure/JdbcRaidRepository.kt`: PostgreSQL implementation.
- `modules/raid/src/main/kotlin/com/hanjjak/raid/api/RaidController.kt`: handwritten Spring HTTP adapter matching generated OpenAPI.
- `modules/raid/src/main/kotlin/com/hanjjak/raid/api/RaidConfiguration.kt`: module wiring and scheduled settlement entry.
- `packages/sim-core/src/main/kotlin/com/hanjjak/sim/RaidSimulator.kt`: deterministic five-minute raid combat.
- `packages/sim-core/src/test/kotlin/com/hanjjak/sim/RaidSimulatorTest.kt`: escalation ordering and deterministic result tests.
- `apps/balance-lab/src/main/kotlin/com/hanjjak/balancelab/RaidBalance.kt`: boss/threshold derivation and economy projections.

### Content and contracts

- `packages/contracts/raid-content.tsp`: raid content schema source annotated for JSON Schema emission.
- `packages/contracts/package.json`, `packages/contracts/tspconfig.yaml`: pin/configure `@typespec/json-schema` 1.15.0 alongside OpenAPI emission.
- `packages/game-content/schema/raids.schema.json`: generated committed raid content schema; TypeSpec emitter config writes this exact filename.
- `packages/game-content/versions/v1/raids/raids.json`: immutable working/applied raid definition.
- `packages/game-content/versions/v1/manifest.json`: raid file registration and checksum.
- `tools/content-validator/src/validate.ts`: raid-specific cross-field and manifest validation beyond JSON Schema.
- `tools/content-validator/src/validate.test.ts`: invalid raid content and checksum regression tests.
- `packages/contracts/raid.tsp`: query, command, event and error contracts.
- `packages/contracts/main.tsp`: raid contract import.
- `packages/contracts/generated/openapi/openapi.yaml`: regenerated OpenAPI artifact.

### Persistence and API integration

- `apps/game-api/src/main/resources/db/migration/V<next>__daily_seal_raid.sql`: session, account state, attempt, contribution, command and claim tables.
- `apps/game-api/build.gradle.kts`, `settings.gradle.kts`: register and consume `modules:raid`.
- `apps/game-api/src/main/kotlin/com/hanjjak/gameapi/character/CharacterStatsService.kt`: expose raid snapshot port without duplicating formulas.
- `apps/game-api/src/main/kotlin/com/hanjjak/gameapi/raid/RaidAdapters.kt`: composition-root adapters implementing raid-owned snapshot, reward, progress and main-battle ports with existing module services/JDBC.
- Existing inventory `InventoryReservationService.grantStackOrReject`, wallet repository/service, cosmetics ticket state, stage progress and battle session services remain owned by their modules; `RaidAdapters` coordinates them without adding reverse dependencies on `modules:raid`.

### Web

- `apps/web/src/features/raid/api.ts`: handwritten feature fetch wrapper and DTOs validated against generated OpenAPI.
- `apps/web/src/features/raid/RaidScreen.tsx`: lobby, slots, current result, claims, TOP100+me.
- `apps/web/src/features/raid/RaidBattle.tsx`: deterministic event playback and HUD.
- `apps/web/src/features/raid/raid.css`: raid layout and states.
- `apps/web/src/features/battle/BattleHud.tsx`: data-driven raid navigation activation.
- `apps/web/src/main.tsx` or current route owner: raid overlay/screen route.
- `apps/web/src/features/raid/*.test.tsx`: lobby, attempt, claim and ranking behavior.

### Documentation and delivery

- `docs/60-content/raids/mvp-v1.md`: derived boss/threshold/content values and status.
- `docs/wiki/06-delivery/tasks/<raid-group>/<codes>.md`: task state and evidence.
- `docs/wiki/06-delivery/tasks/_index.md`: task discoverability/status.
- `docs/30-domain/raid/features/shared-boss.md`: task links only.
- `docs/80-decisions/README.md`: working content result and rollout propagation.
- `docs/30-domain/player/ux/ssot.md`: activate raid route only after vertical-slice verification.

---

### Task 1: Create Raid Delivery Tasks and Module Boundary

**Files:**
- Create: `modules/raid/build.gradle.kts`
- Modify: `settings.gradle.kts`
- Create: `modules/raid/src/main/kotlin/com/hanjjak/raid/domain/RaidModels.kt`
- Create: `modules/raid/src/main/kotlin/com/hanjjak/raid/application/RaidPorts.kt`
- Create: `modules/raid/src/test/kotlin/com/hanjjak/raid/domain/RaidModuleBoundaryTest.kt`
- Create: `docs/wiki/06-delivery/tasks/L-raid/_index.md`
- Create: `docs/wiki/06-delivery/tasks/L-raid/l-01-raid-content-simulation.md`
- Create: `docs/wiki/06-delivery/tasks/L-raid/l-02-raid-session-attempt-persistence.md`
- Create: `docs/wiki/06-delivery/tasks/L-raid/l-03-raid-settlement-ranking-rewards.md`
- Create: `docs/wiki/06-delivery/tasks/L-raid/l-04-raid-api-web-vertical-slice.md`
- Modify: `docs/wiki/06-delivery/tasks/_index.md` to extend the A–K convention to A–L.
- Modify: `docs/30-domain/raid/features/shared-boss.md`

**Interfaces:**
- Produces: `RaidClock`, `RaidCombatSnapshotProvider`, `RaidRewardPort`, `RaidMainBattlePort`, `RaidProgressPort`, and repository interfaces used by later tasks.
- Produces: `RaidSessionId`, `RaidAttemptId`, `RaidClaimId` inline/value types or UUID-backed domain identifiers following repository conventions.
- Non-goal: no controller, DB table, reward mutation or runnable placeholder service.

- [ ] **Step 1: Write the failing boundary test**

Create a Kotlin reflection test that loads representative classes from `com.hanjjak.raid.domain` and asserts their declared field and method types do not reference Spring, JDBC, API or infrastructure packages. Do not add ArchUnit because the repository does not currently use it.

```kotlin
@Test
fun `raid domain public signatures exclude spring jdbc and adapters`() {
    assertNoForbiddenTypeReferences(RaidSession::class, RaidAttemptResult::class)
}
```
- [ ] **Step 2: Run the focused test and capture RED**

```bash
./gradlew.bat :modules:raid:test --tests com.hanjjak.raid.domain.RaidModuleBoundaryTest -Pkotlin.incremental=false --no-daemon --no-configuration-cache --max-workers=1
```

Expected: FAIL because the module and boundary do not exist.

- [ ] **Step 3: Add the module, minimal domain state types, and application ports**

Define only contracts needed by later tasks. The core state shape must include:

```kotlin
enum class RaidSessionStatus { OPEN, SETTLING, SETTLED_SUCCESS, SETTLED_FAILURE }
enum class RaidSlotStatus { AVAILABLE, ACTIVE, RESULT_HELD, CONFIRMED, DISCARDED, EXPIRED }
enum class RaidAttemptStatus { RUNNING, RESULT_HELD, DISCARDED, CONFIRMED }
enum class RaidClaimKind { AUTO_PERSONAL, DAILY_RANK }
enum class RaidClaimStatus { CLAIMABLE, CLAIMED }

data class RaidRewardBundle(val cosmeticTickets: Long, val gemBoxes: Long, val rice: Long)
data class RaidAttemptResult(val damage: Long, val grade: RaidGrade, val sealContribution: Int, val endedAt: Instant)
```

- [ ] **Step 4: Add delivery task records and routing links**

Each task owns implementation status and evidence, links to `docs/30-domain/raid/ssot.md`, and contains no duplicated policy numbers. Add L-01 through L-04 to `L-raid/_index.md`, the root A–L delivery index, and `shared-boss.md`.

- [ ] **Step 5: Run focused module test and documentation link checks**

Expected: module compiles; boundary test passes; every new relative Markdown link resolves.
- [ ] **Step 6: Commit**

```bash
git add settings.gradle.kts modules/raid docs/wiki/06-delivery/tasks/L-raid docs/wiki/06-delivery/tasks/_index.md docs/30-domain/raid/features/shared-boss.md
git commit -m "feat(raid): establish module and delivery boundary"
```

---

### Task 2: Implement Deterministic Raid Combat

**Files:**
- Create: `packages/sim-core/src/main/kotlin/com/hanjjak/sim/RaidSimulator.kt`
- Create: `packages/sim-core/src/test/kotlin/com/hanjjak/sim/RaidSimulatorTest.kt`
- Create: `packages/sim-core/src/test/resources/golden/raid-combat-v1.json`
- Create: `apps/web/src/features/raid/raidTimeline.ts`
- Create: `apps/web/src/features/raid/raidTimeline.test.ts`

**Interfaces:**
- Produces:

```kotlin
data class RaidCombatInput(
    val contentVersion: String,
    val seed: Long,
    val player: FighterStats,
    val skills: SkillProfile,
    val bossInitialAttack: Int,
    val bossInitialDefense: Int,
    val maxTicks: Int = 3_000,
)

data class RaidCombatResult(
    val elapsedTicks: Int,
    val totalDamage: Long,
    val playerDied: Boolean,
    val events: List<RaidCombatEvent>,
)

object RaidSimulator {
    fun simulate(input: RaidCombatInput): RaidCombatResult
}
```

- Consumes: current main-combat attack timing, skill behavior, crit RNG and damage multiplier primitives from `sim-core`; do not duplicate formulas.
- Invariant: tick duration remains the existing 100ms convention, so 5 seconds = 50 ticks and five minutes = 3,000 ticks.

- [ ] **Step 1: Write failing deterministic and boundary tests**

Cover exact tick order:
```kotlin
@Test fun `escalation applies before actions at tick fifty`() {
    val result = RaidSimulator.simulate(escalationFixture())
    val stageOne = result.events.first { it.logicalTick == 50 && it.type == RaidCombatEventType.ESCALATION }
    val sameTickActions = result.events.filter { it.logicalTick == 50 && it.type != RaidCombatEventType.ESCALATION }
    assertTrue(sameTickActions.all { it.sequence > stageOne.sequence })
}

@Test fun `raid hard stops at three thousand ticks`() {
    val result = RaidSimulator.simulate(survivingFixture(maxTicks = 3_000))
    assertEquals(3_000, result.elapsedTicks)
    assertFalse(result.playerDied)
    assertEquals(RaidCombatEventType.TIME_LIMIT_REACHED, result.events.last().type)
}

@Test fun `same input and seed replay exactly`() {
    val input = criticalFixture(seed = 42)
    assertEquals(RaidSimulator.simulate(input), RaidSimulator.simulate(input))
}
```

Add a death test asserting the terminal event is `PLAYER_DIED` and two fixed-seed crit fixtures asserting different total damage. Use concrete fixture constructors in the same test file; do not leave placeholder bodies.

- [ ] **Step 2: Run focused tests and capture RED**

```bash
./gradlew.bat :packages:sim-core:test --tests com.hanjjak.sim.RaidSimulatorTest -Pkotlin.incremental=false --no-daemon --no-configuration-cache --max-workers=1
```

Expected: FAIL because raid simulator types do not exist.

- [ ] **Step 3: Implement raid simulator with existing combat primitives**

Use integer logical ticks. Compute escalation stage as `elapsedTicks / 50`. Reuse `CombatSimulator.damage`, skill timing and RNG primitives rather than copying formulas. The boss is an infinite-health target; player death or tick 3,000 terminates the result. Do not materialize per-tick events when no visible action occurs.

- [ ] **Step 4: Add server-generated golden rendering fixtures and web playback coverage**

Generate one golden raid result and rendering timeline at `packages/sim-core/src/test/resources/golden/raid-combat-v1.json` from Kotlin `RaidSimulator`. Web `raidTimeline.test.ts` reads that same repository file through the existing Vitest/Node filesystem path and verifies event ordering/timestamps and terminal rendering. Do not copy a second fixture. The web must not recalculate damage, crits, grades or death.

- [ ] **Step 5: Run Kotlin simulator and web playback tests**

Run the existing sim-core Kotlin suite and focused web raid timeline test/typecheck. Expected: Kotlin-generated result and web playback timeline match exactly; the web schedules the server-authored timeline and does not author a competing result.

- [ ] **Step 6: Update L-01 evidence and commit**

```bash
git add packages/sim-core apps/web/src/features/raid/raidTimeline.ts apps/web/src/features/raid/raidTimeline.test.ts docs/wiki/06-delivery/tasks/L-raid/l-01-raid-content-simulation.md docs/wiki/06-delivery/tasks/L-raid/_index.md docs/wiki/06-delivery/tasks/_index.md
git commit -m "feat(raid): add deterministic personal combat"
```


### Task 3: Derive Boss and Grade Content Values

**Files:**
- Create: `apps/balance-lab/src/main/kotlin/com/hanjjak/balancelab/RaidBalance.kt`
- Create: `apps/balance-lab/src/test/kotlin/com/hanjjak/balancelab/RaidBalanceTest.kt`
- Create: `apps/balance-lab/src/main/resources/raid-reference-build-v1.json`
- Modify: `apps/balance-lab/build.gradle.kts` to register `raidBalance` with main class `com.hanjjak.balancelab.RaidBalanceKt`.
- Create: `docs/60-content/raids/mvp-v1.md`
- Create: `packages/game-content/versions/v1/raids/raids.json`
- Create: `packages/contracts/raid-content.tsp`
- Modify: `packages/contracts/package.json`
- Modify: `packages/contracts/tspconfig.yaml`
- Generate: `packages/game-content/schema/raids.schema.json`
- Modify: `packages/game-content/versions/v1/manifest.json`
- Modify: `tools/content-validator/src/validate.ts`
- Modify: `tools/content-validator/src/validate.test.ts`

**Interfaces:**
- Consumes: `RaidSimulator.simulate`, production character/equipment/skill/gem/cosmetic content, and `docs/30-domain/raid/ssot.md` invariants.
- Produces a `RaidContent` document with nonzero `bossInitialAttack`, `bossInitialDefense`, and exact positive strictly increasing D/C/B/A/S/SS/SSS thresholds. It also carries the approved session, contribution and reward tables.

- [ ] **Step 1: Write failing content and simulation tests**

```kotlin
assertTrue(referenceBuildResults.count { it.totalDamage >= bThreshold } >= 900)
assertTrue(referenceBuildMedianSurvival in 550..650) // 55-65 seconds
assertStrictlyIncreasing(thresholds)
assertEquals(roundHalfUp(bThreshold * 0.35), thresholds[D])
assertEquals(roundHalfUp(bThreshold * 4.50), thresholds[SSS])
```

Add deficient-build comparisons for attack, HP and penetration. Each deficiency must reduce a distinct observable: early DPS, survival time, or late-stage damage efficiency. Reject any candidate where one deficiency has no measurable loss.

- [ ] **Step 2: Run candidate derivation tests and capture RED**

Expected: FAIL because no raid balance tool, reference input or content exists.

- [ ] **Step 3: Implement deterministic search for initial stats and B threshold**

Use checked-in `raid-reference-build-v1.json` with exact production content IDs and resolved level/equipment/skill/MAIN-gem/cosmetic values captured from the `stage.01-02` normal-growth baseline. Use seeds `1L..1000L`, bounded positive integer candidate ranges fixed in `RaidBalanceTest`, minimize absolute median survival distance from 600 ticks subject to B success >=900 and all three deficiency gates, then choose the lexicographically smallest `(initialAttack, initialDefense, bThreshold)` on ties. Emit `build/reports/balance/raid-mvp-v1-working.json`; do not hand-edit derived values.

- [ ] **Step 4: Generate raid JSON Schema and add validator rules**

Add `@typespec/json-schema` 1.15.0 and configure emission for `RaidContent` to `packages/game-content/schema/raids.schema.json`; do not edit it manually. Keep strict threshold ordering, known stage reference, reward-table equality, manifest registration and checksum integrity in `tools/content-validator/src/validate.ts`. Add a drift check that recompiles TypeSpec and fails if the committed schema changes.

- [ ] **Step 5: Run balance, content generation and validation**

Run `./gradlew.bat :apps:balance-lab:test :apps:balance-lab:raidBalance -Pkotlin.incremental=false --no-daemon --no-configuration-cache --max-workers=1`, then `corepack pnpm --filter @hanjjak/contracts typecheck`, `corepack pnpm --filter @hanjjak/contracts build`, `corepack pnpm --filter @hanjjak/content-validator test`, `corepack pnpm --filter @hanjjak/content-validator validate`, and a clean generated-artifact diff check. Expected: 1,000-seed criteria pass and artifacts are reproducible.

- [ ] **Step 6: Record working/applied status and commit**

Keep `docs/60-content/raids/mvp-v1.md` as `working` unless all reference and deficient-build gates pass; promote only with exact report evidence. Update L-01, both task indexes, source-map content ownership and decision status in the same commit.

---

### Task 4: Define TypeSpec Raid Contracts

**Files:**
- Create: `packages/contracts/raid.tsp`
- Modify: `packages/contracts/main.tsp`
- Regenerate: `packages/contracts/generated/openapi/openapi.yaml`
- Test: TypeSpec compilation and generated OpenAPI diff review.

**Interfaces:**
- Queries:
  - `GET /api/v1/raid`
  - `GET /api/v1/raid/ranking?cursor=&limit=`
  - `GET /api/v1/raid/claims`
  - `GET /api/v1/raid/attempts/current`
- Commands:
  - `POST /api/v1/raid/attempts`
  - `POST /api/v1/raid/attempts/{attemptId}/retry`
  - `POST /api/v1/raid/attempts/{attemptId}/confirm`
  - `POST /api/v1/raid/attempts/{attemptId}/discard`
  - `POST /api/v1/raid/claims/{claimId}`
- Attempt-start body contains mode `REWARD|PRACTICE` only. No client damage, grade, contribution, reward, seed, account or rank fields.

- `RaidAttemptView` includes `tickDurationMilliseconds`, `durationMilliseconds`, `completableAt`, authoritative current/terminal result and `renderingTimeline`.
- Define closed `RaidCombatEventType` values for player action/impact, boss hit, escalation, player death and time-limit end. Each event includes `sequence`, `logicalTick`, nullable actor/target, damage, critical, HP before/after, escalation stage/attack/defense as applicable.
- Start, retry and current-attempt responses return the same persisted server-authored input/result/timeline; confirm/discard responses do not accept or trust a client timeline.

- [ ] **Step 1: Add failing contract assertions**

Compile-time/schema tests assert stable enums, UUID path types, non-negative int64 counters, `currentRank` nullable for ineligible users, `topEntries` may exceed 100 because all competitive rank-100 ties are included, and every command requires standard idempotency headers.

- [ ] **Step 2: Run contract build and capture RED**

```bash
corepack pnpm --filter @hanjjak/contracts typecheck
corepack pnpm --filter @hanjjak/contracts build
```

Expected: FAIL until raid contract is imported.

- [ ] **Step 3: Implement complete DTO and error contract**

Required domain errors include locked content, no available slot, attempt running/result held, attempt limit reached, invalid transition, zero damage cannot confirm, reward capacity, stale/settled session, main battle transition failure, claim not found/already claimed, state-version conflict and reused idempotency key.

Controllers and web types remain handwritten in this feature but must be shape-checked against generated OpenAPI in compile/integration tests. Do not claim or add generated client/server code.

- [ ] **Step 4: Regenerate OpenAPI and run breaking checks**

Regenerate OpenAPI only; do not edit it manually. Run TypeSpec typecheck/build and a clean diff regeneration check. Verify all existing API paths and schemas remain backward compatible.

- [ ] **Step 5: Update L-04 evidence and commit**

---

### Task 5: Add PostgreSQL Raid Persistence

**Files:**
- Create: `apps/game-api/src/main/resources/db/migration/V<next>__daily_seal_raid.sql`
- Create: `modules/raid/src/main/kotlin/com/hanjjak/raid/infrastructure/JdbcRaidRepository.kt`
- Create: `modules/raid/src/test/kotlin/com/hanjjak/raid/infrastructure/JdbcRaidRepositoryTest.kt`
- Modify: `docs/50-architecture/persistence.md`
- Modify: `docs/wiki/06-delivery/tasks/L-raid/l-02-raid-session-attempt-persistence.md`
- Modify: `docs/wiki/06-delivery/tasks/L-raid/_index.md`
- Modify: `docs/wiki/06-delivery/tasks/_index.md`

**Interfaces:**
- Tables logically cover:
  - `raid_session`: unique `settles_at`, content/reward version, status `OPEN|SETTLING|SETTLED_SUCCESS|SETTLED_FAILURE`, `cutoff_at`, batch cursor/counts.
  - `raid_account_state`: one per session/account, terminal-slot and auto-finalization progress.
  - `raid_settlement_account`: immutable per-cutoff account workset with `PENDING|FINALIZED` progress.
  - `raid_reward_slot`: one row per session/account/slot ordinal with status, attempts started and current attempt.
  - `raid_attempt`: reward/practice mode, slot reference/attempt ordinal, snapshot/result/timeline, `completable_at`, terminal status.
  - `raid_confirmed_result`: append-only confirmed damage/grade/contribution/reward version.
  - `raid_contribution`: one aggregate per session/account.
  - `raid_final_rank`: immutable settled competitive rank and hidden tie keys.
  - `raid_reward_claim`: one whole bundle per auto-personal result or daily-rank settlement.
  - `raid_command_record`: account/idempotency fingerprint/result.
- Required uniqueness:
  - one session per `settles_at` independent of content version; at most one `OPEN` session, while multiple older sessions may be `SETTLING`.
  - one account state per session/account and one slot per `(session, account, ordinal)`.
  - one attempt per `(slot_id, attempt_ordinal)` for reward attempts.
  - at most one `RUNNING` attempt per account across reward and practice modes.
  - one contribution aggregate and final rank per session/account.
  - one claim per source kind/source ID/account.
  - one command result per account/idempotency key.

- [ ] **Step 1: Discover the next unused Flyway version and write failing migration tests**

Use the full migration directory. Add `FlywayMigrationVersionTest` expectation for uniqueness and a Testcontainers test that migrates empty PostgreSQL and verifies constraints.

- [ ] **Step 2: Run migration tests and capture RED**

Expected: FAIL because tables/repository do not exist.

- [ ] **Step 3: Implement migration with explicit checks and indexes**

Use UUID identifiers, `timestamptz`, `jsonb` snapshots/results/timelines, non-negative bigint constraints, enumerated string checks, FK ownership and partial unique indexes for the sole `OPEN` session, running attempts and claimable records. Allow multiple independently progressing `SETTLING` sessions. Store immutable content/reward versions on sessions/results/claims. `raid_reward_slot` owns slot terminal history; attempts reference a slot only in reward mode.

Do not store static reward/grade definitions as mutable DB tables.

- [ ] **Step 4: Implement lock/read/write repository operations**

Account commands use one global lock order compatible with existing services:

```text
account -> raid_session -> raid_account_state -> raid_reward_slot -> raid_attempt -> raid_contribution -> wallet -> cosmetic ticket -> inventory stack -> raid_reward_claim
```

Settlement first fences `raid_session` in its own short transaction, then processes accounts in UUID ascending order using the same account-first suffix without holding the session row lock across account work. Adapter methods used inside raid transactions must have caller-lock variants that do not acquire a conflicting account lock or bump `stateVersion` themselves. Repository methods support `FOR UPDATE` for session/account/slot/attempt/claim and append-only confirmed results.

- [ ] **Step 5: Run repository, migration uniqueness and Spring context tests**

Expected: empty-DB migration, constraints, row locking and current-state queries pass.

- [ ] **Step 6: Update persistence SSOT and L-02 evidence; commit**

---

### Task 6: Implement Attempt Lifecycle and Main-Battle Handoff
**Files:**
- Modify: `modules/raid/src/main/kotlin/com/hanjjak/raid/application/RaidService.kt`
- Create: `modules/raid/src/main/kotlin/com/hanjjak/raid/application/RaidAttemptTerminalizer.kt`
- Modify: `modules/raid/src/main/kotlin/com/hanjjak/raid/application/RaidPorts.kt`
- Modify: `modules/raid/src/main/kotlin/com/hanjjak/raid/api/RaidConfiguration.kt`
- Create: `apps/game-api/src/main/kotlin/com/hanjjak/gameapi/raid/RaidAdapters.kt`
- Modify: `apps/game-api/src/main/kotlin/com/hanjjak/gameapi/character/CharacterStatsService.kt`
- Test: `modules/raid/src/test/kotlin/com/hanjjak/raid/application/RaidAttemptServiceTest.kt`
- Test: `apps/game-api/src/test/kotlin/com/hanjjak/gameapi/RaidAttemptIntegrationTest.kt`

**Interfaces:**

```kotlin
fun startAttempt(accountId: UUID, commandId: UUID, mode: RaidAttemptMode): RaidAttemptView
fun retryAttempt(accountId: UUID, commandId: UUID, attemptId: UUID): RaidAttemptView
fun discardAttempt(accountId: UUID, commandId: UUID, attemptId: UUID): RaidStateView
fun currentAttempt(accountId: UUID): RaidAttemptView?
```

- [ ] **Step 1: Write failing lifecycle tests**

Cover unlock, sequential slots, maximum attempts, live retry, live confirm from an elapsed timeline prefix, held-result retry, final-slot discard, practice eligibility after any terminal mix, snapshot immutability, main battle pause/resume, disconnected completion, stale session and idempotent replay.

- [ ] **Step 2: Run focused tests and capture RED**

- [ ] **Step 3: Implement start/retry/discard with a single state transition table**

Do not scatter transition conditions across controller/repository. Model allowed transitions explicitly and keep command fingerprints stable.

Starting raid acquires the account lock, aborts/pauses the active main battle through a caller-lock adapter, then creates the raid attempt. If pause fails, create no raid attempt. Retry terminalizes/discards the old attempt and creates the next attempt in the same transaction with a new server seed. Final discard resumes main battle and advances the slot. Practice never mutates reward slots.

- [ ] **Step 4: Implement deterministic disconnected completion**

At attempt start, simulate once and persist the complete input snapshot, authoritative result, rendering timeline and `completable_at`; status remains `RUNNING` only until wall-clock eligibility. `RaidAttemptTerminalizer` first reads a bounded page of due `(accountId, attemptId)` keys without retaining row locks, sorts by account UUID, then processes each through the shared account-first one-attempt transaction: lock account, re-read/lock the still-running attempt, change it to `RESULT_HELD`, and resume main battle. Query only observes or opportunistically invokes that same transaction. Retrying a held result pauses main battle again in the new-attempt transaction. The terminalizer never holds an attempt lock while acquiring an account lock.

- [ ] **Step 5: Run unit and PostgreSQL integration tests**

- [ ] **Step 6: Update L-02 evidence and commit**

---

### Task 7: Implement Atomic Confirmation and Claims

**Files:**
- Modify: `modules/raid/src/main/kotlin/com/hanjjak/raid/application/RaidService.kt`
- Modify: `modules/raid/src/main/kotlin/com/hanjjak/raid/application/RaidPorts.kt` with caller-lock reward methods.
- Modify: `apps/game-api/src/main/kotlin/com/hanjjak/gameapi/raid/RaidAdapters.kt`
- Modify: `modules/wallet/src/main/kotlin/com/hanjjak/wallet/application/WalletService.kt` to expose checked credit with `bumpStateVersion=false`.
- Test: `modules/raid/src/test/kotlin/com/hanjjak/raid/application/RaidRewardServiceTest.kt`
- Test: `apps/game-api/src/test/kotlin/com/hanjjak/gameapi/RaidRewardIntegrationTest.kt`

**Interfaces:**

```kotlin
fun confirmAttempt(accountId: UUID, commandId: UUID, attemptId: UUID): RaidConfirmationResult
fun claimReward(accountId: UUID, commandId: UUID, claimId: UUID): RaidClaimResult

interface RaidRewardPort {
    fun preflightLocked(accountId: UUID, bundle: RaidRewardBundle)
    fun creditLocked(accountId: UUID, sourceId: UUID, bundle: RaidRewardBundle)
}
```

`RaidPorts.kt` owns the port. `RaidAdapters.kt` implements it with direct caller-lock operations in the composition root. Do not add raid-named interfaces to inventory, wallet, cosmetics, stage or battle modules. All asset mutations join game-api's shared PostgreSQL transaction manager.


- [ ] **Step 1: Write failing reward table and transaction tests**

Exhaust all eight personal grades, four success rank bands, four failure rank bands, including `101+ failure = 6/4/5600`. Test zero-damage refusal, gem-box capacity failure with no ticket/rice/contribution mutation, ledger/ticket overflow rollback, duplicate/reused keys, concurrent confirms, whole-claim failure and concurrent claim once-only behavior.

- [ ] **Step 2: Run focused tests and capture RED**

- [ ] **Step 3: Implement canonical `RaidRules` tables**

`RaidRules` owns grade threshold lookup from immutable content, contribution and exact reward tables. Do not recalculate user-approved integer tables from percentages at runtime; validators verify the table derivation.

- [ ] **Step 4: Implement manual confirmation transaction**

Lock in the account-first order. For a `RUNNING` attempt, derive `elapsedTick = min(floor((serverNow - startedAt)/100ms), precomputedResult.elapsedTicks)`, then derive authoritative damage/grade from persisted rendering events with `logicalTick <= elapsedTick`; never accept a client checkpoint or use future events. Atomically change the attempt to `CONFIRMED`, resume main battle and use that prefix result for rewards/contribution. For `RESULT_HELD`, use the full persisted terminal result. Then run `preflightLocked`, `creditLocked`, append confirmed result, update contribution/slot, increment account state version exactly once, publish Outbox and save the command result in the same transaction. Tests cover ticks 0/49/50/terminal, concurrent terminalizer versus confirm, prefix without damage, and idempotent replay. Injected failures after every asset/contribution/Outbox write must roll back all components.

- [ ] **Step 5: Implement whole-bundle claims**

Auto-personal and daily-rank claims share the same preflight/credit path but retain claim kind/source. Credits and claimed state commit together. Claims do not gate new raid participation.

- [ ] **Step 6: Run unit, PostgreSQL and rollback tests; update L-03; commit**

---

### Task 8: Implement KST 17:30 Settlement and Ranking

**Files:**
- Create: `modules/raid/src/main/kotlin/com/hanjjak/raid/application/RaidSettlementService.kt`
- Modify: `modules/raid/src/main/kotlin/com/hanjjak/raid/api/RaidConfiguration.kt` for scheduled phase execution.
- Test: `modules/raid/src/test/kotlin/com/hanjjak/raid/application/RaidSettlementServiceTest.kt`
- Test: `apps/game-api/src/test/kotlin/com/hanjjak/gameapi/RaidSettlementIntegrationTest.kt`

**Interfaces:**

```kotlin
fun settleDueSessions(now: Instant): List<RaidSessionId>
fun ranking(accountId: UUID, sessionId: UUID, cursor: RaidRankCursor?, limit: Int): RaidRankingView
```

- [ ] **Step 1: Write failing boundary and ranking tests**

Use fixed clocks around `17:29:59.999`, `17:30:00.000`, and after. Cover running/held/zero/unstarted account states, auto-credit success, auto-personal claim fallback, unused slot expiry, seal target exact/over/under, one settlement under concurrent schedulers, next session creation, ranking tie chain, competitive rank skips, all rank-100 ties, caller outside top list, success/failure claim generation and immutable prior results.

- [ ] **Step 2: Run focused tests and capture RED**

- [ ] **Step 3: Implement crash-safe staged settlement**

Use these exact restartable phases:

0. `ROTATE_SESSION`: `rotateSession(now)` derives the current KST 17:30 interval. In one short transaction it first locks/fences every due `OPEN` session to `SETTLING` with immutable `cutoff_at=settles_at`, then inserts the current interval's `OPEN` session by unique `settles_at`. The partial unique index sees no expired OPEN row at insert time. It runs at startup, before raid commands, and on the scheduler. Older sessions may remain `SETTLING`. Test scheduler delay, process downtime and settlements spanning later cutoffs.
1. `FREEZE_WORKSET`: for each newly `SETTLING` session without a frozen set, one transaction inserts one `raid_settlement_account` row for every account with a raid account-state/slot/attempt/confirmed result/claim source in that session. Accounts with no raid interaction have no state to expire and are not included. Work rows are `PENDING|FINALIZED`; their row count is the expected count. Commands on a fenced session reject. Test zero participants and accounts created/unlocked immediately before/after cutoff.
2. `FINALIZE_ACCOUNTS`: workers read pending account IDs without locks, sort by account UUID, then process each through the global account-first transaction and lock its work row after the account. For each frozen account, materialize the persisted attempt prefix at the cutoff, auto-confirm the one eligible current result, create an auto-personal claim if immediate credit preflight fails, expire remaining existing slots, update contribution, and mark the work row finalized. Each account phase is idempotent and retryable after crash.
3. `MATERIALIZE_RANKS`: only when every frozen work row is `FINALIZED`, one transaction computes immutable competitive ranks from frozen contributions and inserts one daily-rank claim per eligible account. Unique source keys make restart idempotent.
4. `SETTLED_SUCCESS|SETTLED_FAILURE`: a final short transaction verifies work/rank/claim completeness, writes outcome and completion time. It never reruns combat, account rewards or session rotation.

Persist phase and completion markers. Test crashes/restarts before and after each phase, concurrent schedulers, confirms at the cutoff fence, zero-work sessions, deadlock-sensitive terminalizer races, and idempotent calendar rotation.
- [ ] **Step 4: Implement rank query with tie-safe cursor**

Cursor must include all ordering keys and stable account ID after the three public/hidden contribution keys. Public rank<=100 includes all ties even when row count exceeds 100. Caller result is returned separately.

- [ ] **Step 5: Run concurrency, clock and PostgreSQL tests**

- [ ] **Step 6: Update L-03 evidence and commit**

---

### Task 9: Wire Handwritten API Adapters and Game-API Integration

**Files:**
- Modify: `modules/raid/src/main/kotlin/com/hanjjak/raid/api/RaidController.kt`
- Modify: `modules/raid/src/main/kotlin/com/hanjjak/raid/api/RaidConfiguration.kt`
- Modify: `apps/game-api/src/main/kotlin/com/hanjjak/gameapi/raid/RaidAdapters.kt`
- Modify: `apps/game-api/build.gradle.kts`
- Test: `apps/game-api/src/test/kotlin/com/hanjjak/gameapi/RaidApiIntegrationTest.kt`
- Modify: `docs/50-architecture/networking.md` raid API routing only.

**Interfaces:**
- Handwritten Spring adapter matches Task 4 generated OpenAPI exactly; an integration test compares serialized response property names and required/null fields to the contract fixture.
- Query responses include server time, next settlement instant, content version, unlock state, seal progress, success-pending flag, slot/attempt state, allowed actions, caller contribution/rank, top rows and claims.
- Start/retry/current attempt include persisted server input, result and rendering timeline. Command responses include standard stateVersion/command replay behavior.

- [ ] **Step 1: Write HTTP contract integration tests**

Cover anonymous, locked, unlocked, start/retry/confirm/discard, practice, current attempt/timeline, claims, ranking ties, settled/stale commands, invalid UUID/idempotency and response envelope.

- [ ] **Step 2: Run HTTP tests and capture RED**

- [ ] **Step 3: Implement handwritten controller and composition-root adapters**

Controller maps handwritten DTOs to `RaidService` only; it contains no rule, reward, clock or rank arithmetic. `RaidAdapters` implements raid-owned ports and calls existing module services/repositories without creating reverse module dependencies.

- [ ] **Step 4: Run contract-shape, Spring context and HTTP tests**

Run TypeSpec regeneration first, then raid controller integration tests that assert JSON shape against committed contract examples, Spring bean wiring and all HTTP scenarios.

- [ ] **Step 5: Update networking routing, L-04 evidence and commit**



---

### Task 10: Implement Raid Web Vertical Slice

**Files:**
- Create: `apps/web/src/features/raid/api.ts`
- Create: `apps/web/src/features/raid/RaidScreen.tsx`
- Create: `apps/web/src/features/raid/RaidBattle.tsx`
- Create: `apps/web/src/features/raid/raidTimeline.ts`
- Create: `apps/web/src/features/raid/raid.css`
- Modify: `apps/web/src/features/battle/BattleHud.tsx`
- Modify: `apps/web/src/main.tsx`
- Test: `apps/web/src/features/raid/RaidScreen.test.tsx`
- Test: `apps/web/src/features/raid/RaidBattle.test.tsx`
- Test: `apps/web/src/features/raid/raidTimeline.test.ts`

**Interfaces:**
- Consumes handwritten raid DTOs in `api.ts` whose property names/nullability are contract-tested against Task 4 generated OpenAPI.
- Raid navigation remains disabled when API reports feature unavailable; locked state shows server `unlockStageId`; unlocked opens raid lobby.
- UI never calculates grade, reward, contribution, rank, settlement result or allowed transition.

- [ ] **Step 1: Write failing lobby tests**

Cover current seal/50,000, success-pending, KST 17:30 countdown from server instant, sequential slot and attempt counts, allowed actions, all-slots-terminal practice, claims, rank<=100 ties, caller outside TOP, loading/auth/content errors.

- [ ] **Step 2: Write failing combat HUD tests**

Cover damage, grade, next threshold, escalation stage/next boundary, 5-minute remaining, live retry, confirm, no-reward exit, third-attempt no-retry, reconnect held result and command uncertainty with same idempotency key.

- [ ] **Step 3: Run focused Vitest tests and capture RED**

- [ ] **Step 4: Implement query/mutation state and deterministic playback**

Use TanStack Query for server state and existing runtime store only for playback. A successful raid command updates raid state and invalidates inventory/auth/character/main battle as appropriate. Unknown command results offer same-key retry; known validation errors refetch authoritative state.

- [ ] **Step 5: Implement current/future navigation exposure**

Do not remove `준비 중` until server/content activation is part of this same verified vertical slice. When activated, show locked `1-2 해금` before progress and enable afterward.

- [ ] **Step 6: Browser-drive actual flow**

Against game-api + PostgreSQL + Vite: signup/login, clear unlock stage through supported test setup, enter raid, retry live, hold result, confirm, fill three terminal slots, enter practice, inspect TOP100+me, settle/claim with controlled clock fixture. Capture no console/network errors attributable to raid.

- [ ] **Step 7: Run web tests, typecheck, build; update L-04 and UX SSOT rollout status; commit**

---

### Task 11: Long-Run, Economy, and Failure Verification

**Files:**
- Create: `apps/game-api/src/test/kotlin/com/hanjjak/gameapi/RaidLongRunIntegrationTest.kt`
- Create: `apps/game-api/src/test/kotlin/com/hanjjak/gameapi/RaidSettlementConcurrencyTest.kt`
- Modify: `apps/balance-lab/src/main/kotlin/com/hanjjak/balancelab/RaidBalance.kt`
- Modify: `apps/balance-lab/src/test/kotlin/com/hanjjak/balancelab/RaidBalanceTest.kt`
- Modify: `docs/wiki/06-delivery/tasks/L-raid/l-01-raid-content-simulation.md`
- Modify: `docs/wiki/06-delivery/tasks/L-raid/l-02-raid-session-attempt-persistence.md`
- Modify: `docs/wiki/06-delivery/tasks/L-raid/l-03-raid-settlement-ranking-rewards.md`
- Modify: `docs/wiki/06-delivery/tasks/L-raid/l-04-raid-api-web-vertical-slice.md`
- Modify: `docs/wiki/06-delivery/tasks/L-raid/_index.md`
- Modify: `docs/wiki/06-delivery/tasks/_index.md`
- Modify: `docs/60-content/raids/mvp-v1.md`
- Modify: `docs/80-decisions/README.md`

**Interfaces:**
- Produces checked-in or documented reports for combat distribution, seal success, reward supply, settlement concurrency, claim backlog and performance.

- [ ] **Step 1: Add deterministic distribution verification**

At least 1,000 seeds per reference/deficient build. Report P10/P50/P90 damage and survival, grade distribution, B success rate and 5-minute cap count.

- [ ] **Step 2: Add population/economy scenarios**

Simulate DAU/participation/grade mixes including 36, 72, 144 and 1,000 active participants. Report seal success probability against 50,000; tickets, gem boxes and rice per account/day and server/day; ranking distribution; auto-claim backlog.

Flag explicitly that three participation confirmations supply `15 tickets, 9 gem boxes, 15,000 rice` before rank rewards.

- [ ] **Step 3: Add settlement and command load scenarios**

Test burst confirms before 17:30, concurrent schedulers, ranking queries, 100th-place tie fanout, replay storms and claim contention. Record transaction latency, lock wait, deadlocks/retries and duplicate count.

- [ ] **Step 4: Run migration uniqueness, affected modules, integration, web and content validation serially**

On this Windows repo, map the checkout to an ASCII drive and serialize Gradle verification lanes to avoid Kotlin cache contention. Required suites include Flyway uniqueness, raid module, sim-core, balance-lab, game-api raid integrations, contract generation, content validation, web raid tests/typecheck/build.

- [ ] **Step 5: Run actual local browser smoke**

Use a disposable PostgreSQL container on an alternate port if the existing local volume has Flyway checksum drift. Exercise reward and practice flows without modifying the user's persistent DB.

- [ ] **Step 6: Reconcile results without silently changing policy**

If combat/reference gates fail, keep raid content `working` and revise derived boss/threshold content only. If reward supply threatens existing systems, report exact projections and request user approval before changing reward tables. Do not mark delivery tasks complete on compilation alone.

- [ ] **Step 7: Final review and documentation synchronization**

Run task reviews, whole-branch review, relative link checks and `git diff --check`. Update task statuses to exact implemented/verified scope, source-map content owner, raid SSOT links and current rollout status. Remove scaffolding and temporary diagnostics.
