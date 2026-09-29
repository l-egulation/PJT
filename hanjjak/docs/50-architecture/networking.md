---
doc_kind: ssot
owner_domain: architecture
authority_level: applied
---

# Networking

웹 클라이언트와 서버 사이의 인증·전송·command·query·event 계약을 소유한다. 도메인 규칙과 필드 의미는 각 도메인 SSOT가 소유하며, API별 DTO와 예시는 [MVP API 계약](../70-plans/api-contract/README.md)에 둔다.

## 배포·전송 기본값

- 모든 API 경로는 `/api/v1/...`을 사용한다.
- 응답 형식은 JSON으로 고정하고 `Accept: application/json`을 요구한다.
- 상태 변경 요청 본문은 `Content-Type: application/json`만 허용한다.
- JSON 파싱·요청 형식 오류는 `400`, 지원하지 않는 Content-Type은 `415`, 도메인·필드 검증 오류는 `422`로 반환한다.
- 요청 본문 공통 최대 크기는 1 MiB다. 초과하면 `413`으로 거부하고 상태를 변경하지 않는다.
- 응답은 서버가 기본 압축을 협상하며 작은 응답은 압축하지 않을 수 있다.
- 날짜·시간은 ISO 8601 UTC 문자열로 전송한다. 예: `2026-09-01T12:34:56.789Z`.
- JSON 필드명은 camelCase다. 응답 DTO의 필드는 고정하며 값이 없으면 `null`, 목록은 빈 배열로 표현한다.
- 목록 조회는 불투명 cursor와 서버 limit 상한을 사용한다.
- 계정 권한 조회 응답은 `no-store`를 사용하고, 버전이 있는 정적 콘텐츠만 장기 캐시한다.

## 인증·세션

- 인증은 Secure·HttpOnly·Persistent 세션 쿠키를 사용한다. 개발 HTTP 환경에서만 Secure를 제외한다.
- 세션 쿠키는 `SameSite=Lax`다. 운영 웹과 API는 동일 사이트 경로를 기본으로 한다.
- 상태 변경 요청은 허용된 Origin을 검증하며 Origin이 없거나 허용 목록과 다르면 `403`으로 거부하고 상태를 변경하지 않는다. 별도 CSRF 토큰은 사용하지 않는다.
- 개발 환경의 다른 Origin은 명시적 allowlist만 credentials로 허용한다. 운영에서 wildcard CORS는 사용하지 않는다.
- MVP 인증 API는 회원가입·로그인·현재 세션·닉네임 수정·비밀번호 변경·로그아웃이다. 사용자 식별자는 이메일, 비밀번호는 최소 8자이며 복잡도 조합은 강제하지 않는다. 캐릭터 닉네임은 가입과 마이페이지 수정에서 서버가 공백 제거 후 1~20자로 검증한다.
- 계정당 로그인 세션은 하나다. 새 로그인은 기존 로그인 세션과 실행 세션을 대체한다. 비밀번호 변경은 현재 비밀번호를 재검증하고 새 비밀번호와 확인 값 일치를 검증한 뒤 BCrypt 해시와 `stateVersion`을 같은 트랜잭션에서 갱신한다.
- 세션은 절대 30일, 비활성 7일 후 만료된다. 로그아웃·세션 대체·비밀번호 변경 시 진행 중 전투는 즉시 중단하고 마지막 서버 체크포인트까지만 유지한다. 비밀번호 변경 성공 시 현재 인증 세션까지 종료해 새 비밀번호 재로그인을 요구한다.
- 로그인 실패와 rate limit 초과는 계정 존재 여부를 노출하지 않는 일반 로그인 실패 응답으로 처리한다.
- 소셜 인증은 `POST /api/v1/auth/social/{provider}/begin`이 생성한 서버 세션의 `state`·OIDC `nonce`·PKCE verifier를 사용하고, 공급자 콜백은 동일 서버 세션과 정확히 대응해야 한다. 공급자 access·refresh·ID token은 계정 식별 처리 후 폐기하며 브라우저에 전달하거나 DB에 저장하지 않는다.
- 신규 외부 식별자는 닉네임 확정 API 전까지 계정 행을 만들지 않는다. 기존 계정 연결은 인증된 세션의 `link/begin`에서만 시작하고, 연결 해제는 마지막 로그인 수단 보호를 적용한다.

