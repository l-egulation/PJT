---
doc_kind: reference
owner_domain: cosmetics
authority_level: reference
status: superseded-by-integration
source: office-hours
source_file: CHARACTER-COSMETICS-COLLECTION-SPEC.md
snapshot_date: 2026-09-01
integrated_by: ./integration-plan.md
---

> 이 문서는 2026-09-01 Office Hours 입력 원문이다. 현재 구현 기준은 [통합 계획](./integration-plan.md), [치장 SSOT](../../30-domain/cosmetics/ssot.md), [MVP 치장 콘텐츠 v1](../../60-content/cosmetics/mvp-v1.md), [치장 뽑기 요구사항](../cosmetic-gacha/requirements.md)을 따른다. 이 원문 안의 우선순위, API envelope, DB 테이블, 거래·등록·전투 반영 문장은 최신 통합 결정으로 대체될 수 있다.

# 캐릭터·치장·도감 통합 기능 명세

> 작성 기준: 2026-09-01  
> 문서 성격: `/office-hours`에서 확정한 제품 규칙을 Frontend, Backend, DB, QA가 함께 사용하는 상세 소유 문서  
> 상태: DRAFT  
> 우선순위: 이 영역에서는 본 문서가 기존 종합 기획 문서와 이미지 시안보다 우선한다.

---

## 1. 문서 목적과 기준

이 문서는 다음 세 화면과 연결된 도메인 규칙을 하나의 기준으로 정의한다.

- **캐릭터:** 현재 전투 능력치와 출처별 상승량 확인
- **치장:** 도감에 등록해 영구 해금한 외형 확인·미리보기·착용
- **도감:** 전체 치장, 등록 현황, 성급, 세트 진행도와 적용 효과 확인 및 치장 등록

### 1.1 기준 자료 우선순위

1. 본 문서의 확정 규칙
2. 2026-09-01 `/office-hours`에서 확정한 결정
3. `GAME-DESIGN-CURRENT-2026-08-31.md`의 게임·전투 규칙
4. 이미지 시안과 로컬 디자인 이미지

이미지 시안은 정보 배치와 시각 방향을 위한 참고 자료다. 이미지에 등장하는 능력치 이름, 수치, 선택·착용 표시는 기능 규칙의 근거로 사용하지 않는다.

### 1.2 핵심 용어

| 용어 | 정의 |
|---|---|
| 거래 가능 수량 | 뽑기 등으로 획득해 아직 도감에 등록하지 않았고, 거래소 등록·예약 상태도 아닌 치장 아이템 수량 |
| 도감 등록 | 거래 가능한 치장 아이템을 영구 소진해 해당 치장의 누적 등록 수량을 올리는 비가역 동작 |
| 외형 해금 | 치장을 최초 1회 도감에 등록해 인벤토리 수량과 무관하게 영구 착용 권한을 얻는 것 |
| 등록 수량 | 해당 치장을 도감에 소진한 누적 개수. 성급 계산의 원본 데이터 |
| 성급 | 등록 수량과 서버 설정 임계값으로 계산한 치장 성장 단계 |
| 도감 단계 | 서로 다른 치장을 최초 등록한 개수에 따라 결정되는 전체 수집 단계 |
| 세트 성급 | 세트의 6개 구성 치장 성급 중 최솟값. 미등록 구성품은 0성 |
| 보유 효과 | 착용 여부와 무관하게 도감 단계 또는 세트 성급으로 얻는 전투 능력치 |
| 해금 치장 | 최초 등록을 완료해 영구 착용할 수 있는 치장 |

문서와 UI에서 기존의 **보유 치장**이라는 표현이 외형 착용 권한을 뜻할 때는 **해금 치장**으로 표기한다. 거래 가능한 실물 수량과 혼동하지 않는다.

---

## 2. 제품 원칙

1. **착용과 전투 효과를 분리한다.** 착용은 외형만 바꾸며 전투 능력치에 영향을 주지 않는다.
2. **도감 등록은 비가역이다.** 등록한 치장 아이템은 거래 가능 수량에서 사라지고 복구되지 않는다.
3. **최초 등록은 외형을 영구 해금한다.** 이후 거래 가능 수량이 0이어도 착용할 수 있다.
4. **개별 치장은 직접 능력치를 주지 않는다.** 전투 능력치는 도감 단계와 세트 성급에서만 발생한다.
5. **도감 단계는 고유 등록 수로 계산한다.** 중복 등록은 전체 도감 단계가 아니라 해당 치장과 세트의 성급에만 기여한다.
6. **세트 성급은 6부위 최저 성급이다.** 하나라도 미등록이면 세트는 0성이며 효과가 없다.
7. **모든 완성 세트 효과는 동시에 적용한다.** 착용한 세트만 적용하는 방식이 아니다.
8. **서버가 모든 권한과 수치를 계산한다.** Frontend는 보유 여부, 성급, 세트 완성, 최종 능력치를 결정하지 않는다.
9. **핵심 성장 능력치는 공격력·최대 체력·방어 관통이다.** 이미지 시안의 명중, 치명타, 속도 등은 현재 범위에 포함하지 않는다.
10. **밸런스 수치는 설정으로 관리한다.** 성급별 누적 등록 임계값, 최대 성급, 도감 단계 기준과 효과, 세트 성급 효과는 코드 상수로 고정하지 않는다.

---

## 3. 범위

### 3.1 포함

- 로그인 사용자의 캐릭터 기본 정보와 레벨 조회
- 공격력·최대 체력·방어 관통의 출처별 분해 및 최종값 조회
- 해금 치장 목록 및 부위별 필터
- 치장 상세, 착용 상태, 거래 가능 수량, 등록 수량과 성급 조회
- 치장 선택 후 로컬 미리보기
- 해금 치장 착용·변경·해제
- 미등록을 포함한 전체 도감 조회
- 치장 1개 등록 및 다음 성급까지 일괄 등록
- 도감 단계와 적용 효과 조회
- 세트별 구성, 진행도, 세트 성급과 적용 효과 조회
- 모든 도감·세트 효과의 능력치별 종합 상승량 조회
- 등록·착용의 인증, 권한, 동시성 및 오류 처리
- 등록 가능 수량 산정에 필요한 거래소 예약 수량 조회·잠금 연동

### 3.2 제외

- 치장 획득·뽑기 API
- 뽑기 확률, 천장과 가격 정책
- 거래소 등록·구매·판매 API와 매칭 엔진 자체. 단, 도감 등록의 등록 가능 수량 검증에 필요한 예약 수량 조회·잠금 연동은 포함한다.
- 치장 아이템 획득 출처별 지급 로직
- 성급 임계값, 최대 성급, 단계별 실제 능력치 수치의 최종 밸런스 확정
- 염색, 복합 부위 치장, 치장 레이어 우선순위 편집
- 치장 자체의 개별 전투 능력치
- 장착한 치장에 따른 전투 능력치
- 부분 세트 효과

치장은 거래 가능한 아이템으로 다룬다. 이 문서는 기존 종합 기획 문서의 `치장 아이템은 거래하지 않는다`는 이전 범위보다 최신 결정이며, 거래소 UI·매칭 구현은 제외하되 도감 등록이 거래 예약 수량을 이중 소비하지 않도록 필요한 데이터 연동은 현재 범위에 포함한다.

---

## 4. 확정된 도메인 규칙

### 4.1 핵심 능력치

핵심 전투 능력치는 다음 세 가지다.

