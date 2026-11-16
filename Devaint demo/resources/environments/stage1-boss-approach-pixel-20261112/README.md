# 1스테이지 보스 길목·보스방 뒷그림 · 2×2 도트 픽셀판

보스방 앞 긴 통로의 낮은 기둥 회랑과 보스방의 탑·들보·둥근 창틀을 지형(stage1-pixel-lawn-wood-32)과 같은 격자로 그린 뒷그림 세트다. 32px = 1유닛, 기본 도트 2×2px.

## 사용할 PNG

`runtime/layers.json`에 크기·기준점·정렬 순서가 있다.

| 대상 | 파일 | 크기 | 배치 |
|---|---|---|---|
| 길목 기둥 회랑 한 칸 | approach-colonnade | 440×208 | 통로 바닥에 붙여 가로로 반복. 위 8px는 6유닛 천장 밑으로 들어간다 |
| 보스방 탑·들보 | boss-room-towers | 960×486 | 보스방 왼쪽 아래 모서리에 맞춘다 |
| 둥근 창틀 | boss-room-rose-window | 960×486 | 같은 자리, 산과 마을 배경 사이 |

## 제작

참고 그림(`environments/stage1-clockroom-original-assets-20261112`의 길목, `environments/stage1-clockroom-monumental-20261111`의 a-arena)을 격자에 대고 블록 단위로 따라 그렸다. 색은 참고 그림에서 뽑았고, 돌 면 안의 음영도 참고 그림에서 읽어 두 단계로만 얹었다. 이음선은 배경이라 연하게, 금은 마지막에 끊기지 않는 가는 선으로 그린다. `source/`의 Python이 그림을 다시 만든다(`s_corridor3.py`, `s_bossroom2.py`, `s_bossring2.py`). `preview/`는 참고 그림과의 비교와 960×540 합성 화면이다.
