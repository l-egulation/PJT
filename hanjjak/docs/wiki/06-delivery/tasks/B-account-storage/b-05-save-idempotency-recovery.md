---
doc_kind: task
owner_domain: delivery
task_code: 'B-05'
task_area: 'B 계정·저장'
task_type: '개발'
priority: 'P1'
target_chapters: '공통'
planning_status: '미확인'
development_status: '부분 구현'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/f7306c8781b0826e9010812357d9bfc5'
notion_id: 'f7306c87-81b0-826e-9010-812357d9bfc5'
snapshot_date: '2026-08-28'
---

# B-05 저장 요청 중복·복구 정책 구현

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [계정·저장](../../../../30-domain/player/ssot.md)를 따른다.

## 완료 기준

중복 요청과 강제 종료에서도 쌀·아이템이 중복 지급되거나 사라지지 않는다.

## 선행 작업

B-02

## 비고

회원가입·로그인·치장 mutation과 전투 세션 start·heartbeat·settlements·complete·abort는 UUID 멱등성 키, fingerprint, 저장 결과 재사용과 payload 충돌 거부를 구현했다. 처치 정산은 `battle_enemy_settlement`의 `(battle_session_id, enemy_index)` 유일성과 결정론 `settlementId`를 보상·성장 내부 멱등 키로 사용한다. 처리 중 상태 조회와 실제 PostgreSQL 강제 종료 복구 검증은 남아 있다.

## 증거 링크

- 구현: `modules/account/src/main/kotlin/com/hanjjak/account/application/AuthenticationCommandService.kt`, `modules/cosmetics/src/main/kotlin/com/hanjjak/cosmetics/application/CosmeticsCommandService.kt`
- 저장: `apps/game-api/src/main/resources/db/migration/V1__account_and_progress.sql`, `apps/game-api/src/main/kotlin/com/hanjjak/gameapi/JdbcCosmeticsCommandRepository.kt`
- 전투 세션 멱등 저장: `apps/game-api/src/main/resources/db/migration/V19__battle_session.sql`, `modules/battle/src/main/kotlin/com/hanjjak/battle/application/BattleSessionService.kt`
- 2026-09-08 검증: 웹 session API·지속 루프 테스트와 Kotlin 전투 입력 재현 테스트가 통과했다. 실제 PostgreSQL/Testcontainers 검증은 로컬 컨테이너 런타임 부재로 미실행했다.
- 2026-09-10 몬스터별 정산: `V40__battle_enemy_settlement.sql`, `BattleEnemySettlementService`, settlements API를 추가했다. 같은 정산 응답의 웹 중복 반영 방지, 정산 키 재사용, Kotlin 컴파일·모듈 테스트는 통과했다. Testcontainers E2E는 Docker·Podman·WSL·로컬 PostgreSQL 부재로 초기화하지 못했다.
- 2026-09-02 빌드·단위 테스트는 통과했으나 이 작업의 중복·강제 종료 시나리오는 실행하지 않았다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 미확인 |
| 개발 | 부분 구현 |
| 검증 | 부분 검증 |
