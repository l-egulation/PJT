---
doc_kind: reference
owner_domain: client-ux
authority_level: candidate
---

# 아늑한 픽셀 온보딩 에셋팩 v1

로그인·회원가입, 주력 나라 선택과 최종 확인 모달 후보 화면을 조립하기 위한 필수 P0 래스터 에셋팩이다. [에셋 제작 명세](../cozy-pixel-asset-plan.md)를 기준으로 신규 필수 에셋 14종을 분리했다.

이 폴더는 시각 검토를 통과한 원본 제작 파일을 보존한다. 2026-09-09에 동일 파일을 `apps/web/src/shared/assets/onboarding-cozy-pixel`, `apps/web/src/features/auth/assets-cozy-pixel`, `apps/web/src/features/material-preference/assets-cozy-pixel`로 복사해 현재 온보딩 구현에 연결했다. 화면 흐름은 [클라이언트 UX SSOT](../../../30-domain/player/ux/ssot.md), 계정 규칙은 [계정·저장 SSOT](../../../30-domain/player/ssot.md), 선택 규칙은 [주력 재료 SSOT](../../../30-domain/character/ssot.md)를 따른다.

## 공통 4종

| 파일 | 원본 크기 | 알파 | 사용처 |
| --- | ---: | --- | --- |
| [`onboarding-guild-background.png`](./common/onboarding-guild-background.png) | 1672×941 | 없음 | 인증·주력 선택 공통 16:9 배경 |
| [`parchment-shell-9slice.png`](./common/parchment-shell-9slice.png) | 1254×1254 | 있음 | 인증 패널·선택 보드·확정 모달 공통 종이 셸 |
| [`primary-button-shell-9slice.png`](./common/primary-button-shell-9slice.png) | 2172×724 | 있음 | 로그인·회원가입·선택 확정 주요 버튼 |
| [`secondary-button-shell-9slice.png`](./common/secondary-button-shell-9slice.png) | 2172×724 | 있음 | 확정 모달 취소 버튼 |

배경은 생성 원본을 보존한 PNG다. 구현 시 시각 손실과 번들 크기를 확인한 뒤 WebP 파생본을 만들 수 있다. 9-slice 보호 폭은 실제 컴포넌트 렌더 크기를 검증한 뒤 구현 계약에서 확정한다.

## 로그인·회원가입 5종

| 파일 | 원본 크기 | 알파 | 사용처 |
| --- | ---: | --- | --- |
| [`auth-brand-logo.png`](./auth/auth-brand-logo.png) | 1358×1158 | 있음 | 빨간 그림자 없는 `한짝 / 젓가락 키우기` 통합 로고 |
| [`auth-input-shell-9slice.png`](./auth/auth-input-shell-9slice.png) | 2172×724 | 있음 | 이메일·닉네임·비밀번호 공통 입력 셸 |
| [`auth-field-email-icon.png`](./auth/auth-field-email-icon.png) | 1536×1024 | 있음 | 이메일 필드 |
| [`auth-field-user-icon.png`](./auth/auth-field-user-icon.png) | 1254×1254 | 있음 | 회원가입 닉네임 필드 |
| [`auth-field-lock-icon.png`](./auth/auth-field-lock-icon.png) | 1254×1254 | 있음 | 비밀번호 필드 |

로그인과 회원가입은 같은 로고·입력 셸·버튼을 사용한다. 탭, 라벨, 입력값, 오류와 로딩 상태는 HTML·CSS가 소유한다.

## 주력 나라 선택 5종

| 파일 | 원본 크기 | 알파 | 사용처 |
| --- | ---: | --- | --- |
| [`country-potato-cozy-v2.png`](./material/country-potato-cozy-v2.png) | 1448×1086 | 있음 | 기준 화면에서 분리하고 도장 가림 영역만 복원한 감자 집과 대표 감자 삽화 |
| [`country-sweet-potato-cozy-v2.png`](./material/country-sweet-potato-cozy-v2.png) | 1448×1086 | 있음 | 기준 화면에서 그대로 분리한 고구마 집과 대표 고구마 삽화 |
| [`country-corn-cozy-v2.png`](./material/country-corn-cozy-v2.png) | 1448×1086 | 있음 | 기준 화면에서 그대로 분리한 풍차 집과 대표 옥수수 삽화 |
| [`material-selected-ink-stamp.png`](./material/material-selected-ink-stamp.png) | 1254×1254 | 있음 | 선택 이미지 위에 겹치는 코랄 원형 체크 도장 |
| [`material-population-icon.png`](./material/material-population-icon.png) | 1254×1254 | 있음 | 나라별 선택 인구 앞의 3인 아이콘 |

세 나라 삽화는 [주력 나라 선택 기준 화면](../concepts/material-country-population-draft-v6.png)의 단순한 그림을 기준으로 분리했으며 같은 4:3 캔버스와 바닥 기준을 사용한다. 나라 이름, 인구수, 비율과 `선택` 문구는 이미지에 포함하지 않는다. 파일명에 `v2`가 없는 이전 3종은 상세도가 높아진 폐기 후보이며 비교 기록으로만 보존한다.

## 기존 파일 재사용

다음 파일은 이 에셋팩에 복제하지 않고 현재 위치에서 재사용한다.

- `apps/web/src/features/material-preference/assets/potato-item-pixel-192.png`
- `apps/web/src/features/material-preference/assets/sweet-potato-item-pixel-192.png`
- `apps/web/src/features/material-preference/assets/corn-item-pixel-192.png`
- `apps/web/src/features/auth/assets/auth-password-visible.png`
- `apps/web/src/features/auth/assets/auth-password-hidden.png`
- `apps/web/src/features/auth/assets/auth-paper-texture.png`

## 확정 모달

확정 모달 전용 래스터 에셋은 추가하지 않는다. 공통 종이 셸, 주요·보조 버튼 셸과 기존 재료 아이콘을 조합하고 딤드 오버레이·문구·구분선은 HTML·CSS로 표현한다.

## 이번 제작에서 제외

P1 선택 항목인 잎 모서리 장식과 고양이 마스코트는 필수 화면 조립에 필요하지 않아 이번 v1 에셋팩에서 제외했다.
