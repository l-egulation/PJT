# 코드 구조

[분야 개요](README.md)

로컬 관찰용 리플레이는 `Assets/Deviant/Replay/Runtime`의 Unity 비의존 `Deviant.Replay`(레코드·사전/변화분·경로 절감·gzip 코덱·파일 보관), `Replay/Unity`의 `ReplayUiTarget`, 기존 기본 어셈블리 `Game/Runtime/ReplayRecordingHost`의 제품 씬 조립으로 나눈다. host는 이미 소비된 액션·명령/명중 polling·읽기 모델과 GUI 상태만 관측하고 게임 명령을 실행하지 않는다. 기본 기록은 켜짐이며 서버 주소가 있으면 자동 전송한다. 기록·전송은 제품 화면의 버튼이나 설정 창 없이 백그라운드에서 진행한다. 기존의 명시적인 중단 설정은 유지한다. Editor 도식 관찰 창과 파일 검사/시험은 `Replay/Editor`, `Replay/Tests`다. `ReplayUploadClient`는 자동 수집 대상인 새 파일의 player binding이 붙은 조각/세션 상태를 Go 수집 서버에 전송한다. 서버/SQLite/파일 구성은 [서버 명세](../telemetry/replay-server-storage.md)를 따른다. 실제 전체 런의 장시간 품질·용량 수락은 별개이며 [기록 명세](../telemetry/replay-recording.md)를 따른다.

개발 프로젝트는 `unity`다. 현재 제품 진입은 `Chapter1Title` → `StartingVillage` → `Chapter1Procedural`이며 상세 경로와 실행 방법은 [플레이 테스트](../../getting-started/playtest.md)를 따른다. 구형 공통 게임 씬은 현재 진입점이 아니다. 이동·공격·체력·상태이상·적 전투는 `Deviant.Combat` 한 구현을 사용한다. 수치의 기준은 문서와 `data/combat/`다.

이 문서는 현재 구현 구조를 설명한다. 리팩토링 후 폴더·레이어·객체 소유권·계약은 [프로젝트 목표 구조](project-structure.md), 게임플레이 실험 방식과 이행 단계는 [목표 아키텍처 제안](gameplay-architecture.md)을 따른다. 목표 전체는 이행 중이며 현재 적용 범위는 아래와 같다.

다중 장치 입력은 `Input/Contracts`의 액션 계약, `Input/Unity`의 Input System 소스·공유 프레임 조회·uGUI 연결, `Integration/Runtime/Input`의 순수 전투 변환기로 나눈다. `CombatKeyboardInput`은 씬/편집 도구의 호환 이름을 유지하며 모든 장치의 공통 액션을 기존 ingress에 접수한다. 기본 배치는 `Input/Resources/Input/PlayerControls.inputactions`가 소유한다. 타이틀·마을·챕터 1/2의 전투·상호작용·대화·공통 GUI를 연결하며 구조·수명·기본 조작은 [다중 입력 장치 구조](../presentation/ui/input-devices.md)를 따른다.

카탈로그 직렬화 타입은 `Assets/Deviant/Definitions/Runtime/Combat/CombatCatalog.cs`의 `Deviant.Definitions` 어셈블리로 분리한다. 이 어셈블리는 `noEngineReferences`로 UnityEngine 참조를 금지한다. 기존 JSON 필드와 `Deviant.Combat` 네임스페이스는 전환 중 직렬화 계약으로 유지하며 전투·테스트가 해당 어셈블리를 참조한다. DTO 자체는 가변 편집/직렬화 입력이며 실행 중 원본이 아니다. `CombatCast.Create`는 입력 전체를 불변 `SkillExecutionDefinition` 그래프로 한 번 캡처하고 `Definition`만 공개한다. hit/box·이동/대상 box·효과/box·무적 배열·폭탄/원거리 발사/자식 투사체·미사일까지 같은 시작 그래프를 읽으며 가변 `CombatCast.data` 경로는 제공하지 않는다. 원본과 배열 편집은 다음 Create에만 반영된다. 기존 근접/직접/차지 정의와 해시, 활성화 시점 스탯/자원 원본과 실제 Native 기하/시간 순서는 유지한다. 불변 입력 이행은 범용 행동/효과 실행기의 순수 이행 완료와 구분한다.

`ArenaTickWorld`는 한 월드 전용 `CombatTickSnapshot`의 재사용 참조 버퍼로 행동·행동 밀기·적 탄·폭탄·플레이어 탄·레이저·폭발 표현의 진입 시점 목록을 순서대로 고정한다. 행동과 밀기는 각 단계에서 독립 재캡처하므로 콜백 중 추가·제거·교체는 이전 `ToArray`와 같은 경계에 반영되고 실제 호출 직전에 Unity 파괴 여부를 검사한다. 단계 예외도 `finally`에서 참조를 비우며 최대 용량만 재사용한다. 기존 `CombatSimulation`의 Step/Reset 재진입 거부와 부분 실패/리셋 정책을 그대로 사용하고 중첩 실행용 버퍼나 새 거부 정책을 만들지 않는다. 새 최대 개체 수일 때만 버퍼 용량을 늘리며 전체 액터/물리 틱의 무할당을 뜻하지 않는다. 적의 live 열거와 분신의 역순 만료는 바꾸지 않는다.

일반 이동·기본 대시·대시 점프 취소는 `data/combat/locomotion/player.json` → `PlayerLocomotion.json` → `Deviant.Definitions.LocomotionProfile` 경로를 사용한다. `CombatArena.Movement`는 검증된 불변 값·내용 해시를 보관하고 본 게임/시험장의 공통 `CombatPlayer`와 대시 표현이 이를 읽는다. `TryApplyMovementAndReset`은 초기화 완료 후 유효한 프로필만 적용하고 기존 테스트 리셋으로 행동·효과·예약·자원을 초기화한다. 잘못된 데이터는 적용과 리셋을 모두 거부한다. 기존 점프 물리·기술별 이동은 유지한다.

이동·대시 편집은 `Tools > deviant > Experiments > Locomotion`의 UI Toolkit 창에 연결한다. `Authoring/Editor`의 `LocomotionSource`가 원본 읽기·검증·해시 충돌 검사·원자적 저장·생성물 복사를 담당한다. 창은 편집 전후 수치, 미저장 상태와 편집/실행 버전을 표시하고 리셋 적용·저장·재로드를 구분한다. Undo는 별도 임시 `LocomotionDraft`의 값만 변경한다. `SourceFiles`가 파일 해시 충돌 검사와 원자적 교체를 공통 제공하고 `LocomotionSource`·`BasicSwordSource`가 각각의 원본 스키마와 컴파일러를 연결한다. 검 편집 창은 `Tools > deviant > Experiments > Basic Sword First Hit`에 있으며 다음 실행/리셋 적용과 원본 저장을 구분한다. 저장 시 기존 Python 생성기로 문서·카탈로그를 재생성한다. 아래 고정 A/B는 검 1타와 기본 대시에 한정되고, 재사용 Lab 시나리오는 지원하는 전체 초기 정의를 별도로 캡처한다. 기존 고정 물리 스텝의 경계 처리를 유지하므로 임의 대시 지속시간의 마지막 tick을 정확히 나누는 처리는 별도 행동 시간표 범위다.

