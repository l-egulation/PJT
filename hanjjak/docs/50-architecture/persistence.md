---
doc_kind: ssot
owner_domain: architecture
authority_level: applied
---

# 데이터 권한과 저장 경계

이 문서는 데이터가 어디에 저장되는지보다 어느 경계가 최종 결정을 내리는지를 정의한다. 도메인 규칙과 필드 목록은 각 도메인 SSOT가 소유하며, 이 문서는 저장·권한 경계만 다룬다.

## 확정된 구현 기반

- game-api의 서버 데이터베이스는 PostgreSQL을 사용한다.
- 서버 물리 스키마는 snake_case를 사용하고 외부 노출·도메인 식별자는 UUID를 사용한다.
- 서버 스키마 변경은 Flyway 버전 마이그레이션으로 관리한다. PostgreSQL/Flyway 마이그레이션 실패 시 기동을 차단하고 명확한 복구 오류를 표시한다.
- 계정·자산·진행을 변경하는 기본 전략은 낙관적 잠금과 계정 `stateVersion` 검사다. 충돌은 상태를 변경하지 않고 409로 처리한다.
- 같은 시장 품목의 주문 등록·수정·체결·취소는 주문 행 비관적 잠금과 품목 단위 직렬화로 엄격한 가격·시간 우선순위를 보장한다. 잠긴 최우선 주문을 건너뛰지 않으며 주문·자산·수수료·정산·수령 변경은 유스케이스별 단일 DB 트랜잭션으로 처리한다.
- 보상·소비·강화·합성·거래·진행 변경과 Outbox 기록은 유스케이스 단위 단일 PostgreSQL 트랜잭션으로 원자 처리한다.
- DB 직렬화 충돌·deadlock은 트랜잭션 롤백 후 동일 command를 같은 멱등성 키로 최대 1회 재시도한다. 계속 충돌하면 상태를 확정하지 않고 충돌 오류를 반환한다.
- Outbox는 같은 PostgreSQL에 저장하고 polling worker가 전달·재시도한다. Kafka는 다중 소비 필요성이 확인된 뒤 도입한다.
- 오프라인 작업은 `offline_job`에 서버 시각·스테이지·전투 snapshot·분당 보상률·고정 결과를 저장하고, claim은 지갑·진행·인벤토리·작업 상태를 하나의 PostgreSQL transaction으로 확정한다.
- 브라우저 로컬 저장소는 IndexedDB다. 로컬 실행 상태·체크포인트·설정·읽기 캐시를 하나의 transaction으로 기록하고 `schemaVersion`을 저장한다.
- IndexedDB 마이그레이션 실패 시 로컬 데이터는 자동 초기화하고 서버 확정 상태로 새 저장소를 생성한다. 서버 권한 자산·진행을 로컬 데이터로 대체하지 않는다.
 
## DB 논리 모델

물리 테이블명·전체 컬럼·인덱스는 구현 단계에서 확정한다. 다음 책임과 키는 구현자가 변경하지 않는 기준이다.

