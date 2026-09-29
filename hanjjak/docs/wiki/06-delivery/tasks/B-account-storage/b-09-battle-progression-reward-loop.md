---
doc_kind: task
owner_domain: delivery
task_code: 'B-09'
task_area: 'B 계정·저장'
task_type: '개발'
priority: 'P0'
target_chapters: '공통'
planning_status: '부분 완료'
development_status: '완료'
verification_status: '부분 검증'
deadline: ''
source_url: ''
notion_id: ''
snapshot_date: '2026-09-03'
---

# B-09 전투 성장 보상 지급 구현

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [성장](../../../../30-domain/progression/ssot.md), 쌀 저장 권한은 [데이터 권한과 저장 경계](../../../../50-architecture/persistence.md)를 따른다.

## 완료 기준

전투에서 확정 처치된 일반 몬스터와 보스 처치가 경험치·쌀 보상으로 한 번만 반영되고, 레벨업 결과가 다음 전투 스탯과 클라이언트 표시 상태에 반영된다.

## 선행 작업

B-02, D-07, E-01, F-05

## 비고

서버 `modules/progression`이 일반 몬스터와 보스 한 마리의 경험치·MVP working 쌀 보상을 계산하고, 처치별 `settlementId`와 `progression_reward_command`로 중복 지급을 막는다. 보상은 `character.level`, `character.experience`, `wallet_balance.balance`에 즉시 반영되고 웹 정산 응답이 레벨·EXP·쌀 표시를 사이클 완료 전에 갱신한다. 정산으로 오른 레벨의 전투 스탯은 현재 입력을 소급하지 않고 다음 사이클부터 사용한다. 쌀 획득 수치는 경제 시뮬레이션 전 working 기준이다.

## 증거 링크

- `modules/progression/src/main/kotlin/com/hanjjak/progression/domain/Progression.kt`
- `modules/progression/src/main/kotlin/com/hanjjak/progression/application/ProgressionRewardService.kt`
- `modules/progression/src/main/kotlin/com/hanjjak/progression/infrastructure/JdbcProgressionRepository.kt`
- `apps/game-api/src/main/resources/db/migration/V10__progression_reward_commands.sql`
- `apps/game-api/src/main/resources/db/migration/V11__battle_command_records.sql`
- `modules/battle/src/main/kotlin/com/hanjjak/battle/api/BattleController.kt`
- `apps/web/src/features/battle/BattleScreen.tsx`
- 2026-09-03 `./gradlew.bat test`, `corepack pnpm --filter @hanjjak/web test`, `corepack pnpm --filter @hanjjak/web typecheck`, `corepack pnpm --filter @hanjjak/web build`, `corepack pnpm --filter @hanjjak/contracts build`, `corepack pnpm --filter @hanjjak/content-validator validate`, `git diff --check` 통과.
- 2026-09-03 API smoke: fresh 계정에서 챕터 1 자동 진행 후 `experience=473`, `rice=144`, 1-1·1-2 클리어와 1-3 실패 중단 확인.
- 2026-09-03 idempotency smoke: 같은 `Idempotency-Key`로 1-3 단일 전투를 재전송해 경험치·쌀이 중복 증가하지 않음을 확인.
- 2026-09-03 브라우저 smoke: 회원가입→주력 재료 선택→챕터 1 자동 진행→전투 결과 `성장 보상`과 마이페이지 `경험치 473`, `쌀 144` 표시 확인.
- 2026-09-10 처치별 지급: `ProgressionRules.grantForEnemy`, `ProgressionRewardService.grantEnemy`, battle settlements API와 웹 `settledProgression` 상태를 연결했다. 개별 보상 합이 기존 사이클 총량과 같은 단위 검증, 웹 즉시 반영·중복 제거 검증이 통과했다. 실제 PostgreSQL E2E는 컨테이너 런타임 부재로 미실행했다.
- 2026-09-10 MR !118 JVM CI 복구: `ProgressionConfiguration`에서 누락된 `ProgressionRepository` bean을 `JdbcProgressionRepository`로 복원하고 `ProgressionConfigurationTest`로 저장소 bean 계약을 고정했다. Docker/Testcontainers PostgreSQL에서 기존 `AccountFlowIntegrationTest`의 Spring application context와 Flyway V42 적용 시나리오가 통과했다. 이어 현재 카탈로그 표시명에 맞지 않던 전투 기록 기대값을 `미니 감자`로 교정하고, 구매 예약 E2E 품목을 `POTATO_M2`로 분리해 같은 클래스의 `POTATO_M1` 경제 지표 집계를 오염하지 않게 했다. 최종 `gradlew.bat check --no-daemon --rerun-tasks` 전체 81개 task가 통과했다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 부분 완료 |
| 개발 | 완료 |
| 검증 | 부분 검증 |
