---
doc_kind: task
owner_domain: delivery
task_code: 'I-02'
task_area: 'I 거래소·경제'
task_type: '개발'
priority: 'P0'
target_chapters: '공통'
planning_status: '완료'
development_status: '부분 구현'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/e1606c8781b083ec85a28139cde3fd07'
notion_id: 'e1606c87-81b0-83ec-85a2-8139cde3fd07'
snapshot_date: '2026-08-28'
---

# I-02 매도 주문 등록·수정·취소·만료 구현

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [거래소](../../../../30-domain/economy/ssot.md)를 따른다.

## 완료 기준

거래 가능한 재료·스킬북·보석의 매도 지정가를 단일 주문 모델에 등록·수정한다. 가격 변경·수량 증가는 새 우선순위와 8시간 만료, 수량 감소는 기존 우선순위·만료를 적용하고 취소·만료는 남은 물품을 통합 수령함으로 옮긴다. 시장 품목 비활성화는 새 거래를 막고 bounded batch 취소·반환을 완료할 때까지 `취소 처리 중`으로 표시한다.

## 선행 작업

I-01, F-04

## 비고

2026-09-11 단일 주문장 구현은 거래 가능한 재료·스킬북·보석 시장 품목, 매도 GTC/IOC 생성, 가격·수량 수정, 8시간 만료, 취소·만료 반환 수령함을 새 계약으로 제공한다. 중첩 품목과 보석 instance 예약·체결·반환 경계를 구현했으며 보석 주문 수량 증가는 새 `instanceIds`를 받는 수정 계약이 없어 명시적으로 거절한다. 시장 품목 비활성화의 bounded batch 취소 worker와 실제 PostgreSQL 보석 E2E가 남아 상태는 부분 구현·부분 검증으로 유지한다.

## 증거 링크
- 2026-09-10 복수 품목 조회와 5세대 카탈로그: `GET /api/v1/market/listings?itemId=...`가 선택된 복수 품목의 활성 매물을 낮은 단가·빠른 등록순으로 반환한다. 재료 카탈로그는 감자·고구마·옥수수 각 M1~M5와 사용자 확정 표시 이름을 제공한다. M5 자동 파밍 공급은 아이템 SSOT의 미해결 범위로 유지한다.
- 2026-09-09 거래소 호가창 UI·카탈로그 확장: 백엔드/API 변경으로 `modules/market/src/main/kotlin/com/hanjjak/market/application/MarketService.kt`가 거래 가능한 `MATERIAL`, `SKILL_BOOK`, `GEM` 카탈로그 품목을 거래소 조회 대상으로 반환한다. `modules/market/src/main/kotlin/com/hanjjak/market/domain/Market.kt`의 최저 단가는 10쌀이며, 판매 등록 수량은 클라이언트에서 최대 999개로 제한한다. 프론트는 `apps/web/src/features/market/MarketScreen.tsx`와 `apps/web/src/styles.css`에서 파란 슬롯 화면을 제거하고 감자·고구마·옥수수 톤의 호가창형 구매 화면으로 바꿨다. `LISTING_SOLD_OUT`은 사용자 문구 `구매 가능한 매물이 없습니다.`로 표시한다.
- GitLab 반영: MR 없이 `main`에 직접 push했다. 원격 기준 커밋은 `10ee6cf`(거래소 UI 정리), `7d1e401`(탭형 품목 슬롯), `771217c`(구매·판매·거래 내역 탭 전환 수정)이며 `origin/main` 최신 HEAD는 `771217c`다. GitLab Merge requests 목록에는 별도 MR 번호가 생기지 않는다.
- 검증: `corepack pnpm --filter @hanjjak/web test -- MarketScreen marketErrors marketForm marketPagination`, `corepack pnpm --filter @hanjjak/web typecheck`, `corepack pnpm --filter @hanjjak/web build`, `cmd.exe /c gradlew.bat :modules:market:test --tests com.hanjjak.market.application.MarketServiceTest --no-daemon --no-configuration-cache --max-workers=1 --no-build-cache`, `cmd.exe /c gradlew.bat :apps:game-api:test --tests com.hanjjak.gameapi.CraftingMarketE2ETest --no-daemon --no-configuration-cache --max-workers=1 --no-build-cache` 통과.

