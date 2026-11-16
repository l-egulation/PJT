# 1스테이지 배경 · 바닥 크림색과 원근 (C안)

채택된 분리 배경(산·먼 마을·가까운 마을), 구름·새, 보스방 둥근 창틀을 바닥 타일의 크림색에 맞춘 판이다. 원본 세트(`stage1-pixel-background-native-20261111`, `stage1-atmosphere-sky-ash-20261111`, `stage1-boss-approach-pixel-20261112`)는 그대로 두고 여기서 새로 뽑는다.

- 점마다 밝기는 그대로 두고, 바닥(#fef7ee)과 같은 노란 기운을 더한다.
- 먼 층일수록 하늘색(#FCF7EE)에 섞는다: 산 30%, 둥근 창틀 22%, 구름 20%, 먼 마을 16%, 가까운 마을·새 0%.
- 카메라 바탕색도 #FCF7EE로 맞춘다.

`runtime/layers.json`에 파일별 원본과 섞는 정도가 있다. `source/make_cream.py`가 다시 뽑는다. `preview/options-now-A-B-C.png`는 지금·A·B·C 시안 비교다(채택 C = 오른쪽 아래).
