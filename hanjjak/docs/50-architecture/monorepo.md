# 모노레포 구조 설계

---
doc_kind: ssot
owner_domain: architecture
authority_level: applied
---

구현 코드베이스의 저장소 배치, 도구 경계와 의존성 규칙을 정의한다. 게임 규칙과 콘텐츠 수치는 소유하지 않는다.

~~~text
/
├─ apps/
│  ├─ web/                   # React·Vite 게임 클라이언트
│  ├─ game-api/              # Kotlin·Spring Boot 모듈형 모놀리스
│  ├─ balance-lab/           # sim-core 기반 Kotlin CLI, 웹 UI는 후속
│  ├─ admin-console/         # 별도 React·Vite 운영 UI
│  ├─ market-worker/         # Kotlin·Spring Boot 시장 처리 분리 경계
│  └─ event-consumers/       # Kotlin·Spring Boot 통계·알림 소비 경계
├─ packages/
│  ├─ contracts/             # TypeSpec 원본, OpenAPI·JSON Schema 산출물
│  ├─ game-content/          # 버전이 있는 정적 게임 콘텐츠
│  ├─ sim-core/              # 서버 판정 정본 Kotlin 결정론 코어
│  ├─ client-sdk/            # 빌드 시 생성하는 TypeScript API client
│  ├─ ui/                    # 디자인 토큰·접근성·UI primitive
│  └─ localization/          # 문구·오류 코드·다국어 자원
├─ infra/
│  ├─ local/                 # Docker Compose 로컬 의존 서비스
│  ├─ database/              # 데이터베이스 공통 운영 구성
│  ├─ broker/                # Outbox 이후 broker 도입 지점
│  └─ observability/         # OpenTelemetry·구조화 로그 구성
├─ tools/
│  ├─ content-validator/
│  ├─ codegen/
│  └─ fixtures/
└─ docs/                     # 게임 중심 문서 계층
~~~

## 도구 기준

- TypeScript workspace는 pnpm workspace를 사용한다. `web`과 `admin-console`은 React·Vite·TypeScript, 단위·컴포넌트 검증은 Vitest·Testing Library를 사용한다.
- JVM workspace는 Gradle Kotlin DSL 멀티모듈을 사용한다. `game-api`, `market-worker`, `event-consumers`는 Kotlin·Spring Boot, `balance-lab`과 `sim-core`는 Kotlin을 사용한다.
- JVM 기본 namespace는 `com.hanjjak`이다.
- Node와 JDK는 LTS를 사용하고 실제 버전은 lockfile, toolchain, Gradle wrapper로 고정한다. 라이브러리는 채택 시점의 안정 버전을 고정한다.

## 의존성 원칙

- `contracts`의 TypeSpec이 외부 API와 콘텐츠 schema 정의의 유일한 편집 원본이다. 생성 흐름은 [Content Pipeline](./content-pipeline.md)이 소유한다.
- `game-content`가 챕터·스테이지·드롭·장비·스킬의 버전형 정적 콘텐츠를 소유한다.
- Kotlin `sim-core`가 서버 판정 계산의 정본이며 UI·HTTP·거래 DB를 참조하지 않는다. TypeScript 예측기는 이를 import하지 않고 golden replay로 동등성을 검증한다.
- `web`과 `admin-console`은 생성된 client를 통해 API를 호출하며 서버 DB schema나 생성된 서버 DTO를 직접 참조하지 않는다.
- JVM 공통 코드는 계약 adapter, 관측성, 테스트 지원으로 제한한다. 도메인 모델을 worker·consumer와 공유하는 공통 패키지를 만들지 않는다.
- `game-api`가 계정, 진행, 거래 권한과 정산을 소유한다. `market-worker`와 `event-consumers`는 해당 내부 모델이나 다른 모듈 테이블을 직접 소유하지 않는다.
- 모든 앱은 첫 골격에서 실행 가능한 health/readiness, 공통 관측성, container build를 갖추되 가짜 비즈니스 처리와 placeholder consumer를 만들지 않는다.
- Kafka는 시장 체결의 정합성 도구가 아니다. PostgreSQL Outbox 이후 다중 소비 필요가 확인될 때 도입한다.

## 프론트엔드 경계

- 앱 내부는 기능 단위 수직 슬라이스로 구성하고 각 feature가 `api`, `model`, `ui`를 소유한다. `app`과 `shared`에는 횡단 책임만 둔다.
- TanStack Query가 서버 상태와 read cache를, Zustand가 전투·로컬 런타임 상태를 소유한다. 일시적인 화면·폼 상태는 가장 가까운 컴포넌트가 소유한다.
- `packages/ui`는 디자인 토큰, 접근성 패턴과 primitive만 제공한다. 게임·운영 도메인 컴포넌트는 각 앱이 소유한다.
- `admin-console`은 `web`의 route가 아니라 별도 빌드·배포·권한 경계를 가진다.
