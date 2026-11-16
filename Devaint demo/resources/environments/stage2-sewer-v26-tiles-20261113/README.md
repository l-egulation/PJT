# 2챕터 지하수로 v26 화풍 · 타일 부품 (2026-11-13)

2챕터 맵은 판마다 시드로 새로 조립되므로, v26 지하수로 화풍(stage2-sewer-modular-20261111 의 v25·v26 통그림 시안)을 통그림 대신 부품으로 나눴다. Unity(Chapter2WaterwayArt·Chapter2SewerDressing·Chapter2SewerPlan)가 맵 모양에 맞춰 깔고, 문·등·채광·물줄기·배관·물웅덩이 자리를 v26 규칙대로 정한다. 사용자 결정(2026-11-12 「타일 방식」, 2026-11-13 창·시험 조명 소품 제거와 조명·깊이감·캐릭터 빛 맞춤).

## runtime (Unity Chapter2/Resources/Chapter2Waterway/Modern/)

| 부품 | 크기 | 쓰임 |
|---|---|---|
| terrain-atlas | 1024×1536 (32×48 칸 × 1024) | 땅 칸. 주변 8칸 모양 256 × 변형 4. 위아래 8px 는 공기 칸으로 넘치는 이끼·물때. 바위 화소는 비어 있다 |
| rock | 896×448 | 땅 속 어두운 바위. 월드 좌표에 고정, 반복 |
| wall-level · ramp | 576×512 · 11×2 | 뒷벽 밝기 단계 지도(R = 단계×20)와 팔레트. SewerWall 셰이더가 등빛 단계를 더해 칠한다 |
| pipe-level | 160×22 | 가로 배관 단계 지도(아래 4줄 = 벽 -1단 그늘) |
| pillar-base · pillar-cap | 36×14 · 36×10 | 9u 마다 선 기둥이 바닥·천장에 닿는 자리 |
| contact-shadow · ceiling-shade · floor-fog · wet-band | | 땅 접촉 그늘, 천장 220px 어둠, 바닥 140px 안개, 바닥 위 물때 |
| deck-* · bracket-left/right · chain | 32×18 … | 통과 발판 돌판(11종), 벽 받침, 사슬 |
| door-0..2 | | 터널 문(큰·중·작) |
| lamp-wall · lamp-hang · lamp-hang-chain · grate | | 벽등·매단 등·채광 격자 |
| waterfall · basin-2..4 · basin-front-2..4 · basin-splash | | 물줄기, 물웅덩이 물과 물 앞면(캐릭터 앞), 물보라 |

## 다시 뽑기

```
cd source
python export_tiles.py ../build     # runtime 은 이 출력의 사본(04_테스트 시안에서 바이트까지 같음을 확인)
```

`render_v26.py` 는 v26 렌더러다. 같은 저장소의 `stage2-sewer-modular-20261111`(v25 세트·채택 v21 타일·맵)을 읽는다. Pillow·NumPy·SciPy 가 필요하다.

## preview

Unity 실제 화면 캡처(예전 통그림과 같은 방 비교, 배관·물줄기, 캐릭터 빛 맞춤 전후, 주인공 빛 무리 3배).