| 논리 모델 | 핵심 책임·관계 |
| --- | --- |
| `account` | 계정 식별자·이메일·상태·계정 `stateVersion`; 이메일 unique |
| `character` | 캐릭터 UUID PK, `account_id` FK + UNIQUE로 Account 1:1 강제, 전문·기본 식별 |
| `cosmetic_collection_state` | 계정별 치장 상태의 논리 모델; 치장별 `registeredQuantity` 누적 등록 수량, `unregisteredQuantity` 미등록 중복 재고, 현재 성급과 도감·세트 보유 효과를 서버 권한으로 관리 |
| `cosmetic_equipment` | 계정별 외형 착용 상태의 논리 모델; `HEAD`, `TOP`, `BOTTOM`, `GLOVES`, `SHOES`, `CAPE` 정확히 6개 슬롯과 슬롯별 치장 참조 또는 비움. 외형 상태와 보유 효과 계산을 분리 |
| `auth_session` | 인증 세션 수명·폐기·마지막 활동; 계정당 활성 세션 하나 |
| `game_session` | 실행 세션 식별·대체·폐기; 인증 세션과 분리 |
| `password_reset_token` | 계정별 일회용 비밀번호 재설정 토큰의 SHA-256 해시·생성·만료·소비 시각. 원문 토큰은 저장하지 않음 |
| `social_identity` | `provider + provider_subject` 복합 PK로 외부 사용자 식별자를 계정에 연결한다. 계정·공급자 조합은 UNIQUE이며 공급자 이메일은 표시·감사용일 뿐 병합 키가 아니다. 공급자 token은 저장하지 않는다. |
| `character_progression` | 서버 권한 레벨·경험치 |
| `wallet_balance` / `wallet_ledger` | 쌀 현재 잔액의 정본과 보상·소비·구매 차감·정산 청구 원장. 구현 이관 후 `character.rice`를 권한 잔액으로 사용하지 않는다 |
| `stage_progress` | `account_id + stage_id` UNIQUE, 최고 클리어·최초 클리어·해금과 기록 콘텐츠 버전 |
| `inventory_stack` | 중첩 아이템의 계정·아이템별 인벤토리 보유 수량. 거래소 외부 보관 수량은 주문·수령함 경계에 분리하며 사용 가능 수량은 서버가 산출 |
| `inventory_instance` | 보석 등 인스턴스 자산 UUID와 소유·슬롯·잠금·거래 상태. 보석 시장 동일성은 레벨·옵션 종류·실제 옵션 수치를 모두 포함 |
| `market_instrument` | 동질 품목의 불변 UUID, canonical key, 원본 아이템 연결·속성·활성 상태. 식별자 생성 규칙은 [거래소 주문장 아키텍처 결정](../80-decisions/architecture/market-order-book.md)을 따르고 카탈로그 동기화에서 선생성하며 물리 삭제하지 않음 |
| `market_revision` | 시장 품목별 주문장 일관성 버전. 주문 등록·수정·체결·취소·만료 시 증가하며 polling 응답 역전과 cursor 재검증에 사용 |
| `market_order` | 매수·매도를 `side`로 구분하는 단일 주문 row. UUID, 소유자·시장 품목·최초/체결/미체결 수량·가격·escrow 또는 외부 보관 참조·상태·우선순위/수정/만료 시각과 표시명 snapshot을 보존 |
| `market_trade` / `market_fill` | maker·taker 주문, 구매자·판매자·실제 체결 수량·maker 가격·총액·체결별 수수료·정산액·체결 시각과 표시명 snapshot의 append-only 기록 |
| `market_delivery` | 대기 매수 체결 또는 매도 취소·만료 반환으로 외부 보관한 물품, 원본 행·체결·주문 참조·출처·소유자·시장 품목·수량·표시명 snapshot·생성/수령 시각. 한 건의 일부 수령은 허용하지 않음 |
| `market_account_control` | 계정별 거래소 전용 guard row. 활성 주문 수·최근 1분 성공 mutation 제한을 서로 다른 시장 품목 사이에서도 원자 판정하고 다른 게임 기능의 계정 row 잠금과 분리 |
| `market_order_mutation_history` | 상태를 변경한 성공 주문 등록·수정·취소의 append-only 계정·품목·방향·종류·시각·command 참조. 최근 1분 30회 제한과 10분 반복 패턴을 판정하고 30일 보존 |
| `market_unread_cursor` | 계정별 체결·물품 수령함·판매 정산·주문 만료 네 stream의 읽음 bigint sequence. 사건 생성 시 계정·stream별 sequence를 단조 증가시키고 모든 기기에서 공유 |
| `market_migration_discrepancy` | 기존 주문 잔량과 escrow·외부 보관 불일치의 원본 값·격리 상태·운영 검토 근거·결정자·결정 시각·반환 결과를 영구 보존 |
| `mail_message` | IOC·지정가 판매의 체결별 정산 우편과 청구 상태. 체결 transaction에서 생성되고 청구 command에서 판매자 지갑으로 지급 |
| `gem_preset_slot` | `gem_instance` 참조와 계정·프리셋·슬롯 관계; 한 보석의 복수 프리셋 참조 허용 |
| `battle_session` | 활성 사이클의 전체 전투 입력 스냅샷·seed·logical tick·상태 |
| `battle_enemy_settlement` | 전투 세션별 적 순번·처치 가능 시각·보상 콘텐츠 버전·요청 보상·성장량과 처치별 단일 확정 결과. `battle_session_id + enemy_index` UNIQUE로 재시도 중복을 막는다. |
| `battle_command_event` | 세션 내 장비 강화·스킬 레벨업의 성공 command sequence와 적용 tick |
| `battle_history_event` | append-only 입장·클리어·실패·던전·복귀 기록; 계정별 최신 50건 유지 |
| `command_record` | `account_id + idempotency_key` UNIQUE, commandId·fingerprint·상태·결과·만료 |
| `outbox_event` | eventId UUID PK, payload·delivery 상태·시도 횟수·다음 재시도 시각 |
| `inventory_capacity_reservation` | 보석 던전 도전별 스택 지급 권리 또는 빈 슬롯 예약·상태 |
| `content_reference` | 필수 시 결과·세션에 콘텐츠/보상표 version key 문자열 기록; 실제 정의는 immutable 파일 |
| `raid_session` | Daily serverwide seal session, immutable content/reward versions, cutoff and bounded settlement cursor/counts; unique settle time, sole `OPEN` partial unique index, and due-OPEN row-lock discovery. |
| `raid_account_state` | Session/account sequential slot progress, attempt counters, state version and automatic-finalization fence. |
| `raid_settlement_account` | Immutable per-cutoff account workset with UUID-ordered pending/finalized progress and caller-owned finalization time. |
| `raid_reward_slot` | Three owned reward slots per session/account; terminal status history and current attempt reference. |
| `raid_attempt` | Reward/practice attempt snapshots, JSON result/timeline, completion deadline and terminal status; mode-aware ordinal and partial unique running-account index. |
| `raid_confirmed_result` | Append-only confirmed damage, grade, contribution, non-null result JSON, and reward-version snapshot with composite attempt ownership. |
| `raid_contribution` / `raid_final_rank` | Session/account atomic contribution deltas and immutable settled rank with hidden comparison values. |
| `raid_reward_claim` | Whole-bundle personal/rank claim, source uniqueness, ownership-scoped lock/claim transition, and claimable partial unique index. |
| `raid_command_record` | Account/idempotency fingerprint, typed status and nullable result record. |
| `ranking_entry` | 공개 종합전투력·전문별 순위 파생 읽기 모델; `character_id` 1:1, 닉네임·전문·전투력·공식 버전·원본 state version·갱신 시각 보관 |

