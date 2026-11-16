# 검사와 빌드

`ForestCombatV2.Tests.PlayerHurtScreenFeedbackTests`는 메모리 프로필의 실제 1·2챕터 씬에서 체력 피해 이후 붉은 화면 외곽빛의 상승·최고 밝기·감쇠·종료, 연속 피격 시 밝기 유지, 리셋과 무적/보호막/적 피해의 미발동을 검사한다. 외곽 텍스처의 투명한 중앙과 빨간 가장자리, 캐릭터의 기존 색상·알파·SpriteRenderer 수 유지 및 추가 카메라 이동·히트스톱 없음도 확인한다. 원본/생성 설정 일치는 `python tools/combat/export_hit_feedback.py --check`로 검사한다.

[분야 개요](README.md)

## 가방·제작 채택 화면

Unity 6000.6.5f1의 `Tools > deviant > Approved UI > Verify inventory flow and capture` 또는 `-batchmode -executeMethod Deviant.Game.Editor.ApprovedCoreVerification.RunInventory`는 실제 전투 씬과 메모리 프로필로 가방 분류 포커스, 공개 두 탭, 39종 잉크, 6칸 에셋 팔레트, F 선택/적용과 R 해제, 확인 취소·확정 소비, 완료 화면의 F/C 전투 복귀, 슬롯 우선·무기 우선 장착, 해제·교환·이름 변경과 패드 이동을 검사한다. 그래픽 장치가 필요하며 `-nographics`와 `-quit`은 넣지 않는다. 하네스가 종료를 관리하며 1920×1080·1280×720 캡처와 결과는 Git 제외 `outputs/unity-inventory-flow-20261116/`에 저장한다. 기존 `RunBagCraft`·`RunEquipment` 진입점도 같은 병합 흐름 검사를 실행한다. `InventoryItemHitAreaTests`·`SketchArsenalTests`를 함께 실행해 클릭 영역·공통 강조 부품과 잉크 획득/예약/소비/누적 총수의 회귀를 확인한다. 실제 OS 한글 조합 체감과 전체 런 밸런스 검증은 자동 입력 검사와 구분한다.

## 지도 통로 경계

Unity 6000.6.5f1 EditMode의 `ForestCombatV2.Tests.MapOpenFrontierTests`는 전체 지도·미니맵 크기의 실제 UI 메시에서 상하좌우 미탐험 통로 입구가 열린 채 표시되는지, 실제 벽·바닥은 유지되는지, 숨겨진 방 내부가 발견 전 노출되지 않는지와 발견 후 캐시 갱신을 검사한다. 생성 시드 42·777의 1챕터 지도와 2챕터 실제 지도 어댑터에서도 방별로 발견 상태를 바꿔 지형 테두리가 실제 고체에만 놓이는지 확인한다. 전체 본게임 씬의 아트·입력·런 플레이 검증과 이 독립 메시 검사를 구분한다. 확대·이동 좌표 회귀는 `Chapter1MapViewportTests`를 함께 실행한다.

## 지형 조회와 바닥 검사

Unity 6000.6.5f1 EditMode의 `Deviant.Combat.Tests.TerrainQueryPerformanceTests`와 `ActorSurfaceQueryTests`는 일반 콜라이더 제외·기존 사용자 지정/Ignore Raycast 레이어·native 순서, 80개 지형 접촉의 결과 확장, 액터 내부 버퍼 재사용과 공개 스냅샷 보존, 독립 PhysicsScene2D와 기본 씬 격리, 읽기 묶음 종료 뒤 발판 이동/비활성화·발 오프셋 변경, 차폐의 단방향/trigger 정책과 실제 모터의 접지·벽·천장·안전한 발판 내려가기를 확인한다. `ActorMotionTests`·`ActorPlacementTests`·`ActorFacingExecutionTests`·`ActorPlatformTests`·`ActorDropSafetyTests`와 `WallClimbInputTests`로 기존 접지·벽·천장·단방향 발판·방향 전환·벽타기 규칙을 함께 검사한다. `ActorMotionIntegrationTests`·`ActorPlacementIntegrationTests`는 구형 씬 회귀 자료를 명시적으로 복원한 경우에만 실행하며 미복원 시 건너뛴다. 시간 수치는 기능 회귀 검사의 통과 조건으로 두지 않으며, 동일한 맵·시드·몬스터 배치의 CPU 비교와 GPU/실제 플레이 검증을 구분한다. 일회성 측정·로그는 Git 제외 `outputs/`에 둔다.


## 런 잉크와 스케치 무기

`SketchArsenalTests`·`SketchRunStateTests`·`SketchRewardsTests`는 여러 미확정 무기 사이의 예약 초과 거부·실패 시 기존 슬롯 보존·예약 해제·확정 소비·확정 후 무기 손실의 미반환, 가방이 열린 동안 자유로운 장착·교체와 닫은 뒤 시작하는 통합 무기 변경 쿨타임 5초(같은 구성 복귀는 미시작, X 순서 교환은 무시), 재획득 합산·전 종류 재공급·보상 내 중복 금지·상점 내 중복 허용과 개별 품절을 검사한다.

`RunInkDesignTests`는 39종 카탈로그·5색 아이콘 매핑과 실제 Sprite 로딩, 확정 후 빈 슬롯 잠금, 잉크 횟수 합산·예약·확정 소비·소진 목록과 재획득, 보관 중 쿨타임 정지, 타격 묶음 이후 내구도 파괴, 6회 보상과 고정 상점 재고를 검사한다. `RunInkCombatTests`는 아트에 의존하지 않는 실제 전투 구성에서 같은 첫 타격의 상태 부여·빨강/파랑 강화, 다른 무기에 유지되는 빨강, 광역 타격 순서, 방어막 상한·감소·0 증가량의 발동 보존, 기본 쿨타임 기준 감소, 대시 원본 무기·경로 접촉 전 회복과 쿨타임 미소모·다수 적/반복 접촉의 회복 1회 제한·최대 체력 시 발동 보존과 중복본 슬롯 순서, 중복 기절 상한·매 적중 갱신·무기 버프 미연장·종료 후 쿨타임, 파괴된 원본의 잔존 공격·빨강과 대상 생명 주기 분리를 검사한다. 빈 A/B 슬롯의 맨손 공격이 반복 장비 동기화에도 인스턴스·공격을 유지해 실제 피해를 주는지, 다른 칸에 무기가 있어도 빈칸의 기본 공격은 가능하고 스킬은 불가능한지, 현재 무기 파괴 후 생존 무기로 전환하면서 빈칸의 맨손 공격도 유지하는지 함께 검사한다.

`RunInkCombatTests`는 추가로 두 무기의 조건이 다른 빨강을 합친 6칸 기여, 강화 우선·동률 최근 발동·초과 효과의 시간 진행과 만료 후 재선택, 보관·파괴 원본을 포함한 상한, 색별 효과의 독립 상한, 회복·방어막 생성의 제한 제외를 검사한다. 강화 6칸의 E=7 지속시간과 파랑·보라의 기존 원본 귀속을 함께 유지한다.

`RunInkEffectVisualTests`는 사용자 선택 잉크 이펙트 시트 17장의 배치·크기·점 필터, 실제 기본 공격으로 발동한 이속 바람·회복 십자와 초록 번쩍임·방어막 생성/피격, 빨강 불꽃과 파랑 불꽃·쿨감 시계, 속박 밧줄의 즉시 표시·몸 중심 배치·앞뒤 레이어·크기 선택·종료 시 숨김, 머리 위 슬로우 달팽이, 대쉬 방어막 초승달과 감소 후 사라짐, 바라보는 방향 뒤집기와 몸 뒤 레이어를 검사한다.

`RunInkPatternClockTests`는 패턴 시계 자체의 속도 변경 기능을 검사한다. 실제 런 보스는 상태 판정과 별개로 이동·공격 동작 감속을 적용하지 않는다. `RunInkSaveTests`는 임시 파일에서 구형 프로필·백업의 일회 초기화와 새 저장의 재로드 보존을 검사한다. `RunInkSceneTests`는 메모리 프로필로 실제 `Chapter1Procedural` 씬을 열어 보상 선택 → 빈 구성 확정 → A 장착 → 인벤토리 종료와 일시정지 해제, 39종의 색별 아이콘 로딩을 실제 UI 버튼 흐름과 GUI 아이콘 해석기로 검사한다. 실제 사용자 프로필은 사용하지 않는다.

치트 모드 편집은 `RunInkDesignTests`의 39종 선택·색상별 기본/강화 값, 무기 ID·내구도·장착본·쿨타임 보존, 대기시간을 소비하지 않는 장비 편집과 일반 구성 확정 잠금으로 검사한다. `DebugGameMenuTests`는 메모리 프로필의 실제 1챕터 치트 모드에서 무기·스킬·팔레트·잉크를 선택하고, 치트 모드 종료·전투 초기화 뒤 설정 유지와 실제 공격 시작을 확인한다. 재현 절차와 구형 훈련장 편집 구분은 [치트 모드](../tools/debug-menu.md)를 따른다.

Unity 6000.6.5f1과 원본 Packages·ProjectSettings를 유지한 격리 프로젝트에서 위 클래스들을 EditMode로 실행한다. `RunInkCombatTests`·`RunInkSceneTests`는 테스트 안에서 Play 모드로 전환한다. 기존 `ProgressionTests`를 함께 실행할 때는 상대 원본 경로 `../data/`도 격리 프로젝트의 부모에 복사한다. 전체 런 수동 플레이·게임패드 실물 입력·화면 가독성·Player 빌드는 이 자동 검사의 범위와 구분한다.

`ForestCombatV2.Tests.BluntAttackTimingTests`는 실제 `Chapter1Procedural` 씬의 Play 모드에서 시작 망치 도면을 선택하고, 정비소에서 대검 스탬프를 적용한 뒤 기본 공격 전 타를 실행한다. 수동 전투 시계를 0.001초씩 진행해 준비·판정·회복 전환과 연계 종료를 측정하며, 망치 2타 후딜0.2초·대검 1타 선딜0.2초·대검 3타 선딜0.3초/후딜0.2초를 검사한다. 관측 오차 허용은0.003초이며 카탈로그 시간 자체는0.00001초 오차로 검사한다.

## 밀집 전투·단계별 초기화

`ActorSurfaceQueryTests`는 밀집한 비지형 콜라이더와 버퍼 초기 용량보다 많은 지형을 함께 만들고 Native 순서·결과 보존, 이전 스냅샷 불변성, 레이어·trigger·무시·이동·비활성 변경을 검사한다. `RunInkCombatTests`는 중립 조회의 미등록, 감속 만료 정리, 독의 마지막 틱과 파괴 대상 정리도 검사한다. `StagedCombatInitializationTests`는 단계별 초기화 중 전투 시간·완료 이벤트·입력 제한, 모든 몬스터 준비 뒤 재개, 중간 취소 시 제한 해제와 미완성 전투 실행 거부를 검사한다. `EnemyPursuitSpatialCacheTests`는 무작위 지형 4종과 경계·동률 지점에서 추적 지형·그래프 격자 캐시의 현재 발판·아래 발판·벽·천장 조회가 전체 검사와 같은 결과와 낮은 id 우선을 내는지 검사한다. `Chapter1RoomActivityTests`는 플레이어 방과 통로 이웃 방의 활성화, 통로 위 플레이어, 방 밖의 거리 기준 위임, 같은 tick 안의 순간이동 반영을 검사한다. `Chapter1ProceduralMapTests.DistantIdleEnemiesResumeWhenTheirRoomIsApproached`는 실제 랜덤 씬에서 먼 방의 적이 쉬고 그 방으로 이동하면 깨어나는지 확인한다.

성능 비교는 같은 Unity 버전·맵 시드·몬스터 종류와 수·밀집 위치·입력·수동 tick 수를 고정한다. 실제 챕터의 전체 tick과 이동·지형 조회를 따로 측정하고, 초기 워밍업과 전체 로딩 경과 시간을 구별한다. CPU 시뮬레이션 계측은 렌더링 프레임률이나 Windows Player의 성능 검증을 대신하지 않는다. 수치·원본 로그·일회성 측정 코드는 Git 제외 `outputs/`에 보관한다.

## 스킬 밸런스·연막 영역 피해

`Deviant.Combat.Tests.ApprovedSkillBalanceTests`는 Unity 6000.6.5f1의 빈 씬에 실제 전투 런타임을 구성해 연막의 첫0.5초 지연·4초8틱·매 틱 범위/고체 차폐·취소와 이동 후 고정 위치 유지·특수 피해 스냅샷·리셋 정리, 대검 회전의2.5초 행동/8타/총192와 취소 시 미래 직접 타격 제거를 검사한다. 원본 카탈로그의 검기·돌진50/6초, 잔상35+35 및 은신 출처별 기습58/25도 확인한다. 실제 난이도·아트 타이밍·손맛은 별도 플레이 확인 대상이다.

제품 빌드의 준비·씬·Windows 명령/배포 단위는 [게임 빌드](../delivery/build-and-distribution.md), Web 명령·설정·정적 호스팅/브라우저 수락은 [Web 빌드](../delivery/web-build.md)를 따른다. 빌드 성공과 대상 Player의 실제 실행·장시간 플레이 검증은 구별한다.

## Web 영구 프로필 저장

