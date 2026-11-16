# Deviant Resources

게임 이미지·음원·폰트·VFX 원본·편집 원본·미디어 포함 HTML 검토 자료·미리보기·보존 패키지·리소스 도구를 관리하는 비공개 Git LFS 저장소다. 메인 저장소는 이 저장소의 특정 커밋을 고정하고 소비할 파일과 Unity 목적지를 선택한다. 별도의 공유 폴더나 다운로드 주소는 작업 진입점이 아니다.

## 받기와 검사

```sh
git lfs install --local
git -c lfs.fetchrecentalways=false lfs pull
python3 tools/assets.py verify
python3 tools/configure_lfs.py
python3 -m unittest discover -s tools -p 'test*.py'
```

메인에 연결해 사용할 때는 메인에서 `GIT_LFS_SKIP_SMUDGE=1 git submodule update --init --recursive --depth 1 resources`로 고정 커밋을 얕게 받은 뒤, 위 명령으로 그 체크아웃의 LFS 파일만 복원한다. PowerShell의 환경 변수 설정은 메인 `docs/getting-started/setup.md`를 따른다. 단독 clone도 `--depth 1`·LFS smudge 생략을 사용하며 합의한 리소스 커밋·브랜치를 명시적으로 선택한다. `master` 최신을 메인에 자동 적용하지 않는다.

일반 다운로드는 현재 트리의 파일만 대상으로 하며 과거 전체 Git/LFS 이력을 미리 받지 않는다. `git fetch --all`, `--unshallow`, `git lfs fetch --all`·`--recent`는 이력 조사·복구 등 범위를 확인받은 별도 작업에만 사용한다. 현재 트리에 포함된 편집 원본·미리보기·보존 ZIP은 다운로드 대상이다. 얕은 받기로 고정 SHA를 받지 못하면 중단하고 확인하며 전체 이력으로 자동 대체하지 않는다. 이미 받은 객체·LFS 캐시는 이 절차로 삭제되지 않는다.

## 파일 찾기

- **채택한 밝은 스테이지 기준**: [발판·벽·배경 제작 규격](environments/stage1-modular-tiles-20261111/STYLE-GUIDE.md), [수치 계약](environments/stage1-modular-tiles-20261111/style-contract.json), [재사용 타일](environments/stage1-modular-tiles-20261111/README.md), [실제 방의 분리 배경](environments/stage1-actual-layered-bright-20261111/README.md). 다음 배색·챕터 제작은 이 기준 PNG와 규격을 함께 사용한다. 미술 채택본이며 Unity에는 아직 적용하지 않았다.

