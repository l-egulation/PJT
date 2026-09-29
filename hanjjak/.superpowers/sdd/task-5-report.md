# Task 5 Report

## Scope

Added the V56 daily seal raid PostgreSQL schema, raid persistence domain records/ports, `JdbcRaidRepository`, behavioral PostgreSQL tests, and persistence/L-02 evidence. Attempt lifecycle, rewards, settlement orchestration, HTTP controllers, and web remain outside Task 5.

## TDD evidence

- RED: `JdbcRaidRepositoryTest` initially failed because `JdbcRaidRepository` did not exist.
- RED: game-api migration coverage initially failed because V56 and its raid tables did not exist.
- RED hardening: repository tests caught immutable session transitions, missing atomic workset freeze/claim transition, cross-account attempt updates caused by SQL precedence, and a lock test that did not prove the writer was blocked before commit.
- RED hardening (final persistence review): new PostgreSQL tests failed before implementation for arbitrary AUTO_PERSONAL/DAILY_RANK claim sources, slot ownership/attempt decrease/terminal reopen, and equal/stale account-state writes.

## GREEN verification

- Final ASCII `Y:` repository suite: `./gradlew.bat :modules:raid:test --tests com.hanjjak.raid.infrastructure.JdbcRaidRepositoryTest --tests com.hanjjak.raid.domain.RaidModuleBoundaryTest -Pkotlin.incremental=false --rerun-tasks --no-daemon --no-configuration-cache --max-workers=1` — `BUILD SUCCESSFUL`; focused PostgreSQL repository and boundary tests pass.
- Final ASCII `Y:` migration suite: `./gradlew.bat :apps:game-api:test --tests com.hanjjak.gameapi.FlywayMigrationVersionTest --tests com.hanjjak.gameapi.RaidMigrationTest -Pkotlin.incremental=false --rerun-tasks --no-daemon --no-configuration-cache --max-workers=1` — `BUILD SUCCESSFUL`; V1–V56 migrated on empty PostgreSQL and named source constraints/indexes executed.
- Production source compilation completed as part of both focused Gradle runs.

## Invariants covered

- Due OPEN-session discovery uses stable ordering and a row-locking repository operation; future OPEN lookup remains separate.
- One OPEN session, unique settlement instant, and independently progressing SETTLING sessions.
- OPEN has null cutoff; fenced/settled states require `cutoff_at = settles_at`. Workset rows carry the same cutoff through a composite session FK.
- Session content/reward versions and settlement instant cannot change through repository updates; status and settlement counters move monotonically.
- Reward slots belong to a session/account and their current attempt must reference that exact slot/session/account.
- Reward attempts have unique slot ordinal, mode-aware ordinal bounds, and each account has at most one RUNNING attempt across modes. Authoritative input/result/timeline/seed fields are immutable after insert; transitions are account-qualified and allow only RUNNING/RESULT_HELD terminal paths.
- Due OPEN sessions are discovered in stable order. Workset membership is frozen atomically from persisted raid-owned account rows, paged by account UUID, row-locked after the account, and finalized once.
- Confirmed results use an ownership composite FK and require the persisted authoritative result JSON.
- Contribution upserts apply atomic deltas/max values; row-lock concurrency proves blocking before commit.
- Claims expose account-scoped locks and guarded once-only transitions. Command reads return typed fingerprint/status/result records, preserving null result versus absent row.
- Competitive final ranks allow multiple accounts to share the same rank; migration tests verify named constraints/indexes rather than table count alone.

## Documentation status

L-02 remains `부분 구현` / `부분 검증`: persistence is implemented and focused-verified, while lifecycle and handoff are Task 6.

## Commit

All Task 5 changes use subject `feat(raid): add PostgreSQL persistence`; the integrating agent records the commit SHA after creation.

---

## Latest mainline first-clear completion evidence

# Task 5 Report: Versioned First-Clear Completion

## Implementation

- Added `StageCompletionService.complete(accountId, stageId, contentVersion, sourceId)` as the single successful stage-progress transition.
- Routed compatibility `/cycles` and chapter auto-run success paths through `StageCompletionService`.
- Routed battle-session completion through `StageCompletionService` using the stored session `input.contentVersion`.
- Added nullable `firstClearReward` fields to `BattleCycleResponse`, `StageRunResponse`, and `BattleSessionService.Completion`.
- Removed the obsolete private `persistClear` implementations from both controllers/services.
- Preserved existing settlement ordering, runtime state transitions, battle history recording, and domain event publication. The draft service emits `STAGE_FIRST_CLEAR_REWARD` with reward version, rice, claimed/pending items, unlocked skill, and item status.
- Kept `CycleResult`, `DomainEventPublishRequest`, and existing event contracts intact.

## Verification

- `R:\\gradlew.bat :modules:battle:compileKotlin :apps:game-api:compileTestKotlin --no-configuration-cache -Pkotlin.incremental=false` — **BUILD SUCCESSFUL**.
- Required wildcard E2E invocation was attempted, but Gradle test discovery failed before executing the requested classes because it attempted to load unrelated nested test classes that were not present on the test classpath (`ClassNotFoundException`, including `AccountFlowIntegrationTest$...`).
- A fully-qualified class invocation was also attempted; Gradle reported the requested test classes as unavailable to the test executor before test execution. No behavioral assertion result was produced.
- `git diff --check` — **clean**.
- Confirmed no `persistClear` references remain under `modules/battle`.
