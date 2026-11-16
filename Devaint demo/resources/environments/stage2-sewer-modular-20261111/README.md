# 2스테이지 지하수로 타일

## 사용할 세트

- **채택본:** `stage2-unified-stone-v21/`의 통일 석재 타일과 깔끔한 통과형 석재 발판. 다른 발판 탐색안과 혼용하지 않는다.
- **채택본 배치 검토:** [배경 없는 실제 맵 8구간](stage2-unified-map-review-v22/review.html). 게임 지형과 착지 좌표를 보존한 합성본이다.
- **채택 화면 v26 (2026-11-12):** [v26 8구간](stage2-sewer-modern-v26/review.html). v25에 바닥을 1u 파고 물을 채운 웅덩이를 더했고, 물 앞면은 액터 앞 반투명 층으로 따로 뺐다. 웅덩이 좌표와 규칙은 [v26 README](stage2-sewer-modern-v26/README.md)에 있다.
- **이전 채택 v25:** [v25 8구간](stage2-sewer-modern-v25/review.html). 뒷벽과 소품을 새로 그렸고, 땅 속은 바위로 비우고 테두리 2~3줄에만 v21 벽돌을 쓴다. 통과 발판은 같은 색 단계의 돌판으로 다시 그렸고 물웅덩이를 넣었다. 문·등·물줄기 배치 규칙은 [v25 README](stage2-sewer-modern-v25/README.md)에 있다.
- **이전 배경 참고:** [공통 벽과 작은 구조물 v24](stage2-room-background-v24/review.html). v25로 대체되었다.
- Unity 적용 및 충돌 변경은 하지 않았다. 현재 게임에 연결된 기존 수로 에셋을 자동 교체하지 않는다.

## 픽셀 규격

| 항목 | 값 |
| --- | --- |
| 기본 PNG | 32×32px |
| Unity | PPU 32, 1타일 = 1유닛 |
| 돌 / 반장석 | 32×16px / 16×16px |
| 줄눈 | 1px |
| 통과 발판 | 그림 높이 최대 12px; 충돌 두께 4px는 적용 시 계약 |
| 규격 미리보기 | 960×540, 캐릭터 가시 영역 66×64px, 확대·리샘플링 없음 |

## 편집과 재제작

`stage2-unified-stone-v21/source/build.py`가 색상·명암·크랙·모서리·발판을 직접 픽셀 단위로 그리는 편집 기준본이다. `terrain/runtime/tiles/`에는 47종 이웃 연결 형태 × 8변주 = 376 PNG가 있다. `runtime/full/`에는 독립형·중앙 반복·양끝·벽 접속 통과 발판 11종이 있다. `runtime/tileset.json`에 256 이웃 조합의 canonicalLookup과 아틀라스 좌표가 있다. 아틀라스 패딩은 2px extrusion이다.

PNG를 직접 편집해도 되지만 재생성 시 덮어쓰므로 의도한 변경은 제작 코드에도 반영한다. `source/`의 맵 JSON은 검토용 스냅샷이며 게임 기획 원본이 아니다. 캐릭터 PNG는 기존 승인 리소스를 바이트 그대로 복사한 스케일 참고 자료다.

Python 3 + Pillow + NumPy가 필요하다. 한글 보드 렌더링은 Windows 맑은 고딕을 사용한다.

```sh
python environments/stage2-sewer-modular-20261111/source/rebuild.py
python environments/stage2-sewer-modular-20261111/source/verify.py
```

기본 검증은 저장한 PNG 바이트·격자·발판 높이·HTML 상대 링크·전경 보존을 검사한다. 의도적으로 디자인을 바꾼 후에는 새 미술 승인을 받고 pixel-baseline.json을 갱신한다.

## 다음 작업

v25는 `chapter2-layouts.json` 스냅샷 위에서 그렸다. Unity는 시드로 블록 맵을 만들기 때문에, Unity에서 시험하려면 실제 시드 맵에 맞춰 다시 그려야 한다.


공통 벽을 유지하되 구간별 기둥·배관·점검구 조합을 다양하게 하는 방향이다. 벽 재질 변경은 아직 선택되지 않았다. 채택된 플레이 타일과 캐릭터를 바꾸지 않고 배경만 별도로 검토한다. 다른 스테이지도 위 픽셀 규격을 사용한다. 1스테이지의 별도 작업 원본은 이 세트의 수정 대상이 아니다.
