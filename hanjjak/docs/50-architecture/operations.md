# 배포·운영 SSOT

---
doc_kind: ssot
owner_domain: deployment-ops
authority_level: applied
---

## 책임

실행 단위, 환경, 빌드·배포·모니터링·백업·롤백 경계를 소유한다. 애플리케이션 도메인 규칙은 소유하지 않는다.

## 현재 확정 기준

- 게임 클라이언트는 Electron·Tauri 같은 데스크톱 셸 없이 React·Vite 웹 애플리케이션으로 빌드·배포한다. `admin-console`은 별도 React·Vite 애플리케이션이다.
- 서버는 Kotlin·Spring Boot 기반 모듈형 모놀리스로 시작한다. `market-worker`와 `event-consumers`는 독립 실행 가능한 경계로 준비하며, 운영에서는 KRaft Kafka 단일 브로커와 `event-consumers` Deployment가 Outbox 발행·시세/경제 통계를 처리한다. Kafka headless Service는 KRaft controller가 자체 StatefulSet DNS 이름으로 bootstrap할 수 있도록 Ready 전에도 Pod 주소를 게시한다. 실제 분리는 장애 격리·부하·다중 소비 근거가 생길 때 적용한다.
- TypeScript는 pnpm workspace, JVM은 Gradle Kotlin DSL 멀티모듈을 사용한다. Node와 JDK는 LTS, wrapper·toolchain·lockfile은 실제 버전을 고정한다.
- 로컬 의존 서비스는 Docker Compose로 제공하고 모든 실행 앱은 재현 가능한 container image를 만든다. 운영 Kubernetes 리소스는 `infra/k8s`가 소유하며 외부 NodePort·LoadBalancer·Ingress 없이 Cloudflare Tunnel만 web ClusterIP Service에 연결한다. `game-api`는 PostgreSQL 5432와 소셜 인증 공급자의 token·JWK·user-info HTTPS endpoint 443만 egress로 허용한다.
- OpenTelemetry와 구조화 JSON 로그를 공통 관측 기준으로 사용한다. 실제 trace·metric·log backend는 배포 환경 결정에서 선택한다.
- 관리자 콘솔 인증은 SSAFY GitLab OAuth 2.0 Authorization Code + PKCE S256을 사용한다. 서버는 `read_user` scope의 access token으로 `GET /api/v4/user`를 호출해 안정 사용자 ID와 `username`을 확인하고 token은 저장하지 않는다. 활성·비잠금 계정이면서 배포 설정의 GitLab `username` 화이트리스트에 포함된 경우만 별도 관리자 세션을 발급한다. 기존 로컬 운영자 ID·비밀번호 로그인은 사용하지 않는다.
- GitLab OAuth의 일회성 `state`는 서버 DB에 SHA-256 해시로 저장하고 10분 안에 한 번만 소비한다. 관리 세션은 `/api/admin` 경로의 HttpOnly 쿠키를 사용하며 운영에서는 Secure를 강제한다. 화이트리스트에서 제거된 사용자의 기존 세션은 다음 요청에서 폐기한다.
- `ADMIN` 역할의 `user:manage` 권한은 사용자 계정의 레벨·누적 경험치, 쌀, 스택 아이템 수량, 보석 인스턴스 수량, 순차 스테이지 해금, 치장 등록·미등록 수량, 부위별 장비 등급·강화 단계를 조정할 수 있다. 모든 조정은 명시적 운영 사유와 관리자별 멱등 키를 요구하고, 거래·장착·판매 예약 상태를 침범하거나 인벤토리 용량·콘텐츠 허용 목록·도메인 상한을 우회하지 않는다. 전투 입력에 영향을 주는 조정은 활성 게임·전투 세션을 종료해 다음 세션부터 일관된 상태를 사용한다.
- 모든 관리자 조회·조작·거절은 GitLab `username`, 관리자 ID, action, 대상, 성공 여부, request ID와 원격 주소를 append-only 감사 기록에 남긴다. 사용자 조작은 추가로 운영 사유, 멱등 키와 변경 전후 서버 상태 요약을 저장하며 관리자 페이지에서 관리자 ID·action·조작 여부로 조회한다. 비밀번호·세션 cookie·OAuth token은 감사 데이터에 포함하지 않는다.
- Outbox, metric, log, backup·rollback을 먼저 설계하고 Kafka는 다중 소비 필요성 이후 도입한다.

## CI·검증 기준

