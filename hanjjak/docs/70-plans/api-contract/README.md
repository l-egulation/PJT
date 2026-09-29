---
doc_kind: requirements
owner_domain: architecture
authority_level: applied
status: confirmed
approved_at: 2026-09-01
---

# MVP API 계약

공통 전송·인증 규칙은 [Networking SSOT](../../50-architecture/networking.md)가 소유한다. 이 문서는 MVP API의 공통 envelope와 현재 확정된 인증 API 계약을 소유하며, 도메인 규칙은 각 도메인 SSOT를 따른다.

## 공통 성공 응답

```json
{
  "requestId": "550e8400-e29b-41d4-a716-446655440000",
  "serverTime": "2026-09-01T12:34:56.789Z",
  "stateVersion": 42,
  "data": {}
}
```

`data`는 JSON object, array 또는 `null`이다. 응답 필드는 고정하고 값 없음은 `null`, 목록 없음은 `[]`으로 표현한다. `stateVersion`은 계정 서버 권한 상태 전체의 단조 증가 버전이며 조회는 읽은 최신 버전을 반환한다.

## Mutation command 응답

동기 처리 성공은 HTTP 200과 다음 필드를 반환한다.

```json
{
  "requestId": "550e8400-e29b-41d4-a716-446655440000",
  "serverTime": "2026-09-01T12:34:56.789Z",
  "stateVersion": 43,
  "data": {
    "commandId": "6ba7b810-9dad-41d1-80b4-00c04fd430c8",
    "idempotencyKey": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
    "status": "SUCCEEDED",
    "result": {}
  }
}
```

10초 안에 확정되지 않는 command는 HTTP 202를 반환하고 `commandId`를 제공한다. 최종 상태는 `GET /api/v1/commands/{commandId}`로 조회한다. command 상태는 `SUCCEEDED`, `FAILED`, `PROCESSING`, `NOT_FOUND`다.

## 오류 응답

```json
{
  "requestId": "550e8400-e29b-41d4-a716-446655440000",
  "serverTime": "2026-09-01T12:34:56.789Z",
  "code": "VALIDATION_FAILED",
  "messageKey": "error.validation.failed",
  "retryable": false,
  "details": [
    {
      "field": "quantity",
      "code": "OUT_OF_RANGE",
      "messageKey": "error.quantity.outOfRange",
      "value": {"min": 1, "max": 10}
    }
  ]
}
```

`details`는 `null` 또는 오류 목록이다. 비밀번호, 세션, 전체 요청 본문, 소유 자산 목록과 대형 값은 포함하지 않는다. 사용자 문구는 `messageKey`를 통해 클라이언트 localization이 결정한다.

## 공통 요청 규칙

- 모든 API는 `/api/v1/...` 경로와 JSON을 사용한다.
- 상태 변경 요청은 `Content-Type: application/json`, `Accept: application/json`을 요구한다.
- 모든 mutation은 `Idempotency-Key` 헤더에 canonical UUIDv4를 필수로 요구한다.
- `X-Request-Id`, `X-Correlation-Id`는 선택적 canonical UUIDv4다. 누락·형식 오류는 서버 생성으로 처리한다.
- 같은 멱등성 키와 같은 payload는 동일 결과를 반환한다. 다른 payload는 HTTP 409 `IDEMPOTENCY_KEY_REUSED`로 거부한다.
- 멱등성 결과는 최소 7일 보존한다.

## 인증 API

### `POST /api/v1/auth/signup`

회원가입 후 인증 세션을 생성한다. 요청 body는 이메일, 비밀번호, 닉네임이다. 이메일은 중복될 수 없고 비밀번호는 최소 8자이며 복잡도 조합은 강제하지 않는다. 닉네임은 공백 제거 후 1~20자로 저장한다. 성공 시 세션 쿠키와 `accountId`, `characterId`, `email`, `nickname`, `level`, `experience`, `rice` 계정 식별 결과를 반환한다.

### `POST /api/v1/auth/login`

이메일·비밀번호로 로그인하고 기존 로그인 세션과 실행 세션을 대체한다. 로그인 실패와 rate limit 초과는 계정 존재 여부를 노출하지 않는 일반 로그인 실패로 반환한다.

### `GET /api/v1/auth/session`

현재 세션의 인증 상태와 계정 식별·캐릭터 기본 상태를 조회한다. 인증된 세션은 `accountId`, `characterId`, `email`, `nickname`, `level`, `experience`, `rice`를 반환한다. 조회이므로 멱등성 키가 필요하지 않다.

