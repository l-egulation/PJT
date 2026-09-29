---
doc_kind: task
owner_domain: delivery
task_code: 'I-05'
task_area: 'I 거래소·경제'
task_type: '개발'
priority: 'P0'
target_chapters: '공통'
planning_status: '완료'
development_status: '부분 구현'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/00906c8781b082d18a0181b0f8ffd29a'
notion_id: '00906c87-81b0-82d1-8a01-81b0f8ffd29a'
snapshot_date: '2026-08-28'
---

# I-05 주문 동시성·우선순위·멱등성 구현

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [거래소](../../../../30-domain/economy/ssot.md)를 따른다.

## 완료 기준

동일 시장 품목의 등록·IOC·수정·취소·만료를 품목 row 잠금 아래 직렬화해 `SKIP LOCKED` 없이 maker 가격과 엄격한 가격·시간 우선순위를 보장한다. 멱등 재생은 확정 결과를 재사용하고, 활성 주문 30개와 상태를 바꾼 성공 mutation 최근 1분 30회를 원자 판정한다.

## 선행 작업

I-03, F-05

## 비고

2026-09-11 동일 시장 품목의 주문 등록·IOC·수정·취소·만료를 `market_instrument FOR UPDATE` 아래 직렬화하고, 후보 주문은 `SKIP LOCKED` 없이 가격·우선순위 시각·UUID 순으로 잠근다. command replay, 계정별 활성 주문 30개와 최근 1분 성공 mutation 30회 guard도 단일 transaction에 구현했다. 단위 테스트와 컴파일은 통과했지만 확정 인수 조건인 hot instrument 동시 100건 실제 PostgreSQL 부하는 Docker runtime 부재로 실행하지 못해 상태는 부분으로 유지한다.

## 증거 링크

- 2026-09-08 실제 인증 HTTP: 동일 멱등 키의 등록·구매·만료 잔량 회수 재요청에서 중복 차감/지급 없음 확인. 순차 재시도 검증이며 동시 구매·회수 경합 및 부하 실험을 대체하지 않는다. 격리 DB `hanjjak_local_verify`의 신규 검증 계정만 사용했다.

- 2026-09-08: 거래소 출고 방식 변경 후 단일·일괄 구매 수용량 초과 무변경 회귀 통과. 기존 명령 멱등성과 잠금 순서는 유지한다. 새 만료·회수 정책의 실제 동시 요청 실험은 별도 인수가 필요하다.

- 백엔드: `modules/market/src/main/kotlin/com/hanjjak/market/application/MarketService.kt`, `modules/market/src/main/kotlin/com/hanjjak/market/infrastructure/JdbcMarketRepository.kt`
- 테스트: `modules/market/src/test/kotlin/com/hanjjak/market/application/MarketServiceTest.kt`
- 검증: `./gradlew.bat test`
- 2026-09-11 코드: `OrderBookService`, `JdbcOrderBookRepository`, `market_account_control`, `market_order_mutation_history`. 검증: `:modules:market:test --tests ...OrderBookServiceTest` 통과, game-api test source 컴파일 통과.
- 2026-09-11 추가 검증: 주문 견적도 시장 품목 row lock 아래 자기 반대 주문 검사·후보 조회·revision 반환을 수행하며, 웹 확인 화면은 동일 멱등 키로 결과 유실 재시도를 허용한다. 로컬 Chromium에서 첫 요청을 응답 유실로 처리한 뒤 재제출해 동일 `orderId`·`commandId`와 단일 거래 결과를 확인했다. hot instrument 동시 100건 부하와 revision gap 자동 복구는 미검증·미구현으로 상태를 유지한다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 부분 구현 |
| 검증 | 부분 검증 |
