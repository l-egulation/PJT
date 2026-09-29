# 2층 상가형 챕터 선택 에셋

승인된 `chapter-selection-2d-shopping-arcade-v2.png`를 기준으로 분리 제작하는 게임 UI 에셋이다.

## 현재 기준 에셋

| 파일 | 용도 | 규격 |
| --- | --- | --- |
| `shops/chapter-01-seongsimdang.png` | 실제 게임용 점포 버튼 | 112×108 RGBA |
| `shops/chapter-01-seongsimdang-highres.png` | 수정·재축소용 고해상도 원본 | 1051×978 RGBA |
| `preview/chapter-01-seongsimdang-alpha-check.png` | 밝은/어두운 배경 알파 검수 | 512×248 RGB |
| `raw-generated/chapter-01-seongsimdang-source.png` | 내장 이미지 생성 원본 | 1278×1230 RGB |

게임용 점포 에셋 제작이 완료되었다.

| 챕터 | 파일 |
| --- | --- |
| 1 | `shops/chapter-01-seongsimdang.png` |
| 2 | `shops/chapter-02-cookie.png` |
| 3 | `shops/chapter-03-sushi.png` |
| 4 | `shops/chapter-04-jeon.png` |
| 5 | `shops/chapter-05-drink.png` |
| 6 | `shops/chapter-06-baseball-food.png` |
| 7 | `shops/chapter-07-campus-cafeteria.png` |
| 8~10 | `shops/chapter-locked-shop.png` 공용 |

전체 비교 검수는 `preview/chapter-shops-contact-sheet.png`에서 확인한다. 각 점포에는 대응하는 `-highres.png`, `raw-generated/*-source.png`, `preview/*-alpha-check.png` 파일이 있다.

## 공통 상가 배경

| 파일 | 용도 | 규격 |
| --- | --- | --- |
| `background/chapter-select-arcade-bg.png` | 실제 게임용 공통 배경 | 640×360 RGB |
| `background/chapter-select-arcade-bg-highres.png` | 수정·재축소용 원본 | 1672×941 RGB |
| `raw-generated/chapter-select-arcade-bg-source.png` | 내장 이미지 생성 원본 | 1672×941 RGB |
| `arcade-layout.json` | 10개 점포의 행·열 배치 좌표 | 640×360 기준 |
| `preview/arcade-background-with-shops-check.png` | 배경과 점포 조립 검수 | 640×360 RGB |

공통 배경은 하늘, 지붕, 5×2 빈 벽면, 기둥, 난간, 양쪽 계단, 석재 바닥만 포함한다. 점포·번호·선택 상태·화면 UI는 포함하지 않는다.

## 점포 버튼 인터랙션 — 현재 채택안 v2

가게 그림 자체를 버튼으로 사용한다. 사각형 카드나 모서리 프레임을 덧씌우지 않는다.

| 상태 | 표시 방식 |
| --- | --- |
| 기본 | `shops/*.png`, 100% |
| 호버 | 같은 이미지를 바닥 중앙 기준 106% 확대 |
| 누름 | 102%로 짧게 축소 |
| 선택 | `shops-selected/*-selected.png`, 106% 유지 + 가게 실루엣을 따르는 금빛 픽셀 외곽선 |
| 키보드 포커스 | 선택 상태와 같은 금빛 실루엣 표시 |
| 잠금 | 확대 없음, 공용 잠금 점포를 62% 불투명도로 표시 |

- 변형 기준점: `bottom center` (`50% 100%`)
- 호버·선택 전환: 90ms, ease-out
- 누름 전환: 60ms
- 확대 시 이웃 점포보다 앞에 보이도록 z-index를 올린다.
- 실제 적용값과 챕터 매핑: `shop-interaction-manifest-v2.json`
- 정적 비교: `preview/shop-button-interaction-v2.png`
- 동작 미리보기: `preview/shop-button-interaction-v2.gif`
- 전체 화면 예상 조립 결과: `preview/chapter-select-assembled-selected-v2.png`

`shops-selected/`의 8개 파일은 원본 점포의 위치와 112×108 캔버스를 그대로 유지한다. 금빛 선은 투명 실루엣을 팽창시켜 만든 픽셀 외곽선이므로 점포 주변에 네모난 판이 생기지 않는다.

## 점포 상태 UI — 이전 v1, 비채택 보존본

아래 사각 오버레이 방식은 현재 사용하지 않는다. 비교와 복구를 위해 파일만 보존한다.

### 점포 오버레이 — 112×108 RGBA

- `states/shop-hover.png`
- `states/shop-selected.png`
- `states/shop-pressed.png`
- `states/shop-focus.png`
- `states/shop-disabled.png`

점포 위에 같은 좌표로 합성한다. 기본 상태는 별도 오버레이가 없다.
비활성 상태에서는 `shop-disabled.png`를 합성하고 점포 본체의 불투명도를 약 45%로 낮춘다.

### 챕터 번호 배지 — 28×28 RGBA

- `badges/chapter-badge-normal.png`
- `badges/chapter-badge-selected.png`
- `badges/chapter-badge-locked.png`
- `badges/chapter-badge-cleared.png`

숫자 `1~10`은 Galmuri11 등 게임 폰트로 배지 중앙에 출력한다.

### 상태 아이콘 — 16×16 RGBA

- `icons/icon-check.png`
- `icons/icon-lock.png`
- `icons/icon-clear-star.png`

이전 상태 비교는 `preview/shop-state-assets-contact-sheet.png`에서 확인한다.

숫자 배지, 선택 표시, 잠금 표시와 챕터명은 점포 이미지에 포함하지 않는다. 공통 UI 레이어로 합성한다.

## 제작 기준

- 정면 직교 시점
- 가게 하나의 네이티브 캔버스는 112×108
- 외곽 4px 이상 투명 여백
- 아래쪽 기준선에 맞춰 배치
- 최근접 보간 축소
- 진갈색 픽셀 외곽선과 2~3단 셀 셰이딩

## 챕터 순서

1. 성심당
2. 쿠키
3. 초밥
4. 전집
5. 음료
6. 야구장 먹거리
7. SSAFY 캠퍼스 식당
8. 잠금
9. 잠금
10. 잠금

## 검증 결과

- 게임용 8개 파일 모두 112×108
- 모두 RGBA PNG
- 네 모서리 알파 0
- 불투명 영역 바닥선 y=101로 통일
- 4번 전집, 5번 음료 순서 반영
- 상가 배경과 점포 조립 결과 640×360 확인
- 상태 오버레이 5개, 배지 4개, 상태 아이콘 3개 RGBA 확인
- v2 금빛 실루엣 선택 에셋 8개 모두 112×108 RGBA, 네 모서리 알파 0 확인
