---
doc_kind: task
owner_domain: delivery
task_code: 'G-06'
task_area: 'G 장비·제작·강화'
task_type: '개발'
priority: 'P1'
target_chapters: '공통'
planning_status: '완료'
development_status: '완료'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/c1106c8781b083fbabb9814c7155881d'
notion_id: 'c1106c87-81b0-83fb-abb9-814c7155881d'
snapshot_date: '2026-08-28'
---

# G-06 영구 장비 적용과 구형 기능 제거

> 작업 상태와 완료 기준의 SSOT. 현재 규칙은 [장비](../../../../30-domain/items/equipment/ssot.md)를 따른다.

## 완료 기준

해금한 부위가 별도 장착 없이 전투에 적용되고, 다중 보유·장착·잠금·분해·판매·거래 기능과 구형 UUID API가 제거된다.

## 선행 작업

G-01, I-01

## 비고

영구 부위 상태는 항상 적용된다. 완성 장비 거래와 기존 인스턴스 부가 기능은 새 모델에서 제공하지 않는다.

## 증거 링크

- DB: `apps/game-api/src/main/resources/db/migration/V24__permanent_equipment_slots.sql`
- API: `modules/equipment/src/main/kotlin/com/hanjjak/equipment/api/EquipmentController.kt`
- UI: `apps/web/src/features/equipment/EquipmentScreen.tsx`
- 검증: 구형 호출 참조 검색에서 런타임 코드·계약의 UUID 장비 API가 제거됐고 JVM·웹 정적 검증이 성공했다. 실제 브라우저에서 해금 부위가 장착 조작 없이 캐릭터 능력치 출처에 반영되는 것을 확인했다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 완료 |
| 검증 | 부분 검증 |
