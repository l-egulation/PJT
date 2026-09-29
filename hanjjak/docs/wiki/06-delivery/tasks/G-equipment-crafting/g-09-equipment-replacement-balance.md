---
doc_kind: task
owner_domain: delivery
task_code: 'G-09'
task_area: 'G 장비·제작·강화'
task_type: '검증'
priority: 'P1'
target_chapters: '챕터 1, 챕터 2, 챕터 3'
planning_status: '완료'
development_status: '완료'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/05606c8781b083c9ab1a810202e098c8'
notion_id: '05606c87-81b0-83c9-ab1a-810202e098c8'
snapshot_date: '2026-08-28'
---

# G-09 반복 재제작 대체와 영구 성장 검증

> 작업 상태와 완료 기준의 SSOT. 현재 규칙은 [장비](../../../../30-domain/items/equipment/ssot.md)를 따른다.

## 완료 기준

반복 제작·세대 교체 수요 기준을 폐기하고 부위별 최초 제작 1회와 강화·챕터 승급 성장 흐름으로 대체한다. 등급 경계·재료 세대·최종 상태를 검증한다.

## 선행 작업

G-08

## 비고

2026-09-10: 차기 정책 전환과 추가 인수는 [A-05](../A-release-scope/a-05-progression-rebalance.md)에서 추적한다. 이 문서의 기존 상태·증거는 기존 버전 범위에 한정하며 차기 구현 완료를 뜻하지 않는다.


구형 `상위 장비 4작 < 이전 장비 7작 < 상위 장비 5작` 및 2~3시간 재제작 수요는 현재 모델에 적용하지 않는다. 도메인 경계 테스트는 통과했으나 실제 공급량 기반 경제 검증은 남아 있다.

## 증거 링크

- 규칙: `docs/30-domain/items/equipment/ssot.md`
- 테스트: `modules/equipment/src/test/kotlin/com/hanjjak/equipment/domain/EquipmentRulesTest.kt`
- 검증: 등급·Q·묶음 경계 단위 테스트와 실제 RARE +1/Q40 승급을 확인했다. 실제 운영 경제 검증은 미검증이다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 완료 |
| 검증 | 부분 검증 |
