---
doc_kind: task
owner_domain: delivery
task_code: 'J-13'
task_area: 'J 데이터·테스트·배포'
task_type: '개발'
priority: 'P0'
target_chapters: '공통'
planning_status: '완료'
development_status: '부분 구현'
verification_status: '부분 검증'
source: 'repository-implementation-sync'
created_at: '2026-09-02'
---

# J-13 코드베이스 기반 구축

> 작업 상태와 완료 증거의 SSOT. 기술 기준은 [코드베이스 기반 아키텍처 결정](../../../../80-decisions/architecture/codebase-foundation.md)과 [모노레포 설계](../../../../50-architecture/monorepo.md)를 따른다.

## 완료 기준

확정된 모노레포·클라이언트·서버·시뮬레이션·계약·콘텐츠·로컬 인프라 경계가 실행 가능한 프로젝트로 구성되고, 결정에서 요구한 생성물·경계·관측성·패키징 검증을 통과한다.

## 선행 작업

없음

## 비고

pnpm·Gradle workspace, React·Vite 앱, Kotlin·Spring Boot 앱과 도메인 모듈, Kotlin 시뮬레이터, TypeSpec·OpenAPI, 버전 콘텐츠·검증기, PostgreSQL Compose, GitLab CI와 실행 앱 container image를 구축했다. TypeScript 전투 예측기·golden replay, 생성 클라이언트와 Kotlin interface/DTO 사용, ArchUnit, 공통 OpenTelemetry·구조화 로그는 남아 있다.

## 증거 링크

- workspace·빌드: `package.json`, `pnpm-workspace.yaml`, `settings.gradle.kts`, `build.gradle.kts`
- 실행 앱: `apps/web`, `apps/admin-console`, `apps/game-api`, `apps/market-worker`, `apps/event-consumers`, `apps/balance-lab`
- 공유 패키지·계약·콘텐츠: `packages`, `tools/content-validator`
- 로컬·CI·image: `infra/local/compose.yaml`, `.gitlab-ci.yml`, `apps/*/Dockerfile`
- 2026-09-02 `pnpm build`, `pnpm test`, `./gradlew.bat test`, `pnpm content:validate` 통과.
- 2026-09-10 Flyway 버전 충돌 수정: 거래소 구매 예약 `V41__market_buy_orders.sql`이 이미 존재하는 상태에서 추가된 채팅 migration의 중복 `V41`을 `apps/game-api/src/main/resources/db/migration/V42__chat_community.sql`로 재번호화했다. `FlywayMigrationVersionTest`가 runtime classpath의 migration 버전 중복을 직접 거부한다.
- 검증: `:apps:game-api:test --tests com.hanjjak.gameapi.FlywayMigrationVersionTest`와 `:apps:game-api:compileTestKotlin` 통과. 처리된 runtime resources에는 `V41__market_buy_orders.sql`, `V42__chat_community.sql`이 각각 한 번만 포함된다. 전체 game-api 테스트는 로컬 Docker 미설치로 Testcontainers 기반 11개 클래스가 초기화되지 않아 완료하지 못했으며, 출력에서 Flyway resolver 중복 오류는 재발하지 않았다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 부분 구현 |
| 검증 | 부분 검증 |
