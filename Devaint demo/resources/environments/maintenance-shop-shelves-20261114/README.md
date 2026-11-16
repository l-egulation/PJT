# 정비소 방 안 진열 (2026-11-14)

정비소 상품은 상점 창 대신 방 안 진열칸에 놓인다(사용자 결정 2026-11-14). 1챕터는 정비소 건물을 한 채로 다시 그려 문 안쪽 오른편에 진열칸을 두었고, 2챕터는 방 벽을 판 벽감 하나에 정비공과 진열을 함께 넣었다. 잉크는 선반 판(바닥에서 2.5칸, 한 번 점프로 오른다) 위, 무기는 그 아래 바닥에 선다. 마을 공방의 상점 진열 확장(무기·잉크 각 +1·+2)을 사면 간격은 그대로 두고 진열칸이 길어진다(사용자 선택 2026-11-14, `preview/expansion.png`의 A안). 공용 새로고침을 해금한 런은 두 줄 끝에 보급 궤짝이 하나씩 선다. 사용자가 시안을 고르고 승인한 판이다(최종 한눈에: `preview/overview.png`).

## runtime (Unity Deviant/Game/Resources/MaintenanceShops/)

| 그림 | 크기 | 자리 |
|---|---|---|
| stage1-shop-3~6 | 922~1156×330 | 1챕터 건물 전체(울퉁불퉁한 큰 돌, 윗돌 풀·꽃, 천막, 공구걸이, 진열 선반 판). 끝 숫자는 한 줄 칸 수(상품 3~5개 + 리롤 궤짝)이고 칸이 늘면 같은 간격으로 진열칸이 길어진다 |
| stage2-shop-3~6 | 732~960×241 | 2챕터 벽감(벽돌 두 단 어둡게, 관·밸브, 전구 줄, 포스터·공구·병, 이끼·금·물웅덩이·렌치, 간판 사슬, 선반 판). 끝 숫자는 위와 같다 |
| sign-stage2 | 60×60 | 2챕터 팔레트 간판. 1챕터 간판과 같은 모양에 2챕터 나무·먹색 |
| stage1-reroll-crate, stage2-reroll-crate | 40×36 | 새로고침 보급 궤짝, 옆면에 둥근 화살표 |

장인·책상·등불·1챕터 간판은 기존 그림을 게임이 따로 놓는다. 선반 판의 자리와 등불·간판 위치는 `runtime/layout.json`(장인 발밑 기준, 칸 단위)에 있고 `MaintenanceShelfDisplay`·`MaintenanceRoomPresentation` 상수와 같다. 가격판(모서리 깎은 판, 가운데 금화와 가격)은 게임이 칠한다.

## 다시 뽑기

```
cd source
python export_runtime.py <메인 작업 폴더> ../runtime         # 칸 수별 건물·벽감 여덟 장, 궤짝·간판, layout.json
```

메인 작업 폴더의 배치된 정비소 그림(`stage1-backdrop`의 공구걸이, `stage2-backdrop`의 관·포스터 조각), 승인 테마, 이 저장소의 `stage1-pixel-structures-20261112/source/px.py`·`maintenance-shops-stage2-pixel-20261113/source/pal.py`를 불러 쓴다. Pillow·NumPy 가 필요하다.

1챕터 그림은 정비소 그림 8색만 쓴다(외곽선 6f6e69, 먹색 없음). 돌은 줄 맞추지 않고 크게 쌓는다(`stones.py`).

## source 의 다른 스크립트

- `overview.py` 최종 한눈에(두 챕터 × 기본·첫 방문 말풍선·잉크 카드·무기 카드·궤짝 앞)
- `anim.py` 새로고침 연출 시안(궤짝 덜컹, 상품 먼지 고리 속 교체)
- `reroll_objects.py`, `reroll_options.py` 새로고침 자리 시안(궤짝 확정)과 안 연 자리 천 덮개
- `expand_mock.py` 진열 확장 시안(A 단계마다 길어짐 채택, B 천 덮인 자리)
- `ch1_scene_new.py`, `ch2_recess.py`, `final_scenes.py`, `bubble.py`, `mock.py`, `approved_art.py` 시안 장면

## preview

- `overview.png` 최종 한눈에(왼쪽 1챕터, 오른쪽 2챕터).
- `reroll-stage1.gif`, `reroll-stage2.gif` 새로고침 연출 시안.
- `expansion.png` 진열 확장 시안(A·B).
