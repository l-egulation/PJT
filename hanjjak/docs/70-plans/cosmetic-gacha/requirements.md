---
doc_kind: requirements
owner_domain: cosmetics
authority_level: implementation-guidance
status: approved
approved_at: 2026-09-02
---

# 치장 뽑기 시스템 요구사항 명세서

## 1. 목적
규칙은 [치장 SSOT](../../30-domain/cosmetics/ssot.md), 확정 통합 결정은 [캐릭터·치장·도감 통합 계획](../character-cosmetics-collection/integration-plan.md), 수치·카탈로그는 [MVP 콘텐츠 v1](../../60-content/cosmetics/mvp-v1.md)가 소유한다. 본 문서는 구현 범위와 검증만 소유한다.

## 2. 기준 문서와 우선순위
[2026-09-07 구현 우선 설계](../../superpowers/specs/2026-09-07-character-cosmetics-implementation-first-design.md), 통합 계획과 SSOT를 현재 기준으로 사용한다. [2026-09-02 콘텐츠 바이블 설계](../../superpowers/specs/2026-09-02-mvp-content-bible-design.md)와 [2026-09-01 설계](../../superpowers/specs/2026-09-01-cosmetic-gacha-design.md)는 이전 승인 기록이며 현재 카탈로그 수·명칭·비용·등급 배치 충돌에는 사용하지 않는다.

## 3. 범위
1-5 해금, 픽업, 1·10회 추첨, 결제, 보유·등록·도감·세트·상자, 외형 착용 API, 전투 보유 효과 반영, 원자성·멱등성·감사를 포함한다. 실제 렌더링·티켓 공급·최종 UI·상세 HTTP 계약·보존 기간은 제외한다.

## 4. 용어와 안정 식별자
pickup banner는 전설 세트 하나의 영구 배너다. common cosmetic ticket은 한 회 비용을 대체한다. `registeredQuantity`는 누적 등록 수량, `unregisteredQuantity`는 미등록 중복 재고, `reservedQuantity`는 거래 예약 재고, `availableUnregisteredQuantity`는 `unregisteredQuantity - reservedQuantity`다. `cosmeticStar`는 1~5성이며 `starThresholds`는 성급별 누적 등록 수량 임계값이고 1성 임계값은 1이다. 최초 미보유 결과는 신규이며 자동 등록과 1성을 함께 확정한다. 이후 같은 치장 결과는 중복이며 미등록 중복 재고에 쌓인다.

collection registration은 `registeredQuantity >= 1`인 도감 등록 상태다. 도감은 등록 현황, 미보유 필터, 치장별 등록 수량·성급, 세트 이동과 완성 기록만 제공하며 도감 단계, 단계 보상과 도감 전투 효과는 없다. set은 `HEAD`, `TOP`, `BOTTOM`, `GLOVES`, `SHOES`, `CAPE` 여섯 부위를 정확히 하나씩 가진 묶음, set star는 여섯 구성원 성급의 최솟값, active set effect는 현재 성급 행의 누적 총값이다. 착용은 외형 슬롯 상태만 바꾸며 보유 효과를 바꾸지 않는다. 같은 계정에 현재 메인 전투가 있으면 보유 효과 변경은 현재 사이클 snapshot을 바꾸지 않고 다음 cycle start input에 포함한다. successful draw count·claimed-box count·claimable-box count는 각각 성공 수·수령 수·`floor(totalSuccessfulDraws / 200) - claimedBoxCount`다. selector box는 선택 치장 교환 아이템이다. content version은 콘텐츠 묶음 버전, audit reproduction token은 서버 재현 토큰이다.

| Rarity ID | 표시 의미 |
| --- | --- |
| NORMAL | 노말 |
| RARE | 희귀 |
| EPIC | 영웅 |
| LEGENDARY | 전설 |

| Slot ID | 표시 의미 |
| --- | --- |
| HEAD | 머리 |
| TOP | 상의 |
| BOTTOM | 하의 |
| GLOVES | 장갑 |
| SHOES | 신발 |
| CAPE | 망토 |

API·저장 ID는 안정적이며 표시 문구만 현지화한다.

## 5. 사용자 흐름
재화 확인 → 요청 → 서버 추첨 → 지급 → 신규 자동 등록 또는 중복 미등록 재고 적립 → 등록·도감·세트 보유 효과 확정 → 다음 메인 전투 사이클 반영 → 반환. 외형 착용 요청은 별도 슬롯 상태만 확정한다. idempotency key는 공통 요청 헤더다.

