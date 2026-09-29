---
doc_kind: task
owner_domain: delivery
task_code: 'B-03'
task_area: 'B 계정·저장'
task_type: '개발'
priority: 'P0'
target_chapters: '공통'
planning_status: '미확인'
development_status: '부분 구현'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/f5e06c8781b083af93a58132ccf5a058'
notion_id: 'f5e06c87-81b0-83af-93a5-8132ccf5a058'
snapshot_date: '2026-08-28'
---

# B-03 스테이지 진행도 저장·복구 구현

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [계정·저장](../../../../30-domain/player/ssot.md)를 따른다.

## 완료 기준

서버 권한의 현재·최고 스테이지, 최초 클리어, 챕터 해금, 반복 대상과 최근 전투 사건 50건이 재접속 후 유지되고, 로컬의 마지막 방치모드와 조합해 권한 충돌 없이 복원된다.

## 선행 작업

B-02

## 비고

서버의 스테이지별 최초 클리어 시각·클리어 횟수, 현재 스테이지와 반복 대상을 저장하고 재접속 시 `runtime-state`로 복원한다. 반복 대상은 해금된 1~9스테이지만 서버가 수락하며 전투 사이클 경계에서 적용한다. 최근 전투 사건은 PostgreSQL에 사용자별 최신 50건으로 유지하고 인증 조회로 재접속 후 복원한다. 최고 스테이지·명시적 챕터 해금 상태는 남아 있다.

## 증거 링크

- 저장 스키마: `apps/game-api/src/main/resources/db/migration/V1__account_and_progress.sql`, `apps/game-api/src/main/resources/db/migration/V28__battle_repeat_target.sql`
- 구현: `modules/stage/src/main/kotlin/com/hanjjak/stage/domain/StageProgress.kt`, `modules/battle/src/main/kotlin/com/hanjjak/battle/application/BattleModeService.kt`, `modules/battle/src/main/kotlin/com/hanjjak/battle/api/RuntimeStateController.kt`
- 단위 검증: `modules/stage/src/test/kotlin/com/hanjjak/stage/domain/StageProgressionTest.kt`
- 2026-09-09 `BattleSessionE2ETest`에서 반복 대상 저장, 재조회, 현재 사이클 보존, 다음 사이클 적용, 잠긴 스테이지·10스테이지 거부를 PostgreSQL로 검증했다. 해당 실행 시점에는 최고 스테이지·최근 전투 기록 복원 시나리오를 실행하지 않았다.
- 2026-09-09 이슈 #4 구현: `V31__battle_history.sql`, `BattleHistoryService`, `BattleHistoryController`, `BattleSessionService`, `GemDungeonService`; 메인 전투와 보석 던전의 입장·결과·복귀를 같은 transaction에서 기록하고 전체 최신순 조회와 스테이지 최근 실패 1건 조회를 제공한다.
- 2026-09-09 `BattleHistoryE2ETest`를 PostgreSQL 17에서 실행해 완료 재요청 중복 방지, 계정 격리, 로그아웃·재로그인 후 동일 기록 복원, 51번째 기록의 가장 오래된 사건 제거, 실패 코드·콘텐츠 버전·전투 스펙 스냅샷과 명시적 빈 상태를 검증했다.
- 2026-09-09 로컬 후속 구현: `BattleHistoryService`가 스테이지 성공·실패 모두 전투 시간, 남은 HP, 처치한 일반 몬스터 수, 경험치·쌀과 실제 지급 아이템의 표시명·수량을 `combat_snapshot`에 보존하고, `BattleHistoryScreen`이 클리어 보상 전체와 실패 처치 수·이유를 표시한다. `BattleHistoryE2ETest`의 PostgreSQL 검증, `modules:battle:test`, 웹 39파일 134테스트·typecheck·build와 실제 `127.0.0.1:5173` 브라우저 검수를 통과했다. 기존 개발 `부분 구현`, 검증 `부분 검증` 상태는 유지한다.
- 2026-09-10 결과 스냅샷 확장: `BattleHistoryService`와 전투 기록 계약에 같은 세션의 실제 스테이지 입장 시각과 마지막 상대의 남은 HP를 추가했다. 승리의 남은 HP는 SSOT의 사이클 완료 회복을 바꾸지 않고 렌더링 타임라인에서 회복 직전 값을 보존한다. 서버·시뮬레이터 단위 테스트와 game-api 테스트 컴파일은 통과했으며 PostgreSQL E2E는 로컬 Docker 엔진 부재로 초기화 단계에서 실행되지 않았다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 미확인 |
| 개발 | 부분 구현 |
| 검증 | 부분 검증 |
