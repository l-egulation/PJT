---
doc_kind: task
owner_domain: delivery
task_code: 'F-03'
task_area: 'F 드롭·인벤토리'
task_type: '개발'
priority: 'P0'
target_chapters: '공통'
planning_status: '완료'
development_status: '부분 구현'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/70006c8781b083aeb8b281239fe621e0'
notion_id: '70006c87-81b0-83ae-b8b2-81239fe621e0'
snapshot_date: '2026-08-28'
---

# F-03 주력 재료 80:10:10 드롭 적용

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [아이템](../../../../30-domain/items/ssot.md)과 [주력 재료](../../../../30-domain/character/ssot.md)를 따른다.

## 완료 기준

사냥의 재료 보상 1개마다 서버가 챕터별 누적 풀에서 세대를 선택하고, 선택 세대 안에서 주력 재료 80%, 나머지 두 재료 각각 10%로 계열을 독립 추첨해 결과를 원자적으로 지급한다.

## 선행 작업

C-03,

## 비고

working 콘텐츠의 25회 독립 기회, 챕터별 세대 가중치, 80:10:10 계열 추첨과 서버 확정 지급을 구현했다. 기준 브랜치의 인벤토리 보상 계층과 함께 사용하며 전체 보상 범위는 F-05 상태를 따른다.

## 증거 링크

- 콘텐츠·드롭: `packages/game-content/versions/v1/drops/material-drops.json`, `packages/game-content/schema/material-drops.schema.json`, `modules/inventory/src/main/kotlin/com/hanjjak/inventory/domain/DropTable.kt`
- 서버 지급: `modules/inventory/src/main/kotlin/com/hanjjak/inventory/application/BattleRewardService.kt`, `modules/inventory/src/main/kotlin/com/hanjjak/inventory/application/InventoryRewardService.kt`
- [주력 재료 구현 증거](../../../../70-plans/material-preference/implementation-evidence.md)

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 부분 구현 |
| 검증 | 부분 검증 |
