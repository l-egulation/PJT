---
doc_kind: task
owner_domain: delivery
task_code: 'K-14'
task_area: ''
task_type: '개발'
priority: 'P0'
target_chapters: '공통'
planning_status: '완료'
development_status: '부분 구현'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/a0806c8781b08254a8b40177d319faa5'
notion_id: 'a0806c87-81b0-8254-a8b4-0177d319faa5'
snapshot_date: '2026-08-28'
---

# K-14 간편 거래·주문 관리·수령·정산 UI 구현

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [클라이언트 UX](../../../../30-domain/player/ux/ssot.md)를 따른다.

## 완료 기준

거래소를 열 때마다 간편 거래로 시작해 가격 보호가 있는 매수·매도 IOC와 매도 지정가를 제공하고, 고급 주문 패널에서 매수·매도 지정가·IOC를 확인 후 제출한다. 활성·종료·`복구 검토 중` 주문, 부분 체결 잔량 수정·취소, 통합 물품 수령함, 판매 정산과 계정 공용 체결·수령·정산·만료 읽지 않음 cursor를 관리한다.

## 선행 작업

I-02, I-03, I-04, K-13

## 비고

2026-09-11 거래 화면과 `MarketManagement.tsx`의 진행/종료 주문·수령·정산·체결 내역을 재구성했다. 주문 수정은 명시적 편집 상태에서 처리하며 성공한 변경은 이전 오류 메시지를 대체한다. 읽음 처리는 요약의 최신 sequence를 보내지 않고 해당 목록을 다시 조회한 성공 응답의 `readThroughSequence`로 요청한다. `복구 검토 중` 주문 탭, 정산·만료 스트림의 조회/읽음 계약과 페이지 경계의 엄밀한 읽음 처리는 남아 부분 구현·부분 검증을 유지한다.

## 증거 링크
- 2026-09-10 판매·거래내역 전면 개편: 판매 화면은 구매와 같은 단일 품목 분류와 세부 필터를 공유하고 판매 수량 MAX는 전체 사용 가능 재고를 입력한다. 내 매물은 남은 수량 0인 행을 서버와 UI에서 제외한다. 거래내역은 `내 거래`·`전체 거래` 탭, 별도 수량 열, 페이지당 10건과 최대 99페이지를 적용하며 공개 응답에는 계정 식별자·수수료·정산액을 포함하지 않는다.
- 2026-09-10 현재 검증: 웹 전체 45개 파일·176개 테스트, 타입 검사와 프로덕션 빌드, TypeSpec 생성, game-api Kotlin 컴파일이 통과했다. Gradle JVM 테스트는 테스트 class 파일 생성 후에도 실행기가 해당 class를 찾지 못하는 기존 `ClassNotFoundException`으로 실행되지 않았다.
- 2026-09-09 거래소 호가창 UI 정리: `apps/web/src/features/market/MarketScreen.tsx`와 `apps/web/src/styles.css`에서 참고 이미지처럼 좌측 품목 필터, 중앙 낮은 가격순 판매 목록, 우측 구매 주문 패널을 둔 호가창형 구매 화면으로 바꿨다. 상단 `구매`·`판매`·`거래 내역` 탭은 각각 구매 호가창, 판매 등록/내 매물/정산, 체결 내역 패널로 전환된다. 파란 색조는 제거하고 감자·고구마·옥수수 중심의 종이색·갈색·붉은 강조 톤을 사용한다. 스킬북·보석은 백엔드 카탈로그에서 내려오는 거래 품목으로 표시하며 `백엔드 연결 전` 문구는 제거했다.
- GitLab 반영: MR 없이 `main`에 직접 push했다. 원격 기준 커밋은 `10ee6cf`(거래소 UI 정리), `7d1e401`(탭형 품목 슬롯), `771217c`(구매·판매·거래 내역 탭 전환 수정)이며 `origin/main` 최신 HEAD는 `771217c`다. GitLab Merge requests 목록에는 별도 MR 번호가 생기지 않는다.
- 2026-09-09 체크 표시 렌더링 보정: `apps/web/src/styles.css`의 문자 체크 표시를 제거하고 CSS border 기반 체크 모양으로 교체했다. `apps/web/src/features/market/MarketScreen.tsx`의 낮은 가격순·최저가 행 표시도 문자 체크에 의존하지 않는다.
- 검증: `corepack pnpm --filter @hanjjak/web test -- MarketScreen marketErrors marketForm marketPagination`, `corepack pnpm --filter @hanjjak/web typecheck`, `corepack pnpm --filter @hanjjak/web build` 통과.
- 2026-09-09 체크박스 레이아웃 재보정: `apps/web/src/styles.css`에서 품목 필터 버튼을 flex 행으로 고정하고 선택 체크를 `::before` 박스 내부 배경으로만 그렸다. 기존 `::after` 체크 레이어가 별도 grid 아이템으로 추가되어 선택된 품목명이 세로로 깨지던 문제를 제거했다.
- 2026-09-09 체크박스 명시 요소 전환: 품목·세부 품목 버튼에 `.market-filter-check` 전용 span을 넣고 flex 행 레이아웃으로 고정했다. 선택 체크는 해당 박스의 `::after` 안에서만 그려져 체크가 박스 밖으로 나오거나 라벨이 세로로 쪼개지지 않는다.
- 2026-09-09 호가창 표시 최종 보정: GitLab `main` 기준 시각 오류를 세 영역으로 수정했다. 품목 필터는 pseudo-element를 강제 제거하고 체크 전용 span과 라벨 span을 flex 한 줄로 고정했다. 중앙 `개당 가격` 헤더는 6열 그리드에 배치해 글자가 세로로 쪼개지지 않게 했고, 우측 구매 주문과 호가 행의 문자 아이콘은 품목 분류별 CSS 아이콘으로 교체했다.
- 2026-09-09 전투 획득 패널도 동일 보정: `apps/web/src/features/battle/BattleScreen.css`의 유니코드 접기/펼치기 문자를 제거하고 CSS border 화살표로 교체했다. 획득 패널 우측에 깨진 문자가 더 이상 렌더링되지 않는다.

