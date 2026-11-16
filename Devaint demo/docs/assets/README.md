# 리소스 받기·Unity 배치

[문서 입구](../README.md)

## 분야별 규격

- [환경](world/README.md)
- [표현](presentation/README.md)
- [제작·적용 범위](production/README.md)
- [리소스 저장소](resource-repository.md)


게임 이미지·음원·폰트·VFX 원본·미디어 포함 HTML 검토 자료·미리보기·보존 패키지와 리소스 도구는 `resources/` 서브모듈에서 관리한다. 공유 폴더 동기화나 수동 ZIP 복원은 실행 준비 절차가 아니다. 게임은 메인이 고정한 외부 커밋과 [배치 계약](manifests/unity-deployment.json)으로 선택한 미디어 바이트만 사용한다.

## 처음 받거나 메인을 갱신한 뒤

두 저장소의 미커밋 작업을 먼저 보존한다. 서브모듈을 받는 과정에서 작업 브랜치·파일을 강제로 바꾸지 않는다.

```sh
git status --short --branch
GIT_LFS_SKIP_SMUDGE=1 git submodule update --init --recursive --depth 1 resources
git -C resources lfs install --local
git -C resources -c lfs.fetchrecentalways=false lfs pull
python3 resources/tools/assets.py verify
python3 tools/assets/update_assets.py --check
python3 tools/assets/update_assets.py
```

