# 한짝 스킬 API 명세서

> 문서 상태: 초안
> 대상: 기획·클라이언트·서버·QA
> API 버전: `v1`
> 기본 경로: `/api/v1`

---

## 1. 문서 목적

이 문서는 한짝의 스킬 관리 기능에서 사용하는 클라이언트–서버 API 계약을 정의한다.

대상 기능은 다음과 같다.

- 스킬 6종 상태 조회
- 스킬 해금
- 스킬 승급
- 스킬 강화
- 실패 보정 조회·반영
- 액티브 스킬 4슬롯 장착 순서 조회·저장

다음 기능은 이 문서 범위에 포함하지 않는다.

- 자동전투 중 매 행동의 스킬 선택 판정
- 스킬북 드롭 처리
- 거래소 구매·판매
- 스킬 콘텐츠 데이터 수정
- 스킬 이름·아이콘·연출 관리

---

## 2. 핵심 정책

### 2.1 서버 권한 상태

다음 상태는 서버가 최종 권한을 가진다.

- 스킬 해금 여부
- 현재 등급
- 현재 강화 단계
- 스킬·등급·목표 단계별 실패 보정
- 액티브 스킬 장착 순서
- 스킬북 보유량
- 쌀 보유량
- 스킬 상태 버전 `stateVersion`
- 명령 멱등성 처리 결과

클라이언트와 로컬 전투 엔진은 서버에서 확정된 스킬 상태를 캐시한다. 클라이언트가 강화 성공 여부, 단계, 실패 보정 또는 자산 수량을 직접 확정하지 않는다.

### 2.2 원자성

스킬 성장 요청에서 다음 처리는 하나의 서버 트랜잭션으로 수행한다.

1. 현재 스킬 상태 검증
2. 스킬북·쌀 보유량 검증
3. 성공률과 실패 보정 계산
4. 스킬북·쌀 차감
5. 성공 또는 실패 판정
6. 등급·단계 또는 실패 보정 변경
7. `stateVersion` 증가
8. 멱등성 결과 저장

중간 처리만 반영된 상태는 외부에 노출하지 않는다.

---

## 3. 공통 규칙

### 3.1 요청·응답 형식

- Content-Type: `application/json`
- 날짜·시각: ISO 8601 UTC
- 수량: 0 이상의 정수
- 성공률: 정수 `%`
- 실패 보정: 정수 `%p`
- 스킬 콘텐츠는 `contentVersion` 기준

### 3.2 인증

- 사용자 계정은 인증 컨텍스트에서 식별한다.
- 클라이언트가 요청 본문에 `accountId`를 보내지 않는다.
- 인증 토큰 전달 방식은 공통 인증 API 정책에서 별도로 정한다.

### 3.3 변경 명령 공통 헤더

성장과 장착 순서 저장 요청에는 다음 헤더가 필요하다.

| 헤더 | 필수 | 형식 | 설명 |
| --- | --- | --- | --- |
| `Idempotency-Key` | 예 | UUID 문자열 | 사용자 명령 재시도 키 |
| `If-Match` | 예 | 정수 문자열 | 마지막 조회에서 받은 `stateVersion` |

### 3.4 멱등성 처리 순서

서버는 변경 요청을 다음 순서로 검증한다.

1. `Idempotency-Key` 조회
2. 요청 fingerprint 비교
3. 완료된 동일 요청이 있으면 최초 결과 반환
4. 처음 보는 키이면 `If-Match` 검증
5. 도메인 규칙·자산 검증
6. 명령 실행

따라서 최초 요청이 성공했으나 응답이 유실된 경우, 클라이언트는 같은 키와 같은 본문으로 재시도한다. 첫 요청으로 `stateVersion`이 증가했더라도 서버는 저장된 최초 결과를 반환한다.

같은 키에 다른 본문을 보내면 `IDEMPOTENCY_KEY_REUSED` 오류를 반환한다.

---

## 4. 공통 타입

### 4.1 SkillId

```text
active_heavy
active_dot
active_haste
active_basic_amp
passive_critical
passive_all_damage
```

