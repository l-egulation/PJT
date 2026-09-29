# Cosmetics generated asset draft v1

`2026-09-11` 내장 이미지 생성 모드로 만든 치장 뽑기 UI 시안 에셋이다. 실제 치장 콘텐츠 이미지는 포함하지 않는다.

## 검수 상태

| 파일 | 역할 | 상태 |
| --- | --- | --- |
| `cosmetic-ticket-hero-v1.png` | 메인 화면 대형 뽑기권 연출 | RGBA, 모서리 알파 0 확인 |
| `cosmetic-draw-button-shell-v1.png` | 1회·10회·다시 뽑기 공용 버튼 셸 | RGBA, 모서리 알파 0 확인 |
| `cosmetic-reveal-background-v1.png` | 16:9 뽑기 결과 배경 | RGB 배경 에셋 |
| `cosmetic-card-normal-v1.png` | 노말 카드 프레임 | RGBA, 모서리 알파 0 확인 |
| `cosmetic-card-legendary-v1.png` | 전설 카드 프레임 | RGBA, 모서리 알파 0 확인 |
| `cosmetic-new-ribbon-v1.png` | 공용 `NEW` 리본 | RGBA, 모서리 알파 0 확인 |
| `cosmetic-card-rare-v1-preview-no-alpha.png` | 희귀 카드 프레임 시안 | 배경 알파 추출 실패, 런타임 사용 금지 |
| `cosmetic-card-epic-v1-preview-no-alpha.png` | 영웅(`EPIC`) 카드 프레임 시안 | 배경 알파 추출 실패, 런타임 사용 금지 |
| `cosmetic-card-back-v1-preview-no-alpha.png` | 공용 카드 뒷면 시안 | 배경 알파 추출 실패, 런타임 사용 금지 |

## 사용 원칙

- 카드 이름, 치장 이미지, 등급 문구, 가격과 보유 수량은 이미지에 굽지 않고 런타임에서 올린다.
- `preview-no-alpha` 파일은 디자인 선택용이며 구현 에셋으로 import하지 않는다.
- 등급 체계는 `NORMAL`, `RARE`, `EPIC`, `LEGENDARY` 네 종류를 기준으로 한다. 화면 표시는 노말, 희귀, 영웅, 전설이다.
- 승인 후 런타임 에셋은 `assets-cozy-pixel` 아래에 별도 버전명으로 옮긴다.

## 생성 방향

- 밝은 크림 종이, 산호색 버튼, 금색 하이라이트, 올리브 잎 장식과 굵은 초콜릿색 픽셀 외곽선을 사용했다.
- 대형 뽑기권은 작은 재화 아이콘과 분리해 메인 화면의 중심 연출로 제작했다.
- 카드 네 등급은 동일한 2:3 레이아웃과 빈 이름·이미지·등급 영역을 유지하도록 요청했다.
- 결과 배경은 다섯 장의 세로 카드를 놓을 수 있도록 중앙을 비운 길드 의상실과 금빛 스포트라이트로 제작했다.