| 능력치 | 코드 | 역할 |
|---|---|---|
| 공격력 | `ATTACK` | 적을 더 빠르게 처치 |
| 최대 체력 | `MAX_HP` | 한 전투 사이클 동안 생존 |
| 방어 관통 | `DEFENSE_PENETRATION` | 방어력이 있는 적에게 피해 확보 |

각 능력치의 서버 계산 불변식은 다음과 같다.

```text
final = base + level + equipment + collection + set
```

- `base`: 캐릭터의 초기 고정값
- `level`: 레벨 성장으로 증가한 값
- `equipment`: 전투 장비로 증가한 값
- `collection`: 현재 도감 단계로 증가한 값
- `set`: 모든 활성 세트 효과의 합
- `final`: 전투 서버와 화면에서 공통으로 사용하는 최종값

Frontend는 위 항목을 더해 진짜 최종값을 만들지 않는다. `final`은 Backend가 계산해 전달한다. Frontend의 합산은 표시 검증 용도로만 사용할 수 있으며 값이 다르면 서버의 `final`을 표시하고 오류를 기록한다.

### 4.2 치장 부위

치장은 다음 고정 6부위 중 정확히 하나에 속한다.

| 부위 | 코드 |
|---|---|
| 머리 | `HEAD` |
| 상의 | `TOP` |
| 하의 | `BOTTOM` |
| 장갑 | `GLOVES` |
| 신발 | `SHOES` |
| 망토 | `CAPE` |

- 사용자당 부위별 최대 1개를 착용한다.
- 부위는 비워 둘 수 있다.
- 치장 정의에 부위가 고정되므로 착용 요청에서 `slot`을 받지 않는다.
- 새 치장을 착용하면 동일 부위의 기존 치장을 원자적으로 교체한다.
- 전투 장비 슬롯과 치장 슬롯은 별도 네임스페이스다. 치장 머리와 전투 투구는 서로 영향을 주지 않는다.
- 현재 범위에서 한 치장은 0개 또는 1개 세트에만 속한다.

### 4.3 도감 등록과 외형 해금

```text
치장 획득
→ 거래 가능한 인벤토리 수량 증가
→ 사용자가 거래 보관 또는 도감 등록을 선택
→ 도감 등록 시 거래 가능 아이템 소진
→ 누적 등록 수량 증가
→ 최초 등록이면 외형 영구 해금
→ 현재 성급과 세트 성급 재계산
→ 도감·세트 효과 및 최종 능력치 재계산
```

등록 규칙:

- 등록은 `1개 등록` 또는 `다음 성급까지 등록` 두 방식만 지원한다.
- `다음 성급까지 등록`은 서버가 다음 임계값과 현재 등록 수량의 차이를 계산한다.
- 다음 성급에 필요한 거래 가능 수량이 부족하면 일부만 소진하지 않고 전체 요청을 실패시킨다.
- 거래소에 등록됐거나 거래 예약 중인 수량은 도감에 등록할 수 없다.
- 등록과 인벤토리 차감은 하나의 DB 트랜잭션으로 처리한다.
- 최대 성급에 도달하면 추가 등록을 차단한다. 남은 아이템은 보유하거나 거래할 수 있다.
- 최초 등록 후 외형 해금은 영구적이다. 등록 수량은 감소하지 않는다.

### 4.4 치장 성급

치장 성급은 누적 등록 수량과 서버의 활성 임계값 설정으로 자동 계산한다.

예시일 뿐 확정값이 아니다.

```text
활성 임계값 [1, 3, 7]
등록 수량 0개 → 0성
등록 수량 1~2개 → 1성
등록 수량 3~6개 → 2성
등록 수량 7개 → 3성(최대)
```

계산식:

```text
cosmeticStar = max(star where registeredQuantity >= threshold[star])
```

규칙:

- 승급 버튼과 승급 API는 없다.
- 등록 수량을 승급 재료처럼 별도로 소비하지 않는다.
- 등록 수량이 단일 원본이며 현재 성급은 파생값이다.
- 임계값은 1성부터 엄격히 증가해야 한다.
- 1성 임계값은 반드시 1이다. 최초 등록과 외형 해금이 같은 시점에 발생한다.
- 최대 성급은 활성 임계값 목록의 마지막 성급이다.
- 정식 운영 후 임계값 상향으로 기존 사용자의 성급이 하락하는 변경은 금지한다. 상향이 필요하면 별도 마이그레이션 또는 기존 사용자 보존 정책을 먼저 승인해야 한다.

### 4.5 도감 단계

도감 단계는 `registeredQuantity >= 1`인 서로 다른 치장 수로 계산한다.

```text
uniqueRegisteredCount = count(cosmetics where registeredQuantity >= 1)
collectionLevel = highest milestone where uniqueRegisteredCount >= requiredUniqueCount
```

- 동일 치장의 중복 등록은 `uniqueRegisteredCount`를 올리지 않는다.
- 도감 단계 효과는 공격력·최대 체력·방어 관통의 묶음이다.
- 활성 단계 한 개가 현재까지의 **누적 총 효과**를 나타낸다. 이전 단계 효과를 다시 합산하지 않는다.
- 다음 단계까지 필요한 고유 치장 수를 응답한다.
- 도감 단계 임계값과 효과는 서버 설정으로 관리한다.

### 4.6 세트 성급과 효과

각 세트는 6부위별 치장 하나로 구성한다.

```text
setStar = min(HEAD, TOP, BOTTOM, GLOVES, SHOES, CAPE 구성품의 cosmeticStar)
```

- 미등록 구성품의 성급은 0이다.
- 하나라도 0성이면 세트 성급은 0이고 효과가 없다.
- 예: `2·2·2·2·2·1성 → 세트 1성`
- 예: `3·3·3·3·3·0성 → 세트 0성, 효과 없음`
- 세트 성급 효과 행은 해당 성급까지의 **누적 총 효과**를 나타낸다. 2성 효과에 1성 효과를 다시 더하지 않는다.
- 완성한 모든 세트의 현재 성급 효과를 합산한다.
- 세트 효과는 착용 상태와 무관하다.

### 4.7 종합 상승량

`종합 상승량`은 단일 전투력 점수가 아니다. 단위가 다른 능력치를 임의로 더하지 않는다.

```json
{
  "attack": 20,
  "maxHp": 300,
  "defensePenetration": 4
}
```

다음 세 묶음을 제공한다.

- 도감 단계 상승량
- 전체 활성 세트 상승량
- 도감 단계 + 전체 세트의 총 상승량

---

## 5. 화면별 기능 명세

## 5.1 캐릭터 탭

### 목적

사용자가 현재 전투 능력치와 그 수치가 어디서 왔는지 확인한다.

### 표시 데이터

- 캐릭터 이름과 이미지
- 현재 레벨
- 필요하면 현재 경험치와 다음 레벨 경험치
- 공격력·최대 체력·방어 관통
- 각 능력치의 `base`, `level`, `equipment`, `collection`, `set`, `final`
- 도감 단계 상승량 묶음
- 전체 활성 세트 상승량 묶음
- 도감·세트 총 상승량 묶음
- 능력치 계산 버전과 계산 시각

### 상태

