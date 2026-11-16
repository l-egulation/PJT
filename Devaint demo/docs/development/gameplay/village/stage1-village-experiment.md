# 1Stage 마을 제작 기반

[분야 개요](README.md)

작업 브랜치는 `experiment/stage1-village-reference`, 작업 폴더는 `03_개발/Worktrees/stage1-village-reference`다. 2차 채택 방향에 따라 밝음과 스케치 양쪽 환경을 [마을 미술 기준](../../../assets/world/village/stage1-village-art-direction.md)에 맞춘다. 밝은 지형은 스케치의 큰 돌·접촉선·외곽에 맞춘 새 짝을 사용한다. 맵·물리·캐릭터·전투·지도는 팀 원본을 유지한다. 현재 팀 씬의 `ForestCombatWorld.cameraVerticalOffset`은 사용자가 지정한150px로, 기본 추적 구도에서 카메라를 위로 올려 캐릭터와 지면을 화면 아래쪽에 표시한다. 다른 씬은 기본값0이며 렌즈·점프·충돌 좌표는 유지한다. 수치 검증과 미술 기준 충족은 구분한다.

제작 범위와 통과 기준은 [제작 계획](stage1-village-production-plan.md)을 따른다. 대표 화면의 실제 렌더·전환 검증 후 같은 종류별 에셋을 기존 전체 맵에 연결한다.

## 팀원 최신 맵에 마을 에셋 적용

`Assets/MeadowDuskV3/MapVer2/Scenes/Chapter1Procedural.unity`는 팀의 기존 랜덤 스테이지다. 이 씬에 `VillagePresentation.teamWorld`와 픽셀 화면 표시기를 연결한다. `ForestCombatWorld`·`Chapter1ProceduralMap`·방/길목 카탈로그가 맵 구조, 충돌, 출입구 높이, 몬스터, 시작 도면, 전투, 카메라와 지도 공개를 계속 소유한다. 아트 연결 때문에 이 데이터를 복제하거나 새로 설계하지 않는다.

마을 표현기는 기존 지형·배경의 Renderer만 끄고 같은 점유 배열과 일반 발판 좌표에 확정 컬러/스케치 자산을 배치한다. `VillageTeamScenery`는 방 안의 실제 지면 전체 지지와 위쪽 여유를 검사해 기존 건물·담장·수풀을 배치한다. 장식에는 충돌을 추가하지 않는다. 기존24구간 씬과 그 입구 A 슈퍼점프는 Unity Assets에서 제거하고 `unity/Tests/Fixtures/LegacyScenes`에 테스트 자료로만 보관한다. 팀 맵에 임의의 새 슈퍼점프 위치를 만들어 길목 설계를 변경하지 않는다.

팀 맵의 표현 연결은 `VillageTeamMapReview.Apply`, 실제 씬 검사는 `VillageTeamMapReview.Run`, Windows 빌드는 `VillageTeamMapReview.BuildWindows`다. 빌드는 최신 타이틀에서 시작하여 팀 랜덤 스테이지로 연결되며, 구맵 선택은 제공하지 않는다. `--team-map-smoke-output <폴더>`를 명시한 실행에서만 자동 검증이 동작한다. 원본 생성 결과와 지형·발판·몬스터 데이터 일치, 도면2개 선택, 일반 점프·착지·공격, 탐색 지도와 정지를 검사한다. Windows 비배치 검사는 실제 표시되는 창에서 시작 화면 종료와 픽셀 표시기 준비를 기다린 뒤 실제 전체 화면의 밝기·대비도 검사한다. 별도 카메라 렌더의 성공만으로 화면 표시를 통과시키지 않는다. 모든 시드의 완전한 길 찾기와 최종 미술 승인은 별도다. 출력은 Git 제외 `outputs/village/team-map/`이다.

`STAGE1_WINDOWS_OUTPUT`은 검토용 Windows 실행파일의 별도 절대 경로를 지정한다. 지정하지 않으면 기존 출력 경로를 사용한다. 실행 중인 다른 검토본을 덮어쓰지 않고 변경본을 구분하는 용도다.

