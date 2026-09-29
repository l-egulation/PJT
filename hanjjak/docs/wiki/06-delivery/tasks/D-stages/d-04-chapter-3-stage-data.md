---
doc_kind: task
owner_domain: delivery
task_code: 'D-04'
task_area: 'D 스테이지'
task_type: '데이터'
priority: 'P0'
target_chapters: '챕터 3'
planning_status: '미확인'
development_status: '완료'
verification_status: '완료'
deadline: ''
source_url: 'https://app.notion.com/p/de306c8781b082c18c1f816eef63d69b'
notion_id: 'de306c87-81b0-82c1-8c1f-816eef63d69b'
snapshot_date: '2026-08-28'
---

# D-04 챕터 3 스테이지 3-1\~3-10 데이터 작성

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [스테이지](../../../../30-domain/world/ssot.md)를 따른다.

## 완료 기준

3-1 명중 입문과 3-10 명중 장벽을 포함한 데이터가 입력·검토된다.

## 선행 작업

D-01

## 비고

3-1~3-10 전투 콘텐츠가 현재 전투 SSOT의 공격·최대 HP·관통 성장축과 `enemy-v1-applied` 기준으로 입력됐고 seed 1,000개 시뮬레이션으로 성장벽을 검증했다. 구형 명중 기준은 적용하지 않는다.

## 증거 링크

- 콘텐츠: `packages/game-content/versions/v1/stages/stages.json`
- [enemy-v1-applied seed 1,000 검증 결과](../../../../70-plans/mvp-release/verification/enemy-v1-applied.csv)
- 2026-09-02 `pnpm content:validate`, `./gradlew.bat :apps:balance-lab:run` 및 기준 CSV 대조 통과.
- 2026-09-10 프레젠테이션 배치: `packages/game-content/versions/v1/stages/stages.json`에 3-1~3-10의 스시 일반몹·보스 편성과 시간대별 스시집 배경 ID를 추가했다. 바이너리 에셋은 `apps/web/public/assets/chapters/chapter-03-sushi`에 보존하며 `pnpm content:validate`를 통과했다.
- 2026-09-10 3-10 편성을 일반 몬스터 없이 `후토마끼 왕(futomaki-king)` 최종 보스 1체만 등장하도록 보정했다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 미확인 |
| 개발 | 완료 |
| 검증 | 완료 |
