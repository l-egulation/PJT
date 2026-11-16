# Web 빌드와 로컬 실행

[분야 개요](README.md)

Unity **6000.6.5f1**의 Web Build Support가 필요하다. 다른 Editor 버전으로 대체하지 않는다. 모듈 설치·고정 리소스 복원·공통 씬·서버 주소 설정은 [게임 빌드 공통 준비](build-and-distribution.md)를 따른다. 같은 프로젝트를 연 Editor가 없는 상태에서 batch 빌드한다.

## 데스크톱 DXT 단일 배포

웹 배포는 **DXT 단일 빌드**를 사용한다. 불투명 이미지는 DXT1(BC1), 알파 이미지는 DXT5(BC3)로 가져온다. 모바일·ASTC는 지원 대상에 포함하지 않는다. 이미지는 원본 PNG를 사용하며 WebP 중간 압축을 적용하지 않는다. GPU 압축과 Opus 128kbps 로딩은 [미디어 압축](media-compression.md)을 따른다.

아래 `GameBuilder.BuildWeb`을 사용하고 `DEVIANT_WEB_TEXTURE_FORMAT`은 지정하지 않거나 `dxt`로 설정한다. 출력은 `index.html`이 있는 완전한 단일 빌드이며, 배포 준비에는 이 디렉터리 하나를 전달한다.

```sh
python3 tools/web/prepare_cloudflare_pages.py \
  --build outputs/builds/web \
  --output outputs/deploy/cloudflare \
  --assets-base-url https://deviant-assets.verte.kr
```

이 경로는 GPU 형식 선택 스크립트나 다른 형식의 data 파일을 포함하지 않는다. 기존 페이지 주소와 StreamingAssets 경로는 유지한다. 준비 도구는 로컬 배포 사본과 업로드 manifest만 만들며 R2·Pages 업로드는 아래 절차로 수행한다.

BC7 비교가 필요할 때만 `DEVIANT_WEB_TEXTURE_FORMAT=bc7` 또는 `GameBuilder.BuildWebDual`을 명시적으로 사용한다. 이중 빌드에는 `bc7/`, `dxt/`가 생기며 `--build <bc7> --dxt-build <dxt>`로 호환 형식 선택을 준비할 수 있다. 비교 도구가 있어도 기본 웹 배포는 DXT 하나다. Windows 바이너리의 가져오기 설정은 별도로 관리한다.

## Linux headless 환경의 Web 모듈 설치

Unity Hub의 Add modules를 사용할 수 있으면 해당 경로를 우선한다. GUI가 없는 Linux에서는 Editor를 닫고, **6000.6.5f1 / revision `3ff58d469c8a`**의 공식 모듈 압축을 기존 설치에 추가할 수 있다. 아래는 그 버전에 한정된 경로이며 다른 버전의 지원 파일을 섞지 않는다. `WebGLSupport`가 이미 설치돼 있으면 이 단계를 생략한다. 설치 디렉터리 쓰기 권한이 필요하다.

```sh
BUILD_REPO='/absolute/path/to/repository'
UNITY_INSTALL='/absolute/path/to/6000.6.5f1'
mkdir -p "$BUILD_REPO/outputs/toolchains/unity-web"
curl --fail --location --retry 2 \
  --output "$BUILD_REPO/outputs/toolchains/unity-web/WebGL-6000.6.5f1.tar.xz" \
  'https://download.unity3d.com/download_unity/3ff58d469c8a/LinuxEditorTargetInstaller/UnitySetup-WebGL-Support-for-Editor-6000.6.5f1.tar.xz'
# 다운로드 성공을 확인한 뒤 해당 모듈 디렉터리만 풀어 넣는다.
tar -xJf "$BUILD_REPO/outputs/toolchains/unity-web/WebGL-6000.6.5f1.tar.xz" \
  -C "$UNITY_INSTALL" ./Editor/Data/PlaybackEngines/WebGLSupport
```

`$UNITY_INSTALL/Editor/Data/PlaybackEngines/WebGLSupport`가 생겼는지 확인한다. 모듈과 로컬 다운로드 캐시는 게임 저장소의 커밋 대상이 아니다.

## 게임 빌드

