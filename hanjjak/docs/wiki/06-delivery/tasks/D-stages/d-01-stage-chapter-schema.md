---
doc_kind: task
owner_domain: delivery
task_code: 'D-01'
task_area: 'D 스테이지'
task_type: '데이터'
priority: 'P0'
target_chapters: '공통'
planning_status: '미확인'
development_status: '부분 구현'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/1d106c8781b082419dcf019b13c9c994'
notion_id: '1d106c87-81b0-8241-9dcf-019b13c9c994'
snapshot_date: '2026-08-28'
---

# D-01 스테이지·챕터 데이터 스키마 정의

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [스테이지](../../../../30-domain/world/ssot.md)를 따른다.

## 완료 기준

챕터, 스테이지, 표시 이름, 스테이지 공통 적 레벨·방어력, 몬스터, 보스 패턴, 주요 보상, 해금 조건과 콘텐츠 버전을 데이터로 관리한다.

## 선행 작업

A-03

## 비고

챕터·스테이지 ID, 번호, 공통 적 레벨·방어력, 일반 적·보스 전투 수치, 보스 유형·패턴, 콘텐츠 버전과 화면용 일반 몬스터·보스·배경 ID는 JSON·JSON Schema로 구현됐다. 표시 이름, 주요 보상과 해금 조건은 아직 스키마에 없다.

## 증거 링크

- 스키마·콘텐츠: `packages/game-content/schema/stages.schema.json`, `packages/game-content/versions/v1/chapters/chapters.json`, `packages/game-content/versions/v1/stages/stages.json`
- 검증: `tools/content-validator/src/validate.ts`, `tools/content-validator/src/validate.test.ts`
- 2026-09-02 `pnpm content:validate`, `pnpm test` 통과.
- 2026-09-09 `normalMonsterIds`·`bossMonsterId`·`backgroundId` 추가 후 `pnpm content:validate` 통과.
- 2026-09-10 성심당 배경 ID 허용 목록으로 갱신 후 `pnpm content:validate` 및 content-validator 14개 테스트 통과.
- 2026-09-10 10스테이지의 `normalMonsterIds: []`를 허용하고 1~9스테이지의 최소 1종 조건을 유지하는 조건부 스키마로 보정했다. `pnpm content:validate` 통과.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 미확인 |
| 개발 | 부분 구현 |
| 검증 | 부분 검증 |
