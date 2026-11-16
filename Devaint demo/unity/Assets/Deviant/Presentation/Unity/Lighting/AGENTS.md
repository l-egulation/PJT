# 조명 에셋·발광 맵 제작과 Unity 연결

이 폴더의 조명 어댑터를 수정하거나 실제 아트를 연결할 때 따르는 작업 지침이다. 아래 경로·명령은 별도 표시가 없으면 **메인 저장소 루트 기준**이다. [루트 작업 기준](../../../../../../AGENTS.md), [에셋 제작 계약](../../../../../../docs/assets/presentation/lighting-props.md), [기술 설계](../../../../../../docs/development/presentation/visuals/lighting-and-reactive-props.md), [고정 리소스 배치](../../../../../../docs/assets/README.md)를 함께 따른다. 제품 규칙·수치 원본을 이 파일에 중복 정의하지 않는다.

## 1. 먼저 구분할 것

현재 구현은 `LightingLabComposition`의 합성 진단 에셋과 명시적 런타임 바인딩이다. 실제 랜턴·창문·캐릭터의 PNG를 폴더에 넣는 것만으로 자동 등록되거나 본 게임에 적용되지 않는다.

| 자료 | 용도 | 현재 연결 지점 |
|---|---|---|
| 컬러 Sprite | 소등 상태에도 보이는 본체·금속·창틀 | 대상 `SpriteRenderer.sprite` |
| 노말맵 | 광원 방향에 따른 표면 명암 | `_NormalMap` secondary texture + `SpriteLightingBinding`의 `_SurfaceNormal` 캐시 |
| 발광 마스크 | 유리·불꽃·전구 등 **그 자체가 밝게 보일 부분** | 전용 마스크 슬롯은 아직 없음. 아래의 별도 Unlit 발광 Sprite로 연결 |
| 점광원 투과 맵/쿠키 | **주변을 비추는 빛의 방출 패턴** | `LightEmitterView.transmissionMask` / `SetTransmissionMask(Sprite)` |
| 창문 개구부 | 프레임을 제외한 열린 영역의 평행 유입광 | `WindowApertureArt.openings` → `WindowLightView.Bind` |
| 그림자 마스크·윤곽 | 다른 광원을 가리는 불투명 부분 | 사전 제작 윤곽 → `SpriteShadowSource` / `SpriteLightingFrame` |

**발광 마스크, 투과 쿠키, 그림자 마스크는 서로 대체할 수 없다.** 특히 `LitInkOutline.shader`의 `_MaskTex`는 URP 2D 조명 블렌드용이지 발광 맵 슬롯이 아니다. 현재 셰이더에는 `_EmissionMap`·`_EmissionMask`나 그 자동 등록 API가 없다. 없는 슬롯·자동 임포터가 있다고 안내하거나 이름만 추가해 연결 완료로 표시하지 않는다.

## 2. 원본과 파생 맵 만들기

### 공통 정합

- 컬러 원본의 투명 캔버스·프레임 ID·rect·피벗·PPU를 보존한다. 새 맵 때문에 원본을 크롭·확대·재인코딩하거나 기존 GUID를 바꾸지 않는다.
- 컬러/노말/발광 마스크는 같은 프레임 좌표로 제작한다. 본체 알파나 이미 그려진 밝기에서 자동 추정한 결과를 승인된 마스크로 간주하지 않는다.
- 금속·프레임, 투명 유리, 발광부, 불투명 차폐를 각각 구분한다. 창문 십자 프레임·랜턴 금속 테두리까지 전부 빛나게 만들지 않는다. 기존 벽 조명·큰 후광을 본체에 굽지 않는다.
- 픽셀 아트의 새 출력은 Point·mipmap 없음·무압축을 기본으로 검토한다. 노말/흑백 마스크/쿠키는 색 그림이 아닌 데이터로 색공간을 설정한다. 노말은 실제 `UnpackNormal` 경로에 맞는 노말 텍스처로 준비하고 선·빗금을 그대로 요철로 바꾸지 않는다.
- 제작 원점·축·단위·피벗·소켓 좌표를 원본 메타데이터에 기록한다. 상단 원점 이미지 좌표를 Unity의 위쪽 +Y로 변환할 때 캔버스 높이와 피벗을 함께 반영한다. 단순히 Y 부호만 뒤집거나 투명 캔버스 중심을 소켓으로 가정하지 않는다. 자동 소켓 변환 도구는 아직 없다.

