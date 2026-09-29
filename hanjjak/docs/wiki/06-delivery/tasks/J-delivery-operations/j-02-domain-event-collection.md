---
doc_kind: task
owner_domain: delivery
task_code: 'J-02'
task_area: 'J 데이터·테스트·배포'
task_type: '개발'
priority: 'P0'
target_chapters: '공통'
planning_status: '부분 완료'
development_status: '부분 구현'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/48206c8781b08261b65281bf54e0d616'
notion_id: '48206c87-81b0-8261-b652-81bf54e0d616'
snapshot_date: '2026-08-28'
---

# J-02 전투·드롭·제작·거래 이벤트 수집 구현

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [데이터·이벤트 및 배포·운영](../../../../40-systems/event-system/ssot.md)를 따른다.

## 완료 기준

전투·드롭·제작·강화·분해·거래 이벤트를 중복 없이 수집하고 5일·7일 검증 지표와 연결한다.

## 선행 작업

J-01

## 비고

J-01 TypeSpec 계약을 기준으로 Outbox 저장 경계를 추가했다. 현재 구현 경로에서는 전투 사이클 완료, 아이템 드롭, 영구 장비 해금, 영구 장비 강화, 영구 장비 승급, 매물 등록, 매물 취소, 거래 완료, 주력 재료 선택 이벤트를 같은 트랜잭션의 `outbox_event`에 저장한다. 영구 장비 이벤트는 `equipmentSlot`과 계정·부위 기반 안정 aggregate ID를 사용한다. `apps:event-consumers`는 `outbox_event`의 `PENDING` 이벤트를 Kafka `domain-events.v1`로 발행한다. 장비 분해는 MVP 런타임에서 제외되어 이벤트 생산도 제공하지 않는다. 5일·7일 지표 연결은 J-03·J-11이 소유한다.

## 증거 링크

