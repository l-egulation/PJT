# 전투 구현 범위

[분야 개요](README.md)

> 장비 성장 이행 범위: 제품 규칙은 [무기 도면·런 잉크 도감](../../../design/equipment/rules.md)을 따른다. 아래 팔레트·스탬프·출발 도면 선택·바닥 아이템 드롭은 현재 구현 설명이며 새 제품 기획이 아니다. 런 도감·무기 잉크 적용·내구도·전투방 3지선다의 구현과 실행 검증은 미완료다.

F2 판정 표시는 Game 뷰의 Gizmos 설정과 독립적이다. 몸체·기존 근접 판정에 더해 챕터 몬스터의 실제 `AttackBounds`를 준비 중 노란색, 피해 활성 구간 빨간색으로 표시하고 취소·후딜에서는 숨긴다. 적 투사체는 실제 0.25×0.25 박스, 플레이어 탄환·수리검·폭탄은 충돌 질의의 반경을 읽은 하늘색 원으로 표시한다. 폭발 직후에는 기존 폭발 표시 수명 동안 실제 폭발 반경을 주황색 원으로 보여준다. 판정 표시는 공격 시점·크기·피해를 변경하지 않는다.

검4·둔기3·원거리3의 총10형태를 공통 전투 런타임으로 구현한다. `CombatV2Arena`는 같은 런타임의 독립 시험장이며 [공통 게임](../../architecture/architecture.md)은 숲 지형·HUD를 연결한다. 실제 챕터 탐험·획득·재고·드랍·저장·보상 경제·전용 잉크 콘텐츠는 후속 범위다. 최종 아트와 임시 시각물을 구분한다.

원거리 15개의 불변 정의·전체 후보 게시/시작 버전은 기존 SkillExecution/PlayerProjectile/Bomb/Missile 소유자를 사용한다. 새로운 runner/시계를 만들지 않으며 발동 시점 피해·실제 비행/복귀/설치/종료 순서를 보존한다. 완전 RangedActionSnapshotData codec은 초기 저작 정의 저장용이지 현재 비행/소모/예약 상태 저장이 아니다.

ICombatHitEvents는 실제 ApplyDamage가 수락한 직후의 불변 CombatAcceptedHit를 제공한다. 명령·실행·타격·효과·발사 액터와 별도 반사자 provenance를 실제 생성/전달 경로에서 캡처하며 나중 currentCast로 추정하지 않는다. 0/보호막 흡수 피해도 수락 사실이고 사망/무적/패링 거부는 기록하지 않는다. tick/논리 시각·요청/적용/흡수 피해·전후 vitals·대상 handle을 고정 버퍼에 관측자 콜백 없이 기록한다. reset/옛 세션은 명시 재바인딩을 요구한다. ForestEnemyPresentation은 HP 감소 추정 대신 이 사실로 피격 표시하고 실험장은 실제 접수/시작 키와 첫 수락 타격을 연결한다.

PlayerActionStartExecution은 기본/특수의 기존 정의·DTO 선택과 자격/페이로드 조율을 상태 없이 실행한다. Native PlayerStartHost는 같은 live 장비/방향/형상·Create 인자 평가와 currentCast 대입을 담당한다. finite 행동·기본/직접/차지 정의의 시작 버전, 원거리/폭탄/수리검 참조와 실제 command Started 소유권은 유지한다. PlayerFallRecoveryExecution은 취소→점프 예약 반환→최대 HP 10% 직접 피해→사망 또는 T-3 안전 이력/시작점→방향 전후 두 위치 쓰기→속도/점프/보호/접지/피격 잠금/로그 순서를 조율한다. 안전 이력/실제 지형 질의는 기존 원본을 사용하며 환경 직접 피해를 Receive/확정 타격으로 추정하지 않는다. 새 상태·시계·제품 규칙을 만들지 않는다.

LocomotionProfile 스키마 2가 이동·점프 속도·공통 탄도 중력의 단일 불변 원본이다. PlayerJumpExecution의 사용량/예약과 Native 모터/반동/미사일은 이 정의를 사용한다. Editor 저장/생성·리셋 적용으로 실험하고 진행 중 공중 상태를 재해석하는 별도 적용 경로는 두지 않는다.

실험장 재사용 시나리오는 지형·카탈로그·매체·정의 해시 외에 실제 관리형 Editor의 로드된 게임플레이 모듈과 소비 중인 Physics2D 질의 설정이 같아야 한다. 코드 또는 설정 불일치는 수동 실행권 획득·리셋 전에 거부하고 선택·이전 결과·현재 전투를 보존한다. 수치/매체 데이터만 바꾸는 실험은 C# 재컴파일을 요구하지 않으며 코드 식별자를 바꾸지 않는다. 이 계약은 일반 월드 저장이나 Player/IL2CPP·교차 플랫폼 결정성 보장이 아니다.

↑를 누르고 있을 때만 벽에 붙어 기어오르며, ↑를 놓으면 즉시 일반 낙하한다(미끄러짐 없음). 벽 차기는 ↑를 유지하면서 Space를 새로 누를 때만 실행한다. 좌우 방향키로 이동하고 A/S로 무기 1/2 기본 공격, Q/W로 무기 1/2 스킬을 요청한다. Space는 일반 점프, Shift는 대시다. ↑ 없는 Space는 일반 점프이며 예약·코요테도 ↑ 조건을 우회하지 않는다. 리셋·창 포커스 상실·일시정지 때 입력을 초기화한다.

`CombatArena.Initialize`는 명시적인 구성 입력을 받은 뒤 해당 arena의 `CombatSimulation`을 생성하고 플레이어 입력을 연결한다. 이 초기화는 `Awake` 호출 여부나 Editor의 도메인 재로드 설정에 의존하지 않는다. 맵 전환은 씬과 arena를 교체하므로 이전 시계·입력·적을 새 맵으로 유지하지 않는다.