### `POST /api/v1/auth/profile/nickname`

인증된 사용자의 캐릭터 닉네임을 수정한다. body는 `nickname`이고 공백 제거 후 1~20자를 허용한다. 성공 시 갱신된 계정 식별·캐릭터 기본 상태를 반환하며, 상태 변경 요청이므로 `Idempotency-Key`가 필요하다.

### `POST /api/v1/auth/profile/password`

인증된 사용자가 현재 비밀번호를 다시 검증한 뒤 새 비밀번호로 변경한다. body는 `currentPassword`, `newPassword`, `newPasswordConfirmation`이며 새 비밀번호는 최소 8자이고 확인 값과 일치해야 한다. 서버는 BCrypt 해시와 `stateVersion`을 한 트랜잭션에서 갱신하고, 성공 즉시 해당 계정의 로그인 세션·실행 세션·진행 중 전투를 종료해 재로그인을 요구한다. 같은 `Idempotency-Key` 재시도는 비밀번호와 상태 버전을 다시 갱신하지 않는다.

### `POST /api/v1/auth/logout`

현재 인증 세션과 게임 세션을 닫고 진행 중 전투를 즉시 중단한다. 오프라인 작업은 종료하지 않으며 마지막 heartbeat 이후 오프라인 보상 규칙에 따라 서버가 `CLAIMABLE`로 고정한다.

### 소셜 인증 API

- `GET /api/v1/auth/social/providers`: 공급자별 독립 활성화 상태를 조회한다.
- `POST /api/v1/auth/social/{provider}/begin`: 서버가 state·nonce·PKCE S256 값을 생성해 Authorization Code 인증 URL을 반환한다.
- `GET /api/v1/auth/social/{provider}/callback`: 서버가 callback 상태와 공급자 응답을 검증하고, 기존 외부 식별자는 로그인하며 신규 식별자는 닉네임 확정 대기 상태로 전환한다.
- `GET /api/v1/auth/social/pending`, `POST /api/v1/auth/social/signup`: 신규 소셜 사용자가 공급자 인증 결과를 확인하고 닉네임 확정 뒤 계정·캐릭터를 생성한다.
- `POST /api/v1/auth/social/{provider}/link/begin`, `GET /api/v1/auth/social/connections`, `DELETE /api/v1/auth/social/connections/{provider}`: 인증 계정의 공급자 연결·조회·해제를 처리하고 마지막 로그인 수단을 보호한다.

외부 식별자는 `(provider, providerSubject)`를 사용하고 공급자 이메일은 자동 병합 키가 아니다. Kakao는 비즈니스 앱 개인 정보 권한 없이 `openid`만 요청하고 검증된 `sub`로 식별하며, 서비스 닉네임은 후속 확정 화면에서 받는다. 공급자 access·refresh·ID token은 장기 저장하지 않는다.

## 스테이지·전투 API

세부 도메인 규칙은 [스테이지 SSOT](../../30-domain/world/ssot.md), [전투 SSOT](../../30-domain/combat/ssot.md), 성장 보상은 [성장 SSOT](../../30-domain/progression/ssot.md), 아이템 보상은 [아이템 SSOT](../../30-domain/items/ssot.md)를 따른다. 기준 메인 전투는 서버 전투 세션을 사용한다. 즉시 판정형 `/cycles`와 챕터 `/auto-run`은 레거시 호환 경로이며 운영 기본값에서 비활성화된다.

### `GET /api/v1/stages`

인증된 사용자의 스테이지 목록을 조회한다. 응답 항목은 `stageId`, `unlocked`, `clearCount`, `contentVersion`이다. 1-1은 기본 해금이고, 이후 스테이지는 직전 스테이지 최초 클리어 여부로 해금된다.

### `POST /api/v1/battles/sessions`

활성 게임 실행 세션 아래 단일 전투 사이클을 시작한다. 서버는 스테이지 접근을 검증하고 seed, logical tick 기준, 완료 가능 시각과 전체 전투 입력 스냅샷을 발급한다.

### `POST /api/v1/battles/sessions/{battleSessionId}/heartbeat`

30초 진행 신호다. 활성 게임 실행 세션과 전투 토큰을 다시 검증하고, 90초 무신호 세션은 만료한다.

### `POST /api/v1/battles/sessions/{battleSessionId}/settlements`

