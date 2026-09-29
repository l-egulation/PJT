---
doc_kind: task
owner_domain: delivery
task_code: 'E-04'
task_area: 'E 자동전투·스탯'
task_type: '개발'
priority: 'P0'
target_chapters: '공통'
planning_status: '미확인'
development_status: '부분 구현'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/69c06c8781b0826c82af818b12ea1cb8'
notion_id: '69c06c87-81b0-826c-82af-818b12ea1cb8'
snapshot_date: '2026-08-28'
---

# E-04 HP 유지·사이클 회복·실패 처리 구현

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [전투](../../../../30-domain/combat/ssot.md)를 따른다.

## 완료 기준

일반 몬스터 20마리와 보스가 하나의 HP로 이어지고 사이클 시작 시에만 완전 회복하며 중간 회복 없이 실패·재도전한다.

## 선행 작업

E-01, E-02

## 비고

일반 적 20마리와 보스 사이에 HP를 유지하고, 성공한 사이클 결과는 최대 HP로 회복한다. 사망·보스 제한시간 실패 판정과 실패 뒤 1초 부활 대기, 같은 스테이지 0/20 자동 재도전 실행 흐름을 연결했다. 실제 적 공격에 따른 전투 중 HP 보간과 장시간 반복 검증은 남아 있다.

## 증거 링크

- 구현: `packages/sim-core/src/main/kotlin/com/hanjjak/sim/CombatSimulator.kt`
- 단위 검증: `packages/sim-core/src/test/kotlin/com/hanjjak/sim/CombatSimulatorTest.kt`
- 2026-09-02 `./gradlew.bat test` 통과. 실패 후 복귀·재도전 시나리오는 실행하지 않았다.
- 2026-09-09 GitLab #3 구현: `modules/battle/src/main/kotlin/com/hanjjak/battle/application/BattleSessionService.kt`, `apps/web/src/features/battle/autoBattleClient.ts`, `apps/web/src/features/battle/runtimeStore.ts`; 실패 결과는 반복 대상 변경보다 실패 스테이지 복귀를 우선하고, 웹은 1초 후 같은 스테이지를 새 세션·최대 HP·일반 몬스터 0/20으로 시작한다.
- 2026-09-09 검증: `BattleSessionE2ETest.failed battle keeps its stage when a pending repeat target exists` 통과. 웹 실패 대기·재도전·종료 타이머 정리와 공유 상태 전환 테스트 17개 통과.
- 2026-09-10 몬스터별 정산: 실패·중단 전 이미 확정한 처치 보상은 유지하고 `complete`·체크포인트가 있는 `abort`가 서버 확인 범위의 누락 처치를 보정하도록 전환했다. 정산 단위 테스트와 웹 중복 반영 회귀는 통과했으며 실제 PostgreSQL 중단 시나리오는 컨테이너 런타임 부재로 미실행했다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 미확인 |
| 개발 | 부분 구현 |
| 검증 | 부분 검증 |