| SkillId | 분류 | 역할 |
| --- | --- | --- |
| `active_heavy` | 공격 액티브 | 한 번에 큰 피해 |
| `active_dot` | 공격 액티브 | 지속 피해 |
| `active_haste` | 버프 액티브 | 기본공격 속도 증가 |
| `active_basic_amp` | 버프 액티브 | 기본공격 피해 증가 |
| `passive_critical` | 패시브 | 치명타 확률 증가 |
| `passive_all_damage` | 패시브 | 모든 피해 증가 |

### 4.2 Enum

```text
SkillKind = ATTACK_ACTIVE | BUFF_ACTIVE | PASSIVE
SkillGrade = NORMAL | RARE | HEROIC | LEGENDARY
GrowthAction = UNLOCK | PROMOTE | ENHANCE
GrowthOutcome = SUCCESS | FAILURE
```

### 4.3 SkillProgress

해금 상태:

```json
{
  "unlocked": true,
  "grade": "NORMAL",
  "level": 4
}
```

잠금 상태:

```json
{
  "unlocked": false,
  "grade": null,
  "level": 0
}
```

### 4.4 GrowthQuote

```json
{
  "action": "ENHANCE",
  "targetGrade": "NORMAL",
  "targetLevel": 5,
  "baseSuccessPercent": 50,
  "failureBonusPercentPoint": 10,
  "finalSuccessPercent": 60,
  "riceCost": 14075,
  "books": [
    {
      "itemId": "skillbook:active_heavy:normal",
      "grade": "NORMAL",
      "required": 1,
      "owned": 3
    }
  ],
  "executable": true,
  "blockedReason": null
}
```

`blockedReason` 값:

```text
SKILL_LOCKED
PREVIOUS_GRADE_NOT_MAXED
INVALID_TARGET_LEVEL
MAX_GROWTH_REACHED
INSUFFICIENT_BOOKS
INSUFFICIENT_RICE
STATE_SYNC_REQUIRED
```

---

## 5. 공통 오류 응답

```json
{
  "error": {
    "code": "INSUFFICIENT_RICE",
    "message": "쌀이 부족합니다.",
    "retryable": false,
    "details": {
      "required": 5630,
      "owned": 4200
    }
  },
  "stateVersion": 18,
  "traceId": "01JEXAMPLE"
}
```

| 필드 | 필수 | 설명 |
| --- | --- | --- |
| `error.code` | 예 | 클라이언트 분기용 오류 코드 |
| `error.message` | 예 | 사용자 표시용 기본 문구 |
| `error.retryable` | 예 | 같은 요청 재시도 가능 여부 |
| `error.details` | 아니오 | 오류별 상세 데이터 |
| `stateVersion` | 아니오 | 서버 최신 상태 버전 |
| `traceId` | 예 | 서버 로그 추적 ID |

클라이언트는 `message`가 아니라 `code`로 분기한다.

---

# API 1. 스킬 목록·상태 조회

## 6. GET `/api/v1/skills`

스킬 6종의 진행 상태, 성장 견적, 보유 자산과 장착 순서를 조회한다.

### 6.1 Request

```http
GET /api/v1/skills
```

본문 없음.

선택 Query Parameter:

| 이름 | 형식 | 설명 |
| --- | --- | --- |
| `contentVersion` | 문자열 | 생략 시 서버 현재 적용 버전 |

### 6.2 Response — `200 OK`

```json
{
  "contentVersion": "v1",
  "stateVersion": 17,
  "stateAuthority": {
    "assetAuthority": "SERVER",
    "progressAuthority": "SERVER"
  },
  "balances": {
    "riceOwned": 10000
  },
  "skills": [
    {
      "skillId": "active_heavy",
      "kind": "ATTACK_ACTIVE",
      "display": {
        "nameKey": "skill.active_heavy.name",
        "iconKey": "skill.active_heavy.icon"
      },
      "progress": {
        "unlocked": true,
        "grade": "NORMAL",
        "level": 4
      },
      "currentEffect": {
        "type": "ATTACK_DAMAGE_PERCENT",
        "value": 230
      },
      "nextEffect": {
        "type": "ATTACK_DAMAGE_PERCENT",
        "value": 240
      },
      "cooldownSeconds": 10,
      "activeSlot": 1,
      "passiveApplied": false,
      "growthQuote": {
        "action": "ENHANCE",
        "targetGrade": "NORMAL",
        "targetLevel": 5,
        "baseSuccessPercent": 50,
        "failureBonusPercentPoint": 10,
        "finalSuccessPercent": 60,
        "riceCost": 14075,
        "books": [
          {
            "itemId": "skillbook:active_heavy:normal",
            "grade": "NORMAL",
            "required": 1,
            "owned": 3
          }
        ],
        "executable": true,
        "blockedReason": null
      }
    }
  ],
  "activeLoadout": [
    "active_heavy",
    "active_dot",
    "active_haste",
    "active_basic_amp"
  ]
}
```

