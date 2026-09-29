---
doc_kind: task
owner_domain: delivery
task_code: 'I-03'
task_area: 'I 거래소·경제'
task_type: '개발'
priority: 'P0'
target_chapters: '공통'
planning_status: '완료'
development_status: '부분 구현'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/a7306c8781b083f28981011aa5e28f80'
notion_id: 'a7306c87-81b0-83f2-8981-011aa5e28f80'
snapshot_date: '2026-08-28'
---

# I-03 매수·매도 IOC와 부분 체결 구현

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [거래소](../../../../30-domain/economy/ssot.md)를 따른다.

## 완료 기준

매수 IOC는 수량·개당 최대 단가를 받아 같은 계정의 판매 중 매물을 제외한 다른 계정의 최우선 주문부터 maker 가격으로 엄격한 가격·시간 순서에 따라 체결한다. 매도 IOC는 수량·개당 최소 단가를 받아 본인 매수 주문과 교차하면 거절하고 상대 주문만 체결한다. 한 개 이상 가능한 수량은 부분 체결하고 미체결 잔량은 주문장에 남기지 않으며, 각 체결·쌀·물품·수수료·정산·거래 기록을 원자 처리한다.

## 선행 작업

I-01, I-02

## 비고

2026-09-11 단일 주문 엔진의 매수·매도 IOC와 GTC 교차 체결을 구현했다. 재구성 UI의 실제 주문 검증 중 IOC를 ACTIVE로 먼저 INSERT하여 DB 제약을 위반하는 오류를 발견해, 첫 체결이 결정된 뒤 종료 주문으로 저장하고 체결 row를 수령·정산 참조보다 먼저 생성하도록 수정했다. 기존 migration의 제약은 변경하지 않았다. 실제 PostgreSQL E2E와 로컬 HTTP 흐름은 아래 증거처럼 통과했으며, 자산 부족으로 요청 일부만 가능한 경계와 전체 동시성 인수 범위는 아직 검증하지 않아 부분 구현·부분 검증을 유지한다.

## 증거 링크
- 2026-09-09 가격 상한 구매 구현: `packages/contracts/main.tsp`, 생성 OpenAPI, `MarketController`, `MarketService`, `MarketRepository`, `JdbcMarketRepository`에 단일 재료 구매 견적과 `maxUnitPrice` 계약을 반영했다. 본인 매물 포함 시장 최저가와 본인 매물 제외 구매 가능 최저가를 구분하고 상한보다 비싼 매물에서 체결을 중단한다.
- 2026-09-09 검증: `:modules:market:test` 31개 통과. 견적의 자기 매물 제외, 상한 이하 부분 체결, 상한 이하 매물 0건의 무변경 실패를 확인했다. `CraftingMarketE2ETest`에서 실제 PostgreSQL·HTTP 구매 견적과 상한 구매·정산 흐름이 통과했다. `@hanjjak/contracts` build도 통과했다.
- 2026-09-09 거래소 UI 정리: `apps/web/src/features/market/MarketScreen.tsx`에서 매물·즉시구매, 판매등록/내 매물, 받을 정산을 데스크톱 3컬럼으로 배치하고, 받을 정산과 거래 내역은 5개 단위 페이지를 적용했다. `apps/web/src/features/market/marketErrors.ts`에서 `INSUFFICIENT_RICE`/`insufficient_rice` 응답은 사용자 문구 `쌀이 부족합니다.`로 표시한다.
- 검증: `corepack pnpm --filter @hanjjak/web test -- marketForm marketUiState marketPagination marketErrors`, `corepack pnpm --filter @hanjjak/web typecheck`, `corepack pnpm --filter @hanjjak/web build`, `cmd.exe /c gradlew.bat :apps:game-api:test --tests com.hanjjak.gameapi.CraftingMarketE2ETest --no-daemon --no-configuration-cache --max-workers=1 --no-build-cache` 통과. 실제 브라우저 시각 검증은 `xd://browser` 공유 브라우저 데몬 시작 실패로 수행하지 못했다.

- 2026-09-08 실제 인증 HTTP: 검증 계정의 재료 999개 등록 후 다른 검증 계정이 10개 구매, 판매자 인벤토리 재차 차감 없음과 구매자 중복 지급 없음 확인. 남은 989개 회수 성공. 격리 DB 대상이며 정확 단가 동의·구매 그룹 상세 등 잔여 구현 상태는 변경하지 않는다.