## 6. 기능 요구사항
| ID | 규범 요구사항 | 관찰 가능한 검증 |
| --- | --- | --- |
| COS-UNLOCK-001 | 1-5 최초 클리어에서만 해금한다. | Setup account `unlocked=false,ticketBalance=0`; emit exactly one `1-5 firstClear=false→true`; assert one committed response has `unlocked=true` and `ticketBalance=10`. |
| COS-UNLOCK-002 | 해금과 공용 티켓 10장 지급을 원자 처리한다. | Setup `unlocked=false,ticketBalance=0`; commit the 1-5 clear; assert unlocked and ticket grant appear in one commit with no intermediate state. |
| COS-UNLOCK-003 | 동일 클리어 재전송은 지급을 반복하지 않는다. | After first clear leaves `ticketBalance=10`, replay the same clear event; assert ticket delta is exactly `0` and final state is unchanged. |
| COS-UNLOCK-004 | 해금 전 모든 치장 API를 거절한다. | Use `unlocked=false`; call banner GET and draw POST; assert each returns `COSMETIC_SYSTEM_LOCKED` and balances, ownership, and counters are unchanged. |
| COS-BANNER-001 | 일반 배너를 제공하지 않는다. | After unlock, GET banners and filter `type=STANDARD`; assert standard-list length is exactly `0`. |
| COS-BANNER-002 | 전설 세트마다 영구 픽업 배너를 연결한다. | For each active LEGENDARY setId, GET the list twice; assert exactly one stable bannerId maps to that set and both mappings are equal. |
| COS-BANNER-003 | 전설 세트·배너는 1:1이며 각 배너는 유효한 선택 상자를 지급한다. | Load active set/banner/box mappings; assert every LEGENDARY set has exactly one stable banner and every banner milestone references a valid selector-box item ID. |
| COS-BANNER-004 | 모든 배너가 동일 하위 등급 풀을 쓴다. | GET details for banners A and B; assert NORMAL, RARE, and EPIC pool ID sets are equal element-for-element. |
| COS-BANNER-005 | 전설은 해당 배너 세트에서만 나온다. | Draw `count=10` from banner B; assert every LEGENDARY result has `setId=B.setId` and none has another setId. |
| COS-BANNER-006 | 배너는 만료·회차 초기화 없이 연다. | Record banner B availability and `successfulDrawCount=n`; cross a time boundary and fetch again; assert availability remains true and count remains n. |
| COS-BANNER-007 | 공통 1회 비용을 콘텐츠에서 읽는다. | Read active `singleRiceCost=c` and detail `oneDrawCost`; assert exact equality `oneDrawCost=c`. |
| COS-BANNER-008 | 목록은 재화·예상 결제를 반환한다. | ticketBalance=3, riceBalance=singleRiceCost×7인 계정이 목록을 조회한다; 1회 예상 결제는 ticket 1·rice 0, 10회 예상 결제는 ticket 3·rice singleRiceCost×7이고 두 요청 모두 executable=true인지 확인한다. |
| COS-BANNER-009 | 상세는 풀·확률을 공개한다. | 배너 상세의 모든 등장 치장에 정확한 numerator·denominator와 표시용 퍼센트 문자열이 있고, 표시 반올림과 무관하게 추첨·검증은 분자·분모를 사용하는지 확인한다. |
| COS-BANNER-010 | 확률·결과는 서버만 결정한다. | Send identical count-only and client-augmented draw bodies; assert extras are ignored or rejected and returned result is server-selected. |
| COS-PAY-001 | 티켓을 항상 먼저 사용한다. | ticketBalance=2, riceBalance=singleRiceCost×8인 계정으로 count=10을 실행해 티켓 2장과 쌀 8회분을 차감하고 결과 10개를 커밋하는지, 쌀이 부족하면 `INSUFFICIENT_GACHA_FUNDS`와 변경 없음인지 확인한다. |
| COS-PAY-002 | 티켓 한 장은 한 회 비용 전체를 대체한다. | ticketBalance=1인 계정에서 count=1을 실행해 티켓만 1 감소하고 쌀은 차감되지 않는지 확인한다. |
| COS-PAY-003 | 부족분은 쌀로 계산한다. | ticketBalance=1, riceBalance=singleRiceCost×9인 계정에서 count=10을 실행해 티켓 1장과 쌀 9회분만 차감하는지 확인한다. |
| COS-PAY-004 | count는 1과 10만 허용한다. | count를 0, 2, 9, 11로 POST하면 모두 `INVALID_DRAW_COUNT`이고 잔액·successfulDrawCount가 요청 전과 같은지 확인한다. |
| COS-PAY-005 | 10회 비용은 1회의 정확히 10배다. | 활성 콘텐츠 singleRiceCost를 c로 읽고 count=1과 count=10의 예상 결제를 조회해 10회 rice 비용이 c×10인지 확인한다. |
| COS-PAY-006 | 부족하면 전부 실패하고 소비하지 않는다. | ticketBalance=0, riceBalance=singleRiceCost×10-1에서 count=10을 실행해 `INSUFFICIENT_GACHA_FUNDS`와 잔액·카운터·보유 상태 변경 없음을 확인한다. |
| COS-PAY-007 | 조회 예상은 예약이 아니며 실행 때 재검증한다. | 조회 후 잔액을 예상 비용 미만으로 낮추고 같은 요청을 POST해 실행 시점 잔액으로 실패하며 예약·선차감 기록이 없는지 확인한다. |
| COS-PAY-008 | 잔액은 0 이상 정수다. | 음수·소수 잔액 저장 시도는 거절되고 기존 잔액이 유지되는지 확인한다. |
| COS-PAY-009 | paymentType과 결제 순서를 입력받지 않는다. | paymentType과 paymentOrder를 추가해 POST해도 무시·거절되고 결제는 ticket 우선으로만 결정되는지 확인한다. |
| COS-DRAW-001 | 추첨·결과를 서버만 확정한다. | 클라이언트 표시 결과를 변조해도 응답 results와 영속 치장 상태가 서버 난수원이 확정한 목록과 일치하는지 확인한다. |
| COS-DRAW-002 | 등급을 먼저 추첨한다. | 각 결과의 첫 난수로 grade가 정해지고 다음 난수로 해당 grade의 cosmeticId가 선택되는지 확인한다. |
| COS-DRAW-003 | 등급 내 치장은 동일 가중치다. | 치장 수가 N인 grade에서 모든 cosmeticId의 가중치가 1이고 조건부 확률이 1/N인지 확인한다. |
| COS-DRAW-004 | 각 결과는 독립 추첨한다. | 두 결과의 결합확률이 각 확률의 곱이고 각 칸마다 별도 난수 호출이 소비되는지 확인한다. |
| COS-DRAW-005 | 같은 치장 중복을 허용한다. | 난수원을 X 연속 선택으로 고정해 results가 [X,X]이고 재추첨하지 않는지 확인한다. |
| COS-DRAW-006 | 보장·미보유 보정·중복 방지·천장을 쓰지 않는다. | 미획득 누적이나 보유 상태가 configuredProbability를 바꾸지 않고, 보유 치장도 재추첨 없이 반환되는지 확인한다. |
| COS-DRAW-007 | 전설 확률은 배너 간 동일하다. | 배너 A와 B의 LEGENDARY probability가 같고 배너 식별자 보정이 없는지 확인한다. |
| COS-DRAW-008 | 응답은 추첨 순서를 보존한다. | 서버 난수원을 [A,B,A]로 고정하고 응답 results 인덱스가 그대로 유지되는지 확인한다. |
| COS-DRAW-009 | 첫 미보유 결과는 신규 자동 등록으로 판정한다. | 미보유 X를 고정해 `isNew=true`, 커밋 후 `registeredQuantity=1`, `unregisteredQuantity=0`, `cosmeticStar=1`인지 확인한다. |
| COS-DRAW-010 | 뒤의 동일 결과는 미등록 중복으로 판정한다. | [X,X] 결과에서 첫 결과는 `isNew=true`로 자동 등록되고 둘째는 `isNew=false` 및 `unregisteredQuantity + 1`인지 확인한다. |
| COS-DRAW-011 | 결제·지급·상태를 원자 커밋한다. | 비용 차감과 신규 자동 등록 또는 중복 재고 적립을 한 트랜잭션으로 실행하고 실패 주입 시 모두 초기값으로 롤백되는지 확인한다. |
| COS-DRAW-012 | 성공 응답은 커밋 후에만 반환한다. | 커밋 직전에는 성공 응답이 전송되지 않고 커밋 후 results 응답이 전송되는지 확인한다. |
| COS-DRAW-013 | 입력으로 확률·결과·보상목록을 받지 않는다. | 요청 스키마에 probability, result, rewardList가 없고 서버 선택 경로에 전달되지 않는지 확인한다. |
| COS-DRAW-014 | 실패 시 어떤 결과도 지급하지 않는다. | 결과 생성 후 커밋 전에 실패를 주입해 비용·등록 재고·미등록 재고·지급 이력이 모두 요청 전과 같은지 확인한다. |
| COS-OWN-001 | 보유를 200칸 인벤토리와 분리한다. | 일반 인벤토리가 가득 차도 치장 획득 시 별도 치장 상태에 저장되는지 확인한다. |
| COS-OWN-002 | 최초 획득은 자동 등록·1성이다. | 미보유 치장 최초 획득 후 `registeredQuantity=1`, `unregisteredQuantity=0`, `reservedQuantity=0`, `cosmeticStar=1`인지 확인한다. |
| COS-OWN-003 | 중복은 미등록 재고로 누적한다. | 등록된 치장을 중복 획득하면 registeredQuantity와 cosmeticStar는 유지되고 unregisteredQuantity만 결과 수만큼 증가하는지 확인한다. |
| COS-OWN-004 | 5성 이후 중복도 미등록 재고로 보관한다. | 5성 치장 중복 후 cosmeticStar는 5, registeredQuantity는 유지되고 unregisteredQuantity가 증가하는지 확인한다. |
| COS-OWN-005 | 등록 상태는 누적 등록 수량으로 판정한다. | registeredQuantity가 1 이상이면 등록, 0이면 미등록으로 판정되는지 확인한다. |
| COS-OWN-006 | 거래 가능 수량은 예약 제외 미등록 재고뿐이다. | unregisteredQuantity=5, reservedQuantity=2이면 availableUnregisteredQuantity=3이고 거래 등록 가능 수량도 3이며 등록 수량·성급·장착 상태는 거래 대상이 아닌지 확인한다. |
| COS-OWN-007 | 보유는 일반 슬롯을 차지하지 않는다. | 치장 신규·중복 획득 전후 일반 인벤토리 usedSlots가 같은지 확인한다. |
| COS-OWN-008 | 신규·중복은 서버 결과 순서로 판정한다. | [미보유 A, A, 미보유 B] 순서가 [A 신규 자동 등록, A 미등록 재고 +1, B 신규 자동 등록]으로 판정되는지 확인한다. |
| COS-REGISTRATION-001 | 등록 명령은 ONE과 UNTIL_NEXT_STAR만 제공한다. | POST `/api/v1/cosmetics/{cosmeticId}/registrations`에 두 mode는 허용되고 다른 mode·임의 목표 성급·수량은 `INVALID_REGISTRATION_MODE` 또는 스키마 오류와 변경 없음인지 확인한다. |
| COS-REGISTRATION-002 | 미등록 중복 재고가 있어야 등록한다. | 등록 재고가 없으면 `INSUFFICIENT_UNREGISTERED_COSMETICS`, 1개가 있으면 ONE 성공 후 registeredQuantity +1·unregisteredQuantity -1인지 확인한다. |
| COS-REGISTRATION-003 | 성급은 누적 등록 임계값으로 계산한다. | starThresholds의 1성 값이 1이고 인접 값이 엄격히 증가하는 콘텐츠를 검증한 뒤 각 임계값 직전·도달 registeredQuantity의 cosmeticStar가 최댓값 공식과 일치하는지 확인한다. |
| COS-REGISTRATION-004 | ONE은 정확히 1개만 등록한다. | availableUnregisteredQuantity가 여러 개여도 ONE은 registeredQuantity +1·unregisteredQuantity -1만 수행하고 필요한 경우에만 성급을 재계산하는지 확인한다. |
| COS-REGISTRATION-005 | UNTIL_NEXT_STAR는 다음 성급까지만 등록한다. | 다음 starThreshold까지 neededCount가 남으면 UNTIL_NEXT_STAR가 정확히 neededCount만 소비하고 한 성급만 올리는지 확인한다. |
| COS-REGISTRATION-006 | 거래 예약 재고는 등록에 쓸 수 없다. | unregisteredQuantity=5, reservedQuantity=3, neededCount=3이면 availableUnregisteredQuantity=2로 실패하고 네 수량·성급이 모두 유지되는지 확인한다. |
| COS-REGISTRATION-007 | 등록 소비와 성급 재계산을 함께 커밋한다. | 등록 처리 중 실패를 주입하면 registeredQuantity, unregisteredQuantity, reservedQuantity, cosmeticStar, 세트 효과가 모두 롤백되는지 확인한다. |
| COS-REGISTRATION-008 | 등록에는 확률 실패를 사용하지 않는다. | 충분한 가용 재고로 같은 mode를 반복해도 확률 추첨 없이 계산된 등록 수량과 성급으로 확정되는지 확인한다. |
| COS-REGISTRATION-009 | 등록 후 세트 보유 효과 상태를 갱신한다. | 등록으로 세트 효과가 바뀌면 커밋 후 조회에 새 누적 총값이 보이고, 진행 중 전투의 snapshot은 유지되며 다음 cycle start input에 새 합계가 포함되는지 확인한다. |
| COS-REGISTRATION-010 | 조회가 등록 가능 상태를 제공한다. | collection 조회에 네 수량, cosmeticStar, 다음 starThreshold, neededCount, 두 mode의 executable이 포함되고 같은 서버 계산이 명령 검증에도 쓰이는지 확인한다. |
| COS-COLLECTION-001 | 모든 고유 등록 치장을 센다. | 서로 다른 A·B·C가 등록되고 A의 미등록 중복 재고가 있어도 uniqueRegisteredCount가 3인지 확인한다. |
| COS-COLLECTION-002 | 중복·성급은 고유 수를 늘리지 않는다. | A의 미등록 재고와 등록 수량을 늘려도 uniqueRegisteredCount가 최초 등록 후 1에서 증가하지 않는지 확인한다. |
| COS-COLLECTION-003 | 도감은 등록 현황과 세트 이동만 제공한다. | 활성 콘텐츠 기준으로 registeredQuantity, cosmeticStar, setId, slotId, owned/unowned 필터와 세트 이동 정보가 반환되고 도감 단계·도감 효과 필드가 없는지 확인한다. |
| COS-COLLECTION-004 | 도감은 단계 임계값을 요구하지 않는다. | collectionThresholds가 없는 활성 콘텐츠 버전이 도감 조회를 통과하고, 도감 단계 산출 없이 고유 등록 수와 완성률만 반환하는지 확인한다. |
| COS-COLLECTION-005 | 현재 수와 완성률을 반환한다. | 응답에 uniqueRegisteredCount와 totalCosmeticCount가 있고 completionRatio가 두 값에서 파생되며 별도 단계·필요 수·효과가 없는지 확인한다. |
| COS-COLLECTION-006 | 도감은 착용과 무관하지만 전투 효과를 제공하지 않는다. | 착용 상태가 같은 두 계정에서 도감 등록 수만 다르게 해도 다음 cycle start input의 세트 효과 합계가 세트 성급이 같은 한 동일한지 확인한다. |
| COS-COLLECTION-007 | 최신 버전으로 도감 표시 상태를 재계산한다. | 콘텐츠 버전 배포 후 모든 계정의 registeredQuantity를 기준으로 등록 현황, 완성률과 세트 이동 상태가 재계산되며 전투 효과 cache를 만들지 않는지 확인한다. |
| COS-EQUIPMENT-001 | 외형 슬롯은 여섯 부위만 허용한다. | PATCH `/api/v1/cosmetics/equipment/{slotId}`에 HEAD, TOP, BOTTOM, GLOVES, SHOES, CAPE는 통과하고 그 외 slotId는 `INVALID_EQUIPMENT_SLOT`과 변경 없음인지 확인한다. |
| COS-EQUIPMENT-002 | 착용·해제는 등록된 동일 부위 치장만 허용한다. | 등록된 동일 부위 치장은 장착되고 body `cosmeticId=null`은 해제되며 미등록·다른 부위는 각각 `COSMETIC_NOT_OWNED`·`COSMETIC_SLOT_MISMATCH`와 변경 없음인지 확인한다. |
| COS-EQUIPMENT-003 | 착용은 외형 전용이다. | 슬롯 변경·해제 전후 registeredQuantity, unregisteredQuantity, cosmeticStar, 세트 효과와 현재·다음 전투 보유 효과 input이 같은지 확인한다. |
| COS-SET-001 | 세트는 여섯 부위를 정확히 하나씩 가진다. | HEAD, TOP, BOTTOM, GLOVES, SHOES, CAPE가 각각 한 개씩 있는 구성만 통과하고 누락·중복·추가 부위는 거절되는지 확인한다. |
| COS-SET-002 | 치장 소속은 최대 하나다. | 세트 A 소속 치장을 세트 B에도 넣으려 하면 거절되고 A 소속이 유지되는지 확인한다. |
| COS-SET-003 | 출시 구성은 불변이다. | 출시 세트의 구성원 추가·삭제·교체가 모두 거절되고 여섯 부위 목록이 유지되는지 확인한다. |
| COS-SET-004 | 구성원 부위는 세트 슬롯과 일치한다. | HEAD 슬롯에 TOP 치장을 넣거나 같은 cosmeticId를 둘 이상의 슬롯에 넣은 버전은 `GACHA_CONTENT_UNAVAILABLE`이고 이전 상태가 유지되는지 확인한다. |
| COS-SET-005 | 세트 성급은 여섯 구성원 최솟값이다. | 여섯 구성원 성급 [5,3,4,2,5,4]에서 setStar가 2인지 확인한다. |
| COS-SET-006 | 미등록 구성원은 0성이다. | 다섯 구성원이 등록되고 하나가 미등록이면 미등록 구성원을 0성으로 처리해 setStar가 0인지 확인한다. |
| COS-SET-007 | 하나라도 미등록이면 효과가 없다. | 미등록 구성원이 있는 세트는 setStar=0·효과 빈 목록이며 어떤 stat도 변하지 않는지 확인한다. |
| COS-SET-008 | 1~5성 누적 총 효과 행을 콘텐츠에서 요구한다. | 세트 콘텐츠의 1~5성 누적 총 효과 행 중 하나라도 누락되면 검증 실패하고 모두 있으면 통과하는지 확인한다. |
| COS-SET-009 | 현재 성급 행이 적용 총값이다. | setStar=3일 때 적용 효과가 3성 행의 누적 총값과 정확히 같고 1·2성 행을 다시 더하지 않는지 확인한다. |
| COS-SET-010 | 완성 세트 효과를 스탯별 합산한다. | 완성 세트의 동일 stat 효과 a,b와 다른 stat 효과 c가 최종 합계에서 각각 a+b,c가 되는지 확인한다. |
| COS-SET-011 | 허용목록 외 stat ID를 거절한다. | 허용목록 외 stat ID가 있는 버전은 `GACHA_CONTENT_UNAVAILABLE`이고 이전 콘텐츠·계정 효과가 유지되는지 확인한다. |
| COS-SET-012 | 버전 변경·새 세트를 기존 계정에 재계산한다. | 콘텐츠 변경 후 registeredQuantity와 여섯 부위 구성을 기준으로 세트 성급·누적 총 효과가 재계산되고 행 값과 일치하는지 확인한다. |
| COS-MILESTONE-001 | 성공 수를 배너별 누적한다. | 배너 A 성공 3회와 B 성공 2회 후 각 totalSuccessfulDraws가 3과 2인지 확인한다. |
| COS-MILESTONE-002 | 결제 방식과 무관하게 센다. | 서로 다른 결제 방식의 성공 2회가 결제 방식별 분리 없이 totalSuccessfulDraws를 2 증가시키는지 확인한다. |
| COS-MILESTONE-003 | 누적을 초기화하지 않는다. | 199회 상태가 재접속·재시작 후 유지되고 성공 1회 뒤 200인지 확인한다. |
| COS-MILESTONE-004 | 청구 수는 floor(totalSuccessfulDraws / 200)-claimedBoxCount다. | totalSuccessfulDraws=650, claimedBoxCount=2에서 claimableBoxCount=1인지 확인한다. |
| COS-MILESTONE-005 | 총량과 수령량을 별도 보관한다. | 상자 수령 후 totalSuccessfulDraws는 유지되고 claimedBoxCount만 1 증가하는지 확인한다. |
| COS-MILESTONE-006 | 권리는 만료되지 않는다. | 시간 경과·재접속 후에도 미수령 권리가 감소하지 않는지 확인한다. |
| COS-MILESTONE-007 | 한 개 수령을 지원한다. | 청구 가능 3개 중 1개 수령 후 claimedQuantity=1이고 claimedBoxCount가 1 증가하는지 확인한다. |
| COS-MILESTONE-008 | 지정 수량 수령을 지원한다. | 청구 가능 5개 중 3개 수령 후 claimedQuantity=3이고 claimedBoxCount가 3 증가하는지 확인한다. |
| COS-MILESTONE-009 | 전체 수령을 지원한다. | 청구 가능 전부를 수령하면 claimableBoxCount가 0인지 확인한다. |
| COS-MILESTONE-010 | 수량을 검증하고 원자 차감한다. | 가능 2개에 3개를 요청하면 `INVALID_CLAIM_QUANTITY`와 변경 없음이고, 2개 경합은 한 요청만 성공해 최종 증가량이 2인지 확인한다. |
| COS-MILESTONE-011 | 수령은 성공 수를 늘리지 않는다. | totalSuccessfulDraws=400에서 수령 후에도 400이고 claimedBoxCount만 증가하는지 확인한다. |
| COS-BOX-001 | 상자는 stackable 200칸 아이템이다. | 선택 상자 201개가 한 스택으로 저장되고 공용 인벤토리 한 칸만 점유하는지 확인한다. |
| COS-BOX-002 | 동일 스택이 있으면 수량을 합친다. | 기존 150개에 30개를 지급해 새 슬롯 없이 180개가 되는지 확인한다. |
| COS-BOX-003 | 없을 때만 슬롯 하나를 쓴다. | 기존 스택이 없을 때 상자 1개가 정확히 한 슬롯을 쓰는지 확인한다. |
| COS-BOX-004 | 수령·카운터·지급을 원자 처리한다. | 카운터 또는 지급 저장 실패 시 상자 수량·카운터·기록이 처리 전과 같은지 확인한다. |
| COS-BOX-005 | 한 번에 상자 하나와 치장 하나를 교환한다. | 상자 2개에서 하나를 열면 상자 1개가 감소하고, 미보유 선택 치장은 registeredQuantity=1·cosmeticStar=1, 등록 치장은 unregisteredQuantity +1인지 확인한다. |
| COS-BOX-006 | 활성 콘텐츠의 전설 치장만 선택 가능하다. | 상자를 지급한 배너와 다른 전설 세트 구성원도 선택할 수 있고, 전설이 아니거나 존재하지 않는 치장 선택은 지급·상자 차감 모두 없는지 확인한다. |
| COS-BOX-007 | 선택 보상은 일반 지급과 같은 의미다. | 선택 보상이 신규 자동 등록·1성 또는 중복 미등록 재고 증가로 처리되는지 확인한다. |
| COS-BOX-008 | 사용은 뽑기 수를 늘리지 않는다. | 상자 사용 전후 drawCount가 같은지 확인한다. |
| COS-BOX-009 | 새 스택 슬롯 부족 시 변경 없이 거절한다. | 새 스택 슬롯이 없으면 요청 거절과 슬롯·수량 변경 없음을 확인한다. |
| COS-COMBAT-001 | stat ID를 허용목록으로 제한한다. | 허용목록 외 stat ID는 거부되고 저장·전투 합계에 포함되지 않는지 확인한다. |
| COS-COMBAT-002 | MVP 치장 stat ID는 전투 SSOT 허용목록을 따른다. | 활성 콘텐츠의 세트 효과 stat ID가 `attackPercent`, `maxHpPercent`, `defensePenetrationPercent`, `basicAttackDamagePercent`, `criticalChancePoint`, `attackSpeedPercent`, `buffDurationSeconds` 중 하나인지 확인한다. |
| COS-COMBAT-003 | 세트 보유 효과 합계를 전투에 전달한다. | 완성 세트의 현재 성급 누적 총 효과가 새 cycle start input에서 stat별 합으로 전달되고 도감 등록 수만으로 효과가 추가되지 않는지 확인한다. |
| COS-COMBAT-004 | 획득·등록·버전 변경은 다음 사이클 입력을 갱신한다. | 현재 전투 중 변경은 현재 cycle snapshot을 유지하고 다음 cycle start input에 새 보유 효과 합계가 반영되는지 확인한다. |
| COS-COMBAT-005 | 공격·관통은 사이클 시작 입력 합계를 쓴다. | cycle start input의 치장 비율 효과가 반영된 attack와 defensePenetration 합계가 각 피해·관통 계산에 사용되는지 확인한다. |
| COS-COMBAT-006 | 진행 중 최대HP와 현재HP를 조정하지 않는다. | 현재 사이클 중 maxHpPercent 효과가 변해도 snapshot의 maxHp와 currentHp가 요청 전 값인지 확인한다. |
| COS-COMBAT-007 | 다음 사이클 최대HP는 새 입력 합계를 쓴다. | 다음 사이클 maxHp가 새 세트 효과 합계를 반영하고 이전 snapshot을 재사용하지 않는지 확인한다. |
| COS-COMBAT-008 | 효과 변경은 현재 생존 상태를 바꾸지 않는다. | 현재 사이클에서 효과 변경이 currentHp와 alive/dead 판정을 바꾸지 않고 다음 사이클 입력에서만 새 규칙이 적용되는지 확인한다. |
| COS-AUDIT-001 | 콘텐츠 버전과 명령 종류를 기록한다. | draw·claim·box open·registration·equipment patch 성공 시 operationType과 contentVersion이 실제 실행과 일치하는지 확인한다. |
| COS-AUDIT-002 | 뽑기 결과 순서를 기록한다. | draw audit의 resultOrder가 결과와 같고 다른 네 명령 audit에는 resultOrder가 없는지 확인한다. |
| COS-AUDIT-003 | 명령별 소비·지급을 기록한다. | draw는 ticketSpend·riceSpend·result IDs, claim은 claimedQuantity·boxItemId, box open은 consumed boxItemId·selected cosmeticId, registration은 mode·consumedUnregisteredQuantity, equipment patch는 slotId·previousCosmeticId·nextCosmeticId를 기록하는지 확인한다. |
| COS-AUDIT-004 | 전후 상태·카운터를 기록한다. | 각 명령 audit의 before/after가 실제 잔액·재고·카운터·성급·슬롯 전후와 일치하는지 확인한다. |
| COS-AUDIT-005 | 재현 토큰은 서버만 발급하고 노출하지 않는다. | 클라이언트 replayToken은 무시·거절되고 서버 내부 auditReproductionToken만 저장되며 응답에 노출되지 않는지 확인한다. |
| COS-AUDIT-006 | 보존 기간은 운영에 위임한다. | 요구사항·API에 고정 retentionDays가 없고 다섯 명령 audit이 운영 보존 정책 식별자를 참조하는지 확인한다. |
| COS-CONTENT-001 | 확률 합계는 1,000,000이다. | 합계 1,000,000 버전은 활성화되고 999,999 버전은 `GACHA_CONTENT_UNAVAILABLE`과 변경 없음인지 확인한다. |
| COS-CONTENT-002 | 양수 확률 등급은 비어 있지 않다. | probability>0인 각 grade에 치장이 하나 이상이고 빈 pool 버전은 거절되는지 확인한다. |
| COS-CONTENT-003 | 하위 풀·비용·전설 확률이 배너 간 같다. | 배너 간 하위 pool, singleRiceCost, LEGENDARY probability가 각각 동일한지 검증한다. |
| COS-CONTENT-004 | 전설 세트·배너 매핑은 완전한 1:1이고 선택 상자 식별자는 유효하다. | 각 전설 세트와 배너가 정확히 하나씩 대응하고 모든 배너가 유효한 선택 상자 item ID를 참조하며 누락·중복 버전은 거절되는지 확인한다. |
| COS-CONTENT-005 | 모든 치장에는 한 부위가 있다. | 각 cosmeticId의 slotId가 여섯 허용 부위 중 정확히 하나이고 누락·복수·허용 외 값은 거절되는지 확인한다. |
| COS-CONTENT-006 | 각 세트는 여섯 부위를 완전하게 채운다. | 각 setId에 HEAD, TOP, BOTTOM, GLOVES, SHOES, CAPE 구성원이 정확히 하나씩 있고 구성원 부위도 일치하는지 확인한다. |
| COS-CONTENT-007 | 성급 임계값은 누적 등록 수량이다. | starThresholds가 1~5성 값을 모두 갖고 1성은 1이며 인접 값이 엄격히 증가하는지 확인한다. |
| COS-CONTENT-008 | 도감 단계 콘텐츠는 요구하지 않는다. | collection tier·collection effect 행이 없어도 버전 검증이 통과하고, 해당 행이 있어도 전투 효과 입력으로 사용하지 않는지 확인한다. |
| COS-CONTENT-009 | 세트 성급 행은 누적 총 효과다. | 각 setId의 1~5성 totalValue 행이 모두 존재하고 누락·소수·허용 외 stat은 거절되는지 확인한다. |
| COS-CONTENT-010 | 승인 상태 버전만 실행한다. | 승인 버전만 상세·draw·collection을 수행하고 초안은 `GACHA_CONTENT_UNAVAILABLE`과 변경 없음인지 확인한다. |
| COS-CONTENT-011 | 무효 버전은 추첨·공개·계산을 차단한다. | 확률·여섯 부위 세트·starThresholds 중 하나가 무효인 버전에서 상세·draw·collection 모두 거절되고 공개·보유·set/combat cache가 유지되는지 확인한다. |
| COS-CONTENT-012 | 누락값을 자동 보정하지 않는다. | 비용·확률·메타데이터·slotId·starThresholds 중 하나가 누락되면 기본값을 만들지 않고 거절하는지 확인한다. |

