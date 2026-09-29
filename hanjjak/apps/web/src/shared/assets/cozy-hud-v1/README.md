# Cozy HUD v1 assets

메인 전투 화면 위에 조립하는 HUD 전용 에셋이다. 이 폴더에는 저장소에 없던 아이콘과 최소 공통 컨트롤만 둔다. 닉네임, 재화, 스테이지, 처치 수, 획득 수량, 레벨, HP와 EXP는 이미지에 포함하지 않고 런타임에서 렌더링한다.

## 새로 생성한 아이콘

아이콘 파일은 모두 `128×128` 투명 PNG이며 실제 표시 크기는 `40~56px` 범위에서 정수 배율과 `image-rendering: pixelated`를 사용한다.

| 파일 | 용도 |
| --- | --- |
| `icons/battle-log-scroll-clock.png` | 전투 기록 열기 |
| `icons/nav-dungeon.png` | 던전 |
| `icons/nav-raid.png` | 레이드 |
| `icons/nav-market.png` | 거래소 |
| `icons/nav-ranking.png` | 랭킹 |
| `icons/nav-settings.png` | 설정 |
| `icons/bottom-items.png` | 하단 아이템 |
| `icons/bottom-skills.png` | 하단 스킬 |
| `icons/bottom-gems.png` | 하단 보석 |
| `icons/chopstick-head.png` | 좌측 프로필·하단 캐릭터에 쓰는 젓가락 얼굴 |
| `icons/monster-progress.png` | 스테이지 처치 진행 표식 |
| `icons/battle-event-stage-enter.png` | 전투 기록의 스테이지 입장 |
| `icons/battle-event-return.png` | 전투 기록의 복귀·재시작 |
| `icons/battle-event-victory.png` | 전투 기록의 승리 |
| `icons/battle-event-failure.png` | 전투 기록의 실패 |
| `icons/auto-battle.png` | 자동 전투 실행 상태 |
| `icons/auto-battle-paused.png` | 자동 전투 일시정지 상태 |
| `icons/monster-progress-boss.png` | 스테이지 보스 진행 표식 |
| `icons/inventory-full-warning.png` | 보상 수령 중 가방 가득 참 경고 |
| `icons/rewards-empty.png` | 현재 체류 획득 내역 없음 |
| `icons/stage-status-current.png` | 스테이지 선택의 현재 위치 |
| `icons/stage-status-cleared.png` | 스테이지 선택의 클리어 상태 |
| `icons/stage-status-repeat.png` | 스테이지 선택의 반복 대상 |
| `icons/reward-skillbook.png` | 획득 목록의 스킬북 품목 |

## 새로 생성한 공통 컨트롤

| 파일 | 크기 | 용도 |
| --- | --- | --- |
| `controls/utility-button-shell.png` | `192×192` | 상단 기능 메뉴의 글자·아이콘 없는 공통 버튼 껍데기 |
| `controls/progress-fill-coral-9slice.png` | `512×48` | HP·처치 진행에 공용하는 coral 채움 스트립 |
| `controls/nav-selected-marker.png` | `256×64` | 선택된 하단 메뉴 아래에 이동 배치하는 coral 밑줄·포인터 |
| `controls/divider-dotted-horizontal.png` | `256×16` | 반복 행과 카드 내부의 가로 도트 구분선 타일 |
| `controls/divider-dotted-vertical.png` | `16×256` | 상태 바와 하단 메뉴의 세로 도트 구분선 타일 |

## 새로 생성한 전용 종이 프레임

아래 파일은 텍스트나 아이콘을 포함하지 않은 투명 PNG다. 모서리는 고정하고 중앙부만 늘리는 9-slice 용도로 사용한다.