캐릭터 표시 위치는 `ActorRenderInterpolation`이 기존120Hz 물리 위치 사이를 한 고정 틱 이내의 지연으로 보간한다. 스프라이트의 빈 부모만 이동하고 논리 좌표·충돌체·점프 시간·높이는 바꾸지 않는다. 기본 카메라도 같은 표시 위치를 따라가며 워프·리스폰·수동 검증·일시정지는 즉시 물리 위치로 맞춘다. 네이티브 스프라이트의 로컬 위치0과 피벗은 보존한다.

체력바는 실제 표시 사각형의 확대율·여백을 거쳐 화면 좌표로 변환하고, 투명 캔버스 대신 현재 스프라이트의 불투명 픽셀 윗부분 위에 표시한다. 사망한 적의 체력바는 숨긴다.

## 1스테이지 전투 채택 미술

`Chapter1Procedural`의 전투 스테이지(`proceduralRooms`, 마을 허브·더미 훈련실 제외)는 `VillagePresentation`이 기존 표현을 만든 직후 `Stage1CombatArt`를 붙여 채택 미술로 바꾼다. 충돌·맵 데이터·액터는 건드리지 않고 기존 지형·발판·하늘·원경 Renderer만 끈다. GameObject는 끄지 않으므로 기존 표현 전환(SetActive)이 옛 그림을 되살리지 않는다.

| 부분 | 원본 리소스 | 표현 |
|---|---|---|
| 지형·나무 발판·장식 | `environments/stage1-pixel-lawn-wood-32` | 47개 연결 형태×4변주를 `terrain-atlas.png`에서 잘라 Tilemap으로 깐다. 타일 윗면은 충돌 접촉선보다 8px(0.25유닛) 위다. 꽃·빗금·박힌 돌·벽 풀·안쪽 모서리를 같은 규칙으로 얹는다. 사라지는 발판은 `Vanishing platform art <번호>` 아래에 그려 기존 사라짐 연출을 받는다. |
| 분리 배경 | `environments/stage1-pixel-background-native-20261111` | 산(-100)·먼 마을(-90)·가까운 마을(-80)을 한 칸씩 좌우 반전 반복하고, 아래로는 맵 바닥까지 마지막 줄을 늘인다. 가로 이동 비율은 0.92, 0.82, 0.55이고, 세로로는 카메라를 거의 따라가(0.99, 0.98, 0.96) 보스 통로처럼 깊은 곳에서도 마을이 같은 높이에 보인다. 카메라 배경색은 `#FCF7EE`다. 산·먼 마을·가까운 마을은 마을 배경과 같은 그림의 색 빠진 판(`environments/stage1-background-color-20261112/runtime/sketch`)이고, 맨 아래 세 줄이 한 색이라 늘인 부분이 덤불 그늘색 면으로 이어진다. 구름·새·보스방 둥근 창틀은 `environments/stage1-background-cream-20261112`의 판이다. 전투는 색 빠진 세계라 구름과 새는 움직이지 않고 카메라 원근으로만 밀린다. |
| 뒤쪽 흙 벽 | 같은 지형 아틀라스와 빗금 | 기존 `Pass-through pale background walls`의 형태는 유지하고 재질만 채택 흙으로 바꾼다. |
| 보스 길목·보스방 | `environments/stage1-boss-approach-pixel-20261112` | 기존 `Chapter1BossBackdrop`의 그림(`Clock room rear art`·`Clock approach rear art`)은 Renderer만 끈다. 보스방 앞 통로의 마지막 40유닛(`Stage1CombatArt.ColonnadeLength`)에 기둥 회랑 한 칸(440×208)을 바닥에 붙여 Tiled로 반복하고 기둥 머리돌부터 시작한다. 통로 앞쪽은 기둥 없이 배경이 보인다. 보스방에는 탑·들보(-45)를 방 왼쪽 아래 모서리에 맞추고, 선 없는 둥근 창틀(-96)을 산과 마을 사이에 둔다. |
| 하늘 | `environments/stage1-atmosphere-sky-ash-20261111` | 구름·새를 seed 7312026으로 흩고 종잇재 한 메시를 화면 전체에 띄운다. |

Unity 경로는 `Stage1VillageExperiment/Resources/Stage1Combat/`이고, `tools/assets/register_stage1_combat_terrain.py`가 PNG 38장을 배치 계약에 올린다(Point·무압축·밉맵 없음·PPU 32). 셰이더 셋은 같은 폴더의 `Shaders/`에 두어 빌드에 포함한다. 전체 지도 보기에서는 배경·하늘을 숨기고 지형만 종이색 위에 보인다.