### A. 물체 자체의 발광 맵

1. 컬러와 같은 캔버스에 흑백 coverage 맵을 만든다. 흰색은 발광, 검정은 비발광, 회색은 부분 발광이다. 랜턴은 불꽃·전구/선택한 유리만, 창문은 실제 유리·개구부만 남기고 금속·창살은 검정으로 제외한다.
2. 이 흑백 맵을 **제작 원본 데이터**로 보존한다. 현재 런타임은 이 맵과 컬러를 자동 합성하지 않는다.
3. 현행 경로로 표시하려면 별도의 RGBA 발광 Sprite도 제작한다. RGB는 승인한 발광색/발광부 그림, 알파는 발광 coverage와 필요한 본체 실루엣의 교집합으로 만든다. 비발광부는 알파 0이다. 검은 배경이 불투명한 흑백 맵을 그대로 Sprite로 올리지 않는다.
4. 이 RGBA는 **컬러 출력**이므로 흑백 데이터 맵과 임포트 색공간을 구분한다. 본체와 같은 피벗·크기·프레임을 사용하고 소등 가능한 별도 Unlit `SpriteRenderer`에 연결한다. 본체 전체를 Unlit로 바꾸지 않는다.
5. 소등 때는 발광 렌더러와 실제 조명을 함께 끄되 본체는 남긴다. 깜빡임을 연동하려면 조립 코드가 같은 시뮬레이션 시각의 `ComputedIntensity`와 점등 상태를 읽어 표시를 명시적으로 갱신해야 한다. 현재 `LightEmitterView.Sample`이 발광 Sprite의 색·알파까지 자동 갱신하지는 않는다.

발광 Sprite만으로 주변이 밝아지거나 Bloom이 생기지는 않는다. 주변 조명은 별도 emitter가 담당한다. 단일 본체 셰이더의 발광 맵 합성을 새로 구현하려면 셰이더·바인딩·소등/깜빡임·모션·수명 검사를 함께 추가하고 이 제한을 갱신한다.

### B. 주변 빛을 제한하는 랜턴·횃불 투과 맵

1. 발광 coverage와 별개의 **정사각형 방출 패턴**을 만든다. URP 점광원 반경에 펼치는 패턴이지 하우징 그림에 붙이는 마스크가 아니다. 복잡한 프레임의 빈 영역·곡선·다각형을 흑백으로 그릴 수 있지만 프레임의 물리적 3D 투영을 자동 계산하지 않는다.
2. **RGB 흰색=투과, RGB 검정=차폐**로 저장한다. 회색은 부분 투과다. 알파는 불투명으로 준비하는 편이 명확하다. RGB가 흰색인 채 알파만 0으로 만든 영역은 점 쿠키의 검정 차폐를 대신하지 않는다.
3. Unity에서 Sprite/Single, 전체 텍스처 rect, 정사각형, Clamp, mipmap 없음, sRGB OFF인 데이터 텍스처로 준비한다. SpriteAtlas·크롭·패킹을 사용하지 않는다. 필터/압축도 실제 경계와 얇은 프레임을 검토한다.
4. [LightEmitterView.cs](LightEmitterView.cs)의 `transmissionMask`에 Sprite를 지정하거나, emitter를 `Bind`한 뒤 `SetTransmissionMask(cookie)`를 호출한다. null은 기존 원형 점광원으로 돌아간다.

