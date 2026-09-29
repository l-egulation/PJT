---
doc_kind: task
owner_domain: delivery
task_code: 'H-01'
task_area: 'H 스킬·자동 사용'
task_type: '데이터'
priority: 'P1'
target_chapters: '챕터 1, 챕터 2, 챕터 3'
planning_status: '완료'
development_status: '완료'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/1fd06c8781b08336b78b813ce269bf40'
notion_id: '1fd06c87-81b0-8336-b78b-813ce269bf40'
snapshot_date: '2026-08-28'
---

# H-01 챕터 1~4 스킬·스킬북 목록 확정

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [스킬](../../../../30-domain/character/skills/ssot.md)를 따른다.

## 완료 기준

스킬 효과, 수치, 쿨타임, 전용 스킬북과 드롭 스테이지가 정의된다.

## 선행 작업

A-03,

## 비고

2026-09-09 스킬 6종·4등급 효과, 등급별 고정 쌀 비용, 복합 스킬북 조합, 스킬북 드롭 구간과 전설 접근 잠금을 버전 콘텐츠와 서버 액션 계약에 연결했다. 표시명은 `한짝의 일격`, `마! 쫄이나`, `잘게 더 잘게!`, `화력 최대로!`, `치명타`, `모든 피해`로 확정했고, MVP 전설 승급은 서버가 `LOCKED` 액션으로 반환한다.

## 증거 링크

- 코드: `modules/skills/src/main/kotlin/com/hanjjak/skills/domain/Skills.kt`, `modules/inventory/src/main/kotlin/com/hanjjak/inventory/infrastructure/StaticItemCatalog.kt`
- 계약: `packages/contracts/skills.tsp`
- 검증: `corepack pnpm --filter @hanjjak/contracts build`, `./gradlew.bat test`
- 2026-09-10 표시명 갱신: 사용자 승인에 따라 `active_dot` 표시명을 `마! 쫄이나`로 변경하고 버전 콘텐츠, 스킬 화면, 전투·보석 던전 HUD와 스킬북 이름에 반영했다. 안정 스킬 ID, 효과 수치, 성장 상태와 재사용시간은 변경하지 않았다. 정본은 [MVP 스킬 표시 콘텐츠 v1](../../../../60-content/abilities/mvp-v1.md)이다.
- 2026-09-10 표시명 회귀 검증: 웹 Vitest 44개 파일·227개 테스트, TypeScript typecheck, Vite production build, `content:validate`와 Gradle `:modules:inventory:test`, `:modules:skills:test`를 통과했다. 변경된 콘텐츠 파일의 SHA-256도 버전 manifest에 동기화했다.
- 2026-09-10 렌더링 확인: 로컬 `skills-preview.html`에서 스킬 카드 제목·아이콘 대체 텍스트와 자동 사용 2번 슬롯의 접근성 이름이 모두 `마! 쫄이나`로 표시되는 것을 확인했다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 완료 |
| 검증 | 완료 |
