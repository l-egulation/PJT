# 게임 미디어 압축과 로딩

[분야 개요](README.md)

게임 이미지는 배치 계약이 지정한 원본 PNG 바이트를 사용하고, GPU 압축과 음원 Opus 인코딩은 유지한다. 메인 설정 원본은 `data/assets/media-compression.json`이며 `images.source`는 `original`이다. `resources/authoring/compressed-game-media-q70`의 manifest는 음원 Opus·입력 스냅샷과 기존 이미지 출처를 연결한다. 보존된 WebP 파생본은 게임 이미지로 배치하지 않는다. 원본과 비교용 파생본은 삭제하지 않는다.

## 이미지

Unity에는 `unity-deployment.json`이 지정한 원본 PNG를 기존 목적지·GUID로 배치한다. WebP 변환이나 그 디코딩 PNG를 중간 단계로 사용하지 않는다. 기존 WebP 배치 사본은 인정된 해시를 확인한 뒤 원본으로 교체한다. 이후 선택 도구를 다시 실행해도 main이 새로 선택한 원본 리소스 버전을 보존한다. 신규 이미지도 배치 계약에서 GPU 가져오기 목록에 포함한다.

`CompressedTextureImporter`가 선택한 이미지의 기존 Sprite·PPU·피벗·Read/Write를 유지하면서 플랫폼 가져오기 설정을 적용한다.

- 기본 Web 배포: 불투명 DXT1(BC1), 알파 DXT5(BC3)의 단일 빌드.
- Standalone: 불투명 BC1(DXT1), 알파 BC7.
- Web BC7·이중 형식 생성은 명시적으로 선택하는 비교용 도구다.
- 모바일 빌드와 ASTC는 제공하지 않는다.
- 폭·높이가 4보다 작거나 4의 배수가 아닌 1,345개는 캔버스를 바꾸지 않도록 RGBA32를 유지한다. 압축 대상은 6,907개다.
- GPU 압축은 손실압축이며 원본과 알파가 완전히 일치한다고 보장하지 않는다. 실기기에서 경계·색·투명도 확인이 필요하다.

PNG를 담은 보스용 `.bytes` TextAsset은 빌드 중 `DVGPU001` 헤더·크기·형식·GPU 블록·PNG fallback으로 포장한다. `CompressedTextureData.LoadImage`가 지원 GPU에서는 블록을 업로드하고 다른 환경에서는 PNG를 사용한다. 일반 PNG 입력도 계속 읽는다. 빌드가 끝나면 로컬 원본 바이트를 복원한다.

## 음원

48kHz, 원래 모노/스테레오 채널, Opus 128,000bps CBR로 인코딩한다. Ogg 컨테이너는 bitexact 옵션으로 생성한다. WAV·MP3 원본과 기존 Editor AudioImporter는 보존한다.

플레이어에는 실제 사용하는 Opus를 `CompressedAudio/Payloads/<SHA256>.bytes`로 넣는다. 메인 소유 `CompressedAudio/Catalog.json`이 기존 Resources 경로와 선택 ID를 연결한다. GUID 기반 선택 음원, 승인 전투 효과음, 배경음, 타이틀 호출은 `CompressedAudioResources`를 통한다. `presentation:` ID는 PresentationSfx 전용이다.

순수 C# Concentus 2.2.2와 Ogg 파서가 첫 사용 때 Opus를 PCM으로 복원해 AudioClip을 만든다. 원래 pre-skip·마지막 granule의 패딩·헤더 gain을 반영한다. C# 스레드나 네이티브 플러그인을 요구하지 않는다. Concentus의 BSD 라이선스는 코드 옆과 빌드의 `StreamingAssets/ThirdPartyNotices`에 포함한다.

디코딩은 동기식이며 생성된 AudioClip은 세션 동안 재사용한다. 다운로드 압축이 재생 중 PCM 메모리까지 줄이는 것은 아니다. 스트리밍 디코딩·음원 캐시 상한은 구현 범위에 포함하지 않는다. 장시간 플레이의 메모리와 첫 BGM 디코딩 지연은 별도로 확인해야 한다.

## 빌드와 원본 보호

제품 `GameBuilder.BuildWeb`, `BuildWebDual`, `BuildWindows`와 개발용 `BuildWindowsDebug`, `BuildLinuxDebug`는 압축 프로파일 준비 후 `CompressedMediaBuild.BeginPackaging()` 범위에서 빌드한다. 두 GUID 기반 AudioClip 카탈로그의 원본 참조를 잠시 해제하고, 원본 Resources 음원을 Resources 밖으로 옮겨 Opus와 중복 포함되는 것을 막는다. 범위 종료 시 카탈로그·음원 경로·GUID·보스 TextAsset을 복원한다. 다른 임의의 BuildPipeline 진입점은 이 처리를 자동 적용하지 않는다.