점 쿠키는 **Sprite rect가 아닌 전체 텍스처 RGB**를 읽는다. `ValidateMask`는 패킹/크롭/비정사각/Repeat/mipmap을 거부하지만, 그림의 RGB 의미나 모든 임포트 설정까지 검사하지는 않는다. 실제 픽셀 검사가 필요하다. 창문 `WindowLightView`에 바인딩된 emitter에는 이 점광원 API를 호출하지 않는다.

## 3. 리소스 등록과 Unity 배치 순서

이미지·노말·마스크·편집 원본은 [resources 작업 기준](../../../../../../resources/AGENTS.md)의 소유다. 이 소스 폴더에 PNG 원본이나 미디어 내장 HTML을 새로 저장하지 않는다. 외부 공유 드라이브용 export·ZIP·업로드·다운로드 진입점을 만들지 않는다.

1. 메인과 `resources`의 브랜치·미커밋 상태를 확인하고 보존한다. 리소스를 수정할 때는 별도 작업 브랜치를 먼저 확인·선택한다. detached HEAD에서 제작 커밋을 만들거나 `--remote`로 최신 리소스를 임의 채택하지 않는다.
2. 대상 에셋의 `source/`에 편집 원본·마스크 의미·입출력 관계·소켓 정보를, 명시적으로 이름 붙인 파생 세트의 `runtime/`에 PNG 결과를 둔다. `preview/`는 필요한 제작 검토 자료다. 기존 승인 세트·출처 해시를 새 결과로 덮어쓰지 않는다. 구체적인 새 디렉터리 이름은 리소스 작업에서 정한다.
3. 대상 `asset.json.files`에 상대 파일 목록을 추가하고 전체 `catalog.json`의 에셋 ID·경로·fileCount를 맞춘다. 원본 해시·변환 설정·출처·검토 상태도 해당 세트의 메타데이터와 검증 목록에 남긴다. 이미지·바이너리는 LFS, 미디어 내장 HTML/JS/JSON은 `.lfs-payloads.json`과 LFS 속성까지 등록한다.
4. 리소스 검증 후 **리소스 저장소에서 먼저 커밋·push**하고 LFS 객체의 독립 복원을 확인한다. 그 뒤 메인에서 채택할 외부 SHA를 고정한다.
5. 메인의 [unity-deployment.json](../../../../../../docs/assets/manifests/unity-deployment.json)에 각 출력의 `target`, 실제 `sha256`, 실제 `bytes`, `source.asset`, `source.path`를 추가한다. 에셋 ID와 경로는 외부 색인에 존재해야 한다. 파일명만으로 다른 세트를 선택하지 않는다.
6. 새 Unity 목적지는 지정 버전 Editor에서 올바른 importer와 `.meta`·GUID를 먼저 준비한다. 기존 목적지의 `.meta`는 보존한다. 배치 실행기는 `.meta`를 만들거나 게임 바인딩·프리팹을 생성하지 않는다. 필요하면 `outputs/`의 격리 프로젝트에서 선택한 바이트로 importer를 저작한다.
7. 메인 index의 `resources` gitlink도 채택 SHA로 맞춘 뒤 고정 배치 실행기로 적용한다. 실행기는 index의 gitlink·외부 HEAD 일치와 깨끗한 외부 작업 상태를 검사하므로 내부 미커밋 파일을 둔 채 우회하지 않는다.
8. 메인에는 배치 계약·gitlink·Unity `.meta`/참조·조립 코드·프리팹·테스트를 기능 단위로 남긴다. 원본 파일을 메인에 이중 등록하거나 `git add resources`만으로 내부 파일까지 저장됐다고 간주하지 않는다.

다음 명령은 메인 루트에서 사용한다. 파일 제작·등록 작업이 끝난 뒤 해당 범위에 맞게 수행하며 실패하면 덮어쓰기·강제 초기화로 우회하지 않는다.

```sh
git status --short --branch
git -C resources status --short --branch
python3 resources/tools/configure_lfs.py
python3 resources/tools/verify_resources.py --check-links
git -C resources lfs fsck
python3 tools/assets/update_assets.py --check
python3 tools/assets/update_assets.py
```