## 실행과 입력

Unity **6000.6.5f1**에서 현재 팀 씬 `Assets/MeadowDuskV3/MapVer2/Scenes/Chapter1Procedural.unity`를 열어 실행한다. 아래 24구간 전용 좌표·워프·슈퍼점프 설명은 `unity/Tests/Fixtures/LegacyScenes/Assets/Stage1VillageExperiment/Scenes/Stage1VillageExperiment.unity.txt`로 옮긴 과거 실험 자료다. 현재 Unity 씬이나 빌드에는 포함하지 않는다. 이를 현재 팀 맵의 진행 분포로 사용하지 않는다.

현재 팀 맵은 `VillageArtTransition.ConfigureTeamRegions`가 기존 방의 전체 가로 범위를 읽어 앞 약2/3을 스케치 영향 구간으로 정한다. 완전 스케치→혼합의 분할은 현재 제작 미리보기에서 전체1/3을 사용하며 최종 확정 수치가 아니다. 밝음/스케치 사이를 넓게 혼합하지 않고 가까운 구조물은 온전한 그림 단위, 원경은 큰 레이어, 지형은 노출 가장자리 깊이에 따라 상태를 선택한다. 원경 레이어 전환부와 실제 산개 배치는 추가 미술 검토 대상이다. 확산 발동 조건·속도·보상은 추가하지 않았다.

`Stage1VillageExperiment.Editor.VillagePhase2Review.Run`은 저장된 씬을 바꾸지 않고 seed29053802로 팀 맵 검증, 15개 방의 실제 상태, 주거·시장·광장·나무의 양쪽 상태, 평지·계단·벽·천장·64/128/256px 발판 조립을 캡처한다. 출력은 `outputs/village/phase2-production-20261027/terrain-review/`다. Windows 빌드는 `VillageTeamMapReview.BuildWindows`, 실제 표시 검증은 `--team-map-smoke-output <폴더>`로 별도 실행한다.

`STAGE1_PHASE2_REVIEW_OUTPUT`으로 별도 비교 출력 폴더를 지정할 수 있다. 현재 검토기는 배경벽의 충돌·강체 없음, 캐릭터 뒤의 그리기 순서, 같은 지면에서 겹치는 구조물 묶음도 검사한다. `phase2-scenery.json`은 이미지 자체 알파로 측정한 접촉선을 지정하며 일반 물리·접지·점프 값을 바꾸지 않는다. Windows 표시 검증은 정상 창으로 실행한다. 숨김 창의 검은 캡처를 표시 성공으로 취급하지 않는다.

## 보존된 24구간 실험 씬의 입력과 검증

| 입력 | 동작 |
|---|---|
| ←/→ | 이동 |
| Space | 점프·공중 추가 점프 |
| ↓ + Space | 통과 발판 아래로 내려가기 |
| Shift | 대시 |
| S / W / Q | 기본공격 / 특수 / 무기 교체 |
| R | 캐릭터 초기화 |
| M / Esc | 전체 지도 열기 / 닫기 |
| 전체 지도 휠·+/− / 드래그 / C / Home | 확대·축소 / 이동 / 현재 위치 / 전체 보기 |
| F3 | 아트 확인용 24개 구간 워프 |
| F6 | 실제 맵 상태 → 컬러 → 스케치 → 전체 혼합 → 비교 경계 → 실제 맵 상태 |
| F7 / F8 | 전체 혼합 가중치 −/+0.05 또는 비교 경계 −/+32px; 실제 맵 상태는 변경하지 않음 |
| F9 | 기존 정상/스케치 구간 상태로 복귀 |

기본 상태는 `Authored`다. `presentation_layout.json`의 `initialSketch`에 따라1–8구간은 스케치,9–24구간은 정상이며 월드X5056–5760px에서 연결된다. 최초 실행부터 적용되고 카메라 이동에 따라 바뀌지 않는다. 전체 비교와 임의 경계는 미술 점검용이며 실제 구간을 대신하지 않는다. 새로운 복원 진행·보상·저장 규칙은 추가하지 않는다.

## 유지하는 구조

