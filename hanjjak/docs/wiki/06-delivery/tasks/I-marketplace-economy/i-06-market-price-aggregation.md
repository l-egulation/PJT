---
doc_kind: task
owner_domain: delivery
task_code: 'I-06'
task_area: 'I 거래소·경제'
task_type: '데이터'
priority: 'P1'
target_chapters: '공통'
planning_status: '완료'
development_status: '부분 구현'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/02d06c8781b082138bd901e8b3519aa9'
notion_id: '02d06c87-81b0-8213-8bd9-01e8b3519aa9'
snapshot_date: '2026-08-28'
---

# I-06 양방향 호가·최근 체결 집계

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [거래소](../../../../30-domain/economy/ssot.md)를 따른다.

## 완료 기준

활성 단일 주문 원본을 동기 SQL로 집계해 시장 품목별 최고 매수·최저 매도·스프레드, 양쪽 5개 가격대의 총잔량·누적 잔량·내 주문 수량과 최근 체결 10건을 같은 revision으로 제공한다. 낮은 revision 폐기와 간격·cursor 오류의 전체 snapshot 복구를 지원한다.

## 선행 작업

I-03

## 비고

2026-09-11 활성 양방향 주문 원본을 SQL로 집계해 최고 매수·최저 매도·스프레드, 각 5단계 총잔량·누적량·내 주문 수량과 최근 체결 10건을 품목 revision과 함께 제공한다. 웹은 간편 시세와 고급 양방향 depth를 3초 polling한다. 낮은 revision 폐기와 revision 간격·cursor 오류 자동 snapshot 복구는 아직 구현되지 않아 상태는 부분 구현·부분 검증으로 유지한다.

## 증거 링크

- 백엔드: `modules/market/src/main/kotlin/com/hanjjak/market/application/MarketService.kt`
- UI: `apps/web/src/features/market/MarketScreen.tsx`
- Kafka 시세 스냅샷: `apps/event-consumers/src/main/kotlin/com/hanjjak/eventconsumers/MarketPriceSnapshotConsumer.kt`, `apps/game-api/src/main/resources/db/migration/V17__market_price_snapshot.sql`
- 계약: `packages/contracts/main.tsp`, `packages/contracts/generated/openapi/openapi.yaml`
- 검증: `cmd.exe /c gradlew.bat :apps:event-consumers:test :modules:market:test --tests com.hanjjak.market.application.MarketServiceTest`, `cmd.exe /c gradlew.bat :apps:game-api:test --tests com.hanjjak.gameapi.CraftingMarketE2ETest`, `corepack pnpm --filter @hanjjak/contracts typecheck && corepack pnpm --filter @hanjjak/contracts build && corepack pnpm --filter @hanjjak/web test && corepack pnpm --filter @hanjjak/web typecheck`, 브라우저 스모크에서 최근 50·평균 50·거래량 3 표시 확인
- 2026-09-10 Kafka 시세 집계 배치화: `MarketPriceSnapshotConsumer`는 한 poll을 batch로 받아 중복 `eventId`를 한 번만 기록하고 품목별 거래 수·수량·금액·수수료·정산액을 한 upsert로 누적한다. 실제 30.642분·16,000건 거래 soak에서 consumer lag 0과 음수 자산 0건을 확인했다. 기존 누적 데이터가 포함된 최종 snapshot 수치와 마지막 회차 1,000건만의 `market_trade` 수치는 직접 일치 비교 대상이 아니며, 이번 검증은 이벤트 유실·중복과 lag를 기준으로 판정했다. 최근 5건 중앙값·체결률·사용자 기준 구매 가능 최저가는 계속 미구현이므로 개발·검증 상태는 `부분`으로 유지한다.
- 2026-09-10 merge 전 정합성 보강: 같은 timestamp 체결이 poll 경계에 나뉘어도 최근가가 바뀌지 않도록 `last_event_id`를 저장하고 `(last_trade_at,last_event_id)`로 비교한다. 잘못된 이벤트 하나가 같은 batch의 정상 거래를 유실시키지 않도록 정상 prefix를 반영하고 실패 index부터 재전달하게 했다.

