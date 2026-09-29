---
doc_kind: task
owner_domain: delivery
task_code: 'G-02'
jira_key: 'S15P21B107-169'
task_area: 'G 장비·제작·강화'
task_type: '데이터'
priority: 'P0'
target_chapters: '공통'
planning_status: '완료'
development_status: '완료'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/5ff06c8781b083ceb7e3816de1a49e90'
notion_id: '5ff06c87-81b0-83ce-b7e3-816de1a49e90'
snapshot_date: '2026-08-28'
---

# G-02 장비 최초 해금·등급 비용 정책 반영

> 작업 상태와 완료 기준의 SSOT. 수치와 상태는 [장비 SSOT](../../../../30-domain/items/equipment/ssot.md)를 따른다.

## 완료 기준

6부위 최초 제작 1회, 등급별 강화·승급 비용, 챕터 마지막 스테이지 승급 게이트가 현재 장비 정책과 구현 계약에 연결된다.

## 선행 작업

A-03, F-02, G-01

## 비고

기존 챕터별 반복 제작식은 폐기했다. 모든 부위는 같은 최초 해금 정책을 사용하고, 이후는 부위 상태의 강화·승급만 제공한다.

## 증거 링크

- SSOT: `docs/30-domain/items/equipment/ssot.md`
- 코드: `modules/equipment/src/main/kotlin/com/hanjjak/equipment/domain/Equipment.kt`
- 계약: `packages/contracts/equipment.tsp`, `packages/contracts/generated/openapi/openapi.yaml`
- 검증: `corepack pnpm --filter @hanjjak/contracts build`와 `:modules:equipment:test` 성공. 실제 HTTP 조회에서 6개 슬롯과 공통 해금 비용을 확인했다. 모든 비용 경계와 전 등급은 부분 검증으로 남아 있다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 완료 |
| 검증 | 부분 검증 |