`Tools > deviant > Game > Build Web`은 현재 타이틀 → 시작 마을 → 1챕터 → 2챕터 씬을 빌드한다. 기본 출력은 저장소의 Git 제외 `outputs/builds/web`, `DEVIANT_WEB_OUTPUT`으로 바꿀 수 있다.

```sh
BUILD_REPO='/absolute/path/to/repository'
UNITY_EDITOR='/absolute/path/to/6000.6.5f1/Editor/Unity'
mkdir -p "$BUILD_REPO/outputs/builds"
DEVIANT_WEB_OUTPUT="$BUILD_REPO/outputs/builds/web" \
  "$UNITY_EDITOR" -batchmode -nographics \
  -projectPath "$BUILD_REPO/unity" -buildTarget WebGL \
  -executeMethod Deviant.Game.Editor.GameBuilder.BuildWeb -quit \
  -logFile "$BUILD_REPO/outputs/builds/web-build.log"
```

Windows PowerShell에서는 다음처럼 실행한다.

```powershell
$buildRepo = 'C:\src\F109'
$unityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.6.5f1\Editor\Unity.exe'
New-Item -ItemType Directory -Force "$buildRepo\outputs\builds" | Out-Null
$previousWebOutput = [Environment]::GetEnvironmentVariable('DEVIANT_WEB_OUTPUT', 'Process')
$env:DEVIANT_WEB_OUTPUT = "$buildRepo\outputs\builds\web"
try {
    & $unityEditor -batchmode -nographics `
      -projectPath "$buildRepo\unity" -buildTarget WebGL `
      -executeMethod Deviant.Game.Editor.GameBuilder.BuildWeb -quit `
      -logFile "$buildRepo\outputs\builds\web-build.log" | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Web build failed. Check web-build.log.' }
} finally {
    if ($null -eq $previousWebOutput) {
        Remove-Item Env:DEVIANT_WEB_OUTPUT -ErrorAction SilentlyContinue
    } else {
        $env:DEVIANT_WEB_OUTPUT = $previousWebOutput
    }
}
```

종료 코드 0과 로그의 성공 결과를 확인한다. `BuildWeb`은 Web Player 설정을 프로젝트에 적용하므로 `ProjectSettings.asset`의 의도한 diff도 확인한다. 초기 가져오기·IL2CPP/WebAssembly 변환·링크·압축은 첫 빌드에서 시간이 걸릴 수 있다.

오프닝 조력자는 `Hub/opening/ink-helper/{appear,idle,leap}/00`부터 시작하는 320×200px PNG 43장을 사용한다. 8,960px 폭의 원본 시트는 `resources/`에 보존하고 Unity에는 배치하지 않는다. headless 가져오기에서는 지원 폭이 8,192px인 환경이 있어 `maxTextureSize`만 늘려도 시트를 가져오지 못할 수 있다. 빌드 전 `ValidateAssets`가 실제 오프닝 데이터가 가리키는 43개 Sprite의 로딩·크기·발 피벗·PPU·Read/Write를 검사하며, 잘못된 프레임이면 빌드를 중단한다. `ForestCombatV2.Tests.OpeningHelperArtTests`는 프레임 연결과 픽셀 가시성도 검사한다.

현재 빌드 설정은 WebGL 2, 초기 메모리 256MiB/최대 2GiB와 확장, gzip+decompression fallback, 데이터 캐시다. C# 스레드를 요구하는 옵션을 켜지 않는다. fallback으로 단순 정적 서버에서도 실행할 수 있지만 실제 서비스에서는 HTTPS·올바른 MIME/압축 헤더와 캐시 정책을 함께 설정한다. 빌드 파일을 `file://`로 직접 열지 않는다.

Web 힙은 한 번 늘면 줄지 않고 최대 2GiB에서 확장에 실패하면 실행이 중단(OOM)되며 같은 탭에서 복구할 수 없다. 메모리를 키우는 작업은 다음 기준을 지킨다. 전투 그림은 장착한 무기만 읽고, 보스 그림은 보스 전투장 근처에서 읽으며, 다른 챕터 장면은 쓰지 않는 몬스터 카탈로그를 직렬화 참조로 물지 않는다. 런타임 텍스처의 Read/Write는 실제로 픽셀을 읽는 파일에만 켠다([전투 표현](../presentation/visuals/combat-presentation.md)·[2챕터 보스](../gameplay/enemies/chapter2-rose-blue-boss.md)). 알려진 제한: 1·2챕터 몬스터와 공용 GUI 그림, 장착했던 무기 그림은 정적 캐시나 장면이 살아 있는 동안 남는다. 실제 Web 힙 최고점은 Web 빌드에서 측정해야 하며 에디터 측정값(Windows 텍스처 형식, 에디터가 붙잡는 자산 포함)과 1:1로 대응하지 않는다.

