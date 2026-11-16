> 이 시안 잘라내기 방식은 사용자가 거절했습니다. 게임용 에셋으로 사용하지 않습니다. 새 독립 에셋은 ../combat-hud-rebuilt-20261106/에서 제작합니다.

# 채택 전투 HUD + 출발 팔레트

첫 번째 채택 이미지의 전투 HUD와 두 번째 채택 이미지의 출발 팔레트를 원본 픽셀로 분리한 에셋입니다. 새로운 그림 생성이나 원본 리사이즈 없이 추출했습니다. 디자인 선택은 완료되었으며, 이 분리본의 게임 통합과 새 상태의 미술 검수는 별도입니다.

- `preview/index.html`: 재조립 화면, 투명·밝은·어두운 배경, 개별 PNG 링크.
- `preview/reassembled-approved-screen.png`: 두 채택 디자인을 합친 참조 화면. 실제 Unity 실행 화면은 아닙니다.
- `runtime/layers/`: 조립에 사용하는 투명 PNG. `runtime/layout.json`의 배열 순서로 배치합니다.
- `runtime/composites/`: 시안의 완성 상태를 그대로 담은 대체 묶음. layers와 동시에 겹쳐 쓰지 않습니다.
- `runtime/battle-ui-transparent.png`, `runtime/palette-ui-transparent.png`: 배경 없는 전체 위치 확인본.
- `source/approved-ui-layers.ora`: Krita/GIMP에서 열 수 있는 위치 보존 레이어 문서. 참조 배경은 기본 숨김입니다.
- `source/approved-battle.png`, `source/approved-palette.png`: 원본 바이트 보존본.
- `source/pigment-existing-game.png`: 기존 게임 안료 아이콘 원본. 정확한 시안 재조립에는 시안에서 추출한 `pigment_icon.png`를 사용합니다.

## 조립 규격

기준 캔버스는 1670 × 942입니다. 두 번째 원본은 1671 × 941이므로 팔레트 부분만 원래 좌표·크기로 옮깁니다. 늘이거나 정규화해 재샘플링하지 않습니다. 각 PNG의 `rect`는 왼쪽 위 기준 `[x,y,width,height]`입니다. UI를 화면 크기에 맞춰 통째로 비례 확대할 수 있지만, 원본 해상도보다 크게 표시하면 새 디테일이 생기는 것은 아닙니다.

Unity에서는 Sprite (2D and UI), Single, Full Rect, Alpha Is Transparency, 압축 없음, Mipmap 끔, Bilinear를 사용합니다. Canvas Scaler는 Scale With Screen Size, reference 1670 × 942입니다. 각 RectTransform은 pivot (0,1), `anchor`와 `unityAnchoredPosition`을 사용합니다. 선택 시안처럼 체력은 왼쪽 위, 무기·스킬은 왼쪽 아래, 재화·가방·일시정지는 오른쪽 위, 지도는 그 아래입니다. 출발 팔레트는 중앙입니다. 비율이 다른 해상도에서는 게임 카메라의 실제 safe area와 함께 별도로 확인해야 합니다.

## 정확히 보존된 범위와 보완할 범위

모든 원본 노출 픽셀과 현재 샘플 상태는 보존했습니다. 각 부품의 재조립과 선택 조합 화면을 비교하면 픽셀 차이가 0입니다. 외곽 1px에는 원래 배경과 섞인 안티앨리어싱 픽셀이 있어 다른 배경에서 약한 색 테두리가 나타날 수 있습니다. `source/extraction-contract.json`의 수치는 이 원본 크기에서의 픽셀 비교입니다.

바탕에서 숫자·아이콘·물방울을 분리한 뒤 그 아래 숨은 영역만 인접 색으로 보간했습니다. 이 부분은 원본 복원이 아니라 조립용 바탕 복원이며 `fidelity: source-visible+restored-underlays`로 표시합니다. 바뀐 내용 뒤가 드러나는 동적 상태는 미술 검수가 필요합니다. 원본에 가려진 무기 원형의 아래쪽과 무기 초상 뒤 판은 완전한 독립 도형으로 복구하지 않았습니다. 시안의 겹침 순서를 유지하세요.

82/100, 240, 18, 3.2는 시안 그대로인 래스터 표본이며 가변 숫자 전체 폰트가 아닙니다. 82% HP 채움과 쿨타임 원도 시안에 보이는 부분만 존재합니다. 100% 채움 텍스처·완전한 radial fill·다른 숫자·hover/pressed/disabled 상태는 추가 제작 또는 게임 드로잉으로 연결해야 합니다. 지도 경로도 표본이며 실제 맵 데이터에서 그려야 합니다. 이 패키지에 없는 상태를 완료된 것으로 보지 않습니다.

## 재생성

리소스 저장소의 현재 작업 브랜치와 미커밋 변경을 확인한 뒤 실행합니다. 지정 출력 그룹만 갱신합니다.

```powershell
python tools/extract_approved_combat_ui.py --battle ui/combat-hud-approved-20261106/source/approved-battle.png --palette ui/combat-hud-approved-20261106/source/approved-palette.png
```

원본을 바꾸지 않으며 catalog.json과 generated-assets.json에 이 그룹의 파일·해시를 갱신합니다. Unity 프로젝트 코드·메타데이터·배포 계약에는 아직 연결하지 않았습니다.
