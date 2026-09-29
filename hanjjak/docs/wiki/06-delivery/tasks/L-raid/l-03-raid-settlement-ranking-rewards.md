---
doc_kind: task
owner_domain: delivery
task_code: 'L-03'
task_area: 'L 레이드'
task_type: '개발'
priority: 'P0'
target_chapters: '공통'
planning_status: '완료'
development_status: '구현 완료'
verification_status: '부분 검증'
deadline: ''
source_url: ''
notion_id: ''
snapshot_date: '2026-09-14'
---

# L-03 정산·순위·보상 구현

> 작업 상태와 완료 증거의 SSOT. 게임 규칙은 [레이드 SSOT](../../../../30-domain/raid/ssot.md)를 따른다.

## 완료 기준

KST 일일 경계의 단계형 정산, 봉인 결과, 경쟁 순위, 개인·순위 보상과 만료 없는 수령권을 원자적·멱등하게 구현한다.

## 선행 작업

L-01, L-02, F-05, J-01

- 승인 설계: `docs/superpowers/specs/2026-09-13-daily-seal-raid-design.md`
- 구현 계획: `docs/superpowers/plans/2026-09-13-daily-seal-raid.md`
- Task 7 확인·claim 구현과 검증: `modules/raid/src/main/kotlin/com/hanjjak/raid/application/RaidService.kt`, `modules/raid/src/main/kotlin/com/hanjjak/raid/domain/RaidRules.kt`, `apps/game-api/src/test/kotlin/com/hanjjak/gameapi/RaidRewardIntegrationTest.kt`, `docs/superpowers/sdd/task-7-report.md`
- Task 8 단계형 정산·순위 구현: `modules/raid/src/main/kotlin/com/hanjjak/raid/application/RaidSettlementService.kt`, `modules/raid/src/main/kotlin/com/hanjjak/raid/domain/RaidModels.kt`, `modules/raid/src/main/kotlin/com/hanjjak/raid/infrastructure/JdbcRaidRepository.kt`, `apps/game-api/src/main/resources/db/migration/V58__raid_settlement_phases.sql`
- Task 8 단위 검증: ASCII `Y:`에서 `:modules:raid:test --tests com.hanjjak.raid.application.RaidSettlementServiceTest` 5개 통과. PostgreSQL 저장소 회귀 `JdbcRaidRepositoryTest` 23개와 V58 포함 migration version test, `RaidSettlementIntegrationTest` smoke test 통과.
- 단계는 ROTATE_SESSION → FREEZE_WORKSET → FINALIZE_ACCOUNTS → MATERIALIZE_RANKS → SETTLED 로 영속화하고, UUID 정렬·경쟁 순위 skip·100등 tie fanout·caller 분리·cursor를 구현했다.
- 2026-09-14 whole-branch review remediation: final ranking accepts an optional settled `sessionId`, while open-session ranking remains the default; API error normalization maps every reachable RAID_/INVALID_* service failure to a declared raid code. Fresh PostgreSQL concurrency, claim, rank, latency, lock-wait and retry counters remain unobserved because Docker daemon is unavailable; the production-schema probe is compile-only.
- Verification remains partial; L-03 is not marked complete.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 구현 완료 |
| 검증 | 부분 검증 |
