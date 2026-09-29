---
doc_kind: task
owner_domain: delivery
task_code: 'B-01'
task_area: 'B 계정·저장'
task_type: '개발'
priority: 'P0'
target_chapters: '공통'
planning_status: '미확인'
development_status: '완료'
verification_status: '완료'
deadline: ''
source_url: 'https://app.notion.com/p/58706c8781b083b28a7f0165a4e0c9a5'
notion_id: '58706c87-81b0-83b2-8a7f-0165a4e0c9a5'
snapshot_date: '2026-08-28'
---

# B-01 회원가입·로그인 흐름 구현

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [계정·저장](../../../../30-domain/player/ssot.md)를 따른다.

## 완료 기준

신규 사용자가 계정을 만들고 재로그인해 같은 사용자 데이터를 불러온다.

## 선행 작업

A-02

## 비고

서버 회원가입·로그인·현재 세션·로그아웃, BCrypt 비밀번호 저장, JDBC 계정·캐릭터 저장과 Spring Session을 구현했다. 새 회원가입과 로그인은 세션 ID를 교체하고 계정별 활성 세션을 PostgreSQL에 저장하며, 다른 기기에서 새로 로그인하면 기존 세션을 무효화한다. 로그아웃·자연 만료 시 활성 세션 연결도 정리하고 익명 조회는 빈 서버 세션을 만들지 않는다. 모든 API 변경 요청은 허용된 Origin 또는 동일 Origin만 받고, 인증 시도는 계정·IP, 기타 변경 요청은 계정·IP 기준으로 제한한다. 이메일은 서비스 계층에서도 공백 제거·소문자화·형식 검증하며 동시 중복 가입을 도메인 오류로 수렴시킨다. 인증 command는 PostgreSQL 트랜잭션 advisory lock으로 같은 멱등성 키의 동시 실행을 직렬화하고, 비밀번호가 포함된 요청 fingerprint는 BCrypt로 추가 보호해 저장한다. 제한 값과 허용 Origin은 배포 설정으로 변경할 수 있다. 웹은 AuthGate에서 세션 확인, 닉네임 가입, 로그인, 로그아웃과 인증 후 게임 진입 흐름을 연결한다.

## 증거 링크

