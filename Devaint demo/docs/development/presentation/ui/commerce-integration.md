# 보상·상점 공용 UI 연결 준비

[UI·입력 구현](README.md) · [현재 보상 UI](reward-choice.md) · [보상·상점 규칙](../../../design/run/rewards.md)

`Assets/Deviant/UI/Integration`은 실제 런 모델을 읽고 명령을 전달하는 순수 C# 어댑터다. 공용 어댑터 자체는 기존 게임 화면에 주입하지 않는다. 현재 실제 화면은 `SketchRunBinding.RewardChoice`와 `TemporaryGameGui.ApprovedCore/ApprovedShop`에서 동일 런 모델에 직접 연결하며, 지급·구매는 한 경로에서만 수행한다. 실제 화면과 입력 범위는 [임시 플레이 GUI](temporary-gui.md)를 따른다. `Deviant.UI.Integration`은 `Deviant.Run`만 참조하며 `autoReferenced: false`, `noEngineReferences: true`다. HUD와 타이틀은 적용 범위가 아니다.

## 기본 연결

이 어댑터로 호출부를 통합할 때에는 **`new RunCommerceAdapter(Arena.Sketch, host)` 생성자**를 쓴다. `SketchEquipment.ChooseReward`·`Buy`가 실제 `SketchRunState` 지급을 수행하고 미리보기 무기를 런 실행 래퍼로 연결하는 흐름까지 유지한다. `SketchRunState`·`RunEconomy` 직접 생성자는 `Arena.Sketch`가 없는 구형 바인딩에 한해서 사용한다. 모델을 복제하거나 프리뷰 fixture로 교체하지 않는다.

```csharp
// 앞으로 적용할 host 연결 예. 현재 런에서 이미 생성한 객체를 그대로 전달한다.
var commerce = new RunCommerceAdapter(arena.Sketch, host);
var view = commerce.Snapshot(openFacilityId); // 상점이 없으면 null
var key = view.Rewards[0].Key;               // 카드에 이 객체를 보관한다.
var result = commerce.Choose(key);
view = commerce.Snapshot(openFacilityId);   // 완료된 명령 뒤에 다시 읽는다.
```

`Snapshot(facility)`의 첫 호출은 기존 `ShopStock` 규칙대로 해당 시설의 진열을 최초 생성한다. 실제 `ShopStock.Count`만큼 반환하며 상점 확장에 따른 5무기·5잉크 진열도 그대로 전달한다. 잉크 공급 목록이 비어 있으면 모델에 있는 무기만 반환하며 가짜 빈 상품을 만들지 않는다. 기존 보유 잉크도 다시 획득할 수 있으므로 외부 획득만으로 진열을 교체하지 않는다. 실제 리롤은 판매 완료 자리를 유지하고 미판매 무기/잉크만 바꾼다. 바뀐 상품의 키만 무효화하며, 변하지 않은 자리·품절 자리의 키는 유지한다.

표시 계약은 실제 `FormId`·`SkillId`·`InkId`, 강화 슬롯 배열, 실제 가격·골드·보관 보상 수다. 표시 이름·설명·아이콘은 기존 카탈로그로 해석한다. 불변 스냅샷에 런의 수정 가능한 배열을 노출하지 않는다. 카드 선택은 배열 번호 대신 세션 전용 `CommerceKey`를 전달한다. 보상 교체, 상점 시설·스테이지 변경, 잉크 리롤 후의 오래된 키는 거부한다. 같은 진열의 단순 다시 읽기는 키를 유지한다. 이미 구매한 자리는 품절이며 실패한 잔액 검사는 골드·상품을 소비하지 않는다.

성공한 선택·구매는 먼저 실제 가방/도감에 지급한 뒤 `Receipt`를 만든다. 무기 영수증에는 실제 `SketchWeapon.Id`가 들어간다. `ContinueReceipt(receipt.Key)`는 영수증을 닫는다. `ConfigureReceipt(receipt.Key)`는 이미 획득한 무기/잉크를 가방에 선택하고 영수증을 닫는다. 가방에서 돌아올 때 보상을 다시 고르거나 지급하지 않는다. 대기 보스 보상은 현재 영수증 뒤에 표시하며 남은 일반 보상을 자동으로 열지는 않는다. 기존 출구·G 키 흐름이 계속 다음 보상을 연다.

