# 32px 모듈형 석재 타일

**미술 채택본 / 실제 재사용 PNG / Unity 미적용.**

다음 버전 제작은 [채택 제작 기준](STYLE-GUIDE.md)과 [수치 계약](style-contract.json)을 먼저 확인한다. 32×32px 타일을 내보낸 뒤 같은 PNG를 다시 읽어 조립했다. 조립 결과에 별도의 그림이나 모서리 리터치를 덧붙이지 않았다.

## 확인할 이미지

- [요청 구조 6종, 정수 2배](preview/assembled-2x.png): ㄴ자, ㄷ자, 1칸 기둥, 벽에 붙은 1/2칸 두께 선반, 막힌 독립 석재, 통과형 목재.
- [같은 조립의 격자 표시](preview/assembled-grid-2x.png).
- [접합부 픽셀, 정수 4배](preview/junctions-4x.png).
- [추가 24개 구조, 원본 1배](preview/all-cases-1x.png): 계단, T/십자, 천장, 좁은 통로, 구멍, 양벽 연결, 대각선 접촉, 문턱 포함.
- [연결형/끝처리 목록](preview/tile-sheet-2x.png).
- `preview/native/`: 투명 배경의 실제 조립 PNG. 한 칸은 32px다.
- [원본 아틀라스](runtime/atlas-32.png).

## 에셋 규격

| 항목 | 실제 출력 |
|---|---|
| 개별 타일 | 모든 PNG 32×32px |
| 월드 배치 | 1u=32px, PPU 32, scale 1 |
| 석재 | 47개 이웃 연결형 × 질감 4종 = 188 PNG |
| 통과형 목재 | single/양끝/중간/벽 연결 11 PNG |
| 기본 돌 / 반장석 | 32×16px / 16×16px |
| 한 칸 기둥 / solid 선반 | 두 단 × 16px = 32px |
| 목재 | 판 8px + 받침/끝캡 최대 4px. y≥12 완전 투명 |
| 목재 물리 | 기존 상단 접촉선 유지, 충돌 두께 4px는 메인이 소유 |
| 줄눈 | 시작 경계 1px 소유. 페인팅의 어두운 경사면은 별도 |
| 아틀라스 | 2px 바깥 압출, 셀 간 stride 36px. Sprite rect는 32×32만 포함 |
| 필터 | Point / mipmap 없음 / 압축 없음 권장. 기존 프로젝트 기준과 동일 |

PNG 한 장의 범위를 넘어간 픽셀은 없다. solid 타일은 전 픽셀이 불투명하므로 모서리 내부에 배경이 새지 않는다. 한 칸 선반과 기둥의 양쪽 띠는 겹쳐 덧그리지 않고 같은 행의 돌로 합친다. 돌을 세로로 회전하지 않는다. 아래면은 어두운 전용 재질이다.

원본 석재의 페인팅을 한 번 32×16 / 16×16로 정돈했다. 모든 미리보기는 그 이후의 **32px 타일 그대로**다. 2배/4배 이미지는 nearest-neighbor 정수 확대이므로 원본보다 디테일을 추가하지 않는다. 고해상도 시안의 모든 미세 묘사가 32px에 보존된다고 주장하지 않는다. 실제 게임에서 보일 품질은 1배 자료와 함께 평가한다.

## 재사용

- `runtime/tiles/stone-{mask}-v{variant}.png`: 지형 타일.
- `runtime/platforms/`: 통과형 목재.
- [tileset.json](runtime/tileset.json): 규격, 256→47 연결표, 아틀라스 rect.
- [lookup.cjs](runtime/lookup.cjs), [Stone32Selection.cs](runtime/Stone32Selection.cs): 동일한 선택 함수. C# 파일은 Unity 타입에 의존하지 않는 선택 함수이며 씬에 설치하지 않았다.
- [assembly-selection.json](preview/assembly-selection.json): 각 조립에서 사용한 PNG ID와 칸 좌표. 재현과 비교에 사용한다.

지형은 각 사각형마다 테두리를 붙이지 않고, 통로/문/미러링까지 합쳐진 최종 solid 점유를 선택 함수에 전달한다. 8방향 이웃 순서는 N, NE, E, SE, S, SW, W, NW이다. 대각선은 해당 두 직교 이웃이 차 있을 때만 유효하다. 좌표는 X 오른쪽/Y 아래이며, 질감 위상은 방마다 초기화하지 않고 최종 월드 격자로 결정한다.

폭 N칸은 N장의 32px 그림으로 구성한다. N=1 전용 single이 있어 양끝이 보존된다. 끝캡은 각 끝 셀 내부에 들어가므로 최종 폭은 N×32px이다. 목재의 벽 연결은 실제 벽 접촉 여부를 넘겨 선택한다.

Unity atlas Rect는 JSON의 위쪽 기준 y를 `atlasHeight-y-height`로 변환한다. Sprite pivot (0.5,0.5), tileAnchor (0.5,0.5), PPU 32를 쓴다. 지형 데이터 셀 (x,y-down)은 (x,-y-1)에 놓는다. 발판의 원래 Y 오프셋은 그대로 유지한다. 구형 landingRow 9/11 보정을 새 그림에 다시 적용하지 않는다.

기존 15장/5장 팀 팔레트의 인덱스와 다른 연결표이므로, 본 게임 적용 시 이 선택 함수를 연결하는 별도 어댑터가 필요하다. 이 산출물은 그림과 선택 규칙이며 Collider·점프·맵 배치는 바꾸지 않는다.

## 재생성 원본

내장 image_gen으로 제작한 [material-master.png](source/material-master.png)는 바이트를 보존했다. [생성 프롬프트](source/prompts.json), [재질 추출 좌표](source/material-crops.json), 승인된 장면 참조와 [설계 계약](source/design-contract.json)을 포함한다.

```powershell
pwsh -NoProfile -File source/build.ps1
node source/package.cjs
```

빌더는 원본 재질 정돈 → 개별 PNG 출력 → 출력 PNG 다시 읽기 → 조립/아틀라스 제작 순서로 실행한다. 원본은 덮어쓰지 않는다.

검사 범위는 32px 크기, solid 알파, 목재 12px 외부 투명, 1~64칸 × 양끝 4상태의 폭, 선택표/실제 조립 픽셀 일치, 아틀라스 rect다. 줄눈·모서리의 미술 자연스러움은 제공한 접합 확대본으로 검토한다. Unity 실행·카메라 이동 검증과 게임 배치는 수행하지 않았다.
