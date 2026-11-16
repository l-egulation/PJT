# 채택한 1스테이지 잔디·흙·나무 발판

32×32px PNG 한 장이 PPU32에서 1유닛이다. 그림의 기본 도트는 2×2px다. 잔디와 흙이 한 타일에 들어 있으며 지형·나무 발판·꽃·접합 부품의 미술이 채택되었다. 본 게임 배치 계약 반영 전이며 격리 Unity 6000.6.5f1에서 검토한다.

## 재사용 규격

- runtime/terrain: 47개 연결 형태 × 4변주, 188장. tileset.json의 maskBits·canonicalMaskForRawMask·tiles로 선택한다.
- terrain-atlas.png: 512×384px, 32px 격자, 간격·여백 0, atlasXY는 왼쪽 위 원점이다.
- runtime/wood: left / middle-a / middle-b / right / single, 각 32×32px. N칸은 좌끝·중간·우끝으로 조립한다. 아래 받침은 없다.
- 접촉면은 PNG 위에서 8px 아래다. 지형 검토 표시는 기존 충돌 면에 맞춰 위로 0.25유닛 이동한다. 중복 오프셋을 적용하지 않는다.
- runtime/junctions: 네 방향 안쪽 모서리 부품, 32×32px, 피벗 (16,16), 반경 6px.
- 꽃 뿌리 (16,24)를 잔디 밑 지지면에 정렬한다. 벽 풀·자갈·빗금 패치는 장식이며 충돌을 만들지 않는다.
- Point / Compression None / Mip Maps Off / PPU32. 기본 검토 출력 960×540, orthographicSize 8.4375에서 1u=32 화면 px다.

[조립 1배](preview/assemblies-1x.png) · [조립 2배](preview/assemblies-2x.png) · [최종 배경·캐릭터가 함께 있는 Unity 화면](../stage1-atmosphere-sky-ash-20261111/preview/index.html).

## 재출력

source/approved-design.png는 채택 디자인 참고본, source/LawnKit.cs는 네이티브 그림과 조립 출력의 편집 원본이다. PowerShell의 source/build.ps1로 재출력한다. 오래된 맵 합성 도구나 실험 폴더에 의존하지 않는다. 별도 출력 경로에서 검토하려면 동일한 runtime/preview 폴더 구조를 만든 뒤 LawnKit.Build(path)를 호출한다. 저장된 채택 PNG를 재인코딩하지 않는다.