### 무결성과 보존

- 치장 논리 상태에서 `availableUnregisteredQuantity`는 `unregisteredQuantity - reservedQuantity`로 서버가 산출한다. 등록 명령은 이 사용 가능 재고만 소비하며, 등록·거래 예약·취소의 동시성은 계정 상태 버전과 자산 원자성으로 검증한다.
- 물리 테이블 분리·병합, 치장별 인덱스와 컬럼은 구현 단계에서 확정한다. 위 치장 모델은 책임과 관계를 고정할 뿐 물리 스키마를 확정하지 않는다.
- 인벤토리 `usedSlots`는 스택 잔액·인스턴스·예약 상태에서 계산한다. 성능 캐시는 원본과 검증 가능할 때만 추가한다.
- 계정·캐릭터·자산·거래 등 내부 관계는 PostgreSQL FK로 보장하고 정적 콘텐츠 ID는 서버 content loader가 검증한다.
- 자산·거래·command·Outbox·전투 기록은 물리 삭제하지 않고 상태 전이·보존 정책으로 닫는다. 전투 화면 기록만 계정별 최신 50건 정책으로 정리한다.
레이드 계정 명령은 `account → raid_session → raid_account_state → raid_reward_slot → raid_attempt → raid_contribution → wallet → cosmetic ticket → inventory stack → raid_reward_claim` 순서로 잠근다. 정산은 세션 fence를 짧은 트랜잭션에서 먼저 확정하고, 세션 잠금을 계정 작업까지 유지하지 않은 채 계정 UUID 오름차순 workset을 처리한다. 정적 보상·등급 정의는 `packages/game-content` 불변 버전에 있으며 데이터베이스에는 시도·결과·청구 당시 버전 snapshot만 저장한다.
- 같은 시장 품목의 등록·IOC·수정·취소·만료·비활성화 mutation은 `market_instrument` row를 transaction 입구에서 잠가 직렬화한다. `SKIP LOCKED`로 선행 주문을 건너뛰지 않고 교차 주문은 maker 가격으로 부분 체결한다.
- 주문 등록은 `command_record`, `market_account_control`, 시장 품목과 주문장 후보, 지갑 또는 판매 자산을 일관된 순서로 잠근다. 매수는 같은 계정의 매도 후보를 건너뛰고, 매도는 본인 반대 주문 교차를 거절한다. guard row에서 활성 주문 수와 최근 1분 성공 mutation을 판정하고 성공 결과와 이력 append를 같은 transaction에 확정한다. 매수 GTC는 `미체결 수량 × 지정가` 쌀을 escrow로, 매도 GTC는 미체결 물품을 외부 보관으로 유지한다.
- 가격 변경·수량 증가는 전체 주문의 우선순위 시각과 만료 시각을 갱신하고 필요한 추가 자산을 같은 transaction에서 확보한다. 수량 감소는 기존 우선순위·만료를 유지하며 해제된 쌀은 반환하고 해제된 판매 물품은 통합 수령함으로 옮긴다.
- 매수 GTC 체결과 매도 취소·만료의 물품은 `market_delivery`에 외부 보관한다. 개별·전체 수령은 `(created_at, delivery_id)` 순서로 행을 잠그며, 전체 수령은 오래된 건부터 전량 수용 가능한 건만 지급한다.
- IOC 구매는 체결 transaction에서 구매자 인벤토리 수용량을 검증해 직접 지급한다. 한 개 이상 체결된 IOC만 종료 주문으로 저장하고 0체결 실패는 7일 `command_record`에만 보존한다.
- 시장 품목 비활성화 transaction은 새 거래를 막고 취소 처리 상태를 확정한다. 후속 worker가 시장 품목 잠금 아래 bounded batch로 주문을 취소하고 매수 escrow 반환·매도 delivery 생성을 처리한다.
- 이관 불일치 주문은 `market_migration_discrepancy`와 `복구 검토 중` 상태로 격리한다. 정상 주문은 계속 이관하며 운영 검토는 원장으로 확인된 자산 반환 또는 무반환 종료만 기록한다.
- 체결·delivery·정산·만료 사건 생성 transaction은 대상 계정과 stream의 bigint sequence를 증가시키고 사건에 저장한다. 읽음 command는 조회 응답이 증명한 `readThroughSequence`까지 `market_unread_cursor`를 단조 증가시킨다.
- 판매 수수료는 개별 체결 총액의 10%를 소수점 이하 내림하고 체결별 정산 우편을 만든다. 판매자 쌀 잔액은 정산 청구 transaction에서만 증가한다.
- deadlock·serialization 실패만 rollback 후 같은 command와 멱등성 키로 제한 재시도한다. 자기 주문 교차, 가격 조건 불충족, 자산·활성 주문·속도 제한 같은 도메인 실패는 재시도로 숨기지 않는다. revision은 주문장 변경 transaction의 commit 경계에서 증가하며 시장 정합성을 이벤트 소비자에 위임하지 않는다.

