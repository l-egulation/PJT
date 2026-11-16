# 리소스 저장소 작업 기준

## 저장소와 적용 범위

- 이 저장소는 `https://lab.ssafy.com/h0422ys/deviant-resources.git`이며, 메인 저장소 `https://lab.ssafy.com/s15-final/S15P31F109`의 `resources/`에 Git 서브모듈로 연결된다. 리소스 저장소의 기본 브랜치는 `master`, 메인 저장소의 기본 브랜치는 `main`이다. 실제 작업 브랜치는 매번 Git으로 확인한다.
- 아래 경로와 명령의 기준 디렉터리는 **리소스 저장소 루트**다. 메인 루트에서 실행할 때는 `git -C resources ...`, `python3 resources/tools/...`처럼 경로를 바꾼다. 단독 clone에서도 이 파일을 기준으로 작업할 수 있어야 한다.
- 먼저 `README.md`와 `catalog.json`을 읽는다. 메인의 `resources/`로 연결해 작업할 때는 `../AGENTS.md`, `../docs/assets/resource-repository.md`, `../docs/workflow/asset-production.md`도 확인한다. 메인의 게임 규칙·전투 수치를 이 저장소의 아트 정보로 대신 확정하지 않는다.

## 경로별 내용

| 경로 | 내용 |
|---|---|
| `catalog.json` | 에셋 ID·분류 경로·파일 수를 연결하는 전체 색인. |
| 각 대상의 `asset.json` | 해당 에셋의 파일 목록·출처·세트와 확인된 검토 상태. |
| `characters/ink/common/` | 공통 캐릭터·무기 열람 자료. |
| `characters/ink/movement/{jump,wall-kick,dash}/` | 점프·벽차기·대시 모션 자료. |
| `characters/ink/weapons/` | 무기별 본체·효과·편집 원본. 발사기는 `launcher/{ball,laser,shotgun,grenade}/`로 나눈다. |
| `monsters/stage-01/normal/` | 가시뿌리·먹빗자루·배수관 포수 자료. |
| `monsters/stage-01/elite/well-warden/`, `monsters/stage-01/bosses/clock-executioner/` | 우물 도르래 수문장과 시계탑 집행자 자료. |
| `monsters/stage-01/source/`, `monsters/stage-01/preview/workbench/` | 여러 몬스터의 공통 제작 설정과 기존 공용 편집기·미리보기. |
| `monsters/legacy/field/`, `monsters/shared/training-dummy/` | 이전 들판 몬스터의 보존 아트와 공용 허수아비. 이전 들판 종을 현행 1스테이지 몬스터로 임의 재배정하지 않는다. |
| `environments/` | 숲 미술 세트(`forest/`), 마을(`village/`), 시계탑 공간(`clock-tower/`). 보스 본체는 `monsters/`가 소유한다. |
| `effects/` | 공용 피격 변주·기본 공격·잉크 효과. 무기·몬스터 전용 효과는 해당 대상 안에 둔다. |
| `sequences/portal/` | 포탈 연출의 캐릭터·포탈 모션, 편집 원본·시간표·미리보기. |
| `ui/` | 전투 HUD·인벤토리 자료. |
| `audio/`, `fonts/`, `effects/wallcoeur/` | 음원·폰트·보유 VFX 텍스처 원본. Unity 적용 경로와 `.meta`·GUID·프리팹·재질·셰이더는 메인이 소유한다. |
| `imports/drive-20261030/packages/` | 기존 배포 ZIP 원본. 임의로 다시 압축해 교체하지 않는다. |
| `imports/drive-20261030/metadata/`, `imports/drive-20261030/unclassified/RuralNature32/` | 원본 안내·패키지 정보와 용도 미확정 이미지. |
| `imports/drive-20261030/mapping.json` | 원본 ZIP 항목과 분류 후 경로·크기·SHA256의 대응. 알려진 이전 Unity 목적지는 `originalProjectPath`다. |
| `tools/verify_resources.py` | 기존 이관 파일의 바이트·색인·이관 매핑·HTML 상대 링크 검사. |
| `tools/configure_lfs.py`, `.gitattributes`, `.lfs-payloads.json` | 바이너리·이미지 내장 파일의 Git LFS 대상과 속성 생성. |
| `authoring/workbench/source/` | 공통 제작 도구·편집 데이터·미리보기의 원래 상대 경로를 보존한 원본. 메인의 이전 공유 사본을 되살리는 배치 경로가 아니다. |
| `tools/assets.py` | 색인 기반 원본 검증과 소비자가 명시한 계약의 출력. 공유 폴더·수동 ZIP·원격 최신으로 대체하지 않는다. |
| `tools/export_*`, `tools/authoring_output.py` | 새 빈 검토 디렉터리로 출력하는 아트 변환. 메인의 게임 수치·모션 원본과 Unity GUID는 쓰지 않는다. |

