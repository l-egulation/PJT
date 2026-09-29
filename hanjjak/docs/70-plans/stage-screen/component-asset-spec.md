# 메인 HUD 컴포넌트·에셋 분리 명세

---
doc_kind: reference
owner_domain: client-ux
authority_level: reference
source: 2026-09-09 user UI review
---

## 목적

메인 전투 장면 자체는 범위에서 제외하고, 화면 위에 배치하는 HUD를 실제 상태와 API 응답에 연결할 수 있도록 `정적 이미지 에셋`, `동적 표시`, `상호작용 컴포넌트`로 분리한다. 이 문서의 숫자와 문구는 시각 제작을 위한 예시이며 게임 규칙의 정본이 아니다. 현재 규칙은 [클라이언트 UX SSOT](../../30-domain/player/ux/ssot.md), [스테이지 SSOT](../../30-domain/world/ssot.md), [계정·저장 SSOT](../../30-domain/player/ssot.md)를 따른다.

## 절대 한 장으로 굽지 않는 정보

다음 값은 이미지에 포함하지 않고 HTML 텍스트, 반복 행, CSS 너비 또는 런타임 아이콘으로 렌더링한다.

- 닉네임, 현재 쌀
- 현재 스테이지 표시명과 스테이지 선택 결과
- 현재 처치 수, 목표 처치 수, 처치 진행률
- 전투 기록의 개수와 각 기록의 유형·스테이지·설명·시각·보조 능력치
- 획득 품목 목록, 품목별 누적 수량, 접힌 상태의 빨간 배지 수
- 레벨, 현재·최대 HP, 현재·필요 EXP, 퍼센트와 두 진행 바의 채움 길이
- 현재 선택된 상단·하단 메뉴

종이 질감 프레임, 모서리, 아이콘, 구분선, 진행 바 트랙과 채움 텍스처만 이미지 에셋으로 만든다.

## 1. 좌측 상단 프로필

### 컴포넌트

`PlayerSummaryCard`는 카드 전체를 하나의 배경 에셋으로 만들지 않고 내부를 독립 레이어로 조합한다.

| 레이어 | 정적 에셋 | 동적 표시·동작 |
| --- | --- | --- |
| 카드 | 신규 `shared/assets/cozy-hud-v1/paper/profile-summary-shell-9slice.png` | 화면 폭에 맞춰 9-slice 확장 |
| 캐릭터 | 신규 `shared/assets/cozy-hud-v1/icons/chopstick-head.png` | 젓가락 상단 얼굴 전용 투명 아이콘 |
| 닉네임 | 없음 | 계정 상태의 현재 닉네임 |
| 재화 아이콘 | 기존 `character/assets-cozy-pixel/rice-icon-128.png` | 없음 |
| 재화 수량 | 없음 | 서버가 확정한 현재 쌀 |
| 내부 구분 | 없음 | 카드 폭에 맞춘 CSS 점선 사용 |

권장 표시 크기는 1920×1080 기준 약 `384×112px`, 젓가락 상단 얼굴은 `88~96px` 정사각 영역이다. 숫자 길이가 늘어날 수 있으므로 오른쪽에 최소 7자리 공간을 둔다.

## 2. 전투 기록 버튼과 확장 창

### 접힌 버튼

`BattleLogTrigger`는 프로필 아래에 붙는 독립 버튼이다.

- 기존 `shared/assets/onboarding-cozy-pixel/secondary-button-shell-9slice.png`: 글자 없는 종이 버튼 프레임
- 신규 `shared/assets/cozy-hud-v1/icons/battle-log-scroll-clock.png`: 두루마리＋시계 아이콘
- 접기·펼치기 chevron과 hover·pressed 상태: CSS로 렌더링한다.

### 펼친 창

`BattleLogPopover`는 버튼 위치를 기준으로 아래·오른쪽으로 확장한다. 한두 개의 고정 예시가 아니라 기록이 누적되는 목록이므로 권장 표시 영역은 약 `560×560px`이며 내부만 스크롤한다. 프로필과 접힌 버튼은 그대로 유지한다.

큰 창은 신규 `shared/assets/cozy-hud-v1/paper/battle-log-panel-shell-9slice.png`, 반복 행·수치 칩은 CSS의 반투명 아이보리 면을 사용한다. 스크롤 트랙과 thumb도 CSS로 렌더링한다.

기록 유형은 신규 `battle-event-stage-enter.png`, `battle-event-return.png`, `battle-event-victory.png`, `battle-event-failure.png`를 사용하고 최근 전투는 기존 검 아이콘을 재사용한다. 행의 제목, 스테이지, 메시지, 발생 시각과 보조 수치는 모두 API 목록을 반복 렌더링한다. 새 기록이 들어오면 맨 위에 추가하며 창 높이는 더 커지지 않는다.

## 3. 중앙 상단 스테이지 진행

`StageProgressPanel`은 현재 상태를 실시간으로 반영한다.

