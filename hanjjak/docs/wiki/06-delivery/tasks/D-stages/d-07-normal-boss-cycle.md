---
doc_kind: task
owner_domain: delivery
task_code: 'D-07'
task_area: 'D 스테이지'
task_type: '개발'
priority: 'P0'
target_chapters: '챕터 1, 챕터 2, 챕터 3, 챕터 4'
planning_status: '미확인'
development_status: '부분 구현'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/d2a06c8781b08226a1ea010eae0c08fe'
notion_id: 'd2a06c87-81b0-8226-a1ea-010eae0c08fe'
snapshot_date: '2026-08-28'
---

# D-07 일반 구간·보스 연속 사이클 구현

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [스테이지](../../../../30-domain/world/ssot.md)를 따른다.

## 완료 기준

1~9스테이지에서는 일반 20마리 후 보스 1마리가 등장하고, 10스테이지에서는 일반 몬스터 없이 최종 보스 1마리가 즉시 등장한다. 사이클 안에서 동일 HP가 유지되며 보스 체급·드롭 가중치를 데이터로 조정할 수 있다.

## 선행 작업

D-02\~E-01

## 비고

Kotlin 판정 엔진은 스테이지 번호에 따라 1~9의 일반 적 20마리 뒤 보스 1마리 또는 10의 최종 보스 1마리를 처리하고 사이클 안에서 HP를 유지한다. 서버 전투 세션이 저장된 입력·seed와 완료 가능 시각을 발급하고 complete에서 같은 입력을 재계산해 보상·성장·스테이지 진행을 확정한다. 웹은 완료 뒤 서버가 반환한 다음 스테이지로 새 사이클을 자동 시작한다. 실제 PostgreSQL 장시간 반복 E2E는 남아 있다.

## 증거 링크

- 구현: `packages/sim-core/src/main/kotlin/com/hanjjak/sim/CombatSimulator.kt`, `modules/battle/src/main/kotlin/com/hanjjak/battle/application/StageBattleService.kt`
- 단위 검증: `packages/sim-core/src/test/kotlin/com/hanjjak/sim/CombatSimulatorTest.kt`, `modules/battle/src/test/kotlin/com/hanjjak/battle/application/StageBattleServiceTest.kt`
- 2026-09-02 `./gradlew.bat test` 통과. 반복 실행 E2E는 실행하지 않았다.
- 2026-09-03 `feature/auto-battle-loop` API·브라우저 스모크: 전투 탭에서 1-1 전투 시작 후 일반 20/20, 보스 포함 사이클 완료, 남은 HP, 소요 tick, 보상 표시를 확인. `POST /api/v1/battles/chapters/1/auto-run`은 fresh 계정에서 1-1→1-2 성공 후 1-3 실패 지점에서 중단되고 누적 보상을 표시함을 확인.
- 2026-09-08 지속 사이클: `modules/battle/src/main/kotlin/com/hanjjak/battle/application/BattleSessionService.kt`, `modules/battle/src/main/kotlin/com/hanjjak/battle/api/BattleSessionController.kt`, `apps/web/src/features/battle/autoBattleClient.ts`
- 2026-09-08 검증: 발급 입력과 Kotlin 정본 시뮬레이터 결과의 재현 테스트, 웹 단일 사이클 완료 후 다음 사이클 자동 시작 테스트가 통과했다.
- 2026-09-10 `stage.01-10` 보스 단독 전투: `StageBattleService`가 `normalCount=0`을 발급하고 simulator가 `BOSS_SPAWNED`·보스 처치·`BATTLE_CYCLE_COMPLETED`를 순서대로 1회 생성하며 `defeatedNormals=0`으로 완료하는지 검증했다. 전체 40스테이지 행렬에서 나머지 39개는 `normalCount=20`임을 함께 검증했다.
- 2026-09-10 최종 검증: ASCII 드라이브에서 `:packages:sim-core:test`, `:modules:battle:test`, `:apps:game-api:compileTestKotlin` 통과. Docker Desktop Linux 엔진 기동 후 `ChapterCombatE2ETest`와 `BattleHistoryE2ETest`도 통과했다.
- 2026-09-10 정산 경계 변경: HP·스킬·seed는 기존 20+1 사이클 동안 유지하되 일반 몬스터 처치별 보상을 즉시 확정하고, 보스 보상·클리어·해금·완전 회복은 사이클 완료에 유지했다. 결정론 드롭 합산·성장 합산 테스트가 통과했다.
- 2026-09-10 10스테이지의 서버 `CycleInput.normalCount`를 0으로 발급해 첫 렌더 사건이 `BOSS_SPAWNED`, 적 인덱스 1인 최종 보스 단독 사이클로 보정했다. `StageBattleServiceTest` 통과.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 미확인 |
| 개발 | 부분 구현 |
| 검증 | 부분 검증 |