일반 몬스터 처치 직후 마지막 처치 순번과 렌더링 체크포인트를 전송한다. 서버는 저장된 입력·seed·처치 가능 시각을 검증해 아직 정산하지 않은 처치를 순서대로 확정하고, 처치별 아이템 지급 결과와 경험치·레벨·쌀 상태를 반환한다. 같은 세션과 적 순번의 재전송은 저장 결과에 수렴한다.

### `POST /api/v1/battles/sessions/{battleSessionId}/complete`

예측 hash와 렌더링 체크포인트만 받는다. 서버는 저장된 입력·seed로 결과를 재계산하고 누락 일반 처치·보스 보상·클리어·다음 스테이지를 원자 확정한다. 같은 멱등 키 재전송은 저장 결과를 반환한다.

### `POST /api/v1/battles/sessions/{battleSessionId}/abort`

선택적 렌더링 체크포인트까지 서버가 확인한 누락 처치를 정산한 뒤 진행 중 사이클을 종료한다. 네트워크 단절·pagehide·스테이지 전환에서 best-effort로 사용한다.

### 레거시 즉시 보상 경로

`POST /api/v1/battles/cycles`와 `POST /api/v1/battles/chapters/{chapter}/auto-run`은 운영에서 비활성화한다. 요청은 상태를 변경하지 않고 `BATTLE_COMPATIBILITY_API_DISABLED` 오류를 반환하며, 서버 로그에는 계정·경로·원격 주소를 기록한다. 개발·회귀 검증에서 필요한 경우에만 `BATTLE_COMPATIBILITY_API_ENABLED=true`를 명시한다.

## 장비 API

세부 도메인 규칙은 [장비 SSOT](../../30-domain/items/equipment/ssot.md), 전투 반영은 [전투 SSOT](../../30-domain/combat/ssot.md)를 따른다. 현재 API는 계정×부위 영구 장비 상태와 최초 해금·강화·승급 명령을 제공한다. 비용·보유량·현재/다음 Q와 능력치·`statIncrease`·실행 가능 여부·비활성 사유는 서버가 계산한다.

### `GET /api/v1/equipment`

인증된 사용자의 6부위 상태와 보유 쌀을 조회한다. 각 부위는 해금 여부, 현재 성장 상태, 가능한 최초 해금·강화·승급 행동, 비용·결과 미리보기와 비활성 사유를 포함한다.

### `POST /api/v1/equipment/{slot}/unlock`

미해금 부위를 최초 제작해 영구 해금하는 command다. 서버는 공통 비용을 원자적으로 차감하고 노말 +1 상태를 만든다. 이미 해금한 부위는 거절한다.

### `POST /api/v1/equipment/{slot}/enhance`

영구 부위 상태의 강화 단계를 하나 올리는 command다. 서버는 해금, 30강 상한과 응답에 제시한 비용 보유량을 검증하고 비용 차감과 상태 변경을 원자적으로 확정한다.

### `POST /api/v1/equipment/{slot}/promote`

현재 등급 30강과 요구 챕터 마지막 스테이지 최초 클리어를 검증하고 다음 등급 +1로 승급하는 command다. 전설 등급 이후 승급은 없다.

모든 장비 command는 UUID `Idempotency-Key`와 빈 JSON body를 사용한다. 같은 키의 동일 명령은 저장 결과를 반환하고 다른 명령 재사용은 거절한다.

## 스킬 API

세부 도메인 규칙은 [스킬 SSOT](../../30-domain/character/skills/ssot.md)를 따른다. 현재 API는 노말 등급 해금·1~10강, 실패 보정, 액티브 자동 사용 순서 저장, 전투 `SkillProfile` 반영을 제공한다.

### `GET /api/v1/skills`

인증된 사용자의 6종 스킬 상태, 보유 노말 스킬북 수량, 다음 비용·성공률, 액티브 장착 순서와 보유 쌀을 조회한다.

### `POST /api/v1/skills/{skillId}/enhance`

스킬 해금 또는 강화 command다. 해금은 노말 스킬북과 쌀을 소비해 노말 1강을 확정 적용한다. 2~10강은 노말 스킬북과 쌀을 소비하고 단계별 성공률·실패 보정을 원자적으로 적용한다.

### `PUT /api/v1/skills/loadout`

해금한 액티브 스킬 최대 4개의 자동 사용 순서를 저장한다. 서버는 중복, 미해금, 패시브 장착을 거부한다.

## 보석 API