일방향 면의 `ArenaSurface.oneWay`는 아래에서 상승·옆 이동할 때 통과하고 하강 시 지지하는 충돌 규칙이다. `blockDropThrough`는 별도 내려가기 허용 여부이며 흙 발판은 차단, 나무 발판은 허용한다. `ActorPlatformContact.CanDropThrough`가 이 사실을 순수 내려가기 실행기로 전달한다. 허용·차단 면이 겹치면 차단 지지를 우선하며 거부한 요청은 위치·속도·무시 핸들을 변경하지 않는다. 지면·벽의 기존 고체 충돌은 바꾸지 않는다.

## 테스트 공간과 임시 적

좌표는 Unity unit·발 피벗 기준이다. 시작점(-4,0), 평지 x=-20~20, 낙사 간격 x=20~24, 건너편 평지 x=24~46이다. 벽 중심(13,1.5)·크기(0.4,3), 일방향 발판 중심(-8,2.7)·크기(5,0.25), 높은 발판 중심(-13,4)·크기(3,0.4)다. 명시적인 낙사 볼륨을 사용한다. 이는 독립 전투 시험장이며 [챕터 내부 자유 탐험 구조](../../../design/run/run-structure.md)의 구현이 아니다.

| 임시 적 | 시작점 | HP | 공격 | 이동·예고·주기 |
|---|---|---:|---|---|
| 근접 | (3,0) | 100 | 피해10·활성0.08초·전방1.6×2 | 속도2.5·예고0.35초·타격 시작 후0.6초 종료·이후0.5초 대기 |
| 원거리 | (9,0) | 60 | 피해8·수평 탄속8·탄체0.25×0.25 | 속도2.5·거리8 이내 발사·예고0.35초·후속 주기는 근접과 동일 |
| 보스 더미 | (31,0) | 1000 | 각20·0.45/0.65/0.85초3연타·각 활성0.08초 | 속도1.5·마지막 타격 시작 후0.6초 종료·이후0.7초 대기 |

근접·보스 공격 시작 조건은 중심 수평거리2.2 이내·높이차2 미만, 추적 거리는10이다. 일반 적 기본 피격 경직0.30초·공격 취소와 플레이어 기본 공격의 밀림0.25unit/0.10초를 적용한다. 전용 밀기 효과와 합산하지 않고 보스·미끼는 기본 밀림에서 제외한다. 보스는 기본 경직/밀기/끌기/도발 면역·속박/기절 적용이다.

공격 시작/추적의 판단은 순수 `EnemyIntentResolver`가 불변 `EnemyObservation`을 읽어 해석한다. 공격 가능 시각과 공격 거리 상한은 포함하고, 높이차2/추적 거리10과 추적 정지 거리(일반/보스1.5, 원거리5)는 기존처럼 엄격한 경계다. 제어 중·대상 없음·AI 비활성·공격 중에는 새 의도를 만들지 않으며, 이 결과가 이미 시작한 공격을 취소하는 명령은 아니다. `TestEnemy`는 기존 대상 선택(일반 적의 가까운 분신 우선/은신 대상 제외), 방향 전환 뒤 절벽 확인과 공통 물리를 사용한 다음 기존 예고/타격/탄 실행을 진행한다. 속박의 실제 이동 차단과 기절/경직의 공격 제어도 기존 경계를 유지한다. 관측 시각/좌표의 비유한 값은 해석 전에 거부한다. 현재 임시 프로필 인덱스/수치를 보존한 분리이며 정식 몬스터 카탈로그·AI 저작·공통 적 행동 실행기 이행은 후속이다.

공격 상태와 시각은 `EnemyAttackExecution` 한 원본이 소유한다. `EnemyAttackSnapshot`은 시작/다음 시각·진행 여부·실행 순번·원거리 발사 상태를 값으로 조회한다. 기존 전투 시각으로 예고·활성창·현재 경과시간 기준 완료를 해석하고 별도 타이머를 누적하지 않는다. 타격창은 매 겹치는 tick마다 기존 Native 탐색을 호출하며 같은 실행/타격/대상만 중복 제거한다. 원거리는 실행당 한 번 발사한다. 취소는 현재 시각+0.3초, 리셋은 +0.5초, 완료는 일반/원거리 +0.5초·보스 +0.7초의 다음 시작 시각을 기록한다. 취소/리셋은 이전 시작 시각과 타격 이력을 보존하고 새 시작만 실행 순번을 증가시킨다. 동기 피해 콜백의 취소/리셋·재진입과 live 프로필 읽기 순서도 기존 동작을 유지한다. 이 경계는 임시 세 프로필의 실행이며 범용 적 행동/정식 AI·콘텐츠 정의가 아니다.

## 명령 접수와 실행 결과

`CombatArena.Commands`의 순수 `ICombatCommands` 계약은 플레이어 `CombatInputSample`을 기존 `CombatInputIngress`로 전달한다. 키보드와 수동/재생 입력은 같은 actor/session·generation/epoch·단조 순번 검사, 개별 press/release/continuous 표본과 같은 tick bit-union 우선순위를 유지한다. `Admitted`는 접수, `Consumed`는 중재 경계이며 실제 행동 시작/완료나 자원 소비 성공이 아니다. release/continuous 표본은 입력 캡처로 조회하고 행동 결과를 만들지 않는다.

실행 키는 `CombatActorHandle + 입력 순번 + 행동 종류`다. 같은 버튼의 중복 press는 첫 접수 표본이 기존 bit-union을 대표하고 나머지는 `Rejected/Priority`다. 기본/특수/교체 예약은 그 키를 실제 시작까지 보존하며 높은 우선순위·교체·리셋으로 사라지면 원래 키를 거부한다. 세 번째 점프의 기존 착지 버퍼도 원래 키를 보존하고 실제 착지 점프·만료·대시/특수의 기존 버퍼 제거에서만 확정한다. 접수나 `currentCast` 변화로 성공을 추측하지 않는다.

