# Rose·Blue 전투 구현

[분야 개요](README.md)

기획 기준은 [보스 명세](../../../design/enemies/chapter2/rose-blue/chapter2-rose-blue-boss.md)다. `Chapter2World`가 기존 임시 보스 대신 `RoseBlueEncounter`를 생성한다. 1페이즈 4개, 2페이즈 4개, 3페이즈 4개 패턴(3페이즈 석궁 낙하는 2026-11-05 삭제)을 실제 플레이어 피해·상태이상·Unity 물리 질의에 연결한 **시험 구현**이다. 최종 모션·VFX·음향·컷신 제작 완료를 뜻하지 않는다. 아래의 미정 판정은 별도 확인 대상이다.

## 전투장 원본

사용자가 제출하고 승인한 `rose-blue-boss-room (1).html`의 JSON만 읽어 `data/combat/chapter2_rose_blue_arena.json`에 반영한다. 첨부 HTML의 스크립트를 실행하지 않는다. 제출 원본은 리소스 저장소 `monsters/stage-02/elite-boss-pattern-drafts/source/approved-boss-room.html`에 바이트 그대로 보존한다. 데이터의 `sourceSha256`으로 연결한다.

30×18u, 바닥 3u, 발판 두께 0.5u다. 좌하단 기준 발판 좌표는 다음과 같다.

| 층 | 발판 왼쪽 X | 너비 | 윗면 Y |
|---|---|---|---|
| 1 | 6.5, 17.5 | 각각 6 | 5 |
| 2 | 2, 12, 22 | 각각 6 | 9.5 |
| 3 | 6.5, 17.5 | 각각 6 | 14 |

기계는 `(14, 9.5)`, 크기 2×3u다. 캐릭터 표시는 승인 HTML의 크기와 위치를 사용한다. 페이즈 전환 때 플레이어 현재 X에서 자연 낙하·Blue의 Rose 위치 이동은 전투 규칙을 우선한다. 3페이즈 HTML 마커는 전투 중 고정 위치가 아니다.

`RoseBlueStage`는 현재 보스방의 좌하단에 위 좌표를 더해 실행 중 콜라이더와 승인 지하수로 발판을 생성한다. `Chapter2Terrain`은 기존 지형 중 전투장 사각형과 겹치는 부분을 분할하여 바깥 부분을 보존한다. 맵 조립 데이터(`data/map/block_rooms.json`)를 바꾸는 방식이 아니다. 배경 미술 전환은 미확정이므로 기존 배경을 사용한다.

## 연결 구조

| 코드 | 책임 |
|---|---|
| `Chapter2/RoseBlue/Encounter.cs` | 페이즈, 한 보스 사망 시 융합 Blue 전환, 교환 투사체 결과, 컷신 완료 토큰 |
| `Patterns.cs`, `HazardRules.cs`, `BattlefieldRules.cs`, `AttackRotation.cs` | 12개 시간표, 반사·유도·재접촉·중복 피해, 발판·범위, 후보 선택·공격 종료 후 휴식 |
| `Chapter2/Runtime/RoseBlueEncounter*.cs` | 게임 액터와 코어 동기화, 공격 생성·전조·피해, 조작 잠금·컷신·낙사·기절·패링 |
| `RoseBlueActor.cs`, `RoseBlueStage.cs` | 보스 전용 피해 허용·제어 면역·HP 하한, 승인 배치와 전장 경계 |
| `Chapter2World.cs`, `Chapter2RoomState.cs` | 진입 카메라 고정, 엘리트와 잔존 공격의 무보상 제거, 클리어·기존 보상·출구·런 종료 연결 |
| `CombatActor`, `ActorDamageExecution` | 선택적 피해 정책을 적용한 뒤 적중·사망 콜백 처리. 일반 액터의 기본 규칙은 유지 |
| `CombatPlayer`, `CombatPlayer.Fall` | 전투 전용 조작/보호 조건과 복귀 목적지 주입. 기존 10% 낙사 피해·0.6초 보호 사용 |
| `TemporaryGameGui.Inventory` | 기계 HP, Rose/Blue 개별 HP 텍스트와 합산 바, 부활 상태, 3페이즈 Blue HP |

같은 스텝의 실제 액터 피해를 집계한 뒤 페이즈를 결정한다. 페이즈 전환은 보스 소유 공격만 제거한다. 플레이어 독립 효과의 시간을 전투 전체 정지로 멈추지 않는다.

