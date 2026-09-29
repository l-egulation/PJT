---
doc_kind: task
owner_domain: delivery
task_code: 'I-08'
task_area: 'I 거래소·경제'
task_type: '개발'
priority: 'P0'
target_chapters: '공통'
planning_status: '완료'
development_status: '부분 구현'
verification_status: '부분 검증'
deadline: ''
source_url: ''
notion_id: ''
snapshot_date: '2026-09-10'
---

# I-08 매수 지정가·자동 체결·통합 수령 구현

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [거래소](../../../../30-domain/economy/ssot.md)를 따른다.

## 완료 기준

단일 주문 모델에 시장 품목·수량·지정가로 매수 주문을 만들고 최대 지출 쌀을 원자 확보한다. maker 가격과 품목 row 직렬화로 매도 주문과 체결하며 취소·만료·수량 감소는 escrow를 반환하고 체결 물품은 원본 행 단위 통합 수령함에서 한 번만 지급한다.

## 선행 작업

I-01, I-02, I-03, I-04, I-05

## 비고

2026-09-11 새 단일 주문 모델에 매수 GTC를 구현했다. 주문 시 `미체결 수량 × 지정가`를 지갑에서 선차감하고 후속 매도 주문을 maker 가격으로 체결하며, 체결 물품은 통합 수령함에 보관하고 취소·만료·수량 감소 escrow를 반환한다. 기존 활성 구매 예약은 시작 시 취소·환급하고 종료 주문 shell과 구매 delivery는 원본 참조로 이관한다. 웹은 매수 지정가 생성·내 주문 취소·통합 물품 개별/전체 수령을 노출한다. 실제 PostgreSQL 이관과 보석 delivery E2E가 남아 상태는 부분으로 유지한다.

## 증거 링크

- DB·도메인: `apps/game-api/src/main/resources/db/migration/V41__market_buy_orders.sql`, `modules/market/src/main/kotlin/com/hanjjak/market/domain/Market.kt`, `modules/market/src/main/kotlin/com/hanjjak/market/application/MarketPorts.kt`, `modules/market/src/main/kotlin/com/hanjjak/market/infrastructure/JdbcMarketRepository.kt`
- 유스케이스·HTTP: `modules/market/src/main/kotlin/com/hanjjak/market/application/MarketBuyOrderService.kt`, `modules/market/src/main/kotlin/com/hanjjak/market/application/MarketService.kt`, `modules/market/src/main/kotlin/com/hanjjak/market/api/MarketController.kt`, `modules/market/src/main/kotlin/com/hanjjak/market/api/MarketConfiguration.kt`
- 계약: `packages/contracts/main.tsp`, `packages/contracts/events.tsp`, `packages/contracts/generated/openapi/openapi.yaml`
- 회귀 검증: `modules/market/src/test/kotlin/com/hanjjak/market/application/MarketServiceTest.kt`에서 쌀 escrow, 기존·후속 판매 매물 단가 체결, 차액 환급, 외부 보관 수령, 판매 정산, 취소 환급, 같은 시각에 생성된 구매 주문의 cursor 누락 방지를 검증했다. `:modules:market:test` 36개와 `:apps:game-api:compileTestKotlin`이 통과했다.
- 실제 백엔드 스모크: 격리 스키마 `market_buy_playground_20260910`에서 판매자·구매자 계정과 임의 매물을 구성했다. 감자 M1 2개 즉시 구매로 240쌀 차감, 감자 M2 최대 300쌀 예약에 250쌀 매물 4개 자동 체결, 200쌀 가격 개선 환급, 잔여 예약 6개·1,800쌀 유지, 수령 후 감자 M2 수량 500→504를 확인했다.
- UI 보류: 2026-09-10 사용자 지시에 따라 구매 예약 버튼·목록·수령 UI와 프론트 adapter를 기존 거래소 UI로 원복했다. UI 구현·브라우저 인수는 후속 거래소 UI 개편 범위다.
- 2026-09-11 회귀: `OrderBookServiceTest`에서 300쌀 escrow 선차감, 후속 SELL IOC 2개 체결 뒤 잔여 100쌀 유지와 delivery 생성을 확인했다. `CraftingMarketE2ETest`에는 실제 HTTP GTC 매수→SELL IOC→수령 시나리오가 있으나 현 머신은 Docker runtime이 없어 실행되지 않았다.
- 2026-09-11 후속 검증: 위 미실행 상태 이후 PostgreSQL 기반 `CraftingMarketE2ETest`의 GTC 매수→SELL IOC→수령 시나리오가 통과했다. 체결 row를 먼저 생성하는 수정과 실행 명령은 [I-03](./i-03-instant-partial-purchase-settlement.md)에 기록했다. 실제 기존 데이터 이관·보석 delivery의 잔여 검증 상태는 유지한다.
- 2026-09-11 구매 예약 상한도 즉시 구매와 같은 10~999,990쌀 범위의 1쌀 단위 정수로 통일했다. 서버 검증과 `market_buy_order.max_unit_price` 제약을 갱신하고 13쌀 예약이 39쌀을 escrow에 보관하는 회귀 테스트를 추가했다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 부분 구현 |
| 검증 | 부분 검증 |