- 편집 원본은 `data/stage1-village-experiment/map-layout.json`, 이동 입력은 같은 폴더의 `ballistic_routes.json`이다. [생성 도구](../../../../tools/stage1_village_experiment/README.md)는 검토용 출력만 만들고 Unity 데이터를 자동 덮어쓰지 않는다.
- `Data/VillageWorld.json`의 충돌 지형과 `Resources/Village/presentation_layout.json`의 월드 고정 배치를 사용한다. 좌상단 원점이며 픽셀 Y가 아래로 증가한다.
- `Stage1VillageWorld`는 공통 플레이어·적·전투와 `Chapter1FullMap`을 연결한다. 물리 수치 원본은 `data/combat/locomotion/player.json`이다. 아트 변경을 위해 충돌·이동·점프 수치를 조정하지 않는다.
- `VillagePixelViewport`는 960×540 논리 화면을 Point 필터로 정수배 확대한다. PPU 32, 원본 캐릭터 크기, 현재 카메라를 기준으로 선과 빗금을 판단한다.
- `VillagePresentation`의 정상 상태는 기존 `Village/color`와 `Village/backgrounds` PNG를 그대로 읽는다. 스케치 건물·배경은 `Village/reference-sketch`, 지형·발판은 `Village/ReferenceGround`를 사용한다. 각 그림 자체의 알파와 접촉점을 사용한다.
- 가까운 건물·담장·지하 앞 기둥은 월드에 고정한다. 현재 원경 가로 이동량은 카메라의 18%, 중경 45%, 구름 5.5%다. 지하 후벽은 가로 8%·세로 4%를 사용한다. 이 수치는 제작 기반이며 새 미술의 최종 배치를 확정하지 않는다.
- 발판의 착지선과 덤불의 시각 위치 보정은 기존 충돌면에 맞춘다. 이미지 캔버스 여백과 실제 그림 접촉선은 다르며, 새 발판의 접촉점은 `ReferenceGround/library.json`에 기록한다.
- 종류별 자산을 기존 인스턴스에 재사용한다. 새 자산의 실제 크기·피벗·접촉선·정상 원본 해시는 `reference-sketch/art-manifest.json`으로 확인한다. 실제 맵의180발판 위치·폭과93개 근경/수로 인스턴스 배치는 바꾸지 않는다.

## 제작·검증 범위

정상 지형은 기존 `Stage1TerrainQuarterRenderer`·코너 보정·돌 접합을 사용한다. 스케치 지형은 `VillageReferenceGround`가 기존 점유 배열에 전체 그림을 배치한다. 정상 컬러를 스케치에 맞춰 재채색하지 않는다. 그림의 돌출 부분과32px 충돌 칸은 독립적이므로, 접촉선과 전환 중 윤곽 일치는 실제 카메라에서 별도로 검토한다.

스케치 내부 빗금은 원본 흙의 명암이나 타일UV를 강도 입력으로 사용하지 않는다. 별도 원화 재질을 월드 좌표에서 연결하고, 국소 그늘은 생성된 돌 외곽 그림에 남긴다. 평지·좌우 벽·오목/볼록 모서리·아랫면은 완전한RGBA 그림과 각 접촉점을 사용한다. 기존 `VillageSketchStairWallCompletion`은 이전 스케치와 비교할 때만 활성화된다. 새 발판은 장식이 잘리지 않는 전체 캔버스의 양끝을 유지하며 기존160/384px 폭에 맞춰 중앙 부분을 반복한다.

혼합 화면은 `VillageSceneComposite`가 동일한 카메라·시뮬레이션 상태의 완성된 컬러/스케치 화면을 한 번 보간한다. 투명 구멍에서 다른 레이어의 알파가 중복 곱해지는 현상을 막으며, 전체 컬러·전체 스케치·전환 영역 밖 카메라는 두 번째 렌더를 생략한다. 이는 에셋 구조 불일치를 숨기는 장치가 아니다. 지붕·창·아치·기둥 등록은 제작 도구의 제약 메시로 별도 수행한다.

`Tools > Stage1 Village > Verify Gameplay And Capture`는 원래 모터·착지·이동·점프·대시·통과 발판·카메라·접지·지도 검사를 유지한다. 비교 모드를 켜기 전에 최초 실제 맵 상태·24구간·월드X5408px 주변 전환을 촬영한다. 추가로0/0.25/0.5/0.75/1 혼합과 이동 후 영역 고정, 임시 조립 레이어의 타일 접합을 확인한다. 워프 촬영은 그림 검토이며 시작점 기반25개 이동 입력과 출구 검증은 별도다. 검사가 통과해도 시안의 선·형태·빗금이 다르면 미술 통과가 아니다.

