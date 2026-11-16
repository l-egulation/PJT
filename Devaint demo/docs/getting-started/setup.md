# 개발 환경 설정

[분야 개요](README.md)

프로젝트 지정 Unity 버전은 **6000.6.5f1**이다. Git LFS는 `resources/` 서브모듈의 이미지·편집 원본을 받는 데 필요하다. Python 3.9 이상은 리소스 검사·배치와 게임 데이터 생성에 사용한다. 일반 배치에는 pip 패키지가 필요 없으며 별도 제작 변환에는 Pillow·NumPy 등이 필요할 수 있다. PowerShell 보조 검사는 `pwsh`를 사용한다.

Editor 실행·테스트·Web/Windows 빌드와 격리 검증 프로젝트 모두 이 버전을 사용한다. 설치되지 않았다면 Unity Hub에서 6000.6.5f1을 설치하고, 다른 버전으로 대체하거나 프로젝트 버전을 낮추지 않는다.

빌드 대상 모듈과 batch/Editor 절차는 [게임 빌드·Windows 배포](../development/delivery/build-and-distribution.md)와 [Web 빌드](../development/delivery/web-build.md)를 따른다.

```sh
git lfs install
git clone --branch main https://lab.ssafy.com/s15-final/S15P31F109.git
```

Windows에서는 `C:/src/F109`처럼 짧은 경로를 권장한다. 위 명령은 메인만 받는다. 새 저장소 폴더로 이동한 뒤 아래 절차로 서브모듈을 준비하고, Unity를 열기 전에 [에셋 복원](../assets/README.md)을 수행한다. 깊이 제한 없는 `--recurse-submodules` clone으로 리소스 과거 이력을 함께 받지 않는다.

## 외부 리소스 서브모듈