## Host 소유 범위

- `SetCommercePaused(bool)`는 전용 `IControlScope` 한 개를 획득·해제한다. 기존 `SketchRunBinding.UpdatePause`처럼 전투/상호작용 입력과 시뮬레이션을 정지한다. 상점·튜토리얼·다른 화면의 pause를 해제하지 않는다.
- `OpenBag(Guid?, string)`은 기존 공용 가방의 선택과 열기를 요청한다. `null/null`은 비교다. `CompareBag()`은 현재 후보를 유지하고 가방 중 선택을 차단한다. 영수증에서 구성할 때는 획득 ID를 전달한다.
- 가방 닫힘을 감지하면 `BagClosed()`를 호출한다. 후보가 남아 있으면 계속 정지하며, 영수증 구성 후에는 현재 후보가 없는 경우 이 어댑터의 pause만 해제한다.
- 호스트 콜백은 성공적으로 완료되어야 한다. 모델의 동기 `Changed` 콜백 안에서 화면을 다시 읽지 말고 다음 UI 갱신으로 예약한다. `Choose`/`Buy`가 여러 중간 이벤트를 발행하므로 명령이 끝난 뒤 `Snapshot`을 읽는다. 명령 도중 재진입 명령은 `Busy`, 스냅샷 재진입은 예외로 거부한다.
- 런/화면 소유자가 폐기될 때 `Dispose()`한다. 어댑터가 가진 pause만 해제한다. 가방 자체의 열기/닫기와 다른 소유권 정리는 호스트 책임이다. 한 런의 활성 커머스 UI에는 어댑터 인스턴스 하나를 사용한다.

## 실제 적용할 호출부

| 위치 | 앞으로 할 연결 |
|---|---|
| `ForestCombatV2/Runtime/SketchRunBinding.cs`의 `Attach` | 기존 `arena.Sketch`가 준비된 뒤 어댑터 생성. `ForestCombatV2.asmdef`에 명시적 assembly reference 추가. |
| `SketchRunBinding.RewardChoice.cs`의 `RefreshRewardScreen`, `TryChooseReward`, `ContinueReward`, `ConfigureReward`, `ToggleRewardBag` | 기존 표시/포커스/튜토리얼 검사를 보존하고 어댑터 스냅샷·키·영수증 명령 연결. 기존 지급 호출과 새 지급 호출을 동시에 실행하지 않는다. |
| `SketchRunBinding.cs`의 `DrawShop` | `OpenShopFacility`의 실제 시설 ID로 실제 진열 수의 스냅샷을 읽고 `Buy` 호출. 기존 상점 pause 소유권은 유지. |
| `TemporaryGameGui.RunInks.cs`의 `SyncSketchBinding` 인근 공용 가방 연결 | 기존 `SharedBagOpen`·`SharedBagVisibility`·`SharedBagSelection`으로 host 구현. 실제 닫힘 시 `BagClosed`. |
| `TemporaryGameGui.RunInks.cs`의 바인딩 없는 보상 fallback | 적용할 때만 동일 어댑터 소유권으로 통합. 주 바인딩과 fallback 두 군데에서 지급하지 않는다. |
| `SketchRunBinding.cs`의 `OnDestroy` | 어댑터 Dispose 및 호스트가 가진 제어 scope 정리. |

기존 입력 소비, 첫 보상 튜토리얼, 포인터 press/release 일치, 포커스 복원, 출구 처리, 사망/종료 처리는 host/presenter에서 유지해야 한다. 이 순수 어댑터가 전체 화면 presenter를 대신하지 않는다. 일반 어셈블리 참조가 없는 `Assembly-CSharp`에는 `autoReferenced: false` 어셈블리가 자동 노출되지 않으므로 현재 asmdef 바인딩을 통해 연결하거나 적절한 host asmdef를 마련한다.

## 잉크 부여 대상

