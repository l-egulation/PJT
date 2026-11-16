# 임시 타이틀: 평온한 광장과 우물의 외눈

사용자가 선택한 위에서 내려다본 마을 구도와 B형 둥근 외눈을 기준으로 한 게임용 에셋이다. Unity의 `WellTitlePresentation`에서 분리 배경·메뉴와 시작 동작을 연결한다. 현재 작업 폴더에 적용한 임시 구성으로, 원격 공유용 리소스 커밋과 메인 gitlink 갱신은 별도다.

## 게임에 적용하는 구성

선택 표시가 설정과 종료로도 이동해야 하므로 **글자 없는 배경 + 로고 + 글자 없는 선택 잉크 + 메뉴 글자 3장**을 합성한다. 로고·글자·선택 표시를 하나의 이미지로 합치지 않는다.

- `runtime/backgrounds/title_calm_no_ui.png`: 평온한 마을과 맑은 우물, 글자 없음.
- `runtime/backgrounds/title_erased_eye_no_ui.png`: 스케치화된 마을과 큰 외눈, 글자 없음.
- `runtime/ui/logo.png`: 투명 로고.
- `runtime/ui/menu/selection_ink.png`: **글자가 전혀 없는** 재사용 선택 잉크, 322×78.
- `runtime/ui/menu/label_start.png`, `label_settings.png`, `label_quit.png`: 배경이 없는 흰색 글자. 같은 알파로 일반/선택 색상을 틴트한다.
- `runtime/audio/title_glitch.wav`: 클릭 뒤 한 번 재생할 치직 효과음. 44,100 Hz, mono, PCM 16-bit, 0.68초. 루프 끔.
- `runtime/layout.json`: 클릭 영역, 분리 스프라이트 위치, 연출 시간표, 임포트 설정.

모든 화면의 기준 크기는 1672×941이다. 시안 원본 크기를 보존했으며 인위적으로 확대하지 않았다. Unity에서는 배경만 종횡비를 유지해 창 전체를 덮는다. 로고·잉크·글자와 투명 클릭 영역은 같은 RectTransform 아래에 두고 화면 안에 모두 들어오도록 함께 맞춘다.

클릭 영역 좌표는 **이미지 왼쪽 위를 원점으로 한 정규화 x, y, width, height**다. Unity 아래쪽 원점의 앵커로 바꾸려면 yMin = 1 - y - height, yMax = 1 - y를 사용한다.

| 동작 | x | y | width | height |
|---|---:|---:|---:|---:|
| 시작하기 | 0.062 | 0.508 | 0.190 | 0.081 |
| 설정 | 0.062 | 0.595 | 0.190 | 0.081 |
| 종료 | 0.062 | 0.675 | 0.190 | 0.081 |

버튼의 클릭 영역을 글자 없는 배경 위에 두고 기존 클릭 처리에 연결한다. 그리기 순서는 Background → Logo → SelectionInk → Labels → ButtonHitTargets다. 잉크와 글자의 Raycast Target은 끄고 버튼이 입력을 받는다. 설정은 타이틀 효과음 켜기·끄기와 돌아가기를 제공한다.

선택 잉크는 1개 Image를 옮겨 재사용하거나 각 버튼 밑에 같은 스프라이트를 배치해 선택된 행만 켠다. **PointerEnter, 키보드/패드 Select, 클릭** 모두 같은 선택 상태를 사용한다. 선택 위치는 왼쪽 위 픽셀 기준 시작하기 (97,481), 설정 (97,557), 종료 (97,631)이며 크기는 모두 322×78이다.

흰색 글자 PNG의 일반 색은 RGBA (0.13,0.11,0.09,1), 선택 색은 (0.969,0.933,0.843,1)을 기본으로 쓴다. 선택이 바뀌면 이전 행의 잉크를 끄고 글자를 일반 색으로 돌린 뒤 새 행 뒤에 잉크를 켜고 글자를 밝게 바꾼다. `preview/menu-selection-states.png`와 `preview/title-settings-selected.png`에서 합성을 확인한다.

## 시작 클릭 연출

시간은 클릭 시점부터이며 `Time.unscaledTime` 또는 동일한 비배율 시간을 사용한다. 클릭을 중복 수락하지 않고 효과음을 0ms에 한 번 시작한다.