## 7. API 요구사항
| ID | Method·Path | Purpose | 연결 요구사항 |
| --- | --- | --- | --- |
| COS-API-001 | GET /api/v1/cosmetic-gacha/banners | 배너·재화·1/10회 예상 결제·누적·미수령 조회 | BANNER, PAY, MILESTONE |
| COS-API-002 | GET /api/v1/cosmetic-gacha/banners/{bannerId} | appearance pool, grade probabilities, cosmetic별 authoritative numerator·denominator·display string, 콘텐츠 버전 조회 | BANNER, CONTENT |
| COS-API-003 | POST /api/v1/cosmetic-gacha/banners/{bannerId}/draws | count만으로 원자 뽑기 실행 | PAY, DRAW, OWN, MILESTONE, AUDIT |
| COS-API-004 | POST /api/v1/cosmetic-gacha/banners/{bannerId}/milestone-claims | 1개·지정·전체 상자 수령 | MILESTONE, BOX |
| COS-API-005 | POST /api/v1/cosmetic-selector-boxes/{boxItemId}/open | 상자 하나와 치장 하나 교환 | BOX, OWN, AUDIT |
| COS-API-006 | GET /api/v1/cosmetics/collection | 등록·미등록·예약 재고, 성급 임계값, 도감 등록 현황·세트 효과·착용 슬롯 조회 | OWN, REGISTRATION, COLLECTION, SET, COMBAT, EQUIPMENT |
| COS-API-007 | POST /api/v1/cosmetics/{cosmeticId}/registrations | body mode=ONE 또는 UNTIL_NEXT_STAR로 원자 등록 | REGISTRATION, COLLECTION, SET, COMBAT, AUDIT |
| COS-API-008 | PATCH /api/v1/cosmetics/equipment/{slotId} | slotId=HEAD/TOP/BOTTOM/GLOVES/SHOES/CAPE, body cosmeticId 또는 null로 외형 착용·해제 | EQUIPMENT, AUDIT |

