# 몬스터별 전투 정산 전환 계획

> 역할: 몬스터별 정산의 구현 설계 기록. 현재 정책은 연결된 SSOT, 구현·검증 상태는 A–K 작업 문서가 소유한다.

## 목표

메인 전투의 일반 몬스터와 보스 보상을 스테이지 사이클 종료 시 한꺼번에 확정하지 않고, 서버가 각 처치를 확인한 시점에 경험치·쌀·아이템을 한 번씩 확정한다.

여기서 `정산`은 몬스터 처치로 생기는 경험치, 쌀, 재료와 스킬북 지급을 뜻한다. 스테이지 클리어·다음 스테이지 해금·완전 회복·방치모드 전환은 보스 처치로 사이클이 끝나는 기존 경계를 유지한다.

## 현재 기준과 구현 차이

현재 정책 문서는 이미 처치 단위 지급을 요구한다.

- [성장 SSOT](../../30-domain/progression/ssot.md)는 경험치와 쌀을 처치 확정 시 즉시 지급하고 사망 후에도 유지하도록 규정한다.
- [스테이지 SSOT](../../30-domain/world/ssot.md)는 처치 확정 보상을 실패·수동 전환·메인 보석 프리셋 변경으로 되돌리지 않도록 규정한다.
- [클라이언트 UX SSOT](../../30-domain/player/ux/ssot.md)는 지급 확정된 쌀을 보유량, 전투 연출, 체류 획득 패널에 즉시 반영하도록 규정한다.
- [아이템 SSOT](../../30-domain/items/ssot.md)는 한 전투 결과의 보상 원자성·멱등성과 인벤토리 초과 시 실제 지급량·건너뛴 수량을 규정한다.

반면 현재 기준 구현은 사이클 단위다.

| 영역 | 현재 동작 | 근거 |
| --- | --- | --- |
| 서버 세션 | 시작 시 일반 20마리와 보스 결과를 미리 계산하고 전체 `completableAt`만 저장한다. | `modules/battle/.../BattleSessionService.kt` |
| 보상·성장 | `complete`에서 누적 처치 수를 인자로 아이템·경험치·쌀을 한 번에 지급한다. | `BattleSessionService.complete`, `BattleRewardService.grant`, `ProgressionRewardService.grantBattle` |
| 드롭 난수 | 같은 seed로 전체 일반 처치 수와 보스 여부를 순회한 뒤 결과를 합친다. 개별 몬스터 결과를 안정적으로 분리할 수 없다. | `modules/inventory/.../DropTable.kt` |
| 웹 | 렌더링 타임라인을 끝까지 재생한 뒤 `complete`를 한 번 호출하고 완료 응답으로 획득 패널을 갱신한다. | `apps/web/.../autoBattleClient.ts`, `runtimeStore.ts` |
| 이벤트·지표 | 사이클 완료 이벤트의 누적 쌀과 완료 시 생성한 아이템 드롭 이벤트를 집계한다. | `packages/contracts/events.tsp`, `modules/events/.../EconomyMetricAccumulator.kt` |
| 전투 기록 | 클리어·실패 시점의 누적 보상을 한 스냅샷으로 기록한다. | `modules/battle/.../BattleHistoryService.kt` |

특히 현재 `abort`는 미완료 사이클 전체를 무보상 종료한다. 일반 몬스터를 처치한 뒤 스테이지를 바꾸거나 프리셋을 변경하면 이미 화면에서 확인한 처치 보상이 사라질 수 있어 현재 SSOT와 맞지 않는다.

또한 [결정 로그](../../80-decisions/README.md)의 `메인 전투 동기화`와 [Networking](../../50-architecture/networking.md)은 사이클 단위 원자 확정을 confirmed 기준으로 기록하고 있다. 구현 착수 시 이번 사용자 요구를 새 결정으로 기록하고 해당 아키텍처 기준을 몬스터별 정산으로 전파해야 한다.

