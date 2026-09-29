---
doc_kind: task
owner_domain: delivery
task_code: 'F-05'
task_area: 'F 드롭·인벤토리'
task_type: '개발'
priority: 'P0'
target_chapters: '공통'
planning_status: '완료'
development_status: '진행 중'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/08306c8781b08260b63a01515dc33845'
notion_id: '08306c87-81b0-8260-b63a-01515dc33845'
snapshot_date: '2026-08-28'
---

# F-05 보상 지급 원자성·중복 방지 구현

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [아이템](../../../../30-domain/items/ssot.md)를 따른다.

## 완료 기준

전투 보상 지급이 실패하거나 재시도돼도 쌀과 아이템이 한 번만 반영된다.

## 선행 작업

B-05,

## 비고

2026-09-08 후속 중첩 정책 승인에 따라 개발을 진행 중으로 전환했다. 후속 구현에서 [아이템 SSOT](../../../../30-domain/items/ssot.md#인벤토리-슬롯과-수량)의 상한을 넘는 보상에 대한 추가 슬롯 계산·부분 지급·재시도 결과 보존을 반영했다. 마지막 슬롯까지 지급하고 초과량을 건너뛰는 경계 테스트가 통과했다. 아래 기존 검증 이력과 구분하며 장시간 전투·화면 연계 인수는 여전히 남아 있다.

2026-09-08 D3에 맞춰 전투 아이템 지급 건너뛰기와 결과 필드를 반영했다. 계정 행 잠금, payload fingerprint와 멱등 결과 재생을 유지한다. 전투 성장 보상은 `progression_reward_command`와 `wallet_balance`를 사용해 경험치·쌀 중복 지급을 막는다. 전체 전투 command 조회와 장시간 session·heartbeat 보상 확정은 아직 남아 있다.

## 증거 링크

- 2026-09-08 후속 중첩 검증: `InventoryServicesTest`의 마지막 슬롯 부분 지급·동일 요청 재생 테스트, `ChapterCombatE2ETest`의 만석 D3 검증 통과. game-api 전체 32건과 인벤토리·거래소·장비·치장 테스트 및 bootJar 성공.

- `apps/game-api/src/test/kotlin/com/hanjjak/gameapi/ChapterCombatE2ETest.kt`: 만석 상태에서 아이템 미지급·건너뜀 결과, 전투 성공·경험치·쌀 증가를 HTTP로 검증, 2026-09-08 통과.
- `packages/contracts/main.tsp`: skippedQuantity 추가, 기존 저장 결과의 discardedQuantity 호환 유지. 계약 재생성·전체 타입 검사 통과.
- 2026-09-08 `:apps:game-api:test` 및 인벤토리·거래소·치장 모듈 테스트 통과. 보석 개봉 실패 시 수량·보석·멱등 기록·상태 버전 무변경과 동일 요청 재생을 검증했다.
- 2026-09-10 몬스터별 아이템 지급: `DropTable.rollEnemy`의 적 순번별 독립 seed, 정산 manifest의 요청 보상 고정, `(battleSessionId, enemyIndex)` 유일성과 `settlementId` 내부 멱등 키를 적용했다. 일반 20+보스의 개별 결과 합산과 모듈 테스트가 통과했다. 실제 PostgreSQL 동시 정산 검증은 런타임 부재로 남아 있다.

- `modules/inventory/src/main/kotlin/com/hanjjak/inventory/application/InventoryRewardService.kt`
- `modules/inventory/src/main/kotlin/com/hanjjak/inventory/application/BattleRewardService.kt`
- `modules/inventory/src/main/kotlin/com/hanjjak/inventory/infrastructure/JdbcInventoryRepository.kt`
- `modules/battle/src/main/kotlin/com/hanjjak/battle/api/BattleController.kt`
- `modules/progression/src/main/kotlin/com/hanjjak/progression/application/ProgressionRewardService.kt`
- `modules/progression/src/main/kotlin/com/hanjjak/progression/infrastructure/JdbcProgressionRepository.kt`
- `modules/inventory/src/test/kotlin/com/hanjjak/inventory/application/InventoryServicesTest.kt`
- [주력 재료 구현 증거](../../../../70-plans/material-preference/implementation-evidence.md)

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 진행 중 |
| 검증 | 부분 검증 |