| 상태 | 표시 규칙 |
|---|---|
| 정상 | 서버가 계산한 `final`과 출처별 분해 표시 |
| 효과 없음 | `collection`, `set`을 0으로 명시 |
| 로딩 | 기존 값을 임의로 0으로 바꾸지 않고 로딩 상태 표시 |
| 인증 만료 | 로그인 화면으로 이동하거나 재인증 유도 |
| 조회 실패 | 마지막 값이 있더라도 최신값으로 오인하지 않게 실패 상태 표시 및 재시도 제공 |

### 금지

- 이미지 시안에 있는 명중·치명타·공격속도 등을 확정 능력치처럼 노출하지 않는다.
- Frontend가 최종 능력치를 계산해 서버값을 덮어쓰지 않는다.
- 세 능력치를 더한 단일 종합 점수를 만들지 않는다.

## 5.2 치장 탭

### 목적

사용자가 영구 해금한 외형만 확인하고, 부위별로 선택·미리보기·착용·해제한다.

### 목록 기준

- `unlocked = true`인 치장만 표시한다.
- 미등록 치장은 치장 탭에 표시하지 않는다.
- 부위별 필터를 제공한다.
- 각 카드에 이름, 부위, 현재 성급, 착용 여부를 표시한다.
- 거래 가능 수량은 착용 조건이 아니므로 목록 노출 조건에 사용하지 않는다.

### 선택·미리보기·확정 흐름

```text
해금 치장 선택
→ 좌측 캐릭터에 로컬 미리보기
→ 서버의 실제 착용 상태는 아직 변경하지 않음
→ 사용자가 착용하기 선택
→ Backend가 해금 여부와 부위를 검증
→ 성공 응답 후 실제 착용 배지와 저장 상태 갱신
```

- 미확정 미리보기는 탭 이동 또는 화면 닫기 시 버린다.
- 선택 치장과 착용 중 치장을 서로 다른 상태로 표시한다.
- 착용 요청 실패 시 실제 착용 상태는 유지하고 미리보기와 실패 이유를 표시한다.
- 현재 착용 중인 치장을 다시 착용하는 요청은 성공한 멱등 동작으로 처리할 수 있다.
- 각 부위에는 `해제하기`를 제공한다.
- 해제 성공 후 해당 부위는 기본 외형으로 표시한다.

### 상태

- 해금 치장 없음
- 부위에 해금 치장 없음
- 선택됨
- 미리보기 중
- 착용 중
- 착용 요청 중
- 착용 성공
- 착용 실패
- 해제 요청 중
- 해제 성공·실패

## 5.3 도감 탭

### 목적

사용자가 게임에 존재하는 전체 치장을 보고, 등록·성급·세트 수집 진행과 실제 적용 효과를 확인한다.

### 목록 기준

- 미등록을 포함한 전체 치장을 표시한다.
- 해금/미해금, 등록 수량, 현재 성급, 다음 성급 진행, 거래 가능 수량을 구분한다.
- 부위, 세트, 등록 상태로 필터링할 수 있다.
- 정렬 기본값은 서버의 `displayOrder`다.

### 치장 상세

- 이름, 설명, 부위, 세트, 이미지
- 외형 해금 여부
- 누적 등록 수량
- 현재 성급과 최대 성급
- 다음 성급 임계값과 추가 필요 수량
- 거래 가능 수량
- 거래소 등록·예약 수량은 등록 가능 수량에서 제외된다는 안내
- 해당 치장이 포함된 세트와 세트의 6부위 진행도
- `1개 등록`, `다음 성급까지 등록` 행동

### 등록 확인

등록은 비가역이므로 실행 전에 다음을 확인한다.

- 소진할 치장 이름
- 소진 수량
- 등록 전·후 등록 수량
- 등록 전·후 성급
- 등록 후 남는 거래 가능 수량
- 등록한 아이템은 거래할 수 없고 복구되지 않는다는 문구

최대 성급에서는 두 등록 버튼을 비활성화하고 `최대 성급`을 표시한다.

### 세트 표시

- 세트명
- 6개 구성 치장과 각 성급
- 등록한 부위 수 `N/6`
- 현재 세트 성급
- 다음 세트 성급을 막고 있는 최저 성급 부위
- 현재 적용 효과
- 다음 성급 효과 미리보기
- 0성이면 `미완성 · 효과 없음`

### 전체 효과 표시

- 현재 도감 단계와 `고유 등록 수 / 전체 치장 수`
- 다음 도감 단계까지 필요한 고유 치장 수
- 도감 단계 상승량 묶음
- 활성 세트 목록과 세트별 상승량
- 전체 세트 상승량 묶음
- 도감 + 세트 총 상승량 묶음

---

## 6. API 명세

### 6.1 공통 규칙

- Base path: `/api/v1`
- 모든 API는 로그인 세션 또는 액세스 토큰이 필요하다.
- 사용자 식별자는 요청 본문이나 경로에서 받지 않고 인증 정보에서 결정한다.
- 숫자 ID는 예시이며 실제 프로젝트의 ID 규칙을 일관되게 적용한다.
- 시간은 ISO 8601 UTC 문자열로 응답한다.
- 등록 API는 `Idempotency-Key` 헤더를 필수로 받는다.
- 캐릭터와 도감 스냅샷은 반드시 같은 활성 밸런스 번들의 `balanceConfigVersion`을 응답한다.
- 오류 응답은 `code`, `message`, `requestId`, 필요하면 `details`를 포함한다.

공통 능력치 묶음:

```json
{
  "attack": 20,
  "maxHp": 300,
  "defensePenetration": 4
}
```

### 6.2 캐릭터 능력치 조회

```http
GET /api/v1/character
```

응답 예시:

```json
{
  "character": {
    "name": "한짝",
    "level": 24,
    "experience": {
      "current": 18720,
      "requiredForNextLevel": 30000,
      "isMaxLevel": false
    }
  },
  "stats": {
    "attack": {
      "base": 10,
      "level": 100,
      "equipment": 36,
      "collection": 5,
      "set": 3,
      "final": 154
    },
    "maxHp": {
      "base": 100,
      "level": 2500,
      "equipment": 600,
      "collection": 200,
      "set": 100,
      "final": 3500
    },
    "defensePenetration": {
      "base": 0,
      "level": 0,
      "equipment": 10,
      "collection": 2,
      "set": 1,
      "final": 13
    }
  },
  "cosmeticBonuses": {
    "collection": { "attack": 5, "maxHp": 200, "defensePenetration": 2 },
    "sets": { "attack": 3, "maxHp": 100, "defensePenetration": 1 },
    "total": { "attack": 8, "maxHp": 300, "defensePenetration": 3 }
  },
  "calculationVersion": "stats-2026-09-01-1",
  "balanceConfigVersion": "cosmetic-balance-2026-09-01-1",
  "calculatedAt": "2026-09-01T12:00:00Z"
}
```

검증:

```text
stats.attack.final
= stats.attack.base
+ stats.attack.level
+ stats.attack.equipment
+ stats.attack.collection
+ stats.attack.set
```

같은 불변식을 세 능력치 모두에 적용한다.

### 6.3 해금 치장 목록 조회

```http
GET /api/v1/cosmetics/unlocked?slot=HEAD
```

쿼리:

| 이름 | 필수 | 설명 |
|---|---|---|
| `slot` | 아니오 | 6개 치장 부위 |
| `cursor` | 아니오 | 다음 페이지 커서 |
| `limit` | 아니오 | 서버 허용 범위 내 페이지 크기 |

응답 예시:

