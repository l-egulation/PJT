# 블록 맵(방·통로 조립)

[분야 개요](README.md)

[방·통로 공통 규격](../../../design/world/room-corridor-standard.md)의 구현이다. 1·2챕터 맵을 같은 원형 데이터와 조립기로 만든다.

## 원본과 생성물

| 파일 | 역할 |
|---|---|
| `data/map/block_rooms.json` | 편집 원본. 1·2챕터 방 원형 42종(일반 28 + 특수 8 + 1챕터 기믹 6)의 블록 크기·슬롯·고체·일방향 발판(지워지는 발판 표시 `vanishing`)·적·기믹 표식 자리(`marker`)·1챕터 시작방의 플레이어 시작 자리(`spawn`)와 종 매핑. 좌표는 방 안쪽 기준, y 위 방향 |
| `unity/Assets/ForestCombatV2/Resources/BlockRooms.json` | 생성 사본. 직접 수정하지 않는다 |
| `unity/Assets/ForestCombatV2/Tests/Editor/BlockGeneratorParity.json` | 같은 시드의 도구 배치기 결과. C# 배치기가 같은 결과를 내는지 Unity 시험이 비교한다 |

```
node tools/map/export_block_rooms.cjs
node tools/map/export_block_rooms.cjs --check
node tools/map/validate.cjs [시드 수]
node tools/map/monster_stats.cjs [시드 수] [--csv 폴더]
```

`validate.cjs`는 원형마다 선언한 슬롯의 모든 활성 조합 × 좌우반전(현재 2,708건)과 배치기 시드를 판정한다. 판정 항목은 고체 두께, 발판 겹침·간격·묻힘, 적의 지면·바닥 폭·출입구 금지 구역, 활성 출입구 사이 왕복, 적 도달, 1챕터 시작방의 시작 자리다. 이동 판정(`tools/map/movecheck.js`)은 이동 수치로 만든 근사 모델로, 위로 오른 뒤 옆으로 이동해 떨어지는 경로와 벽 붙기(면당 7.5u, 벽차기 뒤 공중 점프 1회)만 인정한다. 실제 물리 확인은 아래 Unity 시험이 맡는다.

1챕터 시작방(`S1-START`)은 배치기가 오른쪽으로만 다음 방을 붙이므로 출입구를 오른쪽 출구(`R0`) 하나만 선언한다. `spawn`(방 안쪽 좌표, y는 선 바닥·발판 높이)이 런의 플레이어 시작 위치다. `Chapter1ProceduralMap`이 이 값으로 `ForestData.spawn`을 정한다. 판정기는 `spawn`이 있는지(1챕터 시작방 필수), 방 안쪽의 고체 바닥 또는 출입구 조건 없는 발판 위인지, 머리 위 2칸이 비었는지(P1), 그 자리에서 열린 모든 출입구로 나갈 수 있는지(M4)를 검사한다. 튜토리얼 팻말(`Chapter1ControlsSign`)은 시작 위치와 같은 높이의 바닥이나 발판(일방향 포함) 위, 시작 위치에서 왼쪽 약 2.3u에 가장 가까운 자리에 선다. 머리 위 4.6u가 비고 받침 양끝 1u가 그 면에 걸려야 한다. 개발용 도면대도 이 시작 위치를 기준으로 자리를 잡는다.

`tools/map/start-room-editor/시작방_편집기.html`은 모든 시드가 함께 쓰는 1챕터 시작방의 시작 위치·발판·몬스터를 배치하는 편집기다. `validate.cjs`와 같은 판정에 더해, 추격하는 몬스터가 `data/combat/enemy_pursuit.json`의 점프 한계로 발판을 차례로 밟아 시작 자리 높이까지 오를 수 있으면 경고(J1, 가로 거리는 따지지 않음)를 실시간으로 보이고, 결과 JSON을 `block_rooms.json`의 `S1-START` `spawn`·`plats`·`enemies`에 넣은 뒤 `export_block_rooms.cjs`로 반영한다. 편집 원본은 같은 폴더의 `index.html`이며, `node tools/map/start-room-editor/build.cjs`가 판정 스크립트와 현재 `S1-START` 값을 넣어 한 파일짜리 HTML을 만든다.

