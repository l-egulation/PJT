# 2챕터 정비소 도트판 (2026-11-13)

2챕터 정비소(수로 정비공)의 뒷벽·작업대·랜턴·팔레트 간판을 게임 화면 크기 그대로(1px = 게임 1점, 32px = 1u) 격자에 손으로 다시 그렸다. 예전 그림(`environments/maintenance-shops`)은 큰 그림을 게임에서 줄여 그려 점이 고르지 않았다.

사용자 결정(2026-11-13): 디테일은 색을 더 쓰거나 모양을 깎아 챙긴다. 관은 2챕터 맵 관과 같은 방식(밝기 단계 → 수로 램프 + 기본 1.4단 + 4×4 베이어 디더, render_boss.Painter 의 관 단면·8px 띠·2×2 리벳)으로 칠한다. 상호작용하는 곳이므로 「불 켜진 작업장」으로 눈에 띄게: 랜턴·전구 줄·작업 공간·간판 둘레의 따뜻한 빛을 맵 등불과 같은 방식(단계 올림 + 따뜻한 램프 섞기)으로 굽고 바깥 벽은 한 단 어둡게, 전구 줄과 물감 선반(간판과 같은 빨강·파랑·노랑)을 더했다.

## runtime (Unity Deviant/Game/Resources/MaintenanceShops/)

| 그림 | 크기 | 자리 |
|---|---|---|
| stage2-backdrop | 480×270 | 정비소 방 바닥 가운데 아래 기준 뒷벽 |
| stage2-desk | 166×72 | 작업대(정비공 앞) |
| lantern | 20×38 | 불 켜진 랜턴(뒷벽 왼쪽 고리) |
| sign | 52×66 | 팔레트 간판(뒷벽 오른쪽 받침, 위 고리 기준으로 흔들림) |

## 다시 뽑기

```
cd source
python backdrop4.py ../build/stage2-backdrop.png
python desk.py ../build/stage2-desk.png
python small.py ../build            # lantern.png, sign.png
```

Pillow·NumPy 가 필요하다. 런타임 네 장은 이 스크립트 출력과 바이트까지 같다. `pal.py` 는 v26 수로 램프(차가운 11·따뜻한 11)와 종이·쇠 색.

## preview

- `shop-vs-map.png` 위: 2챕터 맵 게임 캡처, 아래: 같은 자리에 놓은 새 정비소(정비공·작업대·랜턴·간판 포함).
