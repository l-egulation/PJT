# 작업 기준

## 저장소와 경로 안내

이 파일은 메인 저장소 `s15-final/S15P31F109`의 작업 기준이다. `resources/`는 같은 저장소의 일반 폴더가 아니라 별도 저장소 `h0422ys/deviant-resources`를 연결한 Git 서브모듈이다. 리소스 작업에는 `resources/AGENTS.md`도 함께 따른다.

| 경로 | 내용과 역할 |
|---|---|
| `docs/README.md` | 문서 입구. 기획·개발·에셋 복원·검증 문서로 이동한다. |
| `docs/overview/` | 게임 전체 개요·용어·기획/구현 현황·미정 사항 색인. |
| `docs/design/` | 현재 게임 규칙, 런·스토리·전투·UI 등 기획 명세. |
| `docs/development/` | 구현 범위, 코드 구조, 데이터 생성, 테스트·빌드 방법. |
| `docs/assets/` | 리소스 받기·Unity 배치 방법. `manifests/unity-deployment.json`은 선택한 원본 버전과 메인 소유 배치 경로의 계약이다. |
| `data/` | 문서가 지정한 구조화된 편집 원본. 전투 수치·행동·모션 바인딩의 원본은 `data/combat/`다. |
| `unity/Assets/` | 게임 코드, 씬·프리팹·재질·셰이더, Unity 적용 에셋·`.meta`, 생성 카탈로그. |
| `unity/Packages/`, `unity/ProjectSettings/` | 원본 패키지 버전과 프로젝트 설정. |
| `unity/Tests/Fixtures/` | Assets 밖의 구형 씬 회귀 검사 자료. 현재 게임의 진입 씬이 아니다. |
| `tools/` | `assets/`의 고정 리소스 실행기, `combat/`의 게임 데이터 생성, `audio/`의 음원 검사 등 메인 소유 개발 도구. 아트 변환·리소스 검증은 외부 `resources/tools/`가 소유한다. |
| `resources/` | 별도 Git LFS 저장소의 이미지·음원·폰트·VFX 원본·편집 원본·미디어 포함 HTML 검토 자료·미리보기·보존 ZIP·색인. 세부 구조는 그 안의 `AGENTS.md`, `README.md`, `catalog.json`을 읽는다. |
| `resources/authoring/workbench/source/` | 무기·몬스터·숲·스토리·아트의 공통 제작 도구·편집 데이터·미리보기 원본. 메인의 이전 공유 사본 경로를 자동 복원하지 않는다. |
| `Stage1VillageReference/` | 마을 제작 방향의 메인 명세·참조. |
| `Playtest/`, `전투테스트/` | 별도 배포물·이전 시험 자료. 현재 Unity 실행 기준으로 대신 사용하지 않는다. |
| `outputs/` | Git 제외 로컬 검증 사본·로그·캡처·백업·빌드. 커밋하지 않는다. |

현재 게임은 `unity/Assets/ForestCombatV2/Scenes/Chapter1Title.unity` → `unity/Assets/Deviant/Hub/Scenes/StartingVillage.unity` → `unity/Assets/MeadowDuskV3/MapVer2/Scenes/Chapter1Procedural.unity`로 연결된다. 상세 실행 방법은 `docs/getting-started/playtest.md`를 따른다.

## 에셋 공유와 내보내기

- **에셋을 Google Drive 등 외부 공유 드라이브에 공유·배포하기 위한 export·패키징·업로드는 금지한다.** 공유 폴더용 이미지/ZIP 생성, 게시·동기화·복원 스크립트, 별도 배포 목록·다운로드 URL·파일 ID를 새로 만들거나 폐기한 진입점을 되살리지 않는다.
- 드라이브 공유를 목적으로 에셋을 내보내려는 요청이나 작업 흐름을 발견하면 그 경로로 진행하지 않고 **`resources/` 서브모듈에 원본·출력·필요한 메타데이터를 등록하는 방식으로 대신 처리한다.** 리소스 작업 브랜치 확인, 색인·해시·Git LFS 검증, 외부 커밋·push와 메인의 배치 계약·gitlink 갱신은 아래 서브모듈 절차를 따른다.
- 리소스 제작·검토를 위한 새 디렉터리 출력과 메인 배치 계약에 따른 Unity 적용은 공유 드라이브 배포용 export와 구분한다. 해당 작업은 `resources/tools/`의 제작·출력 도구와 메인의 고정 리소스 실행기로 수행하며 별도의 드라이브 공유 경로를 만들지 않는다.