## 설계 결정

### 유지하는 경계

- `battleSessionId` 하나는 계속 일반 몬스터 20마리와 보스 1마리의 사이클 전체를 나타낸다.
- HP, 스킬 재사용시간, 전투 seed와 입력 스냅샷은 사이클 동안 유지한다.
- 레벨업과 자산 상태는 처치 정산 즉시 서버에 반영하지만, 이미 시작한 사이클의 전투 입력과 미리 계산한 타임라인은 바꾸지 않는다. 새 레벨의 전투 스탯은 다음 사이클 입력부터 사용한다.
- 스테이지 클리어, 다음 스테이지 해금, 사이클 완전 회복, 반복 대상 적용은 보스 처치와 최종 완료 경계에 남긴다.
- 최근 전투 기록 50건은 스테이지 입장·클리어·실패·복귀 단위를 유지한다. 몬스터 1마리마다 기록을 추가하지 않는다.

21개의 짧은 전투 세션으로 분할하지 않는다. 그 방식은 HP·스킬 타이머·seed 연속성을 별도로 복원해야 하고 세션 시작/종료 요청도 두 배로 늘린다. 기존 사이클 세션 안에 처치 정산 하위 명령을 두는 편이 단순하다.

### 새 정산 경계

일반 몬스터 처치마다 다음 명령을 보낸다.

```text
POST /api/v1/battles/sessions/{battleSessionId}/settlements
Headers: Idempotency-Key, X-Game-Session-Id, X-Battle-Token
Body: { throughEnemyIndex: N, renderingCheckpoint: { logicalTick, defeatedNormals, playerHp } }
```

- 클라이언트는 보상 수치나 아이템 ID를 보내지 않는다.
- `throughEnemyIndex`는 정상 경로에서 방금 처치한 일반 몬스터 인덱스다.
- 서버는 저장된 세션 입력, seed, 처치 예정 logical tick과 서버 시각으로 실제 처치 가능 여부를 검증한다.
- 서버는 아직 정산하지 않은 다음 인덱스부터 요청 인덱스까지 순서대로 처리한다. 정상 호출은 한 건이고, 응답 유실·일시 오류 뒤의 호출은 누락된 처치를 함께 복구할 수 있다.
- 미래 인덱스, 실제로 쓰러뜨리지 못한 인덱스, 순서 역행은 자산 변경 없이 거부한다.
- 동일 HTTP 멱등 키 재시도는 저장 응답을 반환한다. 다른 키로 같은 처치를 다시 요청해도 `(battleSessionId, enemyIndex)` 유일성으로 기존 정산 결과에 수렴한다.

보스는 기존 `complete`가 정산한다. 성공 완료는 누락된 일반 몬스터 정산, 보스 보상, 스테이지 클리어·해금, 다음 스테이지·방치모드 적용, 전투 기록과 이벤트를 한 트랜잭션으로 확정한다. 실패 완료는 서버 시뮬레이션에서 확인된 일반 몬스터까지만 누락 정산하고 실패 기록을 남긴다.

`abort`에는 마지막 렌더링 체크포인트를 선택적으로 전달한다. 서버가 시각과 시뮬레이션으로 확인할 수 있는 처치까지만 누락 정산한 뒤 세션을 닫는다. 이미 정산된 보상은 되돌리지 않는다. 통신이 완전히 끊겨 서버에 도달하지 않은 처치를 추정 지급하지 않는 기존 중단 정책은 유지한다.

### 응답 모델

정산 응답은 새로 확정되거나 멱등 재생된 처치 목록을 반환한다.

```text
BattleEnemySettlement
- settlementId
- battleSessionId
- enemyIndex
- boss
- settledAt
- reward: 실제 지급량과 skippedQuantity를 포함한 RewardResult
- progression: 이 처치 전후 경험치·레벨·쌀 상태
```

