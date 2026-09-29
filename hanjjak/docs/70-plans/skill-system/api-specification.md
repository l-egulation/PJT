# 스킬 API 명세서

---
doc_kind: plan
owner_domain: skills
authority_level: proposed
status: draft
---

## 1. 목적과 범위

이 문서는 스킬 관리 화면과 서버 사이의 조회·성장·장착 순서 저장 API 계약 초안을 정의한다. 스킬 규칙과 수치는 [스킬 SSOT](../../30-domain/character/skills/ssot.md), 화면 요구사항은 [스킬 시스템 요구사항](./requirements.md), 자산·멱등성 권한은 [계정·저장 SSOT](../../30-domain/player/ssot.md)와 [데이터 권한 경계](../../50-architecture/persistence.md)를 따른다.

포함 API:

- 스킬 목록·성장 견적·장착 상태 조회
- 스킬 해금·승급·강화 명령
- 액티브 4슬롯 장착 순서 저장

제외 API:

- 자동전투의 매 행동 판정
- 스킬북 드롭·거래소 구매
- 콘텐츠 관리와 밸런스 데이터 수정
- 스킬 이름·아이콘·연출 관리

## 2. 공통 계약

### 2.1 기본 경로와 형식

- 기본 경로: `/api/v1`
- 요청·응답 본문: `application/json`
- 날짜·시각: ISO 8601 UTC 문자열
- 비율 단위: 성공률과 실패 보정은 정수 `%` 또는 `%p`
- 쌀과 아이템 수량: 0 이상의 정수
- 스킬·등급·효과 값은 `contentVersion`에 대응하는 버전 콘텐츠를 기준으로 한다.

### 2.2 인증과 계정 식별

- 실제 계정은 인증 컨텍스트에서 식별한다.
- 클라이언트가 본문의 `accountId`를 보내거나 임의 계정을 지정하지 않는다.
- 아래 예시에서는 인증 토큰 형식을 확정하지 않는다.

### 2.3 변경 요청 공통 헤더

해금·승급·강화와 장착 순서 저장 요청에는 다음 헤더가 필수다.

| 헤더 | 형식 | 설명 |
| --- | --- | --- |
| `Idempotency-Key` | UUID 문자열 | 동일 사용자 명령의 재시도 키 |
| `If-Match` | 정수 문자열 | 마지막 조회에서 받은 `stateVersion` |

서버 처리 규칙:

1. 서버는 `Idempotency-Key`와 요청 fingerprint를 `If-Match` 검증보다 먼저 조회한다.
2. 동일 계정·동일 API·동일 `Idempotency-Key`·동일 요청 본문으로 완료된 결과가 있으면 원래 `If-Match`가 오래됐더라도 최초 결과를 그대로 반환한다.
3. 같은 키에 다른 요청 본문을 사용하면 `IDEMPOTENCY_KEY_REUSED`를 반환한다.
4. 처음 보는 키의 `If-Match`가 서버 `stateVersion`과 다르면 명령을 적용하지 않고 `STATE_VERSION_CONFLICT`를 반환한다.
5. 응답 유실 후 재시도할 때 클라이언트는 같은 키를 사용한다.

### 2.4 공통 오류 응답

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
  "traceId": "01J..."
}
```

| 필드 | 형식 | 필수 | 설명 |
| --- | --- | --- | --- |
| `error.code` | 문자열 enum | 예 | 클라이언트 분기용 안정 코드 |
| `error.message` | 문자열 | 예 | 사용자 표시 가능 기본 문구 |
| `error.retryable` | boolean | 예 | 같은 요청을 재시도할 수 있는지 여부 |
| `error.details` | object | 아니오 | 오류별 구조화 정보 |
| `stateVersion` | 정수 | 아니오 | 서버가 알고 있는 최신 상태 버전 |
| `traceId` | 문자열 | 예 | 서버 로그 추적 식별자 |

## 3. 공통 타입

### 3.1 식별자와 enum

```text
SkillId =
  active_heavy |
  active_dot |
  active_haste |
  active_basic_amp |
  passive_critical |
  passive_all_damage