## Unity 버전

- 이 프로젝트의 Editor·테스트·빌드는 **Unity 6000.6.5f1**로 고정한다. 격리 검증 프로젝트도 같은 버전과 원본 패키지 버전을 사용한다.
- 지정 버전이 설치되지 않았으면 설치한 뒤 진행한다. 다른 설치 버전으로 대체하거나 검증을 위해 프로젝트 버전을 낮추지 않는다.

## 치트 모드 명칭과 빌드 요청

- 게임 내 조작 메뉴·콘솔과 이를 포함하는 빌드 기능은 **치트 모드**로 통일해 부른다. 기존 구현의 `DEVIANT_DEBUG_MENU`는 치트 모드 포함 플래그다. 코드 식별자·기존 배치 호출은 호환성을 유지한다.
- 사용자가 **“치트 모드로 빌드해줘”** 또는 **“치트모드로 빌드해줘”**라고 요청하면 대상 플랫폼의 일반 빌드에 **`DEVIANT_DEBUG_MENU`만 추가**한다. `BuildOptions.None`을 사용하며 `Development Build`, `Script Debugging`, 프로파일러 연결을 함께 켜지 않는다. 사용자가 별도로 명시한 디버깅 옵션만 추가할 수 있다.
- Unity 6000.6.5f1에서 `Deviant.Game.Editor.GameBuilder.BuildLinuxCheatMode`, `BuildWindowsCheatMode`, `BuildWebCheatMode` 중 대상 플랫폼의 메서드를 사용한다. `BuildWindowsDebug`는 스크립트 디버깅용이므로 치트 모드 요청에 사용하지 않는다.
- 치트 모드 빌드는 `extraScriptingDefines`로 해당 빌드에만 플래그를 추가한다. 프로젝트의 전역 심볼·개발 빌드 설정을 바꾸지 않는다. 출력은 `DEVIANT_CHEAT_BUILD_OUTPUT`으로 지정하며 기본 경로는 `outputs/builds/<플랫폼>-cheat/`다. 빌드가 끝나면 실제 옵션과 치트 모드 포함 여부를 확인한다.
- 상세 조작과 빌드 계약은 [치트 모드 명세](docs/development/tools/debug-menu.md), [빌드와 배포](docs/development/delivery/build-and-distribution.md)를 따른다.

## 문서와 기준 원본

- 문서 입구는 `docs/README.md`다. 현재 규칙은 `docs/design/`, 구현 범위와 개발·검증 방법은 `docs/development/`를 따른다.
- 각 분야는 `README.md`의 공통 개념에서 하위 개념·개별 콘텐츠로 내려간다. 규칙의 소유 문서는 하나이며 문서 추가·이동은 `docs/workflow/documentation.md`를 따른다.
- **구현의 기준 원본(SSOT)은 문서 시스템과 문서에서 지정한 구조화 데이터다.** 구현 전 관련 명세와 데이터를 확인하고 그 기준에 맞춰 코드·씬·프리팹을 작성한다.
- 코드·씬·프리팹이 명세와 충돌할 때 기존 구현을 정답으로 취급하거나 구현에 맞춰 명세를 자동 변경하지 않는다. 사용자 지시와 확정된 기획을 근거로 충돌을 해소하고, 판단 근거가 부족하면 확인을 요청한다. 명세에 없는 제품 규칙이나 미정 사항을 임의로 확정하지 않는다.
- 동작을 변경할 때는 관련 명세·데이터와 구현을 함께 갱신한다. 문서의 의도한 동작과 실제 구현 범위·알려진 제한을 구분해 기록하며, 문서 갱신만으로 구현·검증 완료를 주장하지 않는다.
- 문서에는 현재 유효한 규칙·미정 사항·구현 범위·작업 방법만 둔다. 결정·변경·작업·검증 이력, 정리·삭제 보고서, 과거 안내 보관함을 별도 파일로 만들지 않는다. 이력은 Git 커밋으로 관리한다.
- 게임 수치의 편집 원본은 `data/combat/`다. 생성 문서와 Unity 카탈로그만 직접 수정하지 않는다. `docs/development/data/data-pipeline.md`의 재생성 절차를 따른다.
- 설계 확정, 테스트 씬 적용, 본 게임 통합, 검증 수행을 구분한다. 문서 정리를 기능 구현이나 검증 완료로 표현하지 않는다.