## 핵심 원칙

- 화면은 상태를 표시하고 명령을 보낸다.
- 웹 클라이언트는 하나의 게임 상태 엔진을 사용하고, 브라우저 로컬 저장소에는 로컬 성장·방치모드·실행 체크포인트와 화면 캐시만 저장한다.
- 서버는 계정, 스테이지 진행·반복 대상·최근 전투 기록, 거래 가능한 자산, 스킬 성장·장착 상태, 치장 등록·중복 재고·도감·세트 보유 효과·외형 착용 상태, 거래소, 정산과 멱등성, 공개 랭킹 파생 상태를 최종 판정한다.
- 정적 챕터·스테이지·아이템·장비·스킬 데이터는 버전이 있는 콘텐츠로 배포한다.
- 이벤트는 변경을 전달하고 기록하지만, 이벤트 소비자가 원본 권한을 대신하지 않는다.

## 권한 경계

| 데이터 분류 | 기본 저장 경계 | 최종 권한 | 세부 SSOT |
| --- | --- | --- | --- |
| 브라우저별 화면 상태·창 대체 UI·소리·볼륨·절전·프레임 제한 등 현재 브라우저 환경 설정 | 웹 저장소 | web | [클라이언트·UX SSOT](../30-domain/player/ux/ssot.md) |
| 여러 기기에서 유지해야 하는 계정 단위 사용자 설정 | game-api 사용자 설정 저장소 | 서버 | [클라이언트·UX SSOT](../30-domain/player/ux/ssot.md), [계정·저장 SSOT](../30-domain/player/ssot.md) |
| 현재 진행·체크포인트·오프라인 복귀 입력 | 브라우저 로컬 저장소 | MVP는 로컬 진행 엔진 | [계정·저장 SSOT](../30-domain/player/ssot.md), [성장 SSOT](../30-domain/progression/ssot.md) |
| 비거래 성장 상태 | 브라우저 로컬 저장소, 동기화 확장 여지 | 현재 MVP 정책에 따름. 단, 스킬 상태는 아래 서버 권한 행을 우선 | [성장 SSOT](../30-domain/progression/ssot.md) |
| 스킬 해금·등급·단계·실패 보정·액티브 장착 순서 | game-api 저장소 | 서버 | [스킬 SSOT](../30-domain/character/skills/ssot.md) |
| 치장 등록·중복 재고·도감·세트 보유 효과 | game-api 저장소 | 서버 | [치장 SSOT](../30-domain/cosmetics/ssot.md), [클라이언트·UX SSOT](../30-domain/player/ux/ssot.md) |
| 치장 외형 착용 6슬롯 | game-api 저장소 | 서버 | [치장 SSOT](../30-domain/cosmetics/ssot.md), [클라이언트·UX SSOT](../30-domain/player/ux/ssot.md) |
| 치장 미등록 중복 거래·예약 | game-api 저장소 + 거래 reservation 경계 | 서버의 거래·경제 경계; `unregisteredQuantity` 중 `reservedQuantity`만 예약하며 `availableUnregisteredQuantity`만 등록·거래 가능 | [거래소 SSOT](../30-domain/economy/ssot.md), [치장 SSOT](../30-domain/cosmetics/ssot.md) |
| 거래 가능한 쌀·재료·스킬북 | game-api 저장소 | 서버 | [거래소 SSOT](../30-domain/economy/ssot.md), [아이템 SSOT](../30-domain/items/ssot.md) |
| 거래 가능한 아이템·장비 소유권 | game-api 저장소 | 서버 | [장비 SSOT](../30-domain/items/equipment/ssot.md), [거래소 SSOT](../30-domain/economy/ssot.md) |
| 매물·구매·취소·수수료·정산 | game-api 저장소 | 서버의 거래·경제 경계 | [거래소 SSOT](../30-domain/economy/ssot.md) |
| 챕터·스테이지·몬스터·드롭·밸런스 정의 | versioned game-content | 콘텐츠 버전 | [콘텐츠 스키마](../60-content/schema.md), 각 도메인 SSOT |
| 명령·도메인 이벤트·분석 전달 | contracts + Outbox | 생산 유스케이스와 이벤트 계약 | [데이터·이벤트 SSOT](../40-systems/event-system/ssot.md) |
| 화면 캐시·조회 결과 | 각 클라이언트 캐시 | 원본이 아님 | 해당 도메인의 원본 SSOT |