빌드 전에 `Stage1RuntimeAssetValidation`이 1스테이지 몬스터의 MonoScript 타입 연결, 모션·효과 카탈로그와 프리팹의 필수 컴포넌트를 검사한다. C# 컴파일 성공만으로 직렬화된 에셋 참조가 유효하다고 판단하지 않는다. 재가져오기 후에도 연결이 풀려 있으면 빌드를 중단한다. 안내에 따라 해당 Runtime 스크립트를 다시 가져오고 지정 Editor를 재시작한 뒤 검사한다.

Editor 강제 종료나 시스템 중단으로 복원이 끝나지 않았다면, 새 빌드를 시작하기 전에 **Tools > deviant > Media > Restore Packaging Sources**를 실행한다. batch에서는 다음 메서드를 실행한다.

```sh
'<Unity 6000.6.5f1>/Editor/Unity' -batchmode -nographics \
  -projectPath '<저장소>/unity' -buildTarget WebGL \
  -executeMethod Deviant.Media.Editor.CompressedMediaBuild.RestorePackagingSources -quit
```

복구 정보는 해당 프로젝트의 `Library/DeviantMediaPackaging/recovery.json`과 백업에 있다. 복구 전에 Library를 지우지 않는다. 복구 대상 위치에 다른 파일이 생겼으면 자동 덮어쓰기하지 않고 중단한다.

## 복원·재생성

일반 게임 준비는 [고정 리소스 배치](../../assets/README.md)만 실행한다. 이미 등록된 파생본을 다시 압축하지 않는다. 메인 바인딩을 재생성할 때는 Unity를 닫고 `python3 tools/assets/select_compressed_media.py`를 실행한다. 기존 GUID를 재사용하며 음원 payload `.meta`, 런타임 카탈로그, Editor 선택 목록과 배치 계약을 함께 갱신한다. 이미지 압축 목록은 현재 배치 계약의 선택을 따르며, 계약에서 제외한 이전 프레임은 보존 미디어 manifest에 남아 있어도 다시 포함하지 않는다.

보존·비교용 새 파생본을 제작하려면 리소스 작업 브랜치에서 명시한 recipe를 새 빈 출력 디렉터리로 내보낸다. Pillow의 WebP 지원과 ffmpeg/libopus, ffprobe가 필요하다.

```sh
python3 resources/tools/export_compressed_game_media.py \
  --recipe resources/authoring/compressed-game-media-q70/source/recipe.json \
  --destination outputs/compressed-media-new --workers 6
```

출력의 WebP·디코딩 PNG·Opus와 manifest를 리소스 저장소의 새 세트로 등록한다. 파일 해시·색인·LFS와 출처를 맞추고 리소스 커밋·push·독립 복원을 마친 뒤 메인이 음원 등 필요한 항목의 SHA를 선택한다. 게임 이미지는 원본을 유지한다. 배치 실행기 자체는 픽셀·오디오를 재인코딩하지 않는다.

보존한 50개 전투 생성 WAV와 Catalog는 현재 승인 출력의 스냅샷이다. 생성기가 전투 시간표와 승인 입력 불일치로 중단하면 압축 작업에서 시간표나 승인 해시를 임의로 갱신하지 않는다. 시간표 재승인은 별도 전투 제작 절차다.

Web BC7·DXT 명령과 R2/Pages 형식 선택은 [Web 빌드](web-build.md)를 따른다. 코덱·가져오기·빌드 성공과 실제 게임의 청취·장시간 OOM 검증은 서로 다른 범위다.

## 자동 검사

합성 테스트 음원은 Git 제외 출력에 생성한다. 게임 음원이나 Assets를 테스트 fixture로 덮어쓰지 않는다.

```sh
python3 tools/audio/prepare_opus_decoder_test.py --output outputs/media-test-fixtures
DEVIANT_MEDIA_FIXTURES="$PWD/outputs/media-test-fixtures" \
  '<Unity 6000.6.5f1>/Editor/Unity' -batchmode -nographics \
  -projectPath "$PWD/unity" -buildTarget WebGL -runTests -testPlatform EditMode \
  -assemblyNames Deviant.Media.Tests -testResults "$PWD/outputs/media-tests.xml" \
  -logFile "$PWD/outputs/media-tests.log"
python3 -m unittest discover -s tools/web -p test_prepare_cloudflare_pages.py -v
```

등록된 Resources payload와 선택 ID의 실제 디코딩·캐시 재사용을 검사한다. Mono/스테레오 디코딩을 ffmpeg의 PCM과 비교하고 pre-skip·끝 패딩·손상 Ogg 거부·AudioClip 생성, DXT5·BC7 픽셀 읽기와 보스 TextAsset 블록/fallback 로딩을 검사한다. fixture 환경 변수를 생략하면 음원 fixture 검사는 건너뛴다. 배포 준비 검사는 단일 형식 계약과 BC7 지원·미지원·macOS 경로의 loader/framework/wasm/data 선택·gzip 헤더·대용량 data 제외를 확인한다.
