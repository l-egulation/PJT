---
doc_kind: task
owner_domain: delivery
task_code: 'B-02'
task_area: 'B 계정·저장'
task_type: '데이터'
priority: 'P0'
target_chapters: '공통'
planning_status: '미확인'
development_status: '완료'
verification_status: '완료'
deadline: ''
source_url: 'https://app.notion.com/p/aca06c8781b082c9b058818b0d90b8d8'
notion_id: 'aca06c87-81b0-82c9-b058-818b0d90b8d8'
snapshot_date: '2026-08-28'
---

# B-02 사용자·캐릭터 기본 데이터 모델 정의

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [계정·저장](../../../../30-domain/player/ssot.md)를 따른다.

## 완료 기준

사용자 ID, 캐릭터 ID, 레벨, 경험치, 쌀, 진행도를 저장할 스키마가 확정된다.

## 선행 작업

A-02

## 비고

계정 UUID, 캐릭터 UUID, 닉네임, 레벨, 경험치, 쌀과 계정별 스테이지 진행을 PostgreSQL/Flyway 스키마와 Kotlin 도메인 모델에 반영했다. 실제 PostgreSQL에서 전체 migration과 계정·캐릭터 복원을 확인하는 Testcontainers 테스트를 실행해 검증했다.

## 증거 링크

- 스키마: `apps/game-api/src/main/resources/db/migration/V1__account_and_progress.sql`, `apps/game-api/src/main/resources/db/migration/V9__character_nickname.sql`
- 도메인 모델: `modules/account/src/main/kotlin/com/hanjjak/account/domain/Account.kt`, `modules/stage/src/main/kotlin/com/hanjjak/stage/domain/StageProgress.kt`
- 2026-09-03 `./gradlew.bat :modules:account:test`, `./gradlew.bat :apps:game-api:compileKotlin` 통과. 로컬 PostgreSQL `hanjjak_auth_login`에서 Flyway V9 적용 확인. Testcontainers migration 검증은 실행하지 않았다.
- 2026-09-07 `apps/game-api/src/test/kotlin/com/hanjjak/gameapi/AccountFlowIntegrationTest.kt`에 Flyway 전체 schema 기동, 계정·캐릭터·닉네임·주력 재료 저장과 재로그인 복원 검증을 추가했다. 테스트 컴파일은 통과했고 로컬 Docker 서비스 장애로 실행은 자동 건너뛰었다.
- 2026-09-08 Docker 엔진 복구 후 `./gradlew.bat :apps:game-api:test --tests com.hanjjak.gameapi.AccountFlowIntegrationTest`를 실행해 PostgreSQL 17.6 컨테이너의 전체 Flyway schema, 계정·캐릭터 생성과 재로그인 복원 검증을 통과했다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 미확인 |
| 개발 | 완료 |
| 검증 | 완료 |
