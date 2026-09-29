---
doc_kind: task
owner_domain: delivery
task_code: 'L-02'
task_area: 'L 레이드'
task_type: '개발'
priority: 'P0'
target_chapters: '공통'
planning_status: '완료'
development_status: '부분 구현'
verification_status: '부분 검증'
source_url: ''
notion_id: ''
snapshot_date: '2026-09-14'
---

# L-02 일일 세션·개인 도전 상태 구현

> 작업 상태와 완료 증거의 SSOT. 게임 규칙은 [레이드 SSOT](../../../../30-domain/raid/ssot.md)를 따른다.

## 완료 기준

일일 세션, 순차 보상 슬롯, 개인 도전·재시도·폐기·연습, 연결 종료 결과 보관과 메인 전투 중단·재개를 서버 권한 상태로 구현한다.

## 선행 작업

L-01, B-05, B-09, E-01

## 증거 링크

- 도메인 모델·포트: `modules/raid/src/main/kotlin/com/hanjjak/raid/domain/RaidModels.kt`, `modules/raid/src/main/kotlin/com/hanjjak/raid/application/RaidPorts.kt`
- 상태 전이·고정 스냅샷: `modules/raid/src/main/kotlin/com/hanjjak/raid/application/RaidService.kt`
- 연결 종료 terminalizer: `modules/raid/src/main/kotlin/com/hanjjak/raid/application/RaidAttemptTerminalizer.kt`
- main-battle caller-lock handoff: `modules/battle/src/main/kotlin/com/hanjjak/battle/application/BattleSessionService.kt`, `apps/game-api/src/main/kotlin/com/hanjjak/gameapi/raid/RaidAdapters.kt`
- Task 5 (2026-09-14, final persistence review): V56 `daily_seal_raid` Flyway migration adds session, account-state, settlement workset, owned reward-slot, attempt, append-only confirmed-result, contribution, immutable final-rank, whole-bundle claim and account/idempotency command tables. Named checks, due-OPEN index, mode-aware ordinal bounds, composite ownership FKs, non-null authoritative JSON, sole OPEN/running partial indexes, shared competitive ranks, and database-enforced claim source FKs/checks are enforced.
- Task 6 lifecycle: `RaidService`, `RaidAttemptTransitions`, `RaidAttemptCompletionService`, `RaidAttemptTerminalizer`, V57 exact battle handoff와 `raid_attempt.resume_pending`, production character/skill/cosmetic snapshot adapter를 구현했다. 서버는 OPEN session을 잠그고 3×3 순차 보상 시도, practice gate, immutable full simulation input/result/timeline, elapsed-prefix 조회, UUIDv4/RFC variant, 멱등 replay를 판정한다. Terminalizer는 account/attempt 순서의 bounded page를 읽고 key마다 별도 transaction으로 RESULT_HELD를 저장한다. 프로세스가 resume 전에 중단되어도 persisted pending marker를 scheduler·조회·재시도·폐기가 exact one-shot handoff로 회수한다.
- 최종 검증은 ASCII `Y:`에서 Task 6 unit, real PostgreSQL lifecycle, Spring-proxied 동시성/rollback, BattleSession exact handoff, production snapshot metadata, V56/V57 migration을 함께 선택한 Gradle command가 `BUILD SUCCESSFUL` in 1m 36s, 43 executed tasks로 통과했다. 개별 suite로 `RaidAttemptServiceTest` 14개, `RaidAttemptIntegrationTest` 8개, `RaidLifecycleSpringIntegrationTest` 2개를 포함한다. Task 7 보상·claim과 Task 8 정산은 남아 있어 개발·검증 상태는 부분으로 유지한다.
- Task 11 named verification sources: `apps/game-api/src/test/kotlin/com/hanjjak/gameapi/RaidLongRunIntegrationTest.kt` is the only enabled named scenario and executes the deterministic 1,000-seed replay. `RaidLongRunDatabaseLoadTest.kt` and `RaidSettlementConcurrencyTest.kt` are Docker-gated, compile-only scaffolding with schema assertions and placeholder HTTP/load setup; they do not provide live burst, contention, ranking, or replay-storm evidence.

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 부분 구현 |
| 검증 | 부분 검증 |