`Started`는 Native 시작 연산의 영수증이며 실행 GUID를 실제 Cast 자원 키와 연결한다. `Completed`는 행동의 기존 완료 경계, `Cancelled`는 실제 취소/제거, `Failed`는 발동 자격·자원 거부·Native 예외를 구분한다. 대시는 기존 끝/벽 종료, 점프·교체·내려가기·수동 미사일 종료는 해당 즉시 연산 완료를 기록한다. 완료는 적중·피해·실제 이동 거리나 이미 생성된 독립 효과의 소멸을 보장하지 않는다. 공격 발동 때 자원 재검사/소비와 예외의 부분 효과·전파 순서는 기존대로다.

AI는 기존 `EnemyIntentResolver`의 관측/정책을 그대로 사용한다. `BeginAttack`/`Chase` 의도는 플레이어 S/D 중재기가 아니라 같은 세션·등록 액터 검사와 액터별 단조 명령 순번을 거친다. 공격은 실제 `EnemyAttackExecution.Begin`에서 시작하고 기존 완료/피격 취소에서 같은 키로 끝난다. 추적은 기존 방향→절벽 확인→모터 순서이며 `Completed`는 모터 호출이 반환했음을 뜻한다. 벽/속박으로 이동량이 0이어도 거리 성공으로 해석하지 않는다. 정식 몬스터 카탈로그·새 AI 정책은 추가하지 않는다.

`TryReadOutcomes(cursor, callerBuffer, out page)`는 arena별 1,024개 preallocated 값 ring을 독립 cursor로 조회한다. 값에는 이벤트 단조 순번·논리 tick/Now·원래 명령/실행 ID·해당 액터의 불변 시점 조회와 이유가 있다. 오래된 cursor는 정확한 유실 수를 반환하며 reset 후에는 새 세션으로 명시적으로 다시 연결한다. 보존된 옛 결과의 키는 바꾸지 않는다. writer/poll은 콜백·전역 버스·게임플레이 재진입 없이 값만 복사한다. `Tools/deviant/Experiments/Combat Command Outcomes` 창은 Game/Lab 전투를 명시적으로 연결해 최근 128개와 유실을 표시하며 입력·시간 제어·저장/재생을 수행하지 않는다. `CombatCommandsTests`, `CombatCommandsIntegrationTests`, `CombatAICommandsIntegrationTests`, `CombatCommandOutcomesWindowTests`는 이 경계와 실제 호출부를 검사한다.

## 데이터와 구현 범위

`build_melee_design.py`는 `data/combat/melee_weapon_design.json`과 `hit_sources`의 행동 원본을 공통 `melee_source.py`로 읽고 검증해 [검·둔기 설계 문서](../../../design/equipment/weapons/forms/melee.md)를 생성한다. JSON은 편집 원본이며 생성기가 덮어쓰지 않는다. `export_unity_catalog.py`는 `melee_weapon_design.json` 및 참조 행동 원본 + `ranged_basic_design.json` + `launcher_skills.json`을 `Assets/Deviant/Combat/Resources/MeleeCatalog.json`으로 내보낸다. 런타임 `CombatArena`가 카탈로그를 읽는다. 수치는 원본 JSON을 수정하고 문서·Unity 카탈로그를 재생성한다. 상세는 [데이터 파이프라인](../../data/data-pipeline.md)을 따른다.

