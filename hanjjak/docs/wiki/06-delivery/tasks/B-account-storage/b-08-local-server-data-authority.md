---
doc_kind: task
owner_domain: delivery
task_code: 'B-08'
task_area: 'B 계정·저장'
task_type: '개발'
priority: 'P0'
target_chapters: '공통'
planning_status: '미확인'
development_status: '부분 구현'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/88506c8781b0836183e7813fa8092930'
notion_id: '88506c87-81b0-8361-83e7-813fa8092930'
snapshot_date: '2026-08-28'
---

# B-08 서버 스테이지 진행·거래 권한과 로컬 성장·실행 상태 분리 구현

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [계정·저장](../../../../30-domain/player/ssot.md)를 따른다.

## 완료 기준

스테이지 진행과 거래 자산은 서버가 최종 판정하고, 로컬은 비거래 성장·방치모드·실행 체크포인트·화면 캐시를 소유하며 서버 장애·재시도에도 클리어·해금·서버 권한 보상·자산을 임의 확정하지 않는다.

## 선행 작업

B-03, B-05, I-01

## 비고

계정·전체 스테이지 진행·반복 대상·최근 전투 기록·거래 자산·치장 재화와 보유 상태는 game-api와 PostgreSQL이 최종 판정한다. 웹은 IndexedDB에 현재 방치모드와 실행 체크포인트를 보존하고 서버 `runtime-state`의 현재 스테이지·반복 대상·최대 HP와 조합해 복원한다. 최근 전투 기록은 로컬에 복제하지 않고 인증 API에서 조회한다. 장시간 자동전투 장애 재조정은 남아 있다.

## 증거 링크

- 서버 저장 경계: `apps/game-api/src/main/resources/db/migration/V28__battle_repeat_target.sql`, `modules/battle/src/main/kotlin/com/hanjjak/battle/application/BattleModeService.kt`, `modules/account`, `modules/stage`, `modules/cosmetics`
- 로컬 실행 경계: `apps/web/src/shared/persistence/runtimeCheckpointDb.ts`, `apps/web/src/features/runtime/reconcileRuntime.ts`, `apps/web/src/features/battle/runtimeStore.ts`
- 서버 복원 조회: `modules/battle/src/main/kotlin/com/hanjjak/battle/api/RuntimeStateController.kt`
- 2026-09-09 PostgreSQL 통합 검증에서 반복 대상 서버 저장·재접속 조회와 전투 완료 시 서버 확정 다음 스테이지 적용을 확인했다. 웹 회귀 79개와 실제 브라우저에서 자동 진행→1-1 반복 전환·서버 `runtime-state` 일치를 확인했다.
- 2026-09-02 전체 빌드·단위 테스트는 통과했으나 최근 전투 기록과 장시간 장애 복구 시나리오는 실행하지 않았다.
- 2026-09-09 이슈 #4 구현은 `battle_history_event`를 서버 권한 기록으로 추가하고, `GET /api/v1/battle-history`와 `GET /api/v1/battle-history/stages/{stageId}/latest-failure`만으로 웹의 전체 기록·스테이지 실패 상태를 구성한다. IndexedDB·LocalStorage에는 전투 이력을 추가하지 않았다.
- 2026-09-09 PostgreSQL 17 `BattleHistoryE2ETest`에서 다른 계정 기록 미노출, 완료 재요청의 단일 사건 수렴, 최신 50건 보존과 로그아웃 후 사용 가능한 서버 영속 조회 경계를 검증했다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 미확인 |
| 개발 | 부분 구현 |
| 검증 | 부분 검증 |