LFS 속성 재생성이 필요하면 리소스 기준의 `configure_lfs.py --write` 절차를 사용한다. 원본 리소스 검증, 바이트 배치, Unity 참조 연결, 실제 게임/미술 검증은 서로 다른 완료 단계다.

## 4. 실제 코드에 연결하는 방법

### 랜턴·횃불: 본체·발광부·광원 소켓

- 공통 연결 예시는 [LightingLabComposition.cs](../../../CombatLab/Unity/LightingLabComposition.cs)의 `BuildLantern`, `BuildTorch`, `MakeFrame`, `BindShadow`다. 이 함수들의 도형 생성 코드를 실제 승인 아트로 오인하거나 합성 PNG를 메인에 저장하지 않는다.
- 천장 고정구, 흔들리는 본체, 본체 위 연결 소켓, 내부 빛 소켓을 구분한다. 광원은 빛 소켓에, 같은 캔버스의 발광 Sprite는 본체와 정합되는 별도 자식에 둔다. 전체 캔버스 발광 이미지를 임의의 빛 소켓 중심에 붙여 피벗을 어긋나게 하지 않는다.
- 아래는 **조립 코드에서 제공해야 하는 참조**를 사용한 연결 순서다. `lightSocket`, `cookie`, `emissionSprite`, `emissionRenderer`, 공유 `unlitMaterial`, `catalog`, `bindings`, `stableSeed`를 자동 검색해 주는 API는 없다.

```csharp
var emitter = UnityEngine.Object.Instantiate(bindings.lanternTemplate, lightSocket, false);
emitter.Bind(catalog.Emitter("lantern"), stableSeed);
emitter.SetTransmissionMask(cookie); // null이면 원형 점광원
emissionRenderer.sprite = emissionSprite;
emissionRenderer.sharedMaterial = unlitMaterial;
// 부모/로컬 자세, sorting, 점등·소등, 같은 시계의 밝기 갱신은 조립자가 연결한다.
```

- `Bind`는 템플릿의 Accurate 노말 품질·normalDistance가 정의와 일치하는지 검사한다. URP 공개 runtime setter가 없는 설정은 Editor에서 저작한다. reflection으로 바꾸지 않는다.
- 반응형 랜턴은 `ReactiveLanternBridge.Bind`에 공통 `CombatArena`, 진자 정의, swing Transform, 별도 접촉 크기, emitter, 선택적 연결부를 넘긴다. 점광원/발광 이미지 크기를 몸 접촉 콜라이더로 사용하지 않는다. 고정 횃불에는 진자 bridge를 강제하지 않는다.
- 사슬/줄 실제 부품은 `Create > deviant > Lighting > Hanging Connector Art`로 만든다. `chainFace`/`chainEdge`는 짝으로, `rope`는 선택적으로 지정하고 각 부품의 sprite-local `topSocket`/`bottomSocket`과 공유 재질을 연결한다. 연결부의 강체·별도 시계는 만들지 않는다.

### 창문: 발광 이미지와 평행 개구부를 따로 연결