| 파일·영역 | 역할 |
|---|---|
| Runtime/CombatCore.cs | 피해·쿨감·충전 부채·중복 타격·점프 예약·안전 접지 규칙 |
| Runtime/Application/ActorMotionResolver.cs · Contracts/ActorMotion.cs | 접지 뒤 속도/제어의 불변 관측·순수 수평 우선순위/수직 적분·충돌 전 이동 제안 |
| Runtime/Application/ActorMotorExecution.cs · Contracts/ActorMotorExecution.cs | 만료/접지·기존 모션/접촉 해석·X→Y 적용/동기화 순서와 live ref Native 호스트 |
| Runtime/Application/ActorFacingExecution.cs · Contracts/ActorFacing.cs | 0/no-op·부호·벽 중심 보정/반전 순서와 scalar live ref Native 호스트 |
| Runtime/Application/ActorVitalsState.cs · Contracts/ActorVitals.cs | 액터별 HP/보호막/방어·수신 횟수 원본과 불변 조회·피해 수치 반영/만료/리셋 |
| Runtime/Application/ActorStatusState.cs · Contracts/ActorStatuses.cs | 절대시각 기반 기절/속박/경직/입력 잠금/밀림/은신 원본과 단계 명령·불변 조회 |
| Runtime/Application/ActorDamageExecution.cs · Contracts/ActorDamageExecution.cs | 현재 자격·HP/제어·가상 취소/기록/사망 순서의 순수 실행과 live ref Native 호스트 |
| Runtime/Application/ActorContactResolver.cs · Contracts/ActorSurfaceQuery.cs | 불변 몸/질의/접촉 값과 축 이동 제한·접지 해석 |
| Runtime/Application/ActorPlacementResolver.cs · Contracts/ActorPlacementQuery.cs | 세 지점 안전 접지·정확한 bounds 막힘·텔레포트 제한/끝 위치 해석 |
| Runtime/Unity/UnityActorSurfaceQuery.cs | 기존 Native BoxCast/Raycast/OverlapBox의 ArenaSurface 기하/현재 무시 사실 변환 |
| Runtime/Application/WorldHitQueryRules.cs · Contracts/WorldHitQuery.cs | 최근접 자격/거리·기존 근사 동률과 노출 교집합/순서점의 순수 해석 |
| Runtime/Application/ActorPlatformState.cs · Contracts/ActorPlatforms.cs | 액터별 일방향 발판 무시 핸들/종료시각 원본·내려가기/복구/리셋 |
| Runtime/Unity/UnityActorPlatforms.cs | 액터 범위 런타임 핸들과 현재 콜라이더/하단 기하의 바인딩·미사용 참조 반환 |
| Runtime/CombatActor.cs | 접촉/내려가기 결과 적용·몸/피격 분리·피격 자격/제어 단계/사망 콜백 |
| Runtime/Application/ActorReturnPlacement.cs · Contracts/ActorPose.cs | 대상 인접 자세·목적지/몸 전체 경로·원점 주변 경계/교점 후보와 결정 순서 |
| Runtime/Application/ActorReturnSchedule.cs · Contracts/ActorReturnReservation.cs | 무기 인스턴스별 불변 예약/원점/종료시각 원본·기대 ID와 시각을 확인한 일회 소비 |
| Runtime/CombatPlayerReturn.cs | 예약 ID→원래 대상 참조·현재 대상/기하 자격·앞 대체·실제 취소/위치 적용 |
| Runtime/Application/PlayerDashExecution.cs · Contracts/PlayerDash.cs | 기본 대시/점프 취소 이월의 시각·방향 원본과 불변 조회 |
| Runtime/Application/PlayerJumpExecution.cs · Contracts/PlayerJump.cs | 두 점프 사용량·착지 입력 예약 원본과 불변 조회 |
| Runtime/Application/PlayerTickExecution.cs · Contracts/PlayerTick.cs · Runtime/Unity/CombatPlayer.Tick.cs | 기존 플레이어 tick/현재 행동·이동·착지·복귀/낙사 순서의 순수 조율과 이전 접지 단일 값 원본; live ref Native 기하/기록/열거 연결 |
| Runtime/Application/EnemyTickExecution.cs · Contracts/EnemyTick.cs · Runtime/Unity/TestEnemy.Tick.cs | 기존 임시 적 tick/근접 후보·claim·피해·표시 순수 조율과 live ref Native 연결; 시계/AI·공격·제어·모터 추가 원본 없음 |
| Runtime/Application/RangedRecoilExecution.cs · Contracts/RangedRecoil.cs | 공중 원거리 발동 반동의 시작/실효 지속시간·방향·벽/착지 종료 원본 |
| Runtime/Application/PlayerComboExecution.cs · Contracts/PlayerCombo.cs | 플레이어별 기본 타수·포함 경계 연계 마감과 현재 형태 기반 다음 타수 선택 |
| Runtime/CombatPlayer.cs 및 partial 파일 | 입력·점프/대시·장착2슬롯·공격 예약·낙사/수리검 복귀 |
| Runtime/CombatCast.cs 및 partial 파일 | 타격 시간창·돌진/순간이동·차지·반사·독립 효과 |
| Definitions/Runtime/Combat/SkillExecutionDefinition.cs | Cast 시작 입력의 불변 hit/box·이동/효과/방어·발사/미사일 그래프; 가변 DTO는 Create 경계에서만 사용 |
| Runtime/Application/CastLifecycleExecution.cs · Contracts/CastLifecycle.cs | 일반 기술 활성화/실패/취소·완료/독립 잔존 원본과 live ref Native 수명 호스트 |
| Runtime/Application/CastTickExecution.cs · Contracts/CastTick.cs · Runtime/Unity/CombatCast.Tick.cs | 상태 없는 Cast tick의 생존/디버그·전용 실행 분기·검기→타격→연막→돌진 종료 추가타→평가 commit→기존 수명 순서와 live ref Native Bounds/소유자 연결 |
| Runtime/Application/CastMotionExecution.cs · Contracts/CastMotion.cs · Runtime/Unity/CombatCast.Motion.cs | 기존 수평 이동·수직 고정·무적 우선순위, fallback 기본 이동 소비시각과 live ref Native 위임 |
| Runtime/Application/CastActivationExecution.cs · Contracts/CastActivation.cs · Runtime/Unity/CombatCast.Activation.cs | 일반 기술 발동 자격·설치·자원/claim·종류별 생성/반동·효과 순수 조율과 live ref Native 포트 |
| Runtime/Application/SkillHitTrackExecution.cs · Contracts/SkillHitTrack.cs | 일반 기술 마지막 평가시각·타격 시간창/유한 앵커 순서와 live ref Native 타격 호스트 |
| Runtime/Application/CastSweepExecution.cs · Contracts/CastSweep.cs | 표본 스윕 기하·제외/노출 후보 수집·기존 정렬/nearest 적용 순서의 순수 실행 |
| Runtime/Application/ChargedMeleeExecution.cs · Contracts/ChargedMeleeExecution.cs | 기존 차지 정책을 읽는 활성화·타격창/claim·실패 회복·완료/취소 원본과 Native 포트 |
| Runtime/Application/ForcedDashExecution.cs · Contracts/ForcedDash.cs | 기존 다섯 형태 특수 돌진의 이동 종료·벽 타격 cutoff·회복 제안과 보스 접촉 구분 |
| Runtime/Application/MissileExecution.cs · Contracts/MissileExecution.cs | 미사일 비행/실제 거리·접촉/수동 종료·백점프의 순수 원본과 ref Native 호스트 |
| Runtime/Application/ApproachRecastExecution.cs · Contracts/ApproachRecast.cs | 채찍 표식 재입력 마감/소비·접근 모드/거리/실효 길이의 원본과 ref Native 호스트 |
| Runtime/Application/CastTeleportExecution.cs · Contracts/CastTeleport.cs | 비대시/비재입력 이동의 시간 경계·일회 claim/분기와 Native 목적지/스윕 연결 |
| Runtime/Application/BombReleaseExecution.cs · Contracts/BombReleaseExecution.cs | 폭탄 행동의 authored hit 방출 커서·claim-before-spawn 원본과 ref Native 호스트 |
| Runtime/Application/SmokeEffectExecution.cs · Contracts/SmokeEffect.cs | 연막 종료/공격 후 재은신 차단 원본; Actor의 현재 은신 마감과 별도 |
| Runtime/Application/SkillWaveExecution.cs · Contracts/SkillWaveExecution.cs | 일반 검기의 현재/이전 좌표·누적 거리·종료 원본과 ref Native 기하/피해/표현 호스트 |
| Runtime/Application/CastHitResponseExecution.cs · Contracts/CastHitResponse.cs | 수신 결과 표식/주 대상·밀기 ID/종료 추가타 claim과 live ref Native 응답 순서 |
| Runtime/Application/CastParryExecution.cs · Contracts/CastParry.cs | 방패 시간/전방 자격·근접 응답/기록 순서와 live ref Native 포트 |
| Runtime/PlayerProjectile.cs · BombProjectile.cs · BombBurst.cs | 원거리 탄·폭발·시한 신관·3연발 |
| Runtime/Application/PlayerProjectileExecution.cs · Contracts/PlayerProjectileExecution.cs | 플레이어 탄 거리/나이·일회/적별 간격·종료 원본과 Native phase 포트 |
| Runtime/Application/PlayerProjectileHitExecution.cs · Contracts/PlayerProjectileHit.cs · Runtime/Unity/PlayerProjectile.HitResponse.cs | 플레이어 탄 접촉/폭발의 상태 없는 claim·수신/복귀·관통/제거 순서와 live ref Native 기하/피해 포트 |
| Runtime/Application/PlayerProjectileSweepExecution.cs · Contracts/PlayerProjectileSweep.cs · Runtime/Unity/PlayerProjectile.Sweep.cs | 플레이어 탄 현재 자격/수리검 겹침·cast 순위와 시작 겹침/콜백 뒤 이동/접촉 나이·최종 위치의 순수 조율; per-call Native 목록·참조/Vector2 연결 |
| Runtime/Application/LaserHitExecution.cs · Contracts/LaserHit.cs · Runtime/Unity/CombatArena.Laser.cs | 레이저의 기존 순서 고체 길이 제한·대상 자격/차폐 전 claim·기본 피해·총구 뒤 표시/기록의 순수 조율과 per-call live ref Native Physics/Vector2·액터/정의/표시 연결 |
| Definitions/Runtime/Combat/RangedProjectileDefinition.cs | 생성 뒤 원거리 탄이 읽는 불변 거리/기하·관통/경직·반복 간격/복귀 설정 |
| Runtime/Application/TimedBombExecution.cs · Contracts/TimedBomb.cs | 시한폭탄의 비행/착지 신관·종료 단일 원본과 불변 조회·동기 Native 포트 |
| Runtime/Application/TimedBombSweepExecution.cs · Contracts/TimedBombSweep.cs · Runtime/Unity/BombProjectile.TimedSweep.cs | 시한폭탄 X→Y 축 순서·벽 face clamp/천장/일방향·마지막 동률/부분 착지와 현재 Native CircleCast/Bounds/Vector2 포트 |
| Runtime/Application/ContactBombExecution.cs · Contracts/IContactBombHost.cs | 접촉 폭탄/파편의 절대 만료·마지막 부분 비행 스텝과 기존 단일 종료 원본 연결 |
| Runtime/Application/ContactBombSweepExecution.cs · Contracts/ContactBombSweep.cs · Runtime/Unity/BombProjectile.Sweep.cs | 접촉 폭탄의 고체/일방향/적 자격·시작 겹침·기존 순서 최근접/clamp·이동/폭발 순수 조율과 live Native 기하/질의/Vector2 포트 |
| Runtime/Application/BombBlastExecution.cs · Contracts/BombBlast.cs · Runtime/Unity/BombProjectile.Blast.cs | 종료 claim 뒤 후보 자격/중복·반경/차폐·기본 피해/폭발·저작 파편 생성/즉시 스윕/제거 순수 조율과 현재 Native 참조/기하/생성 포트 |
| Runtime/Application/RangedLaunchExecution.cs · Contracts/RangedLaunch.cs · Runtime/Unity/CombatArena.RangedLaunch.cs | 원거리 레이저 위임/다섯 수리검 총구 고체·폭발/원본 무기·저작 각도 생성/즉시 스윕과 폭탄 생성/파편 배율/현재 폭 총구 스윕·실제 개체 반환의 순수 조율 및 Native 기하/질의/생성 포트 |
| Definitions/Runtime/Combat/BombProjectileDefinition.cs | 생성 뒤 폭탄의 불변 기하/중력·비행/신관/폭발·한 번 캡처한 파편/자식 정의 |
| Runtime/CombatArena.cs | 공간·장비 공급·표시·전투 로그·리셋 |
| Runtime/CharacterPresenter.cs · CharacterAnimationV2.cs | 무기별 모션 선택·공격 접촉 시각 동기화·시각 이펙트 |
| Runtime/Application/EnemyIntentResolver.cs · Contracts/EnemyIntent.cs | 임시3종의 불변 관측/공격 시작·추적 의도 판단 |
| Runtime/Application/EnemyAttackExecution.cs · Contracts/EnemyAttack.cs | 임시3종의 공격 상태/시각·원거리 발사/타격 중복 원본과 불변 조회 |
| Contracts/CombatCommands.cs · Runtime/Application/CombatCommandOutcomes.cs · Runtime/Unity/CombatArena.Commands.cs | 세션/액터 명령 경계·실제 실행 키/결과와 순번/유실이 있는 bounded polling; 기존 입력/AI 정책·자원 소유권 유지 |
| Runtime/Unity/CombatPlayer.Commands.cs · CombatCast.Commands.cs · TestEnemy.Commands.cs | 예약/점프 버퍼·실제 Native 시작/취소/실패/완료에 명령 원점과 실행 GUID 연결 |
| Authoring/Editor/CombatCommandOutcomesWindow.cs | Game/Lab 공통 읽기 전용 명령 결과 관측·명시적 세션 연결·최근 128개/유실 표시 |
| Runtime/Application/EnemyProjectileExecution.cs · Contracts/EnemyProjectileExecution.cs | 적 탄의 만료/피해·반사·접촉 claim·종료 원본과 Native 호스트 포트 |
| Runtime/Application/EnemyProjectileSweepExecution.cs · Contracts/EnemyProjectileSweep.cs · Runtime/Unity/ArenaProjectile.Sweep.cs | 적 탄의 상태 없는 표본/중복 수집·현재 접촉 자격/차폐·기존 접촉/이동/벽 종료 순서와 Native 속력/정규화·후보 List.Sort/Collider 연결 |
| Runtime/TestEnemy.cs | 의도·순수 공격 실행의 대상/지형/모터·Native 타격/피해/탄 연결·반사 가능한 적 탄 |
| Editor/CombatArenaBuilder.cs | 테스트 씬 생성·Windows 빌드 진입점 |
| Tests/Editor/ | `Deviant.Combat.Tests`, EditMode 실행 중 Play Mode 진입 |