- 구현: `modules/account`, `apps/game-api/src/main/resources/db/migration/V1__account_and_progress.sql`, `V2__spring_sessions.sql`
- 단위 검증: `modules/account/src/test/kotlin/com/hanjjak/account/application/AuthenticationServiceTest.kt`
- 2026-09-02 `./gradlew.bat test` 통과.
- 2026-09-03 웹 인증 흐름: `apps/web/src/features/auth/api.ts`, `apps/web/src/features/auth/AuthGate.tsx`, `apps/web/src/main.tsx`
- 2026-09-03 검증: `corepack pnpm --filter @hanjjak/web test`, `corepack pnpm --filter @hanjjak/web typecheck`, `corepack pnpm --filter @hanjjak/web build`, `./gradlew.bat :modules:account:test`, 브라우저 스모크에서 미인증 세션 → 로그인 폼 → 로그인 성공 → 인벤토리 진입 확인.
- 2026-09-03 로컬 PostgreSQL QA: `DB_URL=jdbc:postgresql://localhost:5432/hanjjak_auth_login`에서 회원가입(`qa-1788395786759@example.com`) → 주력 재료 선택 → 인벤토리 진입 → 새로고침 세션 유지 → 로그아웃 → 같은 계정 재로그인 확인.
- 2026-09-03 사용자 수동 QA: 브라우저에서 회원가입과 주력 재료 선택 성공 확인.
- 2026-09-03 닉네임 가입·재로그인 QA: `apps/game-api/src/main/resources/db/migration/V9__character_nickname.sql`, `packages/contracts/main.tsp`, `packages/contracts/generated/openapi/openapi.yaml`, `apps/web/src/features/auth`에 닉네임 필드 반영. `DB_URL=jdbc:postgresql://localhost:5432/hanjjak_auth_login`에서 회원가입(`nick-1788397659324@example.com`, 닉네임 `김한짝`) → 주력 재료 선택 → 로그아웃 → 재로그인 뒤 인증 상단 닉네임 유지 확인.
- 2026-09-03 마이페이지 닉네임 수정 QA: `POST /api/v1/auth/profile/nickname`, `apps/web/src/features/profile/ProfileScreen.tsx` 반영. `profile-1788398305446@example.com` 계정에서 닉네임을 `최종마이한짝`으로 수정하고 로그아웃·재로그인 뒤 유지 확인.
- 2026-09-03 거래소 백엔드 병합 후 재검증: `feature/auth-login-flow`에 `origin/main`을 병합하고 닉네임 migration을 `V9__character_nickname.sql`로 조정. `./gradlew.bat test`, `corepack pnpm --filter @hanjjak/contracts build`, `corepack pnpm --filter @hanjjak/web test`, `corepack pnpm --filter @hanjjak/web typecheck`, `corepack pnpm --filter @hanjjak/web build` 통과. fresh DB `hanjjak_auth_after_market`에서 회원가입→주력 재료 선택→거래소 재료 조회→닉네임 수정→로그아웃→재로그인 API 스모크와 브라우저 회원가입→주력 재료 선택→마이페이지 닉네임 수정 스모크 확인.
- 2026-09-06 인증 UI 교체 후 회귀 검증: 기존 `authApi.login`·`authApi.signup` 호출과 입력 계약을 유지한 채 `AuthGate.tsx`의 시각 계층과 비밀번호 표시 상태를 변경. `corepack pnpm --filter @hanjjak/web test` 19개, `typecheck`, `build` 통과. game-api 미실행으로 새 화면에서의 실제 제출 성공 흐름은 이번 스모크에서 재검증하지 않음.
- 2026-09-07 인증 보안 구현: `AccountSessionRegistry.kt`, `ApiRequestGuardFilter.kt`, `ApiSecurityProperties.kt`, `RequestRateLimiter.kt`, `SecurityConfiguration.kt`, `V27__account_active_session.sql`, `application.yml`. 최신 `main` 병합 과정에서 최신 main의 V23~V26 마이그레이션과의 번호 충돌을 피하도록 활성 세션 마이그레이션을 V27로 재번호화했다.
- 2026-09-07 단위 검증: `RequestRateLimiterTest.kt`, `ApiRequestGuardFilterTest.kt` 통과. 계정·IP 제한, 제한 창 초기화, Origin 차단, 동일/허용 Origin, 일반 변경 요청 429, 교체된 세션과 30일이 지난 세션의 무효화를 확인.
- 2026-09-07 PostgreSQL·game-api 실행 스모크: 활성 세션 migration 적용 후 회원가입·세션 확인·재로그인·기존 세션 무효화·로그아웃을 확인. Origin 누락과 비허용 Origin은 403, 인증 제한 초과는 `429 LOGIN_FAILED`, `retryable: true`, `Retry-After: 60`을 확인. 세션 쿠키는 30일 절대 수명과 `HttpOnly`, `SameSite=Lax`를 확인.
- 2026-09-07 전체 회귀 검증: `./gradlew.bat test` 통과(55개 Gradle task, 실패 없음).
- 2026-09-07 인증 완결성 보강: 본문·멱등성 헤더 검증 실패를 공통 오류 envelope로 통일하고, 이메일 정규화·형식 검증과 중복 키 수렴, 인증 command 동시 실행 잠금, BCrypt 보호 fingerprint, 익명 조회의 불필요한 세션 생성 방지를 반영했다. `:modules:account:test` 25개와 웹 22개 테스트·타입 검사·TypeSpec 빌드, 전체 `./gradlew.bat check`가 통과했다.
- PostgreSQL 전체 흐름·동시 재시도 검증은 `apps/game-api/src/test/kotlin/com/hanjjak/gameapi/AccountFlowIntegrationTest.kt`에 추가했다. 로컬 Docker 서비스가 시작되지 않아 4개 통합 테스트는 자동 건너뛰었고 Docker-in-Docker를 제공하는 GitLab JVM job에서 실행된다.
- 2026-09-08 최신 `main` 통합 검증: API 변경 요청 테스트를 운영 기본 허용 Origin(`http://localhost:5173`) 계약에 맞추고 중복 Origin 필터를 제거했다. `./gradlew.bat :apps:game-api:test` 21개 PostgreSQL 통합 테스트가 통과했다.
- 2026-09-09 계정 탈퇴: `POST /api/v1/auth/delete`를 추가해 현재 세션 계정을 익명화하고 즉시 세션·게임 세션을 종료한다. `deleted_at` soft delete로 기존 행을 보존하며 원래 이메일은 재사용 가능하다. 설정 계정 카드는 기존 장착 치장 이미지와 카탈로그를 사용하고, 이미지가 없으면 닉네임 첫 글자로 fallback한다. 최신 main 통합으로 계정 탈퇴 migration은 랭킹 `V29`와 충돌하지 않도록 `V30__account_soft_delete.sql`로 정리했다.
- 2026-09-09 이메일 인증 기반 비밀번호 재설정은 별도 [B-11 작업](./b-11-password-reset.md)에서 상태·완료 기준·증거를 관리한다.
- 2026-09-09 마이페이지 비밀번호 변경: `POST /api/v1/auth/profile/password`가 현재 비밀번호를 재검증하고 새 비밀번호·확인 값을 검증한 뒤 BCrypt 해시와 `stateVersion`을 원자 갱신한다. 성공 시 로그인 세션을 무효화하고 활성 게임 세션을 `CLOSED`, 진행 중 전투를 `ABORTED`로 정리한다. `ProfileScreen.tsx`에는 현재·새·확인 입력, 브라우저 자동 완성 의미, 연결된 도움말·불일치 오류와 키보드 포커스를 갖춘 폼을 추가했다.
- 2026-09-09 검증: `:modules:account:test` 통과. `AccountStorageIntegrationTest.password change invalidates sessions aborts battle and replaces login credential`에서 비밀번호 변경 후 세션·게임 세션·전투 종료, 기존 비밀번호 로그인 401 실패, 새 비밀번호 로그인 성공과 갱신된 `stateVersion` 유지를 확인했다. TypeSpec build, 웹 34개 파일·98개 테스트, typecheck, production build가 통과했다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 미확인 |
| 개발 | 완료 |
| 검증 | 완료 |