## 서브모듈 운용과 커밋 소유권

### 어느 저장소에 커밋하는가

| 변경 대상 | 커밋할 저장소 |
|---|---|
| 루트 `AGENTS.md`, `docs/`, `data/`, 메인 `tools/`, 게임 코드·씬·프리팹·Unity 가져오기 설정·배포 매니페스트 | 메인 저장소 |
| `resources/` 안의 파일 전체: `AGENTS.md`, 아트·편집 원본·미리보기·보존 ZIP, `asset.json`, `catalog.json`, 이관 매핑, 리소스 도구·LFS 설정 | 리소스 저장소. `git -C resources ...`로 커밋한다. |
| `.gitmodules`의 서브모듈 등록·URL, 사용할 리소스 커밋을 나타내는 `resources` gitlink | 메인 저장소 |

- 메인 저장소는 리소스 파일 내용이 아니라 **외부 저장소의 특정 커밋 SHA**를 기록한다. 메인에서 `git add resources`만 해서는 리소스 내부의 미커밋 변경이 저장되지 않는다. 반대로 리소스만 커밋해도 메인의 gitlink는 자동 갱신되지 않는다.
- 배포용 원본과 Unity 적용 사본을 구분한다. **이미지·음원·폰트·VFX의 원본은 `resources/`에서 관리**한다. 외부 원본·거기에 포함된 원본 `.meta`는 리소스 저장소, 메인의 Unity 경로·GUID·가져오기 설정·씬·프리팹·재질·셰이더와 배치 매니페스트는 메인 저장소가 소유한다. Unity의 WAV·폰트·VFX 텍스처는 고정 리소스의 로컬 배치 사본으로 사용하며 원본 픽셀·오디오·폰트 바이트를 재인코딩하지 않는다. 기존 메인 추적 해제는 리소스 등록·커밋·push·독립 복원과 배치 검증 후 수행하며 로컬 파일·`.meta`는 지우지 않는다. 비공개 저장소라는 사실만으로 라이선스·재배포 권리가 확인된 것으로 간주하지 않는다.
- **이미지·음원·폰트·VFX를 포함한 HTML 보고서·비교 화면·제작 검토 자료도 `resources/`에 저장**한다. Base64/data URI로 내장했는지 옆 파일을 상대 참조하는지와 관계없이 HTML과 필요한 미디어·스타일·스크립트를 함께 등록하고 링크를 검증한다. 내장 미디어 HTML·JS·JSON은 `.lfs-payloads.json`과 LFS 속성을 함께 등록한다. 메인에는 텍스트 명세·게임 데이터·연결 코드와 리소스 경로 안내를 두며, 파일 확장자가 HTML/JS라는 이유로 미디어 원본을 메인에 저장하지 않는다. 이 규칙은 실행 로그·작업/검증 이력 보고서를 새로 커밋하는 근거가 아니다. 일회성 로그·캡처는 계속 Git 제외 `outputs/`에 둔다.
- 서브모듈 다운로드는 Unity 배치를 뜻하지 않는다. `docs/assets/README.md`의 공통 실행기 `tools/assets/update_assets.py`와 메인의 `unity-deployment.json`으로 배치한다. 외부 폴더·수동 ZIP·원격 최신 세트로 대체하지 않는다. 배치는 계약에 명시된 미디어 바이트만 운반하고 게임 데이터·Unity GUID를 다시 생성하지 않는다.

### 받기와 작업 시작

메인 작업 폴더에서 실제 브랜치와 두 저장소의 변경을 확인한다. 미커밋 파일이 있으면 보존하며, 아래 명령으로 덮어쓰거나 초기화하지 않는다.

