# Docker·GitLab CI/CD·Cloudflare Tunnel 임시 작업 계획

> 임시 실행 계획. 현재 구현 기준은 `docs/70-plans/docker-cloudflare-deployment/README.md`와 `docs/50-architecture/operations.md`를 따른다. 실제 운영 인수 후 삭제한다.

## 목표

GitLab CI/CD에서 애플리케이션 이미지를 빌드하고 Registry 없이 운영 서버의 Docker daemon에 commit SHA 태그로 남긴 뒤 Docker Compose로 배포한다. 외부 공개는 Cloudflare Tunnel을 사용한다.

```text
GitLab push
  -> verify
  -> container image build on the production Docker host
  -> local SHA-tagged image
  -> protected automatic deploy job on the same Docker host
  -> PostgreSQL backup
  -> Docker Compose deploy
  -> readiness/smoke
  -> Cloudflare Tunnel -> web -> game-api -> PostgreSQL
```

이미지 build Runner와 production deploy Runner는 같은 Docker daemon을 사용해야 한다. 이미지 Registry는 사용하지 않는다.

## Runner 분리

### `container-build` Runner

- 이미지 build 전용
- 운영 서버의 Docker socket 사용
- `container-build` tag 사용
- commit SHA 이미지 태그를 같은 Docker daemon에 생성

### `production-docker` Runner

- 같은 운영 서버의 Docker daemon을 사용하는 Docker executor
- `production-docker` tag 사용
- protected runner 및 protected branch 전용
- 이 프로젝트에 lock
- 일반 merge request pipeline에서 실행되지 않도록 제한
- `deploy-production`, `rollback-production` job만 실행

두 Runner가 다른 Docker host를 사용하면 로컬 이미지가 공유되지 않으므로 배포가 실패한다. 다른 host가 필요해지는 시점에 외부 private Registry로 전환한다.

Docker 권한은 사실상 호스트 root 권한이므로 배포 Runner에 신뢰하지 않는 코드를 실행시키지 않는다.

## GitLab pipeline 단계

1. `verify`
   - TypeScript typecheck
   - web test/build
   - JVM test/check
   - contract/content 검증
2. `image`
   - `game-api`
   - `market-worker`
   - `event-consumers`
   - `web`
   - `admin-console`
   - 각 이미지를 `${CI_COMMIT_SHA}` tag로 같은 운영 Docker daemon에 build
3. `deploy`
   - `main` protected branch의 자동 job
   - 같은 운영 Docker daemon을 사용하는 `production-docker` Runner에서 실행
   - 배포 스크립트가 `${CI_COMMIT_SHA}`의 `game-api`, `web` local image 존재를 확인
   - Registry pull 없이 PostgreSQL backup
   - `docker compose up -d --wait --pull never`
   - web/API readiness 및 local smoke
4. `rollback`
   - 이전에 검증한 local image tag를 `ROLLBACK_IMAGE_TAG`로 지정
   - 해당 이미지가 Docker daemon에 있는지 확인 후 Compose 재기동
   - readiness/smoke 재확인

Registry는 사용하지 않는다. `container-build`와 `production-docker` Runner는 같은 Docker daemon을 사용해야 한다.

## 배포 서버 파일

저장소에 secret을 커밋하지 않는다.

```text
/etc/hanjjak/production.env
/etc/hanjjak/secrets/db_password
/etc/hanjjak/secrets/cloudflare_tunnel_token
/var/backups/hanjjak/
```

`production.env` 예시:

```dotenv
PUBLIC_HOST=game.example.com
POSTGRES_DB=hanjjak
POSTGRES_USER=hanjjak
DB_PASSWORD_FILE=/etc/hanjjak/secrets/db_password
TUNNEL_TOKEN_FILE=/etc/hanjjak/secrets/cloudflare_tunnel_token
```

권한:

```sh
sudo install -d -m 700 /etc/hanjjak/secrets /var/backups/hanjjak
sudo chmod 600 /etc/hanjjak/production.env
sudo chmod 600 /etc/hanjjak/secrets/db_password
sudo chmod 600 /etc/hanjjak/secrets/cloudflare_tunnel_token
```

