---
doc_kind: design
owner_domain: skills
status: approved
approved_at: 2026-09-09
authority_level: reference
---

# 스킬 시스템 재구현 설계 — 치장 추첨 경계 분리

## 문서 책임

이 문서는 `codex/cosmetic-gacha-main-refresh` 브랜치에서 스킬 시스템을 다시 구현하기 위한 범위와 경계를 기록한다. 게임 규칙·수치의 정본은 스킬 SSOT와 연결된 콘텐츠 SSOT가 소유하며, 이 문서는 이를 복제하지 않는다.

- 규칙·상태·수치: [스킬 SSOT](../../30-domain/character/skills/ssot.md)
- 표시명·스킬북 콘텐츠: [MVP 스킬 표시 콘텐츠](../../60-content/abilities/mvp-v1.md)
- 구현 상태·증거: [H 스킬 작업 색인](../../wiki/06-delivery/tasks/H-skills/_index.md)
- 치장 추첨 규칙: [치장 SSOT](../../30-domain/cosmetics/ssot.md)
- 치장 추첨 구현 계획: [치장 추첨 요구사항](../../70-plans/cosmetic-gacha/requirements.md)

## 문제

기준 브랜치 `codex/cosmetic-gacha-main-refresh`에는 치장 추첨 변경과 함께 단순화된 스킬 구현이 존재한다. 현재 스킬 코드는 단일 노말 등급 강화만 제공하고, 승인된 스킬 SSOT의 다등급 강화·승급·복합 스킬북·콘텐츠 기반 수치·실패 보정 계약을 제공하지 않는다. 스킬을 복원하는 과정에서 치장 추첨의 HMAC entropy, 배너 확률, 결제, 마일스톤, 감사 기록과 결합하면 두 성장 시스템의 권한·멱등성·재현성 경계가 흐려진다.

## 목표

다음 동작을 스킬 전용 경계 안에서 복원한다.

- 6종 스킬, 액티브 4종과 패시브 2종
- 노말·희귀·영웅·전설 등급과 각 등급 1~10강
- 등급별 고정 쌀 비용과 복합 스킬북 요구
- 강화 성공·실패 및 목표별 실패 보정
- 직전 등급 10강에서의 100% 승급
- 현재 MVP의 전설 접근 잠금
- 액티브 4슬롯 장착 및 자동 사용 우선순위
- 전투 엔진에 전달할 서버 확정 스킬 프로필
- 스킬북의 독립적인 스테이지 보상 판정 연결

## 비목표

이번 작업에서는 다음을 변경하지 않는다.

- 치장 배너·확률·풀·마일스톤
- 치장 추첨 HMAC entropy, 재현 토큰, key 식별자
- 치장 결제·등록·착용·선택 상자
- 치장 command/audit 저장 구조와 migration
- 치장 UI와 치장 콘텐츠
- 자동전투 처치별 보상 정산 정책
- 전체 경제 재밸런싱

## 경계와 소유권

### 스킬 소유 영역

- `modules/skills`: 스킬 도메인 모델, 규칙 adapter, 강화·승급·장착 서비스, 전투 프로필
- `packages/game-content/versions/v1/skills/skills.json`: 버전형 스킬 수치·효과·콘텐츠
- `packages/game-content/schema/skills.schema.json`: 스킬 콘텐츠 계약
- `apps/game-api/src/main/resources/db/migration`: 스킬 상태·장착·스킬 명령 결과를 위한 스킬 migration
- `apps/web/src/features/skills`: 서버 결과 표시와 스킬 명령 UI
- 스킬 전용 서버·웹 테스트 및 통합 테스트

### 치장 추첨 소유 영역

- `modules/cosmetics`
- `apps/game-api`의 치장 entropy configuration과 치장 controller adapter
- 치장 배너·풀·확률·마일스톤 콘텐츠
- 치장 command/audit 및 치장 migration
- `apps/web/src/features/cosmetics`

