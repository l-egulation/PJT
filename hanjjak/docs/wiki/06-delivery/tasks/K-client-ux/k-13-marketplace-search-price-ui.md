---
doc_kind: task
owner_domain: delivery
task_code: 'K-13'
task_area: ''
task_type: '개발'
priority: 'P0'
target_chapters: '공통'
planning_status: '완료'
development_status: '부분 구현'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/1ce06c8781b082f491f2817b9104ba45'
notion_id: '1ce06c87-81b0-82f4-91f2-817b9104ba45'
snapshot_date: '2026-08-28'
---

# K-13 거래소 품목 탐색·양방향 호가창 UI 구현

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [클라이언트 UX](../../../../30-domain/player/ux/ssot.md)를 따른다.

## 완료 기준

거래 품목 탐색·선택과 간편/고급 화면을 [클라이언트 UX SSOT의 거래소 기준](../../../../30-domain/player/ux/ssot.md#거래소)으로 검증한다. 시장 깊이 차트의 표시 값과 서버 depth 응답을 비교하고, hover 없이 가격·해당 잔량이 각 매물대 내부에 보이며 적은 물량·큰 숫자·좁은 화면에서도 문자가 영역 밖으로 벗어나지 않는지 확인한다. 최대 10호가의 매수·매도 양쪽이 가로 스크롤 없이 프레임 안에 함께 표시되는지 확인한다. 표시 전환·영역 선택·반응형 배치가 의도하지 않은 주문 변경이나 실행을 만들지 않음을 확인한다. 낮은 revision을 폐기하고 간격·cursor 오류 때 snapshot을 재조회하며 복구 중 주문 입력을 막는다.

## 선행 작업

I-02, I-06, K-02

## 비고

2026-09-11 단일 시장 품목 탐색과 간편/고급 화면을 구현했다. 최신 시장 깊이 차트와 탐색 고도화의 구현·검증 범위는 아래 증거를 따른다. 분류·검색, 3초 polling과 수동 query 무효화는 동작한다. 낮은 revision 폐기와 revision 간격·cursor 오류 snapshot 복구는 아직 없어 상태는 부분 구현·부분 검증으로 유지한다.

## 증거 링크
- 2026-09-10 거래소 전면 개편: 거래소를 메인 전투 위 dialog로 유지하면서 품목을 `강화재료`·`스킬북`·`보석` 단일 선택으로 바꿨다. 강화재료는 감자·고구마·옥수수 복수 체크와 선택 계열 기반 등급 콤보박스, 스킬북은 선택 시에만 등급·스킬 복수 필터, 보석은 레벨·옵션 콤보박스를 표시한다. 구매·판매·거래내역이 같은 필터를 공유하고, 재료 F·D·C·B·A 표시명과 `apps/web/src/features/equipment/assets/material-ranks` 이미지를 사용한다. 모든 페이지는 `<`·`>`와 `현재 / 총 페이지`를 표시하며 거래 내역은 페이지당 10건, 최대 99페이지다.

- UI: `apps/web/src/features/market/MarketScreen.tsx`, `apps/web/src/features/market/marketUiState.ts`, `apps/web/src/features/market/api.ts`, `apps/web/src/styles.css`
- 현재 테스트: `apps/web/src/features/market/OrderBook.test.ts`, `apps/web/src/features/market/marketUiState.test.ts`; 제거된 구형 `MarketScreen.test.ts`·`api.test.ts`는 이전 listing UI 검증 이력이다.
- 2026-09-10 현재 검증: 웹 전체 45개 파일·176개 테스트, 웹 TypeScript 타입 검사, Vite 프로덕션 빌드, TypeSpec 계약 생성, `:apps:game-api:compileKotlin`이 통과했다. Gradle JVM 테스트 실행기는 생성된 테스트 class가 존재하는데도 중첩/최상위 테스트 class를 찾지 못하는 `ClassNotFoundException`으로 종료되어 로컬 실행기 문제로 남겼다.
- 검증: `corepack pnpm --filter @hanjjak/web test` 36개 파일·113개 테스트 통과, `corepack pnpm --filter @hanjjak/web typecheck` 통과, `corepack pnpm --filter @hanjjak/web build` 통과. 로컬 Vite HMR 반영 확인. 기존 검증 범위: 실제 브라우저 두 계정에서 판매자 매물을 구매한 뒤 판매자 거래소 화면이 새로고침 없이 갱신되고 선택 재료·구매 수량·거래소 화면이 유지되는지 확인
- 2026-09-11 거래소 밀도·가격 정보 보정: 강화재료 등급 목록을 아래 방향으로 펼치는 인라인 목록으로 바꾸고, 판매 필터는 재료 계열·스킬북 등급·스킬을 각각 단일 선택으로 제한했다. 내 거래·전체 거래 표에는 거래 금액과 구분 사이에 개당 가격 열을 추가하고 헤더·행 글자를 확대해 기준선을 통일했으며, 기록이 종이 프레임을 넘지 않도록 내부 세로 스크롤과 숨김 스크롤바를 적용했다. 구매 상세의 최근 거래가는 시장 요약, 구매 가능 최저가는 견적 우선·시장 최저가 예비값으로 항상 표시한다. 필터와 거래 표 회귀 테스트를 포함해 웹 Vitest 50개 파일·288개 테스트, TypeScript typecheck와 production build를 통과했다.
- 2026-09-11 새 UI: `apps/web/src/features/market/MarketScreen.tsx`, `api.ts`, `OrderBook.test.ts`, `styles.css`. 웹 전체 46파일·274테스트, TypeScript typecheck와 production build 통과. 실제 브라우저 시각 검증은 백엔드·DB runtime 부재로 수행하지 못했다.
- 2026-09-11 디자인 재구성: `apps/web/src/features/market/MarketScreen.tsx`와 전용 `MarketScreen.css`에 종이 프레임, 재료 등급별 픽셀 이미지, 품목 탐색·시세/호가·주문서 배치를 적용하고 `apps/web/src/styles.css`의 구형 거래소 전용 규칙을 제거했다. 간편/호가 보기 전환은 선택 품목을 유지하며 호가 선택은 가격만 바꾼다. 1440×1000과 390×844 실제 Chromium에서 탐색·스크롤·주문 버튼 접근·Esc 닫기·재진입 시 간편 보기 초기화를 확인했다. 좁은 화면의 내역은 카드로 전환하며 호가 헤더와 행의 폭을 통일했다.
- 2026-09-11 재구성 검증: 웹 TypeScript 검사와 Vite production build 통과, 기존 웹 46파일·271테스트 통과. 테스트 과정의 localhost:3000 연결 거부 로그와 기존 production chunk 크기 경고는 남는다. 로컬 시각 증거는 `build/market-redesign-simple.png`, `build/market-redesign-book.png`, `build/market-redesign-mobile.png`에 저장했다. 이는 해당 실행의 로컬 산출물이며 정책 정본이나 배포 증거가 아니다. revision 복구의 잔여 범위는 그대로 유지한다.
- 2026-09-11 화면 밀도 조정: `apps/web/src/features/market/MarketScreen.css`에서 제목·본문·탭·패널 여백을 줄이고 품목 행 52px, 호가 행 28px, 주문 입력 30px로 압축했다. 데스크톱 시세 요약은 한 줄, 좁은 화면은 두 줄로 배치한다. 품목 목록의 내용 높이를 보존해 행 겹침을 막았으며 1440×1000에서 품목 10개가 완전히 보이는 것을 확인했다. 정책 변경 없이 [클라이언트 UX SSOT](../../../../30-domain/player/ux/ssot.md)의 표시 정보와 보기 전환 책임을 유지한다.
- 2026-09-11 밀도 검증: 실제 로컬 API·PostgreSQL 연결 Chromium의 1440×1000, 1366×768, 390×844에서 확인했다. 호가 125쌀 선택 뒤 매수 방향·수량 1개·선택 품목이 유지되고 가격만 바뀌었다. 낮은 데스크톱 화면은 주문서 내부 스크롤로 합계와 제출 버튼에 접근하며, 모바일 호가·주문서는 가로 넘침 없이 제출 버튼에 접근한다. Esc 닫기와 재진입 시 `거래`·`간편 거래` 초기화를 확인했다. TypeScript 검사와 최종 Vite production build 통과; 기존 500kB 초과 chunk 경고는 유지된다. 이번 CSS 조정에서는 단위 테스트를 추가하거나 전체 테스트를 재실행하지 않았다. 로컬 시각 증거: `build/market-density-book.png`, `build/market-density-short.png`, `build/market-density-mobile.png`. revision 복구 미구현으로 개발·검증 상태와 A–K/그룹 색인은 부분 상태를 유지한다.
- 2026-09-11 품목 찾기 계층화: `apps/web/src/features/market/MarketScreen.tsx`의 `InstrumentPicker`와 `MarketScreen.css`를 평면 목록에서 접기·펼치기 그룹과 등급/레벨 버튼으로 교체했다. 탐색과 주문 대상 선택을 분리했고, 전체 검색·검색어 지우기·선택 품목 위치 복귀를 연결했다. 상세 동작의 정본은 [클라이언트 UX SSOT](../../../../30-domain/player/ux/ssot.md#거래소)다. 보석 수치 표시는 `apps/web/src/features/gems/GemManagement.tsx`의 기존 `valueText`를 공유하며, 시장 품목 식별자를 합치거나 API 계약을 바꾸지 않았다.
- 2026-09-11 계층화 브라우저 검증: 실제 API 카탈로그의 재료 15종·스킬북 24종·보석 42종이 각각 3계열·6스킬·6옵션으로 묶이는 것을 확인했다. 1440×1000에서 주문 수량 7·단가 130을 입력한 뒤 스킬북 분류, `공격력 3레벨` 전체 검색, 결과 없는 검색으로 이동해도 감자 한 조각과 입력값이 유지됐다. 검색으로 고정 공격력·공격력% 보석이 구분됐으며, 키보드 Enter로 그룹 접기/펼치기와 Tab·Enter로 2레벨 고정 공격력 보석 선택을 확인했다. 390×844에서 `희귀 한짝` 검색→희귀 한짝의 일격 비법서 선택→선택 위치 복귀→호가 보기 전환, 옥수수 그룹→B등급 선택을 확인했고 탐색·본문 가로 넘침은 없었다. 선택 위치 복귀의 잘림은 scroll margin으로 보정 후 선택 버튼 전체 노출을 확인했다. 이번 작업에서는 주문 제출을 실행하지 않았다.
- 2026-09-11 계층화 검증 결과: TypeScript 검사·Vite production build 통과(기존 chunk 크기 경고 유지). 변경된 검색 계약의 `OrderBook.test.ts`와 기존 `GemsScreen.test.tsx` 2파일·10테스트 통과. 전체 웹 테스트는 재실행하지 않았다. 로컬 화면 증거: `build/market-picker-material.png`, `build/market-picker-search.png`, `build/market-picker-gem.png`, `build/market-picker-mobile.png`. 기존 revision 복구 잔여 범위 때문에 K-13과 A–K/그룹 색인의 부분 구현·부분 검증 상태는 유지한다.
- 2026-09-11 통합 호가 수량 그래프: `apps/web/src/features/market/MarketScreen.tsx`의 `OrderBookPanel`과 `MarketScreen.css`에서 양쪽 호가표를 통합 막대 그래프로 교체하고 고급 화면의 시세 요약 카드를 제거했다. 표시 규칙은 [UX SSOT](../../../../30-domain/player/ux/ssot.md#거래소), 최신 결정은 [결정 로그](../../../../80-decisions/README.md)를 따른다. 간편 화면·최근 체결 목록·API 계약·주문 체결 엔진은 유지한다.
- 2026-09-11 실제 API 연결 Chromium 검증: 1440×1000에서 95~140쌀의 매수 4개·매도 3개 막대가 가격순으로 배치되고 공통 수량축을 사용하는 것을 확인했다. 125쌀 매도 막대 선택 시 수량 7·매수 방향·IOC 유형을 유지하고 가격만 125로 변경했으며 상세에 수량 70·누적 108·내 주문 0을 표시했다. 390×844에서 본문·그래프 외곽 가로 넘침 없이 그래프 내부만 스크롤됐고 Enter로 140쌀 막대를 선택해 수량을 유지했다. 간편 화면으로 복귀하면 시세 요약이 그대로 표시된다. 이번 검증에서 주문 제출은 실행하지 않았다.
- 2026-09-11 그래프 경계 스모크: 실제 컴포넌트를 브라우저의 일회성 별도 root에 렌더링해 수량 1·전량 본인 주문, 한쪽 호가만 있음, 양쪽 빈 상태, 수량 1,000,000·내 주문 200,000을 확인했다. 0 기준 축·본인 주문 사선 비율·빈 상태 전환·단가 콜백을 확인했고 root와 임시 script는 제거했다. 이는 실제 시장 거래가 아니라 명시적 합성 입력의 표시 검증이다. TypeScript 검사와 Vite production build 통과; 기존 chunk 크기 경고 유지. 새 영구 테스트·전체 테스트 실행은 추가하지 않았다. 로컬 증거: `build/market-histogram-desktop.png`, `build/market-histogram-mobile.png`. K-13과 색인의 부분 구현·부분 검증 상태는 유지한다.
- 2026-09-11 시장 깊이 재디자인: 사용자 후속 요청으로 `apps/web/src/features/market/MarketScreen.tsx`의 `OrderBookPanel`과 `MarketScreen.css`에서 앞선 막대 그래프를 계단형 면적 시각화로 교체했다. 앞선 그래프 증거는 당시 이력이다. 현재 표현 기준은 [UX SSOT](../../../../30-domain/player/ux/ssot.md#거래소)이며 API·체결 엔진·간편 거래·최근 체결 목록은 변경하지 않았다.
- 2026-09-11 `origin/main` 병합: 최신 전역 폰트·장비/인벤토리 진입 UI를 유지하고 선택 품목을 단일 주문장의 매도 GTC 화면으로 연결했다. 관련 웹 45개 테스트, SDK 5개, 관리자 콘솔 3개 테스트와 TypeScript 검사·production build가 통과했다. 기존 localhost:3000 연결 거부 로그는 테스트가 의도대로 fallback을 검증할 때 출력되는 비차단 로그다.
- 2026-09-11 실제 API 연결 Chromium 검증: 1440×1000에서 매수 4개·매도 3개 호가점과 양쪽 누적 면적을 확인했다. 125쌀 매도 호가점을 클릭하면 상세에 해당 잔량 70·누적 108·내 주문 0이 표시되고, 수량 7·매수 방향·IOC 유형을 유지한 채 가격만 125로 입력됐다. 390×844에서 차트와 본문 가로 넘침이 0이며 상세가 줄바꿈되는 것을 확인했다. 140쌀 호가점의 키보드 Enter 선택도 수량 7·방향·유형을 유지하고 가격만 바꿨다. 간편 화면 복귀 시 기존 시세 요약이 표시됐다. 주문 제출은 실행하지 않았다.
- 2026-09-11 경계 표시 스모크: 실제 컴포넌트를 모바일 브라우저의 일회성 root에 렌더링해 단일 매수·수량 1·전량 본인 주문의 면적과 본인 표시·단가 콜백, 단일 매도·단가 999,999·수량 1,000,000·본인 200,000의 축과 넘침 없음, 양쪽 빈 상태로의 전환을 확인했다. 합성 입력을 사용한 표시 검증이며 root와 임시 전역은 제거했다. 최종 TypeScript 검사·Vite production build 통과(기존 chunk 크기 경고 유지). 새 영구 테스트나 전체 테스트 실행은 추가하지 않았다. 최종 로컬 화면 증거는 `build/market-depth-desktop.png`, `build/market-depth-mobile.png`다. K-13과 A–K 색인의 부분 구현·부분 검증 상태는 유지한다.
- 2026-09-11 가격·잔량 상시 라벨: `apps/web/src/features/market/MarketScreen.tsx`의 `OrderBookPanel`과 `MarketScreen.css`에 숫자 라벨·호가점 연결선·폭과 글꼴 측정 기반 배치를 추가했다. hover 없이 읽는 표현 기준은 [UX SSOT](../../../../30-domain/player/ux/ssot.md#거래소)를 따른다. API·주문 엔진과 누적 면적 의미는 변경하지 않았다.
- 2026-09-11 실제 API 연결 Chromium 검증: 1440×1000과 390×844에서 모든 7호가의 가격·해당 잔량이 hover 없이 표시됐다. 모바일의 라벨 사각형 간 겹침 0·차트와 본문 가로 넘침 0을 확인했다. 125쌀·잔량 70개 라벨 클릭 시 수량 7·매수·IOC를 유지하고 가격만 125로 바뀌었으며 하단 상세 누적 108과 상시 잔량 70이 구분됐다. 140쌀 호가점 Enter 선택도 주문 가격만 140으로 바꿨다. 주문 제출은 실행하지 않았다. 로컬 화면 증거: `build/market-depth-labels-desktop.png`, `build/market-depth-labels-mobile.png`.
- 2026-09-11 라벨 경계 스모크: 모바일 일회성 root에 합성 10호가·1쌀 간격·1,000,000,000,000 수량을 렌더링해 겹침·가로 넘침·문자 가로 잘림이 없고 큰 숫자는 생략 없이 줄바꿈되는 것을 확인했다. 단일 본인 주문 1개, 동일 가격의 수량 4,321 갱신·본인 표시 제거, 양쪽 빈 상태 전환도 확인했다. 임시 root·전역을 제거했다. TypeScript 검사·Vite production build 통과(기존 chunk 크기 경고 유지). 새 영구 테스트나 전체 테스트 실행은 추가하지 않았다. K-13과 A–K 색인의 부분 구현·부분 검증 상태는 유지한다.
- 2026-09-11 매물대 내부 표시: 사용자 후속 요청으로 `apps/web/src/features/market/MarketScreen.tsx`의 `OrderBookPanel`과 `MarketScreen.css`에서 떠 있는 숫자 라벨·연결선·측정 재배치를 제거하고 각 채움 영역 중앙에 가격·해당 잔량을 직접 넣었다. 표시 축·읽기 폭·적은 수량의 정보 공간은 [UX SSOT](../../../../30-domain/player/ux/ssot.md#거래소)를 따른다. 앞선 라벨 증거는 당시 이력이며 API·체결 엔진은 변경하지 않았다.
- 2026-09-11 실제 API 연결 Chromium 검증: 1440×1000에서 모든 7호가의 문자 경계가 해당 채움 영역 안에 있고 차트 가로 스크롤이 0이며 별도 라벨·연결선이 없음을 확인했다. 125쌀 매도 영역 클릭 시 수량 7·매수 방향·IOC 유형을 유지하고 가격만 125로 바뀌었고, 영역의 잔량 70과 하단 상세 누적 108을 구분했다. 390×844에서는 본문·차트 외곽 넘침 없이 내부만 451px 스크롤됐으며 140쌀 영역 focus·Enter가 해당 영역을 드러내고 가격만 140으로 바꿨다. 간편 화면 복귀도 수량 7·가격 140과 기존 요약을 유지했다. 주문 제출은 실행하지 않았다. 로컬 최종 화면: `build/market-depth-regions-desktop.png`, `build/market-depth-regions-mobile.png`.
- 2026-09-11 영역 경계 스모크: 모바일 일회성 root에 합성 10호가·1쌀 간격·수량 1과 1,000,000,000,000의 극단 차이를 넣어 모든 숫자가 해당 영역 내부에 있고 가로 잘림이 없음을 확인했다. 본인 주문 금색 밑선·텍스트, 단일 본인 주문 1개에서 스크롤 없음, 양쪽 빈 상태 전환도 확인했다. 임시 root·전역은 제거했다. TypeScript 검사·Vite production build 통과(기존 chunk 크기 경고 유지). 새 영구 테스트나 전체 테스트 실행은 추가하지 않았다. K-13과 A–K/그룹 색인의 부분 구현·부분 검증 상태는 유지한다.
- 2026-09-11 한 프레임 정보 밀도 개선: `apps/web/src/features/market/MarketScreen.tsx`의 `OrderBookPanel`과 `MarketScreen.css`에서 가격대 고정 최소 폭·가로 스크롤을 제거했다. 두 방향 차트의 반응형 배치·영역 내부 숫자·압축 상세 기준은 [UX SSOT](../../../../30-domain/player/ux/ssot.md#거래소)를 따른다. 앞선 내부 스크롤 증거는 당시 이력이며 API·체결 엔진은 변경하지 않았다.
- 2026-09-11 실제 API 연결 Chromium 검증: 1440×1000에서 매수 4개·매도 3개가 좌우 한 프레임에 표시되고 프레임 높이 약 325px·가로 넘침 0을 확인했다. 390×844에서는 두 방향이 상하로 배치되고 프레임 높이 약 402px·본문과 차트 가로 넘침 0이며 모든 숫자가 영역 안에 있었다. 125쌀 매도 영역 클릭과 140쌀 영역 Enter 선택이 수량 7·매수 방향·IOC 유형을 유지하고 가격만 변경했다. 간편 거래 복귀 시 수량 7·가격 140과 기존 시세 요약이 유지됐다. 주문 제출은 실행하지 않았다. 로컬 화면 증거: `build/market-depth-compact-desktop.png`, `build/market-depth-compact-mobile.png`.
- 2026-09-11 압축 경계 스모크: 일회성 root의 합성 10호가·6자리 가격·수량 1과 1,000,000,000,000으로 390px·320px 모바일 폭과 814px 데스크톱 컴포넌트 폭을 확인했다. 두 방향의 모든 영역이 같은 프레임 안에 들어가고 가로 넘침·문자 영역 이탈·가로 잘림이 없었다. 큰 숫자는 생략 없이 줄바꿈됐으며 모바일 프레임 높이는 약 462px였다. 단일 본인 주문·반대편 빈 상태·양쪽 빈 상태 전환도 확인했다. 임시 root·전역을 제거했다. TypeScript 검사·Vite production build 통과(기존 chunk 크기 경고 유지). 새 영구 테스트나 전체 테스트 실행은 추가하지 않았다. K-13과 A–K/그룹 색인의 부분 구현·부분 검증 상태는 유지한다.
- 2026-09-11 데스크톱 방향 판정 수정: 사용자 보고에 따라 `apps/web/src/features/market/MarketScreen.css`의 차트 컨테이너 기준 상하 전환과 해당 containment를 제거하고 거래소 기존 모바일 media query에 배치 전환을 통합했다. 현재 방향 판정 기준은 [UX SSOT](../../../../30-domain/player/ux/ssot.md#거래소)를 따른다. 앞선 컴포넌트 폭 기준 검증은 당시 이력으로 보존한다. API·주문 엔진은 변경하지 않았다.
- 2026-09-11 실제 API 연결 Chromium 검증: viewport 1024×900에서 차트 폭 436px이어도 양쪽이 같은 행에 배치되고 수량 높이 130px·가로 넘침 0·7호가의 문자 영역 내부 표시를 확인했다. viewport 681px·1280px에서는 좌우·130px, 680px·390px에서는 상하·62px로 전환됐으며 각각 본문과 차트의 가로 넘침이 0이었다. 125쌀 매도 영역 선택은 수량 7·매수 방향·IOC 유형을 유지하고 가격만 125로 바꿨다. 주문 제출은 실행하지 않았다. 화면 증거: `build/market-depth-desktop-fix.png`, `build/market-depth-mobile-breakpoint.png`.
- 2026-09-11 방향 판정 검증 결과: TypeScript 검사·Vite production build 통과(기존 chunk 크기 경고 유지). CSS의 실제 viewport 경계를 브라우저로 확인했으며 새 영구 테스트나 전체 테스트 실행은 추가하지 않았다. K-13과 A–K/그룹 색인의 부분 구현·부분 검증 상태는 유지한다.
- 2026-09-11 간편 가격대 안내 구현: `apps/web/src/features/market/PriceLevelGuide.tsx`와 전용 CSS를 추가해 BUY는 SELL 가격대를 낮은 가격순으로, SELL IOC는 BUY 가격대를 높은 가격순으로, SELL GTC는 경쟁 SELL 가격대를 표시한다. 각 가격대의 타인 물량·내 물량·누적 타인 물량과 입력 한도 안의 예상 배분을 숫자로 보여주며, 가격 선택은 가격만 입력한다. 10개 단위 exclusive pagination, 동일 revision 페이지 결합, revision 불일치 대기/재조회, 자기 교차 경고를 반영했다.
- 2026-09-11 실제 Chromium 거래 모드 검증: 1440×1000에서 520·900·1,200개 판매 가격대와 600개 요청의 110~125쌀 범위·예상 600개·평균 112쌀을 확인했다. 21개 가격대는 10·10·1개로 더 보기를 수행했고 누적 231개를 확인했다. 대기 매수의 호가창 전환은 수량 600·단가 125를 보존했고, 간편 구매 전환은 즉시 구매 변경을 별도 버튼으로 요구했다. 390×844 즉시 판매 화면의 본문·가격대·주문서 scrollWidth가 각각 clientWidth와 같고, 확인 화면에서 수량·최소 단가·수수료·순정산액을 확인했다. 가격대 페이지·견적은 합성 입력이 아닌 로컬 PostgreSQL API를 사용했다.
- 2026-09-11 구현 검증: `pnpm contracts:build`, `pnpm --filter @hanjjak/web typecheck`, `pnpm --filter @hanjjak/web build`, `pnpm --filter @hanjjak/web test`, `cmd.exe /c gradlew.bat :modules:market:test --no-daemon --max-workers=2`, `cmd.exe /c gradlew.bat :apps:game-api:test --tests com.hanjjak.gameapi.CraftingMarketE2ETest --no-daemon --max-workers=2`가 통과했다. Vite의 기존 500kB 초과 chunk 경고는 유지된다. revision 간격·cursor 복구 전체 snapshot은 아직 미구현이므로 상태는 부분 구현·부분 검증으로 유지한다.
- 2026-09-12 가격 추이 런타임 스모크: 인증된 Chromium 1440×1000에서 실제 로컬 API·PostgreSQL 연결 거래소를 열어 품목 탐색과 간편 가격대 안내가 렌더링되는 것을 확인했다. 첫 시장 품목에 `GET /api/v1/market/instruments/{instrumentId}/candles?interval=5m`을 호출해 HTTP 200, `interval: 5m`, 유효한 응답 envelope와 빈 체결 목록을 확인했다. 해당 품목에 체결 데이터가 없어 캔들·거래량의 비어 있지 않은 시각 표현은 아직 검증하지 않았으며, 앞선 호가창 실제 화면 증거는 유지한다.
- 2026-09-12 호가 거래 실제 화면 스모크: 인증된 Chromium 1440×1000에서 `호가 거래`로 전환해 가격 추이 SVG와 `매물 현황` 양방향 패널이 렌더링되는 것을 확인했다. 실제 API 응답은 candles HTTP 200·5분 봉 `items: []`, order book HTTP 200·매수 0·매도 0이었고, 거래소 품목 탐색·간편 가격대 안내·호가 화면의 주요 빈/비빈 상태 문구를 확인했다. 체결 없는 품목이므로 캔들 및 호가 잔량 수치의 비어 있지 않은 시장 검증은 별도 범위로 남긴다.
- 2026-09-11 UI 명칭·밀도 보정: 시장 깊이 패널 제목을 `매물 현황`으로 변경하고, 매도 차트 상단 `매도`를 오른쪽에 배치했다. 양쪽 차트의 `가격 쌀 / 잔량 개` 반복 라벨은 제거했다. 간편 가격대 판매 화면은 해당 가격의 전체 물량을 표시하고 그 아래에 본인 물량만 별도로 표시한다. 구매 수량 초기화 버튼은 `개` 오른쪽에 배치했다.
- 2026-09-11 구매 수량 초기화 표시 보정: numeric field의 입력 영역을 `flex: 1 1 0; width: 0`으로 제한하고 `개`·초기화 버튼을 고정 flex item으로 분리했다. x 문자는 의존하지 않고 CSS 두 선으로 `×`를 그려 좁은 주문 패널에서도 버튼이 오른쪽으로 밀려나지 않게 했다. 웹 typecheck·production build 통과.
- 2026-09-12 간편 거래 구매 안내 보정: `PriceLevelGuide`의 가격대 행을 main 브랜치의 판매 목록 표현과 맞춰 품목·분류·등급/속성·잔량·개당 가격 열을 표시하고, 각 행에 실제 시장 품목 아이콘과 아이템명을 렌더링했다. 행의 가격 버튼은 기존처럼 가격 입력만 갱신하며 수량·방향·주문 유형·제출 상태는 변경하지 않는다. 인증된 Chromium 1440×1000에서 감자 한 조각 아이콘·아이템명·F등급·125개 잔량·200쌀 단가를 확인했고, 웹 typecheck·Vitest 55파일 310테스트·production build를 통과했다.
- 2026-09-12 시장 fixture 보강: `tools/fixtures/market-fixture.mjs`를 추가해 로컬 PostgreSQL에 `POTATO_M1`·`POTATO_M2`·`CORN_M1`·`SWEET_POTATO_M1`의 가격대별 매도 주문과 최근 24시간 5분 봉 원본 체결을 반복 가능한 UUID로 적재한다. `corepack pnpm market:fixture` 실행 후 인증 Chromium에서 감자 한 조각 간편 안내의 95·110·125·140·200쌀 매도 가격대와 잔량을 확인하고, 호가 거래에서 5분 봉 12개·90~145쌀 고저가·2,611개 거래량과 매수/매도 depth를 확인했다. 스모크 계정은 검증 후 삭제했다. fixture는 로컬 DB에 명시적으로 실행할 때만 데이터를 넣고 기본 compose volume은 자동 seed하지 않는다.
- 2026-09-12 가격 추이 상호작용 보강: 가격 추이의 x 좌표를 봉 배열 순서가 아닌 API `from`·`to`·`openedAt` 시간 비율로 계산하고 날짜/시간 tick을 표시했다. 5분·15분·30분 봉 선택, 캔들·꺾은선 전환, 마우스/키보드 hover·focus의 x/y 점선과 시각·OHLC·거래량 tooltip을 추가했다. 로컬 fixture API에서 실제 5분 봉 데이터로 꺾은선 전환, 30분 선택, hover 이벤트 시 crosshair 2개와 tooltip 내용을 확인했으며 web typecheck·build, market module test, contracts build를 통과했다.
- 2026-09-12 후속 UI 요청 반영: 캔들과 종가 꺾은선을 동시에 표시하도록 가격 추이 표현을 통합하고, 24시간·7일·14일 표시 기간을 봉 간격과 독립적으로 선택하게 했다. 간편 거래의 `market-entry-modes market-side-toggle`는 가격 안내 프레임 내부로 이동해 가격 정보와 구매/판매 방향 선택을 한 덩어리로 배치했다.
- 2026-09-12 거래소 레이아웃 충돌 보정: `apps/web/src/styles.css`에 중복으로 남아 있던 구형 거래소 전역 규칙을 제거해 `MarketScreen.css`를 단일 화면 스타일 책임으로 복구했다. 데스크톱에서 제목·탭·품목 탐색·가격대·주문서의 상단 기준선을 맞추고, 간편 주문서의 중복 여백을 제거했다. 모바일에서는 간편 가격대와 주문서가 축소되지 않고 콘텐츠 영역의 단일 세로 스크롤로 이어지도록 고쳤다. 인증된 Chromium 1440×1000·1280×800·390×844에서 dialog와 데스크톱 하위 패널의 프레임 이탈 0, dialog·가격대·주문서의 가로 넘침 0, 모바일 가격대·주문서의 읽을 수 있는 고유 높이를 확인했다. 정책·API·주문 동작은 변경하지 않았으며 기존 revision 복구 잔여 범위 때문에 작업 상태는 부분 구현·부분 검증으로 유지한다.
- 2026-09-12 Playwright 레이아웃 후속 검수: 별도 일회성 Playwright Chromium 실행으로 인증·거래소 진입 후 1440×1000·1280×800·1024×768·390×844에서 거래·내 주문·물품 수령함·판매 정산·체결 내역을 순회했다. 모든 화면에서 수평 프레임 이탈 0과 dialog 가로 스크롤 0을 확인했다. 검수 중 모바일 `fieldset.market-picker-lock`이 데스크톱의 `height: 100%`를 유지해 품목 탐색 아래에 빈 358px을 만들고 거래 패널을 불필요하게 밀어내는 결함을 발견했다. 모바일에서 fieldset과 거래 영역을 내용 높이로 전환한 뒤 탐색 패널 다음 8px 간격으로 가격대가 이어지고 콘텐츠 scrollHeight가 1,328px에서 970px로 줄어든 것을 Playwright 실제 bounding box로 확인했다. 일회성 Playwright 설치·스크립트·화면 산출물은 저장소 밖 `C:/project/outputs/market-layout-playwright`에 두었고 제품 의존성이나 영구 테스트로 추가하지 않았다.
- 2026-09-12 거래소 글자 가독성 보정: `MarketScreen.css`와 `PriceLevelGuide.css`의 제목·탭·탐색·가격대·주문서·관리 목록·보조 문구를 기존 계층마다 약 1px씩 확대하고 화면 제목은 30→32px, 숫자 입력은 14→15px로 조정했다. 가격 추이 차트 축 글자도 11→12px로 맞췄다. Playwright Chromium 1440×1000·1280×800·1024×768·390×844에서 dialog 수평 이탈 0, 가격대 품목명 잘림 0, 제목·닫기 버튼 glyph 잘림 0을 확인했다. 1024px 이하 가격대는 품목·분류·속성·잔량·가격 열 폭을 명시하고 품목명 줄바꿈을 허용해 확대된 글자를 말줄임표로 숨기지 않는다. 모바일은 기존 단일 세로 스크롤을 유지한다.
- 2026-09-12 품목 찾기 아이콘 확대: `MarketScreen.css`에서 품목 그룹 아이콘을 24→29px, 강화 재료 등급 아이콘을 22→26px로 약 20% 확대하고 버튼 최소 높이를 각각 40px·35px로 맞췄다. Playwright Chromium 1440×1000·1024×768·390×844에서 실제 렌더 크기 29×29px·26×26px, 탐색 패널 가로 넘침 0, F~A 등급 글자 wrap 0을 확인했다.
- 2026-09-12 대형 품목 아이콘 배치: `MarketScreen.css`에서 품목 그룹 아이콘을 50×50px, `market-group-*` 하위 강화 재료 등급 아이콘을 40×40px로 확대했다. 그룹 행은 60px, 등급 버튼은 64px 이상으로 맞추고 데스크톱 품목 찾기 열을 310px로 넓혔다. 1100px 이하에서는 가격대·주문서의 최소 읽기 폭을 침범하지 않도록 품목 찾기를 상단 전체 폭으로 전환했다. Playwright Chromium 1440×1000·1280×800·1024×768·390×844에서 아이콘 실측 50×50px·40×40px, F~A 등급 wrap 0, dialog·품목 찾기·가격대·주문서 가로 넘침 0을 확인했다.
- 2026-09-12 선택 등급 그룹 아이콘 동기화: `InstrumentPicker`가 각 그룹의 첫 품목 아이콘을 고정 표시하지 않고, 해당 그룹에 현재 선택된 품목이 있으면 그 등급의 artwork를 50×50px 그룹 아이콘으로 사용하도록 변경했다. Playwright에서 감자 D·C·B·A·F등급을 순서대로 선택해 각 하위 40×40px 아이콘의 `src`와 상위 `.market-group-toggle` 아이콘의 `src`가 매번 일치하고 그룹 아이콘 크기가 50×50px로 유지되는 것을 확인했다.
- 2026-09-12 가격대 열 단순화와 모서리 보정: 간편 가격대 표에서 분류·등급/속성 열을 제거하고 `품목·잔량·가용 매물·개당 가격`을 기본 열로 사용한다. `가용 매물`은 각 가격까지의 누적 타인 물량 숫자만 표시하며 `누적 타인` 보조 문구는 제거했다. 구매 방향에서만 요청 수량과 가격 한도에 따른 `예상 배분` 열을 추가한다. Playwright 실제 API 화면에서 구매 헤더 5개와 판매 헤더 4개, 본문 셀 수 일치, 해당 문구 미노출, 표 가로 넘침 0을 확인했다. 함께 dialog와 `.market-screen`에 같은 배경·라운딩·overflow clip을 적용해 둥근 모서리에서 backdrop blur가 비쳐 빈 것처럼 보이던 영역을 채웠고, 1440×1000에서는 20px, 390×844에서는 14px 반경과 배경 이미지 적용을 확인했다.
- 2026-09-12 호가 거래 토글 통합: 간편 가격대 상단의 구매·판매 버튼 오른쪽에 `호가 거래` 토글을 같은 3열 컨트롤로 추가했다. 토글 활성화 시 가격대 표를 가격 추이·매물 현황으로 교체하고 버튼을 활성 상태로 표시하며, 다시 누르면 간편 가격대로 돌아간다. 구매·판매 방향과 입력 수량·가격은 보기 전환 뒤 유지하고, 호가 화면에서는 중복 매수·매도 버튼을 주문서에서 제거해 상단 공통 컨트롤만 사용한다. Playwright Chromium 1440×1000·390×844에서 세 버튼이 한 행에 표시되고 토글의 `aria-pressed` false→true→false, 가격대 1→0→1, 호가 패널 0→1→0 전환과 판매 방향 유지, 컨트롤 가로 넘침 0을 확인했다.
- 2026-09-12 호가 보기 위계 보정: `TradeModeControl`의 `호가 거래`를 구매·판매와 같은 크기의 세 번째 버튼에서 보조 체크박스로 변경했다. 구매·판매는 동일 너비의 주요 버튼으로 유지하고 체크박스 라벨은 데스크톱·모바일 모두 구매 버튼 폭의 60% 미만으로 표시한다. 실제 실행 중인 worktree UI에서 checkbox 접근성 role과 선택 전·후 간편 가격대↔가격 추이·매물 현황 전환을 확인했으며, 390×844에서 가로 넘침 0과 구매·판매 방향 유지도 확인했다. 웹 TypeScript 검사와 production build가 통과했고 기존 500kB 초과 chunk 경고만 유지된다.
- 2026-09-12 간편·호가 공통 프레임 고정: 모드별로 루트 시장 정보 컴포넌트를 교체하던 분기를 `market-market-frame` 내부 콘텐츠 교체로 바꿔 사용자가 지정한 시장 정보 `section`과 구매·판매·호가 거래 컨트롤을 동일 DOM·동일 위치로 유지한다. 실제 Chromium 1440×1000·1280×800에서 전환 전후 프레임과 구매·판매 버튼의 x·y·너비·높이가 모두 같았고, 390×844에서도 프레임 DOM·외곽선·배경과 상단 컨트롤 위치가 유지됐다. 웹 TypeScript 검사와 production build가 통과했으며 기존 500kB 초과 chunk 경고만 유지된다.
- 2026-09-12 가격 추이 기간 선택 변경: `24시간`·`7일`·`14일`을 `12시간`·`1일`·`2일`로 교체하고 12시간을 기본 선택으로 지정했다. 실제 Chromium에서 초기 `최근 12시간`, 버튼 선택 후 `period=1d`·`period=2d` 요청과 `최근 2일` 선택 상태를 확인했다. 웹 TypeScript 검사와 production build가 통과했으며 기존 500kB 초과 chunk 경고만 유지된다.
- 2026-09-12 가격 추이 crosshair 시각 형식 변경: Lightweight Charts `localization.timeFormatter`를 연결해 x축 crosshair 라벨을 로케일 기본값 대신 `YYYY-MM-DD HH:MM`으로 표시한다. 포맷 함수 회귀 테스트와 실제 2일 차트 hover에서 `2026-09-11 22:40` 형식의 축 라벨을 확인했다. 웹 55파일 311테스트, TypeScript 검사와 production build가 통과했으며 기존 localhost:3000 연결 거부 진단과 500kB 초과 chunk 경고만 유지된다.
- 2026-09-12 가격 추이 빈 기간 자동 확장: 선택 품목의 12시간 응답이 비면 1일, 다시 비면 2일을 순서대로 조회하고 활성 기간 버튼도 응답 범위에 맞춰 이동한다. 2일도 비면 Lightweight Charts canvas·시간축·격자를 유지한 채 차트 중앙에 `최근 2일간 체결 내역이 없습니다.`를 표시한다. 실제 체결 없는 1레벨 고정 공격력 보석에서 네트워크 요청이 `12h → 1d → 2d` 순서이고 2일 버튼 활성·canvas 7개·빈 상태 문구가 함께 존재함을 Chromium 1440×1000에서 확인했다. 전환 함수 회귀 테스트 포함 관련 4테스트, TypeScript 검사와 production build가 통과했으며 기존 500kB 초과 chunk 경고만 유지된다.
- 2026-09-13 간단 목록 기본화: `MarketSimpleList`를 추가하고 거래소 진입 기본 화면을 값·잔량·개당 가격 중심 목록으로 단순화했다. 전체·분류·계열·단일 품목 선택 범위를 가운데 목록과 공유하고, 선택 품목을 주문 영역에 다시 표시한다. `시세 보기`에서는 가격 추이와 양방향 매물 현황, 바로·예약 거래를 유지하며 두 보기 전환은 품목·방향·수량·가격 입력을 보존한다. 관련 구현은 `apps/web/src/features/market/MarketSimpleList.tsx`, `MarketTradeBoard.tsx`, `MarketScreen.tsx`, `MarketPlaza.css`, `MarketScreen.css`다.
- 2026-09-13 배포 전 검증: `corepack pnpm --filter @hanjjak/web test` 68파일·403테스트, `corepack pnpm --filter @hanjjak/web typecheck`, `corepack pnpm --filter @hanjjak/web build`가 통과했다. 테스트의 `localhost:3000` 연결 거부 로그는 API 미기동 fallback 검증 과정의 비차단 진단이며, Vite의 기존 500kB 초과 chunk 경고는 유지된다. 실제 운영 배포 후 시각 검증은 남아 있어 개발·검증 상태는 부분 상태를 유지한다.






### 2026-09-14 시세 보기 판매 가격 회귀 수정

- 시세 보기에서 바로판매(IOC)도 판매자가 희망 단가를 직접 입력할 수 있게 하고, 바로판매·예약판매(GTC) 전환 시 기존 입력을 유효한 경우 보존한다. 반대편 최우선 호가는 빈 값 또는 유효하지 않은 값일 때만 초기값으로 사용해 예약 가격이 0으로 되돌아가던 덮어쓰기를 제거했다.
- `MarketTradeBoard.price.regression.test.tsx`가 시세 보기의 바로판매 가격 입력 노출과 예약 123쌀→바로판매→예약 전환 보존을 검증한다. 1280×720 실제 미리보기에서도 동일 흐름의 입력값 `123` 유지를 확인했으며 주문 제출은 실행하지 않았다. 기존 미구현 revision 복구 범위 때문에 작업 상태는 유지한다.

## 간편·호가창 역할 분리 제안 검증

2026-09-11 사용자 테스트 보고와 후속 명확화를 반영한 [거래 모드 개선 계획](../../../../70-plans/market-order-book/trading-modes-plan.md)의 구체 표시안 인수 항목 후보다. 경험 기준은 [UX SSOT](../../../../30-domain/player/ux/ssot.md#거래소)에 전파됐으며 세부 표시안의 승인·전파 상태는 결정 로그를 따른다. 아래 항목을 추가한 것으로 기존 개발·검증 상태를 올리지 않는다.

- 사용자 보고의 기존 구매 경험을 기준으로 새 표현에서도 품목·수량·최대 단가를 정할 수 있는지 비교한다. 가격대별 물량을 읽고 허용 가격에 따른 누적 물량을 설명할 수 있는지 관찰한다. 기존 약 50명 테스트 결과의 재입증이나 새 화면 검증 완료를 주장하지 않는다.
- 해당 가격 물량과 누적 물량, 시장 물량과 내 조건의 예상 구매량을 혼동하지 않는지 확인한다. 특정 매물 리스트 모양이 아니라 읽을 수 있는 정보와 조작 결과를 검증한다. 색·막대 길이·hover 없이도 숫자를 확인할 수 있어야 한다.
- 동일 목적의 즉시 거래에서 모드 전환 뒤 품목·필터·방향·수량·허용 단가가 유지되고, 고급의 대기 조건이 간편의 즉시 거래로 몰래 변환되지 않는지 확인한다. 품목/방향 변경 및 늦은 견적 응답으로 이전 거래 대상이 제출되지 않아야 한다.
- 보석의 표시명이 같아도 실제 옵션 값이 다른 품목과 보유 인스턴스가 섞이지 않는지 확인한다.
- 짧은 데스크톱·모바일에서 구매/판매 확인과 취소·최종 실행에 접근하고, 키보드만으로 같은 흐름을 완료하는지 확인한다. 실제 화면과 사용한 viewport를 증거로 남긴다.
- 고급 호가 영역 선택의 기존 동작과 공통 품목 탐색을 회귀 확인한다. 기존 revision 복구 잔여 범위는 이 개선과 별도로 추적한다.
- 표시 마지막 가격대 밖의 입력, 추가 가격대 조회와 본인 물량 포함 사례에서 일부 표시 누적을 전체 시장 물량으로 오인하지 않는지 확인한다. 가격대 선택은 가격 한도만 바꾸며 요청 수량 변경·개별 매물 선택·자동 제출로 동작하지 않는지 확인한다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 부분 구현 |
| 검증 | 부분 검증 |
