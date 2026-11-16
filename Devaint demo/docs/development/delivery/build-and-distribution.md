# 게임 빌드와 배포 준비

[분야 개요](README.md)

현재 제품 빌드 진입점은 [`GameBuilder.cs`](../../../unity/Assets/Deviant/Game/Editor/GameBuilder.cs)의 Web과 Windows x64다. 이 문서는 공통 준비와 Windows 바이너리를 소유하고, Web 설정·실행은 [Web 빌드](web-build.md), 수집 서버 배포는 [Go/Docker 안내](../../../backend/collector/README.md)를 따른다.

## 공통 준비

1. 작업 브랜치·워크트리와 기존 변경을 확인한다. 실행 중인 게임/테스트 서버를 빌드 준비 때문에 종료하지 않는다. batch 빌드에는 같은 `unity` 프로젝트를 연 Editor가 없어야 한다.
2. `unity/ProjectSettings/ProjectVersion.txt`와 동일한 **Unity 6000.6.5f1**을 사용한다. 최초 실행에 필요한 Unity 라이선스를 준비하고 원본 `Packages/manifest.json`·lock의 패키지 버전을 유지한다.
3. Unity Hub에서 해당 Editor의 **Add modules**로 대상 모듈을 설치한다. Web은 Web Build Support, Windows는 선택한 scripting backend에 맞는 Windows Build Support가 필요하다. Windows IL2CPP를 선택할 경우 해당 모듈과 네이티브 빌드 도구도 필요하며, 아래 메서드는 backend를 임의로 변경하지 않는다.
4. Unity를 닫고 메인이 고정한 `resources` SHA와 LFS 파일을 받은 뒤, 고정 배치 계약으로 미디어를 복원한다. [에셋 복원](../../assets/README.md)의 검증을 따른다.

저장소 루트에서 실행한다. 미커밋 리소스 변경이 있으면 먼저 보존한다.

```sh
git status --short --branch
GIT_LFS_SKIP_SMUDGE=1 git submodule update --init --recursive --depth 1 resources
git -C resources lfs install --local
git -C resources -c lfs.fetchrecentalways=false lfs pull
python3 resources/tools/verify_resources.py --check-links
python3 tools/assets/update_assets.py --check
python3 tools/assets/update_assets.py
```