| 시간(ms) | 화면 |
|---|---|
| 0–120 | 평온한 마을 |
| 120–249.2 | 재난/외눈 화면과 강한 가로 찢김·입자 잡음 |
| 249.2–304.2 | 평온한 화면이 짧게 돌아오며 12% 어두워짐 |
| 304.2–555 | 재난/외눈 화면과 불규칙한 가로 찢김·잡음 |
| 555–855 | 재난 화면에서 검정으로 암전, 찢김은 첫 110ms 동안 감소 |
| 855 이후 | 기존 시작하기 동작을 **한 번** 실행하여 게임으로 이동 |

미리보기에서 다시 타이틀로 돌아오던 동작은 반복 확인용이었다. 게임에서는 자동 복귀하지 않는다. 외눈 이미지에 화면 잡음은 구워 넣지 않았으므로 가로 슬라이스 변위와 입자 잡음은 화면 연출에서 처리한다. 효과음의 불규칙한 여섯 잡음은 WAV에 포함돼 있다.

## 보존한 기존 합성 자료

- `runtime/backgrounds/title_calm_no_ui.png`
- `runtime/backgrounds/title_erased_eye_no_ui.png`
- `runtime/ui/title_ui_overlay.png`: 배경과 같은 1672×941 투명 캔버스. 로고와 메뉴를 원래 위치에 합성한다.
- `runtime/ui/logo.png`, `start_selected.png`, `settings.png`, `quit.png`: 투명 여백을 줄인 개별 부품. 위치는 `layout.json`의 `sprites` 참고.

기존 `start_selected.png`는 시작하기 글자와 잉크가 붙어 있는 참고용 합성본이고 `title_ui_overlay.png`는 시작하기가 선택된 고정 합성본이다. **선택이 이동하는 게임 UI에는 두 합성본을 사용하지 않고 위의 `ui/menu/` 부품을 쓴다.** 글자는 자동 글꼴 파일이 아니라 래스터 아트다. 배경에서 주인공·우물·발자국은 별도 분리하지 않았다.

분리본은 기본 이미지 생성 도구로 UI를 제거/추출한 편집 결과이므로 원래 화면과 작은 픽셀 차이가 있을 수 있다. `flat/` 완성본은 승인 시안의 픽셀을 보존한 참고 자료이며, 선택 잉크를 다른 행으로 옮길 수 있는 배경은 아니다.

## Unity 임포트

- Texture Type: Sprite (2D and UI), Sprite Mode: Single, Mesh Type: Full Rect.
- Filter Mode: Point, Compression: None, Generate Mip Maps: Off, Wrap Mode: Clamp.
- Max Size: 2048. UI PNG는 Alpha Is Transparency: On.
- 기존 게임 Canvas Scaler를 유지하고 타이틀 배경은 cover, UI와 클릭 영역은 contain 비율로 별도 보정한다. 기준 크기는 1672×941이다.
- 효과음: Decompress On Load, PCM, 루프 끔. 내보낸 WAV의 레벨은 이미 낮게 조정돼 있으므로 우선 AudioSource 볼륨 1로 비교한다.

## 원본과 재생성

`source/approved-title-*.png`와 `source/approved-eye-detail.png`에 선택 원본을 바이트 그대로 보존했다. `source/generated/`는 분리 작업의 이미지 생성 원본, `source/prompts.json`은 실제 요청문이다. `preview/contact-sheet.png`는 분리본 합성 확인용이다.

제작 도구는 `../../tools/build_title_well_assets.cjs`다. 명시적인 입력 JSON과 **존재하지 않는 새 출력 폴더**를 받는다. 원본·출력을 덮어쓰지 않는다. PNG 절단·트림에는 sharp를 사용하며 `NODE_PATH`로 설치 위치를 전달한다. 효과음은 이 도구의 고정 시드 노이즈·필터·시간표로 재생성한다.

독립 메뉴의 후처리 도구는 `../../tools/split_title_menu.cjs`이며, 글자 없는 잉크 원본·흰색 글자 원본·대상 에셋 폴더를 받는다. 생성 원본은 `source/generated/selection_ink_only.png`, `menu_labels_white.png`에 보존하고 글자의 알파만 그대로 사용해 RGB를 흰색으로 정규화한다. 실제 분리 요청문은 `source/menu-separation-prompts.json`이다.

현재 main의 Unity 배치 매니페스트와 gitlink는 변경하지 않았다. 자동 배치에 연결할 때는 이 리소스 세트를 고정 커밋으로 보존하고 메인 계약에서 사용할 파일과 목적지를 선택한다.
