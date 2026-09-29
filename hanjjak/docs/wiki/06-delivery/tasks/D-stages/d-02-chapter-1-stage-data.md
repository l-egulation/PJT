---
doc_kind: task
owner_domain: delivery
task_code: 'D-02'
task_area: 'D 스테이지'
task_type: '데이터'
priority: 'P0'
target_chapters: '챕터 1'
planning_status: '미확인'
development_status: '완료'
verification_status: '완료'
deadline: ''
source_url: 'https://app.notion.com/p/db106c8781b083dca15f81209204e56b'
notion_id: 'db106c87-81b0-83dc-a15f-81209204e56b'
snapshot_date: '2026-08-28'
---

# D-02 챕터 1 스테이지 1-1\~1-10 데이터 작성

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [스테이지](../../../../30-domain/world/ssot.md)를 따른다.

## 완료 기준

1-1\~1-9 파밍과 1-10 돌파가 가능한 데이터가 입력·검토된다.

## 선행 작업

D-01

## 비고

1-1~1-10 전투 콘텐츠가 `enemy-v1-applied` 기준으로 입력됐고 기준 빌드 seed 1,000개 시뮬레이션으로 파밍·돌파 성격을 검증했다. 각 스테이지의 성심당 일반·보스 외형과 튀소정거장·성심당 매장·메아리곳간 배경 ID도 같은 콘텐츠에 입력했다.

## 증거 링크

- 콘텐츠: `packages/game-content/versions/v1/stages/stages.json`
- [enemy-v1-applied seed 1,000 검증 결과](../../../../70-plans/mvp-release/verification/enemy-v1-applied.csv)
- 2026-09-02 `pnpm content:validate`, `./gradlew.bat :apps:balance-lab:run` 및 기준 CSV 대조 통과.
- 2026-09-09 챕터 1 초밥 프레젠테이션 배치 추가 후 `pnpm content:validate`와 로컬 1-1 브라우저 스모크 통과.
- 2026-09-10 챕터 1 성심당 프레젠테이션으로 교체: `apps/web/public/assets/chapters/chapter-01-sungsimdang`, `apps/web/src/features/battle/battleVisuals.ts`, `packages/game-content/schema/stages.schema.json`, `packages/game-content/versions/v1/stages/stages.json`.
- 2026-09-10 `pnpm content:validate`, content-validator 14개 테스트, 웹 typecheck·184개 테스트·production build 통과. 로컬 1-1·1-4·1-7 배경과 순수롤·딸기 시루 보스 경고 스모크 확인.
- 2026-09-10 성심당 일반 몬스터 6종 시트와 메타데이터를 `seongsimdang-mobs-v1` 후처리본으로 교체하고, 패키지 매니페스트의 SHA-256·`4×5` 그리드·`192×192` 셀 규격 일치를 확인했다.
- 2026-09-10 후처리본 교체 후 `pnpm content:validate`, 웹 typecheck·184개 테스트·production build를 통과하고 로컬 1-1 전투 화면에서 튀김소보로 렌더링을 확인했다.
- 2026-09-10 1-10 편성을 일반 몬스터 없이 `딸기 시루(strawberry-siru)` 최종 보스 1체만 등장하도록 보정했다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 미확인 |
| 개발 | 완료 |
| 검증 | 완료 |