WebGL 페이지는 `Assets/WebGLTemplates/DeviantLoading/index.html` 커스텀 템플릿을 사용한다. Unity 로더의 진행률을 `다운로드 진행률`로 표시하고, 초기화 중에는 `네트워크에서 게임 데이터 다운로드 중`이라는 안내를 보여 준다. 브라우저의 Fetch/XHR 수신 바이트를 관찰해 `현재 다운로드 속도`를 0.25초 간격으로 갱신한다. 압축 응답·브라우저 캐시·서버의 `Content-Length` 공개 여부에 따라 속도와 남은 시간은 근사값이며, 캐시된 파일만 읽는 경우 속도 측정 문구가 유지될 수 있다.

```sh
# 저장소 루트에서 실행한다.
python3 -m http.server 18091 --bind 127.0.0.1 --directory outputs/builds/web
```

브라우저에서 `http://127.0.0.1:18091`로 접속한다. WebAssembly/WebGL 2 지원과 충분한 브라우저 메모리가 필요하다. 탭 종료/메모리 제한/백그라운드에서 마지막 기록 저장·전송을 보장하지 않는다.

## 기준 시각 이전 로컬 저장 초기화

Web Player의 고정 reset 기준은 `WebBuildStorageReset.ResetBeforeUtc`의 **2026-11-16 05:00:00 UTC (한국 시각 2026-11-16 14:00:00)**다. 브라우저 저장 영역의 `.deviant-web-reset-utc`에 기록한 적용 완료 기준 시각이 이보다 이전이면 초기화한다. 표식이 없거나 파싱할 수 없는 구버전 저장(기존 GUID 표식 포함)도 한 번 초기화한다. 기준 시각과 같거나 이후인 저장은 유지한다. 기준 시각은 빌드할 때 자동 갱신하지 않으며, 의도적으로 저장을 다시 초기화할 때만 코드의 날짜·시분초를 변경한다.

초기화는 `SubsystemRegistration`에서 프로필·설정·리플레이 호스트가 로드되기 전에 수행한다. 프로필·백업·스토리 진행·로컬 리플레이 큐를 포함한 `Application.persistentDataPath`의 모든 파일과 하위 폴더 및 PlayerPrefs 전체(음량·키 설정·기록 설정·수집 player ID 포함)를 삭제한다. 삭제와 설정 저장이 끝난 뒤 적용 완료한 기준 시각을 UTC로 기록한다. 기기 현재 시각이나 빌드 GUID·빌드 시각을 사용하지 않아 재빌드·재접속·기기 시계 변경으로 다시 초기화하지 않는다. 서버에 이미 전송한 데이터는 삭제하지 않는다.

일반/치트 모드와 Editor에서 직접 만든 Web 빌드 모두에 적용하며 Windows·Linux Player와 Editor 저장에는 적용하지 않는다. 다른 origin·경로의 접근할 수 없는 브라우저 저장 영역이나 Unity 다운로드 캐시는 삭제 대상이 아니다. IDBFS 자동 영속화 정책과 브라우저 종료 시 저장 제한은 그대로 적용한다.

`ForestCombatV2.Tests.WebBuildStorageResetTests`는 임시 폴더와 격리된 설정 콜백으로 구버전 전체 삭제·기준 1초 전/정각/1초 후 경계·재접속 유지·이전 기준 유지·동일 UTC/KST 시각·손상 표식·실패 후 재시도를 검사한다. 이 파일 IO 검사는 실제 Web Player의 IndexedDB 영속성 검사를 대신하지 않는다.

## 산출물과 정적 호스팅

배포 단위는 출력 디렉터리 전체다. `index.html`, `Build/`의 loader·framework·wasm·data 파일, `TemplateData/`, 생성된 `StreamingAssets/` 등 실제 산출물을 상대 경로 그대로 함께 올린다. 파일명이나 확장자를 임의 변경하지 않는다. 수집 서버의 Go 바이너리/DB/관리자 토큰을 이 정적 디렉터리에 넣지 않는다.

