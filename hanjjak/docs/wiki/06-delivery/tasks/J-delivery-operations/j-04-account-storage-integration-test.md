---
doc_kind: task
owner_domain: delivery
task_code: 'J-04'
task_area: 'J 데이터·테스트·배포'
task_type: '검증'
priority: 'P0'
target_chapters: '공통'
planning_status: '완료'
development_status: '완료'
verification_status: '검증'
deadline: ''
source_url: 'https://app.notion.com/p/ba806c8781b082b0888a8106fb299065'
notion_id: 'ba806c87-81b0-82b0-888a-8106fb299065'
snapshot_date: '2026-08-28'
---

# J-04 계정·저장 통합 테스트

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [데이터·이벤트 및 배포·운영](../../../../40-systems/event-system/ssot.md)를 따른다.

## 완료 기준

가입, 로그인, 저장, 재접속, 강제 종료 복구 시나리오가 통과한다.

## 선행 작업

B-01, B-03, B-04, B-05

## 비고

2026-09-07 구현 기준으로 Testcontainers PostgreSQL과 MockMvc 기반 계정·저장 통합 검증을 추가했다. 신규 가입 세션, 재료 선호 저장, 닉네임 변경 저장, 전투 클리어 저장, 지갑 보상 저장, 재로그인 복원, 쿠키 유실/강제 종료 유사 상황 후 로그인 복원을 검증한다.

## 증거 링크

- 테스트: `apps/game-api/src/test/kotlin/com/hanjjak/gameapi/AccountStorageIntegrationTest.kt`
- 검증: `./gradlew.bat :apps:game-api:test --tests com.hanjjak.gameapi.AccountStorageIntegrationTest` 성공
- 계약 확인: `corepack pnpm --filter @hanjjak/contracts typecheck && corepack pnpm --filter @hanjjak/contracts build` 성공
- 검증 범위: 가입, 로그인, 세션 조회, 재료 선호 저장, 닉네임 저장, 전투 클리어 `stage_progress` 저장, 지갑 보상 저장, 재접속 복원, 쿠키 유실 후 로그인 복구
- 2026-09-08 현재 전투 command 계약에 맞춰 통합 테스트 클라이언트가 게임 실행 세션을 열고 `X-Game-Session-Id`를 전달하도록 이관했다. 실제 PostgreSQL/Testcontainers 재실행은 로컬 컨테이너 런타임이 없어 수행하지 않았고, Kotlin 테스트 컴파일로 호출 계약을 확인했다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 완료 |
| 검증 | 검증 |