```json
{
  "items": [
    {
      "cosmeticId": 101,
      "name": "별빛 마법사 모자",
      "slot": "HEAD",
      "thumbnailUrl": "/assets/cosmetics/101.png",
      "star": 2,
      "maxStar": 5,
      "equipped": true
    }
  ],
  "equippedBySlot": {
    "HEAD": 101,
    "TOP": null,
    "BOTTOM": null,
    "GLOVES": null,
    "SHOES": null,
    "CAPE": null
  },
  "nextCursor": null
}
```

### 6.4 치장 상세 조회

```http
GET /api/v1/cosmetics/{cosmeticId}
```

응답 예시:

```json
{
  "cosmeticId": 101,
  "name": "별빛 마법사 모자",
  "description": "별빛을 담은 마법사 모자",
  "slot": "HEAD",
  "set": {
    "setId": 10,
    "name": "별빛 마법사 세트"
  },
  "assets": {
    "thumbnailUrl": "/assets/cosmetics/101.png",
    "previewLayerUrl": "/assets/cosmetics/101-layer.png"
  },
  "collection": {
    "unlocked": true,
    "registeredQuantity": 3,
    "star": 2,
    "maxStar": 5,
    "nextStar": 3,
    "nextStarThreshold": 7,
    "quantityNeededForNextStar": 4
  },
  "inventory": {
    "totalQuantity": 6,
    "listedOrReservedQuantity": 2,
    "registerableQuantity": 4,
    "inventoryVersion": 17
  },
  "equipped": true
}
```

타인의 보유 상태를 조회하는 파라미터는 제공하지 않는다.

### 6.5 치장 착용

```http
PUT /api/v1/cosmetics/{cosmeticId}/equip
```

요청 본문은 없다. 서버가 치장 정의에서 부위를 결정한다.

응답 예시:

```json
{
  "changedSlot": "HEAD",
  "previousCosmeticId": 88,
  "equippedCosmeticId": 101,
  "equippedBySlot": {
    "HEAD": 101,
    "TOP": 202,
    "BOTTOM": 303,
    "GLOVES": null,
    "SHOES": 505,
    "CAPE": null
  },
  "updatedAt": "2026-09-01T12:00:00Z"
}
```

서버 검증:

1. 인증 사용자 확인
2. 치장 존재와 `equippable=true` 확인. `acquirable=false` 또는 `registrable=false`는 이미 해금한 사용자의 착용 권한을 막지 않는다.
3. 사용자의 최초 등록 여부 확인
4. 치장의 고정 부위 확인
5. 동일 부위 기존 착용을 새 치장으로 원자적 교체

### 6.6 치장 해제

```http
DELETE /api/v1/cosmetics/slots/{slot}/equip
```

응답 예시:

```json
{
  "changedSlot": "HEAD",
  "previousCosmeticId": 101,
  "equippedCosmeticId": null,
  "updatedAt": "2026-09-01T12:01:00Z"
}
```

이미 빈 부위를 해제해도 성공한 멱등 응답을 반환한다.

### 6.7 도감 조회

```http
GET /api/v1/collection?slot=HEAD&unlockState=ALL&setId=10
```

도감 효과 조회를 별도 API로 분리하지 않는다. 도감 화면에 필요한 단계·세트·종합 효과를 같은 스냅샷으로 반환한다.

쿼리:

| 이름 | 필수 | 값 |
|---|---|---|
| `slot` | 아니오 | 치장 부위 |
| `unlockState` | 아니오 | `ALL`, `UNLOCKED`, `LOCKED` |
| `setId` | 아니오 | 세트 필터 |
| `cursor` | 아니오 | 목록 커서 |
| `limit` | 아니오 | 페이지 크기 |

응답 예시:

```json
{
  "summary": {
    "uniqueRegisteredCount": 18,
    "totalCosmeticCount": 60,
    "collectionLevel": 3,
    "nextCollectionLevel": 4,
    "uniqueCountNeededForNextLevel": 7,
    "bonuses": {
      "collection": { "attack": 5, "maxHp": 200, "defensePenetration": 2 },
      "sets": { "attack": 3, "maxHp": 100, "defensePenetration": 1 },
      "total": { "attack": 8, "maxHp": 300, "defensePenetration": 3 }
    }
  },
  "activeCollectionMilestone": {
    "level": 3,
    "requiredUniqueCount": 15,
    "effect": { "attack": 5, "maxHp": 200, "defensePenetration": 2 }
  },
  "sets": [
    {
      "setId": 10,
      "name": "전집 조리사 세트",
      "registeredParts": 6,
      "totalParts": 6,
      "setStar": 1,
      "effectActive": true,
      "currentEffect": { "attack": 3, "maxHp": 100, "defensePenetration": 1 },
      "nextSetStar": 2,
      "blockingParts": [
        { "slot": "BOTTOM", "cosmeticId": 103, "currentStar": 1, "requiredStar": 2 }
      ],
      "nextEffect": { "attack": 6, "maxHp": 200, "defensePenetration": 2 },
      "parts": [
        { "slot": "HEAD", "cosmeticId": 101, "unlocked": true, "star": 2 },
        { "slot": "TOP", "cosmeticId": 102, "unlocked": true, "star": 2 },
        { "slot": "BOTTOM", "cosmeticId": 103, "unlocked": true, "star": 1 },
        { "slot": "GLOVES", "cosmeticId": 104, "unlocked": true, "star": 2 },
        { "slot": "SHOES", "cosmeticId": 105, "unlocked": true, "star": 2 },
        { "slot": "CAPE", "cosmeticId": 106, "unlocked": true, "star": 2 }
      ]
    }
  ],
  "items": [
    {
      "cosmeticId": 101,
      "name": "전집 조리사 두건",
      "slot": "HEAD",
      "setId": 10,
      "unlocked": true,
      "registeredQuantity": 3,
      "inventoryVersion": 17,
      "star": 2,
      "maxStar": 5,
      "nextStar": 3,
      "quantityNeededForNextStar": 4,
      "registerableQuantity": 4,
      "equipped": false,
      "displayOrder": 10
    }
  ],
  "nextCursor": null,
  "collectionRevision": 42,
  "balanceConfigVersion": "cosmetic-balance-2026-09-01-1",
  "calculatedAt": "2026-09-01T12:00:00Z"
}
```

목록을 페이지네이션하더라도 `summary`와 `sets`는 필터에 잘린 부분집합이 아니라 사용자의 전체 도감 상태를 기준으로 계산한다.

### 6.8 도감 등록

```http
POST /api/v1/collection/cosmetics/{cosmeticId}/registrations
Idempotency-Key: 4f672a53-...
Content-Type: application/json
```

요청:

```json
{
  "mode": "ONE",
  "expectedCollectionRevision": 42,
  "expectedInventoryVersion": 17,
  "expectedBalanceConfigVersion": "cosmetic-balance-2026-09-01-1"
}
```

또는:

```json
{
  "mode": "TO_NEXT_STAR",
  "expectedCollectionRevision": 42,
  "expectedInventoryVersion": 17,
  "expectedBalanceConfigVersion": "cosmetic-balance-2026-09-01-1"
}
```

클라이언트가 임의 `quantity`를 보내지 않는다. 서버가 모드에 따라 소진량을 결정한다.

