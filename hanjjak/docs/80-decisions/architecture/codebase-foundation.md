---
doc_kind: decision
owner_domain: architecture
authority_level: confirmed
status: confirmed
decided_at: 2026-09-02
---

# 코드베이스 기반 아키텍처 결정

## 결정

첫 구현 골격은 다음 기준을 사용한다. 현재 적용 기준은 [모노레포 설계](../../50-architecture/monorepo.md), 런타임별 세부 경계는 [Client](../../50-architecture/client.md), [Server](../../50-architecture/server.md), [Simulation](../../50-architecture/simulation.md), 빌드·배포·검증 기준은 [배포·운영 SSOT](../../50-architecture/operations.md)가 소유한다.

- TypeScript 영역은 pnpm workspace, JVM 영역은 Gradle Kotlin DSL 멀티모듈로 구성한다.
- `web`과 `admin-console`은 각각 React·Vite·TypeScript 애플리케이션이다.
- `game-api`, `market-worker`, `event-consumers`는 Kotlin·Spring Boot 애플리케이션이다.
- `balance-lab`은 Kotlin CLI로 시작하고 웹 UI는 실제 필요가 생긴 뒤 추가한다.
- 서버 도메인 모듈은 `api/application/domain/infrastructure` 경계를 사용하고 Gradle 모듈과 ArchUnit으로 의존 방향을 검증한다.
- 서버 저장 접근은 Spring Data JDBC, PostgreSQL 통합 검증은 Testcontainers를 사용한다.
- TypeSpec을 HTTP API와 콘텐츠 schema 정의의 유일한 원본으로 사용한다. OpenAPI와 JSON Schema 산출물은 커밋하고 생성된 클라이언트·서버 interface·DTO는 빌드 산출물로 둔다.
- Kotlin `sim-core`가 판정 정본이며 TypeScript 예측기는 golden replay로 결과 동등성을 검증한다.
- 로컬 의존 서비스는 Docker Compose, 실행 앱 패키징은 container image까지 제공한다. 특정 클라우드와 Kubernetes는 이 결정에 포함하지 않는다.
- GitLab CI는 정적 검사, 단위 테스트, Testcontainers 통합 테스트, 계약·생성물 검사, ArchUnit 경계 검사를 merge gate로 사용한다. 전체 Playwright E2E와 부하 검증은 별도 pipeline으로 둔다.

## 선택 근거

- 서버 권한과 브라우저 예측을 분리하면서도 replay로 계산 드리프트를 검출해야 한다.
- 언어 간 DTO 중복을 피하고 구현이 계약을 임의로 바꾸지 못하게 해야 한다.
- 모듈형 모놀리스의 경계를 코드 리뷰 관례가 아니라 컴파일·자동 검증으로 강제해야 한다.
- 거래 잠금과 Flyway 동작은 PostgreSQL과 다른 대체 DB가 아니라 실제 PostgreSQL로 검증해야 한다.
- 아직 선택하지 않은 클라우드 토폴로지를 초기 코드 구조에 고정하지 않아야 한다.

## 명시적으로 수용한 비용

`admin-console`, `market-worker`, `event-consumers`도 첫 골격에서 실행 가능한 최소 프로젝트와 health/readiness, 공통 관측성, container build를 갖는다. 가짜 비즈니스 처리, placeholder consumer, 미래 API는 만들지 않는다. 이 선택으로 빌드 그래프와 CI 비용은 증가하지만 독립 실행·배포 경계를 초기에 검증할 수 있다.

## 제외·후속 결정

- 특정 클라우드, Kubernetes, 운영 데이터 플랫폼
- Kafka 도입 시점과 broker topology
- `balance-lab` 웹 UI
- 트래픽 근거 이후의 서비스·DB 분리
- working 또는 unresolved 콘텐츠 수치