각 대상 안의 `source/`는 재편집할 기준본·레이어·요청문, `runtime/`는 게임용으로 내보낸 결과, `preview/`는 열람·비교 자료다. `runtime/`에 있다는 이유로 최종 승인·현재 게임 연결이 완료된 것으로 보지 않는다. 서로 다른 기존 세트는 `approved-eleven`, `approved-eleven-package` 등 명시적 세트로 구분하며 이름·날짜가 크다는 이유로 자동 선택하지 않는다.

이미지뿐 아니라 **음원·폰트·VFX 원본도 이 저장소가 소유**한다. Unity에서 사용하는 사본은 메인의 배치 계약으로 원래 경로에 복원하며 메인 `.meta`·GUID·씬·프리팹·재질·셰이더·카탈로그를 이관하거나 다시 생성하지 않는다. 메인의 기존 추적 원본은 외부 커밋·push·독립 복원·배치 검증 전에는 추적 해제하지 않으며, 추적 해제 시에도 로컬 배치 사본은 보존한다. 비공개 저장소라는 사실은 개별 라이선스나 제3자 재배포 권리의 확인을 대신하지 않는다.

**이미지·음원·폰트·VFX가 포함된 HTML 보고서·비교 화면·제작 검토 자료도 이 저장소에 저장**한다. 대상 에셋의 `preview/` 또는 공통 제작 자료의 `authoring/` 아래에서 관리한다. 미디어 내장형과 상대 파일 참조형 모두 HTML·필요한 미디어·스타일·스크립트를 함께 색인에 등록하고 상대 링크를 검사한다. 영구 보존할 제작·검토 자료와 일회성 실행 로그·작업/검증 이력 보고서는 구분하며, 후자는 메인의 Git 제외 `outputs/`에 둔다.

## 원본·색인·LFS 편집

- 원본 픽셀·프레임 이름·투명 캔버스·원본 데이터·제공된 `.meta`의 바이트와 GUID를 보존한다. 분류·정리를 이유로 확대·크롭·재인코딩하거나 같은 이름의 다른 세트를 덮어쓰지 않는다.
- 대상별로 함께 편집하는 자료를 묶고 `asset.json`과 `catalog.json`을 함께 맞춘다. 승인·통합 여부가 확인되지 않았으면 미확정 상태를 유지한다.
- `mapping.json`은 기존 입력 ZIP의 출처 대응이다. 편집 결과를 기존 ZIP의 원본인 것처럼 해시만 바꿔 통과시키지 않는다. 새 출력·변형·출처는 기존 원본을 보존하고 그 관계와 검증 범위를 명시한다.
- 이미지·오디오·폰트·VFX 텍스처·영상·보존 ZIP 등 바이너리는 Git LFS로 관리한다. 이미지/오디오/폰트/VFX를 내장한 HTML·JS·JSON도 `.lfs-payloads.json`에 등록하고 `python3 tools/configure_lfs.py --write`로 속성을 재생성한다. 외부 미디어를 상대 참조하는 HTML은 본문과 미디어를 함께 등록하며 바이너리 미디어에 LFS를 적용한다. 작은 색인·설정·코드·`AGENTS.md`는 일반 Git으로 관리한다.
- 원본·변환·검증·출력 도구는 이 저장소가 소유한다. 메인은 선택한 에셋 버전·배치 경로와 Unity 어댑터만 소유하며 `tools/assets/update_assets.py`로 고정 gitlink의 공통 출력을 호출한다. 현행 안내·코드·메타데이터에 과거 다운로드 URL·파일 ID·외부 폴더 선택 경로를 유지하지 않는다. 보존된 원본 파일 자체의 바이트는 바꾸지 않는다.
- 편집 원본·미리보기·보존 ZIP·미분류 자료를 인게임 미사용이라는 이유만으로 삭제하지 않는다. Unity 적용 사본의 정리와 리소스 원본의 보존은 별개다. `imports/`는 기존 출처 보존 경계이며 새 제작물이나 변경마다 생기는 ZIP의 일상 저장 위치가 아니다.

## 서브모듈 상태와 커밋 순서

### 받기

메인 루트에서 `GIT_LFS_SKIP_SMUDGE=1 git submodule update --init --recursive --depth 1 resources`로 메인이 고정한 커밋을 얕게 받고 LFS 자동 다운로드를 건너뛴 뒤, 이 저장소에서 실행한다. PowerShell에서는 메인의 `docs/getting-started/setup.md`에 따라 환경 변수를 설정한다.

```sh
git status --short --branch
git lfs install --local
git -c lfs.fetchrecentalways=false lfs pull
python3 tools/verify_resources.py --check-links
```

