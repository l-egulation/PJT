---
doc_kind: task
owner_domain: delivery
task_code: 'B-04'
jira_key: 'S15P21B107-139'
task_area: 'B 계정·저장'
task_type: '개발'
priority: 'P0'
target_chapters: '공통'
planning_status: '완료'
development_status: '완료'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/d0206c8781b0836b80df8195573b5328'
notion_id: 'd0206c87-81b0-836b-80df-8195573b5328'
snapshot_date: '2026-08-28'
---

# B-04 인벤토리·영구 장비 상태 저장 구현

> 작업 상태와 완료 기준의 SSOT. 저장 규칙은 [계정·저장](../../../../30-domain/player/ssot.md), 장비 규칙은 [장비](../../../../30-domain/items/equipment/ssot.md)를 따른다.

## 완료 기준

재접속 후 아이템 수량과 계정×부위 영구 장비 성장 상태가 일치한다. 장비 장착·잠금·거래 상태는 새 모델에서 저장하지 않는다.

## 선행 작업

B-02, G-01

## 비고

인벤토리 스택과 영구 장비 슬롯 저장을 구현했다. 개발 DB의 구형 장비·장착 데이터는 V23 clean cutover에서 제거한다.

## 증거 링크

- DB: `apps/game-api/src/main/resources/db/migration/V24__permanent_equipment_slots.sql`
- API: `GET /api/v1/equipment`, `POST /api/v1/equipment/{slot}/unlock`, `POST /api/v1/equipment/{slot}/enhance`, `POST /api/v1/equipment/{slot}/promote`
- 검증: JVM·웹 계약 검증과 실제 Flyway V23 적용, HTTP 상태 변경·멱등 재시도·PostgreSQL 잔액/행 대조를 완료했다. 재료 부족 승급에서 트랜잭션 롤백도 확인했으며 재접속 세션 복원은 미검증이다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 완료 |
| 검증 | 부분 검증 |
