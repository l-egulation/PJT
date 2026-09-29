---
doc_kind: task
owner_domain: delivery
task_code: 'G-07'
jira_key: 'S15P21B107-171'
task_area: 'G 장비·제작·강화'
task_type: '검증'
priority: 'P1'
target_chapters: '공통'
planning_status: '완료'
development_status: '완료'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/7b306c8781b083a4b54981add66fa17b'
notion_id: '7b306c87-81b0-83a4-b549-81add66fa17b'
snapshot_date: '2026-08-28'
---

# G-07 영구 장비 비용·성장 경제 검증

> 작업 상태와 완료 기준의 SSOT. 수치 정책은 [장비](../../../../30-domain/items/equipment/ssot.md)를 따른다.

## 완료 기준

최초 해금·등급별 강화·승급의 비용, Q 경계, 재료 세대 누적 규칙과 서버 응답 미리보기가 서로 일치한다.

## 선행 작업

G-04

## 비고

2026-09-10: 차기 정책 전환과 추가 인수는 [A-05](../A-release-scope/a-05-progression-rebalance.md)에서 추적한다. 이 문서의 기존 상태·증거는 기존 버전 범위에 한정하며 차기 구현 완료를 뜻하지 않는다.


도메인 및 서비스 테스트에서 비용 경계·재료 묶음·승급 비용·멱등성을 검증했다. 플레이 시간과 실제 재료 공급량을 포함한 거시 경제 검증은 별도 범위로 남긴다.

## 증거 링크

- 규칙: `modules/equipment/src/test/kotlin/com/hanjjak/equipment/domain/EquipmentRulesTest.kt`
- 명령: `modules/equipment/src/test/kotlin/com/hanjjak/equipment/application/EquipmentServiceTest.kt`
- 검증: `:modules:equipment:test` 성공. 실제 HTTP/DB에서 해금·강화·승급 비용, wallet ledger source, 서버 `statIncrease`와 상태 결과를 대조했고 재료 부족 승급의 잔액·상태 롤백을 확인했다. 실제 플레이 시간·공급량 기반 경제 시뮬레이션은 미검증이다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 완료 |
| 검증 | 부분 검증 |