- 2026-09-08 실연결 추가: 격리 DB의 신규 계정으로 실제 인증 HTTP 등록→부분 구매→만료→만석 회수 거절→공간 확보 후 잔량 회수 통과. 만료·만석은 검증 계정/매물에 한해 SQL 구성. 실제 브라우저에서는 별도 ACTIVE 매물 989개 등록·전량 회수와 인벤토리 소멸/복원을 확인했다. 만료·만석 오류 화면 자체의 브라우저 인수는 남아 있다.

- 2026-09-08: `MarketService.cancelListing` 및 `expireActiveForItem`, `MarketInventoryService`와 회수 UI 변경. 관련 인벤토리·거래소·game-api 테스트 통과. 실제 인증 API 회수 흐름 브라우저 인수는 남아 있다.
- 2026-09-09 기준 정합성 보강: 거래소 최저 단가는 `MIN_MARKET_UNIT_PRICE=10`이며 서버 도메인·웹 폼·TypeSpec·생성 OpenAPI의 등록/가격 수정 범위를 10~999,990쌀·10쌀 단위로 일치시켰다. 기존 문서에 남아 있던 1,000쌀 기준 문장을 현재 SSOT 기준으로 정정했다.
- 2026-09-10 판매 가능 수량 확인: 판매 등록은 `InventoryItem.availableQuantity`를 기준으로 1개 이상을 직접 입력하며, 등록 시 서버가 잠금 후 최종 가용 수량을 재검증한다. 거래소 전용 999개 상한은 제거했다.
- 2026-09-10 스킬북 한정 백엔드 검증: 거래 가능 stack 자산 검증을 등록·구매·회수 경로에 공통 적용했다. 격리 PostgreSQL에서 `skillbook:active_heavy:normal` 3개 등록, 2개 구매, 잔여 1개 회수와 판매자 180쌀 정산을 확인했다. 보석 등록·구매와 UI 검증은 남아 있으므로 I-02 상태는 유지한다.
- 2026-09-10 거래소 전면 개편: 강화재료 F·D·C·B·A 표시명과 `apps/web/src/features/equipment/assets/material-ranks` 이미지를 카탈로그에 연결했다. 목록·내 매물·거래 내역에 필터 기준 총 건수와 복합 cursor를 적용하고 남은 수량 0인 내 매물을 서버 조회에서 제외했다. 거래 내역은 페이지당 10건, UI 최대 99페이지로 제한한다.

- 현재 백엔드: `modules/market/src/main/kotlin/com/hanjjak/market/application/OrderBookService.kt`, `modules/market/src/main/kotlin/com/hanjjak/market/api/MarketController.kt`, `modules/inventory/src/main/kotlin/com/hanjjak/inventory/application/MarketInventoryService.kt`
- 현재 UI: `apps/web/src/features/market/MarketScreen.tsx`, `apps/web/src/features/market/api.ts`
- 현재 검증 경로: `:modules:market:test`, `:apps:game-api:compileTestKotlin`, `@hanjjak/web` 관련 테스트·typecheck·build. 구형 listing 검증 기록은 위 날짜별 이력으로 보존한다.
- 2026-09-11 새 경로: `POST /api/v1/market/orders`, `PATCH /api/v1/market/orders/{orderId}`, `POST /api/v1/market/orders/{orderId}/cancel`, `GET/POST /api/v1/market/deliveries/*`. 구형 listing API·서비스·repository와 웹 adapter를 제거했다. market 단위 테스트, Kotlin test source 컴파일, 웹 46파일·274테스트/typecheck/build 통과.
- 2026-09-11 `origin/main` 병합 검증: 구형 listing 구현을 되살리지 않고 `OrderBookService`·`JdbcOrderBookRepository`와 관리자 주문장 운영 계약으로 통합했다. Kotlin test source 컴파일, TypeSpec 생성, 프론트 typecheck/build와 관련 테스트를 통과했다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 부분 구현 |
| 검증 | 부분 검증 |
