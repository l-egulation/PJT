# 타격감 개선 기술 설계

[분야 개요](README.md)

## 상태와 기준

보유 에셋 `VFX - Impact and Hit - Vol 2`를 사용하는 기술 계약이다. [개선 계획](combat-hit-feedback-plan.md)의 작업 범위와 함께 읽는다. 아래 계약의 공통 명중·프리팹 Cue·시간 요청·카메라 킥·URP 2D 왜곡·로컬 잔해 물리를 구현했으며, 제안 포트 이름과 실제 공개 API는 다를 수 있다. **전체 제품 아트 조합과 체감 튜닝은 완료 상태가 아니다.**

현재 바인딩은 `HitFeedbackBridge`가 담당한다. `CombatHitOrigin.Stamp`는 행동 시작의 무기·행동을 보존하고 `CombatAcceptedHit.Contact`는 수신 직전의 위치·입사 방향을 보존한다. `HitFeedbackSession`은 명중별 Cue 스코프 수명과 중복·용량·만료를 처리한다. 별도 장식 레이어별 admission 대신 원본의 복합 파티클 프리팹을 한 단위로 제한한다.

제품 연결 범위는 공통 `CombatContentLoader`를 사용하는 Game/Lab과 기존 숲·들판 및 현재 마을 몬스터 표현기다. 이미지 없는 구조 검사는 테스트가 명시 주입한다. 실제 death clip 완료 포트와 승인 idle Sprite의 분할 캐시를 사용하며, 다른 종의 그림으로 대체하지 않는다. 마을의 처치 실루엣은 몸체 표시를 소유하되 native death clip의 시계를 중단하지 않아 물리 파편 완료와 공존한다. 지형·배경 제작과 개별 아트 품질 승인은 별도 범위다.

현재 구형 숲의 boar/eye 데이터에는 death clip이 없다. 이 두 데이터는 기존 페이드를 보존하고 `ReportMissingDeathClip` 진단을 남기며 파편을 생성하지 않는다. 들판 6종의 실제 death clip 완료와는 별개 범위다. 리셋 시 이전 출력 루트는 즉시 비활성화하고 조회 이름을 폐기해 지연 Destroy 중에도 새 세대의 출력 루트로 조회되지 않게 한다. 부모 비활성화 콜백 중에는 Transform 재부모화를 하지 않는다.