draw 입력은 paymentType, user ID, probability, result, reward list를 받지 않는다. registration 입력은 mode 외 목표 성급·수량·효과 값을 받지 않는다. equipment 입력은 cosmeticId 또는 null 외 보유·효과 값을 받지 않는다. 별도 결과등록·collection-add·set-refresh·registration-preview·gacha-history API는 없다.

## 8. 데이터 요구사항과 불변 조건
서버 소유 최소 레코드는 catalog/version, cosmetic slot, starThresholds, set/banner definition/version, 계정 registeredQuantity·unregisteredQuantity·reservedQuantity·cosmeticStar·equippedBySlot·ticket balance, 배너별 성공·수령 카운터, selector-box stack, idempotency 결과, draw/claim/box/registration/equipment audit, 재구축 가능한 collection-display/set/combat-effect cache다. 물리 테이블은 규정하지 않는다.

## 9. 원자성·멱등성·동시성
해금은 클리어·해금·10장을, draw는 잠금·결제·추첨·지급·상태·카운터·감사·멱등 결과를, registration은 미등록 재고 소비·누적 등록 수량·성급·도감 표시 상태·세트·다음 전투 사이클 입력·감사를, equipment patch는 슬롯 검증·외형 슬롯·감사를, claim은 권리 검증·차감·상자를, box open은 상자 차감·지급·슬롯·감사를 함께 커밋한다. 같은 키 재시도는 저장 결과를 반환하고 payload 변경은 거절한다. 계정 상태, 재화, 미등록 재고와 거래 예약을 잠가 직렬화한다.