- **일상적인 받기는 현재 고정 SHA의 체크아웃에 필요한 파일과 Git 메타데이터로 한정한다.** 단독 clone도 `--depth 1`과 `GIT_LFS_SKIP_SMUDGE=1`을 사용하고, 합의한 커밋·브랜치를 체크아웃한 뒤에만 LFS를 받는다. 현재 트리에 있는 제작 원본·미리보기·보존 ZIP은 포함하며 Unity 배치 파일만 받는 절차와 혼동하지 않는다.
- 과거 전체 Git 이력을 받는 일반 clone·`git fetch --all`·`--unshallow`, LFS의 `fetch --all`·`fetch --recent`와 최근 이력 자동 다운로드를 기본 절차로 사용하지 않는다. 추가 이력이 필요한 조사·복구·백업은 목적·대상 커밋·범위를 확인받아 별도로 수행한다. 얕은 받기로 고정 SHA를 확보할 수 없으면 중단·보고하고 전체 이력이나 `master` 최신으로 자동 대체하지 않는다.
- `git submodule sync`는 URL 설정 동기화일 뿐 다운로드가 아니다. 얕은 갱신·현재 체크아웃의 LFS 받기는 기존 Git 객체·LFS 캐시를 지우지 않는다. 사용자의 기존 작업·캐시를 별도 승인 없이 prune·GC·재clone·초기화하지 않는다.
- 서브모듈의 detached HEAD는 고정 SHA를 받은 정상 상태다. 그 상태에서 새 커밋을 만들거나, 메인의 지시 없이 `master` 최신으로 바꾸지 않는다.
- 수정·커밋 전에는 현재 변경을 보존하고, 합의한 리소스 작업 브랜치를 명시적으로 선택한다. 메인 작업 브랜치와 별도로 관리한다. 받기 작업에 `git submodule update --remote`, 강제 checkout·reset을 사용하지 않는다.

### 어느 저장소에 무엇을 남기는가

1. **리소스 저장소**: 이 파일을 포함한 리소스 내용, 관련 색인·메타데이터·LFS 설정·도구를 기능 단위로 커밋한다. 여기서의 `git add` 경로에는 `resources/` 접두사를 붙이지 않는다.
2. 리소스 커밋을 원격에 push하고 LFS 업로드도 확인한다. 외부 MR이 필요한 변경은 리소스 저장소를 대상으로 처리하며, 메인에서 채택할 확정 SHA를 정한다.
3. **메인 저장소**: 사용할 외부 SHA로 `resources/`를 맞춘 후, 메인 루트에서 `git add resources`로 gitlink를 스테이징한다. 필요한 Unity 코드·적용 `.meta`·복원 매니페스트·문서·테스트도 메인에 커밋한다.
4. 메인에서 `git diff --cached --submodule=log`를 확인하고, 리소스 커밋과 LFS 객체를 다른 환경에서도 받을 수 있는 상태에서 메인 MR을 머지한다. 리소스 미커밋 변경이나 로컬에만 있는 SHA를 메인 커밋으로 배포하지 않는다.

원본에 포함된 `.meta`를 보존하는 변경은 리소스 커밋, 메인 Unity 적용 경로·GUID·가져오기 설정 변경은 메인 커밋이다. `originalProjectPath`는 출처 정보이지 모든 파일의 자동 배치 지시가 아니다. 다운로드·Unity 배치·게임 바인딩·빌드 검증은 각각 확인한다.

## 검증·리뷰·MR

```sh
python3 tools/configure_lfs.py
python3 tools/verify_resources.py --check-links
git lfs fsck
git diff --check
git diff --cached
```

- 원본 Drive ZIP이 있을 때만 `python3 tools/verify_resources.py --archive "<원본 ZIP>" --check-links`로 전체 입력 대응까지 확인한다. 실행하지 않은 원본 아카이브 검증을 통과로 기록하지 않는다.
- 파일·색인·링크 검사 통과는 브라우저 상호작용·미술 재승인·Unity 게임 실행 통과를 의미하지 않는다. Unity 검증은 메인에서 **6000.6.5f1**과 원본 패키지 버전으로 따로 수행한다.
- 이슈·MR은 해당 저장소에서 `glab` CLI로 관리한다. 인증 대상은 `lab.ssafy.com`이며 필요하면 `glab auth login --hostname lab.ssafy.com`으로 설정한다. **머지 시 squash를 사용하지 않는다.**
- 독립적으로 검토·검증·되돌릴 수 있는 기능 단위로 커밋하고, 제목에는 변경 후 동작을 적는다. 본문에는 문제·목적·핵심 변경·영향 범위·검증 명령/환경/결과와 미실행 항목을 남긴다. 스테이징한 diff와 LFS 속성을 확인하고 관련 없는 파일·로컬 설정을 포함하지 않는다.
- 작업·변경·검증 이력은 커밋에 남긴다. 저장소에 별도 이력·삭제 보고서를 만들지 않는다. 로컬 로그·캡처·검증 사본은 메인의 Git 제외 `outputs/` 또는 저장소 밖에 둔다.
