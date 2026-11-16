# 채택한 1스테이지 분리 배경

외곽선 없는 따뜻한 회갈색 산·먼 마을·가까운 마을이다. [캐릭터·채택 지형·하늘·종잇재를 함께 배치한 Unity 화면](../stage1-atmosphere-sky-ash-20261111/preview/index.html)에서 확인한다. 미술 채택본이며 본 게임 배치 계약 통합 전이다.

| 레이어 | PNG 크기 | 그림 도트 | 색 수 | 패럴랙스 X/Y |
|---|---:|---:|---:|---:|
| 가까운 마을 | 666×334 | 2×2px | 6 | .45 / .30 |
| 먼 마을 | 444×222 | 1px | 3 | .18 / .15 |
| 산 | 888×444 | 1px | 2 | .08 / .08 |

PPU32 / scale1 / Point / 압축 없음 / 밉맵 없음 / 이진 알파. 배경을 32px 사각 타일로 자르지 않고 각 분리 캔버스로 사용한다. 하늘은 #F4F1E9다. runtime/layers.json에 실제 팔레트와 규격을 기록했다. 가까운 마을은 contrast1.35와 종이색 혼합 .30, 먼 마을 contrast1.10, 산 contrast1.0의 채택 색 범위를 사용한다.

NativeBackground.shader는 PNG 색을 그대로 그린다. 레이어 하단의 일정한 면색을 Clamp 샘플링하는 연속 메시로 아래로 연장해서 이미지 캔버스의 사각 끝이 노출되지 않게 한다. 가로는 거울 반복이다. VirtualWallOutlineSoil.shader는 통과형 흙 벽의 채택 재질이다. #E9E2D5, textureContrast1.5, outlineWidth0을 사용하고 지형 atlas와 빗금 패치를 참조한다. 충돌 지형과 별개다.

source/inputs에는 채택한 그림의 생성 입력, source/*-redraw.png에는 기본 이미지 생성 도구의 정리 원본, source/requests.json에는 요청문을 보존한다. source/BuildNativeBackground.cs가 실제 크기의 격자·팔레트·알파를 확정한다. AI 출력 자체를 정확한 도트 원본이라고 간주하지 않는다. PowerShell source/build.ps1로 재출력한다. 현재 구름과 새는 [채택 하늘 세트](../stage1-atmosphere-sky-ash-20261111/README.md)가 소유한다.