`complete` 응답의 사이클 누적 `reward`·`progression`을 클라이언트가 다시 더하는 구조는 제거한다. 대신 `settlements`에 보스 및 완료 과정에서 복구한 미정산 처치를 반환하고, 웹은 `settlementId`로 중복 반영을 막는다. 스테이지 기록용 누적값은 서버가 해당 세션의 정산 행을 합산한다.

## 서버 데이터와 결정론

### 세션 시작

현재 시작 시 이미 전체 전투 trace를 계산한다. 이 결과에서 실제로 처치된 적마다 정산 manifest를 batch insert한다.

```text
battle_enemy_settlement
- settlement_id UUID PK
- battle_session_id UUID FK
- enemy_index
- boss
- eligible_at
- reward_algorithm_version
- requested_reward_json
- experience
- rice
- settled_at nullable
- reward_result_json nullable
- progression_result_json nullable
- UNIQUE (battle_session_id, enemy_index)
```

- `eligible_at`은 세션 시작 시각과 서버 logical tick으로 계산한다.
- `requested_reward_json`, 경험치와 쌀은 세션 시작 때 고정한다. 배포나 콘텐츠 reload가 진행 중 세션 결과를 바꾸지 않는다.
- 정산 manifest에는 실제로 처치된 적만 존재한다. 실패를 일으킨 미처치 적은 지급 대상이 아니다.
- 기존 방식으로 생성되어 manifest가 없는 ACTIVE 세션은 배포 마이그레이션에서 `ABORTED`로 닫는다. 완료된 과거 결과와 보상 기록은 유지한다.

### 몬스터별 드롭

`DropTable`에 적 한 마리용 결정론 API를 둔다.

```text
rollEnemy(stageId, primaryMaterial, sessionSeed, enemyIndex, boss)
```

- 일반 적은 재료 1회와 스킬북 1회, 보스는 재료 5회와 스킬북 1회의 기존 확률을 유지한다.
- `sessionSeed + enemyIndex` 단순 덧셈 대신 고정된 seed 혼합 함수와 `rewardAlgorithmVersion`을 사용한다.
- 전체 사이클 시뮬레이션·호환 API가 필요한 경우 `rollEnemy` 결과를 순서대로 합산한다. 서버에 별도의 사이클 드롭 구현을 남기지 않는다.
- 동일 세션·적 인덱스는 항상 같은 요청 보상을 만들고, 서로 다른 적의 난수 스트림은 독립적이다.

### 트랜잭션과 동시성

각 처치 정산은 다음을 하나의 PostgreSQL 트랜잭션으로 커밋한다.

1. 계정과 활성 전투 세션 잠금
2. 게임 세션·전투 토큰·만료·처치 가능 시각 검증
3. 미정산 행 잠금과 인덱스 순서 검증
4. 경험치·레벨·쌀 적용
5. 인벤토리 수용량 판정과 아이템 실제 지급/건너뜀 확정
6. 정산 결과 저장
7. 계정 상태 버전과 Outbox 기록
8. HTTP command 결과 저장

내부 보상·성장 멱등 키는 `settlementId`에서 결정론적으로 파생한다. HTTP 요청 키가 달라도 같은 처치가 두 번 지급되지 않아야 한다. `settle`·`complete`·`abort`·장비/스킬 명령 경합은 기존 계정 잠금 순서를 사용한다.

## 이벤트와 지표

- `BATTLE_ENEMY_SETTLED` 이벤트를 추가한다. 적 인덱스, 보스 여부, 경험치, 쌀, 실제 지급 아이템 합계를 포함한다.
- `ITEM_DROPPED`는 각 처치 트랜잭션에서 실제 지급 수량만 발행한다.
- `BATTLE_CYCLE_COMPLETED`는 사이클 성공·실패와 전투 결과를 나타내는 수명주기 이벤트로 유지하되, 경제 지표의 쌀 생성량은 더 이상 이 이벤트의 누적값으로 계산하지 않는다.
- 경제 지표는 `BATTLE_ENEMY_SETTLED`의 쌀과 처치별 `ITEM_DROPPED`를 합산한다. 완료 이벤트와 중복 집계하지 않는다.
- 세션이 실패·중단돼도 이미 정산한 처치 이벤트와 공급량은 남는다.