| 영역 | 정적 에셋 | 동적 표시·동작 |
| --- | --- | --- |
| 전체 프레임 | 신규 `shared/assets/cozy-hud-v1/paper/stage-progress-shell-9slice.png` | 반응형 너비 |
| 현재 스테이지 | 작은 새싹 아이콘 | `STAGE 1-7` 같은 현재 스테이지 표시 |
| 선택 버튼 | 기존 `secondary-button-shell-9slice.png`, CSS chevron | 클릭하면 스테이지 선택 화면을 연다 |
| 처치 아이콘 | 신규 `monster-progress.png`, `monster-progress-boss.png` | 일반 처치와 보스 단계에 맞춰 교체 |
| 처치 바 | 기존 `progress-track-9slice.png`, 신규 `progress-fill-coral-9slice.png` | `현재 처치 / 목표 처치` 비율로 채움 너비 변경 |
| 처치 숫자 | 없음 | 예: `1 / 20`; 적 처치 확정 때마다 갱신 |

바 안의 빨간 채움과 `1 / 20`을 이미지에 굽지 않는다. 목표 수가 달라져도 같은 에셋을 사용한다.

### 스테이지 선택 상태

스테이지 선택 화면의 상태는 신규 `stage-status-current.png`, `stage-status-cleared.png`, `stage-status-repeat.png`와 기존 `auth-field-lock-icon.png`를 사용한다. 각 아이콘 옆에는 `현재`, `클리어`, `반복`, `잠금` 접근성 텍스트를 함께 제공하며 색상이나 이미지 하나에만 의미를 의존하지 않는다. 스테이지 번호와 해금 여부는 런타임 데이터로 렌더링한다.

### 자동 전투 버튼

자동 전투는 기존 밝은 버튼 9-slice 위에 신규 `auto-battle.png` 또는 `auto-battle-paused.png`와 코드 텍스트를 조립한다. disabled 상태는 CSS와 접근성 상태값으로 구분하며 상태별 통짜 버튼 이미지를 만들지 않는다.

## 4. 우측 상단 기능 버튼

`TopUtilityNav`에는 던전·레이드·거래소·랭킹·설정의 정확히 다섯 클릭 영역을 둔다. 시각적으로는 한 줄이지만 하나의 통짜 이미지나 클릭 영역으로 만들지 않는다.

공통 에셋은 신규 `shared/assets/cozy-hud-v1/controls/utility-button-shell.png` 하나를 사용한다. hover·pressed·selected·disabled는 CSS 오버레이와 명도·외곽선 변화로 표현해 상태별 이미지를 중복 생성하지 않는다.

신규 아이콘 에셋:

- `shared/assets/cozy-hud-v1/icons/nav-dungeon.png`
- `shared/assets/cozy-hud-v1/icons/nav-raid.png`
- `shared/assets/cozy-hud-v1/icons/nav-market.png`
- `shared/assets/cozy-hud-v1/icons/nav-ranking.png`
- `shared/assets/cozy-hud-v1/icons/nav-settings.png`

글자는 코드로 올리고 버튼마다 독립 라우팅·포커스·hover·pressed 상태를 제공한다. 다섯 버튼의 외형은 같은 프레임을 공유한다.

## 5. 우측 획득 패널

`SessionRewardPanel`은 접힌 헤더와 펼친 목록을 같은 anchor에서 전환한다.

### 접힌 상태

- 신규 `shared/assets/cozy-hud-v1/paper/reward-panel-shell-9slice.png`를 낮게 9-slice 배치
- 빨간 배지 원과 chevron: 내부 숫자를 포함하지 않고 CSS로 렌더링한다.
- 배지 숫자는 현재 표시 기준에 맞춘 동적 값이다.

### 펼친 상태

- 신규 `shared/assets/cozy-hud-v1/paper/reward-panel-shell-9slice.png`
- 반복 행 배경은 CSS로 렌더링하고 가로 구분에는 신규 `controls/divider-dotted-horizontal.png` 타일을 사용한다.
- 페이지 전환 chevron: CSS로 렌더링한다.

한 화면에는 최대 네 행만 배치한다. 각 행의 아이콘, 이름, 누적 수량은 현재 세션 획득 목록에서 렌더링한다. 기존 쌀·고구마·옥수수·감자 아이콘과 신규 `icons/reward-skillbook.png`를 사용한다. 다섯 번째 이후 품목은 패널을 늘리지 않고 페이지 또는 내부 목록 전환으로 확인한다.

현재 체류 획득 내역이 없으면 `rewards-empty.png`, 인벤토리 공간 부족으로 일부 보상이 지급되지 않으면 `inventory-full-warning.png`를 보조 상태 아이콘으로 표시한다. 경고 설명과 미지급 수량은 이미지에 넣지 않고 API 값으로 렌더링한다.

## 6. 하단 레벨·HP·EXP 상태 바

`PlayerCombatStatus`는 배경 한 장에 수치를 포함하지 않는다.

필요 에셋:

