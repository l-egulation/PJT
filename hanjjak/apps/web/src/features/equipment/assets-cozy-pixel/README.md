# Equipment cozy-pixel assets

장비 성장 화면의 확정 시안에서 사용하는 독립 PNG 에셋 묶음이다. 화면 동작과 강화·승급 규칙은 [`docs/30-domain/items/equipment/ssot.md`](../../../../../../docs/30-domain/items/equipment/ssot.md)를 따른다.

## 크기 규칙

- `*-96.png`: 장비 목록과 재료 행에 바로 사용하는 UI 크기
- `*-256.png`: 고밀도 화면, 확대 모달, 후속 리사이즈를 위한 원본 크기
- 아이콘 런타임 에셋은 정사각형 캔버스와 투명 알파 배경을 사용한다.
- `equipment-paper-panel-v2.png`: 장비 6행 보드용 얇은 갈색 픽셀 테두리의 가로형 크림 종이. 생성 결과의 바깥 안전 영역은 CSS 배경 크롭으로 제외한다.
- `equipment-loadout-courtyard-v2.png`: 장비 배치 영역 전용의 밝은 픽셀 정원 배경. 캐릭터와 장비 카드가 올라갈 중앙 여백을 비우고 배너·덩굴·화분·나무 상자는 가장자리에 배치했다.
- `*-v2.png`: 2026-09-10 `젓키 레퍼런스.pptx`의 밝은 아이보리·코랄·골드·올리브 팔레트에 맞춰 다시 그린 256px 런타임 아이콘. 내부 픽셀은 더 촘촘하고 바깥 초콜릿색 윤곽은 더 굵다.
- 거래소 모달은 생성 완료된 세로형 `market-modal-paper-v2.png`를 원본 비율로 표시한다. 컨테이너도 1199:1312 비율을 유지해 테두리와 코너 장식을 변형하지 않는다.
- `equipment-action-button-*-v2.png`, `market-purchase-button-red-v2.png`: 실제 표시 비율에 맞춰 다시 그린 강화·승급·구매 버튼 셸. 바깥 영역은 실제 알파 투명이다.
- 브라우저에서는 픽셀 윤곽 보존을 위해 `image-rendering: pixelated` 사용을 권장한다.

## 에셋 목록

| 역할 | 파일 접두어 |
| --- | --- |
| 기본 무기 | `equipment-weapon-basic` |
| 기본 장갑 | `equipment-gloves-basic` |
| 기본 갑옷 | `equipment-armor-basic` |
| 기본 투구 | `equipment-helmet-basic` |
| 기본 망토 | `equipment-cape-basic` |
| 기본 신발 | `equipment-boots-basic` |
| 쌀 모양 골드 | `currency-rice-gold` |
| 거래소 상점 | `market-stall` |
| 젓가락 마스코트 | `mascot-chopsticks` |
| 감자 재료 | `material-potato` |
| 고구마 재료 | `material-sweet-potato` |
| 옥수수 재료 | `material-corn` |
| 장비 보드 종이 | `equipment-paper-panel-v2.png` |
| 장비 배치 정원 배경 | `equipment-loadout-courtyard-v2.png` |
| 거래소 전용 종이 | `market-modal-paper-v2.png` |
| 강화 버튼 | `equipment-action-button-red-v2.png` |
| 승급 버튼 | `equipment-action-button-blue-v2.png` |
| 거래소 구매 버튼 | `market-purchase-button-red-v2.png` |

젓가락과 재료 3종은 기존 공용·주력 나라 에셋을 동일한 패딩 규칙으로 재출력했다. 장비 6종, 쌀 모양 골드, 거래소 상점은 승인된 장비 화면 시안의 화풍을 기준으로 새로 생성했다. 장비 보드 종이는 2026-09-10 사용자 제공 세로형 얇은 픽셀 테두리를 가로형으로 확장한 이미지 생성 결과다.

장비 화면의 쌀 모양 골드는 굵은 외곽선이 필요한 작은 표시 크기에서 `currency-rice-gold-256.png`를 사용한다. v2 런타임 묶음은 감자, 고구마, 옥수수, 거래소 상점과 기본 장비 6종이다. 생성 후 256×256 ARGB PNG로 축소했고 모서리 알파 0을 확인했다. 버튼 셸 3종도 32-bit ARGB와 모서리 알파 0을 확인했으며, 표시 높이를 바꾸지 않고 투명 안전 여백만 잘라냈다. 강화·승급 버튼은 모서리를 건드리지 않고 중앙 면만 조정해 동일한 3.60:1 외곽 비율로 맞췄다.
