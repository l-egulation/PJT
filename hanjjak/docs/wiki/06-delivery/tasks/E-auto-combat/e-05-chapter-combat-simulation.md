---
doc_kind: task
owner_domain: delivery
task_code: 'E-05'
task_area: 'E 자동전투·스탯'
task_type: '검증'
priority: 'P1'
target_chapters: '챕터 1, 챕터 2, 챕터 3, 챕터 4'
planning_status: '완료'
development_status: '완료'
verification_status: '완료'
deadline: ''
source_url: 'https://app.notion.com/p/a7f06c8781b08393985b8103a074d5be'
notion_id: 'a7f06c87-81b0-8393-985b-8103a074d5be'
snapshot_date: '2026-08-28'
---

# E-05 챕터별 전투 수치 시뮬레이션

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [전투](../../../../30-domain/combat/ssot.md)를 따른다.

## 완료 기준

챕터 1~4의 스테이지 공통 적 레벨·방어력과 공격·최대 HP·관통 3축 전투 수치를 사건 시뮬레이션하고, 기존 성장벽 실패 원인과 1~9 파밍·10 돌파 성격을 재현한다.

## 선행 작업

D-02, D-03, E-04

## 비고

2026-09-01 공통 적 레벨·방어력과 3스탯 전투 SSOT 반영. 구형 권장 레벨·명중 기준은 아래 2026-08-28 Notion 원문에만 보존한다.

## 증거 링크

- [enemy-v1-applied seed 1,000 검증 결과](../../../../70-plans/mvp-release/verification/enemy-v1-applied.csv)
- 실행 명령: `./gradlew :apps:balance-lab:run`

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 완료 |
| 검증 | 완료 |