- 신규 `shared/assets/cozy-hud-v1/paper/status-panel-shell-9slice.png`
- 신규 `shared/assets/cozy-hud-v1/controls/divider-dotted-vertical.png`
- 기존 `character/assets-cozy-pixel/heart-icon-128.png`
- 기존 `character/assets-cozy-pixel/sprout-icon-32.png`
- 기존 `character/assets-cozy-pixel/progress-track-9slice.png`
- HP 채움은 신규 `shared/assets/cozy-hud-v1/controls/progress-fill-coral-9slice.png`, EXP 채움은 기존 `progress-fill-cyan-9slice.png`

HP와 EXP 트랙의 표시 길이는 동일하게 맞춘다. 단위와 채움 비율만 각각 다르며, 현재·최대 값과 EXP 퍼센트는 코드가 오른쪽에 렌더링한다. 값 변경 애니메이션은 에셋이 아니라 컴포넌트가 담당한다.

## 7. 하단 성장 메뉴

`GrowthBottomNav`는 캐릭터·장비·아이템·스킬·보석의 다섯 독립 버튼이다.

공통 에셋:

- 신규 `shared/assets/cozy-hud-v1/paper/bottom-nav-shell-9slice.png`
- 신규 `shared/assets/cozy-hud-v1/controls/divider-dotted-vertical.png`
- 신규 `shared/assets/cozy-hud-v1/controls/nav-selected-marker.png`
- hover·pressed 면은 CSS 오버레이

기존·신규 아이콘 에셋:

- 캐릭터: 신규 `shared/assets/cozy-hud-v1/icons/chopstick-head.png`
- 장비: 기존 `sword-icon-128.png`
- 아이템: 신규 `shared/assets/cozy-hud-v1/icons/bottom-items.png`
- 스킬: 신규 `shared/assets/cozy-hud-v1/icons/bottom-skills.png`
- 보석: 신규 `shared/assets/cozy-hud-v1/icons/bottom-gems.png`

캐릭터 아이콘은 사람 얼굴이 아니라 젓가락 상단 얼굴을 사용한다. 선택 밑줄과 화살표는 현재 라우트에 따라 이동하며 메뉴 전체 이미지에 고정하지 않는다.

## 8. 공통 종이 UI 조립 원칙

반복 가능한 공통 패널은 기존 parchment·button 9-slice를 사용하고, 화면 비율과 가장자리 형태가 고유한 HUD 구조에는 전용 종이 프레임을 사용한다. 텍스트와 상태값은 계속 코드로 조립한다.

1. 프로필·스테이지 카드: 신규 `paper/profile-summary-shell-9slice.png`, `paper/stage-progress-shell-9slice.png`
2. 밝은 직사각 버튼: 기존 `secondary-button-shell-9slice.png`
3. 상단 정사각 기능 버튼: 신규 `utility-button-shell.png`
4. 진행 바: 기존 track·cyan fill과 신규 coral fill
5. 상태·하단 메뉴·전투 기록·획득 패널: 신규 `paper/*-9slice.png`
6. 가로·세로 도트선과 선택 밑줄·pointer: 신규 `controls/divider-*`, `controls/nav-selected-marker.png`
7. chevron과 hover·pressed·selected·disabled: CSS 상태 오버레이

모든 PNG는 투명 배경, 정수 배율 확대, `image-rendering: pixelated`를 전제로 한다. 글자와 숫자는 포함하지 않는다. 종이 프레임 중앙부는 늘여도 질감 이음새가 보이지 않도록 만들고 모서리는 고정한다.

## 권장 폴더 구조

```text
apps/web/src/shared/assets/cozy-hud-v1/
├─ icons/
│  ├─ nav-*.png
│  ├─ bottom-*.png
│  └─ battle-event-*.png
├─ controls/
│  ├─ utility-button-shell.png
│  ├─ progress-fill-coral-9slice.png
│  ├─ nav-selected-marker.png
│  └─ divider-dotted-*.png
├─ paper/
│  └─ *-shell-9slice.png
└─ README.md
```

이 경로는 현재 생성 결과다. 기존 공용 에셋은 원래 경로에서 import하며, 이전 마이페이지 에셋 폴더를 참조하거나 재사용하지 않는다.

## 제작 순서

1. 기존 종이·버튼·캐릭터·재료·상태 에셋 재사용
2. 신규 HUD 아이콘 24개, 공통 컨트롤 5개, 전용 종이 프레임 6개 적용
3. 빈 프레임 위에 가변 텍스트·수치·반복 행을 코드로 얹은 동작 프로토타입 검수

## 확인이 필요한 표시 의미

- 접힌 획득 패널의 빨간 배지가 `현재 세션의 획득 품목 종류 수`인지 `아직 확인하지 않은 갱신 수`인지 구현 전에 확정해야 한다.
- 전투 기록 보존 개수와 더 보기 방식은 [계정·저장 SSOT](../../30-domain/player/ssot.md)의 최근 전투 기록 정책 및 실제 API 계약을 확인해 정한다.
- 이 명세는 레이아웃·렌더링 분리를 설명하며 서버가 확정하는 게임 결과를 클라이언트가 계산하게 만들지 않는다.
