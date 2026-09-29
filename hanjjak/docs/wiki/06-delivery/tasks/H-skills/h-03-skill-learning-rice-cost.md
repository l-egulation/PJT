---
doc_kind: task
owner_domain: delivery
task_code: 'H-03'
task_area: 'H 스킬·자동 사용'
task_type: '개발'
priority: 'P1'
target_chapters: '공통'
planning_status: '완료'
development_status: '완료'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/cbb06c8781b082d699e981de162a8c58'
notion_id: 'cbb06c87-81b0-82d6-99e9-81de162a8c58'
snapshot_date: '2026-08-28'
---

# H-03 스킬 성장·쌀 차감 구현

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [스킬](../../../../30-domain/character/skills/ssot.md)를 따른다.

## 완료 기준

해금·승급은 요구 스킬북과 등급별 쌀을 한 번 차감하고 확정 적용하며, 2~10강은 단계별 책·쌀·확률·실패 보정을 원자적으로 적용한다.

## 선행 작업

H-01, F-05, 챕터별 쌀 획득량·스킬 성장 정수 비용표

## 비고

2026-09-09 서버가 SkillRules를 주입받아 UNLOCK·ENHANCE·PROMOTE·LOCKED·COMPLETE 액션과 등급별 복합 책·쌀 비용·성공률을 계산한다. 모든 요구 책과 쌀은 기존 트랜잭션에서 원자적으로 차감하고 실패 보정·성공 초기화·멱등 재생을 적용한다. EPIC 10→LEGENDARY는 소비 없이 잠금 거절한다. 서버 소유 SecureRandom 롤 소스를 주입하고, 같은 키 재생은 저장 결과를 재사용한다.

V33은 `skill_command_record.result_version`을 추가한다. 신규 결과는 v2로 저장하며 v1의 구형 `SkillSummary`·`SkillScreenState`는 저장된 JSON만 사용해 현재 action shape로 변환한다. v1 replay는 지갑·인벤토리·현재 정책을 변경하거나 재조회하지 않고 기존 행을 보존한다.

## 증거 링크

- API: `modules/skills/src/main/kotlin/com/hanjjak/skills/application/SkillService.kt`, `modules/skills/src/main/kotlin/com/hanjjak/skills/application/LegacySkillResultReader.kt`, `modules/skills/src/main/kotlin/com/hanjjak/skills/api/SkillConfiguration.kt`
- DB: `apps/game-api/src/main/resources/db/migration/V13__skills_mvp.sql`, `apps/game-api/src/main/resources/db/migration/V33__version_skill_command_results.sql`
- UI: `apps/web/src/features/skills/SkillsScreen.tsx`, `apps/web/src/features/skills/api.ts`
- 검증: `./gradlew.bat :apps:game-api:test --tests com.hanjjak.gameapi.SkillProgressionIntegrationTest`에서 Testcontainers PostgreSQL 기반 17개 시나리오가 실패·오류·건너뜀 없이 통과했다. 복합 책·쌀 원자 차감, rollback, 강화 roll, 실패 보정, 승급, 전설 잠금, v1/v2 멱등 replay와 등급별 전투 profile을 포함한다. 격리한 실제 API·웹에서도 스킬 상태 조회 응답과 화면을 확인했다.
- 2026-09-10 `SkillConfiguration`의 Bean 생성 호출을 현재 6개 인자 `SkillService` 계약과 다시 맞췄고, `:modules:skills:compileKotlin`, `:modules:market:compileTestKotlin`, `:apps:game-api:compileKotlin`을 함께 통과했다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 완료 |
| 검증 | 완료 |