- GitLab CI를 기준 pipeline으로 사용한다.
- merge request 필수 gate는 lint·정적 검사, TypeScript typecheck, 단위·컴포넌트 테스트, Testcontainers PostgreSQL 통합 테스트, TypeSpec·OpenAPI·JSON Schema 생성물 최신 상태와 breaking change 검사, ArchUnit 모듈 경계 검사다.
- 전체 Playwright E2E와 부하 검증은 별도 pipeline에서 실행한다. 핵심 계약을 작은 단위 테스트로 대체하거나 전체 E2E를 모든 merge request에 강제하지 않는다.
- 프레임워크와 라이브러리는 채택 시점 안정 버전을 고정하고 변경은 검토 가능한 dependency update로 반영한다.
- `main` protected branch는 contracts/content, web, JVM verify와 commit SHA image의 K3s containerd 직접 build까지만 자동 수행하고, 운영 배포는 `deploy-production`의 명시적 수동 승인 뒤 실행한다. 배포 Runner는 cluster-admin이 아닌 `hanjjak` namespace Role을 사용한다. 승인된 배포는 PostgreSQL custom-format backup과 별도 migration Job을 먼저 통과한 뒤 game-api `maxUnavailable: 0` rolling update와 readiness smoke를 완료하고, 호환 API와 web이 서비스된 상태에서 resumable progression backfill Job을 끝낸 뒤 배포를 성공 처리한다. backfill이 실패하면 새 API와 이미 전환된 계정의 호환성을 보존한 채 `backfill-production` Job-only retry를 실행한다. retry는 현재 game-api가 같은 `IMAGE_TAG`를 서비스할 때만 허용한다. application rollback도 protected manual job만 허용하며 DB restore를 자동 실행하지 않는다.
- migration Job은 애플리케이션의 `migrate` 진입점으로 Flyway만 실행하고 웹 서버·scheduler를 시작하지 않는다. 배포·rollback·수동 restore는 namespace의 배타 유지보수 잠금을 공유하며 잠금이 이미 있으면 동시 실행을 거부한다. 중단된 작업의 잠금은 진행 중인 작업이 없음을 운영자가 확인한 후에만 해제한다.
- 수동 DB restore는 원본 dump를 보존하고 임시 DB에서 전체 archive 복원을 검증한 뒤 실행한다. backup CronJob을 suspend하고 migration·backup writer 부재 및 앱 Pod 종료를 확인한 후 실제 DB를 교체한다. 성공·실패 후 앱과 backup CronJob을 자동 재개하지 않으며 승인된 명시적 복구 절차를 거친다.
- web blue-green 교체에서 이전 HTML이 요청하는 content-hash asset은 공유 asset PVC에 보존한다. 배포 중 이전 asset을 삭제하지 않는다. 해당 PVC의 용량과 별도 정리 시점은 운영 인수에서 확인하며 DB backup으로 복구되는 데이터로 간주하지 않는다.
- 운영 host에는 web, API, PostgreSQL NodePort·LoadBalancer·Ingress를 만들지 않는다. Cloudflare Tunnel은 outbound connector로 동일 origin의 web Service만 공개한다. 단일 노드 K3s는 rollout 중 가용성만 제공하며 Windows host·VM·node·disk 장애에 대한 고가용성을 제공하지 않는다.

## 배포 서버 로컬 이미지

- image Registry를 운영하지 않는다. protected `container-build` Runner는 K3s와 같은 Ubuntu VM의 BuildKit containerd worker에서 `docker.io/hanjjak/game-api:<commit SHA>`, `docker.io/hanjjak/web:<commit SHA>`, `docker.io/hanjjak/admin-console:<commit SHA>`를 K3s `k8s.io` namespace에 직접 빌드한다. `event-consumers` artifact는 운영 호스트에 설치된 기존 builder 허용 목록과 호환되도록 `docker.io/hanjjak/web:<commit SHA>-event-consumers`에 격리하며, web artifact의 `<commit SHA>` 태그와 충돌하지 않는다.
- `docker save`·`ctr images import` 같은 중간 archive 복사 경로를 사용하지 않는다. `nerdctl build`가 K3s containerd socket과 전용 BuildKit daemon을 사용하고, build job은 최종 image name이 containerd에 존재하는지 확인한다.
- Kubernetes application manifest는 위 이름과 `imagePullPolicy: Never`를 사용한다. source manifest의 `event-consumers` 정규 이름은 구조 기반 renderer가 배포·검증 시 호환 artifact 이름으로 변환하며, deploy와 rollback도 같은 규칙을 사용한다. Kafka·PostgreSQL·cloudflared·curl 같은 고정 지원 이미지는 bootstrap 때 containerd에 확보하며, 새 Pod가 시작되기 전에 해당 이름이 존재해야 한다.
- K3s containerd socket과 image build 권한은 사실상 VM 관리자 권한이다. 이 권한을 가진 Runner는 protected image job 전용으로 제한하고, 비신뢰 MR 검증 job과 실행 경계를 분리한다. `production-k3s` 배포 job에는 namespace kubeconfig만 제공하며 containerd·BuildKit socket 접근은 부여하지 않는다.
- rollback은 containerd에 남아 있는 검증된 commit SHA image만 사용한다. image GC 전에는 현재와 rollback 대상으로 보존할 태그를 확인하며 Docker image cache를 K3s rollback 근거로 간주하지 않는다.

## 운영 게이트

배포 전에는 콘텐츠 버전, 저장 마이그레이션, 거래 정합성, 이벤트 중복, 백그라운드 탭 진행, discard·브라우저 종료·기기 절전·네트워크 단절 중단, PIP 대체 UI, 단일 실행 세션, 장시간 자동전투와 핵심 사용자 흐름을 증거와 함께 확인한다. 실제 운영 완료 여부는 J-08~J-10 작업 문서가 소유한다.

관련 자료: [모노레포 설계](monorepo.md), [DB·Kafka 실험](../90-reference/experiments/db-and-kafka.md)