확정한 무기는 빈 슬롯에도 잉크를 추가할 수 없다. `SketchArsenal.Confirm`이 빈 슬롯까지 고정하며 `PlaceInk`가 확정 무기 수정을 거부하는 현재 게임 규칙을 유지한다. **지금 부여하기의 대상은 미확정 스케치 무기뿐이다.** 호스트 가방은 획득 잉크를 선택한 다음 미확정 무기만 부여 대상으로 안내해야 한다. `ConfigureReceipt`는 선택한 잉크 ID를 전달할 뿐, 모델 제약을 우회하거나 자동 부여하지 않는다. 미확정 무기가 없으면 잉크의 남은 부여 횟수는 보존되며 가방을 닫아 계속할 수 있다.

잉크는 획득할 때마다 현재 카탈로그 기준 10회분을 받는다. `Snapshot.Inks`는 실제 `Remaining`·`Reserved`·`Available`을 불변 값으로 제공한다. 임시 배치는 1회분을 예약하고 제거/폐기는 예약을 풀며, 제작 확정할 때만 실제 횟수를 차감한다. 남은 횟수에는 임시 예약분도 포함되므로 부여 가능 여부는 `Available`로 확인한다. 동일 잉크 재획득은 사용 횟수를 추가하며 도감 기록은 소진 후에도 유지한다. 6칸 팔레트의 강화 칸은 최대 2개이고 두 칸은 같은 색이다.

## 에셋 적용 계약

어댑터는 이미지·폰트·Unity 리소스를 배포하거나 로드하지 않는다. 미디어 원본은 `resources/`에서 관리하고, 선택한 리소스 커밋과 `docs/assets/manifests/unity-deployment.json` 계약을 `tools/assets/update_assets.py`로 배치한다. 외부 공유 드라이브용 export는 만들지 않는다. 새 테마 이미지 배치는 별도 승인된 리소스 경로와 메인 소유 `.meta`/GUID를 유지해야 한다.

무기 그림은 `SketchRewardArt/Weapons/{formId}/{skillId}` → `SketchRewardArt/Weapons/{formId}` → `ApprovedItemIcons/{formId}` 순서, 일반 런 잉크 그림은 생성 `RunInks.json`의 `icon`을 사용한다. 상세 가져오기와 누락 fallback은 [현재 보상 UI](reward-choice.md)의 계약을 따른다. 표시 이름을 에셋 키로 쓰지 않는다.

## 격리 검증 구성

Unity **6000.6.5f1**와 원본 `Packages/manifest.json`·`packages-lock.json`을 사용한 별도 프로젝트에 다음을 복사한다. 원래 실행 씬을 열거나 본 게임 스크립트를 바꾸지 않는다.

- `Assets/Deviant/Run/Runtime` 전체와 `.meta`
- `Assets/Deviant/UI/Integration` 전체와 `.meta`
- `Assets/Deviant/Combat/Resources/RunInks.json`과 `.meta` (기본 게임 경로 생성자 2개 테스트가 실제 카탈로그를 읽는다)

Unity Test Runner의 EditMode에서 `Deviant.UI.Integration.Tests`를 실행한다. CLI 예:

```sh
"/Applications/Unity/Hub/Editor/6000.6.5f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics -projectPath /absolute/path/to/isolated-project \
  -runTests -testPlatform EditMode -assemblyNames Deviant.UI.Integration.Tests \
  -testResults /absolute/path/to/results.xml -logFile /absolute/path/to/editor.log
```

테스트는 실제 런 모델/플래너를 사용해 지급 ID, 잉크 10회 지급·재획득·예약·확정 차감, 영수증·가방·보스 순서, 중복/재진입 구매, 잔액 부족 후 재시도, 시설·스테이지·잉크 교체의 오래된 선택 차단, 상점 확장·부족한 공급 목록·리롤 품절 보존과 폐기 시 pause 해제를 검증한다. 카탈로그 기반 테스트는 기존 `SketchEquipment` 미리보기 객체의 실제 모델 채택까지 확인한다. 이 검증과 본 게임 화면의 시각·입력·pause 통합 검증은 별개다.