3페이즈 휴식 이동은 Unity에 저장된 float 좌표가 double 전장 경계의 float 반올림값과 정확히 일치하는 경우 원래 경계로 보정해 계산한다. 임의의 범위 밖 좌표·비유한 좌표·뒤집힌 경계는 계속 거부한다. 속도 2u/s·휴식당 최대 3u·벽에서 정지하는 규칙은 유지한다. `RoseBlueRestMovementTests`는 양쪽 벽·양수/음수 월드 좌표·반복 휴식과 이동 제한을, `Phase3BounceExplosionsAtEitherWallKeepTheSimulationRunning`은 실제 5발 도탄 폭발 뒤 휴식 진입을 검사한다. 전투 시뮬레이션이 오류로 중단된 동안에는 새 게임 입력을 받지 않으며, 초기화 후 정상 입력을 다시 받는다.

교환 투사체의 파괴를 이동·충돌보다 먼저 해결한다. 일반 투사체는 `Rigidbody2D`와 트리거 콜라이더를 가지며 연속 이동 경로의 `Physics2D.BoxCastAll`로 피격 누락을 막는다. 반사 시 한 스텝에서 생기는 각 선분도 따로 검사한다. 유도는 회전 속도 제한, 반사는 전장 월드 경계 기준이다. 화살 그림과 히트박스 회전은 아래 미정 목록에서 구분한다.

교환 패턴의 파괴 가능한 화살·구체는 생성 시 빈 `InkDropSource.tableKey`를 지정하여 인벤토리의 일반 적 드랍표를 사용하지 않는다. `Chapter2World`가 초기 액터에 지정하는 설정과 같으며, 전투 중 생성되는 두 투사체에도 별도로 적용한다.

함정은 7개 모두 활성 순간 한 번만 접지를 검사하며 늦은 착지·재진입으로 속박하지 않는다. 순서 기억 폭발은 별도로 안전 발판 1개를 추첨하며, 마지막 시각 효과까지 패턴을 유지한다. 장치는 8초 경과 후 진행 중 패턴과 휴식을 마치고, 같은 자리에서 추가 패턴 한 번과 2초 휴식을 더 수행한 다음 이동하며 HP를 유지한다. 피격 콜라이더·분출·화살비의 기준 발판도 함께 변경한다.

## 승인 1페이즈와 남은 임시 표현

1페이즈에는 승인 Blue 손바닥 발사, 중앙 정면 장치, 지하수로 발판을 연결했다.

### 보스 몸 동작

Rose·Blue·흑화 Blue의 몸은 사용자 확정 동작(resources `monsters/stage-02/boss-motions-v8-20261108`)과 기본 자세 들썩 대기(`boss-idle-breath-20261107`)를 쓴다. 칸은 384×256, 발 기준점 (192,232)이고 원래 크기(32px=1u)로 actor 발 위치에 그린다. 판정 몸통 1.2×2.6u는 그대로다. `tools/assets/register_rose_blue_motions.py`가 동작 시트 18장을 배치 계약(`RoseBlueMotions/`)과 바인딩 `Resources/RoseBlueMotions.json`에 등록하고 `update_assets.py`로 복원한다.

동작 시트와 `RoseBlueVfx` 시트는 2챕터를 만들 때 모두 자르지 않는다. 바인딩만 읽고, 각 동작·VFX는 처음 쓸 때 시트에서 자른다. 플레이어가 전투장 원점에서 64u 안에 들어오거나 전투가 시작되면 `RoseBlueEncounter.WarmArtNear`가 남은 시트를 시뮬레이션 단계당 3ms 안에서 미리 자른다. 초기 표시용 숨 동작과 1페이즈 손·구슬 그림은 이전처럼 생성 시 읽는다. 2챕터 장면의 `Stage1VillageMonsterContent`는 종 데이터만 쓰므로 1챕터 몬스터 동작 카탈로그(`motions`)를 연결하지 않는다. 1챕터 몬스터 그림을 그리는 `CreateEnemy`만 그 카탈로그를 요구한다. 1→2챕터 전환은 `Chapter1NewMonsterArt` 공유 프레임 캐시를 비운 뒤 2챕터를 읽는다.

