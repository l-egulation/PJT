---
doc_kind: task
owner_domain: delivery
task_code: 'E-01'
task_area: 'E 자동전투·스탯'
task_type: '개발'
priority: 'P0'
target_chapters: '공통'
planning_status: '미확인'
development_status: '부분 구현'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/1cc06c8781b083a0877501935198df48'
notion_id: '1cc06c87-81b0-83a0-8775-01935198df48'
snapshot_date: '2026-08-28'
---

# E-01 자동 이동·조우·전투 사이클 구현

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [전투](../../../../30-domain/combat/ssot.md)를 따른다.

## 완료 기준

일반 몬스터 20마리와 보스 1마리의 사이클이 무조작으로 반복된다.

## 선행 작업

D-01

## 비고

일반 적 20마리와 보스 1마리의 서버 판정이 순서화된 렌더링 사건을 함께 생성하며, 웹은 논리 tick 시각에 조우·공격·타격·사망 사건을 재생한다. 적 등장 사건과 같은 tick의 첫 공격 사이에는 클라이언트 접근 연출 0.65초를 삽입하고 공격 재생 간격을 서버 판정과 분리해 4프레임 타격이 끝난 뒤 피해를 반영하되 서버 판정과 전투 결과는 바꾸지 않는다. 완료 후 서버 권한 결과와 다음 스테이지로 교정·계속하고 `BATTLE_SESSION_NOT_READY`는 같은 활성 세션으로 재시도한다. 레거시 즉시 보상 호환 경로는 운영 기본값에서 비활성화하고, 요청 시 `BATTLE_COMPATIBILITY_API_DISABLED`로 거부하며 계정·경로·원격 주소를 서버 경고 로그에 남긴다. 실제 PostgreSQL 장시간 안정성 검증은 남아 있다.

## 증거 링크

- 구현: `packages/sim-core/src/main/kotlin/com/hanjjak/sim/CombatSimulator.kt`, `modules/battle/src/main/kotlin/com/hanjjak/battle/application/StageBattleService.kt`
- 단위 검증: `modules/battle/src/test/kotlin/com/hanjjak/battle/application/StageBattleServiceTest.kt`
- 2026-09-02 `./gradlew.bat test` 통과. 반복 실행 E2E는 실행하지 않았다.
- 2026-09-03 `feature/auto-battle-loop` 브라우저 스모크: 회원가입→주력 재료 선택→전투 탭→1-1 전투 시작→사이클 완료 결과 표시→아이템 탭 인벤토리 보상 반영 확인. 챕터 1 자동 진행 버튼은 서버 `auto-run` command를 호출하며 fresh 계정에서는 1-3 실패와 누적 보상을 표시했다.
- 2026-09-08 전투 세션: `apps/game-api/src/main/resources/db/migration/V19__battle_session.sql`, `packages/contracts/battle-session.tsp`, `modules/battle/src/main/kotlin/com/hanjjak/battle/application/BattleSessionService.kt`, `apps/web/src/features/battle/autoBattleClient.ts`
- 2026-09-08 검증: `StageBattleServiceTest` 입력 재현, 웹 battle session API와 완료 후 다음 사이클 자동 시작·중단 테스트 통과.
- 2026-09-08 readiness 회귀 검증: `apps/web/src/features/battle/autoBattleClient.test.ts`에서 첫 완료가 `BATTLE_SESSION_NOT_READY`이고 다음 완료가 성공하는 조건에 자동전투 오류 미전환·동일 세션 재시도·다음 사이클 시작을 고정했다.
- 2026-09-09 전투 사건 타임라인: `packages/sim-core/src/main/kotlin/com/hanjjak/sim/CombatSimulator.kt`, `packages/contracts/battle-session.tsp`, `modules/battle/src/main/kotlin/com/hanjjak/battle/application/BattleSessionService.kt`, `apps/web/src/features/battle/autoBattleClient.ts`. 웹 85개 테스트·타입 검사·빌드와 game-api Kotlin 컴파일 통과, 로컬 PostgreSQL 전투에서 실제 HP·처치 진행 확인.
- 2026-09-09 접근 재생: `apps/web/src/features/battle/autoBattleClient.ts`, `apps/web/src/features/battle/autoBattleClient.test.ts`. 같은 논리 tick의 등장과 첫 타격을 0.65초 간격으로 재생하고 화면 재생이 끝난 뒤 완료 요청하도록 고정했다. 웹 87개 테스트·타입 검사·프로덕션 빌드 통과.
- 2026-09-09 타격 동기화: 직접 공격 시작과 피해 반영을 분리하고 4번째 `strike1` 프레임 뒤 적 HP·피해 숫자가 한 번 갱신되도록 렌더링 타임라인을 조정했다. 지속 피해는 직접 타격 숫자로 중복 표시하지 않는다.
- 2026-09-09 GitLab #3 실패 복귀 구현: `apps/web/src/features/battle/autoBattleClient.ts`가 서버 확정 실패 뒤 같은 스테이지를 1초 대기 후 새 battle session으로 시작하며, `stop()`은 대기 타이머를 함께 정리한다.
- 2026-09-09 회귀 검증: `apps/web/src/features/battle/autoBattleClient.test.ts`에서 성공은 즉시 다음 스테이지로 이어지고 실패는 999ms 동안 시작하지 않은 뒤 1000ms에 같은 스테이지를 시작하며 세션 종료 시 예약 재시작이 발생하지 않음을 확인했다.
- 2026-09-10 몬스터별 정산: `BattleEnemySettlementService`, `V40__battle_enemy_settlement.sql`, battle session settlements API와 웹 정산 큐를 연결했다. 일반 적 처치 사건 재생 직후 서버가 처치별 경험치·쌀·아이템을 확정하고 완료 전 HUD에 반영한다. 웹 170개 테스트·typecheck·production build와 battle/inventory/progression/events 모듈 테스트·game-api 테스트 코드 컴파일이 통과했다. 실제 PostgreSQL 전투 E2E는 컨테이너 런타임 부재로 미실행했다.
- 2026-09-11 긴급 차단: `modules/account/src/main/kotlin/com/hanjjak/account/api/ApiRequestGuardFilter.kt`가 레거시 `/battles/cycles`·`/battles/chapters/{chapter}/auto-run` 요청을 운영 기본값에서 보상 처리 전에 차단하고 `BATTLE_COMPATIBILITY_API_DISABLED`와 서버 경고 로그를 남긴다. `ApiRequestGuardFilterTest`가 두 경로의 403과 오류 코드를 검증한다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 미확인 |
| 개발 | 부분 구현 |
| 검증 | 부분 검증 |