- 2026-09-08: 구매 지급 시 판매자 인벤토리를 재차 차감하지 않도록 단일·일괄 경로 수정. 매물 잔량 차감과 구매자 지급의 기존 트랜잭션은 유지한다. 관련 모듈 테스트와 game-api 회귀 테스트 통과.
- 2026-09-09 구매 입력 개선: `MarketScreen.tsx`의 구매 수량을 +/- 단계 조작에서 숫자 직접 입력으로 변경하고 1~999 범위·숫자 입력·초과값 상한 처리를 적용했다. 10쌀 매물 1개 등록 및 구매 smoke에서 성공 응답, 구매자 지갑 100,000→99,990, 구매자 아이템 +1, 매물 20→19, 거래 1건·정산 우편 1건을 확인했다. 원본은 격리 smoke DB 실행 결과 `build/reports/market-fix-smoke.json`에 보존했다.
- 2026-09-09 구매 수량 UI 재확인: `apps/web/src/features/market/MarketScreen.tsx`는 +/- 단계 버튼 대신 `구매 수량` 숫자 입력 필드(`type="text"`, `inputMode="numeric"`)를 사용하고, `MAX_MARKET_QUANTITY=999`와 `numericInputValue`로 1~999 범위를 적용한다. 이 변경은 현재 `main` 소스에 포함되어 있으며, 운영 화면은 최신 web 이미지 배포 후 반영된다.
- 2026-09-11 1쌀 단위 즉시 구매: 13쌀·14쌀 매물도 견적·MAX·예상 지출·구매가 동작하도록 웹 구매 상한과 서버 `maxUnitPrice` 검증을 10~999,990쌀 범위의 1쌀 단위 정수로 변경했다. `MarketScreen.test.ts`는 999개×13쌀=12,987쌀과 두 버튼 활성화를, `MarketServiceTest`는 13쌀 매물 5개를 65쌀에 체결함을 검증한다.

- 백엔드: `modules/market/src/main/kotlin/com/hanjjak/market/application/MarketService.kt`, `modules/mail/src/main/kotlin/com/hanjjak/mail/application/MailService.kt`
- UI: `apps/web/src/features/market/MarketScreen.tsx`
- 검증: `./gradlew.bat test`, 브라우저 스모크 구매·거래내역·정산 우편 확인
- 2026-09-11 회귀: `OrderBookServiceTest`가 BUY IOC의 maker 가격·부분 체결, SELL IOC의 기존 BUY escrow 단일 차감, 본인 교차 거절을 검증한다. market 테스트와 web 46파일·274테스트/typecheck/build 통과.
- 2026-09-11 실제 DB 오류 수정: `modules/market/src/main/kotlin/com/hanjjak/market/application/OrderBookService.kt`의 IOC 생성 및 체결·delivery·mail 저장 순서를 수정했다. `apps/game-api/src/test/kotlin/com/hanjjak/gameapi/CraftingMarketE2ETest.kt`는 실제 재료 선택 선행 조건을 수행하고 수수료 차감 후 정산을 검증하도록 수정했다. JDK 21에서 `:apps:game-api:test --tests com.hanjjak.gameapi.CraftingMarketE2ETest :modules:market:test --no-daemon --no-configuration-cache --max-workers=1 --no-build-cache` 통과(PostgreSQL E2E 2개, market 단위 5개). 결과: `apps/game-api/build/test-results/test/TEST-com.hanjjak.gameapi.CraftingMarketE2ETest.xml`, `modules/market/build/test-results/test/TEST-com.hanjjak.market.application.OrderBookServiceTest.xml`.
- 2026-09-11 UI에서 BUY/SELL IOC 성공, SELL IOC 부분 체결 종료와 유동성 소진 후 0체결 거절을 확인했다. 정확한 실행 결과와 수령·정산 화면 증거는 [K-14](../K-client-ux/k-14-listing-management-settlement-ui.md)에 기록했다.
- 2026-09-11 추가 검증: 실제 PostgreSQL E2E `CraftingMarketE2ETest`에 가격대 페이지의 exclusive cursor·global cumulative·내 물량 제외 회귀를 추가했고 테스트가 통과했다. 로컬 Chromium에서 복수 가격대 BUY IOC 600개가 110·125쌀 maker 가격으로 520+80개 체결되어 67,200쌀로 정산되고, 가격대 유동성 변화 뒤 50개 요청 중 20개 체결·30개 종료되는 실제 결과를 확인했다. 응답 유실 후 동일 멱등 키 재시도는 단일 주문 결과를 반환했다.
- 2026-09-11 정책 변경 반영: 매수 견적·실행 후보에서 본인 판매 중 매물을 제외하되 다른 계정 매물은 계속 가격·시간 우선으로 체결하도록 `OrderBookService`와 회귀 테스트를 갱신했다. 본인 매물만 있을 때 0체결 견적을 반환하고, 본인·타인 매물이 섞이면 타인 매물만 체결한다.
- 2026-09-11 구매 수량 조작 변경: 강화 재료 구매 빠른 버튼을 `10개·100개·500개`로 바꾸고 클릭마다 현재 수량에 누적한다. 구매 수량 입력 오른쪽 `×` 버튼은 입력을 빈 값으로 초기화한다. 가격·주문 수량 직접 입력과 서버 정수 검증은 유지한다.
- 2026-09-12 호가 주문서 판매 수량 shortcut을 구매와 동일한 `10개`·`100개`·`500개`로 통일하고, 판매에서도 현재 입력값을 덮어쓰지 않고 클릭한 수량을 누적한다. `TradeTicket.test.tsx`가 `10 → 10 → 100 → 500` 클릭 후 주문 수량 `620`과 `1개` 버튼 제거를 검증한다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 부분 구현 |
| 검증 | 부분 검증 |
