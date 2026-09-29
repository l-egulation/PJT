---
doc_kind: plan
owner_domain: deployment-ops
authority_level: implementation-guidance
status: working
updated_at: 2026-09-11
related_task: J-08
---

# JVM verify 추가 단축 CONTEXT

> 역할: 이미 적용된 JVM verify 개선, 실측 병목, 다음 작업의 순서와 검증 기준을 보존한다. 운영 정책은 [배포·운영 SSOT](../../50-architecture/operations.md), 실행 상태와 증거는 [J-08 작업 문서](../../wiki/06-delivery/tasks/J-delivery-operations/j-08-production-build-cicd.md)가 소유한다.

## 목적

후속 작업자가 기존 조사와 실패한 시도를 반복하지 않고 GitLab JVM verify의 critical path를 더 줄일 수 있게 한다. 이 문서는 목표 시간이나 완료 상태를 확정하지 않는다. 수치는 관측 결과와 추정치를 구분한다.

## 현재 적용 상태

`perf/ci-jvm-verify` 브랜치에는 다음 개선이 적용되어 있다.

- 애플리케이션 scheduler와 Spring Session JDBC cleanup을 통합 테스트에서 비활성화한다.
- Testcontainers와 결합한 Spring 컨텍스트를 클래스 종료 시 닫아 종료된 PostgreSQL 재접속을 막는다.
- CI는 digest가 고정된 PostgreSQL `17.11-alpine` service 하나를 사용한다.
- `PostgresTestDatabase`가 테스트 클래스별 독립 schema를 할당한다.
- 로컬에서는 외부 테스트 DB가 없을 때 test JVM당 PostgreSQL Testcontainer 하나를 fallback으로 시작한다.
- `apps/game-api:test`는 CI에서 test worker 두 개를 사용한다.
- Embedded Kafka는 application topic과 internal topic을 각각 단일 partition으로 실행한다.
- Gradle wrapper, dependency, local build cache, configuration cache 경로를 GitLab cache 대상으로 선언한다.
- Node verify도 pnpm store를 lockfile key로 캐시하고 변경 경로에 따라 실행한다.

구현 및 변경 경로:

- [GitLab CI](../../../.gitlab-ci.yml)
- [game-api Gradle 설정](../../../apps/game-api/build.gradle.kts)
- [공유 PostgreSQL 테스트 연결](../../../apps/game-api/src/test/kotlin/com/hanjjak/gameapi/PostgresTestDatabase.kt)
- [조건부 scheduler 구성](../../../apps/game-api/src/main/kotlin/com/hanjjak/gameapi/SchedulingConfiguration.kt)
- [테스트 환경 설정](../../../apps/game-api/src/test/resources/application.properties)
- [Embedded Kafka 통합 테스트](../../../apps/event-consumers/src/test/kotlin/com/hanjjak/eventconsumers/MarketPriceSnapshotConsumerKafkaTest.kt)

## GitLab 실측 기준선

| 단계 | Pipeline / job | JVM job | Gradle `check` | 관측 |
| --- | --- | ---: | ---: | --- |
| 변경 전 | `#189700` / `#517575` | 351.2초 | 5분 41초 | 클래스별 PostgreSQL, scheduler 종료 오류 |
| scheduler 수명주기 개선 | `#190071` / `#518844` | 324.1초 | 5분 3초 | cache miss, 종료 DB 오류 제거 |
| 공유 PostgreSQL service | `#190180` / `#519216` | 185.9초 | 2분 39초 | Testcontainers 시작 0회 |
| 최종 검증 | `#190212` / `#519334` | 183.9초 | 2분 38초 | contracts/content 27.7초, web 25.8초 |
| worker context 최초 pipeline | `#190443` / `#520216` | 220.6초 | 3분 13초 | cache miss, runner concurrent-0 |
| 동일 job retry | `#190443` / `#520231` | 175.9초 | 2분 33초 | cache miss, runner concurrent-0 |
| MinIO seed pipeline | `#190476` / `#520354` | 198.7초 | 2분 23초 | 418MiB JVM cache 업로드, runner concurrent-2 |
| MinIO warm pipeline | `#190477` / `#520357` | 115.0초 | 38초 | cache 복원, 57 `FROM-CACHE`, runner concurrent-2 |
| MinIO concurrent slot A | `#190489` / `#520428` | 110.8초 | 미분리 | cache 복원, 57 `FROM-CACHE`, concurrent-2 |
| MinIO concurrent slot B | `#190488` / `#520431` | 99.2초 | 미분리 | cache 복원, 57 `FROM-CACHE`, concurrent-1 |
| config cache 제외 1차 | `#190497` / `#520481` | 86.5초 | 37초 | 57 `FROM-CACHE`, archive 417MiB |
| config cache 제외 2차 | `#190500` / `#520491` | 86.9초 | 37초 | 57 `FROM-CACHE`, archive 417MiB |
| split cache seed | `#190508` | 226초 critical path | 미분리 | unit 196.1초, PostgreSQL 225.7초, Kafka 155.3초 |
| split warm | `#190512` | 119초 pipeline | 미분리 | unit 100.4초, PostgreSQL 105.8초, Kafka 90.4초 |
| single gate 복귀 | `#190516` / `#520574` | 89.8초 | 미분리 | 전체 82 task gate 성공 |
| wrapper cache 제외 1차 | `#190527` / `#520620` | 83.8초 | 미분리 | archive 286MiB, 57 `FROM-CACHE` |
| wrapper cache 제외 2차 | `#190529` / `#520624` | 83.4초 | 45초 | archive 286MiB, wrapper 직접 다운로드 |