`expectedCollectionRevision`은 사용자가 확인한 도감 스냅샷 버전이고, `expectedInventoryVersion`은 해당 치장의 거래 가능 수량을 확인한 인벤토리 버전이며, `expectedBalanceConfigVersion`은 다음 성급 필요 수량을 계산한 밸런스 번들 버전이다. 서버는 멱등 키 확인 후, 수량을 소진하기 전에 세 값을 모두 현재 상태와 비교한다. 하나라도 다르면 `COLLECTION_STATE_CHANGED`로 실패시키고 최신 도감 조회를 요구한다. 이 비교가 없으면 사용자가 본 수량·필요 수량·효과와 다른 조건으로 아이템을 소진할 수 있으므로 등록 요청에서 생략할 수 없다.

성공 응답:

```json
{
  "cosmeticId": 101,
  "mode": "TO_NEXT_STAR",
  "consumedQuantity": 4,
  "before": {
    "registeredQuantity": 3,
    "star": 2,
    "registerableInventoryQuantity": 4
  },
  "after": {
    "registeredQuantity": 7,
    "star": 3,
    "unlocked": true,
    "registerableInventoryQuantity": 0
  },
  "collectionChanged": false,
  "set": {
    "setId": 10,
    "beforeStar": 1,
    "afterStar": 2
  },
  "bonuses": {
    "collection": { "attack": 5, "maxHp": 200, "defensePenetration": 2 },
    "sets": { "attack": 6, "maxHp": 200, "defensePenetration": 2 },
    "total": { "attack": 11, "maxHp": 400, "defensePenetration": 4 }
  },
  "collectionRevision": 43,
  "balanceConfigVersion": "cosmetic-balance-2026-09-01-1",
  "inventoryVersion": 18,
  "registeredAt": "2026-09-01T12:03:00Z"
}
```

`collectionChanged`는 최초 등록으로 고유 등록 수가 증가했는지를 뜻한다.

트랜잭션 순서:

1. 사용자별 도감 상태 행을 잠근다.
2. 사용자와 치장 정의를 확인하고 `registrable=true`인지 검증한다.
3. 동일 사용자·치장 인벤토리 행 또는 거래소 예약 원장을 잠근다.
4. 이미 처리된 `Idempotency-Key`인지 확인한다.
5. 현재 사용자 도감 리비전이 `expectedCollectionRevision`과 같은지 확인한다.
6. 현재 인벤토리 버전이 `expectedInventoryVersion`과 같은지 확인한다.
7. 현재 활성 밸런스 번들이 `expectedBalanceConfigVersion`과 같은지 확인한다.
8. 활성 밸런스 번들과 최대 성급을 확인한다.
9. 모드에 따른 소진량을 계산한다.
10. 거래소 등록·예약을 제외한 등록 가능 수량을 검증한다.
11. 인벤토리 수량을 차감하고 인벤토리 버전을 증가시킨다.
12. 누적 등록 수량을 증가시킨다.
13. 사용자별 도감 리비전을 증가시킨다.
14. 치장 성급, 도감 단계, 해당 세트 성급과 효과를 재계산한다.
15. 멱등 응답을 저장한 뒤 커밋한다.

---

## 7. 오류 명세

공통 오류 형식:

```json
{
  "code": "COSMETIC_NOT_UNLOCKED",
  "message": "도감에 등록해 해금한 치장만 착용할 수 있습니다.",
  "requestId": "req_...",
  "details": {}
}
```

| HTTP | 코드 | 조건 | Frontend 처리 |
|---:|---|---|---|
| 401 | `UNAUTHENTICATED` | 로그인 정보 없음·만료 | 재인증 유도 |
| 403 | `COSMETIC_NOT_UNLOCKED` | 미등록 치장 착용 요청 | 실제 착용 상태 유지, 도감 등록 안내 |
| 404 | `COSMETIC_NOT_FOUND` | 존재하지 않는 `cosmeticId` | 상세 닫기 또는 목록 새로고침 |
| 409 | `COSMETIC_NOT_REGISTRABLE` | 현재 도감 등록이 중지된 치장 등록 요청 | 소진 없이 최신 상세 상태 반영 |
| 409 | `INSUFFICIENT_REGISTERABLE_QUANTITY` | 다음 성급 또는 1개 등록 수량 부족 | 최신 거래 가능 수량 표시 |
| 409 | `COSMETIC_QUANTITY_RESERVED` | 수량이 거래소 등록·거래 예약 상태 | 거래 상태 확인 안내 |
| 409 | `MAX_STAR_REACHED` | 최대 성급 치장 추가 등록 | 등록 버튼 비활성화 및 최신 상태 반영 |
| 409 | `COLLECTION_STATE_CHANGED` | 오래된 도감 리비전, 인벤토리 버전 또는 밸런스 버전으로 등록 요청 | 도감 재조회 후 확인 내용 재표시 |
| 409 | `IDEMPOTENCY_KEY_REUSED` | 같은 키로 다른 요청 본문 사용 | 요청 중단 및 오류 기록 |
| 422 | `INVALID_REGISTRATION_MODE` | 허용하지 않는 등록 모드 | 클라이언트 오류 기록 |
| 422 | `INVALID_COSMETIC_SLOT` | 정의 데이터의 부위가 유효하지 않음 | 일반 오류, 운영 알림 |
| 503 | `COSMETIC_CONFIG_UNAVAILABLE` | 활성 임계값·효과 설정 없음 | 등록 차단, 조회는 안전한 오류 상태 표시 |

다른 사용자의 치장 정보에 접근할 수 있는 API 경로 자체를 제공하지 않는다. 내부 관리 API가 생기면 별도 관리자 권한과 감사 로그를 적용한다.

---

## 8. DB 모델과 제약

실제 테이블명은 프로젝트 규칙에 맞출 수 있으나 다음 책임과 제약은 유지한다.

### 8.1 치장 마스터 `cosmetic`

| 필드 | 설명 |
|---|---|
| `id` | 치장 ID |
| `name` | 표시 이름 |
| `description` | 상세 설명 |
| `slot` | 고정 6부위 |
| `display_order` | 기본 정렬 |
| `visible` | 도감과 목록 노출 가능 여부 |
| `acquirable` | 신규 획득 가능 여부 |
| `registrable` | 도감 등록 가능 여부 |
| `equippable` | 해금 사용자의 착용 가능 여부 |
| `thumbnail_asset` | 목록 이미지 |
| `preview_layer_asset` | 캐릭터 합성 레이어 |

제약:

- `slot IN (HEAD, TOP, BOTTOM, GLOVES, SHOES, CAPE)`
- 세트 소속의 단일 원본은 `cosmetic_set_member`다. `cosmetic` 테이블에는 `set_id`를 중복 저장하지 않는다.
- `acquirable=false` 또는 `registrable=false`는 신규 획득·등록만 막는다. 기존 `user_cosmetic_collection` 기록과 외형 해금은 유지한다.
- `equippable=false`는 저작권, 운영 사고, 금칙 표현 등 안전상 회수에 준하는 예외 상황에서만 사용한다. 이 경우 기존 착용자는 해제되며 공지·보상·마이그레이션 정책을 별도 승인해야 한다.

### 8.2 치장 세트 `cosmetic_set`, `cosmetic_set_member`

`cosmetic_set_member` 제약:

- `(set_id, slot)` unique
- `(set_id, cosmetic_id)` unique
- 세트 활성화 전 정확히 6부위가 하나씩 있어야 한다.
- 구성 치장의 실제 `slot`과 멤버의 `slot`이 일치해야 한다.
- 치장의 세트 소속 조회는 항상 이 테이블을 사용한다. 치장 마스터에 파생 `set_id`를 캐시하더라도 쓰기 원본으로 사용하지 않으며, 캐시는 같은 트랜잭션 또는 재생성 작업으로 일치시켜야 한다.