- UI: `apps/web/src/features/market/MarketScreen.tsx`, `apps/web/src/features/market/marketUiState.ts`, `apps/web/src/features/market/api.ts`
- 테스트: `apps/web/src/features/market/marketUiState.test.ts`, `apps/web/src/features/market/api.test.ts`
- 검증: `corepack pnpm --filter @hanjjak/web test`, `corepack pnpm --filter @hanjjak/web typecheck`, `corepack pnpm --filter @hanjjak/web build`, 실제 브라우저 두 계정에서 구매 체결 후 판매자의 현재 매물 잔량·내 매물 잔량·정산 우편·거래 내역이 거래소 화면을 유지한 채 자동 갱신되는지 확인
- 2026-09-09 거래소 판매 최저가를 1,000쌀에서 10쌀로 낮췄다. 서버 도메인 상수, 클라이언트 입력 검증·안내, 단위 테스트와 SSOT/작업 문서를 함께 갱신했다.

- 2026-09-11 clean cutover: 웹의 구형 listing·purchase·buy-order adapter와 pagination helper를 제거하고 `/market/orders`, `/market/deliveries`, `/mails` 계약으로 교체했다. 웹 전체 46파일·274테스트, typecheck, production build 통과.
- 2026-09-11 실제 로컬 API·PostgreSQL 연결 Chromium 검증: 매수 IOC 1개/120쌀, 매도 IOC 1개/190쌀 및 정산 171쌀 수령, 매도 IOC 60개 요청의 54개 체결·6개 종료, 유동성 소진 후 `MARKET_NO_FILL` 응답을 확인했다. 매도 지정가 1개/230쌀 등록→취소→반환 물품 개별 수령, 기존 매수 주문 수량 100→99→100 수정도 모두 성공했다. 실패 뒤 성공 메시지 교체와 FILLS 읽음 요청 `readThroughSequence: 4`의 HTTP 200을 확인했다. 거래 상태 수치는 검증 계정의 관찰 결과이며 정책을 재정의하지 않는다.
- 2026-09-11 화면 증거: `build/market-redesign-orders.png`, `build/market-redesign-mobile-history.png`(로컬 산출물). 390px 폭의 체결 내역은 수량·단가·총액·시각을 모두 표시하며 내역 영역의 clientWidth/scrollWidth가 308px로 같음을 확인했다. 웹 검증 결과는 [K-13](./k-13-marketplace-search-price-ui.md), 검증 중 발견한 IOC 저장 순서 수정과 PostgreSQL E2E 결과는 [I-03](../I-marketplace-economy/i-03-instant-partial-purchase-settlement.md)을 참조한다.
- 2026-09-11 관리 화면 밀도 조정: `apps/web/src/features/market/MarketScreen.css`에서 주문서·관리 탭·기록 카드·버튼의 글자와 여백을 압축했다. 빈 목록의 쌀 아이콘이 원본 크기로 표시되는 문제를 발견해 40px로 제한하고 안내를 중앙 정렬했다. 모바일에서 내 주문·물품 수령함·판매 정산·체결 내역 진입과 빈 상태를 확인했다.
- 2026-09-11 실제 Chromium 390×844 검증: 로컬 검증 계정에서 매도 IOC 1개/115쌀을 제출해 체결 성공 안내, 판매 정산 104쌀 카드, 체결 내역의 수량·단가·총액·시각 표시를 확인했다. 판매 주문서에서 예상 판매금액·수수료·순정산액이 유지됐고, 정산 패널은 가로 넘침이 없으며 체결 카드 clientWidth/scrollWidth는 모두 311px였다. 이번 밀도 검증에서 정산 수령이나 주문 수정·취소를 다시 실행하지는 않았다. 로컬 증거: `build/market-density-empty.png`, `build/market-density-settlement.png`, `build/market-density-history.png`; 빌드와 화면 공통 검증은 [K-13](./k-13-marketplace-search-price-ui.md)을 참조한다. 기존 부분 구현·부분 검증 상태를 유지한다.
- 2026-09-11 `origin/main` 병합: 구매·판매·거래내역 진입 탭을 단일 주문서의 BUY·SELL·체결 내역에 연결하고 최신 navigation·관리 화면 변경을 보존했다. 관련 웹 테스트, typecheck와 production build가 통과했다.
- 2026-09-11 간편 거래·재시도 검증: 50개 구매 요청에서 1,000~1,005쌀 가격대 21개만 체결되고 30개가 종료되는 결과를 화면의 체결 수량·미체결 수량·가격 범위·평균 단가로 확인했다. 견적 확인 중 입력·모드가 잠기고, 응답 유실을 합성한 뒤 같은 멱등 키로 재시도해 동일 `orderId`·`commandId`와 단일 재고 증가를 확인했다. 판매 등록은 3개/1,010쌀 대기 주문으로, 즉시 판매는 2개/950쌀 체결·1,710쌀 순정산 예상 및 실제 정산 카드로 확인했다. 확인 취소는 입력값을 보존하고 주문을 만들지 않았다.

