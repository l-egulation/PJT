---
doc_kind: task
owner_domain: delivery
task_code: 'D-06'
task_area: 'D 스테이지'
task_type: '검증'
priority: 'P0'
target_chapters: '챕터 1, 챕터 2, 챕터 3, 챕터 4'
planning_status: '미확인'
development_status: '부분 구현'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/d0206c8781b0838e9d1d01c6b2ac3c17'
notion_id: 'd0206c87-81b0-838e-9d1d-01c6b2ac3c17'
snapshot_date: '2026-08-28'
---

# D-06 챕터 1→2→3→4 해금·진행 검증

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [스테이지](../../../../30-domain/world/ssot.md)를 따른다.

## 완료 기준

1-10·2-10·3-10 최초 클리어 시 서버가 다음 챕터를 정확히 열고, 같은 결과 재처리에도 중복 해금하지 않으며, 4-10 완료 시 챕터 5를 만들지 않는다.

## 선행 작업

A-03, D-02, D-03,

## 비고

전투 요청에서 1-1을 제외한 스테이지는 직전 스테이지 최초 클리어를 요구하고 성공 결과를 계정별로 저장한다. 스테이지 조회는 직전 스테이지 클리어 기반으로 다음 스테이지 해금을 표시한다. 명시적 다음 챕터 해금, 동일 결과 재처리와 4-10 경계 검증은 남아 있다.

## 증거 링크

- 구현: `modules/battle/src/main/kotlin/com/hanjjak/battle/api/BattleController.kt`
- 저장: `apps/game-api/src/main/resources/db/migration/V1__account_and_progress.sql`
- 2026-09-02 빌드·단위 테스트는 통과했으나 이 작업의 챕터 해금 시나리오는 아직 실행하지 않았다.
- 2026-09-03 `feature/auto-battle-loop` API 스모크: fresh DB `hanjjak_auto_battle_loop`에서 회원가입→주력 재료 선택 뒤 `GET /api/v1/stages`가 1-1만 해금으로 반환, `POST /api/v1/battles/cycles` 1-1 성공 후 `GET /api/v1/stages`가 1-1 `clearCount=1`과 1-2 해금을 반환함을 확인. 챕터 1-10→2-1, 2-10→3-1, 3-10→4-1, 4-10 경계 검증은 남아 있다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 미확인 |
| 개발 | 부분 구현 |
| 검증 | 부분 검증 |
