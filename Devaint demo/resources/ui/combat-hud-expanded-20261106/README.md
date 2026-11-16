# 확장 전투 HUD · 출발 팔레트

현행 데이터에 맞춘 재사용 부품 세트입니다. 채택 시안은 화풍 참고에만 사용했고, 화면에서 잘라낸 픽셀은 에셋으로 쓰지 않았습니다. 기본 image_gen 도구로 새 원화를 만들고 원본을 source/generated에 보존했습니다. runtime/sprites는 투명 여백만 정리한 출력 또는 공개된 조합 레시피의 결과입니다.

- [전체 목록](ASSET-LIST.md): 제작 구분, 10무기·13외형, 발사기 포함 40특수공격, 54잉크, 전체 PNG.
- [배치 편집기](preview/index.html): 에셋·묶음 이동과 크기 조절, 텍스트·표시 순서, 되돌리기, 자동 저장, JSON 불러오기·저장, PNG 출력. [사용 안내](EDITOR.md).
- [사용자 배치 열기](preview/index.html?layout=matched-weapons): 사용자 수정값과 무기 2 맞춤을 적용합니다. [배치 JSON](runtime/layout-edited.json), [화면](preview/layout-edited.png), [발사기 스킬 예시](preview/layout-launcher-skills.png).
- `preview/편집기_열기.cmd`로 열면 로컬 브라우저에서 PNG 저장도 사용할 수 있습니다. 무기·스킬·잉크, HP, 쿨타임, 차징, 횟수, 3~5칸, 보스 상태도 계속 바꿀 수 있습니다.
- [수정 부품](preview/corrected-parts.png), [상태 비교](preview/state-matrix.png).
- [총 4종 축소 비교](preview/guns-small-scale.png): 내부 장식을 줄인 아이콘의 전후와 48px·HUD 표시 크기.
- [연결 데이터](runtime/catalog.json), [배치·Unity 설정](runtime/layout.json), [조합 레시피](runtime/composition-recipes.json).
- source/prompts-*.json: 기본 이미지 생성 도구에 전달한 요청문.
- source/reference-data: 게임 설계 JSON의 바이트를 보존한 참조 사본. 게임 규칙의 소유권은 메인에 있습니다.

## 변경한 내용

미니맵은 출발 카드와 같은 313:201 비율이며 재화·버튼 아래에 배치합니다. health-fill-red.png 자체가 빨간색이므로 흰색 tint로 사용합니다. 기존 32px 안료는 사용하지 않고 5색 팔레트 형태를 같은 화풍으로 새로 제작했습니다.

총 아이콘은 잉크통·긴 총열·쌍총열·큰 원통으로 구분하고, 홈·나사·세부 방아쇠 같은 내부 장식은 줄였습니다. 작은 HUD에서도 구분되도록 만든 새 그림은 source/generated/*-simple.png에 보존하며, 기존 그림도 같은 폴더에 유지합니다. runtime의 기존 무기 ID를 통해 새 그림을 사용합니다. 실제 요청문은 source/prompts-guns-simplified.json에 있습니다.

기본검·채찍·레이피어·한손 단검·기본망치·방패·대검·발사기·수리검·폭탄이 선택 가능한 무기 10종입니다. 발사기의 기본 스킬은 차징 후 발사이며, 레이저·샷건·유탄포는 발사기에 장착하는 스킬입니다. 장착한 스킬에 따라 큰 무기 아이콘과 출발 카드의 외형·이름도 바뀌고, 기본 공격 칸은 발사기 기본 공격을 유지합니다. 발사기 스킬 ID는 UI 별칭이며 별도 세션의 게임 구현과 연결해야 합니다. 기존 launcher_laser/shotgun/grenade 무기 선택 JSON은 발사기 + 해당 스킬로 호환 변환합니다. 스킬 준비 상태, 남은 쿨타임, 현재/최대 사용 횟수, 차징 진행률은 서로 다른 입력입니다. 선택하지 않은 무기도 독립적으로 회복 상태를 표시합니다.

팔레트는 3/4/5칸의 정확한 PNG와 9-slice 경계를 함께 제공합니다. 색상·잉크 ID·빈칸·잠금·무색 상태는 슬롯 데이터로 받습니다. 무색은 여섯 번째 잉크가 아닙니다. 출발 카드에는 색상 구성만 표시하며 상세 잉크 아이콘은 필요할 때 따로 씁니다. 기본 제공 출발 연출은 3.5초입니다. 조작 설명 줄은 만들지 않았습니다.

## 연결 방법

1. runtime/sprites를 Sprite/FullRect/알파/비압축/Bilinear로 가져옵니다. 원본 source는 편집 보존용입니다.
2. 공용 슬롯에 catalog의 weaponId/skillId/partId로 아이콘을 교체합니다. 원거리 스킬 ID는 원본 설계 필드에 연결하는 UI 별칭임을 확인합니다.
3. HP는 빨간 PNG의 Horizontal fill, 쿨타임/차징은 별도 링의 Radial360 fill을 갱신합니다. 숫자와 키캡 문자는 TMP 등 런타임 텍스트로 렌더링합니다.
4. layout.json의 anchor와 1670x942 기준에 layout-edited.json의 사용자 변경값을 적용합니다. 편집기의 editor-core.js가 변경값을 해석하는 참조입니다. 지도 내부는 실제 맵 데이터로 그리며 프레임 안쪽 여백으로 마스킹합니다.
5. 새 무기·잉크 추가 시 catalog와 그림/조합만 추가합니다. 첫 슬롯 무색 해금 등 게임 규칙은 게임에서 계산해 넘깁니다. UI가 임의 추첨하거나 게임 수치를 변경하지 않습니다.

프리뷰의 기본 숫자와 지도 경로는 검토용 예시입니다. 사용자 편집 배치는 메인의 TemporaryGameGui 전투 HUD·출발 팔레트에 로컬 적용되어 실제 상태를 표시합니다. 메인 문서 docs/development/combat-hud-approved.md에서 연결·실행 방법을 확인합니다. 로컬 게임 검토와 팀 배포는 별개이며, 발사기의 실제 공격 메커니즘 변경은 메인 게임 코드가 소유합니다.

## 다시 내보내기

리소스 저장소에서 `python -X utf8 tools/build_expanded_combat_ui.py`를 실행합니다. Pillow, NumPy, Node와 Windows 맑은 고딕이 필요합니다. 새로운 생성 API를 호출하지 않고 보존된 원본으로 출력합니다. 프롬프트와 미채택 beam 원본도 source에 남습니다. 검증은 `python -X utf8 tools/verify_expanded_combat_ui.py`입니다.
