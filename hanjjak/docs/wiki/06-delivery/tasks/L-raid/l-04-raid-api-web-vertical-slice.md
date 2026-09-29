---
doc_kind: task
owner_domain: delivery
task_code: 'L-04'
task_area: 'L 레이드'
task_type: '개발'
priority: 'P0'
target_chapters: '공통'
planning_status: '완료'
development_status: '부분 구현'
verification_status: '부분 검증'
deadline: ''
source_url: ''
notion_id: ''
snapshot_date: '2026-09-13'
---

# L-04 API·웹 수직 슬라이스 구현

> 작업 상태와 완료 증거의 SSOT. 게임 규칙은 [레이드 SSOT](../../../../30-domain/raid/ssot.md)를 따른다.

## 완료 기준

TypeSpec 계약, game-api, 레이드 메인·개인 전투·순위·미수령 보상 웹 흐름을 연결하고 실제 PostgreSQL·브라우저에서 전체 도전을 검증한다.

## 선행 작업

L-01, L-02, L-03, K-03, K-15

## 증거 링크

- 승인 설계: `docs/superpowers/specs/2026-09-13-daily-seal-raid-design.md`
- 구현 계획: `docs/superpowers/plans/2026-09-13-daily-seal-raid.md`
- TypeSpec 계약: `packages/contracts/raid.tsp`, `packages/contracts/main.tsp`, 생성 OpenAPI `packages/contracts/generated/openapi/openapi.yaml`
- 계약 검증: `corepack pnpm --filter @hanjjak/contracts typecheck`, `corepack pnpm --filter @hanjjak/contracts build`, `corepack pnpm --filter @hanjjak/contracts test`
- Task 9 whole-branch remediation: ranking accepts optional `sessionId` for immutable settled final ranks and defaults to current OPEN; all reachable raid raw failures are normalized to declared `RaidErrorCode` values. Docker/PostgreSQL HTTP runtime remains unavailable, so this evidence is compile-only.
- Task 11 deterministic replay moved to enabled non-Spring `RaidLongRunIntegrationTest` and executes 1,000 seeds; database load probes remain Docker-gated. L-04 stays partial verification.

- 2026-09-14 서버 반영 시도: 최신 `main` 동기화 후 레이드 migration 버전 충돌을 확인해 V56/V57/V58로 재번호화하고 `main`에 반영했다. 로컬 JVM·계약·콘텐츠·웹 검증은 통과했다. 현재 `main` pipeline은 필수 verify Runner 미할당으로 `pending`이며 image·deploy job은 시작하지 않았다. 레이드 콘텐츠는 `working`, UI는 잠금 상태를 유지한다.


## 상태

웹 수직 슬라이스는 구현·부분 검증 상태다. 실제 PostgreSQL과 브라우저를 통한 가입·해금·정산·수령 전체 흐름은 로컬 Docker 런타임을 확보한 뒤 검증한다.