- 전투 결과의 기준: [전투 공통 규칙](../../../design/combat/rules.md)
- 현재 구현: [전투 표현](combat-presentation.md), [HUD 시간 제어](../ui/hud.md), [카메라](../../gameplay/world/forest-prototype.md#카메라)
- 모듈 경계: [목표 구조](../../architecture/project-structure.md), [표현 구조](../../architecture/presentation-narrative-ui.md)
- 검증 환경: Unity **6000.6.5f1**, 원본 패키지 버전, 현재 URP 17.6.0 / 2D Renderer

기술 설계의 v1 선택과 제품 연출 확정을 구분한다. 강타 자격, 구체적인 프리팹, 강도·용량은 승인된 콘텐츠·시험값을 명시적으로 주입하며 여기서 임의 확정하지 않는다. 사망 슬로우는 플레이어 본인 사망으로 한정한다. 기존 전투 설계 체크리스트의 완료율을 이 기술 설계로 올리지 않는다.

## 1. v1 설계 선택

| ID | 설계 선택 | 이유·제약 |
|---|---|---|
| D1 | 수락된 명중 사실에 접촉 문맥을 함께 보존 | 렌더 시점의 현재 무기·위치로 과거 타격을 추정하지 않음 |
| D2 | 전투 세션당 명중 소비자 하나 | 중복·누락 진단·효과 수 제한을 한곳에서 처리 |
| D3 | 명중별 짧은 Cue 스코프와 캡처된 출력 바인딩 | 기존 Cue 수명을 재사용하면서 완료 핸들의 무한 누적을 방지 |
| D4 | v1 효과 카탈로그는 세션 시작·리셋에만 게시 | 비행 중 투사체의 효과가 저작 변경으로 바뀌는 문제와 버전 보관 비용을 줄임 |
| D5 | 월드 시간·강조 시간·UI 시간을 명시적으로 구분 | 물리·모션 동기화와 짧은 화면 강조의 실제 시간 길이를 함께 제어 |
| D6 | 슬로우는 소유자별 요청, 정지 우선, 가장 느린 유효 배율 선택 | 곱셈 누적·과거 배율 복원 오류를 피함. 제품 적용 전 합성 정책 검증 필요 |
| D7 | 잔해는 전용 로컬 PhysicsScene2D에서 전투 step으로 진행 | 기본 물리 세계와 접촉하지 않으며 수동 시험장도 같은 시간 예산 사용. 지형 프록시 관리가 추가됨 |
| D8 | 치명 타격 후보와 실제 죽음 표현의 완료를 별도 계약으로 처리 | 피해 콜백 중 리셋·부활 및 종별 죽음 길이를 보존 |
| D9 | 카메라 킥은 기존 최종 위치 출력에만 가산 | 추적·구도·패럴랙스의 출력 소유권 유지 |
| D10 | 왜곡은 투명 월드까지 합성한 뒤 HUD 이전에 처리 | Opaque Texture의 Sprite 누락과 HUD 왜곡을 피함 |

D6의 수명·합성 방식과 D7의 물리 공간 선택은 이 기술 설계의 우선안이다. 최소 검증에서 성립하지 않으면 문제와 대안을 문서에 반영한 뒤 변경한다. 기존 구현에서 이미 지원되는 기능이라고 표시하지 않는다.

## 2. 모듈과 연결 계약

경로의 기준은 `unity/Assets/Deviant/`다. 불필요한 새 asmdef나 범용 이벤트 버스는 만들지 않는다.

| 구성 요소 | 제안 위치 | 책임 | 의존하지 않는 것 |
|---|---|---|---|
| `CombatAttackStamp`, `CombatHitContact` | `Combat/Contracts` | 시작 공격 식별값과 명중 시점 기하의 불변 값 | Unity 객체·Cue·VFX 파일 |
| 명중 기록 확장 | `Combat/Runtime` | 기존 수락 사실에 문맥 저장 | 렌더 성공 여부 |
| `HitFeedbackBridge` | `Integration/Unity` | cursor 소비·세션 검사·출력 바인딩·사망 사실 번역 | 피해 재계산 |
| `HitFeedbackSession` | `Presentation/Runtime` | recipe 선택·재생 스코프·예산·화면 요청 | CombatActor·Physics2D 전역 |
| `PrefabCueDefinition` / recipe 정의 | `Definitions/Runtime/Presentation` | 수명·배치·stable ID 검증 | 프리팹 인스턴스 |
| `PrefabCueFactory` / pool | `Presentation/Unity` | 파티클·셰이더 출력과 즉시 정리 | 게임 시간 배율 쓰기 |
| `FeedbackTimeScope` | `Control` | 소유자별 배율·만료·정지 합성 | 특정 무기·엘리트 판정 |
| `DebrisBake` / `DebrisWorld` | `Presentation/Editor`, `Presentation/Unity` | 분할 산출물·로컬 물리·표시·회수 | 게임 콜라이더·체력 변경 |
| `CameraKickMixer` / `WorldDistortionOutput` | `Presentation/Runtime`, `Presentation/Unity` | 오프셋·파동·최종 출력 | 공격 수락·사망 결정 |
| 조립 | `Game` / 시험장 | 단일 session 생성·입력 포트 주입·종료 | 전투 규칙 복제 |

### 제안 입력 포트

다음은 서명 수준의 설계이며 실제 C# 선언이나 현재 API가 아니다.

- `ConsumeHit(HitFeedbackInput)` → `Started / Suppressed / MissingBinding / InvalidContext / Expired`
- `OnSimulationStep(CompletedStep)` → 잔해·월드 표현 진행. 성공한 step의 세션·tick·시작/종료 시각·dt를 받음.
- `AdvanceAccent(realDelta, paused)` → 강조 수명만 진행. 전투 tick을 호출하지 않음.
- `BindTarget(actorHandle, targetOutput)` → 피격 점멸·죽음 출력 바인딩 등록. 반환 핸들로 자기 등록만 해제.
- `BeginDeath(DeathToken, optionalHitKey, sourceSpriteBinding)` / `CompleteDeath(token, pose)` / `CancelDeath(token)`
- `QueueFootSweep(session, tick, segment, motionKind)` → 완료된 연속 이동 구간만 전달.
- `BindWorldFrame(worldCamera, frameProvider)` → 왜곡용 월드 출력 연결. 소유한 자원만 반환.

명중 처리 결과는 표현 진단이다. 이를 전투의 명중 성공/실패로 다시 해석하지 않는다. 기존 `Receive` 반환값·피해 콜백 순서는 유지한다.

## 3. 명중 데이터와 위치 계약

### 3.1. 불변 데이터

기존 [CombatAcceptedHit](../../../../unity/Assets/Deviant/Combat/Contracts/CombatHitEvents.cs)의 순번·시각·Origin·Target·전후 체력·TargetPose는 그대로 권한을 가진다. 다음 값만 보강한다.

| 값 | 필드 의미 | 캡처 시점 |
|---|---|---|
| `CombatAttackStamp` | 원본 무기 instance/form ID, 행동 content ID·정의 버전, 기본/특수·변환 구분 | 실제 행동 시작. 독립 효과는 방출 때 원본 값을 상속 |
| `CombatHitContact` | point X/Y, incoming X/Y, `AnchorKind`, direction 유효성 | 각 대상의 피해 수신 직전 |
| `AnchorKind` | `SweepContact`, `OverlapRepresentative`, `BeamRepresentative`, `RadialRepresentative`, `TargetFallback`, `Missing` | 위치 산출 시 |
| `HitKey` | 기존 세션 전체 값 + long `EventSequence` | 수락 사실 게시 시 |

`HitId`, `HitIndex`, `EffectId`, 실행 ID, 반사 원인은 기존 `CombatHitOrigin`을 사용한다. 공격 내용을 표현 모듈이 매핑하므로 Combat 계약에 프리팹 ID·Material·카메라 강도를 넣지 않는다. Stamp 없는 직접 피해를 현재 장비로 채우지 않는다.

무기 정체성은 시작에 고정하지만 **입사 방향은 실제 명중 때** 캡처한다. 근접 기본 공격은 동작 중 방향 전환이 가능하므로 시작 시선으로 고정하면 안 된다. 반사탄은 원래 공격 정체성과 현재 진행 벡터·기존 반사 원인을 함께 보존한다.

### 3.2. 위치 산출

- **근접 겹침:** 이미 명중을 인정한 스윕 표본의 공격 박스와 피격 bounds 교집합에서 대표점을 고른다. `Exposed`가 채택한 첫 비차폐 표본을 받을 수 있으면 그 값을 사용한다. 검사 순서·단락 평가·대상 claim을 그대로 두고 채택 좌표만 기록한다.
- **투사체 스윕:** 기존 cast가 가진 접촉 정보를 보존한다. cast의 접점과 투사체 중심 위치를 구분한다. 시작부터 겹친 경우에는 피격 기하의 대표점으로 분류한다.
- **레이저:** 해당 대상과 빔의 겹침 기하에서 입사 쪽 대표점을 고른다. 벽 끝점이나 모든 대상의 중앙을 공통 접점으로 사용하지 않는다.
- **폭발:** 폭심에서 피격 영역으로 향하는 벡터와 기존 `ClosestPoint`를 재사용한다. 폭심이 피격 영역 내부면 방향 미지정을 허용하고 무방향 recipe를 사용한다.
- **직접 피해:** 같은 수신 시점에 기록한 대상 피격 중심을 `TargetFallback`으로 명시한다. 기하가 없거나 비유한 값이면 `Missing`으로 두고 진단한다.

대표점은 시각 앵커이며 새 충돌 판정이 아니다. 형상 없는 직접 기술을 지원하려고 존재하지 않는 스윕 접점을 꾸며내지 않는다. 기하 전달 실패가 원래 피해 계산을 중단하지 않도록 단순 값 생성과 진단 저장을 사용한다.

첫 버전의 위치는 명중 당시 월드에 고정한다. 렌더 보간 위치를 따라 매 프레임 이동시키지 않는다. 피격 몸의 흰 점멸만 대상 Sprite를 따른다. 시뮬레이션/그림 보간 차이는 실제 플레이에서 측정하고 필요하면 **출력 전용 보정**을 추가한다.

### 3.3. 시간의 정밀도

현재 `CombatAcceptedHit.Now`·TickCount는 기존 기록 시점의 전투 시각이다. 투사체의 부분 접촉 나이와 동일한 고정밀 타임스탬프라고 주장하지 않는다. v1은 기존 시각으로 재생하고 다음 표시 프레임에서 반응한다. 세부 부분-tick 시각을 추가하려면 해당 판정 경로에서 별도 근거를 캡처해야 한다.

## 4. 명중 소비와 Cue 수명

### 4.1. 입력 처리

1. 새 소비자는 현재 끝 cursor에 연결한다. 오래된 타격을 재생하지 않는다.
2. 페이지의 사실을 순서대로 읽고 개별 Target/Source 세션을 확인한다. 기존 스트림의 리셋 전 사실을 새 세션에 재해석하지 않는다.
3. HitKey를 확인하고 입력 문맥을 순수 `HitFeedbackInput`으로 번역한다. 최신 장비나 currentCast를 조회하지 않는다.
4. 세션 카탈로그의 `forms`로 공격 Stamp의 형태 자격을 확인한 뒤 recipe를 고른다. 치명타 명중(`Critical`)은 `criticalRecipe`, 평소 명중은 나머지 43개 중 명중 seed로 하나다. 게임 전역 난수 상태를 사용하지 않고 실행 중 선택도 없다. 매체 끄기는 기존 명중 VFX 스위치가 담당한다. 매핑 없는 직접 피해를 현재 장비로 보완하지 않는다. recipe·배율·회전은 해당 명중의 factory에 캡처한다.
5. 레이어별 수명·예산·위치 자격을 확인한 뒤 명중별 스코프를 연다. 같은 명중의 core/slash/fragments는 recipe의 서로 다른 ordinal을 사용한다.
6. 처리 결과와 무관하게 소비 cursor는 앞으로 진행한다. 실패한 같은 사건을 Draw마다 재시도하지 않는다. 입력 스트림 손실은 `DroppedBefore`로 기록한다.

처리량 상한에 도달해 페이지 일부만 처리했으면 **마지막 처리 사실까지만** cursor를 전진시킨다. 나머지는 다음 프레임에 읽고, 지연 때문에 수명이 끝난 장식은 `Expired`로 진단한다. 체력 감소나 피격 횟수로 유실 사건을 복원하지 않는다.

### 4.2. 발생 키와 해제

`HitKey → ActiveHitScope`는 현재 활성 효과만 보관한다. 완료 중복 방지는 cursor가 담당하므로 영구 HashSet을 추가하지 않는다.

기존 `CueOccurrence(ExecutionId, CueId, Ordinal)`는 **명중별 스코프 안에서** 사용한다. 서로 다른 대상·반복 명중은 별도 스코프이므로 같은 공격 실행 ID라도 충돌하지 않는다. long EventSequence를 int ordinal로 변환하지 않는다. 여러 레이어가 같은 Cue를 쓰면 ordinal로 구분한다.

- 스코프는 actor GameObject의 자식 수명에 묶지 않는다. 죽거나 공격이 취소돼도 이미 시작된 접촉 효과는 자기 꼬리 수명까지 남을 수 있다.
- 모든 핸들이 terminal이면 `CuePlayer.Tick` 콜백 **바깥에서** 스코프를 닫고 활성 사전에서 제거한다. 현재 CuePlayer의 재진입 금지를 지킨다.
- 리셋·출력 세션 종료는 pending·playing을 모두 취소한다. 늦게 도착한 뷰는 표시하지 않고 반환한다.
- 과거 사건이 이미 수명을 넘겼다면 Cue를 만들지 않고 `Expired`로 집계한다. 지연 재생을 매번 0초부터 시작해 과거 전투를 폭발시키지 않는다.

### 4.3. 예산의 단위

명중 사실 보존과 장식 표시 제한은 분리한다. `maxActiveHitScopes`, `maxInstancesByPrefab`, `maxParticles`, `maxWaves`, `maxDebrisBodies`는 서로 다른 비용 한도다. 우선순위는 recipe에 명시하고 동률은 사건 순번으로 안정적으로 결정한다. 용량 부족 시 장식 레이어를 먼저 생략하며, core까지 생략했다면 별도 진단으로 노출한다. 구체적 한도는 프로파일링 후 정한다.

## 5. 프리팹 출력 바인딩

### 5.1. 기존 Cue 계약 재사용

v1에서 `CueAnchor`의 기존 Sprite 계약을 대규모 변경하지 않는다. [CuePlayer.OpenScope의 factory override 경로](../../../../unity/Assets/Deviant/Presentation/Runtime/CuePlayer.cs)를 활용해 명중별로 고정된 factory를 공급한다.

`CapturedHitCueFactory`는 선택한 recipe·프리팹 바인딩·각 레이어의 최종 포즈·seed를 보관한다. 기존 `Begin(definition, anchor, resolved)` 요청이 캡처한 ID/버전과 일치하는지 확인한 뒤 해당 뷰를 준비한다.

최종 포즈는 다음 값으로 표현한다.

- 명중점의 X/Y, 장식 정렬용 Z
- incoming 벡터에서 구한 XY 평면 회전
- 프리팹 제작축을 보정하는 Unity 바인딩의 고정 rotation
- 양수 scale과 sorting layer/order

방향 미지정 효과는 바인딩에 명시한 무방향 자세를 사용한다. 임의의 음수 scale로 메시·파티클 전체를 반전하지 않는다. 효과의 앞뒤 가림은 Z만이 아니라 셰이더 render queue·sorting 계약으로 검증한다.

### 5.2. 지원 매체와 샘플링

v1 첫 지원 대상은 **비루프 ParticleSystem과 명시적으로 제어 가능한 자식 매체**다. VFX Graph·임의 Update/Coroutine·물리 피해 스크립트가 있는 프리팹은 자동 지원하지 않는다. 실제 팩 조사 후 조정한 Variant를 바인딩한다.

| 상태/호출 | 출력 동작 |
|---|---|
| Acquire | 비활성 인스턴스 확보, 이전 상태 Clear, 캡처 포즈·재질 속성·seed 적용 |
| 첫 `Sample(t)` | 자동 진행을 끄고 시작 상태에서 t까지 시뮬레이션 후 표시 |
| 같은 t | 재방출·재시뮬레이션 없음 |
| 증가한 t | 차이만 진행. 한 제어 루트가 자식까지 진행하면 자식을 다시 진행하지 않음 |
| 감소한 t | Clear 후 같은 seed로 t까지 재구성. 기본 전투에서는 역행 대신 reset을 사용 |
| 완료/취소/실패 | 즉시 출력 비활성·파티클/트레일/라이트 정리·pool 반환 |
| 이중 반환 | 인스턴스 lease 세대로 차단. 다음 소유자의 인스턴스를 끄지 않음 |

seed는 HitKey·레이어 ordinal에서 안정적으로 산출한다. 런타임마다 바뀔 수 있는 문자열 기본 해시를 재현성 기준으로 쓰지 않는다. 같은 seed가 Unity 버전·플랫폼 간 픽셀 단위 결정성을 보장한다는 뜻은 아니다.

`ICueDefinition.Duration`은 start delay·방출 종료·최대 입자 수명·서브 이미터·트레일 꼬리를 포함한 검증된 총 수명이다. 무한 지속 자식은 v1 비루프 recipe 검증에서 거부한다. 프리팹 자신의 `Destroy`나 `StopAction.Destroy`가 수명을 소유하지 않게 한다.

ParticleSystem을 멈춰도 셰이더 `_Time`과 Animator는 별도로 움직일 수 있다. 필요한 셰이더 시간 속성을 주입하고 Animator는 명시 샘플 어댑터가 있는 경우만 허용한다. 정지할 수 없는 프리팹을 시간 제어 지원으로 표시하지 않는다. 패키지의 음향·카메라 제어도 연결 대상에서 제외한다.

## 6. 시간과 진행 순서

### 6.1. 세 가지 시간

| 도메인 | 입력 | 소비자 | 정지/슬로우 |
|---|---|---|---|
| `World` | 기존 전투 시각·성공한 step의 dt | 명중 VFX, 죽음 모션, 잔해 수명·물리 | 전투와 같이 정지·감속 |
| `Accent` | 조립이 제공하는 실제 경과시간, 세션 정지 중 0 | 카메라 킥·왜곡 수명, 슬로우 요청의 실제 시간 TTL | 일시정지 때 보류, 슬로우 배율은 곱하지 않음 |
| `UI` | 기존 비스케일 UI 시간 | HUD·메뉴 | 월드 정지와 독립 |

월드 표현의 경과시간은 기존 전투 시각에서 시작 시각을 뺀다. v1은 새로운 렌더 보간 시계를 추가하지 않는다. 계단처럼 보이는 효과가 있다면 실제 화면에서 확인한 뒤 출력 보간만 검토한다. 0.35배속을 전투 시각과 파티클 simulationSpeed 양쪽에 중복 적용하지 않는다.

시험장도 명시적으로 시간을 주입한다. 자동 실행은 Editor 실제 시간, 수동 재현은 테스트가 지정한 accentDelta를 쓴다. `Step(worldDt)`가 암묵적으로 전역 wall clock이나 다른 arena를 진행시키지 않는다. 정지 상태의 진단용 한 tick은 실행 도구의 기존 허용 절차를 따르며 제품 일시정지를 우회하지 않는다.

### 6.2. 제안 프레임/step 계약

1. 조립의 frame 시작에서 Accent 시계·슬로우 만료를 갱신하고 공통 제어 결과를 반영한다. pause 중에도 만료 관리 코드는 호출하되 accentDelta=0이다.
2. 전투는 기존 순서로 tick을 실행한다. 명중 기록은 기존 피해 수락 지점에서 그대로 확정한다.
3. `simulation.Step`이 정상 반환하고 재진입 guard가 풀린 뒤, Native 조립 연결이 `CompletedStep`을 전달한다. 새로운 전투 단계나 피해 콜백 안에서 물리를 진행하지 않는다.
4. 완료된 이동 구간·지형 프록시 갱신을 반영하고 잔해를 해당 dt만큼 진행한다. 새 사망 전환은 바인딩된 실제 모션 경과로 판별한다.
5. 표시 단계에서 명중 페이지를 소비하고, Cue 시계를 갱신한 뒤 CuePlayer를 샘플링한다. 반복 LateUpdate는 같은 시각을 다시 진행하지 않는다.
6. 카메라 구도 선택에 킥을 합성하고, 그 최종 카메라로 월드→파동 중심 투영을 계산한다.
7. 월드 렌더, 왜곡 출력, HUD 합성 순으로 그린다.

표현 연결에서 발생한 예외는 해당 출력의 실패·정리로 제한한다. 완료 통지의 예외가 정상 전투 step을 `SimulationFaulted`로 바꾸지 않게 전투 실행 바깥에서 격리한다. 실패한 전투 tick에 포함된 부분 명중 사실은 기존 의미대로 남지만, 잔해를 성공한 tick처럼 진행하지 않는다. 결함 복구는 reset이며 추측으로 시간을 따라잡지 않는다.

`CompletedStep`은 `(session, tick)`으로 한 번만 수락한다. 중복·역행·다른 세션을 거부하고 진단한다. 정상 실행 중 통지 누락이 발생하면 잔해 출력을 실패 처리하며 임의의 큰 dt로 한 번에 밀어 넣지 않는다. 새 바인딩은 현재 tick을 기준점으로 삼는다.

### 6.3. 슬로모션 합성

제안 계약은 `scope.RequestSlow(scale, ttlAccentSeconds)`로 lease를 반환한다. 유한한 `0 < scale <= 1`, 양수 TTL을 검사한다. 0은 정지 요청을 사용하고 음수·NaN·무한은 거부한다.

- `resolvedScale = paused ? 0 : min(1, 모든 유효 slowScale)`
- 본 게임의 단일 어댑터는 명시적 기준 배율 B에 resolvedScale을 한 번만 반영한다. v1 B는 바인딩 때 캡처하며 외부 직접 쓰기와 혼용하지 않는다.
- scope 반환·TTL 만료 때 남은 요청을 재해석한다. 슬로우 전에 저장한 값을 요청별로 복원하지 않는다.
- 배율만 바뀌는 요청은 입력 차단·pause 전이와 구분한다. 슬로우 시작·종료가 기존 공격 입력 예약을 불필요하게 지우지 않는다.
- pause 중 TTL은 보류한다. pause 중 명시 취소된 slow는 재개 후 살아나지 않는다.
- 다른 actor의 요청을 취소하거나 리셋 과정에서 모든 외부 시간 소유자를 무조건 해제하지 않는다.
- `fixedDeltaTime`은 바꾸지 않는다. 본 게임은 실제 fixed tick 빈도가 줄고, 수동 시험장은 스케줄러가 실제 시간 budget에 resolvedScale을 한 번 적용한다. 수동으로 전달된 worldDt에는 다시 배율을 곱하지 않는다.

제품 피드백의 슬로우 요청은 현재 세션 플레이어의 실제 `Died` 통지에서만 생성한다. 생명당 첫 통지만 수락하고 반복 호출로 TTL을 연장하지 않는다. 살아 있는 플레이어의 정리용 Die, 과거 세션 통지, 콜백 중 부활·리셋은 새 생명에 적용하지 않는다. 몬스터 처치에는 요청하지 않는다. 배율·길이는 스키마 2 시간 원본의 시험값이며 수동 시험장의 순수 제어 합성 fixture는 별도 명시 요청으로 검사한다.

## 7. 죽음 전환과 시체 분할

### 7.1. 죽음 표현 토큰

`DeathToken = (actorHandle, bindingGeneration, deathSerial)`로 같은 죽음의 시작·완료·취소를 연결한다. 게임플레이 생명 수치가 아니라 **해당 출력의 사망 전환 인스턴스**다.

- `Before.Dead=false && After.Dead=true`는 치명 타격 후보일 뿐이다. 성공한 tick 뒤 현재 대상·세션·사망 상태와 실제 죽음 표현 전환을 확인한다.
- `BeginDeath`는 실제 모션 바인딩에서 발행한다. 치명 사건의 귀속이 확인될 때만 optionalHitKey를 연결한다. 단순 체력 직접 쓰기·누락 사건으로 사망했으면 귀속 없는 죽음으로 처리한다.
- 몬스터의 죽음 시작·완료는 파편 수명 계약이며 슬로우 요청을 만들지 않는다. 플레이어 사망 슬로우는 별도 native 사망 통지와 현재 생명·세션을 검사한다.
- `CompleteDeath`는 바인딩된 실제 죽음 모션 종료를 기준으로 한다. 공통 시스템에 종별 시간을 복사하지 않는다. death clip이 없거나 완료 포트가 없으면 잔해 전환 미지원으로 진단한다.
- 완료 전에 부활·reset·unbind되면 `CancelDeath`. 완료 통지가 늦게 와도 이전 토큰으로 생성하지 않는다.
- 잔해 생성 실패는 기존 죽음 표현 종료를 막지 않는다. 잔해는 보상·사망 처리를 호출하지 않는다.

### 7.2. 분할 캐시

에디터에서 승인 기본 Sprite의 alpha mask를 추출한다. 고정 seed로 불투명 영역에 분할 영역을 만들고, 작은 연결 조각을 합치거나 명시적으로 제외한다. 각각을 삼각분할한 표시 메시와 단순 볼록 충돌 조각으로 만든다.

`DebrisBakeAsset`의 제안 내용:

- 원본 Sprite GUID/local ID, 내용 해시, rect·pivot·PPU, packing 검증 정보
- 분할 설정·seed·출력 버전
- 조각별 원래 로컬 오프셋·질량 중심, vertices/triangles/UV, 충돌 다각형
- 불투명 면적 비율, 빈 조각·잘못된 winding·누락/중복 면적 검사 결과

분할 수·질량·마찰·반발·배출 속도는 시험 설정이다. 표시 메시를 물리 콜라이더와 똑같이 복잡하게 만들 필요는 없다. 원본 텍스처를 공유하고 런타임 픽셀 읽기·새 텍스처 생성은 하지 않는다. 기본 Sprite와 죽음 마지막 자세가 다른 데서 생기는 시각 전환은 실제 화면 승인 항목으로 남긴다.

flip은 생성 pose에서 조각 위치와 winding/충돌 모양에 일관되게 반영한다. Dynamic Rigidbody의 음수 scale을 애니메이션처럼 바꾸지 않는다. Sprite Atlas의 회전·tight packing을 지원하는 exporter가 없으면 해당 바인딩을 명시적으로 거부하고 잘못된 UV로 진행하지 않는다.

## 8. 전용 잔해 물리 공간

### 8.1. 선택과 비용

v1 제안은 전투 세션당 `LocalPhysicsMode.Physics2D` 씬 하나다. 이 공간에는 **지형 충돌 프록시와 잔해 body만** 존재한다. 게임 플레이어·적·투사체를 복제하지 않으므로 잔해가 캐릭터를 밀거나 발판이 되는 경로가 없다.

기존 기본 물리 세계에 Dynamic body를 추가하고 캐릭터 충돌만 제외하는 안보다 지형 복제 비용이 크다. 대신 본 게임·수동 시험장의 pause/step을 동일하게 제어하고 전역 `Physics2D.simulationMode` 변경을 피한다. 이 선택의 성립 조건은 지원 지형 프록시와 두 arena 독립 진행 검증이다.

### 8.2. 지형 프록시 계약

- 월드 조립이 등록한 collider만 변환한다. 씬 전체를 매 프레임 검색하지 않는다.
- 원본 GameObject 전체를 복제하지 않고 geometry·transform·고체/일방향 분류만 읽는다. 프록시에 AI·HurtRegion·게임 ArenaSurface·렌더러를 복사하지 않는다.
- stable surface ID와 geometry revision으로 추가·갱신·제거한다. Tilemap/Composite도 실제 출력 기하를 얻는 전용 변환기가 있어야 지원한다.
- 고체 바닥·벽, 위쪽에서만 받치는 일방향 발판을 구분한다. 움직이는 지형은 명시적인 pose 갱신 포트가 있을 때만 지원하며 원본 물체로 반작용을 보내지 않는다.
- 도착 지형이나 proxy 기하가 미지원이면 해당 잔해 바인딩을 실패 처리한다. 조각을 임의의 바닥 높이에 고정해 정상 검증처럼 보이지 않게 한다.
- 지형 언로드는 proxy 제거와 관련 잔해 회수 경계를 함께 적용한다. 프록시 collider·physics scene은 세션이 소유하고 종료 시 해제한다.

### 8.3. 진행·표시

`CompletedStep.dt`는 이미 감속이 반영된 **전투가 실제로 진행한 게임 시간**이다. 여기에는 slowScale을 곱하지 않는다. Unity가 이 로컬 scene을 자동 진행하지 않는 것을 검증하고 `PhysicsScene2D.Simulate`만 사용한다.

- 누적 budget을 세션 고정 물리 step h로 진행하고 잔여 dt는 보관한다. v1 h는 기존 fixedDeltaTime을 세션 시작에 캡처한다.
- manual Step의 큰 dt도 작은 step으로 분할한다. 한 번의 처리량 제한을 넘으면 미처리 budget을 보존하고 catch-up 상태를 표시한다. 무제한 큰 step이나 시간 폐기는 하지 않는다.
- impulse·생성·proxy 변경은 world 시각으로 큐에 넣어 그 시각 이전의 누적 budget에 새 잔해가 거꾸로 참여하지 않게 한다. 과도한 backlog에서는 새 장식의 admission을 제한하고 진단한다.
- pause는 새 world budget을 만들지 않는다. freeze된 상태를 표시하며 reset은 budget·대기 명령·body를 함께 비운다.
- 로컬 scene에는 숨긴 물리 body, 원래 월드 출력 루트에는 Collider 없는 표시 메시를 둔다. 진행 결과 pose만 복사하므로 기존 카메라·레이어 설정에 맞춰 표시할 수 있다.
- 첫 버전은 잔해끼리의 충돌을 끈 시험 프로필로 비용을 확인한다. 플레이어 통과 충격·지형 충돌만으로 목표 느낌을 만드는지 검토하고, 조각 간 충돌은 별도 튜닝 판단으로 남긴다.

### 8.4. 걷어차기

입력은 이동 전후 발밑 영역의 **실제 연속 구간 목록**이다. X→Y 이동을 대각선 한 줄로 대체해 벽 반대편 잔해를 건드리지 않는다. 걷기·대시 등 연속 운동과 순간이동·checkpoint 복귀·reset을 호출 경로에서 구분한다. 거리 임계값만으로 순간이동을 판별하지 않는다.

구간과 겹치는 활성 조각에 이동 방향 impulse와 작은 상향·회전 impulse를 한 번 적용한다. 크기는 이동 속도와 설정 상한으로 제한하고, 0 이동에는 적용하지 않는다. 동일 조각의 연속 겹침은 재접촉/쿨다운 설정으로 제한한다. sleep 중인 조각도 검색·재기동 대상이다.

적 이동이나 공격에 의한 걷어차기는 v1 입력 포트에 자동 추가하지 않는다. 사용자가 요청한 플레이어 통과 동작부터 검증한다.

## 9. 카메라와 화면 왜곡

### 9.1. 카메라 킥

`KickRequest`는 HitKey, 방향, 진폭(내부 픽셀), Accent 시작 시각·길이, priority를 가진다. 무방향 타격의 방향은 recipe에 명시하고 랜덤 카메라 흔들림으로 대체하지 않는다.

정규화 경과 u에 대해 v1 시험 곡선은 `envelope=(1-u)^2`로 빠르게 감쇠하는 한 방향 이동이다. 여러 요청의 벡터 합을 설정된 최대 길이로 clamp한다. 곡선·상한 수치는 화면 승인 전 시험값이다.

직교 카메라에서 `worldPerInternalPixel = 2 × orthographicSize / internalHeight`로 변환한다. 최종 위치는 `현재 구도 선택 위치 + kickOffset`이며 다음 프레임 추적 입력에 offset을 되먹임하지 않는다. `CameraOutputBinding`만 Transform을 쓴다. 종료 시 옛 카메라 좌표로 복원하지 않는다.

카메라 경계 여백·패럴랙스·픽셀 양자화는 출력 어댑터에서 확인한다. HUD는 별도 화면 출력으로 유지한다. 효과 감소/끄기는 순수 출력 진폭을 조절하며 명중 사건과 전투 결과를 바꾸지 않는다.

### 9.2. 왜곡 출력 포트

`WorldFrameLease`는 원본 world color, width/height, displayRect, 카메라, color-space 정보, 출력 세대를 제공한다. lease가 유효한 동안 원본은 읽기 전용이며 효과가 소유하지 않은 camera targetTexture를 교체하지 않는다.

목표 경계는 **최종 투명 월드 렌더 → 별도 결과 RT에 왜곡 → 기존 화면 출력 → HUD**다.

- 기존 RT 출력이 있으면 producer의 명시적인 렌더 완료 지점에 연결한다. 단순 LateUpdate에서 전 프레임 RT를 처리하지 않는다.
- 직접 화면 렌더 씬은 현재 URP 2D Renderer에서 지원되는 패스·RenderGraph 자원 획득 방식으로 별도 adapter를 검증한다. 패키지 전용 함수명이나 지원되지 않는 Feature를 설계만으로 확정하지 않는다.
- source와 destination은 다르게 두고, MSAA가 있다면 resolve된 color를 읽는다. 폭/높이·그래픽 형식·색 공간은 원본과 맞추고 해상도 변경에는 새 세대의 자원을 준비한다.
- Opaque Texture나 Depth Texture가 있다는 이유로 캐릭터·파티클이 포함됐다고 판단하지 않는다. HUD 제외도 실제 렌더 순서로 확인한다.

### 9.3. 파동 계산

파동은 명중 월드 좌표에 고정하고 매 렌더마다 최종 카메라에서 viewport로 투영한다. 창의 전체 픽셀이나 레터박스 사각형을 내부 픽셀 좌표와 섞지 않는다.

제안 셰이더 계산:

1. `p = (uv - centerUV) × (width, height)`로 내부 픽셀 거리 벡터를 만든다.
2. Accent 경과에 따라 반경 r을 증가시키고, `abs(length(p)-r)`가 띠 두께 안인 곳에만 매끄러운 envelope를 적용한다.
3. p 방향의 변위를 최대 내부 픽셀 수로 제한하고 `(width,height)`로 나눠 UV 변위로 변환한다. 중심의 0 벡터는 변위 0이다.
4. 제한된 파동들의 변위를 합한 후 표현 원본 `waveMaxPixels`의 총 상한으로 clamp하고, 화면 경계에서는 원본 밖 샘플을 방지한다. 상한은 유한 양수로 검증해 세션 설정에서 출력 생성자로 명시 주입하며, 단일 파동의 `wavePixels`와 함께 F1 강도를 곱한다. 출력 코드가 별도의 고정 픽셀 상한을 덧씌우지 않는다.

파동이 없거나 감소 설정이 0이면 원본 출력 경로를 사용한다. 첫 버전은 blur·색수차·별도 발광 고리를 더하지 않는다. 화면 가장자리·흔들림 동시 적용·UV 상하 반전·HDR 변환을 그래픽 fixture로 확인한다.

## 10. 설정·버전·해제

### 설정 원본 제안

| 원본/산출물 | 소유 값 |
|---|---|
| `data/presentation/hit_feedback.json` (신규 제안) | recipe ID, 공격→recipe 매핑, 레이어 순서·수명 도메인, 시각 배율·색·정렬 역할·표시 한도 |
| `data/combat/hit_feedback_time.json` (신규 제안) | 시뮬레이션에 영향을 주는 슬로우의 콘텐츠 자격·배율·실시간 길이·재발동 정책 |
| Unity `HitFeedbackBindings` (신규 제안) | prefabKey→원본/Variant, 셰이더 어댑터, 제작축 보정, sorting layer/order, Sprite·잔해 캐시 참조 |
| `DebrisBakeAsset` (생성물) | 원본 Sprite·분할 설정에서 생성한 메시·UV·충돌 기하 |

표현 JSON은 스키마 1, 시간 JSON은 `trigger=player-death`의 스키마 2 편집 원본이다. `tools/combat/export_hit_feedback.py`가 `Resources/HitFeedback/{HitFeedback,HitFeedbackTime}.json`으로 운반하고 `HitFeedbackSettings`와 `HitFeedbackTimeData.Validate`가 의미를 검증한다. 이전 중요 처치 시간 스키마는 자동 보완하지 않는다. 슬로우 수치는 게임 시간에 영향을 주므로 `data/combat/` 원본을 따르고, 표현 JSON에 배율·발동 조건을 중복 정의하지 않는다. 기존 Sprite/Audio CueSource의 내용을 이중 등록하지 않는다.

현재 `distortAllHits=true`로 모든 수락된 플레이어 기본·특수 명중에 왜곡을 요청한다. 요청은 공통 명중 VFX가 새로 시작된 경우에만 생성해 빗나감·거절·중복을 제외하고, 기존 파동 용량 제한을 따른다. 필드가 없거나 `false`이면 `strongActions` 자격만 사용한다. `strongActions`는 여전히 비어 있으며 큰 카메라 킥의 콘텐츠 자격은 확정하지 않았다. 왜곡 자격은 강타 카메라 자격과 독립적으로 복사한 세션 설정이다. 슬로우는 플레이어 사망 전용이며 이전 `HitFeedbackTarget.importantDeath`는 호환용 필드로만 남긴다. `IFeedbackTimeScope.RequestSlow`가 독립 lease를 만들고 `CombatArena.AdvanceFeedbackAccent`가 실제 시간 TTL을 진행한다. 수동 Step의 worldDt에는 다시 배율을 곱하지 않는다. 화면 반응 강도는 기존 F1 테스트 패널에서 조정하며 시간 배율이나 피해를 바꾸지 않는다.

`HitFeedbackAssetBuilder`는 Edit Mode에서만 로컬 Variant와 분할 산출물을 생성한다. 원본 프리팹은 수정하지 않으며 지원 컴포넌트를 ParticleSystem/Renderer/Transform으로 제한한다. 자체 `_Time`에 의존하지 않는 `Deviant/HitFeedback/Particle` 셰이더로 바인딩한다. 원본 Material/프리팹을 Play 중 Inspector에서 직접 수정하는 것은 지원하지 않는다.

왜곡은 해당 카메라의 `beginCameraRendering`에서 명시적으로 enqueue하는 URP 17.6 RenderGraph 패스다. Renderer2D는 `requiresIntermediateTexture`만으로 중간 색상을 생성하지 않으므로 패스의 `ConfigureInput(Color)` 요구도 명시한다. HDR·MSAA를 끈 마을의 네이티브 출력에서도 중간 색상을 받고, `AfterRenderingTransparents`의 색을 별도 texture에 기록해 후속 cameraColor로 넘긴다. camera.targetTexture를 교체하지 않으며 ScreenSpaceOverlay HUD는 월드 패스 뒤에 남는다. 월드 공간 UI나 다른 카메라 스택까지 자동 지원한다고 해석하지 않는다.

분할은 명시된 `Chapter1MonsterCatalog`의 첫 idle Sprite, 현재 `ApprovedStage1Monsters`의 명시 idle Sprite와 기존 숲 `InkMonsterData.move` 첫 Sprite만 사용한다. 원본 알파의 각 불투명 픽셀을 결정적 영역에 한 번 배정하고, 원본 UV를 공유하는 조각 메시·반전 메시·볼록 충돌 hull을 생성한다. 개별 의미 부위 인식이나 정밀 오목 충돌은 하지 않는다. Sprite rect/pivot/PPU·packing과 Editor 의존 해시가 달라지면 재생성이 필요하다. 로컬 지형 프록시는 Box/Polygon/Edge/Circle/Capsule과 polygon Composite를 지원한다. Composite에 합쳐진 Tilemap/Box 입력은 실제 composite로 해석해 한 번만 복제한다. 독립적인 원시 Tilemap/outline Composite·skew transform은 지원하지 않는다. 지형이 제거·비활성화되면 현재 잔해를 보수적으로 회수한다. 청크 스트리밍 전체·움직이는 발판의 품질 검토는 별도다.

v1은 모든 후보의 ID·참조·유한 수치·매체 호환을 검사해 불변 세션 카탈로그를 만든다. 오류가 있는 후보는 현재 정상 카탈로그를 교체하지 않는다. **플레이 중 새 카탈로그 Publish는 지원하지 않고 새 세션/명시적 리셋에만 반영한다.** 독립 미리보기의 편집값은 게임 카탈로그를 바꾸지 않는다.

세션 고정은 검증한 정의·참조·런타임 출력 설정의 계약이다. Unity 원본 Material·프리팹을 Play 중 Inspector에서 직접 수정하는 것까지 자동 격리한다는 뜻은 아니다. 게임 튜닝은 원본 변경 대신 별도 미리보기/후보 편집과 리셋 적용을 사용하며, 활성 출력의 값은 인스턴스 설정·MaterialPropertyBlock으로 분리한다. 직접 원본 편집이 발견되면 재검증 필요 상태로 표시한다.

현재 F1의 개별 스위치는 명중 VFX·명중 흰 점멸·시체 파편·카메라 킥·왜곡·사망 슬로우를 독립 제어한다. VFX 없는 수락도 순번·문맥·만료를 검증해 화면 출력에 전달하며, VFX를 다시 켰을 때 과거 사건을 재생하지 않는다. 출력 비활성화는 해당 활성 매체·파동·자기 시간 lease만 정리한다. 파편을 끌 때 이미 관측한 죽음의 생성 자격도 폐기한다. 전투 리셋은 스위치를 유지하고 새 씬은 기본 켜짐으로 시작한다. `CombatFeelFeedback`의 비교 설정과 승인 검 접촉 효과는 별도 층이며 이 스위치가 변경하지 않는다.

### reset/dispose 순서

1. 출력 세대를 무효화해 새 입력·늦은 callback 수락을 막는다.
2. 새 명중 소비·완료 step·죽음·이동·지형 입력 연결을 해제한다.
3. 자기 시간 요청·카메라 킥·파동 요청을 반환한다. 외부 정지 요청은 남긴다.
4. Cue 핸들을 취소하고 callback 밖에서 스코프를 닫는다. 인스턴스 출력은 즉시 끈다.
5. 잔해 명령·body·표시·proxy를 정리하고 물리 scene 해제를 완료할 때까지 소유권을 유지한다.
6. 자신이 만든 RT·Material·pool 자원을 반환한다. 다음 세션 자원을 옛 lease로 해제하지 않는다.

전투 step/Cue callback 중 reset·disable이 들어오면 출력 중지와 세대 무효화는 즉시 수행하고, 재진입 금지 객체의 Dispose는 외부 안전 지점까지 지연한다. 파괴되거나 미지원인 매체는 원래 PNG나 현재 장비의 효과로 조용히 대체하지 않는다.

## 11. 설계 검증 사례와 구현 진입 조건

| 사례 | 기대 결과 |
|---|---|
| 검 한 번으로 적 둘 명중 | HitKey 2개·Cue 스코프 2개. 화면 강조는 상한 정책 적용 |
| 동일 페이지 재처리·같은 시각 Draw | 추가 방출·시계 진행 없음 |
| 수리검 방출 뒤 무기 교체 | Stamp와 고정 세션 카탈로그로 원래 효과 사용 |
| 동작 중 좌우 전환 | 무기 정체성은 유지하고 실제 명중 방향만 반영 |
| 실드 완전 흡수·0 피해 수락 | 수락된 명중으로 처리. 거절과 혼동하지 않음 |
| 피해 콜백 중 reset·부활 | 옛 사실을 새 세션에 적용하지 않고 미확정 처치 강조·잔해 생성 없음 |
| 죽음 모션 종료 전에 대상 unbind | 옛 DeathToken의 완료 통지는 생성하지 않음 |
| slow A=0.5, B=0.35 중 B 종료 | 1이 아니라 0.5로 재해석. pause가 있으면 계속 0 |
| slow 중 pause와 명시 취소 | TTL 보류, 취소된 요청은 재개 후 복원되지 않음 |
| 수동 arena A를 한 step 진행 | A의 Cue·잔해만 진행. arena B·전역 물리는 영향 없음 |
| 걷기·대시·순간이동 | 실제 연속 발밑 구간만 잔해 impulse 생성 |
| pool 반환 뒤 늦은 callback | 인스턴스 세대 검사로 새 소유자의 효과 보존 |
| 월드 왜곡과 HUD | 투명 캐릭터·파편은 왜곡, HUD는 원본 픽셀 유지 |
| 카메라 킥·창 크기 변경 | 새 최종 투영·내부 크기를 사용해 파동 위치·원형 비율 유지 |
| 장기 연타·처치 | 활성 스코프·pool·파동·잔해가 설정 한도 안에 있고 완료 이력 무한 증가 없음 |

첫 구현 진입은 다음 세 조건을 확인한 뒤 한다.

1. 보유 팩의 후보 하나가 현재 renderer에서 표시되며 Cue 시간 제어에 맞게 조정 가능한가.
2. 로컬 PhysicsScene2D의 명시 진행·지원 지형 proxy·pause·두 arena 독립성을 검증할 수 있는가.
3. 실제 타깃 화면에서 투명 월드/HUD 경계를 제공하는 출력 어댑터를 만들 수 있는가.

기술 검증은 세 항목을 작게 먼저 수행하되 첫 제품 연결은 **검의 명중 하나 → 공통 소비자 → 프리팹 Cue → 정지·리셋·풀 반환**으로 제한한다. 성공 후 망치·다른 무기·잔해·화면 효과를 확장한다. 구현 진행 순서와 회귀 테스트 묶음은 [개선 계획](combat-hit-feedback-plan.md)에 둔다.
