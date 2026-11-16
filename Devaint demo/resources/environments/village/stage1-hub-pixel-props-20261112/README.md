# 1스테이지 마을·튜토리얼 구조물 · 2×2 도트 픽셀판

우물 마을(허브)과 튜토리얼 길의 구조물을 전투 구조물 세트(`environments/stage1-pixel-structures-20261112`)와 같은 격자로 다시 그린 세트다. 32px = 1유닛(PPU 32), 기본 도트 2×2px, 선은 1점 굵기. 건물(기록 농가·팔레트 공방)은 이 세트에 없다.

한 그림을 두 벌로 뽑는다. `sketch`는 색이 빠진 마을(지형 6색 #fffcf7 #fef7ee #ccc7be #a09d96 #6f6e69, 먹 #3f3e3c), `color`는 색이 돌아온 마을이다.

| 대상 | 파일 | 크기 |
|---|---|---|
| 우물 | runtime/props/*/return-well.png | 126×138 |
| 기록 책상 | runtime/props/*/record-desk.png | 92×70 |
| 장인 작업대 | runtime/props/*/palette-bench.png | 106×88 |
| 출발 이정표(튜토리얼 팻말 겸용) | runtime/props/*/departure-sign.png | 62×84 |
| 낮은 담 | runtime/props/*/low-wall.png | 90×48 |
| 손수레 | runtime/props/*/handcart.png | 96×62 |
| 튜토리얼 다리 기둥·상자·울타리 | runtime/course-props/{bridge-pier,crate,fence}(-sketch).png | 54×90 · 52×64 · 48×52 |

`runtime/props.json`에 크기·기준점이 있다. 밑줄이 바닥 접점이고 기준점은 아래 가운데다.

## 제작

`source/`의 스크립트가 격자 위에 재질 이름(나무·돌·밧줄·잎·물감…)으로 그리고 `SKETCH`·`COLOR` 팔레트로 두 번 칠한다(`px.py`의 lint로 2×2 뭉침·ㄱ자 꺾임 검사). 손수레·장인 작업대는 승인 그림의 모양을 따라 `hub_redo.py`로 다시 그렸고, 기록 책상은 승인 그림을 10색으로 줄여 픽셀화한 색 판(`pixelize.py`)에서 점을 밝기 순서대로 지형 6색에 옮겨 스케치 판을 만들었다(`sk_from_co.py`). `preview/unity-*.png`는 Unity 6000.6.5f1에서 실제 마을·튜토리얼 길을 960×540으로 찍은 지금/새 그림 비교다.