## 웹 동작

1. `AutoBattleClient`가 `ENEMY_DEFEATED` 일반 몬스터 사건을 재생하는 즉시 정산 명령을 직렬 큐에 넣는다.
2. 정산별 멱등 키는 응답을 받을 때까지 재사용한다. 뒤 인덱스 요청은 앞 인덱스 누락을 복구할 수 있다.
3. 정산 성공 응답을 `onSettled`로 전달한다. `runtimeStore`는 `settlementId`를 중복 제거하고 다음을 즉시 갱신한다.
   - 레벨·EXP·쌀 잔액
   - 해당 스테이지 체류 획득 쌀
   - 실제 지급 아이템과 공간 부족 경고
4. 서버 완료 응답에 포함된 보스·복구 정산도 같은 reducer로 처리한다. 완료 응답의 누적값을 다시 더하지 않는다.
5. 사이클 완료는 앞선 정산 요청 큐가 끝난 뒤 실행한다. 영구 정산 오류가 있으면 최종 완료로 손실을 숨기지 않고 자동전투를 오류 상태로 멈춘다.
6. 스테이지 선택·프리셋 변경·페이지 수명주기 중단은 마지막 체크포인트를 담은 abort를 best-effort로 전송한다.
7. HUD는 정산 응답으로 즉시 바뀌고, 인벤토리·인증 세션·캐릭터 조회 캐시는 stale 처리한다. 매 처치마다 여러 조회를 강제 refetch하지 않는다.

## 호환 경로

`POST /api/v1/battles/cycles`와 챕터 `auto-run`은 현재 통합·검증용 호환 경로다. 제거하지 않되 다음 원칙으로 정리한다.

- 공통 몬스터 정산 유스케이스를 순서대로 호출해 결과를 합산한다.
- 별도의 누적 `grantBattle`·`rollStage` 서버 지급 경로를 사용하지 않는다.
- 기존 응답 모양이 필요하면 정산 결과를 합산해 반환하되, 권한 상태와 Outbox는 실제 몬스터별 정산 경계에서 한 번씩만 변경한다.

## 구현 순서

### 1. 정책과 계약

- [결정 로그](../../80-decisions/README.md)에 사이클 단위 원자 확정을 대체하는 몬스터별 정산 결정을 기록한다.
- [Networking](../../50-architecture/networking.md)의 기본 결과 단위, complete·abort 계약과 API 목록을 수정한다.
- 전투·스테이지·성장·아이템·클라이언트 UX·이벤트 SSOT에서 정산 책임과 유지되는 사이클 경계를 명확히 한다. 같은 규칙을 여러 문서에 복사하지 않고 각 owner의 책임만 수정한다.
- `packages/contracts/battle-session.tsp`와 `events.tsp`에 정산 요청·응답·이벤트를 추가하고 생성 OpenAPI와 웹 타입을 갱신한다.

### 2. 결정론과 저장

- `DropTable`을 몬스터별 독립 추첨으로 전환하고 사이클 집계는 그 결과 합으로 만든다.
- `ProgressionRules`에 일반/보스 한 마리 지급 계산을 추가하고 기존 사이클 계산이 필요하면 같은 원시 함수로 합산한다.
- Flyway migration으로 정산 manifest/결과 테이블, 유일성, 조회 인덱스와 레거시 ACTIVE 세션 종료를 추가한다.
- 세션 시작이 전투 trace와 몬스터별 요청 보상을 한 버전으로 저장하도록 수정한다.

### 3. 서버 정산 유스케이스

