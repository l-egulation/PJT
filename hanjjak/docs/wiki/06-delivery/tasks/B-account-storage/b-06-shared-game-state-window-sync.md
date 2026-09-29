---
doc_kind: task
owner_domain: delivery
task_code: 'B-06'
task_area: 'B 계정·저장'
task_type: '개발'
priority: 'P0'
target_chapters: '공통'
planning_status: '미확인'
development_status: '부분 구현'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/1b606c8781b083a7a44d8114ac86826f'
notion_id: '1b606c87-81b0-83a7-a44d-8114ac86826f'
snapshot_date: '2026-08-28'
---

# B-06 단일 게임 상태 엔진·웹 화면 상태 공유 구현

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [계정·저장](../../../../30-domain/player/ssot.md)를 따른다.

## 완료 기준

단일 브라우저 탭의 펫 UI와 관리 UI가 하나의 전투 타이머·게임 상태를 공유하고 중복 진행이 발생하지 않는다. 단일 실행 세션 정책과 브라우저 수명주기 중단·복귀 동작을 검증한다.

## 선행 작업

B-02

## 비고

한짝 설계 브리핑 v2 반영. 메인 전투 화면과 PIP가 하나의 Zustand store에서 스테이지·실행 상태·최근 서버 결과를 공유한다. 서버는 계정당 활성 게임 실행 세션 하나를 허용하고 새 세션이 기존 세션을 대체하며, 웹은 30초 heartbeat와 90초 만료 계약을 사용한다. 지속 자동전투 타이머와 실제 기기 절전·discard 검증은 남아 있다.

## 증거 링크

- 2026-09-08 공유 상태: `apps/web/src/features/battle/runtimeStore.ts`, `apps/web/src/features/battle/BattleScreen.tsx`, `apps/web/src/features/battle/PetBattleSurface.tsx`
- 2026-09-08 실행 세션: `modules/battle/src/main/kotlin/com/hanjjak/battle/application/GameSessionService.kt`, `modules/battle/src/main/kotlin/com/hanjjak/battle/api/GameSessionController.kt`, `apps/game-api/src/main/resources/db/migration/V18__game_session.sql`
- 2026-09-08 웹 수명주기: `apps/web/src/features/runtime/RuntimeLifecycle.tsx`, `apps/web/src/features/runtime/gameSessionClient.ts`
- 2026-09-08 검증: `corepack pnpm --filter @hanjjak/web test`, `typecheck`, `build` 통과. Chromium PIP를 연 상태에서 전투 명령 1회에 동일 store의 경험치·쌀·처치 결과가 갱신되고 메인 인라인 표면이 중복 렌더링되지 않음을 확인.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 미확인 |
| 개발 | 부분 구현 |
| 검증 | 부분 검증 |