### 8.3 거래 가능 인벤토리 `user_cosmetic_inventory`

| 필드 | 설명 |
|---|---|
| `user_id` | 인증 사용자 |
| `cosmetic_id` | 치장 |
| `quantity` | 현재 실물 수량 |
| `reserved_quantity` | 거래소 등록·거래 예약 수량 |
| `version` | 낙관적 잠금 또는 변경 버전 |

제약:

- `(user_id, cosmetic_id)` primary 또는 unique
- `quantity >= 0`
- `reserved_quantity >= 0`
- `reserved_quantity <= quantity`
- `registerableQuantity = quantity - reserved_quantity`

거래소가 별도 원장으로 예약 수량을 관리하면 `reserved_quantity`를 중복 저장하지 않고 트랜잭션 안에서 해당 원장을 조회·잠근다. 어떤 방식이든 등록 시 거래 중 수량을 소진할 수 없어야 한다.

인벤토리 `version`은 거래 가능 수량이 바뀌거나 예약 수량이 바뀔 때마다 증가한다. 도감 등록 요청의 `expectedInventoryVersion`은 이 값과 비교한다. 거래소가 별도 원장으로 예약을 관리하더라도 동일한 비교에 사용할 수 있는 단조 증가 버전을 제공해야 한다.

### 8.4 사용자 도감 `user_cosmetic_collection`

| 필드 | 설명 |
|---|---|
| `user_id` | 사용자 |
| `cosmetic_id` | 치장 |
| `registered_quantity` | 영구 누적 등록 수량 |
| `first_registered_at` | 외형 해금 시각 |
| `updated_at` | 최근 등록 시각 |

제약:

- `(user_id, cosmetic_id)` primary 또는 unique
- `registered_quantity >= 1`인 행만 존재
- 등록 수량은 일반 기능에서 감소시키지 않는다.
- 사용자당 동일 치장의 도감 행은 하나뿐이다.
- 현재 성급은 등록 수량과 활성 설정에서 계산한다. 조회 최적화를 위해 캐시하더라도 원본은 등록 수량이다.

### 8.5 사용자 도감 상태 `user_cosmetic_collection_state`

도감 등록 확인 화면의 스냅샷을 보호하기 위한 사용자별 상태 행이다.

| 필드 | 설명 |
|---|---|
| `user_id` | 사용자 |
| `collection_revision` | 사용자의 도감 상태 버전 |
| `updated_at` | 최근 변경 시각 |

제약:

- `(user_id)` primary 또는 unique
- 최초 사용자는 `collection_revision = 0`에서 시작한다.
- 어떤 치장이든 도감 등록에 성공할 때마다 트랜잭션 안에서 1 증가한다.
- 등록 트랜잭션은 이 행을 먼저 잠가 같은 사용자의 여러 등록을 직렬화한다.
- `GET /collection`의 `collectionRevision`은 이 값이다.

### 8.6 사용자 착용 상태 `user_cosmetic_equip`

| 필드 | 설명 |
|---|---|
| `user_id` | 사용자 |
| `slot` | 치장 부위 |
| `cosmetic_id` | 착용 치장 |
| `updated_at` | 변경 시각 |

제약:

- `(user_id, slot)` primary 또는 unique
- 같은 사용자가 한 부위에 두 치장을 착용할 수 없다.
- 착용 치장의 정의 부위와 행의 부위가 일치해야 한다.
- `user_cosmetic_collection`에 해당 사용자·치장 행이 있어야 한다.
- 해제는 행 삭제 또는 nullable 값 저장 중 프로젝트 관례 하나를 사용한다. 혼용하지 않는다.

### 8.7 밸런스 설정 번들 `cosmetic_balance_config`

성급 임계값, 도감 단계 효과, 세트 성급 효과는 하나의 활성 번들로 배포한다. 한 API 스냅샷 안에서 서로 다른 설정 버전을 섞지 않는다.

| 필드 | 설명 |
|---|---|
| `config_version` | 밸런스 번들 ID |
| `active_from` | 적용 시작 시각 |
| `active_until` | 적용 종료 시각, nullable |
| `status` | `DRAFT`, `ACTIVE`, `RETIRED` |
| `created_at` | 생성 시각 |

제약:

- 한 시점에 `ACTIVE` 번들은 하나뿐이다.
- 아래 성급 임계값, 도감 단계, 세트 효과 테이블은 모두 같은 `config_version`을 참조한다.
- 설정 검증은 번들 전체 단위로 통과해야 하며, 일부 테이블만 먼저 활성화할 수 없다.
- 조회 응답의 `balanceConfigVersion`은 이 번들의 `config_version`이다.

### 8.8 성급 임계값 설정 `cosmetic_star_threshold`

| 필드 | 설명 |
|---|---|
| `config_version` | 밸런스 설정 버전 |
| `star` | 성급 |
| `required_registered_quantity` | 해당 성급 최소 누적 등록 수 |
| `created_at` | 설정 행 생성 시각 |

제약:

- `(config_version, star)` unique
- 1성 임계값은 1
- 성급이 증가할수록 임계값도 엄격히 증가
- 활성 여부는 `cosmetic_balance_config` 번들에서만 결정한다.

### 8.9 도감 단계 설정 `collection_milestone`

| 필드 | 설명 |
|---|---|
| `config_version` | 설정 버전 |
| `level` | 도감 단계 |
| `required_unique_count` | 필요한 고유 등록 수 |
| `attack` | 해당 단계의 누적 총 공격력 보너스 |
| `max_hp` | 해당 단계의 누적 총 최대 체력 보너스 |
| `defense_penetration` | 해당 단계의 누적 총 관통 보너스 |

단계가 증가할수록 `required_unique_count`가 엄격히 증가해야 한다.
- `config_version`은 `cosmetic_balance_config.config_version`을 참조한다.

### 8.10 세트 성급 효과 `cosmetic_set_star_effect`

| 필드 | 설명 |
|---|---|
| `config_version` | 설정 버전 |
| `set_id` | 세트 |
| `star` | 세트 성급 |
| `attack` | 해당 성급의 누적 총 공격력 보너스 |
| `max_hp` | 해당 성급의 누적 총 최대 체력 보너스 |
| `defense_penetration` | 해당 성급의 누적 총 관통 보너스 |

제약:

- `(config_version, set_id, star)` unique
- 0성 효과 행은 만들지 않거나 모든 값이 0이어야 한다.
- 효과 행이 없는 성급은 활성화할 수 없다. 설정 검증 단계에서 배포를 차단한다.
- `config_version`은 `cosmetic_balance_config.config_version`을 참조한다.

### 8.11 멱등 처리 `idempotency_record`

도감 등록처럼 비가역인 요청은 사용자, HTTP 메서드, `cosmeticId`를 포함한 정규화된 전체 경로, 키, 표준화된 요청 본문 해시, 응답, 상태를 저장한다.

- 같은 사용자·같은 키·같은 메서드·같은 전체 경로·같은 요청 본문은 이전 성공 응답을 반환한다.
- 같은 사용자·같은 키라도 `cosmeticId`, 경로, 메서드 또는 요청 본문이 다르면 `IDEMPOTENCY_KEY_REUSED`로 거절한다.
- 네트워크 재시도로 동일 아이템이 두 번 소진되면 안 된다.