- `BattleEnemySettlementService`를 단일 정산 책임으로 추가하고 세션 `settle`, `complete`, `abort`, 호환 cycle/chapter API가 이를 사용하게 한다.
- `BattleSessionService.complete`의 누적 `BattleRewardService.grant`와 `ProgressionRewardService.grantBattle` 호출을 제거한다.
- 보스 정산과 스테이지 클리어·해금·기록을 같은 완료 트랜잭션에 묶는다.
- 전투 기록 누적 스냅샷을 저장된 정산 결과에서 조립한다.

### 4. 이벤트와 경제 지표

- 처치 정산 및 아이템 드롭 Outbox를 정산 트랜잭션 안에서 발행한다.
- 경제 지표를 처치 정산 이벤트 기반으로 전환하고 사이클 이벤트 누적 쌀의 중복 집계를 제거한다.
- 이벤트 계약·집계 테스트와 J-02/J-03/J-11 증거를 갱신한다.

### 5. 웹 즉시 반영

- `sessionApi.ts`에 정산 명령과 같은 키 재시도를 추가한다.
- `AutoBattleClient`에 처치 정산 큐, 완료 전 drain, abort 체크포인트 전달을 연결한다.
- `runtimeStore`에 정산 ID 중복 제거와 몬스터별 성장·획득 누적 reducer를 추가한다.
- 메인 HUD와 공유 표면이 정산 응답의 레벨·EXP·쌀·아이템을 즉시 표시하도록 연결한다.

### 6. 정리와 문서 동기화

- 더 이상 호출되지 않는 사이클 누적 지급 함수·DTO·테스트를 제거한다.
- E-01, E-04, B-05, B-09, D-07, F-05, J-02, K-06 작업 문서와 A–K 색인의 실제 개발·검증 상태 및 증거를 갱신한다.
- API 계약 계획과 관련 기능 경로는 새 정책을 복사하지 않고 정본·작업 문서 링크만 유지한다.

## 검증 계획

### 결정론 단위 검증

- 같은 세션 seed·적 인덱스·보상 버전은 같은 드롭을 만든다.
- 서로 다른 적 인덱스는 독립 난수 스트림을 사용한다.
- 일반 20마리와 보스의 추첨 기회 총합은 기존 25회 재료·21회 스킬북 기준을 유지한다.
- 10스테이지는 모든 몬스터에서 아이템 드롭이 없다.
- 일반/보스 한 마리 경험치·쌀의 합은 기존 사이클 총량과 같다.

### PostgreSQL 통합 검증

- 첫 일반 몬스터 정산 직후, 사이클 완료 전 지갑·경험치·인벤토리가 변경된다.
- 같은 멱등 키 재시도와 다른 키의 같은 인덱스 요청 모두 단일 지급 결과를 반환한다.
- 미래 인덱스·미처치 적·잘못된 토큰은 자산·정산 행·Outbox를 바꾸지 않는다.
- 7마리 정산 뒤 사망, 수동 abort, 프리셋 변경에서도 7마리 보상이 유지되고 나머지는 지급되지 않는다.
- 응답 유실 뒤 높은 `throughEnemyIndex` 요청이 누락 구간을 한 번씩 복구한다.
- 인벤토리 만석에서도 경험치·쌀은 지급되고 아이템은 실제 지급 0과 `skippedQuantity`로 한 번만 기록된다.
- 한 처치로 여러 레벨이 오르거나 500레벨에 도달해도 잔액과 결과가 정확하다.
- 보스 완료는 보스 정산, 클리어 횟수, 다음 스테이지 해금, 전투 기록을 한 번만 확정한다.
- settle/complete/abort 동시 요청과 serialization 재시도에서 중복·유실이 없다.

### 웹 검증