배치 진입점은 `Stage1VillageExperiment.Editor.Stage1VillageBuilder.VerifyFresh`다. 검증기가 종료하므로 `-quit`을 붙이지 않는다. 출력은 `STAGE1_VILLAGE_VERIFY_OUTPUT`, 입력은 `STAGE1_VILLAGE_ROUTES`, 전체 경로는 `STAGE1_VILLAGE_VERIFY_ALL_ROUTES=1`로 지정한다. 로그·검증 결과는 Git 제외 `outputs/`에 둔다.

캡처에는 `-force-d3d11` 같은 실제 그래픽 장치를 사용하고 `-nographics`를 붙이지 않는다. `VillagePixelViewport`의 실제 960×540 → 정수 확대와 HUD는 Windows 실행파일의 `--village-smoke-output <폴더>` 비배치 실행으로 별도 확인한다. 해당 smoke는 걷기·점프·착지·공격·지도와 컬러/스케치/지역/연속 혼합 화면을 저장한다.

기존 배경·건물의 일부 지붕·창·수관과 새 발판 장식은 컬러 원본과 형태가 달라 중간 혼합에서 이중 윤곽이 남는다. **채택 시안의 완전한 구조 일치·최종 미술 통과는 미완료**다. 새 지형은 사용자가 지정한 imagegen CLI와 환경변수의 서버를 통해 실제 투명 배경으로 준비했다. 생성물 자체의 약한 외곽 알파와 내부 연결부는 전용 재질에서 처리하며, 컬러 원본의 알파를 덮어쓰지 않는다.

Windows 빌드는 `Stage1VillageExperiment.Editor.Stage1VillageBuilder.BuildWindows`와 `STAGE1_VILLAGE_BUILD_OUTPUT`을 사용한다. 빌드·실행 검증은 별도 수행해야 하며 문서 갱신이나 정지 시안 승인으로 대신하지 않는다. 보스·보물·출구는 배치 표지이며 전용 전투·보상·다음 스테이지 구현은 포함하지 않는다.

## 원본 스케치 지형 적용

현재 스케치 기준은 `Stage1VillageReference/ArtDirection/approved-sketch.png`다. 게임용 그림은 `Assets/Stage1VillageExperiment/Resources/Village/ReferenceGround/`에만 둔다. 이전 미채택 에셋·검토용 코드·생성기는 Unity Assets 밖 `outputs/village/99-rejected-before-reference-reset/`으로 이동했다.

`prepare_reference_ground.py`는 imagegen CLI로 원본에서 준비한 실제RGBA 그림을 균일 축소하고 투명 여백과 착지 기준점을 등록한다. 현재 발판을 세부 화풍 기준으로 삼아 `output/imagegen/platform-unified/`에 평지·모서리·벽·빗금 원화를 준비한다. 발판 원본은 유지하며 평지에는24/32/40px 계열의 온전한 돌을 사용한다. 좌우 바깥/안쪽 모서리, 좌우 벽, 아랫면도 발판과 같은 선 굵기로 맞춘다. 수평·수직 길이는 연속 구간 단위로 배치하며 컬러 알파나16px 사분면으로 그림을 잘라내지 않는다. `library.json`은 캔버스 크기·접촉점·논리 길이를 분리한다. 내부 빗금은160px 연속 원화 재질을 공통 월드 좌표로 표시하고 팔레트를 별도로 회색화하지 않는다.

`VillageReferenceGround`는 기존 점유 배열과 발판 위치에서 그림만 만든다. 충돌·플레이 수치·맵 데이터는 수정하지 않는다. `VillagePresentation.SetReferenceGroundEnabled`는 새 그림과 기존 스케치를 비교하는 기능이다. 정상 컬러는 항상 기존 렌더를 사용하고 스케치 표시만 교체한다. 기본값은 새 그림 사용이며 씬 재생성은 필요하지 않다. `VillageReferenceGroundImporter`가 새 그림에만 Point·비압축·MipMap 없음·FullRect용 설정을 적용한다.