---

## 9. Backend 계산 순서

캐릭터 능력치 조회 시:

```text
1. 캐릭터 기본값과 레벨 기여 조회
2. 전투 장비 기여 계산
3. 고유 등록 수로 현재 도감 단계 결정
4. 현재 도감 단계의 누적 총 효과 조회
5. 세트별 6부위 치장 성급 계산
6. 1성 이상인 모든 세트의 현재 누적 총 효과 합산
7. 능력치별 final 계산
8. 출처별 분해와 final을 같은 응답으로 반환
```

도감 등록 성공 시 영향 범위:

- 거래 가능 인벤토리 수량
- 해당 치장 등록 수량과 성급
- 최초 등록이면 해금 치장 목록과 고유 등록 수
- 해당 치장이 속한 세트의 세트 성급
- 도감 단계 및 도감 효과
- 전체 세트 효과
- 캐릭터 최종 능력치

캐시를 사용하더라도 등록 트랜잭션 완료 뒤 관련 캐시를 무효화해야 한다. 오래된 캐시가 전투 서버와 화면에 서로 다른 최종 능력치를 제공하면 안 된다.

---

## 10. Frontend 책임

- 치장 탭에는 해금 치장만 표시한다.
- 도감 탭에는 미등록을 포함한 전체 치장을 표시한다.
- 선택 상태, 로컬 미리보기, 실제 착용 상태를 분리한다.
- 서버 성공 응답 전 `착용 중` 배지를 이동하지 않는다.
- 미등록 치장에는 착용 행동을 제공하지 않는다.
- 등록 가능 수량과 다음 성급 필요량을 서버 응답으로 표시한다.
- 도감 등록 요청에는 서버가 내려준 `expectedCollectionRevision`, `expectedInventoryVersion`, `expectedBalanceConfigVersion`을 그대로 포함한다.
- 수량 부족, 최대 성급, 거래 예약 상태에서 등록 버튼을 비활성화한다.
- 비활성화만 보안 수단으로 삼지 않는다. Backend 실패 응답을 항상 처리한다.
- 비가역 등록 전에 소진량과 결과를 확인받는다.
- 캐릭터 최종 능력치와 도감·세트 효과를 직접 결정하지 않는다.
- 등록·착용 성공 후 영향받은 탭의 데이터를 무효화하거나 재조회한다.
- 사용자에게 단일 전투력 점수를 임의로 만들지 않는다.

---

## 11. Backend 책임

- 인증 사용자 기준으로만 조회·변경한다.
- 치장 존재, `equippable`, 고정 부위를 검증한다.
- 착용 시 최초 등록과 영구 해금 여부를 검증한다.
- 등록 시 거래 가능 수량과 거래 예약 상태를 잠금·검증한다.
- 등록 시 `registrable=true`, 도감 리비전, 인벤토리 버전, 밸런스 버전을 소진 전에 검증한다.
- 등록 수량 차감과 도감 증가를 원자적으로 처리한다.
- 비가역 등록 요청의 멱등성을 보장한다.
- 성급, 도감 단계, 세트 성급과 모든 효과를 계산한다.
- 공격력·최대 체력·방어 관통의 최종값과 출처별 분해를 계산한다.
- 최대 성급 추가 등록을 거절한다.
- 설정 누락·오류 시 등록을 안전하게 차단한다.
- 다른 사용자 데이터 접근 경로를 차단한다.

---

## 12. QA 인수 조건

### 12.1 캐릭터 능력치

- [ ] 공격력·최대 체력·방어 관통만 핵심 능력치로 표시된다.
- [ ] 각 능력치에 `base`, `level`, `equipment`, `collection`, `set`, `final`이 표시된다.
- [ ] 세 능력치 모두 `final = 각 출처 합`이 성립한다.
- [ ] Frontend 표시값과 전투에 사용되는 Backend 최종값이 일치한다.
- [ ] 도감·세트 효과가 0일 때 0으로 명시된다.
- [ ] 도감 등록으로 효과가 변하면 캐릭터 조회에도 반영된다.

### 12.2 치장 목록과 착용

- [ ] 치장 탭에는 최초 등록해 해금한 치장만 표시된다.
- [ ] 거래 가능 수량이 0이어도 해금한 치장을 착용할 수 있다.
- [ ] 미등록 치장을 직접 착용 API로 요청하면 실패한다.
- [ ] 존재하지 않는 `cosmeticId` 착용 요청이 실패한다.
- [ ] 치장을 선택하면 서버 저장 전 로컬 미리보기만 바뀐다.
- [ ] 착용 성공 후 같은 부위 기존 치장이 교체된다.
- [ ] 착용 실패 시 실제 착용 상태가 유지된다.
- [ ] 부위별 하나만 착용 가능하다.
- [ ] 각 부위를 해제해 빈 상태로 만들 수 있다.
- [ ] 다른 사용자의 해금 정보로 착용할 수 없다.

### 12.3 도감 목록

- [ ] 도감에는 미등록을 포함한 전체 `visible=true` 치장이 표시된다.
- [ ] 해금/미해금 상태가 구분된다.
- [ ] 등록 수량, 성급, 다음 성급 필요량이 정확하다.
- [ ] 고유 등록 수는 최초 등록에서만 증가한다.
- [ ] 동일 치장 중복 등록은 도감 단계 진행 수를 올리지 않는다.
- [ ] 부위·세트·등록 상태 필터가 목록만 바꾸고 전체 요약 효과를 왜곡하지 않는다.

- [ ] 도감 세트 응답에 다음 세트 성급, 최저 성급을 막는 구성품, 다음 효과가 포함된다.
- [ ] 최대 세트 성급에서는 `nextSetStar`, `blockingParts`, `nextEffect`가 각각 `null`, 빈 배열, `null`이다.
- [ ] 세트 소속은 `cosmetic_set_member` 단일 원본에서 조회되며 치장 마스터와 불일치할 수 없다.
### 12.4 도감 등록

- [ ] 1개 등록 시 거래 가능 수량이 1 감소하고 등록 수량이 1 증가한다.
- [ ] 최초 등록 시 외형이 영구 해금된다.
- [ ] 다음 성급까지 등록 시 정확히 임계값 차이만큼 소진한다.
- [ ] 다음 성급 필요 수량이 부족하면 아무것도 소진하지 않는다.
- [ ] 거래소 등록·예약 수량은 소진할 수 없다.
- [ ] `registrable=false`인 치장 등록 요청은 아무것도 소진하지 않고 실패한다.
- [ ] 최대 성급에서는 추가 등록이 차단된다.
- [ ] 같은 멱등 키로 재시도해도 한 번만 소진된다.
- [ ] 같은 멱등 키에 다른 본문을 보내면 실패한다.
- [ ] 같은 멱등 키를 다른 `cosmeticId`에 재사용하면 실패한다.
- [ ] 오래된 `expectedCollectionRevision`으로 등록하면 아무것도 소진하지 않고 `COLLECTION_STATE_CHANGED`가 반환된다.
- [ ] 오래된 `expectedInventoryVersion`으로 등록하면 아무것도 소진하지 않고 `COLLECTION_STATE_CHANGED`가 반환된다.
- [ ] 오래된 `expectedBalanceConfigVersion`으로 등록하면 아무것도 소진하지 않고 `COLLECTION_STATE_CHANGED`가 반환된다.
- [ ] 동시에 등록과 거래 요청이 발생해도 같은 수량을 이중 소비하지 않는다.
- [ ] 등록 트랜잭션 중 실패하면 인벤토리와 도감이 모두 원상태다.