로컬 캐시는 서버 권한 스테이지 진행·반복 대상·최근 전투 기록, 거래 자산과 스킬 진행·장착 상태, 치장 등록·중복 재고·도감 표시 상태·세트 효과·외형 착용 상태의 소유권을 만들거나 확정하지 않는다. game-api는 스테이지 클리어·입장·반복 설정과 챕터 접근에서 현재 진행·콘텐츠 버전·멱등 키를, 스킬 성장과 치장 등록·거래 경계에서 현재 소유권·진행 상태·거래 가능 여부·수량·멱등 키를 다시 검증한다.

## 권장 논리 경계

```text
web
  ├─ browser preferences
  ├─ local growth / idle mode / runtime checkpoint
  └─ read cache / pending command

game-api
  ├─ account and character identity
  ├─ password reset token hashes and session invalidation
  ├─ stage progression and repeat target
  ├─ recent battle history (50 events)
  ├─ cosmetic collection state and appearance equipment slots
  ├─ wallet balance and ledger
  ├─ inventory stack and instance ownership
  ├─ material market listing, revision, fills, settlement mail
  └─ domain event outbox

packages/game-content
  └─ versioned static definitions

packages/contracts
  └─ commands, queries, events, error contracts
```

실제 테이블·컬럼·마이그레이션은 구현 단계에서 확정한다. 논리 경계를 먼저 지키고, 물리 데이터베이스를 서비스별로 쪼개는 결정은 운영 요구와 트래픽 근거가 생긴 뒤 별도 결정으로 승격한다.

