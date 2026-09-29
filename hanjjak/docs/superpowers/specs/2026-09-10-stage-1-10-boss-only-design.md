---
doc_kind: design
owner_domain: world
status: approved
approved_at: 2026-09-10
---

# 1-10 보스 단독 전투 설계

## 결정

- `stage.01-10`은 일반 몬스터를 생성하지 않고 강력한 보스 1마리만 즉시 생성한다.
- 나머지 39개 스테이지의 기존 일반 몬스터 20마리 후 보스 1마리 사이클은 유지한다.
- 서버 simulator와 웹 렌더링은 같은 `bossOnly` 스테이지 속성을 사용한다.
- 보스 수치와 승패 판정은 기존 서버 결정론 전투를 그대로 사용한다.

## 계약

- `StageDefinition.bossOnly`은 `stage.01-10`에서만 `true`다.
- `StageBattleService.input("stage.01-10", ...)`은 `normalCount=0`을 발급한다.
- 1-10의 첫 전투 사건은 `BOSS_SPAWNED`이며 `enemyIndex=1`, `boss=true`다.
- 1-10 결과의 `defeatedNormals`는 0이다.
- `StageSummary.bossOnly`을 받은 메인 HUD와 PIP는 일반 몬스터 진행 바 대신 보스 진행 상태를 표시한다.
- 전투 기록은 전투 입력의 `normalCount` 스냅샷으로 당시 조우 구성을 보존하며 1-10 실패를 `진행: 보스전`으로 표시한다.
- 1-10 실패 분석과 재도전 안내는 일반 구간이 아니라 보스전으로 표현한다.

## 검증

- sim-core: `normalCount=0` 입력이 잡몹 없이 보스부터 시작하는지 확인한다.
- battle: 1-10은 `normalCount=0`, 1-1은 `normalCount=20`인지 확인한다.
- web: 메인 HUD·PIP·전투 기록에서 1-10에 일반 `0 / 20`이 노출되지 않는지 확인한다.
