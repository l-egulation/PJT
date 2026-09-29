# Docker·Cloudflare 배포 계획

운영 기준은 [배포·운영 SSOT](../../50-architecture/operations.md), 실행 상태와 증거는 [J-08](../../wiki/06-delivery/tasks/J-delivery-operations/j-08-production-build-cicd.md)·[J-09](../../wiki/06-delivery/tasks/J-delivery-operations/j-09-monitoring-backup-rollback.md)·[J-10](../../wiki/06-delivery/tasks/J-delivery-operations/j-10-production-acceptance-test.md)이 소유한다.

## 범위

- 이미지 공급 범위와 실행 위치는 [배포 서버 로컬 이미지](../../50-architecture/operations.md#배포-서버-로컬-이미지)를 따른다. 이 계획은 같은 VM의 BuildKit이 K3s containerd에 image를 직접 빌드하는 경로를 다룬다.
- protected `production-k3s` Runner는 namespace 제한 kubeconfig를 사용하고 `deploy-production` 수동 승인 뒤 K3s에 배포한다.
- 배포·복구 흐름은 [운영 SSOT](../../50-architecture/operations.md#ci검증-기준)를 따르며 전용 Flyway 실행과 bootstrap/live 전환 분리를 검증한다.
- game-api는 가용 replica를 유지하고 web은 inactive slot 전체가 ready인 경우에만 Service selector를 바꾼다.
- Cloudflare remotely-managed Tunnel은 ClusterIP `web` Service만 외부에 공개한다.
- application rollback과 PostgreSQL restore는 분리하며 restore는 명시적 확인 뒤에만 실행한다.
- K3s 인수 완료 전까지 기존 Compose 운영 환경은 중지 가능한 복구 경로로 보존한다.

서버 등록, protected Runner·branch 설정, 운영 환경·secret·backup 경로, Cloudflare hostname route, 실제 운영 인수는 저장소 밖 작업이다. 해당 완료 여부는 작업 문서에서만 갱신한다.

## 구현 경로

- `.gitlab-ci.yml`
- `apps/*/Dockerfile`
- `apps/game-api/src/main/resources/application.yml`
- `apps/game-api/src/main/resources/db/migration/V31__distributed_rate_limit.sql`
- `modules/account/src/main/kotlin/com/hanjjak/account/api/RequestRateLimiter.kt`
- `infra/k8s/base.yaml`, `migration-job.yaml`, `smoke-pod.yaml`
- `infra/k8s/deploy.sh`, `deployment-common.sh`, `maintenance-lock.sh`, `rollback.sh`, `restore-postgres.sh`, `verify.sh`, `render.py`
- `infra/k8s/bootstrap-host.sh`, `bootstrap-image-builder.sh`, `build-local-image.sh`

## 호스트 준비

Hyper-V Ubuntu VM의 K3s bootstrap 검토 대상은 `infra/k8s/bootstrap-host.sh`다. `bootstrap-image-builder.sh`가 K3s containerd worker를 쓰는 BuildKit daemon과 nerdctl을 고정 버전·checksum으로 준비하고, `build-local-image.sh`가 application image를 K3s `k8s.io` namespace에 직접 빌드한다.

CI Runner 배치·BuildKit·K3s containerd 직접 build는 [J-08](../../wiki/06-delivery/tasks/J-delivery-operations/j-08-production-build-cicd.md), rollback image 보존과 복구 검증은 [J-09](../../wiki/06-delivery/tasks/J-delivery-operations/j-09-monitoring-backup-rollback.md), 실제 배포 서버 인수는 [J-10](../../wiki/06-delivery/tasks/J-delivery-operations/j-10-production-acceptance-test.md)에서 추적한다.

## 최초 적용

VM에 Bash·kubectl·curl·jq·Python 3·PyYAML·K3s를 준비하고 image builder를 한 번 bootstrap한다. 최초 적용 전에 고정 지원 이미지 PostgreSQL·cloudflared·curl을 K3s containerd에 확보한다. application image는 중간 archive 없이 다음처럼 직접 빌드한다.

```bash
export IMAGE_TAG='검증한-commit-SHA'
bash infra/k8s/bootstrap-image-builder.sh
IMAGE_NAME=game-api DOCKERFILE=apps/game-api/Dockerfile bash infra/k8s/build-local-image.sh
IMAGE_NAME=web DOCKERFILE=apps/web/Dockerfile bash infra/k8s/build-local-image.sh
python3 infra/k8s/render.py --input infra/k8s/base.yaml --image-tag "$IMAGE_TAG" | kubectl apply -f -
```

최초 bootstrap과 manifest 구조 변경은 관리자 kubeconfig로 렌더된 `base.yaml`을 적용한다. raw manifest의 `bootstrap` tag는 배포 이미지가 아니다. 이후 CI에는 namespace deployer kubeconfig만 제공한다. `deploy.sh`는 bootstrap 리소스가 없으면 실패하며 active blue/green selector·replica를 기본 manifest 값으로 되돌리지 않는다. `/var/backups/hanjjak`의 별도 디스크 mount와 쓰기 권한은 운영 환경에서 확인한다.

## 배포·복구

필수 CI 변수는 `PUBLIC_ORIGIN`, `EXTERNAL_SMOKE_URL`, `POSTGRES_DB`, `POSTGRES_USER`다. production runner kubeconfig는 read-only host mount로 제공한다.

Application rollback은 `bash infra/k8s/rollback.sh`를 사용하고 `ROLLBACK_IMAGE_TAG`와 `EXTERNAL_SMOKE_URL`을 제공한다. DB restore는 관리자 권한으로 `RESTORE_CONFIRM=YES bash infra/k8s/restore-postgres.sh <dump>`를 유지보수 창에서 실행한다. 성공 뒤에도 앱과 backup CronJob은 재개되지 않는다. 데이터 확인 후 승인된 배포를 명시적으로 실행하고 필요할 때 `kubectl -n hanjjak patch cronjob postgres-backup --type=merge -p '{"spec":{"suspend":false}}'`로 백업을 재개한다. 복구 정책은 [운영 SSOT](../../50-architecture/operations.md#ci검증-기준)를 따른다.

## 검증과 한계

`IMAGE_TAG=<검증한-SHA> bash infra/k8s/verify.sh`는 관리자 kubeconfig로 렌더된 manifest server-side dry-run과 impersonation 기반 deployer RBAC를 검사한다. 실제 deployer 자격증명으로 `deploy.sh`를 실행하는 검증도 별도로 필요하다. 로컬 실행 결과는 [J-08](../../wiki/06-delivery/tasks/J-delivery-operations/j-08-production-build-cicd.md)·[J-09](../../wiki/06-delivery/tasks/J-delivery-operations/j-09-monitoring-backup-rollback.md)에 기록한다. 운영 인수에서는 연속 외부 요청, 로그인 세션 유지, 이전 web asset 접근, 실패 readiness 차단, rollback·restore를 실제 환경에서 검증한다.

단일 노드 K3s는 프로세스 교체 중 서비스 연속성만 제공한다. Windows host, Ubuntu VM, 단일 K3s node, 가상 디스크 장애에는 고가용성이 없다. Compose 리소스는 K3s 인수가 끝날 때까지 제거하지 않는다.