[Web 빌드의 격리 저장 검사](../delivery/web-build.md#격리-web-저장-검사)는 실제 Run/프로필 저장 코드를 같은 Editor/패키지/프로젝트 설정으로 빌드해 브라우저에서 합성 재화·해금 저장과 재로드를 확인한다. 원본 버전/세이브 코드를 간소 구현으로 대체하지 않는다. 자동 IDBFS 초기화와 Replay 수동 fallback 공존은 `node tools/replay/test_browser_storage.cjs`로 검사한다.

`Deviant.Run.Tests.ProgressionTests`는 파일 교체/백업·손상 복구와 영구 진행 불변식을, `ForestCombatV2.Tests.Chapter1ProfilePersistenceTests`는 리플레이 없이 포커스 상실/일시정지 때 pending 마법안료를 저장하는지 확인한다. 실제 브라우저 IndexedDB 동작과 Editor 파일 IO 검사는 별개다.

## 플레이 데이터 수집 서버

`backend/collector`에서 `go test -race ./...`, `go vet ./...`로 임시 SQLite/API·파일·제한/중복·만료/삭제를 검사한다. `ADMIN_TOKEN`에 테스트 전용 값을 주입한 `docker compose config --quiet`는 데몬 없이 설정만 검사한다. 실제 이미지 실행/volume 재시작은 Docker 데몬 접근이 필요하다. 설정과 선택적인 C# 합성 파일 호환 검사는 [백엔드 안내](../../../backend/collector/README.md)를 따른다.

Unity 테스트 프로세스에 `DEVIANT_COLLECTOR_TEST_URL`을 설정하면 폐기 가능한 실제 Go 서버에 합성 player/session을 업로드하고 committed 이후 파일 삭제·종료 상태 동기화를 확인한다. 미설정이면 이 통합 검사만 건너뛴다. 운영 서버를 대상으로 실행하지 않는다. ID 유지·자동 수집 binding·기록 중단·commit 응답 검사는 같은 `Deviant.Replay.Tests`에 포함한다.

`node tools/replay/test_browser_storage.cjs`는 브라우저 FS 동기화 브리지의 병합/오류 처리를 stub으로 검사한다. 실제 Web Player의 IndexedDB 영속성이나 탭 종료 검증을 대신하지 않는다.

`ReplayUuid7Tests`는 RFC 9562 알려진 벡터/UTC 해석, 같은 ms·시계 후퇴의 문자열 단조 증가, suffix carry/소진, 병렬 생성·게임 RNG 보존을 검사한다. Go의 시간 ID 검사는 v4/v7 공존과 역순 수신 후 전역 seq 정렬, UUID 생성/조각 기록 UTC의 구별을 확인한다. 기존 플레이어 데이터나 원본 파일을 바꿔 시험하지 않는다.

## 로컬 리플레이 기록

Unity 6000.6.5f1의 EditMode에서 `Deviant.Replay.Tests`를 실행한다. 바이너리 round-trip·형식/길이/참조/비유한 값 거부, 순번·좌표 단순화/사건 anchor·불연속, SHA/충돌·로컬 인덱스 복구/퇴거, 실제 공통 GUI marker와 기록 host의 선택/hover/활성화·입력 텍스트 제외를 확인한다. `OneHourSyntheticMovementAndCombatReportsActualCompressedSize`의 한 시간은 합성 데이터이며 실제 전체 게임 한 시간의 용량 측정을 대신하지 않는다.

`ReplayUiIntegrationTests`는 기본 기록 활성화·이전 업로드 선택에 관계없는 새 파일의 수집 binding·명시적인 중단 설정 유지·중단 시 큐 삭제도 확인한다. 이 검사는 임시 큐와 로컬 전용 설정을 사용하고 PlayerPrefs를 복원하며 운영 수집 서버에 접속하지 않는다.

Web의 전체화면 ESC 브리지는 `node tools/web/test_browser_fullscreen.cjs`로 검사하며 실제 브라우저 권한·키 입력 검증은 [Web 안내](../delivery/web-build.md#전체화면과-escape)를 따른다.

```sh
'<Unity 6000.6.5f1>/Editor/Unity' -batchmode -nographics -projectPath '<저장소>/unity' -runTests -testPlatform EditMode -assemblyNames Deviant.Replay.Tests -testResults '<저장소>/outputs/replay/tests.xml' -logFile '<저장소>/outputs/replay/tests.log'
```

`-executeMethod Deviant.Replay.Editor.ReplayBuildCheck.BuildSmoke -quit`은 원본 프로젝트·패키지로 최소 Linux Player의 코드 패키징을 확인한다. 임시 빈 씬을 사용하므로 실제 데모 씬·미디어 복원·게임 전체 실행/리플레이 품질을 검증하지 않는다. 기록 활성화/파일 관찰 방법과 실제 남은 범위는 [리플레이 기록](../telemetry/replay-recording.md#10-현재-구현-사용과-제한)을 따른다. 실측 로그/캡처/빌드는 `outputs/replay/`에만 둔다.

## 로그라이트 해금 개편

Unity 6000.6.5f1 EditMode에서 `Deviant.Run.Tests.RogueliteOverhaulTests`, `Deviant.Combat.Tests.ActorDamageExecutionTests`, `ForestCombatV2.Tests.RoguelitePerkIntegrationTests`를 실행한다. 현재 카탈로그의 혜택·잉크/증강 묶음 공급 제외·환급 일회성·공용 리롤 예산·챕터 간 잔여 유지·50% 부활·소수 골드와 스케치 런의 출발 보상 제안·보상 리롤·상점 진열 확장·새로고침 품절 유지를 확인한다. 실제 1챕터 화면 연결은 `ForestCombatV2.Tests.RoguelitePerkGuiTests`가 확인한다. 기존 `ProgressionTests`는 `unity/Tests/Fixtures/roguelite-progression-v1.json`으로 이전 버전의 거래·저장 입력 호환성을 검사하며 현행 해금 목록 검사를 대체하지 않는다.

Unity가 번들한 `Editor/Data/DotNetSdk/dotnet.exe run --project tools/combat/roguelite-tests/RunChecks.csproj --disable-build-servers -p:UseSharedCompilation=false`는 순수 Run·Equipment 코드를 직접 컴파일해 공용 예산·묶음·실패 미차감·환급·출발 보상 제안·상점 진열·무기 교체 ID·파괴 영수증·보관 자원의 시계 보정을 검사한다. 저장소 루트에서 실행하며 빌드 중간 파일은 `outputs/roguelite-overhaul/`에 둔다. 생성기 검사는 `python -m unittest discover -s tools/combat -p test_roguelite_overhaul.py -v`와 생성기의 `--check`를 사용한다.

실제 마을의 구매·출발·귀환은 `UnifiedVillageServicesTests`, `Chapter1DeparturePlanTests`, `StartingVillageLoopTests`, `StartingVillageColouringTests`를 실행한다. 화면 캡처가 포함된 검사는 그래픽 장치가 필요하므로 `-nographics`를 쓰지 않는다. 자동 기능 검사와 최종 난이도·가독성·장시간 플레이 확인을 구분한다.

## 잉크 충전소

`Deviant.Run.Tests.StageRecoveryTests`는 2회 예산·25% 비례 회복·최대 체력·소수 회복·사망/유효하지 않은 입력·전투 시계 간격·독립 챕터 예산·리셋·100/150G 결제·골드 부족 및 재실패 시 미차감·연속 1초 누르기·키 해제/차단/피격 취소·한 번만 완료를 검사한다. `ForestCombatV2.Tests.StageRecoveryStationTests`는 두 실제 절차 생성 씬의 단일 설치·지도 발견/소진 표시·거리/공중/기절/입력 차단/일시정지 거부·실제 길게 누르기·결제·회복 효과의 실제 회복 시 1회 재생과 종료·개발용 전투 리셋를 검사하며, 여러 시드의 실제 생성 지형 collider로 각 구조물 크기에 맞는 안전 바닥 후보도 확인한다. 선택한 스프라이트·Trilinear와 밉맵, 22×24px 공유 F 키캡·갈무리11/12px 폰트·가격 숫자의 굵은 획·캐릭터를 따라가는 실제 회복량의 표시/종료/리셋·반쯤 누른 테두리 진행과 해제 초기화, 접근 안내의 동전 아이콘·2→1→0회·100→150 가격 갱신·일시정지 및 거리 이탈 시 숨김도 확인한다. Unity 6000.6.5f1 EditMode 테스트로 실행한다. 데이터 최신성은 `python tools/combat/export_stage_recovery.py --check`로 확인한다. 실제 조작 가독성·최종 아트·난이도 체감 승인은 별도 플레이 확인이 필요하다.

`Deviant.Combat.Tests.ShieldReviewedIntegrationTests`는 승인 투명 시트의 로딩·알파와 실제 공통 전투 입력을 검사한다. 기본 타격 효과, 0.16초의 정면 근접 패링·0.26초의 창 종료, 전방 0.75unit 연장 투사체 반사와 후방 비반사, 방패 앞면 밀어내기·1회 피해, 보호막30 소진 후 남은 피해감소, 미끼의 지면 배치·벽 거절·파괴/만료 시 고정20피해 1회·교체 무폭발을 포함한다. 실제 씬의 광원·카메라·몬스터별 체감은 플레이 확인 대상이다.

`ForestCombatV2.Tests.RandomStageSwordHitTests`는 `Open Random Stage`와 동일한 `Chapter1Procedural` 씬을 열고 시작 검 도면을 선택한 뒤, 생성된 마을 몬스터와 원래 지형에서 1·2타의 A안 재생·대미지·금색 입자 대체를 검증한다. 숲 테스트 씬 통과만으로 랜덤 스테이지 적용을 대신하지 않는다.

검 A안 접촉 효과는 EditMode의 `ForestCombatV2.Tests.BasicSwordHitFeedbackTests`와 기존 `Chapter1HitFeedbackIntegrationTests`를 함께 실행한다. 실제 기본공격 입력으로 다중 적의 1·2타 방향, 첫타 빗나감 후 2타 명중, 좌측 공격, 일시정지, 반복 Draw, 비활성화·리셋, 다른 무기와 직접 피해의 일반 효과를 검사한다. `python tools/combat/export_sword_hit.py`는 `data/combat/presentation/base_sword_hit.json`의 승인 재생 시간을 Resources에 반영한다. 실행 여부와 결과는 테스트 출력으로 구분한다.

`ForestCombatV2.Tests.Chapter1PlayerBuildGateTests`는 개발 기능 활성·비활성 상태 모두 플레이 GUI 하나만 설치되고 전환 버튼 없이 구형 표시가 억제되는지, 일반 빌드 상태(`DevFeatures` 끔)에서 F5 초기화·디버그 HUD는 숨겨지고 C 입력은 포탈 설치를 시작하는지 검사한다. 포탈 키 안내 문구는 플레이 GUI에서 여전히 숨긴다.

`Deviant.Combat.Tests.LocomotionPlaybackTests`는 형태별 재생 배율 문서의 범위·중복·스키마 거부와, 목록 밖 형태·대시·공격 상태가 1.0으로 재생되는지를 검사한다.

`Deviant.Combat.Tests.WallClimbInputTests`는 공통 전투에서 ↑ 기어오르기·↑ 해제 시 즉시 낙하(미끄러짐·↑ 없는 접촉 붙기 없음), ↑ 없는 Space의 일반 점프, 직접·예약·코요테 벽 차기의 ↑ 조건, 예약 중 ↑ 해제 후 뒤늦은 킥 없음, 맞은편 도착·턱 보조·일방향 면·차단, 실제 키보드 입력을 검사한다. 입력 녹화는 기존 `climbAxis`를 그대로 사용한다. 실제 키보드의 장시간 체감은 수동 확인한다.

`Deviant.Combat.Tests.MeleePushRangeTests`는 실제 일반·특수·탄 입력으로 좌우의 안쪽/가장자리/짧은 사거리 밖, 근접+근접·근접+원거리·사용 불가 슬롯, 6회 연속 타격의 위치 상한, 명중 뒤 이동·장비 변경, 리셋·보스와 전용 밀기 예외를 검사한다. `ActorDamageExecutionTests`는 일반/손맛 밀림의 거리 제한과 0 거리 재피격이 이전 밀림을 덮어쓰면서 피해·경직을 보존하는지, 전용 밀기는 상한을 우회하는지 검사한다. 실제 몬스터별 크기·지상 전진과의 체감은 수동 확인한다.

`Deviant.Combat.Tests.MeleeRearCoverageTests`는 근접 7형태의 기본 17타가 기존 앞쪽 끝을 유지하는지 검사하고, 실제 공통 런타임에서 좌우 방향·겹침·몸 뒤쪽 안/밖의 기본공격과 직접·차지·일반·잔상 특수공격을 실행한다. 공중 배치로 지상 전진 이동과 X 판정을 분리하며, 기본 타격의 중복 피해도 검사한다. 지상 전진 중의 체감과 실제 몬스터별 시각 확인은 수동으로 수행한다.

검증 결과는 해당 변경의 커밋 본문에 실행 환경·명령·검사 범위·통과/실패·미실행 항목으로 기록한다. 로그와 빌드는 Git 제외 `outputs/`에 저장한다. 이 문서는 재현 절차와 기대 동작을 관리한다.

Assets에서 내려간 옛 씬(`CombatV2Arena`, `Chapter1MeadowCombat`, `Chapter1ForestVer2`, `deviant` 등 16개)을 여는 테스트는 `LegacySceneFixture.Open`으로 씬을 연다. 씬이 없고 [보존본](../../../unity/Tests/Fixtures/LegacyScenes/README.md)에만 있으면 실패가 아니라 「건너뜀(Ignored)」으로 보고한다. 기본 EditMode 실행에서 이 테스트들은 검사하지 않는다는 뜻이다. 다시 검사하려면 README 절차대로 필요한 보존본을 원래 경로에 복원한 뒤 같은 테스트를 실행한다.

## 다중 장치 입력

안내 문구는 Unity 6000.6.5f1의 `LocalInputLabelTests`, `GamepadRuntimeTests`, `Chapter1StartingGuideTests`로 검사한다. 키보드·일반 패드·Xbox·DualSense의 실제 액션 이름, 열린 튜토리얼의 장치 전환과 시범 시간 보존, 동작/방향 재설정, 가방 닫기·지도 위치/전체 보기 안내를 확인한다. 정비소 포탈 대사의 원본/Unity 사본은 `python tools/story/export_maintenance_dialogue.py --check`로 비교한다. 합성 입력 검사는 실물 패드의 화면 가독성·전체 런 수동 검증을 대신하지 않는다.

`StartingVillageDashUnlockTests`는 메모리 프로필로 미해금 마을의 1회 제한, 저장 실패 시 미적용·구매 성공 직후 2회 적용, 실제 연속 대시와 첫 사용 기준 공통 회복, 다른 프로필 갱신 시 미충전, 저장된 해금 재바인딩·전투 리셋 보존·프로필 교체/초기화의 1회 복원을 검사한다. `PlayerDashTests`, `ProgressionTests`, `Chapter1DeparturePlanTests`를 함께 실행해 기존 대시·해금 거래·챕터 출발 스냅샷을 확인한다. 실제 저장 파일이나 플레이어의 마법안료는 사용하지 않는다.

`GamepadWorldInteractionTests`는 Unity 6000.6.5f1에서 실제 1챕터의 봉인 도전 시작·8/10/12마리 웨이브 처치·보상 상자·증강 카드 탐색/획득을 합성 DualSense 입력으로 실행한다. R1 유지가 나중의 보상 상자까지 열지 않는지, 보상 선택 중 가방/지도/취소/일시정지가 다른 화면으로 새지 않는지, 확인이 점프로 이어지지 않는지 검사한다. 일반 패드의 잠긴 상자·열쇠 소모·잉크/도면 선택 열림·몬스터 목록이 비어 있을 때의 상호작용, 입력 차단/가방 중 거부, 성공한 상호작용의 단일 소비와 실패 후보의 미소비, 키보드 바인딩 재설정·R1/RB 키캡도 확인한다. `GamepadRuntimeTests`, `DeviceInputIntegrationTests`, 기존 `Chapter1ProceduralMapTests`의 상자/봉인 검사와 함께 실행한다. `GamepadFeedbackOutputTests`는 HID 정보가 없는 DualSense 레이아웃에서도 입력을 유지하고 미지원 출력만 비활성화하는지도 검사한다.

HTML 피드백 튜너는 `python -m unittest discover -s tools/combat -p test_gamepad_feedback_tuner.py -v`로 검증한다. 임시 프로필만 사용해 스키마 1 단발/스키마 2 구간·곡선·쉼·총시간 상한, 원본/생성물 저장, 외부 편집 충돌·저장 도중 변경, 생성물 저장 실패의 원본 복구, HTTP 저장 경계를 검사한다. `node tools/combat/test_gamepad_feedback_tuner.cjs`는 Playwright 브라우저에서 입력 범위·편집·JSON 왕복·실제 HTTP 저장/충돌·파일 모드·반응형 화면과 모의 Gamepad/WebHID의 USB/BT 입력·출력·CRC·정지·전송 실패를 검사한다. 패턴 예시 적용·구간 추가/쉼/복제/이동/삭제·곡선·2초 상한·구형 JSON 가져오기/스키마 2 저장과, 실제 형식의 얼굴 버튼 누름/유지/해제로 쉼 뒤 다음 박자를 포함한 패턴 재생을 확인한다. L2/R2의 독립 누름/해제·양쪽 동시 저항·진동과 트리거의 동시 출력·Options 정지·창 복귀 때 유지 입력의 재생 방지·Bluetooth 최소 입력에서 전체 입력으로의 전환을 확인한다. `FEEDBACK_TEST_PLAYWRIGHT_PATH`로 설치된 Playwright 모듈 경로, `FEEDBACK_TEST_BROWSER_PATH`로 Chrome/Edge 실행 파일을 지정할 수 있다. 트리거 저항 0의 저장·해제 인코딩과 USB/BT에서 누른 채 킥이 끝난 뒤 `Off`가 전송되는지도 확인한다. 모듈이 없으면 `npm install --prefix outputs/dualsense/html-tests --no-package-lock --no-audit --no-fund --ignore-scripts playwright`로 Git 제외 경로에 준비한다. 캡처와 임시 프로필은 `outputs/dualsense/tuner-browser/`에 두며, 실물 촉감·권한 동작 검사는 별도다.

`GamepadFeedbackPatternTests`는 Unity 6000.6.5f1에서 스키마 1 감쇠 보존, 스키마 2의 일정·쉼·증가·감쇠 및 종료, 쉼 뒤 다음 박자, 혼합 형식, JSON 읽기·구간 수/시간/잘못된 곡선의 거부를 검사한다. 기존 `DeviceInputIntegrationTests`도 함께 실행해 일반 전투 이벤트와 정지 경계를 확인한다.

패드 피드백 수치는 `python tools/combat/export_gamepad_feedback.py --check`로 원본 일치를 확인한다. `GamepadFeedbackOutputTests`는 USB/BT 출력 패킷·양쪽 트리거·CRC 독립 기준값·출력 길이 패딩·busy 재시도·이전 패드 해제·일반 패드 진동을 검사한다. `DeviceInputIntegrationTests`의 피드백 검사는 실제 행동 시작·확정 명중/피격·두 슬롯·차지 저항과 일시정지·리셋·포커스·장치 지정·비활성화 뒤 해제를 확인한다. 이들은 합성 장치/명령 검사다. 실물 DualSense를 USB/BT 각각 연결해 공격·피격·L2/R2 스킬·차지 후 놓기·메뉴·Alt-Tab·연결 해제/재연결을 확인하고, 트리거와 진동이 종료되는지 별도로 검사한다. 호환 모터 진동을 오디오 파형 기반 HD 햅틱 검증으로 해석하지 않는다.

[다중 입력 장치 구조](../presentation/ui/input-devices.md)의 `Deviant.Input.Tests`, `CombatInputIngressTests`, `DeviceInputIntegrationTests`, `WallClimbInputTests`, `GamepadRuntimeTests`를 Unity 6000.6.5f1에서 실행한다. 합성 Keyboard/Gamepad/XInput/DualSense의 공통 바인딩·순서·hold·장치 지정/상실·포커스·컨텍스트·override·overflow와 기존 전투 접수를 검사한다. 실제 전투 실행기에서 슬롯별 공격·이동·점프/내려가기·대시와 메뉴 닫힘 프레임을 확인한다. UI 스크롤은 선택 가능한 자식이 없는 긴 설명도 포함한다. 전투 입력 무효화가 UI의 유지·미소비 edge를 끊지 않아야 한다.

`GamepadRuntimeTests`는 현재 타이틀→마을 오프닝/이동/시설과 챕터 1/2에서 도면·전투·B의 대시/취소 구분·가방·지도·일시정지/확인을 패드 이벤트로 실행한다. `StartingVillageFirstVisitInputTests`, `Chapter1StartingBlueprintTests.InteractRequiresANewPressAndNeverAutoCollectsOnContact`, `Chapter1ManualCheckpointTests`도 기존 키보드 회귀로 함께 실행한다. `Tests/Fixtures`는 Assets 밖에 복사하고 미복원 보존 씬은 Ignored로 보고한다. 설정·리소스·패키지는 원본과 같은 격리 프로젝트를 사용한다. 실물 USB/BT·진동/트리거·최종 렌더링/가독성·전체 Player 패키징은 별도 범위다.

## 4버튼 입력·점프 정점 보정

`pwsh -NoProfile -File tools/combat/verify_movement_assist.ps1`은 이동 보정 프로필 값·이전 프로필 유지·착지 예약·Space 유지/조기 해제·가감속 NUnit과 첫/둘째 점프의 끝까지 누름·짧은 탭·상승 중 해제와 착지 전 해제한 예약 점프가 모두 3unit인지와 정점 보정 유지를 순수 C#으로 확인한다. `pwsh -NoProfile -File tools/combat/verify_targeted_input.ps1`은 슬롯별 입력·홀드·예약과 실행 시 준비 상태 재검사를 순수 C#으로 확인한다. `pwsh -NoProfile -File tools/combat/verify_jump_apex.ps1`은 자연 정점·0.06초 한도·스텝 분할·중단·비플레이어 기존 궤적을 확인한다. `verify_combat_v2.ps1 -RulesOnly`도 함께 실행한다. 각 스크립트는 별도 `pwsh` 프로세스에서 실행한다(같은 프로세스의 `Add-Type` 중복 방지). 벽 코요테·실제 Space 뗌·가감속 체감은 Unity에서 확인한다. 이 검사는 Unity 컴파일·실제 키보드 이벤트·카메라·스프라이트 표시를 검증하지 않는다.

Unity 6000.6.5f1에서 A/S·Q/W의 두 슬롯 기본·스킬, 스킬별 유지·해제, Space 길게/짧게 모두 최대 높이3unit·아래+Space·벽타기·벽 코요테·Shift·F5, F 도면 선택·정비소·보스 출구, C/V 포탈, 지상·공중 가감속, 포커스 상실과 가방/일시정지 중 입력 차단을 확인한다. 점프 아트는 `Chapter1ApprovedArtTests.NativeAssetsAndApprovedDurationsAreComplete`와 실제 점프·재점프 중 캐릭터 표시로 확인한다.

## 조명·동적 그림자·반응형 랜턴

[조명 실험장 실행](../presentation/visuals/lighting-and-reactive-props.md)의 바인딩을 준비하고 Unity 6000.6.5f1의 EditMode Test Runner에서 다음을 검사한다. 생성 원본 동기화는 `python3 tools/lighting/export_lighting.py --check`로 별도 확인한다.

- `Deviant.Presentation.Tests.LightingDirectorTests`: 불변 입력·유효성, 소유자 우선순위·동순위 최신 요청·중단 블렌드, 중복 해제·외부 요청 보존, 정지 delta와 독립 깜빡임, 그림자 부드러움·광원/반그림자 감쇠의 불변 캡처와 유한 0~1 범위 검증.
- 같은 director 검사는 부분 가중치의 하위 요청 합성·가중치 변경의 동순위 순서 보존, 상위 전량 요청에 가려진 갱신/0 가중치/반복 표본이 동일 목표의 시간 블렌드를 재시작하지 않는지, 잘못된 가중치와 해제된 핸들의 변경 거부를 포함한다.
- `Deviant.Presentation.Tests.LightingZoneTests`: 안쪽 거리의 smoothstep 가중치, 경계 왕복의 동일 요청 유지·확장 사각형 이탈, 현재 구역으로의 오버라이드 해제, 시간 분할·정지, 소유자별 리셋·해제, 불변 좌표·무효 데이터 거부를 검사한다.
- `Deviant.CombatLab.Tests.LightingZoneIntegrationTests`: 동일 실제 이동의 구역 ON/OFF 전투·진자 결과 일치, 완료된 시뮬레이션 위치 기반 진입/이탈, 경계 반복·정지 중 렌더 재샘플·진단 오버라이드 해제·독립 요청 보존·비활성화/재활성화·F5와 카탈로그의 프로필/ID 검증을 검사한다. 실제 제품 맵이나 시퀀스 실행기 연결 검사가 아니다.
- `Deviant.World.Tests.ReactivePendulumTests`: 충격 방향·감쇠·진폭/속도 상한·시간 분할, 동일 실행/타격 중복 방지, 리셋과 실행 종료 후 기록 해제.
- `Deviant.Presentation.Tests.HangingConnectorCurveTests`: 연결 수치의 불변 캡처·유효성, 양끝 소켓 고정·굽힘 제한·시간 분할·정지·리셋·영 길이.
- `Deviant.CombatLab.Tests.HangingConnectorIntegrationTests`: 사슬/줄 전환의 렌더러 재사용·게임 상태 불변, 실제 공격 뒤 굽힘·정지·리셋·영 길이 복귀, 비중앙 피벗의 실제 부품 스프라이트 소켓 정합.
- `Deviant.Presentation.Tests.WindowLightGeometryTests`: 사각 면적의 평행 스윕·방향·거리, 여러 각도의 유한 볼록 클리핑, 서로 다른 깊이·횡위치에서 그림자 폭·방향이 유지되는 평행 역추적, 자기 윤곽·개구부 뒤 차폐 제외, 무효 입력과 불변 정의.
- `Deviant.Presentation.Tests.WindowApertureTests`: 분리된 개구부 사이의 미조명, 사각 바운드가 아닌 실제 다각형 투과, CW/CCW 정규화·불변 캡처, 오목/퇴화/중복점·조각 예산 초과 거부.
- `Deviant.CombatLab.Tests.WindowLightIntegrationTests`: Freeform은 영역 핸들로만 남고 Native 창문 강도·그림자는 꺼져 있는지, 수신체별 일정한 입사 방향·현재 윤곽 선분, 실내 수신 범위·반복 샘플 캐시, 각도/개구부 조절의 전투 상태 불변, 광원/빛줄기 켜기·끄기와 수명 정리, 여러 창문의 독립 비활성화·파괴·등록 해제와 중복 등록, 기존 프로퍼티 블록 보존, 다섯 번째 등록 거부, 무효 점 쿠키의 이전 상태 보존과 창문 쿠키 거부를 검사한다.
- `Deviant.CombatLab.Tests.WindowLightGraphicsTests`: 빛줄기를 숨긴 상태에서도 창문 방향에 따라 실제 Lit 배경이 밝아지고 반대 영역은 유지되는지, 같은 윤곽의 두 물체가 서로 다른 깊이에서도 벌어지지 않는 그림자를 만드는지, 같은 노말이 위치와 무관하게 같은 입사 방향으로 밝아지는지 검사한다. 같은 평행 차폐가 빛줄기에도 적용되어야 한다. `window-angle-down-light-only.png`, `window-angle-up-light-only.png`, `window-area-shaft.png`, `window-area-steep.png`, `parallel-window-shadows.png`, `parallel-window-shaft-shadows.png`, `parallel-window-normal-direction.png`를 검증 사본 상위 `captures/lighting/`에 저장한다.
- 같은 그래픽 fixture는 네 유리 영역 사이 프레임 틈의 실제 표면·빛줄기 미조명, 두 창문의 누적·독립 소등/해제, 점광원 비직사각 투과 쿠키의 실제 조명 억제·원형 복귀를 검사한다. `opening-*.png`, `two-windows-*.png`, `lantern-cookie-*.png`는 합성 진단 캡처이며 제품 아트나 GPU 성능 승인이 아니다.
- `Deviant.CombatLab.Tests.LightingLabIntegrationTests`: 실제 공통 전투의 몸·근접 활성 스윕, 빗나간 공격의 소품 접촉, 고체 차폐·발동 전 취소, 비차단 이동·순간이동 구분, 일시정지·다른 소유자 보존·리셋·관측 해제와 조명 토글의 전투 상태 불변.
- `Deviant.CombatLab.Tests.LightingLabGraphicsTests`: `-nographics` 없이 실제 URP 2D Renderer에서 노말/평면 픽셀 차이, 플레이어·몬스터 각각의 그림자, 환경광 전환, 실제 근접 접촉 뒤 광원 소켓 이동·화면 변화, 사슬/줄의 서로 다른 픽셀과 굽힘·연결점, 모션·반전·노말 A/B 이후 형상 메시 업로드가 반복되지 않음을 검사한다. 캡처는 검증 프로젝트 상위 `captures/lighting/`에 저장하므로 검증 프로젝트는 Git 제외 `outputs/` 아래에 둔다.
- 같은 그래픽 fixture의 `ZoneDistanceBlendChangesActualAmbientPixelsAndRestoresTheOutsideState`는 국소 광원을 끈 고정 배경 픽셀에서 구역 밖/혼합 폭 중간/중심의 실제 환경광 변화와 이탈 후 원래 RGB 복귀를 검사한다. `zone-outside.png`, `zone-half-blend.png`, `zone-core.png`, `zone-restored-outside.png`는 합성 진단 사본이다.

동일 광원·정지 시각의 환경광만/차폐 없음/하드/소프트 출력을 비교하는 `SoftShadowsProduceIntermediatePenumbraWithoutChangingSpritesOrCombat`은 단일 광원·완전 차폐의 비교 조건에서 소프트 경계에 중간 밝기 픽셀이 실제로 생기는지 확인한다. URP의 반그림자는 하드 그림자의 중심을 유지하고 바깥으로 확장될 수 있으므로, 기존 어두운 영역이 밝아져야 한다고 가정하지 않는다. 이때 Sprite·머티리얼·잉크 외곽선 강도·체력·전투 시각은 유지해야 한다. `hard-shadow-boundary.png`와 `soft-shadow-boundary.png`는 검증 사본의 일회성 캡처이며 최종 제품 미술 승인이나 대상 GPU 성능 검사를 대체하지 않는다.

격리 프로젝트에 원본 Assets·Packages·ProjectSettings와 고정 리소스를 준비한다. Editor·소프트웨어 렌더러의 합성 픽셀 검사는 제품 아트 승인·실제 GPU 성능 기준·전체 게임 플레이나 Player 빌드를 대신하지 않는다. 전체 공격 종류·실제 제작 에셋·제품 맵 구역·시퀀스·청크 언로드 연결은 현재 실험장의 검사 범위가 아니다.

```sh
<Unity6000.6.5f1>/Editor/Unity -batchmode -projectPath <격리프로젝트> -runTests -testPlatform EditMode -testFilter 'LightingDirectorTests|LightingZone|ReactivePendulumTests|LightingLabIntegrationTests|LightingLabGraphicsTests|HangingConnector|WindowLight|WindowAperture' -testResults <outputs/결과.xml> -logFile <outputs/실행.log>
```

## 명중 VFX·잔해·시간·화면 출력

`HitFeedbackGraphicsTests.PaperOutlineColorRendersWithHardPixelEdgesAtNativeAndScaledSizes`는 실제 URP 2D 렌더러의 960×540·1920×1080 출력에서 빨강/파랑 외곽선, 기본 중성 회색 `#1A1A1A`, 밝은 몸체 유지와 이진 픽셀 경계를 검사하고 캡처한다. 합성 실루엣의 픽셀 검사는 실제 44종 전체의 최종 시각 품질 승인을 대신하지 않는다. `HitFeedbackMediaTests.PaperStyleUpdatesPausedMediaAndKeepsOriginalsAndOtherFactoriesIndependent`는 실제 프리팹의 정지 중 설정 변경·재질 캐시 재사용·다른 factory/원본 격리·원래 색 복귀·풀 반환을 검사한다. `HitFeedbackSessionTests.PaperStyleValidatesHexColorsAndCapturesSettings`는 색상 형식과 불변 설정을 검사하고 기존 선택 통합 검사는 F5의 색상 유지와 전투 시각·체력 불변도 확인한다. 수동으로는 F1에서 `#1A1A1A`·다른 색상·잘못된 코드를 적용하고 기본값 복원을 확인한다.

`HitFeedbackMediaTests.OrdinaryHitsDrawFromTheOtherRecipesAndCriticalHitsUseTheCriticalOne`는 평소 43개 후보 도달·같은 seed 같은 그림·치명타의 `hit_blunt` 고정·게임 전역 난수 불변을 검사한다. `GeneratedCatalogContainsEveryLicensedPrefabWithValidExternallySampledMedia`는 전체 바인딩과 지원 매체 계약, `Generated`에 recipe 밖 프리팹이 남지 않았음을 검사하고 기존 배율·풀 재사용 검사는 44개 후보 모두를 순회한다. `HitFeedbackIntegrationTests.OrdinaryHitsDrawRandomPrefabsCriticalHitsTheBigOneAndOtherOutputsSurviveReset`는 10개 무기 형태의 평소 명중이 큰 고리 없이 여러 후보를 쓰는지, 치명타 확률 100%에서 늘 `hit_blunt`인지, `ResetPlayer`로 새 입력 세션이 열린 뒤에도 프리팹을 계속 재생하는지, 외곽선 변경 뒤 기존 Cue 보존·전체 스위치와 점멸/왜곡의 독립성·과거 명중 재생 방지·F5 뒤 치명타 그림 유지를 검사한다. 후보별 화면 크기/종이 화풍은 수동 확인하며, 구조·요청 검사만으로 최종 표시 품질을 승인하지 않는다.

`HitFeedbackIntegrationTests`는 두 피드백 층의 동시 재생, 카메라 오프셋 합성, VFX·왜곡·점멸·파편·사망 슬로우의 독립 끄기/다시 켜기, 전투 리셋에서 스위치 유지, 외부 정지·슬로우 소유권 보존을 검사한다. `HitFeedbackSessionTests`는 VFX 없는 수락의 문맥·만료·중복 검사와 매체 정리 뒤 과거 사건 재생 거부를 검사한다. `CombatFeelFeedbackTests`의 검·채찍 궤적·피격 변주·숫자·처치 회귀도 함께 실행한다. 이전 Game/Lab/들판 씬을 참조하는 검사는 [Legacy fixture 절차](../../../unity/Tests/Fixtures/LegacyScenes/README.md)에 따라 필요한 세 씬을 격리 프로젝트에만 복원하고 검사 후 제거한다. 현재 실행·빌드는 타이틀과 랜덤 스테이지 두 씬을 유지한다.

[에셋 복원·생성](../presentation/visuals/combat-hit-feedback-plan.md#로컬-보유-에셋-복원생성) 후 동일 Unity 6000.6.5f1/패키지 버전의 격리 프로젝트에서 `Deviant.Presentation.Tests`·`Deviant.Control.Tests`·`Deviant.Combat.Tests.HitFeedbackIntegrationTests`를 실행한다. `HitFeedbackGraphicsTests`는 `-nographics` 없이 실제 URP 2D Renderer를 사용해야 한다. `Deviant.Authoring.Tests.LabExecutionTests`도 수동 실행기의 시간 배율 회귀에 포함한다.

`HitFeedbackMediaTests`는 보유 프리팹의 같은 시각/역행/정지·풀 재사용과 로컬 물리의 바닥 충돌·걷어차기·두 세계 독립성을 검사한다. `DebrisBakeTests`는 합성 alpha mask의 면적 보존·UV·반전을 검사하며 실제 종별 아트 승인의 대체가 아니다. 죽음/파편 통합 fixture는 명시 합성 Sprite/분할 바인딩을 주입하므로 제품 몬스터의 캐시 누락을 다른 종으로 숨기지 않는다. `PlayerDeathFeedbackTests`는 Game/Lab 실제 사망·치명 낙사·중복 통지·pause·리셋·외부 시간 소유자 잔류를 검사하며 몬스터 처치가 슬로우를 만들지 않는지도 확인한다. `HitFeedbackGraphicsTests`의 캡처는 검증 프로젝트 상위 `captures/`에 저장한다. 투명 Sprite 왜곡·외부 targetTexture 보존과 명시적인 월드 RT→표시 카메라→HUD 합성의 HUD 픽셀 불변을 검사한다. 이 fixture는 실제 제품 HUD/모든 카메라 스택의 품질을 증명하지 않는다.

`HitFeedbackStressTests`는 600개의 수동 매체 step에 총 7,200개 명중 입력을 넣고 Cue 스코프·파티클 용량·프리팹별 풀 한도를 확인한다. 종료 후 활성 재생과 인스턴스가 남지 않아야 한다. 매체 샘플링의 CPU/관리형 할당 표본은 검증 프로젝트 상위 `profiling/`에 기록하며 실제 게임 GPU 프레임 시간이나 제품 성능 기준 통과로 해석하지 않는다. 관리형 할당 카운터는 사전 probe로 지원 여부를 검사하고 미지원이면 -1을 기록하므로 0을 무할당 증거로 취급하지 않는다. `Deviant.Presentation.Editor.HitFeedbackRuntimeCompile.CompileRuntimeScripts`는 Linux64/Windows64 Player용 스크립트 어셈블리 구성을 검사하며 전체 Player 패키징/플레이 검증의 대체가 아니다.

`python3 tools/combat/test_hit_feedback_import.py`는 합성 아카이브로 원본 해시·경로 이탈·외부 경로·반복 복원·사용자 수정 보존을 검사한다. 유료 아트는 테스트 파일로 배포하지 않는다. `python3 tools/combat/export_hit_feedback.py --check`로 편집 원본과 생성 DTO 일치를 확인한다. 기존 PNG가 없는 경우에는 원본 `unity/`를 열지 않고 Assets/Packages/ProjectSettings 사본을 사용한다.

승인 아트 복원 후 `ForestCombatV2.Tests.HitFeedbackApprovedArtTests`는 실제 검 명중, 6종 양방향 피격과 중복 방지, 실제 죽음 clip 종료 전 미생성/종료 후 생성, 원본 텍스처 파편의 지형 충돌·실제 발 이동에 의한 걷어차기·리셋을 검사한다. `Deviant.Combat.Tests.HitFeedbackForestArtTests`는 Game 씬이 공급한 실제 숲 Sprite 바인딩과 death clip 누락 시 기존 페이드/명시적 미지원 처리를 검사한다. 두 검사는 `RequiresArt`이며 캡처는 검증 프로젝트 상위 `approved-art-captures/`에 둔다. 기존 `Chapter1HitFeedbackIntegrationTests`는 공통 출력만 비활성화한 standalone legacy 경로를 계속 검사한다. 종별 적용 확인과 전 장면 성능·최종 아트 승인은 구분한다.

`HitFeedbackWeaponCoverageTests`는 Game/Lab에서 기존 10형태와 발사기 4변환을 합한 13외형의 실제 기본 공격을 실행한다. 수락된 명중·시작 무기/form/변환 Stamp·Contact·실제 Sprite·공통 VFX 재생을 함께 확인하고 렌더 재샘플로 추가 명중이 생기지 않아야 한다. 공통 숲의 composite 입력 collider는 실제 polygon Composite로 한 번만 변환하는 fixture와 함께 검사한다. `PlayerDeathFeedbackTests`의 F1 패널 검사는 기존 장비 패널을 실제 IMGUI 창에서 렌더해 출력 설정 포트를 확인하며 화면 강도 조정이 피해·정지 소유권을 변경하지 않아야 한다.

모든 명중 왜곡 시험 설정에서는 `HitFeedbackSessionTests`가 왜곡 자격과 강타 카메라 자격의 독립성·세션 설정 복사를 검사한다. `HitFeedbackIntegrationTests`는 기본·특수로 표기된 수락/거절 명중, 중복 방지, 동시 파동 상한·일시정지·만료·리셋을 검사하며, `HitFeedbackWeaponCoverageTests`는 실제 13외형 기본 공격의 왜곡 요청도 확인한다. 왜곡 픽셀·HUD 보존은 별도의 `HitFeedbackGraphicsTests`로 검증하며, 요청 검사만으로 실제 렌더링 통과를 주장하지 않는다.

왜곡 강도 검사는 유한 양수 `waveMaxPixels`의 검증·세션 복사를 포함한다. `HitFeedbackGraphicsTests`는 낮은 상한과 높은 상한의 실제 Sprite 픽셀 차이로 고정 상한에 의한 강도 잘림을 검출하고, 동시 파동의 총 변위 제한·F1 강도 감소를 별도로 검사한다. 실제 게임 화면의 최종 체감 승인을 대신하지 않는다.

## 1챕터 런 흐름

`WeaponGrowthMvpTests`는 알 수 없는 효과 종류·발동·적용 대상·조건과 무효 보호막 상한의 로딩 거부를 검사한다. `WeaponGrowthMvpCombatTests.SwitchShieldUsesTheCurrentGrowthCapIncludingExistingNativeShield`는 현재 성장 상한 10과 기존 스킬 보호막 4에서 새 보호막이 6만 붙고 5초 뒤 만료되는지 확인한다.

`ForestCombatV2.Tests.Chapter1GrowthRuntimeTests`는 실제 `Chapter1Procedural` 런의 정비소·성장 객체로 기본 공격을 실행하고, 캐릭터 +0/+1/+10의 기본·잉크 피해와 슬롯 +0~+5의 추가타 피해를 수치로 비교한다. 처치 누적 공격력·보호막/체력 조건·피격 반격·5처치 회복·쿨타임 회수·기절 지속 피해·상태 수 피해·스위칭 공격·이동 전기장(전기장마다 작은 번개 파티클 3개, 번개 장판 시트 미사용)·대시 적중을 실제 전투 경계에서 검사하며, 범위·사거리·공격 속도는 시작된 공격의 스냅샷 수치로 확인한다. `Chapter1FullRunSmokeTests`는 2챕터 전환 후 동일 성장 객체·캐릭터 +1(1.05배)·동일 잉크·슬롯 +2(추가타 26%)와 새 런 초기화를 검사한다. 렌더 품질·손맛·전체 파츠 조합의 검증을 대신하지 않는다.

EditMode `Deviant.Run.Tests`는 순수 `RunFlow`의 런별 시드, 사망·클리어의 1회 정산, 보스 처치 전 클리어 거부, 챕터 시계 기준 플레이 시간, 새 런의 처치 수 초기화를 검사한다. `ForestCombatV2.Tests.Chapter1RunFlowTests`는 실제 `Chapter1Procedural`·`Chapter1Title` 씬에서 지정 시드 유지, 처치 수 집계, 사망 시 결과 화면·시뮬레이션 정지·중복 정산 거부, 다시 하기의 새 시드·체력·도면 재제시, 타이틀에서 준비한 런의 1회 사용, 보스 처치 전 출구 거부와 클리어 후 2챕터 준비 중 복귀를 검사한다. 결과 화면의 배치·글꼴은 수동으로 확인한다.

`ForestCombatV2.Tests.Chapter1MaintenanceFacilityTests`는 실제 랜덤 스테이지에서 상점·정비소 두 시설이 모두 `정비소`로 표시되고, 적이 비전투여도 시작방·전투방에서는 정비가 거부되며 두 시설 방 안에서만 `EditingAllowed`가 허용되는지 검사한다.

EditMode `Deviant.Run.Tests`의 `RunEconomyTests`·`RunStashTests`는 보상 범위의 양끝 포함과 엘리트 확률, 보스 확정 보상, 항목별 성공 후 가격 상승과 시험 가격표, 잔액 부족 시 무변경, 스탬프 도면 일치·빈 칸 무료 첫 적용·교체 20G·미반환, 같은 효과·잔액 부족·적용 실패 시 무변경, 새 런의 초기화, 스테이지 스탬프 상한(스테이지별 3개·네 번째 거절·2챕터와 새 런의 별도 계수)과 시험 데이터의 상한 3·엘리트/보스 확정 스탬프를 검사한다. `ForestCombatV2.Tests.Chapter1KillRewardTests`는 실제 랜덤 스테이지에서 고정 잉크 드롭표 비활성, 일반 처치의 골드 전용 드롭(6~10G)·거리와 무관한 즉시 지급·동전 한 알의 제자리 가루 전환과 제거·정지 중 연출 보존·중복 지급 방지·다른 보상의 접촉 획득 유지, 엘리트의 골드 200~280G·색상 잉크·스탬프(범위·이동·후속타 중 하나인 스킬 스탬프)와 가방 잉크 없음, 엘리트 1회와 보스 30회 중 스테이지 스탬프 3개까지만 확정 드롭되고 이후에는 골드 등 다른 보상만 남는지, 둔기 도면의 다른 형태(방패·대검)로 제한되는 무기 스탬프를 검사한다. 확정 스탬프의 도면 형태·세 스킬 변형 분포와 도면이 없을 때 스킬 스탬프 대체는 `GuaranteedStampsFollowTheBlueprintOrFallBackToSkill`이 검사한다.

`ForestCombatV2.Tests.Chapter1LootPresentationTests`는 실제 랜덤 스테이지의 잉크·스탬프·코인·안료 그림과 16 PPU·Point 필터·원색 유지, 골드 즉시 1회 지급을 검사한다. `Chapter1LootTossTests`는 드롭 궤적의 시드 재현·드롭마다 다른 흩뜨림·솟는 높이와 튕김 높이·데이터 파일 읽기를 검사하고, 실제 랜덤 스테이지에서 엘리트를 처치해 색상 잉크가 몸에서 출발해 비행 중 획득되지 않고 지형 밖 바닥에 멈추는지 확인하며 `outputs/loot-toss/capture/`에 프레임을 남긴다. `CurrencyPowderPresentationTests`는 마을의 실제 지형에서 골드 가루의 밝기·크기·바닥 반발·정지 보존과 안료 팔레트의 숨김·실제 지급량 표시·상승·소멸, 중복 지급 없음을 검사한다. `Deviant.Chapter2.Tests.Chapter2PigmentDropIntegrationTests`는 실제 2챕터의 안료 1회 지급·팔레트와 +1 표시·소멸, 전투 리셋 뒤 중복 지급·표시 거절, 훈련 런의 지급·표시 없음을 검사한다. [승인 아트와 연출 규칙](../gameplay/equipment/inventory-and-loot.md#데이터에셋-교체)을 함께 확인하며 이 검사 설명은 실행 결과나 최종 수동 가독성 검수를 대신하지 않는다.

`Deviant.Run.Tests.MaintenanceServiceTests`는 강화 가격 트랙(공격 강화·슬롯 강화)의 런 공유 가격, 잔액 부족 시 골드 유지, 강화의 결제 후 지급을 검사한다. `Chapter1ProceduralMapTests.AugmentChoicesAreFreeAndTheBossChoiceWaitsForTheWeaponChoice`는 실제 1챕터에서 봉인 보상의 즉시 무료 선택·일시정지와, 보스 무기 선택이 끝난 뒤에야 열리는 보스 증강 선택, 1챕터 보스만 증강을 주고 마지막 2챕터 보스는 주지 않는 조건을 검사한다. `MaintenanceAugmentsTests`는 무료 증강 제안(안정형 1장·서로 다른 계열 도박형 2장·보유 제외, 후보 부족 시 남은 카드 최대 3장, 모두 보유 시 빈 제안), 1회 보유, 같은 능력치의 합연산과 하한, 성장통의 30%→150% 성장과 선택 전 기준을 검사한다. `ForestCombatV2.Tests.MaintenanceAugmentCombatTests`는 생성 카탈로그 30종(내구도 대기 9종 제외), 주는 피해·기본 공격·스킬 합연산과 맨손 기본 공격, 이번엔 너다·다섯 번째는 진심·제자리 인쇄소의 공격 집계, 받는 피해·회복량 합연산, 최대 체력 증가분 회복·감소 절단과 처치 성장, 반송 봉투의 1회 반사·두 배 피해, 위험한 책갈피의 3초 복귀를 검사한다. `ProgressionTests`는 영구 해금 카탈로그·프로필 검증, 해금 거래·저장 실패·파일 원자 저장·손상 복구, 마법안료 1회 지급, 출발 스냅샷과 시작 골드 1회 지급을 검사한다. `ForestCombatV2.Tests.Chapter1MaintenanceShopTests`는 잉크 판매대 구매(진열 2~3개·3~4등급·서로 다른 파츠·등급별 고정 가격, 구매 뒤 골드 차감·색상 잉크 1개 증가·판매 완료 표시와 재구매 거부)를 포함해 실제 랜덤 스테이지에서 시설 밖 열기 거부, 시설 안 열기·시뮬레이션 정지, 출발 팔레트(기본 3칸, 무작위 무색 없음)·판매대·강화 금액, 스탬프 적용(도면 불일치·같은 효과·잔액 부족 거절, 첫 적용 무료·교체 20G, 형태·특수 변형 변경과 팔레트 유지, 정비소를 닫은 뒤 실제 특수 공격이 `shield_follow_up`으로 시작), 닫기 후 재개, 다시 연 시설의 공유 강화 가격과 같은 진열(판매 완료 유지)을 검사한다. `WeaponGrowthMvpTests`는 출발 팔레트 3/4/5칸·첫 칸 무색, 명시적 파괴의 회수 보상 1회(3개 60G), 무료 교체의 기존 잉크 반환·반복 교체·슬롯 강화 유지, 교체·판매·색 변경의 회수 보상 제외, 두 팔레트 공용 런당 2회·회당 100G 색 변경과 세 번째 거절, 초반 5개 색상 잉크의 팔레트 색 제한(드롭 전용, 색 변경 후 현재 색 기준, 색이 가진 등급만)과 이후 다섯 색 균등, 제단 후보(같은 색·서로 다름·보유 중복 불가형 제외·횟수 미포함)와 보스 색상 잉크 드롭 0개, 잉크 판매대 진열(등급 3~4만·서로 다른 2~3개·잠긴 파츠와 보유 중복 불가형 제외·같은 시설은 같은 진열·초반 색 제한 횟수 미포함)과 구매(등급별 고정 가격·구매 뒤 가격 유지·「판매 완료」·가방 포화와 잔액 부족은 결제 없이 거절), 색상 잉크 판매 8G 고정, 처치 보상의 팔레트 부재를, `WeaponGrowthMvpCombatTests`는 훈련장에서 색상 잉크 54종 중 표식 묻히기(터트리기 파츠가 있을 때만·두 무기 공통), 무지개 표식의 최대 7개·역순 0.08초 연쇄·표식당 기준 피해 15%·마지막 빨강에서의 터트리기 파츠 발동·체력바 숫자 감소, 몬스터가 죽으면 멈추는 연쇄와 번지는 표식의 이웃 이어 가기, 번개 표식의 순번 조건, 붉은 낙관의 표식 피해 증가, 색 번짐의 단독 표식 기능·이웃 표식, 질주 충전의 적립·방출·소멸, 단단한 껍질의 시간 제한 없는 1회 보호막과 쿨타임, 독폭탄의 남은 독 일괄 피해와 쿨타임, 보랏빛 낙인의 독 중첩과 쪽빛 족쇄의 속박, 스위칭 보호막 15와 자동 공격 뒤 표식 터트리기·신호 순서, 사약의 갱신과 슬롯 강화별 최대 중첩·지속 피해, 먹물 늪의 속박(기절 아님)·추가 피해와 잔류 폭발, 티끌 모아 태산의 50회 적중 회복과 비전투 무회복, 전기 장판의 3초 전기장 피해와, 추가 11종의 농묵 공격·받는 피해 배율, 밑줄 긋기의 특수→같은 무기 기본 공격 4초 강화, 내달린 획의 대시 직후 0.5초 강화, 약점 찌르기의 속박 대상 강화, 옮겨 쓰기의 순간 이동·통과 피해와 앞당긴 획의 첫 공격 사거리·범위, 되갚는 잉크의 1회 저장 피해, 갑옷 깁기의 10번째 처치 보호막, 정리 정돈의 처치 쿨타임 감소, 겹쳐 쓰기의 자기 무기 충전 +1, 넘어뜨리기의 대시 기절, 범위 잉크가 몸 중심 대신 켜진 피격 영역의 가장 가까운 점까지 재고 꺼진 피격 영역은 몸 중심으로 대체하는지를 검사한다. `Chapter1InkAltarTests`는 실제 랜덤 스테이지 보스방에서 처치 전 제단 숨김, 처치 뒤 F 키캡·정지·색 선택(변경 불가)·후보 1개 획득·재사용 불가와 고르지 않고 출구로 떠날 때 후보 폐기(후보 수만큼 폐기 효과·효과음)와 획득 시 카드 효과·효과음을, `Chapter1ExitMarkerTests`는 출구 전용 아치·`keycap-f`·열림 효과음 1회를, `StartingVillageColouringTests`는 새 해금 건물이 채색 효과가 끝날 때까지 스케치로 남고 이미 칠해진 건물은 다시 재생하지 않음을, `StartingVillageCaptureTests`는 농가 장면의 인물 표시를, `Chapter1DeparturePlanTests`는 실제 1챕터에서 출발 스냅샷의 도면 2개·무기 1 활성·4칸·무기 1 무색·+100G와 전투 리셋 뒤 재지급 없음을 검사한다.

`ClockExecutionerChainImpactTests`는 실제 랜덤 스테이지와 수동 전투 시계에서 사슬 도착 전 무해·도착 순간 전체 경로 기준 피해 20과 공통 피해 변동 범위의 단일 판정·정지 시각 재샘플 중복 방지·놓친 도착 뒤 유지/감김 접촉 무해·도착 순간 대시 후 지연 피해 없음·큰 틱의 시각 건너뜀·새 패턴의 새 판정을 검사한다. 0.85·1·1.25 패턴 속도에서도 도착 기준을 확인한다. `EmbeddedChainsHoldForOneCombatSecondAtEveryPatternSpeed`는 0.85·1·1.0625·1.25 속도에서 도착 후 전투 시간 1초 직전까지 완전 전개·1초 직후 감김 시작·유지 중 추가 피해 없음·연장된 유지 뒤 회복과 패턴 종료를 확인한다. `Verify Boss Combat`의 대시 비교 역시 도착 때 막은 사슬이 이후 피해를 다시 주지 않는 규칙을 사용한다.

`Chapter1BossClearTests.DebugTravelIntoTheExistingBossRoomPreservesTheRunAndLoadout`는 실제 랜덤 스테이지에서 치트 모드 이동 명령으로 씬 재로딩 없이 보스방 바닥에 즉시 도착하고 속도 정리·카메라 고정·기존 입장 연출이 이어지는지 검사한다. 시드·런 객체·시작 장비·체력·적 목록과 보스 체력 보존, 개발 기능 비활성 시 거부, 기존 직접 호출의 일시정지 중 거부, 치트 모드 명령의 정지·입력 차단 상태 보존도 확인한다.

`Tools > Deviant > Chapter1 > Verify Boss Health Phases`는 보스 체력 전환·처치·사슬 판정 테스트를 함께 실행하고 로컬 결과를 `outputs/clock-boss/health-phases.xml`에 저장한다. `FinalPhaseAccelerationPreservesTheRemainingEmbeddedHold`는 사슬이 꽂힌 도중 HP 700을 넘을 때 진행 위치와 남은 고정 유지 시간이 보존되는지 검사한다.

`ForestCombatV2.Tests.Chapter1BossClearTests`는 실제 랜덤 스테이지에서 보스방의 고정형 시계탑 집행자 1개체(`ClockExecutionerEncounter`의 보스·시험 체력·보스 역할, 보스 더미 없음), 방 진입 전 비교전·진입 시 입장 연출 뒤 교전, 보스 생존 중 출구 거부, 처치 후 보스 처치 표시·보상 골드, 출구 거리 조건과 클리어 결과 화면을 검사한다. `Chapter1BossInfoTests`는 HP 3,500·누적 피해 499/500 경계·HP 701/700 경계·포효 1회·진행 중 가속·네 패턴의 회복 및 대기 제거·재시작 초기화와, seed 42에서 가상 키보드 F로 입장과 2페이즈 포효를 건너뛰는지, 오래 싸워도(300초) 시간으로 쓰러지지 않고 공격 속도가 페이즈 속도 그대로인지(제한시간·시간 기반 광폭화 없음)를 검사한다.

`ForestCombatV2.Tests.Chapter1FullRunSmokeTests`는 타이틀의 새 게임부터 시작 도면 2개 선택, 엘리트 보상·정비, 보스 생존 중 출구 거부, 실제 철창·페이드 뒤 같은 런으로 2챕터에 진입하는 흐름을 검사한다. 1챕터 출구는 런을 정산하지 않고 장비 ID·체력·경제를 유지해야 한다. 현재 마지막 챕터인 2챕터의 보스 처치·출구 정산과 마을 귀환 뒤 새 런의 골드·보관함·보스 상태·도면 초기화까지 확인한다. 화면 입력 대신 화면과 같은 공개 진입점을 호출하며 전투 난도와 화면 배치는 수동으로 확인한다.

```powershell
& '<Unity 설치 경로>/Editor/Unity.exe' -batchmode -projectPath '<저장소>/unity' -runTests -testPlatform EditMode -assemblyNames 'Deviant.Run.Tests;ForestCombatV2.Tests' -testFilter 'Chapter1FullRunSmokeTests|Chapter1RunFlowTests|Chapter1BossClearTests|Chapter1MaintenanceShopTests|Chapter1KillRewardTests|Chapter1MaintenanceFacilityTests|Deviant.Run.Tests' -testResults '<출력>/chapter1-run.xml' -logFile '<출력>/chapter1-run.log'
```

`ForestCombatV2.Tests.Chapter1DropClearanceTests`는 격리한 바닥·벽에서 `DropPoint`가 벽 쪽 펼침을 벽 앞에서 멈추고 열린 쪽은 그대로 두는지, 벽 옆에서 보스 보상을 떨어뜨려도 모든 아이템이 벽 밖 바닥에 놓이는지 검사한다.

`ForestCombatV2.Tests.Chapter1InkStatEffectTests`는 실제 랜덤 스테이지의 정비소에서 힘의 잉크를 가방 명령으로 1개·2개 장착한 뒤 같은 더미에 대한 다음 실제 기본 공격 피해가 장착 전의 ×1.2·×1.44인지 측정한다. `EquipmentStatTests`는 혼합·5개 장착의 곱 중첩 값(피해 ×2.49, 공속 ×1.61, 쿨타임 ×0.33)을 검사한다.

`ForestCombatV2.Tests.Chapter1BagDiscardTests`는 파괴의 선택 없음 거부·첫 누름 확인·다른 선택 시 해제·두 번째 누름 영구 삭제(바닥·골드 없음)와, 선택 없는 버리기 거부, 가방 잉크를 버리면 가방에서 빠지고 발밑에 놓이는지, 그 자리에 서 있는 동안 다시 줍지 않고 벗어났다 돌아오면 다시 줍는지 검사한다. 새 GUI 문구를 추가하면 `python tools/temporary-gui-preview/update-font-glyphs.py`로 정적 Galmuri 글리프를 갱신한다(인벤토리 코드 문구 포함).

`ForestCombatV2.Tests.Chapter1HitVfxBindingTests`는 생성 바인딩이 있을 때 실제 랜덤 스테이지의 전투에 `HitFeedbackBridge`가 붙고 준비 상태가 되는지 검사한다. 배치 실행 전에 `-executeMethod Deviant.Presentation.Editor.HitFeedbackBindingsGuard.EnsureBuilt`로 바인딩을 만든다.

## 1챕터 랜덤 스테이지

`Chapter1StartingBlueprintTests`는 실제 랜덤 스테이지에서 시작 도면 3개와 가능한 6가지 선택 순서, 즉시 슬롯 장착·두 번째 장착 뒤 무기 1 활성 유지(출발 준비대 사전 선택 포함)·중복 선택 거부·세 번째 도면 제거, 새 씬의 재제시와 F5 리셋의 선택 유지, 미선택 무기의 공격/교체 차단과 이동 유지, 지도/상호작용 차단/사망 중 획득 거부를 검사한다. 정식 도면 재고·제작·등급·영구 저장은 이 테스트 기능에 포함하지 않는다. `Chapter1StartingGuideTests`는 seed 42 랜덤 스테이지에서 도면 2개 전에는 오른쪽으로 걸어도 시작방 출구를 넘지 못하고 두 번째 도면 뒤에는 나가는지, 첫 상하 길목의 벽점프·내려가기 안내가 6초 뒤 닫히고 같은 런에서 다시 뜨지 않는지 검사한다.

방·통로 구조는 [블록 맵](../gameplay/world/block-map.md#시험)의 `node tools/map/validate.cjs`(모든 원형·출입구 조합·반전과 배치기 시드의 근사 이동 판정)와 `ForestCombatV2.Tests.BlockMapTests`·`Deviant.Chapter2.Tests.Chapter2BlockLayoutTests`(C#/도구 배치기 일치, 테두리·적 지지면, 조립 정렬, 1·2챕터 데이터 계약, 실제 플레이어 물리의 수직 직결·벽타기 통로)로 검사한다. 자동 판정은 실제 캐릭터의 모든 이동과 전투 난도를 증명하지 않는다.

Unity 6000.6.5f1에서 `Assets/MeadowDuskV3/MapVer2/Scenes/Chapter1Procedural.unity`를 연다. `ForestCombatV2.Tests.Chapter1ProceduralMapTests`의 남은 검사는 봉인 도전방(정비소 뒤 주 경로, 좌우 출입구만)·잠긴 상자방(정비소에서 또는 그 뒤에서 갈라지는 우회)의 자리와 표식 지지면, 주 경로 전투방 2곳의 지워지는 발판 40%(출입구 키트 발판 제외), 시작 바닥의 하향 통과 거부, 보이는 지면의 무피해, 낙하 중 카메라 추적, 멀리 있던 대기 적의 재개를 확인한다. Editor 메뉴 `Tools > Deviant > Chapter1 > Verify Random Stage`는 플레이 중 방과 갈림길 화면을 `outputs/chapter1-procedural/captures/`에 저장한다. 결과 화면을 확인한 뒤 주 경로와 위·아래 갈림길을 직접 왕복하며 점프 가능 높이, 발판 접지, 몬스터 배치와 우회 통과를 확인한다.

`ForestCombatV2.Tests.Chapter1WallTraversalTests`는 절차 생성 씬에서 실제 입력으로 벽 이동 규칙을 검사한다. `WallClimbDetachAndKickVariants`는 지도 밖 공중의 시험 벽으로 ↑ 해제 즉시 낙하·↑ 없는 접촉 비붙기, 기존 1초 스태미나, 코요테·근접 벽 차기의 ↑ 조건, 일반 공중 점프, 꼭대기 보조와 A→B→A 스태미나 재충전을 검사한다. `AlternatingWallsUseBufferedSpaceWithoutAirJumpCharges`는 생성 통로와 같은 폭의 연속 벽 fixture에서 ↑+Space 8회 교차 벽 차기와 도착 후 오르기/낙하를 확인한다. 보관된 구형 씬이 없는 검사는 Legacy fixture 절차에 따라 건너뛴다. 손맛은 실제 플레이로 따로 확인한다.

`pwsh -NoProfile -File tools/combat/verify_combat_v2.ps1 -RulesOnly`는 실제 순수 소유자와 canonical `data/combat/locomotion/player.json`을 컴파일/로드해 점프·자원·복귀 규칙을 실행한다. 이동 schema를 바꿀 때 이 호출부도 함께 이행한다. 이 portable smoke는 Unity 타입 검사·씬 실행·Player 빌드의 대체가 아니다.

문서 변경 후 저장소 루트에서 상대 링크와 폐기 경로를 검사한다.

```sh
python tools/check_docs.py
```

리소스 배치의 경로·버전·충돌 보호를 수정했다면 다음 검사를 실행한다. 원본 바이트 검사와 실제 Unity 실행은 별도로 수행한다.

```sh
python -m unittest discover -s resources/tools -p 'test*.py'
python -m unittest discover -s tools/assets -p 'test*.py'
python tools/assets/update_assets.py --check
```

현재 캐릭터·공격 효과 카탈로그의 배치 계약이나 `.meta`를 변경했다면 `python tools/assets/test_current_art_delivery.py`도 실행한다. 현행 동적 프레임·빌드 필수 이미지의 명시적 선택과 가져오기 메타데이터, 고유 목적지·게임 데이터 출력 금지를 검사한다. 실제 PNG 로딩·씬 실행·Player 빌드는 별도로 검증한다.

음원·폰트·보유 VFX의 배치 계약을 변경했다면 `python tools/assets/test_media_delivery.py`로 이관 매핑·219개 Unity 목적지·원본 바이트·기존 `.meta` 해시·런타임 오디오 카탈로그와 편집 입력의 복원 가능성을 검사한다. 정리 목록의 원본 보존 증거를 갱신해도 이력 말소 승인은 활성화하지 않는다. resources의 `test_import_main_media.py`는 합성 Git 원본으로 미커밋 변경/충돌 보호·읽기 전용 사전 검사·반복 이관과 빈 소비자 복원을 검사한다.

격리 소비자에 메인의 Assets·Packages·ProjectSettings와 `Tests/Fixtures`를 복사하고 고정 resources를 새 얕은 체크아웃/빈 LFS 캐시에서 받아 배치한다. `ForestCombatV2.Tests.MediaSubmoduleImportTests`는 Unity **6000.6.5f1**에서 모든 이관 WAV·폰트·VFX 텍스처가 기존 경로·GUID로 가져와지는지 확인한다. 이 검사는 실제 장치 청취·GUI/글리프·VFX 렌더링 품질이나 Player 빌드를 대신하지 않는다. 이전 오디오 회귀를 실제 실행하려면 위 Legacy fixture 절차대로 해당 씬을 격리 Assets에만 복원하며, 복원하지 않은 회귀는 건너뜀으로 구분한다.

같은 fixture의 `MigratedStationaryBossTextAssetKeepsItsGuidAndDecodesAsPng`는 이관한 `ClockExecutioner/StationaryBytes`의 TextAsset 로딩·기존 GUID·PNG 디코딩을 검사한다. `test_media_delivery.py`는 이 보스와 두 단일 맵 편집기 HTML의 원본/배치 해시·내장 PNG 9장·오프라인 스크립트 구성을 추가 검사한다. 맵 편집기 저장/불러오기·소스/단일 HTML·이전 지형 호환 및 새 검토 HTML 검사는 [편집기 검사](../../../tools/chapter1-map-editor/README.md#검사)를 따른다. Node DOM 모의 검사는 브라우저 렌더링·파일 선택/CORS 확인을 대신하지 않는다.

## 재생성과 검증 실행

저장소 루트에서 필요할 때 실행한다. 검·둔기 문서/데이터를 바꿨다면 생성기부터, 원거리 데이터만 바꿨다면 카탈로그 내보내기부터 수행한다.

```powershell
python tools/combat/build_melee_design.py
python tools/combat/export_unity_catalog.py
python tools/combat/export_attack_motion.py
```

생성기의 타임라인·기하·검5개 원본 대조 검사는 게임 실행 검증이 아니다. 카탈로그 내보내기 후 Unity 컴파일과 Test Runner **EditMode → Deviant.Combat.Tests**를 실행한다. 테스트 내부에서 Play Mode로 전환한다. 실제 실행한 Editor 버전·통과/실패/건너뜀·로그/결과 위치를 남긴다.

순수 규칙과 실제 Unity 타입 검사는 다음과 같이 구분한다. PowerShell 7과 지정 Editor를 사용하고, 원본 Assets/Packages/ProjectSettings를 고정 리소스로 복원한 격리 프로젝트를 준비한다.

```powershell
pwsh -NoProfile -File tools/combat/verify_combat_v2.ps1 -UnityEditor '<Unity 6000.6.5f1 Editor 실행 파일>' -UnityProject '<복원한 격리 프로젝트>'
```

이 도구는 순수 규칙 30개를 실제 Equipment/Combat 소유자와 함께 컴파일·실행한 뒤 **Unity 6000.6.5f1 자체의 Editor/테스트 어셈블리 및 Linux/Windows Player 스크립트 컴파일**을 실행한다. 원본과 같은 프로젝트 버전·Packages manifest/lock 및 C#/asmdef 입력을 요구하며 원본 `unity/`를 직접 검증 대상으로 열지 않는다. `-UnityEditor` 대신 `UNITY_EDITOR` 환경 변수를 사용할 수 있다. 구형 빌드 ZIP이나 추출 DLL 캐시는 사용하지 않는다.

로그 기본 위치는 Git 제외 `outputs/ink_forest/CombatV2Verification/unity-<고유 ID>.log`다. `-LogFile`로 새 경로를 지정할 수 있으며 기존 로그는 덮어쓰지 않는다. 두 Player 대상의 성공 표식과 Editor 종료 코드를 모두 확인하고, 파일·버전 불일치나 성공 표식 누락은 실패다. Editor 플랫폼 모듈이 격리 프로젝트에 SDK/toolchain 의존성을 자동 추가할 수 있으므로 기존 검증 폴더를 재사용하기 전에는 원본 패키지 입력과 차이를 검토한다. 원본 저장소의 패키지 버전을 바꾸거나 검증을 위해 낮추지 않는다. 이 컴파일 검사는 씬/PlayMode·실제 Player 패키징·수동 플레이·청취를 대체하지 않는다.

Editor 실행 없이 순수 C# 규칙만 검사할 수도 있다.

```powershell
pwsh -File tools/combat/verify_combat_v2.ps1 -RulesOnly
```

`-RulesOnly`는 Unity Editor/테스트·Player 컴파일을 건너뛰었다고 출력한다. 이 결과를 Unity 컴파일이나 플레이 검증으로 사용하지 않는다. 전체 검사는 Editor와 격리 프로젝트를 지정하지 않으면 실패한다. `PWSH=<pwsh 경로> python3 -m unittest discover -s tools/combat -p 'test_combat_verifier.py'`는 합성 Editor로 경로·패키지·성공 표식·기존 로그 보호를 검사하며 실제 Unity 검증의 대체가 아니다.

## 수동 확인 기준

아래는 검사 절차·기대 결과다. 자동 검사와 수동 확인을 각각 수행한다.

| 검사 | 기대 결과 |
|---|---|
| 이동·점프·기본 공격 | 이동5, 두 점프 각3, 기본 공격 중 공중 이동5·중력 유지·방향 입력 추종 |
| 근접 지상 전진 | 이동 가능한 형태는 입력이 있을 때 타격별0.65/0.08초, 타격 사이 정지. 망치·대검 제자리 |
| 대시·벽·취소 | 3/0.20초·시작+1초 회복·[0,0.15초) 무적·벽 정지. 점프 취소 수평5/0.10초 |
| 발판·낙사 | 현재 발판만 내려가기, 최대HP10% 피해, T-3 이하 안전 접지 복귀·보호0.60초 |
| 검 | 기본10+10·최단0.20/0.60초, 강베기35/쿨5, 검기50/쿨6, 돌진50/쿨6, 잔상35+35/쿨8 |
| 단검·채찍·레이피어 | 단검4타5/5/5/9, 매듭 적중 후3초 새D·속박0.6, 레이피어360도 탐색·적 정면 이동 |
| 발사기 | D 없음, S4변환. 샷건 공중 후방4를 높이1의 전체 점프 포물선 동안 이동 |
| 수리검 | 기본6/6, 관통24, 폭발24/반경3, 적 뒤 이동·도착5초 복귀, 회전5/적별0.25초 |
| 폭탄 | 기본18/반경1.5, 3연발12씩, 대형30/반경3, 미사일24/반경2·종료마다백점프0.5/0.6 |
| 시한폭탄 | 적 통과, 착지부터1.5초 뒤50/반경3, 미착지5초 공중 폭발. 착지 신관과 비행 수명 분리 |
| 보스·방패 | 3연타 각20·피격 후 공통 무적0. 패링 전방0.30초·근접 반사기준30·탄 반사1회 |
| 독립 효과·교체·리셋 | 발동 위치/대상/원본스탯 유지, 취소 시 직접 다단만 종료. R은 장비 유지·보상 생성 없음 |

형태별 박스·제어·중단·설치 실패는 [검·둔기](../../design/equipment/weapons/forms/melee.md), [원거리](../../design/equipment/weapons/forms/ranged.md), [발사기](../../design/equipment/weapons/forms/launcher.md)의 상세 기대값과 비교한다.

`TimedBombExecutionTests`는 독립 부분 스텝/나머지·비행/신관 종료 여유·부분 착지·늦은 착지·현재 fuse/lifetime 변경·다른 개체/불변 조회·종료/동기 콜백 재진입/예외를 검사한다. `TimedBombIntegrationTests`는 두 씬의 실제 공통 수동 틱에서 비행 만료·착지/늦은 신관·벽/천장/일방향·0 거리/시각·긴 tick·중복 피격·생성 전후 취소/교체/사망/리셋을 실행한다. `-timedBombTraceRoot <outputs 경로>`는 실제 위치/속도·착지/남은 신관·폭발·피해/무기 자원의 float 비트를 기록해 정확한 이전 `BombProjectile`/`BombProjectileTimed` 쌍과 대조한다. 별도의 retained-cleanup 검사는 사망/리셋 직후 yield 없이 옛 참조의 Step/SweepTo가 이동/무장/폭발을 재개하지 못하고 새 폭탄은 정상 동작하는지 확인하며 옛 결함과 동등하다고 주장하지 않는다. 기존 `BombTimedTests`도 함께 실행한다. 전체 탄/효과의 정의 버전·제품 아트/Run·패키징 검증은 아니다.

`TimedBombSweepExecutionTests`는 기존 TimedBombExecution을 내장한 순수 consumer에서 X→Y 질의/쓰기 순서·벽 face/native 거리 최소값과 margin 후 allowed 비교·벽/천장/일방향/적 통과·축 미세 경계·현재 위치/반경/bounds와 콜백·Y 마지막 동률·부분 착지/0 duration·기존 stopped/착지 입구·재진입 독립 후보·예외의 부분 위치/속도/신관을 검사한다. 기존 `TimedBombIntegrationTests`의 `-timedBombTraceRoot`와 `BombProjectileDefinitionIntegrationTests`의 `-bombProjectileDefinitionTraceRoot`는 fixture를 바꾸지 않고 이전 Native에서 얻은 실제 두 씬 상태/float 비트와 대조하며 BombTimed 회귀와 실제 smoke를 함께 수행한다. 순수 consumer는 Native 물리/아트/패키징 검증을 대신하지 않고 별도 착지/경과시계를 만들지 않는다.

`BombProjectileDefinitionTests`는 두 씬에서 생성 뒤 원본의 중력/기하·비행/신관·종류 전환/만료·파편 배열 편집/교체/추가와 설정 변경이 현재 폭탄을 재해석하지 않고 다음 생성만 바꾸는지 검사한다. `BombProjectileDefinitionIntegrationTests`의 20조건은 실제 contact/edge/중복/차폐/총구/일방향·조용한 만료·파편 겹침/비행/만료·timed 비행/착지/늦은 신관/벽/천장·취소/피해 스냅샷/사망/리셋/독립 신관을 실행한다. `-bombProjectileDefinitionTraceRoot <outputs 경로>`로 같은 정확한 이전 폭탄 소비자와 Native float 비트를 대조한다. 일반 CircleCast의 contact margin과 timed wall-face clamp를 서로 같은 정책으로 취급하지 않는다. 원본 편집 결함은 별도 old 실패/new 성공이며 실제 아트/제품 패키징·모든 효과 정의 이행과 구분한다.

`ContactBombExecutionTests`는 절대 만료의 조용한 제거/폭발·마지막 부분 스텝·속도 반영/스윕·현재 시각/종료·콜백 오류의 상태를 검사한다. 기존 `BombProjectileDefinitionIntegrationTests`의 실제 두 씬 접촉/파편/만료와 시한 정책 관측을 같은 fixture의 이전 Native와 비교하며 BombRange/Timed/Launcher 회귀도 함께 실행한다. 접촉 만료는 시한 신관 시계로 바꾸지 않고 실제 아트/패키징 검증과 구분한다.

`ContactBombSweepExecutionTests`는 고체 우선·일방향 하강/법선/바닥 경계·적 제외/단락, 시작 겹침의 비행 선점·0/미세/빈 스윕, Native 순서 strict-less 최근접/동률 첫 후보와 구간 clamp, 질의 뒤 현재 바닥/최종 위치, delta 캡처와 예외의 부분 결과, 기존 stopped와 시한 스윕 일회 위임을 검사한다. 기존 `BombProjectileDefinitionIntegrationTests`와 `-bombProjectileDefinitionTraceRoot <outputs 경로>`의 같은 두 씬 fixture로 이전 Native와 접촉/파편/시한 관측을 비교하고 BombTimed/BombRange 회귀 및 실제 smoke를 함께 수행한다. 순수 consumer 검사는 Native 물리/아트/패키징 검증을 대신하지 않는다.

`BombBlastExecutionTests`는 후보 자격/ID claim 단락·기존 후보 순서/폭발별 독립 중복 제거·claim 뒤 거리/차폐·반경 안팎의 피해와 차폐 단락 평가·현재 대상/소유자/피해/위치/반경·기본 피해→폭발→저작 파편 생성/현재 위치 즉시 스윕→제거와 예외의 부분 결과를 검사한다. 기존 `BombProjectileDefinitionIntegrationTests`와 `-bombProjectileDefinitionTraceRoot <outputs 경로>`의 같은 fixture로 이전 Native와 실제 두 씬 상태/float 비트를 비교하고 BombTimed/BombRange 회귀와 실제 두 씬 smoke를 함께 확인한다. 새 종료/비행 시계를 만들지 않으며 순수 정책 검사와 실제 아트/손맛 확인을 구분한다.

`RangedLaunchExecutionTests`는 기존 PlayerProjectileExecution/HitExecution을 사용하는 체력·claim consumer에서 다섯 수리검의 총구 검사/최근접/일방향 단락·막힌 비폭발 무생성·폭발 clamp/선종료/기록·생성 뒤 현재/명시 원본 무기의 즉시 복귀 대입·레이저 위임·저작 각도 순서/각 탄 즉시 스윕·콜백 뒤 현재 값/독립 재진입·예외의 부분 생성/피해/claim을 검사한다. 폭탄은 각도/원점→생성→파편 배율→현재 소유자 폭의 총구 스윕→같은 개체 반환과 실패의 부분 결과를 확인한다. RangedLaunchIntegrationTests는 실제 본 게임/시험장에서 총구 차단 무생성/즉시 폭발·원본 무기 복귀 예약·생성/접촉 뒤 소유자 변경에도 유지되는 저작 탄도·레이저 피해·폭탄 생성 로그 콜백 뒤 현재 폭과 배율 적용 후 즉시 본체/파편 피해·실제 Receive 오류의 claim/부분 개체를 실행한다. 기존 RangedProjectileDefinitionIntegrationTests의 `-rangedProjectileTraceRoot`와 BombProjectileDefinitionIntegrationTests의 `-bombProjectileDefinitionTraceRoot`는 fixture 변경 없이 이전 Native의 두 씬 상태/float 비트와 대조하고 LauncherArenaTests 및 실제 두 씬 smoke를 함께 확인한다. 실제 아트/손맛·제품 패키징 검증과 구분한다.

`RangedProjectileDefinitionTests`는 생성 뒤 원본의 반복 간격 on/off·폭발 추가가 현재 탄의 타격 ledger/시계/폭발 경로를 바꾸지 않고 다음 생성에만 반영되는지 실제 Play에서 확인한다. `RangedProjectileDefinitionIntegrationTests`는 본 게임/시험장의 겹침/동률/중복·벽/일방향/끝 거리·거친 반복 간격/재진입·폭발 차폐/총구·복귀/원래 무기·취소/교체/사망/리셋·적 탄 반사 등 15조건을 실행한다. `-rangedProjectileTraceRoot <outputs 경로>`의 실제 명령/틱 관측을 정확한 이전 `PlayerProjectile`/복귀 API 소비자로 실행한 기록과 대조할 수 있다. 별도 원본 편집 시험은 진행 중 탄의 거리/기하/관통/경직·반복/복귀/폭발이 시작 값을 유지함을 확인하며 옛 mutable 소스 결함과의 동등성으로 취급하지 않는다. Native 실행 전체의 순수 이행·제품 빌드/아트 검증은 아니다.

`PlayerProjectileExecutionTests`는 거리/시간 분할·적별 접촉 간격/늦은 진입·수명 여유·중복/독립 개체·실제 speed 읽기·스윕/회계/회전/끝 겹침 순서·재진입/예외와 terminal claim을 검사한다. `PlayerProjectileExecutionIntegrationTests`는 실제 두 씬에서 Native 늦은 스윕 진입/재진입·중복/독립 반복 간격을 실행하며 `-playerProjectileExecutionTraceRoot <outputs 경로>`로 실제 비트 관측을 기록한다. 기존 15조건 원거리 trace도 같은 이전 소비자와 대조한다. 별도 Reset/사망 시험은 yield 전에 남은 정상/반복/폭발/복귀 탄 참조의 Step/Sweep/Detonate가 피해나 이동/예약을 만들지 못하고 새 탄은 정상 동작하는지 검사하며, 옛 결함의 실패 전/성공 후를 정상 동등성과 구분한다.

`PlayerProjectileHitExecutionTests`는 순수 fixture와 기존 PlayerProjectileExecution 원본으로 접촉 거부·폭발/고체 종료·claim/Receive 거부·유효 복귀/관통/제거 순서·현재 대상/소유자/원본 무기와 재진입/부분 예외를 검사한다. 폭발은 제거 선확정·중복/후보 자격·기하 거부 전 dedup·거리 여유/차폐·콜백 뒤 현재 기하/피해/소유자·표현/기록 및 예외의 부분 효과를 검사한다. 기존 RangedProjectileDefinitionIntegrationTests의 `-rangedProjectileTraceRoot`와 PlayerProjectileExecutionIntegrationTests의 `-playerProjectileExecutionTraceRoot`는 fixture를 바꾸지 않고 같은 이전 Native의 실제 두 씬 관측과 대조한다. 실제 물리/후보 순서·전체 투사체/폭탄 정책·제품 아트 검증의 순수 이행을 뜻하지 않는다.

`PlayerProjectileSweepExecutionTests`는 기존 hit/수명 원본을 사용하는 순수 기하 consumer에서 실제 체력/대상 순서·고체 우선/일방향·적 제외·수리검 다섯 종류의 최근접/동률 ID·중복/거부 claim·콜백 뒤 현재 자격/위치/속도/나이·clamp 위치와 원래 거리 나이·0/미세/빈 이동·제거 콜백과 독립 재진입 목록·예외의 부분 위치/피해/claim을 검사한다. 기존 RangedProjectileDefinitionIntegrationTests의 `-rangedProjectileTraceRoot`와 PlayerProjectileExecutionIntegrationTests의 `-playerProjectileExecutionTraceRoot`는 fixture를 바꾸지 않고 같은 이전 Native의 실제 두 씬 관측과 비교한다. 순수 consumer 검사는 Native 물리/후보 순서·제품 아트/손맛 확인을 대신하지 않는다.

`LaserHitExecutionTests`는 순수 기하 consumer와 기존 ActorVitalsState로 후보 순서/체력·좌우 끝점·총구/최소 폭·고체/일방향·차폐 전 중복 claim·수신 거부·현재 후보 자격/ClosestPoint·소유자 사망/독립 재진입·예외의 피해/claim/표시/기록 부분 결과를 검사한다. 기존 LauncherArenaTests 레이저/발사기 회귀와 SkillExecutionDefinitionTests의 `-skillExecutionTraceRoot <outputs 경로>`는 fixture를 바꾸지 않고 이전 Native와 실제 본 게임/시험장 무편집 10형태 관측을 대조한다. 순수 consumer 검사는 실제 Physics 후보 순서·두 씬 runtime smoke·제품 아트/손맛 검증을 대신하지 않는다.

`RangedRecoilExecutionTests`는 전체 낮은 점프 구간의 거리 분배·마지막 부분 tick·좌우 반전·고정 지속시간·착지/벽 종료와 다른 실행 독립성을 검사한다. 기존 `LauncherArenaTests`의 실제 공중 샷건 후방4·벽 종료·지상 무반동·발사/기본 강화 회귀와 `SkillExecutionDefinitionTests`의 Game/Lab 무편집 카탈로그 관측으로 Native 모터/중력 결과를 함께 확인한다. 순수 반동 원본 이행은 실제 아트/손맛·전체 행동/효과 순수 실행의 완료를 뜻하지 않는다.

`ForcedDashExecutionTests`는 준비/활성/끝의 부분 구간·긴 tick·비활성/다른 이동 정책·벽 이전/이후·회전 지속·자연 종료와 보스 종료의 cutoff 차이를 검사한다. `ForcedDashExecutionIntegrationTests`는 두 씬에서 실제 다섯 형태의 정상/공중/벽·취소·제어와 주 대상/보스·마지막 스윕/종료 타격 순서를 실행한다. `-forcedDashExecutionTraceRoot <outputs 경로>`로 같은 이전 Native의 위치/속도·타격/자원·행동 관측과 float 비트를 대조할 수 있다. 표식 재입력/순간이동·전체 행동 수명/효과/저작과 제품 아트 검증은 별도다.

`MissileExecutionTests`는 현재 Cast 자격·실제 이동/남은 거리·부분 접촉 claim·이른/새/정확한 수동 입력·종료 콜백 재진입/예외·자동/수동 백점프 시작 차이·현재 시각 읽기·접지 epsilon과 마지막 부분 구간을 검사한다. `MissileNativeTraceTests`는 두 씬의 좌우 최대거리·수동/자동·벽/적/일방향/얇은 벽/바닥 여유·폭발 차폐/중복·공중/벽/천장/착지 반동·취소/사망/리셋·발동 스탯을 실행한다. `-missileExecutionTraceRoot <outputs 경로>`로 실제 이전 Native와 상태/float 비트를 대조하며 기존 `BombMissileTests`의 비행 무적/입력 폐기·종료/백점프/자원 회귀도 유지한다. 전체 행동/효과 실행·실제 아트/손맛·제품 패키징 검증은 별도다.

`ApproachRecastExecutionTests`는 첫 적중/현재 자격·마감/일회 소비·시작 콜백 순서/부분 오류·현재 시각·거리 예산/접촉 종료를 검사한다. `ApproachRecastNativeTraceTests`는 두 씬에서 이른/유지/새/정확한 마감 입력·대상 이동/유실·같은/다른 무기·차폐/부분 이동/최대 거리·취소/교체/정비/사망/리셋과 같은 Cast 재시작/이전 완료 통지를 실행한다. `-approachRecastTraceRoot <outputs 경로>`의 실제 위치/속도·표식·타격/자원 비트를 이전 Native와 대조한다. 입력 예약·자원/전체 Cast 수명·제품 아트/저작은 별도다.

`CastTeleportExecutionTests`는 이동 종류/시간 경계·일회 claim·대상 조건/콜백 순서·재진입/부분 오류를 검사한다. `TeleportNativeTraceTests`는 두 씬의 실제 레이피어 이동/후속타·기존 이동 분기·좌우/상하/뒤 대상·유실/차폐/도착 타격·긴 tick/취소·Receive/로그 재진입을 실행한다. `-teleportTraceRoot <outputs 경로>`로 이전 Native와 위치/타격/자원·claim 결과의 비트를 비교하고 기존 SkillFeedbackR8 회귀를 함께 실행한다. 전체 행동 수명/제품 콘텐츠·아트 검증은 별도다.

`BombReleaseExecutionTests`는 시간 0/발사 마감·긴 tick·현재 시각/차지 shift·입구 취소/생존 검사·생성 전 claim·재진입/예외의 커서 보존을 검사한다. `BombReleaseNativeTraceTests`는 실제 두 씬에서 3연속/큰 tick·발사 전후 취소/독립 탄 유지·발동 피해/정비·기본 폭탄/다른 신관과 사망/리셋을 실행한다. `-bombReleaseExecutionTraceRoot <outputs 경로>`로 이전 Native와 상태/float 비트를 대조하며 기존 BombSpecial/Range/Timed 회귀도 유지한다.

`SmokeEffectExecutionTests`는 효과의 배타적 만료·겹침 갱신/이탈의 0 마감·공격 소비/재진입 차단·다른 은신/새 연막 독립성을 검사한다. `SmokeEffectNativeTraceTests`는 두 씬에서 경계 접촉/분리·이탈/재진입·정확한 만료·공격/새 인스턴스·기본 은신 중첩·취소/교체/사망/리셋을 실행한다. `SmokeExitIntegrationTests`는 확정 명세대로 현재 tick 이탈과 효과 만료에 연막만 해제되고 기본 은신은 유지되는지 확인한다. `-smokeEffectTraceRoot <outputs 경로>`는 현재 상태/float 비트를 기록하며 옛 갱신 여유 결함과의 동등성을 주장하지 않는다.

`SkillWaveExecutionTests`는 사거리/접촉 종료 여유의 안팎·긴/0 이동·현재 방향·이동/타격/종료/표현 순서와 콜백 재진입/취소/부분 오류의 상태를 검사한다. `SkillWaveNativeTraceTests`는 두 씬의 좌우 생성·차폐/얇은 벽/바닥 접선/일방향·늦은 진입/스윕/중복·최대거리/긴 tick·생성 차단·취소/교체/사망/리셋과 Receive 콜백을 실행한다. `-skillWaveExecutionTraceRoot <outputs 경로>`는 실제 이전 Native와 위치/HP/타격/자원/표현 상태 비트를 대조한다. 얇은 벽 fixture는 실제 차폐/이동 제한과 표적 전체가 벽 뒤임을 먼저 검사한다. 전체 Cast/효과 수명과 제품 아트 검증은 별도다.

`CastHitResponseExecutionTests`는 claim/Receive 거부·현재 분류/표식·일반/보스 주 대상·종료 추가타 일회·밀기 현재 사실·재진입/부분 예외를 검사한다. `CastHitResponseNativeTraceTests`는 두 씬의 실제 적 목록/피해/제어로 수신 거부·사망/취소·일반/보스·최종 스윕/종료타·콜백 중 현재 사실 변경과 밀기 순서를 실행한다. `-castHitResponseTraceRoot <outputs 경로>`는 이전 Native와 상태/float 비트를 비교하며 전체 액터/제품 아트 검증은 별도다.

`CastParryExecutionTests`는 시간/전방 경계·현재 scalar 자격·소스 응답 거부/재진입/부분 오류·투사체 무추가피해를 검사한다. `ParryNativeTraceTests`는 두 씬의 0/0.15 인접 시각·앞/뒤/정면 경계/원점 override·현재 위치/방향·반복/취소/소스 유실·발동 배율과 Receive 콜백을 실행한다. `-parryTraceRoot <outputs 경로>`로 이전 Native와 상태/float 비트를 비교하고 기존 적 탄 반사·ActorVitals를 함께 검사한다. 새 방어 콘텐츠/전체 트랙 저작·실제 아트 검증은 별도다.

## 표현·숲·HUD 확인

1챕터 들판 몬스터의 피격 연출은 `ForestCombatV2.Tests`로 검사한다. [별도 에셋 안내](../../assets/README.md)에 따라 원본 6종 이미지와 승인 대기 모션, 들판 씬 리소스를 복원한 프로젝트에서 `unity test unity --mode EditMode --filter ForestCombatV2.Tests --output outputs/chapter1-hit-feedback/results.xml`을 실행한다. 별도 검증 사본을 사용할 때는 위 통합 씬 검사와 같이 `Assets`·`Packages`·`ProjectSettings`를 복사하고 실제 설치된 Editor 버전을 지정한다. 발 기준점·원본 스프라이트 보존 검사와 실제 씬의 피해·넉백·파티클·리셋·표현 파티클의 실제 시간 경과 및 기존 제어 소유자에 따른 일시정지/재개를 함께 검사한다. 수동으로는 양쪽 방향에서 타격하고, 멧돼지 기존 모션 유지·흰 점멸·파편·타격음·연타·일시정지와 재개를 확인한다.

Combat V2 표현의 자동 검사는 `Deviant.Combat.Tests`의 `ConfirmedWeaponTests`에서 모션 로드·접촉 시각·단검4타·이펙트 반전·리셋·무기별 자세를 다룬다. [표현 계층](../presentation/visuals/combat-presentation.md)의 수동 확인도 함께 수행한다.

숲·HUD도 같은 공통 플레이어·전투 구현으로 검사한다. `GameIntegrationTests`로 청크 접지·재사용·실제 HP·입력 차단·일시정지·리셋을 검사하고 [숲 표현](../gameplay/world/forest-prototype.md)과 [HUD 구현](../presentation/ui/hud.md)의 화면 기준은 별도로 확인한다. HUD·아트 생성기는 프리팹과 데이터를 저장할 수 있으므로 단순 실행 확인과 구분한다.

## 통합 씬 검사

[에셋 복원](../../assets/README.md) 후 `Deviant.Game.Editor.GameBuilder.Open`으로 공통 씬을 연다. 기본 숲/HUD 이미지와 R16 캐릭터 이미지가 없으면 빌드 진입점은 오류로 중단한다. 이미지 없는 체크아웃에서 Unity를 열면 고립된 `.meta`를 제거할 수 있으므로 구조 검사는 Assets·Packages·ProjectSettings를 Git 제외 임시 프로젝트에 복사해 수행한다.

```sh
unity test unity --mode EditMode --filter Deviant.Combat.Tests.GameIntegrationTests --output outputs/integration-tests.xml
```

`GameIntegrationTests`는 외부 PNG가 없어도 구조를 검사하도록 테스트 중에만 `loadCharacterArt=false`로 실행한다. 단일 전투 플레이어, 숲 콜라이더 접지·청크 재사용, 실제 HP 공급, 전투 시간 진행·일시정지·리셋·입력 차단을 검사한다. 이것은 모션·화면 검사가 아니다. `CommonRuntimeStructureTests`는 진입 씬·중복 타입 제거·빌드 대상과 이동된 프리팹·데이터의 스크립트 타입 연결을 검사한다. 로그·결과 XML은 로컬 제외 경로에 보관한다. 전투 시험장 테스트도 `CombatTestScene`을 통해 동작 검사에서는 외부 캐릭터 아트를 로드하지 않는다. 실제 스프라이트·피벗·애니메이션을 검사하는 `ConfirmedWeaponTests`와 개별 표현 테스트는 `RequiresArt`로 분류하며 아트를 로드하고 기존 단언을 유지한다. 아트가 없는 환경에서 이 분류만 제외해 전투·게임 통합 회귀를 실행한다. 아래 임시 프로젝트는 원본의 `Assets`·`Packages`·`ProjectSettings`를 복사해 준비한다.

명시적 조립 검사는 미설정 전투가 자동 액터/지형을 만들지 않음, 공급된 생성 요청/환경/입력 순서, 무효 데이터의 콜백/생성 전 거부, 시험장 지형과 공통 틱/리셋·기술 진단 입력 제한을 확인한다. Game 시작은 최종 숲 위치에서 비공격 상태로 생성한 뒤 기존 다음 프레임 활성화/표현 연결을 유지하는지 검사한다. 일반 Windows64 스크립트 컴파일은 CombatLab.Unity/Editor/Authoring를 제외하고 Game.Diagnostics.Unity를 포함해야 하며 시험장 define 컴파일은 DEVIANT_COMBAT_LAB로 Lab 루트를 포함해야 한다. 제품 패키징·실제 이미지 검증과 스크립트 의존성 검사는 구분한다. GameBuilder.BindSceneComposition은 공통 작성 씬을 보존해 바인딩하며 Lab 생성기는 시험장만 재생성한다.

CombatReadModelTests/CombatReadModelIntegrationTests/CombatReadModelIdentityTests는 불변 값·현재 운동/체력/상태/행동/무기 조회·0 피해 수신·조회 중 시계 불변·초기화/리셋 재바인딩·옛 세션/액터·다른 전투/파괴·할당 경계를 검사한다. Game HUD와 SwordDashComparison의 실제 HP/유효 적중/위치 지표는 같은 계약을 사용하며 성공한 틱 뒤 관측한다. 이 관측은 입력 접수나 개별 행동 성공 결과·세계 저장으로 해석하지 않는다.

```sh
unity test outputs/refactor/UnityVerification --mode EditMode --filter Deviant.Combat.Tests --output outputs/refactor/gameplay-tests.xml --timeout 1800 -- -nographics -testCategory '!RequiresArt' -logFile outputs/refactor/gameplay-editor.log
```

결과 XML에서 실제 실행 수·통과·실패와 제외된 표현 범위를 확인한다. 이미지 복원 후에는 `-testCategory` 제한 없이 전체 테스트를 실행하고 수동으로 HUD 배치·ESC 복귀·공격·교체·몬스터 모션·사망을 확인한다.

## Editor 저작 도구 검사

`Deviant.Authoring.Tests`는 임시 디렉터리에서 원본 저장·재로드·생성물 일치·외부 편집 충돌·잘못된 값·생성 실패 후 재시도를 검사한다. 검 검사는 임시 저장소에 복사한 실제 Python 생성기를 실행해 1타만 변경되고 나머지 카탈로그가 유지되는지 확인하므로 Python 실행 파일이 필요하다. 원본 참조 해석은 `python tools/combat/test_melee_source.py`로 별도 검사한다. `RequiresGraphics` 테스트는 실제 Editor 창을 열어 필드/버튼, 변경값 표시, Undo, Play Mode 진입 시 편집 보존과 저장 없는 런타임 적용을 검사한다. 이 분류는 외부 캐릭터 이미지 의존성과 별개다.

MeleeFrameCompilerTests·MeleeFrameFamilyIntegrationTests는 전체 20 변환/0 source contact·양수 구간/공속/전진·고정 공유 비중과 로컬 override 독립·현재/다음 시작·무효 그래프/구조 거부·원자 family 복구·실제 UI/첫검 위임/저장 격리와 거부된 reset의 상태 보존을 검사한다. MeleeFrameNativeTraceTests는 이전/새 Game/Lab 기본/직접 특수·정상/고공속·지상/공중·입력 방향 raw 값을 비교한다. 프리셋 schema 2와 시나리오 schema 8은 같은 compiler/명시 catalog context를 사용하며 옛 형식을 자동 호환하지 않는다. 실제 제품 아트/차지·원거리 FrameTime 완료는 별도다.

완전 source 시나리오는 명시 catalog/hash 선검사·context mismatch 저장 보존·일반/원거리/모션 full payload/group/결합 hash·이전 스키마/누락/첫검 중복 권한 거부, 실제 일반/원거리 A/B 피해·저장 버전 상태/HitTrace 고정·모션 비중 변경의 동일 gameplay·전체 이전 family 복구를 검사한다. 현재 metadata/palette 실패는 변경 전에 거부한다. 실제 body renderer/UI proof와 제품 아트 품질은 구분한다.

`ActionSpecialFrameCompilerTests`·`SpecialFrameCatalog/Channel/PolicyCoverage`는 전체 23 정책의 채널·양수 구간·정확한 고정 투영·명시 로컬 저작·누락/중복/순환/정책 불일치와 수치 우회를 검사한다. `SpecialFrameIntegration/IntentionalEdit/MediaRetention/StartRejection/TailPolicyTests`는 Game/Lab의 시작 고정/다음 후보·무효 시작의 무부작용·리셋/취소·시간 0 패링·본체 뒤 독립 타격과 기존 설치 자격을 검사한다. Source/Preview/BuildValidation은 저장 충돌·결정적 생성·완전 가족 복구·실제 창/Undo/미리보기와 생성물 최신성을 검사한다. `SpecialFrameNativeTraceTests`는 같은 fixture로 23개×세 조건×180tick의 실제 두 씬 payload/float bits/자원/적중/위치/표현을 이전 구현과 비교한다. 기술 Sprite smoke는 제품 아트 검증과 구분한다.

`ExperimentRuntimeProvenanceTests`는 실제 로드 모듈 캡처, 네 질의 설정 불일치의 선택/기록/재생/A-B 거부와 리셋·수동 실행권·HP/행동/입력/세션/위치/시계/정의/파일 보존, 이전 스키마와 누락 거부, 새 Play 세대의 코드 식별과 데이터 전용 변경 독립성을 검사한다. 실제 미변경 소스 컴파일·도메인 재로드·재생과 격리 프로젝트의 실제 DLL 변경 거부는 별도 smoke로 관측한다. 가짜 MVID 벡터의 단위 검사를 실제 컴파일 증거로 취급하지 않는다.

`CombatReadModelTests`·`CombatReadModelIntegrationTests`는 Game/Lab의 실제 두 충전 소유자·분리된 불변 값·장비 없는 적·준비된 조회의 무할당·리셋 세대 거부를 검사한다. `ExperimentObservationTests`는 실제 시작/완료/쿨다운 거부/취소 이유와 자원 소비/미소비, 무타격 결과 차이·반복 trace, 순차 생성 분신의 0 피해 수락과 대상 교체/동일 대상 인과 차이, Native 질의에서 타격 뒤 발생한 예외의 부분 사실·이전 결과/실행권 보존, 실제 창의 자원/결과/실패 표시를 검사한다. 원시 ID 변화와 관계 해시 안정성을 구분하며 HP/로그에서 성공을 추정하지 않는다.

MovementValidationIntegrationTests는 스키마 유효하지만 지형을 통과할 수 없는 후보의 HP/포즈/속도/현재 행동·입력/세션/시계/장비/증명/로드 ID/reset 횟수 보존, 사용자 점프 12/16·중력 30/50·이동 8 수락, 옛 transaction 거부·중첩 작업 차단/복구·성공인데 transaction이 없는 owner 거부를 검사한다. 기존 실제 숲 Native baseline은 바꾸지 않는다.

RangedActionDefinitionTests·ApplicationTests·SourceTests·StartupTests는 전체 15 정책/수치·공유 원본/alias·방어 복사/codec·두 파일 충돌/선택 소유자 저장·생성 일치·실제 창 15개/Undo/scrub/저장·신규 시작/이미 방출한 결과의 버전 고정을 검사한다. RangedActionNativeTraceTests는 같은 fixture로 이전/새 Game/Lab의 15개 접촉/공중 취소/비행 400tick raw payload/float bits/방출/로그를 비교한다. 기술 화면/합성 매체와 제품 아트/밸런스 검증은 구분한다.

유한 수치 검사는 전체 네 정책/내용 분리·stable 숫자 shape·비유한/범위 거부·비활성 provenance 보존과 실제 Game/Lab 피해/돌진 거리/벽 회복·잔상/검기 취소 후 수명·spawn/비행 범위·방어 HP/감소/bypass·원자 거부/현재-다음 고정을 다룬다. Source/Window/Startup/A-B는 선택 저장/실제 네 UI/Undo·생성 실패 재시도·신규 초기화와 두 동일 입력 비교를 검사한다. 기존 Native trace와 기본 ContentHash는 정확 비교하고 의도한 policy SourceVersion 변경은 별도 기록한다.

AuthoredSkillDefinitionTests·ApplicationTests·SourceTests·WindowTests·StartupTests는 열아홉 정책/수치·원자 부분집합·시작/다음 버전, 실제 패링/연막/분신/수직 고정 소비, 단일 원본 저장/생성·충돌/누락·실제 창/Undo/공간 표본·신규 초기화를 검사한다. AuthoredSkillNativeTraceTests는 이전/새 공통 Game/Lab의 실제 기존 API 관측을 비교한다. 다른 원거리 수치/전체 프레임 저작·제품 아트 품질은 별도다.

RepeatingLayoutTests·ForestCommonLayoutIntegrationTests는 절대 주소/해시·같은 세션 창 재사용/복구·프로필 재검증·원본 변경과 던지는 관측자에서도 진행 거부·Game/Lab 같은 원본 입력/trace를 검사한다. ForestLayoutNativeTraceTests는 이전/새 실제 Game의 671행 이동/단차/발판/내려가기/접합/반복/풀 재사용 float bits를 비교한다. 기술 witness는 정식 제품 필수 경로/아트 가독성 검증이 아니다.

MotionSourceRegistryTests·MotionRegistryIntegrationTests·MotionRegistryAuthoringTests는 완전 바인딩/변환 구분·안정 프레임/팔레트·첫검 위임·복사/해시·원자 게시·진행/다음 캡처·원본 저장/생성·실제 창/Undo/공간 표본을 검사한다. MotionRegistryNativeTraceTests는 동일 fixture로 이전/새 Game/Lab의 전체 60바인딩×90tick 숫자/매체 관측을 정규화 없이 비교한다. 제공 아트 품질과 다른 정책의 gameplay FrameTime 완료는 별도다.

ExplicitLoadoutCompositionTests·DialogueHostGenerationTests·GameSessionIntegrationTests는 소유자 누락의 생성 전 거부, 실제 장비/뷰 동일성·사망/개발 리셋, Configure 세대/옛 TryClose 거부와 실제 Game 대화/시퀀스/화면 정리·외부 제어 보존·disable/destroy·프로토타입 공간 capability를 검사한다. 실패 Configure는 새 usable 세대를 남기지 않으며 원본/현재 정의 pin을 보존한다. 실제 Game smoke와 전체 맵/Run/아트 검증은 구분한다.

ChargedMeleeNumericTests·ApplicationTests·SourceTests·PreviewTests·WindowTests·StartupTests는 정책/수치 해시·방어 복사·Resolve float 순서, Game/Lab 유지 중 버전 고정·최소/최대 다음 해제·대상 실패 전 자원 보존·원자 거부, 단일 원본 정렬/파생 주석/충돌·생성 일치, 해제 표본 기하/매체·실제 창/Undo/읽기 전용 scrub·저장 실패 재시도와 신규 초기화 피해를 검사한다. 실제 두 씬 smoke/시각/제품 아트는 별도로 구분한다.

CharacterPresentationReadTests와 기존 읽기/모션/Cue/프레임 검사는 실제 detached Draw·시작 매체/실패 고정·조회 중 전투 불변·카탈로그 교체/제거·역방향 샘플·꼬리/reset을 검사한다. CharacterLocomotionNativeTraceTests는 이전/새 소비자의 전체 Game/Lab 행·float bits·Sprite/클립·색/포즈·차지/접근/벽/사망을 정규화 없이 비교한다. 기술 합성 Sprite와 실제 제품 아트 검증을 구분한다.

JumpGravityNativeTraceTests는 이전 541bb13+같은 fixture와 스키마 2 소비자를 Game/Lab에서 실행해 첫/둘째 점프 높이/정점 시각·모든 액터 중력·산탄총/미사일 반동의 float bits를 비교한다. 의도한 프로필 버전 변경은 별도 검증한다. JumpGravitySourceIntegrationTests/JumpGravityScenarioTests는 열한 필드 Undo/원자 저장/생성 일치·리셋 적용·사용자 속도/중력·거부 시 보존·완전 프로필 roundtrip/옛 스키마 거부를 검사한다. 실제 Editor/Game/Lab smoke와 제품 아트 검증은 구분한다.
DirectMeleeSpecialNumericTests·ApplicationTests·SourceTests·WindowTests·StartupTests는 정책/수치 해시·대칭·방어 복사, 실제 세 ID의 Game/Lab 시작 고정/다음 시작/원자 거부, 단일 원본 저장/재생성/초기 회귀 숫자 비고정·생성 일치, 실제 창/Undo/읽기 전용 공간 샘플링/적용/저장 오류 재시도와 새로운 초기화를 검사한다. 기술 미리보기와 전체 행동/아트/제품 빌드 완료는 구분한다.

CombatHitEventTests·CombatHitIntegrationTests는 불변 사실·독립 cursor/overflow/할당과 실제 Game/Lab 근접/플레이어 탄/폭탄/레이저/파편/잔상/미사일/적/반사/reset을 검사한다. 피해 콜백 실패 전 수락 사실을 기록하며 실험장은 실제 명령 상관·수락 0 피해와 정규화된 사실 재생을 검사한다. 실제 Forest 피격 표시·Lab 상관 지표·화면은 별도 smoke로 관측하고 제품 아트/청취 품질과 구분한다.

HudScreenRoutingIntegrationTests는 실제 Game HUD의 기존 키보드/패드 Cancel·포커스·외부 오버레이/Navigation 제한·열림/닫힘 프레임 전투 차단·재바인딩/비활성화를 검사한다. ScreenRouterTests/ScreenInputBindingTests는 callback 중 snapshot/일괄 닫기/재바인딩·옛 결과 거부와 정상 Poll의 무할당 경계를 검사한다. 실제 HUD 화면 캡처와 제품 아트/가독성 검증은 구분한다.

PlayerActionStartExecutionTests·PlayerFallRecoveryExecutionTests는 live 조회/정의·페이로드 float/참조·순서·거부·예외의 부분 결과, 직접 낙사 피해·사망 단락·이력/시작점·방향 콜백 전후 위치·보호 시각을 검사한다. PlayerStartRecoveryNativeTraceTests는 같은 fixture를 이전/새 소비자로 Game/Lab에서 실행해 모든 형태/기본/변환/특수·공속·예약 시각·payload float bits·자원/명령 관계·tick/로그·낙사 이력을 비교한다. 실제 GUID는 동등한 관계로 기록하고 동작값을 정규화하지 않는다. 실제 두 씬 smoke와 아트/제품 패키징은 별도다.

CombatCommandsTests·CombatCommandsIntegrationTests·CombatAICommandsIntegrationTests와 CombatCommandOutcomesWindowTests는 실제 Game/Lab 우선순위 거부·예약 원인·점프 버퍼/착지/만료·자원 실패·Native 예외·동기 재입력 콜백 취소·reset/옛/타 세션·ring overflow/할당·실제 키보드 및 읽기 창을 검사한다. 실제 Editor 실행과 full regression의 관측 결과는 별도 기능 커밋에 남긴다.

DialogueSourceAuthoringTests는 버전/typed 참조·그래프·저장/재로드/해시 충돌·무효 후보 보존·시작 버전/다음 세션·옛 토큰/조건/명령 실패를 검사한다. 실제 Editor 창의 노드/간선/선택지·Undo·원자 저장·TextAsset 내보내기와 private uGUI 미리보기를 확인하고 Game/Lab의 실제 NpcDialogueHost에도 생성 후보를 연결해 Run/세계/장비가 변하지 않는지 확인한다. 기술 미리보기·가짜 명령 영수증은 실제 제품 대사/보상/서사 API 구현과 구분한다.

ExperimentScenarioTests는 버전/해시/canonical 문화권·방어 복사·원자 저장/충돌·입력 한도를 검사한다. ExperimentSessionTests는 실제 Lab 반복 상태/진단 순서·새 Play 재로드·적용 정의/장비/방향 복구·정상 A/B 형상 차이·0 피해 수신·terrain/content/Native 피격 바인딩의 변경 전 거부·소유권·실제 tick/B초기화 실패 정리·실제 창 저장/새 창 재생을 검사한다. 창 경로는 RequiresGraphics이며 기존 Lab fixture의 기술 저장이지 일반 Game/Run 저장 proof가 아니다.

직접 정의 시나리오는 스키마 3/이전 1·2 거부·누락/중복·ToData/정책/내용 해시 roundtrip과 실제 세 직접 기술의 A/B 피해 +7·시작 버전 고정·저장/재로드/반복 재생·모든 이전 정의 복구를 검사한다. 현재 생성 ID/정책 불일치와 무효 파일은 변경/창 선택 교체 전에 거부하며 지원 ID를 표시한다.

SequenceSourceAuthoringTests는 기존 일곱 종류/ParallelAll 생성자·해시 호환, 버전/필드/ID/시간/앵커/스킵 거부, canonical 원자 저장/충돌/TextAsset, 실제 host 시작 고정/다음 후보/실패 보존·옛 명령, 실제 창/Undo/저장/재로드/private 출력·완료/취소/스킵, private Sprite root와 유실 거부 및 Game/Lab 세계 불변을 검사한다. 생성 기술 미디어·가짜 도메인 영수증은 제품 콘텐츠/아트/Run 구현이 아니며 실제 UI/runtime proof와 테스트 작성은 구분한다.

EffectTrackDefinitionTests는 typed 마커/앵커/형상·Native float 지속시간과 검기 직접 타격 제외·짧은 authored 끝이 실제 검기를 강제 종료하지 않음을 검사한다. EffectTrackIntegrationTests는 Game/Lab 시작 버전·독립 검기 수명·실제 활성화 기준 방어 마감/피해/bypass/갱신·원자 부분집합 보존을 검사한다. EffectTrackAuthoringTests는 선택 저장/다른 행동 보존·실제 생성/빌드 게이트·0-hit 방패/검기 편집·공간 미리보기·동일 입력 A/B를 검사한다. 실제 기존 Native trace 비교와 Editor 표면 실행은 별도 부모 검증이다.

CueSourceAuthoringTests는 의미/정확한 GUID+local-ID 바인딩 해시·원자 저장/내보내기/충돌·typed 매체/Audio 메타데이터·시작/다음 캡처·root 유실·실제 창 Undo/ObjectField·Sprite 시각·Play AudioSource hold/역방향 scrub/재개/정리·명시 게시·Game/Lab private 제어/전투 불변을 검사한다. 실제 청취/믹싱/제품 콘텐츠 검증을 대체하지 않는다.

차지 시나리오는 스키마 4/이전 1~3 거부·전체 차지 payload/해시·누락/중복/정책, 실제 두 기술 최소/최대 held/released 입력·저장/새 창 고정 재생·상태 및 HitTraceHash/명령 시작 상관·A/B 피해 +5/+9·모든 이전 정의 복구와 무효 후보의 변경 전 거부를 검사한다.

전체 트랙 시나리오는 스키마 2/이전 1 거부·누락/중복/부분 inventory·효과/방어 해시 roundtrip·실제 검기 지연 A/B 최초 적중/40 피해·실제 방어 지속시간/실드 만료·현재 편집 뒤 저장 버전 고정/전체 복구·변경 전 현재 catalog 거부·창 선택 보존과 지원 ID 표시를 검사한다.

ActionGeometryPreviewTests는 고정 몸/피격·비균일 공격 박스 좌우 반전, [impact,impact+active) 경계, Sprite subrect/pivot/PPU와 참조 캡처, 17타 전진/선택 기하, 기존 float 순서의 공속 컴파일, 프레임 편집 뒤 같은 후보의 몸/판정 시간, 고정 매핑, 기존 대체/누락 표시와 무효 후보 형상 제거를 다룬다. RequiresGraphics는 네 창 컨트롤과 역방향 스크럽이 실행 시계/cast/위치/HP/적중 수/자원/투사체/Cue/파일을 바꾸지 않는지 검사한다. 제공 Sprite는 기술적 형상/스케일 증거이며 실제 아트 가독성 검증을 대체하지 않는다.

`python tools/combat/test_attack_motion_source.py`는 이미지/Pillow 없이 고정 모션 원본 검증과 영구 프레임 스키마의 구조 운반·단일 원본·잘못된 바인딩·동시 생성물 편집 거부·결정적 게시/재가져오기 합성을 검사한다. Python은 프레임 마커의 의미를 해석하지 않는다. 실제 ZIP/이미지 재가져오기는 별도다. `FixedAttackMotionTests`와 `AttackMotionAuthoringTests`는 안정 ID/방어 복사/해시·환산 프레임, 실제 생성기를 사용한 저장/재로드·다른 클립/이펙트 보존·충돌/실패 보존과 두 씬의 다음 실행 적용/진행 중 버전 보존을 검사한다. `RequiresGraphics` 창 검사는 실제 프레임 필드/환산 시간/diff·Undo·Play Mode 편집 보존·명시적 적용/저장/재로드를 다룬다. 범용 프레임 저작/ActionRunner·실제 아트/제품 빌드는 이 검사와 구분한다.

`FrameBuildFreshnessTests`는 유효하지만 오래된 생성 수치·고정 매체·고정→프레임 전환/이후 프레임 편집의 빌드 거부와 명시적 재생성 뒤 같은 해석 결과의 수락을 검사한다. 공백 변경은 내용 충돌을 만들지 않는다. 실제 `OnPreprocessBuild`도 이 원본 비교를 사용하며 Player 스크립트 컴파일 검사만으로 전체 패키징 전처리 실행을 대신하지 않는다.

FiniteActionTrackDefinitionTests/IntegrationTests는 stable ID/참조/메타데이터·전체 후보·시작 버전·자원/이동/취소/독립 잔상·이전 Native 시간/기하 보존을 검사한다. Source/Window/Comparison/Startup 검사는 원자 저장·충돌·생성 실패 보존·실제 UI/Undo/적용·같은 입력 다섯 A/B 구간·생성 뒤 새 초기화/콜백 전 검증과 무효 그래프 거부를 확인한다. 기존 SkillExecutionDefinition/SkillHitTrack 관측은 같은 두 씬 이전 소비자와 비교하며 잘못된 DTO를 다음 실행에 허용하던 검사는 무효 전체 후보 거부와 유효 다음 트랙 채택으로 전환한다. 실제 미디어/손맛·전체 행동 이행과 구분한다.

`FrameTimelineTests`는 절대 초·스텝 시작/끝·다른 마커+오프셋, 프레임 +30ms/삽입 뒤 안정 ID 참조, 방어 복사·읽기 전용 결과·문화권/마커 선언 순서와 해시, 중복/누락/순환/비유한/음수/진행하지 않는 시간 거부를 검사한다. 고정 모션의 실제 환산 시간을 가져올 때 접촉/종료 보존과 편집 뒤 마커 이동, 0 유지 시간 거부, 일반 마커와 접촉<양수 종료 미리보기 제약도 확인한다. `FrameTimingPreviewTests`는 실제 Editor 창의 +30ms 결과·Undo/Redo·최초 로드 Undo 뒤 모델 유지·순환/무효 편집/재로드, Play Mode 편집 보존과 수동 실행권 아래 전투 시각·위치·HP·행동 정의/원본 불변을 검사한다. 이 창은 편집만으로 실행을 바꾸지 않으며 명시적 임시 적용/리셋/복귀와 파일 저장을 구분한다. `FrameMeleeDefinitionTests`는 접촉/활성/종료/Cue 투영 보존과 +30ms 이동·공속/직접 프레임 시계·기존 Cue 매체 곡선 보존, 누락/무효 활성/Cue/바인딩/클립 종료와 잘못된 공속 거부를 검사한다. `FrameMeleeApplicationTests`는 두 씬의 공통 평지/합성 Sprite에서 고정 공격 진행 중 프레임 적용·다음 공격의 실제 타격 지연/프레임/Cue·동일 피해, 후보 교체/복귀 뒤 시작 버전·공속/점프 취소·리셋 보존, 오래된 수치/모션 후보의 거부와 충돌 편집/A-B의 상태 보존을 확인한다. 실제 창의 별도 적용/리셋/복귀·무효 버튼과 두 원본 파일 불변도 검사한다. 원본 저장/생성·전체 트랙·실제 아트/제품 빌드는 검증하지 않는다. 창 검사는 `RequiresGraphics`를 사용한다.

`FramePresetSourceTests`는 후보/프레임/마커·원본 버전과 해시의 저장/재로드, 숫자/아트 복제 없는 형식, 결정적 BOM 없는 직렬화·문화권/마커 순서 독립, 원자적 생성/교체·외부 수정/삭제·다른 파일/원본 버전 거부와 파일 크기 한도를 검사한다. `FramePresetIntegrationTests`는 실제 창의 저장 대비 편집·Undo/Redo·불러오기 Undo 뒤 원본 해시/기준 시각 일치·Play Mode 파일 연결 보존/충돌·원본 재로드, 잘못되거나 원본이 다른 파일 뒤 후보/실행 보존을 검사한다. 본 게임/시험장은 Play Mode를 닫고 새 전투/창을 만든 뒤 저장 파일만 불러와 명시적으로 적용해 동일 타격 관측 tick·피해/적중 수·타격/종료·버전을 비교한다. 이는 같은 원본 버전의 실험 파일 재사용이며 OS/Editor 프로세스 재시작·제품 자동 로드·빌드용 원본 생성·전체 월드/장비/입력 저장 검증은 아니다. 창 검사는 `RequiresGraphics`를 사용한다.

`FrameSwordDashComparisonTests`는 같은 공통 시나리오에서 프레임 두 버전의 같은 입력/피해·다른 첫 타격/이동 관측, 고정/프레임 혼합 비교·현재 프레임/고정 모드와 기반 수치/장비/이동 복원을 검사한다. B 초기화 중 예외 뒤 프레임 복원·기존 수동 소유자 보존, 잘못된 모션 버전/Sprite 개수의 리셋 전 거부, 저장한 프레임/입력 파일 재생과 실제 창의 완전한 프레임 캡처/Play Mode 보존을 확인한다. 이 결과는 타격/이동/버전/입력 비교이며 실제 렌더 프레임 영상·모션 캡처/손맛·플랫폼 간 결정성은 검증하지 않는다.

`FrameMotionSourceTests`는 고정→영구 프레임 투영의 해석 시각/Cue 매체 곡선 보존, 같은 원본 저장/재로드/결정적 생성·공통 C# 컴파일 결과, 현재 수치의 피해/형상과 프레임 마커의 단일 시간 권한, 외부 수정/삭제·오래된 원본·무효 후보/용량·생성 실패 뒤 보존과 실험 파일 독립성을 검사한다. `FrameMotionBuildValidation`의 생성 JSON 검사는 정상 고정/프레임과 잘못된 그래프/바인딩/중복의 빌드 전 거부를 확인하며 실제 패키징 실행은 아니다. `FrameMotionSourceIntegrationTests`는 생성 JSON을 Start 전에 주입해 본 게임/시험장 각각 두 새 Play 세션에서 자동 활성화·동일 본체/Cue/타격/피해/종료와 버전을 확인하고 잘못된 그래프는 Player 생성/고정 대체 전에 거부한다. 실제 창의 영구 저장/재로드·diff·Undo·충돌/실패 보존과 고정/수치 창의 시간 권한 차단·피해/형상 저장도 검사한다. 선택적 `-frameSourceScreenshot <outputs PNG 절대경로>`는 Editor 창 캡처를 시도하며 실제 생성/화면을 확인한 경우에만 시각 증거로 사용한다. OS/Editor 프로세스 재시작·실제 아트/제품 실행 검증은 별도다.

기본 씬에 직렬화된 빈 모션 카탈로그도 미설정으로 처리해 실제 Resources 생성물을 읽는지 본 게임/시험장에서 검사한다. 이 경로는 시작 전 주입 fixture와 별개이며, 비어 있지 않은 명시적 바인딩이나 무효 생성 소스는 조용히 Resources로 대체하지 않는다.

`CombatSimulationTests`는 UnityEngine 없는 실행기 어셈블리, 기존 갱신 순서·시계 증가 위치, float 누적, 입력 접수 여부, 무효 시간·재진입 거부, 두 리셋 모드와 부분 실패 후 리셋 전 진행 차단을 가짜 월드 포트로 검사한다. 실제 씬 검사는 자동 갱신과 수동 틱이 같은 시계/틱 수를 사용하고, 리셋 콜백 실패 후 성공한 리셋으로 복구되는지 확인한다. 이 검사는 개별 행동/물리까지 순수 계층으로 분리됐음을 뜻하지 않는다.

`CombatInputIngressTests`는 반복 press/release 순서·개별 시각/장치·최신 axis/held·bit-union, 잘못된 액터/세션·순번·메타데이터/용량의 변경 전 거부, KeepTime/NewEpoch의 세대/연속값, bounded overflow/유실, 동기 중재 중 제출/clear, 완료 전 비게시·Consumed와 Completed/Faulted 분리, 소비→사망 폐기 순서를 검사한다. 관측 유무의 gameplay 기록/완료 경로는 초기화 뒤 0 할당을 측정하고 불변 배치는 관측자 poll에서만 만든다.

OrderedInputIntegrationTests는 Game/Lab 합성 키보드로 tick 전 반복 press/release·양쪽 Shift·held/축·Drop 해제를 제출해 순번/시각·한 번 소비와 기존 대시 우선순위를 검사한다. 차단/정지 뒤 edge 미실행·stale 토큰/순번·두 reset·수동 ingress와 Native 오류 뒤 원래 예외/소비→폐기/전체 tick Faulted를 확인한다. LiveInputCaptureTests는 기존 recorder의 실제 소비 poll·불변성·bounded 보관/유실/중지와 A/B 창의 Game 수집 시작/poll/중지/표시를 확인한다. 입력 파일 검사는 Released 포함 스키마 2 왕복/해시·스키마 1 거부를 확인한다. 라이브 관측은 실패 tick의 성공 재생 기록이나 전체 월드 저장이 아니며 실제 장치/아트·제품 패키징 검증은 별도다.

`CombatTickSnapshotTests`는 실제 `CombatSimulation` 단계 실행에서 진입 순서·추가/제거/교체·행동/밀기 재캡처·중복/null·예외 뒤 참조 해제와 독립 월드, 확보 용량 내 7단계 캡처의 0 관리 할당을 검사한다. `ArenaTickSnapshotIntegrationTests`는 본 게임/시험장의 실제 수동 틱과 Receive 콜백에서 목록 변경·파괴 필터·단계별 생성 경계·모든 해당 단계의 자기 제거·기존 분신 만료·부분 예외/리셋·기존 Step/Reset 재진입 거부를 확인한다. `-combatTickSnapshotTraceRoot <outputs 경로>`는 기존 `ToArray` 원본과 같은 fixture의 순서/상태/float 비트를 비교할 관측을 기록한다. 개별 액터/물리·전체 틱의 0 할당이나 실제 아트 검증을 대체하지 않는다.

`MeleeActionRunnerTests`는 불변 시간/형상·공속/해시와 Unity 비의존 경계, 이동 전 활성화→이동 후 타격→마지막 스윕 후 완료, 시간 0/한 tick보다 짧은 행동·활성, 중립/공중 구간의 전진 시간 소비·분수 시각 시작, 대상별/실행별 중복, 취소/콜백 중 취소, 무효/역행/다른 tick 거부, 콜백 오류의 실패/재시도 차단과 재진입을 검사한다. `MeleeActionIntegrationTests`는 본 게임/시험장의 격리된 공통 평지 fixture에서 공통 기본 공격 컴파일러의 같은 payload로 기존 경로와 이행 경로를 각각 실행하고 매 tick의 위치·HP·행동 ID/시작 시각·적중 수를 비교한다. 기본/고공속·공중·연계/분수 시작·타격 전 점프/타격 후 대시 취소·극히 짧은 활성/행동을 포함한다. 이행 1타는 공개 호환 DTO의 시간/형상/피해/전진 편집으로 재해석되지 않고 실제 활성화 때 기본 피해 증가율을 캡처하며, 리셋/사망이 실행을 취소하는지도 확인한다. 실제 물리/피해 적용은 기존 Unity 어댑터이며 다른 기술/전체 트랙의 순수 계층 이행이나 실제 아트 검증을 대체하지 않는다.

`CastLifecycleExecutionTests`는 일반/방출 활성화 경계·실패/취소 자원 순서·현재 차지/시각·완료 claim 전후 재진입/예외·독립 잔존을 검사한다. `CastLifecycleNativeTraceTests`는 두 씬의 시간 0/부분 시작·방출/미사일 0회복·취소/독립 앵커·대상/지상/설치 거부·자원 거부/예외·완료 콜백/차지를 실행한다. `-castLifecycleTraceRoot <outputs 경로>`로 이전 Native와 상태/float 비트를 비교한다.

`CastMotionExecutionTests`는 실제 기존 근접·차지·미사일·접근·반동·강제 대시 실행기를 가능한 경로에서 사용해 우선순위·시간창 경계·첫 부분 구간·공중/중립 기본 이동 소비·직접/차지 방어 제외·추가 무적 없음·현재 사실 재조회와 예외 뒤 부분 결과를 검사한다. 실제 본 게임/시험장 회귀는 `SkillExecutionDefinitionTests`의 `-skillExecutionTraceRoot`와 `CastLifecycleNativeTraceTests`의 `-castLifecycleTraceRoot`로 이전 Native와 대조하며 Game/Lab smoke를 별도로 수행한다.

`CastActivationExecutionTests`는 지상/대상·설치 실패의 미소비, 순간이동 대상 뒤 nearest 덮어쓰기, 현재 바닥·배치 실패와 단락 평가, 자원 거부·오류, 자원→claim→종류별 생성/반동→효과/기록 순서 및 콜백 변이·재진입/예외의 부분 결과를 검사한다. 실제 본 게임/시험장 회귀는 기존 `SkillExecutionDefinitionTests`의 `-skillExecutionTraceRoot`와 `CastLifecycleNativeTraceTests`의 `-castLifecycleTraceRoot`로 이전 Native trace와 비교한다.

`SkillHitTrackExecutionTests`는 시간 0/긴/가변 tick·다단/앵커·현재 자격/차지·대상 claim과 콜백 취소/재진입/예외의 평가 commit 경계를 검사한다. `SkillHitTrackNativeTraceTests`는 두 씬의 직접 다단/잔상/지연 설치/표식 타격·좌우/취소/이동/늦은 진입과 실제 Receive/시각 변경 콜백을 실행한다. `-skillHitTrackTraceRoot <outputs 경로>`로 이전 Native와 위치/HP/타격/자원 비트를 비교한다. 연막 확정 규칙 수정은 수정 후 기준과 구조 추출을 대조하며 옛 이탈 결함과 동등하다고 표현하지 않는다.

`CastTickExecutionTests`는 실제 기존 근접/차지·검기·타격 평가/ledger·수신 응답·액터 HP/상태·연막·강제 대시·수명 실행기를 사용해 생존/디버그 단락·전용 실행의 종료/예외·검기 선행·현재 타격 콜백 뒤 연막 겹침/소비/만료와 마감 clamp·마지막 스윕 뒤 주 대상 종료 추가타·완료 전 평가 commit·예외의 부분 결과와 재진입을 검사한다. 실제 본 게임/시험장 회귀는 기존 SkillExecutionDefinitionTests의 `-skillExecutionTraceRoot`, SkillHitTrackNativeTraceTests의 `-skillHitTrackTraceRoot`, SmokeEffectNativeTraceTests의 `-smokeEffectTraceRoot`로 이전 소비자와 비교하고 두 씬 smoke를 별도로 수행한다. 순수 흐름 추출을 Native 판정/물리 전체 이행이나 제품 아트/통합 완료로 표현하지 않는다.

`CastSweepExecutionTests`는 표본 수/좌표·0/좁은 범위·제외/중복/노출·정렬/nearest·현재 조회와 콜백/부분 오류를 검사한다. `CastSweepNativeTraceTests`는 두 씬의 좌우 정지/이동 스윕·거리/ID/작은 차이·중복/차폐/부분 노출/일방향·nearest/표적 유실/늦은 진입·취소/재진입/예외와 실제 debug Bounds를 실행한다. `-castSweepTraceRoot <outputs 경로>`로 이전 Native와 후보 적용 순서/상태/float 비트를 비교하며 기존 차지/돌진/검기 회귀도 유지한다.

`PlayerTickExecutionTests`는 기존 입력·행동 중재·대시·점프·무기 충전 원본을 사용해 차단/사망·우선순위·무입력 지연 처리·복귀 뒤 현재 입력·콜백마다 행동 변경/제거·공중 기본 이동 예외·벽/대시 종료 수직 정리·일회 착지와 현재 접지 commit·안전 기록/첫 낙사·예외의 부분 결과를 검사한다. `PlayerTickIntegrationTests`는 실제 Game/Lab 바닥/수동 driver로 착지·예약 점프·리셋/낙사·수리검 방문/복귀·Native 질의 콜백의 접지 원본 변경과 착지 뒤 바닥 오류를 실행한다. `-playerTickTraceRoot <outputs 경로>`의 동일 fixture를 정확한 이전 소비자에도 적용해 상태/float 비트를 비교한다. 기존 `PlayerDashIntegrationTests`/`PlayerJumpTests`, `SkillExecutionDefinitionTests`의 `-skillExecutionTraceRoot`와 `ActorMotionIntegrationTests`의 `-actorMotionTraceRoot`, 실제 두 씬 smoke를 함께 확인하며 순수 흐름 이행을 전체 물리/낙사 정책·제품 아트 검증 완료로 표현하지 않는다.

`PlayerComboExecutionTests`는 포함 경계 연계·타수 순환·현재 형태 수의 지연 조회·플레이어 독립/초기화와 시작 기록을 검사한다. `PlayerComboNativeTraceTests`는 두 씬의 근접 7형태/원거리 기본(잉크별 발사기 그림 포함)·고공속/예약·정확한 마감/직후·특수/교체 우선·취소/정비/사망/리셋을 실행한다. `-playerComboTraceRoot <outputs 경로>`는 이전 Native와 상태/float 비트를 비교한다. `PlayerComboFailureBoundaryIntegrationTests`는 Create 오류 전 타수 확정과 실패한 시작/정비의 기록 보존·콜백 순서를 확인한다.

`SwordComboTimelineTests`는 기존 생성 카탈로그 2타의 수치/형상·정의 복사·타격 ID/버전/정책 불일치의 생성 전 거부를 검사한다. 두 씬의 공통 평지에서 기존 2타 실행 경로와 새 실행기를 같은 실제 기본 공격 컴파일러 입력으로 실행해 매 tick의 위치·HP/적중 수·행동/시작 시각을 비교한다. 정상/고공속·공중·반전/분수 시작·예약 후 1타 순환·점프/대시 취소, 실제 1→2타와 새 입력 없는 종료·공속과 독립인 0.30초 연계 여유·특수/교체 우선순위·1타 프레임 후보 뒤 고정 2타·공개 DTO 편집 뒤 시작 정의/발동 피해/전진 유지·로드 카탈로그 항목 유실 뒤 시작 정의 유지·리셋/사망 자원 반환을 확인한다. `SwordComboMotionTests`는 실제 고공속 연계의 tick 중간 2타 시작과 첫 Draw 전 캡처, 본체/Cue 배열 편집/카탈로그 제거·중복 시작 통지·다음 실행 버전, 시작 바인딩 실패 유지와 같은 피해/적중을 합성 Sprite로 확인한다. 2타의 실시간 저작/프레임 모드·다른 무기/기술/전체 트랙·실제 아트/손맛·제품 빌드 검증은 이 범위에 포함하지 않는다.

`BasicMeleeCatalogTests`는 7형태 기본 17타의 기존 수치/형상·ID별 독립 정의·읽기 전용/방어 복사·원거리 정책 분리·공속 해석·누락/중복 형태·무효 타격 시각/기하의 전체 게시 거부를 검사한다. `MeleeBasicFormsIntegrationTests`는 두 씬에서 17타 각각의 정상/고공속·공중·반전/분수 시작·예약·점프/대시 취소를 기존 실행과 같은 실제 컴파일 payload로 실행하고 매 tick의 위치·HP/적중 수·행동/시작 시각을 대조한다. 공개 DTO 편집/카탈로그 타격 유실 뒤 시작 정의·발동 피해/고정 공속·형태별 전진/제자리·리셋 수명과 검 1타 프레임 후보의 타 무기 적용 차단도 확인한다. `MeleeBasicMotionIntegrationTests`는 각 타격의 시작 본체/Cue 참조·첫 Draw 전 편집·카탈로그 유실·중복 시작·다음 실행 버전·시작 바인딩 실패 유지와 같은 전투 결과, 기존 본체 대체/다른 형태 Cue 차단을 합성 Sprite로 검사한다. 실시간 저작/프레임 모드는 여전히 검 1타만 지원하며 특수·원거리·전체 행동 트랙/Actor·실제 아트/손맛/제품 패키징 검증과 구분한다.

`DirectMeleeSpecialDefinitionTests`는 강베기/장침/반월참의 고정 시각/수치·공속 비적용·불변 DTO/해시·없는 정책/무효 입력 거부와 캐스트 생성 전 버전/분류/전체 payload 일치를 검사한다. `DirectMeleeSpecialIntegrationTests`는 두 씬에서 세 기술의 정상/기본 공속 5칸·공중 무입력·반전/분수 시작·공격/교체 예약·발동 전후 대시 취소를 기존 실행과 같은 실제 컴파일 payload로 실행해 매 tick의 위치·HP/적중 수·충전/회복시각·방향·행동/시작 시각을 대조한다. 세 기술의 공중 수평 입력은 현행 수평 0 명세에 맞춰 별도로 검사하며 기존 공중 수평 5 경로와의 동등성으로 주장하지 않는다. 카탈로그/호환 DTO 편집 뒤 시작 시각/형상·발동 스탯·방어/수직 정책·리셋/사망 자원 반환도 확인한다. `DirectMeleeSpecialMotionTests`는 시작 본체 버전/첫 Draw 전 편집·유실·중복/다음 실행·표현 실패/대체와 기본 공격 Cue 비생성을 합성 Sprite로 확인한다. 특수 저작/프레임 적용·다른 정책·실제 아트/손맛/제품 빌드는 아직 후속 범위다.

`ChargePolicyTests`는 순수 참조 방향·최소 대기/상한 자동 해제·중간 피해 보간/회복 지연·분수 시작·두 번째 시계 없는 시각 해석·불변 이전 결과·중복/취소 뒤 해제 차단·무효/역행 시각·정의/해시를 검사한다. `ChargePolicyIntegrationTests`는 두 씬의 레이피어/대검 기본 차지를 조기 해제/중간/상한·기본 공속·발동 전후 대시 취소·교체 예약 8조건으로 각각100틱 실행한다. 시각/위치/속도/피해·충전/회복·타격/종료/행동 시각을 씬별1600행의 float 비트로 관측한다. `-chargePolicyTraceRoot <outputs 경로>`를 지정하면 game.json/lab.json을 기록해 같은 fixture의 이전 `CombatCast` 실행과 대조할 수 있다. 일반 검사는 파일을 쓰지 않는다. 정책 수치의 실행 중 DTO 편집 뒤 시작 해제/피해·발동 스탯, 지연 파괴 전 정책 반환과 해제 사실 보존도 확인한다. 공중 방향 입력은 두 기술의 수평 0 명세로 따로 검사하며 기존 수평 5 덮어쓰기와의 동등성을 주장하지 않는다. 전체 차지 기술의 기하/조건/표현·저작/Run·실제 아트/제품 검증은 아니다.

`ChargedMeleeDefinitionTests`는 기존 레이피어/대검의 정책/기하·nearest/메타데이터·소스/반환 DTO 방어 복사·문화권 독립 해시·미지원/무효 입력 거부와 캐스트 생성 전 분류/버전/전체 payload 검증을 검사한다. `ChargedMeleeIntegrationTests`는 두 씬의 실제 입력으로 시작 정의/발동 피해·원본/공개 DTO hit 배열·기하/이동/효과/방어 편집 뒤 현재 판정 보존·같은 단일 대상/중복·입력 대상 부재와 발동 전 대상 유실의 0.10초 실패 회복/미소비·리셋 반환을 확인한다. 기존 `ChargePolicyIntegrationTests`의 3,200행 관측도 같은 입력으로 대조할 수 있다. 전체 차지 타격 실행의 순수 이행/범용 트랙·차지 저작·실제 아트/제품 검증과 구분한다.

`ChargedMeleeMotionTests`는 두 씬의 실제 입력으로 조기/중간/상한 해제·전용/검/idle 대체를 검사하고, 첫 Draw 전 본체 편집/제거·다음 실행·최초 바인딩 실패·취소/리셋에도 시작 매체와 피해/충전·기본 Cue 비생성을 확인한다. 기존 유지 중 끝 자세와 해제 후 재매핑은 같은 캡처 매체에 현재 차지 시각을 대입해 검사한다. 선택적 `-chargedMotionTraceRoot <outputs 경로>`는 무편집 시나리오의 씬별 2,400 tick 시각/위치·피해/자원·실제 프레임/포즈를 기록해 정확한 이전 표현 소비자와 대조한다. 미디어 변경 버그 수정은 별도 실패 전/성공 후 증거이며 이전 버그와의 동등성을 주장하지 않는다. 실제 차지 아트/저작·전체 판정 실행/제품 검증과 구분한다.

`ChargedMeleeExecutionTests`는 기존 해제 정책과 별도 시계 없는 활성화/타격/완료, 시간 0/긴 tick·반복창의 늦은 대상과 claim·0.10초 실패 회복·콜백 취소/자원 반환/시각 변경·재진입/예외 순서를 독립 기대값으로 검사한다. `ChargedMeleeExecutionIntegrationTests`는 실제 두 씬에서 각 형태의 최소/중간/상한 해제·가변 dt·발동 전후 대상 유실/재진입/nearest 변경·취소/리셋·자원 거부/예외·실제 발동 스탯을 실행한다. `-chargedExecutionTraceRoot <outputs 경로>`는 실제 타격/HP·행동/해제/실패 종료·자원/위치/속도 비트를 기록해 정확한 이전 `CombatCast`/차지 partial 쌍과 대조한다. Native 물리/Receive가 순수 계층으로 옮겨졌거나 범용 전체 행동·실제 아트/제품 통합이 완료된 것은 아니다.

`SkillExecutionDefinitionTests`는 본 게임/시험장의 기존 카탈로그 기본/특수 기술을 같은 Create 입력과 수동 시뮬레이션으로 실행한다. 선택적 `-skillExecutionTraceRoot <outputs 경로>`는 무편집 조건의 위치/속도·HP/타격·행동/효과/발사·자원을 float 비트로 기록하며 이전 소비자와 비교할 수 있다. `SkillExecutionDefinitionIntegrationTests`는 Create에 실제 전달한 DTO/중첩 box·효과/이동/방어·발사/각도/파편 배열을 실행 도중 바꿔 현재 직접/독립 타격·이동/무적·보호/연막/설치·탄/미사일이 시작 입력을 유지하고 다음 Create만 편집을 채택함을 두 씬에서 검사한다. 설치 사례는 Native 고체 설치 거부와 혼동하지 않도록 일방향 지지를 사용하고 자원 발동 성공 뒤 실제 피해/생성을 확인한다. 기존 특수/기본 정의 버전·해시 검사는 공개 가변 캐스트 경로 대신 Create에 전달한 원본을 편집한다. 범용 행동/효과 실행 이행·전체 저작·제품 아트 검증은 아니다.

`ActionResourceTests`는 엔진 비의존 계약/실행기·키/불변 스냅샷·실행당 결과 캐시·잘못된/옛 입력·거부/취소/실패·포트 예외/결과 누락/다른 스냅샷·재진입·반환 뒤 완료 사실·기본/특수 소비 분리·같은 형태의 다른 무기와 기존 순차 충전 원본을 검사한다. `ActionResourceIntegrationTests`는 두 씬의 공통 전투에서 시작 타이밍과 발동 스탯 분리·준비 취소 미소비·발동 후 취소/교체/편집에도 같은 잔상 피해·비활성 무기 회복·자원 거부의 정상 실패·리셋 직후 옛 호출·시간 0 패링 소비·발사기 S 미소비를 검사한다. `MeleeActionRunnerTests`는 명시적 활성화 거부가 타격/완료나 시뮬레이션 예외를 만들지 않는 경계도 검사한다. 무기 자원 원본/전체 Equipment·재고/정비/저장 또는 실제 아트 검증의 증거는 아니다. 범위는 [행동 발동 자원](../gameplay/combat/action-resources.md)을 따른다.

`EquipmentChargeTests`는 Equipment 계약/구현의 UnityEngine·Combat 비참조, 읽기 전용 스냅샷/독립 원본, Equipment 직접 명령과 기존 `ChargePool` 어댑터의 같은 원본 조회, 3회 소모/추가 사용/큰 tick의 순차 회복·1↔3 정비 부담·주기 변경/Reset 호환을 검사한다. `EquipmentChargeIntegrationTests`는 두 씬에서 실제 단검3회 발동/취소·교체 뒤 비활성 회복, 현재 무기의 1↔3 정비에서 부담/회복시각/ID 보존, 리셋과 이전 스냅샷 불변을 확인한다. 전체 무기/재고/Run·저장이나 아트 검증은 아니다. 오프라인 `-RulesOnly`도 실제 Equipment 원본/계약과 호환 어댑터를 함께 컴파일하며 어셈블리 분리는 Unity에서 별도 검사한다.

`EquipmentStatTests`는 순수 참조 방향·5칸 초기/혼합 합산·입력/조회 방어 복사·읽기 전용 목록·무효 단일/일괄 명령의 상태 보존·원본과 인덱스 호환 뷰/Clone·무기 독립성·충전/선택 불변을 검사한다. `EquipmentStatIntegrationTests`는 두 씬의 공통 플레이어에서 시작 공속과 발동 피해의 서로 다른 캡처 경계·정비 쿨타임·발동 후 슬롯 변경·교체/리셋·발사기 기본/비활성 슬롯 경계를 검사한다. 수치 A/B는 분리된 Clone으로 편집을 보관하고 Clear/CopyFrom으로 Equipment 원본을 복원한다. 기존 프레임/독립 효과 회귀도 이 명령을 사용한다. 실제 잉크 아이템/재고/안전 정비 판정·전체 무기/Run 이행과 구분한다.

`EquipmentInstanceTests`는 순수 참조 방향·인스턴스 ID/선택/자식 원본 소유·불변 전체 스냅샷·무효 선택 보존·독립 인스턴스·같은 원본을 읽는 복수 호환 어댑터·기존 스탯 주입을 검사한다. `EquipmentInstanceIntegrationTests`는 두 씬의 실제 발동 키/선택·활성 정비 거부·교체·비활성 무기 정비 부담·리셋 뒤 ID/선택/슬롯 보존과 무기별 충전 소비를 확인한다. 고정/프레임 A/B도 같은 쌍 선택 명령으로 설정/복원한다. 현재 인덱스를 영구 콘텐츠/아이템 저장 키로 사용하는 검증은 아니다.

`EnemyTickExecutionTests`는 실제 EnemyIntentResolver·EnemyAttackExecution·액터 vitals와 이동 대역을 함께 사용해 사망/대상 포착·getter 순서·공격 시작/방향·물리 전 제어 snapshot/물리 후 공격시각·방향 전환 뒤 절벽·공중 추적·비활성/대상 유실 뒤 기존 원거리 공격·보스 긴 tick을 검사한다. Native 목록 후보 단락·늦은 대상/피해 거부 뒤 claim 유지·현재 보스 피해/방향/debug·동기 취소/리셋/새 시작·탐색/바닥/물리/차폐/피해/표시 오류의 부분 결과와 커서 반환도 검사한다. 실제 두 씬은 기존 EnemyIntentIntegrationTests의 `-enemyIntentTraceRoot` 및 EnemyAttackIntegrationTests의 `-enemyAttackTraceRoot`로 fixture를 바꾸지 않고 이전 Native 소비자와 관측/float 비트를 비교하며 Game/Lab smoke는 별도 실제 실행 결과로 구분한다. 순수 검사 작성이나 연결만으로 Native 동등성·제품 아트/플레이 검증 완료를 주장하지 않는다.

`EnemyIntentTests`는 계약/해석기의 Unity·씬/GUI 비참조, 공격·추적 경계/공격 가능 시각·방향/프로필 속도와 기존 미지원 인덱스의 일반 적 호환, 현재 공격/제어/비활성/대상 유실의 새 의도 억제·독립 관측/읽기 전용 결과·무효 관측/의도 거부를 검사한다. `EnemyIntentIntegrationTests`는 두 씬의 실제 임시3종/공통 물리·타격/탄 경로에서 가까운/먼/높은 대상·추적·기절·공격 중 비활성·은신·분신·절벽·피격 취소/반전을 실행한다. 각 씬은 30개 시나리오의 4,500 tick에서 위치/속도/방향·플레이어 HP/적중·예고/탄 수를 관측한다. `-enemyIntentTraceRoot <outputs 경로>`를 명시하면 비교용 game.json/lab.json만 그 경로에 기록하며 일반 검사 실행은 파일을 쓰지 않는다. 이전 `TestEnemy`만 같은 검증 복사본에 넣은 기준 실행과 수정본의 기록을 외부에서 대조할 수 있다. 이는 기존 임시 판단의 분리이며 정식 몬스터 AI/전체 적 행동·아트·제품 플레이 검증이 아니다.

`EnemyAttackTests`는 독립 시각/예고·활성창과 완료 경계, 긴 tick의 보스 타격 순서·원거리 일회 발사, 늦은 대상/실행별 중복, 취소·리셋 뒤 시작/회복 시각, 제어 억제·동기 콜백의 취소/리셋/새 시작/프로필 변경·재진입과 Native 오류 전파를 검사한다. `EnemyAttackIntegrationTests`는 실제 본 게임/시험장에서 세 프로필의 정상/늦은 진입·타격 전후 취소/피격/기절·리셋·제어 억제·은신·긴 tick·패링/반사에 의한 소스 사망을 실행한다. 선택적 `-enemyAttackTraceRoot <outputs 경로>`는 씬별 6,480 tick과 명령 관측의 시각/위치/속도·HP/수신·예고·탄 상태를 float 비트로 기록한다. 같은 fixture에 정확한 이전 `TestEnemy` 소비자만 복구해 실행한 기록과 대조할 수 있다. 일반 실행은 파일을 쓰지 않으며 순수 소비자 연결 확인과 행동 동등성 증거를 구분한다. 정식 적 AI·범용 행동/투사체·실제 아트/제품 플레이 검증은 아니다.

`EnemyProjectileExecutionTests`는 절대 만료/소스 수명·중복 claim·패링의 소스/속도/현재 배율 순서·반사 ledger와 terminal/예외 경계를 검사한다. `EnemyProjectileExecutionIntegrationTests`는 두 씬의 실제 Native 후보/중복·최근접/동률·차폐·벽/일방향·긴 스윕/정지·패링 시각과 소스 사망/유실을 실행한다. 선택적 `-enemyProjectileExecutionTraceRoot <outputs 경로>`의 Game/Lab 기록은 서로 다른 거리의 후보로 비교하며 같은 거리의 승자 ID는 고정하지 않고 별도 행동 검사에서 총 한 타격만 요구한다. `EnemyProjectileRetainedCleanupIntegrationTests`는 사망/리셋 직후 지연 Destroy 전 보관한 반사/일반 적 탄의 Step이 다시 이동/피해를 일으키지 않음을 검사한다. 정식 적 AI·제품 아트·전체 투사체/기기 수명 검증은 아니다.

`EnemyProjectileSweepExecutionTests`는 0/짧은/긴 이동의 양 끝 포함 표본 수·0.1 이하 간격·고체 제한/콜백 뒤 현재 방향·중간 표적 기하·중복 수집/현재 거리 정렬·늦은 자격/차폐·기존 접촉 claim/패링/수신 거부의 조기 반환·벽 종료 여유·독립 재진입 목록과 오류의 부분 결과를 검사한다. 실제 두 씬은 기존 EnemyProjectileExecutionIntegrationTests를 바꾸지 않고 `-enemyProjectileExecutionTraceRoot <outputs 경로>`로 정확한 이전 Native와 위치/속도·피해/접촉/자원 float 비트를 대조하며 동거리 후보는 새 승자를 고정하지 않는 기존 행동 단언을 유지한다. 실제 Game/Lab smoke와 순수 흐름 검사를 구분하고 전체 물리/투사체 정책·제품 아트 검증의 완료로 표현하지 않는다.

`EquipmentLoadoutTests`는 순수 참조 방향·두 무기/활성 슬롯의 실제 원본·명시적 초기 조립/목록 방어 복사·누락/중복 ID/무효 슬롯 거부·읽기 전용/불변 전체 조회·같은 원본을 보는 안정적인 호환 뷰와 반복 교체 중 ID/충전 보존을 검사한다. `EquipmentLoadoutIntegrationTests`는 두 씬에서 기본 공격 종료까지 교체 대기·공격 예약보다 우선하는 교체·특수 준비의 대시 취소/교체 예약 폐기·발동 뒤 원본 무기 ID와 비활성 회복·리셋의 활성 슬롯/인스턴스/잉크 보존·차단 중 교체 입력 폐기를 검사한다. 기존 고정/프레임 A/B의 프로필 설정·복원도 같은 Loadout 명령을 사용한다. 점유된 프로필 쌍의 소유권 이행이며 실제 무기 획득/바닥 교환·재고/저장/Run 정책 검증은 아니다.

`ActorKinematicsTests`는 순수 위치/속도/방향/접촉 원본의 독립성·불변 스냅샷·성분 명령·기존 단계별 리셋과 값/할당 경계를 검사한다. ActorKinematicsIntegrationTests는 실제 동기 Native 출력·Configure/Reset 및 취소 예외의 부분 결과·부모가 있는 루트 그림 별칭/플레이어 제외·외부 Transform 편집이 원본을 바꾸지 않음과 다음 모터 출력 복구를 검사한다. 두 축이 건너뛰어도 원본 X/Y를 출력하고 Z는 Native 뷰 값으로 유지한다. `-actorKinematicsTraceRoot <outputs 경로>`는 씬별 7행의 의도한 권한 변경 회귀이며 이전 Transform 원본과의 동등성을 요구하지 않는다. 기존 Motion/Motor/Facing/Dash/Skill/Return/Recovery 관측은 이전 값/float 비트와 대조한다. Facing의 명시적 SetPosition 깊이 설정은 기존 81행/씬과 0/반복 반전·루트 별칭 사례를 유지한다. 실제 아트·전체 Actor/Run 저장 및 플랫폼 간 결정성 검증과 구분한다.

`ActorMotionTests`는 순수 계약/참조 방향·읽기 전용 입력/결과·밀림/잠금/강제 이동/사망/분신의 우선순위와 만료 경계, 독립적인 작은 수치의 상승/정점/정점 통과/하강/접지·수직 고정 예제·무효 입력/비유한 결과 거부를 검사한다. `ActorMotionIntegrationTests`는 두 씬의 실제 공통 액터/모터에서 걷기/점프/낙하/고정·밀림/잠금/속박/기절/실제 강제 이동·사망/분신·벽/천장·일방향 발판 내려가기 13조건을 각120틱 실행한다. 시각/위치/속도의 float 비트와 접지/벽 접촉을 씬별1,560틱 관측하며, `-actorMotionTraceRoot <outputs 경로>`를 지정하면 game.json/lab.json을 기록한다. 같은 fixture에 이전 `CombatActor`만 넣은 기준 실행과 대조할 수 있다. 이는 같은 Editor/고정 틱의 모터 보존 검증이며 플랫폼 간 결정성이나 Actor/지형 질의·피해·Run 전체의 순수 이행을 의미하지 않는다.

`ActorMotorExecutionTests`는 실제 기존 모션/접촉 해석기를 사용해 만료/발판/접지·X→Y·최종 동기화 순서, 현재 몸/질의/속도 변경, 부분 오류와 skipped/contact 결과를 검사한다. `ActorMotorNativeTraceTests`는 두 씬의 실제 Actor에서 X 적용/벽 확정 뒤 Y 오류·접지 콜백·X 콜백의 몸/질의 변경·건너뛴 Y의 상태 보존·낙하/상승 접촉을 실행한다. `-actorMotorTraceRoot <outputs 경로>`는 이전 Actor와 부분 상태 비트를 비교하며 기존 ActorMotion/Placement/Platform 및 PlayerDash 회귀를 함께 실행한다. 포즈 저장/전체 액터 상태·플랫폼 간 결정성 검증은 별도다.

`ActorFacingExecutionTests`는 0/부호·벽 중심 보정과 live 질의·재진입/부분 오류·적/플레이어 뷰 순서를 검사한다. `ActorFacingNativeTraceTests`는 두 씬의 실제 플레이어/적 좌우 벽·비대칭 오프셋·0/특이 정수/반복 방향·컴포넌트 부재/동일성/alias·현재 질의/오류를 실행한다. `-actorFacingTraceRoot <outputs 경로>`로 이전 Native와 기하/상태/float 비트를 비교하며 실제 아트 품질이나 전체 포즈 상태 이행을 뜻하지 않는다.
`WorldHitQueryRulesTests`는 현재 후보/거리·strict-less/근사 동률·반경 경계·노출 교집합/좁은 구간/순서와 호스트 예외의 부분 결과를 검사한다. `WorldHitQueryNativeTraceTests`는 두 씬의 차폐/일방향·동률/근사 후보·반경 ULP·대형 적 노출/경계·실제 Receive 중 재조회와 위치/기하 변경을 실행한다. `-worldHitQueryTraceRoot <outputs 경로>`로 이전 Native와 선택/노출/float 비트를 비교하며 별도 물리 엔진이나 제품 가독성 증거는 아니다.

`ActorVitalsTests`는 순수 참조 방향·감소/보호막/HP의 순서·0/완전 흡수 수신·우회/엄격한 만료·초과 피해/사망 후 거부·불변 이전 조회/원본 독립·리셋/기존 최대/감소율 의미·무효 명령의 상태 보존을 검사한다. `ActorVitalsIntegrationTests`는 두 씬의 실제 공통 플레이어와 취소 관측 액터로 보호/우회·0 피해 제어·보스 제어·치명타와 연속 취소의 상태 관측 순서·만료/리셋·플레이어 잠금/재피격·대시 무적/패링·탄 피격 구분을 확인한다. `-actorVitalsTraceRoot <outputs 경로>`를 명시하면 수치/제어 상태 비트와 콜백 당시 상태를 game.json/lab.json으로 기록해 이전 `CombatActor` 기준과 대조할 수 있다. 일반 검사 실행은 파일을 쓰지 않는다. 현재 런타임 수치 원본의 이행이며 실제 회복/저장/Run 정책이나 전체 Actor/상태/판정·제품 아트 검증은 아니다.

`ActorStatusTests`는 순수 참조 방향·플레이어 잠금/밀림 만료/방향 보존·강제 이동 중 밀림 미예약, 속박/기절 최대 갱신과 기본 경직 대입·밀림 쌍/최소 시간, 은신/적 제어의 엄격한 종료·관측/원본 독립·부분 해제/리셋·무효 명령의 상태 보존을 검사한다. `ActorStatusIntegrationTests`는 두 씬에서 일반/보스/분신 제어·재피격·가상 취소 콜백 중 편집의 다음 단계 반영·대시 후반 유효 피격의 밀림 생략·낙사 부분 해제·대상 은신/해제·리셋을 확인한다. `-actorStatusTraceRoot <outputs 경로>`는 상태/콜백 수치 비트 기록을 game.json/lab.json으로 출력한다. `ActorVitalsIntegrationTests`의 기존 피격 관측 기록도 같은 전후 대조에 사용할 수 있다. 일반 실행은 기록 파일을 쓰지 않으며 같은 Editor의 단계/수치 보존만 다룬다. 정식 상태 콘텐츠/전체 Actor/판정·Run/저장·실제 아트/제품 검증은 후속이다.

`ActorDamageExecutionTests`는 실제 Vitals/Statuses와 현재 자격·가변 제어/종류의 콜백 중 변경·패링/강제 이동·0/흡수 피해·기록/사망·중첩 수신/부분 예외를 검사한다. `ActorDamageNativeTraceTests`는 두 씬의 기본 경직/기절 콜백 뒤 DTO 재조회·보스/분신/플레이어 변경·불변 경로·중첩 Receive·기록/사망 중 부활/분류 변경과 실제 플레이어 효과 정리를 실행한다. `-actorDamageTraceRoot <outputs 경로>`는 이전 Native와 상태/float 비트를 대조하며 기존 Vitals/Status도 함께 검사한다. 새로운 사망/저장/Run 정책을 확정하지 않는다.

`EquipmentCommandsTests`는 순수 참조 방향·해석된 설정/성공 스냅샷·1↔3 부담 정규화·기존 쿨감 계산·없는 슬롯/옛 무기/시각/설정 거부·스탯 예외/누락/비유한 값의 상태 보존·재진입·같은 원본을 보는 호환 뷰를 검사한다. `EquipmentMaintenanceIntegrationTests`는 두 씬에서 기존 void/명시적 Try 정비 경로의 잘못된 카탈로그 요청·무효 수치·누락 항목 거부와 선택/충전/스탯/다른 무기 보존을 확인한다. 유효한 선택/용량 변경의 같은 무기 ID와 회복 부담·행동/전투 중 기존 정비 거부·실패 요청 뒤 접근 표식 유지와 성공 뒤 표식 정리도 검사한다. 실제 재고/아이템/안전 정비/Run 정책 검증과 구분한다.

`ActorContactTests`는 순수 질의/해석 참조 방향·몸 오프셋/질의 여유/접촉 제한·무시/단방향/법선/발 높이와 접지 속도 경계·작은 이동/방향 signed-zero·무효 질의 거부를 검사한다. `ActorSurfaceQueryTests`는 실제 Play Mode에서 Native BoxCast/Raycast/OverlapBox 결과와 변환 값/순서·정확한 XYZ bounds·ArenaSurface 필터·액터별 무시·읽기 전용 관측/콜라이더 파괴 뒤 이전 값·Transform 불변을 확인한다. 기존 `ActorMotionIntegrationTests`와 `EnemyIntentIntegrationTests`를 같은 복사본의 이전 `CombatActor`/수정본에서 각각 실행해 두 씬의 모션 3,120틱 float 비트/접촉과 적 9,000틱 관측을 대조할 수 있다. 이는 현재 공통 축/접지 경계이며 모든 Actor 질의/모터나 플랫폼 간 물리 결정성·아트/제품 플레이 검증을 의미하지 않는다.

`ActorPlacementTests`는 막힘의 요청 위치/방향/inner 크기·바닥 접촉/정확한 bounds/Z/단방향·세 ray 지지/첫 유효 높이/기울기/무시/평탄도 경계·대각선 텔레포트/시작 겹침/끝 위치 거부·작은 요청/무효 응답과 순수 불변 경계를 확인한다. `ActorPlacementIntegrationTests`는 두 씬의 공통 액터에서 평지/부분 지지/높이 차/발판 내려가기, 벽 반전의 몸 중심, Native 깊이 포함과 bounds Z 재검사, 열린/벽 제한/대각선/단방향/시작 겹침/막힌 끝/짧은 텔레포트를 검사한다. 선택적 `-actorPlacementTraceRoot`는 씬별 16개 위치/중심/속도/높이 비트·결과/방향을 로컬에 기록하므로 같은 fixture에 이전 `CombatActor`를 복구해 대조할 수 있다. 실제 낙사 복귀/수리검 후보 정책·아트/제품 플레이 검증과 구분한다.

`ActorRecoveryTests`는 현재 무효 표본의 최신→이전 순서·포함 cutoff·미래 제외·오래된 유일 안전 표본 보존·조회 실패/예외 뒤 비소비와 리셋을 검사한다. `ActorRecoveryIntegrationTests`는 실제 본 게임/시험장에서 최신 위치의 고체 추가·지지대 제거·현재 낙사 위험 추가 후 이전 안전 표본으로 복귀하는지 확인한다. 같은 10% 피해와 기존 T-3/공중 cutoff·대시 회복/취소 경계를 함께 검사한다. 이전 코드의 잘못된 시작점 대체/무지지·위험 표본 선택은 실패 전/성공 후로 구분하며 동등성으로 취급하지 않는다. 실제 챕터/모든 시작 무효 정책·세계 전체 복원 검증은 아니다.

`ActorPlatformTests`는 순수 불변/참조 경계·첫 상향 법선/질의 기하/절대 종료시각·접지/고체 거부의 상태 보존·시간과 몸 이탈의 동시 경계·반복/리셋/오래된 조회·현재 하단/파괴·잘못된 범위/관측 실패/재진입을 검사한다. `ActorPlatformBindingTests`는 실제 Play Mode에서 미선택 바인딩 반환·불변 접촉·비활성/이동/파괴 관측·리셋/새 핸들/참조 반환을 확인한다. `ActorPlatformIntegrationTests`는 두 씬에서 시간만/이탈만/두 조건, 동적 하단·비활성 재활성·리셋/파괴/재생성·겹친 발판과 반복 선택/상승 거부를 실제 액터로 실행한다. 선택적 `-actorPlatformTraceRoot`는 씬별 12개 시각/위치/속도 비트·결과/접지를 로컬에 기록한다. 같은 이전 `CombatActor`로 수명/기존 모션/배치 관측을 대조할 수 있다. `ActorDropSafetyTests`는 고체가 내려갈 거리 중간/끝에 있을 때 두 씬에서 요청 거부·위치/속도 보존·새 유효 요청을 검사하며, 고체 검사 없는 이전 코드와 동등하다고 주장하지 않는다.

`ActorReturnPlacementTests`는 순수/불변 포즈·대상 인접 비대칭 오프셋·목적지 검사 순서·전체 몸 대각선 스윕·일방향/초기/끝 거리 경계·원점 우선·1unit 후보/동률 X 순서·실패/무효 사실을 검사한다. `ActorReturnPlacementIntegrationTests`는 두 씬의 실제 플레이어/장비/시계에서 정상 원점·주변/대각선 후보·대상 앞 대체·죽은 대상·막힌 방문/복귀·무기 교체 뒤 예약/대시 취소를 9조건×260틱 실행한다. 선택적 `-actorReturnTraceRoot`는 씬별2340행의 위치/속도/시각/남은 예약/회복 비트와 방향/장착/예약/대시를 로컬에 기록한다. 같은 이전 `CombatPlayerReturn`으로 실제 관측을 대조할 수 있다. 이 검사는 전체 반환 정책/상태 이행·일반 월드 저장/재생·실제 아트/제품 플레이를 의미하지 않는다.

`ActorReturnScheduleTests`는 순수/불변 사실·원본/삽입 순서·무기/토큰 중복 거부·시각/허용 오차·늦은 조회의 예약 보존·소비 뒤 새 예약/오래된 토큰 거부·다른 소유자/잘못된 무기·리셋/이전 스냅샷·무효 입력의 상태 보존을 검사한다. `ActorReturnIdentityTests`는 두 씬에서 같은 Equipment 인스턴스를 보는 두 WeaponState가 예약 중복을 우회하지 못하는지 확인한다. `ActorReturnScheduleIntegrationTests`는 두 씬의 실제 예약 원점/시각/순서·같은 tick 두 무기 복귀·대상 소멸/복귀 실패·사망/리셋과 불변 조회를 검사한다. 기존 `ActorReturnPlacementIntegrationTests`의 시각/위치/속도/예약/대시 기록을 이전 복귀 partial과 비교할 수 있으며, 같은 무기 별도 뷰 수정은 옛 참조 비교와 동등하다고 주장하지 않는다. 현재 예약/대상 ID는 런타임 식별자이고 일반 저장/프로세스 재시작·제품 Run 정책 검증이 아니다.

`PlayerActionArbiterTests`는 순수 입력 합성, 유지 표본, 실패한 취소의 예약 보존, 기본/특수/교체 예약 우선순위, 자원 재검사, 미사일 비행 입력 폐기를 검사한다. `PlayerArbitrationIntegrationTests`는 두 씬의 실제 플레이어에서 같은 우선순위와 취소·리셋·차단·사망·완료 동일성 경계를 검사한다. `SpecialAdmissionTests`는 공통 임시 바닥과 배치 데이터를 사용하고 장비·지상/대상 조건을 먼저 확인한다. 지상 전용/`nearest` 기술의 예약 시 조건 거부와 실제 시작 시 재검사, 조건 회복 뒤 새 입력의 정상 시작을 본 게임/시험장에서 각각 검사한다. 이 검사는 실제 맵 전체의 통과 가능성 검증과 구분한다.

`PlayerDashTests`는 대시/무적/회복/이월의 독립 경계·현재 프로필/절대 종료시각·벽/취소/리셋·성공 복귀의 이월만 종료·이전 스냅샷과 액터별 독립성을 검사한다. `PlayerDashIntegrationTests`는 본 게임/시험장에서 정상/반전·벽·공중/유효·무효 점프·예약/교체·피격/기절·낙사/사망/리셋·정확 회복/프로필 변경을 같은 실제 수동 틱으로 실행한다. `-playerDashTraceRoot <outputs 경로>`는 시각/위치/속도·HP·대시/무적/회복·점프/행동/자원 관측을 float 비트로 기록하며 정확한 이전 `CombatPlayer`와 `CombatPlayerReturn` 쌍을 같은 fixture에 복구해 대조할 수 있다. 기존 수리검/귀환 검사는 성공 복귀의 이월 정리도 포함한다. 시간창의 마지막 tick을 새 부분 스텝 규칙으로 바꾸거나 전체 점프/낙사/모터·제품 진행을 이행한 것은 아니다.

`PlayerJumpTests`는 첫/둘째 수직속도 교체·사용량·소진 후 예약 갱신, 착지 예약의 포함 만료/그 이후 거부·일회 소비, ClearBuffer/Reset과 이전 스냅샷을 검사한다. 기존 두 씬 `PlayerDashIntegrationTests`의 점프/무효 점프/이월·피격/취소/낙사·리셋과 실제 첫/둘째 높이·행동 중재 검사를 같은 소비자로 실행한다. 순수 사용량/예약 이행은 수직 물리·전체 이동 데이터 저작·벽 점프나 제품 저장 구현을 뜻하지 않는다.

`Deviant.Control.Tests`는 엔진 비의존 입력 채널 합성, 정지/입력 분리, 개별 핸들·동일 이름의 다른 소유자·세션 종료/오래된 핸들, HUD 제어 Presenter의 ESC/가방/클릭/정지 수명을 검사한다. `ControlBindingTests`는 실제 본 게임/시험장의 자동·수동 정지, 차단 중 입력 폐기, 리셋의 제어권 보존, 단일 Unity 시간 어댑터/이전 배율 복원, HUD 버튼 콜백·비활성화·재바인딩과 오래된 해제, 합성 마우스·키보드의 클릭 중 전투 입력 차단을 검사한다. EventSystem·GraphicRaycaster·버튼 Raycast 설정도 확인한다. 이 검사는 화면 가독성·포커스 라우터 전체·실제 대화 구현의 증거가 아니다.

`ScreenRouterTests`는 순수 UI 경계·명시적 우선순위/동률·화면별 제어 반환·Back 소비/옛 토큰·부분 바인딩/제어/포커스 실패·View 수명·콜백 중 종료·최초 기준 포커스의 단일 반환·정리 예외와 일시적 포커스 전환을 검사한다. `ScreenFocusIntegrationTests`는 임시 CanvasGroup/버튼과 실제 EventSystem으로 중첩 화면의 기억한 선택/기준 선택 복귀·뒤 화면 콜백 거부·같은 Back 소비·소유자 전체 종료·옛 세대·선택 트랜잭션 중 닫힘·파괴된 이전 선택을 확인한다. 제품 화면 등록/키/전체 입력 레이어나 수동 가독성의 증거는 아니다. 현재 범위는 [화면 포커스·Back](../presentation/ui/screen-routing.md)을 따른다.

`ScreenInputBindingTests`는 실제 Input System Keyboard/Gamepad edge를 기존 라우터/ScreenView에 연결해 한 토큰/한 Back 소비, 유지 입력 비반복, Navigation 제한/화면 없는 결과와 earlier callback 재바인딩의 옛 delivery 차단을 검사한다. batch Editor의 합성 장치는 IgnoreFocus/AllDeviceGameView 설정 뒤 생성하고 종료에 원래 설정/장치를 반환한다. 제품 키/전체 HUD·대화 입력 조립이나 렌더링/가독성 검증은 별도다.

`Deviant.World.Tests`는 엔진 비의존 스트리밍 계획의 경계 좌표·원본 템플릿 반복/물리 배치 구분, 불변 스냅샷·같은 창 재사용, 무효 설정/좌표 거부를 검사한다. `WorldStreamingIntegrationTests`는 본 게임의 실제 숲이 계획의 청크 인덱스/월드 위치를 사용하고, 0번/8번 템플릿 타일 일치·기존 뷰 재사용·무효 입력의 무변경 거부·공통 플레이어 유지가 성립하는지 검사한다. 기존 `GameIntegrationTests`의 재사용 지형 접지 검사를 함께 실행한다. 이는 반복 프로토타입의 스트리밍 검증이며 챕터 공급/상태 검증과 구분한다.

`ChapterLayoutTests`는 고정/캡처 공급의 공통 경로·불변/버전/기하 해시·누락/충돌/틈/연결/미지원 이동 거부·실패 정리·배치 상태·오래된 세션/앵커·재진입·같은 공간의 이동 재검증을 검사한다. `ChapterWorldIntegrationTests`는 두 씬에서 제작 박스를 같은 정적 충돌면으로 구성하고 기존 공통 플레이어로 왕복하며, 이동 버전·마커 재로드·앵커 좌표·기하 편집 거부를 검사한다. `ChapterModuleAdapterTests`는 미지원 원본 기하를 뷰 생성 전에 거부하는지 확인한다. 이들은 `Deviant.World.Tests`와 `Deviant.Combat.Tests`에 포함된다. 현재는 격리된 평지 모듈/마커이며, 점프·대시/발판·실제 생성기·기본 숲/Run·NPC/액터/아이템 수명 전체의 검증은 아니다. 자세한 지원 범위는 [챕터 레이아웃](../gameplay/world/chapter-layout.md)을 따른다.

`Deviant.Presentation.Tests`는 엔진 비의존 Cue 정의/재생기의 참조, 방어 복사/해시·프레임 경계, 발생 중복/시작 버전, 소유자별 반복/취소, 지연/중복/리셋 뒤 미디어 결과, 누락/준비/시계/표시/정리 실패와 재진입을 검사한다. 또한 단일 핸들 갱신이 다른 소유자의 준비된 뷰를 샘플링하지 않음, 늦은 미디어의 현재 프레임/종료 경계, 갱신 콜백 재진입과 리셋/종료 뒤 핸들 갱신 무효화를 검사한다. `CueCombatIntegrationTests`는 임시 Sprite로 실제 좌표·좌우·물리 컴포넌트 부재·버전 바인딩과 기존 프레임 구간 일치를 확인하고, 본 게임/시험장에서 정상·취소·누락·표현 비활성의 피해/첫 타격/이동/시계 관측이 같은지 검사한다. 원본 배열/공격 타이밍 편집 중 기존 Cue의 스냅샷과 리셋도 확인한다. 이는 실제 외부 아트·손맛·카메라·음향/다른 매체 검증이 아니다. [전투 표현](../presentation/visuals/combat-presentation.md)의 지원 범위와 `RequiresArt` 검사를 구분한다.

`CharacterLocomotionExecutionTests`는 표현 상태 우선순위·상태 진입/동일 상태 dt·재점프/상승/정점/낙하/착지·형태/리셋·필요한 길이의 지연 조회/예외 부분 상태를 검사한다. `CharacterLocomotionNativeTraceTests`는 두 씬의 합성 Sprite/실제 Renderer로 걷기/정지·재점프·낙하 지연/반복·착지/달리기 우선·대시/채찍접근·사망/기본타수·공격/착지 캡처와 현재 회복 길이의 클립/프레임을 확인한다. `-locomotionPresentationTraceRoot <outputs 경로>`로 이전 Presenter와 표시/float 비트를 비교하며 실제 아트 품질 증거는 아니다.

`AudioCueTests`는 기존 Sprite 정의의 버전 호환과 음향 클립 ID/샘플 경계·버전, 같은 Cue 실행기의 완료/취소/실패·루프/소유자와 지연/중복/옛 결과를 검사한다. `AudioCueOutputTests`는 실제 Play Mode의 AudioSource 샘플 위치·전진/정지/역행·출력 비활성/파괴/클립 유실·정리 및 Sprite와 같은 플레이어의 독립 수명을 확인한다. `AudioCueCombatIntegrationTests`는 본 게임/시험장에서 주입 음향의 정상/취소/누락/출력 실패가 동일 전투 시각·피해/타격·자원을 유지하는지 검사한다. 첫 샘플 정지와 poll 사이의 DSP 보간은 [음향 Cue](../presentation/visuals/combat-presentation.md#주입한-음향-cue)의 제한을 따른다. 장치 청취/믹싱·제품 소리 배치·시퀀스 음향 바인딩은 완료로 취급하지 않는다.

`AttackMotionTimingTests`는 Unity 비의존 시작 스냅샷의 프레임/초·방어 복사/해시·접촉 전후 보정·프레임 경계/루프·최소 회복 보정과 무효 입력을 검사한다. `AttackMotionSnapshotTests`는 이미지 로드 없이 현행 공격 클립 메타데이터의 컴파일 가능성을 확인하고, 본 게임/시험장에서 첫 Draw 전 본체/Cue 배열 편집·카탈로그 제거·다음 공격의 새 버전·누락/잘못된 표현에도 같은 타격/종료 시각과 피해를 유지하는지 검사한다. 이 경로는 검 1타의 기존 전투 시간 고정 호환 컴파일이며 전체 프레임 기준 저작·행동 시간표/ActionRunner·실제 아트/손맛 검증은 아니다.

`GenericAttackMotionTests`는 본 게임/시험장의 검기·채찍 접근·방패 보호 및 폭탄/수리검/발사기 기본을 실제 입력으로 시작하고 첫 Draw 전 프레임/시간/Sprite 편집·카탈로그 제거 뒤 시작 본체 유지와 다음 실행의 새 매체 채택을 검사한다. 특수/변환 기본의 불필요한 기본 Cue 비생성도 확인한다. 기존 근접/직접/차지 모션 검사와 전체 카탈로그의 전투 관측을 함께 실행하며 실제 아트·가독성/손맛 검증으로 표현하지 않는다.

`CameraDirectorTests`는 기본/남은 위치 소스의 재평가·우선순위/동률 순번·소유자별 반환·완료/취소/실패·잘못된 입력·리셋/오래된 핸들·콜백 재진입을 검사한다. `CameraRequestIntegrationTests`는 단일 기본 공급자/실제 Transform 출력, 두 씬의 기존 기본 구도와 가려진 추적 갱신, 전투 리셋/중첩 정지와 독립적인 카메라 수명, 렌즈 값 유지와 실제 카메라 파괴를 검사한다. `ChapterWorldIntegrationTests`는 챕터 세션/앵커를 같은 실제 카메라 요청에 연결하고 종료 뒤 앵커 거부도 검사한다. 이 검사는 렌즈/PixelPerfectCamera 품질·줌/흔들림·블렌드·실제 대화/연출 구도 검증을 포함하지 않는다. 지원 범위는 [카메라 구현](../gameplay/world/forest-prototype.md#카메라)을 따른다.

`CameraOutputLifecycleTests`는 실제 Play Mode의 출력/계층 비활성화·관측한 카메라 가용성 유실·재활성화/옛 서비스 거부·최신 기본 구도 복귀, 소스/관측 콜백 중 비활성화·재활성화/파괴의 요청 반환과 옛 프레임/관측 차단을 검사한다. 본 게임/시험장에서 기존 추적을 출력 비활성 중 갱신한 뒤 복귀하고 옛 핸들이 새 요청을 해제하지 않으며 중첩 입력/정지·전투 시각/HP·렌즈가 유지됨을 확인한다. `SequenceIntegrationTests`의 순차 카메라 단계는 같은 두 씬/고정·캡처 챕터에서 출력 취소 전파·모달 보존·새 출력에 대한 옛 바인딩 거부와 명시적 새 바인딩을 검사한다. 렌즈/PixelPerfectCamera 품질·줌/흔들림·실제 연출/아트 검증과는 구분한다.

`Deviant.Narrative.Tests`는 불변 직선 정의·표시 토큰/중복·후보와 진행 조건 재검사·시작 버전·종료 이유·재진입·소유자 제어·오래된 바인딩·텍스트 누락 실패를 검사한다. `DialogueChoiceTests`는 분기/합류·선택 순서와 방어 복사·기존 직선 해시·잘못된 참조/순환/도달 불가·명시적 선택/중복/옛 토큰·현재/선택/목적지 조건 재검사·가용성 변경 리비전·모두 비활성·시작/다음 선택지 준비 실패·재진입·대상 수명·누락 선택지 텍스트와 남은 모달 제어를 검사한다. `DialogueCleanupTests`는 입력 거부와 반환 오류 구분·구독/제어/뷰 반환 실패 집계·남은 반환 시도·중첩 모달 보존을 검사한다. `NpcDialogueIntegrationTests`는 두 씬의 실제 챕터 마커와 임시 uGUI 버튼으로 범위 재검사·진행/연속 클릭·세계 계속/정지·중첩 모달·취소/뷰 비활성화·모듈 언로드/재로드·수명 정리와 등록 참조 거부를 검사한다. `DialogueChoiceIntegrationTests`는 같은 Game/Lab·챕터/호스트에서 주입 슬롯의 텍스트/가용성/일반 진행 거부·선택 직전 조건 변화·실행 중 NPC 버전 유지·모달/중복/옛 입력·언로드를 검사한다. 임시 uGUI에서 슬롯 부족/중복 참조·레이아웃 재바인딩과 같은 UnityEvent 앞 콜백이 줄 또는 세션을 바꾼 뒤 남은 옛 클릭이 새 선택을 실행하지 않음도 검사한다. `DialogueCommandTests`는 등록된 명령 참조/해시·미등록/없는 포트·범위 획득/부분 실패·성공/거부 재시도/취소/실패·예외/결과 누락·선택 전 현재/선택지/목적지 조건 검사·성공 뒤 조건 조회 실패에서 영수증 유지·줄별 중복/다른 선택 거부·옛 세션/범위·콜백 재진입·완료/취소 사실과 반환 오류 분리·나머지 Presenter 제어/View 반환을 검사한다. `DialogueCommandIntegrationTests`는 두 씬의 고정/캡처 챕터와 실제 선택지/NPC 호스트/시퀀스로 등록 누락의 사전 거부·결과/중복·중첩 모달·스킵/타임아웃/모듈 언로드·명령 반환 오류 전파와 플레이어 시각/피격·배치 사실 보존을 검사한다. 제공 서비스는 호출/결과만 기록하는 기술 fixture이며 제품 보상/장비 변경/서사 사실을 제공하지 않는다. 이는 제품 NPC/대사·실제 이야기 분기/보상/영구 사실·전체 화면 포커스/탐색·수동 가독성 검증을 포함하지 않는다. 현재 지원 범위는 [NPC·대화](../presentation/narrative/narrative-dialogue.md)를 따른다.

`Deviant.Sequences.Tests`는 엔진 비의존 불변 정의/해시·단계 순서/시간·주입 시계·완료/취소/실패/명시적 스킵·중복/오래된 실행·외부 단계/스킵 타임아웃·재진입·정리 실패와 중첩 제어 반환을 검사한다. `SequenceParallelTests`는 기존 순차 해시 유지·자식/전체 ID 검증·같은 경과시간/전체 완료 대기·완료 자식의 조기 반환·첫 실패/미시작 구분·복수 정리 오류·그룹 취소/스킵과 불변 자식 상태를 검사한다. `SequenceIntegrationTests`는 본 게임/시험장과 고정/캡처 레이아웃에서 임시 uGUI 대화·실제 Sprite Cue·챕터 앵커/카메라를 순차/ParallelAll로 연결한다. 필수 앵커 선언/누락·시작 뒤 NPC 버전 교체·모듈 언로드·대화 반환 오류·누락 미디어·늦은 준비 응답·비활성/콜백 중 호스트 종료·옛 실행 해제와 다른 소유자 보존을 확인한다. 그룹 내부 참조/시작 NPC 버전·단일 View의 동시 대화 거부·동일 Cue 두 발생·서로 다른 자식 종료 시각·병렬 미디어 도착 시각의 표시 프레임/만료·그룹 스킵 뒤 늦은 준비 결과도 검사한다. 제품 스킵 종료 서비스/이야기·보상·전환, 중첩 그룹/경쟁 종료·게임플레이 액터 제어·전체 GUI/Run·실제 아트/연출 품질은 검증하지 않는다. 현재 시간/종료 계약과 제한은 [시퀀스 실행](../presentation/narrative/sequence-execution.md)을 따른다.

`FadeDirectorTests`는 명시적 색/불투명도/시간·해시·선형 보간·완료 후 유지/반환·소유 채널 교체·우선순위·현재 프레임 복귀·무효/역행 시각·리셋/오래된 핸들을 검사한다. `FadeOutputTests`는 실제 uGUI Image 색·기준 프레임·Canvas 가용성·출력/Image 비활성/유실·재바인딩·쓰기 오류·콜백 재진입과 Play Mode 종료를 확인한다. `SequenceFadeIntegrationTests`는 본 게임/시험장과 고정/캡처 챕터의 대화→카메라→페이드 아웃/유지/인, 병렬 Cue/대화/카메라 조합·스킵/취소/타임아웃·출력 세대 고정·없는 출력의 사전 거부·기존 비페이드 경로·챕터 종료·다른 소유자 제어/표시 보존을 검사한다. 제품 Canvas 배치·마스크/곡선·포커스/키 매핑·실제 화면 가독성·씬 전환 자체는 검증하지 않는다.

`StageActorSessionTests`는 명시적 이동 정의/포즈·보간·독점 소유·교체·현재 기준 포즈·무효/역행 시간과 리셋/오래된 핸들을 검사한다. `StageActorOutputTests`는 실제 Sprite의 위치/방향/가시성·물리/스크립트/애니메이션 계층 거부·실행 중 안전성 유실 시 쓰기 중단·내용 교체/재바인딩·비활성/세대를 검사한다. `SequenceStageActorIntegrationTests`는 두 씬과 고정/캡처 챕터의 앵커 이동, 서로 다른 뷰와 대화/카메라/Cue/페이드의 병렬 조합, 독점 경합·중복 역할·필수 앵커·취소/스킵/마감·세대/챕터 종료를 확인한다. 플레이어 위치·HP·전투 시각과 배치 사실을 변경하지 않음도 확인한다. 게임플레이 액터의 제어권/물리 인계·실제 연출 아트/가독성·제작 원본/저작 도구는 이 검사의 범위가 아니다.


LabExecutionTests는 실제 Lab lease와 창에서 Run/Pause/한 tick/슬로모션·고정 dt·backlog 보존·Game 거부·중첩 외부 제어/영수증·실패 뒤 own pause 복구·재바인딩/종료·tick/Reset 콜백 중 대상 비활성의 지연 반환을 검사한다. 시각 on/off에서 HP/피격수/tick/위치와 Cue 수명이 같고 나중 카메라 소유자 쓰기를 덮어쓰지 않는지 확인한다. 실제 UI는 RequiresGraphics이며 사람의 손맛/아트/장치 청취 검증과 구분한다.

`OrderedInputIntegrationTests`는 Game/Lab에서 한 InputSystem 갱신 이전의 S press/release/press와 D→S 이벤트 순서·원래 native timestamp를 그대로 소비하고 기존 bit-union 우선순위를 유지하는지 검사한다. 이벤트 콜백만으로 행동을 시작하지 않으며 Space press 시 Jump/Drop과 release의 대응·reset 세대 폐기·차단/정지·수동 소유와 실제 HUD 포인터/Back 소비를 기존 입력/제어 fixture와 함께 검사한다. 큐는 값만 보관하고 실제 시간 메타데이터와 전투 시계를 구분한다. 장치 물리 청취/패드 신규 매핑·raw 장치 파일 재생 검증은 아니다.

`SimulationInputTests`는 입력 계약의 UnityEngine 비의존성, 실제 Input System 키보드 표본, 두 씬의 동일 입력 수동 실행, 입력 차단·무효 스텝·중복/이전 실행권·자동 갱신 복귀를 검사한다. `CombatInputRecordingTests`는 Editor 전용 입력 기록 계약의 엔진 비의존성·방어 복사·입력 순서/모든 필드/시나리오/간격의 해시·문화권 독립성·무효 헤더·용량 제한·완료 후 불변성과 빈 완료 거부를 검사한다. `SwordDashInputSourceTests`는 JSON 버전·두 구간·내용 해시·입력 값·시나리오/틱 간격·용량 검증, BOM 없는 결정적 저장/재로드, 파일 크기 한도, 새 파일 덮어쓰기 거부·해시가 일치한 교체·외부 수정/삭제 충돌과 임시 파일 반환을 검사한다. `SwordDashComparisonTests`는 같은 버전의 A/A 결과, 타이밍·범위·대시 속도 변경의 관측 차이, 0 피해의 유효 타격과 미적중의 구분, 벽 정지·다중 전투 거부, 중간 실패 시 장비/정의 복원과 실행권 반환, 실제 비교 창의 A/B 기록·실행을 검사한다. A가 기록한 두 입력 구간을 B가 같은 불변 스냅샷으로 재생하며 창에도 입력 수/해시가 표시되는지 확인한다. 저장/재로드한 사용자 표본이 기본 공격/대시 스크립트 대신 두 버전을 실제로 구동함과, 창의 가져오기·내보내기·기본 입력 복귀/파일 오류 뒤 선택 보존도 확인한다. 이 실험은 기존 전투 실행기를 호출하며 별도 전투 계산식이나 외부 아트는 필요하지 않는다. 입력 파일은 초기 상태/정의/레이아웃 저장이나 일반 장치 이벤트 녹화의 증거가 아니다. 창 검사는 `RequiresGraphics`를 사용한다.

창을 여는 검사는 그래픽 장치가 필요하므로 `-nographics`를 붙이지 않는다. 격리 복사본에서 아트 검사만 제외한 전체 전투·저작 회귀는 다음과 같다.

```sh
unity test outputs/refactor/UnityVerification --mode EditMode --filter 'Deviant.Combat.Tests;Deviant.Authoring.Tests;Deviant.Control.Tests;Deviant.World.Tests;Deviant.Presentation.Tests;Deviant.Narrative.Tests;Deviant.Sequences.Tests' --output outputs/refactor/authoring-regression.xml --timeout 1800 -- -testCategory '!RequiresArt' -logFile outputs/refactor/authoring-regression-editor.log
```

그래픽 장치가 없는 환경에서는 `-nographics -testCategory '!RequiresArt;!RequiresGraphics'`로 창 검사를 명시적으로 제외한다. 테스트의 그래픽 장치 오류를 무시하거나 창 검증 통과로 기록하지 않는다. 자동 UI 이벤트 검사는 수동 레이아웃·가독성 확인을 대신하지 않는다.

## 확정 11종 아트·발사기 표현 확인

[11종 원본](../../../resources/README.md)을 복원하고 **Unity 6000.6.5f1**에서 실행한다. 격리 검증 사본도 같은 Editor·패키지 버전을 사용하며 검사를 위해 프로젝트 버전을 낮추지 않는다.

```sh
unity test unity --mode EditMode --filter 'Deviant.Combat.Tests.ApprovedElevenWeaponTests;Deviant.Combat.Tests.ApprovedWeaponGameSceneTests;Deviant.Combat.Tests.ConfirmedWeaponTests;Deviant.Combat.Tests.LauncherArenaTests' --output outputs/approved-eleven/results.xml --timeout 1800
```

`ApprovedElevenWeaponTests`는 11세트·PPU32·피벗·발사기 4모드의 시작 본체 선택, 발사 시 총구 효과·레이저 표시/일회 피해·실제 유탄 접촉과 정리를 검사한다. `ApprovedWeaponGameSceneTests`는 실제 들판 씬의 11외형 선택과 원거리 본체·효과를 확인한다. `RequiresArt` 검사이므로 이미지 누락을 통과로 취급하지 않는다. 기존 `CharacterPresentationReadTests`, 모션 registry·FrameTime·시작 스냅샷 회귀도 실행해 Draw의 읽기 경계와 이미 시작한 행동의 시간을 확인한다.

수동으로 11종의 대기→이동→점프→대시→공격→죽음을 좌우에서 재생한다. 대기의 노란 다리·검정 발이 고정되고 베이지 로브만 움직이는지, 무기 잔픽셀과 죽음의 잉크 소멸, 기본포 단발의 재발사처럼 보이는 자세가 없는지 확인한다. 레이저는 흰 중심과 주변 광·벽 제한·발사 자세 동기화, 샷건은 비대칭 총구 폭발과 실제 5탄, 유탄은 탄착·파편별 폭발을 확인한다. 원본 해시/자동 검사·실제 렌더·수동 손맛과 Player 패키징의 수행 여부를 구분해 커밋에 기록한다.

## Windows 빌드

아트와 독립적으로 런타임 어셈블리 경계를 검사할 때는 격리 프로젝트에서 `UnityEditor.Build.Player.PlayerBuildInterface.CompilePlayerScripts`를 `StandaloneWindows64`/`Standalone`/`ScriptCompilationOptions.None`으로 호출하고 출력은 `outputs/`에 둔다. 결과의 DLL 목록과 실제 파일에서 본 게임·공통 전투·Definitions·Equipment 계약/무기 인스턴스·선택·장착 목록/활성 슬롯·정비 명령·충전·능력치 원본·Control·Cue/표현·카메라·페이드·StageActor·Narrative·Sequences·UI Presenter/View·World 스트리밍이 포함되고 프로젝트 Editor/Tests와 `Deviant.CombatLab.Contracts`를 포함한 실험/저작 어셈블리가 제외됐는지 확인한다. 이는 Player 스크립트 컴파일 검사이며 씬·리소스 패키징·실행 파일·화면/플레이 검증을 포함하지 않는다.

통합 빌드는 `Deviant.Game.Editor.GameBuilder.BuildWindows` 또는 `Tools > deviant > Game > Build Windows`를 사용한다. 기본 출력은 `Builds/Windows/deviant/deviant.exe`다. `TEAM_PLAYTEST_OUTPUT`으로 출력 디렉터리를 지정할 수 있으며 실행 파일명은 `deviant.exe`로 유지된다. 기존 `Tools > Team Playtest > Build Windows`도 같은 빌드를 실행한다. 전투 시험장 빌드는 아래 진입점을 계속 사용한다.

빌드가 필요할 때 실제 Editor·프로젝트·로그 경로로 바꿔 실행하고 결과를 별도 기록한다. 다음 명령이 현재 Windows 빌드의 존재나 성공을 증명하지는 않는다.

```powershell
& '<Unity 설치 경로>/Editor/Unity.exe' -batchmode -quit -projectPath '<저장소>/unity' -executeMethod Deviant.Combat.Editor.CombatArenaBuilder.BuildWindows -combatV2BuildOutput '<출력 경로>/deviant.exe' -logFile '<로그 경로>/combat-v2-build.log'
```

진입점의 기본 출력은 프로세스 작업 폴더 기준 `../deviant-CombatV2-Windows/deviant.exe`다. 공유 명령에서는 `-combatV2BuildOutput`으로 출력 경로를 명시한다. `deviant_Data`, `UnityPlayer.dll` 등 동반 파일을 함께 배포하고 빌드 로그·결과 경로를 확인한다. 경로 길이 문제는 해당 PC의 짧은 연결 경로로 해결할 수 있으나 특정 개인 PC 경로를 공용 실행 경로로 사용하지 않는다.

## 색상 파츠 공통 전투 시험

Unity 6000.6.5f1 격리 사본에서 `ForestCombatV2.Tests.InkPartExpandedTests`, `InkPartCombatTests`, `DummyTrainingTests`와 `Deviant.Combat.Tests.EquipmentLoadoutTests`, `CombatHitEventTests`를 실행한다. 기존 승인 아트·가방 버리기 검사도 함께 실행할 수 있다. 기본 7종 회귀에 더해 신규 52종의 적중·처치·교차·피격·이동·시작 효과, 표식 격발 경쟁과 예약 표식만의 소비, 실제 픽업·장판·이동 파동, 보호막 흡수의 공통 기록, 활성 무기 강화 전환, 정확한 회피 중복 방지, 10개 무기별 차징 프로필의 실제 발동 피해·쿨타임 미소비 및 일반 적 이동/공격 취소를 검사한다. 원본/생성물 일치는 두 생성기의 `--check`로 확인한다. 강철 주입은 사용자 요청으로 보류한다. 이 범위는 획득/제작/가방 아이템·최종 파츠 아트·전체 제품 회귀 또는 수동 가독성 검증 완료를 뜻하지 않는다.

## 잉크 상태의 최소 시각 표시

`Tools > Deviant > Chapter1 > Open Random Stage`로 정상 랜덤 스테이지를 연 뒤, 시작 도면 선택을 완료하고 F1 개발 패널의 `파츠 실험 설정 · 전투 일시정지`를 누른다. 무기 슬롯별 자유 6칸·색상 필터·파츠 목록과 별도 시험 차징 스탬프를 설정할 수 있다. 설정 완료 또는 F1로 닫으면 전투가 재개된다. 일반 스테이지의 적·맵·런 진행을 유지하며 허수아비 훈련 오버레이를 켜지 않는다. 이 기능은 개발 실험 설정이며 획득/제작/저장 규칙을 대체하지 않는다. 강철 주입은 장착을 거부한다. 본체 비활성화 시 설정의 입력 차단·정지 스코프도 해제한다.

`InkStatusVisualTests.OpenRandomStageConfiguresRealPalettesAndShowsSharedStatusesWithoutTrainingOverlay`는 실제 정상 씬에서 런 바인딩·15개 방·훈련 오버레이 부재, 설정 중 시간 정지와 두 무기 독립 팔레트/차징 설정, 일반 적의 표식·빙결과 플레이어 보호막 표시, 완료/비활성화 시 정지 해제를 검사한다.

`ForestCombatV2.Tests.InkStatusVisualTests`는 보호막의 실제 흡수·만료, 일반 적과 허수아비의 빙결·둔화·냉기·표식·화상, 대상 하나 초기화 뒤 다른 대상 표시 보존, 표시 유무와 읽기 반복의 전투 상태 불변, F1 정지 중 표시 위상 고정, 기존 캐릭터와 장식의 실제 렌더 색상 출력을 검사한다. Unity 6000.6.5f1의 그래픽 장치가 있는 격리 사본에서 실행하며 `-nographics`를 사용하지 않는다. 실제 렌더 미리보기는 격리 프로젝트의 `../outputs/ink-status-ui/overview.png`로 출력한다. 이 검사는 모든 해상도의 수동 가독성이나 최종 아트 승인을 대신하지 않는다.

시작 마을: `StartingVillageTests`는 실제 마을 씬에서 작업대 6곳을 실제 이동 입력으로 왕복하고 같은 평지·가장 가까운 대상 선택·공격 불가·F 전달을, `StartingVillageLoopTests`는 실제 화면 버튼으로 타이틀 → 마을 → 첫 방문 이야기(건너뛰기도 각성 장면 표시) → 출발 준비대(도면 2개, 3개째 거절) → 1챕터 → 엘리트 마법안료 → 클리어 결과 「마을로」 → 염색공방 4칸 구매 → 다음 출발 4칸 → 사망 → 마을을, `StartingVillageCaptureTests`는 960×540 컬러/스케치 거리 캡처와 이야기·작업대 화면의 960×540·1280×720·1920×1080 캡처 및 정적 글리프 존재를 검사한다. 프로필은 메모리 저장소를 쓰며 캡처는 `outputs/hub/capture`에 남는다.

`ForestCombatV2.Tests.Chapter1DownKeyTests`는 실제 1챕터에서 Input System 키보드 이벤트를 주입해 S/↓의 지속 하강(-1), 아래 키 뒤 Space와 동시 누름의 Drop, Space 뗌의 Drop 해제, 두 아래 키 중 하나만 뗄 때의 하강 유지, Space 단독 Jump를 검사한다.

`Chapter1EquippedInkGuiTests`는 실제 정비소에서 공통 책의 즉시 슬롯 강화·색 변경·스탬프 적용과 판매·성장·잉크 판매대 거래를 실행한다. `Chapter1NativeEquipmentBookTests.CheckShop`과 같은 검사를 사용하며, 실제 카메라·uGUI 렌더 및 한글 글리프 검사로 화면을 `outputs/hub/capture/`에 저장한다.

`Chapter1FirstRunInkGuideTests`는 안내 잉크 고르기(팔레트 색·가장 낮은 등급·무색 칸만이면 다섯 색·잠긴 파츠 제외)와 대상 판정(프로필 첫 출발만, 기록·두 번째 런·다른 프로필·훈련 제외)을 EditMode로, 메모리 프로필의 첫 출발 런에서 안내 몬스터 등장·접근, 처치 시 팔레트 색 1등급 잉크 정확히 1개와 바닥 테두리, 주인공 말풍선, 줍기 뒤 `guide-ink` 기록·상단 가방 안내, 가방 안 맞는 칸 테두리·안내 줄·무기 탭 전환, 장착 뒤 `guide-bag` 기록과 표시 해제를 실제 1챕터에서 검사한다. 첫 출발 런을 쓰지만 이 안내가 주제가 아닌 검사(`Chapter1PigmentTests`·`Chapter1BossExitHomeTests`·`StartingVillageFirstReturnTests`)는 `guide-bag`을 미리 기록해 안내 몬스터를 끈다. `Chapter1HudKeyAndBagMessageTests`는 HP 막대 오른쪽 열쇠 그림의 보유 중 표시·상자 연 뒤 숨김, 가방 안내 줄의 색상 잉크 규칙(해제 불가 문구 없음), 정비소 팔레트 관리에서 칸을 고르기 전 보유 잉크의 안내 줄(맞는 칸이 있으면 빈 줄, 없으면 이유)을 검사한다. 같은 테스트는 공통 책의 팔레트 탭에서 「색 변경」을 누른 뒤 다른 색의 보유 잉크를 골라도 대상 칸과 모드가 유지되고 판매·파괴가 숨겨지는지, 「색 바꾸고 장착」으로 칸 색 변경(100G 차감·새 잉크 장착·기존 잉크 가방 반환)을 실행하는지도 검사한다.

`InkInventoryTests`는 모든 종류가 공유하는 5칸, 다른 종류 간 바닥 교환의 보관 수 유지, 색상 잉크가 모든 종류의 자리를 차지하는지를 검사한다. `Chapter1KindStorageTests`는 실제 1챕터에서 형태·특수·색상 잉크를 합쳐 5개 보관 뒤 모든 종류의 획득 거부, 색상 바닥 보상 보존·재획득, 가방 포화 시 결제 없는 판매대 구매 거부, 가방 능력치 조회·장착·무료 교체(기존 잉크 반환·보관 수 유지), 포화 제단의 후보 보존과 공간 확보 뒤 수령을 검사한다. `Chapter1InventoryInkTests`는 정비소 밖 시작 방에서 가방 팔레트 칸의 색 이름 표시, 색상 잉크 장착·무료 교체·되돌리기·두 번 눌러 파괴, 체력 감소 시 가방 닫힘을 검사한다.

`Chapter1InventorySkinTests`는 공통 인벤토리 책을 열어 800×440 프레임·팔레트/스탬프 2탭·실제 열린 3/4/5칸·초록 빈 칸의 연한 방울·장착 잉크 상세와 해제 버튼·정비소 밖 강화 버튼 부재를 검사한다. `Chapter1NativeEquipmentBookTests.CheckInventory`와 같은 검사를 사용하며 실제 카메라/uGUI 렌더와 한글 글리프 검사로 `native-inventory-approved.png` 캡처를 출력한다.

`Chapter1TitlePresentationTests.NativeOverlayRendersHumanOnLeftAndInkOnRight`는 실제 Play의 ScreenSpaceOverlay 모드를 바꾸지 않고 Game view를 캡처한다. 좌우 얼굴 픽셀을 원본 human/ink 프레임과 대조하고 하늘의 좌측 채색·우측 종이 분할을 검사한다. 카메라 렌더 모드로 바꾼 캡처만으로 실제 Overlay 표시를 검증했다고 간주하지 않는다.

`Chapter1TitlePresentationTests`는 실제 타이틀의 승인 로고·노란 붓펜·공용 폰트 메뉴와 렌더 픽셀, 포인터·키보드·패드 선택, 공유 설정과 조작 편집의 취소·포커스 복귀를 검사한다. 960×540·1280×720·2340×1080·3440×1440·800×1000 출력에서 풍경 채움과 글자의 화면 내 배치를 확인한다. 새 캐릭터 48프레임의 채색/스케치 대기, 중앙 종이재 경계, 좌측 채색 인간/우측 스케치 잉크와 시선·중앙 정렬, 시작 중 경계 고정과 즉시 1.2초 암전, 검은 프레임 이후 한 번만 시작하는 콜백과 취소, 시간 배율 0의 실제 마을 진입, 2챕터 준비 화면 복귀도 확인한다. 캡처는 `outputs/title-sketch-tests/`에 둔다.

`BackgroundMusicTests`는 실제 AudioSource의 PCM 출력·반복·소스 전환·동일 요청 유지·시뮬레이션 정지 중 재생과 원본 Streaming 임포트를 검사한다. 페이드인의 느린 시작, 페이드 도중 재요청 시 끊지 않는 페이드아웃과 떠난 큐의 재사용, 같은 파일의 조용한 버전으로 재생 위치를 유지한 연속 음량 변경, 메인 스레드 정지 뒤 페이드 진행 제한도 검사한다. 실제 타이틀→마을(조용한 길·지워짐 컷신 시작 시 추격 음악)→1챕터 정비소·조우에서 지정 곡을 확인하고, 2챕터 정비소 방, 1·2챕터 보스전 사망 시 종소리 1회와 정적 유지, 보스 처치 뒤 필드 복귀·씬 전환의 단일 플레이어 유지도 검사한다.

## 튜토리얼 피드백 회귀 검사

- 장애물: `StartingVillageOpeningGroundTests`는 넓은 흙 장식 제거·벽 충돌 유지·울타리 필터/대비 및 컬러·스케치 지형을 검사하고 캡처한다.
- 대사: `StartingVillageOpeningTests`는 실제 키 입력으로 전체 도입부를 진행하며 왼쪽 주민의 지나간 뒤 혼잣말과 각성 대사를 검사한다.
- 지워진 주민: `StartingVillageOpeningTests`는 컬러 주민의 대기 프레임이 넘어가고, 앞선이 두 주민을 삼킨 뒤에는 0.8초 동안 프레임이 그대로인지 검사한다.
- 화면·입력: `StartingVillageOpeningTests`는 양방향 흔들림 중 고정 마스크와 불투명 검정 바탕, 방향키 안내를 검사한다. `GamepadRuntimeTests.OpenTutorialPromptsFollowPadAndKeyboardWithoutRestartingTheLesson`은 키보드·패드 전환 후 안내와 시범 재생 시간을 검사한다.
- 무기 선택: `StartingVillageOpeningTests`는 출발 준비의 2개 선택 제한 표시와 선택 해제 복원을 검사한다. 실제 화면 캡처는 `outputs/hub/capture/opening-table-two-selected.png`와 `opening-table-one-deselected.png`에 저장한다.

## 색상 잉크 재설계·슬롯별 비용

Unity 6000.6.5f1 EditMode에서 `ForestCombatV2.Tests.WeaponGrowthMvpTests`, `ForestCombatV2.Tests.InkRedesignRuntimeTests`, `Deviant.Run.Tests.RunEconomyTests`, `Deviant.Combat.Tests.PlayerActionArbiterTests`를 실행한다. 강화 비용의 슬롯·무기별 독립성, 최대 단계·골드 부족의 무차감, 2스테이지 보상 범위와 양 끝값, 이전 여의봉 ID 호환, 범위의 네 방향 단일 확대, 원타격 피해 증가, 덧칠의 자기 재발동 금지와 범위 증폭 피해의 비중복, 교대 기본 공격의 후딜 취소와 1회 보너스, 기절 대상별 5/20초 제한, 독의 높은 중첩·피해 유지와 만료 초기화, 충전 회복 진행률 보존, 표식 전이 보너스의 중복 곱 방지, 풀컨디션의 적중 시 체력 조건을 검사한다. 무아트 테스트 전투장을 사용하므로 실제 맵의 아트·UI 가독성 및 난이도 체감은 별도 플레이 확인 대상이다.

생성물은 `python tools/combat/export_weapon_growth.py --check`, `python tools/combat/export_chapter1_economy.py --check`, `node tools/ink-combination-lab/generate-color-parts.cjs --check`로 확인한다. 강화 수치 원본은 `ink_color_parts_tuning.json`의 `levelTable`이며 HP·개수는 정수다.

## 인벤토리 아이템 카드 선택

`ApprovedCoreVerification.RunInventory`의 실제 씬 검사는 위 [가방·제작 채택 화면](#가방제작-채택-화면) 절의 현재 통합 경로를 사용한다. 별칭의 ID 귀속·게임 상태 불변과 잘못된 입력·초안·파괴 무기 거부는 `SketchArsenalTests.RenameTracksTheWeaponAcrossSlotsWithoutChangingItsCombatState`로 함께 검사한다.

Unity 6000.6.5f1 EditMode의 `ForestCombatV2.Tests.InventoryItemHitAreaTests`는 가방·장비·제작 잉크·팔레트 칸 크기에서 비정사각형 아이콘과 여러 줄 이름의 가로 중심, 아이콘·이름·중간 여백·카드 모서리의 실제 uGUI 레이캐스트와 클릭 전달, 인접 카드의 독립된 선택 영역을 검사한다. 선택 버튼을 만든 같은 프레임의 첫 렌더, 이름 길이 변경과 비활성화·재활성화에서도 강조 배경이 현재 글자 크기로 표시되는지 확인한다. 첫 캔버스 갱신의 `CanvasRenderer` 메시가 존재하고 현재 강조 크기와 일치하는지도 검사해, 이미지가 켜져 있지만 도형은 다음 프레임에 생기는 깜빡임을 검출한다. 전체 런과 화면의 미술 검토는 별도로 확인한다.

## 공통 인벤토리·정비소 책

Unity 6000.6.5f1 EditMode의 `ForestCombatV2.Tests.Chapter1NativeEquipmentBookTests`는 실제 절차 생성 씬에서 800×440 프레임·인벤토리 2탭·팔레트 3/4/5칸·초록 빈 칸·장착 잉크 조회, 즉시 슬롯 강화 결제와 단계, 색 변경 중 후보 재선택·대상 유지·판매/파괴 숨김·취소·100G 결제·기존 잉크 반환·슬롯 레벨 보존, 스탬프 적용·25G 판매와 중복 판매 거부, 공통 167×39 버튼, 성장 탭과 잉크 판매대 구매·판매 완료·잔액 부족을 검사한다. 그래픽 장치가 있는 같은 버전의 격리 프로젝트에서 `-nographics` 없이 실행한다. 실제 씬 1920×1080 캡처는 Git 제외 `outputs/hub/capture/native-*-approved.png`에 저장한다. 이 문단은 검사 절차와 기대 동작이며 실제 통과 여부는 해당 실행의 결과 XML·로그와 기능 커밋에 기록한다. `Chapter1InventoryInkTests`는 전투 중 무료 교체·반환·명시적 파괴, 인벤토리와 Esc 메뉴의 정지·재개을, `Chapter1FirstRunInkGuideTests`는 첫 런의 같은 팔레트 칸·대상 링·획득 파츠 설명과 안내 종료를 확인한다. UI 비교 자료의 브라우저 검사는 실제 Unity 검사를 대신하지 않는다.

## 전투 HUD 체력 게이지 연출

Unity 6000.6.5f1 EditMode의 `Deviant.Presentation.Tests.HealthBarInkTests`는 첫 관측의 무연출 기준값, 회복의 바닥 게이지 지연·붓질 진행·바닥 게이지로의 합류와 0.5초 회복량 합산, 피해의 즉시 감소·번쩍임·연해짐·오른쪽부터 지우기·종료, 보호막의 민트 칸·틀 덧그리기·깨짐 뒤 틀 풀림과 시간 감소의 무번쩍임, 최대 체력을 넘는 보호막의 분모 확대, 게이지 기울기 마스크와 리셋, 도트 체력판의 판·초상 원 없음·빨간 게이지·숫자 크기(현재 체력 14줄, `/ 최대` 10줄)·피격 흰 번쩍임·초록 숫자와 `+N`을 검사한다. `ForestCombatV2.Tests.HealthInkHudTests`는 실제 절차 생성 씬의 HUD에서 도트 체력판 교체(기존 판·숫자 숨김, 초상 그림 유지·판 앞, 흔들림 묶음 소속), 회복 시 게이지 대기·`+N`·합류, 보호막 칠과 깨짐 뒤 정리, 피해의 즉시 감소를 검사하고 회복·보호막·피해 화면을 `outputs/hub/capture/health-ink-*-1920.png`로 남긴다. 기존 `ApprovedCombatHudIntegrationTests`·`Chapter1HudKeyAndBagMessageTests`로 HUD 연결과 피격 흔들림을 함께 확인한다. 실제 전투의 가독성·연속 회복/피해의 체감은 플레이 확인 대상이다.

## 검 기준 기본 피해와 방패 패링

`Deviant.Combat.Tests.WeaponBalanceReleaseTests`는 무강화·피해 변동1의 실제 공통 전투 입력에서 10형태의 1·3명 콤보 피해·동시 대상 수와 원본 공속 적용 모션·발동을 검사한다. 발사체는 생성점에서 대상에 접촉하는 조건이며 비행 중 회피·실제 AI·플레이 숙련도는 범위 밖이다. 방패는 첫 성공 뒤 재차단 불가·근접 반격26·성공0.10초 복귀·0.60초 재무장, 실패·후방·창 종료·투사체 반사와 쿨타임 환급/재설정에 대한 하한을 검사한다. 기본 피해와 별개인 공격 모션·타별 발동·활성창은 기존 원본을 유지한다. 대검은 기본공격만 몸 앞끝부터의 전방 판정이며 특수 4종은 패치 이전 범위를 사용한다. 모아내려찍기의 뒤쪽 여유, 반월참·회전 전진의 시전자 좌우, 낙인 대검의 설치 중심 좌우 적중을 검사한다. 같은 검사 묶음의 `GreatswordBasicExcludesTheRear`·`AllTwelveRangedSkillsDamageThreeTargetsAndVisitDoesNotRepeat`와 `GreatswordSpecialsRetainOriginalBoundsAndHitBothSides`가 좌우 시선의 기본공격 뒤쪽 비적중·특수 4종 양쪽 적중·원본 피해와 타격 수, 원거리12스킬의3대상 피해·산탄 대상별100 상한·이동 수리검의 한 번 뒤이동과 복귀를 검사한다. 실제 난이도와 그림·소리의 최종 체감은 플레이 확인 대상이다.
