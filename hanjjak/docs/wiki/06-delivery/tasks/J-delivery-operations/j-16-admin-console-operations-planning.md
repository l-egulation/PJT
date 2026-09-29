---
doc_kind: task
owner_domain: delivery
task_code: 'J-16'
jira_key: ''
gitlab_issue: '12'
task_area: 'J 데이터·테스트·배포'
task_type: '개발'
priority: 'P1'
target_chapters: '공통'
planning_status: '부분 완료'
development_status: '부분 구현'
verification_status: '부분 검증'
deadline: ''
source_url: ''
notion_id: ''
snapshot_date: '2026-09-11'
---

# J-16 어드민 콘솔 인증·사용자·거래소 운영

> 작업 상태와 완료 증거의 SSOT. 운영 경계는 [배포·운영 SSOT](../../../../50-architecture/operations.md), 데이터 경계는 [데이터·이벤트 SSOT](../../../../40-systems/event-system/ssot.md)를 따른다.

## 완료 기준

운영자용 별도 콘솔의 사용자, 핵심 화면, 조회·변경 경계, 권한·감사 원칙, 우선순위와 미해결 정책이 구현 이슈로 분해할 수 있을 정도로 정리된다.

## 선행 작업

J-03, J-08, J-09

## 비고

계정·진행·경제·전투·콘텐츠·이벤트·배포·감사 기능을 [어드민 콘솔 운영 기획](../../../../70-plans/admin-console/README.md)에 정리했다. 구현은 SSAFY GitLab OAuth 전용 인증, P0 읽기·진단, 사용자 상태 조정과 거래소 운영 범위를 포함한다. 로컬 운영자 ID·비밀번호 로그인은 제거되었고 GitLab `username` 화이트리스트만 관리자 세션을 발급한다. `ADMIN`의 `user:manage`는 레벨·누적 경험치, 쌀, 스택 아이템, 보석, 순차 스테이지 해금, 치장 보유, 장비 등급·강화 상태를 조정하며 `market:manage`는 거래소 운영 명령을 제공한다.

사용자 조정은 판매 예약·장착·인벤토리 용량·콘텐츠 허용 목록을 우회하지 않고 운영 사유와 관리자별 멱등 키를 요구한다. 모든 관리자 조회·조작·거절은 GitLab username·관리자 ID·action·대상·결과를 기록하며 사용자 조작은 사유·멱등 키·전후 상태 요약을 추가로 append-only 감사 로그에 남겨 콘솔에서 조회한다. 이번 변경은 전체 계정 목록·포함 검색, 카탈로그 기반 선택 UI, 인라인 버튼 조정 UI와 거래소 단일 매물 임의 수량 등록을 포함한다. MFA·외부 접근 프록시, 최종 RBAC 관리 UI, 2인 승인과 감사 보존 기간은 아직 미해결이다. 실행 기록은 [GitLab 이슈 #12](https://lab.ssafy.com/s15-bigdata-dist-sub1/S15P21B107/-/work_items/12)에 연결한다.

## 증거 링크

- 기획: `docs/70-plans/admin-console/README.md`
- 실행 이슈: `https://lab.ssafy.com/s15-bigdata-dist-sub1/S15P21B107/-/work_items/12`
- 구현: `modules/admin/src/main/kotlin/com/hanjjak/admin/application/AdminAccountService.kt`, `modules/admin/src/main/kotlin/com/hanjjak/admin/application/AdminCatalogService.kt`, `modules/admin/src/main/kotlin/com/hanjjak/admin/api/AdminOperationsController.kt`, `modules/admin/src/main/kotlin/com/hanjjak/admin/api/AdminAccessFilter.kt`, `modules/market/src/main/kotlin/com/hanjjak/market/application/AdminMarketService.kt`, `packages/contracts/admin.tsp`, `packages/contracts/generated/openapi/openapi.yaml`, `packages/client-sdk/src/index.ts`, `apps/admin-console/src`
- 서버 검증: `cmd.exe /c gradlew.bat :modules:admin:compileKotlin :modules:market:test` 통과. 시장 서비스의 10,000개 시스템 등록이 단일 매물로 유지되는 회귀 검증을 포함한다.
- 관리자 API 검증: `cmd.exe /c gradlew.bat :apps:game-api:test --tests com.hanjjak.gameapi.AdminOperationsIntegrationTest` 통과. 전체 사용자 목록, 포함 검색, 카탈로그 조회와 10,000개 단일 매물 응답을 확인했다.
- 프론트·계약 검증: `corepack pnpm --filter @hanjjak/admin-console typecheck` 및 `corepack pnpm --filter @hanjjak/contracts build` 통과. 실제 Vite `/admin/` 화면을 브라우저에서 mock API로 열어 사용자 필터·버튼 조정·이름 선택·10,000개 1개 매물 피드백을 확인했다.
- 제한: 전체 workspace 테스트와 실제 OAuth/운영 API 연결 smoke는 이번 검증 범위에 포함하지 않았다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 부분 완료 |
| 개발 | 부분 구현 |
| 검증 | 부분 검증 |