SkillKind = ATTACK_ACTIVE | BUFF_ACTIVE | PASSIVE
SkillGrade = NORMAL | RARE | HEROIC | LEGENDARY
GrowthAction = UNLOCK | PROMOTE | ENHANCE
GrowthOutcome = SUCCESS | FAILURE
```

### 3.2 진행 상태

```json
{
  "unlocked": true,
  "grade": "NORMAL",
  "level": 4
}
```

잠긴 스킬은 다음처럼 표현한다.

```json
{
  "unlocked": false,
  "grade": null,
  "level": 0
}
```

### 3.3 성장 견적

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
      "grade": "NORMAL",
      "required": 1,
      "owned": 3,
      "itemId": "skillbook:active_heavy:normal"
    }
  ],
  "executable": true,
  "blockedReason": null
}
```

`blockedReason` enum:

- `SKILL_LOCKED`
- `PREVIOUS_GRADE_NOT_MAXED`
- `INVALID_TARGET_LEVEL`
- `MAX_GROWTH_REACHED`
- `INSUFFICIENT_BOOKS`
- `INSUFFICIENT_RICE`
- `STATE_SYNC_REQUIRED`

## 4. 스킬 목록·상태 조회

### 4.1 엔드포인트

```http
GET /api/v1/skills
```

### 4.2 요청

본문 없음.

선택 query:

| 이름 | 형식 | 설명 |
| --- | --- | --- |
| `contentVersion` | 문자열 | 생략 시 서버의 현재 적용 버전 사용 |

### 4.3 성공 응답 `200 OK`

