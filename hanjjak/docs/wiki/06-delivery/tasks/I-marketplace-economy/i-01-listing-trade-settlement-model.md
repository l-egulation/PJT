---
doc_kind: task
owner_domain: delivery
task_code: 'I-01'
task_area: 'I 거래소·경제'
task_type: '데이터'
priority: 'P0'
target_chapters: '공통'
planning_status: '완료'
development_status: '부분 구현'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/93406c8781b0837eb0f2814cf7dfafb2'
notion_id: '93406c87-81b0-837e-b0f2-814cf7dfafb2'
snapshot_date: '2026-08-28'
---

# I-01 매수·매도 주문·체결·정산 데이터 모델 정의

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [거래소](../../../../30-domain/economy/ssot.md)를 따른다.

## 완료 기준

UUIDv5 시장 품목, 매수·매도 단일 주문, 매수 쌀 escrow, 매도 물품 외부 보관, maker 가격 체결, 통합 물품 수령함, 거래소 전용 계정 guard·성공 mutation 30일 이력, 계정·stream별 읽지 않음 sequence와 이관 불일치 격리·감사 기록을 서버 권한 데이터로 저장한다.

## 선행 작업

B-02,

## 비고

2026-09-08 최신 main 통합 후 거래소 보관 마이그레이션은 V24로 재번호했으며 신규 격리 DB의 실제 인증 HTTP 재검증 결과는 [F-04 통합 증거](../F-items-inventory/f-04-inventory-query-sort-quantity.md#최신-main-통합-검증)를 따른다. 기존 DB와 팀 DB는 변경하지 않았다.

2026-09-11 단일 주문장 전환으로 UUIDv5 시장 품목, 양방향 `market_order`, maker/taker 체결 참조, 매수 escrow, 매도 외부 보관, 통합 `market_delivery`, 계정 guard·mutation 이력, 네 unread cursor와 이관 감사 테이블을 `V51__unified_market_order_book.sql`에 구현했다. 기존 활성 매도 주문은 시작 시 우선순위를 보존해 이관하고, 구형 매수 예약은 취소·환급하면서 종료 주문 shell과 기존 구매 수령 행을 새 내역에 보존한다. 운영 discrepancy 판정·해결 API와 실제 데이터 이관 스모크는 남아 있어 상태는 부분 구현·부분 검증으로 유지한다.

## 증거 링크

- 2026-09-08 추가: 격리 로컬 `hanjjak_local_verify`에 V21~V23 적용 성공. 실제 인증 HTTP에서 등록·부분 구매·만료·회수의 수량 보존과 만석 회수 실패 시 매물 재고 보존 확인. 아래 DB 미적용 기록 이후의 검증이며 기존 `hanjjak` 및 팀 DB는 계속 미변경이다.

- 2026-09-08: `V25__market_inventory_escrow.sql`로 ACTIVE 매물 잔량과 기존 예약의 일치를 검증한 후 실제 인벤토리에서 차감한다. 기존 자동 반환된 EXPIRED/CANCELLED 매물은 잔량 0으로 이관한다. 기존 로컬·팀 DB에는 미적용이며 보석 거래는 여전히 후속 범위다.

- 구형 구현 이력: `apps/game-api/src/main/resources/db/migration/V8__wallet_market_mail.sql`, 제거된 `Market.kt`·`JdbcMarketRepository.kt`, 현재 `modules/mail/src/main/kotlin/com/hanjjak/mail/infrastructure/JdbcMailRepository.kt`. 당시 검증은 `./gradlew.bat test`였다.

- 2026-09-11 주문장 구현: `apps/game-api/src/main/resources/db/migration/V51__unified_market_order_book.sql`, `modules/market/src/main/kotlin/com/hanjjak/market/domain/OrderBook.kt`, `modules/market/src/main/kotlin/com/hanjjak/market/infrastructure/JdbcOrderBookRepository.kt`. JDK 21 기준 market·inventory·game-api test source 컴파일과 Flyway 버전 유일성 테스트 통과. Docker runtime 부재로 Testcontainers 기반 실제 이관/E2E는 실행하지 못했다.
- 2026-09-12 최신 `origin/main` 병합: main의 오프라인 보상 `V50__offline_rewards.sql`을 보존하고 아직 main에 적용되지 않은 주문장 migration을 다음 미사용 버전 `V51__unified_market_order_book.sql`로 재번호화했다. market·admin·game-api Kotlin test source 컴파일, TypeSpec 생성, 웹·SDK·관리자 콘솔 typecheck/build와 관련 53개 프론트 테스트가 통과했다.
- 2026-09-12 MR 전 JVM gate 보정: `InventoryRepository`의 보석 instance 예약·이관 계약을 관리자 단위 테스트 fake에도 반영하고, 공유 통합 테스트 스키마가 클래스 시작 때 application table을 비운 뒤 `AdminOperationsIntegrationTest`가 시장 품목을 명시적으로 동기화하도록 수정했다. `./gradlew.bat :apps:game-api:test --tests com.hanjjak.gameapi.AdminOperationsIntegrationTest --no-daemon --no-configuration-cache --max-workers=1`과 전체 `./gradlew.bat --no-daemon check` 82개 task가 통과했다.
## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 부분 구현 |
| 검증 | 부분 검증 |