## 10. 오류 처리 요구사항
모든 검증 실패는 state change: none이며 HTTP 상태·공통 envelope는 networking 문서 소유다.

| 코드 | 안정적 의미 |
| --- | --- |
| AUTHENTICATION_REQUIRED | 인증 정보가 없거나 유효하지 않음; 변경 없음 |
| COSMETIC_SYSTEM_LOCKED | 1-5 최초 클리어 전 시스템 잠금; 변경 없음 |
| GACHA_BANNER_NOT_FOUND | 요청한 배너 식별자가 없음; 변경 없음 |
| GACHA_CONTENT_UNAVAILABLE | 승인된 활성 콘텐츠 버전이 없거나 유효성 실패; 변경 없음 |
| INVALID_DRAW_COUNT | count가 1 또는 10이 아님; 변경 없음 |
| INSUFFICIENT_GACHA_FUNDS | 티켓과 쌀을 합쳐 요청 횟수를 지불할 수 없음; 변경 없음 |
| IDEMPOTENCY_KEY_REUSED | 키가 다른 요청 payload에 재사용됨; 변경 없음 |
| COSMETIC_NOT_FOUND | 치장 식별자가 없음; 변경 없음 |
| COSMETIC_NOT_OWNED | 계정이 해당 치장을 등록하지 않았거나 사용할 권한이 없음; 변경 없음 |
| INVALID_REGISTRATION_MODE | registration mode가 ONE 또는 UNTIL_NEXT_STAR가 아님; 변경 없음 |
| COSMETIC_ALREADY_MAX_STAR | 이미 5성이라 다음 성급 등록이 불가능함; 변경 없음 |
| INSUFFICIENT_UNREGISTERED_COSMETICS | 예약을 제외한 미등록 중복 재고가 등록 필요 수량보다 부족함; 변경 없음 |
| INVALID_EQUIPMENT_SLOT | 외형 슬롯이 HEAD/TOP/BOTTOM/GLOVES/SHOES/CAPE 중 하나가 아님; 변경 없음 |
| COSMETIC_SLOT_MISMATCH | 요청 슬롯과 치장 부위가 다름; 변경 없음 |
| NO_CLAIMABLE_SELECTOR_BOX | 수령 가능한 선택 상자가 없음; 변경 없음 |
| INVALID_CLAIM_QUANTITY | 상자 수령 수량이 허용 범위를 벗어남; 변경 없음 |
| INVENTORY_CAPACITY_EXCEEDED | 새 선택 상자 스택을 위한 슬롯 부족; 변경 없음 |
| SELECTOR_BOX_NOT_FOUND | 요청한 상자 item 식별자가 없음; 변경 없음 |
| SELECTOR_BOX_NOT_OWNED | 계정이 해당 선택 상자를 보유하지 않음; 변경 없음 |
| INVALID_SELECTOR_COSMETIC | 선택 치장이 활성 콘텐츠의 전설 치장이 아님; 변경 없음 |
| COSMETIC_STATE_CONFLICT | 동시 변경으로 계정 치장 상태 조건이 충돌함; 변경 없음 |
| COSMETIC_SERVICE_UNAVAILABLE | 치장 서비스가 처리 불능 상태임; 변경 없음 |

