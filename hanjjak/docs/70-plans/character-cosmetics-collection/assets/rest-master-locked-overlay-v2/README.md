# REST 마스터 잠금형 신규 치장 10종 v2

## 편집 원칙

- 기준 마스터: `rest-master-LOCKED-UNMODIFIED.png`
- 기준 마스터 SHA-256: `67E9DE556A36C7D8684C206BF81A7F639FEF7332CB2829AECBF4B93916FA9820`
- 마스터는 재생성, 리터치, 리사이즈, 색 보정, 신체 비율 보정하지 않는다.
- 치장은 마스터와 같은 `1474×1067` 캔버스의 별도 투명 PNG 레이어다.
- 합성 프리뷰는 마스터를 하단에 두고 치장 레이어를 위에 올리도록 알파 합성한 검수용 산출물이다.
- 치장 레이어의 투명 픽셀 좌표에서는 합성 프리뷰의 RGBA가 마스터의 RGBA와 완전히 동일해야 한다.
- 각 치장 레이어는 불투명 색상 최대 16색, 4픽셀 블록, 이진 알파, 디더링·평활화·윤곽선 팽창 없음으로 저장한다.

## 폴더

- `overlays/`: 목재 신체가 없는 실제 투명 치장 레이어 10종
- `composite-previews/`: 잠긴 마스터 위에 치장 레이어만 올린 검수본 10종
- `generated-overlay-source/`: 치장 레이어 생성 원본
- `locked-master-composite-contact-sheet.png`: 10종 비교 시트

## 신규 콘셉트

1. Inkstorm Calligrapher
2. Coral Tide Navigator
3. Blooming Mushroom Alchemist
4. Thunder Kite Pilot
5. Royal Confectioner
6. Moon Rabbit Festival Herald
7. Verdant Marionette Gardener
8. Stained Glass Cathedral Warden
9. Deep Sea Lantern Diver
10. Paper Theater Shadowmaster

## 검증 결과

- 마스터 원본과 보관 사본의 SHA-256 일치
- 10개 오버레이 모두 `1474×1067` 투명 PNG
- 10개 오버레이 모두 불투명 색상 수 16 이하
- 10개 합성본 모두 오버레이 알파가 0인 좌표에서 마스터 픽셀과 완전 일치

치장 시스템의 정식 외형 슬롯은 `HEAD`, `TOP`, `BOTTOM`, `GLOVES`, `SHOES`, `CAPE`이며, 무기 비주얼은 이 시안에서 별도 부착 자산으로 취급한다. 정책 근거는 `docs/30-domain/cosmetics/ssot.md`다.
