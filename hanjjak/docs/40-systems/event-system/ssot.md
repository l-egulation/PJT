# 데이터·이벤트 SSOT

---
doc_kind: ssot
owner_domain: data-events
authority_level: proposed
---

## 책임

게임·경제 이벤트의 공통 envelope, 이벤트 ID와 버전, Outbox 기록·재시도·소비 경계를 소유한다. 도메인 상태의 최종 판정은 각 도메인 서비스가 소유한다.

## 공통 envelope와 이벤트 계약

공통 이벤트 계약은 `packages/contracts/events.tsp`가 소유한다. 모든 이벤트는 `eventId`, `eventType`, `schemaVersion`, `occurredAt`, `producer`를 필수로 갖고, `accountId`, `characterId`, `chapter`, `stageId`, `itemId`, `quantity`, `unitPrice`, `totalPrice`, `commandId`, `correlationId`는 이벤트 성격에 따라 선택적으로 둔다. 이벤트별 상세 payload 필드는 TypeSpec 계약을 따른다.

J-01 확정 범위의 이벤트 타입은 몬스터 처치 정산, 전투 사이클 완료, 아이템 드롭, 영구 장비 해금 완료, 영구 장비 강화 시도, 영구 장비 승급 완료, 장비 분해, 매물 등록, 매물 취소, 구매 주문 등록, 구매 주문 취소·만료, 거래 완료, 주력 재료 선택이다. 영구 장비 이벤트는 인스턴스 UUID 대신 계정과 `equipmentSlot`으로 수명주기를 식별하며, 계정·부위로부터 계산한 안정 aggregate ID를 사용한다. 반복 모드 적용, 스테이지 입장·클리어·실패·복귀, 스킬 성장 이벤트는 후속 구현 범위에서 실제 생산 필요가 확인될 때 계약을 추가한다.

## Outbox와 Kafka

권한 상태 변경과 Outbox 기록은 같은 트랜잭션에 묶는다. `modules:events`의 `DomainEventPublisher`와 `outbox_event` 저장 경계가 1차 구현이며, 몬스터 처치 정산·전투 사이클 완료·드롭·영구 장비 해금·강화·승급·매물 등록·매물 취소·구매 주문 등록·구매 주문 취소/만료·거래 완료·주력 재료 선택은 현재 구현 경로에서 Outbox에 기록한다. `apps:event-consumers`는 `outbox_event`의 `PENDING` 이벤트를 `domain-events.v1` Kafka 토픽으로 발행하고, 가격 스냅샷 소비자는 `MARKET_TRADE_COMPLETED`를 소비해 `market_price_snapshot`에 최근 거래가·평균 거래가·체결량을 멱등 갱신한다. 시장의 잠금·체결 정합성은 API와 DB 트랜잭션이 소유하며, Kafka 집계는 읽기용 파생 상태다.

## 경제 지표 집계

경제 지표 1차 구현은 `modules:events`의 `EconomyMetricsService`가 `outbox_event`를 날짜 범위로 읽어 `economy_metric_daily`에 재계산 저장한다. 조회 API 계약은 `packages/contracts/metrics.tsp`가 소유한다. 집계 단위는 `metric_date`, `item_family`, `generation`, `item_id`이며, 거래소 매물 등록·취소, 거래 수량·금액·수수료·정산액, 아이템 드롭 공급량, 영구 장비 해금·강화·승급 소비량, 몬스터 처치 정산의 전투 쌀 생성량, 영구 장비 해금·강화·승급 쌀 소각량을 기록한다. 전투 사이클 완료 이벤트의 누적 쌀은 중복 집계하지 않는다.

관련 작업: [J 작업 색인](../../wiki/06-delivery/tasks/J-delivery-operations/_index.md), [계약 구조](../../50-architecture/monorepo.md)

