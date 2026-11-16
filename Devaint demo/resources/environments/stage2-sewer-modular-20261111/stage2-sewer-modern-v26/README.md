# 2스테이지 지하수로 v26

2026-11-12 사용자 선택. v25에서 바닥을 1u 파고 물을 채운 웅덩이를 더한 판이다(③ 잠김). 캐릭터와 적은 웅덩이 바닥에 서고, 반투명한 물 앞면이 몸 아래를 덮는다. 실제 `current` 레이아웃 8구간, 960×540, PPU 32이고 Unity에는 아직 적용하지 않았다.

- `runtime/background/`: RGB. v25와 같은 배경이다.
- `runtime/foreground/`: RGBA. v25 지형에 웅덩이 안벽(판 칸 바깥 땅 속 3px), 수면 위 2px 그늘, 물 뒷면을 더했다.
- `runtime/water-front/`: RGBA. 액터 앞에 덮는 물 앞면이다. 수면 줄은 진하게(알파 220), 물속은 연하게(알파 140). 액터가 없으면 전경과 합친 모습이 미리보기와 같다.
- `preview/`: 합성 미리보기. `preview/in-basin/`은 캐릭터가 웅덩이 바닥에 선 모습이다. `compare-v25-v26-*.png`, `detail-4x.png`, [review.html](review.html).
- `manifest.json`의 `views[].basins`: 웅덩이 월드 좌표 `x0`, `x1`, `floorTop`, `bottom`(1u 아래)과 물줄기 여부.

## 웅덩이 규칙

- 판 칸은 폭 2·3·4u × 깊이 1u다. Unity에서는 이 칸을 바닥 충돌에서 뺀다. 수면은 바닥 윗면 2px 아래다.
- 물줄기가 떨어지는 바닥에는 하나씩 둔다. 그 밖에는 바닥 288px마다 후보를 잡아 셋 중 둘을 둔다.
- 아래와 옆으로 8px 넘게 땅이 꽉 찬 곳에만 판다. 문 발치(문 테 + 30px), 캐릭터 발밑, 다른 웅덩이(40px 이내)는 피한다.
- v25의 문·등·물줄기 배치 규칙은 그대로다.

## 재제작

`source/build.py`가 `source/render_modern.py`로 8구간을 그린다. Python 3, Pillow, NumPy, SciPy가 필요하다.