검·둔기 **7형태의 기본 17타**를 `BasicMeleeCatalog`/`BasicMeleeDefinition`으로 컴파일해 피해·모션 길이·타격 시각·활성·판정 박스와 내용 해시를 고정한다. 1타 편집 원본은 `actions/base_sword_basic_0.json` 한 곳이며 `melee_weapon_design.json`의 `hit_sources`가 이를 참조해 `MeleeCatalog.json`을 생성한다. 나머지 근접 기본 16타는 기존 `melee_weapon_design.json` 배열을 `Melee Basic Hits` 창에서 편집하고 `TryApplyBasicMelee`가 전체 후보를 검증해 다음 시작/리셋 적용한다. 형태 공통 활성시간의 영향 ID·전체 diff와 진행 중/다음 정의 버전을 구분하며 저장은 해시 충돌 검사와 원자적 교체 후 기존 생성기를 사용한다. `GetBasicMelee`·`BasicSwordFirst`·`BasicSwordSecond`는 같은 불변 정의를 조회한다. 검 1타의 전용 창·프레임 검사와 `TryApplyBasicSwordFirst` 경로는 유지하고 다른 후보로 우회하지 않는다. 기존 실행은 시작 정의를 유지하고 새 실행만 새 정의와 현재 기본 공속을 읽는다. 잘못된 후보는 적용/리셋/저장을 거부하며 적용과 저장은 별도다. 원거리·폭탄·특수·프레임/아트 저작은 이 16타 수치 창의 범위가 아니다. 자세한 절차는 [데이터 파이프라인](../data/data-pipeline.md#나머지-근접-기본-16타-수치-편집)을 따른다.

근접 기본 17타는 `MeleeActionDefinition`/`MeleeActionRunner`가 시작 속도·시간/형상, 활성화/타격 구간·지상 전진·대상별 중복·완료/취소/실패를 소유한다. `CombatCast.MeleeAction`의 호스트가 기존 Unity 스윕/차폐·피해 적용과 활성화 시점 스탯 캡처, 플레이어 완료 통지를 연결한다. 활성화는 이동 전에, 판정은 이동 뒤에, 마지막 스윕은 완료 통지 전에 실행한다. `ActionTimelineVersion`/`ActionTimelineState`/`ActionTimelineError`로 이행 여부와 상태를 조회하며 `Age`·`ImpactTime`·`MotionDuration`·`EndTime`은 이 실행 정의를 읽는다. 공개 호환 DTO의 시간/타격/전진 편집은 진행 중 검 타격을 재해석하지 않는다. 각 타격의 완료 시각은 기존 중재기로 전달하며 예약·0.30초 연계 여유·특수/교체 우선순위를 유지한다. 검 1타 전용 프레임 후보는 다른 타격/무기에 전달하지 않는다. 명시적 다음 1타 적용은 기존 API를 사용한다. 아래 직접 근접 특수 3개 이외의 특수·차지·독립 효과는 기존 경로를 유지한다.

근접 기본 17타의 AttackMotionTiming/Unity AttackMotionSnapshot은 행동 ID의 형태/타격에 맞는 본체/Cue 프레임·시각·Sprite를 시작에 고정한다. 없는 전용 아트의 기존 대체도 시작 캡처하고 다른 형태 Cue를 만들지 않는다. 같은 순수 시간 해석을 읽으며 카탈로그 실행 중 편집/제거가 진행 행동을 바꾸지 않는다. 표현 컴파일 오류는 별도 진단이고 피해·소비·행동 시간은 유지한다. 아래 근접 20개와 유한/일반 특수 23개 가족은 FrameTime 저작을 지원한다.

근접 기본 17타와 직접 특수 3개는 하나의 MeleeFrameDefinition/Compiler/Catalog로 고정 시간 또는 행동별 FrameTime을 선택한다. 공유 registry는 매체/안정 step ID/기본 비중만 소유하고 행동 override는 같은 ID의 로컬 seconds·타격/종료/전진/Cue 마커를 소유한다. 첫검은 기존 원본에 위임하며 나머지 19개는 melee_frame_bindings.json을 사용한다. 같은 시작 후보가 payload/MeleeActionRunner/본체/Cue를 함께 컴파일하고 기본 공속은 한 번, 직접 특수는 1로 해석한다. 기존 first-only FrameMeleeDefinition은 제거하며 첫검 창/프리셋은 같은 일반화 정의의 범위 제한 뷰다. 차지/원거리 FrameTime은 별도 정책 작업이다.

기존 유한 4개·일반 특수 19개는 `ActionSpecialFrameCompiler/Catalog/Definition`과 `special_frame_bindings.json`의 완전 가족으로 고정/행동별 FrameTime을 선택한다. 로컬 시간표는 같은 원래 실행 정책의 활성화·타격·이동·무적·수직 고정·해당 독립 효과/패링 시간 채널만 컴파일한다. 피해/형상·발동 자원·배치 자격·취소·독립 잔존 수명은 기존 소유자가 실행한다. 원래 committed float를 바꾸거나 양수 프레임 구간을 만들 수 없는 자동 투영은 거부하며, 완전한 로컬 시간표를 명시적으로 저작할 수 있다. 시간 0 패링과 본체 종료 뒤 독립 타격을 임의 보정하지 않는다.

진행 중 행동은 시작 payload·매체·모드 버전을 유지하고 다음 시작만 후보를 채택한다. 공유 모션 교체는 수치 원본과 전체 근접/특수 후보·매체·해시를 미리 준비한 뒤 게시한다. `TryApplyCompleteFrameState`는 수치·registry·첫검·근접 20·특수 23을 완전 사전 검사하고 리셋 예약을 확보해 한 번 게시하며 실제 리셋은 예약 해제 뒤 수행한다. 활성 FrameTime의 시간 필드는 수치 편집/저장으로 우회하지 못한다. 차지 2개·원거리 15개의 FrameTime은 별도 범위다.

나머지 원거리/폭탄 기본과 일반 특수(발사기 스킬 포함)도 `CombatCast.Create`에서 같은 본체/Cue 매체 캡처를 수행한다. 일반 특수는 시작 형태의 `attack1`/기존 대체 바인딩을 고정하고 기본 공격 Cue를 추가하지 않는다. 변환 기본은 기존 Cue 제외를 유지한다. 차지·일반 특수의 현재 ImpactTime/MotionDuration 재매핑은 시작 매체에만 적용하므로 지연 독립 타격이나 벽 회복이 표현 시간을 바꿔도 카탈로그를 다시 읽지 않는다. 첫 바인딩 실패·같은 실행의 재통지는 기존처럼 고정하고 다음 실행만 새 매체를 채택한다. 이는 현재 매체의 시작 격리이며 다른 행동의 프레임 저작이나 전용 아트 완성이 아니다.

이동·공중·착지/대시의 표시 상태 전이는 `Presentation.CharacterLocomotionExecution` 한 값 원본이 소유한다. 현재 전투 사실과 필요한 클립 길이만 입력받아 기존 사망/대시/공격/공중/지상 우선순위와 표현 dt·재점프/형태 전환·reset을 해석한다. Native Sprite/클립/색/반전과 시작 공격 바인딩/Cue는 기존 Presenter에 남으며 별도 전투 타이머나 모터 상태를 만들지 않는다.

검 강베기(`base_sword_default`)·레이피어 장침(`rapier_range`)·대검 반월참(`greatsword_range`)은 `DirectMeleeSpecialDefinition`으로 기존 카탈로그의 단일 타격/전체 메타데이터를 고정하고 같은 `MeleeActionRunner`를 사용한다. 고정 방향·수평 입력 0·정상 중력·특수 분류/발동 충전을 유지하며 기본 공속을 전파하지 않는다. 기존 공중 이동 덮어쓰기의 수평 5는 이 세 기술에 한해 기술별 수평 0 명세에 맞춰 차단한다. 실행마다 본체도 시작에 캡처한다. 대검 범위는 전용 소환 모션을 사용한다. 공개 DTO 편집과 카탈로그 수정은 진행 중 실행에 반영하지 않으며 본체 대체·첫 바인딩 실패도 시작에 고정한다. 수치 저작과 적용은 아래 경로를 사용한다.

이 세 직접 특수의 SupportedIds/SourceVersion·수치 Recompile과 비어 있지 않고 중복 없는 부분집합 TryApplyDirectMeleeSpecials가 편집 후보를 같은 실행기에 게시한다. 기존 runner/정의 해석과 자원·콜백·claim 순서는 유지한다. 창/저장/생성 경로는 데이터 파이프라인을 따른다. 다른 nearest/root/접지/push 기술을 의미가 다른 이 runner로 자동 전환하지 않는다.

레이피어 모아찌르기·대검 모아내려찍기는 ChargedMeleeDefinition이 차지 정책·단일 nearest 타격/기하·고정 방향/수평/수직·자원·실행 메타데이터를 고정한다. GetChargedMelee/ChargedMeleeCount로 조회하며 Cast는 시작 정의를 보관한다. ChargeDefinition/ChargeExecution은 같은 전투 시각/유지 표본으로 해제를 해석하고 별도 타이머/피해 원본을 만들지 않는다. 시작/예약/발동 대상 검사와 현재 판정은 같은 불변 기하를 읽고 스윕/차폐·대상 claim·발동 자원·완료/0.10초 실패 회복 순서를 유지한다. DTO/카탈로그 편집은 진행 중 판정을 바꾸지 않으며 취소/리셋은 새 해제를 막는다. AttackMotionSnapshot은 본체/대체 매체와 첫 실패를 시작에 고정하고 유지 끝 자세/해제 매핑을 보존한다. 기본 Cue·공속을 추가하지 않으며 수평 0·정상 중력을 유지한다. 대검 차지는 해제 입력까지 유지하며 1초에서 피해가 상한에 도달한다. 전용 모션과 기운 흡수 이펙트는 차지 실행 상태를 읽는다.

이 두 차지는 SupportedIds/SourceVersion·ToNumericData/Recompile과 TryApplyChargedMeleeDefinitions의 원자 부분집합 게시로 수치 저작을 지원한다. 기존 ChargeDefinition.Resolve·ChargeExecution.TryRelease·ChargedMeleeExecution은 그대로 사용하며 해제 clamp/보간·현재 시계·자원 전 대상 재검사·실패 회복/claim/콜백 순서를 바꾸지 않는다. 진행 중 차지/본체/매체는 기존 시작 버전을 유지한다. 전체 트랙/전용 아트는 별도다.

`ChargedMeleeExecution`은 이 두 차지의 활성화 여부·실패 후 0.10초 종료시각·마지막 타격창 관측·대상별 claim·완료/취소 원본을 소유한다. 기존 `ChargeExecution`은 해제·shift·피해의 유일한 원본으로 같은 인스턴스를 주입한다. Native host는 현재 시각·nearest 선택→자원 소비→발동 기록, 이동 뒤 스윕·Receive·완료 통지/정리를 연결한다. 콜백 뒤 시각을 다시 읽으며 완료 사실은 통지 전에 반영하고, 이미 진입한 스윕의 취소나 Receive 거부/예외가 claim을 되돌리지 않는다. 리셋/효과 제거의 ReleaseResources는 기존 자원/차지만 닫고 없던 취소/완료 사실을 만들지 않는다. 일반 기본/직접 특수의 실행기와 차지 정책을 중복 실행하지 않으며 Native 판정/물리와 범용 전체 트랙·저작은 후속이다.

`CastLifecycleExecution`은 근접/차지 전용 실행기 이외의 일반 기술 활성화·취소·모션 완료와 활성화/종료 경과시각의 private 값 원본이다. 기존 ChargeExecution·자원·같은 시계와 정책을 참조하며 Native 지상/대상/설치 자격→자원 소비→활성화 claim→효과 순서를 유지한다. 실패/취소의 자원 반환 후 상태 변경, 타격 뒤 현재 시각의 완료 claim→플레이어 통지→독립 잔존 재조회 순서와 콜백 재진입/부분 예외를 보존한다. 미사일/접근/벽/보스 종료도 같은 원본을 명시적으로 갱신하며 옛 Cast bool/시각은 남기지 않는다.

`CastTickExecution`은 상태 없는 순수 흐름으로 CombatCast.Step의 dt 기록→소유자 생존 검사/정리→디버그 초기화→차지/근접 전용 실행 위임→활성 일반 기술의 검기/타격/연막→돌진 주 대상 종료 추가타→현재 시각의 타격 평가 commit→기존 수명 후처리를 조율한다. 효과 참조는 기존 활성 구간 진입 지점에서 불변 시작 정의로 고정하고 타격 콜백 뒤 연막 경과시각·저장 위치·현재 소유자 기하/시각을 읽는다. Native ref 호스트는 실제 Bounds/Vector2 연산·디버그 박스와 기존 실행기를 연결하며 연막 은신 마감만 효과 종료까지 clamp한다. 별도 시계·수명/타격/연막 상태 복사나 콜백 뒤 새 생존/취소·재진입 차단 규칙을 만들지 않는다.

`CastMotionExecution`은 기존 행동의 수평 이동·이동 중 판정·수직 고정·무적 우선순위를 순수 계층에서 조율한다. 근접·차지·미사일·접근·반동·강제 대시는 기존 실행기를 그대로 사용하고 Native ref 호스트가 필요한 지점마다 현재 사실을 읽는다. 중첩 Moving/Recoiling 조회도 재평가하며 별도 시계·수명 상태를 만들지 않는다. fallback 기본 이동의 마지막 소비시각만 private 값으로 소유하여 첫 부분 구간과 공중·중립 입력의 시간 소비를 보존한다. 취소/완료 뒤 기존 수평속도 5도 유지한다.

`CastActivationExecution`은 일반 기술의 지상/대상/설치 자격→발동 자원→기존 lifecycle 활성화 claim→미사일/폭탄/원거리 생성·반동→효과·기록 순서를 상태 없는 순수 실행으로 조율한다. per-call ref Native 호스트는 실제 대상 참조·저장 위치·현재 바닥/겹침/차폐·생성·시각/상태 적용을 소유한다. 순간이동 대상 뒤 첫 nearest 타격의 덮어쓰기, 설치 기하와 단락 평가, 콜백 뒤 현재 사실 조회 및 재진입/예외의 부분 결과를 유지하며 새 활성화 bool·시계·자원/정의 복사본을 만들지 않는다.

`SkillHitTrackExecution`은 일반 기술의 마지막 완전 평가시각과 authored hit 순서/시간창·유한 앵커 분기를 소유한다. 매 타격은 현재 취소/완료·차지 shift·대상/순간이동 사실을 다시 읽고 실제 Native 몸/저장 위치/대상 기하·Receive와 기존 dedup claim을 연결한다. wave→모든 직접/독립 타격→연막→주 대상 종료 타격 뒤에만 평가시각을 commit하고 lifecycle 완료로 넘어간다. 예외/재진입이 이미 수행한 claim이나 부분 결과를 되돌리지 않으며 독립 앵커의 취소 후 유지도 보존한다. 전체 트랙 저작/콘텐츠 검증과 물리 후보 처리는 별도다.

ActionTrackDefinition은 기존 base_sword_movement/base_sword_follow_up/base_sword_range/shield_range 네 행동의 action-local FrameTimeline·마커·본체/타격/돌진·효과/방어 구간을 컴파일한다. ProjectileWave는 기존 거리/속도/고체 접촉으로 이동·피해·종료하며 authored 끝은 독립 cast 최소 보존이지 새 강제 만료 타이머가 아니다. OwnerGuard 지속시간은 실제 활성화 콜백 Now에 더하고 기존 보호막 교체·최대 지속 갱신·감소/보호막/HP 순서·bypass를 보존한다. 실행은 불변 시작 버전을 고정한다. 기존 돌진/잔상 해시는 새 필드가 없으면 그대로다. 본체는 attack1 대체 매체를 유지하며 차지/재입력/미사일·없는 Cue/무적은 새 트랙으로 바꾸지 않는다.

네 유한 행동의 ActionTrackNumericDocument/Recompile은 실제 피해·돌진/검기/방어 수치와 그래프를 한 번에 컴파일한다. SourceVersion은 고정 정책/자원만 식별하고 ContentHash는 전체 수치/트랙을 식별한다. 기존 기본값의 complete-content 인코딩은 보존하고 정책 버전만 명시적으로 바뀐다. 기존 Native 모터/파형/방어·자원·콜백 순서를 유지하며 새 runner/시계/정책 언어를 만들지 않는다.

나머지 일반 근접 특수 열아홉은 AuthoredSkillDefinition의 닫힌 ID/정책 descriptor로 실제 수치 그래프를 검증하고 같은 기존 activation/lifecycle/hit/sweep/teleport/approach/forced-move/smoke 원본을 실행한다. 시작은 불변 Execution/버전을 고정하고 부분 콜백/예외 순서를 보존한다. permissive 기술 fixture의 기존 SkillExecution 복사 생성자를 전역 strict 언어로 바꾸지 않는다. Spawn된 분신은 시작 효과의 몸/도발 범위/버전을 보관한다. 읽기 전용 공간 표본은 실제 행동/자원/피해·지형 성공을 실행하는 가짜 preview가 아니다.

`CastSweepExecution`은 기존 근접/검기 박스 스윕의 표본 수/좌표·제외/중복·노출 수집·정렬 뒤 nearest 적용 순서를 순수 계층에서 조율한다. Native per-call 호스트는 현재 TargetsInBox와 CombatActor 후보 목록·debug Bounds·실제 ApplyTarget을 보관한다. List.Sort 알고리즘과 비교 때마다 현재 owner 중심을 읽는 거리/ActorId 순서를 유지하며 수집 후 새 생존/취소 자격 검사를 추가하지 않는다. 새 버퍼/정렬 정책·전투 시계나 Collider 참조의 순수 유출 없이 같은 콜백/재진입/부분 예외 결과를 유지한다.

`LaserHitExecution`은 상태 없는 순수 흐름으로 Native 총구 간격/거리→기존 BoxCast 순서의 고체 길이 제한→현재 대상 자격/claim/차폐/기본 피해→총구 뒤 표시/기록을 조율한다. per-call ref 호스트는 실제 Physics/Vector2·대상 목록 열거/액터 참조·시작 정의·표시와 같은 arena.Now를 연결한다. 일방향 통과와 차폐 전 claim, 콜백 뒤 후보의 현재 생존/기하, 이미 진입한 실행의 소유자 사망/재진입/예외 부분 결과를 유지하며 새 빔 시계·대상 사실 스냅샷·정렬 규칙을 만들지 않는다.

검 1타 모션의 편집 원본은 data/combat/presentation/base_sword_attack1.json이다. FixedAttackMotion이 고정 모드의 안정 스텝·프레임/초·접촉/Cue를 검증한다. 기존 첫검 모션 창과 전체 가족 창은 같은 위임 원본을 사용한다. 알려진 프레임 편집·FrameTime 전환·명시 고정 복귀는 해당 원본에 저장하고 다른 가족 파일에 시간을 복제하지 않는다. 모션 영상 캡처와 제품 아트 제작은 별도 범위다.

FrameTimeline/FrameTimingProjection은 안정 스텝/마커를 해석하고 MeleeFrameCompiler/MeleeFrameDefinition이 전체 20개 가족의 행동/본체/Cue 후보를 컴파일한다. Frame Timing Preview는 첫검 위임 원본의 제한된 뷰이며 임시 적용·리셋·고정 복귀·프리셋과 영구 저장/생성을 구분한다. 활성 FrameTime 시간은 로컬 프레임/마커만 소유하고 수치 원본의 시간은 비활성 기준값이다. 저장은 해시 충돌/원자 교체/생성 실패 보존을 사용하며 시작/빌드는 같은 C# 완전 가족 검증을 수행한다. 진행 실행은 시작 정의/공속/매체를 유지한다.

`FramePresetSource`는 원본 수치/모션 ID·내용 해시와 프레임/마커 편집값·후보 해시만 버전 명시 실험 JSON으로 저장/재로드한다. `Frame Timing Preview`의 저장/불러오기는 편집 Draft와 파일 연결만 바꾸고 적용/리셋은 기존 명시 동작으로 유지한다. 원본이 다른 파일은 재기준화하지 않고 거부하며 잘못된 파일은 기존 편집/실행을 유지한다. 바이트 해시 충돌·1MiB 한도·원자적 파일 생성/교체와 저장 대비 미저장 표시를 제공한다. 파일은 같은 원본 버전의 편집 프리셋이며 피해/형상/아트·월드/장비/입력을 복제하거나 제품 빌드에서 자동 로드하지 않는다.

ActionGeometryPreview/ActionGeometryPreviewModel은 수치/모션/프레임 창이 공유하는 읽기 전용 장면이다. 실제 Sprite.rect/pivot/PPU·몸/피격·선택 타격·기존 전진 경로와 같은 후보의 유효 시간을 표시한다. FrameTime은 MeleeFrameDefinition의 Action/CompileMotion을 읽고 기본 전진은 같은 MeleeActionDefinition을 사용한다. 스크럽은 행동/tick/피해/자원/Cue/저장/적용을 실행하지 않는다. 무효 후보는 형상을 숨기고 대체/누락 매체를 명시하며 지형/중력/충돌 성공이나 제품 아트 완성을 주장하지 않는다.

`CombatInputIngress`는 액터별 입력 세션·세대/epoch·단조 순번·개별 표본과 bit-union 중재 투영의 원본이다. 키보드·패드는 `InputActionSource`의 native 액션 이벤트를 `LocalInput`이 수집하고, 전투 어댑터가 캡처 시 actor/권한과 현재 세대를 검사한 뒤 기존 Update 경계에서 접수한다. 원래 realtime 시각·장치 ID·서로 다른 이벤트의 순서를 보존하며 한 이벤트의 동시 액션은 이동→무기 1/2 기본→무기 1/2 스킬→대시→점프 순서를 사용한다. 물리 기본값은 `PlayerControls.inputactions`, 레거시 도구가 바꾼 키만 `CombatKeyboardBindings`의 세션 override로 반영한다.

패드도 기존 targeted attacks·slot holds·정수 `Axis`/`ClimbAxis`로 변환한다. 벽 오르기/차기는 `ClimbAxis > 0`, 아래+점프의 해제는 최초 `Drop`에 대응한다. 소유권·차단 변경은 Gameplay 입력을 버리면서 UI hold/edge를 보존하고, actor reset·포커스/장치 상실은 전체를 무효화한다. UI를 열고 닫은 프레임의 전투·월드 입력은 소비/토큰 검사로 차단한다. 이벤트 시각은 명시 `Realtime`이며 전투 시계를 대체하지 않는다. 수동/재생은 같은 ingress의 `Simulation`/Now 경로를 사용하고 동일 tick의 bit union·중재 우선순위를 유지한다. 모든 reset은 ingress 세대를 바꾸며 KeepTime의 기존 연속값/시계 정책과 NewEpoch의 초기화 정책을 유지한다.

`PlayerActionArbiter`가 플레이어의 유효 대시/점프 우선순위, 기본/특수/교체 예약 하나, 접근 재입력, 미사일 비행 입력 폐기와 이동 종료 후 교체를 결정한다. `IPlayerActionHost`의 Unity 어댑터가 실제 상태·자원·지형·대상을 조회하고 기존 행동을 시작한다. 특수 예약은 지상 전용 조건과 첫 타격의 `nearest` 대상 조건을 검사하며, 실제 시작에도 같은 조건을 다시 검사한다. 실패한 예약은 나중에 조건이 충족돼도 자동 실행하지 않는다. 즉시 시작의 기존 방향 전환/검사 순서는 유지한다. `CancelAction`·사망·리셋·성공한 자동 귀환은 같은 중재기의 예약을 정리하고, 완료 콜백은 현재 `CombatCast`와 같은 객체인 경우에만 전달한다.

`PlayerTickExecution`은 입력 차단 정리→고정 두 무기 쿨타임→사망 단락→수리검 복귀→현재 입력 중재/눌림 정리→행동/대시 이동 준비→Native 물리→벽/대시 종료 수직 정리→현재 행동 후처리→새 착지→안전 바닥 기록→첫 낙사→이동 후 중재 순서를 순수 계층에서 조율한다. 이전 접지 bool 하나만 플레이어에 내장된 값 실행기가 소유하며 점프·낙사·리셋·수리검 위치 적용은 같은 원본을 갱신한다. per-call ref Native 호스트는 실제 참조/Vector2·몸/지형 질의·안전 기록·pit 열거를 유지하고 각 콜백 뒤 현재 행동/접지 사실을 다시 읽는다. 고정 무기 쌍은 인덱스로 순회하되 각 무기의 현재 시각 조회를 유지한다. 기존 대시/점프/행동 중재기/모터를 재사용하며 별도 시계·종료/재진입 정책·낙사 규칙을 만들지 않는다.

`PlayerComboExecution`은 플레이어별 기본 공격 타수와 종료시각+0.30초 마감만 private 값으로 소유한다. 새 입력의 포함 경계와 다음 타수 순환은 현재 시각/현재 형태 타격 수를 필요한 때 읽고 예약·시작 자격·자원은 기존 중재기에 남긴다. BeginBasic은 Create 전에 타수를 확정하며 취소/교체/성공한 정비는 기존 콜백 순서 뒤 기록을 비운다. 실패한 특수 시작/정비는 연계 기록을 바꾸지 않는다. 별도 시계·후속 예약이나 장비 복사본을 만들지 않는다.

`PlayerDashExecution`은 기본 대시의 시작/종료·회복 시각·방향과 점프 취소 이월의 종료/방향 한 원본을 소유한다. `Dash`는 불변 `PlayerDashSnapshot`이며 같은 `arena.Now`와 현재 `Movement`를 읽고 별도 타이머/대시 충전 복사본을 만들지 않는다. 시작 자격 검사→기존 행동 취소→Native 방향 보정→시작, 유효 점프만의 취소/이월, 모터 이동 뒤 벽 종료/수직 0과 기존 마지막 tick 처리는 그대로다. 일반 취소/사망/낙사는 회복 시각과 기존 이월을 보존하고 테스트 리셋은 자원을 초기화한다. 성공한 수리검 자동 복귀는 `ClearCarry`로 이월만 종료하며 회복/방향을 초기화하지 않는다. 점프 사용량·실제 몸/속도·충돌·낙사/행동 중재는 기존 소유자에 남는다.

PlayerJumpExecution은 두 점프 사용량, 프로필의 착지 예약, 누름·뗌 순서로 복원한 점프 키 유지와 1회 해제 관측 상태를 소유하고 Press/Land에서 같은 불변 LocomotionProfile의 첫/둘째 속도를 받는다. 현재 해제 배율은 1이므로 키를 놓아도 상승속도를 줄이지 않는다. 생성자에 수치 복사본을 보관하지 않는다. 실제 접지/속도 적용·행동 취소는 Native 호스트가 담당한다. 착지는 사용량을 비우고 포함 경계 예약만 첫 점프로 한 번 소비하며 ClearBuffer는 사용량을 환불하지 않는다. Actor 모터는 같은 profile 중력, 원거리 반동/미사일은 시작 때 캡처한 같은 중력 쌍을 사용한다. 네 기존 CombatRules 상수는 모든 호출부 전환 후 제거한다. 전체 이동 적용은 검증 뒤 리셋만 지원하며 새 clock이나 공중 라이브 적용 정책을 추가하지 않는다. 일반 수평 가감속은 `HorizontalRunControl`이 이전 확정 수평속도에서 계산하고, 벽 코요테는 Unity 벽 이동 어댑터가 소유한다.

`RangedRecoilExecution`은 공중 원거리 발동 반동의 시작·실효 지속시간·방향·벽/착지 종료를 Cast 내부에 값으로 보관한다. mutable 실행 상태를 복사/boxing하지 않고 불변 `RangedRecoilSnapshot`만 조회한다. 높이 지정 시 기존 상승/하강 중력의 전체 포물선 시간에 후방 거리를 분배하며, Cast는 시작 정의·발동 자원·취소·행동 종료와 실제 수직속도/지형 적용을 계속 소유한다. 동일한 시뮬레이션 시각과 마지막 부분 tick 비율을 읽고 기본 공속을 탄도/반동에 전파하지 않는다. 미사일 백점프나 기본 대시의 정책을 합치지 않는다.

`ForcedDashExecution`은 기존 검·단검·망치·방패·대검 특수 돌진의 이동 완료와 벽 직접 타격 cutoff를 소유한다. Cast는 불변 이동 정의를 private 값 실행기에 주입하고 활성화/취소·행동 완료·발동 자원은 계속 직접 소유한다. 이동창 교차의 부분 속도와 벽/자연 종료를 같은 순서로 해석한다. 일반 벽 종료는 회복 종료시각을 제안하지만 회전 지속 정책은 남은 타격을 유지한다. 주 대상 보스 접촉은 이동/회복만 끝내고 벽 cutoff를 새로 만들지 않는다. Native는 실제 모터 접촉 뒤 결과·수직 0을 적용하고 마지막 스윕 뒤 주 대상 종료 타격을 처리한다. 표식 재입력/순간이동과 미사일은 이 원본을 겸하지 않는다.

`MissileExecution`은 기존 폭탄 미사일의 발진시각·실제 비행거리·이번 이동/접촉 예약·종료와 백점프 시각/지속/접촉 종료를 Cast 내부의 private 값 원본으로 보관한다. ref-return 접근과 constrained ref 호스트로 복사/boxing 없이 변이하고 활성화/취소·행동 완료·생존/시각은 기존 Cast의 현재 사실을 읽는다. 수동 D는 새 호출 때 실제 발진 지연을 검사하며 이른 요청을 예약하지 않는다. Native 후보 기하/최근접을 먼저 읽고 실제 이동 뒤 거리/자동 종료를 반영하며, 종료 사실을 폭발 콜백 전에 확정한다. Native 피해/표현 뒤 현재 시각으로 백점프를 시작하고 자동 종료만 현재 tick의 dt를 더한다. 수직속도·기하/차폐·시각 뷰 복귀는 Unity에 남고 새 시계·자원/일반 취소 상태나 제품 수치를 만들지 않는다.

`ApproachRecastExecution`은 채찍 첫 유효 적중의 재입력 마감·일회 소비·접근 모드/남은 거리/실효 길이를 private 값 원본으로 소유한다. 현재 시각·취소/모션 완료·같은 무기/생존 표적은 ref Native 호스트에서 읽으며 새 시계나 Cast 수명 원본을 만들지 않는다. 목적지 계산→소비 claim→Player.StartApproach→Cast 재개 순서와 콜백 뒤 현재 위치/시각 재조회, 실제 이동량의 거리 차감·차폐/도착/예산 종료를 유지한다. 대상·무기 참조, MoveTowards/몸 스윕과 실제 위치/수직속도 적용은 Unity에 남고, 이미 유효한 표식은 원래 마감까지 주 행동 종료 후에도 유지한다.

`CastTeleportExecution`은 대시/채찍 재입력을 제외한 기존 이동 기술의 시작 경계와 일회 claim/분기만 private 값 원본으로 소유한다. 대상 유효성 조회·방향/목적지·실제 몸 스윕과 현재 도착 위치는 Native ref 호스트가 원래 순서로 처리한다. claim을 콜백/대상 조건 전에 확정하므로 대상 유실의 no-op·예외·재진입도 이동을 재시도하지 않는다. 레이피어는 대상 시선 반전→목적지→몸 이동→previousOwner 캡처→로그를 유지하고 실제 도착 기준 타격은 같은 claimed 사실을 읽는다. 일반 Cast의 활성화/취소/자원/모션 완료나 별도 시계를 복제하지 않는다.

`BombReleaseExecution`은 폭탄 행동의 이미 claim한 방출 커서만 Cast 내부 값으로 소유한다. 취소/소유자 생존은 방출 호출 입구에서 한 번 검사하고 각 while 조건은 현재 경과시각·기존 차지 shift를 다시 읽는다. 커서를 Native 생성 전에 증가시키므로 재진입/예외가 이미 시도한 발사를 중복 생성하지 않는다. 방향·발동 피해 배율과 생성 탄도/독립 수명·자원은 기존 호스트가 소유하며 새 예약 큐/시계를 만들지 않는다.

`SmokeEffectExecution`은 시작 효과 정의의 종료 경과시각과 공격 후 재은신 차단 사실만 private 값 원본으로 소유한다. Cast는 활성화/취소·저장 위치·주 행동 완료/자원을, ActorStatus는 현재 은신 마감을 계속 소유한다. Native Bounds 겹침으로 영역 내 시각+step×1.5 갱신을 수행하되 효과 종료시각을 넘기지 않는다. 영역 이탈·효과 만료·공격 소비 때 연막 은신 마감만 0으로 지우며 별도 기본 은신은 유지한다. 공격 시작의 `MarkSmokeSpent`는 해당 연막의 재진입 은신을 차단하고 새 연막은 독립 자격을 갖는다. 취소/교체 뒤 독립 수명과 같은 시전자 새 연막 교체는 기존 경로를 유지한다. 이탈은 현재 전투 tick의 Bounds 평가에서 확정하며 렌더 프레임 중 Transform 이동만으로 전투 상태를 갱신하지 않는다.

`SkillWaveExecution`은 일반 검기의 현재/이전 좌표·실제 누적 거리·종료 사실을 private 내장 값으로 소유한다. Native 활성화 겹침/차폐·몸 스윕·대상/피해·Renderer는 ref 호스트에 남는다. 현재 Cast 방향으로 이동→누적→타격→종료→표현 순서를 유지하며 타격 콜백의 취소/제거/예외가 이미 진행한 이동을 되돌리거나 새 취소 규칙을 만들지 않는다. 독립 효과는 기존 행동 취소 뒤에도 자기 사거리까지 유지하고 Cast는 같은 효과 상태로 잔존 수명을 계산한다. 새 투사체 데이터/시계나 전역 후보 정렬을 만들지 않는다.

`CastHitResponseExecution`은 기존 타격 claim→현재 피해/Receive→유효 적중의 표식/접근→주 대상/보스 종료→밀기 등록 순서를 순수 계층에서 조율한다. 종료 추가타의 일회 claim과 밀기 대상 ID만 같은 원본으로 보관하며 실제 표적 참조·기존 ledger/전용 타격 claim·Receive/목록/현재 시각은 ref Native 호스트에 남는다. Receive 콜백 뒤 현재 분류/표식/이동을 재조회하고 보스 종료 추가타는 마지막 밀기 등록 전에 처리한다. Push는 기존 목록 순서/재진입 오류와 현재 힘/시각을 유지하고 새 타격·취소·정렬 규칙을 만들지 않는다.

`CastParryExecution`은 기존 방패의 활성화/취소·효과 종류·[0,0.30초)·전방 자격과 근접 반사→기록 순서를 순수 해석한다. ref Native 호스트는 현재 경과시각·소스/몸 중심/방향·발동 배율과 실제 Receive/로그를 제공한다. 소스 Receive가 거부돼도 들어온 타격 차단은 성공이며 진입한 콜백 뒤 자격을 재검사하지 않는다. 투사체는 여기서 반사 피해를 추가하지 않고 기존 적 탄 소유자에서 한 번 반사한다. 새 ledger/자원/모션 완료 게이트나 Definitions 참조를 Contracts에 추가하지 않는다.

`IActionResources`/`ActionResourceExecution`은 공격 실행·원본 무기 ID와 기본/특수 분류로 발동 결과 한 개를 보관한다. `WeaponActionResourceSource`는 기존 `WeaponState` 호환 조회를 통해 Equipment의 발동 피해 배율을 받고 호환 `ChargePool`을 통해 충전을 소비한다. `Equipment/Contracts/IWeaponCharges`의 읽기 전용 `ChargeState`와 명령은 `WeaponResources.CreateCharges`가 만든 가변 구현을 감춘다. 기존 `ChargePool`은 이 인터페이스만 보관하고 별도 숫자/타이머를 소유하지 않는다. 순차 회복·정비 부담·기존 float 순서와 비활성 무기 회복을 유지한다. 기존 캐스트와 근접 기본 17타의 호스트가 같은 발동 경계를 사용하며 중복/옛 요청은 재소비하지 않는다. 반환 뒤에도 성공 스냅샷을 유지하고 리셋/효과 제거는 Unity 지연 파괴 전에 이전 실행권을 닫는다. `IWeaponInstance`가 프로필 무기의 실행 ID·형태/특수 선택 쌍과 충전/스탯 소유자를 묶고 불변 `WeaponSnapshot`을 제공한다. 기존 `WeaponState`는 인스턴스/자식 뷰만 보관하며 별도 ID/인덱스 원본이 없다. 플레이어 정비는 `IEquipmentCommands`가 같은 무기의 선택/충전 설정을 함께 반영하고 A/B 설정/복원은 저수준 쌍 선택 명령을 사용한다. 기존 조건/충전 부담은 유지하며 `TryReconfigure`는 카탈로그/수치를 먼저 검증하고 성공 뒤에만 접근 표식/연계를 정리한다. ID/인덱스는 현재 프로필 실행/카탈로그 호환 값이며 영구 아이템/콘텐츠 저장 키가 아니다. 안정 콘텐츠 정의·실물 재고/안전 정비/Run 조립은 후속이다. `IWeaponLoadout`이 프로필의 고정된 장착 무기 두 개와 활성 슬롯을 소유하고 불변 `WeaponLoadoutSnapshot`을 제공한다. 기존 `weapons`는 같은 원본을 보는 안정적인 `WeaponState` 뷰 목록이며 `selected`는 Loadout 명령/조회에 전달한다. 일반 교체 자격/예약·우선순위/취소와 비활성 무기 회복·테스트 리셋의 활성 슬롯 보존은 기존 중재기/호스트 경로를 유지한다. 실제 획득·바닥 무기 교환/보관·Run 시작 장비 정책은 추가하지 않는다. 현재 계약과 검사 범위는 [행동 발동 자원](../gameplay/combat/action-resources.md)을 따른다.

현재 플레이어 입력/예약·기본 연계·행동 시작/수명/개별 정책·투사체/독립 효과·운동/체력/상태·피해와 공간 후보 조율은 이 문서의 순수 소유자와 실행 경계로 분리한다. Unity 어댑터는 실제 참조·Physics 질의/Vector2 연산·Collider/Transform/Renderer 출력과 씬 수명을 연결한다. 이 분리를 제품 재고/획득·Run 정책·정식 적 콘텐츠나 모든 행동의 프레임 저작 완료로 확대하지 않는다. 현재 FrameTime 지원 가족과 남은 차지/원거리 정책은 위 저작 범위에서 구분한다.

시한폭탄의 경과시간/착지시각·종료 사실은 `TimedBombExecution`의 private 내장 상태 한 곳에 둔다. mutable struct는 복사/boxing하지 않고 `TimedBombSnapshot`만 값으로 조회한다. 기존 dt의 최대 0.02초 부분 스텝·미세 나머지/종료 여유와 착지 접촉 비율 계산 순서를 유지하며 착지는 비행 제한보다 우선해 전체 신관을 부여한다. `ITimedBombHost`는 현재 lifetime/fuse와 Native 비행/폭발을 연결하고 중력·속도 적분과 Vector2 연산은 기존 어댑터에 남는다. 종료를 먼저 claim한 뒤 같은 폭발 본문을 한 번 호출한다. 사망/리셋은 `BombProjectile.Release`로 소유자를 동기 종료한 뒤 지연 Destroy하므로 남은 참조의 같은 프레임 Step/SweepTo가 다시 이동/무장/폭발하지 않는다. 일반 접촉탄의 절대 만료 시각과 시한폭탄의 부분 스텝 경과시간을 서로 바꾸지 않는다.

`TimedBombSweepExecution`은 시한폭탄의 진입 stopped/착지 검사→delta 일회 캡처→X 벽 자격/외부 face clamp와 속도 정지→Y 천장/바닥/일방향 자격·접촉 선택→위치/속도 반영→기존 TimedBombExecution.Land→전체 속도 정지/기록 순서를 상태 없이 조율한다. per-call ref Native 호스트는 Physics CircleCast 배열 순서·현재 Collider/Bounds·Mathf.Abs와 Vector2 delta/compound assignment를 유지한다. 벽은 현재 반경/Y 겹침과 외부 면으로 contact를 줄인 뒤 0.001 여유를 적용하며 다음 비교도 이미 줄어든 allowed를 사용한다. Y는 X 응답 뒤 현재 위치에서 질의하고 동률의 마지막 접촉이 travel/contactDistance를 갱신한다. 일방향은 현재 bottom/top 0.03 여유의 하강만 허용하며 콜백 뒤 현재 사실·예외의 부분 쓰기를 되돌리거나 새 종료/착지/시계를 만들지 않는다. 착지 분수와 비행/신관은 기존 소유자 한 곳에 남는다.

`BombProjectile.Spawn`도 생성 뒤 소비할 반경/중력·폭발/비행/착지 신관·만료/파편 설정을 `BombProjectileDefinition`으로 한 번 고정한다. 각도 배열은 방어 복사하고 모든 파편은 한 불변 자식 정의를 재사용해 파편마다 DTO/정의를 다시 만들지 않는다. 원본 편집은 다음 생성에만 반영하며 기존 탄의 fuse on/off·지형/피해 기하/폭발/파편을 바꾸지 않는다. 발사 각도/속도/총구는 활성화 입력, 실제 velocity/damage/배율은 전달받은 원본으로 유지한다. Native 판정/float 순서와 기존 단일 `TimedBombExecution` 수명은 그대로이며 새 카탈로그/제품 잉크 정책을 추가하지 않는다.

`ContactBombExecution`은 일반 접촉 폭탄/파편의 생성 기준 절대 만료시각과 만료/마지막 부분 비행 스텝을 private 값으로 해석한다. Native ref 호스트가 현재 시각·불변 정의·위치/속도와 기존 SweepTo/폭발/제거를 제공하고 속도 반영 전후/스윕 뒤 현재 시각 조회 순서를 유지한다. 종료 사실은 접촉/시한 모두 기존 TimedBombExecution 한 원본만 사용하며 새 stopped/나이 시계를 만들지 않는다. Fuse 분기·Native CircleCast/중력 적용 결과·폭발/파편 정책은 기존 경로를 유지한다.

`ContactBombSweepExecution`은 접촉 폭탄/파편의 고체 우선·일방향 하강/상향 법선/현재 바닥 높이·적 자격과 시작 겹침→미세 이동 제외→기존 순서 cast의 strict-less 최근접/구간 clamp→위치 반영→폭발을 상태 없이 조율한다. per-call ref Native 호스트는 실제 Collider/Actor 참조·Physics 배열 순서·Vector2 delta/길이/정규화·현재 위치 compound assignment를 유지한다. 겹침은 이동 길이 검사보다 앞서며 동률은 먼저 나온 접촉을 유지하고 새 전역 정렬을 추가하지 않는다. 자격 때 현재 Position과 bounds를 읽고 최종 이동은 질의 뒤 현재 Position에 더하므로 예외의 부분 결과를 되돌리지 않는다. 양수 fuse는 기존 SweepTimed(destination,0)에 한 번 위임하고 stopped는 기존 TimedBombExecution 한 원본을 계속 사용한다.

`BombBlastExecution`은 이미 기존 종료를 claim한 폭탄의 후보 순서·자격/ActorId claim·반경+0.0001 경계/차폐 단락 평가·기본 Receive→폭발 표현→저작 각도 순서의 파편 생성/즉시 스윕→제거를 상태 없이 조율한다. Native per-call 호스트는 새 HashSet·Physics 겹침 결과와 HurtRegion/Actor/Collider 참조·ClosestPoint/거리·현재 위치/소유자/반경·삼각함수/생성을 보관한다. 후보 전체의 사실이나 새 stopped/시계를 캡처하지 않고 콜백 뒤 현재 사실과 기존 float 연산 순서를 유지한다. 파편 생성/스윕 예외는 이미 적용한 피해/폭발/파편만 남기며 finally 제거·재시도·병렬 생성을 하지 않는다. 접촉/시한 수명은 기존 소유자를 그대로 사용하고 플레이어 탄 폭발 정책과 공통화하지 않는다.

`RangedLaunchExecution`은 기존 FirePlayerRanged/SpawnBomb의 발사 순서만 상태 없이 조율한다. 원점/총구의 순차 현재 소유자 조회→레이저 일회 위임 또는 다섯 수리검의 몸→총구 고체 검사/최근접→막힌 폭발탄의 clamp 생성/즉시 폭발/차단 기록, 열린 총구의 생성→명시 원본 무기 또는 생성 뒤 현재 플레이어 무기 대입→즉시 스윕→기록을 유지한다. 다른 탄은 저작 각도 순서마다 원점 생성→같은 캡처 총구 스윕을 마친 뒤 다음 각도를 읽는다. 폭탄은 각도/원점 캡처→실제 생성→파편 배율 대입→생성 뒤 현재 소유자 폭으로 총구 스윕→실제 생성 개체 반환 순서다. ref Native 호스트는 Vector2/정규화/길이·Mathf 삼각함수/clamp·Physics 배열/Collider·Spawn/실제 탄 참조를 유지하며 순수 계층은 분기·고체 자격/최소 거리·scalar 총구 여유와 동기 순서만 담당한다. 막힌 비폭발탄을 가상 생성하거나 막힌 폭발탄에 원본 무기를 대입하지 않으며 콜백의 소유자 변경/재진입과 예외의 피해·claim·부분 생성 결과를 되돌리는 정리/재시도·새 시계/취소 정책을 추가하지 않는다.

`PlayerProjectile.Spawn`은 이후 실행이 읽는 종류·거리·충돌/폭발 반경·관통/경직·적별 반복 간격·복귀 지연/간격을 불변 `RangedProjectileDefinition` 한 개로 캡처한다. 원본 `RangedData`나 캐스트 DTO를 나중에 바꿔도 이미 생성한 탄은 재해석하지 않고 다음 생성만 새 값을 채택한다. 실제 속도는 전달받은 velocity, 피해/원본 무기는 기존 발동 스냅샷/참조를 유지하며 별도 speed 원본을 복사하지 않는다. Native 겹침/스윕·순서/차폐·반복 적중·폭발/복귀·생명은 기존 소비자를 사용한다. 복귀 API는 캡처한 지연/간격 scalar를 받아 mutable DTO를 되읽지 않는다. 새 카탈로그/시계/제품 원거리 정책이나 전체 투사체 실행 이행은 추가하지 않는다.

`PlayerProjectileExecution`은 이 불변 정의와 이동 거리·반복 경과시간·대상별 일회/다음 적중 시각·종료 사실의 단일 원본을 소유한다. Native host가 기존 실제 이동/스윕·회전/제거를 연결하고 일반 탄은 거리 한 번, 반복 탄은 원래 최대 0.02초와 각 적별 마감에 나눈다. 한 Step의 실제 speed는 처음에 고정하지만 접촉 나이는 Native 현재 속도로 계산하며 별도 시계를 누적하지 않는다. 스윕→거리/나이/나머지→회전→끝 겹침/만료 순서와 예외/재진입의 부분 결과를 유지한다. Release는 종료를 먼저 확정하고 사망/리셋의 실제 Arena 호출도 이를 사용하므로 지연 Destroy 전 옛 참조가 피해/복귀/폭발을 재개하지 않는다. 이미 진입한 스윕은 별도 취소 규칙으로 중단하지 않으며 종료 이후 새 Step/Sweep/Detonate만 차단한다.

`PlayerProjectileHitExecution`은 접촉 자격→폭발 또는 기존 대상 claim→Receive→유효 복귀 방문→관통/제거와 폭발의 종료 우선 확정→새 대상 dedup→후보 자격/최근접점 거리·차폐→Receive→표현/기록 순서를 상태 없는 순수 실행으로 조율한다. per-call ref Native 호스트는 Collider/Actor 참조·현재 owner/velocity/정의·기존 PlayerProjectileExecution claim/종료·Physics/HashSet/ClosestPoint를 연결한다. Receive 뒤 현재 region.actor·owner·원본 무기를 다시 읽고 이미 진입한 응답의 제거/재진입/예외 부분 결과를 유지한다. 폭발은 기하로 거부되기 전에 ActorId를 claim하며 후보 위치를 미리 저장하거나 새 전역 정렬·수명/차폐 정책을 만들지 않는다. 일반 Resolve는 새 할당/boxing 없이 기존 호스트 관례를 사용하고 폭탄 폭발 정책과 공통화하지 않는다.

`PlayerProjectileSweepExecution`은 플레이어 탄의 현재 접촉 자격/우선순위와 기존 다섯 수리검 종류의 겹침 순위·cast 거리/우선순위 비교, 시작 겹침→첫 terminal 응답→콜백 뒤 delta→미세 이동 제외→정렬된 cast의 현재 자격/접촉 위치/원래 거리 나이→최종 목적지를 상태 없이 조율한다. per-call ref Native 호스트는 새 겹침/cast 목록·실제 Collider/HurtRegion/Actor 참조·List.Sort 순서·Vector2 길이/정규화/sqrMagnitude를 유지하며 비교마다 두 현재 Position을 원래 순서로 읽는다. 고체 자격은 actor보다 우선하고 일방향 표면은 actor로 재해석하지 않는다. clamp된 위치와 달리 접촉 나이는 원래 Native 거리·현재 속도·기존 실행 나이를 사용한다. 종료 확인은 진입 때 한 번이며 동기 콜백이 제거해도 이미 진입한 비terminal 스윕은 계속 진행한다. 재진입 목록/기하를 공유하거나 새 수명/시계를 추가하지 않고 예외의 위치·피해·claim 부분 결과를 유지한다.

`EnemyProjectileExecution`은 적 탄의 절대 만료·캡처 피해·반사 여부·ActorId별 접촉 claim·종료 사실을 소유한다. `ArenaProjectile`은 Native 현재 소스/속도·스윕 후보/거리 정렬·차폐·패링/피해와 실제 제거를 연결한다. 최초 플레이어 패링 뒤 소스/속도를 먼저 바꾸고 현재 특수 배율을 곱하며 ledger를 비운 뒤 반사 대상만 다시 등록한다. 같은 거리 후보의 새 전역 동률 규칙을 만들지 않는다. `Release`는 제거 전 종료를 확정하고 Arena의 사망/리셋도 이를 사용하므로 지연 Destroy 전 남은 참조의 새 Step/접촉은 이동·패링·피해를 재개하지 않는다. 현재 진입한 Native 호출의 오류/부분 결과는 유지하며 일반 장치/풀링/정식 적 콘텐츠는 이행 범위가 아니다.

`EnemyProjectileSweepExecution`은 적 탄의 이전 위치→현재 속력×dt→고체 이동 제한→현재 정규화 속도로 계산한 다음 위치→0.1 간격 이하의 양 끝 포함 표본→후보 중복 제거/정렬→현재 접촉 자격/차폐→기존 접촉 실행→이동/벽 종료 순서를 상태 없이 조율한다. per-call ref Native 호스트는 현재 Vector2 속력/정규화·TargetsInBox와 새 CombatActor 후보 목록·현재 중심 거리의 List.Sort·Collider 최근접점/차폐와 기존 EnemyProjectileExecution을 연결한다. 같은 거리의 새 ActorId 동률 규칙이나 후보 사실의 선캡처·재사용 목록·종료/시계를 추가하지 않고, 진입한 콜백 뒤 기존 부분 결과와 계속 진행을 유지한다. 적 탄과 폭탄의 스윕 정책을 공통화하지 않는다.

`WorldHitQueryRules`는 기존 Box/Circle 최근접 후보의 자격·거리/근사 동률 ActorId 선택과 노출 교집합/다섯 순서점의 기하를 순수 계층에서 해석한다. Native ref 호스트는 현재 중심/목록·Collider bounds/ClosestPoint·Linecast를 연결하고 public Arena API와 후보 순서를 유지한다. strict-less 거리 우선과 기존 Mathf epsilon 기반 근사 비교를 보존하며 노출 모서리의 0.001 inset을 임의로 자르지 않는다. 중간점 배열 할당 없이 같은 순서로 검사하고 새로운 전역 정렬/가시성 규칙을 추가하지 않는다.

`ActorMotionResolver`가 접지 질의 뒤의 속도/제어 상태를 불변 ActorMotionInput으로 받아 충돌 전 ActorMotionProposal을 만든다. 일반 이동보다 밀림을 먼저 적용하고 입력 잠금·기절·속박·경직을 검사하며 실제 강제 이동은 이 제한을 우회한다. 사망/분신의 수평 정지·수직 고정과 상승/하강 중력·tick 내부 정점 분할도 같은 순수 해석을 사용한다. 중력은 기존 CombatRules 값을 전달하고 새 편집 원본이나 시계를 만들지 않는다. 결과는 이동 제안이며 실제 접촉 해석과 확정된 운동 원본 갱신은 아래 모터/포즈 경계를 사용한다.

`ActorKinematicsState`는 액터의 현재 X/Y·수평/수직 속도·방향·접지·벽 접촉 한 원본을 순수 계층에서 소유한다. ActorKinematicsSnapshot은 불변 값이며 Unity Vector2 프로퍼티는 값 변환만 수행하고 성분 변경은 scalar 명령으로 전달한다. Position 명령은 원본을 먼저 갱신한 뒤 Transform에 동기 출력하므로 X 이동 뒤 Y 질의와 부분 오류는 이미 적용한 현재 좌표를 본다. Transform의 외부 편집은 전투 상태를 덮어쓰지 않으며 모터 완료는 두 축을 건너뛴 경우에도 원본 X/Y를 출력한 뒤 Physics를 동기화한다. Native Z는 뷰 전용이고 명시적 SetPosition(Vector3)로만 함께 설정한다. 루트 Renderer 별칭의 localPosition 변경도 같은 원본의 월드 X/Y로 변환한 뒤 출력하며 자식 그림은 포즈를 소유하지 않는다. 점프·대시·상태·플랫폼 원본을 중복 생성하거나 리셋/이동 정책을 변경하지 않는다.

`ActorMotorExecution`은 보호/발판 만료→접지→기존 모션 해석→X축→Y축→동기 출력/Physics 동기화의 실행 순서를 순수 계층에서 조율한다. 각 축은 현재 몸/질의를 다시 읽고 Native ref 호스트가 ActorKinematicsState에 확정한 위치/속도/접촉을 명령한다. X축 뒤 콜백 변경은 Y축에 반영되고 Y축 오류가 이미 적용한 X축을 되돌리지 않는다. 건너뛴 축은 접촉 사실을 덮어쓰지 않으며 낙하 접촉만 접지를 만든다. Collider/Transform/플랫폼 바인딩은 Unity에 남고 별도 운동 상태/시계나 물리 계산을 만들지 않는다.

`ActorFacingExecution`은 0 입력 no-op·부호 정규화·기존 몸/새 방향 막힘 질의→벽 중심 보정→방향→몸/피격 오프셋→그림 반전→비플레이어 위치 순서를 순수 계층에서 조율한다. 질의 뒤 같은 운동 원본의 현재 위치·방향과 실제 오프셋을 다시 읽고 예외/재진입의 부분 결과를 유지한다. 방향/위치는 ActorKinematicsState에 명령하고 Collider/Renderer는 Native 출력이며 직접 방향 대입 뒤 SyncFacing하는 귀환 경로도 유지한다.

`ActorVitalsState`가 액터 한 개의 최대/현재 HP·보호막·방어 종료/감소율·수신 횟수 원본을 소유한다. `IActorVitals`의 명시적 저수준 프로필 쓰기/피해 반영·보호막 만료/리셋과 불변 `ActorVitalsSnapshot`을 사용하며, 기존 `CombatActor` 이름의 프로퍼티는 같은 원본을 조회/변경한다. 방어 감소→보호막 흡수→0 하한 HP·수신 횟수 증가를 같은 float 순서로 수행한다. 최대 HP 변경이 현재 HP를 자동 조정하거나 HP를 최대값에 자르지는 않는다. 무적/패링 자격 검사는 기존 호출부에서 먼저 수행하며 수치 반영 뒤 피격 제어/취소·사망 콜백도 기존 순서를 유지한다. 리셋은 HP를 현재 최대값으로 복구하고 보호막/방어 종료/수신 횟수를 비우며 기존 감소율 값은 보존한다. 이는 런타임 수치 원본이며 World/저장에 두 번째 실시간 HP를 만들지 않는다. 실제 회복/저장 정책과 전체 물리/판정 이행은 후속 범위다.

`ActorStatusState`가 기절·속박·기본 경직·입력 잠금·밀림 종료/속도·은신/연막 종료시각 원본을 소유한다. `IActorStatuses`의 단계별 명령과 불변 `ActorStatusSnapshot`을 사용하고 기존 `CombatActor` 프로퍼티는 같은 원본을 조회/변경한다. 플레이어 피격과 기본 경직·속박/기절 최대값 갱신·밀림을 각각 반영해 사이의 가상 취소 콜백이 현재 상태를 관측/편집하는 순서를 유지한다. 모션/적 의도/대상 선택은 같은 절대 만료시각을 읽으며 조회가 시각을 누적하거나 만료값을 자동 초기화하지 않는다. 전체 리셋은 기존처럼 비활성 밀림 속도를 남기고 만료시각을 비운다. 낙사 복귀의 피격 이동 제한 해제·공격 시작의 은신 해제·캐스터 밀기의 종료/속도 쌍은 각각 명시적 명령이며 다른 상태를 지우지 않는다. 효과 수명·물리·피격/사망 콜백과 정식 상태 콘텐츠/저장/Run 조립은 후속이다.

`ActorDamageExecution`은 기존 피해 수신 자격→Vitals 반영→플레이어/적 제어→기록→사망 처리의 순서를 순수 계층에서 조율한다. 기존 Vitals/Statuses가 같은 상태를 계속 소유하고 Native ref 호스트는 현재 플레이어/보스/분신·강제 이동/패링·가상 취소/Die/색/로그를 연결한다. 가변 HitData는 취소 콜백 뒤 각 제어 단계에서 다시 읽으며 같은 불변 SkillHitDefinition 경로도 유지한다. 기록/사망 콜백이 HP/종류를 바꿔도 진입한 단계와 이후 현재 조회의 원래 의미를 보존한다. 두 번째 피해/HP 원본이나 추가 검증·예외 억제/자동 재시도는 만들지 않는다.

일반 적의 기본 피격 경직·밀림은 생성 카탈로그에서 검증한 불변 `EnemyHitReactionDefinition`을 사용한다. 경직은 기존 종료시각과 현재+0.30초의 최댓값이고 플레이어 기본 공격의 밀림은 0.25unit/0.10초이며 전용 밀기와 합산하지 않는다. 최종 물리 tick은 남은 밀림 시간만 적분한다. 원시/불변 제어 타격 모두 같은 피해 수락 경계를 지나며 `CombatHitOrigin`과 확정 타격을 보존한 뒤 공통 `OnAcceptedHit`에서 들쥐 등 실제 피격 반응을 연결한다.

일방향 발판 내려가기는 선택 당시 Native 접촉점 `hit.point.y`를 순수 무시 상태의 `PlaneY`로 고정한다. 최소 복구시각과 원래 접촉면 완전 이탈을 함께 확인하며 움직이는 콜라이더의 현재 bounds로 복구 기준을 바꾸지 않는다. Unity 바인딩은 핸들의 현재 생존 여부만 공급한다.

`ActorContactResolver`와 `IActorSurfaceQuery`는 공통 축 이동/접지 판정을 Unity 질의에서 분리한다. `ActorPlacementResolver`는 같은 Native 어댑터의 확장 `IActorPlacementQuery`로 안전 접지·막힘·텔레포트를 해석한다. `ActorBodyFrame`/`ActorBoxCast`·ray/overlap 요청과 불변 접촉/표면 bounds가 현재 기하·단방향/무시 사실만 전달하며 씬 객체/콜라이더 ID를 포트에 노출하지 않는다. `UnityActorSurfaceQuery`는 기존 Native BoxCast/Raycast/OverlapBox의 ArenaSurface 결과 순서를 유지한다. 축 이동의 skin/접촉 여유·단방향/법선/발 높이와 접지 속도 경계, 안전 접지의 세 ray/첫 유효 높이·지지/평탄도, 막힘의 발 높이/정확한 XYZ bounds 재검사, 텔레포트의 시작 겹침/단방향 제외·제한/끝 위치 거부를 보존한다. `CombatActor`는 같은 운동 원본을 통해 X→Y 위치·속도/접지를 확정하고 Unity 출력을 동기화한다. 발판 복구는 위의 캡처한 접촉면 규칙을 따르며 파괴 관측은 다음 상태 갱신에서 해제하고 비활성화를 파괴로 취급하지 않는다. 핸들은 콘텐츠/저장 ID가 아니며 조회가 만료를 갱신하지 않는다. 수리검의 도착/복귀 기하는 `ActorReturnPlacement`가 같은 현재 기하 질의로 원점 우선·주변 후보·대상 앞 대체를 해석한다. `ActorReturnSchedule`은 무기 인스턴스별 예약·원점/방향·원래 대상 ID·도착 기준 시각과 간격을 소유하고, 기대 예약 ID/무기/현재 시각을 확인해 실제 시도 전에 한 번 소비한다. Unity는 예약과 실제 대상 참조만 연결하며 리셋/사망이 양쪽을 비운다. 두 번째 상태·시계·제품 저장 규칙은 만들지 않는다.

지형의 기본 레이어 객체는 `ArenaSurface`가 깨어나거나 활성화될 때 `ArenaTerrain`(8)로 분류한다. 기존 사용자 지정 레이어는 유지하면서 기본 레이어를 제외한 기존 조회 마스크에 포함하고 `Ignore Raycast`는 기존처럼 제외한다. 이동·바닥·발판 내려가기와 공통 차폐/고체/바닥 조회는 이 마스크와 기존 trigger 설정을 사용한다. 레이어 충돌 행렬과 공격·피격 영역의 판정 규칙은 바꾸지 않는다. 런타임 사용자 지정 레이어 변경도 즉시 조회하며, Edit 모드 도구는 `Awake`가 없는 마커도 읽도록 기존 기본 조회 레이어를 사용한다.

`UnityActorSurfaceQuery`의 공개 생성자는 호출 후에도 보존 가능한 읽기 전용 결과를 반환한다. `CombatActor` 내부 어댑터만 종류별 재사용 결과 뷰를 즉시 소비하며 다음 같은 종류의 질의를 넘어 보관하지 않는다. Native 목록과 변환 목록은 액터별로 소유하고, Unity의 List 조회 오버로드가 용량을 늘리므로 고정 배열 포화로 접촉을 누락하지 않는다. 내부 바인딩 판정에는 외부 콜백이 없으며, 공개 조회의 바인딩 콜백이 재진입해도 호출별 결과를 분리한다.

낙사 복귀 이력은 순수 `SafeGroundHistory`가 불변 `SafeGroundSample`로 보관한다. `TrySafeBefore`는 T-3 이하의 최신→이전 순서로 현재 안전성을 질의하며 후보를 소비하거나 미래 표본을 고르지 않는다. 시간만으로 오래된 표본을 버리면 현재 유일한 안전 표본을 잃을 수 있어 액터 리셋까지 보존한다. `CombatPlayerRecoveryQuery`는 실제 액터를 이동하지 않고 후보 방향/몸으로 기존 막힘·전체 지지/평탄도와 현재 위험 bounds를 검사한다. 기록 때도 현재 위험 겹침은 제외한다. 같은 최대 HP 10% 피해·행동 취소·점프/속도/보호·대시 절대 회복과 상태 해제 범위를 유지한다. 유효 기록이 없으면 기존 테스트 시작 위치를 사용하며 모든 시작점까지 무효인 제품 맵의 대체 정책·챕터 식별/진행/저장은 아직 정하지 않는다.

`Combat/Runtime/Application`의 `Deviant.Combat.Execution`은 `noEngineReferences`로 UnityEngine을 참조하지 않는다. `CombatSimulation`이 `ICombatTickWorld` 포트를 통해 갱신 순서를 실행하고 `SimulationClock`이 유일한 전투 시각·틱 수를 보관한다. `CombatArena.Now`는 이 시계의 조회이며 별도 값을 누적하지 않는다. 자동 FixedUpdate와 수동 실행은 같은 실행기를 호출한다. 현재 고정 주기와 float 누적을 유지한다.

현재 순서는 플레이어 → 행동 → 행동 밀기 → 적 → 적 탄 → 폭탄 → 플레이어 탄 → 레이저 → 폭발 표현 → 분신 만료 → 시계 증가 → 피드백 만료다. `Runtime/Unity/CombatArenaTickWorld`가 각 단계에서 기존 Unity 엔티티를 호출한다. 단계별 목록 복사 시점과 순서를 유지하며, 아직 액터·판정·투사체 규칙 전체가 순수 실행기로 옮겨진 것은 아니다. 레이저/폭발/피드백 수명은 현재 호환 단계이며 Cue 수명 이행 때 분리한다.

실행기는 잘못된 시간·재진입 요청을 상태 변경 전에 거부한다. 단계나 리셋 콜백 중 예외가 발생하면 `SimulationFaulted`를 표시하고 추가 진행을 차단한다. 부분 처리의 자동 재시도나 롤백을 약속하지 않으며 성공한 리셋 뒤 진행을 재개한다. 일반 리셋은 시각/틱 수를 유지하고 새 실험 리셋은 시각/틱 수와 입력을 비운 뒤 같은 액터·효과 초기화 경로를 호출한다.

`AcquireManualSimulation`은 한 전투에 하나의 수동 실행권을 부여한다. 소유 중에는 해당 전투의 자동 FixedUpdate와 키보드 처리를 막고 `Step`의 명시적 입력으로 같은 갱신 순서를 한 번 실행한다. 다른 전투의 자동 갱신 설정과 `Time.timeScale`은 바꾸지 않는다. 수동 `Reset`은 시각 0부터 실험을 시작하며 일반 게임의 `ResetTest` 시계 정책은 유지한다. 입력 차단은 그대로 존중한다. 해제는 입력을 비우고 자동 갱신을 복구하며, 중복/이전 실행권 해제는 새 소유자를 해제하지 않는다. 수동 실행권과 일반 입력/정지 요청은 별도 소유권이다. `Control`의 정지 요청이 남아 있으면 수동 `Step`도 진행 전에 거부되며, 수동/일반 전투 리셋은 제어 요청을 해제하지 않는다.

`CombatLab/Editor/SwordDashComparison`은 기존 시험장 지형에서 빈 능력치 슬롯·고정 배치·0.02초 입력 틱으로 A/B를 실행한다. A의 공격/대시 60틱과 벽 앞 대시 20틱에서 공통 수동 실행 호출이 성공한 뒤 제출 표본을 `CombatInputRecorder`에 기록하고, B는 A가 완성한 같은 불변 기록을 읽어 재생한다. 입력 파일을 불러온 모드에서는 A/B 모두 검증된 두 구간의 같은 표본을 사용한다. `Authoring`의 `Sword and Dash A-B` 창은 현재 고정/프레임 실행 정의와 이동 프로필을 A/B로 복사한다. 프레임 후보·기반 고정 정의·모드를 함께 유지하고 Play Mode 전환에서 캡처를 보존하며, 누락 프레임 캡처를 수치로 대체하지 않는다. HP 감소·첫 적중 관측 틱·평지/벽 앞 이동 거리와 구간별 입력 수/해시를 표시한다. 타격은 `CombatActor.ReceivedHitCount`로 관측해 0 피해와 미적중을 구분하며 HP 감소는 별도로 측정한다. 비교 전 A/B와 원래 모드의 프레임 바인딩/배열을 검사하고, 완료/실패 후 장비·정의·이동·고정/프레임 모드와 기반 고정 정의·적 설정을 복원해 전투를 리셋한다. 기존 HP·진행 중 공격을 복원하는 저장 기능은 아니다.

`CombatLab/Contracts/CombatInputRecording`은 시나리오 구간 ID·고정 틱 간격·순서 있는 CombatInput 표본·내용 해시를 방어 복사한 스냅샷이다. 계약 어셈블리는 Editor 전용이며 noEngineReferences를 적용한다. 고정 기록기는 호출자가 지정한 용량으로 제한하고 빈 기록의 완료·완성 후 추가 쓰기를 거부한다. 실패한 비교는 진행 중 기록을 공개하지 않으며 기록은 실제 행동 성공이나 초기 세계/장비 저장이 아니다. SwordDashInputSet은 기존 두 구간 ID·틱 간격·60/20틱 용량을 확인한다. SwordDashInputSource는 두 구간/모든 입력 값·해시를 검증한 뒤 선택을 바꾸고 잘못된 파일은 기존 선택을 유지한다. 파일은 1MiB 제한·덮어쓰기 없는 원자적 생성·읽었던 바이트 해시를 재확인한 원자적 교체를 사용한다. 아래의 실제 입력 live 관측과 이 고정 A/B 파일은 구분하며 제품 빌드는 실험/저작 어셈블리를 포함하지 않는다.

공통 시뮬레이션은 Player.Configure 뒤 구체 입력 소유자를 한 번 바인딩한다. Consumed는 중재 뒤 edge-clear이며 전체 tick 성공이나 공격 성공이 아니다. 선택적 bounded capture는 진입 tick/Now·Completed/Faulted·ObservedThroughNow를 별도로 제공한다. 사전 확보한 원시 ring에 값만 쓰므로 gameplay 중 할당·관측자 콜백이 없고 TryRead에서만 불변 배치를 만든다. 오래된 관측 유실 수를 노출하며 CombatInputRecorder와 기존 Sword and Dash A-B 창은 Game/Lab의 실제 소비·폐기를 256개 경계로 제한해 poll한다. 이 메모리 진단은 고정 A/B 파일·행동별 성공 결과·전체 월드 저장이 아니다. 고정 A/B JSON/해시는 스키마 2의 Released를 포함하고 스키마 1은 거부한다. 패드/원시 장치 이벤트·개별 edge 파일 재생·초기 월드/장비/정의 저장은 남은 범위다.

ExperimentInitialState/ExperimentScenario/ExperimentSession은 고정 입력 파일·라이브 관측과 별개로 명시적 단일 Lab의 공간/프로필/장비/정의 초기 조건과 임의 길이 입력을 원자 저장/재생한다. 기존 공통 실행권과 ICombatReadModel을 사용하고 미지원 공간/현재 Native 바인딩·콘텐츠 불일치는 reset 전에 거부한다. 읽기 지표/실제 진단 로그를 같은 Editor에서 비교하며 일반 Game 세계·Run 저장이나 모든 장치 사건 재생은 남아 있다.

`ActorReadSnapshot.Resources`는 실제 두 무기의 충전 소유자에서 인스턴스/형태/특수·용량/가용량/주기/다음 준비 시각·선택 슬롯을 불변 값으로 읽는다. Equipment 참조나 별도 부채 계산기를 보관하지 않고 준비된 조회에서 컬렉션을 할당하지 않는다. 초기화/리셋의 세션 경계는 기존 읽기 모델과 같다.

재사용 시나리오 관측은 실제 모든 명령 결과와 이유/상세·원시 키/이벤트 순서/실행 ID, tick별 두 슬롯 자원·명령 구간을 보존한다. `ResourceTraceHash`·`CommandTraceHash`를 결합 `TraceHash`에 포함하고 기존 `HitTraceHash`의 수락 타격 의미는 유지한다. 별도 `IdentityTraceHash`는 fixture 순서와 최초 관측 순번으로 액터/명령/실행/효과 인과 관계를 비교하며 원시 사실의 실제 ID를 바꾸지 않는다. 피해나 로그로 성공을 추정하지 않는다.

실패한 tick은 성공 결과로 게시하지 않는다. `LastFailure`는 시도 tick·완료 수·원래 예외와 선택적 관측 예외·한 번 회수한 부분 명령/수락 타격을 공개하고 이전 성공 결과는 유지한다. UI는 실패한 시도를 명시하며 자동 재실행하지 않는다. 이는 관측 계약이지 전체 액터 저장이나 환경 직접 피해의 새 타격 사실 생성 규칙이 아니다.

실험 초기 정의는 기본/유한/직접/차지/일반 특수/원거리와 전체 모션 registry를 각 같은 컴파일러로 캡처·검증·재생·복구한다. `lab-versions-v7`는 모든 full payload·정책/내용·원거리 group·캡처 첫검/registry·완전 근접/특수 프레임 가족의 결합 hash를 포함한다. 명시 canonical Catalog 검증이 컴파일보다 먼저이며 시나리오 스키마 8은 이전 1~7과 누락된 실제 코드/질의 provenance 또는 특수 프레임 가족을 거부한다. 기존 명령/수락 타격 상관은 유지하고 HP/로그로 대체하지 않는다. 일반 Game/Run 저장과는 별개다.

GameComposition은 기존 두 무기의 IWeaponLoadout 한 소유자를 만들고 CombatRuntimeConfiguration.Loadout→Player.BindLoadout으로 초기화 전에 주입한다. Lab도 명시 소유자를 공급하며 Player 내부 숨은 장비 생성은 없다. GameSession은 기존 프로토타입/개발 리셋 수명 ID와 자기 Controls facade·HUD 라우터·명시 등록한 대화/시퀀스·숲 follow/subscription lease를 소유한다. 사망의 기존 행동/입력/효과 정리 뒤 자기 자식/화면/제어/추적만 닫으며 전역 ControlSession·외부 정지·응용 engine-time adapter·지형/플레이어/적 뷰는 파괴하지 않는다. ResetCompleted는 같은 Player/장비/뷰로 새 세션을 열며 제품 새 Run/부활/보상 규칙이 아니다. 옛 세션/대화 BindingId·정확한 시퀀스 핸들은 새 외부 실행을 닫지 않는다.

반복 숲은 실제 원본 기하를 공통 provider/구조 검증·동일 모터 기술 증명/WorldAssembler에 공급한다. world의 지속 ChapterSession과 GameSession의 추적 lease를 구분하고 reset/재방문은 기존 세션/상태를 유지한다. 프로필 변경은 같은 맵을 재검증하며 정식 Run/챕터 콘텐츠·필수 경로 채택과 구분한다.

| 영역 | 담당 작업과 공통 게임 연결 |
|---|---|
| `Assets/Deviant/Authoring/Editor` | Editor 전용 이동·대시·검 1타 수치/고정·프레임 모션 편집·A-B 비교 창과 원본 IO·생성 프레임 의미의 빌드 전 검증. 런타임 빌드에는 포함하지 않는다. |
| `Assets/Deviant/Definitions/Runtime` | UnityEngine 비의존 전투 카탈로그 직렬화 타입·이동/검 정의·Sprite/Audio Cue·페이드/연출 전용 이동 정의와 공격 모션/영구 프레임 시간 해석. |
| `Assets/Deviant/Combat/Contracts` | UnityEngine 비의존 입력 표본·갱신 단계·월드/행동/발동 자원 포트·읽기 전용 자원 결과·리셋 모드. |
| `Assets/Deviant/CombatLab/Contracts` | 엔진 비의존 불변 틱 입력 기록. 현재 Editor 실험 전용이며 제품 빌드에서 제외한다. |
| `Assets/Deviant/CombatLab/Editor` | 공유 전투의 고정 검/대시/벽 A/B와 용량 제한 입력 기록·B 재생. 제품 빌드에서 제외한다. |
| `Assets/Deviant/Combat/Runtime/Application` | UnityEngine 비의존 `Deviant.Combat.Execution`. 전투 시계·갱신 순서·리셋/실패 경계·플레이어 입력 버퍼/행동 중재·근접 기본 17타의 시간표/전진/대상 중복·임시 적 공격 시작/추적 의도와 공격 상태/시각·타격 중복 원본·액터 이동/제어 합성·수직 적분 제안·축 접촉/접지·안전 접지/막힘/텔레포트 해석·일방향 발판 무시/내려가기 원본·수리검 도착/복귀 기하·무기별 복귀 예약 원본·HP/보호막/방어·제어/은신 상태 원본·실행당 발동 자원 결과 수명. |
| `Assets/Deviant/Equipment/Contracts`, `Equipment/Runtime` | UnityEngine·Combat 비의존 프로필 무기 인스턴스/선택·두 무기 장착/활성 슬롯·해석된 설정의 정비 명령·충전/능력치 명령·불변 조회와 실제 원본. 기존 두 씬/실험 도구의 어댑터가 사용. 획득/교환·재고/Run은 후속. |
| `Assets/Deviant/Combat/Runtime` | 공통 전투·플레이어·적 AI·투사체·캐릭터 표현과 Unity 갱신 어댑터. `CombatArena`가 순수 실행기를 조립한다. |
| `Assets/Deviant/Combat/Scenes/CombatV2Arena.unity` | 같은 전투 구현을 평지·벽·발판에서 검사하는 시험장 |
| `Assets/Deviant/World/Contracts`, `World/Runtime/Chapter` | UnityEngine 비의존 `ChapterLayout`·고정/캡처 공급·공통 구조 검증·세션/배치 사실·뷰 구성 포트. 격리 모듈과 실제 반복 숲이 사용하며 실제 모터 기술 witness는 주입 포트로 검증한다. |
| `Assets/Deviant/World/Runtime/Application` | 기존 반복 숲의 불변 스트리밍 창/배치 계획. 정식 챕터 데이터로 치환하지 않는다. |
| `Assets/Deviant/Integration/Unity` | 제작 박스 기하 추출·공통 지형 바인딩·모듈/마커 뷰·세션 앵커의 Unity 좌표 연결. |
| `Assets/Deviant/World` | 순수 배치 계획을 소비하는 숲 타일/청크 뷰·카메라·몬스터 모션·체력바. 별도 플레이어/피해 시스템은 없다. |
| `Assets/Deviant/Run/Runtime` | UnityEngine 비의존 `RunFlow`·`RunSession`. 런별 시드, 처치 수, 보스 처치 표시, 챕터 시계 기준 플레이 시간과 사망·클리어의 1회 정산. 1챕터의 씬 전환·결과·타이틀은 `ForestCombatV2`의 `Chapter1RunHost`·`Chapter1RunBinding`·`Chapter1TitleScreen`이 연결한다. 저장·챕터 전환·경제는 후속. |
| `Assets/Deviant/Control/Contracts`, `Control/Runtime` | UnityEngine 비의존 입력 채널·스코프/핸들과 세션별 제한 합성/정지. |
| `Assets/Deviant/Presentation/Contracts`, `Presentation/Runtime` | UnityEngine 비의존 Cue 재생/소유자/미디어 준비 포트와 완료·취소·실패 수명·위치 요청 선택·페이드 재생/유지 채널·전용 뷰의 독점 포즈 수명. |
| `Assets/Deviant/Presentation/Unity` | 버전에 맞는 Sprite 프레임과 주입한 AudioClip 바인딩·표현 전용 뷰·단일 카메라 위치 출력. 기본 공격 Sprite 이펙트/두 씬 추적과 격리 AudioSource/페이드 Image/연출 전용 Sprite 출력에 연결. |
| `Assets/Deviant/Sequences/Contracts`, `Sequences/Runtime` | UnityEngine 비의존 순차/ParallelAll 실행·자식 상태·완료/취소/실패/명시적 스킵·타임아웃과 소유 제어 반환. 현재 격리 fixture 범위. |
| `Assets/Deviant/Narrative/Contracts`, `Narrative/Runtime` | UnityEngine 비의존 NPC/직선·선택지 대화·후보/진행/선택 조건·명시적 동기 명령 포트/세션별 실행 기록과 수명. 현재 격리 데이터 연결 범위. |
| `Assets/Deviant/UI/Contracts`, `UI/Runtime/Presentation` | UnityEngine 비의존 HUD/대화 Presenter·초기 화면 포커스/Back 라우터·표시/입력 계약. 제품 전체 화면 등록/키/레이어는 후속 범위. |
| `Assets/Deviant/UI/Unity` | 주입한 TMP/버튼/패널의 대화 View와 CanvasGroup/EventSystem/Selectable 화면 View. 도메인/시간/플레이어 입력 bool을 직접 소유하지 않는다. |
| `Assets/Deviant/UI` | HUD 프리팹·레이아웃·게이지·일시정지. 데이터와 제어 요청을 인터페이스로 연결한다. |
| `Assets/Deviant/Weapons` | 무기 표현 자료·라이브러리·임포터. 독자 이동·공격 처리는 없다. |
| `Assets/Deviant/Game` | 공통 씬·지형 연결·몬스터 표현·HUD 연결·빌드 진입점 |
| `data/combat`, `tools/combat` | 수치 편집 원본과 카탈로그·모션 가져오기 |
| `WeaponReference`, `ForestReference`, `아트` | 제작·공유 자료와 모션/배경 비교 도구. 게임 규칙 원본으로 사용하지 않는다. |

## 씬과 작업 연속성

게임 표시명은 `deviant`, 내부 코드의 공통 네임스페이스 루트는 `Deviant`다. 코드와 에셋은 `Assets/Deviant` 아래에 모은다. `Deviant.Game`은 조합·진입점, `Deviant.Combat`은 전투, `Deviant.World`는 지형·몬스터 표현, `Deviant.UI`는 HUD, `Deviant.Weapons`는 무기 표현 자료를 담당한다. 하위 네임스페이스는 한 게임 안의 역할 구분이며 별도 시뮬레이션을 만들지 않는다. 전투 어셈블리와 테스트도 `Deviant.Combat`, `Deviant.Combat.Editor`, `Deviant.Combat.Tests`를 사용한다.

`TwilightInkMotionTest`, `TwilightInkMonsterTest`, `TwilightInkHUDTest`, `TwilightInkHUDLayoutTest`는 `Assets/Deviant/World/Scenes`에, `FourWeaponsForestTest`는 `Assets/Deviant/Weapons/Scenes`에 있는 진입 씬이다. 씬·스크립트·에셋의 GUID를 유지하며 새 경로를 사용한다. 실행하면 `GameEntry`가 공통 씬을 연다. 별도 월드·플레이어·전투 상태를 포함하지 않는다. 기존 숲·몬스터·무기 메뉴도 공통 게임을 연다. HUD 레이아웃은 `BattleHUDv2.prefab` Prefab Mode에서 편집한다.

공통 씬은 직접 편집하는 기준 씬이다. `GameBuilder.Create`는 호환용 열기 함수이며 씬을 다시 생성하지 않는다. `RebuildEntryScenes`는 진입 씬만 다시 만든다. 기본 팀 빌드와 이전 `BuildForestWindows` 호출은 같은 공통 게임을 빌드한다. 독립 전투 시험장 빌드는 계속 제공한다.

숲 담당자는 `InkForestWorld`, `InkForestData`, `ForestChunk.prefab`과 타일을, 몬스터 담당자는 `InkMonsterData`, `InkMonsterMotion`, `ForestEnemyPresentation`을 수정한다. `Deviant.World.Editor.InkForestBuilder.RefreshTerrain`과 `Deviant.World.Editor.InkMonsterBuilder.RefreshApprovedArt`는 제작 데이터를 갱신한다. 무기 담당자는 확정 패키지와 [표현 파이프라인](../presentation/visuals/combat-presentation.md)을, HUD 담당자는 프리팹과 `BattleHudView`를 수정한다. HUD 생성기는 프리팹만 갱신하며 공통 씬의 연결을 다시 만들지 않는다.

## 공통 연결

CombatArena는 명시적 CombatRuntimeConfiguration을 받는 공통 Unity 실행 어댑터이며 암묵적인 시험장 시작 경로를 제공하지 않는다. Integration의 CombatContentLoader는 Resources 전송/미디어 해석을 제공하고 Arena.Initialize는 공급된 카탈로그를 같은 컴파일러로 검증해 요청한 액터만 생성한다. GameComposition은 숲/카메라·입력·표현과 세 임시 적의 최종 숲 위치/프로필을 직접 공급한다. 생성 후 시험장 적을 옮기지 않으며 기존 초기 프레임 비공격→다음 프레임 AI 활성화 경계·HP/크기/offset·정렬/아트/체력바·HUD·카메라 리셋을 유지한다. 임시 프로필 조립은 정식 챕터/Run 콘텐츠 확정이 아니다.

챕터/Meadow의 `ForestCombatWorld`도 같은 명시 초기화를 사용한다. 지형 기하를 먼저 준비하고 `CombatRuntimeConfiguration.EnemyFactory`가 요청한 실제 적만 생성하며 임시 Lab 지형·적을 만들었다가 지우지 않는다. 들쥐는 기존 Brain의 공격 요청을 공통 AI 명령·실제 시작/완료·확정 타격 출처로 연결한다. HUD는 기존 Cancel 액션과 ScreenRouter·자기 시간/카메라 핸들을 사용한다. 챕터 맵의 120Hz는 해당 씬의 명시 정책이며 비활성화 때 반환한다. 정식 Run/몬스터 전체 정책을 새로 확정하지 않는다.

LabComposition은 시험장의 실제 일곱 지형·낙사 볼륨·초기 배치·카메라 렌즈/추적·캡처 CLI와 코루틴을 소유한다. CombatLab.Unity는 UNITY_EDITOR 또는 DEVIANT_COMBAT_LAB일 때만 컴파일하며 autoReferenced=false다. 시험장 빌더는 해당 define을 명시하고 일반 제품 Player는 Lab/Authoring/Editor를 포함하지 않는다. 기존 기술 HUD/장비 IMGUI는 Game.Diagnostics.Unity의 CombatDiagnostics로 옮겨 두 루트에서 명시적으로 연결한다. 공통 Arena는 디버그 박스/플래시/이벤트의 읽기 값만 제공하고 UI·캡처·시험장 카메라 정책은 소유하지 않는다. GameBuilder.BindSceneComposition은 직접 작성한 공통 씬을 재생성하지 않고 바인딩하며 기존 GUID를 유지한다.

공통 `CombatControlBinding`이 순수 `ControlSession`을 조립하고 입력 제한/정지를 두 씬의 플레이어·키보드·자동/수동 틱에 적용한다. 입력 채널은 합집합이며 정지는 남은 요청 중 하나라도 있으면 유지한다. 소유자 이름은 진단용이고 같은 이름도 서로 다른 스코프다. 개별 핸들·스코프는 중복 반환이 가능하고 세션 파괴 뒤 무효화된다. 전투 입력 제한/정지의 유효 결과 변경 시 미소비 표본을 비우며 차단 중 새 표본도 버린다. 기존 `InputBlocked` 호환 프로퍼티의 테스트 차단은 스코프와 별개로 유지한다.

본 게임은 `BindEngineTime`으로 단일 Unity 시간 어댑터를 명시적으로 연결한다. 최초 정지 때의 `Time.timeScale`을 마지막 정지 반환 시 복원하며 HUD가 직접 쓰지 않는다. 실험장 로컬 정지는 전역 배율을 바꾸지 않는다. 상호작용/대화 입력은 현재 `NpcDialogueHost`/`DialoguePresenter`에서 소비한다. 슬로모션 요청 합성, 전체 GUI 표시 Presenter/제품 화면 등록, 장치 내비게이션 입력·카메라 렌즈/흔들림 정책은 후속 범위다. 자세한 수명·리셋 구분은 [HUD 구현](../presentation/ui/hud.md)을 따른다.

기존 기본 공격 이펙트는 불변 Sprite `PresentationCueDefinition`과 `CuePlayer`를 사용한다. `BasicAttackCueBridge`가 발생 당시 위치/방향·시작 공격 시계와 행동별 스코프를 연결한다. 공통 `ICueDefinition`과 `ICueView.Sample(elapsed)`는 Sprite/Audio를 같은 준비·재생·완료·취소·실패 수명으로 처리한다. `AudioCueDefinition`은 클립 ID·정확한 샘플 수/주파수를 고정하고 `AudioCueFactory`/`AudioCueOutputBinding`은 주입한 AudioClip을 실제 AudioSource에 연결한다. 저작·수동 검사 경로의 첫 샘플은 정지 위치, 전진한 시각은 재생, 같은/역행한 시각은 정지/seek한다. 게임용 PublishContinuous 경로는 사건 시점에 시작하고 정상 진행 중 재탐색 없이 DSP를 이어가며 명시 hold/재개를 구분한다. 양쪽의 완료 권한은 주입 시계에만 있다. DSP는 poll 사이를 보간할 뿐 sample-accurate 결합은 아니다. 출력 유실·소유자 종료·루프·늦은 결과도 같은 핸들 수명으로 정리한다. Cue Source 창은 Sprite/Audio 원본 저작·private 미리보기·명시 카탈로그 게시를 제공하고 시퀀스도 같은 유한 Cue 핸들을 사용한다. 제품 음향/믹서·장치 청취·실제 필요에 따른 풀은 별도이며 기본 공격에 새 소리를 자동 추가하지 않는다. 자세한 연결과 제한은 [전투 표현](../presentation/visuals/combat-presentation.md)을 따른다.

카메라 위치의 실제 적용은 `CameraOutputBinding`이 맡는다. 기존 숲/시험장 추적은 기본 위치 공급자로 남고 `CameraDirector`의 소유자 스코프가 우선순위/순번으로 위치 요청을 합성한다. 종료 시 남은 현재 소스/기본 추적을 다시 선택한다. 출력 비활성화/파괴와 관측한 카메라 가용성 유실은 해당 요청 세션을 닫고, 재활성화는 현재 `Requests`로 새 세션을 얻는다. 기본 추적 공급자는 유지해 최신 구도로 돌아가며 조회 콜백 중 출력 종료는 옛 프레임 쓰기를 막는다. 시퀀스는 이전 카메라 핸들의 취소를 전달하고 새 출력에는 바인딩을 명시적으로 다시 조립한다. `ChapterCameraSource`는 같은 챕터 세션의 앵커를 읽고 오래된 바인딩은 실패한다. 렌즈·PixelPerfectCamera 설정·줌/흔들림·블렌드·실제 대화/연출 콘텐츠 우선순위는 미이행이다. 자세한 적용/리셋 수명은 [카메라 구현](../gameplay/world/forest-prototype.md#카메라)을 따른다.

`ChapterWorldHost`는 명시적 구성 진입점이다. 고정 공급 또는 캡처한 생성 결과를 같은 `ChapterLayout` 검증/구성 경로로 열고, 모듈 뷰와 세션 배치 사실을 분리한다. `ChapterWorldIntegrationTests`는 본 게임/시험장의 격리된 평지 박스 데이터에서 이 호스트와 기존 공통 플레이어를 연결한다. 실제 반복 숲도 `ForestLayoutSource`의 원래 기하를 같은 공급/검증/`WorldAssembler`/`ChapterSession`으로 연결하며 실제 모터의 기술 witness로 이동 프로필을 사전 검증한다. 정식 챕터/필수 경로·Run 전환·제품 NPC/아이템 콘텐츠는 미정 범위다. 지원되는 기하·해시/이동 재검증·실패/수명 계약은 [챕터 레이아웃](../gameplay/world/chapter-layout.md)을 따른다.

`NpcDialogueHost`는 `ChapterNpcTargets`의 안정적인 세션/배치/앵커를 직선·선택지 대화와 Presenter/uGUI View에 연결한다. 후보와 실제 시작의 조건을 다시 검사하고 입력/정지 정책은 명시적으로 받는다. 선택은 현재 줄·선택지·목적지 조건을 다시 검사하며 가용성 변경은 표시 리비전으로 구분한다. 선택적 명령 ID는 주입한 `IDialogueCommandBindings`가 사전 검증하고 세션 범위에서 동기로 실행한다. 명령 성공 뒤에만 진행하고 거부/취소/실패를 구분하며 중복 실행 기록은 세션 종료에 비운다. 성공 영수증과 반환 오류는 별도 스냅샷으로 제공하고 효과를 임의 롤백하지 않는다. 주입된 선택지 슬롯과 표시 당시 토큰을 캡처한 버튼 콜백을 사용해 연속/옛 클릭을 거부한다. 완료/취소/대상 유실/표시 실패는 자기 제어만 정리한다. 현재는 두 씬의 격리 마커/기술 텍스트/가짜 명령이며 기본 숲의 실제 NPC·대사·이야기 사실·제품 도메인 효과/보상/Run 수명은 이행하지 않았다. 상세한 계약과 UI/키 매핑 제한은 [NPC·대화](../presentation/narrative/narrative-dialogue.md)를 따른다.

`ScreenRouter`는 주입한 포커스 가능 View를 명시적 우선순위·입력/정지·Back 정책으로 조율한다. 실행별 바인딩 ID로 옛 입력을 소비하고, 포커스 전환 중에도 같은 Back이 뒤의 화면/게임으로 새지 않도록 호출자에게 소비 결과를 반환한다. 기준 포커스는 라우터가 한 번 소유하고 모든 화면 종료 뒤 유효한 이전 대상으로 반환한다. `ScreenView`는 기존 패널/CanvasGroup/EventSystem/Selectable을 연결하고 선택 콜백의 종료/전환을 지연 처리한다. 실제 Game HUD는 GameComposition이 주입한 라우터와 기존 UI cancel 액션을 사용하며 ScreenInputBinding만 물리 Back을 처리한다. 다른 제품 화면/대화 전체 등록·포인터 전체 라우팅·재고/설정 콘텐츠·수동 가독성은 별도다. 범위와 제한은 [화면 포커스·Back](../presentation/ui/screen-routing.md)을 따른다.

`SequenceRunner`는 `ISequenceBindings`로 직선 대화·카메라 위치 요청·일회성 Sprite Cue·페이드·연출 전용 뷰 이동·대기를 순차 또는 중첩 없는 `ParallelAll` 그룹으로 실행한다. 그룹은 모든 자식의 완료를 기다리고 첫 실패/취소/타임아웃에 활성 자식을 정리하며, 자식 상태는 불변 스냅샷으로 제공한다. `ChapterSequenceBindings`가 필수 앵커/배치·시작 버전·스킵 종료 서비스·페이드 출력 세대를 검사하고 기존 대화 Presenter·Cue·카메라·Fade와 독점 StageActor 포트를 연결한다. `SequenceHost`는 비활성화/파괴와 콜백 중 닫힘에서 자기 실행/제어만 정리한다. 현재 두 씬의 고정/캡처 챕터 fixture이며 제품 연출/스킵 결과를 추가하지 않는다. 시간·종료/페이드 유지 계약과 게임플레이 액터 제어·저작/실제 Run 연결의 제한은 [시퀀스 실행](../presentation/narrative/sequence-execution.md)을 따른다.

몬스터는 공통 `TestEnemy`의 임시 3종 AI와 고정 피격 박스를 사용한다. 공격 시작/추적 판단은 Unity 비의존 `EnemyObservation` → `EnemyIntentResolver` → `EnemyIntent`로 분리한다. `EnemyAttackExecution`은 공격 시작/진행 여부·다음 공격 시각·실행 순번·원거리 일회 발사와 타격별/대상별 중복 원본을 소유하며 `EnemyAttackSnapshot`은 불변 조회다. 별도 시계를 누적하지 않고 같은 `arena.Now`를 읽는다. `TestEnemy`는 기존 은신/분신 대상 선택과 의도 해석 → 방향/절벽 확인 → 공통 모터 → 타격창의 반복 Native 질의 → 현재 경과시간 기준 완료 순서를 연결한다. 예고·3연타·취소/리셋 대기와 float 계산 순서를 유지하며 활성창에 늦게 들어온 대상도 기존처럼 검사한다. 동기 피해 콜백의 취소/리셋은 이미 진입한 타격 순서를 소급 중단하지 않는다. Native 대상 탐색·차폐·피해·탄/물리와 정식 몬스터 정의/AI 저작은 후속 범위다. `ForestEnemyPresentation`은 멧돼지·잉크눈 모션, 피격 점멸, 사망·리셋 표현만 담당하며 모션별 콜라이더나 별도 HP·무적 원본을 만들지 않는다. 실제 몬스터 체력바도 `CombatActor`를 읽고 보스는 임시 외형이다.

`EnemyTickExecution`은 사망 단락→Native 대상 참조 포착→기절/경직 제어 snapshot→기존 공격 조회/의도 해석→공격 시작 뒤 방향 또는 추적 방향/절벽 확인→공통 모터→같은 제어 snapshot으로 기존 공격 진행 순서를 상태 없이 조율한다. live ref 호스트는 기존 대상/좌표 getter 순서와 방향 전환 후 현재 위치·방향의 바닥 질의를 연결한다. 근접 흐름도 Native 박스 목록 순서로 현재 플레이어/분신·사망·차폐 단락→기존 FirstHit claim→현재 프로필/방향 피해→현재 debug 표시를 실행하며 물리 뒤 제어를 다시 계산하지 않는다. Native 대상 참조·Vector2/Physics 기하·목록·차폐/피해·탄/표시는 Unity 어댑터에 남고 공격·상태·모터·시계 원본은 추가하지 않는다.

기존 숲 전투의 다른 무적·피격 취소·물리 정지 규칙은 승계하지 않는다. 전투 규칙은 [현행 명세](../../design/combat/rules.md)를 따른다. 성장·가방·획득·챕터 진행·저장·보상은 후속 범위다. `RuralDusk32` 확정 배경과 `LatestMotion` 보정본은 별도 공급 경로이며 이 연결만으로 자동 반영되지 않는다. 이미지 복원 전에는 전체 화면·플레이 검증을 완료할 수 없다.

검증은 [검사 방법](../testing/testing.md), 제작 기준본과 공유 사본은 [제작 규칙](../../workflow/asset-production.md)을 따른다.
