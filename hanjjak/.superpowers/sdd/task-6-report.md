# Task 6 report

## Scope

Implemented account-first raid attempt lifecycle, deterministic persisted combat, bounded disconnected completion, and exact one-shot main-battle handoff. Reward credit/claims, settlement/ranking, HTTP controller routes, and web remain outside Task 6.

## Implementation

- `RaidService`: start/retry/discard/current; under-lock command replay; locked OPEN-session/version fencing; sequential 3×3 reward attempts; practice gating; server snapshots and elapsed-prefix views.
- `RaidAttemptTransitions`: one explicit attempt action table for retry, discard, and terminalization.
- `RaidAttemptCompletionService`: one `REQUIRES_NEW` transaction per due key and a separate optional resume transaction, so stale game sessions cannot roll back held raid results.
- `BattleSessionService` + V57: exact paused battle identity, exact-only resume, active game-session row fence, one-shot consumed marker, and persisted `raid_attempt.resume_pending`; scheduler/live queries retry an interrupted resume until the marker clears.
- `CharacterStatsService`/`SkillService`/`RaidAdapters`: production level, FighterStats, full SkillProfile, unlocked skill IDs, active loadout, MAIN preset, and applied cosmetic source IDs flow into the immutable stored snapshot. Player defense is explicitly 0 because the current combat model has no player-defense stat.
- Public `RaidCombatInputSnapshotView` matches the TypeSpec contract; private simulator parameters, grading tables, and handoff identity remain persisted server-side rather than leaked to clients.

## TDD evidence

- RED: lifecycle types/services absent; focused tests failed compilation.
- RED review: six unit scenarios failed for event mapping, prefix damage/end time, stale due keys, bounded paging, and exact handoff propagation.
- RED integration: transaction test using a different DataSource falsely leaked state; corrected Spring-proxied test initially observed rollback/concurrency defects until one shared application DataSource and real proxy were used.
- GREEN: all final commands below passed against the current source.

## Final verification from ASCII `Y:`
- `./gradlew.bat :modules:raid:test --tests com.hanjjak.raid.application.RaidAttemptServiceTest -Pkotlin.incremental=false --rerun-tasks --no-daemon --no-configuration-cache --max-workers=1` — `BUILD SUCCESSFUL`; 14 lifecycle unit scenarios.
- `./gradlew.bat :apps:game-api:test --tests com.hanjjak.gameapi.RaidAttemptIntegrationTest -Pkotlin.incremental=false --rerun-tasks --no-daemon --no-configuration-cache --max-workers=1` — `BUILD SUCCESSFUL`; 8 real PostgreSQL lifecycle scenarios.
- `./gradlew.bat :apps:game-api:test --tests com.hanjjak.gameapi.RaidLifecycleSpringIntegrationTest -Pkotlin.incremental=false --rerun-tasks --no-daemon --no-configuration-cache --max-workers=1` — `BUILD SUCCESSFUL`; 2 Spring-proxy scenarios prove concurrent same-key replay and full pause-failure rollback.
- `./gradlew.bat :apps:game-api:test --tests com.hanjjak.gameapi.BattleSessionE2ETest -Pkotlin.incremental=false --rerun-tasks --no-daemon --no-configuration-cache --max-workers=1` — `BUILD SUCCESSFUL`; exact one-shot handoff and expired game-session no-restart behavior included.
- `./gradlew.bat :apps:game-api:test --tests com.hanjjak.gameapi.GemDungeonIntegrationTest -Pkotlin.incremental=false --rerun-tasks --no-daemon --no-configuration-cache --max-workers=1` — `BUILD SUCCESSFUL`; production raid snapshot metadata scenario included.
- `./gradlew.bat :apps:game-api:test --tests com.hanjjak.gameapi.FlywayMigrationVersionTest --tests com.hanjjak.gameapi.RaidMigrationTest -Pkotlin.incremental=false --rerun-tasks --no-daemon --no-configuration-cache --max-workers=1` — `BUILD SUCCESSFUL`; V57 marker/index migrate and all Flyway versions are unique.
- Final combined command selecting all Task 6 unit, lifecycle, Spring, battle handoff, snapshot metadata, and V56/V57 migration tests completed `BUILD SUCCESSFUL` in 1m 36s with 43 executed Gradle tasks after adding crash-gap resume recovery.

## Documentation status

L-02 remains `부분 구현` / `부분 검증`: lifecycle and handoff are implemented and focused-verified; Task 7 reward/claim and Task 8 settlement behavior remain.

## Commit

All changes use subject `feat(raid): implement attempt lifecycle`; the integrating agent records the amended SHA after creation.

---

## Latest mainline first-clear API and web evidence

# Task 6 Report

## Delivered

- Published the first-clear reward TypeSpec models and GET pending / POST claim contracts, including nullable battle result fields and generated OpenAPI output.
- Added the authenticated reward inbox controller with common state-version envelopes, idempotent claim handling, pending-only list projection, and capacity error details.
- Added the web API and inbox screen. The screen renders stage/version/item quantities/slot facts, keeps failed rows visible, explains capacity recovery, removes successful claims optimistically, and invalidates reward, inventory, equipment, skills, and session queries.
- Integrated nullable first-clear results into battle runtime state and rendered separate first-clear rows for rice, immediate items, pending items, and direct skill unlock without merging pending quantities into residence rewards.
- Added persistent screen storage, primary/shared navigation, app rendering, and battle HUD preview route support.

## Verification

- `corepack pnpm --filter @hanjjak/contracts build` — PASS.
- Focused Vitest (`src/features/first-clear-rewards`, `BattleHud.test.tsx`, `runtimeStore.test.ts`, `navigationState.test.ts`) — 5 files / 36 tests PASS.
- `corepack pnpm --filter @hanjjak/web typecheck` — PASS.
- `./gradlew.bat :modules:account:compileKotlin :modules:battle:compileKotlin --no-configuration-cache` — BUILD SUCCESSFUL.
- `git diff --check` — PASS.

The full web build was intentionally skipped per the Task 6 narrow-validation constraint.