기존 최종 기준 `#190212`와 worker context 최초 pipeline은 모두 cache miss였고 각각 183.9초와 220.6초로, 최초 실행은 36.8초(20.0%) 느렸다. 동일 commit retry는 175.9초로 기존 기준보다 8.0초(4.3%) 빠르고 최초 실행보다 44.7초(20.3%) 빨랐다. 두 새 실행 모두 Gradle wrapper를 다시 다운로드하고 configuration graph를 재계산했으며 `82 actionable tasks: 82 executed`였다. 따라서 44.7초 차이는 cache 개선 증거가 아니라 동일 Runner의 cold 실행 변동으로 해석한다. 실제 branch pipeline과 로그 링크는 [J-08 증거](../../wiki/06-delivery/tasks/J-delivery-operations/j-08-production-build-cicd.md#증거-링크)에 기록되어 있다.

## 2026-09-11 shared cache 적용 결과

현재 호스트 Docker Desktop에 project runner `verify-shared-cache`를 등록하고 MinIO S3-compatible cache backend를 구성했다. runner와 MinIO는 `unless-stopped` container이며 legacy Windows runner `2054`는 pause했다. cache object와 runner authentication token은 저장소에 넣지 않는다.

- seed pipeline `#190476`은 JVM 198.7초, Gradle `check` 2분 23초에 성공하고 418MiB JVM cache를 MinIO에 업로드했다.
- 바로 다음 pipeline `#190477`은 같은 `concurrent-2` slot이지만 shared cache에서 내려받았다. JVM job 115.0초, Gradle `check` 38초, 57 task `FROM-CACHE`였다.
- JVM job은 83.6초(42.1%), Gradle `check`는 105초(73.4%) 감소했다.
- warm job에는 Gradle wrapper 다운로드가 없었다.
- Node 두 job도 pnpm cache object를 공유했다. contracts/content는 35.0초에서 30.8초, web은 34.8초에서 30.4초였다.
- 동시에 시작한 `#190488`·`#190489`는 서로 다른 `concurrent-1`·`concurrent-2` slot에서 같은 MinIO key를 복원했고, 두 JVM job 모두 57 task를 `FROM-CACHE`로 가져와 각각 99.2초·110.8초에 성공했다.

Gradle `8.14.3` 문서는 configuration cache를 CI machine 간 공유할 수 없는 기능으로 명시한다. archive가 복원돼도 새 job은 machine-specific encryption key와 실행 환경이 달라 entry를 재사용하지 못하고 `Calculating task graph`를 출력했다. 안전한 경계는 wrapper·dependency·build cache만 공유하고 `.gradle/configuration-cache`는 GitLab cache에서 제외하는 것이다. 제거 전 warm JVM 115.0초에는 418MiB archive의 23초 download·extract 비용이 포함됐다.

configuration cache 제외 후 연속 pipeline `#190497`·`#190500`은 JVM 86.5초·86.9초, Gradle `check` 37초로 안정적으로 성공했다. cache archive는 418MiB에서 417MiB로 1MiB만 줄어 전송 비용의 주원인이 dependency/build cache임을 확인했다. 첫 측정은 이전 418MiB archive를 내려받은 뒤 417MiB로 교체했고, 두 번째 측정은 417MiB archive를 복원했다. 둘 다 57 task `FROM-CACHE`였으며 configuration cache 공유를 제거해도 Gradle 실행 시간은 유지됐다.

## 2026-09-11 cache archive 구성 분석

단일 JVM cache 417.4MiB의 ZIP compressed 구성은 dependency artifacts 278.3MiB, Gradle wrapper distribution 131.1MiB, build cache 5.9MiB, metadata 0.4MiB였다. wrapper가 archive의 31.4%를 차지하지만 현재 네트워크에서 `gradle-8.14.3-bin.zip` 137.4MB 다운로드는 3.46초였다.

wrapper cache는 매 pipeline에서 131MiB를 MinIO로 내려받고 다시 올리는 반면 직접 다운로드 비용은 작다. dependency와 build cache는 57 task `FROM-CACHE`와 dependency resolution에 직접 기여하므로 유지하고 wrapper path만 제외했다.

연속 pipeline `#190527`·`#190529`는 JVM 83.8초·83.4초로 성공했고 archive는 417MiB에서 286MiB로 31.4% 감소했다. 두 번째 실행은 wrapper를 직접 다운로드하면서도 57 task를 `FROM-CACHE`로 복원했다. restore는 약 22.6초에서 16.0초, archive 저장은 약 12.9초에서 9.1초로 줄었다. Gradle 실행은 wrapper 다운로드 때문에 37초에서 45초로 늘었지만 전체 job은 86.5~86.9초에서 83초대로 3~4% 단축돼 제외 설정을 유지한다.

## 2026-09-11 현재 호스트 재측정과 적용 결과

기존 GitLab 수치는 다른 호스트의 관측으로 보존한다. 현재 Windows 11·Ryzen 7 5700X 호스트에서는 Docker PostgreSQL `17.11-alpine`, `CI=true`, 외부 `TEST_DATABASE_URL`, Gradle build/configuration cache 비활성 조건으로 다시 측정했다.

| 단계 | 명령 | Gradle `check` | `apps/game-api:test` task | 관측 |
| --- | --- | ---: | ---: | --- |
| 적용 전 cold | `./gradlew --no-daemon --no-build-cache --no-configuration-cache clean check --profile` | 8분 14.27초 | 5분 0.58초 | 103 task 중 82 executed, game-api 93개 통과 |
| worker context 적용 후 cold | 동일 명령 | 5분 10.85초 | 2분 47.36초 | 103 task 모두 executed, game-api 93개 통과 |

현재 호스트 cold `check`는 493.27초에서 310.85초로 182.42초(37.0%) 감소했고, `apps/game-api:test` task는 300.58초에서 167.36초로 133.22초(44.3%) 감소했다. profile HTML은 생성물이라 저장소에는 포함하지 않는다.

적용 내용:

- 공통 `@GameApiIntegrationTest`가 Spring Boot context와 test worker schema를 공유한다.
- 12개 Spring 통합 테스트 클래스의 클래스별 `@DynamicPropertySource`, `@DirtiesContext`, 중복 test configuration을 제거했다.
- class 시작 시 migration history와 admin bootstrap identity를 제외한 application table을 `TRUNCATE ... RESTART IDENTITY CASCADE`한다.
- mutable battle stats, password-reset mail, skill roll fake를 각 test method 전후에 초기화한다.
- 공유 context 때문에 테스트 간 누적되던 인증·mutation rate-limit은 test property에서 충분히 큰 한도로 격리했다.

별도 migration 원자성 테스트는 각 테스트마다 중간 migration 상태를 구성해야 하므로 기존 독립 schema를 유지한다. 따라서 전체 game-api suite의 Flyway 실행 횟수가 worker 수와 정확히 같아지는 것은 이 범위의 완료 조건이 아니다.

## 남은 병목

### 1. Runner shared cache — 적용

Docker executor의 slot별 local volume만으로는 cross-slot 공유가 되지 않아 MinIO S3-compatible backend를 적용했다. `#190476`에서 cache를 seed하고 `#190477`에서 동일 key를 복원해 JVM 57 task와 pnpm store를 재사용했다.

현재 backend는 이 workstation의 Docker Desktop과 로컬 disk에 있으므로 workstation·Docker Desktop이 중지되면 verify pipeline도 멈춘다. cache는 재생성 가능한 데이터지만 runner 가용성은 별도 운영 위험이다. runner token·MinIO credential은 저장소에 기록하지 않는다.

### 2. Spring Boot context와 Flyway 클래스별 반복 — 공통 context 적용

Spring 기반 통합 테스트 12개 클래스는 이제 `org.gradle.test.worker`별 schema와 Spring Boot context를 공유한다. 첫 클래스에서 Flyway를 한 번 적용하고 이후 클래스는 application table 초기화와 mutable fake reset으로 격리한다.

### 3. JVM job 분리 — 검증 후 롤백

`jvm-unit`, `jvm-postgres-integration`, `jvm-kafka-integration` 세 gate를 구현하고 seed·warm pipeline으로 검증했다. 각 job의 독립 JUnit·HTML report와 `deploy-production.needs` 연결도 확인했다.

seed pipeline `#190508`은 unit 196.1초, PostgreSQL 225.7초, Kafka 155.3초였다. warm pipeline `#190512`는 unit 100.4초, PostgreSQL 105.8초, Kafka 90.4초로 pipeline 119초였다. 단일 warm pipeline 86.5~86.9초보다 약 32초(36.9%) 느렸다.

partition archive가 unit 281MiB, PostgreSQL 298MiB, Kafka 372MiB로 총 951MiB여서 job setup과 cache 전송 중복이 critical path를 늘렸다. 실패 원인 분리보다 성능 저하가 커 단일 `jvm` gate로 복귀하고 `deploy-production.needs`도 기존 gate로 되돌렸다. 복귀 pipeline `#190516`은 전체 JVM gate를 89.8초에 통과해 단일 job 기준을 재확인했다.

### 4. configuration cache 공유 — 제외

Gradle `8.14.3`은 configuration cache의 개발자·CI machine 간 공유를 지원하지 않는다. MinIO에서 `.gradle/configuration-cache`를 복원한 연속·동시 pipeline 모두 entry를 재사용하지 않고 새 graph를 저장했다. machine-specific encryption key도 기본 동작의 일부다.

따라서 CI shared cache에는 wrapper·dependency·build cache만 유지하고 configuration cache는 제외한다. 같은 workspace에서 반복하는 로컬 Gradle 실행에는 `org.gradle.configuration-cache=true`를 계속 사용한다.

## 검증했지만 채택하지 않은 방법

### KRaft Embedded Kafka

`@EmbeddedKafka(kraft = true)`를 두 번 실행했다. 테스트 자체는 통과했지만 Kafka `3.9.2` 종료 과정에서 매번 metadata snapshot 관련 `NoSuchFileException`과 `KafkaStorageException`이 발생했다. 로그 오류를 숨긴 채 채택하지 않는다. 현재는 ZooKeeper 기반 embedded broker와 단일 partition을 유지한다.

### Pipeline의 `if-not-present` image pull policy

Node, JDK, PostgreSQL image는 digest를 고정했으므로 local image 재사용을 시도했다. 하지만 project Runner의 `allowed_pull_policies`가 `always`만 허용해 세 verify job이 실행 전에 거절됐다. Pipeline 설정은 제거했다. 적용하려면 Runner host의 `config.toml`에서 허용 정책을 변경한 뒤 다시 검증해야 한다.

### `GIT_CLEAN_FLAGS` cache 제외

`.gradle-user-home`, `.gradle`, `.pnpm-store`를 Git clean에서 제외해도 concurrent slot 간 cache file은 공유되지 않았다. repository worktree 보존과 GitLab Runner cache backend는 별도 문제다. 해당 설정은 제거했다.

## 권장 실행 순서

1. dependency artifacts 278MiB 중 실제 runtime에 필요하지 않은 중복을 줄일 수 있는지 조사한다.
2. compile 산출물 전달 DAG는 총 전송량이 단일 job보다 작다는 근거가 있을 때만 재검토한다.
3. 이후에도 configuration이 critical path라면 dependency-management plugin과 task graph를 최적화한다.

## 기대 효과와 불확실성

현재 실측에서 shared cache는 유효하지만 세 job 분리는 단일 warm job보다 느렸다. 이후 조치는 추정 목표가 아니라 pipeline critical path 실측으로만 채택한다.

## 검증 체크리스트

### Runner cache

- 두 번째 pipeline에 Gradle Wrapper 다운로드가 없었다.
- build cache 57 task가 `FROM-CACHE`였다.
- 동시 pipeline의 `concurrent-1`과 `concurrent-2`가 같은 MinIO cache key를 복원했다.
- pnpm store도 shared cache object에서 복원됐다.
- configuration cache는 Gradle의 비공유 제약에 따라 GitLab shared cache에서 제외했다.

### Spring context 통합

- 현재 호스트의 전체 JVM `check`가 통과했다.
- game-api 93개 테스트가 실패·오류·skip 없이 통과했다.
- Spring 기반 통합 테스트는 worker context와 schema를 공유한다. migration 원자성 테스트의 독립 Flyway 실행은 유지한다.
- mutable fake와 application table은 테스트 경계에서 초기화한다.
- scheduler 오류, 종료 DB 접속, Hikari validation 오류가 없다.

### 병렬 job

- 세 JVM job과 report artifact, deploy gate 연결은 `#190508`·`#190512`에서 검증됐다.
- warm split pipeline은 119초로 단일 warm pipeline보다 약 32초 느렸다.
- 성능 회귀 때문에 단일 `jvm` gate로 복귀했다.

## 완료 조건

- Runner shared cache가 pipeline과 서로 다른 concurrent slot을 넘어 실제 적중했다.
- Spring 기반 통합 테스트의 context와 Flyway는 worker당 재사용하고, migration 원자성 테스트만 독립 schema를 유지한다.
- 단일 `jvm` gate 복귀 후 기존 검증 범위와 deploy 선행 조건을 유지한다.
- 실제 GitLab pipeline cold·warm 결과를 J-08 작업 문서에 기록한다.
- 목표 시간은 실측으로만 기록하고 추정치를 완료 증거로 사용하지 않는다.