### 6.3 Response 규칙

- 잠긴 스킬을 포함해 항상 6종을 반환한다.
- 패시브의 `activeSlot`은 `null`이다.
- 해금된 패시브는 `passiveApplied=true`다.
- 액티브의 `activeSlot`은 1~4 또는 `null`이다.
- `growthQuote`는 조회 시점의 서버 자산과 실패 보정을 기준으로 한다.
- 전설 10강은 `executable=false`, `blockedReason=MAX_GROWTH_REACHED`다.
- 효과·확률·비용을 클라이언트가 다시 계산하지 않는다.

---

# API 2. 스킬 성장

## 7. POST `/api/v1/skills/{skillId}/growth`

스킬 해금·승급·강화를 요청한다.

### 7.1 Request

```http
POST /api/v1/skills/active_heavy/growth
Idempotency-Key: 47f8a0f8-5643-4e8f-aef5-d11111111111
If-Match: 17
Content-Type: application/json
```

```json
{
  "action": "ENHANCE",
  "targetGrade": "NORMAL",
  "targetLevel": 5,
  "contentVersion": "v1"
}
```

| 필드 | 필수 | 규칙 |
| --- | --- | --- |
| `action` | 예 | 현재 상태에서 가능한 동작과 일치해야 함 |
| `targetGrade` | 예 | 해금은 NORMAL, 승급은 다음 등급, 강화는 현재 등급 |
| `targetLevel` | 예 | 해금·승급은 1, 강화는 현재 단계+1 |
| `contentVersion` | 예 | 견적 조회에 사용한 콘텐츠 버전 |

클라이언트가 보내지 않는 값:

- 쌀 비용
- 스킬북 비용
- 성공률
- 실패 보정
- RNG seed
- 성공 여부

### 7.2 Response — 강화 성공 `200 OK`

```json
{
  "commandId": "01JCOMMAND",
  "replayed": false,
  "outcome": "SUCCESS",
  "consumed": {
    "rice": 14075,
    "books": [
      {
        "itemId": "skillbook:active_heavy:normal",
        "grade": "NORMAL",
        "quantity": 1
      }
    ]
  },
  "before": {
    "unlocked": true,
    "grade": "NORMAL",
    "level": 4
  },
  "after": {
    "unlocked": true,
    "grade": "NORMAL",
    "level": 5
  },
  "failureBonusPercentPoint": 0,
  "nextGrowthQuote": {
    "action": "ENHANCE",
    "targetGrade": "NORMAL",
    "targetLevel": 6,
    "baseSuccessPercent": 45,
    "failureBonusPercentPoint": 0,
    "finalSuccessPercent": 45,
    "riceCost": 16890,
    "riceOwned": 4370,
    "books": [
      {
        "itemId": "skillbook:active_heavy:normal",
        "grade": "NORMAL",
        "required": 1,
        "owned": 0
      }
    ],
    "executable": false,
    "blockedReason": "INSUFFICIENT_BOOKS"
  },
  "balances": {
    "riceOwned": 4370,
    "books": [
      {
        "itemId": "skillbook:active_heavy:normal",
        "grade": "NORMAL",
        "owned": 0
      }
    ]
  },
  "stateVersion": 18,
  "contentVersion": "v1"
}
```

### 7.3 Response — 강화 실패 `200 OK`

강화 시도 자체는 정상 처리됐으므로 HTTP 상태는 `200 OK`다.

