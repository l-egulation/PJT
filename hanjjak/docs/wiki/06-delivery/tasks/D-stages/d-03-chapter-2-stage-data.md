---
doc_kind: task
owner_domain: delivery
task_code: 'D-03'
task_area: 'D 스테이지'
task_type: '데이터'
priority: 'P0'
target_chapters: '챕터 2'
planning_status: '미확인'
development_status: '완료'
verification_status: '완료'
deadline: ''
source_url: 'https://app.notion.com/p/80806c8781b082d8aafb815130c4ec69'
notion_id: '80806c87-81b0-82d8-aafb-815130c4ec69'
snapshot_date: '2026-08-28'
---

# D-03 챕터 2 스테이지 2-1\~2-10 데이터 작성

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [스테이지](../../../../30-domain/world/ssot.md)를 따른다.

## 완료 기준

2-1\~2-9 파밍과 2-10 돌파가 가능한 데이터가 입력·검토된다.

## 선행 작업

D-01

## 비고

2-1~2-10 전투 콘텐츠가 `enemy-v1-applied` 기준으로 입력됐고 기준 빌드 seed 1,000개 시뮬레이션으로 파밍·돌파 성격을 검증했다.

## 증거 링크

- 콘텐츠: `packages/game-content/versions/v1/stages/stages.json`
- [enemy-v1-applied seed 1,000 검증 결과](../../../../70-plans/mvp-release/verification/enemy-v1-applied.csv)
- 2026-09-02 `pnpm content:validate`, `./gradlew.bat :apps:balance-lab:run` 및 기준 CSV 대조 통과.
- 2026-09-10 쿠키 선물세트 프레젠테이션 배치 추가: `apps/web/public/assets/chapters/chapter-02-cookie-gift-set`, `apps/web/src/features/battle/battleVisuals.ts`, `packages/game-content/schema/stages.schema.json`, `packages/game-content/versions/v1/stages/stages.json`.
- 2026-09-10 챕터 2 스프라이트 10종의 시트·프레임·그리드 규격을 대조했고, `pnpm content:validate`, 웹 typecheck, 전체 185개 테스트와 production build를 통과했다. 로컬 미리보기에서 2-1·2-4·2-7·2-10의 일반 전투, 중간 보스 경고·전투, 최종 보스 경고·전투 배치를 확인했다.
- 2026-09-10 2-10 편성을 일반 몬스터 없이 `로열 어소트먼트 선물 골렘(royal-assortment-gift-golem)` 최종 보스 1체만 등장하도록 보정했다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 미확인 |
| 개발 | 완료 |
| 검증 | 완료 |
