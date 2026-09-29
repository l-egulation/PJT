---
doc_kind: ssot
owner_domain: architecture
authority_level: applied
---

# Server / Online Backend

서버는 계정 인증, 스테이지 진행, 거래 가능한 자산, 거래소 정산과 권한 검증을 담당하는 초기 런타임 경계다. 스테이지 클리어·챕터 해금·직접 접근은 서버가 현재 진행과 콘텐츠 버전으로 다시 검증한다. 저장 권한과 경계는 [Persistence](./persistence.md), 진행 규칙은 [Stage Domain](../30-domain/world/ssot.md), 거래 규칙은 [Economy Domain](../30-domain/economy/ssot.md)을 따른다.

초기 서버 배포 단위와 이후 분리 후보는 [Operations SSOT](./operations.md)가 소유한다. 이벤트 전달과 Outbox는 [Event System SSOT](../40-systems/event-system/ssot.md)를 참조한다.

## 구현 기반

- 서버 언어와 framework는 Kotlin·Spring Boot, 빌드는 Gradle Kotlin DSL 멀티모듈, 기본 namespace는 `com.hanjjak`이다.
- `game-api`는 초기 모듈형 모놀리스다. 계정, 진행, 전투, 인벤토리, 지갑, 우편, 거래소, 장비, 스킬, 보석, 치장, 콘텐츠, Outbox 책임을 도메인 모듈로 나눈다.
- 거래소 1차 구현의 서버 경계는 `modules:inventory`, `modules:wallet`, `modules:mail`, `modules:market`이다. `market`은 다른 모듈의 application port만 호출하고 infrastructure·table에 직접 접근하지 않는다.
- 각 도메인 모듈은 `api`, `application`, `domain`, `infrastructure` 경계를 사용한다. API adapter는 application use case만 호출하고 domain은 Spring, HTTP, DB 구현에 의존하지 않는다.
- 모듈 간 순환 의존, 다른 모듈 infrastructure·table 직접 접근, API DTO를 domain model로 사용하는 방식을 금지한다.
- Gradle module이 컴파일 의존 경계를 만들고 ArchUnit이 계층과 금지 의존성을 검증한다.
- 외부 HTTP adapter는 TypeSpec에서 생성한 Spring API interface와 DTO를 구현한다. 같은 계약을 수작업 controller·DTO로 중복 정의하지 않는다.

## 저장과 migration

- PostgreSQL 접근은 Spring Data JDBC를 사용한다. 거래·잠금·Flyway 동작을 H2 호환 모드로 대체 검증하지 않는다.
- Flyway migration은 해당 도메인 모듈이 소유하고 `game-api` 기동 시 하나의 전체 순서로 조합해 적용한다. worker와 consumer는 schema 변경을 실행하지 않는다.
- PostgreSQL integration test는 Testcontainers를 사용한다. 트랜잭션, 제약, 잠금과 migration을 실제 PostgreSQL에서 검증한다.
- 트랜잭션 경계는 application use case가 소유하며 세부 원자성·재시도 규칙은 [Persistence](./persistence.md)를 따른다.

## 후속 실행 앱

`market-worker`와 `event-consumers`는 Kotlin·Spring Boot 독립 실행 앱으로 첫 골격에 포함한다. 계약 adapter, OpenTelemetry·구조화 로그, 테스트 지원 외의 공통 JVM 도메인 모델을 공유하지 않는다. 실제 처리 책임은 Outbox 이후 분리 근거와 작업 범위가 생길 때 추가하며 가짜 처리나 placeholder consumer를 만들지 않는다.