1. `Create > deviant > Lighting > Window Aperture Art`에서 바인딩을 만든다. `openings[].vertices`는 **창문 앵커 로컬 Unity 좌표**의 열린 볼록 둘레다. CW/CCW 모두 허용하며 마지막에 시작점을 반복하지 않는다.
2. 십자 프레임은 네 개 열린 유리 영역으로 분리한다. 아치·곡선은 다각형 근사로, 오목 외곽/구멍은 여러 볼록 조각으로 나눈다. 조각/꼭짓점 상한은 [WindowAperture.cs](../../Runtime/WindowAperture.cs)를 따른다. PNG 알파에서 자동 추출하는 도구는 없다.
3. 템플릿 emitter를 먼저 `Bind`한 뒤 `WindowLightView.Bind(apertureArt, receivingRoom, catalog.WindowBeam, emitter, bindings.windowBeamShader, sortingLayerId, sortingOrder)`로 연결한다. `receivingRoom`도 같은 앵커 로컬 좌표의 유효 사각형이다.
4. 실제 차폐 소스는 `SetShadowCasters`로, 전용 셰이더를 사용하는 실제 표면 Sprite는 `AddReceiver`로 명시 등록한다. 장면의 모든 렌더러를 자동 검색하거나 공용 전투 재질 전체를 교체하지 않는다. 풀 반환 때는 `RemoveReceiver`를 호출한다.
5. 창문 자체의 밝은 유리 이미지는 앞 절의 별도 발광 Sprite다. `WindowApertureArt`를 만드는 것만으로 창문 그림/발광/배경벽의 구멍까지 생성되지 않는다. 벽 모듈의 개구부나 별도 렌더 마스킹도 조립해야 하며 이동 충돌과는 분리한다.

창문의 Native `Light2D.intensity == 0`은 의도한 상태다. 실제 기여는 수신체 셰이더가 계산하며 빛줄기를 꺼도 남아야 한다. 여러 창문은 `WindowLightReceiver`가 합성하므로 개별 창문이 수신체의 프로퍼티 블록 전체를 덮어쓰지 않는다. 세부 예산·수명·2D 투영 한계는 기술 설계를 따른다.

### 캐릭터·몬스터·빛을 받는 소품의 노말과 그림자

- [SpriteLightingBinding.cs](SpriteLightingBinding.cs)의 `SpriteLightingFrame`에 `Original`, `Lit`, `Flat`, 사전 제작 `ShadowVertices`/`ShadowEdges`를 구성한다. 현재 이 타입은 자동 수집 카탈로그나 Inspector용 ScriptableObject가 아니다. 파일명만으로 프레임이 등록되지 않는다.
- 세 Sprite의 rect·피벗·PPU를 일치시키고 `Lit`/`Flat` 모두 `_NormalMap` secondary texture를 연결한다. 노말 텍스처 크기도 컬러 텍스처와 같아야 한다. 평면 A/B용 Flat에도 명시적인 평면 노말을 둔다.
- 런타임 `Sprite.Create`를 쓰면 secondary texture도 생성 인자로 전달한다. 현재 바인딩이 노말 텍스처별 `_SurfaceNormal` 머티리얼을 캐시하므로 프레임마다 새 머티리얼을 만들거나 secondary texture만으로 모든 경로가 연결됐다고 가정하지 않는다.
- `bindings.shadowTemplate`을 인스턴스화해 `SpriteShadowSource`를 준비하고 `SpriteLightingBinding.Bind(renderer, litMaterial, frames, shadowSource)`를 호출한다. 정적 소품은 `SpriteShadowSource.Configure`로 원본 Sprite와 사전 윤곽을 연결할 수 있다. 템플릿의 공개 `CachedSpriteShadowProvider` 선택을 보존한다.
- `ShadowVertices`는 Sprite 피벗 기준 로컬 좌표·Z=0, `ShadowEdges`는 선분 끝점 인덱스의 쌍이다. 윤곽은 제작/임포트 단계에서 준비한다. 원본 알파·공격 VFX·잉크 외곽선을 매 렌더 프레임 추출하지 않는다. 현재 전용 마스크→윤곽 임포터는 미완료다.
- 실제 모션이 선택한 프레임·flip·자세를 그대로 읽는다. 노말/발광/그림자용 새 모션 타이머를 만들어 전투 시간표와 어긋나게 하지 않는다. 실제 아트 전체 모션 정합은 별도 검사한다.

## 5. 등록 원본과 생성물 주의