### Lab 실행 제어

Tools > deviant > Experiments > Lab Execution은 초기화한 Lab 전투의 기존 단일 ManualSimulation lease만 얻어 Run/Pause/실제 한 tick/새 epoch Reset을 제공한다. 본 게임과 다른 수동 도구의 lease를 빼앗지 않는다. SingleTick은 자기 정지만 잠시 해제하고 finally에 복구하며 외부 정지는 우회하지 않는다. 입력은 공통 ingress에 먼저 접수하고 StepPending은 기존 표본 순서/영수증을 교체하지 않고 소비한다. 외부 제한·정지·리셋·연결 해제 뒤 눌림이 남지 않는다.

로컬 슬로모션은 Editor 비스케일 경과에 0 초과 1 이하 배율을 곱해 기존 fixedDeltaTime tick 빈도만 조절한다. dt·timeScale·기본 공속은 바꾸지 않고 callback당 최대 128 tick 뒤 backlog를 버리지 않고 표시/재개한다. 지표는 명시적으로 바인딩한 ICombatReadModel 값이다. 시각 출력은 해당 ViewCamera.cullingMask만 0으로 만들며 Renderer/Cue 수명·피해·카메라 추적·소리는 계속 진행한다. 복구는 자기 마지막 쓰기가 유지될 때만 수행한다. 창/Play/assembly/대상 비활성은 자기 소유권만 반환하고 tick/reset 중 비활성은 현재 호출 finally까지 실제 lease 반환을 지연해 진입한 결과를 보존한다.