계단과 긴 평지는 동일한 돌 단위로 연결한다. 별도 안쪽·바깥쪽 모서리 그림의 돌·삼각형 빗금이 벽과 겹치는 것을 피하고, 공통 벽면이 모서리까지 이어지게 한다. 내부 메시의 이웃 칸은 노출면에서도2px 안쪽으로 줄이지 않는다. 벽과 아랫면의 긴 그림은 접합점 양쪽1px씩 겹쳐 원화 끝의 투명 여백이 틈을 만들지 않게 한다. 원화PNG·접지·충돌은 그대로다. `VillageTerrainArtReview`는 양쪽 계단·천장·오목한 모서리를 조립한 실제 렌더의 점유 픽셀 알파를 검사하고 `terrain-join-coverage.json`을 출력한다.

현재 재현 순서는 다음과 같다.

1. `python tools/stage1_village_experiment/prepare_reference_ground.py`
2. Unity6000.6.5f1의 `Stage1VillageExperiment.Editor.VillageReferenceGroundReview.Run`
3. 필요 시 기존 `Stage1VillageExperiment.Editor.Stage1VillageVerification.Run` 및 `Stage1VillageExperiment.Editor.Stage1VillageBuilder.BuildWindows`

전용 검토는 기존 게임 씬에서 컬러 전후 픽셀, 충돌 경계·캐릭터 발 위치 보존, 전체 스프라이트 사각형과 투명 캔버스 여백을 확인한다. 1·2·4·7구간과 중간 전환을960×540으로 촬영한다. 출력은 `outputs/village/reference-ground/`에 둔다. 전체 게임플레이 검증·Windows 빌드 여부는 해당 실행 결과로 따로 판단한다. 배경 이중 윤곽·근경과 원경의 정보량은 지형 적용과 별개로 검토한다.

위 `VillageReferenceGroundReview`의 컬러 픽셀 불변 검사는 1차 스케치 전용 작업의 보존 기록이다. 2차에서는 밝은 타일의 형태 변경이 요청되었으므로 현행 검증 진입점은 `VillagePhase2Review.Run`이다. 이전 기대값을 바꿔 통과시키지 않고 기존 맵·발판·적 생성 데이터와 실제 조립/두 상태 화면을 별도로 검증한다.

## 가까운 구조물의 선명도

타일·발판은 사용자가 1차 확정했으며 `ReferenceGround` 그림과 재질은 유지한다. `VillagePresentation`은 `props/` 및 `vault_front_native`의 기존 컬러/스케치 쌍에만 `_NearSketchInk`를 지정한다. `VillageRegion`은 스케치의 기존 짙은 획과 빗금에 흑연 농도를 보강하고 밝은 종이 면과 알파를 유지한다. 컬러 렌더, 배치, 크기, UV와 먼 배경·지하 후벽에는 영향을 주지 않는다. 새 원화 생성이나 원본 PNG의 교체는 필요하지 않다.

Unity6000.6.5f1의 `Stage1VillageExperiment.Editor.VillageNearStructureReview.Run`은 같은 카메라에서 근경 처리만 켜고 끈 비교를 `outputs/village/near-structure/`에 저장한다. 1·2·4·7구간, 컬러 보존, 충돌·접지·구조물 배치와 확정 지형 재질의 독립성을 확인한다. 실제 사용자용 비교 페이지는 `outputs/village/art-review/near-structures.html`이다. 이 화면의 미술 승인은 지형의 1차 확정과 별개다.

## 슈퍼점프 발판 후보의 맵 배치

A 돌 스프링형을 기존 1구간 입구의 상승 동선에 한 개 적용한다. 위에서 착지하면 상판이 내려가고 캐릭터가 함께 눌린 뒤 슈퍼점프로 발사된다. 바닥은 고정하고 원화 전체를 연결한 메시의 스프링 구간만 압축·신장하며, 돌 상판의 폭과 두께는 유지한다. 동작 참고는 `output/imagegen/super-jump-press-preview/a-press-rebound-final.png`다. 원래 전투 규칙과 일반 이동·점프 프로필, 기존 지형·일반 발판은 유지한다. 기능 구현·실행 검증·최종 미술 승인은 구분한다.

