---
doc_kind: task
owner_domain: delivery
task_code: 'J-01'
task_area: 'J 데이터·테스트·배포'
task_type: '데이터'
priority: 'P0'
target_chapters: '공통'
planning_status: '완료'
development_status: '완료'
verification_status: '완료'
deadline: ''
source_url: 'https://app.notion.com/p/d4506c8781b082dfb807017989e444b7'
notion_id: 'd4506c87-81b0-82df-b807-017989e444b7'
snapshot_date: '2026-08-28'
---

# J-01 공통 게임·경제 이벤트 스키마 정의

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [데이터·이벤트 및 배포·운영](../../../../40-systems/event-system/ssot.md)를 따른다.

## 완료 기준

전투·드롭·제작·강화·분해·거래·주력 재료 선택 이벤트에 계정·챕터·스테이지·아이템·수량·가격·시간 필드를 정의한다.

## 선행 작업

B-02, I-01

## 비고

공통 envelope와 J-01 MVP 이벤트 payload를 TypeSpec 계약으로 정의했다. 런타임 이벤트 수집과 Outbox 저장은 J-02 범위다.

## 증거 링크

- 계약: `packages/contracts/events.tsp`, `packages/contracts/main.tsp`
- 검증: `corepack pnpm --filter @hanjjak/contracts typecheck && corepack pnpm --filter @hanjjak/contracts build` 통과.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 완료 |
| 검증 | 완료 |