`ActorMotionResolver`는 일반 입력/밀림/입력 잠금/기절/속박/경직·강제 이동·사망/분신의 기존 수평 우선순위와 접지/수직 고정·상승/하강 중력·정점 분할의 float 계산 순서를 보존한다. 중력 수치는 기존 `CombatRules`에서 받아 사용한다. 실제 접지/천장/벽/발판 질의와 X→Y 이동, 접촉 뒤 수직속도 0/접지 반영은 같은 `CombatActor` 모터를 사용한다. 새 가속·점프/대시·피격 규칙이나 물리 주기를 추가하지 않는다.

체력/보호막/방어는 `ActorVitalsState` 한 원본에서 읽는다. 기존 `health`/`maxHealth`/`shield`/`guardUntil`/`guardReduction` 접근은 같은 상태에 전달하고 `Vitals`는 값 스냅샷이다. 피해 수치는 무적/패링 검사를 통과한 뒤 감소→흡수→HP/수신 횟수에 반영하며 0 피해·완전 흡수도 기존처럼 유효 수신이다. 이후 일반 적 경직/제어/취소·플레이어 밀림/잠금·사망 콜백은 기존 실행부가 같은 순서로 처리한다. 보호막 만료는 같은 물리 tick 경계에서 명시적으로 호출한다. 저수준 프로필 수치 쓰기는 유한한 값을 받으며 회복/장비/저장 자격 정책을 대신하지 않는다. 현재 액터는 조립 시 생성하며 이 수치들은 작성 씬/프리팹의 직렬화 필드로 사용하지 않는다.

제어/은신의 수치 원본은 `ActorStatusState`다. `Statuses`는 불변 시점 조회이며 기존 만료시각/밀림 속도 접근은 같은 소유자를 사용한다. 기절/속박은 최대 종료시각으로 갱신하고 기본 경직/플레이어 잠금은 기존 대입 의미를 유지한다. 유효 플레이어 피격은 입력 잠금과 가능한 밀림을 반영하며 기존 밀림/강제 이동 중 새 밀림을 나중에 예약하지 않는다. 기본 경직→취소 관측→속박→기절/취소 관측→밀림의 순서를 유지해 앞 콜백의 상태 편집을 다음 단계가 다시 읽는다. 일반/보스/분신 자격은 기존 호출부에서 결정한다. 리셋·낙사 복귀·은신 해제의 서로 다른 범위도 명령으로 구분하고 효과/콜라이더 수명은 이 상태가 소유하지 않는다.

낙사 복귀는 T-3 이하 표본을 최신부터 검사하고 현재 막힘·전체 바닥 지지·평탄도·위험 겹침이 깨진 표본은 건너뛰어 이전의 유효 표본을 찾는다. 가상 후보 몸으로 질의하므로 검사 중 실제 위치/방향을 바꾸지 않는다. 이력은 같은 액터 리셋까지 보존하고 리셋에서 비운다. 최대 HP 10% 피해와 기존 복귀 보호/점프 초기화·대시 회복 유지 규칙은 그대로다. 유효 이력이 없으면 기존 검증된 테스트 시작 위치를 쓰며 모든 시작점까지 무효인 실제 맵의 복구 정책은 후속이다.