세부 도메인 규칙은 [보석 SSOT](../../30-domain/gems/ssot.md), 보석 수치는 [보석 콘텐츠 v1](../../60-content/gems/mvp-v1.md)을 따른다. 구현·검증 상태는 [J-14](../../wiki/06-delivery/tasks/J-delivery-operations/j-14-gems-mvp-vertical-slice.md)에서 관리한다.

### `GET /api/v1/gems`

인증된 사용자의 보석 해금 여부, 입장권, 오늘 보스, 보석함 수량, 보유 보석, 프리셋별 장착 상태를 조회한다.

### `GET /api/v1/gem-dungeons/today`

오늘의 보스, 입장권, 보스별 최고 단계, 다음 신규 도전 단계, 소탕 가능 단계와 활성 도전을 조회한다.

### `POST /api/v1/gem-dungeons/challenges`

신규 단계 도전을 시작한다. 서버가 프리셋·캐릭터·스킬·콘텐츠 버전을 스냅샷하고 결정론 전투 결과와 순서화 사건 로그, `minimumCompleteAt`, `expiresAt`을 반환한다.

### `POST /api/v1/gem-dungeons/challenges/{challengeId}/complete`

저장된 서버 결과를 확정한다. `minimumCompleteAt` 전 요청은 거부하고, 성공 결과에만 입장권 차감·보석함 지급·최고 단계 및 최초 클리어 기록·수용량 예약 소비를 원자 적용한다.

### `POST /api/v1/gem-dungeons/challenges/{challengeId}/abort`

활성 도전을 미차감·미보상으로 중단하고 보상 수용량 예약을 해제한다.

### `POST /api/v1/gem-dungeons/sweeps`

오늘 보스의 최고 클리어 단계를 `count`회 소탕하고 입장권 차감과 보석함 보상을 원자 적용한다. `count`는 1~3이다.

### `POST /api/v1/gems/boxes/open`

보석함 개봉 command다. body는 `quantity`다. 보석함을 소비하고 결과 레벨·옵션을 멱등성 키 기반 seed로 추첨해 보석 인스턴스를 생성한다.

### `PUT /api/v1/gems/presets/{preset}`

메인·생존형·폭주형·장갑형 중 지정한 보석 프리셋에 최대 6개를 저장한다. 활성 보석 던전의 보스 프리셋은 변경을 거부하고, 메인 프리셋 변경은 진행 중 메인 전투를 종료해 현재 스테이지를 0/20부터 재시작한다.

### `POST /api/v1/gem-fusions/preview`

수동 3개 또는 같은 레벨의 옵션별 안전 일괄 소비 수량을 검증하고, 실제 소비할 보석·옵션별 수량·예상 결과 수를 반환한다. 상태는 변경하지 않는다.

### `POST /api/v1/gem-fusions`

미리 확인한 미장착·미잠금·미판매 보석 ID를 원자적으로 소비하고 3개당 다음 레벨 보석 1개를 독립 추첨해 지급한다.

## 거래소 API

