---
doc_kind: task
owner_domain: delivery
task_code: 'H-02'
task_area: 'H 스킬·자동 사용'
task_type: '개발'
priority: 'P1'
target_chapters: '공통'
planning_status: '완료'
development_status: '완료'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/8d106c8781b08318bbf981eacdf08303'
notion_id: '8d106c87-81b0-8318-bbf9-81eacdf08303'
snapshot_date: '2026-08-28'
---

# H-02 스킬북 드롭·거래 속성 적용

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [스킬](../../../../30-domain/character/skills/ssot.md)를 따른다.

## 완료 기준

스킬북이 지정 확률로 드롭되고 미사용 스킬북을 거래할 수 있다.

## 선행 작업

H-01, I-01

## 비고

2026-09-09 현재 코드의 1~9스테이지 일반 몬스터 20회와 보스 1회, 총 21회 독립 0.5% 판정을 정책으로 확정했다. 스킬북 드롭과 거래 가능 아이템 정의를 구현했고, 드롭·인벤토리·거래소·스킬 성장 action이 동일 `itemId`와 `StaticItemCatalog` 표시명을 사용하도록 정합성을 맞췄다. 표시명은 `노말 한짝의 일격 비법서` 형식이며 전설 등급 카탈로그도 추가했다. 2026-09-10에는 스킬북 판매 등록·구매·남은 매물 회수·판매자 정산을 기존 거래소 트랜잭션 경계로 검증했다. 전설 5-1 접근 잠금과 스킬북 판매·구매 UI 검증은 남아 있다.

2026-09-11 핫픽스로 희귀 스킬북 최초 등장 챕터를 3에서 2로 앞당기고, 챕터 2~3 등급 비중을 노말:희귀 `2:1`로 맞췄다. 챕터 1 노말 100%, 전체 스킬북 판정 성공률 0.5%, 챕터 4 비중과 10스테이지 무드롭 규칙은 유지한다.

## 증거 링크

- 드롭: `modules/inventory/src/main/kotlin/com/hanjjak/inventory/domain/DropTable.kt`
- 2026-09-11 챕터 2 희귀 핫픽스: `DropTable.GRADE_CHAPTER`의 희귀 최초 등장 챕터와 `SKILL_GRADE_WEIGHTS`의 챕터 2 가중치를 함께 변경했다. 긴급 반영 요청에 따라 자동 테스트는 실행하지 않았고, 변경 상수와 diff만 확인했다.
- 카탈로그: `modules/inventory/src/main/kotlin/com/hanjjak/inventory/infrastructure/StaticItemCatalog.kt`
- 강화 action: `modules/skills/src/main/kotlin/com/hanjjak/skills/application/SkillService.kt`
- 표시명 SSOT: `docs/60-content/abilities/mvp-v1.md`
- 검증: ASCII 경로에서 `./gradlew.bat :modules:skills:test`와 `./gradlew.bat :modules:inventory:test --tests com.hanjjak.inventory.domain.DropTableTest`가 성공했다. 격리한 PostgreSQL·API·웹의 스킬 화면에서 서버가 반환한 스킬북 `itemId`와 최종 표시명을 확인했다. 스킬북 판매·구매 UI는 미검증이므로 부분 상태를 유지한다.
- 2026-09-10 스킬북 거래 백엔드: `MarketInventoryService`가 거래 가능 stack 검증을 판매 등록·구매 지급·회수에 공통 적용하고, `CraftingMarketE2ETest`가 PostgreSQL에서 스킬북 5개 중 3개 등록 → 2개 구매 → 잔여 1개 회수 → 수수료 제외 180쌀 정산을 검증했다. `:modules:inventory:test`, `:modules:market:test`, 해당 game-api E2E 2건이 통과했다. 보석 거래·시세 집계·UI는 이번 범위에서 변경하지 않았다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 완료 |
| 검증 | 부분 검증 |
