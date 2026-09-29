---
doc_kind: task
owner_domain: delivery
task_code: 'J-06'
jira_key: 'S15P21B107-189'
task_area: 'J 데이터·테스트·배포'
task_type: '검증'
priority: 'P0'
target_chapters: '공통'
planning_status: '완료'
development_status: '완료'
verification_status: '검증'
deadline: ''
source_url: 'https://app.notion.com/p/b6e06c8781b08205a871015c156aff12'
notion_id: 'b6e06c87-81b0-8205-a871-015c156aff12'
snapshot_date: '2026-08-28'
---

# J-06 제작·강화·거래 E2E 테스트

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [데이터·이벤트 및 배포·운영](../../../../40-systems/event-system/ssot.md)를 따른다.

## 완료 기준

전투 드롭 획득부터 영구 장비 최초 제작, 강화, 등록, 구매, 정산까지 두 계정으로 통과한다.

## 선행 작업

F-05, G-07,

## 비고

2026-09-07 Testcontainers PostgreSQL과 MockMvc 기반 두 계정 E2E가 추가되어 전투 드롭, 장비, 매물 등록·구매·판매자 정산, 경제 지표 집계를 검증했다. 2026-09-08 영구 장비 clean cutover에 맞춰 구형 `/api/v1/equipment/craft`와 장비 UUID 강화 호출을 부위 기반 `WEAPON/unlock`·`WEAPON/enhance`로 이관했다.

현재 E2E는 두 계정을 생성하고 판매자 계정이 전투 드롭으로 영구 장비 해금·강화 비용을 확보한 뒤, 무기를 노말 +1로 해금하고 +2로 강화한다. 이후 감자 M1 등록·구매·10% 수수료 정산 메일 수령과 Outbox·경제 지표까지 한 흐름으로 검증한다.

## 증거 링크

- E2E 테스트: `apps/game-api/src/test/kotlin/com/hanjjak/gameapi/CraftingMarketE2ETest.kt`
- 영구 장비 API: `modules/equipment/src/main/kotlin/com/hanjjak/equipment/api/EquipmentController.kt`
- 영구 장비 명령·DB: `modules/equipment/src/main/kotlin/com/hanjjak/equipment/application/EquipmentService.kt`, `apps/game-api/src/main/resources/db/migration/V24__permanent_equipment_slots.sql`
- 검증: `Z:\gradlew.bat :apps:game-api:test --tests com.hanjjak.gameapi.CraftingMarketE2ETest -x :modules:stage:jar -x :packages:sim-core:jar --no-daemon --no-configuration-cache --max-workers=1 --no-build-cache` 성공
- 검증 범위: 영구 장비 `WEAPON` 해금·강화, `ITEM_DROPPED`, `EQUIPMENT_CRAFTED`, `EQUIPMENT_ENHANCEMENT_ATTEMPTED`, `MARKET_LISTING_CREATED`, `MARKET_TRADE_COMPLETED` Outbox 기록과 `economy_metric_daily`의 `POTATO_M1` 공급·소비·거래·수수료·정산 집계

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 완료 |
| 검증 | 검증 |
