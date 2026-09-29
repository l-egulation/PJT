---
doc_kind: task
owner_domain: delivery
task_code: 'E-02'
task_area: 'E 자동전투·스탯'
task_type: '개발'
priority: 'P0'
target_chapters: '공통'
planning_status: '미확인'
development_status: '완료'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/91006c8781b083469344812386eae9d5'
notion_id: '91006c87-81b0-8346-9344-812386eae9d5'
snapshot_date: '2026-08-28'
---

# E-02 공격·방어·HP 피해 계산 구현

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [전투](../../../../30-domain/combat/ssot.md)를 따른다.

## 완료 기준

정의된 스탯으로 피해와 생존 결과가 일관되게 계산된다.

## 선행 작업

E-01

## 비고

공격력·방어 관통·적 방어력과 스킬 배율로 피해를 결정론적으로 계산하고 최소 피해 1과 반올림 규칙을 적용한다. 현재 테스트는 동일 입력의 결정성 중심이며 피해 경계값 표 전체는 검증하지 않았다.

## 증거 링크

- 구현: `packages/sim-core/src/main/kotlin/com/hanjjak/sim/CombatSimulator.kt`
- 단위 검증: `packages/sim-core/src/test/kotlin/com/hanjjak/sim/CombatSimulatorTest.kt`
- 2026-09-02 `./gradlew.bat test` 통과.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 미확인 |
| 개발 | 완료 |
| 검증 | 부분 검증 |