- `catalog.json`과 각 대상의 `asset.json`: 에셋 ID·파일·세트·출처·승인/통합 상태.
- `characters/ink/`: 공통 몸체, 이동, 무기별 본체·전용 효과·편집 원본.
- `monsters/`: 스테이지·역할·종별 아트. 이전 들판 종은 `legacy/field/`에 보존한다.
- `environments/`, `effects/`, `sequences/`, `ui/`: 공간, 공용 효과, 연출, 화면 미술.
- [`ui/combat-hud-expanded-20261106/`](ui/combat-hud-expanded-20261106/README.md): 현재 채택한 전투 HUD·출발 팔레트의 독립 PNG 169개, 10종 무기·13종 외형·40종 스킬·54종 잉크와 3~5칸 팔레트. [전체 목록](ui/combat-hud-expanded-20261106/ASSET-LIST.md), [배치 편집기 사용법](ui/combat-hud-expanded-20261106/EDITOR.md), 사용자 배치 JSON과 제작 원본을 함께 보존한다.
- [`ui/inventory-symbols-20261106/`](ui/inventory-symbols-20261106/README.md#개별-unity용-제작본): 인벤토리·정비소용 개별 투명 PNG 71개. 잉크 54종·형태 스탬프 7종·스킬 스탬프 3종·문양 없는 바탕 7종이며, 실제 파일·ID·해시는 `runtime/items.json`으로 확인한다.
- [`ui/combat-hud-approved-20261106/`](ui/combat-hud-approved-20261106/README.md)는 사용자가 반려한 시안 추출본, [`ui/combat-hud-rebuilt-20261106/`](ui/combat-hud-rebuilt-20261106/README.md)는 독립 제작의 중간 보존본이다. 현재 HUD 선택은 `combat-hud-expanded-20261106`이다. 각 제작 문서의 검토 중·미적용 표기는 해당 제작 단계의 기록이며, 메인의 UI 연결 구현과 최신 Unity 실행 검증은 메인에서 관리한다.
- `ui/monster-loot-flat/`: 네이티브 픽셀 격자 원본과 드롭 아이콘·비교 이미지. 승인한 9종은 `asset.json`의 `selectedIcons`로 선택한다. `ui/monster-loot/`에는 생성 원본과 이전 출력·참조를 보존하며 Unity 연결은 메인이 소유한다.
- [`ui/monster-loot-grades/`](ui/monster-loot-grades/)에서 5색의 1~4등급 바닥 드롭 아이콘 20종과 편집용 픽셀 격자 원본 `source/icons.json`을 찾을 수 있다.
- [`ui/ink-part-symbols/`](ui/ink-part-symbols/)에서 잉크 파츠 문양 31종, 등급 문장 4종, 색상별 빈 슬롯 아이콘 5종과 편집용 픽셀 격자 원본 `source/pixels.json`을 찾을 수 있다.
- `audio/approved-combat/`, `fonts/game/`, `effects/wallcoeur/`: 메인에서 사용하던 음원·폰트·보유 VFX 텍스처 원본. `imports/main-media/mapping.json`은 원래 메인 커밋·경로·바이트·해시·메타데이터 해시를 보존한다.
- `monsters/stage-01/bosses/clock-executioner/runtime/stationary/`, `authoring/map-editor/runtime/`: 시계탑 집행자 PNG bytes와 이미지 9장씩을 내장한 현재·이전 지형용 맵 편집기 HTML. `imports/main-boss-map-editor/mapping.json`으로 원래 메인 경로·커밋·바이트·해시를 확인한다. 보스 `.meta`와 편집기 텍스트 원본은 메인이 소유한다.
- `authoring/workbench/source/`: 서로 상대 경로를 참조하는 공통 제작 도구·편집 데이터·미리보기 원본. 메인의 이전 무기·몬스터·숲·스토리·아트 사본을 찾을 수 있다. 원본에 없던 이미지나 링크 대상이 자동으로 생기는 것은 아니다.
- `imports/`: 원본 패키지와 이관 매핑. 파일의 원래 경로·바이트·SHA256 또는 원본 Git 커밋을 연결한다. 패키지를 재압축하거나 원본 해시를 편집 결과로 덮어쓰지 않는다.
- [`environments/stage2-sewer-modular-20261111/`](environments/stage2-sewer-modular-20261111/): 2스테이지 채택 석재 타일, 실제 맵 8구간 검토, 채택 화면 v25(바위 땅 속·돌판 발판·물웅덩이). 편집·재제작 코드 포함, Unity 적용 전.
- `generated-assets.json`: 시작 마을·드롭 아이콘 등 신규 제작 리소스의 파일 크기·SHA256 검증 목록. 기존 이관 입력과 분리한다.

각 대상의 `source/`는 편집 기준본, `runtime/`는 내보낸 출력, `preview/`는 열람 자료다. 같은 이름의 다른 세트는 구분하고 날짜나 번호로 자동 선택하지 않는다. 파일 위치만으로 최종 미술 승인·게임 통합을 확정하지 않는다.

`authoring/workbench/source/` 안의 원본 스크립트는 재제작 입력이며 소비자 명령이나 자동 배치 대체 경로가 아니다. 지원 실행기는 `tools/`에 둔다. 마을 이미지 등록·제작 recipe의 실제 재생성은 필요한 원본 입력·전용 작업 브랜치·출력 분리를 별도로 검증해야 하며 이번 일반 이미지 배치 검증에 포함하지 않는다.

## 소비자에게 배치

메인은 `docs/assets/manifests/unity-deployment.json`에서 사용할 리소스 ID·파일/내장 PNG·내용 해시와 목적지를 선택한다. 메인의 `tools/assets/update_assets.py` 또는 `에셋 업데이트.bat`이 고정 gitlink·깨끗한 외부 작업 상태·Unity 메타데이터를 확인하고 아래 공통 출력 기능을 호출한다.

```sh
python3 tools/assets.py export --manifest /path/to/main/docs/assets/manifests/unity-deployment.json --destination /path/to/main --check
python3 tools/assets.py export --manifest /path/to/main/docs/assets/manifests/unity-deployment.json --destination /path/to/main
```

전체 원본·해시·소유자·목적지·로컬 충돌을 먼저 검사한다. JSON 내장 PNG는 재인코딩 없이 추출한다. 같은 파일은 건너뛰고 알 수 없는 로컬 수정은 덮어쓰지 않는다. 이전에 관리한 바이트만 교체하며 백업·배치 상태는 소비자의 Git 제외 `outputs/assets/`에 둔다. 목록에서 빠진 파일이나 사용자의 개인 백업을 자동 삭제하지 않는다. 외부 코드·별도 폴더·원격 최신 버전으로 대체하지 않는다.

게임 규칙·모션 원본·Unity GUID·씬·프리팹·재질·셰이더·카탈로그·가져오기 설정은 메인 소유다. 미디어 배치로 이들을 재생성하지 않는다. 음원·폰트·보유 VFX 원본은 이 저장소의 LFS로 보존하고 메인의 기존 Unity 경로에 바이트 그대로 배치한다. 비공개 저장은 라이선스 검증이나 외부 공개·제3자 전달 승인을 뜻하지 않는다.

## 제작 출력

아트 재생성은 새 빈 검토 디렉터리에 출력한다. 원본을 결과에서 역생성하거나 소비자 프로젝트를 제작 도구의 임시 출력으로 쓰지 않는다.

```sh
python3 tools/export_stage1_monster_motions.py --destination /path/to/review/monsters
python3 tools/export_stage1_monster_effects.py --destination /path/to/review/effects
python3 tools/export_clock_boss_preview.py --destination /path/to/review/boss
python3 tools/export_clock_boss_telegraphs.py --destination /path/to/review/telegraphs
```

이 변환에는 Pillow·NumPy가 필요하다. `export_hit_variants.py`는 메인의 명시적 `--recipe`, `export-pixel-icons.cjs`는 메인의 잉크 카탈로그와 빈 검토 출력 경로를 받는다. 최종 선택·원본 색인·배치 계약·Unity 연결은 별도로 검토한다. 시작 마을 제작 도구와 원본 출처 가져오기 도구도 `tools/`에 있으며 일반 게임 배치의 대체 입력은 아니다.

맵 편집기 내장 변환은 `tools/build_map_editor.py`가 담당한다. 메인의 `tools/chapter1-map-editor/tools/package.py --out <새 빈 검토 폴더/파일.html>`은 고정된 색인 원본과 메인의 템플릿·JS·맵 데이터를 명시적으로 전달한다. 기존 HTML 원본·소비자 사본·검토 결과를 덮어쓰지 않으며 공유 ZIP을 생성하지 않는다. 생성 후보를 원본으로 채택하려면 에셋 목록·해시·내장 LFS 목록·소비자 배치 계약을 별도로 갱신한다.

## 채택한 1스테이지 미술

잔디·흙·나무 발판은 `environments/stage1-pixel-lawn-wood-32`, 산·마을 분리 배경은 `environments/stage1-pixel-background-native-20261111`, 랜덤 구름·새와 종잇재는 `environments/stage1-atmosphere-sky-ash-20261111`에 있다. [채택본 미리보기](environments/stage1-atmosphere-sky-ash-20261111/preview/index.html)에서 계단·수직 방과 움직이는 효과를 확인한다. 세트의 독립 검증은 `python tools/verify_stage1_approved.py`다. 미술 승인과 본 게임 Unity 배치 계약 통합을 구분한다.

## 원본과 커밋

이미지·음원·폰트·VFX 텍스처·ZIP·미디어 내장 파일은 LFS, 작은 색인·설정·코드는 일반 Git으로 관리한다. 이미지·음원·폰트·VFX를 포함한 HTML 보고서·비교 화면·제작 검토 자료는 에셋 `preview/` 또는 공통 `authoring/`에 저장한다. 내장형 HTML·JS·JSON은 `.lfs-payloads.json`과 `python3 tools/configure_lfs.py --write`로 속성을 함께 맞춘다. 상대 참조형은 HTML·스타일·스크립트·미디어를 함께 색인에 등록하고 링크를 검사한다. 일회성 실행 로그·캡처·작업/검증 이력 보고서는 메인의 Git 제외 `outputs/`에 두며 이 규칙으로 영구 커밋하지 않는다. 기존 원본 바이트·GUID·승인 상태를 보존하고, 현행 작업 문서에 과거 다운로드 진입점을 유지하지 않는다. 원본 파일 자체의 바이트와 변경 이력은 각각 원본 및 Git으로 보존한다.

리소스 커밋·LFS를 먼저 push하고 원격 복원을 확인한 뒤 메인의 배치 계약·필요한 연결 코드·gitlink를 갱신한다. 공유 이력을 squash하지 않는다. 원본·색인·배치 검사는 Unity 실행·브라우저 상호작용·미술 재승인을 대신하지 않는다. 게임 검증은 Unity **6000.6.5f1**과 메인의 원본 패키지 버전을 사용한다.