| 파일 | 크기 | 용도 |
| --- | --- | --- |
| `paper/profile-summary-shell-9slice.png` | `384×112` | 젓가락 얼굴·닉네임·보유 쌀 프로필 카드 |
| `paper/stage-progress-shell-9slice.png` | `640×128` | 현재 스테이지·선택 버튼·처치 진행 카드 |
| `paper/bottom-nav-shell-9slice.png` | `1024×168` | 캐릭터·장비·아이템·스킬·보석 하단 메뉴 |
| `paper/status-panel-shell-9slice.png` | `1024×112` | 레벨·HP·EXP 상태 영역 |
| `paper/battle-log-panel-shell-9slice.png` | `640×640` | 누적 전투 기록 확장 창 |
| `paper/reward-panel-shell-9slice.png` | `384×640` | 최대 네 행을 표시하는 획득 목록 |

## 실제 표시 비율 전용 종이 셸

작은 카드에서 종이 결이 사라지지 않도록 아래 파일은 9-slice가 아니라 컴포넌트의 실제 비율에 맞춘 배경으로 사용한다. 획득 패널·하단 상태/성장 바의 반듯한 크림 종이, 작은 홈, 얇은 갈색·금빛 이중 테두리를 기준으로 통일했다. 크기가 가변적인 전투 기록 확장창은 같은 화풍의 `reward-panel-shell-9slice.png`를 재사용한다.

| 파일 | 원본 비율 | 용도 |
| --- | --- | --- |
| `paper/profile-history-shell-dedicated-v2.png` | `3:1` | 좌상단 프로필과 전투 기록 열기 버튼 |

## 기존 에셋 재사용

| 표시 | 기존 파일 |
| --- | --- |
| 공통 종이 프레임 | `src/shared/assets/onboarding-cozy-pixel/parchment-shell-9slice.png` |
| 공통 밝은 버튼 | `src/shared/assets/onboarding-cozy-pixel/secondary-button-shell-9slice.png` |
| 장비 | `src/features/character/assets-cozy-pixel/sword-icon-128.png` |
| 쌀 | `src/features/character/assets-cozy-pixel/rice-icon-128.png` |
| 하트 | `src/features/character/assets-cozy-pixel/heart-icon-128.png` |
| EXP 새싹 | `src/features/character/assets-cozy-pixel/sprout-icon-32.png` |
| 진행 바 트랙 | `src/features/character/assets-cozy-pixel/progress-track-9slice.png` |
| EXP 진행 채움 | `src/features/character/assets-cozy-pixel/progress-fill-cyan-9slice.png` |
| 스테이지 잠금 | `src/features/auth/assets-cozy-pixel/auth-field-lock-icon.png` |
| 고구마 | `src/features/material-preference/assets/sweet-potato-item-pixel-192.png` |
| 옥수수 | `src/features/material-preference/assets/corn-item-pixel-192.png` |
| 감자 | `src/features/material-preference/assets/potato-item-pixel-192.png` |
| 전투 기록 최근 전투 | `src/features/character/assets-cozy-pixel/sword-icon-128.png` |

`src/features/profile/assets-cozy-pixel*`의 이전 마이페이지 전용 에셋은 이 HUD에서 참조하지 않는다.

## 이미지로 만들지 않는 요소

- 글자, 숫자와 날짜
- 진행 바의 실제 너비
- 접힌 획득 패널의 배지 숫자
- chevron과 단순 점·체크·느낌표 상태 마크
- hover, pressed, focus-visible 상태

이 요소들은 CSS와 접근 가능한 HTML로 구현해야 API 갱신, 키보드 포커스와 화면 크기 변화에 대응할 수 있다.

## 생성 정보

- 생성 방식: Codex 내장 이미지 생성
- 스타일 기준: 밝은 아이보리 종이 UI, 굵은 짙은 갈색 외곽선, 따뜻한 코지 픽셀 색상
- 출력 후 처리: 투명 영역 기준 crop, nearest-neighbor 방식으로 아이콘·컨트롤·9-slice 프레임을 용도별 규격으로 정규화
- 검수: 아이콘 24종, 공통 컨트롤 5종, 9-slice 종이 프레임 6종, 실제 비율 전용 종이 셸 1종 등 총 36개 파일의 실제 alpha와 출력 크기 확인