| 상황 | 동작 |
|---|---|
| 평소(1·2페이즈 Rose·Blue, 3페이즈 흑화 Blue) | rose-breath·blue-breath·fused-breath 반복 |
| 1페이즈 LayerShots | Rose rose-triple-shot(0.7·1.7·2.7초 사격 간격에 맞춰 늘림). Blue 는 기존 손바닥 발사 |
| 1페이즈 MachineAndPlatforms | Blue blue-ink-burst. 내리찍기를 `MachineBurstTell`에 맞추고, 시전 시작부터 보이며 사라지는 시각은 `MachineBurstVisibleSeconds` 그대로 |
| 2페이즈 StunAndInk / Swap | Rose rose-stun-shot·Blue blue-ink-throw / Rose rose-sky-volley·Blue blue-ink-whirl |
| 기절(교환 뒤 `ExchangeStun` 등) | rose-stun·blue-stun, 3페이즈 p3-stun |
| 3페이즈 Homing·Bounce·ArrowRain·ExpandingFan | p3-homing·p3-bounce·p3-rain·p3-fan |
| 3페이즈 휴식 이동 | p3-hover(떠오르기·이동·착지를 `Rest`에 맞춰 줄임) |
| 처치(Complete) | p3-defeat 후 1초 유지하고 사라짐 |

동작 칸은 패턴 시계로 고른다. 바인딩의 `cueKeys`(게임 시각·동작 시각 쌍)가 핵심 칸(팔 뻗기·내리찍기·두 손 밀기)을 `Patterns.cs` 큐 시각에 오도록 구간마다 재생 속도를 늘이고 줄인다. 입장 컷신 동작 R1·R2·R3·R6·B3와 1→2 전환 동작 R4·R7은 각 시간표(`rosePlan`·`bluePlan`)로 고른다. 흡수 동작(R5·R8·B2·변신)은 resources에 있지만 흡수 컷신이 아직 임시 타이머라 연결하지 않았다. 내리꽂기 속박·교환의 몸은 들썩 대기를 쓴다. 보스 퇴장은 0.12초 수평 잔상을 사용하고, 장치 파괴 시 파편을 표시한다. 투사체·전조·폭발·속박·압박은 1~3페이즈 모두 다크 판타지 VFX 세트를 쓴다. 원본은 resources에 보존하며 Unity 적용본은 고정 배포 계약으로 복원한다. 상세·재현 방법은 [1페이즈 아트 연결](../../presentation/visuals/chapter2-rose-blue-phase1-art.md)을 따른다.

입장 컷신은 `data/combat/chapter2_rose_blue_intro.json`(Unity `Resources/RoseBlueIntro.json`), 1→2 전환은 `data/combat/chapter2_rose_blue_phase_shift.json`(`Resources/RoseBluePhaseShift.json`)의 시간표로 `startAt`부터 끝까지 재생한다(현재 19.7초·14.65초). 두 파일은 `tools/combat/export_chapter2_rose_blue_cutscenes.py`로 동기화한다. 1→2 전환의 낙하는 기존 규칙대로이고 착지 조건과 시간표 끝을 모두 만족하면 2페이즈를 시작한다. 말풍선과 검은 띠는 `RoseBlueEncounter.CutsceneLines`·`CutsceneBars`를 게임 GUI(`TemporaryGameGui.BossCutscene.cs`)가 `MaintenanceBubbleView`로 그린다. 임시 컷신은 흡수 2초다. 바닥 전환은 파괴 정지 0.09초 후 현재 X에서 중력 낙하하며 최소 0.65초·착지 뒤 0.22초 조건을 모두 충족하면 끝난다. `CompleteCutscene(token)` 완료 신호로 조작을 복구한다. 최종 컷신에서는 이 임시 타이머를 대체해야 한다. 전용 음향·완성 애니메이션·스킵·중간 저장은 구현하지 않았다.

추가 판정은 [보스 명세의 확정한 추가 판정](../../../design/enemies/chapter2/rose-blue/chapter2-rose-blue-boss.md)을 따른다. 몸통 1.2×2.6u, 화살 히트박스 회전, 구체 재접촉 피해 간격 유지, 3페이즈 유도탄 패링 불가·2페이즈 유도 화살 패링 가능, 협공 파트너별 시전 취소, 복귀 보호 중 속박 차단, 전환 HP 유지와 기절·휴식 병행을 적용한다.

패링한 튕김 화살은 기존 시간표의 해당 화살 예약을 취소한다. Blue의 현재 위치로 직행하여 도착 시 폭발 피해 15를 한 번 적용한다. 플레이어 피해나 접촉 피해 7을 추가하지 않는다. 발사된 일반 탄의 수명과 교환 투사체의 규칙은 각 패턴을 따른다.

`RoseBlueEncounter.Revision.cs`는 이동 협공·원위치 복귀, 단발 함정, 순서 점등, 자연 낙하, 공유 보호막을 연결한다. 구체는 기존 물리 스윕을 사용하며 X축으로 전장 폭의 절반을 이동한 뒤 5초간 유도한다. 교환 탄의 가속 거리는 코어 `ExchangeVolley.Distance`를 렌더와 공유한다.