축 이동/접지는 `ActorContactResolver`가 `IActorSurfaceQuery`의 현재 접촉 값으로 계산한다. Unity 어댑터는 native 결과 순서를 유지하고 ArenaSurface가 아닌 콜라이더를 제외하며 액터별 현재 무시 사실을 전달한다. 질의 결과는 읽기 전용 값으로 이전 관측에 살아 있는 콜라이더 참조를 남기지 않는다. 기존 축별 여유값·단방향 발판/법선/발 높이·접지 상승속도 조건을 보존하고 `CombatActor`가 실제 X→Y 이동/벽·천장·접지 반응을 적용한다. 안전 접지·막힘·텔레포트는 `ActorPlacementResolver`가 같은 어댑터의 `IActorPlacementQuery`로 해석한다. 안전 접지는 기존 접지 자격 뒤 몸 전체 세 ray의 첫 유효 높이/평탄도를 확인하며 새 바닥이나 이력을 만들지 않는다. 막힘은 Native 접촉 여유만으로 바닥 접촉을 거부하지 않고 기존 발 높이와 XYZ bounds 교차를 다시 확인한다. 텔레포트는 기존 시작 겹침/단방향 제외와 거리 제한·끝 위치 거부를 유지하며 무시 발판 사실을 고체의 통과 허가로 사용하지 않는다. 실제 위치·속도/제어·복귀 이력과 Transform 동기화는 기존 액터/플레이어가 소유한다. 몸/지형 데이터를 JSON으로 복제하지 않는다. 발판 무시와 내려가기는 `ActorPlatformState`가 소유한다. `Platforms`는 액터 범위의 불변 무시 핸들/종료시각 조회이며 콘텐츠/저장/재생 식별자가 아니다. Unity 바인딩은 Native 순서의 일방향 접촉과 핸들별 현재 생존/하단 기하만 제공하고 실제 무시 여부는 같은 상태를 조회한다. 내려가기는 현재 접지·상향 법선 뒤 고체 전용 축 해석으로 아래 0.08unit 전체가 가능한지 검사한다. 거부하면 위치/속도·무시를 바꾸지 않으며 성공하면 최소 종료시각과 수직 -2를 반영한다. 복구는 0.25초 이상과 몸 상단이 현재 발판 하단보다 0.02unit 아래라는 두 조건을 함께 요구한다. 파괴는 다음 상태 갱신에서 제거하고 비활성 콜라이더는 현재 Native bounds를 보는 살아 있는 객체로 취급한다. 리셋은 무시만 비우며 새 바인딩에 옛 핸들을 재사용하지 않는다. 수리검의 도착/복귀 기하는 `ActorReturnPlacement`가 공통 현재 `BodyFrame`과 `ActorPose`, 같은 `IActorPlacementQuery`로 해석한다. 대상 몸 크기/중심·목적 방향 오프셋·목적지 겹침 뒤 전체 몸 스윕을 사용하며 일방향/초기 접촉/끝 여유를 기존처럼 처리한다. 원점이 가능하면 먼저 선택하고, 아니면 현재 고체 bounds의 경계/교점에서 1unit 안 후보를 거리/X/Y 순서로 검사한다. 예약의 무기 인스턴스·원래 대상 ActorId·원점/방향·도착 기준 종료시각/간격은 `ActorReturnSchedule`의 불변 `ActorReturnReservation`이 소유한다. 같은 무기의 두 뷰는 Equipment의 같은 `InstanceId`를 사용해 중복 예약을 허용하지 않는다. 서로 다른 무기는 별도 예약을 유지하고 실제 갱신에서 기존 삽입 순서로 기대 예약/무기 ID와 시각을 검사한 뒤 시도 전에 소비한다. 조회/남은 시각은 소비·갱신하지 않는다. Unity 호출부는 대상 참조만 예약 ID에 연결하고 현재 인스턴스/생존 자격·대상 앞 대체와 실패 시 현재 위치 유지/예약 종료·공격/대시 취소/독립 효과 유지를 수행한다. 사망/리셋은 예약과 대상 연결을 함께 비운다. 현재 런타임 예약/무기/Actor ID는 영구 콘텐츠/저장 ID가 아니며 별도 복귀 시계·피해·진행 정책을 만들지 않는다. 기하 해석기는 새 복귀 정책/바닥 판정/세계 상태를 소유하지 않는다. 전체 Actor/판정·지형 질의 이행과 실제 맵 전체의 안전 검증은 후속이다.

지형 조회는 실행 중 Default 레이어의 `ArenaSurface`를 예약 레이어 8(`ArenaTerrain`)로 옮겨 일반 액터 콜라이더를 후보에서 제외한다. 명시적으로 작성한 다른 레이어와 Ignore Raycast 제외, 전역 trigger 조회 설정은 유지한다. 이동·접지·배치 어댑터와 시야·지지면 검사는 Native 버퍼를 재사용하며 결과 수에 고정 상한을 두지 않는다. 어댑터는 매번 현재 물리를 조회한 뒤 값이 같은 불변 스냅샷만 재사용한다. 이전 결과를 보관해도 다음 질의의 이동·무시 변경이 이전 값에 반영되지 않는다. 좌표·콜라이더 offset이 이미 같으면 같은 값을 다시 쓰지 않으며, 이동 이후의 기존 Transform 동기화 시점과 X→Y 판정 순서는 유지한다.

일반 절차 생성 런은 `CombatContentLoader.LoadInFrames`로 캐릭터 프레임을 나눠 등록하고 `CombatArena.InitializeInFrames`로 전투 초기화·몬스터 묶음 생성을 분리한다. 무기 그림은 장착한 형태만 읽는다([전투 표현](../../presentation/visuals/combat-presentation.md)). 캐시된 미디어에는 배치마다 고정 대기를 추가하지 않으며 매체 로드 예산을 넘겼을 때 다음 프레임으로 넘긴다. 챕터 구성도 자신이 소유한 정지·입력 제한을 유지해 월드의 후속 장치까지 준비한 뒤 전투를 허용한다. 준비 중인 런타임은 전투·상호작용 입력과 시뮬레이션을 잠그며 구성 완료 후 `Initialized`를 발행한다. 인벤토리 바인딩도 구성 완료까지 기다린다. 중간 취소는 자신이 소유한 정지·입력 제한만 해제하며 미완성 런타임의 자동·수동 전투를 허용하지 않는다. 작은 시험장·기존 마을의 동기 `Initialize`는 같은 생성 순서를 끝까지 실행한다. 개별 리소스 로드와 각 초기화 단계 안의 작업은 동기적이므로 프레임 분산이 전체 로딩 시간 단축이나 대상 Player의 모든 시작 프레임 예산 충족을 보장하지 않는다.

