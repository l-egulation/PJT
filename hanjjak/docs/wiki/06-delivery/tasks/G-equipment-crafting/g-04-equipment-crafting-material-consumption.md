---
doc_kind: task
owner_domain: delivery
task_code: 'G-04'
jira_key: 'S15P21B107-170'
task_area: 'G 장비·제작·강화'
task_type: '개발'
priority: 'P0'
target_chapters: '공통'
planning_status: '완료'
development_status: '완료'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/46106c8781b0831fb58f018fc98c0943'
notion_id: '46106c87-81b0-831f-b58f-018fc98c0943'
snapshot_date: '2026-08-28'
---

# G-04 영구 장비 해금·강화·승급 비용 차감 구현

> 작업 상태와 완료 기준의 SSOT. 도메인 규칙은 [장비](../../../../30-domain/items/equipment/ssot.md)를 따른다.

## 완료 기준

최초 해금·강화·승급 각각의 서버 비용을 원자적으로 차감하고 계정×부위 성장 상태를 한 단계만 변경한다. UUID 장비 생성과 반복 제작은 제공하지 않는다.

## 선행 작업

F-04, F-05

## 비고

서비스는 계정 잠금과 UUID 멱등 키를 유지한다. 비용 부족·성장 게이트 실패는 상태와 잔액을 변경하지 않으며, 동일 명령 재시도는 저장 결과를 재생한다.

## 증거 링크

- API: `modules/equipment/src/main/kotlin/com/hanjjak/equipment/application/EquipmentService.kt`
- 저장소: `modules/equipment/src/main/kotlin/com/hanjjak/equipment/infrastructure/JdbcEquipmentRepository.kt`
- DB: `apps/game-api/src/main/resources/db/migration/V24__permanent_equipment_slots.sql`
- 검증: `:modules:equipment:test` 성공. 실제 HTTP에서 CAPE 해금, 동일 키 재시도, WEAPON 강화, GLOVES 승급을 실행하고 DB에서 상태·쌀·재료 차감을 대조했다. `wallet_ledger`의 `EQUIPMENT_UNLOCK` 원장과 idempotency source ID, 재료 부족 승급의 상태·쌀·재료 롤백도 확인했다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 완료 |
| 검증 | 부분 검증 |