- 일반 몬스터 `ENEMY_DEFEATED` 사건마다 정산 API가 순서대로 한 번 호출된다.
- 첫 정산 응답만으로 레벨·EXP·쌀·체류 획득 패널이 사이클 완료 전에 갱신된다.
- 같은 `settlementId` 재응답은 HUD 누적값을 늘리지 않는다.
- 정산 요청 응답 유실은 같은 키로 재시도되고, 뒤 요청의 누락 복구 결과도 한 번만 반영된다.
- 성공 완료의 보스 정산과 실패 완료의 복구 정산은 일반 정산과 같은 reducer를 사용한다.
- 정산 큐가 남아 있는 동안 complete가 먼저 전송되지 않고, 중단 시 마지막 체크포인트가 abort에 포함된다.

### 실행 스모크

실제 PostgreSQL·game-api·web으로 다음 시나리오를 수행한다.

1. 강한 테스트 계정으로 1-1을 시작한다.
2. 첫 일반 몬스터 처치 직후, 보스 도달 전 서버 지갑·경험치·정산 행과 HUD 획득량이 증가했는지 확인한다.
3. 여러 마리 처치 뒤 스테이지를 전환해 이미 받은 보상은 유지되고 미처치 보상은 없는지 확인한다.
4. 전체 사이클을 완료해 보스 보상·클리어·1-2 해금과 전투 기록 누적 보상을 확인한다.
5. 같은 정산 요청을 재전송해 DB 잔액, 인벤토리, Outbox 건수가 증가하지 않는지 확인한다.

### 부하 확인

기준 처치속도 50마리/분은 계정당 초당 약 0.83회의 정산 transaction을 만든다. 동시 세션 부하에서 정산 p95, 계정 잠금 대기, Outbox 증가율, 중복/누락 건수를 측정한다. 목표치를 임의 확정하지 않고 측정 결과를 E-06 또는 J-07 증거에 기록한다.

## 영향 파일

주요 변경 예상 경로:

- `packages/contracts/battle-session.tsp`
- `packages/contracts/events.tsp`
- `packages/sim-core/src/main/kotlin/com/hanjjak/sim/CombatSimulator.kt`
- `modules/battle/src/main/kotlin/com/hanjjak/battle/application/BattleSessionService.kt`
- `modules/battle/src/main/kotlin/com/hanjjak/battle/api/BattleSessionController.kt`
- `modules/battle/src/main/kotlin/com/hanjjak/battle/api/BattleController.kt`
- `modules/inventory/src/main/kotlin/com/hanjjak/inventory/domain/DropTable.kt`
- `modules/inventory/src/main/kotlin/com/hanjjak/inventory/application/BattleRewardService.kt`
- `modules/progression/src/main/kotlin/com/hanjjak/progression/domain/Progression.kt`
- `modules/progression/src/main/kotlin/com/hanjjak/progression/application/ProgressionRewardService.kt`
- `modules/events/src/main/kotlin/com/hanjjak/events/application/EconomyMetricAccumulator.kt`
- `apps/game-api/src/main/resources/db/migration/`
- `apps/web/src/features/battle/sessionApi.ts`
- `apps/web/src/features/battle/autoBattleClient.ts`
- `apps/web/src/features/battle/runtimeStore.ts`
- `apps/web/src/features/battle/AutoBattleRuntime.tsx`
- 관련 Kotlin E2E·모듈 단위 테스트와 웹 Vitest

## 완료 조건

- 보스 도달 전 첫 일반 몬스터 보상이 서버 DB와 HUD에 확정된다.
- 실패·중단 이전에 정산된 모든 처치 보상은 유지되고, 미처치 보상은 지급되지 않는다.
- 정상·재시도·경합 경로에서 몬스터 한 마리당 보상은 정확히 한 번 적용된다.
- 보스 처치의 보상·클리어·해금은 한 번만 확정된다.
- 사이클 총 드롭 기회와 경험치·쌀 수치는 기존 정책을 유지한다.
- 경제 지표와 전투 기록은 몬스터별 저장 결과를 중복 없이 집계한다.
- 실제 브라우저에서 처치 직후 레벨·EXP·쌀·아이템 획득 표시가 스테이지 완료 전에 바뀐다.
