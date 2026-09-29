---
doc_kind: task
owner_domain: delivery
task_code: 'C-03'
task_area: 'C 주력 재료'
task_type: '데이터'
priority: 'P0'
target_chapters: '공통'
planning_status: '완료'
development_status: '완료'
verification_status: '완료'
deadline: ''
source_url: 'https://app.notion.com/p/db406c8781b083459402811766fbe4f3'
notion_id: 'db406c87-81b0-8345-9402-811766fbe4f3'
snapshot_date: '2026-08-28'
---

# C-03 주력 재료 80:10:10 드롭 비율 적용

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [주력 재료](../../../../30-domain/character/ssot.md)를 따른다.

## 완료 기준

사냥의 재료 보상 1개마다 선택 재료 80%, 나머지 두 재료 각각 10%로 종류를 독립 추첨한다.

## 선행 작업

C-01, C-02

## 비고

80:10:10 계열 가중치와 경계값, 동일 seed 재현은 기준 브랜치의 `modules/inventory/src/main/kotlin/com/hanjjak/inventory/domain/DropTable.kt`와 `DropTableTest.kt`에서 검증한다. 재료 콘텐츠의 `working` 수치와 보상 연결은 현재 기능 브랜치에서 병합 검증한다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 완료 |
| 검증 | 완료 |
