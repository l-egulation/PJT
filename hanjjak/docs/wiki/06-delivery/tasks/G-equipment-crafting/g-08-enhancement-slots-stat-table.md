---
doc_kind: task
owner_domain: delivery
task_code: 'G-08'
jira_key: 'S15P21B107-172'
task_area: 'G 장비·제작·강화'
task_type: '기획'
priority: 'P0'
target_chapters: '공통'
planning_status: '완료'
development_status: '완료'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/41206c8781b08335bba3019bc90f8634'
notion_id: '41206c87-81b0-8335-bba3-019bc90f8634'
snapshot_date: '2026-08-28'
---

# G-08 6부위 등급·강화·Q 반영

> 작업 상태와 완료 기준의 SSOT. 능력치 규칙은 [장비](../../../../30-domain/items/equipment/ssot.md)와 [전투](../../../../30-domain/combat/ssot.md)를 따른다.

## 완료 기준

6부위의 영구 상태가 등급별 1~30강과 Q 계산에 반영되고, 미해금 부위는 Q=0 기준 기여를 유지한다. 어떤 부위·등급·강화 단계에서도 강화 증가량이 0이 아니며, 공격력·최대 HP 장비는 같은 단계에서 `희귀 > 노말`, `영웅 > 희귀`, `전설 > 영웅` 증가량을 보장하고 승급 후 1강이 직전 등급 30강보다 커야 한다. 장비 미리보기·강화 결과·캐릭터 능력치·전투 입력은 서버 `EquipmentRules` 정수 기여값을 공유해야 한다.

## 선행 작업

G-01

## 비고

기존 42개 강화 슬롯·품질 선택표는 현재 정책의 legacy 기준으로 유지하지 않는다. 승급은 등급별 +1로 돌아가지만 Q는 승인된 등급 오프셋으로 연속 성장한다.

## 증거 링크 (2026-09-11 검증)

- SSOT: `docs/30-domain/items/equipment/ssot.md`, `docs/30-domain/combat/ssot.md`
- 코드: `modules/equipment/src/main/kotlin/com/hanjjak/equipment/domain/Equipment.kt`, `modules/equipment/src/main/kotlin/com/hanjjak/equipment/application/EquipmentService.kt`, `apps/game-api/src/main/kotlin/com/hanjjak/gameapi/character/CharacterStatsCalculator.kt`
- 2026-09-11 JVM 검증 (성공 경로 `X:\` ASCII 매핑): `./gradlew.bat :modules:equipment:test :apps:game-api:test --tests com.hanjjak.equipment.domain.EquipmentRulesTest --tests com.hanjjak.equipment.application.EquipmentServiceTest --tests com.hanjjak.gameapi.character.CharacterStatsCalculatorTest -Pkotlin.incremental=false --rerun-tasks --no-daemon --no-configuration-cache --max-workers=1`. 결과 `BUILD SUCCESSFUL`; 각각 `EquipmentRulesTest` 10/10, `EquipmentServiceTest` 10/10, `CharacterStatsCalculatorTest` 6/6, skipped/failures/errors 모두 0.
- 2026-09-11 직접 production-rule smoke (X:\ 컴파일 산출물): `GLOVES NORMAL +1 attack contribution = 25`, `GLOVES NORMAL +2 attack contribution = 26`, `preview statIncrease.attack = 1`, `character fighter.attack increase without percent modifiers = 1`.
- 2026-09-11 웹 검증: `corepack pnpm --filter @hanjjak/web exec vitest run src/features/equipment/EquipmentScreen.test.tsx` 4/4 통과, `corepack pnpm --filter @hanjjak/web typecheck` 통과. 렌더링 HTML에서 접근 가능한 양의 증가량 라벨 `강화 시 +1`을 확인했고 `강화 시 +0`은 포함되지 않는다. 로컬 실제 앱 브라우저의 장갑 노말 +1→+2 실행 증거는 아래 별도로 기록한다.
- 2026-09-11 밸런스 검증 (X:\): `./gradlew.bat :apps:balance-lab:test :apps:balance-lab:run :apps:balance-lab:dungeonBalance -Pkotlin.incremental=false --rerun-tasks --no-daemon --no-configuration-cache --max-workers=1`가 `BUILD SUCCESSFUL`로 통과했다. 생성물은 `build/reports/balance/enemy-v1-working.csv`, `build/reports/pre-kafka-j11-validation.json`, `build/reports/balance/gem-dungeon-candidate.csv`다.
- 2026-09-11 적용 적 수치 대조: `docs/70-plans/mvp-release/verification/enemy-v1-applied.csv`는 변경하지 않았다. working 대 applied 차이는 `stage.01-03`/`01-04`/`01-09`/`03-04`/`03-08`/`04-01`/`04-03`/`04-08`의 p10/p50/p90 HP 각 ±1, `stage.01-07` deficient `57.0 → 38.6`, `stage.03-07` success `90.8 → 95.8`·deficient `3.4 → 0.9`, `stage.04-10` success `67.1 → 98.7`·deficient `33.0 → 86.9`이다. `stage.01-07`은 attainable Q19 보장 곡선 차이이며, `stage.03-07`/`stage.04-10` 차이는 abstract Q98/Q149와 후속 시뮬레이터가 포함된 stale applied baseline으로 장비 곡선 parity 변경에 귀속하지 않는다. 적용 적 수치 acceptance는 변경하지 않았다.
- 2026-09-11 HTTP/PostgreSQL 검증: Docker 29.6.2/Testcontainers가 사용 가능했고 `./gradlew.bat :apps:game-api:test --tests com.hanjjak.gameapi.CraftingMarketE2ETest --no-daemon --no-configuration-cache --max-workers=1`가 `BUILD SUCCESSFUL`, 3/3 통과(skipped/failures/errors 0)했다. 기존 flow에서 계정·장비 unlock/enhance·시장 정산을 실제 PostgreSQL로 통과시켰다.
- 2026-09-11 로컬 실제 앱 브라우저 검증: 기존 PostgreSQL 볼륨은 Flyway V15 체크섬 불일치가 있어 변경하지 않고, PostgreSQL 17.6 임시 컨테이너와 실제 `game-api`·Vite 웹을 연결했다. 신규 계정 가입·주력 재료 선택 후 장갑 노말 +1 상태에서 장비 화면이 `공격력 +25`, `강화 시 +1`을 표시하는 것을 확인했다. 강화 버튼 실행 후 장갑은 노말 +2·공격력 +26으로 바뀌고 다음 강화도 `+1`로 표시됐으며, 캐릭터 API의 최종 공격력은 103에서 104로 증가했다. 쌀 30과 세 재료 각 125의 차감도 장비 응답과 화면에 반영됐다.
- 따라서 강화·미리보기·캐릭터 계산·웹의 `+0` 결함과 HTTP/PostgreSQL 경로는 검증되었으나, 적용 적 baseline의 acceptance 차이가 남아 `부분 검증`을 유지한다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 완료 |
| 검증 | 부분 검증 |