현재 gzip+fallback 출력의 압축 payload는 `.unityweb` 확장자를 사용한다. 직접 압축 응답을 제공하는 호스트는 실제 gzip 바이트에 `Content-Encoding: gzip`과 원래 내용의 MIME을 맞춘다(예: wasm은 `application/wasm`, framework JS는 `application/javascript`). 헤더 없이 제공할 때는 loader fallback이 해제하므로 시험용 Python 서버로도 실행할 수 있다. 일반 `web.loader.js`나 압축하지 않은 HTML에 gzip 헤더를 붙이지 않는다. 확장자만 보고 다른 압축 방식을 선언하거나 파일을 이중 압축하지 않는다.

배포 시 index/loader/config와 나머지 Build 파일을 같은 버전으로 교체하고 캐시를 확인한다. 현재 빌드는 고정 파일명을 사용하므로 과거 index와 새 data/wasm이 섞이지 않도록 한다. 해시 파일명·CDN 무효화·실제 호스트의 HTTPS/MIME/CORS는 별도 배포 환경에서 확인한다.

## 전체화면과 Escape

`Assets/Deviant/Game/Plugins/BrowserFullscreen.jspre`는 Web Player 초기화 시 Fullscreen API의 전체화면 변경을 관찰한다. 게임 canvas 또는 이를 포함하는 요소가 전체화면이고 Keyboard Lock API를 지원하면 `Escape` 하나만 잠근다. 허용된 환경에서는 짧은 ESC가 기존 게임의 Pause/Cancel 입력으로 전달되고, Chrome의 약 2초 길게 누르기 전체화면 종료는 유지된다. 전체화면을 벗어나면 잠금을 해제하고, 뒤늦게 권한 요청이 완료되는 경우도 해제한다.

커스텀 템플릿의 오른쪽 아래 전체화면 버튼은 아이콘과 「전체화면」 텍스트를 표시하며 최소 너비 128px·높이 48px를 사용한다. 버튼은 canvas를 포함하는 컨테이너를 Fullscreen API로 전환한다. 전체화면에서는 canvas의 게임 화면 비율을 유지하며 뷰포트에 맞게 키우고, 종료하면 고정 크기로 되돌린다. 전체화면 동안 버튼을 숨기고 포커스를 해제해 Space 입력이 버튼을 누르지 않도록 한다.

