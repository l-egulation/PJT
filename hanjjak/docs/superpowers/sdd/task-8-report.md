# Task 8 report — staged raid settlement

## Scope
Implemented KST 17:30 session rotation, fenced settlement, immutable account worksets, restartable account finalization, rank materialization, terminal success/failure, and tie-safe final rank reads. Task 9 controllers and Task 10 web were not changed.

## Implementation evidence
- `modules/raid/src/main/kotlin/com/hanjjak/raid/application/RaidSettlementService.kt`
- `modules/raid/src/main/kotlin/com/hanjjak/raid/domain/RaidModels.kt`
- `modules/raid/src/main/kotlin/com/hanjjak/raid/application/RaidPorts.kt`
- `modules/raid/src/main/kotlin/com/hanjjak/raid/infrastructure/JdbcRaidRepository.kt`
- `modules/raid/src/main/kotlin/com/hanjjak/raid/api/RaidConfiguration.kt`
- `apps/game-api/src/main/resources/db/migration/V58__raid_settlement_phases.sql`

The persisted phases are `ROTATE_SESSION`, `FREEZE_WORKSET`, `FINALIZE_ACCOUNTS`, `MATERIALIZE_RANKS`, and `SETTLED`. Workset rows are unique and account finalization uses account-first locking. Rank rows and rank claims use deterministic source IDs and database uniqueness for restart idempotency.

## Verification
Executed from the ASCII `Y:` drive:

- `Y:\gradlew.bat :modules:raid:test --tests com.hanjjak.raid.application.RaidSettlementServiceTest` — BUILD SUCCESSFUL; 5 tests.
- `Y:\gradlew.bat :modules:raid:test --tests com.hanjjak.raid.infrastructure.JdbcRaidRepositoryTest` — BUILD SUCCESSFUL; 23 tests.
- `Y:\gradlew.bat :apps:game-api:test --tests com.hanjjak.gameapi.FlywayMigrationVersionTest :modules:raid:test --tests com.hanjjak.raid.infrastructure.JdbcRaidRepositoryTest` — BUILD SUCCESSFUL.
- `Y:\gradlew.bat :apps:game-api:test --tests com.hanjjak.gameapi.RaidSettlementIntegrationTest` — BUILD SUCCESSFUL; PostgreSQL workset smoke test.
- `Y:\gradlew.bat :apps:game-api:compileKotlin :apps:game-api:compileTestKotlin` — BUILD SUCCESSFUL.

Initial RED was captured before service creation: focused settlement tests failed to compile with unresolved `RaidSettlementService` and its boundary/ranking API. After implementation, the focused unit suite passed.

## Remaining verification boundary
Full crash/restart, multi-scheduler, reward-capacity fallback, and HTTP contract coverage remain in the broader Task 8/Task 9 integration selection; this report only claims the focused and PostgreSQL smoke evidence above.

## 2026-09-14 recovery verification

The interrupted session left a reviewed follow-up patch in the worktree. The patch removes permissive repository defaults, makes the settlement collaborators explicit Spring beans so transaction proxy boundaries are real, backfills V58 phase fields for pre-existing rows, and strengthens cutoff, completion, and concurrent-scheduler coverage.

- `:modules:raid:test --tests com.hanjjak.raid.application.RaidSettlementServiceTest` — `BUILD SUCCESSFUL`.
- `:apps:game-api:compileKotlin :apps:game-api:compileTestKotlin` — `BUILD SUCCESSFUL`.
- The full `:modules:raid:test` application tests passed, while its nine JDBC tests could not initialize because Docker Desktop was not running: Testcontainers reported `Could not find a valid Docker environment` before repository assertions.
- `RaidSettlementIntegrationTest` was blocked at the same environment boundary in `GameApiIntegrationTestInitializer`; no raid assertion executed.
- `git diff --check` reported no whitespace errors; only Git's existing LF-to-CRLF warning for V58 appeared.

PostgreSQL behavior therefore retains the earlier successful Task 8 evidence above, while this recovered follow-up is freshly compile- and unit-verified but not freshly Docker-verified.

## Task review paging fix

Independent review found that one scheduler call fetched only the first pending-account page. Added a five-account regression with `pageSize=2`; before the production fix it left three accounts pending and failed at `pending.isEmpty()`. `settleOne` now drains pending pages until empty, requires monotonic finalized-count progress, and only then advances to rank materialization. The full focused `RaidSettlementServiceTest` class passed after the fix.
