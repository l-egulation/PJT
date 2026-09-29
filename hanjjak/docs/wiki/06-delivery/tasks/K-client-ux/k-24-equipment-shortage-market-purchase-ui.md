---
doc_kind: task
owner_domain: delivery
task_code: 'K-24'
task_area: 'K 클라이언트 UI·UX'
task_type: '개발'
priority: 'P0'
target_chapters: '공통'
planning_status: '완료'
development_status: '부분 구현'
verification_status: '부분 검증'
source: '2026-09-09-user-approved-plan'
created_at: '2026-09-09'
---

# K-24 장비 부족 재료 거래소 구매 UI 구현

> 작업 상태와 완료 증거의 SSOT. 거래 규칙과 화면 동작은 [거래소 SSOT](../../../../30-domain/economy/ssot.md), [클라이언트 UX SSOT](../../../../30-domain/player/ux/ssot.md)와 [구매 계획](../../../../70-plans/equipment-shortage-market-purchase/requirements.md)을 따른다.

## 완료 기준

장비 강화 카드에서 부족한 재료 하나를 선택하고, 본인 주문을 제외한 최저 매도호가를 수정 가능한 최대 단가 기본값으로 사용해 예상 체결 수량·가격 범위·가중평균 단가·미체결 수량과 최대 지출을 확인한 뒤 매수 IOC를 실행한다. 결과로 장비 상태를 다시 조회하며 부족량이 남으면 강화를 계속 막고 모든 비용을 충족해도 별도 강화 확인 전에는 강화하지 않는다.

## 선행 작업

I-03, K-11, K-15

## 비고

한 번에 단일 재료만 구매한다. 여러 부족 재료 일괄 구매, 최초 제작·승급 연계, 구매·강화 결합 command와 자동 강화는 범위에서 제외한다.

2026-09-11 장비 부족 재료 구매 모달을 새 시장 품목 UUID와 매수 IOC 견적·주문 계약으로 이관했다. 본인 주문을 제외한 매도호가 기준 예상 체결 수량·미체결 수량·가격 범위·가중평균·최대 지출을 조회하고, 수량 고정 상한 없이 안전한 숫자 입력과 서버 견적을 사용한다. 구매 뒤 장비 상태를 다시 조회하며 자동 강화를 하지 않는다. 변경된 장비 모달의 전용 회귀 테스트는 구형 API 테스트 정리 과정에서 아직 복구하지 못했고 실제 브라우저 HTTP 인수도 남아 상태는 부분 구현·부분 검증으로 유지한다.

## 증거 링크

- 기획: `docs/70-plans/equipment-shortage-market-purchase/requirements.md`
- 정책: `docs/30-domain/economy/ssot.md`, `docs/30-domain/player/ux/ssot.md`
- UI: `apps/web/src/features/equipment/EquipmentScreen.tsx`, `apps/web/src/features/equipment/api.ts`, `apps/web/src/styles.css`
- 시장 UI 계약 동기화: `apps/web/src/features/market/MarketScreen.tsx`, `apps/web/src/features/market/api.ts`
- 테스트: `EquipmentScreen.test.tsx`, 장비·시장 `api.test.ts`, `marketErrors.test.ts`
- 2026-09-09 검증: 웹 테스트 32개 파일·90개 테스트, typecheck, Vite build 통과. 실제 컴포넌트 정적 렌더에서 부족 재료에만 `거래소 구매`가 노출됨을 확인했다. 브라우저에서 1440×1000 구매 확인 표면을 열어 단일 재료, 시장 최저 매물, 구매 가능 최저가, 개당 상한, 최대 예상 지출, 자동 강화 제외 안내와 가로 overflow 없음을 확인했다. Vite 프록시 대상 game-api를 함께 실행한 실제 브라우저 HTTP 구매는 미검증이므로 부분 검증을 유지한다.
- 2026-09-09 빈 매물 회귀 수정: API 전역 `NON_NULL` 직렬화로 가격 `null` 필드가 생략돼 클라이언트의 strict null 분기에서 `undefined.toLocaleString()`이 발생하며 장비 화면 전체가 비던 문제를 수정했다. `MarketPurchaseQuote`는 두 가격 필드를 명시적 `null`로 직렬화하고 클라이언트도 nullish 값을 안전하게 처리한다. 실제 로컬 `asdf@asdf.com` 계정과 활성 매물 0건 DB에서 거래소 구매를 실행해 장비 화면 유지, 시장 최저 매물·구매 가능 최저가 `없음`, 구매 가능 수량 0개, `구매 확인` 비활성화를 브라우저로 확인했다. `:modules:market:test`, 웹 32개 파일·91개 테스트, typecheck, build가 통과했다.
- 2026-09-09 구매 상한 활성화 회귀 수정: 시스템 매물 10쌀 견적이 입력되지만 판매 등록용 1,000쌀 하한 검증을 구매 상한에도 재사용해 `구매 확인`이 비활성화되던 문제를 수정했다. 사용자 판매 등록은 기존 1,000쌀 하한을 유지하고 구매 `maxUnitPrice`는 10~999,990쌀의 10쌀 단위로 분리했다. 실제 로컬 `asdf@asdf.com` 계정에서 감자 M1 시장·구매 가능 최저가 10쌀, 부족 25개, 최대 예상 지출 250쌀, `구매 확인` 활성 상태와 브라우저 오류 0건을 확인했다. `:modules:market:test`, TypeSpec build, 웹 32개 파일·92개 테스트, typecheck, build가 통과했다.
- 2026-09-11 구매 수량 입력 안정화: 수량 입력값과 견적 조회값을 `useDeferredValue`로 분리하고 직전 견적을 유지해 입력할 때마다 구매 모달 전체가 로딩 화면으로 교체되지 않게 했다. 사용자가 수정한 가격 상한은 후속 견적 응답으로 덮어쓰지 않는다. 장비 구매 모달 회귀 테스트를 포함해 웹 Vitest 50개 파일·288개 테스트, TypeScript typecheck와 production build를 통과했다.
- 2026-09-11 계약 이관: `EquipmentScreen.tsx`, `features/equipment/api.ts`가 `GET /market/instruments`, `GET /market/instruments/{id}/order-quote`, `POST /market/orders`를 사용한다. 웹 46파일·274테스트, typecheck와 production build 통과.
- 2026-09-11 `origin/main` 병합: 최신 장비 6부위 레이아웃을 유지하면서 부족 재료 구매를 `instrumentId`·BUY IOC 견적·통합 주문 API에 다시 연결했다. `EquipmentScreen` 관련 테스트와 웹 TypeScript 검사·production build가 통과했다.
- 2026-09-11 1쌀 단위 구매 상한: 장비 부족 재료 구매 모달도 거래소와 같은 범위 검증을 사용해 13·14쌀 최저가를 반올림하지 않고 그대로 구매 상한으로 허용한다. 오류 안내도 10~999,990쌀 범위의 정수 기준으로 바꿨다.
- 2026-09-11 구매 결과 레이아웃 보호: 구매 성공·실패 결과와 결과 불확실 재확인 동작을 장비 본문 흐름에서 제거하고 확인 버튼이 있는 오버레이 모달로 표시한다. 브라우저에서 구매 완료 모달과 확인 후 같은 장비 배치 복귀를 확인했고 웹 전체 59개 파일·338개 테스트, TypeScript typecheck와 production build가 통과했다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 부분 구현 |
| 검증 | 부분 검증 |