## 공통 식별자·응답 envelope

- 상태를 변경하는 모든 POST·PUT·PATCH·DELETE는 `Idempotency-Key` 헤더에 canonical UUIDv4 문자열을 필수로 받는다.
- `X-Request-Id`와 `X-Correlation-Id`는 선택적 canonical UUIDv4 문자열이다. 누락·형식 오류는 요청을 거부하지 않고 서버가 새 값을 생성한다. 두 값은 멱등성 판단에 사용하지 않는다.
- 응답 헤더에는 확정된 request/correlation ID를 반영한다. `202`, `429`, 일시 장애 응답에는 필요할 때 `Retry-After`를 포함한다.
- 모든 성공·오류 응답의 `serverTime`은 응답 생성 시점의 UTC 시각이다. KST 일자와 입장권 권한 판정은 서버 내부 처리 시각을 사용한다.
- 성공 envelope는 다음 공통 필드를 갖는다: `requestId`, `serverTime`, `stateVersion`, `data`. `data`는 객체·배열·`null`만 허용한다.
- `stateVersion`은 계정 서버 권한 상태 전체의 단조 증가 버전이다. 서버 권한 상태를 변경한 모든 성공 mutation에서 증가하고, 조회는 읽은 최신 버전을 반환한다.
- 오류 envelope는 `requestId`, `serverTime`, `code`, `messageKey`, `retryable`, `details`를 갖는다. `details`는 `null` 또는 `field`, `code`, `messageKey`, 선택적 안전 값으로 구성된 오류 목록이다. 비밀번호·세션·전체 payload·소유 자산 목록·대형 값은 포함하지 않는다.
- 인증되지 않은 요청은 `401 AUTHENTICATION_REQUIRED`다. 다른 계정의 리소스는 존재 여부를 숨기기 위해 `404`, 인증된 사용자의 기능 권한 부족은 `403`으로 반환한다.

## Command·멱등성·재시도

- mutation은 모두 command 의미로 처리한다. 서버는 `commandId`와 멱등성 키, 요청 fingerprint, 처리 상태와 최종 결과를 저장한다.
- 같은 멱등성 키와 같은 요청이면 동일 결과를 반환한다. 같은 키에 다른 payload가 오면 `409 IDEMPOTENCY_KEY_REUSED`이며 상태를 변경하지 않는다.
- 멱등성 결과는 최소 7일 보존한다. `GET /api/v1/commands/{commandId}`는 `SUCCEEDED`, `FAILED`, `PROCESSING`, `NOT_FOUND` 상태를 조회하며 보존 만료·미존재는 `404 COMMAND_NOT_FOUND`다.
- 일반 동기 mutation 성공은 `200 OK`와 command 결과 envelope를 반환한다. 결과가 10초 안에 확정되지 않으면 원본 요청은 `202 Accepted`와 command 식별자를 반환하고 command 조회 API로 최종 결과를 확인한다.
- command 결과 envelope의 mutation 필드는 `commandId`, `idempotencyKey`, `status`(`SUCCEEDED`·`FAILED`·`PROCESSING`), `stateVersion`, `serverTime`, `data`다. 검증·권한 실패는 공통 오류 HTTP 응답으로 반환하고 command 조회에서는 `FAILED` 결과로 확인할 수 있다.
- 클라이언트는 조회에만 제한된 지수 backoff 자동 재시도를 적용한다. mutation은 command 결과 조회 후 동일 멱등성 키로 최대 1회 재시도한다.
- HTTP 상태 매핑은 `200` 성공, `202` 처리 중, `400` 잘못된 JSON·요청 형식, `401` 인증 필요, `403` CSRF·기능 권한 부족, `404` 리소스·command 없음, `409` 멱등 키·상태 충돌, `413` 본문 초과, `415` Content-Type 오류, `422` 도메인 검증 오류, `429` rate limit, `500/503` 서버·일시 장애다.
- 로그인과 상태 변경 API에는 계정·IP 기준 rate limit을 적용한다. 조회는 상대적으로 완화한다.

