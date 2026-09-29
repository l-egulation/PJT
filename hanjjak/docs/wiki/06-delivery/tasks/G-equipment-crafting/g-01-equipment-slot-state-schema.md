---
doc_kind: task
owner_domain: delivery
task_code: 'G-01'
jira_key: 'S15P21B107-168'
task_area: 'G 장비·제작·강화'
task_type: '데이터'
priority: 'P0'
target_chapters: '공통'
planning_status: '완료'
development_status: '완료'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/0d7066c8781b08224a4df01157ef33cda'
notion_id: '0d7066c87-81b0-8224-a4df-01157ef33cda'
snapshot_date: '2026-08-28'
---

# G-01 장비 6부위·영구 성장 상태 스키마 정의

> 작업 상태와 완료 기준의 SSOT. 현재 규칙은 [장비 SSOT](../../../../30-domain/items/equipment/ssot.md)를 따른다.

## 완료 기준

계정×부위 단일 행으로 해금·등급·강화 단계와 시각을 저장하고, 미해금 부위는 행 없음으로 표현한다. 다중 인스턴스·장착·잠금·거래 상태는 새 모델의 범위가 아니다.

## 선행 작업

없음

## 비고

승인된 영구 장비 모델로 V23에서 기존 장비 인스턴스·장착 상태와 장비 command 기록을 개발 데이터 기준으로 교체한다. 계정·지갑·인벤토리·치장 데이터는 초기화하지 않는다.

## 증거 링크

- DB: `apps/game-api/src/main/resources/db/migration/V24__permanent_equipment_slots.sql`
- 코드: `modules/equipment/src/main/kotlin/com/hanjjak/equipment/domain/Equipment.kt`, `modules/equipment/src/main/kotlin/com/hanjjak/equipment/infrastructure/JdbcEquipmentRepository.kt`
- 검증: `:modules:equipment:test` 성공. 실제 PostgreSQL에서 Flyway V23 적용, `equipment_slot_state`·`equipment_command_record` 생성, 구형 장비·로드아웃 테이블 제거를 확인했다. 계정·지갑·인벤토리·스테이지 행은 보존됐다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 완료 |
| 검증 | 부분 검증 |