- 2026-09-11 구현: `GET /api/v1/market/order-books/{instrumentId}`, `JdbcOrderBookRepository.depth/recentTrades`, 웹 `OrderBookPanel`. web 46파일·274테스트에서 양방향 가격대·내 주문 표시와 선택 가격 반영을 확인했고 TypeScript typecheck·production build가 통과했다.
- 2026-09-11 가격대 집계 보완: `GET /api/v1/market/instruments/{instrumentId}/price-levels`와 `MarketPriceLevels`가 exclusive `afterUnitPrice`, `nextUnitPrice`, 총/내/타인/전역 누적을 제공한다. 실제 PostgreSQL에서 21개 가격대를 10·10·1개로 이어 읽고 누적 231개 및 본인 물량 제외를 확인했으며, 마지막 페이지는 `nextUnitPrice`를 명시적 null로 직렬화한다. 페이지 revision 혼합 방지와 품목 잠금 read를 구현했지만 revision gap 전체 snapshot 복구는 남아 상태를 부분으로 유지한다.
- 2026-09-12 가격 추이 런타임 스모크: 인증된 Chromium에서 실제 로컬 API·PostgreSQL의 `GET /api/v1/market/instruments/{instrumentId}/candles?interval=5m`가 HTTP 200과 `MarketCandles` envelope를 반환하는 것을 확인했다. 검증 품목은 체결이 없어 `items: []`였으므로 OHLCV 값 자체의 비어 있지 않은 집계 검증은 남아 있다.
- 2026-09-12 호가 거래 실제 화면 스모크: 인증된 Chromium 1440×1000에서 가격 추이와 `매물 현황` 양방향 패널을 렌더링했고, 실제 API의 candles/order-book 응답은 각각 HTTP 200이었다. 검증 품목에는 체결·대기 주문이 없어 `items: []`, 매수 0·매도 0이었으므로 비어 있지 않은 OHLCV·depth 집계값 검증은 남아 있다.
- 2026-09-12 로컬 fixture 보강: `tools/fixtures/market-fixture.mjs`가 로컬 PostgreSQL에 4개 재료 품목의 반복 가능한 매도 가격대와 `market_trade` 이력을 적재하도록 추가했다. 실제 API에서 `POTATO_M1` candles 5분 봉 12개, 고가 145쌀·저가 90쌀·거래량 2,611개, 매수 2개·매도 5개 가격대를 확인했다. 기본 compose는 자동 seed하지 않으며 `corepack pnpm market:fixture`로 명시 실행한다.
- 2026-09-12 가격 추이 interval 확장: 서버 `MarketCandleInterval`과 JDBC bucket을 5분·15분·30분으로 확장하고 TypeSpec/API client 계약을 갱신했다. 동일 fixture 체결 원본을 서버가 각 interval로 재집계하며, 클라이언트는 선택 interval을 조회 key에 포함한다.
- 2026-09-12 가격 추이 표시 기간 변경: 서버 `MarketCandlePeriod`와 웹 API를 `12h`·`1d`·`2d`로 clean cutover하고 기본 조회를 12시간으로 변경했다. 고정 시계 단위 테스트에서 각 `from` 경계가 12·24·48시간 전이고 응답 interval이 `5m:12h`·`5m:1d`·`5m:2d`임을 검증했다. 실행 중인 인증 API에서도 세 기간이 HTTP 200과 정확한 시간 범위를 반환했다.
- 2026-09-12 가격 차트 lifecycle 수정: 호가 거래에서 품목 또는 polling 응답이 바뀔 때 `lightweight-charts` 인스턴스를 다시 생성하지 않고 단일 chart·series 인스턴스의 데이터만 갱신한다. chart 제거는 컴포넌트 unmount에만 수행하고 데이터별 crosshair 구독만 교체한다. `PriceTrendChart.test.tsx`가 품목 변경 전후 `.tv-lightweight-charts` DOM이 한 개이며 이전 구독 해제와 unmount 제거가 각각 수행됨을 검증한다.
- 2026-09-12 거래소 시각 표기 통일: 가격 차트 축·crosshair·hover, 최근 체결, 내 주문 등록·만료, 물품 도착·수령, 판매 정산 발생·수령, 체결 내역을 한국 표준시 기준 `YYYY-MM-DD HH:mm` 형식으로 통일했다. `marketDateTime.test.ts`가 UTC 원본 `2026-09-12T09:40:00Z`와 날짜 경계를 넘는 `2026-09-12T17:40:00Z`를 각각 `2026-09-12 18:40`, `2026-09-13 02:40`으로 검증한다.
- 2026-09-12 세부 품목 전환 chart identity 보강: `MarketScreen`에서 `PriceTrendChart`에 부여하던 품목별 React `key`를 제거해 거래 이력이 있는 세부 품목을 연속 선택해도 동일 컴포넌트·동일 TradingView 인스턴스를 유지한다. 통합형 `PriceTrendChart.test.tsx`가 감자 F→D→C등급의 서로 다른 캔들·최근 체결 데이터를 차례로 표시하면서 `.tv-lightweight-charts`와 `createChart` 호출이 각각 한 개임을 검증한다.
- 2026-09-12 매물 현황 공통 y축: 매수·매도 누적 수량을 양쪽 전체의 최대 누적량에서 산출한 단일 `ceiling`과 동일한 y 좌표식으로 렌더링하고, 두 그래프 사이에 `ceiling`·절반·0 눈금을 공유 축으로 표시한다. `OrderBook.test.ts`는 매수 누적 40개·매도 누적 400개가 공통 400개 축에서 각각 y=90.8·8에 놓이고 공통 눈금이 400·200·0임을 검증한다.
- 2026-09-12 가격 방향 색상 통일: 가격 추이 캔들·심지·테두리와 거래량은 시가 대비 상승 `#D84A3A`, 하락 `#3976C5`, 보합 `#806956`을 사용한다. 별도 상승·하락 범례는 추가하지 않았다. 매물 현황도 같은 방향 원칙으로 매수는 상승 빨강, 매도는 하락 파랑을 선·영역·상태색에 적용하며 주문서의 기존 구매·판매 동작색은 변경하지 않는다. `OrderBook.test.ts`가 세 가격 방향의 정확한 색상 매핑을 검증한다.
- 2026-09-12 거래소 화면 용어 변경: 양방향 시장 깊이 영역의 화면 제목과 접근성 이름을 `매물 현황`에서 `호가 차트`로 변경했다. 주문·가격·수량 계산과 공통 y축 구조는 변경하지 않았다.
- 2026-09-12 실제 호가 depth chart 전환: 좌우에 분리된 막대형 영역을 제거하고 하나의 가격 x축·누적 수량 y축에서 매수·매도 누적 곡선과 영역을 함께 표시한다. 최우선 매수·매도 사이 스프레드 구간을 표시하고 각 가격 점 선택은 기존처럼 주문 가격만 갱신한다. 단위·렌더링 검증은 `OrderBook.test.ts`와 실제 fixture Chromium 화면으로 수행한다.
- 2026-09-12 호가 차트 좌표·채움·입력 재검증: `MarketScreen.tsx`, `MarketScreen.css`에서 SVG 150px/선택점 180px 높이 불일치와 중복 이동 명령으로 발생한 비스듬한 채움 바닥을 수정했다. 매수·매도 누적 계단 방향, 단일 가격의 유효 영역, 축·시세 요약·고정 상세 영역을 보완했다. Playwright를 실제 로컬 Chromium에 연결해 API·PostgreSQL 데이터의 6개 호가를 1440×1000·1024×768·390×844에서 확인했다. 모든 선택점이 해당 선 위에 있고 좌표 오차는 최대 0.014px 미만, 차트 가로 넘침은 없었다. 매수 85쌀 클릭·매도 110쌀 Enter 선택 시 수량 12개·구매 방향·지정가 유형이 유지됐다. 별도 응답 fixture로 양방향 5단계·내 주문, 매수/매도 단일 가격, 빈 주문장, 누적 11억 대 5개의 편중 주문장을 검증했고, 390px에서 인접한 99/101쌀의 겹친 클릭 영역도 각각 올바른 가격을 선택했다. Fixture는 브라우저 응답에만 적용한 뒤 해제했으며 이 경계 시나리오를 실제 서버 집계 검증으로 주장하지 않는다. `OrderBook.test.ts`의 마크업·고정 좌표 단정은 누적 비율·가격 순서·유효 좌표·정수 눈금 경계 검증으로 교체했다. 거래소 7파일·19테스트, web typecheck·production build 통과. 테스트의 localhost:3000 연결 거부 로그와 build의 500kB 청크 경고는 남아 있다. 본 작업은 UI 보완이며 위의 revision 복구 미구현과 `부분 구현·부분 검증` 상태는 유지한다.
- 2026-09-12 보기 전환 체크박스 표시·접근성 이름 변경: 사용자 요청에 따라 `MarketScreen.tsx`의 표시를 `호가 기반 거래`로 변경하고 UX SSOT 및 기존 `PriceTrendChart.test.tsx` 탐색 조건을 맞췄다. 차트 계산·주문 동작은 변경하지 않았다. 실제 로컬 Playwright 화면에서 새 이름으로 체크하면 차트가 표시되고 해제하면 시장 가격대 표로 복귀함을 확인했다. 영향받는 가격 추이 테스트 2개 통과; 기존 localhost:3000 연결 거부 로그는 유지된다.
- 2026-09-12 depth chart 고도화: `MarketScreen.tsx`의 `marketDepthChart`·`OrderBookPanel`과 `MarketScreen.css`를 변경했다. 실제 API `order-books/{instrumentId}?levels=20`의 HTTP 200 응답과 감자 F등급 중간가격 90쌀·가격축 30~150쌀을 Playwright에서 확인했다. 서버의 기존 1~20단계 계약은 변경하지 않았다. 1440×1000·1024×768·390×844에서 중간가격 x=50%, 가로 넘침 없음, 선택점 좌표 오차 최대 0.014px 미만을 확인했다. 실제 화면의 차트 상단 빈 공간에서 110쌀 탐색·클릭, 85쌀 Enter 선택 시 수량 12개·구매·지정가 유형을 유지했으며 탐색만으로는 가격이 바뀌지 않았다. 최종 확인 중 차트 클릭은 95쌀을 유지했고 거래는 실행하지 않았다. 정확한 최우선 호가 85/95쌀 클릭과 CDP 터치의 110쌀 입력도 검증했다. 브라우저 응답 fixture로 양쪽 20단계·내 주문·99/101쌀 인접 선택, 중간가격 99.5쌀, 단일 매도·빈 주문장·가격 하한 조정·10억 대 5개 물량 편차를 확인했다. 한도 바깥과 스프레드 중앙 클릭은 가격을 바꾸지 않았다. Fixture 해제 후 실제 API 데이터로 복귀했다. `OrderBook.test.ts`에 비대칭 주문장의 중간가격 중심·조회 경계·음수 가격축 방지 회귀를 추가했고 거래소 7파일·22테스트 및 web typecheck·production build가 통과했다. 마지막 포인터 경계 보정 후 해당 10테스트·typecheck와 실제 클릭·터치를 재검증했다. 기존 localhost:3000 연결 거부 로그·번들 크기 경고 및 revision 복구 미구현은 그대로이며 작업 상태와 A–K 색인의 `부분 구현·부분 검증`을 유지한다.
- 2026-09-12 MR !196 CI 시간대 회귀 보정: [파이프라인 191016](https://lab.ssafy.com/s15-bigdata-dist-sub1/S15P21B107/-/pipelines/191016)의 web 작업은 `OrderBook.test.ts`가 호스트 로컬 시각으로 만든 입력 때문에 실패했다. 입력을 명시적인 UTC instant로 바꾸고 한국 시각 출력 기대값은 유지했다. 런타임 날짜 변환·UI 정책은 변경하지 않았다. `TZ=UTC`에서 web typecheck·전체 60파일 336테스트·production build, `TZ=Asia/Seoul`에서 `OrderBook.test.ts`와 `marketDateTime.test.ts` 12테스트가 통과했다. 기존 localhost:3000 연결 거부 로그와 500kB 청크 경고는 유지되며 I-06 및 A-K 색인의 부분 구현·부분 검증 상태는 변경하지 않는다.
- 2026-09-12 가격 추이 연결선 색상 조정: `apps/web/src/features/market/MarketScreen.tsx`의 `PriceTrendChart`에서 종가 연결선을 기존 황토색 대신 축 글자와 같은 잉크색으로 변경했다. 계산·조회·주문 동작은 변경하지 않았다. Chromium의 실제 로컬 화면을 1440×1000·390×844에서 확인했고, 데스크톱 차트 canvas에서 변경된 선색 픽셀 996개·이전 선색 픽셀 0개·chart DOM 1개를 확인했다. 30분 봉으로 전환해 상승·하락 캔들과 연결선의 구분도 시각 확인했다. 같은 시점의 실제 감자 F등급 5분/12시간 candles API는 HTTP 200, 8개 봉 모두 시가=고가=저가=종가, 총 456개 거래량을 반환했다. 색상 변경은 브라우저 검증으로 확인했으며 테스트·빌드는 이번 조정에서 재실행하지 않았다. I-06 및 A-K 색인은 부분 구현·부분 검증을 유지한다.
- 2026-09-12 가격 추이 정보·탐색 고도화: `apps/web/src/features/market/MarketScreen.tsx`의 `PriceTrendChart`와 `MarketScreen.css`에 [UX 정본](../../../../30-domain/player/ux/ssot.md)의 독립 가격·거래량 패널, 고정 봉 상세, 선택형 종가선·SMA 5, 탐색 제어 및 사용자 요청의 차트 높이 두 배 확대를 반영했다. 실제 로컬 Chromium에서 기본 680px·크게 보기 1080px, 390px 좁은 화면에서 기본 600px·크게 보기 880px 및 차트 가로 넘침 없음을 확인했다. 확대 후 logical range 1.05~5.95에서 최신 이동 시 폭을 유지한 3.10~8로 이동했고 전체 보기로 복귀했다. 브라우저 QueryClient에만 넣은 30봉 fixture에서 동일 조회 갱신 전후 범위 8~18 유지, 앞쪽 3봉 제거 후 동일 시각 기준 5~15 보정을 확인했다. SMA의 첫 유효 평균 115 및 첫 4봉 미표시, 4봉 데이터의 부족 안내를 확인했다. 포인터 탐색 시 113→123쌀 봉의 +10쌀·+8.85%와 거래량 140개를 표시했고 상세 영역 높이 79px를 유지하며 포인터 이탈 시 최근 봉으로 복귀했다. 빈 candles 응답을 브라우저에서 주입해 2일 자동 확장·빈 상태·탐색 버튼 비활성·2개 패널·chart DOM 1개 유지를 확인했다. 터치로 SMA를 켜고 키보드 Enter로 확대 제어를 실행했다. fixture·요청 가로채기를 해제한 뒤 실제 API의 최근가 90쌀·총거래량 456개로 복귀했으며 서버 거래나 주문을 실행하지 않았다. `corepack pnpm --filter @hanjjak/web typecheck`와 기존 `PriceTrendChart.test.tsx` 통합 회귀 1개가 통과했다. 구독 해제·생성 호출 횟수만 중복 단정하던 테스트는 제거하고 품목 전환의 표시 결과·단일 차트 회귀를 유지했다. 기존 localhost:3000 테스트 연결 거부 로그는 남아 있다. 전체 테스트·production build는 이번 변경에서 재실행하지 않았으며 I-06과 A-K 색인의 부분 구현·부분 검증 상태는 유지한다.
- 2026-09-12 가격 추이 간소화: 사용자 후속 요청으로 `apps/web/src/features/market/MarketScreen.tsx`의 개별 봉 상세 영역과 전용 hover 상태·crosshair 구독·등락 계산을 제거하고 `MarketScreen.css`의 차트 높이를 직전 대비 66%로 축소했다. 조회 기간 요약·캔들·종가선·SMA·가격/거래량 패널은 유지한다. 실제 로컬 Chromium의 1440×1200 화면에서 기본 448.797px·크게 보기 712.797px, 390×844 화면에서 기본 396px·크게 보기 580.797px를 측정했고 두 화면 모두 상세 DOM 0개·chart DOM 1개·차트 가로 넘침 없음을 시각 확인했다. 데스크톱의 실제 API 요약은 최근가 90쌀·총거래량 456개였다. UX 정본을 동기화했고 기존 `PriceTrendChart.test.tsx`에서 불필요해진 구독 mock만 제거했다. web typecheck 및 기존 품목 전환 회귀 1개 통과; 기존 localhost:3000 연결 거부 로그는 남아 있다. 전체 테스트·production build는 재실행하지 않았으며 I-06과 A-K 색인의 부분 구현·부분 검증 상태는 유지한다.
- 2026-09-12 가격 추이 고도화 철회: 사용자 요청으로 위 정보·탐색 고도화와 후속 간소화를 되돌렸다. `apps/web/src/features/market/MarketScreen.tsx`의 `PriceTrendChart`와 `MarketScreen.css`를 고도화 전의 캔들·꺾은선·거래량 통합 표시 및 포인터 탐색 시 한 줄 상세로 복원했다. 고도화 전에 적용한 연결선 잉크색·날짜 표시·품목 전환 수명주기 수정과 별도 호가 차트 개선은 유지했다. UX 정본의 철회된 고도화 규칙은 제거했으며 이전 두 항목은 현재 제공 범위가 아닌 검증 이력으로 보존한다. 실제 로컬 Chromium의 1440×1000·390×844 화면에서 차트 높이 210px·chart DOM 1개·추가 도구/상세 DOM 0개를 확인했고 모바일 차트 가로 넘침은 없었다. 실제 데이터 요약은 최근가 90쌀·거래량 456개였고 포인터 상세는 한국 시각 2026-09-12 17:25·종가 105·거래량 48개로 표시됐다. web typecheck와 기존 품목 전환 회귀 1개 통과; 기존 localhost:3000 테스트 연결 거부 로그는 유지된다. 전체 테스트·production build는 재실행하지 않았으며 I-06과 A-K 색인의 부분 구현·부분 검증 상태는 유지한다.
- 2026-09-12 가격 추이 높이·기본 분봉 조정: 사용자 요청으로 `apps/web/src/features/market/MarketScreen.css`의 차트 높이를 직전 대비 두 배로 늘리고 `MarketScreen.tsx`의 최초 봉 간격을 변경했다. 캔들·꺾은선·거래량 통합 표시와 기존 봉 간격·조회 기간 선택은 유지한다. [UX 정본](../../../../30-domain/player/ux/ssot.md)을 동기화하고 기존 `PriceTrendChart.test.tsx`의 품목 전환 fixture를 새 조회 간격에 맞췄다. 실제 로컬 Chromium의 1440×1000·390×844 화면에서 차트 높이 420px·최초 선택 15분/12시간·chart DOM 1개를 확인했으며 모바일 차트 가로 넘침은 없었다. 표시 요약은 최근가 90쌀·고가 145·저가 90·거래량 456개였다. web typecheck와 기존 품목 전환 회귀 1개가 통과했다. 기존 localhost:3000 테스트 연결 거부 로그는 유지되며 전체 테스트·production build는 재실행하지 않았다. I-06과 A-K 색인의 부분 구현·부분 검증 상태는 유지한다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 부분 구현 |
| 검증 | 부분 검증 |