### 금지 결합

- 스킬 강화·승급은 치장 추첨 `DrawEntropy`를 사용하지 않는다.
- 치장 추첨은 스킬북·스킬 상태·스킬 command record를 변경하지 않는다.
- 두 시스템은 command record, idempotency fingerprint namespace, random source를 공유하지 않는다.
- 기존 치장 migration 파일에 스킬 테이블이나 스킬 컬럼을 추가하지 않는다.
- 스킬 API가 치장 추첨 서비스에 의존하지 않는다.
- 스킬북을 치장 추첨 결과의 alias나 치장 품목 namespace로 취급하지 않는다.

## 서버 설계

### 콘텐츠 로딩

`modules/skills`는 versioned `skills.json`을 `SkillContentLoader`로 읽고, 로더는 applied authority, 콘텐츠 버전, 스킬 6종, 등급·레벨 범위, 성공률, 등급별 책 조합과 효과 필드를 검증한다. `SkillRules`는 로드된 콘텐츠를 통해 스킬 정의, 효과, 비용, 승급 대상, 전설 접근 가능 여부를 계산한다. 클라이언트는 이 값을 재계산하지 않는다.

### 명령 API

기존 다등급 계약을 복원한다.

- `GET /api/v1/skills`
- `POST /api/v1/skills/{skillId}/enhance`
- `POST /api/v1/skills/{skillId}/promote`
- `PUT /api/v1/skills/loadout`

응답에는 서버가 계산한 현재 상태, 목표 등급·레벨, 필요한 등급별 스킬북, 쌀 비용, 다음 성공률, 실행 가능 여부와 거절 사유를 포함한다. API의 도메인 오류는 치장 추첨 오류와 별도 코드 namespace를 유지한다.

### 원자성과 멱등성

강화·승급 명령은 계정 권한과 최신 계정 상태를 잠근 뒤 콘텐츠 버전, 현재 스킬 단계, 스킬북, 쌀, MVP 접근 범위를 검증한다. 책·쌀 차감, 성공 판정, 단계 변경, 실패 보정, 계정 상태 버전, 저장된 명령 결과는 하나의 트랜잭션에서 확정한다.

명령은 스킬 전용 command record와 UUID `Idempotency-Key`를 사용한다.

- 같은 계정의 같은 키와 같은 fingerprint: 저장된 결과 재반환
- 같은 키와 다른 fingerprint: `IDEMPOTENCY_KEY_REUSED`
- 실패한 강화: 책·쌀을 소비하고 등급·단계를 유지하며 목표별 보정 증가
- 성공한 강화: 단계 변경 및 해당 목표 보정 초기화
- 승급: 직전 등급 10강과 현재 MVP 접근 조건을 검증하고 100% 적용
- 조건 불충족: 어떤 자원도 소비하지 않고 안정 오류 반환

강화 roll source는 스킬 전용 `SkillRollSource`로 주입한다. 치장 추첨의 HMAC 기반 `DrawEntropy`와는 타입·구성·재현 메타데이터를 공유하지 않는다.

### 전투 연결

`SkillProfileProvider`는 서버에 저장된 스킬 상태와 액티브 장착 순서에서 `packages/sim-core`가 사용하는 프로필을 만든다. 액티브 버프의 기본공격 동시 수행, 공격 액티브의 기본공격 대체, 지속 피해 수명은 전투 SSOT와 sim-core 계약에 맞춰 별도 테스트한다. 스킬 서비스는 치장 서비스나 치장 장착 상태를 직접 참조하지 않는다.

## 콘텐츠·아이템 연결

스킬북 item ID는 다음 안정 namespace를 사용한다.

```text
skillbook:<skillId>:<grade>
```

표시명은 스킬 표시 콘텐츠 SSOT를 따르고, 인벤토리·드롭·거래소·스킬 성장 action에서 동일한 품목 ID를 사용한다. 스킬북 드롭 판정은 스테이지 보상 흐름에서 독립적으로 수행하며 치장 추첨 결과·확률·entropy를 호출하지 않는다.