## 메인 전투 동기화 계약

- 메인 전투의 최종 판정은 서버 결정론 시뮬레이션이 소유한다. 클라이언트 로컬 엔진은 동일 입력·seed를 사용하는 예측 렌더러이며 보상·경험치·쌀·진행을 확정하지 않는다.
- 메인 전투는 일반 몬스터 20마리와 보스 1마리의 사이클 세션을 유지하되, 경험치·쌀·아이템의 서버 확정 단위는 몬스터 한 마리의 처치 정산이다.
- 사이클 시작은 `POST /api/v1/battles/sessions` command다. 서버는 `battleSessionId`, 토큰, logical tick 기준, seed와 전체 전투 입력 스냅샷을 발급한다.
- 시작 스냅샷은 스테이지·반복 대상·콘텐츠 버전·보상표 버전·캐릭터 레벨/경험치·장비·스킬·메인 보석 프리셋과 서버 확정 치장 도감·세트 보유 효과를 포함한다.
- 계정에는 활성 메인 전투 세션을 하나만 둔다. 새 시작은 기존 세션과 토큰을 대체·폐기한다.
- 전투 토큰은 사이클 종료 또는 중단까지 유효하다. heartbeat가 90초 동안 없으면 토큰을 만료시키며, 이미 확정한 처치 정산은 유지하고 서버가 확인하지 못한 처치의 보상은 만들지 않는다.
- 클라이언트는 30초마다 heartbeat를 보낸다. `visibilitychange`, `pagehide`, 네트워크 단절·복귀 시 lifecycle 신호와 함께 중단을 알린다.
- lifecycle 중단은 abort를 best-effort로 한 번 시도한다. 전송 실패나 강제 종료는 90초 무신호 만료로 처리한다.
- `POST /api/v1/battles/sessions/{battleSessionId}/settlements`는 마지막으로 확인한 일반 몬스터 순번과 렌더링 체크포인트만 받는다. 보상·경험치·쌀 결과 수치는 받지 않는다.
- 정산 시 서버는 토큰·활성 세션·콘텐츠 버전, 저장된 입력·seed의 처치 사건과 처치 가능 시각을 검증하고, 아직 정산되지 않은 처치를 순서대로 확정한다.
- 클라이언트는 일반 몬스터 처치 사건을 재생한 직후 정산을 요청한다. 네트워크 지연이나 시계 경계에서 서버가 `BATTLE_SETTLEMENT_NOT_READY`를 반환하면 같은 멱등 키로 재시도한다.
- 한 처치의 경험치·레벨·쌀·아이템 지급·정산 결과·Outbox는 하나의 원자 처리로 확정한다. 같은 세션과 적 순번은 다른 요청 키로 재전송돼도 저장 결과에 수렴한다.
- `POST /api/v1/battles/sessions/{battleSessionId}/complete`는 예측 hash·렌더링 체크포인트만 받고, 누락된 일반 처치 정산과 보스 처치 보상, 스테이지 진행·다음 반복 사이클을 원자 확정한다. 실패는 확인된 일반 처치까지만 정산하고 실패 기록을 저장한다.
- 예측 hash·체크포인트 불일치는 서버 결과를 무효화하지 않는다. 전투 버전·클라이언트 오류·조작 분석을 위한 진단 이벤트로만 기록한다.
- 활성 세션이 없는 재접속은 마지막 서버 확정 진행·스테이지·반복 대상만 복원한다. 확정된 자산은 유지하고 미확정 전투 예측 상태를 복원하지 않는다.
- `POST /api/v1/battles/sessions/{battleSessionId}/abort`는 선택적 렌더링 체크포인트까지 서버가 확인한 누락 처치를 정산한 뒤 세션을 종료하고 토큰을 폐기한다.
- 스킬 레벨업·장비 강화는 성공한 command sequence가 확정된 다음 logical tick부터 현재 전투에 즉시 적용한다. 서버가 확정 시점 tick을 부여하고, 완료 재계산은 해당 세션 내 성공한 두 종류의 command 이력을 sequence 순서로 재생한다.
- 보석 변경과 치장 도감·세트 보유 효과 변경은 현재 전투에 즉시 적용하지 않으며 다음 사이클 시작부터 적용한다. 같은 계정에 현재 메인 전투가 있으면 치장 보유 효과 변경은 현재 사이클 스냅샷을 바꾸지 않고 다음 사이클 시작 입력에 포함한다. 장비 장착·해제, 스킬 장착 순서, 전문 변경은 실시간 적용 대상이 아니다. 치장 외형 착용·해제는 전투 능력치와 무관한 외형 상태 변경으로 처리한다. 메인 보석 프리셋 변경은 기존 규칙대로 현재 전투를 종료하고 0/20부터 재시작한다.
- 강화 command와 완료 command가 겹치면 계정 command sequence에서 먼저 확정된 순서를 따른다.
- 같은 `battleSessionId`와 같은 멱등성 키의 완료 재전송은 저장된 결과를 반환한다. 이미 종료된 세션에 다른 키로 완료를 요청하면 `409 BATTLE_SESSION_CLOSED`로 거부한다.

