---
doc_kind: task
owner_domain: delivery
task_code: 'B-07'
task_area: 'B 계정·저장'
task_type: '개발'
priority: 'P0'
target_chapters: '공통'
planning_status: '미확인'
development_status: '완료'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/00206c8781b082c1958501cd75eb21ad'
notion_id: '00206c87-81b0-82c1-9585-01cd75eb21ad'
snapshot_date: '2026-08-28'
---

# B-07 브라우저 로컬 실행 상태 복원 구현

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [계정·저장](../../../../30-domain/player/ssot.md)를 따른다.

## 완료 기준

브라우저 로컬 저장소는 마지막 방치모드를 저장하고, 재진입 시 서버의 현재 스테이지와 조합해 경과 보상 없이 최대 HP·초기 전투 상태·0/20·새 체류 획득 세션으로 복원한다. 탭 discard·브라우저 종료·기기 절전·네트워크 단절은 경과 보상 없이 중단 처리한다.

## 선행 작업

B-02, B-05

## 비고

2026-09-01 서버 스테이지 진행 권한 결정에 따라 IndexedDB `hanjjak-runtime` 체크포인트를 이식했다. 방치모드는 보존하지만 재진입 시 서버 현재 스테이지·최대 HP·0/20·새 체류 세션으로 초기화하고 로컬 전투 상태로 경과 보상을 만들지 않는다. 실제 기기 절전·브라우저 프로세스 강제 종료 검증은 남아 있다.

## 증거 링크

- 구현: `apps/web/src/shared/persistence/runtimeCheckpointDb.ts`, `apps/web/src/features/runtime/reconcileRuntime.ts`, `apps/web/src/features/battle/runtimeStore.ts`
- 2026-09-08 검증: `reconcileRuntime.test.ts`에서 방치모드 보존, 서버 스테이지·최대 HP·0/20 복원, 콘텐츠 버전 불일치 체크포인트 폐기를 확인했다. 웹 전체 Vitest 39개, typecheck, build가 통과했다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 미확인 |
| 개발 | 완료 |
| 검증 | 부분 검증 |
