# 2스테이지 기믹 구조물(색만 바꾼 판)

2026-11-13 사용자 선택. 1스테이지 봉인 석상·철창·잠긴 상자·봉인 보상 상자·열쇠·낙하 그림자를 모양 그대로 두고 색만 2스테이지 v26 지하수로 색으로 바꾼 판이다. 2챕터 봉인 도전방·잠긴 상자방에 쓴다(메인 `docs/design/world/stage-gimmicks.md`).

| 대상 | 원본 | 색 |
|---|---|---|
| 석상(`seal-*`) | `stage1-pixel-structures-20261112/runtime` | 이끼 낀 청록 돌(v26 `COOL` 10~3단계) |
| 철창(`iron-gate-*`) | 같은 세트 | 어두운 청록 쇠(`COOL` 9~1단계) |
| 잠긴 상자·보상 상자(`*-chest-*`) | 같은 세트 | 갈색 나무(`WARM` 9~2단계, 등불빛) |
| 낙하 그림자 | `village/runtime/stage-gimmicks-20261105` | 지하수로 바닥의 검푸른 얼룩 |
| 열쇠 | 같은 세트 | 그대로(의미 있는 색) |

석상의 노란 눈·불빛도 의미 있는 색이라 그대로 둔다. 색 단계는 `stage2-sewer-modular-20261111/stage2-sewer-modern-v26/source/render_modern.py`의 `COOL`·`WARM`이다.

- `runtime/`: 게임용 PNG(원본과 같은 크기, PPU 32 기준).
- `preview/compare-ch1-ch2.png`: 1스테이지 원본과 바꾼 색 비교(3배).
- `preview/in-sewer-ch2.png`, `preview/in-sewer-ch1-original.png`: v26 지하수로 미리보기 위에 놓은 모습(2배). 사용자가 이 시안을 보고 채택했다.

철창은 어두운 벽 앞에서 대비가 약하다. 플레이에서 길이 막힌 것이 잘 안 보이면 철창만 밝게 다시 정한다(미정).

## 재제작

`python source/recolor.py`(이 폴더에서, Pillow 필요)가 두 원본 세트에서 `runtime/`을 다시 만든다.