### 메인 전투 API

- `POST /api/v1/battles/sessions`: 사이클 시작과 전체 입력 스냅샷 발급
- `POST /api/v1/battles/sessions/{battleSessionId}/heartbeat`: 30초 진행 신호
- `POST /api/v1/battles/sessions/{battleSessionId}/settlements`: 확인된 일반 몬스터 처치별 보상·성장 원자 확정
- `POST /api/v1/battles/sessions/{battleSessionId}/complete`: 누락 일반 처치·보스 정산과 스테이지 진행 확정
- `POST /api/v1/battles/sessions/{battleSessionId}/abort`: 확인된 누락 처치 정산 후 전투 중단
- `GET /api/v1/offline-rewards/pending`은 로그아웃·브라우저 종료·장기 네트워크 단절 뒤 첫 재로그인에서 서버 UTC 실제 경과시간, 1분 단위·최대 480분 보상 적용시간과 현재 스테이지 1분 기대 보상의 50%로 고정한 결과 또는 `null`을 반환한다. 1분 미만은 보상 작업을 취소하며 같은 탭 새로고침은 저장한 게임 세션 heartbeat를 재개한다. 점진 배포 호환을 위해 `offlineSeconds`는 선택 필드이고 구형 응답에서는 웹이 `eligibleSeconds`로 대체한다.
- `POST /api/v1/offline-rewards/claim`은 UUIDv4 `Idempotency-Key`로 쌀·경험치·주력 재료를 한 번만 지급하고 `CLAIMED` 결과를 반환한다. 오프라인 보상은 스테이지 해금·랭킹·거래 자산을 변경하지 않는다.


현재 메인 전투는 위 battle session API로 사이클 입력 발급·heartbeat·서버 재계산 완료·중단과 다음 사이클 자동 시작을 처리한다. `POST /api/v1/battles/cycles`와 `POST /api/v1/battles/chapters/{chapter}/auto-run`은 레거시 즉시 보상 경로이며 운영 기본값에서 비활성화한다. 비활성화된 경로의 요청은 보상·진행을 변경하지 않고 `BATTLE_COMPATIBILITY_API_DISABLED`로 거부하며, 서버 경고 로그에 계정·경로·원격 주소를 남긴다.

## MVP API 범위

상세 DTO·오류·예시는 [MVP API 계약](../70-plans/api-contract/README.md)에 기록한다. 계약 범위는 인수 흐름 전체이며 인증·세션, 챕터·스테이지, 전투 명령·체크포인트, 성장·보상, 인벤토리·장비·스킬·보석·치장, 거래소, 설정 조회·저장을 포함한다.