GitLab protected variable:

```text
DEPLOY_ENV_FILE=/etc/hanjjak/production.env
BACKUP_DIR=/var/backups/hanjjak
```

## 배포 서버 사전 확인

```sh
docker version
docker compose version
sudo -u gitlab-runner docker info
sudo -u gitlab-runner docker compose version
```

저장소의 운영 Compose 검증:

```sh
docker compose \
  --env-file /etc/hanjjak/production.env \
  -f infra/deploy/compose.yaml \
  --profile tunnel \
  config --quiet
```

초기 배포는 수동으로 한 번 검증한다.

```sh
export DEPLOY_DIR=/path/to/project/infra/deploy
export DEPLOY_ENV_FILE=/etc/hanjjak/production.env
export BACKUP_DIR=/var/backups/hanjjak
export IMAGE_TAG=<검증된-commit-sha>

./infra/deploy/deploy.sh
```

## Cloudflare Tunnel

1. 대화에 노출된 기존 Tunnel token을 Cloudflare Dashboard에서 폐기한다.
2. 새 remotely-managed Tunnel token을 발급한다.
3. 새 token을 서버의 `/etc/hanjjak/secrets/cloudflare_tunnel_token`에만 저장한다.
4. Published application route를 다음처럼 지정한다.

```text
game.example.com -> http://web:8080
```

5. Cloudflare DNS를 Tunnel hostname에 연결한다.
6. Cloudflare WAF/cache에서 HTML, 세션, API 응답을 캐시하지 않고 hashed static asset만 캐시한다.
7. Tunnel connector 상태가 `Healthy`인지 확인한다.

## 배포 구성

- `infra/deploy/compose.yaml`
  - `postgres`
  - `game-api`
  - `web`
  - 선택적 `cloudflared`
- `infra/deploy/deploy.sh`
  - local `game-api`·`web` image 존재 확인
  - Compose config
  - Registry pull 없이 PostgreSQL backup
  - `docker compose up -d --wait --pull never`
  - readiness/smoke
- `infra/deploy/backup-postgres.sh`
  - PostgreSQL custom-format dump
- `infra/deploy/restore-postgres.sh`
  - `RESTORE_CONFIRM=YES` 명시 필요
- `infra/deploy/rollback.sh`
  - 이전 local image tag 기반 rollback

## 운영 검증

### CI/서버

- 모든 Dockerfile이 clean checkout에서 build된다.
- `${CI_COMMIT_SHA}` image가 운영 Docker daemon에 존재한다.
- `container-build`와 `production-docker` Runner가 같은 Docker daemon을 사용한다.
- `docker compose config --quiet`가 통과한다.
- PostgreSQL → game-api → web → cloudflared 순서로 healthy가 된다.
- 배포 전 backup 파일이 외부 backup 경로에 생성된다.
- 컨테이너 재기동 후 계정·진행 데이터가 유지된다.

### 실제 hostname

- 신규 계정 생성
- 로그인·로그아웃
- 새로고침 후 세션 유지
- 올바른 Origin mutation 성공
- 잘못된 Origin mutation `403`
- `/api/v1/...` 동일 origin 호출
- `/actuator/*` 외부 차단
- PostgreSQL 외부 차단
- Tunnel 재연결
- rollback 및 restore drill
- A-04 전체 인수 시나리오

## 남은 작업

- GitLab protected variables 등록
- 노출된 Cloudflare Tunnel token 폐기·재발급
- 실제 hostname DNS 및 Published application route 구성
- PostgreSQL backup schedule·보존 기간·외부 저장소 확정
- 실제 운영 환경에서 backup/restore/rollback drill
- J-08/J-09/J-10 증거 링크 갱신

## 주의

- `latest` tag를 운영 배포 입력으로 사용하지 않는다.
- image는 commit SHA 또는 digest로 배포한다.
- `container-build`와 `production-docker` Runner는 같은 Docker daemon을 사용해야 한다.
- DB migration은 이전 애플리케이션 image와 호환되는 expand/contract 방식으로 작성한다.
- 실제 운영 backup/restore 및 Cloudflare hostname 인수 전에는 배포 완료로 표시하지 않는다.
