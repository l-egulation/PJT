---
doc_kind: task
owner_domain: delivery
task_code: 'J-10'
task_area: 'J 데이터·테스트·배포'
task_type: '검증'
priority: 'P0'
target_chapters: '공통'
planning_status: '미확인'
development_status: '미확인'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/f7906c8781b082658a0801afa1fc6744'
notion_id: 'f7906c87-81b0-8265-8a08-01afa1fc6744'
snapshot_date: '2026-08-28'
---

# J-10 운영 환경 배포 인수 테스트

> 작업 상태와 완료 증거의 SSOT. 이벤트 규칙은 [데이터·이벤트](../../../../40-systems/event-system/ssot.md), 운영 규칙은 [배포·운영](../../../../50-architecture/operations.md)을 따른다.

## 완료 기준

운영 환경에서 신규 계정으로 A-04 인수 시나리오 전체를 통과하고 증거 링크를 남긴다.

## 선행 작업

A-04, J-08, J-09

## 비고

Hyper-V Ubuntu VM·K3s·Cloudflare route의 운영 인프라 인수와 외부 rolling-update 연속성을 확인했다. 다만 신규 계정 A-04 전체 인수, 인증 세션 유지, web asset 404 부재, application rollback, PostgreSQL restore는 완료 기준 전체를 충족하지 않았으므로 검증 상태는 `부분 검증`으로 유지한다.

## 증거 링크