## 간편·호가창 역할 분리 제안 검증

2026-09-11 사용자 테스트 보고와 후속 명확화를 반영한 [거래 모드 개선 계획](../../../../70-plans/market-order-book/trading-modes-plan.md)의 구체 거래 흐름 인수 항목 후보다. 경험 기준은 [UX SSOT](../../../../30-domain/player/ux/ssot.md#거래소)에 전파됐으며 상세 제안은 현재 정책과 구분한다. 기존 실행 상태는 유지하고 거래 규칙을 재정의하지 않은 채 아래 관찰 결과를 책임 작업의 증거로 남긴다.

- 실제 로컬 API와 서로 다른 두 계정에서 최대 단가를 직접 설정한 간편 구매·판매 등록·즉시 판매를 실행하고 인벤토리·지갑·판매 정산·내 주문의 최종 상태를 비교한다. 기존 간편 판매의 첫 행동을 임의로 바꾸거나 구매 결과를 자동 강화·판매 정산 자동 청구로 연결하지 않는지 확인한다.
- 최저 가격대보다 많은 요청 수량을 더 높은 허용 단가로 여러 가격대에서 구매하는 경우를 검증한다. 예상 결제액·최대 지출·실제 결제액이 구분되고 가격 한도 밖의 물량은 체결되지 않아야 한다. 본인 반대 주문만 있거나 타인 주문과 섞인 경우, 상대 거래가 없는 경우도 각각 견적과 실제 승인/거절을 비교한다.
- 견적 뒤 다른 계정이 물량을 소진하거나 가격을 바꾼 경우, 확인한 가격 한도를 자동 변경하지 않는지와 일부 거래·거래 불가 결과가 실제 응답과 일치하는지 확인한다. 못 산/못 판 수량은 종료 후 잔량 필드만 보고 누락하지 않아야 한다.
- 견적 뒤 지갑·재고가 바뀌거나 구매 인벤토리가 부족한 경우를 검증한다. 실패를 성공으로 표시하거나 일부 자산만 반영하지 않는지 확인하고, 서버 계약과 다른 동작은 해당 I 작업에 연결한다.
- 화면의 예상 판매 수수료와 실제 개별 체결 수수료 합계·정산액을 비교한다. 예상 금액을 확정 금액으로 표시하거나 받을 쌀을 사용 가능 잔액으로 합치지 않아야 한다.
- 확인 취소·입력 변경·늦은 응답·중복 제출·결과 불확실 상황에서 의도하지 않은 별도 주문이 생기지 않는지 검증한다. 판매 등록의 즉시 판매분과 대기분, 취소 후 수령 경로도 확인한다.
- 고급 주문·주문 수정/취소·공통 수령/정산과 기존 장비 부족 재료 구매 경로가 계속 동작하는지 영향 범위를 검증한다. 기존 미완료 관리·읽음 항목의 상태는 유지한다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 부분 구현 |
| 검증 | 부분 검증 |