- 계약: `packages/contracts/events.tsp`
- 구현: `modules/events`, `modules/account/src/main/kotlin/com/hanjjak/account/application/MaterialPreferenceService.kt`, `modules/battle/src/main/kotlin/com/hanjjak/battle/api/BattleController.kt`, `modules/equipment/src/main/kotlin/com/hanjjak/equipment/application/EquipmentService.kt`, `modules/market/src/main/kotlin/com/hanjjak/market/application/MarketService.kt`
- 검증: `./gradlew.bat :modules:account:test :modules:events:test :apps:game-api:test --tests com.hanjjak.gameapi.AccountStorageIntegrationTest --tests com.hanjjak.gameapi.ChapterCombatE2ETest --tests com.hanjjak.gameapi.CraftingMarketE2ETest` 통과. `corepack pnpm --filter @hanjjak/contracts typecheck && corepack pnpm --filter @hanjjak/contracts build` 통과.
- 영구 장비 이벤트: `modules/equipment/src/test/kotlin/com/hanjjak/equipment/application/EquipmentServiceTest.kt`; 해금·강화·승급 타입, 부위 식별, 비용 payload, 동일 부위 aggregate ID와 멱등 재시도 단일 발행을 검증한다. 승급 소비량·쌀 소각 집계는 `modules/events/src/test/kotlin/com/hanjjak/events/application/EconomyMetricAccumulatorTest.kt`가 검증한다.
- 스모크: 실제 `game-api`와 PostgreSQL에서 거래소 구매 2건 실행 후 `outbox_event`에 `MARKET_LISTING_CREATED` 4건, `MARKET_TRADE_COMPLETED` 2건 저장 확인. 원본은 Git에서 제외된 `build/j02-market-outbox-smoke-20260907.json`에 보존했다.
- Kafka: `apps/event-consumers/src/main/kotlin/com/hanjjak/eventconsumers/OutboxKafkaPublisher.kt`, `apps/event-consumers/src/test/kotlin/com/hanjjak/eventconsumers/MarketPriceSnapshotConsumerKafkaTest.kt`; 검증 `cmd.exe /c gradlew.bat :apps:event-consumers:test :modules:market:test --tests com.hanjjak.market.application.MarketServiceTest` 통과.
- 실제 Kafka 스모크: Docker PostgreSQL(`55434`)·Apache Kafka(`19092`)·`game-api`(`18080`)·`apps:event-consumers`를 함께 실행했다. 구매 4건 후 `outbox_event`는 `MARKET_LISTING_CREATED` 1건, `MARKET_TRADE_COMPLETED` 4건, `MATERIAL_PREFERENCE_SELECTED` 3건 모두 `SENT`였고, consumer lag는 0이었다. `market_price_snapshot`은 `POTATO_M1` 거래 4건·수량 12·금액 600·수수료 60·정산 540으로 갱신됐다. 이 스모크에서 `JdbcMarketPriceSnapshotWriter` bean 조건 오류를 발견해 prod writer를 항상 등록하고 테스트 writer는 `@Primary`로 대체하도록 수정했다.
- outbox 저장 성능 개선: `DomainEventPublisher.publishAll`을 추가하고 `JdbcOutboxEventPublisher`가 여러 이벤트를 한 insert로 저장하게 했다. 거래소 구매의 `MARKET_TRADE_COMPLETED`는 기존과 같은 이벤트 건수·payload를 유지하면서 요청별 batch insert로 저장한다. 검증은 `./gradlew.bat :modules:events:test :modules:market:test --tests com.hanjjak.market.application.MarketServiceTest`, `./gradlew.bat :apps:game-api:test --tests com.hanjjak.gameapi.CraftingMarketE2ETest`, J-07 outbox batch 부하 비교로 통과했다.
- 2026-09-10 시세 consumer DB 병목 개선: Kafka listener를 batch mode로 전환하고 한 poll의 이벤트 ID 멱등 기록을 다중행 insert, 품목별 가격 스냅샷을 한 upsert로 집계한다. 이벤트별 수량·금액·수수료·정산 합계와 최신 체결 시각 기준 최근가는 유지된다. `:apps:event-consumers:test`가 batch listener 전달을 통과했고, 실제 Kafka 30분 soak 종료 시 Outbox 80,019건 전부 `SENT`, consumer lag 0, 음수 자산 0건을 확인했다. J-07의 응답시간 기준은 일부 회차에서 미달해 전체 검증 상태는 변경하지 않는다.
- 2026-09-10 merge 전 리뷰 보강: malformed Kafka record는 앞선 정상 이벤트를 먼저 멱등 반영한 뒤 정확한 batch index의 `BatchListenerFailedException`으로 거부해 같은 poll의 정상 이벤트가 함께 폐기되지 않게 했다. 동일 timestamp 체결의 최근가는 `market_price_snapshot.last_event_id`와 `(occurredAt,eventId)` 순서로 poll 경계와 무관하게 결정한다. Flyway V39와 Kafka consumer 회귀 테스트를 추가했다.
- 2026-09-10 처치 정산 이벤트: `BATTLE_ENEMY_SETTLED` 계약과 Outbox 생산을 추가하고 처치별 실제 지급 `ITEM_DROPPED`를 같은 트랜잭션 경계에 연결했다. TypeSpec 생성, 이벤트 모듈 테스트와 game-api 컴파일이 통과했다. 실제 Kafka 전달 검증은 이번 범위에서 실행하지 않았다.
+ 2026-09-11 경제 통계 consumer: `MARKET_TRADE_COMPLETED` 등 경제 이벤트를 별도 `economy-metrics` consumer group으로 받아 `economy_metric_daily`에 일자·품목별로 누적한다. `processed_kafka_event`의 consumer별 event ID를 먼저 기록해 재전달·배치 중복을 무시한다. `apps:event-consumers:test --tests com.hanjjak.eventconsumers.EconomyMetricsKafkaConsumerTest`가 정상 이벤트 중복 제거와 malformed 이벤트 거부를 검증했다.
- 2026-09-12 Kafka 계측 wiring을 추가했다. `event-consumers`의 economy·market consumer가 batch/event/duplicate/failure/latency를 `KafkaOpsMetrics`에 기록하고, 관리자 Kafka 조회가 `kafka_ops_metric`·`kafka_ops_consumer_metric`을 읽도록 연결했다. 실제 broker rollout과 운영 거래 smoke는 배포 후 확인한다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 부분 완료 |
| 개발 | 부분 구현 |
| 검증 | 부분 검증 |
