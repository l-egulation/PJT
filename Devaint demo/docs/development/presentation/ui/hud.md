# HUD 구현과 연결 범위

[분야 개요](README.md)

1·2챕터 플레이 화면은 `TemporaryGameGui`의 [채택 전투 HUD·출발 팔레트](combat-hud-approved.md)를 사용한다. 아래 `BattleHUDv2`는 별도 프리팹 기반 HUD 경로이며, 해당 프리팹 수정으로 현재 챕터의 전투 HUD 배치가 바뀌지는 않는다.

현재 HUD는 소유자별 제어 요청과 실제 ScreenRouter/ScreenInputBinding을 사용한다. GameComposition이 기존 UI 모듈의 Cancel 액션과 EventSystem을 명시적으로 주입한다. 격리 NPC/대화와 다른 제품 화면 전체 등록은 별도 범위다. 전체 목표는 [GUI 구조](../../architecture/presentation-narrative-ui.md#5-gui와-공통-제어), 라우터 계약은 [화면 포커스·Back](screen-routing.md)을 따른다.

`Assets/Deviant/UI`의 HUD는 [공통 게임](../../architecture/architecture.md)에 연결한다. 프리팹은 `Assets/Deviant/UI/Prefabs/BattleHUDv2.prefab`이다. HUD 담당자는 Prefab Mode에서 배치를 편집하거나 `Deviant.UI.Editor.BattleHudBuilder.Build`로 제작 레이아웃을 다시 가져온다. 이 함수는 프리팹을 갱신하므로 수동 편집 내용과 구분한다.

BattleHudView는 IBattleHudDataSource.Read로 표시를 읽고 BindControls(IControlRequests, ScreenRouter, InputAction, EventSystem)로 명시 제어·라우터·기존 Cancel 액션·포커스를 받는다. GameComposition이 실제 HP/최대 HP/무기와 전투 세션을 제공한다. BattleHudDataSource는 프리팹 샘플 공급자다. HUD는 빌린 액션을 활성화/비활성화하거나 라우터를 Dispose하지 않으며 제어 미연결 미리보기는 전역 시간을 바꾸지 않는다.

경험치·레벨은 정식 성장과 연결되지 않았다. 공통 게임에서는 `InventoryGameBinding`이 가방 버튼을 [개별 잉크 가방·임시 정비 UI](../../gameplay/equipment/inventory-and-loot.md)에 연결하고 실제 용량을 표시한다. 연결되지 않은 프리팹 미리보기에서는 기존 안내 창을 사용한다. 혼합·통화·용량·잉크 삽입 위젯은 런타임에서 숨긴다. Combat V2의 F1 장비 패널은 개발용 장비 공급 도구이며 실물 가방과 별개다.

## 사망 전환

1·2챕터의 `PlayerDeathPresentation`은 기존 피격 VFX·붉은 테두리·HUD와 별도로 `CameraOutputBinding`에 0.28초의 전용 충격을 합성한다. 캐릭터 위치·속도·카메라 추적 기준은 쓰지 않는다. `PlayerDeathSequence`는 현재 무기의 사망 클립이 시뮬레이션 시간으로 완료되는 것을 기다리고, 여운 0.1초 뒤 실제 시간 1.8초의 부드러운 전체 암전과 0.25초의 검은 화면을 진행한다. 외부 일시정지는 기존 피드백 시계에서 존중한다.

암전은 비입력 `FadeOutputBinding` 캔버스(정렬 110)로 HUD까지 덮는다. 결과 소유자는 완전 암전 후에만 기존 정산·시뮬레이션 정지를 실행한다. 기존 결과 메뉴가 실제 활성화된 다음 암전 캔버스를 90으로 내려 결과 캔버스(100) 뒤에 검은 화면을 유지한다. 부활·F5·씬 해제·비활성화에서 충격과 암전을 정리하며, 반복 사망 알림은 시간을 늘이지 않는다.

2챕터 클리어(런 끝)는 출구 문이 열리면 `Chapter2World`가 입력을 막고 `PlayerDeathPresentation.BeginClearBlackout`으로 같은 1.8초 암전과 0.25초 검은 화면을 진행한 뒤 결과를 연다. 카메라 충격과 사망 모션 대기는 없다(`PlayerDeathSequence.BeginClear`). 결과가 뜨면 사망과 같이 암전 캔버스를 90으로 내린다. 1챕터는 클리어 결과 없이 2챕터로 이어진다. 결과 화면은 일시정지 메뉴와 같은 메뉴를 쓰지만 일시정지 전용 「내구도: 게이지/숫자」 버튼은 결과 화면에서 숨긴다(QA 2026-11-13, 결과 줄을 가리던 문제).

2챕터 데모 완료 창의 플레이 시간·처치 수·마법안료는 정산된 `SourceRun`의 런 전체 합계를 표시한다. 런 세션이 없는 2챕터 단독 실행만 해당 챕터의 값을 표시한다. 완료 창은 공통 메뉴 뷰포트의 연속 배율을 사용해 1280×720에서도 960×540의 작은 영역으로 남지 않고 화면비에 맞춰 표시한다.

2챕터 보스의 스케치 보상은 마지막 처치 모션과 여운이 끝난 뒤 연다. 런을 이어 받은 장면에서는 `Chapter2World`가 보상을 한 번 열고, 런 세션이 없는 단독 실행에서만 출구가 같은 종료 시점을 기다린 뒤 보상을 대신 연다. 보상 창의 일시정지가 처치 모션의 시계를 먼저 멈추지 않도록 두 경로의 소유자를 구분한다.

## 입력·일시정지

HudControlPresenter는 Unity 비의존 화면 핸들과 자체 포인터/닫힘 프레임 차단을 소유한다. 실제 기존 대화상자의 CanvasGroup·EventSystem·닫기 버튼을 ScreenView로 연결하고 라우터가 전투 제한/선택적 정지·포커스를 소유한다. 가방은 입력 제한만, 일시정지는 정지를 요청하며 일시정지 중 가방/기존 닫기 동작을 유지한다. ScreenInputBinding이 기존 UI 모듈 Cancel을 한 번 소비하고 미소비 Back만 일시정지를 연다. 소비된 Back을 뒤 화면에 재전달하지 않는다. 비활성화/재바인딩은 자기 핸들만 반환하고 오래된 expected unbind는 새 연결을 닫지 않는다.

공통 전투는 세션 정지 중 자동/수동 틱을 진행하지 않는다. `ControlSession`은 소유자별 `IFeedbackTimeScope.RequestSlow(scale, accentSeconds)` 요청 중 가장 느린 유효 배율을 선택하며 정지가 항상 우선한다. 조립이 `AdvanceAccent(realDelta)`로 실제 시간 TTL을 진행하고 일시정지 동안 잔여 시간을 보류한다. 요청 만료·취소·소유자 종료는 다른 소유자의 입력 제한·정지·슬로우를 해제하지 않는다. 발동 조건은 요청 소비자가 별도로 소유한다.

본 게임은 `GameComposition`에서 명시적으로 단일 Unity 시간 어댑터를 연결해 기존 세계/물리 정지도 유지한다. 이 어댑터만 `Time.timeScale`을 쓰며 바인딩 때 고정한 기준 배율에 현재 합성 배율을 곱한다. 정지 해제 때는 남아 있는 슬로우 요청을 다시 적용하고 바인딩 종료 때 기준 배율로 반환한다. 실험장의 로컬 제어는 전역 시간을 바꾸지 않는다. 직접 시간 배율을 쓰는 다른 경로를 병행하는 계약은 제공하지 않는다.

제한/정지 중 입력은 버리고 해제 뒤 눌림을 예약 실행하지 않는다. HUD 포인터·열림/닫힘/Back 프레임도 전투 입력을 막아 같은 클릭이 공격으로 새지 않게 한다. 버튼 검사는 매 프레임 배열을 만들지 않는다. UI가 기존 행동 예약을 임의 취소하지 않으며 외부 소유 제한/정지는 남는다. 실제 HUD는 기존 키보드/패드 Cancel 매핑을 재사용하고 다른 화면의 확인/포인터 전체 라우팅은 후속 범위다.

## 한글 글꼴

`Assets/Deviant/UI/Resources/UI/KoreanHUD.asset`에 현재 HUD 문구·무기/잉크 이름·숫자/기호를 포함한 정적 SDF 아틀라스를 제공한다. `BattleHudView.Awake`는 이 글꼴과 해당 아틀라스의 머티리얼을 함께 연결한다. 플레이어에서 원본 TTF나 운영체제 폰트를 불러오지 않아도 현재 문구가 표시된다. 글꼴만 바꾸고 예전 머티리얼을 유지하거나 초기 시안 글자만 들어 있는 폰트 에셋을 재사용하지 않는다.

HUD 문구/콘텐츠 이름을 추가하면 `Tools > deviant > UI > Bake Korean HUD Font` 또는 `Deviant.UI.Editor.HudKoreanFontBuilder.Build`를 실행한다. 생성기는 체크인된 `malgun.ttf`를 직접 읽고 현재 UI/게임 코드와 전투/인벤토리 카탈로그의 한글을 모아 아틀라스를 만든다. 메인 에셋 GUID를 유지하고 머티리얼/텍스처를 같은 에셋에 포함한다. HUD 프리팹 생성도 같은 글꼴을 사용한다. 지역명은 기존 표시 영역을 넘으면 줄임표로 표시한다.

`Deviant.Inventory.Tests/HudKoreanFontTests`는 실제 표시 문구·전체 숫자의 커버리지, 원본 폰트 없는 정적 표시, 올바른 셰이더/아틀라스 연결과 한글 메시의 실제 글자 사용을 검사한다. 전체 11,172개 한글이나 미래의 모든 문구를 포함하는 폰트가 아니므로 새로운 문구는 재생성/커버리지 검사를 함께 수행한다.

## 배치·게이지

Screen Space Overlay Canvas와 기준 해상도1672×941 CanvasScaler를 사용한다. 이미지 종횡비를 유지하며 Canvas 너비1400 미만이면 가방 그룹을 위로 옮긴다. [PreservedGauge](../../../../unity/Assets/Deviant/UI/Scripts/PreservedGauge.cs)는 현재/최대 비율을0~1로 제한하고 마스크로 폭을 자른다. 최대값0 이하 또는 현재값0이면 채움 마스크를 숨긴다.

자동 검사는 `GameIntegrationTests`의 실제 HP 연결·입력 차단·일시정지, `Deviant.Control.Tests`의 순수 소유권/Presenter, `ControlBindingTests`의 두 씬 입력/정지·중첩 소유자·비활성화/재바인딩·시간 어댑터 수명 검사를 사용한다. 수동으로 해상도별 배치·클릭·ESC·게이지 끝값을 확인한다. 외부 이미지 복원 없이 수행한 구조 검사만으로 화면 검증을 완료했다고 하지 않는다.