```json
{
  "commandId": "01JCOMMAND",
  "replayed": false,
  "outcome": "FAILURE",
  "consumed": {
    "rice": 14075,
    "books": [
      {
        "itemId": "skillbook:active_heavy:normal",
        "grade": "NORMAL",
        "quantity": 1
      }
    ]
  },
  "before": {
    "unlocked": true,
    "grade": "NORMAL",
    "level": 4
  },
  "after": {
    "unlocked": true,
    "grade": "NORMAL",
    "level": 4
  },
  "failureBonusPercentPoint": 15,
  "nextGrowthQuote": {
    "action": "ENHANCE",
    "targetGrade": "NORMAL",
    "targetLevel": 5,
    "baseSuccessPercent": 50,
    "failureBonusPercentPoint": 15,
    "finalSuccessPercent": 65,
    "riceCost": 14075,
    "riceOwned": 4370,
    "books": [
      {
        "itemId": "skillbook:active_heavy:normal",
        "grade": "NORMAL",
        "required": 1,
        "owned": 0
      }
    ],
    "executable": false,
    "blockedReason": "INSUFFICIENT_BOOKS"
  },
  "balances": {
    "riceOwned": 4370,
    "books": [
      {
        "itemId": "skillbook:active_heavy:normal",
        "grade": "NORMAL",
        "owned": 0
      }
    ]
  },
  "stateVersion": 18,
  "contentVersion": "v1"
}
```

### 7.4 서버 처리 규칙

- 해금·승급 성공률은 100%다.
- 강화 실패 시에도 요구 스킬북과 쌀을 소비한다.
- 실패해도 등급·단계는 유지한다.
- 실패할 때마다 해당 목표의 보정이 5%p 증가한다.
- 최종 성공률은 100%를 넘지 않는다.
- 성공 시 해당 목표의 실패 보정만 초기화한다.
- 최종 성공률이 100%면 확정 성공한다.
- 모든 상태 변경과 자산 차감은 하나의 서버 트랜잭션으로 처리한다.
- 동일 요청 재생 응답은 `replayed=true`다.

### 7.5 Error

| HTTP | code | 조건 | 자산 변경 |
| ---: | --- | --- | --- |
| 400 | `INVALID_GROWTH_ACTION` | 현재 상태와 action 불일치 | 없음 |
| 400 | `INVALID_TARGET_GRADE` | 등급 조건 불일치 | 없음 |
| 400 | `INVALID_TARGET_LEVEL` | 한 단계 초과 강화 등 | 없음 |
| 401 | `UNAUTHENTICATED` | 인증 실패 | 없음 |
| 404 | `SKILL_NOT_FOUND` | 존재하지 않는 SkillId | 없음 |
| 409 | `STATE_VERSION_CONFLICT` | If-Match 불일치 | 없음 |
| 409 | `IDEMPOTENCY_KEY_REUSED` | 같은 키에 다른 요청 | 없음 |
| 409 | `PREVIOUS_GRADE_NOT_MAXED` | 10강 전 승급 요청 | 없음 |
| 409 | `MAX_GROWTH_REACHED` | 전설 10강에서 성장 요청 | 없음 |
| 409 | `INSUFFICIENT_BOOKS` | 스킬북 부족 | 없음 |
| 409 | `INSUFFICIENT_RICE` | 쌀 부족 | 없음 |
| 409 | `CONTENT_VERSION_MISMATCH` | 콘텐츠 버전 불일치 | 없음 |
| 503 | `SKILL_STATE_SYNCING` | 스킬 상태 동기화 중 | 없음 |

---

# API 3. 액티브 장착 순서 저장

## 8. PUT `/api/v1/skills/loadout`

액티브 스킬 슬롯과 자동 사용 우선순위를 전체 교체한다.

### 8.1 Request

```http
PUT /api/v1/skills/loadout
Idempotency-Key: 713a09ac-82d0-45e9-bf37-d22222222222
If-Match: 18
Content-Type: application/json
```

```json
{
  "skillIds": [
    "active_haste",
    "active_heavy",
    "active_dot"
  ]
}
```

### 8.2 Request 규칙

- 배열 순서가 슬롯 1부터의 우선순위다.
- 빈 배열을 허용한다.
- 최대 4개다.
- 중복 ID를 허용하지 않는다.
- 패시브 ID를 허용하지 않는다.
- 잠긴 액티브를 허용하지 않는다.
- 부분 수정 API 없이 전체 배열을 교체한다.

