# 2챕터 보스 앞 통로·보스방 v26 배경 (2026-11-13)

「보스 길목·보스방 시안」 2차(2026-11-12 사용자 OK)를 게임 좌표에 맞춰 자른 뒷배경이다. 시안은 v26 지하수로 렌더러로 그렸고 등빛·채광·어둠이 그림에 구워져 있다.
통로 48u×6u 는 ① 평범한 수로 → ② 배관 통로(굵은 관·받침 기둥) → ③ 보스 예고(어두워짐·밸브·관측창·바닥 격자·올라간 수문)로 서서히 바뀐다.
보스방(전장 30×18u)은 가운데 닫힌 큰 수문과 밸브 상자, 양옆 랜턴, 천장 격자, 배수구 물줄기 둘. 발판·기계·출구 문은 Unity 가 따로 그린다.

## runtime (Unity Chapter2/Resources/Chapter2Waterway/Boss/)

| 그림 | 크기 | 자리 |
|---|---|---|
| boss-hall | 1536×192 (48×6u) | 통로 안쪽 바닥 위. 보스방이 통로 왼쪽이면 좌우를 뒤집는다 |
| boss-hall-grates | 1536×9 | 통로 바닥 격자 둘. 윗변 = 바닥 윗면 1px 아래 |
| boss-room | 960×480 (30×15u) | 보스방 바닥 윗면(전장 y 3) 위 |
| boss-floor | 960×104 (30u × 3u+8px) | 입장·2·3페이즈 바닥(전장 y 0..3, 위 8px 는 이끼) |
| boss-room-air | 960×576 (30×18u) | 1페이즈 공중전 판. 전장 전체(바닥 자리 포함)를 덮는다. 바닥 없이 양옆 벽·수문·물줄기가 전장 아래 끝까지 이어진다 |

## 다시 뽑기

```
cd source
python render_boss.py ../build                 # 시안 전체(background·foreground·full)
python render_boss.py ../build-air --air       # 1페이즈 공중전 판(전장 바닥 3u 를 비움)
python cut_boss.py ../build ../build/runtime ../build-air    # 게임 조각 다섯 장
```

`render_map.py`·`render_v26.py` 는 v26 렌더러 코어다. 같은 저장소의 `stage2-sewer-modular-20261111`(v25 세트·채택 v21 타일·맵·캐릭터)을 읽는다. Pillow·NumPy·SciPy 가 필요하다. 런타임 다섯 장은 04_테스트 시안 출력과 바이트까지 같다.

## preview

- `full.png` 통로와 보스방 전체(시안 좌표 x -14..84).
- `room-30u-phase1-and-clear.png` 보스방을 30u 고정 화면으로 본 1페이즈 공중전 판(발판·결정)과 처치 뒤(바닥·가운데 출구 문).
