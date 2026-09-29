# Cosmetics cozy-pixel assets

치장 뽑기 화면에서 사용하는 독립 PNG 에셋 묶음이다. 기능 규칙과 콘텐츠 확정 상태는 [`docs/30-domain/cosmetics/ssot.md`](../../../../../../docs/30-domain/cosmetics/ssot.md)와 [`docs/60-content/cosmetics/mvp-v1.md`](../../../../../../docs/60-content/cosmetics/mvp-v1.md)를 따른다.

## 에셋 목록

- `cosmetic-draw-ticket-v1.png`: 금빛 옷걸이 문양이 있는 산호색 치장 뽑기권. 256×256 ARGB PNG이며 투명 배경과 굵은 초콜릿색 픽셀 외곽선을 사용한다.
- `cosmetic-selector-box-v1.png`: 200회 마일스톤에서 사용하는 크림·산호색 옷장형 선택 상자. 256×256 ARGB PNG이며 금빛 옷걸이 문양과 투명 배경을 사용한다.
- `cosmetic-ticket-hero-v1.png`: 뽑기 메인 화면 중앙의 대형 치장 티켓.
- `cosmetic-draw-button-shell-v1.png`: 비용 영역을 포함한 1회·10회 공용 버튼 셸.
- `cosmetic-reveal-background-v1.png`: 치장 결과 화면 전용 길드 내부 배경.
- `cosmetic-card-normal-v1.png`: 노말 결과 카드 셸.
- `cosmetic-card-rare-v1.png`: 희귀 결과 카드 셸.
- `cosmetic-card-epic-v1.png`: 영웅 결과 카드 셸.
- `cosmetic-card-legendary-v1.png`: 전설 결과·선택 카드 셸.
- `cosmetic-new-ribbon-v1.png`: 최초 획득 카드 우측 상단 NEW 리본.

희귀·영웅 카드 원본은 바깥 투명 영역이 완전한 알파가 아니므로 런타임 CSS에서 카드 윤곽으로 잘라 사용한다. 실제 치장 이미지는 콘텐츠의 nullable `imageUrl` 자리에 별도로 합성한다.

## 생성 기준

2026-09-11 기존 쌀 골드, 스킬북, 아이템 주머니, 거래소 상점 에셋을 화풍 참고로 사용해 생성했다. 실제 UI에서는 48–64px에서도 실루엣이 읽히도록 `image-rendering: pixelated`와 `object-fit: contain`을 함께 사용한다. 에셋은 UI 재화 표식이며 치장 콘텐츠의 실제 명칭·이미지를 확정하지 않는다.