## 이벤트와 복구의 경계

- 권한 상태 변경과 Outbox 기록은 같은 유스케이스 경계에서 원자적으로 다룬다.
- 몬스터 처치 정산은 경험치·레벨·쌀·아이템·정산 결과·Outbox를 한 트랜잭션으로 확정한다. 실패·중단은 이미 확정된 처치 결과를 되돌리지 않는다.
- 오프라인 보상 claim은 계정·작업 멱등성 키를 잠그고 쌀·경험치·아이템·`CLAIMED` 전이를 원자 처리한다.
- 이벤트 소비자는 중복·지연·재처리를 전제로 하지만, 거래 정산을 이벤트 소비자에게 위임하지 않는다.
- 백그라운드 탭은 서버 기준 경과시간 또는 서버 확정 결과로 진행하되, 탭 discard·기기 절전·네트워크 단절은 메인 전투를 중단한다. 같은 탭은 `sessionStorage`의 게임 세션 식별자를 heartbeat로 재검증해 이어 쓰고, 저장값을 잃은 새 문서도 서버가 1분 미만 기존 활성 세션을 재사용한다. 오프라인 결과는 서버 UTC 경과시간을 1분 단위·최대 480분으로 고정하며 1분 미만 작업은 취소한다.

이벤트 envelope, Outbox, 멱등성의 필드와 상태 전이는 [데이터·이벤트 SSOT](../40-systems/event-system/ssot.md)가 소유한다. 거래 원자성과 escrow는 [거래소 SSOT](../30-domain/economy/ssot.md)가 소유한다.

## 구현 전 확인 질문

다음 질문은 이 문서가 임의로 답하지 않는다. 답이 정해지면 해당 도메인 SSOT와 [결정 로그](../80-decisions/README.md)를 함께 갱신한다.

- 서버 권한 스킬 상태를 로컬 전투 엔진에 반영하는 시점과 연결 실패 시 재동기화 방식은 무엇인가?
- 서버 진행 명령이 실패하거나 응답을 잃었을 때 로컬 전투 결과를 어떻게 재시도하고 서버 상태와 재조정할 것인가?
- 거래 가능 장비와 재료의 경계를 콘텐츠 데이터에서 어떻게 표현할 것인가?
- 콘텐츠 버전이 바뀐 뒤 진행 중 전투와 저장 데이터를 어떻게 처리할 것인가?
- 설정 화면의 프레임 제한 허용 목록, 절전 모드 렌더링 정책, 알림 종류별 계정 저장 여부를 어떻게 확정할 것인가?