`monster_stats.cjs`는 시드마다 같은 배치기·조립기로 맵을 만들어 종류별 마리수와 일반 몬스터 체력 합의 최소·평균·최대를 낸다. 체력은 `data/combat/`의 1·2챕터 원본과 `Chapter2ApprovedNormals.json`을 읽는다. 처음 배치만 세며 봉인 도전의 떨어지는 몹은 기댓값, 보스 패턴 소환물은 제외한다. 결과는 [몬스터 배치 규모](../../../design/enemies/README.md#스테이지별-배치-규모)에 적는다.

## 런타임 구성

- `ForestCombatV2/Runtime/BlockMap/BlockMap.cs`: 원형 로드, 방 조립(슬롯 키트·`need` 발판·반전), 통로 블록(보스 앞 긴 통로는 `bw`=`BlockGenerator.Hall` 3블록), 스테이지 조립(셀 소유·정렬 검사), 셀→충돌 사각형.
- `BlockGenerator.cs`: 시드 배치기. `tools/map/generator.js`와 같은 난수·후보 순서를 쓴다. 1챕터 `Stage1(seed)`는 왼쪽 출발 가로 진행, 2챕터 `Stage2(seed)`는 세로 진행이다.
- 1챕터: `Chapter1ProceduralMap.Generate`가 배치를 `ForestData`로 바꾼다. 방 순서는 주 경로(시작 0번 … 보스 9번) 다음 갈래 방이다. 보스방 사각형은 기존 보스 장치 계약(안쪽 30u, 바닥 = room.y+15, 왼쪽 입구)을 따른다. 엘리트(우물 도르래 수문장)는 엘리트 표식에서 생성한다.
- 2챕터: `Chapter2BlockLayout.Generate`가 배치를 `C2Layout`으로 바꾸고 `C2Bank.Generate(seed)`가 이를 돌려준다. 방 id·`E:elite`·보스 전투장 원점(boss−(0,3))·출구 지점 계약을 지킨다. 일반 적의 이동 영역은 `ApproachZones`로 열린 좌우 출입구 접근 구역을 잘라 낸다.
- 2챕터 기믹 방: `Stage2`는 순환 갈래를 전투방 1개 + 1챕터 잠긴 상자방 원형으로, 봉인 도전방을 정비소 자리 뒤 주 경로 방의 막다른 갈래로 놓는다(규칙은 [스테이지 기믹](../../../design/world/stage-gimmicks.md#2챕터)). `BlockLayout.chestFrom`은 상자 갈래가 갈라진 주 경로 순번이다. `C2Layout.seal`·`chest`는 석판·상자 발 위치와 봉인 철창 상자, `carrierRooms`는 열쇠 운반자를 고를 주 경로 전투방, `carrierMap`(직렬화하지 않음)은 운반자 경로용 1챕터 형식 타일 지도(월드 y = `carrierMapOffset` − 행)다. 생성기를 바꾸면 `node tools/map/export_block_rooms.cjs`로 C#·JS 일치 기준(`BlockGeneratorParity.json`)을 다시 만든다.

## 시험

- `ForestCombatV2.Tests.BlockMapTests`: C#/도구 배치기 시드 일치, 모든 원형·조합·반전에서 테두리가 활성 출입구 외에 닫혀 있는지와 적의 지지면, 40개 시드 조립(겹침·정렬·활성 슬롯 일치), 1챕터 `ForestData` 계약, 실제 플레이어 물리로 수직 직결(접근 발판 → 디딤 → 위 방 문 바닥) 오르기와 층마다 멈추는 내려가기.
- `Deviant.Chapter2.Tests.Chapter2BlockLayoutTests`: 40개 시드의 `C2Layout` 계약(16방 id·엘리트·보스 전투장 폭·시작방과 보스방 높이·벽타기 통로 1~3개·정수 격자·같은 시드 같은 배치), 출입구 접근 구역, 실제 플레이어 물리로 벽타기 통로 오르기(점프 뒤 ↑로 한 면 등반 → 착지 발판 → 2단 점프로 위 방 바닥).
- `BlockMapTests.LowestConnectorRungCanBeLandedOnAgainAfterDropping`: 좌·우 굴곡 통로의 실제 조립 지형과 플레이어 모터로 옆에서 접근해 바닥 2u → 첫 디딤 4u에 처음 착지하고, 아래로 통과한 뒤 같은 발판에 다시 착지하는 과정을 30·60·120Hz에서 반복한다. 원점과 깊은 월드 좌표(320, -144)를 모두 검사한다. `ActorPlatformTests`는 발판 아래 몸 전체 이탈·위쪽 발 이탈에 따른 충돌 복구, 최소 0.25초와 몸이 겹친 동안의 무시 유지를 검사한다.
- `BlockMapTests.GeneratedConnectorRungsSupportFreshLandingsFromBelowAndAbove`: 4개 시드(1·7·42·20261113)의 `ForestData`와 본게임 `BuildGeometry`로 충돌면을 생성한다. 일반 세로 통로의 첫 디딤마다 내려가기 이력이 없는 플레이어를 아래에서 점프시키거나 위에서 떨어뜨려 첫 착지를 검사한다. 아트 표시와 원본 제보 세션의 입력 재현은 이 검사에 포함하지 않는다.

## 알려진 제한

- 판정기는 근사 모델이다. 모든 원형·조합의 실제 물리 왕복은 자동 시험하지 않으며, 수직 직결과 벽타기 통로의 대표 구간만 실제 물리로 확인한다.
- 원형의 배경 장식은 원형 데이터에 없다. 1챕터는 장식이 없는 방에 기존 절차 소품을 배치하고, 2챕터는 기존 수로 배경 규칙을 쓴다.
- 1챕터 일반 적의 대기 배회는 방 경계로 제한하지 않는다(스폰 위치 기준).