`resources/`는 [deviant-resources](https://lab.ssafy.com/h0422ys/deviant-resources)의 Git 서브모듈이다. 이 등록이 포함된 작업 브랜치를 받은 뒤 메인 저장소에 기록된 외부 커밋으로 초기화·갱신한다. 비공개 외부 저장소와 LFS 객체를 읽을 수 있는 GitLab 계정 권한이 필요하다.

이미 clone한 저장소나 브랜치를 갱신한 체크아웃에서는 메인 저장소 루트에서 실행한다. 서브모듈에서 편집 중인 파일과 커밋은 먼저 보존한다.

```sh
GIT_LFS_SKIP_SMUDGE=1 git submodule update --init --recursive --depth 1 resources
git -C resources lfs install --local
git -C resources -c lfs.fetchrecentalways=false lfs pull
python resources/tools/verify_resources.py --check-links
python tools/assets/update_assets.py --check
python tools/assets/update_assets.py
```

첫 줄의 환경 변수 문법은 POSIX 셸/Git Bash용이다. PowerShell에서는 첫 줄 대신 아래 명령을 실행한 뒤 나머지 LFS·검사·배치 명령을 그대로 실행한다. 기존 환경 변수 값은 보존한다.

```powershell
$previousSkipSmudge = $env:GIT_LFS_SKIP_SMUDGE
try {
    $env:GIT_LFS_SKIP_SMUDGE = '1'
    git submodule update --init --recursive --depth 1 resources
    if ($LASTEXITCODE -ne 0) { throw '고정 리소스 커밋 받기 실패; 전체 이력으로 대체하지 마세요.' }
} finally {
    $env:GIT_LFS_SKIP_SMUDGE = $previousSkipSmudge
}
```

일상적인 받기는 현재 고정 SHA의 트리와 필요한 Git 메타데이터만 대상으로 한다. `--depth 1`은 새로 받는 이력을 제한하고, smudge 생략은 초기 clone의 기본 브랜치나 중간 체크아웃의 LFS 파일을 미리 받지 않게 한다. LFS는 고정 커밋을 확인한 후 현재 체크아웃에서만 받으며 최근 이력 자동 다운로드도 비활성화한다. 현재 트리의 원본·미리보기·보존 ZIP은 포함한다. Unity 배치 계약만 선택적으로 다운로드하는 절차는 아니다.

`git fetch --all`·`--unshallow`·`git lfs fetch --all`·`--recent`는 기본 준비에 사용하지 않는다. 추가 이력은 조사·복구 목적과 대상 범위를 확인받아 받는다. 얕은 받기로 고정 SHA에 접근할 수 없으면 중단·보고하며 임의로 전체 이력이나 최신 브랜치를 받지 않는다. 이미 받은 과거 Git 객체·LFS 캐시는 자동 삭제되지 않으며 prune·GC·재clone·초기화는 별도 승인 없이 수행하지 않는다. `git submodule sync`는 URL 설정만 갱신하며 파일을 다운로드하지 않는다.

서브모듈은 메인 커밋이 지정한 외부 커밋을 사용한다. 평소 받기에는 `git submodule update --remote`를 사용하지 않는다. 파일 구성과 현재 보존 범위는 [리소스 폴더 구조](../assets/resource-repository.md)를 따른다.

원본은 `resources/`에 받고, 게임 이미지는 메인의 [배치 계약과 공통 실행기](../assets/README.md)로 Unity 경로에 적용한다. 이 실행기는 고정 gitlink·외부 작업 상태를 확인하며 브랜치나 외부 입력 경로를 자동 선택하지 않는다.

## Unity 프로젝트 열기

Unity Hub에서 `unity`를 추가하고 패키지 다운로드·임포트·컴파일을 완료한다. [전투 테스트](playtest.md)를 실행한다. 실제 사용한 Editor 버전이 지정 버전과 다르면 검증 커밋 본문에 구분해 기록한다.

## 에디터를 열기 전 준비 조건

1. `main`을 받은 경로가 실제 작업 저장소인지 확인하고, 로컬 수정이 있으면 보존한 상태에서 갱신한다.
2. 고정된 `resources`와 LFS를 받은 뒤 [에셋 안내](../assets/README.md)에 따라 `에셋 업데이트.bat --check`와 `에셋 업데이트.bat`을 실행한다. 폴더 선택은 없다.
3. 배치 계약 전체의 이미지 검증·적용 결과를 확인한다. 알 수 없는 로컬 수정은 보존하고 원인을 확인하며 탐색기에서 임의로 덮어쓰지 않는다.
4. 코드·설정·GUID는 메인, 이미지 원본·제작 도구는 `resources`가 소유한다. 들쥐·마을·이동·포탈·GUI도 같은 배치 경로를 사용한다.
5. Unity 6000.6.5f1로 `unity/`를 열고 첫 임포트·폰트 처리·컴파일 완료를 기다린다.

코드만 clone한 상태에서 `.meta`가 있다고 이미지까지 있는 것은 아니다. 원본 이미지가 없는 채로 에디터를 열면 누락 경고가 나고 `.meta`가 제거될 수 있으므로 먼저 복원한다. 기존 이미지·수동 편집본을 지우는 초기화로 해결하지 않는다.

## 실행 준비를 확인하는 최소 기준

Console의 컴파일 오류가 없고, 필요한 이미지·폰트·맵 런타임이 복원되어야 한다. 공통 씬이 열리는 것, Play가 시작되는 것, 지형·캐릭터·HUD가 정상 동작하는 것은 각각 확인한다. 자동 테스트 통과 여부는 [검증 안내](../development/testing/testing.md)에 따라 별도로 기록한다.

## 자주 만나는 문제

| 증상 | 확인할 내용 | 처리 |
|---|---|---|
| `executeMethod ... GameBuilder ... could not be found` | 먼저 발생한 C# 컴파일 오류 | 원래 컴파일 오류부터 해결한 뒤 진입점 재실행 |
| 이미지에 대응하는 `.meta`만 존재 | 고정 gitlink·Git LFS·배치 계약 | `resources`의 올바른 커밋과 LFS를 확인한 뒤 `에셋 업데이트.bat` 실행 |
| `CS0618` 경고 | 지정 Unity 버전에서 폐기 예정 API 사용 | 경고와 실행을 막는 `error`를 구분. 변경 시 호출 의미 확인 |
| Play 중 초기화·참조 오류 | 에셋·카탈로그·씬 참조 누락 등 첫 예외 | 첫 오류의 원인부터 확인. 뒤따르는 반복 예외만 숨기지 않음 |

문제를 전달할 때는 메인·리소스 커밋, Unity 버전, 씬, 배치 결과, 재현 순서, 첫 오류를 함께 보낸다. 원본 로그는 Git 제외 `outputs/`에 둔다.
