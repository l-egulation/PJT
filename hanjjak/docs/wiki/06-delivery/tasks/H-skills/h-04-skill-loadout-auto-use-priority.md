---
doc_kind: task
owner_domain: delivery
task_code: 'H-04'
task_area: 'H 스킬·자동 사용'
task_type: '개발'
priority: 'P1'
target_chapters: '공통'
planning_status: '완료'
development_status: '부분 구현'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/faa06c8781b08314a8fc812fd789fb79'
notion_id: 'faa06c87-81b0-8314-a8fc-812fd789fb79'
snapshot_date: '2026-08-28'
---

# H-04 스킬 장착·자동 사용 우선순위 구현

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [스킬](../../../../30-domain/character/skills/ssot.md)를 따른다.

## 완료 기준

목록 앞에서부터 쿨이 끝난 첫 스킬을 사용하고 없으면 기본 공격하며 행동 종료 후 처음부터 재평가한다.

## 선행 작업

H-03, B-02

## 비고

액티브 4슬롯 순서 저장과 전투 엔진의 순서 기반 자동 사용을 구현했다. 공속·기본공격 피해 버프는 발동 행동에 같은 행동의 기본공격을 함께 수행하며, 메인 전투는 서버 이벤트 preview와 코드 기반 피드백을 사용한다. 실제 해금 스킬의 장시간 DPS와 브라우저 실기 검증은 남아 있다.

## 증거 링크

- 전투: `packages/sim-core/src/main/kotlin/com/hanjjak/sim/CombatSimulator.kt`, `modules/battle/src/main/kotlin/com/hanjjak/battle/api/BattleController.kt`; 버프 동시 기본공격과 서버 이벤트 preview를 추가했다.
- UI: `apps/web/src/features/skills/SkillsScreen.tsx`, `apps/web/src/features/battle/PetBattleSurface.tsx`
- 검증: ASCII 경로에서 `./gradlew.bat :packages:sim-core:test`가 성공했고, `corepack pnpm --filter @hanjjak/web exec vitest run src/features/skills/api.test.ts src/features/skills/SkillsScreen.test.tsx`에서 API 멱등 키와 불확실 결과 동일 요청 재시도를 포함한 5개 테스트가 통과했다. 격리한 PostgreSQL·API·웹에서 실제 스킬 화면까지 확인했으며, 해금 스킬의 장시간 DPS 검증은 부분 상태다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 부분 구현 |
| 검증 | 부분 검증 |