각 명령의 종료 코드와 출력을 확인하고 실패를 해결한 뒤 다음 단계로 진행한다. PowerShell의 환경 변수 문법은 [환경 설정](../../getting-started/setup.md#외부-리소스-서브모듈)을 따른다. 전체 리소스 색인 검사와 게임에 선택된 파일의 배치 검사는 범위가 다르므로, 실패한 검사를 다른 검사 성공으로 대체하지 않는다. 고정 리소스 불일치를 로컬 원본 재작성이나 원격 최신 커밋 채택으로 숨기지 않는다.

일반 Editor에서는 `unity`를 열고 에셋 가져오기·폰트 처리·컴파일이 끝날 때까지 기다린다. batch 명령도 처음에는 같은 처리를 수행하므로 첫 대상 빌드는 오래 걸릴 수 있다. 명령 실행 전 Git 제외 `outputs/builds/`를 만든다. 빌드·로그·일회성 캡처를 커밋하지 않는다.

## 씬과 제품 설정

Web/Windows 메서드는 다음 네 씬을 명시적으로 전달한다. Editor Build Settings에 임시 시험 씬이 남아 있어도 이 목록을 대신하지 않는다.

1. `Assets/ForestCombatV2/Scenes/Chapter1Title.unity`
2. `Assets/Deviant/Hub/Scenes/StartingVillage.unity`
3. `Assets/MeadowDuskV3/MapVer2/Scenes/Chapter1Procedural.unity`
4. `Assets/Chapter2/Scenes/Chapter2Procedural.unity`

두 메서드는 [미디어 압축](media-compression.md)의 플랫폼 설정·Opus 포장을 적용하고 필수 게임 에셋 존재를 확인하고 `HitFeedbackAssetBuilder.Build()`로 Git 제외 타격 VFX 바인딩을 생성한 뒤 `BuildPipeline.BuildPlayer`를 실행한다. 필수 파일의 간단한 존재 검사만으로 전체 해시/리소스 검증을 대신하지 않는다.

표준 Web/Windows 제품 빌드는 `BuildOptions.None`을 사용한다. 현재 공통 개발 도구는 [치트 모드](../tools/debug-menu.md)의 Editor/`DEVIANT_DEBUG_MENU` 빌드 옵션을 따른다. Web/Windows 표준 메서드는 이 define을 추가하지 않는다. Windows 개발 빌드는 아래 `BuildWindowsDebug`를 사용한다. 기록·서버 전송은 제품 화면의 버튼·설정 창 없이 백그라운드에서 진행한다. 수치/생성 데이터 변경이 있으면 빌드 전에 [데이터 파이프라인](../data/data-pipeline.md)의 해당 생성·검사를 수행한다.

## 치트 모드 빌드

“치트 모드로 빌드해줘”라는 요청은 **`BuildOptions.None` + `DEVIANT_DEBUG_MENU`만 활성화**하는 빌드다. Development Build·Script Debugging·프로파일러 연결은 추가하지 않는다. 해당 빌드에만 `extraScriptingDefines`를 전달하고 프로젝트의 전역 설정은 유지한다.

| 대상 | Unity 배치 메서드 (`Deviant.Game.Editor.GameBuilder.` 뒤) | 기본 출력 |
|---|---|---|
| Linux | `BuildLinuxCheatMode` | `outputs/builds/linux-cheat/deviant.x86_64` |
| Windows | `BuildWindowsCheatMode` | `outputs/builds/windows-cheat/deviant.exe` |
| Web | `BuildWebCheatMode` | `outputs/builds/web-cheat/` |

Editor 메뉴는 **Tools > deviant > Game > Build <플랫폼> With Cheat Mode**다. `DEVIANT_CHEAT_BUILD_OUTPUT`으로 출력 디렉터리를 지정한다. 새 변수가 없으면 기존 `DEVIANT_DEBUG_BUILD_OUTPUT`도 호환용으로 읽는다. `BuildLinuxDebug`는 기존 배치 호출 호환용 별칭이다. `BuildWindowsDebug`는 아래의 별도 스크립트 디버깅 요청에만 사용한다.

## 수집 서버 주소를 빌드에 넣기

현재 데모 수집 서버의 공개 base URL은 **`https://deviant-log.verte.kr`**이다. Web과 바이너리 빌드에 같은 주소를 사용한다. `/v1`은 클라이언트가 붙이므로 설정값에 포함하지 않는다.

Web과 바이너리 모두 다음 TextAsset의 값을 빌드에 포함한다.

`unity/Assets/Deviant/Replay/Unity/Resources/ReplayCollector.json`

```json
{"base_url":"https://deviant-log.verte.kr"}
```

빈 문자열이면 서버 전송 옵션이 없는 로컬 전용 빌드다. 운영은 HTTPS, 개발은 loopback HTTP만 허용한다. 이 파일에는 관리자 토큰을 넣지 않는다. 새 저장 영역에서는 기록이 기본 활성화되고 이 주소로 자동 전송한다. 별도의 서버 전송 동의는 없다. 기존 PlayerPrefs에 명시적으로 기록을 중단한 값이 있으면 다음 실행에도 유지한다.

Web은 페이지의 정확한 origin을 서버 `CORS_ORIGINS`에 등록한다. 바이너리는 브라우저 CORS의 대상이 아니다. 게임 빌드와 Go 서버/DB/Docker 볼륨은 별개 산출물이다. 주소를 바꿨으면 게임을 다시 빌드한다. 시험용 주소를 잠깐 사용했다면 빌드 후 소스 설정은 원래 값으로 돌리고, 이미 만든 산출물에는 시험 주소가 남는다는 점을 구분한다.

## Windows x64 바이너리

Editor 메뉴는 **Tools > deviant > Game > Build Windows**다. **Tools > Team Playtest > Build Windows**도 같은 메서드를 호출한다. 기본 출력은 Unity 프로젝트의 `Builds/Windows/deviant/deviant.exe`이며 `TEAM_PLAYTEST_OUTPUT`에 절대 디렉터리를 지정해 바꿀 수 있다. 권장 출력은 저장소의 `outputs/builds/windows`다.

디버깅용은 **Tools > deviant > Game > Build Windows With Debugging** 또는 batch의 `-executeMethod Deviant.Game.Editor.GameBuilder.BuildWindowsDebug`를 사용한다. 공통 네 씬과 미디어 준비 절차를 사용하고, `Development Build`와 `Script Debugging`(`BuildOptions.Development | BuildOptions.AllowDebugging`)을 켜며 해당 빌드에만 `DEVIANT_DEBUG_MENU`를 추가한다. 기본 출력은 저장소의 `outputs/builds/windows-debug/deviant.exe`이고 `DEVIANT_DEBUG_BUILD_OUTPUT`에 절대 디렉터리를 지정해 변경한다. 실행 시 치트 모드는 Ctrl+백틱, 전투 시험 패널은 F1로 연다.

### Linux/macOS 쉘에서 batch 호출

해당 호스트의 Editor가 Windows 대상 모듈과 프로젝트 backend를 지원하는지 먼저 확인한다. 로그의 실제 대상은 Windows x64여야 한다. 아래 경로 변수는 자기 환경에 맞게 지정한다.

```sh
BUILD_REPO='/absolute/path/to/repository'
UNITY_EDITOR='/absolute/path/to/6000.6.5f1/Editor/Unity'
mkdir -p "$BUILD_REPO/outputs/builds"
TEAM_PLAYTEST_OUTPUT="$BUILD_REPO/outputs/builds/windows" \
  "$UNITY_EDITOR" -batchmode -nographics \
  -projectPath "$BUILD_REPO/unity" -buildTarget Win64 \
  -executeMethod Deviant.Game.Editor.GameBuilder.BuildWindows -quit \
  -logFile "$BUILD_REPO/outputs/builds/windows-build.log"
```

macOS에서는 변수에 Editor 앱의 실제 실행 파일(`Unity.app/Contents/MacOS/Unity`)을 지정한다. 호스트/모듈/backend 조합이 지원되지 않으면 지원되는 Windows 환경에서 빌드하며 다른 Unity 버전으로 우회하지 않는다.

### Windows PowerShell에서 batch 호출

```powershell
$buildRepo = 'C:\src\F109'
$unityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.6.5f1\Editor\Unity.exe'
New-Item -ItemType Directory -Force "$buildRepo\outputs\builds" | Out-Null
$previousBuildOutput = [Environment]::GetEnvironmentVariable('TEAM_PLAYTEST_OUTPUT', 'Process')
$env:TEAM_PLAYTEST_OUTPUT = "$buildRepo\outputs\builds\windows"
try {
    & $unityEditor -batchmode -nographics `
      -projectPath "$buildRepo\unity" -buildTarget Win64 `
      -executeMethod Deviant.Game.Editor.GameBuilder.BuildWindows -quit `
      -logFile "$buildRepo\outputs\builds\windows-build.log" | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Windows build failed. Check windows-build.log.' }
} finally {
    if ($null -eq $previousBuildOutput) {
        Remove-Item Env:TEAM_PLAYTEST_OUTPUT -ErrorAction SilentlyContinue
    } else {
        $env:TEAM_PLAYTEST_OUTPUT = $previousBuildOutput
    }
}
```

예시는 종료 시 기존 출력 환경 변수 값을 복원한다. `Out-Host` 파이프라인으로 Unity 프로세스가 끝날 때까지 기다린 뒤 종료 코드를 확인한다.

### 실행·배포 단위

생성된 `deviant.exe`를 Windows x64에서 실행한다. 실행 파일 하나만 전달하지 말고 출력 디렉터리 전체를 함께 배포한다. `deviant_Data`, `UnityPlayer.dll`, 생성된 runtime 폴더/라이브러리 등은 실제 산출물 그대로 유지한다. 포함 파일은 backend/Unity 설정에 따라 달라질 수 있다. 동반 파일을 임의로 추려내지 않는다.

타이틀 시작, 마을/런/귀환, 입력/가방/지도/사망·결과와 자동 기록·업로드 및 중단/재개를 실제 Windows Player에서 검사한다. Editor 성공이나 다른 OS의 패키징 성공을 Windows 실행 성공으로 취급하지 않는다. 플랫폼의 게임 저장·리플레이 큐는 빌드 출력과 별개인 `Application.persistentDataPath` 아래에 있으므로 출력 폴더 교체만으로 이전 프로필/동의가 초기화되지는 않는다.

## 성공 확인과 문제 분리

batch 프로세스의 종료 코드 0, 로그의 성공 결과, 예상 출력 파일을 함께 확인한다. 생성 중인 디렉터리나 이전 빌드 파일의 존재만으로 성공 처리하지 않는다. 실패하면 로그의 마지막 요약보다 먼저 나타난 C#·에셋·링커 오류를 찾는다.

- 에셋 누락: 고정 SHA/LFS/배치 계약과 대상 `.meta`를 확인한다. `ValidateAssets` 우회나 대체 이미지를 넣지 않는다.
- 메서드를 찾을 수 없음: 먼저 C# 컴파일 오류와 namespace/진입점 이름을 확인한다.
- 대상 플랫폼 미지원: 같은 Editor 버전의 대상 모듈과 backend/호스트 지원을 확인한다.
- 프로젝트 사용 중: 같은 프로젝트를 연 Editor를 닫는다. 다른 Editor나 플레이 중인 배포물을 무조건 종료하지 않는다.
- 서버 수신 없음: 빌드에 포함한 주소·기록 설정·큐·네트워크·Web origin과 서버 응답을 확인한다. 관리자 토큰을 클라이언트에 추가하지 않는다.

`Deviant.Replay.Editor.ReplayBuildCheck.BuildSmoke`는 임시 빈 씬을 사용하는 **최소 Linux 패키징 검사**다. 제품의 Linux 빌드 메뉴나 전체 게임 검증이 아니다. 별도로 `GameBuilder.BuildLinuxCheatMode` / **Tools > deviant > Game > Build Linux With Cheat Mode**는 `DEVIANT_DEBUG_MENU`만 추가하고 `BuildOptions.None`을 사용하는 치트 모드 Linux 빌드다. `DEVIANT_CHEAT_BUILD_OUTPUT`(기본 `outputs/builds/linux-cheat`) 아래 `deviant.x86_64`를 만들며, 공통 4개 씬과 `RoseBlueBoss` 시험 씬을 포함한다. 치트 모드 포함 여부와 Development Build 여부를 구별한다. macOS 제품 빌드 진입점은 제공하지 않는다. 자세한 검사 범위는 [테스트 안내](../testing/testing.md)를 따른다.