### 8.3 Response — `200 OK`

```json
{
  "commandId": "01JLOADOUT",
  "replayed": false,
  "skillIds": [
    "active_haste",
    "active_heavy",
    "active_dot"
  ],
  "stateVersion": 19
}
```

### 8.4 Error

| HTTP | code | 조건 |
| ---: | --- | --- |
| 400 | `LOADOUT_TOO_LARGE` | 5개 이상 |
| 400 | `LOADOUT_DUPLICATE` | 같은 스킬 중복 |
| 400 | `LOADOUT_PASSIVE_NOT_ALLOWED` | 패시브 포함 |
| 409 | `LOADOUT_SKILL_LOCKED` | 잠긴 액티브 포함 |
| 409 | `STATE_VERSION_CONFLICT` | If-Match 불일치 |
| 409 | `IDEMPOTENCY_KEY_REUSED` | 같은 키에 다른 요청 |

저장에 실패하면 기존 서버 확정 순서는 유지한다.

---

## 9. stateVersion과 동시성

- 스킬 성장 또는 장착 순서가 변경되면 `stateVersion`을 1 증가시킨다.
- 조회 응답과 변경 성공 응답은 최신 `stateVersion`을 포함한다.
- 클라이언트는 마지막으로 조회한 값을 `If-Match`로 보낸다.
- 충돌이 발생하면 서버는 명령을 적용하지 않는다.
- 클라이언트는 스킬 상태를 다시 조회한다.
- 성장 명령과 장착 명령은 같은 계정의 스킬 상태에 대해 순서를 보장한다.

---

## 10. 멱등성과 재시도

| 상황 | 클라이언트 처리 | 서버 처리 |
| --- | --- | --- |
| 요청 전 연결 실패 | 새 키로 재요청 가능 | 명령 미수신 |
| 요청 후 응답 유실 | 같은 키·같은 본문으로 재시도 | 최초 결과 재생 |
| 같은 키·다른 본문 | 요청 중단 | `IDEMPOTENCY_KEY_REUSED` |
| 결과 미확정 | 같은 키로 재시도 | 중복 차감 금지 |
| stateVersion 충돌 | 상태 재조회 | 명령 미적용 |

멱등 결과 보존 기간은 최소 24시간을 제안한다. 정확한 기간은 서버 공통 API 정책에서 확정한다.

---

## 11. 오류 코드 전체 목록

```text
UNAUTHENTICATED
SKILL_NOT_FOUND
INVALID_GROWTH_ACTION
INVALID_TARGET_GRADE
INVALID_TARGET_LEVEL
PREVIOUS_GRADE_NOT_MAXED
MAX_GROWTH_REACHED
INSUFFICIENT_BOOKS
INSUFFICIENT_RICE
CONTENT_VERSION_MISMATCH
STATE_VERSION_CONFLICT
IDEMPOTENCY_KEY_REUSED
SKILL_STATE_SYNCING
LOADOUT_TOO_LARGE
LOADOUT_DUPLICATE
LOADOUT_PASSIVE_NOT_ALLOWED
LOADOUT_SKILL_LOCKED
```

---

## 12. 미확정 항목

다음 항목은 별도 공통 정책에서 확정한다.

1. 인증 토큰과 세션 전달 방식
2. `commandId`, `traceId` 형식
3. 멱등 결과 보존·삭제 정책
4. 오류 문구 localization 책임 위치
5. 서버 확정 스킬 상태를 로컬 전투 엔진에 반영하는 시점
6. 연결 실패 시 로컬 캐시 재동기화 방식
7. 콘텐츠 버전 교체 중 진행 중 요청 처리 방식

---

## 13. API 요약

| Method | Endpoint | 설명 |
| --- | --- | --- |
| GET | `/api/v1/skills` | 스킬 상태·견적·장착 순서 조회 |
| POST | `/api/v1/skills/{skillId}/growth` | 해금·승급·강화 요청 |
| PUT | `/api/v1/skills/loadout` | 액티브 장착 순서 전체 저장 |