```sh
git status --short --branch
git -C resources status --short --branch  # 초기화된 서브모듈에서만 실행
git submodule status
GIT_LFS_SKIP_SMUDGE=1 git submodule update --init --recursive --depth 1 resources
git -C resources lfs install --local
git -C resources -c lfs.fetchrecentalways=false lfs pull
python3 resources/tools/verify_resources.py --check-links
```

- Git LFS와 비공개 저장소 접근 권한이 필요하다. 전체 환경 설정은 `docs/getting-started/setup.md`를 따른다.
- **일상적인 받기는 현재 메인이 고정한 체크아웃에 필요한 파일만 대상으로 한다.** 일반 Git 객체는 `--depth 1`로 이력을 제한하고, clone·checkout 중 LFS 자동 다운로드는 `GIT_LFS_SKIP_SMUDGE=1`로 건너뛴 뒤 고정 SHA의 체크아웃에서만 `git lfs pull`을 실행한다. PowerShell 환경 변수 설정은 `docs/getting-started/setup.md`를 따른다.
- 과거 전체 객체를 받는 일반 clone·`git fetch --all`·`--unshallow`, LFS의 `fetch --all`·`fetch --recent`와 최근 이력 자동 다운로드는 기본 절차에서 사용하지 않는다. 이력 조사·복구·백업에 추가 객체가 필요하면 목적·대상 커밋·범위를 확인받아 별도로 수행한다. 얕은 받기로 고정 SHA를 확보하지 못하면 중단·보고하며 임의로 전체 이력이나 최신 브랜치로 대체하지 않는다.
- 현재 파일은 고정 SHA의 트리에 포함된 원본·미리보기·보존 ZIP을 뜻하며 Unity 배치 목록만을 뜻하지 않는다. `git submodule sync`는 URL 설정만 동기화한다. 위 절차는 이미 받은 Git 객체·LFS 캐시를 자동 삭제하거나 기존 clone의 디스크 사용량을 줄이지 않으며, 별도 승인 없이 prune·GC·재clone·초기화로 기존 작업을 정리하지 않는다.
- 메인 브랜치를 갱신한 뒤에도 위 서브모듈 갱신 절차로 **메인이 지정한 커밋**을 받는다. 일상적인 받기에 `git submodule update --remote`나 서브모듈 안의 무조건적인 `git pull`을 사용하지 않는다.
- 서브모듈이 고정 커밋의 detached HEAD인 것은 정상이다. 리소스를 수정·커밋하려면 먼저 리소스 저장소에서 사용할 작업 브랜치를 확인·선택한다. 메인의 작업 브랜치와 리소스 작업 브랜치는 독립적이며, 한쪽의 브랜치 전환으로 다른 쪽이 자동 전환되지 않는다.

### 리소스를 바꿔 메인에 반영하는 순서

1. 리소스 작업 브랜치에서 해당 파일과 색인·해시·LFS 속성을 함께 검증한다. 상세 절차는 `resources/AGENTS.md`를 따른다.
2. **리소스 저장소에서 먼저 커밋하고 push**한다. 외부 MR이 필요하면 확정된 리소스 커밋을 준비한다. LFS 객체도 정상 업로드되어야 한다.
3. 메인의 `resources/`를 사용할 외부 커밋에 맞춘다. 원격에서 받을 수 없는 로컬 전용 SHA나 의도하지 않은 외부 브랜치의 tip을 기록하지 않는다.
4. 메인에서 필요한 코드·Unity 적용 설정·배포 목록·문서·테스트와 `resources` gitlink를 해당 기능 단위로 커밋한다. 리소스 파일 내용과 메인 gitlink는 두 저장소에 각각 커밋해야 한다.
5. push·머지 전 두 저장소의 상태, `git diff --cached --submodule=log`, 외부 SHA의 원격 접근성·LFS 복원, 필요한 Unity 검증을 확인한다. 메인 MR을 먼저 머지해 팀원이 받을 수 없는 리소스 커밋을 배포하지 않는다.

파일이 게임에서 호출되지 않는다는 사실만으로 리소스 저장소의 제작 원본·다른 세트·공유 패키지를 삭제하지 않는다. Unity 배치 사본을 제거할 때는 동적 로딩·테스트·생성 입력을 재확인하고 메인의 `.meta`와 복원 목록도 함께 맞춘다. 외부 보존 원본의 변경은 별도 검토한다.