거래 규칙은 [거래소 SSOT](../../30-domain/economy/ssot.md), 적용할 경로·멱등성·revision·오류 경계는 [거래소·주문장 계약](../../50-architecture/networking.md#거래소주문장-계약)이 소유한다. 이 절은 2026-09-11 호가창 결정의 구현 대상 DTO를 연결하며, 현재 TypeSpec·서버·웹이 이미 전환됐다는 증거가 아니다.

### 시장 품목과 주문장

- `GET /api/v1/market/instruments`는 시장 품목 UUID, canonical key, 표시 정보, 활성/취소 처리 상태, 최고 매수·최저 매도·최근 체결과 `marketRevision`을 반환한다. 식별자 생성·직렬화는 [거래소 주문장 아키텍처 결정](../../80-decisions/architecture/market-order-book.md)을 따른다.
- `GET /api/v1/market/order-books/{instrumentId}?levels=5`는 양쪽 5개 가격대의 가격·총잔량·누적 잔량·내 주문 수량, 스프레드와 최근 체결 10건을 반환한다.
- `GET /api/v1/market/instruments/{instrumentId}/order-quote`는 `side`, `timeInForce`, `quantity`, `limitUnitPrice` 기준 예상 체결 수량·미체결 수량·가격 범위·가중평균 단가·총액·수수료·순정산액과 사용 가능 자산을 반환한다. 조회는 가격·수량을 예약하지 않는다.

### 주문 command와 내 주문

- `POST /api/v1/market/orders`는 `instrumentId`, `BUY|SELL`, `quantity`, `limitUnitPrice`, `IOC|GTC`를 받아 즉시 체결과 대기 잔량을 하나의 멱등 결과로 반환한다.
- `GET /api/v1/market/orders/me`는 활성 주문 또는 최근 30일 종료 주문을 상태·방향·시장 품목과 cursor로 조회한다.
- `PATCH /api/v1/market/orders/{orderId}`는 미체결 수량 또는 가격을 수정하고 새 우선순위·만료 여부와 수정으로 발생한 즉시 체결을 반환한다.
- `POST /api/v1/market/orders/{orderId}/cancel`은 매수 escrow를 반환하거나 매도 잔량을 통합 물품 수령함으로 옮긴 뒤 주문을 닫는다.

주문 결과의 개별 `fills`는 `tradeId`, maker·taker 주문 식별자, 수량, maker 체결 단가, 총액과 판매 정산 식별자를 포함한다. 전체 결과는 요청·체결·미체결 수량, 가격 범위, 가중평균 단가, 총액, escrow 또는 반환 자산, 상태와 `marketRevision`을 포함한다.

### 통합 물품 수령과 정산

- `GET /api/v1/market/deliveries/me`는 구매 체결·판매 취소 반환·판매 만료 반환 물품을 cursor로 조회한다.
- `POST /api/v1/market/deliveries/{deliveryId}/claim`은 물품 한 건을 인벤토리에 전량 지급한다.
- `POST /api/v1/market/deliveries/claim-all`은 오래된 건부터 전량 수용 가능한 건만 지급하고 성공·잔류 건을 반환한다.
- `GET /api/v1/mails`, `POST /api/v1/mails/{mailId}/claim`, `POST /api/v1/mails/claim-all`은 판매 정산 우편을 조회·수령한다.
- `GET /api/v1/market/trades/me`와 `GET /api/v1/market/trades`는 개인·공개 체결 내역을 조회한다. 공개 응답은 계정 식별자·수수료·정산액을 제외한다.
- `GET /api/v1/market/summary/me`는 활성·종료·복구 검토 주문, 미수령 물품·정산과 항목별 최신·읽음 event ID를 반환한다.
- `POST /api/v1/market/unread-cursors/{stream}`는 체결·물품 수령함·판매 정산·주문 만료 중 조회가 성공한 항목의 `readThroughEventId`까지 계정 공용 읽음 cursor를 전진한다.

기존 `/market/materials`, 개별 `/listings`, `/materials/{itemId}/purchase`, `/buy-orders`와 `/purchase-deliveries` 계약은 새 계약으로 호출자를 모두 전환한 뒤 제거한다. 구현 상태와 전환 증거는 [I 거래소·경제 작업](../../wiki/06-delivery/tasks/I-marketplace-economy/_index.md)과 K-13·K-14가 소유한다.

### 관리자 주문장 운영

`/api/admin/v1/market/orders` 조회와 주문별 `purchase`·`cancel`, `/api/admin/v1/market/system-orders` 등록은 [거래소 SSOT](../../30-domain/economy/ssot.md)의 관리자 운영 경계를 따른다. 운영 API는 구형 매물 식별자가 아니라 통합 `orderId`를 사용하며 `market:manage`, `Idempotency-Key`와 운영 사유를 요구한다.

## 상태 코드

| 상태 | 사용 기준 |
| ---: | --- |
| 200 | 동기 성공 또는 조회 성공 |
| 202 | command 처리 중 |
| 400 | JSON 파싱·요청 형식 오류 |
| 401 | `AUTHENTICATION_REQUIRED` |
| 403 | Origin/CSRF 실패 또는 기능 권한 부족 |
| 404 | 리소스·command 없음 또는 타 계정 리소스 은닉 |
| 409 | 멱등 키 재사용·상태 충돌 |
| 413 | 1 MiB 본문 초과 |
| 415 | JSON 이외 Content-Type |
| 422 | 필드·도메인 검증 오류 |
| 429 | rate limit 초과 |
| 500/503 | 서버 오류·일시 장애 |

## 후속 API 범위

상세 DTO와 도메인별 오류는 다음 순서로 추가한다.

1. command 조회·전투 체크포인트
2. 챕터·스테이지·진행·보상
3. 인벤토리·장비·스킬
4. 보석 콘텐츠 12개 API
5. 계정 설정

각 계약은 해당 도메인 SSOT와 [인수 기준](../mvp-release/acceptance.md)을 참조하며 규칙과 수치를 복사하지 않는다. 거래소 1차 계약은 이 문서와 `packages/contracts/main.tsp`에 반영됐다.