## 11. 책임 경계
SSOT는 규칙, 콘텐츠는 미승인 수치·카탈로그, 본 문서는 구현 계약·검증을 소유한다. 인증·지갑·일반 인벤토리·전투 계산·HTTP 공통 계약은 각 정본에 위임한다.

## 12. 검증 기준
- 해금·10장 일회성, 티켓-only·쌀-only·혼합 1/10회와 부족 원자성을 검증한다.
- 순서 보존 신규/중복, 최초 자동 등록·1성, 중복 미등록 재고, 예약 제외 등록, ONE/UNTIL_NEXT_STAR mode, 200/400회 청구·영구 권리, 상자 슬롯·보상을 검증한다.
- 여섯 부위 세트, 누적 등록 임계값, 도감·세트 누적 총 효과, 착용 외형 전용, 응답 유실 재시도, 동시 뽑기·쌀·재고 경합, 무효 콘텐츠, 보유 효과의 다음 메인 전투 사이클 적용을 검증한다.

## 13. 추적성 매트릭스
| 그룹 | 규칙 | 콘텐츠 | API | 검증 |
| --- | --- | --- | --- | --- |
| COS-UNLOCK | [SSOT](../../30-domain/cosmetics/ssot.md) | [MVP v1](../../60-content/cosmetics/mvp-v1.md) | 외부 1-5 최초 클리어 이벤트; COS-API-001, COS-API-002, COS-API-006 조회 엔드포인트 | 해당 ID 행·§12 |
| COS-BANNER | [SSOT](../../30-domain/cosmetics/ssot.md) | [MVP v1](../../60-content/cosmetics/mvp-v1.md) | COS-API-001, COS-API-002, COS-API-003 | 해당 ID 행·§12 |
| COS-PAY | [SSOT](../../30-domain/cosmetics/ssot.md) | [MVP v1](../../60-content/cosmetics/mvp-v1.md) | COS-API-001, COS-API-003 | 해당 ID 행·§12 |
| COS-DRAW | [SSOT](../../30-domain/cosmetics/ssot.md) | [MVP v1](../../60-content/cosmetics/mvp-v1.md) | COS-API-002, COS-API-003 | 해당 ID 행·§12 |
| COS-OWN | [SSOT](../../30-domain/cosmetics/ssot.md) | [MVP v1](../../60-content/cosmetics/mvp-v1.md) | COS-API-003, COS-API-005, COS-API-006 | 해당 ID 행·§12 |
| COS-REGISTRATION | [SSOT](../../30-domain/cosmetics/ssot.md) | [MVP v1](../../60-content/cosmetics/mvp-v1.md) | COS-API-006, COS-API-007 | 해당 ID 행·§12 |
| COS-COLLECTION | [SSOT](../../30-domain/cosmetics/ssot.md) | [MVP v1](../../60-content/cosmetics/mvp-v1.md) | COS-API-006, COS-API-007 | 해당 ID 행·§12 |
| COS-EQUIPMENT | [SSOT](../../30-domain/cosmetics/ssot.md) | [MVP v1](../../60-content/cosmetics/mvp-v1.md) | COS-API-006, COS-API-008 | 해당 ID 행·§12 |
| COS-SET | [SSOT](../../30-domain/cosmetics/ssot.md) | [MVP v1](../../60-content/cosmetics/mvp-v1.md) | COS-API-006, COS-API-007 | 해당 ID 행·§12 |
| COS-MILESTONE | [SSOT](../../30-domain/cosmetics/ssot.md) | [MVP v1](../../60-content/cosmetics/mvp-v1.md) | COS-API-001, COS-API-003, COS-API-004 | 해당 ID 행·§12 |
| COS-BOX | [SSOT](../../30-domain/cosmetics/ssot.md) | [MVP v1](../../60-content/cosmetics/mvp-v1.md) | COS-API-004, COS-API-005 | 해당 ID 행·§12 |
| COS-COMBAT | [SSOT](../../30-domain/cosmetics/ssot.md) | [MVP v1](../../60-content/cosmetics/mvp-v1.md) | COS-API-003, COS-API-005, COS-API-006, COS-API-007 | 해당 ID 행·§12 |
| COS-AUDIT | [SSOT](../../30-domain/cosmetics/ssot.md) | [MVP v1](../../60-content/cosmetics/mvp-v1.md) | COS-API-003, COS-API-004, COS-API-005, COS-API-007, COS-API-008 | 해당 ID 행·§12 |
| COS-CONTENT | [SSOT](../../30-domain/cosmetics/ssot.md) | [MVP v1](../../60-content/cosmetics/mvp-v1.md) | COS-API-001, COS-API-002, COS-API-003, COS-API-006, COS-API-007, COS-API-008 | 해당 ID 행·§12 |

승인 설계 §5–20과 통합 계획 확정 결정은 각각 UNLOCK, BANNER/DRAW/CONTENT, PAY, OWN/REGISTRATION, COLLECTION, EQUIPMENT, SET, COMBAT, MILESTONE/BOX, §9/AUDIT, API §7, 오류 §10, 데이터 §8, 검증 §12, 추적성 §13, 경계 §11, CONTENT로 추적된다.

## 14. 승인 구현 입력과 후속 콘텐츠
[MVP 콘텐츠 v2 임시 카탈로그](../../60-content/cosmetics/mvp-v1.md)의 66개 번호형 ID, 임시 11세트·6부위 배치, 1회 5,000쌀, 등급 확률, 등급별 성급 임계값과 기본 세트 효과를 구현 입력으로 사용한다. 실제 명칭·이미지와 전설 고유 효과는 후속 콘텐츠 입력이며 현재 번호형·이미지 준비 중 fallback을 영구 콘텐츠로 해석하지 않는다. 런타임은 기존 30종 개발 초안에서 clean cutover한다.