## 기획 작업

- 게임 기획은 `docs/overview/overview.md`, 런 구조는 `docs/design/run/run-structure.md`를 따른다. 스토리·연출·튜토리얼 작업 전 `docs/design/story/opening/opening.md`를 읽는다.
- 무기·잉크는 `docs/design/equipment/rules.md`, 전투는 `docs/design/combat/rules.md`를 따른다. 미정 종류·형태와 전용 잉크 개별 콘텐츠를 임의로 채우지 않는다.
- 후속 설계 질문은 관련 항목을 최대 10개까지 묶고, 질문별 최소 3개 선택지와 추천 근거를 제시한다. 선택하지 않은 후보를 자동 확정하지 않는다. 검·둔기(창 제외)의 초기 설계는 기존 위임 범위에서 진행할 수 있다.
- 설계 진행은 `docs/design/combat/checklist.md`를 따른다. 설계 5항목(10%) 완료마다 보고하며 구현 완료율과 혼동하지 않는다.
- 제작 기준본과 공유 사본은 `docs/workflow/asset-production.md`에 따라 구분한다. 기획 변경을 이유로 공용 아트나 별도 배포 원본을 임의로 삭제하지 않는다.

## GitLab 작업

- 이 저장소는 GitLab을 사용한다. 이슈·MR(Merge Request) 관리 등 GitLab 관련 작업은 `glab` CLI를 사용한다.
- `glab` CLI가 설치되어 있지 않거나 인증·대상 GitLab 호스트 설정이 완료되지 않았다면, 사용자에게 설치 및 `glab auth login --hostname lab.ssafy.com` 등 필요한 설정 방법을 안내한다. 메인 MR과 리소스 MR은 각각 해당 저장소를 대상으로 실행한다.

## 커밋 규칙

- 커밋은 **독립적으로 검토·검증·되돌릴 수 있는 기능 작업 단위**로 묶는다. 한 기능의 구현·호출부·테스트·관련 문서·필요한 생성물은 함께 커밋한다.
- 서로 독립된 기능이나 버그 수정, 작업 기준 변경은 별도 커밋으로 나눈다. 같은 기능을 완성하는 연속 편집·보완은 하나의 일관된 작업으로 묶는다.
- 커밋 크기는 기능의 완결성과 리뷰에 필요한 맥락을 기준으로 정한다. 경로 이동도 해당 기능 작업에 필요하면 함께 묶을 수 있다.
- 제목에는 대상 기능과 변경 후의 동작을 구체적으로 적는다. `문서 수정`, `정리`, `업데이트`처럼 작업의 결과를 알 수 없는 제목은 피한다.
- 본문은 커밋 하나만 읽어도 **해결한 문제·변경 목적·핵심 구현·변경 후 동작·영향 범위·검증 결과**를 이해할 수 있게 작성한다. 중요한 결정 이유와 대안 선택도 함께 남긴다.
- 검증 결과에는 실행 명령·환경·범위·결과·미실행 항목을 기록한다. 실행하지 않은 검증을 통과로 기록하지 않는다.
- 검증 로그와 보고서는 저장소 문서로 추가하지 않는다. 필요한 실행 출력은 Git 제외 `outputs/`에 두고 커밋 본문에는 판단에 필요한 결과를 남긴다.
- 커밋 전 스테이징된 diff를 확인하고 사용자 변경·관계없는 파일·로컬 설정을 포함하지 않는다.
- **MR 머지 시 squash를 사용하지 않는다.** 대상 브랜치의 Git 이력에서 기능 작업 단위의 커밋과 결정·검증 근거를 추적할 수 있도록 보존한다. Merge commit 또는 squash 없는 fast-forward·rebase 기반 머지를 사용한다.
- 머지 전에는 같은 기능 작업에 속하는 구현·수정·보완 커밋을 하나로 합칠 수 있다. 합친 커밋의 본문에는 기능 전체의 변경 이유·동작·검증 근거를 보존한다. 이미 공유한 이력을 재작성할 때는 협업 영향을 먼저 확인한다.
