---
doc_kind: task
owner_domain: delivery
task_code: 'A-03'
task_area: 'A 배포 범위'
task_type: '데이터'
priority: 'P0'
target_chapters: '챕터 1, 챕터 2, 챕터 3, 챕터 4'
planning_status: '미확인'
development_status: '부분 구현'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/54806c8781b082b1bdb681c15556f603'
notion_id: '54806c87-81b0-82b1-bdb6-81c15556f603'
snapshot_date: '2026-08-28'
---

# A-03 챕터별 콘텐츠 매핑표 확정

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [제품 범위](../../../../70-plans/mvp-release/requirements.md)를 따른다.

## 완료 기준

40개 스테이지의 몬스터·보스·재료·장비·스킬 연결표가 완성된다.

## 선행 작업

A-01

## 비고

2026-09-10: 차기 정책 전환과 추가 인수는 [A-05](../A-release-scope/a-05-progression-rebalance.md)에서 추적한다. 이 문서의 기존 상태·증거는 기존 버전 범위에 한정하며 차기 구현 완료를 뜻하지 않는다.


챕터 1~4와 40개 스테이지의 ID, 적·보스 전투 수치, 보스 유형·패턴과 기준 스킬 토큰은 버전 콘텐츠로 입력됐다. 재료·장비·실제 스킬·주요 보상 연결표는 아직 완성되지 않았다.

## 증거 링크

- 콘텐츠: `packages/game-content/versions/v1/chapters/chapters.json`, `packages/game-content/versions/v1/stages/stages.json`
- 검증: `tools/content-validator/src/validate.ts`, [enemy-v1-applied 결과](../../../../70-plans/mvp-release/verification/enemy-v1-applied.csv)
- 2026-09-02 `pnpm content:validate`, `./gradlew.bat :apps:balance-lab:run` 및 기준 CSV 대조 통과.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 미확인 |
| 개발 | 부분 구현 |
| 검증 | 부분 검증 |
