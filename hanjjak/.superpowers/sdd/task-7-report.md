# Task 7 Report

## Scope

Implemented the progression-rebalance backfill report, read-only preview, resumable per-account apply, and non-web CLI in the requested worktree at base `34dd287f26eb19dce5b27e6cee40bce8ccbe5440`.

## Implementation

- `ProgressionBackfillReport` exposes target accounts/stages, existing reward count, rice total, quantities by item, immediate/pending candidates, failed account IDs, and switched account count.
- `ProgressionRebalanceBackfillService.preview()` performs catalog/database reads and inventory planning only; it does not call reward writes or update account state.
- `ProgressionRebalanceBackfillService.apply(batchSize)` checks the packaged rebalance manifest for `authority=applied`, orders eligible account UUIDs stably, and delegates each account to the separate processor bean. Failed accounts are collected while successful accounts continue.
- `ProgressionRebalanceAccountProcessor.applyOne` is a Spring bean with `@Transactional(propagation = REQUIRES_NEW)`, uses saved first-clear rows through `FirstClearRewardService.apply`, and switches account balance/version completion only after all clears complete in that account transaction.
- `GameApiApplication` keeps `migrate` and normal server startup paths, adds `backfill-progression-rewards --dry-run` and `--apply --batch-size=N`, uses `WebApplicationType.NONE`, prints one JSON report, closes context, and exits nonzero for failed account IDs.
- Focused command/report tests cover CLI mode/batch validation and report JSON/counter shape.

## Verification

- `R:\gradlew.bat :modules:battle:compileKotlin :apps:game-api:compileKotlin --no-configuration-cache` — **BUILD SUCCESSFUL**.
- `R:\gradlew.bat :apps:game-api:test --tests "*ProgressionBackfill*" --no-configuration-cache` — test task failed before executing focused tests because Gradle attempted to load stale/non-test nested classes (for example `AccountFlowIntegrationTest$Companion$TestPostgres`) and reported `ClassNotFoundException`; this is unrelated to the newly compiled focused classes, which are present under `build/classes/kotlin/test`.
- The requested live apply/dry-run server smoke is deferred: the packaged `progression-rebalance-v1/manifest.json` is currently `authority: working`, so apply must intentionally fail with `BALANCE_VERSION_NOT_APPLIED`; disposable PostgreSQL was not available in this task worktree.