Git 이력은 얕게 받고 LFS는 현재 고정 SHA의 체크아웃에 필요한 파일만 받는다. 과거 전체 Git/LFS 이력을 미리 받거나 기존 캐시를 자동 삭제하지 않는다. 예전 세트·보존 ZIP도 현재 트리에 있으면 대상이다. 추가 이력이 필요한 예외는 목적·범위를 확인받으며 얕은 받기 실패를 전체 이력 받기로 우회하지 않는다. 상세 기준은 [환경 설정](../getting-started/setup.md#외부-리소스-서브모듈)을 따른다.

Windows PowerShell에서는 위 환경 변수 설정을 [환경 설정의 PowerShell 절차](../getting-started/setup.md#외부-리소스-서브모듈)로 대체한다. Python 3.9 이상·Git LFS를 준비한 뒤 마지막 두 명령 대신 저장소 루트의 `에셋 업데이트.bat --check`, `에셋 업데이트.bat`을 실행해도 된다. 폴더 선택이나 저장된 외부 경로는 사용하지 않는다. 비공개 리소스 저장소를 읽을 수 있는 GitLab 권한이 필요하다.

Unity를 닫은 상태에서 적용하고 **Unity 6000.6.5f1**로 `unity/`를 연다. 타이틀 `Assets/ForestCombatV2/Scenes/Chapter1Title.unity`에서 시작 마을 `Assets/Deviant/Hub/Scenes/StartingVillage.unity`을 거쳐 `Assets/MeadowDuskV3/MapVer2/Scenes/Chapter1Procedural.unity`로 연결된다. 시작 마을의 55개 이미지도 같은 배치 계약에 포함한다.

## 배치 계약과 파일 보호

- 메인은 사용할 리소스 에셋 ID·파일/내장 이미지 참조·해시와 Unity 목적지를 명시한다. 원본 목록과 바이트는 외부 색인·이관 매핑이 소유한다. 같은 파일명의 다른 세트나 날짜가 큰 세트를 자동 선택하지 않는다.
- 현재 계약은 일반 게임 이미지, 벽 이동·점프·포탈, 마을 139장·타일 47장, 몬스터·보스 미디어와 GUI를 포함한다. 현재 게임 이미지에는 명시적으로 채택한 WebP 65 파생본의 디코딩 PNG를 사용한다. 캔버스·GUID는 유지하며 원본은 resources에 보존한다. 배치 실행기는 선택한 바이트를 운반하고 재인코딩하지 않는다.
- 원본 음원 WAV·MP3와 게임용 Opus payload·폰트 TTF 8개·보유 Wallcoeur VFX 텍스처도 같은 계약으로 원래 Unity 경로에 배치한다. 오디오 생성기 입력 WAV도 먼저 이 절차로 준비한다. 음원·폰트 바이트와 기존 importer·GUID를 보존하고 재인코딩하지 않는다.
- 시계탑 집행자 `ClockExecutioner/StationaryBytes.bytes`와 2챕터 `RoseBlue/rose.bytes`·`RoseBlue/blue.bytes`의 PNG 바이트, `tools/chapter1-map-editor/`의 현재·이전 지형용 단일 HTML 두 개도 같은 계약에 포함한다. 보스 TextAsset은 이 명시적 세 경로만 허용하고 경로·`.meta`·GUID를 유지한다. HTML은 이미지 9장씩을 내장한 원본을 그대로 로컬 도구 경로에 복원한다. 허용한 두 HTML 외의 코드·문서는 배치 출력으로 쓰지 않는다.
- 실행기는 메인 index의 gitlink와 외부 HEAD가 같은지, 외부 작업 트리가 깨끗한지 먼저 검사한다. 자동 pull·브랜치 전환·원격 최신 채택·별도 입력 경로 대체는 하지 않는다.
- 전체 입력·목적지·해시·로컬 충돌을 검사한 다음 적용한다. 같은 이미지는 건너뛰고 알 수 없는 로컬 수정은 덮어쓰지 않는다. 이전에 배치한 바이트 또는 계약이 명시한 교체 가능 해시만 갱신한다.
- 변경 전 바이트와 배치 상태는 Git 제외 `outputs/assets/backups/`, `outputs/assets/state.json`에 둔다. 이전 개인 백업·외부 경로 캐시·목록에서 제외된 파일은 자동 삭제하지 않는다.
- Unity `.meta`·GUID·가져오기 설정, 씬·프리팹과 게임 수치·행동·모션 시간표는 이미지 배치의 출력이 아니다. `.meta`가 없는 Unity 목적지는 거부한다.

## 채택 스케치 타이틀

`ui.title-sketch-20261115`는 승인된 단일 HTML 시안, 로고·붓펜 원본, 새 캐릭터 GIF와 human/ink 48장씩을 보존한다. 캐릭터 PNG는 원본 128×128 바이트 그대로이며 발 기준은 y121, GIF의 80/80/80/60ms 간격을 12번 반복해 3.6초로 재생한다. 런타임 로고는 원본의 종이색만 투명하게 처리한 크롭이며, 노란 붓펜은 승인된 아래쪽 획의 종이 제거와 세로 18px 보강을 적용한 파생본이다. 원본을 덮어쓰지 않는다.

메인 `tools/assets/register_title_sketch.py`는 PNG 99개의 `TitleSketch/` 배치 계약과 Unity `.meta`를 등록한다. 실제 복사는 공통 실행기가 담당한다. 하늘·산·타일·꽃·구름은 기존 `Hub/pixel-village/` 원본을 재사용하며, 공용 `ApprovedTheme/Fonts/MemomentKkukkukk` 폰트는 새 사본을 만들지 않는다. 타이틀 전환 규칙은 [화면별 동작](../design/ui/screens.md)을 따른다.

## 채택 잉크 충전소 미술

1챕터 잉크 충전소는 `resources/environments/stage1-pixel-structures-20261112/runtime/recovery-station.png`(2×2 도트 주유기형, 96×112px)를 `StageRecoveryArt/stage1.png`에 배치하고, 2챕터의 대형 배관 탱크는 `resources/environments/shared/ink-recovery-stations/runtime/stage2.png`를 그대로 쓴다. 1챕터는 PPU32·점 필터·무압축·밉맵 없음, 바닥 피벗으로 높이 3.5유닛이고, 2챕터는 원본 바이트·투명 여백·Unity GUID를 유지하며 PPU로 3.3유닛을 맞추고 무압축·Trilinear·밉맵으로 축소한다. 기존 초록 회복 효과를 재사용한다. 상호작용·가격·배치는 [런 구조](../design/run/run-structure.md)의 잉크 충전소 항목을 따른다.

1스테이지 전투 구조물(정비소 뒷벽·책상·1챕터 등불·간판, 출구 닫힘·열림과 열림 8장, 잉크 제단과 등장·사용·획득·폐기, 잠긴/보상 상자, 봉인 석상, 철창)은 `resources/environments/stage1-pixel-structures-20261112`의 2×2 도트 픽셀 세트를 `tools/assets/register_stage1_pixel_structures.py`로 기존 Unity 경로에 배치한다. 크기·기준점은 세트의 `runtime/structures.json`을 따른다. 1챕터 정비소 등불·간판은 2챕터와 나누어 `lantern-stage1`·`sign-stage1`을 쓴다.

1스테이지 전투 지형·나무 발판·분리 배경·하늘·보스 길목과 보스방 뒷그림은 `environments/stage1-pixel-lawn-wood-32`·`stage1-pixel-background-native-20261111`·`stage1-atmosphere-sky-ash-20261111`·`stage1-boss-approach-pixel-20261112`의 runtime PNG를(산·먼 마을·가까운 마을은 `stage1-background-color-20261112/runtime/sketch`의 색 빠진 판을, 구름·새·둥근 창틀은 `stage1-background-cream-20261112`의 판을) `tools/assets/register_stage1_combat_terrain.py`로 `Stage1VillageExperiment/Resources/Stage1Combat/`에 배치한다. 연결 방식은 [1Stage 마을 제작 기반](../development/gameplay/village/stage1-village-experiment.md)의 전투 채택 미술 항목을 따른다.

출발 마을과 튜토리얼 길의 바닥·나무 발판·장식·배경·하늘·구름·새는 색 빠진 판(잔디 타일 `stage1-pixel-lawn-wood-32`, 배경 `stage1-background-color-20261112/runtime/sketch`)과 색 있는 판(`stage1-background-color-20261112/runtime`·`runtime/village`)을 짝으로 `tools/assets/register_stage1_village_colour.py`가 `Deviant/Hub/Resources/Hub/pixel-village/`에 배치한다. 잔디 타일 묶음은 벽 그림을 만들려고 읽기 가능(isReadable)으로 가져온다.

## 채택 정비소 미술

[1·2챕터 정비소 에셋과 HTML 검토](world/maintenance-shops.md)는 사용자 채택 디자인이며 기존 Unity 정비소 배치·대화·장식 불빛에 연결한다. 이 연결을 신규 노말맵·동적 조명·반응형 랜턴의 구현 완료로 해석하지 않는다.

## 조명 소품 제작 규격

[랜턴·창문·횃불 에셋 계약](presentation/lighting-props.md)은 구조물·발광부·노말·개구부·그림자·접촉 영역과 기존 리소스 재사용 경계를 정의한다. `environments.waterway-lighting`의 7개 분리 부품·몸 접촉 섬광은 배치 계약이 선택한 2챕터 S/F01 시험 세트다. 전체 노말·그림자와 최종 미술 승인은 별도이며 다른 파생 세트를 자동 채택하지 않는다. [조명 기술 설계](../development/presentation/visuals/lighting-and-reactive-props.md)와 함께 확인한다.

## 리소스를 수정할 때

`resources/AGENTS.md`와 [리소스 구조](resource-repository.md)를 따른다. 해당 저장소의 합의한 작업 브랜치에서 원본·에셋 목록·해시·LFS를 함께 갱신한다. 필요한 제작 내보내기는 `resources/tools/`에서 새 검토 디렉터리로 실행하며 메인의 게임 데이터를 직접 다시 쓰지 않는다.

리소스를 먼저 커밋·push하고 LFS를 원격에서 복원할 수 있는지 확인한 뒤, 메인의 배치 계약·필요한 Unity 연결과 `resources` gitlink를 함께 갱신한다. 원격에 없는 로컬 SHA를 메인에 배포하지 않는다. 두 저장소의 커밋을 별도로 검토하고 squash 없이 이력을 보존한다.

## 기존 추적 파일과 라이선스

WAV·폰트와 `unity/Assets/ThirdParty/Wallcoeur/`의 보유 VFX 텍스처 원본은 resources가 소유하며 메인의 Unity 파일은 로컬 배치 사본이다. Unity `.meta`·GUID·씬·프리팹·재질·셰이더·카탈로그는 계속 메인이 추적한다. 외부 커밋·push·독립 복원·배치 검증을 마치기 전에는 기존 메인 추적을 해제하지 않으며 로컬 파일을 지우지 않는다. 보유 패키지 자체의 개별 복원 검사는 `tools/combat/import_hit_feedback.py`로 구분하고, 일반 실행 준비는 위 고정 리소스 배치 절차를 따른다. 비공개 저장소라는 사실만으로 라이선스 확인을 완료 처리하지 않는다. 외부 공개·제3자 전달은 해당 라이선스와 수신자의 권한을 별도로 확인한다.

이미지·음원·폰트·VFX를 포함한 HTML 보고서·비교 화면도 resources의 에셋 `preview/` 또는 공통 `authoring/`에 저장한다. 미디어 내장 HTML/JS/JSON은 LFS payload 목록에, 상대 참조 미디어는 HTML과 함께 에셋 색인에 등록한다. 본문·링크를 유지하며 일회성 검증 로그·캡처는 Git 제외 `outputs/`와 구분한다.

## 검증 범위

원본 바이트·색인·배치 성공은 미술 재승인이나 Unity 실행 통과를 뜻하지 않는다. [검증 안내](../development/testing/testing.md)에 따라 컴파일·현재 씬·모션·GUI·빌드를 별도로 확인한다. 원본 편집 자료·미리보기와 원본 패키지는 외부 저장소에 보존하며 실행 시 전체를 Unity에 풀지 않는다.

지하수로 채택 에셋과 적용 규격은 [2챕터 지하수로](world/stage2-waterway-registration.md)를 따른다.

가방 수첩은 리소스 그룹 `ui.inventory-journal-kit`에서 선택한 원형·탭·슬롯·닫기 PNG를 `TemporaryGUI/Resources/InventoryJournalUI/`로 배치한다. 원본 바이트와 좌표 자료는 리소스 저장소가, Unity 가져오기 설정·GUI 바인딩·배치 계약은 메인이 소유한다.

현재 원본 PNG·Opus 음원과 DXT·BC7 가져오기·게임 로딩 계약은 [미디어 압축](../development/delivery/media-compression.md)을 따른다.
