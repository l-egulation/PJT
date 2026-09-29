---
doc_kind: reference
owner_domain: client-ux
authority_level: candidate
---

# 아늑한 픽셀 온보딩 에셋 제작 명세

필수 P0 에셋 14종의 원본은 [아늑한 픽셀 온보딩 에셋팩 v1](./asset-pack-v1/README.md)에 보존한다. 2026-09-09에 권장 배치 경로로 복사해 `apps/web` 인증·주력 나라 선택 구현에 연결했다.

## 범위

다음 후보 화면을 실제 HTML·CSS·React UI로 조립하기 위한 래스터 에셋 목록이다.

- [로그인·회원가입 기준 초안](./concepts/login-cozy-pixel-draft-v1.png)
- [주력 나라 선택 기준 초안](./concepts/material-country-population-draft-v6.png)
- [최종 확인 모달 기준 초안](./concepts/material-country-confirm-modal-draft-v2.png)
- [한짝 젓가락 캐릭터 로고 후보 v4](./assets/auth-brand-logo-v4.png)

로그인·회원가입 기준 초안에 보이는 밥그릇 장식은 폐기한다. 실제 화면에는 `한짝` 제목 위로 나무젓가락 캐릭터가 빼꼼 보이고, 제목 아래에 작은 `젓가락 키우기` 문구가 놓이는 통합 로고를 사용한다.

화면 흐름은 [클라이언트 UX SSOT](../../30-domain/player/ux/ssot.md), 계정 규칙은 [계정·저장 SSOT](../../30-domain/player/ssot.md), 선택 규칙과 서버 제공 비율은 [주력 재료 SSOT](../../30-domain/character/ssot.md)를 따른다. 이 문서는 시각 자산의 제작 범위만 정리하며 규칙·수치·API 계약을 새로 정의하지 않는다.

## 제작 원칙

- 완성 화면을 한 장으로 잘라 쓰지 않고 배경, 프레임, 버튼, 아이콘, 삽화를 독립 에셋으로 만든다.
- 배경처럼 불투명한 큰 이미지는 WebP, UI 프레임과 삽화는 실제 알파 채널을 가진 PNG를 사용한다.
- 가변 크기 프레임은 9-slice를 전제로 네 모서리와 가장자리의 픽셀 형태가 늘어나지 않게 보호 영역을 둔다.
- 화면 제목, 탭, 필드 라벨, 입력값, 오류, 로딩, 나라 이름, 인구, 비율, 버튼 문구와 모달 문구는 이미지에 굽지 않고 HTML로 표시한다.
- hover, pressed, disabled, focus, 선택되지 않은 나라의 명도, 모달 딤드 오버레이와 선택 바닥 강조는 CSS로 처리한다.
- 공통 배경과 공통 종이·버튼을 인증과 주력 선택 화면이 함께 사용해 화풍 차이를 막는다.

## 기존 에셋 처리

### 그대로 재사용하며 신규 제작에서 제외

| 용도 | 기존 파일 | 비고 |
| --- | --- | --- |
| 감자 아이템 | `apps/web/src/features/material-preference/assets/potato-item-pixel-192.png` | 주력·보조 크기는 CSS로 조절 |
| 고구마 아이템 | `apps/web/src/features/material-preference/assets/sweet-potato-item-pixel-192.png` | 주력·보조 크기는 CSS로 조절 |
| 옥수수 아이템 | `apps/web/src/features/material-preference/assets/corn-item-pixel-192.png` | 주력·보조 크기는 CSS로 조절 |
| 비밀번호 표시 | `apps/web/src/features/auth/assets/auth-password-visible.png` | 작은 렌더 크기에서 화풍을 재검수하되 우선 재사용 |
| 비밀번호 숨김 | `apps/web/src/features/auth/assets/auth-password-hidden.png` | 작은 렌더 크기에서 화풍을 재검수하되 우선 재사용 |
| 종이 내부 질감 | `apps/web/src/features/auth/assets/auth-paper-texture.png` | 공통 프레임 내부에 낮은 불투명도로 재사용 |

### 파일은 보존하지만 새 화면에서는 교체

