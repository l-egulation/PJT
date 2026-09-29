---
doc_kind: task
owner_domain: delivery
task_code: 'B-11'
task_area: 'B 계정·저장'
task_type: '개발'
priority: 'P1'
target_chapters: '공통'
planning_status: '완료'
development_status: '완료'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://lab.ssafy.com/s15-bigdata-dist-sub1/S15P21B107/-/work_items/9'
snapshot_date: '2026-09-09'
---

# B-11 이메일 인증 기반 비밀번호 재설정 구현

> 작업 상태와 완료 증거의 SSOT. 계정 규칙은 [계정·저장](../../../../30-domain/player/ssot.md), 화면 규칙은 [클라이언트 UX](../../../../30-domain/player/ux/ssot.md)를 따른다.

## 완료 기준

등록 이메일의 일회용 HTTPS 링크로 비밀번호를 변경하고, 계정 존재 여부를 노출하지 않으며, 성공 후 기존 인증·게임 실행 세션을 사용할 수 없다.

## 선행 작업
B-01, B-06, K-04

## 비고

등록 이메일 요청과 링크 확인 API, URL fragment로 전달해 HTTP 요청·접근 로그에서 분리하고 서버에 원문을 저장하지 않는 SHA-256 토큰 해시, 만료·단일 사용, 이메일·IP 요청 제한, 동일 성공 응답, 비밀번호 변경 후 기존 Spring 인증 세션·게임 실행 세션·활성 전투 폐기를 구현했다. 메일 발송은 `PasswordResetMailSender` 포트와 Gmail SMTP STARTTLS 어댑터로 분리하고 공개 HTTPS origin·Gmail 계정·Google 앱 비밀번호·토큰 수명·제한 값을 환경 설정으로 주입한다. 웹 로그인 화면은 비밀번호 찾기, 이메일 제출, `reset-password#token=...` 재설정 화면과 로그인 복귀를 제공한다.

## 증거 링크

- API·도메인: `modules/account/src/main/kotlin/com/hanjjak/account/application/PasswordResetService.kt`, `modules/account/src/main/kotlin/com/hanjjak/account/api/AuthenticationController.kt`
- 저장·세션: `modules/account/src/main/kotlin/com/hanjjak/account/infrastructure/JdbcPasswordResetTokenRepository.kt`, `modules/account/src/main/kotlin/com/hanjjak/account/api/AccountSessionRegistry.kt`, `modules/battle/src/main/kotlin/com/hanjjak/battle/application/GameSessionService.kt`, `apps/game-api/src/main/resources/db/migration/V32__password_reset_token.sql`
- Gmail·배포 설정: `modules/account/src/main/kotlin/com/hanjjak/account/infrastructure/GmailPasswordResetMailSender.kt`, `apps/game-api/src/main/resources/application.yml`, `infra/deploy/compose.yaml`, `infra/deploy/production.env.example`
- 계약·웹: `packages/contracts/main.tsp`, `packages/contracts/generated/openapi/openapi.yaml`, `apps/web/src/features/auth/api.ts`, `apps/web/src/features/auth/AuthGate.tsx`, `apps/web/src/features/auth/AuthGate.css`
- 단위 검증: `modules/account/src/test/kotlin/com/hanjjak/account/application/PasswordResetServiceTest.kt`, `modules/account/src/test/kotlin/com/hanjjak/account/api/RequestRateLimiterTest.kt`, `apps/web/src/features/auth/api.test.ts`
- PostgreSQL 통합 검증: `apps/game-api/src/test/kotlin/com/hanjjak/gameapi/AccountFlowIntegrationTest.kt`에서 Flyway V32, 토큰 해시, 단일 사용, 새 비밀번호 로그인, 기존 인증·게임 세션 무효화, 존재·미존재 계정 동일 응답을 확인했다.
- 2026-09-09 검증: 비밀번호 재설정 대상 통합 테스트, account 단위 테스트, 웹 테스트·타입 검사·프로덕션 빌드, TypeSpec 생성, Compose 설정 검사를 통과했다. 브라우저 1280×720에서 비밀번호 찾기와 재설정 화면의 입력·복귀 동작과 배치를 확인했다.
- 실제 Gmail 수신함 전달은 운영 Gmail 계정과 Google 앱 비밀번호가 저장소에 없어 미검증이다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 완료 |
| 검증 | 부분 검증 |