절차 생성 챕터의 월드 구성은 로딩 화면의 달리는 주인공이 멈추지 않도록 여러 프레임에 나눠 실행한다. `FrameSlice`는 한 프레임의 구성 시간이 20ms를 넘으면 다음 단계 전에 프레임을 넘기며, 한 단계는 중간에 끊지 않는다. 1챕터 `ForestCombatWorld`(`ComposesInFrames`)는 맵·전투·플레이어·카메라가 준비되면 `Prepared`, 전체 지도·시작 설계도·보스·이동 연출·체크포인트 등 `Start`의 장치를 모두 붙이면 `Composed`, 같은 오브젝트에서 `IStagedWorldPresentation`을 구현한 그림 구성(`VillagePresentation`·`Stage1CombatArt`)이 모두 끝나면 `Ready`가 된다. 이 동안 월드 자체의 구성 범위가 시뮬레이션·전투 입력을 계속 잠그며, 장치 초기화가 확인하는 준비 조건은 `Prepared`를 사용한다. 그림 구성은 `Composed` 뒤에 시작하고 마을식 지형 타일과 승인 전투 지형은 맵 한 줄씩 만든다. 그림 구성이 예외로 중단되면 오류를 기록하고 `Settled`로 보고해 로딩 화면이 무한히 남지 않게 한다. 마을·시험장 등 나머지 월드는 이전처럼 `Prepared`와 `Ready`를 같은 시점에 켜고 그림을 한 번에 만든다. 2챕터 `Chapter2World.Start`도 자신의 정지·입력 제한 아래 레이아웃 생성·하수로 계획·캐릭터 매체·전투 초기화·적·보스·이동 연출·출구 사이를 같은 기준으로 나누며 `Ready` 시점과 그 뒤의 구성 순서는 바꾸지 않는다. 인벤토리 바인딩은 같은 장면의 1·2챕터 월드가 `Ready`가 된 뒤 화면을 연결한다. 알려진 제한: 맵·레이아웃 생성, 시계 처형인·이동 연출 초기화, 전투 GUI 바인딩은 각각 한 단계로 실행되므로 그 프레임에서는 로딩 애니메이션이 여전히 0.2–1초가량 멈출 수 있다.

시한폭탄은 기존 비행 제한/착지 신관과 부분 스텝을 `TimedBombExecution`으로 해석한다. 착지 시각은 실제 하향 스윕의 접촉 거리 비율이며 착지가 비행 만료 직전이어도 전체 신관을 유지한다. 축 스윕/벽·천장·일방향/착지 응답 순서는 상태 없는 `TimedBombSweepExecution`이 해석하며 Native 비행 적분·Vector2/기하 질의·폭발 피해/파편 생성은 기존 Bomb 어댑터다. 사망·테스트 리셋은 상태를 먼저 종료해 지연 파괴 전 남은 참조도 실행할 수 없게 한다. 취소/교체 후 이미 생성한 폭탄은 기존처럼 유지하며 원본 피해/충전·일반 접촉탄 동작을 바꾸지 않는다.

플레이어 몸/피격은 size=(0.9375,2), offset=(±0.1875,1)이며 적끼리·플레이어와 적은 통과하고 접촉 피해는 없다. 검/채찍/레이피어/단검은 [확정 패키지](../../../../resources/README.md)의 모션·이펙트를 사용한다. 접촉 시각 동기화, 점프·착지 전환, 미지원 형태의 대체 모션과 이펙트 수명은 [표현 계층](../../presentation/visuals/combat-presentation.md)을 따른다. 다른 형태·원거리의 임시 시각물과 최종 아트 제작을 구분한다.

근접 공격의 일반 피격 밀림은 `CombatArena.MeleePush`가 명중 시 저장한 거리 상한으로 제한한다. 사용 가능한 근접 2개는 짧은 기본 사거리, 근접+원거리는 근접의 기본 사거리를 사용하며 대상 피격박스가 사거리 안에 `basicPushOverlap`만큼 남도록 한다. `ActorDamageExecution`은 일반·손맛 시험 밀림에만 이 상한을 적용하고, 0 거리도 이전 밀림을 덮어써 경계 밖으로 계속 밀리지 않게 한다. 피해·경직과 전용 밀기·원거리 밀림은 각 기존 경로를 사용한다.

전방 근접 기본·특수는 몸 뒤 경계에서 0.25unit까지 포함하는 카탈로그 박스를 사용한다. 오른쪽 기준 root x=-0.53125부터 기존 앞쪽 끝까지이며, 왼쪽은 대칭이다. 일반·직접·차지·돌진 및 저장 위치 잔상은 같은 원본 형상을 사용하고, F2도 실제 판정을 표시한다.

화면 최근10줄과 Console `[CombatV2]` 로그에서 발동·실패·피해/잔여HP·교체·패링·낙사·리셋을 확인한다. 짧은 활성창은 시간 구간 교차로 검사하고 같은 타격/대상 중복을 막는다.

장비 패널은 설계 장비를 직접 공급하는 **테스트 도구**다. 실물 재고·5칸 가방·획득 교환 UI·전용 잉크 콘텐츠·정식 보상 경제를 구현한 것이 아니다. 저장 파일·재화·보상은 쓰지 않는다. 테스트 씬 연결과 전체 게임 통합은 별도 상태다. 실제 실행 결과는 Git 커밋 본문에서 확인한다.

색상 잉크 런은 `GrowthData.Validate`에서 파츠의 효과 종류·발동 조합·적용 대상·조건과 6단계 데이터 구조를 검사하고 지원하지 않는 항목은 파츠 ID를 포함한 오류로 거부한다. 초록 02의 보호막 합계 상한은 현재 성장 원본 `weapon_growth_mvp_tuning_draft.json`의 `proposedSafety.shieldCap`(50)을 사용한다. 기존 스킬 보호막을 포함해 상한을 계산하며 구형 훈련 잉크의 별도 상한은 해당 경로에 남아 있다. 옛 정비소 IMGUI의 `RunStash` 전용 강화 버튼 두 개는 표시하지 않고, 현재 강화는 정비소 수첩의 캐릭터 강화·팔레트 슬롯 강화 경로로 수행한다.