과거 부활 보호막 구현은 현재 전투에서 진입하지 않는다. 플레이어 공격력·피해 편차는 공통 전투 규칙을 사용한다.

은신 대상 조준·동일 프레임 다중 접촉 우선순위·최종 연출 세부는 별도 검증 대상이다.

보스방은 좌·우 출입구로만 들어온다([방·통로 공통 규격](../../../design/world/room-corridor-standard.md)). `RoseBlueStage.OpenBottomEntries`는 아래쪽 포트가 열린 배치에서 전투 전 바닥의 `opening` 폭을 비우는 기능으로 남아 있으나, 현재 런타임 조립은 보스방 아래 출입구를 만들지 않는다.

로컬 실행본에 `--rose-blue-boss-test`를 지정하면 2챕터를 열어 보스방에서 바로 시험한다. 일반 실행은 기존 타이틀·새 런 경로를 사용한다. 이 직접 시험은 다른 실행 중 프로세스의 런·장비를 이관하지 않는다.

## 생성과 검증

```powershell
python tools/combat/export_chapter2_rose_blue.py --check
python tools/combat/export_chapter2_rose_blue_arena.py --check
python tools/combat/export_chapter2_rose_blue_cutscenes.py --check
./tools/combat/test_rose_blue.ps1
```

첫 생성 때는 `--check`를 생략한다. 전투 수치 원본은 `data/combat/chapter2_rose_blue.json`, 방 원본은 `data/combat/chapter2_rose_blue_arena.json`, 컷신 원본은 `data/combat/chapter2_rose_blue_intro.json`·`chapter2_rose_blue_phase_shift.json`이다. `Tuning.g.cs`와 `Resources/RoseBlueArena.json`·`Resources/RoseBlueIntro.json`·`Resources/RoseBluePhaseShift.json`을 직접 편집하지 않는다.

코어 하네스는 Unity 6000.6.5f1의 Mono로 상태·시간표·기하·회전·재접촉 43개 사례를 검사한다. 실제 게임 연결은 같은 Editor의 EditMode 필터 `Deviant.Chapter2.Tests.RoseBlueIntegrationTests`로 검사한다. 이 테스트는 Play Mode에 진입하여 기존 Chapter2 씬에서 페이즈 전환·무보상 엘리트 제거·낙사·카메라·12개 패턴 생성·네이티브 피격·교환 탄 파괴·소환물 시전 취소·유도탄 Blue 명중을 확인한다. 로그·결과 XML·그래픽 장치가 있을 때의 캡처는 Git 제외 `outputs/rose-blue-unity/`에 둔다.

이 검사는 전체 게임 회귀·모든 무기/잉크 조합·사람의 플레이 난도·완성 연출·독립 배포 복원을 대신하지 않는다. 리소스 고정 커밋과 `unity-deployment.json` 배치 계약으로 원본을 복원한 작업 공간에서 실행한다. 리소스 MR의 커밋·LFS 객체를 먼저 원격에 게시하고, 메인 MR은 해당 고정 gitlink를 사용한다.

## 현재 전투 규칙 연결

2페이즈는 각 HP 12,000이며 어느 보스든 사망하면 융합 Blue로 전환한다. 부활 경로는 사용하지 않는다. 교환탄은 스왑 후 양쪽 HP 100·충돌 피해 100·회복 100을 사용한다. 2페이즈 패턴 1(`StunAndInk`)은 제자리 0.5초 전조 뒤 Rose 스턴 화살(40u/s, 4면 반사·관통·이탈 후 0.5초 유예 재피격, 3초 소멸)과 Blue 잉크 파이어볼(10u/s 직진, 접촉 즉시 또는 2초 뒤 4×4u 폭발, 경계 무시)을 동시에 쏜다. 모든 페이즈는 첫 공격부터 균등 추첨하며 직전 패턴을 제외하고, 남은 피해 판정 종료 후 2초 휴식한다. 스왑·유도 화살의 강제 후속 시전은 없다. 중앙 교환탄 폭발은 이펙트의 최대 외곽 23.8125×11.8125u 안에만 피해를 준다. 급강하는 직전 X 고정·무전조·폭 2u 단발 속박·즉시 복귀다. 튕기는 화살은 5발·26u/s이며 패링 후에도 26u/s다. 순차 화살은 중심 간격 2.5u와 전체 높이 폭발을 사용한다. ExpandingFan은 중앙에서 2초간 확장하는 광역기와 6발 개별 조준 연사를 연결한다. 3페이즈 순환은 유도 화살·튕기는 화살·순차 낙하·광역기 네 가지다.
