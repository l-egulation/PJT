---
doc_kind: task
owner_domain: delivery
task_code: 'K-19'
task_area: ''
task_type: '검증'
priority: 'P0'
target_chapters: '공통'
planning_status: '미확인'
development_status: '부분 구현'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/08a06c8781b082c1869b818a2d61b4d7'
notion_id: '08a06c87-81b0-82c1-869b-818a2d61b4d7'
snapshot_date: '2026-08-28'
---

# K-19 백그라운드 탭 진행·브라우저 수명주기 검증

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [클라이언트 UX](../../../../30-domain/player/ux/ssot.md)를 따른다.

## 완료 기준

백그라운드 탭에서 메인 전투·성장·보상이 계속되고, 같은 탭의 새로고침은 기존 실행 세션을 재개한다. 실제 로그아웃·브라우저 종료·기기 절전·장기 네트워크 단절 뒤에는 서버 경과시간에 따른 오프라인 보상을 제공하며 재진입 후 단일 실행 세션과 게임 상태가 일치한다.

## 선행 작업

B-06, B-07, K-18

## 비고

웹은 서버 실행 세션을 열고 30초마다 heartbeat를 전송한다. 동일 탭의 `sessionStorage`에 계정·게임 세션 식별자를 보관하고 새로고침 뒤 기존 세션 heartbeat가 성공하면 새 세션을 만들지 않는다. 저장값이 없더라도 서버는 1분 미만의 기존 활성 세션을 재사용하며, 1분 이상 무신호일 때만 오프라인 결과를 고정하고 새 실행 세션을 연다. 네트워크 오류는 기존 식별자를 보존해 재시도한다.

## 증거 링크

- 구현: `apps/web/src/features/runtime/RuntimeLifecycle.tsx`, `apps/web/src/features/runtime/gameSessionClient.ts`, `apps/web/src/features/runtime/reconcileRuntime.ts`, `modules/battle/src/main/kotlin/com/hanjjak/battle/application/GameSessionService.kt`
- 2026-09-14 검증: 저장 세션 재개, 만료 세션 대체, 네트워크 오류 보존, heartbeat 권한 상실, 계정 전환 경합과 실제/적용 시간의 정확한 `초` 표시를 포함한 대상 Vitest 13건과 web typecheck가 통과했다. 저장값 없는 새 문서의 서버 세션 재사용은 PostgreSQL 통합 테스트에 추가했으나 로컬 Docker 비실행으로 CI 검증 대상이다. `offlineSeconds`가 없는 구형 응답은 웹이 `eligibleSeconds`로 대체한다. 실제 브라우저 새로고침·로그아웃 운영 검증은 배포 후 확인한다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 미확인 |
| 개발 | 부분 구현 |
| 검증 | 부분 검증 |