```json
{
  "contentVersion": "v1",
  "stateVersion": 17,
  "riceOwned": 10000,
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
      "stateAuthority": {
        "assetAuthority": "SERVER",
        "progressAuthority": "SERVER"
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
            "grade": "NORMAL",
            "required": 1,
            "owned": 3,
            "itemId": "skillbook:active_heavy:normal"
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

### 4.4 조회 규칙

- 항상 스킬 6종을 반환한다. 잠긴 스킬도 목록에 포함한다.
- 패시브의 `activeSlot`은 `null`이고 해금됐다면 `passiveApplied=true`다.
- 액티브의 `activeSlot`은 1~4 또는 `null`이다.
- `growthQuote`는 조회 시점의 서버 자산과 실패 보정을 사용한다.
- 최대 전설 10강은 `growthQuote.executable=false`, `blockedReason=MAX_GROWTH_REACHED`다.
- `currentEffect`와 `nextEffect`는 콘텐츠 값을 그대로 표시하기 위한 값이며 클라이언트가 재계산하지 않는다.

## 5. 스킬 성장 명령

해금·승급·강화는 하나의 엔드포인트를 사용한다. 서버가 현재 상태와 목표를 비교해 요청의 `action`이 맞는지 검증한다.

### 5.1 엔드포인트

```http
POST /api/v1/skills/{skillId}/growth
Idempotency-Key: 47f8...
If-Match: 17
```

### 5.2 요청 본문

```json
{
  "action": "ENHANCE",
  "targetGrade": "NORMAL",
  "targetLevel": 5,
  "contentVersion": "v1"
}
```

| 필드 | 형식 | 필수 | 규칙 |
| --- | --- | --- | --- |
| `action` | `GrowthAction` | 예 | 현재 상태에서 가능한 동작과 일치해야 함 |
| `targetGrade` | `SkillGrade` | 예 | 해금은 `NORMAL`, 승급은 바로 다음 등급, 강화는 현재 등급 |
| `targetLevel` | 1~10 정수 | 예 | 해금·승급은 1, 강화는 현재 단계+1 |
| `contentVersion` | 문자열 | 예 | 견적을 확인한 콘텐츠 버전 |

클라이언트는 비용·확률·seed·성공 여부를 보내지 않는다.

### 5.3 성공 응답 `200 OK`

성공 판정 예시:

```json
{
  "commandId": "01J...",
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
        "grade": "NORMAL",
        "required": 1,
        "owned": 0,
        "itemId": "skillbook:active_heavy:normal"
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

실패 판정 예시:

```json
{
  "commandId": "01J...",
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
        "grade": "NORMAL",
        "required": 1,
        "owned": 0,
        "itemId": "skillbook:active_heavy:normal"
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

### 5.4 권한과 성장 처리 규칙

- 거래 가능한 스킬북·쌀, 스킬 해금·등급·단계, 목표별 실패 보정과 액티브 장착 순서는 서버가 최종 권한을 가진다.
- 클라이언트와 로컬 전투 엔진의 스킬 상태는 서버 확정 상태의 캐시다.
- `stateVersion`은 서버가 발급하고 성장 또는 장착 순서 변경 시 증가시킨다.
- 해금·승급은 100% 성공하고 `outcome=SUCCESS`만 반환한다.
- 책·쌀 차감, 성공 판정, 진행 변경, 실패 보정 변경, `stateVersion` 증가와 멱등 결과 저장은 하나의 서버 트랜잭션에서 처리한다.
- 실패 보정은 `skillId + grade + targetLevel` 단위다.
- 성공 시 해당 목표의 실패 보정만 0으로 초기화한다.
- `finalSuccessPercent=100`이면 확정 성공한다.
- 같은 요청을 재생한 응답은 `replayed=true`이고 `commandId`, 소비량과 결과가 최초 응답과 같다.

### 5.6 오류 응답

| HTTP | 코드 | 조건 | 자산 변경 |
| ---: | --- | --- | --- |
| 400 | `INVALID_GROWTH_ACTION` | 현재 상태와 `action` 불일치 | 없음 |
| 400 | `INVALID_TARGET_GRADE` | 건너뛴 승급 또는 현재 등급과 불일치 | 없음 |
| 400 | `INVALID_TARGET_LEVEL` | 한 단계 초과 강화 등 | 없음 |
| 401 | `UNAUTHENTICATED` | 인증 실패 | 없음 |
| 404 | `SKILL_NOT_FOUND` | 콘텐츠에 없는 `skillId` | 없음 |
| 409 | `STATE_VERSION_CONFLICT` | `If-Match` 불일치 | 없음 |
| 409 | `IDEMPOTENCY_KEY_REUSED` | 같은 키에 다른 요청 | 없음 |
| 409 | `PREVIOUS_GRADE_NOT_MAXED` | 10강 전 승급 요청 | 없음 |
| 409 | `MAX_GROWTH_REACHED` | 전설 10강에서 요청 | 없음 |
| 409 | `INSUFFICIENT_BOOKS` | 요구 책 부족 | 없음 |
| 409 | `INSUFFICIENT_RICE` | 쌀 부족 | 없음 |
| 409 | `CONTENT_VERSION_MISMATCH` | 견적 버전이 현재 적용 버전과 다름 | 없음 |
| 503 | `SKILL_STATE_SYNCING` | 서버 자산 동기화 중 | 없음 |

## 6. 액티브 장착 순서 저장

### 6.1 엔드포인트

```http
PUT /api/v1/skills/loadout
Idempotency-Key: 713a...
If-Match: 18
```

### 6.2 요청 본문

```json
{
  "skillIds": [
    "active_haste",
    "active_heavy",
    "active_dot"
  ]
}
```

- 배열 순서가 슬롯 1부터의 자동 사용 우선순위다.
- 빈 배열을 허용한다.
- 최대 4개다.
- 중복 ID, 패시브 ID와 잠긴 액티브 ID를 허용하지 않는다.
- 전체 배열을 한 번에 교체하며 부분 수정 API는 제공하지 않는다.

### 6.3 성공 응답 `200 OK`

```json
{
  "commandId": "01J...",
  "replayed": false,
  "skillIds": [
    "active_haste",
    "active_heavy",
    "active_dot"
  ],
  "stateVersion": 19
}
```

### 6.4 오류 응답

| HTTP | 코드 | 조건 |
| ---: | --- | --- |
| 400 | `LOADOUT_TOO_LARGE` | 5개 이상 |
| 400 | `LOADOUT_DUPLICATE` | 같은 스킬 중복 |
| 400 | `LOADOUT_PASSIVE_NOT_ALLOWED` | 패시브 포함 |
| 409 | `LOADOUT_SKILL_LOCKED` | 잠긴 액티브 포함 |
| 409 | `STATE_VERSION_CONFLICT` | `If-Match` 불일치 |
| 409 | `IDEMPOTENCY_KEY_REUSED` | 같은 키에 다른 요청 |

저장 실패 시 기존 서버 확정 순서는 유지된다.

## 7. 상태 버전과 동시성

- `stateVersion`은 계정의 스킬 성장 또는 장착 순서가 변경될 때 1 증가한다.
- 조회 응답과 모든 변경 성공 응답에 최신 `stateVersion`을 포함한다.
- 클라이언트는 변경 명령에 마지막으로 본 값을 `If-Match`로 보낸다.
- 충돌 응답에는 최신 `stateVersion`을 포함하며 클라이언트는 `GET /skills` 후 사용자의 의도를 다시 확인한다.
- 성장 명령과 장착 명령은 같은 계정의 스킬 상태에 대해 순서를 보장한다.

## 8. 멱등성과 재시도 시나리오

| 상황 | 클라이언트 | 서버 |
| --- | --- | --- |
| 요청 전 연결 실패 | 새 키로 새 요청 가능 | 명령 미수신 |
| 요청 후 응답 유실 | 같은 키·같은 본문으로 재시도 | 최초 결과 재생 |
| 동일 키·다른 본문 | 요청 중단 | `IDEMPOTENCY_KEY_REUSED` |
| 결과 미확정 | 같은 키로 결과를 재조회하거나 명령 재시도 | 중복 차감 금지 |
| 상태 버전 충돌 | 상태 재조회 | 명령 미적용 |

멱등 결과의 보존 기간은 최소 24시간을 제안한다. 정확한 운영 보존 기간은 서버 공통 API 정책에서 확정한다.

## 9. 오류 코드 목록

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

클라이언트는 `message` 문자열이 아니라 `code`로 분기한다.

## 10. 미확정 항목

다음은 스킬 API 단독으로 확정하지 않는다.

1. 인증 토큰과 세션 전달 방식
2. `commandId`, `traceId`의 구체 형식
3. 멱등 결과의 24시간 이후 보존·삭제 정책
4. 서버 오류 문구의 localization 책임 위치
5. 서버 확정 스킬 상태를 로컬 전투 엔진에 반영하는 시점과 연결 실패 시 재동기화 방식
6. 콘텐츠 버전 교체 중 진행 중 명령의 처리 방식

이 항목이 결정되면 [네트워크 구조](../../50-architecture/networking.md), [계정·저장 SSOT](../../30-domain/player/ssot.md), [결정 로그](../../80-decisions/README.md)를 함께 갱신한다.

## 11. 추적성

| API | 요구사항 |
| --- | --- |
| `GET /api/v1/skills` | FR-SKL-001, FR-SKL-007, FR-SKL-009, FR-SKL-010, DR-SKL-001~002 |
| `POST /api/v1/skills/{skillId}/growth` | FR-SKL-002~004, FR-SKL-008, DR-SKL-002~003, AC-SKL-001~004 |
| `PUT /api/v1/skills/loadout` | FR-SKL-005~006, FR-SKL-010, DR-SKL-003, AC-SKL-005~006 |