배치 원본은 `data/stage1-village-experiment/jump-pad-art.json`이다. 중심 X696px, 바닥 접촉 Y2176px, 상판 폭112px이며 주변 장식까지 약128px 폭이다. 기존 입구 지면 위에 서고 플레이어·지면·다른 발판의 위치를 바꾸지 않는다. 원화는 `output/imagegen/super-jump-placement/`의 imagegen CLI 투명 RGBA 응답이며, `prepare_jump_pad_art.py`가 전체 캔버스를 균일 축소해192×96px 여백 캔버스에 공통 접촉점(96,80)을 등록한다. 원본 컬러 알파를 스케치에 덮어쓰지 않는다.

`VillageJumpPadArtwork`는 `Resources/Village/JumpPadArt`의 두 PNG와 배치 JSON을 읽어 기존 `VillageRegion` 전환에 연결한다. `gameplayEnabled`가 켜진 A 발판에만 `VillageSpringPad`와 한 개의 단방향 착지면을 추가한다. 기존 지형333면과 일반 발판180개는 변경하지 않는다. 옆면·아래에서의 접촉에는 발동하지 않으며, 눌리는 중 옆으로 벗어나면 발사를 취소한다. 발사 시 진행 중인 행동을 한 번 중단해 기술의 수직 고정이 환경 반동을 덮어쓰지 않도록 한다. 일반 공격·기술의 설계나 수치는 바꾸지 않는다.

발판 동작 수치의 편집 원본은 `data/stage1-village-experiment/jump-pad-motion.json`이며 해당 발판에만 적용하는 플레이테스트 값이다. 상판 높이48px, 눌림12px/0.16초, 바닥 대기0.035초, 반동5px/0.12초, 복귀0.24초, 재발동 대기0.18초다. 목표 상승량은 발사점에서384px이고 실제 발사 속도는 기존 상승 중력으로부터 계산한다. 일반 점프의 속도·중력 원본은 그대로 사용한다. `prepare_jump_pad_art.py`는 배치와 동작 JSON을 함께 게임 Resources로 복사한다.

동작은 `CombatArena`의 플레이어 스텝 전후 알림에 연결하므로 일반 고정 스텝과 수동 검증이 같은 시계를 사용한다. 지도에 따른 시뮬레이션 정지 중에는 압축·반동 시간도 진행하지 않는다. 리셋·죽음·오브젝트 비활성화 시 탑승 참조와 눌림을 해제한다. 다시 활성화하면 알림을 재연결한다.

`VillageSpringPadReview.Run`은 기존 씬에서 실제 점프 입력으로 눌림·캐릭터 동행·반동 높이·일회 발사·위쪽 기존 발판 착지와 일반 점프 보존, 옆면/아래 통과, 이탈 취소·리셋·재활성화를 검사한다. 실행 출력과 장면은 `outputs/village/jump-pad-gameplay/`에 저장한다. `VillageJumpPadArtReview.Run`도 동작이 켜져 있으면 같은 검사로 연결한다. 이 검사는 전체25경로 재검증을 대신하지 않는다.

## 이미지 전달


PNG는 Git에서 제외하고 소스·씬·`.meta`·manifest와 별도로 전달한다. 기존 정상 원본과 게임용 스케치는 `package_images.py`의 이미지 ZIP으로, 생성 응답·프롬프트·참조·등록 자료는 ignored outputs의 제작 복원 ZIP으로 구분한다. 공통 캐릭터·몬스터·이펙트는 [기존 복원 절차](../../../assets/README.md)를 따른다. 현재 지형 적용과 실행 검증 결과는 `outputs/village/reference-ground/`, 비교 페이지는 `outputs/village/art-review/reference-ground.html`이다. `reference-correction` 등 과거 결과와 새 지형 검토본을 혼동하지 않는다.

## 몬스터 작업 브랜치의 통합

`feat/stage1-monster-assets`에서는 이 마을 브랜치를 병합한 뒤 팀 `Chapter1Procedural` 씬에 [확정 몬스터 모션과 시험용 전투 연결](../../presentation/visuals/stage1-monster-motions.md#마을-게임-적용)을 추가한다. 마을 미술 원본·지형·플레이어는 유지하고, 적 콘텐츠만 일반 세 종과 우물 도르래로 교체한다. 보스는 등장하지 않는다. 원래 마을 브랜치/다른 작업 폴더를 전환하거나 수정하지 않는다.