- `auth-background-vintage.png`, `material-country-background-vintage.png`: 실사형 빈티지 배경이라 새 픽셀 배경으로 교체한다.
- `auth-vintage-*-paper.png`, `material-board-v2.png`, `material-vintage-modal-paper.png`: 기존 종이 외곽이 새 시안의 픽셀 테두리와 다르므로 공통 9-slice 프레임으로 교체한다.
- `auth-vintage-primary-button.png`, `material-primary-button-v2.png`, `material-secondary-button-v2.png`: 새 공통 픽셀 버튼 판으로 교체한다.
- `potato-country-v2.png`, `sweet-potato-country-v2.png`, `corn-country-v2.png`: 세부 묘사가 많고 새 시안과 실루엣이 달라 단순화한 나라 삽화로 교체한다.
- `material-selected-seal-v2.png`: 작은 왁스 배지이므로 대형 잉크 도장으로 교체한다.

## 권장 배치 경로

```text
apps/web/src/shared/assets/onboarding-cozy-pixel/             공통
apps/web/src/features/auth/assets-cozy-pixel/                 로그인·회원가입 전용
apps/web/src/features/material-preference/assets-cozy-pixel/  주력 선택 전용
```

구현을 시작하기 전까지 이 경로는 제안이며 실제 디렉터리와 코드 참조를 만들지 않는다.

## 공통 신규 에셋

| 우선순위 | 권장 파일명 | 형식·권장 원본 크기 | 투명 | 용도와 제작 조건 |
| --- | --- | --- | --- | --- |
| P0 | `onboarding-guild-background.webp` | WebP, 1920×1080 | 아니요 | 로그인 초안과 같은 따뜻한 목조 길드 주방. 중앙 UI 안전 영역은 저대비·저밀도로 비우고 글자·패널·캐릭터를 포함하지 않는다. 인증과 주력 선택이 같은 파일을 사용한다. |
| P0 | `parchment-shell-9slice.png` | PNG, 768×768 | 예 | 큰 인증 패널, 주력 선택 보드, 확인 모달에 공용으로 쓰는 빈 크림색 픽셀 종이. 테두리 보호 폭을 메타데이터 또는 구현 문서에 기록하고 글자·장식·그림자를 굽지 않는다. |
| P0 | `primary-button-shell-9slice.png` | PNG, 512×128 | 예 | 코랄색 주요 버튼의 빈 판. 로그인, 계정 만들기, 선택 확정에 공용으로 사용하고 상태 변화는 CSS로 처리한다. |
| P0 | `secondary-button-shell-9slice.png` | PNG, 512×128 | 예 | 크림색 보조 버튼의 빈 판. 최종 확인 취소 동작에 사용한다. |
| P1 | `leaf-corner-ornament.png` | PNG, 96×96 | 예 | 제목 주변과 종이 모서리에 제한적으로 쓰는 작은 잎 장식. 회전·반전 재사용이 가능하고 큰 새싹 심볼은 포함하지 않는다. |

## 로그인·회원가입 전용 신규 에셋

로그인과 회원가입은 같은 자산을 사용한다. 회원가입은 HTML 입력 행 하나만 늘어나므로 별도의 배경이나 패널 에셋을 만들지 않는다.

| 우선순위 | 권장 파일명 | 형식·권장 원본 크기 | 투명 | 용도와 제작 조건 |
| --- | --- | --- | --- | --- |
| P0 | `auth-brand-logo.png` | PNG, 세로형 1.17:1 내외 | 예 | `한짝` 제목 위로 나무젓가락 캐릭터가 손을 얹고 빼꼼 보는 아늑한 픽셀 로고. 제목과 작은 `젓가락 키우기` 문구는 밝은 황금빛 나무색, 성긴 픽셀 나뭇결, 크림색 상단 하이라이트와 짙은 목재 외곽선을 사용하며 빨간색 그림자는 두지 않는다. 밥그릇·김·새싹·재료·별도 마스코트는 포함하지 않는다. 제작 후보는 [`auth-brand-logo-v4.png`](./assets/auth-brand-logo-v4.png)다. |
| P0 | `auth-input-shell-9slice.png` | PNG, 768×160 | 예 | 이메일·닉네임·비밀번호에 공용으로 쓰는 빈 입력 프레임. 실제 입력 배경과 포커스 링은 HTML·CSS가 소유한다. |
| P0 | `auth-field-email-icon.png` | PNG, 64×64 | 예 | 이메일 입력의 봉투 픽셀 아이콘. |
| P0 | `auth-field-user-icon.png` | PNG, 64×64 | 예 | 회원가입 닉네임 입력의 사용자 픽셀 아이콘. |
| P0 | `auth-field-lock-icon.png` | PNG, 64×64 | 예 | 비밀번호 입력의 자물쇠 픽셀 아이콘. 표시·숨김 동작 아이콘은 기존 파일을 재사용한다. |
| P1 | `auth-cat-mascot.png` | PNG, 320×320 | 예 | 로그인 초안의 패널 우하단 고양이. 폼과 버튼을 가리지 않는 독립 장식이며 회원가입에서도 같은 파일을 사용한다. |

