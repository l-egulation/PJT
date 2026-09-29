---
doc_kind: task
owner_domain: delivery
task_code: 'I-04'
task_area: 'I 거래소·경제'
task_type: '개발'
priority: 'P0'
target_chapters: '공통'
planning_status: '완료'
development_status: '부분 구현'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/77006c8781b08233b389019aaeeb3cdf'
notion_id: '77006c87-81b0-8233-b389-019aaeeb3cdf'
snapshot_date: '2026-08-28'
---

# I-04 판매 완료 수수료 10% 적용

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [거래소](../../../../30-domain/economy/ssot.md)를 따른다.

## 완료 기준

매수·매도 IOC와 지정가 주문의 실제 개별 체결마다 판매 수수료를 체결 총액의 10%로 계산해 소수점 이하를 내리고, 예상 수수료·순정산액 표시와 체결별 정산 우편·수수료 소각 결과를 일치시킨다.

## 선행 작업

I-03

## 비고

2026-09-11 새 주문 엔진의 체결별 수수료·정산을 구현했다. market 회귀는 15쌀 체결 2건의 개별 수수료 합산 결과를 검증한다. 이후 실제 PostgreSQL E2E에서 수수료 차감 후 정산 청구를 통과했고 브라우저에서도 판매 정산을 수령했다. 전체 수령·재시도·경계값의 전체 인수 검증은 남아 부분 검증을 유지한다.

## 증거 링크

- 백엔드: `modules/market/src/main/kotlin/com/hanjjak/market/application/MarketService.kt`
- UI: `apps/web/src/features/market/MarketScreen.tsx`
- 기존 검증 기록: `./gradlew.bat :modules:market:test --tests com.hanjjak.market.application.MarketServiceTest`, `corepack pnpm --filter @hanjjak/web test`, 실제 브라우저에서 230쌀 등록과 정산 우편 수령 후 목록 제거 확인
- 2026-09-10 1쌀 단위 가격 반영: 83쌀 매물 2개가 166쌀에 체결될 때 수수료 16쌀, 정산 150쌀로 계산되는 회귀 시나리오를 추가했다.
- 2026-09-11 구현: `OrderBookService.match`·`quote`, 체결별 `mail_message`, 웹 `판매 정산` 목록·개별/전체 수령 adapter. market 테스트와 웹 typecheck/build 통과.
- 2026-09-11 PostgreSQL E2E 및 실제 브라우저 정산 검증: 실행 명령과 회귀 결과는 [I-03](./i-03-instant-partial-purchase-settlement.md), 190쌀 매도 체결 후 171쌀 개별 정산 수령은 [K-14](../K-client-ux/k-14-listing-management-settlement-ui.md)에 기록했다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 부분 구현 |
| 검증 | 부분 검증 |