브라우저 권한이 거부되거나 API가 없으면 기존 전체화면 동작을 유지한다. 데스크톱 Chrome 130 이상에서는 Keyboard Lock 권한 허용이 필요하며, F11 같은 브라우저 자체 전체화면에는 이 API를 적용할 수 없다. 모든 브라우저에서 ESC 종료를 차단한다고 보장하지 않는다. 기존 HUD Pause 버튼도 사용할 수 있다. [Chrome 전체화면 안내](https://developer.chrome.com/blog/better-full-screen-mode), [Keyboard Lock 조건](https://developer.chrome.com/docs/capabilities/web-apis/keyboard-lock)

`node tools/web/test_browser_fullscreen.cjs`는 Escape 한정 요청·게임 canvas 범위·전체화면 종료/비동기 권한 응답의 해제·권한 거부/미지원 fallback을 검사한다. 이 검사는 JS 계약 검사이며 실제 브라우저의 권한 창·단일/긴 ESC 입력 검증을 대신하지 않는다. 실제 확인은 Web Player 빌드에서 수행한다.

## Cloudflare Pages + R2 배포

게임의 배포 도메인은 **`https://deviant.verte.kr`**, Pages 프로젝트는 **`deviant`**(production branch `main`), 기본 Pages 주소는 `https://deviant-6xh.pages.dev`다.

Pages의 정적 파일 제한은 **파일 하나당 25MiB**다. 현재 전체 게임의 압축 data 파일은 이 제한을 넘으므로 R2의 `deviant-assets` 버킷에 저장한다. Pages에는 그 data URL을 참조하는 HTML과 나머지 빌드 파일을 배포한다. Unity 빌드는 로컬/별도 빌드 환경에서 수행하고 Pages에는 완성된 파일을 Direct Upload한다. [Pages 제한](https://developers.cloudflare.com/pages/platform/limits/), [Direct Upload](https://developers.cloudflare.com/pages/get-started/direct-upload/)

현재 버킷의 공개 base URL은 **`https://deviant-assets.verte.kr`**이다. 이 주소는 `deviant-assets` 버킷에 직접 연결한 R2 커스텀 도메인이며 최소 TLS는 1.2다. 신규 배포는 이 주소를 사용하고 기존 릴리스의 object key는 보존한다. 개발용 `r2.dev` 주소는 요청 제한이 있고 CDN 캐시를 지원하지 않으므로 운영 데이터 URL로 사용하지 않는다. [R2 공개 주소](https://developers.cloudflare.com/r2/buckets/public-buckets/)

도메인 연결과 CDN 캐시 규칙 적용은 별도 단계다. [캐시 규칙 원본](../../../tools/web/cloudflare-r2-cache-rule.json)은 `deviant-assets.verte.kr`의 `/releases/` 아래 `.unityweb`에 대한 GET/HEAD만 캐시 대상으로 지정한다. 엣지 TTL은 7일, 브라우저 TTL은 원본의 `public, max-age=31536000, immutable`을 존중하며 4xx/5xx는 저장하지 않는다. HTML·수집 API와 다른 호스트는 이 규칙의 대상이 아니다.

대시보드에서 직접 적용하려면 `verte.kr` → **Caching → Cache Rules → Create rule**로 이동한다. 이름은 `Deviant Unity assets`, 조건은 Custom filter expression의 Edit expression에 아래 내용을 붙여 넣는다. [대시보드 공식 안내](https://developers.cloudflare.com/cache/how-to/cache-rules/create-dashboard/)

```text
(http.host eq "deviant-assets.verte.kr"
 and starts_with(http.request.uri.path, "/releases/")
 and ends_with(http.request.uri.path, ".unityweb")
 and http.request.method in {"GET" "HEAD"})
```

Cache eligibility는 **Eligible for cache**, Edge TTL은 **Ignore cache-control header and use this TTL / 7 days**, Browser TTL은 **Respect origin TTL**로 설정한다. 상태 코드별 TTL에서 400~599 응답은 **No store**로 설정한 뒤 Deploy한다. 다른 호스트의 기존 규칙은 유지한다. 규칙 JSON을 준비한 것과 실제 원격 적용을 구분하고, 적용 뒤에는 동일 URL의 응답에서 `CF-Cache-Status: HIT`·`Age`를 확인한다. [캐시 설정 항목](https://developers.cloudflare.com/cache/how-to/cache-rules/settings/)

Wrangler 인증은 실행 셸에 준비한다. `CLOUDFLARE_API_TOKEN`을 `.zshrc` 등에 설정했다면 해당 셸 설정을 다시 읽은 터미널에서 실행한다. 토큰을 명령 인자·정적 파일·업로드 manifest·Git에 넣지 않는다.

공개 게임 에셋용 [CORS 설정](../../../tools/web/cloudflare-r2-cors.json)은 모든 origin에 `GET`·`HEAD`만 허용하고 gzip·캐시 확인에 필요한 응답 헤더를 노출한다. 이 정책은 R2의 공개 파일 읽기용이며, 리플레이 수집 서버의 `CORS_ORIGINS`와는 별개다. [R2 CORS](https://developers.cloudflare.com/r2/buckets/cors/)

```sh
npx wrangler whoami
npx wrangler r2 bucket cors set deviant-assets --file tools/web/cloudflare-r2-cors.json
npx wrangler r2 bucket domain get deviant-assets --domain deviant-assets.verte.kr
python3 tools/web/prepare_cloudflare_pages.py \
  --build outputs/builds/web \
  --output outputs/deploy/cloudflare \
  --assets-base-url https://deviant-assets.verte.kr
```

준비 도구는 원본 빌드를 보존하고 별도의 빈 출력 디렉터리에 `pages/`와 `r2-upload.json`을 만든다. `Build/*.data.unityweb`은 Pages 사본에서 제외하고 HTML의 `dataUrl`을 `releases/{압축 data의 SHA256}/{파일명}`으로 바꾼다. 나머지 파일 중 25MiB를 넘는 파일이 있으면 배포 준비를 거부한다. wasm/framework의 gzip MIME·압축 헤더와 HTML 재검증 정책은 `pages/_headers`에 포함한다. 원본 HTML은 Unity 기본 템플릿의 `dataUrl: buildUrl + "/파일명"` 형태여야 한다.

manifest가 지정한 실제 압축 파일을 **원격** R2에 먼저 업로드한다. gzip payload에 맞는 `Content-Encoding`과 content hash 경로의 immutable 캐시를 설정한다. 같은 key의 내용을 다른 빌드로 덮어쓰지 않는다.

```sh
python3 - <<'PY'
import json
from pathlib import Path
import subprocess

manifest = json.loads(Path('outputs/deploy/cloudflare/r2-upload.json').read_text())
for m in manifest.get('variants', [manifest]):
    subprocess.run([
        'npx', 'wrangler', 'r2', 'object', 'put', 'deviant-assets/' + m['object_key'],
        '--file', m['data_file'], '--remote',
        '--content-type', m['content_type'], '--content-encoding', m['content_encoding'],
        '--cache-control', m['cache_control'],
    ], check=True)
PY
```

공개 data URL의 상태·CORS·gzip 헤더와 다운로드 SHA256을 확인한 뒤, **`pages/`만** Pages에 올린다. `r2-upload.json`과 원본의 대용량 data를 Pages에 넣지 않는다. 대상 Pages 프로젝트명은 실제 생성한 프로젝트로 지정한다.

```sh
npx wrangler pages deploy outputs/deploy/cloudflare/pages \
  --project-name deviant --branch main
```

커스텀 도메인은 먼저 Pages 프로젝트에 `deviant.verte.kr`을 등록하고, `verte.kr`의 DNS에 프록시를 켠 CNAME `deviant` → `deviant-6xh.pages.dev`를 연결한다. Pages 배포 권한과 별개로 해당 zone의 DNS 편집 권한이 필요하다. Pages의 도메인 상태·인증서가 active인지와 실제 HTTPS 응답을 함께 확인한다. [Pages 커스텀 도메인](https://developers.cloudflare.com/pages/configuration/custom-domains/)

Pages origin은 수집 서버에 별도로 허용해야 한다. HTTP 다운로드·헤더 확인은 실제 배포 페이지의 게임 실행·브라우저 저장·업로드 검증을 대신하지 않는다.

## 영구 진행의 브라우저 저장

마법안료·해금·보스 클리어/런 집계·본 이야기 등의 영구 진행은 `Chapter1ProfileHost`와 `FileProfileStore`가 `Application.persistentDataPath/profile/profile.json`에 저장하고 다음 실행에 검증/로드한다. 구매·해금 등은 변경을 파일로 저장한 뒤 현재 상태에 반영한다. 처치로 얻은 마법안료는 일반적으로 2초마다 묶어 저장하고, 포커스 상실·앱 일시정지·종료 콜백에서도 남은 값을 저장한다. 런 중 골드·장비·진행 위치의 중간 저장은 추가하지 않는다.

`Assets/Deviant/Game/Plugins/BrowserPersistence.jspre`는 Web 런타임의 preInit에서 `autoSyncPersistentDataPath`를 켠다. `/idbfs`가 마운트되기 전에 적용되므로 Unity의 autoPersist 큐가 파일 쓰기/교체/삭제를 IndexedDB로 비동기 반영한다. 리플레이 기록의 활성화나 수집 서버 연결 여부와 독립적으로 동작한다. Replay의 수동 FS 동기화는 이 자동 모드와 중복 실행하지 않으며, 자동 모드가 없는 환경의 기존 fallback은 유지한다.

진행은 해당 브라우저의 사이트 저장 영역에만 있다. 브라우저/프로필·origin/호스팅 경로가 바뀌거나 사이트 데이터를 지우면 다른/새 저장 영역이 될 수 있다. 비공개 모드·저장 차단·브라우저 quota와 마지막 쓰기 직후 강제 종료에서는 지속성을 보장하지 않는다. 자동 저장 완료 전에 탭을 즉시 닫는 상황의 마지막 변경까지 보장하는 동기 서버 세이브는 아니다. 수집 서버의 리플레이/사건은 클라우드 세이브나 다른 기기 복구용 프로필로 사용하지 않는다.

### 격리 Web 저장 검사

실제 프로필 코드·진행 데이터와 원본 Packages/ProjectSettings를 복사한 별도 프로젝트를 준비한다. 고정 Unity **6000.6.5f1**을 사용하며 게임 미술/제품 씬을 재구현하지 않는다.

```sh
python3 tools/web/prepare_profile_storage_test.py
'<Unity 6000.6.5f1>/Editor/Unity' -batchmode -nographics \
  -projectPath '<저장소>/outputs/profile-storage/project' -buildTarget WebGL \
  -executeMethod ProfilePersistenceBuild.Build -quit \
  -logFile '<저장소>/outputs/profile-storage/build.log'
python3 -m http.server 18094 --bind 127.0.0.1 --directory outputs/profile-storage/web
```

`http://127.0.0.1:18094`에서 합성 재화/해금 저장 버튼을 누른 뒤 동일 페이지 새로고침 및 새 탭 재접속에서 profile ID·잔액·해금·이야기/런 집계를 비교한다. 일반 처치 1회의 2초 자동 저장도 확인한다. 격리 fixture origin/경로에서만 실행한다. 기준 시각 초기화 코드를 함께 사용하므로 미적용 저장 영역 전체를 한 번 지우며, 합성 프로필은 `browser-storage-fixture/profile.json`에 쓴다. 같은 기준 시각으로 재빌드한 fixture를 같은 URL에 배포해도 합성 재화·해금·profile ID가 유지되는지 확인한다. 의도적으로 기준 시각을 나중으로 바꾼 fixture에서는 한 번 초기화되고 그 뒤 재접속은 저장을 유지하는지도 확인한다. 실제 게임 배포 주소에서 이 fixture를 실행하지 않는다. 브라우저의 `window.__deviantProfilePersistenceProbe`는 이 fixture에만 있는 읽기용 관측값이다. `pending=false`, `persisting=false`와 IndexedDB의 실제 프로필 값이 반영된 것을 확인한 뒤 재접속한다. 저장 버튼 직후 비동기 반영 전에 즉시 새로고침하는 것은 완료된 저장의 재로드 검사와 구별한다. fixture 소스는 `tools/web/fixtures/`에 있고 제품 빌드에 포함하지 않는다. 이 검사는 브라우저 저장 검증이며 전체 게임/모든 브라우저/장시간 플레이를 대신하지 않는다.

## 리플레이 수집 시험

현재 데모 수집 서버는 **`https://deviant-log.verte.kr`**이다. 기본 `ReplayCollector.json`에 이 주소가 들어 있으며, 게임 Web 페이지의 origin은 수집 서버의 `CORS_ORIGINS`에 별도로 등록한다. CORS 허용값은 게임 페이지 주소이며 수집 서버 주소가 아니다.

[수집 서버](../../../backend/collector/README.md)를 별도 실행하고 빌드 전 `unity/Assets/Deviant/Replay/Unity/Resources/ReplayCollector.json`의 base_url을 지정한다. 개발용 loopback HTTP 외에는 HTTPS를 사용한다. 서버 CORS 목록은 Web 페이지의 정확한 origin과 맞춘다. 게임 빌드에 관리자 토큰을 넣지 않는다.

새 브라우저 저장 영역에서 기록과 서버 전송이 별도 조작 없이 자동 시작하는지 확인한다. 제품 화면에 기록 버튼·설정 창이 표시되지 않는지 확인한다. 일반 조각 회전 주기인 약 30초 이상 기록하거나 씬을 전환해 조각을 확정한 뒤, 관리자 API에서 session/chunk/주요 사건과 원본 SHA를 확인한다. 같은 페이지를 새로 열어 player ID는 유지되고 session ID는 바뀌는지도 검사한다.

오프라인 상태에서 조각을 확정한 뒤 새로고침하고 연결을 복구하면 원래 player/session의 대기 파일을 전송해야 한다. 기록 중단·로컬 삭제 후에는 대기 전송이 없어야 하며 새로고침해도 중단 상태를 유지해야 한다. IndexedDB 실제 저장과 브라우저 콘솔/서버 수신을 함께 검사한다. JS bridge stub이나 Editor의 성공만으로 Web Player 수락을 대신하지 않는다.

브라우저별 실물 입력·장시간 플레이·메모리/프레임·모바일 호환과 실제 배포 서버의 압축 헤더는 별도 범위다. 로컬 시험용 서버 주소를 넣은 빌드를 운영 배포물로 사용하지 않는다.