탭 판, 선택 밑줄, 구분선, 필드 글자, 버튼 글자와 오류·로딩 상태는 신규 이미지가 필요하지 않다.

## 주력 나라 선택 전용 신규 에셋

세 나라 삽화는 같은 논리 캔버스, 바닥 기준선, 여백과 광원 방향을 사용한다. 도장과 인구 아이콘을 제외한 글자와 수치는 포함하지 않는다.

| 우선순위 | 권장 파일명 | 형식·권장 원본 크기 | 투명 | 용도와 제작 조건 |
| --- | --- | --- | --- | --- |
| P0 | `country-potato-cozy.png` | PNG, 512×384 | 예 | [기준 화면](./concepts/material-country-population-draft-v6.png)의 감자 집 하나와 큰 감자 하나를 그대로 분리한 단순 삽화. 제작 후보는 [`country-potato-cozy-v2.png`](./asset-pack-v1/material/country-potato-cozy-v2.png)다. |
| P0 | `country-sweet-potato-cozy.png` | PNG, 512×384 | 예 | [기준 화면](./concepts/material-country-population-draft-v6.png)의 고구마 집 하나와 큰 고구마 하나를 그대로 분리한 단순 삽화. 제작 후보는 [`country-sweet-potato-cozy-v2.png`](./asset-pack-v1/material/country-sweet-potato-cozy-v2.png)다. |
| P0 | `country-corn-cozy.png` | PNG, 512×384 | 예 | [기준 화면](./concepts/material-country-population-draft-v6.png)의 작은 풍차 하나와 큰 옥수수 하나를 그대로 분리한 단순 삽화. 제작 후보는 [`country-corn-cozy-v2.png`](./asset-pack-v1/material/country-corn-cozy-v2.png)다. |
| P0 | `material-selected-ink-stamp.png` | PNG, 512×512 | 예 | 선택한 나라 삽화 위에 겹치는 반투명 코랄 원형 도장 테두리와 체크. `선택` 문구는 HTML로 올리고 도장 자체에는 굽지 않는다. |
| P0 | `material-population-icon.png` | PNG, 64×64 | 예 | 나라별 선택 인구 앞에 쓰는 3인 픽셀 실루엣. |

기존 재료 아이콘 세 종, 나라 이름, 선택 인구, 비율 숫자, 최고 인구 색상 강조, 주력·보조 아이콘 크기 차이와 선택 바닥 강조에는 신규 이미지가 필요하지 않다.

## 확정 모달 전용 신규 에셋

필수 전용 래스터 에셋은 없다.

- 모달 외곽은 공통 `parchment-shell-9slice.png`를 사용한다.
- 선택 재료 그림은 기존 아이템 픽셀 아이콘 세 종 중 현재 선택값을 사용한다.
- 취소·확정 버튼은 공통 보조·주요 버튼 판을 사용한다.
- 화면 딤드 오버레이, 구분선, 문구, 포커스와 처리 중 상태는 HTML·CSS로 만든다.

별도 모달 종이 이미지를 만들면 같은 테두리의 두 번째 정본이 생기므로 공통 프레임으로 표현할 수 없는 모양이 확정되기 전에는 추가하지 않는다.

## 제작 수량 요약

| 구분 | P0 신규 | P1 선택 | 비고 |
| --- | ---: | ---: | --- |
| 공통 | 4 | 1 | 배경·종이·주요/보조 버튼 |
| 로그인·회원가입 | 5 | 1 | 필드 아이콘 3종 포함 |
| 주력 나라 선택 | 5 | 0 | 나라 삽화 3종·도장·인구 아이콘 |
| 확정 모달 | 0 | 0 | 공통·기존 에셋 재사용 |
| 합계 | 14 | 2 | 기존 재사용 에셋은 제외 |

## 에셋이 아닌 선행 계약

나라별 선택 인구는 현재 `GET /api/v1/material-preference` 응답에 없다. 이미지에는 인구 값을 굽지 않으며 구현 전 다음 항목을 별도 계약에서 정해야 한다.

- 집계 대상의 범위
- 집계 시점과 갱신 주기
- 값이 없거나 조회에 실패했을 때의 표시
- 선택 전 화면에서 집계값을 제공하는 서버 응답 필드
