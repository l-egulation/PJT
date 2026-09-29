---
doc_kind: task
owner_domain: delivery
task_code: 'C-04'
task_area: 'C 주력 재료'
task_type: '개발'
priority: 'P0'
target_chapters: '공통'
planning_status: '완료'
development_status: '완료'
verification_status: '완료'
deadline: ''
source_url: 'https://app.notion.com/p/71d06c8781b08289a3c5810cfaa2b422'
notion_id: '71d06c87-81b0-8289-a3c5-810cfaa2b422'
snapshot_date: '2026-08-28'
---

# C-04 주력 재료 선택·영구 저장 구현

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [주력 재료](../../../../30-domain/character/ssot.md)를 따른다.

## 완료 기준

선택값이 서버 권한 데이터로 한 번만 저장되고 재접속·다른 클라이언트에서도 유지되며 다른 재료로 변경되지 않는다.

## 선행 작업

B-02, C-02

## 비고

조회·선택 API, DB 유일성, 같은 선택 멱등 성공, 다른 선택 거절, 계정 `state_version` 갱신과 재접속 복원을 구현했다. 서로 다른 DB 연결의 동시 선택을 검증하는 Testcontainers 테스트와 실제 브라우저 재로그인 복원까지 확인했다.

## 증거 링크

- 서버·저장: `modules/account`, `apps/game-api/src/main/resources/db/migration/V7__material_preference.sql`
- 계약: `packages/contracts/material-preference.tsp`
- [주력 재료 구현 증거](../../../../70-plans/material-preference/implementation-evidence.md)
- 2026-09-07 `AccountFlowIntegrationTest.kt`에 서로 다른 재료의 동시 선택 두 건 중 한 건만 성공하고 `state_version`이 한 번만 증가하는 PostgreSQL 통합 검증을 추가했다. 테스트 컴파일은 통과했고 로컬 Docker 서비스 장애로 실행은 자동 건너뛰었다.
- 2026-09-08 Docker 엔진 복구 후 `./gradlew.bat :apps:game-api:test --tests com.hanjjak.gameapi.AccountFlowIntegrationTest`를 실제 PostgreSQL Testcontainers 환경에서 실행해 통과했다. 실제 game-api API 세션과 브라우저에서 감자 선택, 로그아웃, 재로그인 뒤 선택 복원과 첫 진입 화면 생략을 확인했다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 완료 |
| 검증 | 완료 |