### 12.5 성급과 세트

- [ ] 성급은 누적 등록 수량과 활성 임계값으로 자동 계산된다.
- [ ] 1성 임계값은 최초 1개 등록이다.
- [ ] 세트 성급은 6부위 성급의 최솟값이다.
- [ ] 미등록 부위는 0성으로 계산된다.
- [ ] 6부위 중 하나라도 0성이면 세트 효과가 없다.
- [ ] `2·2·2·2·2·1` 구성은 1성 세트 효과를 받는다.
- [ ] 세트 성급 효과는 이전 성급 효과와 이중 합산되지 않는다.
- [ ] 완성한 여러 세트의 현재 효과가 모두 합산된다.
- [ ] 어떤 치장을 착용했는지와 세트 효과가 무관하다.

### 12.6 도감 단계와 효과

- [ ] 도감 단계는 고유 등록 치장 수로만 계산된다.
- [ ] 현재 단계 효과는 이전 단계가 포함된 누적 총 효과다.
- [ ] 도감 단계 효과와 모든 세트 효과가 능력치별로 분리된다.
- [ ] 총 상승량은 공격력·최대 체력·방어 관통 묶음으로 표시된다.
- [ ] 단위가 다른 세 능력치를 합친 임의 점수가 생성되지 않는다.

### 12.7 보안과 권한

- [ ] 미인증 사용자의 모든 조회·변경 요청이 거절된다.
- [ ] 요청에 임의 `userId`를 넣어 다른 사용자 데이터를 조회·변경할 수 없다.
- [ ] 다른 사용자의 인벤토리 수량을 등록에 사용할 수 없다.
- [ ] 클라이언트가 보낸 성급·효과·최종 능력치 값은 신뢰하지 않는다.


### 12.8 운영 상태와 설정 버전

- [ ] `acquirable=false`인 치장도 이미 해금한 사용자는 착용할 수 있다.
- [ ] `registrable=false`인 치장은 추가 등록이 차단되지만 기존 등록 수량과 성급은 유지된다.
- [ ] `equippable=false`인 치장은 착용이 차단되고 기존 착용 해제·공지·보상 정책 없이는 활성화할 수 없다.
- [ ] 캐릭터 조회와 도감 조회가 같은 활성 `balanceConfigVersion`을 반환한다.
- [ ] 성급 임계값, 도감 단계, 세트 효과 중 하나라도 누락된 밸런스 번들은 활성화되지 않는다.
---

## 13. 설정 및 플레이테스트 항목

다음 값은 **미정**이며 플레이테스트 후 서버 설정으로 확정한다.

| 항목 | 현재 상태 | 검증 목적 |
|---|---|---|
| 최대 성급 | 미정 | 중복 뽑기 수명과 거래 공급량 균형 |
| 성급별 누적 등록 임계값 | 미정 | 다음 성급 도달 속도와 반복 뽑기 동기 |
| 도감 단계별 고유 등록 수 | 미정 | 신규 치장 수집 동기와 전체 콘텐츠 수의 균형 |
| 도감 단계별 능력치 | 미정 | 장비·레벨 성장 대비 도감 영향도 |
| 세트별 성급 효과 | 미정 | 세트 완성 가치와 세트 간 편차 |
| 도감 페이지 크기 | 조정 가능 | 화면 성능과 탐색성 |

시안·프로토타입에서 임시 값을 사용하면 반드시 `임시값`으로 표시한다. 임시 수치를 최종 밸런스나 API 불변값으로 취급하지 않는다.

---

## 14. 설정 배포 검증

새 밸런스 설정은 활성화 전에 다음을 검증한다.

- 성급 임계값이 1부터 연속된 성급을 가진다.
- 1성 임계값이 1이다.
- 임계값이 엄격히 증가한다.
- 최대 성급을 초과하는 효과 행이 없다.
- 모든 활성 세트에 정확히 6부위가 있다.
- 모든 활성 세트의 1성부터 최대 성급까지 효과 행이 있다.
- 도감 단계의 고유 등록 수가 엄격히 증가한다.
- 능력치 효과에 음수나 허용 범위를 벗어난 값이 없다.
- 정식 운영 중인 사용자의 성급을 예고 없이 낮추지 않는다.

검증 실패 설정은 활성화하지 않는다.

---

## 15. 접근 방식 결정 기록

### 선택한 방식: 기능·API·DB·QA 통합 명세

제품 규칙만 적는 문서보다 구현 경계, 원자성, 오류와 검증 조건까지 한 문서에 둔다. 도감 등록은 거래 가능한 아이템을 영구 소진하는 동작이므로 화면 설명만으로는 인벤토리·거래소와의 충돌을 막을 수 없다.

### 제외한 방식

- **기능 명세만 작성:** DB 제약과 비가역 등록의 동시성·멱등성이 빠져 구현팀이 다시 결정해야 하므로 제외
- **명세와 Jira 티켓 동시 작성:** 현재 요청은 문서 기준 보강이며 담당자·스프린트·Story Point가 정해지지 않아 제외
- **화면 단일 집계 API:** 전체 도감이 커질수록 불필요한 응답이 커지므로 제외
- **도감 효과 별도 API:** 도감 목록과 효과의 조회 시점이 달라지는 문제와 중복 응답 때문에 제외

---

## 16. 미정이지만 구현을 막지 않는 항목

- 최대 성급과 성급별 임계값
- 도감 단계별 고유 등록 기준
- 도감·세트가 제공할 실제 공격력·최대 체력·방어 관통 수치
- 카드의 최종 시각 디자인, 폰트, 색상과 애니메이션
- 목록 기본 페이지 크기

이 항목들은 설정 또는 표현 계층에 한정된다. 도감 등록, 영구 해금, 성급 계산, 세트 최솟값, 효과 합산, 착용 규칙의 데이터 모델은 그대로 유지한다.

---

## 17. 후속 작업 순서

1. 본 명세를 팀 기준 문서로 승인한다.
2. 임시 성급·도감·세트 설정을 `조정 가능 기준값`으로 만든다.
3. DB 스키마와 등록 트랜잭션을 설계·구현한다.
4. 캐릭터·해금 치장·도감 API 계약 테스트를 작성한다.
5. 치장 선택→미리보기→착용, 도감 등록 확인 UX를 구현한다.
6. 중복 획득·거래·등록을 포함한 플레이테스트로 임계값과 효과를 조정한다.
7. 정식 운영 전 설정 변경과 사용자 성급 보존 정책을 확정한다.

---

## 18. 완료 정의

다음이 모두 만족되면 캐릭터·치장·도감 기능 명세가 구현 가능한 상태다.

- Frontend가 세 탭의 목록 기준과 모든 주요 상태를 구분할 수 있다.
- Backend가 사용자 입력 없이 성급·도감 단계·세트 성급·최종 능력치를 계산할 수 있다.
- DB가 거래 가능 수량, 영구 등록 수량, 외형 해금과 착용 상태를 혼동 없이 저장한다.
- 등록과 거래의 동시 요청이 아이템을 이중 소비하지 않는다.
- QA가 정상 흐름, 실패 흐름, 비가역 등록, 권한과 계산 불변식을 자동 또는 수동으로 검증할 수 있다.
- 미정 밸런스 값이 코드에 확정 상수로 박히지 않는다.