- 준비된 검증 경로: `infra/k8s/verify.sh`, `smoke-pod.yaml`, `deploy.sh`, `rollback.sh`, `restore-postgres.sh`
- 2026-09-08 로컬에는 K3s kubeconfig가 없어 server-side dry-run과 실제 rollout 인수는 수행하지 못했다. Bash syntax, GitLab CI lint, Registry Compose config만 통과했다.
- 2026-09-09 Registry 기반 설계 검토 기록은 당시 이력이다. 현재 운영 기준은 [로컬 이미지 직접 build](../../../../50-architecture/operations.md#배포-서버-로컬-이미지)다.
- 2026-09-09 이후 격리 K3s에서 수행한 배포·연속 Service 요청·실패 복구·Registry 보존 결과는 [J-08](./j-08-production-build-cicd.md), DB 복원·writer 차단 결과는 [J-09](./j-09-monitoring-backup-rollback.md)에 기록했다. 실제 Hyper-V VM·GitLab Runner·Cloudflare 경로의 인수가 아니므로 이 작업의 개발·검증 상태는 `미확인`을 유지한다.
- 2026-09-09 실제 운영 hostname에서 K3s deploy를 실행하며 `/healthz`를 연속 호출했다. backup·migration Job, game-api rolling update, web blue→green selector 전환을 포함한 2분 동안 61회 모두 HTTP 200, 실패 0회였다. 기존 Compose tunnel을 중지한 뒤 K3s cloudflared만으로 추가 50회 모두 HTTP 200을 확인했다.
- 최종 운영 상태는 game-api 2/2, web-green 2/2, cloudflared 2/2, PostgreSQL 1/1 Ready이며 web Service는 `green`, release annotation은 `rollout-proof-4-20260909`다. NodePort·LoadBalancer·Ingress는 생성하지 않았고 Windows에서 K3s API·Registry forward는 loopback에만 bind했다.
- 초기 전환 검증 중 Compose와 K3s에 쓰기가 갈린 이력이 있었으나 최종 전환에서는 Compose writer를 먼저 중지하고 새 dump를 생성했다. 최종 source·target 57개 public table row count가 모두 일치했으므로 이전 분기 데이터 경고는 해소됐다.

## 로컬 이미지 직접 build 인수에 추가할 증거

- [J-08](./j-08-production-build-cicd.md)의 K3s containerd direct builder와 protected `main` pipeline의 수동 승인 deploy 경로를 확인한다. 과거 자동 deploy 성공 기록은 당시 이력으로 보존한다.
- 2026-09-09 실제 전환 완료: Compose application writer 중지→최종 custom-format backup→K3s PostgreSQL restore→V38 migration→game-api·admin-console·web·cloudflared 기동 순서로 수행했다. `/healthz`, `/`, `/admin/`, `/api/v1/auth/session`은 200, 비인증 `/api/v1/stages`와 `/api/admin/v1/auth/session`은 예상대로 401이었다. Compose PostgreSQL까지 중지한 뒤에도 `/healthz` 200을 확인했다.
- 최종 운영 상태는 game-api 2/2, web-blue 2/2, admin-console 2/2, cloudflared 2/2, PostgreSQL 1/1이며 Service annotation은 `k3s-migration-9c74938-v2`다. 실제 사용자 요청에서 인증 세션, 거래소 조회·등록, 게임 session heartbeat가 200으로 기록됐다.
- 2026-09-10 Hyper-V VM 재부팅 뒤 stale Windows portproxy·UFW 허용 주소로 `TLS handshake timeout`이 발생한 pipeline `#186211`을 재현했다. `production-k3s` Runner를 K3s VM으로 이전해 host network의 `127.0.0.1:6443` API를 사용하도록 한 뒤 같은 pipeline 재시도 job `505505`가 성공했다. backup·migration·game-api·admin-console rolling update·web blue-green 전환 뒤 commit `066b0d03` workload가 Ready였고 외부 `/healthz`는 HTTP 200이었다.
- 2026-09-10 `deploy-production`을 수동 승인 job으로 전환했다. 이번 Kafka 변경 병합에서는 해당 job을 실행하지 않으며, 실제 수동 승인 deploy 인수는 후속 작업으로 남긴다.
- 2026-09-10 pipeline `#187491`, deploy job `509468`이 main commit `7b208b00e226495cbcea609da74ee128897a406e`을 운영 web-green slot에 배포했다. 배포 후 `https://hanjjak.verte.kr/`와 `/api/v1/auth/session`은 HTTP 200이었고 브라우저에서 로그인 화면의 실제 콘텐츠를 확인했다. 루트 응답은 0.79초였으며 favicon 404 한 건 외에 페이지 렌더를 막는 오류는 없었다.
- 2026-09-13 pipeline [`#191310`](https://lab.ssafy.com/s15-bigdata-dist-sub1/S15P21B107/-/pipelines/191310)의 수동 `deploy-production` job [`#523625`](https://lab.ssafy.com/s15-bigdata-dist-sub1/S15P21B107/-/jobs/523625)이 `main` commit `1a359b8def3213283a95cc07e58b2f745f79195f`의 K3s 롤아웃을 성공했다. 외부 `/`, `/healthz`, `/api/v1/auth/session`은 각각 HTTP 200이었고 루트 응답은 0.21초였다. 로그인 이후 사용자 시나리오와 정적 asset 404 검사는 이번 확인 범위에 포함하지 않아 검증 상태는 `부분 검증`으로 유지한다.
- 2026-09-13 pipeline [`#191327`](https://lab.ssafy.com/s15-bigdata-dist-sub1/S15P21B107/-/pipelines/191327)의 수동 `deploy-production` job [`#523702`](https://lab.ssafy.com/s15-bigdata-dist-sub1/S15P21B107/-/jobs/523702)이 `main` commit `634d64f8026f3cd7743983bc342012abc4d5932f`의 K3s 롤아웃을 성공했다. 외부 `/`, `/healthz`, `/api/v1/auth/session`은 각각 HTTP 200이었고 응답 시간은 0.32초·0.37초·0.25초였다. 로그인 첫 화면의 브라우저 콘솔 경고·오류는 없었지만 인증 이후 사용자 흐름은 이번 확인 범위에 포함하지 않아 검증 상태는 `부분 검증`으로 유지한다.
- 2026-09-13 pipeline [`#191344`](https://lab.ssafy.com/s15-bigdata-dist-sub1/S15P21B107/-/pipelines/191344)의 `deploy-production` job [`#523782`](https://lab.ssafy.com/s15-bigdata-dist-sub1/S15P21B107/-/jobs/523782)이 최신 `main` commit `423051c9c6aba038ccc9001bea9607b9ecd3969f`를 운영 web-green slot에 배포했다. 외부 `/`, `/healthz`, `/api/v1/auth/session`은 각각 HTTP 200이었고 응답 시간은 0.22초·0.36초·0.22초였다. 로그인 첫 화면의 브라우저 콘솔 경고·오류는 없었지만 인증 이후 사용자 흐름은 이번 확인 범위에 포함하지 않아 검증 상태는 `부분 검증`으로 유지한다.
- 2026-09-13 pipeline [`#191390`](https://lab.ssafy.com/s15-bigdata-dist-sub1/S15P21B107/-/pipelines/191390)의 수동 `deploy-production` job [`#523993`](https://lab.ssafy.com/s15-bigdata-dist-sub1/S15P21B107/-/jobs/523993)이 merge commit `fe1840ab39a3e9b8a5242173873a0b1c4238b9dc`를 운영 web-blue slot에 배포했다. 외부 `/`, `/healthz`, `/api/v1/auth/session`은 모두 HTTP 200이었고 `/healthz`는 `ok`, 비로그인 세션 응답은 `{"authenticated":false}`였다. 루트 HTML이 해당 빌드의 `index-C95P80dl.js`와 `index-DckmkGH_.css`를 참조했고 로그인 첫 화면의 브라우저 콘솔 경고·오류는 없었다. 인증 이후 사용자 흐름은 이번 확인 범위에 포함하지 않아 검증 상태는 `부분 검증`으로 유지한다.
- 2026-09-13 pipeline [`#191667`](https://lab.ssafy.com/s15-bigdata-dist-sub1/S15P21B107/-/pipelines/191667)의 수동 `deploy-production` job [`#525205`](https://lab.ssafy.com/s15-bigdata-dist-sub1/S15P21B107/-/jobs/525205)은 commit `8826cdaadad91af3dfa1f2a8ca658cb0559ddda9`의 application/web 롤아웃과 외부 smoke를 완료했지만 후속 progression backfill의 PostgreSQL 연결 거부로 최종 실패했다. 실패 직후 외부 `/`, `/healthz`, `/api/v1/auth/session`은 모두 HTTP 200이어서 가용성은 유지됐으나, backfill 완료와 인증 이후 사용자 흐름은 검증하지 못했으므로 `부분 검증` 상태를 유지한다.
- 인증 세션 유지, web asset 404, application rollback, 별도 disk backup·restore와 A-04 전체 사용자 인수를 추가한다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 미확인 |
| 개발 | 미확인 |
| 검증 | 부분 검증 |