## 웹 설계

`apps/web/src/features/skills`는 서버가 반환한 action summary를 표시한다.

- 등급·강화 단계와 다음 목표 표시
- 등급별 책 요구량과 보유량 표시
- 쌀 비용과 다음 성공률 표시
- 강화·승급·잠금 상태를 서버 action으로 결정
- 강화 실패 결과와 다음 보정 표시
- 액티브 장착 순서 저장
- 네트워크 오류 또는 결과 불확실 시 새 키로 중복 명령을 만들지 않고 같은 명령 결과를 재조회

치장 UI의 draw action, 결과 목록, 마일스톤 상태와 스킬 화면은 상태·API·오류 처리 코드를 공유하지 않는다.

## 검증 전략

구현은 각 계약에 대해 실패 테스트를 먼저 추가하고, 최소 구현 후 전체 영향 범위를 검증한다.

### 도메인·콘텐츠

- 모든 6종 스킬과 4등급 효과값 로딩
- 등급별 강화 비용·승급 비용·복합 책 요구
- 강화 성공률과 실패 보정 상한·초기화
- 승급 대상과 전설 MVP 잠금
- 잘못된 콘텐츠 필드·중복 스킬·등급 누락 거절

### 애플리케이션·DB

- 해금·강화·승급의 원자적 책·쌀 차감
- 성공·실패 상태 전이
- 같은 명령 재시도와 fingerprint 충돌
- 부족 자원·최대 단계·접근 잠금 시 무변경 거절
- 장착 중복·비액티브·4슬롯 초과 검증
- 치장 command/audit 행과 스킬 command 행의 독립성

### 전투·드롭

- 스킬 프로필의 등급별 효과 반영
- 버프 행동의 기본공격 동시 처리
- 공격 액티브의 기본공격 대체
- 지속 피해 대상 전환·만료 규칙
- 일반 몬스터·보스 스킬북 판정이 치장 추첨과 독립적으로 실행됨

### 웹·통합

- 서버 action summary 기반 버튼·비용·오류 표시
- 강화·승급·장착 API의 idempotency header 전송
- 스킬 API와 치장 API의 경로·응답 계약 분리
- Spring 통합 테스트와 웹 focused test
- 사용 가능한 로컬 PostgreSQL/Docker 환경에서 실제 HTTP 재시도와 DB 원자성 확인

## 구현 순서

1. 기준 브랜치의 치장 추첨 미커밋 변경을 보존한 채 스킬 관련 현재 코드와 계약을 고정한다.
2. 스킬 콘텐츠 schema·JSON과 로더/규칙 테스트를 먼저 복원한다.
3. 스킬 도메인·서버 service·migration·API와 실패 테스트를 복원한다.
4. 스킬북 catalog/drop 연결과 전투 profile 연결을 복원한다.
5. 웹 API 타입·화면·재시도 처리와 focused test를 복원한다.
6. 스킬 전용 테스트, 통합 테스트, build와 smoke를 실행한다.
7. H 작업 문서의 구현·검증 상태와 증거 링크를 실제 결과에 맞게 갱신한다.

## 완료 기준

- 승인된 스킬 SSOT의 다등급 성장·승급 계약이 서버에서 동작한다.
- 스킬 콘텐츠와 스킬북 item ID가 versioned catalog에서 로드된다.
- 강화·승급·장착의 멱등성과 원자성이 검증된다.
- 전투 profile과 스킬북 drop 연결이 검증된다.
- 웹이 서버 확정 비용·확률·잠금 사유를 표시한다.
- 치장 추첨 파일·DB·API·entropy·audit의 동작과 테스트가 변경되지 않는다.
- 스킬 테스트와 관련 통합 검증의 실제 결과가 H 작업 문서에 기록된다.