- `GET /api/v1/chapters`는 버전이 있는 정적 챕터 정보와 인증된 사용자의 서버 권한 진행 상태를 조합해 잠금·최초 클리어 진행률·완료·현재 전투·진행 목표를 반환한다.
- `GET /api/v1/chapters/{chapterId}/stages`는 챕터 존재와 최신 해금 상태를 재검증한다. 존재하지 않는 챕터는 `CHAPTER_NOT_FOUND`, 잠긴 챕터는 선행 정보를 포함한 `CHAPTER_LOCKED`로 거부한다.
- 챕터 선택용 POST와 수동 해금 API는 만들지 않는다. 마지막 스테이지 최초 클리어 command가 챕터 완료와 다음 챕터 해금을 원자적으로 확정한다.
- `GET /api/v1/stages/{stageId}`는 스테이지 상태·주요 보상·`damagePercent`·해당 스테이지 최근 실패 1건을 반환한다.
- `POST /api/v1/stages/{stageId}/enter`는 존재와 해금 상태를 검증하고 성공 시 즉시 0/20부터 시작한다.
- `PATCH /api/v1/battle/repeat-stage`는 해금된 1~9스테이지만 허용한다. 10스테이지는 `STAGE_NOT_REPEATABLE`로 거부한다.
- `GET /api/v1/progression/stages`는 서버 권한의 현재·최고 클리어 스테이지와 반복 대상을 반환한다.
- `POST /api/v1/battles/sessions`와 하위 heartbeat·settlements·complete·abort는 활성 게임 실행 세션과 전투 토큰을 검증하고, 저장된 입력·seed로 처치별 보상·성장과 사이클 진행을 확정한다.
- 레거시 `POST /api/v1/battles/cycles`와 `POST /api/v1/battles/chapters/{chapter}/auto-run`은 운영에서 비활성화된 통합·검증용 호환 command다. 개발·회귀 검증에서만 `BATTLE_COMPATIBILITY_API_ENABLED=true`로 명시적으로 켤 수 있다.
- 인벤토리 조회는 `GET /api/v1/inventory`, `GET /api/v1/inventory/items/{itemId}`, `GET /api/v1/inventory/status`를 사용한다. 임의의 인벤토리 변경 API는 만들지 않는다.
- 거래소 조회와 command는 시장 품목 목록·단일 품목 주문장·주문 견적·매수/매도 IOC·지정가 주문·내 주문·통합 물품 수령함·체결 내역 계약을 사용한다. 상세 경로와 clean cutover는 [거래소·주문장 계약](#거래소주문장-계약)을 따른다.
- 정산 우편은 `GET /api/v1/mails`, `POST /api/v1/mails/{mailId}/claim`, `POST /api/v1/mails/claim-all`을 사용한다.

### 일일 봉인 레이드 API 라우팅

- `GET /api/v1/raid`, `/ranking`, `/claims`, `/attempts/current`는 인증 세션의 계정 권한을 조회하며 `no-store` 응답을 사용한다.
- 도전 시작·재시도·확정·폐기와 보상 수령은 `/api/v1/raid/attempts*`·`/api/v1/raid/claims/{claimId}` command 경로로 라우팅하고 canonical UUID `Idempotency-Key`를 요구한다.
- 상태·입력 snapshot·전투 timeline·순위·claim은 raid 모듈이 소유하고 game-api composition root는 전투·성장·재화 포트만 연결한다.

## 치장 뽑기 계약

세부 목적과 연결 요구사항은 [치장 뽑기 시스템 요구사항 명세서 §7](../70-plans/cosmetic-gacha/requirements.md#7-api-요구사항)이 소유한다. 이 문서는 API 경로와 책임 경계만 안내한다.

- `GET /api/v1/cosmetic-gacha/banners`: 배너·재화·예상 결제·누적 및 미수령 상태 조회.
- `GET /api/v1/cosmetic-gacha/banners/{bannerId}`: 배너 풀·확률·콘텐츠 버전 조회.
- `POST /api/v1/cosmetic-gacha/banners/{bannerId}/draws`: `count`만 받아 서버 권위로 결제·추첨·지급·상태를 원자 처리한다.
- `POST /api/v1/cosmetic-gacha/banners/{bannerId}/milestone-claims`: 선택 상자 수령을 처리한다.
- `POST /api/v1/cosmetic-selector-boxes/{boxItemId}/open`: 선택 상자와 치장 교환을 처리한다.
- `GET /api/v1/cosmetics/collection`: 보유 치장, `registeredQuantity`, `unregisteredQuantity`, `reservedQuantity`, `availableUnregisteredQuantity`, 6부위 외형 착용 슬롯, 도감 등록 현황, 세트 효과와 등록 가능 상태를 조회한다.
- `POST /api/v1/cosmetics/{cosmeticId}/registrations`: body `mode`는 `ONE` 또는 `UNTIL_NEXT_STAR`이며, 서버가 다음 성급 필요량과 거래 예약을 제외한 사용 가능 미등록 재고를 검증해 등록을 원자 처리한다.
- `PATCH /api/v1/cosmetics/equipment/{slotId}`: body `cosmeticId|null`로 `HEAD`, `TOP`, `BOTTOM`, `GLOVES`, `SHOES`, `CAPE` 슬롯의 외형 착용·해제를 처리한다.

추첨 요청 body에는 `count`만 포함하며 payment, 결과, 확률, 사용자 식별자와 보상 목록은 서버가 소유·결정한다. 상태를 변경하는 API는 이 문서와 [MVP API 계약](../70-plans/api-contract/README.md)의 확정된 공통 idempotency, HTTP 상태 코드와 envelope를 사용한다. 치장별 상세 DTO와 도메인 오류만 후속 계약 범위다.

책임 연결: [치장 SSOT](../30-domain/cosmetics/ssot.md), [MVP 치장 뽑기 콘텐츠 v1](../60-content/cosmetics/mvp-v1.md), [치장 뽑기 시스템 요구사항 명세서](../70-plans/cosmetic-gacha/requirements.md).

- 권한과 재시도 경계: [Player SSOT](../30-domain/player/ssot.md)
- 이벤트 envelope: [Event System SSOT](../40-systems/event-system/ssot.md)
- 계약 정의·생성 경계: [Content Pipeline](./content-pipeline.md), [Monorepo](./monorepo.md)
- 미확정 콘텐츠 수치: [Decision Log](../80-decisions/README.md)

## 거래소·주문장 계약

거래소 도메인 규칙은 [거래소 SSOT](../30-domain/economy/ssot.md)가 소유한다. 이 문서는 목표 API 경로, mutation 멱등성, revision과 오류 경계를 고정하며 구현 전환 근거는 [호가창 계획](../70-plans/market-order-book/README.md)을 따른다.

- `GET /api/v1/market/instruments`: 거래 가능한 시장 품목의 UUID 식별자·canonical key·표시 정보·활성/취소 처리 상태, 최고 매수·최저 매도·최근 체결과 품목별 `marketRevision`을 반환한다. API path와 관계 참조는 UUID를 사용하고 생성·직렬화 규칙은 [거래소 주문장 아키텍처 결정](../80-decisions/architecture/market-order-book.md)이 소유한다.
- `GET /api/v1/market/order-books/{instrumentId}?levels=5`: 선택 품목의 매수·매도 가격대 각 5개와 가격·총잔량·누적 잔량·인증 사용자의 주문 수량, 최고 매수·최저 매도·스프레드, 최근 체결 10건과 `marketRevision`을 반환한다. 공개 잔량은 본인 주문을 포함하고 사용자별 체결 가능 수량은 본인 주문을 제외한다.
- `GET /api/v1/market/instruments/{instrumentId}/price-levels`: 대기 주문 방향 `side=BUY|SELL`, 선택적 `afterUnitPrice`, `limit`(기본 10, 1~10)을 받아 유리한 가격순의 가격대와 `marketRevision`, `nextUnitPrice`를 반환한다. 다음 조회는 마지막 가격을 제외한 뒤쪽 구간이다. 각 행은 총량·내 물량·시장 전체 누적량·본인 제외 전체 누적량을 제공하며 누적은 페이지 시작점에서 초기화하지 않는다. 다음 가격이 null이면 끝이며 서로 다른 revision의 페이지를 이어 붙이지 않는다. 가격대 조회는 품목 잠금 안에서 만료·revision·집계를 일관되게 읽는다.
- `GET /api/v1/market/instruments/{instrumentId}/candles?interval=5m|15m|30m&period=24h|7d|14d`: 원본 `market_trade`를 서버에서 선택한 봉 간격과 표시 기간으로 OHLCV 집계해 반환한다. 기본값은 5분 봉·24시간이며 거래가 없는 구간은 행을 만들지 않는다. 클라이언트는 체결 10건을 이어 붙여 추이를 계산하지 않는다.
- `GET /api/v1/market/instruments/{instrumentId}/order-quote`: `side`, `timeInForce`, `quantity`, `limitUnitPrice`를 받아 최신 상대 주문 기준 예상 즉시 체결 수량·미체결 수량·최저/최고 체결 단가·가중평균 단가·총액·예상 수수료·순정산액과 사용 가능 쌀 또는 판매 수량을 반환한다. 품목 잠금 안에서 매수는 본인 매도 주문을 후보에서 제외하고, 매도는 본인 매수 주문과의 교차를 생성 명령과 같이 거절하며 후보와 revision을 읽는다. 견적은 가격·수량을 예약하지 않으며 반환 자산 필드를 반영한 확정 실행 가능량으로 해석하지 않는다.
- `POST /api/v1/market/orders`: 시장 품목, `BUY|SELL`, 수량, 가격과 `IOC|GTC`를 받아 IOC 또는 8시간 지정가 주문을 실행한다. 한 개 이상 체결된 IOC만 종료 주문으로 저장하고 0체결 실패는 7일 command 결과로만 보존한다.
- `GET /api/v1/market/orders/me`: `ACTIVE|CLOSED|RECOVERY_REVIEW`, 방향·상태·시장 품목과 cursor를 받아 활성 주문, 최근 30일 종료 주문 또는 이관 불일치로 격리된 `복구 검토 중` 주문을 반환한다. 부분 체결 주문은 최초·체결·미체결 수량과 우선순위·만료를 구분한다.
- `PATCH /api/v1/market/orders/{orderId}`: 활성 본인 지정가 주문의 가격 또는 미체결 수량을 변경한다. 가격 변경·수량 증가는 전체 주문의 새 우선순위와 8시간 만료를 부여하고 수량 감소는 기존 우선순위·만료를 유지하며, 교차하면 maker 가격으로 즉시 체결한다.
- `POST /api/v1/market/orders/{orderId}/cancel`: 활성 본인 주문을 즉시 닫는다. 매수 주문의 남은 escrow는 사용 가능 쌀로 반환하고 매도 주문의 남은 물품은 인벤토리 공간과 무관하게 통합 물품 수령함으로 옮긴다.
- `GET /api/v1/market/deliveries/me`: 미수령 구매 체결 물품과 판매 취소·만료 반환 물품을 원본 참조·출처·표시명 snapshot·생성 시각과 함께 cursor로 반환한다.
- `POST /api/v1/market/deliveries/{deliveryId}/claim`: 미수령 본인 물품 한 건을 인벤토리 공간 검증 후 전량 지급한다.
- `POST /api/v1/market/deliveries/claim-all`: `(createdAt, deliveryId)` 순서로 건별 전량 수용 가능 여부를 검사해 가능한 건만 지급하고 공간 부족 건은 유지한다. 성공·잔류 건과 최신 인벤토리 상태를 하나의 멱등 결과로 반환한다.
- `GET /api/v1/market/summary/me`: 활성·종료·복구 검토 주문, 미수령 delivery·정산과 `FILLS|DELIVERIES|SETTLEMENTS|EXPIRATIONS` 네 stream의 계정별 최신·읽음 bigint sequence를 반환한다.
- `POST /api/v1/market/unread-cursors/{stream}`: 조회 성공 응답의 `readThroughSequence`를 받아 계정 공용 cursor를 단조 증가시킨다. 서버가 해당 계정·stream에 발급하지 않았거나 아직 조회로 증명되지 않은 미래 sequence까지 전진할 수 없다.
- `GET /api/v1/market/trades/me`와 `GET /api/v1/market/trades`: 개인·전체 체결을 cursor로 조회한다. 공개 응답은 계정 식별자·수수료·정산액을 포함하지 않고 거래 당시 표시명 snapshot을 사용한다.
- `GET /api/v1/mails`, `POST /api/v1/mails/{mailId}/claim`, `POST /api/v1/mails/claim-all`: IOC와 지정가 판매 체결의 정산 우편 조회·개별 수령·전체 수령을 공통 처리한다.
- 모든 거래소·우편 mutation은 `Idempotency-Key`를 필수로 받고 command 결과 envelope를 반환한다. 실제 상태를 바꾼 성공 주문 등록·수정·취소만 활성 주문 30개와 최근 1분 30회 제한 이력에 기록한다.
- 주문 결과는 maker 주문 가격의 개별 체결, 체결·미체결 수량·가격 범위·가중평균 단가·총액·수수료·정산 또는 escrow 변화와 `marketRevision`을 반환한다. 도메인 실패는 상태를 임의 변경하지 않고 원인을 구분한다.
- 공개 주문장과 내 주문은 3초 polling과 수동 새로고침을 지원한다. 낮은 revision 응답은 폐기하고 revision 간격 또는 cursor 변경 오류면 선택 품목 주문장·해당 품목 내 주문·개인 요약을 전체 재조회한다. 복구가 끝날 때까지 주문 제출을 비활성화한다.
- 기존 개별 매물·즉시 구매·구매 예약·구매 수령 API는 새 주문·주문장·통합 수령 계약으로 모든 호출자를 전환한 뒤 제거한다. 호환 alias나 이중 쓰기를 남기지 않는다.

### 데이터 전환 계약

- 기존 활성 판매 주문은 각 행을 새 `SELL GTC` 한 건으로 이관하고 구형 가격 수정 여부와 관계없이 원래 `createdAt` 우선순위를 보존한다.
- 기존 활성 구매 예약은 `BUY GTC`로 소급 이관하지 않고 취소해 남은 escrow를 반환한다.
- 기존 구매 수령 물품과 판매 취소·만료 물품은 합산하지 않고 원본 행 참조를 보존해 delivery 한 건씩 이관한다.
- 주문 잔량과 실제 escrow·외부 보관이 불일치하면 해당 주문만 `RECOVERY_REVIEW`로 격리하고 정상 주문 이관을 계속한다. 자동 보전하지 않으며 운영 검토는 원장으로 확인한 자산 반환 또는 무반환 종료만 허용하고 감사 기록을 남긴다.

## 도메인별 계약 경계

- 챕터·스테이지 계약의 응답 필드와 검증은 [스테이지 화면 요구사항](../70-plans/stage-screen/requirements.md)과 [스테이지 SSOT](../30-domain/world/ssot.md)를 따른다.
- 보석 API 12개의 기능 범위와 원자성은 [보석 콘텐츠 구현 요구사항](../70-plans/gem-content/implementation-requirements.md)을 따른다. DTO·상태·오류 상세는 MVP API 계약에서 확정한다.
- 이벤트 envelope와 Outbox는 [데이터·이벤트 SSOT](../40-systems/event-system/ssot.md)가 소유한다.
- 권한과 재시도 경계는 [계정·저장 SSOT](../30-domain/player/ssot.md)와 이 문서를 함께 따른다.
