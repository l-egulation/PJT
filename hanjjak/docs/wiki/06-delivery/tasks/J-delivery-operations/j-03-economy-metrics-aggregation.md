---
doc_kind: task
owner_domain: delivery
task_code: 'J-03'
task_area: 'J 데이터·테스트·배포'
task_type: '데이터'
priority: 'P1'
target_chapters: '공통'
planning_status: '완료'
development_status: '구현'
verification_status: '검증'
deadline: ''
source_url: 'https://app.notion.com/p/9fc06c8781b08255bb1e818e49f85ef9'
notion_id: '9fc06c87-81b0-8255-bb1e-818e49f85ef9'
snapshot_date: '2026-08-28'
---

# J-03 경제 지표 집계 작업 구현

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [데이터·이벤트 및 배포·운영](../../../../40-systems/event-system/ssot.md)를 따른다.

## 완료 기준

계열별 공급량, 거래량, 가격, 쌀 생성·소각량을 기간별로 조회할 수 있다.

## 선행 작업

J-02, I-06

## 비고

2026-09-10 구현 기준으로 `outbox_event`의 몬스터 처치 정산·드롭·장비 제작·장비 강화·매물 등록·매물 취소·거래 완료 이벤트를 날짜 범위로 재계산해 `economy_metric_daily`에 저장한다. 전투 쌀 생성량은 `BATTLE_ENEMY_SETTLED`에서만 합산하고 사이클 완료 이벤트의 누적값은 중복 집계하지 않는다. 조회 단위는 `metric_date`, `item_family`, `generation`, `item_id`이며, API는 `POST /api/v1/metrics/economy/daily/refresh`와 `GET /api/v1/metrics/economy/daily`이다. Kafka 경로에서는 별도 `economy-metrics` consumer group이 같은 경제 이벤트를 수신해 실시간 누적한다.

## 증거 링크

- 계약: `packages/contracts/metrics.tsp`, `packages/contracts/events.tsp`
- 구현: `modules/events/src/main/kotlin/com/hanjjak/events/application/EconomyMetricsService.kt`, `modules/events/src/main/kotlin/com/hanjjak/events/application/EconomyMetricAccumulator.kt`, `modules/events/src/main/kotlin/com/hanjjak/events/api/EconomyMetricsController.kt`, `modules/account/src/main/kotlin/com/hanjjak/account/api/ApiRequestGuardFilter.kt`, `apps/game-api/src/main/resources/db/migration/V15__economy_metrics.sql`, `apps/game-api/src/main/resources/db/migration/V16__economy_metric_family.sql`
- 검증: `./gradlew.bat :modules:events:test :modules:equipment:test :apps:game-api:compileKotlin && corepack pnpm --filter @hanjjak/contracts typecheck && corepack pnpm --filter @hanjjak/contracts build` 성공
- 스모크: 관리자 조회 경로 `GET /api/admin/v1/metrics/economy/daily?from=...&to=...`를 사용한다. 공개 호환 경로 `/api/v1/metrics/economy/**`는 비로그인 요청에 `401 AUTHENTICATION_REQUIRED`, 로그인한 플레이어 요청에 `403 ECONOMY_METRICS_ADMIN_ONLY`를 반환하며 컨트롤러에 도달하지 않는다.
- 2026-09-10 회귀 검증: `EconomyMetricAccumulatorTest`가 처치 정산 쌀을 합산하고 같은 사이클 완료 payload는 무시하는 것을 확인했다.
- Kafka 검증: `:apps:event-consumers:compileKotlin`과 `:apps:event-consumers:test --tests com.hanjjak.eventconsumers.EconomyMetricsKafkaConsumerTest` 성공. 중복 event ID 제거와 malformed 이벤트 거부를 확인했다. 실제 PostgreSQL·Kafka end-to-end 집계는 PR 이후 실행한다.
- main 기준 재검증 브랜치 `feat/kafka-e2e-verification`에서 `:apps:event-consumers:test` 전체 테스트 성공. Kafka 거래 이벤트 소비자와 경제 통계 소비자 모두 정상 동작했고, 관리자 조회 경로는 `GET /api/admin/v1/metrics/economy/daily?from=...&to=...`로 확인했다. 별도 `economy-metrics` consumer group과 `processed_kafka_event(consumer_name)` 기준 중복 방지 구조를 확인했다.
- 2026-09-12 운영 첫 Kafka 활성화에서 `market_price_snapshot` upsert의 `updated_at=now())` 구문 오류가 확인됐다. 닫는 괄호를 바로잡고 실제 PostgreSQL에서 신규 두 거래의 가격 스냅샷 누적과 같은 event ID 재처리 무효화를 검증하는 `JdbcMarketPriceSnapshotWriterTest`를 추가했다. `:apps:event-consumers:test --tests com.hanjjak.eventconsumers.JdbcMarketPriceSnapshotWriterTest`가 통과했다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 구현 |
| 검증 | 검증 |
