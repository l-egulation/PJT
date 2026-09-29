---
doc_kind: task
owner_domain: delivery
task_code: 'K-07'
task_area: ''
task_type: '개발'
priority: 'P0'
target_chapters: '챕터 1, 챕터 2, 챕터 3, 챕터 4'
planning_status: '미확인'
development_status: '부분 구현'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/e5206c8781b083d3ae7f81a28b70d505'
notion_id: 'e5206c87-81b0-83d3-ae7f-81a28b70d505'
snapshot_date: '2026-08-28'
---

# K-07 챕터·스테이지 선택과 진행도 UI 구현

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [클라이언트 UX](../../../../30-domain/player/ux/ssot.md)를 따른다.

## 완료 기준

챕터 화면에서 네 챕터의 잠금·최초 클리어 진행률·완료·현재 전투·진행 목표를 구분하고, 스테이지 화면에서 클리어·현재·잠금·반복 대상을 구분해 주요 보상·`피해 N%`·해당 스테이지 최근 실패 1건을 확인하며, 해금 스테이지 즉시 입장과 1~9 다음 사이클 반복 설정을 수행할 수 있다.

## 선행 작업

B-08, D-02, D-03, D-06, K-02, K-15, K-16

## 비고

전투 탭의 단일 스테이지 선택기에서 서버가 반환한 챕터 1~4 스테이지의 해금·잠금·클리어·현재 전투·반복 대상 상태를 표시한다. `자동 진행`과 해금된 1~9 `스테이지 반복`은 서버에 저장하고, 진행 중 전투가 있으면 현재 적용 모드와 다음 사이클 적용 예정 모드를 구분한다. 서버 오류 시 기존 적용 상태를 유지하고 오류를 표시한다. 선택한 스테이지의 최근 실패 1건 또는 빈 상태 조회 계약과 현재 전투 화면 표시는 구현했다. 별도 네 챕터 묶음 화면, 주요 보상·피해 N% 표시는 남아 있다.

## 증거 링크

- 2026-09-03 구현: `modules/battle/src/main/kotlin/com/hanjjak/battle/api/StageController.kt`, `modules/battle/src/main/kotlin/com/hanjjak/battle/api/BattleController.kt`, `apps/web/src/features/battle/BattleScreen.tsx`
- 2026-09-03 검증: fresh DB `hanjjak_auto_battle_loop`에서 1-1 클리어 전 1-2 잠김, 1-1 클리어 후 1-2 해금 API 스모크와 브라우저 스테이지 목록 표시 확인. 서버 챕터 1 자동 진행은 fresh 계정에서 1-3 실패까지 순차 실행하고 1-1·1-2 클리어 횟수 반영을 확인했다.
- 2026-09-08 구현: `apps/web/src/features/battle/BattleScreen.tsx`, `apps/web/src/features/battle/runtimeStore.ts`, `apps/web/src/shared/persistence/runtimeCheckpointDb.ts`; 전체 스테이지 선택, 방치 모드 전환, 10스테이지 반복 방지와 반복 대상 유지 로직을 연결했다.
- 2026-09-08 브라우저 QA: 3-7 전투 중 `스테이지 반복`을 선택한 뒤 다음 사이클이 3-7에서 다시 시작되고 진행 수가 0/20부터 증가하는 것을 확인했다.
- 2026-09-09 최신 `main` 재기반 후 잠긴 3-8 이후 스테이지의 비활성 상태, 3-7 반복 선택 유지, 5·10·15·20·보스 체크포인트를 실제 로그인 세션에서 다시 확인했다.
- 2026-09-09 구현: `PATCH /api/v1/battle/repeat-stage`, `apps/web/src/features/battle/BattleScreen.tsx`, `apps/web/src/features/battle/runtimeStore.ts`; 반복 대상 서버 저장, 현재/다음 사이클 상태 구분, 저장 중 중복 클릭 방지와 오류 표시를 연결했다.
- 2026-09-09 검증: `BattleSessionE2ETest`, 웹 29개 파일·79개 테스트, TypeScript typecheck, production build와 TypeSpec 계약 검사를 통과했다. 격리 PostgreSQL과 실제 브라우저에서 자동 진행 중 1-1 반복 설정, 서버 `runtime-state`의 `REPEAT_STAGE`·`stage.01-01`, 화면의 `현재 1-1 반복` 표시를 확인했다.
- 2026-09-09 이슈 #4는 #7의 선행 계약 `GET /api/v1/battle-history/stages/{stageId}/latest-failure`를 TypeSpec·서버·웹 API에 추가했다. 응답은 `data.latestFailure` 한 건 또는 명시적 `null`이며 전체 메뉴와 같은 `battle_history_event`를 조회한다. GitLab #7 note `2779728`에 소비 경로를 연결했다.
- 2026-09-10 스테이지 HUD 조립: 현재 스테이지와 서버 처치 수를 `N / 20` 단일 진행 바로 표시하고, `스테이지 선택` 버튼을 펼치면 해금 상태를 보존한 선택기와 자동 진행·스테이지 반복 설정이 열린다. `BattleHud.test.tsx`에서 13/20이 65%로 표시되는 동적 렌더를 검증했고 웹 전체 149개 테스트, typecheck, production build가 통과했다.
- 2026-09-10 스테이지 조작 재배치: 현재 스테이지 제목을 종이 중앙축에 고정하고 `스테이지 선택`, `자동 진행`, `스테이지 반복`을 상단 조작 줄로 분리했다. 선택기는 진행 정보를 가리지 않고 종이 아래로 열리며, 자동·반복 토글은 기존 `onChangeMode` 서버 저장 흐름을 직접 호출한다. 웹 43개 파일·150개 테스트, typecheck, production build와 1265×712 펼침 상태 시각 검수가 통과했다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 미확인 |
| 개발 | 부분 구현 |
| 검증 | 부분 검증 |