- `data/presentation/lighting/lab.json`이 실험장의 프로필·광원·진자·연결부·창문 방향·구역 수치 원본이다. `unity/Assets/Deviant/Presentation/Resources/Lighting/Lab.json`만 직접 수정하지 않는다. 실제 게임 구역 배치는 해당 맵 소유 원본과 연결해야 한다.
- `python3 tools/lighting/export_lighting.py` 후 `--check`로 동기화를 확인한다. 이는 JSON 바이트 전달이며 실제 PNG·발광 맵·소켓·윤곽 등록기가 아니다.
- [LightingLabBuilder.cs](../../../CombatLab/Editor/LightingLabBuilder.cs)의 `Build` 또는 `Tools > deviant > Experiments > Lighting > Open Lab`은 공통 Bindings·광원/그림자 템플릿을 재생성한다. **재생성 대상 템플릿에만 실제 아트/쿠키 참조를 수동 저장하면 다시 만들 때 사라질 수 있다.** 콘텐츠 소유 프리팹/바인딩이나 명시적 조립 참조로 보관한다.
- JSON에 emitter ID를 추가하는 것만으로 새 프리팹/소켓/에셋이 생기지 않는다. 현재 builder는 `lantern`/`window` 템플릿을 준비한다. 새 종류는 정의·Editor 저작·제품 조립·검사를 함께 연결한다.
- `LightingBindings`는 공유 템플릿·셰이더 참조이지 모든 제품 발광 맵의 등록 목록이 아니다. 본 게임 통합 전에는 기존 정비소/마을의 장식 후광과 새 조명이 이중 적용되지 않는지 확인한다.

## 6. 검증과 완료 기준

Editor·테스트·빌드는 **Unity 6000.6.5f1과 원본 패키지 버전**만 사용한다. 실제 검증 절차는 [testing.md](../../../../../../docs/development/testing/testing.md)를 따른다. 로그·캡처는 Git 제외 `outputs/`에 둔다.

```sh
python3 tools/lighting/export_lighting.py --check
python3 -m unittest discover -s tools/lighting -p 'test*.py'
python3 tools/check_docs.py
```

Unity EditMode에서는 `LightingDirectorTests|LightingZone|WindowLight|WindowAperture|LightingLabIntegrationTests|LightingLabGraphicsTests|HangingConnector` 범위를 실행한다. 픽셀 검사는 `-nographics` 없이 실제 URP 2D Renderer로 수행한다. 격리 프로젝트의 원본/패키지·고정 리소스 준비와 실행 명령은 검사 문서에 따른다.

- 발광: 소등 시 발광만 사라지고 본체가 남는지, 프레임/금속이 원치 않게 빛나지 않는지, 본체·발광부·소켓이 최대 흔들림/모션/flip에서도 맞는지 확인한다.
- 쿠키: 막힌 RGB 영역은 실제 주변 조명이 줄고 열린 영역은 여전히 비추는지, null 복귀·무효 입력의 이전 상태 보존·복수 인스턴스 독립성을 확인한다. 발광 Sprite의 모양만 달라진 것은 조명 통과 증거가 아니다.
- 창문: 십자/곡선 개구부, 다른 깊이의 평행 그림자, 빛줄기 OFF 상태의 실제 표면 명암, 실내 범위 밖 빛샘과 복수 창문의 독립 해제를 확인한다.
- 캐릭터/몬스터: 전체 적용 모션의 색/노말/윤곽, 자기 차폐, 평면 A/B·반전·재질/형상 캐시 재사용을 확인한다. 접촉·피해·이동·시계는 조명 유무와 같아야 한다.
- 리셋·일시정지·풀 반환·재활성화·씬 종료에서 자기 요청/등록만 정리하고 다른 소유자를 지우지 않는다. 일반 전환과 깜빡임은 공통 시뮬레이션 시계를 따르며 임의 `unscaledTime` 루프를 추가하지 않는다.

원본 등록, Unity 배치/참조 연결, 실험장 통과, 본 게임 통합, 실제 미술 승인·GPU 성능 검증을 각각 구분한다. 문서·바인딩·합성 테스트만 추가하고 실제 제품 아트 적용까지 완료했다고 기록하지 않는다.
