---
doc_kind: task
owner_domain: delivery
task_code: 'J-05'
task_area: 'J 데이터·테스트·배포'
task_type: '검증'
priority: 'P0'
target_chapters: '챕터 1, 챕터 2, 챕터 3, 챕터 4'
planning_status: '완료'
development_status: '완료'
verification_status: '검증'
deadline: ''
source_url: 'https://app.notion.com/p/6fa06c8781b08294b8ea017551a315a3'
notion_id: '6fa06c87-81b0-8294-b8ea-017551a315a3'
snapshot_date: '2026-08-28'
---

# J-05 챕터 1~4 전투 E2E 테스트

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [데이터·이벤트 및 배포·운영](../../../../40-systems/event-system/ssot.md)를 따른다.

## 완료 기준

챕터 1 시작부터 4-10까지 전투·실패·재도전·서버 클리어·챕터 해금·잠긴 챕터 직접 접근 거부·재접속 복원 흐름이 통과한다.

## 선행 작업

B-03, B-08, D-06, E-05, K-07

## 비고

2026-09-07 구현 기준으로 Testcontainers PostgreSQL과 MockMvc 기반 챕터 전투 E2E 검증을 추가했다. 신규 계정의 1-1 해금 상태, 전투 실패 후 클리어 미반영, 재도전 성공, 잠긴 2-1 직접 접근 거부, 챕터 1 자동 진행, 재로그인 후 2-1 해금 복원, 챕터 2~4 자동 진행, 4-10 클리어와 전투 Outbox 기록을 검증한다. 전투 스탯은 테스트 전용 `BattleStatsProvider`로 실패·성공 조건을 고정한다.

## 증거 링크

- 테스트: `apps/game-api/src/test/kotlin/com/hanjjak/gameapi/ChapterCombatE2ETest.kt`
- 검증: `./gradlew.bat :apps:game-api:test --tests com.hanjjak.gameapi.ChapterCombatE2ETest` 성공
- 계약 확인: `corepack pnpm --filter @hanjjak/contracts typecheck && corepack pnpm --filter @hanjjak/contracts build` 성공
- 검증 범위: 1-1 초기 해금, 1-2·4-10 초기 잠금, 실패 시 `stage_progress` 미반영, 재도전 성공, 2-1 잠금 접근 4xx, 1~4장 자동 진행, 재접속 복원, `BATTLE_CYCLE_COMPLETED` Outbox 기록

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 완료 |
| 검증 | 검증 |
